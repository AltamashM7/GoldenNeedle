# Golden Needle — Current State

Authoritative refresh: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## Governance

- Do **not** merge to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or rewrite shared history.
- Independently verify the live remote branch before new work.
- USER Unity/manual/runtime evidence is the acceptance authority.
- Keep game-specific mechanics separate from the reusable motion/player foundation.

## Motion Engine V1 — USER ACCEPTED FOR CURRENT PROJECT SCOPE

Accepted implementation baseline:

`140a939160530394ee5c70aa1dbc31c62f3a12e1`

The USER has concluded the Motion Engine V1 development phase and does not want further crouch tuning before gameplay work.

The final crouch behavior is **accepted with a known limitation**: avatar-relative crouch now works to a usable degree, but crouch/ground-contact fidelity is not considered perfect. This limitation is intentionally deferred rather than treated as a blocker for gameplay.

Do not reopen Motion Engine V1 corrective work unless later gameplay QA exposes a concrete blocking defect.

## Accepted production motion pipeline

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
-> Phase 4 positional/analytic-IK avatar pose
-> Phase 5 physical/cadence/root locomotion
-> presentation
```

Accepted properties include:

- CPU webcam/body inference path suitable for the low-end target;
- latest-useful-frame scheduling with bounded backlog;
- stabilized canonical body as calibration/locomotion authority;
- modular calibration and accepted humanoid retargeting;
- room-relative X/Z physical locomotion with support validation;
- cadence-based in-place locomotion;
- recenter and tracking-loss/reacquisition continuity;
- semantic Jump/Crouch interpretation;
- avatar-relative crouch root scaling;
- Phase 4 remains the normal leg/knee pose authority;
- commands/speech and camera-preset foundations remain available;
- detailed/rich hands remain deferred.

## Final locomotion architecture

### Horizontal

`CameraSpaceRootTracker` is the single stateful physical X/Z authority. Torso/body motion forms the continuous candidate while bilateral support evidence validates real room translation. `LocomotionFusion` maps accepted physical displacement and blends cadence; it does not own a second physical-position state machine.

The USER reported the previous locomotion-caused idle/root jitter as substantially improved after this reconstruction. Freeze this architecture unless a later gameplay scene proves a specific regression.

### Vertical

`VerticalLocomotionInterpreter` remains the semantic Jump/Crouch authority and produces normalized body-compression evidence.

For grounded crouch, production root Y is mapped through `AvatarRelativeCrouchGrounding`, using stable reference-pose avatar leg geometry rather than fixed world-meter crouch depth. There is no post-root crouch-specific leg IK pass; Phase 4 remains the normal leg-pose owner.

Known limitation: foot-floor/crouch fidelity is approximate rather than perfect. The USER has explicitly chosen to stop further motion-engine work here and move on.

## Deferred work

The following are **not blockers** for gameplay foundation work and should remain deferred unless the USER reopens them:

- additional crouch/grounding refinement;
- broad performance/logging investigation;
- detailed/rich hand tracking;
- Foundation-C/Foundation-D/Foundation-E experimental work;
- provider/OpenVINO redesign;
- general Phase-3 or Phase-4 retuning.

## Next project stage — modular gameplay foundation

The next planned stage is **not game mechanics yet**. Before fitness-field gameplay is implemented, create a reusable scene-independent player/motion prefab so the accepted Motion Engine can be dropped into any gameplay scene without rebuilding or manually rewiring the tracking stack.

Recommended target concept:

`GoldenNeedleMotionPlayer.prefab`

The prefab should encapsulate the reusable player-control stack, including the avatar, binding/retargeting, motion runtime/provider wiring, locomotion, calibration/control facade, and required internal dependencies.

A fitness-field scene should provide only environment/gameplay-specific objects and place the prefab at the desired spawn point.

### Prefab boundaries

The reusable player prefab should own:

- motion/body-tracking runtime;
- avatar and humanoid rig binding;
- Phase-4 retargeting;
- Phase-5 locomotion;
- calibration/recenter entry points;
- a small stable gameplay-facing player API/facade;
- optional diagnostics that can be disabled outside the lab scene.

The prefab should **not** own:

- fitness-field geometry;
- obstacles/exercise rules;
- scoring/progression;
- field-specific cameras unless later gameplay design explicitly requires one;
- scene lighting/UI unrelated to motion control;
- gameplay-specific physics decisions that have not yet been designed.

Prefer internal/self-contained references over hard-coded scene object references. The existing `PoseTrackingSpike` scene remains the diagnostic/development lab and should not become the gameplay prefab itself.

## Next boundary

Before implementing actual gameplay mechanics, design and implement the modular player prefab/facade foundation, then validate it in a minimal clean scene by dropping the prefab in and confirming that calibration + avatar control work without manual scene rewiring.

The USER will explain the actual gameplay design after this foundation is established.

No merge to `main` has been approved.
