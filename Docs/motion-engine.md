# Golden Needle — Motion Engine V1

Status: **PHASE 4 USER ACCEPTED — PASS. PHASE 5A BATCH 2 IMPLEMENTED / USER QA DEFERRED / NOT YET USER ACCEPTED.**

Authoritative roadmap refresh: 2026-09-16.

The Motion Engine V1 objective is a responsive webcam-driven embodied-control system that works on low-end CPU-first hardware, reproduces trustworthy body pose through the accepted Phase 3 + Phase 4 stack, and translates deliberate user movement into gameplay locomotion without conflating pose reproduction with world movement.

## Current phase status

| Phase | Status |
|---|---|
| Phase 1 — provider/raw pose | **PASS WITH NOTES** |
| Phase 2 — canonical skeleton | **PASS** |
| Phase 3 — stabilization/confidence/calibration foundation | **PASS** |
| Phase 4 — humanoid retargeting | **USER ACCEPTED — PASS** |
| Low-end optimization milestone | **USER SATISFIED / FROZEN FOR CURRENT MILESTONE** |
| Phase 5A — horizontal locomotion | **BATCH 2 IMPLEMENTED / USER QA DEFERRED / NOT YET USER ACCEPTED** |
| Phase 6 — graybox vertical slice | **NOT STARTED** |

## Normal production body pipeline

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
-> presentation
```

Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths. The accepted scheduling model allows one active body inference and at most one replaceable newest pending frame. No FIFO/history/replay/catch-up backlog is part of the normal architecture.

The USER reports that performance appears restored after the corrective pose-baseline work and has explicitly closed further optimization for the current hackathon milestone. Performance can be revisited later if new evidence justifies it.

## Phase 1 — provider/raw pose boundary

`MediaPipePoseProvider` remains the single normal body-provider pipeline. MediaPipe-specific result structures stay upstream. The provider preserves MediaPipe 0.10.22 preprocessing/tracking/decode/world semantics around the accelerated OpenVINO neural execution path, and publishes the 33 normalized/world pose landmarks through project-owned observation state.

The provider remains replaceable. Downstream game and course systems must not depend directly on MediaPipe classes or source landmark indices.

## Phase 2 — CanonicalBodyV1

The engine-owned `CanonicalPoseFrame` remains the accepted 20-joint contract:

`Pelvis, Spine, Chest, Head, LeftShoulder, LeftElbow, LeftWrist, RightShoulder, RightElbow, RightWrist, LeftHip, LeftKnee, LeftAnkle, LeftHeel, LeftToe, RightHip, RightKnee, RightAnkle, RightHeel, RightToe`.

Partial bodies remain valid per joint/chain; unavailable lower-body landmarks do not invalidate trustworthy upper-body data. Canonical axes remain +X camera/view right, +Y up, +Z away. Canonical image space is x=0 left/x=1 right and y=0 bottom/y=1 top.

## Phase 3 — stabilization and calibration authority

Stable Phase 3 canonical data remains the trusted calibration and locomotion input. The existing independent per-joint confidence/hysteresis/dropout logic and One Euro filtering remain preserved.

Phase 4 modular calibration remains accepted: comfortable body reference establishes torso/reference geometry and limb chains accumulate their own geometry independently. Missing limbs do not globally block usable body control.

Raw/responsive avatar-drive sources remain engineering options for avatar responsiveness, but they do not silently replace the stable calibration/locomotion authority.

## Phase 4 — production body-pose authority

Phase 4 remains USER accepted.

Production behavior preserves:

- signed canonical-to-avatar reference mapping;
- stable source/target basis semantics;
- canonical positional targets;
- project-owned analytic two-bone IK;
- avatar-authored limb proportions;
- partial-body/per-chain validity;
- accepted pelvis/chest orientation behavior;
- modular measurement calibration.

Normal limb orientation remains deliberately swing/position based where the monocular source does not reliably observe axial twist. The production system does not fabricate free pronation/supination or other unobservable axial DOFs.

The current normal execution boundary is:

```text
Phase 4 solve
-> presentation
```

Foundation C rich anatomical orientation and Foundation E post-solve detail are not normal production authorities. `MotionEngineRuntime` has no production `RichMotionFrame`; `HumanoidRetargeter` is restored to the Phase-4 solve/presentation path; production `RichHumanoidDetailRetargeter` was removed.

## Retained operational foundations

### Foundation A — shared commands and speech

The shared command/action system is retained. Keyboard and speech invoke the same command router rather than simulating key presses or duplicating behavior.

USER microphone QA succeeded with the Windows phrase system/`KeywordRecognizer`. The configured commands were recognized and dispatched; noisy conditions frequently yielded Low-confidence results, while clearer/louder speech produced successful cases.

All 14 mappings from `SpeechCommandConfiguration.CreateDefault()` explicitly use `SpeechRecognitionConfidence.Low`. The configurable wake prefix remains empty by default. Custom mappings still default to Medium unless changed.

### Foundation B — camera presets

The unified primary-camera preset architecture is retained:

`Back`, `Front`, `Left`, `Right`, `FullBody`, `Hands`, `LeftHand`, `RightHand`.

Camera preset selection uses shared command ownership. F12 Lab/Game mode remains presentation-only and orthogonal to the selected preset.

## Deferred/rejected pose-detail experiments

### Foundation C

`DEFERRED / DORMANT RESEARCH — NOT PRODUCTION POSE AUTHORITY`.

Rich-motion contracts/solver code may remain for historical research, but corrective restoration removed the rich path from normal `MotionEngineRuntime` composition.

### Foundation D

`DEFERRED`.

The separate MediaPipe Hand Landmarker stream produced unacceptable low-end performance in USER testing, entering roughly the 10–15 FPS class with detailed hands active. Existing research/lifecycle code may remain, but detailed hand inference is not normal production operation or a Motion Engine V1 requirement for this hackathon.

### Foundation E

`RETIRED FROM PRODUCTION`.

The production `RichHumanoidDetailRetargeter` was removed and obsolete Foundation-E Editor tests were removed at `f1819fda36547343bb32a972d39405d0a6be6f72`. Foundation-E files that remain are research/history, not live post-Phase-4 execution.

### Coarse hand / fist experiment

`DEFERRED`.

Former Batch 4A reused pose landmarks without extra inference and passed Builder/static checks, but the USER rejected the feature/value tradeoff after runtime evaluation and the feature was rolled back. The project does not claim the coarse vector arithmetic alone was proven to cause the observed slowdown.

## POSE != LOCOMOTION

Pose reproduction and game-world movement remain separate concerns.

Phase 4 controls body pose. Phase 5 locomotion interprets the stable canonical body/support evidence and moves the player/avatar root for gameplay. Locomotion must not corrupt the accepted Phase 4 body solve.

## Phase 5A — Batch 2 horizontal locomotion architecture

Batch 2 began from remote SHA `21184fe89d8f4c6f7b9ec387cdab84f884ad0e41`. The implementation/tests/scene checkpoint before documentation is `e40e326e3f9a6fa8c9675dcf60fb1c0b2e2904c9`.

### Physical camera-space/root tracking

`CameraSpaceRootTracker` continues to build each left/right support-foot estimate from trusted ankle/heel/toe canonical image observations. Relative foot displacement is normalized by the captured body-scale reference. Torso apparent scale remains auxiliary normalization/depth-corroboration evidence and does not independently initiate room translation.

The former unconditional common/midpoint authority `(leftDelta + rightDelta) / 2` is superseded because a moving raised/swing foot could shift the midpoint while the opposite foot remained planted.

Batch 2 adds a stateful support authority:

- `Both`: used when normalized left/right foot heights are near equal; authority is the two-foot midpoint;
- `Left`: used when the left foot is clearly lower than the right; authority is left-foot displacement;
- `Right`: used when the right foot is clearly lower than the left; authority is right-foot displacement;
- ambiguous height separation retains the previous single-foot mode rather than switching every frame.

Canonical image Y increases upward, so the higher image-Y foot is the raised candidate and the lower foot is the support candidate.

The Batch-2 thresholds are:

- `supportSingleFootEnter = 0.12` normalized body/reference scale;
- `supportBothEnter = 0.06` normalized body/reference scale.

The separate enter/return thresholds provide hysteresis. They are Inspector-serialized through `CameraSpaceRootTrackerSettings` and sanitized so the Both threshold remains below the single-foot-enter threshold.

### Continuity and tracking loss

A support-mode change must not move the avatar merely because the coordinate authority changed. Whenever support authority changes, the tracker computes an offset that maps the newly selected raw support displacement onto the current filtered displacement. The same continuity rebase is required on the first valid sample after temporary support loss.

Temporary invalid support still publishes the last trusted displacement with zero velocity. `Recenter()` still captures the current trusted support measurement as a new physical origin and resets physical displacement to zero.

Genuine room translation remains possible: when both feet/support base relocate together, `Both` authority follows that relocation. Existing camera-depth differential attenuation and torso-scale corroboration remain in place.

`supportCommonXZ` and `supportDifferentialXZ` remain diagnostic/evidence channels; Batch 2 did not add per-frame console spam or a new diagnostics subsystem.

### Planted-feet lean preservation

Torso-only lateral movement and torso apparent-scale change still do not create physical translation when the feet remain planted. Batch 2 retains that separation and adds/keeps deterministic Editor coverage for both lateral lean and forward/back scale change.

### Cadence

`CadenceDetector` still uses alternating lower-body image rhythm. Ankle vertical separation is primary and knee separation is supporting evidence, normalized by apparent body scale. Batch 2 changes tuning, not detector architecture.

Current Batch-2 defaults are:

- `minimumJointConfidence = 0.40`;
- `signalResponse = 14`;
- `eventThreshold = 0.07` — unchanged;
- `minimumStepRate = 0.8`;
- `maximumStepRate = 4.5`;
- `acquisitionEvents = 2` — previously 3;
- `acquireConfidence = 0.38` — previously 0.50;
- `sustainConfidence = 0.25`;
- `stopTimeoutSeconds = 0.50`;
- `virtualStridePerStep = 0.60` — previously 0.42;
- `maximumVirtualSpeed = 3.0` — previously 2.5.

This baseline is intended to let two clean alternating events acquire materially faster while a single isolated event still cannot activate cadence. The event threshold remains unchanged rather than globally weakening rhythm detection.

Cadence virtual speed remains:

`min(maximumVirtualSpeed, rateStepsPerSecond * virtualStridePerStep)`

so increasing distance per step increases cadence travel until the maximum-speed clamp.

### Inspector controls and scene serialization

The existing serialized cadence controls remain the runtime authority. The user can tune confidence, signal response, event threshold, min/max step rate, acquisition event count, acquire/sustain confidence, stop timeout, stride/distance, and max virtual speed from the `EmbodiedLocomotionController` settings.

For clearer presentation without breaking serialization:

- `virtualStridePerStep` is labeled **Distance Per Step**;
- `maximumVirtualSpeed` is labeled **Maximum Cadence Speed**.

The active Motion Engine Lab scene already serialized the old cadence values, so changing C# defaults alone would not have applied the new baseline. Its locomotion block was updated only for:

- `supportSingleFootEnter = 0.12`;
- `supportBothEnter = 0.06`;
- `acquisitionEvents = 2`;
- `acquireConfidence = 0.38`;
- `virtualStridePerStep = 0.60`;
- `maximumVirtualSpeed = 3.0`.

A net comparison against the Batch-2 starting SHA was used to verify unrelated scene serialization values were restored and excluded from the final Batch-2 scene diff.

### Heading and fusion

`BodyHeadingEstimator` still supplies mapped world heading for cadence travel.

`LocomotionFusion` still:

- scales/deadzones physical lateral/depth displacement;
- maps physical camera-space displacement through the accepted Phase 4 canonical-to-avatar reference map;
- suppresses cadence progressively while trusted physical motion is active to avoid obvious double counting;
- holds the last trusted physical offset through temporary root-tracking loss.

The fusion defaults remain lateral/depth scale `0.9 / 1.5`, lateral/depth deadzones `0.012 / 0.012`, physical velocity suppression from `0.08` to `0.32`, and minimum trusted root confidence `0.30`.

### Controller and recenter

`EmbodiedLocomotionController` still consumes `runtime.StabilizedFrame` and writes only avatar-root world **X/Z**, preserving current Y and root rotation. Cadence integrates a virtual origin; physical displacement is added as a mapped offset. `Recenter()` preserves the current virtual world X/Z while the physical tracker captures a new zero origin.

Lab/Game presentation and third-person camera operation remain unchanged around this locomotion layer.

## Batch-2 deterministic coverage and verification status

`Assets/GoldenNeedle/Tests/Editor/Phase5LocomotionTests.cs` was updated in place. The old `SingleStepOnsetMovesSupportMidpointWithoutHardHolding` expectation was removed/reframed because its required behavior contradicted current USER runtime evidence.

The suite now covers at least:

1. planted feet + torso lateral lean -> no physical translation;
2. planted feet + torso scale/forward-back lean -> no physical translation;
3. clearly raised/moved swing foot -> no meaningful physical translation;
4. lifted-foot movement across samples -> no accumulated root translation;
5. genuine bilateral/support-base relocation -> physical translation remains;
6. support transition/landing -> continuity without a teleport;
7. jogging in place -> physical root remains near zero while cadence can acquire;
8. temporary support loss -> hold last trusted displacement;
9. tracking reacquisition -> continuity rebase without a root jump;
10. default two-event cadence acquisition and cadence stop after rhythm disappears;
11. one isolated cadence event -> no activation;
12. distance per step changes virtual speed until the max-speed clamp;
13. physical translation still suppresses cadence through fusion;
14. accepted front-camera mapping/heading remains covered;
15. recenter remains covered by the bilateral relocation test.

Verification status is deliberately precise:

`IMPLEMENTED / AUTOMATED-VERIFIED AS AVAILABLE / USER QA DEFERRED`

No Unity Editor/Test Runner is available in this Builder execution environment. GitHub also reports no workflow run/combined status attached to the Batch-2 implementation checkpoint. Therefore the updated Editor tests were **not actually executed here**. Source semantics, canonical-Y polarity, branch scope, scene serialization and net diffs were statically audited. This is not a passing Unity test result and not USER acceptance.

## Jump — Motion Engine V1 requirement

A real physical jump must produce corresponding vertical game movement.

The Batch 3 implementation must, at a requirements level:

- use coherent body/support evidence;
- distinguish a real jump from lifting only one leg;
- reject ordinary tracking noise;
- expose a clear takeoff/airborne/landing lifecycle;
- expose useful Inspector tuning where appropriate;
- integrate without corrupting the Phase 4 body pose.

Batch 2 did **not** implement jump, root-Y gameplay, gravity, or an airborne gameplay state.

## Crouch — Motion Engine V1 requirement

A real physical crouch must correspondingly lower/crouch the game character.

The Batch 3 implementation must, at a requirements level:

- use normalized body-compression/height evidence instead of fragile raw-pixel-only thresholds;
- support holding a crouched state;
- use acquisition/release hysteresis;
- expose useful Inspector tuning;
- integrate without corrupting the Phase 4 body pose.

Batch 2 did **not** implement crouch or a crouch root offset.

## Final Motion Engine V1 completion plan

### Batch 1 — documentation synchronization

**COMPLETE.**

### Batch 2 — horizontal locomotion completion

**IMPLEMENTED / AUTOMATED-VERIFIED AS AVAILABLE / USER QA DEFERRED.**

### Batch 3 — vertical locomotion + final V1 completion

**NOT STARTED.** After Orchestrator review/authorization, implement jump/crouch, preserve Phase 4 body-pose authority, and prepare one final comprehensive USER Motion Engine QA.

## Testing policy

USER/runtime testing remains intentionally deferred until all three completion batches have been implemented. No Batch-2 USER runtime test was requested. Builder-side static/deterministic evidence does not create USER acceptance. Batch 3 prepares the final integrated runtime pass.

## Phase 6 boundary

Phase 6 remains **NOT STARTED**. It is the later graybox/playable vertical-slice integration step after Motion Engine V1 completion. Do not begin Phase 6 during Batch 2 or before explicit new USER direction.
