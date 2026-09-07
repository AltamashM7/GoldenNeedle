# Current state

This is the concise durable snapshot of the accepted Motion Engine through Phase 4 and the next-stage handoff.

## Authoritative Git state

- Branch: `engine/pose-tracking-spike`
- Phase 1 accepted SHA: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6`
- Phase 2 accepted SHA: `f5a15648607adf6034800c6a2b4d685b0e6f03ea`
- Phase 3 accepted SHA: `2ee4d6eb606a8b845183cc44126ecf9530d8280b`
- Phase 4 runtime investigation parent: `5e830dce7ac3de542ab159b8b90992935d9dd0b0`.
- Phase 4 handoff-doc HEAD before the correction: `4a26589ec2f90688b80fb6b1da0b849adda65d6b` — `docs: hand off phase 4 investigation state`.
- Phase 4 accepted implementation SHA: `f0c81e84d0a482c40448505f2904af93ef4aa881` — `fix: correct front-camera body basis handedness`.
- The branch contains the accepted Phase 4 coordinate/source correction, modular calibration redesign, procedural retarget validation, Neko Animator Humanoid asset, and the rollback of the failed HumanPose semantic-forward experiment.
- Phase 4 is **USER ACCEPTED — PASS**.
- Phase 5 has **STARTED**. Phase 5A — Embodied Hybrid Locomotion Prototype — is implemented on the engine branch and is **NOT USER ACCEPTED**.
- No Phase 4 PR or merge to `main` is authorized.

Phase 1, Phase 2, and Phase 3 were committed, pushed, USER accepted with verdict **PASS WITH NOTES**, and audited by Web Sol. Phase 4 is now also USER accepted, with accepted implementation SHA `f0c81e84d0a482c40448505f2904af93ef4aa881`. The final Phase 4 correction preserves the accepted Phase 3 canonical/stabilization foundation while fixing front-camera anatomical semantics and body-basis handedness, then driving both the procedural rig and a real Animator Humanoid successfully.

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

The coordinate/retarget correction keeps the provider/canonical/stabilization boundaries, positional targets, analytic IK, rig binding, and debug harness. Production limb mapping uses the explicit signed canonical-to-avatar basis map with the immutable pre-HumanPose target-reference behavior. 2D presentation, modular calibration, procedural F3/F5 retargeting, Animator Humanoid binding/limb response, real-avatar facing, and left/right torso yaw have all passed USER QA. Phase 4 is **USER ACCEPTED — PASS**.

## Latest USER QA evidence at the checkpoint

The latest visible state before this handoff is:

- **2D PRESENTATION QA — PASSED** at `d73b01b0915da56cb3815082f12b5aaea65266d4`.
- **MODULAR CALIBRATION QA — PASSED** after the version-5 body-reference/per-chain redesign.
- **PROCEDURAL F3/F5 RETARGET QA — PASSED** on the explicit/procedural rig.
- **ANIMATOR HUMANOID BINDING — PASSED** with NekoLegends `android01.fbx`.
- **ANIMATOR HUMANOID LIMB RESPONSE — PASSED**: correct side and sensible limb response remain consistent with the procedural path.
- **REAL-AVATAR BODY/FACING ORIENTATION — PASSED** after the accepted source-semantics/body-basis correction; the Neko avatar now faces correctly without an avatar-side orientation workaround.
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
- Final USER QA confirmed correct real-avatar orientation and approximately 45° left/right torso yaw direction/stability.
- Coordinate/presentation diagnostics remain observability tools; final acceptance came from USER visual/motion QA, not diagnostics alone.

## Verification state

- Unity 6.5 / project version `6000.5.0f1` remains the engine baseline.
- URP `17.5.0` remains configured.
- MediaPipeUnityPlugin `0.16.3` and local Pose Landmarker Lite remain the tracking backend.
- Phase 3 physical QA historically passed the then-current T-pose calibration, smoothing and responsiveness with **PASS WITH NOTES**; Phase 4 now deliberately supersedes the hard T-pose calibration architecture.
- Phase 4 2D presentation QA has **PASSED** at `d73b01b0915da56cb3815082f12b5aaea65266d4`.
- The pre-correction Phase 4 source/test compilation was repeatedly reported successful by Luna.
- The `83239b...` HumanPose semantic-forward experiment **FAILED USER QA** and has been deliberately rolled back from production code/tests.
- F6 completed the diagnostic localization: front-camera inference H mirroring reversed anatomical semantics before canonical mapping. The focused source-boundary plus body-basis correction subsequently passed USER runtime QA.
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
- Phase 4 is frozen as accepted behavior while Phase 5 locomotion is developed separately; pose reproduction must not be redesigned to solve locomotion problems.

## Phase 5A — Embodied Hybrid Locomotion Prototype

Phase 5A is the first integrated locomotion prototype. It intentionally combines finite camera-space physical displacement with cadence-based infinite-range extension while keeping the accepted Phase 4 pose/retarget path unchanged.

Implemented separation:

```text
stabilized canonical/image observations
    -> CameraSpaceRootTracker
        -> relative physical X/Z displacement

stabilized lower-body rhythm
    -> CadenceDetector

stabilized torso orientation + accepted Phase 4 signed map
    -> BodyHeadingEstimator

camera-space physical displacement + accepted Phase 4 reference map
cadence + heading
    -> LocomotionFusion
        -> world-space physical contribution + cadence velocity
        -> EmbodiedLocomotionController
            -> bound AvatarRoot world X/Z only
```

The camera-space root tracker does **not** use canonical pelvis/world position as absolute room position. Lateral position comes from the absolute image-space torso center, normalized by apparent body scale. Relative depth is a monocular proxy from the log of apparent torso/body scale. Shoulder/hip apparent widths are compensated by torso yaw and fade out near side-on poses; torso height remains as stable support. The result is filtered relative displacement, not claimed metric camera depth.

Cadence uses alternating left/right ankle rhythm with knee rhythm as supporting evidence. It has short acquisition, interval consistency, sustain confidence, and a short stop timeout. No arm-based fallback is implemented in Phase 5A.

Physical and cadence movement are fused rather than blindly added. Camera-space physical displacement and velocity are first scaled, mapped through the accepted Phase 4 **reference** canonical-to-avatar axis map, and projected to game-world X/Z. Current mapped physical root velocity then produces a physical-activity confidence, and cadence contribution is multiplied by `1 - physicalActivity`. Actual translation therefore suppresses cadence extension, while in-place rhythmic stepping can advance a persistent virtual origin along the live mapped body heading.

`EmbodiedLocomotionController.Recenter()` makes the current physical position the new tracking origin while first preserving the avatar's current virtual X/Z as the virtual origin. The character does not jump. The public method is deliberately suitable for a future discrete voice command, but speech recognition is not part of Phase 5A.

Root Y and root rotation are never driven by locomotion. Crouch remains pose reproduction; jump/gravity remain future gameplay/physics interpretation.

Default prototype tunables are deliberately exposed in serializable settings, including lateral/depth physical scale, tracking response, yaw compensation floor, cadence thresholds/rates/virtual stride, physical-velocity suppression thresholds, and physical deadzones.

The Lab adds compact locomotion diagnostics, **K = recenter**, and a runtime-created fixed world grid viewport. Existing R/C/X and F1–F6 controls remain unchanged.

Phase 5A is **NOT USER ACCEPTED**.

## Next action

Run one continuous Phase 5A USER QA session: stillness, physical X/Z displacement, diagonal movement, body turning, in-place cadence, turn-while-cadencing, cadence stop, real walking without obvious double-counting, and K recenter. Tune only what runtime feel exposes.

Phase 4 remains accepted and unchanged. Phase 5A remains unaccepted; do not begin Phase 6 or merge to `main` without explicit USER approval.

