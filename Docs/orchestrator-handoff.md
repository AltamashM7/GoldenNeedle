# Orchestrator handoff

## Project

**Golden Needle** — SIH 2026 gamified embodied fitness.

Core proposition: a webcam tracks continuous full-body movement and drives a humanoid avatar. Locomotion is a separate interpreted system.

## Start here

Repository: `AltamashM7/GoldenNeedle`

Current branch: `engine/pose-tracking-spike`

<!-- LATEST_HANDOFF_2026_09_08:START -->
## Current authoritative checkpoint

Starting correction checkpoint: `33698719a2907d30bb3396f66e5b79e59ccbfe9e`.

Status:
- Phase 4: **USER ACCEPTED — PASS**.
- Phase 5A: **SECOND USER QA PARTIAL PASS / V3 CORRECTION PENDING RE-QA / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.
- No merge without explicit USER approval.

Second QA passed idle stability and the planted-feet torso-lean fix, but real physical walking became intermittent because v2 hard-required near-equal left/right support displacement.

V3 keeps ankle/heel/toe composite feet but uses common support displacement for room position and differential displacement only as gait/depth-trust evidence. Lateral movement starts during the first step; depth still needs support-midpoint movement plus matching body-scale evidence. Missing supports hold.

Physical scales remain `0.9 / 1.5`; cadence timing and accepted mapping/recenter/Phase 4 behavior remain unchanged.

F12 adds Lab/Game presentation switching. Game View hides webcam/IMGUI and enables the third-person screen camera following avatar root + retained mapped heading. F1–F11 state is preserved exactly.
<!-- LATEST_HANDOFF_2026_09_08:END -->

Phase 4 handoff HEAD before the correction:

`4a26589ec2f90688b80fb6b1da0b849adda65d6b` — `docs: hand off phase 4 investigation state`

Its runtime parent is `5e830dce7ac3de542ab159b8b90992935d9dd0b0`. The branch now contains the accepted Phase 4 source/retarget correction, modular calibration redesign, passed procedural harness, Neko Animator Humanoid asset, and the focused rollback of the failed `83239b...` HumanPose semantic-forward experiment.

Accepted Motion Engine baseline:

`f0c81e84d0a482c40448505f2904af93ef4aa881` — Phase 4 implementation, **USER ACCEPTED — PASS**.

Phase 5 has **STARTED**. Phase 5A — Embodied Hybrid Locomotion Prototype — is implemented and **NOT USER ACCEPTED**. The long-lived engine branch remains authoritative for the core-engine train; do not merge to `main` unless the USER explicitly changes that workflow.

The previous Web Orchestrator conversation was intentionally retired because the Phase 4 debugging thread became long and hypothesis-heavy. The USER explicitly wants the new Orchestrator to inspect the repository itself before deciding on a solution.

## Accepted history

- Phase 1 CPU pose spike: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6` — accepted PASS WITH NOTES.
- Phase 2 canonical skeleton/debug visualization: `f5a15648607adf6034800c6a2b4d685b0e6f03ea` — accepted PASS WITH NOTES.
- Phase 3 calibration/confidence/smoothing: `2ee4d6eb606a8b845183cc44126ecf9530d8280b` — accepted PASS WITH NOTES.
- Phase 4 handoff-doc checkpoint: `4a26589ec2f90688b80fb6b1da0b849adda65d6b` — historical investigation checkpoint, not accepted.
- Phase 4 final implementation: `f0c81e84d0a482c40448505f2904af93ef4aa881` — **USER ACCEPTED — PASS**.

The Motion Engine intentionally remains on the long-lived `engine/pose-tracking-spike` branch through the core-engine train. Intermediate engine phases are not mechanically merged into `main`.

## Product and technical rules

- Unity `6000.5.0f1`, URP `17.5.0`.
- CPU-first; no discrete GPU requirement.
- Integrated laptop webcam baseline.
- MediaPipe Pose Landmarker V1, one person, local CPU inference.
- No face tracking, finger tracking, or segmentation in V1 unless later justified.
- Partial-body tracking is valid; missing legs must not disable usable upper-body control.
- MediaPipe must remain isolated behind a replaceable provider boundary.
- Engine-owned canonical skeleton is the downstream contract.
- Preserve avatar-authored proportions.
- **POSE != LOCOMOTION**.
- Motion Engine must remain independently testable/replaceable after the full game is built.
- Retain a permanent Motion Engine Lab/debug harness.

## What Phase 4 currently contains

The correction keeps the accepted/upstream Motion Engine boundaries and simplifies the production retarget path:

```text
MediaPipe provider
→ canonical pose
→ stabilization
→ calibration
→ canonical positional chain vectors
→ explicit signed canonical-to-avatar basis map
→ avatar-world positional targets
→ analytic two-bone IK
→ explicit / Animator Humanoid binding
→ procedural debug rig
```

`CanonicalRotationFrame` remains available for diagnostics/future orientation work, but the production limb mapping no longer depends on per-chain quaternion characterization or moving parent-frame quaternions. The signed source basis records handedness explicitly. Under the corrected unmirrored front-camera convention, calibration should produce a proper source basis (`R≈-X, U≈+Y, F≈-Z`, handedness +1); reflected bases remain representable for diagnostics/generic math but are not the intended frontal production baseline.

The coordinate/presentation correction is retained. **2D presentation, modular calibration, procedural F3/F5 retargeting, Animator Humanoid binding/limb response, real-avatar facing, and torso yaw have all passed USER QA. Phase 4 is accepted.**

## Phase 4 failure history, condensed

1. Initial direct source-rotation → target-bone retargeting produced obvious orientation/inversion errors.
2. Generic bind-axis reconciliation fixed some visible cases but not the general articulated pose.
3. The limb path was revised to four positional chains with analytic two-bone IK.
4. Current-parent-space/per-chain reference mapping was added after USER QA showed generated targets could be self-consistent while the actual pose still differed from F3.
5. Coordinate/camera foundation work then found inconsistent 2D/3D/presentation behavior. Several transformations were revised.
6. Latest pre-correction USER evidence showed F3 substantially better/upright and viewer-side-correct, and the 2D skeleton human-shaped/aligned, but the visible webcam preview still horizontally mirrored.
7. Repository audit traced the preview issue to an extra front-facing presentation heuristic, separate from MediaPipe inference preparation.
8. Earlier investigation treated `Right=+X, Up=+Y, Forward=-Z` as the frontal source reference and therefore as reflected. Fresh source-semantics audit corrected that premise: +X is viewer-right, while a front-facing user's anatomical Right is approximately -X. Calibration must therefore use `Forward=Cross(Right, Up)`, yielding approximately `R=-X, U=+Y, F=-Z` and handedness +1.
9. The correction removes that production mapping in favor of explicit signed-axis vector conversion.
10. USER QA subsequently passed the corrected upright/unmirrored 2D presentation at `d73b01b0915da56cb3815082f12b5aaea65266d4`.
11. Calibration then blocked at the old `AwaitingTPose 0%` gate, so the USER/Orchestrator approved replacing hard T-pose recognition with modular body-reference and independent chain measurements before F5 QA resumes.

The important lesson is not to continue stacking fixes from this history. Inspect the current code and establish the actual coordinate/presentation/retarget behavior from first principles.

## First files/subsystems to inspect

At minimum inspect the current implementations around:

- `MediaPipePoseProvider`
- `CameraOrientationState`
- `PoseObservation`
- `MediaPipeCanonicalPoseMapper`
- `CanonicalCoordinateSystem`
- `MediaPipeCanonicalPoseSource`
- `MotionEngineRuntime`
- `PoseTrackingSpikePresenter`
- `CanonicalKinematicTargetBuilder`
- canonical rotation solver/frame
- analytic two-bone IK implementation
- `HumanoidRigBinding`
- `HumanoidRetargeter`
- `ProceduralDebugHumanoidRig`
- `ProceduralDebugRigView`
- relevant Editor tests

Trace coordinate spaces explicitly from camera pixels through inference, normalized landmarks, world landmarks, canonical positions, F3, targets, and actual rig transforms. Do not assume an H/V flip used for pixel transport is automatically a metric-world reflection.

## Latest USER-visible state

At the checkpoint:

- Webcam/2D presentation: **USER QA PASSED**.
- Modular calibration: **USER QA PASSED**.
- Procedural F3/F5 retarget: **USER QA PASSED**.
- Real Neko Animator Humanoid binding and limb responsiveness: **PASS**.
- Real Animator Humanoid body-forward/facing orientation: **PASS** after the source-semantics/body-basis correction.
- NekoLegends `android01.fbx` binds successfully and its limb responsiveness matches the procedural path.
- Initial Neko test at root Y=0 exposed the orientation problem.
- `83239b00e891f7e8273e1449a26a6030f68334df` used `HumanPose.bodyRotation` as a semantic-forward experiment; fresh USER QA showed the same relative problem, so the experiment is rejected and removed.
- Rotating Neko root to Y=180 while that failed experiment was active merely flipped the avatar and did not solve the relative issue.
- Root Y=0 versus Y=180 is no longer being treated as an explanatory fix: rotating the root rotates the target anatomy/reference basis/bind rotations together and preserves the relative mismatch.
- Repository anatomy evidence supports Neko target Forward≈+Z from both torso basis and foot/toe geometry.
- F6 runtime evidence then localized the source-side defect: during a known physical right-shoulder-toward-camera turn, semantic `LeftShoulder`/`LeftHip` became the near side. Neutral source Right was correspondingly approximately -X and source Forward approximately +Z.
- Repository tracing found front-facing status was the sole trigger for a literal horizontal inference pixel mirror via `ImageTransformationOptions.Build(... shouldFlipHorizontally:true ...)` → `TextureFrame.ReadTextureAsync(... flipHorizontally:true ...)`.
- The focused correction keeps that automatic inference H mirror removed while preserving vertical/rotation transport and unmirrored presentation.
- The Orchestrator then identified the remaining basis-order error: after semantic Left/Right is fixed, anatomical Right is approximately -X for a front-facing subject, so the old `Cross(Up, Right)` still yields +Z. Calibration is corrected to `Cross(Right, Up)`, consistent with the rotation solver and frontal -Z convention.
- Canonical mapper, signed-axis architecture, target/avatar basis, Humanoid binding, and IK remain unchanged.
- F6 remains available for future regression diagnosis.
- Final USER QA confirmed the Neko avatar faces correctly and follows approximately 45° left/right torso yaw in the correct direction without avatar-side compensation.

The committed Lab scene now serializes the accepted Neko `android01` child and Animator Humanoid binding used for final QA. A second machine receives the runtime code, Neko assets, and Lab wiring from Git.

## Tests and tooling

Luna repeatedly source-compiled Phase 4 production and Editor tests successfully, but Unity Test Runner execution was frequently blocked by an already-open Editor/licensing channel. Treat Phase 4 test methods as **present**, not necessarily Unity-executed/passing.

Unity MCP/Pipeline are development tooling only. Use them if they materially improve inspection, but do not make them runtime dependencies.

## Repository hygiene

The checkpoint includes `GoldenNeedle.slnx` and `ProjectSettings/ProjectSettings.asset` changes that had repeatedly been described as pre-existing/unintended editor state. Inspect these before any next accepted checkpoint. Do not silently bless them.

## Recommended next task

Phase 4 is accepted and frozen. Next USER-QA Phase 5A should first verify the correction: planted-feet torso lean/bend/twist must not translate; actual support relocation must still move laterally/depth/diagonally in the already-passed directions; jogging in place must remain physical-stationary while cadence activates; temporary support loss must hold; reduced scale feel and K recenter must be checked. Then continue heading/stop/double-counting checks.

The root estimator intentionally does not consume canonical pelvis/world positions as absolute room coordinates and no longer uses torso center as room-position authority. Cadence remains lower-body-only. Locomotion changes only bound avatar-root X/Z; root Y/rotation and Phase 4 bone retargeting stay outside Phase 5A authority.

Do not begin Phase 6 and do not merge until the USER explicitly approves the Phase 5 checkpoint.

## Governance

The USER primarily uses GitHub Desktop for Git mutations. Do not merge without explicit USER approval. Continue the core-engine train on `engine/pose-tracking-spike` unless the USER explicitly changes that workflow. Phase 4 is accepted; Phase 5 may start.



## Phase 5A implementation snapshot

New core modules:

- `CameraSpaceRootTracker` — relative camera-space physical displacement from ankle/heel/toe support common mode, with differential gait evidence reducing depth trust and torso scale used only as auxiliary depth corroboration/normalization.
- `CadenceDetector` — alternating ankle/knee lower-body rhythm, cadence rate/confidence, fast stop.
- `BodyHeadingEstimator` — maps live source torso Forward through the accepted Phase 4 signed map into avatar/game-world heading.
- `LocomotionFusion` — independent lateral/depth scaling, accepted Phase 4 reference-map conversion from camera X/Z to game-world X/Z, plus mapped physical-velocity suppression of cadence.
- `EmbodiedLocomotionController` — persistent virtual origin, X/Z root application, public no-jump recenter.
- `LocomotionPrototypeView` — runtime-only fixed grid/world-reference viewport.

Existing F1–F6/R/C/X controls remain. **K** invokes the same public `Recenter()` action reserved for a future discrete voice command.


### Phase 5A first-QA correction notes

- Support disagreement (including alternating jog/step cycling) is a hold condition, not a torso fallback.
- Temporary support loss holds the last trusted physical contribution through the existing fusion behavior.
- Prototype physical scale defaults: lateral `0.9`, depth `1.5`.
- `PoseTrackingSpike.unity` now serializes `EmbodiedLocomotionController`; presenter runtime creation is fallback-only.
