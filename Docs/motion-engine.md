# Golden Needle — Motion Engine V1

Status: **PHASE 4 USER ACCEPTED — PASS. PHASE 5A IMPLEMENTED / NOT USER ACCEPTED / ACTIVE DEVELOPMENT TARGET.**

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
| Phase 5A — horizontal locomotion | **IMPLEMENTED / NOT YET USER ACCEPTED / ACTIVE DEVELOPMENT TARGET** |
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

Partial bodies remain valid per joint/chain; unavailable lower-body landmarks do not invalidate trustworthy upper-body data. Canonical axes remain +X camera/view right, +Y up, +Z away.

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

## Phase 5A — current horizontal locomotion architecture

Phase 5A is already substantially implemented.

### Physical camera-space/root tracking

`CameraSpaceRootTracker` builds each support-foot estimate from trusted ankle/heel/toe image observations. Relative foot displacement is normalized by a body-scale reference. The current common component `(leftDelta + rightDelta) / 2` drives physical room displacement; the differential component `(leftDelta - rightDelta) / 2` is gait/asymmetry evidence that attenuates camera-depth trust.

Torso apparent scale is auxiliary normalization/depth-corroboration evidence and does not independently initiate room translation.

This current midpoint/common-displacement design is also the source-level reason the known **raised/swing-leg false translation** defect is plausible: one foot moving while the other remains planted can shift the two-foot midpoint. Batch 2 must distinguish planted/support motion from swing-leg movement rather than interpreting every midpoint change as room translation.

Current root-tracker settings include support confidence/scale thresholds, depth-differential attenuation, depth corroboration thresholds, position/velocity response, and a depth-proxy clamp.

### Cadence

`CadenceDetector` uses alternating lower-body image rhythm. Ankle vertical separation is primary and knee separation is supporting evidence, normalized by apparent body scale.

Current Inspector-facing settings already include:

- `minimumJointConfidence = 0.40`;
- `signalResponse = 14`;
- `eventThreshold = 0.07`;
- `minimumStepRate = 0.8`;
- `maximumStepRate = 4.5`;
- `acquisitionEvents = 3`;
- `acquireConfidence = 0.50`;
- `sustainConfidence = 0.25`;
- `stopTimeoutSeconds = 0.50`;
- `virtualStridePerStep = 0.42`;
- `maximumVirtualSpeed = 2.5`.

These values are current code defaults, not final accepted tuning. USER observation is that cadence activates more slowly than desired and current travel distance/speed after activation is not satisfactory. Batch 2 must audit/tune the existing settings path so responsiveness and distance are clearly controllable from the Inspector without code edits.

### Heading and fusion

`BodyHeadingEstimator` supplies mapped world heading for cadence travel.

`LocomotionFusion`:

- scales/deadzones physical lateral/depth displacement;
- maps physical camera-space displacement through the accepted Phase 4 canonical-to-avatar reference map;
- suppresses cadence progressively while trusted physical motion is active to avoid obvious double counting;
- holds the last trusted physical offset through temporary root-tracking loss.

Current fusion defaults include lateral/depth scale `0.9 / 1.5`, lateral/depth deadzones `0.012 / 0.012`, physical velocity suppression from `0.08` to `0.32`, and minimum trusted root confidence `0.30`. These remain tuning values, not final acceptance constants.

### Controller and recenter

`EmbodiedLocomotionController` consumes `runtime.StabilizedFrame` and currently writes only avatar-root world **X/Z**, preserving the current Y and root rotation. Cadence integrates a virtual origin; physical displacement is added as a mapped offset. `Recenter()` makes the current physical position the new tracking origin while preserving the current virtual world X/Z.

Lab/Game presentation and third-person camera operation are already implemented around this locomotion prototype.

## Phase 5A known issues entering Batch 2

1. **Planted-feet lean:** suppression appears mostly successful in current USER observation, but is not finally accepted because integrated runtime testing is deferred.
2. **Raised/swing-leg false translation:** one lifted/moving leg while the other remains planted can trigger locomotion. This is a required Batch 2 fix.
3. **Cadence acquisition:** works but activates slower than desired.
4. **Cadence travel distance:** current travel after activation is unsatisfactory.
5. **Horizontal consistency audit:** lateral/depth/heading/recenter/fusion already exist but must be audited together before acceptance.

Phase 5A is not being rebuilt from scratch.

## Jump — Motion Engine V1 requirement

A real physical jump must produce corresponding vertical game movement.

The Batch 3 implementation must, at a requirements level:

- use coherent body/support evidence;
- distinguish a real jump from lifting only one leg;
- reject ordinary tracking noise;
- expose a clear takeoff/airborne/landing lifecycle;
- expose useful Inspector tuning where appropriate;
- integrate without corrupting the Phase 4 body pose.

Batch 1 deliberately does not prescribe the final algorithm.

## Crouch — Motion Engine V1 requirement

A real physical crouch must correspondingly lower/crouch the game character.

The Batch 3 implementation must, at a requirements level:

- use normalized body-compression/height evidence instead of fragile raw-pixel-only thresholds;
- support holding a crouched state;
- use acquisition/release hysteresis;
- expose useful Inspector tuning;
- integrate without corrupting the Phase 4 body pose.

Batch 1 deliberately does not prescribe the final algorithm.

## Final Motion Engine V1 completion plan

### Batch 1 — documentation synchronization

Synchronize current authority only. No runtime/code changes.

### Batch 2 — horizontal locomotion completion

- audit current Phase 5A;
- fix raised/swing-leg false physical translation;
- preserve/regression-check the mostly successful planted-feet lean suppression;
- improve cadence acquisition responsiveness;
- make cadence distance/speed clearly Inspector-tunable;
- verify lateral/depth/heading/recenter/fusion coherence;
- do not add jump/crouch yet.

### Batch 3 — vertical locomotion + final V1 completion

- implement jump detection/application;
- implement crouch detection/application;
- distinguish jump from single-leg lift;
- expose appropriate Inspector tuning;
- integrate with current Phase 5A while preserving Phase 4;
- prepare one final comprehensive USER Motion Engine QA.

## Testing policy

USER/runtime testing is intentionally deferred until all three completion batches have been implemented. Batch 1 requires no USER runtime test. Batch 2 should continue through Builder-side compile/static/deterministic validation as appropriate rather than stop waiting for USER QA. Batch 3 prepares the final integrated runtime pass.

No Builder test result should be described as USER acceptance.

## Phase 6 boundary

Phase 6 remains **NOT STARTED**. It is the later graybox/playable vertical-slice integration step after Motion Engine V1 completion. Do not begin Phase 6 during this documentation batch or the horizontal-locomotion Batch 2 without explicit new USER direction.
