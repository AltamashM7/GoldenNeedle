# Golden Needle — Current State

Authoritative current-state refresh: 2026-09-16.

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## Governance

- Do **not** merge to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or rewrite shared history.
- Independently inspect the live remote branch before new work.
- USER Unity/manual evidence is the acceptance authority.
- Phase 6 remains **NOT STARTED**.

## Current checkpoint

Phase-5 authority reconstruction baseline:

`08d7ea5785dd5935ed7240f004313b2a769d7a52`

Implementation + deterministic-test checkpoint:

`753ef564c253c48d6d2c71e9095d1e6e7878e2fe`

Current status:

**PHASE-5 AUTHORITY RECONSTRUCTION IMPLEMENTED / USER QA PENDING**

Motion Engine V1 remains **NOT YET USER ACCEPTED**.

## Preserved production baseline

The accepted body path remains unchanged:

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

Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths. The accepted low-end optimization milestone remains frozen unless new reproducible evidence requires reopening it.

Phase 3 stable canonical data remains locomotion/calibration input. Phase 4 remains the USER-accepted bone/IK authority. Phase 5 executes later and moves only the avatar/player root.

## Why Phase 5 was reconstructed

The post-Batch-3 runtime had accumulated multiple position authorities:

1. `CameraSpaceRootTracker` owned filtered support-based position and support-transition rebasing;
2. `LocomotionFusion` later added another committed-position/stationary gate with its own rebase state;
3. vertical crouch later added post-Phase-4 solved-foot anchoring as another root-Y authority.

The USER still observed constant locomotion-caused idle offset/jitter and crouching that visually bent/lifted the legs beneath an insufficiently descending body.

Historical checkpoints were therefore inspected before editing:

- `87698948b12cd10b6fef2072d0ad0ce9eeaecdfe` — early torso/body-root prototype;
- `33698719a2907d30bb3396f66e5b79e59ccbfe9e` — support-base corrective commit;
- `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0` — last known good pre-Foundation Phase-5 reference;
- `466d65826ab747493c211e1a3250aafa305167c8` — immediate pre-reconstruction implementation.

The reconstruction keeps the valid lessons from all four instead of reverting wholesale.

## Reconstructed horizontal authority

### One physical position owner

`CameraSpaceRootTracker` is again the **only stateful physical-position authority**.

It owns:

- physical reference/origin;
- accepted camera-space displacement;
- filtering and physical velocity;
- support validation;
- tracking-loss continuity/reacquisition rebase;
- physical recenter.

`LocomotionFusion` no longer owns another accepted-position/stationary state machine. It maps the already-authoritative root displacement through the accepted Phase-4 reference basis, holds the last mapped contribution during invalidity, and blends cadence according to authoritative physical velocity.

### Body candidate + support validation

The tracker reconstructs the useful separation seen across the historical checkpoints:

- torso center and apparent body scale provide the continuous **body/root candidate**;
- ankle/heel/toe observations provide **support evidence**;
- support evidence validates or rejects candidate room movement rather than becoming an independent downstream position owner.

A candidate commits only when body movement and coherent bilateral support relocation agree in direction and exceed bounded evidence thresholds.

Consequences:

- planted torso lean/sway does not move the physical root because support does not corroborate it;
- apparent-scale change alone does not create forward/back travel;
- a raised/swinging single foot does not create room translation;
- completed coherent dual-support relocation can commit real physical displacement;
- small idle noise does not continually retarget the accepted position;
- slow deliberate movement accumulates against the last accepted state and eventually commits;
- support/tracking loss holds the last accepted position; first reacquired evidence is continuity-rebased before new movement can commit.

Current support classification hysteresis remains:

- `supportSingleFootEnter = 0.12`;
- `supportBothEnter = 0.06`.

The current depth safeguards remain:

- differential reliability start/full `0.05 / 0.20`;
- minimum support depth evidence `0.018`;
- minimum matching body-scale depth evidence `0.012`.

Current root response remains `10/s` position and `8/s` velocity.

### Cadence retained

Cadence code and active Lab serialization are unchanged:

- event threshold `0.07`;
- acquisition events `2`;
- acquire confidence `0.38`;
- sustain confidence `0.25`;
- stop timeout `0.50 s`;
- step-rate range `0.8–4.5/s`;
- Distance Per Step `0.60`;
- Maximum Cadence Speed `3.0`.

Jogging in place should remain near-zero physical displacement while cadence can acquire.

### Recenter

`Recenter()` remains X/Z-only. The tracker captures the current physical measurement as the new origin; the controller simultaneously sets the virtual X/Z origin to the current avatar world position so recenter does not teleport the character.

## Reconstructed vertical authority

`VerticalLocomotionInterpreter` remains the only semantic Jump/Crouch state authority.

The semantic states remain:

- `Unavailable`;
- `Standing`;
- `Jump` with `Grounded/Takeoff/Airborne/Landing` phases;
- `Crouch`.

### Continuous grounded body compression

Negative root Y is now driven directly from trustworthy normalized **pelvis-to-support compression** relative to the runtime standing reference.

The same compression evidence exists before the semantic crouch threshold is crossed. Therefore:

- shallow planted bend may remain semantically `Standing` but already lower the avatar root;
- deeper compression continues smoothly through the same root-Y path when semantic `Crouch` acquires;
- semantic `Crouch` remains thresholded independently for gameplay;
- standing recovery returns the root toward the calibration-session Y origin.

A small motion deadband is derived from existing crouch-release tuning (`0.25 * crouchReleaseThreshold`, clamped to `0.01–0.05`) to suppress neutral compression noise without delaying body descent until semantic Crouch.

Grounded compression requires the existing evidence to remain trustworthy: coherent feet, approximately grounded support, and stable apparent scale.

### Jump remains separate

Jump remains coherent whole-body rise from both support feet + pelvis + chest with the existing asymmetry, coherence-spread and apparent-scale safeguards.

When semantic Jump is active, jump exclusively owns positive root Y. Grounded compression does not pin takeoff to the floor.

The existing controller/fusion boundary depth hold remains: while jump/landing is active, the copy of physical root data passed to fusion holds pre-jump depth and zeroes depth velocity so vertical takeoff cannot leak into world Z.

### Removed vertical duplicate owner

`GroundedCrouchFootAnchor` and the separate post-Phase-4 solved-foot rise correction are removed from the runtime authority path. Feet remain evidence/constraint; pelvis/body compression is the primary negative root-Y signal.

The controller again applies one vertical result:

```text
desiredY = verticalOriginY + verticalSample.worldOffsetY
```

No CharacterController, Rigidbody gravity, collision/ground probing, autonomous jump animation, or Phase-6 gameplay physics is introduced.

## Diagnostics

The low-cost F9 locomotion view now exposes the reconstructed authority split:

- body/root candidate X/Z;
- support mode and whether bilateral support validated the frame;
- support evidence;
- authoritative accepted X/Z and velocity;
- whether movement was accepted this frame;
- vertical semantic state/jump phase;
- grounded-bend activity;
- compression, support rise, asymmetry, apparent-scale delta;
- vertical offset/final Y and standing-origin readiness.

No per-frame Console logging is added.

## Deterministic coverage

`Phase5LocomotionTests.cs` was rewritten around behavioral requirements rather than the superseded ownership implementation. It covers idle stability, nonzero-position settling, torso-lean and scale-only rejection, swing-foot rejection, coherent physical relocation, alternating-step completion, landing continuity, slow deliberate movement, loss/reacquisition, recenter, physical-vs-cadence blending, depth corroboration, axis/heading semantics, and jogging-in-place cadence.

`VerticalLocomotionTests.cs` covers neutral standing, shallow grounded compression before semantic Crouch, deeper continuous descent into semantic Crouch, semantic/motion separation, no planted-crouch X/Z travel, smooth standing recovery, coherent jump, jump ownership after a bend, single-leg rejection, scale-change rejection, landing, tracking/calibration reset, and in-place jump X/Z isolation.

The obsolete `LocomotionGroundingStabilityTests.cs` was removed because it encoded the superseded downstream fusion gate and solved-foot root-Y ownership.

## Verification status

No Unity Editor/Test Runner is available in the Builder execution environment. The deterministic Editor tests are authored and source/diff behavior is statically audited, but they are **not claimed as executed** unless actual runner evidence appears.

The next acceptance boundary is genuine USER Unity webcam QA of the reconstructed Motion Engine. Do not start Phase 6 before that result is reviewed.
