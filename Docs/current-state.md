# Current state

This is the concise durable snapshot. It intentionally distinguishes accepted Motion Engine checkpoints from the current Phase 4 investigation state.

## Authoritative Git state

- Branch: `engine/pose-tracking-spike`
- Phase 1 accepted SHA: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6`
- Phase 2 accepted SHA: `f5a15648607adf6034800c6a2b4d685b0e6f03ea`
- Phase 3 accepted SHA: `2ee4d6eb606a8b845183cc44126ecf9530d8280b`
- Phase 4 runtime investigation parent: `5e830dce7ac3de542ab159b8b90992935d9dd0b0`.
- Phase 4 handoff-doc HEAD before the correction: `4a26589ec2f90688b80fb6b1da0b849adda65d6b` — `docs: hand off phase 4 investigation state`.
- The branch now contains a Phase 4 coordinate/retarget correction **awaiting USER QA and Orchestrator audit**.
- Phase 4 is **NOT USER ACCEPTED**.
- Phase 5 has **NOT STARTED**.
- No Phase 4 PR or merge to `main` is authorized.

Phase 1, Phase 2, and Phase 3 were committed, pushed, USER accepted with verdict **PASS WITH NOTES**, and audited by Web Sol. The accepted engine baseline remains Phase 3 at `2ee4d6eb606a8b845183cc44126ecf9530d8280b`. The Phase 4 handoff preserved the unfinished investigative state. The subsequent correction intentionally keeps the accepted Phase 3 canonical/stabilization foundation while replacing the handedness-sensitive retarget mapping and the unintended front-camera presentation heuristic. It remains unaccepted until fresh USER QA.

## Phase 4 correction status

Phase 4 moves stabilized canonical pose data into a visibly driven humanoid/debug rig while preserving avatar proportions and keeping pose reproduction separate from locomotion.

The branch currently contains, among other work:

- provider-independent `ICanonicalPoseSource` and `MotionEngineRuntime`;
- canonical rotation/torso reconstruction;
- four positional limb-chain targets;
- project-owned analytic two-bone IK;
- explicit and structural Animator Humanoid rig binding;
- procedural debug humanoid plus dedicated RenderTexture view;
- calibration-profile extensions for per-side reach/reference geometry;
- coordinate-space diagnostics and the permanent Motion Engine Lab controls.

The latest correction keeps the provider/canonical/stabilization/calibration boundaries, per-side reach, positional targets, analytic IK, rig binding, and debug harness. It replaces production current-parent/per-chain quaternion characterization with one explicit signed canonical-to-avatar basis map. The avatar target basis is cached from bind/reference geometry instead of recomputed from already-driven transforms, and 2D debug overlays explicitly inverse inference preparation before entering display space. This is **still not accepted Phase 4 architecture** until USER QA and Orchestrator audit pass.

## Latest USER QA evidence at the checkpoint

The latest visible state before this handoff is:

- F3 canonical 3D appears upright and broadly follows the correct viewer-left/viewer-right motion.
- The canonical 2D skeleton is human-shaped and aligned over the visible person.
- USER QA at `0eda91393bd94f9f621b8d5376df1b507207312f` confirmed the webcam is upright/unmirrored, F3 is upright, and internal 2D↔3D X/Y agreement reports PASS. That QA also exposed one presentation-only regression: the 2D overlay was vertically inverted. The follow-up correction restores the single display-normalized-to-IMGUI Y inversion while preserving the horizontal fix.
- Earlier and repeated Phase 4 USER QA showed the procedural rig failing to accurately reproduce the F3 articulated pose across multiple arm, leg, asymmetric, and side-view poses.
- Therefore the procedural retargeter remains **unaccepted** even if individual diagnostics report targets/chains/bones as valid or solved.
- Coordinate/presentation diagnostics were changed several times during investigation. They are useful observability tools but must not be treated as proof that the foundation is correct without code inspection and fresh USER QA.
- The production Animator Humanoid path has not been physically tested against the USER's real character asset.

Do not infer that the correction is visually correct merely because the signed-axis math is internally consistent. USER visual/motion QA remains the decisive gate, followed by Orchestrator audit.

## Verification state

- Unity 6.5 / project version `6000.5.0f1` remains the engine baseline.
- URP `17.5.0` remains configured.
- MediaPipeUnityPlugin `0.16.3` and local Pose Landmarker Lite remain the tracking backend.
- Phase 3 physical QA passed calibration, smoothing and responsiveness with **PASS WITH NOTES**.
- The pre-correction Phase 4 source/test compilation was repeatedly reported successful by Luna.
- This correction was produced repository-first; targeted source/math checks are documented in the handoff, but USER Unity QA is still required.
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

## Next action

Perform fresh USER QA on the correction, then have the Web Orchestrator audit the exact pushed head.

The highest-value QA is:

1. Recheck the upright/unmirrored webcam with the 2D skeleton and confirm both horizontal and vertical overlay registration.
2. Only after that presentation check passes, recalibrate and compare F3 against the procedural rig across neutral/T-pose, asymmetric arms, bent arms, raised/bent legs, depth motion, and large body yaw/side views.
3. Keep F5 target/hint markers visible when diagnosing any remaining mismatch.
4. If the procedural path passes, physically test one real Animator Humanoid asset before Phase 4 acceptance.
5. Do not begin Phase 5 and do not merge Phase 4 into `main` until explicit USER acceptance.

