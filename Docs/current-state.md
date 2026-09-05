# Current state

This is the concise durable snapshot. It intentionally distinguishes accepted Motion Engine checkpoints from the current Phase 4 investigation state.

## Authoritative Git state

- Branch: `engine/pose-tracking-spike`
- Phase 1 accepted SHA: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6`
- Phase 2 accepted SHA: `f5a15648607adf6034800c6a2b4d685b0e6f03ea`
- Phase 3 accepted SHA: `2ee4d6eb606a8b845183cc44126ecf9530d8280b`
- Phase 4 runtime investigation parent: `5e830dce7ac3de542ab159b8b90992935d9dd0b0`.
- Phase 4 handoff-doc HEAD before the correction: `4a26589ec2f90688b80fb6b1da0b849adda65d6b` — `docs: hand off phase 4 investigation state`.
- The branch contains the Phase 4 coordinate/retarget correction, modular calibration redesign, procedural retarget validation, Neko Animator Humanoid asset, and the rollback of the failed HumanPose semantic-forward experiment. Phase 4 remains **awaiting USER QA and Orchestrator audit**.
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
- modular body-reference plus independent per-chain calibration geometry;
- coordinate-space diagnostics and the permanent Motion Engine Lab controls.

The coordinate/retarget correction keeps the provider/canonical/stabilization boundaries, positional targets, analytic IK, rig binding, and debug harness. Production limb mapping uses the explicit signed canonical-to-avatar basis map with the immutable pre-HumanPose target-reference behavior. 2D presentation, modular calibration, and procedural F3/F5 retargeting have now passed USER QA. The real Animator Humanoid path binds and drives limbs correctly, but its facing/orientation remains unresolved. Phase 4 is **still not accepted** until that real-avatar orientation gate is deliberately resolved and audited.

## Latest USER QA evidence at the checkpoint

The latest visible state before this handoff is:

- **2D PRESENTATION QA — PASSED** at `d73b01b0915da56cb3815082f12b5aaea65266d4`.
- **MODULAR CALIBRATION QA — PASSED** after the version-5 body-reference/per-chain redesign.
- **PROCEDURAL F3/F5 RETARGET QA — PASSED** on the explicit/procedural rig.
- **ANIMATOR HUMANOID BINDING — PASSED** with NekoLegends `android01.fbx`.
- **ANIMATOR HUMANOID LIMB RESPONSE — PASSED**: correct side and sensible limb response remain consistent with the procedural path.
- **REAL-AVATAR BODY/FACING ORIENTATION — UNRESOLVED**: the original Neko test at root Y=0 showed the relative facing/orientation problem.
- Commit `83239b00e891f7e8273e1449a26a6030f68334df` attempted to disambiguate Animator Forward from `HumanPose.bodyRotation`; fresh USER QA showed the same relative orientation problem, so that experiment is rejected and removed.
- During that failed HumanPose experiment the USER also rotated the Neko root to Y=180. That flipped the whole avatar but preserved the same relative orientation problem; this does **not** prove the next test combination will fail.
- Changing the Neko root between Y=0 and Y=180 has been rejected as an explanatory fix by itself: it rotates anatomy, target basis, and bind rotations together while the relative facing mismatch remains.
- Neko target anatomy is internally coherent: bind shoulders/chest imply target Right≈+X, Up≈+Y, Forward≈+Z, and independent foot/toe geometry also points toward +Z. Target Forward is therefore not being changed in the current investigation.
- F6 runtime evidence localized the earliest proven inversion to the front-camera source boundary: with the USER's physical right shoulder moved toward the webcam, semantic `LeftShoulder` and `LeftHip` became the near/depth-smaller side while semantic right became farther. Neutral source Right correspondingly pointed approximately -X and `Cross(Up, Right)` produced approximately +Z Forward.
- The provider was still passing `_selectedDevice.isFrontFacing` as `shouldFlipHorizontally` into `ImageTransformationOptions.Build`. The embedded `TextureFrame.ReadTextureAsync` performs that flag as a literal horizontal pixel mirror before MediaPipe inference, while the canonical mapper preserves MediaPipe anatomical IDs directly.
- The source correction keeps the automatic front-camera inference H mirror removed. Front-facing remains metadata; inference still applies required vertical/rotation transport correction. A follow-up Orchestrator audit found one remaining source-basis error: Golden Needle +X is **viewer/camera right**, not the front-facing user's anatomical right. In an unmirrored frontal view anatomical Right is therefore approximately -X, so calibration must derive Forward with `Cross(Right, Up)`, not `Cross(Up, Right)`.
- Neko target anatomy remains independently coherent with Forward≈+Z from torso and foot/toe evidence; root-rotation and HumanPose experiments remain rejected.
- With corrected unmirrored frontal semantics, the expected neutral source basis is anatomical `R≈-X`, `U≈+Y`, `F≈-Z`. Because `F = Cross(R, U)`, the calibrated source basis is expected to be proper/handedness +1 rather than the previously reflected -1 case. The signed-axis architecture remains capable of representing signed bases, but production calibration no longer forces the old reflected premise.
- F6 diagnostics are retained as runtime regression evidence for corrected semantic Right/Forward/yaw.
- Coordinate/presentation diagnostics remain observability tools rather than proof of correctness.

Do not infer that the correction is visually correct merely because the signed-axis math is internally consistent. USER visual/motion QA remains the decisive gate, followed by Orchestrator audit.

## Verification state

- Unity 6.5 / project version `6000.5.0f1` remains the engine baseline.
- URP `17.5.0` remains configured.
- MediaPipeUnityPlugin `0.16.3` and local Pose Landmarker Lite remain the tracking backend.
- Phase 3 physical QA historically passed the then-current T-pose calibration, smoothing and responsiveness with **PASS WITH NOTES**; Phase 4 now deliberately supersedes the hard T-pose calibration architecture.
- Phase 4 2D presentation QA has **PASSED** at `d73b01b0915da56cb3815082f12b5aaea65266d4`.
- The pre-correction Phase 4 source/test compilation was repeatedly reported successful by Luna.
- The `83239b...` HumanPose semantic-forward experiment **FAILED USER QA** and has been deliberately rolled back from production code/tests.
- F6 completed the diagnostic localization: front-camera inference H mirroring reversed anatomical semantics before canonical mapping. A focused source-boundary correction is now awaiting USER runtime QA.
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

1. Re-run the previously passed webcam/2D presentation check with display mirror OFF: upright, unmirrored preview and raw/canonical/stabilized overlays aligned to the physical user.
2. Calibrate normally and open F6. Neutral source basis should move toward anatomical Right≈-X, Up≈+Y, Forward≈-Z, with source handedness approximately +1.
3. Repeat the controlled turn with the USER's physical right shoulder toward the webcam. Semantic `RightShoulder` and `RightHip` must now be the near/depth-smaller side.
4. If source semantics pass, enable F5 and confirm procedural retargeting still behaves as before, then recheck Neko facing/yaw without changing avatar-side code.
5. Do not begin Phase 5 and do not merge Phase 4 into `main` until explicit USER acceptance.

