# Golden Needle — Current State

Authoritative refresh: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## Governance

- Do **not** merge to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or rewrite shared history.
- Independently verify the live remote branch before new work.
- USER Unity/manual evidence is the acceptance authority.
- Phase 6 remains **NOT STARTED**.

## Current checkpoint

Phase-5 authority-reconstruction baseline:

`08d7ea5785dd5935ed7240f004313b2a769d7a52`

Published reconstruction implementation/tests/diagnostics/docs checkpoint:

`dc48e9d2f18c65b1631fbdb390e60578087a1303`

Current status:

**PHASE-5 AUTHORITY RECONSTRUCTION IMPLEMENTED / USER QA PENDING**

Motion Engine V1 remains **NOT YET USER ACCEPTED**.

## Preserved production baseline

```text
Unity WebCamTexture
-> reusable WebCamCPU/GetPixels32 acquisition
-> reusable 320x240 CPU preparation
-> bounded newest-only two-slot scheduling
-> persistent OpenVINO CPU FP32 worker
-> MediaPipe 0.10.22 pose semantics
-> 33 normalized + world landmarks
-> CanonicalBodyV1
-> Phase 3 stabilization/calibration authority
-> Phase 4 positional/IK avatar control
-> Phase 5 root translation
-> presentation
```

Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths. The accepted low-end optimization milestone remains frozen unless new reproducible evidence requires reopening it. Phase 3 remains locomotion/calibration data authority; Phase 4 remains USER-accepted bone/IK authority; Phase 5 moves only the avatar/player root after Phase 4.

Foundation A commands/speech and Foundation B cameras remain retained. Foundation C is dormant research, Foundation D hands are deferred, Foundation E is retired from production, and coarse hands remain deferred/rolled back.

## Why Phase 5 was reconstructed

The post-Batch-3 runtime had accumulated multiple position authorities: support/rebase state in `CameraSpaceRootTracker`, another committed-position/stationary state machine in `LocomotionFusion`, and later a post-Phase-4 solved-foot root-Y anchor. USER runtime still showed locomotion-caused idle motion and insufficient body descent during crouch.

Historical checkpoints were independently inspected before editing:

- `87698948b12cd10b6fef2072d0ad0ce9eeaecdfe` — early continuous torso/body-root prototype;
- `33698719a2907d30bb3396f66e5b79e59ccbfe9e` — support-base corrective commit;
- `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0` — last known good pre-Foundation Phase-5 reference;
- `466d65826ab747493c211e1a3250aafa305167c8` — immediate pre-reconstruction implementation.

The reconstruction keeps useful behavior from all four rather than reverting wholesale.

## Reconstructed horizontal authority

`CameraSpaceRootTracker` is again the **only stateful physical-position authority**. It owns the physical reference, accepted displacement, filtering/velocity, support validation, tracking-loss/reacquisition continuity and recenter.

Torso center plus yaw-compensated apparent scale provide the continuous body/root candidate. Weighted ankle/heel/toe support observations validate whether that candidate is genuine room relocation rather than lean/sway/swing articulation.

Support classification remains `Both/Left/Right` with:

- `supportSingleFootEnter = 0.12`;
- `supportBothEnter = 0.06`.

Only coherent dual support can validate a new room-position commit. Body and bilateral-support deltas must agree in direction. Small evidence is measured from the last accepted physical state, so idle fluctuations do not rewrite position while slow deliberate movement accumulates until it can commit.

The long-standing normalized lateral evidence floor is `0.012`. Existing depth safeguards remain body-scale evidence `0.012`, support-depth evidence `0.018`, and foot-differential reliability `0.05–0.20`. Root position/velocity responses remain `10/s` and `8/s`.

Tracking loss holds the last accepted displacement. First reacquired body/support observations are continuity-rebased onto that state before subsequent coherent movement can commit.

`LocomotionFusion` no longer owns another committed-position/stationary/rebase state machine. It maps the authoritative root displacement, holds the last mapped contribution during invalidity, and blends cadence based on authoritative root velocity.

Current fusion values remain lateral/depth scale `0.9 / 1.5`, origin deadzones `0.012 / 0.012`, physical velocity suppression `0.08 / 0.32`, minimum root confidence `0.30`.

Cadence code/serialization remains unchanged: event threshold `0.07`, 2-event acquisition, acquire `0.38`, sustain `0.25`, timeout `0.50 s`, rate `0.8–4.5/s`, distance per step `0.60`, max speed `3.0`.

Horizontal `Recenter()` remains X/Z-only and preserves virtual world position.

## Reconstructed vertical authority

`VerticalLocomotionInterpreter` remains the only semantic Jump/Crouch state authority. States remain `Unavailable`, `Standing`, `Jump`, `Crouch`, with jump phases `Grounded -> Takeoff -> Airborne -> Landing -> Grounded`.

Negative root Y is now driven directly from trustworthy normalized pelvis-to-support compression relative to the runtime standing reference. The same signal exists before semantic `Crouch` acquisition, so a shallow planted bend can remain semantically `Standing` while already lowering root Y. Deeper compression continues through the same path and may cross the existing `0.18` Crouch enter threshold; release remains `0.09`.

A small neutral-motion deadband is derived from existing crouch release tuning:

```text
motionDeadband = clamp(0.25 * crouchReleaseThreshold, 0.01, 0.05)
effectiveCompression = max(0, compression - motionDeadband)
rootY = -clamp(effectiveCompression * crouchWorldScale, 0, maximumCrouchDepth)
```

Current crouch mapping remains `1.20` scale / `0.65` max. Grounded compression requires coherent feet, approximately grounded support and stable apparent scale.

Jump remains coherent whole-body rise and exclusively owns positive root Y while active. Current jump thresholds/scales remain `0.12` enter, `0.045` release, `1.60` scale, `0.90` max, `0.08` max foot asymmetry, `0.10` coherence spread. The existing pre-jump physical-depth hold remains at the controller/fusion boundary so takeoff cannot leak into world Z.

`GroundedCrouchFootAnchor` and the separate post-Phase-4 solved-foot-rise root-Y correction are removed from runtime authority. Feet are evidence/constraint; pelvis/body compression is the primary negative-Y signal.

Controller target is again one vertical result:

```text
X/Z = virtualOriginXZ + mapped physical contribution + integrated cadence
Y   = verticalOriginY + verticalSample.worldOffsetY
```

No CharacterController, Rigidbody gravity, collision probing, autonomous ballistics or Phase-6 gameplay physics is introduced.

## Diagnostics

F9 locomotion diagnostics expose body candidate, support mode/validation, support evidence, accepted physical displacement/velocity, per-frame commit state, vertical state/jump phase, grounded-bend activity, compression/support rise/asymmetry/scale change, and current/final root Y. No per-frame Console logging is added.

## Deterministic coverage

`Phase5LocomotionTests.cs` now tests neutral/nonzero idle stability, lean and scale-only rejection, swing-foot rejection, coherent room relocation, alternating-step completion, landing continuity, slow-movement accumulation, loss/reacquisition, recenter, physical/cadence blending, depth corroboration, axis/heading semantics and jogging-in-place cadence.

`VerticalLocomotionTests.cs` tests neutral standing, shallow compression before semantic Crouch, deeper continuous descent through semantic Crouch, semantic/motion separation, planted-crouch X/Z isolation, recovery, coherent jump, jump ownership after bend, single-leg/scale rejection, landing, tracking/calibration reset and in-place jump X/Z isolation.

The obsolete `LocomotionGroundingStabilityTests.cs` suite was removed because it encoded the superseded downstream fusion gate and post-solve foot-anchor authority.

## Verification status

No Unity Editor/Test Runner is available in the Builder execution environment. The deterministic Editor tests are authored and statically audited but are **not claimed as executed** without real runner evidence.

The next boundary is genuine USER Unity webcam QA of the reconstructed Motion Engine. Do not start Phase 6, reopen performance/hands work, or merge `main` before that result is reviewed.
