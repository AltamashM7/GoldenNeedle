# Current state

This is the concise durable snapshot. It intentionally distinguishes accepted Motion Engine checkpoints from the current Phase 4 investigation state.

## Authoritative Git state

- Branch: `engine/pose-tracking-spike`
- Phase 1 accepted SHA: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6`
- Phase 2 accepted SHA: `f5a15648607adf6034800c6a2b4d685b0e6f03ea`
- Phase 3 accepted SHA: `2ee4d6eb606a8b845183cc44126ecf9530d8280b`
- Current Phase 4 investigation checkpoint: `5e830dce7ac3de542ab159b8b90992935d9dd0b0` — `feat: checkpoint phase 4 motion retargeting investigation`
- Phase 4 is **NOT USER ACCEPTED**.
- Phase 5 has **NOT STARTED**.
- No Phase 4 PR or merge to `main` is authorized.

Phase 1, Phase 2, and Phase 3 were committed, pushed, USER accepted with verdict **PASS WITH NOTES**, and audited by Web Sol. The accepted engine baseline remains Phase 3 at `2ee4d6eb606a8b845183cc44126ecf9530d8280b`. The Phase 4 checkpoint preserves an intentionally unfinished investigative state so the next Orchestrator can inspect the actual code rather than reconstruct it from chat history.

## Phase 4 investigation status

Phase 4 attempted to move stabilized canonical pose data into a visibly driven humanoid/debug rig while preserving avatar proportions and keeping pose reproduction separate from locomotion.

The checkpoint currently contains, among other work:

- provider-independent `ICanonicalPoseSource` and `MotionEngineRuntime`;
- canonical rotation/torso reconstruction;
- four positional limb-chain targets;
- project-owned analytic two-bone IK;
- explicit and structural Animator Humanoid rig binding;
- procedural debug humanoid plus dedicated RenderTexture view;
- calibration-profile extensions for per-side reach/reference geometry;
- coordinate-space diagnostics and the permanent Motion Engine Lab controls.

These are **investigative implementations, not accepted Phase 4 architecture**. Several iterations of direct rotation mapping, bind-axis reconciliation, kinematic targets, IK, current-parent-space mapping, and camera/canonical presentation corrections were tried during USER QA.

## Latest USER QA evidence at the checkpoint

The latest visible state before this handoff is:

- F3 canonical 3D appears upright and broadly follows the correct viewer-left/viewer-right motion.
- The canonical 2D skeleton is human-shaped and aligned over the visible person.
- The visible webcam preview still appears horizontally mirrored even though display mirroring is intended to be off.
- Earlier and repeated Phase 4 USER QA showed the procedural rig failing to accurately reproduce the F3 articulated pose across multiple arm, leg, asymmetric, and side-view poses.
- Therefore the procedural retargeter remains **unaccepted** even if individual diagnostics report targets/chains/bones as valid or solved.
- Coordinate/presentation diagnostics were changed several times during investigation. They are useful observability tools but must not be treated as proof that the foundation is correct without code inspection and fresh USER QA.
- The production Animator Humanoid path has not been physically tested against the USER's real character asset.

Do not infer that the current preview/canonical/retarget formulas are correct merely because they are documented in the Phase 4 checkpoint. The new Orchestrator must inspect the implementation directly.

## Verification state

- Unity 6.5 / project version `6000.5.0f1` remains the engine baseline.
- URP `17.5.0` remains configured.
- MediaPipeUnityPlugin `0.16.3` and local Pose Landmarker Lite remain the tracking backend.
- Phase 3 physical QA passed calibration, smoothing and responsiveness with **PASS WITH NOTES**.
- Phase 4 source/test compilation was repeatedly reported successful by Luna.
- The expanding Phase 4 EditMode suites were often only **present/source-compiled**, not executed by Unity Test Runner, because another Unity Editor instance/licensing channel blocked batch execution. Do not convert those counts into passing Unity tests without rerunning them.
- Known one-off ShaderGraph editor warnings around recompilation/exit remain non-blocking unless behavior changes.

## Repository hygiene note

The Phase 4 investigation checkpoint includes changes to:

- `GoldenNeedle.slnx`
- `ProjectSettings/ProjectSettings.asset`

These had repeatedly been reported as pre-existing/unintended editor differences rather than deliberate Phase 4 product changes. Because the USER checkpointed the working state as-is, the new Orchestrator must inspect these diffs before carrying them into any future accepted checkpoint. In particular, do not silently treat them as approved architecture/settings changes.

## Locked product/architecture rules that survive Phase 4 uncertainty

- CPU-first; no required discrete GPU.
- Integrated webcam is a valid baseline.
- Partial-body tracking remains valid.
- MediaPipe stays behind a replaceable provider boundary.
- Downstream systems consume engine-owned canonical data.
- **POSE != LOCOMOTION**.
- Preserve avatar-authored proportions; do not scale bones to match USER limb lengths.
- Motion Engine systems must remain modular, independently testable, and inspectable after the game is complete.
- The Motion Engine Lab/debug scene is permanent engineering infrastructure, not disposable spike code.
- Courses/Hub/gameplay must not depend on MediaPipe internals.
- Phase 5 locomotion must not start until Phase 4 is deliberately resolved/accepted.

## Next action for a fresh Web Orchestrator

Start with a **repository-first read-only diagnosis**.

Before directing Luna to modify anything:

1. Verify branch/head and compare `2ee4d6eb606a8b845183cc44126ecf9530d8280b` to `5e830dce7ac3de542ab159b8b90992935d9dd0b0`.
2. Read `AGENTS.md`, this file, `Docs/orchestrator-handoff.md`, `Docs/decisions.md`, `Docs/architecture.md`, and `Docs/motion-engine.md`.
3. Inspect the actual current code paths for:
   - camera capture/input preparation;
   - preview presentation;
   - normalized-landmark → canonical 2D mapping;
   - world-landmark → canonical 3D mapping;
   - F3 projection;
   - stabilization/calibration reset/version behavior;
   - kinematic-target construction;
   - torso/parent frames;
   - analytic IK;
   - Humanoid binding and procedural rig presentation.
4. Reproduce the unresolved preview mirror and retarget mismatch from code/runtime evidence before proposing another correction.
5. Prefer simplifying/re-establishing a correct foundation over adding more compensating flips/quaternion patches.
6. Do not begin Phase 5 and do not merge Phase 4 into `main`.

