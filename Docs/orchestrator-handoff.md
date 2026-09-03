# Orchestrator handoff

## Project

**Golden Needle** — SIH 2026 gamified embodied fitness.

Core proposition: a webcam tracks continuous full-body movement and drives a humanoid avatar. Locomotion is a separate interpreted system.

## Start here

Repository: `AltamashM7/GoldenNeedle`

Current branch: `engine/pose-tracking-spike`

Current branch checkpoint:

`5e830dce7ac3de542ab159b8b90992935d9dd0b0` — `feat: checkpoint phase 4 motion retargeting investigation`

Accepted Motion Engine baseline:

`2ee4d6eb606a8b845183cc44126ecf9530d8280b` — Phase 3, USER ACCEPTED — PASS WITH NOTES.

Phase 4 is **NOT ACCEPTED**. Phase 5 has **NOT STARTED**. Do not create/merge a Phase 4 PR into `main`.

The previous Web Orchestrator conversation was intentionally retired because the Phase 4 debugging thread became long and hypothesis-heavy. The USER explicitly wants the new Orchestrator to inspect the repository itself before deciding on a solution.

## Accepted history

- Phase 1 CPU pose spike: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6` — accepted PASS WITH NOTES.
- Phase 2 canonical skeleton/debug visualization: `f5a15648607adf6034800c6a2b4d685b0e6f03ea` — accepted PASS WITH NOTES.
- Phase 3 calibration/confidence/smoothing: `2ee4d6eb606a8b845183cc44126ecf9530d8280b` — accepted PASS WITH NOTES.
- Phase 4 current investigative checkpoint: `5e830dce7ac3de542ab159b8b90992935d9dd0b0` — **not accepted**.

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

The unaccepted checkpoint contains substantial exploratory work:

```text
MediaPipe provider
→ canonical pose
→ stabilization
→ calibration
→ canonical torso/rotation output
→ positional kinematic targets
→ per-chain characterization
→ analytic two-bone IK
→ explicit / Animator Humanoid binding
→ procedural debug rig
```

It also contains repeated coordinate/presentation revisions and diagnostics intended to compare canonical 2D, canonical 3D/F3, target generation, IK residual, bend plane, and the actual procedural rig.

Do **not** assume this entire pipeline is the correct final Phase 4 architecture merely because the code exists.

## Phase 4 failure history, condensed

1. Initial direct source-rotation → target-bone retargeting produced obvious orientation/inversion errors.
2. Generic bind-axis reconciliation fixed some visible cases but not the general articulated pose.
3. The limb path was revised to four positional chains with analytic two-bone IK.
4. Current-parent-space/per-chain reference mapping was added after USER QA showed generated targets could be self-consistent while the actual pose still differed from F3.
5. Coordinate/camera foundation work then found inconsistent 2D/3D/presentation behavior. Several transformations were revised.
6. Latest USER evidence shows F3 substantially better/upright and viewer-side-correct, and the 2D skeleton human-shaped/aligned, but the visible webcam preview still appears horizontally mirrored.
7. Across earlier multi-pose QA sets, the procedural rig still failed to accurately reproduce F3. It remains unaccepted.

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

- F3: appears upright and viewer-side-correct.
- Canonical 2D: human-shaped and aligned over the visible person.
- Webcam preview: still appears horizontally mirrored despite intended display mirror being off.
- Procedural rig: not accepted; prior multi-pose QA showed large articulated mismatches versus F3.
- The USER has a real humanoid character asset available for later import, but it has not yet been used to validate the structural Animator Humanoid binding.

Do not judge the real avatar path until the tracking/presentation foundation and procedural acceptance harness are trustworthy.

## Tests and tooling

Luna repeatedly source-compiled Phase 4 production and Editor tests successfully, but Unity Test Runner execution was frequently blocked by an already-open Editor/licensing channel. Treat Phase 4 test methods as **present**, not necessarily Unity-executed/passing.

Unity MCP/Pipeline are development tooling only. Use them if they materially improve inspection, but do not make them runtime dependencies.

## Repository hygiene

The checkpoint includes `GoldenNeedle.slnx` and `ProjectSettings/ProjectSettings.asset` changes that had repeatedly been described as pre-existing/unintended editor state. Inspect these before any next accepted checkpoint. Do not silently bless them.

## Recommended first task

Perform a read-only repository audit against the actual Phase 4 checkpoint and answer:

1. What exact orientation does MediaPipe see?
2. What exact orientation does the preview display?
3. What spaces do normalized and world landmarks occupy in the current code?
4. Why is the preview still horizontally mirrored?
5. Does F3 project the same canonical data retargeting consumes?
6. Which current Phase 4 retarget pieces are mathematically sound and which are compensating for earlier coordinate mistakes?
7. What is the smallest clean correction/rewrite that restores a trustworthy foundation?

Only after that audit should a new Luna implementation brief be written.

## Governance

The USER primarily uses GitHub Desktop for Git mutations. Do not merge without explicit USER approval. Keep Phase 4 on the engine branch. Do not start Phase 5 until Phase 4 has a deliberate USER acceptance decision.

