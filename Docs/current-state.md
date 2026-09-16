# Golden Needle — Current State

Authoritative refresh: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

Current integration branch: `main`

Planned active development branch: `gameplay/foundation`

## Governance

- Do **not** merge new work to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or rewrite shared history.
- Independently verify live remote refs before editing or integrating.
- USER Unity/manual/runtime evidence is the acceptance authority.
- Keep game-specific mechanics separate from the reusable player/motion foundation.

## Motion Engine V1 — USER ACCEPTED FOR CURRENT PROJECT SCOPE

Accepted implementation baseline:

`140a939160530394ee5c70aa1dbc31c62f3a12e1`

The USER ended the Motion Engine V1 development phase after confirming that the avatar-relative crouch works to a usable degree. Crouch/ground-contact fidelity remains imperfect, but this is an accepted limitation rather than a blocker.

The accepted Motion Engine branch was merged into `main` with explicit USER approval. The Motion Engine/core architecture is now the authoritative foundation for gameplay work.

Do not reopen Motion Engine V1 corrective work unless later gameplay QA exposes a concrete blocking defect or the USER explicitly asks to revisit it.

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
-> presentation/gameplay consumer
```

Accepted properties include:

- CPU-first webcam/body inference suitable for the low-end target;
- latest-useful-frame scheduling with bounded backlog;
- stabilized canonical body as calibration/locomotion authority;
- modular calibration and accepted humanoid retargeting;
- room-relative X/Z physical locomotion with support validation;
- cadence-based in-place locomotion;
- recenter and tracking-loss/reacquisition continuity;
- semantic Jump/Crouch interpretation;
- avatar-relative crouch root scaling;
- Phase 4 remains the normal leg/knee pose authority;
- commands/speech and camera-preset foundations retained;
- detailed/rich hands remain deferred.

## Final locomotion architecture

### Horizontal

`CameraSpaceRootTracker` is the single stateful physical X/Z authority. Torso/body motion forms the continuous candidate while bilateral support evidence validates real room translation. `LocomotionFusion` maps accepted physical displacement and blends cadence; it does not own a second physical-position state machine.

The USER reported the previous locomotion-caused idle/root jitter as substantially improved. Freeze this architecture unless a gameplay scene proves a specific regression.

### Vertical

`VerticalLocomotionInterpreter` remains the semantic Jump/Crouch authority and produces normalized body-compression evidence.

For grounded crouch, production root Y is mapped through `AvatarRelativeCrouchGrounding`, using stable reference-pose avatar leg geometry rather than fixed world-meter crouch depth. There is no post-root crouch-specific leg IK pass; Phase 4 remains the normal leg-pose owner.

Known accepted limitation: foot-floor/crouch fidelity is approximate rather than perfect.

## Environment integration status

Already integrated into `main`:

- accepted Motion Engine V1;
- Calibration environment from `realcourse/arihant`;
- Obstacle Course environment from `realcourse/arihant`;
- Hub environment from `course/janhavi`.

Current primary scene assets:

- Calibration: `Assets/Scenes/Caliberation.unity`;
- Hub: `Assets/Scenes/GoldenNeedle_Hub.unity`;
- Obstacle Course: `Assets/Scenes/Obstacle Course.unity`.

Pending environment:

- Boxing Course — still on a local branch and currently undergoing baking, so it is not yet available in the remote repository.

The boxing environment is **not a blocker** for gameplay-foundation development. When it becomes available, merge it into `main` with Motion Engine/current project configuration taking priority on conflicts, then merge updated `main` normally into the gameplay branch.

## Approved four-scene game structure

The USER has now defined the actual gameplay direction.

1. **Calibration Scene**
   - avatar starts idle;
   - button/speech command `Begin Calibration` starts the shared calibration action;
   - fade transition;
   - calibration presentation/T-pose;
   - on success show confirmation;
   - transition to Hub.

2. **Hub**
   - persistent calibrated player can free roam;
   - two portals lead to Boxing and Obstacle Course;
   - activities return to Hub after completion.

3. **Boxing Course**
   - player spawns in ring facing enemy;
   - 10-second countdown;
   - wrist/foot strike hitboxes for player, hand hitboxes for enemy;
   - health bars;
   - post-match statistics/results;
   - return to Hub.

4. **Obstacle Course**
   - player remains inside fenced square arena;
   - randomized incoming hazards such as cars/rocks;
   - warning/telegraph indicates danger location;
   - player physically dodges;
   - survival timer and health/damage;
   - results on defeat;
   - return to Hub.

See `Docs/gameplay-foundation-plan.md` for the full approved architecture and sequence.

## Next project stage — Gameplay Foundation

The next implementation stage should create a reusable persistent player/session layer **before** scene-specific gameplay mechanics are built.

Target concept:

`GoldenNeedlePlayer.prefab`

It should encapsulate the reusable accepted player-control stack and survive normal scene changes so calibration does not need to be repeated for every activity.

### Reusable player responsibilities

- accepted motion/body-tracking runtime;
- avatar and humanoid rig binding;
- Phase-4 retargeting;
- Phase-5 locomotion;
- calibration/session state;
- `GoldenNeedlePlayerFacade` or equivalent stable gameplay-facing API;
- generic health/damage receiver;
- body anchors for left/right wrists and feet;
- optional gameplay capabilities such as boxing strike hitboxes, disabled outside the relevant mode.

### Separate persistent game-flow responsibilities

Use `GameFlowManager` or equivalent for:

- fades;
- scene loading;
- Hub returns;
- destination spawn-point placement;
- avoiding duplicate persistent player/session objects.

### Scene-owned responsibilities

Scenes/game modes own:

- environment geometry;
- portals and scene-specific controllers;
- boxing rules/enemy/stats;
- obstacle spawning/warnings/results;
- scoring/timers/UI specific to a mode;
- scene cameras/lighting as required.

## Speech-system expansion

The existing Foundation-A speech/command system should be extended rather than replaced.

Approved initial additions:

- `Reduce latency` / `Low latency mode` -> visible avatar uses `RawCanonical` presentation drive;
- `Smooth motion` / `Stabilized mode` -> visible avatar uses `StabilizedCanonical` presentation drive.

Important: calibration and locomotion remain stabilized even when the visible avatar uses raw presentation.

Prefer context-aware command availability:

- Calibration: `Begin Calibration`;
- post-calibration: `Recenter`;
- recovery: `Retry Tracking`;
- presentation: `Reduce latency`, `Smooth motion`;
- possible later: `Pause Tracking`, `Resume Tracking` if useful.

Do not expose debug/lab-only commands in normal gameplay. Do not make `Return to Hub` globally always active without contextual gating/confirmation.

## Development order

1. Gameplay Foundation;
2. Calibration Scene integration;
3. Hub integration;
4. Boxing Mode after boxing environment integration;
5. Obstacle Mode;
6. full cross-scene game-flow QA/polish.

## Branch strategy

Create `gameplay/foundation` from the latest documentation-refreshed `main` and begin environment-independent foundation work there now.

While the boxing environment is still baking locally:

- do not block foundation development;
- avoid boxing-scene-specific placement/wiring;
- keep `main` available for the later boxing environment merge.

After Boxing is merged into `main`, merge updated `main` into `gameplay/foundation` with a normal merge. Do not rebase shared history.

## Deferred work

The following are not blockers for gameplay foundation work and remain deferred unless explicitly reopened:

- further crouch/grounding refinement;
- broad performance/logging investigation;
- detailed/rich hand tracking;
- provider/OpenVINO redesign;
- general Phase-3 or Phase-4 retuning.

## Immediate next boundary

Refresh authoritative handoff/decision docs, create `gameplay/foundation`, and continue development from that branch. The boxing environment can be integrated later without blocking this stage.
