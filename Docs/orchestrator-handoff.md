# Orchestrator handoff

## Project

**Golden Needle** — SIH 2026 gamified embodied fitness.

Core proposition: a webcam tracks continuous full-body movement and drives a humanoid avatar. Locomotion is a separate interpreted system.

## Start here

Repository: `AltamashM7/GoldenNeedle`

Current branch: `engine/pose-tracking-spike`

Phase 4 handoff HEAD before the correction:

`4a26589ec2f90688b80fb6b1da0b849adda65d6b` — `docs: hand off phase 4 investigation state`

Its runtime parent is `5e830dce7ac3de542ab159b8b90992935d9dd0b0`. The branch contains the Phase 4 coordinate/retarget correction, modular calibration redesign, passed procedural harness, Neko Animator Humanoid asset, and the focused rollback of the failed `83239b...` HumanPose semantic-forward experiment. Phase 4 still awaits final real-avatar orientation QA and Orchestrator audit.

Accepted Motion Engine baseline:

`2ee4d6eb606a8b845183cc44126ecf9530d8280b` — Phase 3, USER ACCEPTED — PASS WITH NOTES.

Phase 4 is **NOT ACCEPTED**. Phase 5 has **NOT STARTED**. Do not create/merge a Phase 4 PR into `main`.

The previous Web Orchestrator conversation was intentionally retired because the Phase 4 debugging thread became long and hypothesis-heavy. The USER explicitly wants the new Orchestrator to inspect the repository itself before deciding on a solution.

## Accepted history

- Phase 1 CPU pose spike: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6` — accepted PASS WITH NOTES.
- Phase 2 canonical skeleton/debug visualization: `f5a15648607adf6034800c6a2b4d685b0e6f03ea` — accepted PASS WITH NOTES.
- Phase 3 calibration/confidence/smoothing: `2ee4d6eb606a8b845183cc44126ecf9530d8280b` — accepted PASS WITH NOTES.
- Phase 4 handoff-doc checkpoint: `4a26589ec2f90688b80fb6b1da0b849adda65d6b` — **not accepted**.
- Phase 4 correction after that handoff — **awaiting USER QA / Orchestrator audit; not accepted**.

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

`CanonicalRotationFrame` remains available for diagnostics/future orientation work, but the production limb mapping no longer depends on per-chain quaternion characterization or moving parent-frame quaternions. The source semantic basis is allowed to be reflected; reflections are represented explicitly rather than hidden inside `Quaternion` composition.

The coordinate/presentation correction direction is retained. **2D PRESENTATION QA has PASSED** at `d73b01b0915da56cb3815082f12b5aaea65266d4`. Phase 4 as a whole is still not accepted; modular calibration and procedural retarget QA remain.

## Phase 4 failure history, condensed

1. Initial direct source-rotation → target-bone retargeting produced obvious orientation/inversion errors.
2. Generic bind-axis reconciliation fixed some visible cases but not the general articulated pose.
3. The limb path was revised to four positional chains with analytic two-bone IK.
4. Current-parent-space/per-chain reference mapping was added after USER QA showed generated targets could be self-consistent while the actual pose still differed from F3.
5. Coordinate/camera foundation work then found inconsistent 2D/3D/presentation behavior. Several transformations were revised.
6. Latest pre-correction USER evidence showed F3 substantially better/upright and viewer-side-correct, and the 2D skeleton human-shaped/aligned, but the visible webcam preview still horizontally mirrored.
7. Repository audit traced the preview issue to an extra front-facing presentation heuristic, separate from MediaPipe inference preparation.
8. Repository audit also found the source semantic basis can be reflected (`Right=+X, Up=+Y, Forward=-Z` in the reference case), while the production mapping attempted to encode it through quaternions and forward-hemisphere compensation.
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
- Real Animator Humanoid body-forward/facing orientation: **UNRESOLVED**.
- NekoLegends `android01.fbx` binds successfully and its limb responsiveness matches the procedural path.
- Initial Neko test at root Y=0 exposed the orientation problem.
- `83239b00e891f7e8273e1449a26a6030f68334df` used `HumanPose.bodyRotation` as a semantic-forward experiment; fresh USER QA showed the same relative problem, so the experiment is rejected and removed.
- Rotating Neko root to Y=180 while that failed experiment was active merely flipped the avatar and did not solve the relative issue.
- The next untested combination is **restored pre-HumanPose production behavior + Neko root Y=180**, chosen to match the procedural Lab's authored facing convention. Do not treat it as proven.

Do not judge the real avatar path until the tracking/presentation foundation and procedural acceptance harness are trustworthy.

## Tests and tooling

Luna repeatedly source-compiled Phase 4 production and Editor tests successfully, but Unity Test Runner execution was frequently blocked by an already-open Editor/licensing channel. Treat Phase 4 test methods as **present**, not necessarily Unity-executed/passing.

Unity MCP/Pipeline are development tooling only. Use them if they materially improve inspection, but do not make them runtime dependencies.

## Repository hygiene

The checkpoint includes `GoldenNeedle.slnx` and `ProjectSettings/ProjectSettings.asset` changes that had repeatedly been described as pre-existing/unintended editor state. Inspect these before any next accepted checkpoint. Do not silently bless them.

## Recommended next task

Audit the exact pushed correction head, then use USER QA as the gate:

1. Use the restored exact pre-HumanPose retarget behavior and set Neko root Y=180 in the USER's local Lab wiring.
2. Recheck neutral facing, one asymmetric arm pose, and moderate yaw.
3. Confirm whether facing now matches F3/procedural convention without regressing the already-passing limb side/response.
4. This exact combination is untested; if it passes, perform the final Phase 4 Orchestrator audit.
5. Keep Phase 4 unaccepted and Phase 5 unstarted until the USER explicitly approves.

## Governance

The USER primarily uses GitHub Desktop for Git mutations. Do not merge without explicit USER approval. Keep Phase 4 on the engine branch. Do not start Phase 5 until Phase 4 has a deliberate USER acceptance decision.

