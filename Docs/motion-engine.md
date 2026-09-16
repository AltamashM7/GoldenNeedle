# Golden Needle — Motion Engine V1

Status: **PHASE-5 AUTHORITY RECONSTRUCTION IMPLEMENTED / USER QA PENDING. MOTION ENGINE V1 NOT YET USER ACCEPTED. PHASE 6 NOT STARTED.**

Authoritative refresh: 2026-09-16.

## Core invariant: POSE != LOCOMOTION

Phase 4 remains the USER-accepted pose/bone/IK authority. Phase 5 executes after Phase 4 and translates only the bound avatar/player root.

The production input remains the stabilized canonical body from Phase 3. Phase 5 introduces no additional inference model.

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

The Phase-5 authority reconstruction was based on direct inspection of:

- `87698948b12cd10b6fef2072d0ad0ce9eeaecdfe` — continuous torso/body-root prototype;
- `33698719a2907d30bb3396f66e5b79e59ccbfe9e` — support-base correction for lean/swing false translation;
- `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0` — last known good pre-Foundation Phase-5 reference;
- `466d65826ab747493c211e1a3250aafa305167c8` — immediate pre-reconstruction runtime with duplicate horizontal/vertical ownership.

Reconstruction baseline: `08d7ea5785dd5935ed7240f004313b2a769d7a52`.

Implementation/tests checkpoint: `753ef564c253c48d6d2c71e9095d1e6e7878e2fe`.

This is not a wholesale revert. Later accepted cadence values, swing protection, support-loss continuity, alternating-step completion, jump lifecycle, calibration lifecycle and jump/depth isolation are retained where compatible with a single authority model.

## Horizontal physical locomotion

### `CameraSpaceRootTracker` is the one stateful position authority

The tracker owns:

- camera-space physical origin/reference;
- accepted physical displacement;
- position/velocity filtering;
- support validation;
- tracking-loss/reacquisition continuity;
- recenter.

The tracker deliberately separates **candidate** from **validation**.

### Continuous body candidate

Torso/hip/shoulder image geometry provides:

- body center X;
- yaw-compensated apparent body scale;
- body confidence.

Relative body center yields the lateral candidate. Relative apparent scale yields the depth candidate. This restores the useful continuous body-root estimate from the early prototype.

### Support validation

Each support foot is formed from weighted ankle/heel/toe image observations. Left/right support displacement is compared with the same feet at the physical reference.

Support classifies into `Both`, `Left` or `Right` with the retained hysteresis:

- single-foot enter `0.12`;
- dual-support return `0.06`.

Only coherent `Both` support may validate a new room-position commit. This keeps the valid correction introduced by the support-base work:

- torso lean without support relocation is rejected;
- torso scale/depth change without support relocation is rejected;
- a raised/moving swing foot cannot translate the root;
- a completed bilateral relocation can validate real movement.

### Commit semantics

The accepted state is measured relative to the **last accepted physical position**, not against every incoming sample through another downstream gate.

For lateral movement, body and common bilateral-support deltas must have matching sign and each exceed the long-standing `0.012` normalized physical evidence floor. Smaller deliberate increments therefore accumulate until a coherent movement can commit.

For depth, body-scale depth and bilateral support-depth evidence must agree in direction. Existing thresholds remain:

- body-scale depth evidence `0.012`;
- support depth evidence `0.018`;
- foot differential reliability start/full `0.05 / 0.20`.

The accepted body candidate then passes through the tracker’s single `10/s` position filter and `8/s` velocity filter.

### Tracking loss

If required evidence is unavailable, the tracker holds the last accepted displacement and publishes zero physical velocity. The first valid measurement after loss is rebased onto that accepted position so reacquisition cannot teleport the character. Subsequent coherent body+support motion can commit normally.

### `LocomotionFusion` is no longer a second position owner

Fusion now only:

1. applies the existing origin deadzones and world scale;
2. maps the authoritative camera-space displacement through the accepted Phase-4 reference basis;
3. holds the last mapped contribution during root invalidity;
4. uses authoritative filtered root velocity to suppress cadence while physical movement is occurring;
5. adds cadence velocity along the accepted body heading.

The later `lateralMovementEnter/release` and `depthMovementEnter/release` committed-position gate and its reacquisition offset are removed. Physical continuity belongs to `CameraSpaceRootTracker` only.

Current world scales/deadzones remain:

- lateral scale `0.9`;
- depth scale `1.5`;
- lateral/depth origin deadzone `0.012`;
- physical velocity suppression start/full `0.08 / 0.32`;
- minimum root confidence `0.30`.

## Cadence

Cadence architecture/code is unchanged by reconstruction.

Current active defaults remain:

- event threshold `0.07`;
- acquisition events `2`;
- acquire confidence `0.38`;
- sustain confidence `0.25`;
- stop timeout `0.50 s`;
- step-rate range `0.8–4.5/s`;
- Distance Per Step `0.60`;
- Maximum Cadence Speed `3.0`.

Jogging in place should produce alternating cadence while physical room displacement remains approximately zero.

## Recenter

Horizontal `Recenter()` preserves virtual world position:

```text
1. save current avatar world X/Z
2. capture current trusted physical measurement as tracker origin
3. root displacement becomes zero
4. set virtual origin X/Z to the saved avatar world X/Z
5. reset fusion's mapped hold only
```

Vertical standing reference/root-Y origin are independent and are not redefined by horizontal recenter.

## Vertical locomotion

### One semantic authority

`VerticalLocomotionInterpreter` remains the only semantic action authority.

States remain:

- `Unavailable`;
- `Standing`;
- `Jump`;
- `Crouch`.

Jump phases remain `Grounded -> Takeoff -> Airborne -> Landing -> Grounded`.

### Standing reference

A calibration-session standing reference is captured from trustworthy bilateral support feet, pelvis, chest, hips and knees. It is not continuously rewritten during actions. Calibration/body-reference loss resets it.

### Continuous grounded body compression

The primary negative root-Y signal is normalized pelvis-to-support compression:

```text
compression = 1 - currentPelvisSupportHeight / referencePelvisSupportHeight
```

If feet remain coherent, support remains within grounded tolerance and apparent scale remains stable, compression continuously drives negative root Y even before semantic `Crouch` acquisition.

A small motion deadband derived from existing crouch-release tuning removes neutral noise:

```text
motionDeadband = clamp(0.25 * crouchReleaseThreshold, 0.01, 0.05)
effectiveCompression = max(0, compression - motionDeadband)
rootY = -clamp(effectiveCompression * crouchWorldScale, 0, maximumCrouchDepth)
```

Therefore:

- upright standing -> approximately zero negative offset;
- shallow planted bend -> proportionally negative root Y while semantic state may still be `Standing`;
- deeper bend -> same continuous root-Y path while semantic `Crouch` crosses its existing threshold;
- rise to standing -> response-filtered recovery toward zero.

Semantic Crouch remains separately controlled by the existing `0.18` enter / `0.09` release thresholds for gameplay/state reporting.

The post-Phase-4 `GroundedCrouchFootAnchor` solved-foot-rise correction is removed from root-Y authority. Feet are evidence/constraint, not the primary vertical translation signal.

### Jump

Jump remains coherent whole-body rise. Acquisition requires bilateral support-foot, pelvis and chest rise with bounded asymmetry/spread and stable apparent scale.

Current jump defaults remain:

- enter `0.12`;
- release `0.045`;
- world scale `1.60`;
- maximum height `0.90`;
- maximum foot asymmetry `0.08`;
- coherence spread `0.10`.

Jump exclusively owns positive root Y while active. Grounded compression is disabled during semantic Jump.

### Jump/depth isolation

The controller retains the narrow cross-talk boundary fix: when jump/landing owns vertical action, the root sample passed to fusion holds pre-jump depth displacement and zeroes depth velocity. Lateral physical movement is not altered.

## Controller authority

`EmbodiedLocomotionController` remains `[DefaultExecutionOrder(150)]`, after Phase 4.

Runtime target is:

```text
X/Z = virtualOriginXZ + mapped authoritative physical contribution
      + integrated cadence contribution
Y   = verticalOriginY + verticalSample.worldOffsetY
```

Root rotation is not copied from pose tracking.

On calibration invalidation, root/cadence/vertical state reset; when locomotion drive is active, Y returns to the saved calibration-session standing origin.

## Diagnostics

F9 Lab diagnostics expose the reconstruction:

- body candidate X/Z;
- support mode and validation;
- support evidence;
- accepted physical X/Z and velocity;
- whether a physical commit occurred on the frame;
- vertical state/jump phase;
- continuous grounded-bend flag;
- compression/support rise/asymmetry/scale delta;
- current Y offset and final root Y.

No per-frame Console logging is introduced.

## Deterministic verification

Behavior-oriented Editor tests cover:

- neutral and nonzero-position idle stability;
- torso lean/sway rejection;
- scale-only depth rejection;
- swing-foot rejection;
- coherent room relocation;
- alternating-step completion and landing continuity;
- slow movement accumulation;
- tracking loss/reacquisition continuity;
- recenter semantics;
- cadence suppression only during authoritative physical velocity;
- corroborated forward/back movement;
- accepted axis/heading mapping;
- jogging-in-place cadence;
- shallow continuous negative Y before semantic Crouch;
- deeper continuous descent through semantic Crouch;
- planted crouch isolation from X/Z;
- smooth standing recovery;
- jump ownership/landing;
- single-leg and scale-change rejection;
- vertical tracking/calibration reset;
- in-place jump X/Z isolation.

The obsolete test suite for the removed fusion stationary gate and post-solve foot anchor is deleted.

## Verification limitation and next boundary

No Unity Editor/Test Runner is available in the Builder environment. Editor tests are authored and statically reasoned but are **not claimed as executed** without real runner evidence.

Next action is genuine USER Unity webcam QA of the reconstructed Motion Engine. Phase 6, performance work, hands/foundations and `main` merge remain out of scope until that QA is reviewed.
