# Golden Needle — Current State

Authoritative refresh: 2026-09-17

Repository: `AltamashM7/GoldenNeedle`

Active development branch: `gameplay/foundation`

Integration branch: `main`

## Governance

- Do **not** merge gameplay work to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or rewrite shared history.
- Independently verify live remote refs before making decisions or preparing Builder/Luna work.
- USER Unity/manual/runtime evidence is the decisive runtime acceptance authority.
- Keep Motion Engine internals separate from scene/gameplay logic.
- Prefer Luna for Unity scene/code execution when a task is approved, but the Orchestrator must inspect the actual repository and independently audit claims.
- Do not generate an implementation handoff until the USER has finished defining the phase and explicitly approves the plan/handoff.

## Live branch baselines at this refresh

Before these documentation commits, the verified remote refs were:

- `main`: `82a8475752826a7e07d446adcbe33b0b67e8f0d0`
- `gameplay/foundation`: `79a543c44788f0318d0e19455e188cb68607e55e`

The gameplay/runtime implementation lineage immediately before this documentation refresh is:

- `96bb5346ed4db52904172f2c4629af9021f34e16` — preserve quarter-turn Calibration webcam geometry;
- `b081a5514cda1ff7640d840463e509a0357750c5` — Calibration presentation control architecture;
- `cc9db033b64f6f4d514fd0a8f7933e6f09af022e` — world-space Calibration presentation polish;
- `544afd099e186b6bfe824c092e168cdc0f6ab7fd` — release Calibration rig to external idle-animation authority;
- `79a543c44788f0318d0e19455e188cb68607e55e` — Unity 6 warning cleanup.

The new Orchestrator must verify the live branch HEAD instead of assuming it still equals any SHA listed here.

## Motion Engine V1 — USER ACCEPTED / FROZEN FOR CURRENT SCOPE

Accepted Motion Engine implementation baseline:

`140a939160530394ee5c70aa1dbc31c62f3a12e1`

The accepted body-control stack includes the CPU-first OpenVINO path, canonical body, stabilization/calibration, Humanoid retargeting, physical/cadence locomotion, recenter/reacquisition continuity, and Jump/Crouch semantics.

Crouch/ground-contact fidelity is not perfect, but the USER explicitly accepted it as sufficient for this project stage. Do not reopen Motion Engine tuning, OpenVINO/provider architecture, Phase-3/Phase-4 behavior, hands, or general locomotion tuning unless gameplay exposes a concrete blocker or the USER explicitly reopens it.

Important preserved production invariants:

- calibration and locomotion use stabilized canonical data;
- raw/stabilized user commands affect visible avatar presentation only;
- `CameraSpaceRootTracker` remains the physical X/Z authority;
- Phase 4 remains normal limb/leg-pose authority;
- no post-root crouch-specific full leg IK;
- accepted OpenVINO/WebCamCPU path remains frozen;
- no dedicated GPU is required.

## Gameplay Foundation — IMPLEMENTED

The reusable gameplay/session foundation now exists on `gameplay/foundation`.

Key concepts already implemented before the current Hub phase include:

- persistent `GoldenNeedlePlayer` session object/prefab;
- `GoldenNeedlePlayerFacade` gameplay-facing boundary;
- reusable health/damage foundation and body anchors;
- persistent `GameFlowManager` with fade/scene transition handling;
- exact destination `PlayerSpawnPoint` resolution;
- player relocation that preserves calibration/session state;
- contextual gameplay command host/speech routing;
- visible-avatar Raw/Stabilized presentation commands;
- Calibration scene flow controller;
- Hub context controller.

The persistent player/session must survive normal scene transitions so normal play does not recalibrate between Hub and activities.

## Calibration -> Hub integration — FUNCTIONAL FLOW USER-VERIFIED

The USER manually verified the core production flow:

1. Calibration scene opens;
2. webcam preview works;
3. `Begin Calibration` works;
4. calibration becomes usable;
5. the scene fades;
6. `GoldenNeedle_Hub` loads.

That functional Calibration -> Hub gate is accepted. Do not regress it while working on Hub.

### Current Calibration presentation architecture

Visible Calibration presentation is scene-local and world-space:

- character composition on the RIGHT;
- prompt/webcam/success presentation on the LEFT;
- TextMesh Pro presentation resources are present in the project;
- `CalibrationPresentationController` owns scene presentation state/timing;
- `CalibrationSceneController` remains flow/calibration/transition authority;
- `WorldSpacePresentationGroup` provides reusable group fades;
- `PresentationCameraRig` provides authored camera staging;
- the existing `CalibrationCameraPreview` remains the single webcam presentation path;
- quarter-turn preview geometry from `96bb534...` must remain intact.

Approved state model:

- Intro: pose drive OFF, locomotion OFF, idle presentation allowed;
- Calibrating: pose drive OFF, locomotion OFF, idle presentation continues;
- Calibration usable: pose drive ON, locomotion OFF, success/live-pose preview;
- Hub: pose drive ON, locomotion ON.

### Deferred Calibration animation-authority issue / current USER workaround

The branch contains the external animation-authority support introduced by `544afd0...`, but the USER later chose not to spend more time on this issue before Hub work.

Current USER local presentation workaround:

- the persistent Golden Needle Player is hidden visually during Calibration;
- a fresh/raw character using the same Animator Controller supplies the looping idle presentation;
- the rest of the Calibration/tracking/GameFlow system is preserved;
- further player animation-authority refinement is deferred.

Treat this as a USER-local creative/runtime state unless/until it is committed. Do not assume the remote `Caliberation.unity` exactly contains those local creative edits, and do not overwrite them through an authoring rerun without explicit USER approval.

## Environment status

Integrated into `main` and inherited by the gameplay branch:

- Calibration: `Assets/Scenes/Caliberation.unity`;
- Hub: `Assets/Scenes/GoldenNeedle_Hub.unity`;
- Obstacle Course: `Assets/Scenes/Obstacle Course.unity`.

Boxing Course remains pending/local while its environment/bake work is completed. This is **not a blocker** for Hub trigger infrastructure or for wiring the already-available Obstacle destination.

When Boxing becomes available, integrate it into `main` with accepted Motion Engine/current project configuration taking priority on conflicts, then normally merge updated `main` into `gameplay/foundation`. Do not rebase shared history.

## Current phase — Hub integration

The USER has deliberately narrowed Hub scope. The Hub is not intended to become another large gameplay phase.

The required Hub behavior is:

1. after Calibration transitions to Hub, the persistent calibrated player immediately has normal free-roam locomotion;
2. the Hub contains two activity portals;
3. crossing/colliding with a portal using the player character triggers the appropriate activity scene transition;
4. portal detection should use simple invisible trigger volumes/cuboids placed over the visual portal geometry;
5. the common `GameFlowManager` fade/scene-loading path should perform the transition rather than custom direct scene loading;
6. portal triggers must be reliable, one-shot/debounced, and must not duplicate persistent player/session objects.

No additional Hub gameplay, tutorial system, complex interaction prompt, cutscene, scoring, or custom navigation system is currently required.

### Portal mapping is not yet safe to invent

The Hub contains the retained visual portal objects from the environment integration, including yellow and blue portal geometry. Their exact activity mapping must come from existing scene metadata/code or an explicit USER decision.

Do **not** infer that a color means Boxing or Obstacle merely from appearance. The next Orchestrator should inspect the actual Hub scene first. If the repository does not already establish the mapping, ask the USER before wiring destination names.

### Portal trigger implementation direction

Use scene-owned trigger volumes rather than modifying visual portal meshes:

- invisible `BoxCollider` or similarly simple primitive collider;
- `isTrigger = true`;
- positioned/scaled to cover the walk-through area of each existing portal;
- a small Hub portal trigger component that accepts an authored destination scene name and spawn id;
- detect only the persistent player/facade/session, not arbitrary environment colliders;
- route through `GameFlowManager.TryTransitionTo(...)` or the existing equivalent;
- guard against repeated `OnTriggerEnter` calls while a transition is in progress;
- keep destination data Inspector-authored;
- do not change portal visual placement/scale/rotation unless the USER explicitly asks.

The invisible cuboid approach is approved as the optimization/simplicity choice.

See `Docs/hub-integration-plan.md` for the current Hub-specific scope.

## Protected Hub environment details

Do not normalize or casually replace the Hub environment. Earlier inspection established that the retained portal presentation includes:

- a yellow portal in the existing environment;
- one retained blue portal;
- an older duplicate blue portal had already been removed during environment integration.

Portal geometry/location is environment-authored and should be treated as presentation data. Add separate triggers instead of rebuilding the portals.

## Development order from here

1. Hub integration: immediate free roam + reliable portal triggers + common transitions;
2. Boxing Mode after Boxing environment is integrated;
3. Obstacle Mode;
4. full cross-scene QA/polish.

Each major gameplay phase still has two gates:

- functional acceptance;
- presentation/polish acceptance where presentation work is relevant.

For Hub, because the scope is intentionally minimal, the primary functional gate is enough unless the USER asks for additional Hub presentation work.

## Immediate next boundary

The next Orchestrator should:

1. verify the live branch HEADs;
2. read the current-state, Hub plan, gameplay plan, and handoff docs;
3. inspect `GoldenNeedle_Hub.unity`, `HubSceneContextController`, `GameFlowManager`, `PlayerSpawnPoint`, Build Settings, and any existing portal/trigger scripts read-only;
4. confirm how free-roam is enabled on Hub entry and whether this already works at the current head;
5. identify the exact two visual portal objects and whether an activity mapping already exists;
6. only after that inspection, prepare the Luna Hub implementation handoff;
7. do not start Boxing/Obstacle gameplay in the same task.
