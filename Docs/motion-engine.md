# Golden Needle — Motion Engine V1

Status: **PHASE-5 AUTHORITY RECONSTRUCTION IMPLEMENTED / USER QA PENDING. MOTION ENGINE V1 NOT YET USER ACCEPTED. PHASE 6 NOT STARTED.**

Authoritative refresh: 2026-09-16.

## Core invariant: POSE != LOCOMOTION

Phase 4 remains the USER-accepted pose/bone/IK authority. Phase 5 executes after Phase 4 and translates only the bound avatar/player root. The production input remains the stabilized canonical body from Phase 3; no additional inference model is introduced for locomotion.

## Preserved body pipeline

```text
Unity WebCamTexture
-> WebCamCPU/GetPixels32 acquisition
-> reusable 320x240 CPU preparation
-> newest-only bounded two-slot scheduling
-> persistent OpenVINO CPU FP32 worker
-> MediaPipe pose semantics
-> CanonicalBodyV1
-> Phase 3 stabilization/calibration
-> Phase 4 positional/analytic-IK pose
-> Phase 5 root translation
-> presentation
```

OpenVINO/WebCamCPU remains the accepted low-end path. Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths. The optimization milestone remains frozen for the current hackathon milestone.

## Reconstruction lineage

Phase-5 authority was reconstructed after independently inspecting:

- `87698948b12cd10b6fef2072d0ad0ce9eeaecdfe` — continuous torso/body-root prototype;
- `33698719a2907d30bb3396f66e5b79e59ccbfe9e` — support-base correction for lean/swing false translation;
- `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0` — last known good pre-Foundation Phase-5 reference;
- `466d65826ab747493c211e1a3250aafa305167c8` — immediate pre-reconstruction runtime with duplicate horizontal/vertical ownership.

Reconstruction baseline/handoff: `08d7ea5785dd5935ed7240f004313b2a769d7a52`.

Published reconstruction checkpoint: `dc48e9d2f18c65b1631fbdb390e60578087a1303`.

This is not a wholesale revert. Current cadence tuning, swing protection, support-loss continuity, alternating-step completion, jump lifecycle, calibration lifecycle and jump/depth isolation are retained where compatible with a single authority model.

## Horizontal physical locomotion

### One stateful physical-position owner

`CameraSpaceRootTracker` owns:

- physical reference/origin;
- accepted camera-space displacement;
- position/velocity filtering;
- support validation;
- tracking-loss/reacquisition continuity;
- recenter.

`LocomotionFusion` intentionally owns no second committed-position/stationary/rebase state machine.

### Continuous body candidate

Torso/hip/shoulder image geometry produces body center X, yaw-compensated apparent scale and body confidence. Relative center yields lateral candidate; relative apparent scale yields depth candidate.

### Support validation

Each support foot is formed from weighted ankle/heel/toe observations. Support classifies into `Both`, `Left`, `Right` with retained `0.12` single-enter / `0.06` dual-return hysteresis.

Only coherent `Both` support may validate a new room-position commit. Therefore torso lean without support relocation, scale-only depth-like change and a raised swing foot do not move the physical root; coherent body + bilateral support relocation can.

### Commit semantics

Movement evidence is measured relative to the **last accepted physical position**. Small idle fluctuations do not continuously retarget position, while slow deliberate movement accumulates until it crosses a bounded evidence floor.

Lateral body and common-support deltas must agree in sign and each exceed `0.012` normalized evidence. Depth requires matching body-scale and bilateral-support evidence with existing thresholds:

- body-scale depth evidence `0.012`;
- support depth evidence `0.018`;
- foot-differential reliability start/full `0.05 / 0.20`.

Accepted body displacement passes through one tracker filter: position response `10/s`, velocity response `8/s`.

### Tracking loss

Missing required evidence holds the last accepted displacement and publishes zero physical velocity. The first recovered body/support observation is rebased onto the accepted position, preventing a reacquisition teleport; subsequent coherent motion can commit normally.

### Fusion

Fusion now only:

1. applies existing origin deadzones and world scales;
2. maps authoritative camera-space displacement through the accepted Phase-4 reference basis;
3. holds the last mapped contribution during invalidity;
4. suppresses cadence from authoritative physical velocity;
5. adds cadence along accepted body heading.

The later `lateralMovementEnter/release` and `depthMovementEnter/release` committed-position gate is removed.

Current fusion values remain lateral/depth scale `0.9 / 1.5`, origin deadzones `0.012 / 0.012`, physical velocity suppression start/full `0.08 / 0.32`, minimum root confidence `0.30`.

## Cadence

Cadence code and active scene serialization are unchanged:

- event threshold `0.07`;
- acquisition events `2`;
- acquire confidence `0.38`;
- sustain confidence `0.25`;
- stop timeout `0.50 s`;
- step-rate range `0.8–4.5/s`;
- Distance Per Step `0.60`;
- Maximum Cadence Speed `3.0`.

Jogging in place should remain near-zero physical displacement while cadence acquires.

## Recenter

Horizontal recenter preserves virtual world position: save current avatar X/Z, capture the current trusted physical measurement as tracker origin, reset mapped physical contribution, then set virtual origin X/Z to the saved avatar position. Vertical reference/root-Y origin are not redefined.

## Vertical locomotion

### Semantic authority

`VerticalLocomotionInterpreter` remains the only Jump/Crouch semantic state authority: `Unavailable`, `Standing`, `Jump`, `Crouch`, with jump phases `Grounded -> Takeoff -> Airborne -> Landing -> Grounded`.

### Continuous grounded compression

Negative root Y is driven from normalized pelvis-to-support compression:

```text
compression = 1 - currentPelvisSupportHeight / referencePelvisSupportHeight
```

When feet remain coherent, support remains within grounded tolerance and apparent scale remains stable, compression drives negative root Y even before semantic `Crouch` acquisition.

```text
motionDeadband = clamp(0.25 * crouchReleaseThreshold, 0.01, 0.05)
effectiveCompression = max(0, compression - motionDeadband)
rootY = -clamp(effectiveCompression * crouchWorldScale, 0, maximumCrouchDepth)
```

So shallow planted bend can remain `Standing` while lowering the body; deeper bend continues smoothly and may acquire semantic Crouch. Semantic thresholds remain `0.18` enter / `0.09` release. Crouch world mapping remains `1.20` scale / `0.65` max.

`GroundedCrouchFootAnchor` and post-Phase-4 solved-foot rise are removed as root-Y authority. Feet are evidence/constraint, not the primary vertical translation signal.

### Jump

Jump remains coherent whole-body rise from both support feet + pelvis + chest. Current defaults remain enter `0.12`, release `0.045`, world scale `1.60`, max height `0.90`, max foot asymmetry `0.08`, coherence spread `0.10`, apparent-scale guard `0.12`, grounded-support tolerance `0.06`, response `18/s`, tracking grace `0.16 s`.

Jump owns positive root Y exclusively while active. Grounded compression cannot pin takeoff.

The existing controller/fusion depth hold remains: during jump/landing, the copy passed to fusion holds pre-jump depth and zeroes depth velocity; lateral physical movement remains available.

## Controller authority

`EmbodiedLocomotionController` remains execution order 150 after Phase 4.

```text
X/Z = virtualOriginXZ + mapped authoritative physical contribution + cadence integration
Y   = verticalOriginY + verticalSample.worldOffsetY
```

Root rotation is not copied from tracking. Calibration invalidation resets locomotion state and restores driven Y to the saved standing origin.

No CharacterController, Rigidbody gravity, collision probing, autonomous ballistics or Phase-6 gameplay systems are introduced.

## Diagnostics

F9 Lab diagnostics expose body candidate, support mode/validation, support evidence, accepted physical displacement/velocity, per-frame commit state, vertical state/jump phase, grounded-bend flag, compression/support rise/asymmetry/scale change, and current/final Y. No per-frame Console logging is introduced.

## Deterministic verification

Behavior-oriented Editor tests cover idle stability, torso-lean/scale/swing rejection, coherent room relocation, alternating-step completion, transition continuity, slow movement accumulation, loss/reacquisition, recenter, cadence coexistence, depth corroboration, axis/heading semantics, jogging-in-place, shallow continuous negative Y before semantic Crouch, deeper descent through Crouch, planted-crouch X/Z isolation, smooth recovery, jump ownership/landing, single-leg/scale rejection, reset behavior and in-place jump isolation.

The obsolete test suite for the removed fusion stationary gate and post-solve foot-anchor authority is deleted.

## Verification limitation and next boundary

No Unity Editor/Test Runner is available in the Builder environment. Editor tests are authored and statically audited but are **not claimed as executed** without real runner evidence.

Next action is genuine USER Unity webcam QA of the reconstructed Motion Engine. Phase 6, performance work, hands/foundations and `main` merge remain out of scope until that QA is reviewed.
