# Golden Needle — Architecture

Authoritative architecture refresh: 2026-09-16.

Status: **Phase 4 USER ACCEPTED. Phase 5A implemented but not USER accepted. Motion Engine V1 completion is now focused on locomotion. Phase 6 is not started.**

Golden Needle remains a CPU-first, webcam-driven embodied-fitness system. The Motion Engine reproduces trustworthy user body pose separately from gameplay locomotion, and gameplay systems consume stable project-owned contracts rather than MediaPipe internals.

## Current high-level architecture

```text
optimized pose provider
-> CanonicalBodyV1
-> Phase 3 stabilization / calibration authority
-> Phase 4 signed mapping + positional/IK avatar control
-> Phase 5 locomotion interpretation / player-root movement
-> gameplay / player systems
```

Normal production pose execution is:

```text
Phase 4 solve
-> presentation
```

There is no active Foundation-E post-Phase-4 production layer.

## Optimized provider boundary

The best-tested low-end body path is:

```text
Unity WebCamTexture
-> reusable WebCamCPU/GetPixels32 acquisition
-> reusable 320x240 CPU preparation
-> bounded newest-only two-slot scheduling
-> persistent OpenVINO CPU FP32 worker
-> MediaPipe 0.10.22 pose semantics
-> 33 normalized + world landmarks
-> MediaPipeCanonicalPoseSource
-> CanonicalBodyV1
```

Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths. The scheduling rule remains one active inference plus at most one replaceable newest pending frame; no unbounded or historical inference queue is allowed.

MediaPipe-specific structures and numeric landmark knowledge remain on the provider/mapping side. Downstream Motion Engine and gameplay code consume Golden Needle contracts.

## Canonical / Phase 3 boundary

`CanonicalPoseFrame` remains the accepted fixed 20-joint CanonicalBodyV1 representation with partial-body validity. Canonical image/world coordinate meanings remain unchanged.

`MotionEngineRuntime` owns the normal canonical source -> stabilization -> calibration -> rotation/kinematic-target pipeline. Stable Phase 3 data remains the authority for calibration and Phase 5 locomotion interpretation, even when Raw/responsive avatar-drive frames are selected for avatar responsiveness experiments.

## Phase 4 production pose authority

Phase 4 remains the accepted body-pose authority:

- signed canonical-to-avatar reference mapping;
- pelvis/chest orientation from trusted body evidence;
- normalized positional chain targets;
- project-owned analytic two-bone IK;
- modular body/chain calibration;
- partial-body/per-chain validity;
- preservation of avatar-authored proportions.

Monocularly unobservable free axial/twist DOFs are not fabricated in normal production operation.

Corrective restoration deliberately removed Foundation C from production runtime composition and removed the production `RichHumanoidDetailRetargeter`. `MotionEngineRuntime` no longer owns a production `RichMotionFrame`. Research files may remain without being runtime authorities.

## POSE != LOCOMOTION

Body reproduction and world movement are different systems.

```text
Phase 4 avatar pose
        |
        +---- remains body-pose authority

Phase 3 stabilized canonical/body-support evidence
        -> CameraSpaceRootTracker
        -> CadenceDetector
        -> BodyHeadingEstimator
        -> LocomotionFusion
        -> EmbodiedLocomotionController
        -> player/avatar root movement
```

The current Phase 5A controller writes root **X/Z only**, preserving current root Y and rotation. This is the current horizontal implementation, not a permanent ban on vertical locomotion: jump and crouch have now been approved as Motion Engine V1 requirements for Batch 3.

### Current physical-root interpretation

`CameraSpaceRootTracker` estimates each foot from ankle/heel/toe image observations. The common two-foot displacement drives current physical translation; differential gait evidence reduces depth trust. Torso apparent scale is auxiliary normalization/depth corroboration only.

Because the current common component is the two-foot midpoint, moving one swing leg can still shift that midpoint even when the other foot is planted. That is the known Batch 2 raised/swing-leg false-translation defect. Batch 2 must add support/planted interpretation without redesigning the accepted Phase 4 pose system.

### Cadence and fusion

`CadenceDetector` derives an alternating lower-body rhythm from ankles and knees. It already owns Inspector-facing thresholds, acquisition-event count, acquire/sustain confidence, rate limits, timeout, virtual stride and maximum virtual speed.

`LocomotionFusion` maps physical camera displacement through the accepted Phase 4 reference axis map and blends cadence only when meaningful physical activity is low. Body heading is used for cadence travel. Recenter remains a separate supported action.

## Retained operational systems

### Shared commands / speech (Foundation A)

Keyboard, speech and future inputs share one command/action authority. The Windows phrase-recognition/`KeywordRecognizer` path passed USER microphone QA. All 14 product-default mappings now require Low recognition confidence; the configurable wake prefix remains empty by default.

### Camera presets (Foundation B)

One primary gameplay/presentation camera owns data-driven presets:

`Back`, `Front`, `Left`, `Right`, `FullBody`, `Hands`, `LeftHand`, `RightHand`.

Preset choice is independent of F12 Lab/Game mode and uses shared command ownership.

## Deferred research boundaries

### Foundation C

`DEFERRED / DORMANT RESEARCH — NOT PRODUCTION POSE AUTHORITY`.

Rich anatomical orientation ideas may remain in source for future research but do not participate in the normal production runtime.

### Foundation D

`DEFERRED`.

The independent detailed Hand Landmarker stream is not normal production operation after unacceptable low-end USER performance. It must not become a hidden dependency of body tracking or Motion Engine V1 completion.

### Foundation E

`RETIRED FROM PRODUCTION`.

Historical orientation/finger/post-solve research does not sit between Phase 4 and presentation. The production component was removed and stale Editor tests were later removed at `f1819fda36547343bb32a972d39405d0a6be6f72`.

### Coarse hands

`DEFERRED / ROLLED BACK`.

The former coarse `Unknown/Open/Closed` experiment is not part of current runtime or V1 completion scope.

## V1 vertical locomotion requirements

### Jump

Batch 3 must translate a real physical jump into vertical gameplay movement using coherent support/body evidence, distinguish jump from single-leg lift, reject tracking noise, model takeoff/airborne/landing state, and expose useful tuning.

### Crouch

Batch 3 must translate a real physical crouch into corresponding game lowering using normalized body-compression/height evidence, held-state support, acquire/release hysteresis, and useful tuning.

The final algorithms are intentionally not locked in this documentation batch.

## Module dependency direction

```text
pose-provider implementation
    -> Motion Engine contracts
    -> player / locomotion systems
    -> gameplay / future Hub / courses
```

Provider changes must not require rewriting gameplay. Pose refinement must not silently change locomotion semantics. Locomotion must not corrupt Phase 4 body pose. Optional/deferred research systems must not become mandatory body dependencies.

## Motion Engine maintainability

The Motion Engine remains independently maintainable and testable from the permanent Lab/debug infrastructure. Provider acquisition/inference, canonical stabilization/calibration, Phase 4 retargeting, locomotion interpretation, commands and presentation remain separable modules.

The current low-end performance milestone is frozen because the USER reports restored performance and is satisfied for the hackathon milestone. Future optimization remains possible, but only after new evidence identifies a worthwhile bottleneck.

## Completion / game integration boundary

The approved remaining engine sequence is:

```text
Batch 1: documentation synchronization
-> Batch 2: horizontal locomotion completion
-> Batch 3: jump + crouch + final Motion Engine V1 integration
-> one final comprehensive USER Motion Engine QA
-> Phase 6 graybox/playable vertical-slice integration later
```

Phase 6, Hub and course implementation have **not** begun simply because the Motion Engine roadmap is synchronized.
