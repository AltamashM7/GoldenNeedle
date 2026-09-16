# Golden Needle — New Orchestrator Handoff

Handoff refresh: 2026-09-17

Repository: `AltamashM7/GoldenNeedle`

Primary development branch: `gameplay/foundation`

Integration branch: `main`

## Start here

You are the new Web Orchestrator for Golden Needle.

Before making any implementation decision:

1. verify the live remote HEAD of `gameplay/foundation`;
2. verify the live remote HEAD of `main`;
3. read `Docs/current-state.md`;
4. read `Docs/hub-integration-plan.md`;
5. read `Docs/gameplay-foundation-plan.md`;
6. read `Docs/decisions.md` for locked/historical technical decisions;
7. inspect the actual repository files relevant to the task before writing a Builder/Luna handoff.

`Docs/current-state.md` is the current status authority. Some older stage-status lines in `Docs/decisions.md` predate the Calibration/Hub integration work; preserve its locked technical decisions, but use the refreshed current-state/handoff documents for phase status.

## Hard governance

- Do **not** merge gameplay work to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or rewrite shared history.
- Independently verify Builder/Luna claims rather than accepting summaries at face value.
- USER manual Unity/runtime QA is decisive runtime evidence.
- Do not reopen Motion Engine V1 merely because scene integration is underway.
- Do not create a Luna implementation handoff until the USER has finished defining the phase and approved the plan/handoff.
- When a task is approved, keep Luna instructions narrow and explicit; stop at real USER runtime QA boundaries.

## Verified repository baseline before this documentation refresh

Immediately before the documentation-only commits created for this handoff:

- `main` = `82a8475752826a7e07d446adcbe33b0b67e8f0d0`
- `gameplay/foundation` = `79a543c44788f0318d0e19455e188cb68607e55e`

The documentation refresh itself advances `gameplay/foundation`, so **do not** assume `79a543...` is still the live branch HEAD. It is the last gameplay/runtime implementation baseline before the handoff documentation commits.

Important recent gameplay lineage:

- `96bb5346ed4db52904172f2c4629af9021f34e16` — fixed quarter-turn Calibration preview geometry;
- `b081a5514cda1ff7640d840463e509a0357750c5` — added Calibration presentation-control architecture;
- `cc9db033b64f6f4d514fd0a8f7933e6f09af022e` — authored world-space Calibration presentation;
- `544afd099e186b6bfe824c092e168cdc0f6ab7fd` — added external animation-authority support for Calibration idle presentation;
- `79a543c44788f0318d0e19455e188cb68607e55e` — cleaned Unity 6 Calibration/editor warnings.

`main` was not changed by this gameplay work or by the current documentation refresh.

## Project status

### Motion Engine V1

**USER ACCEPTED / FROZEN FOR CURRENT PROJECT SCOPE**

Accepted implementation baseline:

`140a939160530394ee5c70aa1dbc31c62f3a12e1`

The body-control stack is good enough for this project stage. Crouch/ground-contact fidelity is approximate but accepted.

Do not reopen general:

- OpenVINO/provider architecture;
- Phase-3 stabilization/calibration;
- Phase-4 retargeting;
- locomotion tuning;
- crouch/grounding;
- detailed hand tracking;
- performance redesign;

unless the USER explicitly asks or scene gameplay reveals a concrete blocking defect.

### Gameplay Foundation

**IMPLEMENTED**

The current branch already contains the reusable session/gameplay foundation, including the persistent player/facade, health/body-anchor foundations, shared GameFlow/fades/spawn resolution, contextual command/speech routing, and scene context integration.

Do not restart the foundation phase.

### Calibration -> Hub functional flow

**USER-VERIFIED**

The USER manually confirmed:

- webcam preview appears;
- Begin Calibration works;
- calibration runs;
- fade transition works;
- Hub loads.

Treat that flow as accepted and protect it from regression.

### Calibration presentation

World-space presentation architecture is now on the branch:

- character-right / presentation-left composition model;
- TMP world-space prompt/success/failure UI;
- world-space webcam preview reusing the existing provider texture;
- presentation groups and scene-authored camera staging;
- success/live-pose preview before Hub transition;
- split avatar-pose-drive vs locomotion control;
- Hub context enables locomotion after transition.

The quarter-turn webcam geometry fix at `96bb534...` is protected.

#### Important local USER workaround

After testing idle-animation authority, the USER chose to defer further work on that issue.

Current USER local presentation setup is:

- persistent Golden Needle Player hidden visually in Calibration;
- a fresh/raw character with the same Animator Controller supplies the looping idle presentation;
- the rest of tracking, calibration, presentation flow, and GameFlow remains intact.

This local scene state may not be committed to the remote branch. Do **not** run an authoring command or rewrite `Caliberation.unity` in a way that destroys the USER's local creative edits without explicit approval.

The branch still contains `544afd0...` external animation-authority code; do not expand/rework it now unless the USER reopens the issue.

## Current approved phase — Hub integration

The USER has explicitly simplified the Hub scope.

The Hub is a **free-roam routing scene**, not a large gameplay feature.

Only the following is required now:

1. after Calibration -> Hub transition, free roam is active immediately;
2. the persistent calibrated player can move normally through the Hub;
3. two portals lead to the two activity fields;
4. crossing/colliding with a portal using the player character loads the appropriate activity through the shared GameFlow transition path;
5. simple invisible cuboid/box trigger volumes should be placed over the existing portal openings;
6. trigger handling must be reliable and must not generate repeated scene-load requests.

No tutorial, interaction prompt, Hub minigame, scoring, cutscene, or special navigation system is currently required.

Full approved scope is in `Docs/hub-integration-plan.md`.

## Portal mapping warning

The Hub environment contains retained yellow and blue visual portals. Earlier environment integration intentionally removed an older duplicate blue portal and kept the intended current visual set.

**Do not infer a color -> activity mapping.**

The USER has said the portals should load the appropriate course fields, but has not established a trustworthy color mapping in the current handoff context.

Before implementation:

- inspect `Assets/Scenes/GoldenNeedle_Hub.unity`;
- inspect existing components/names/metadata around both portal objects;
- search for any existing portal destination mapping in code/docs;
- inspect Build Settings and actual scene names;
- if no authoritative mapping exists, ask the USER which visual portal maps to Boxing vs Obstacle before final wiring.

Do not guess.

## Expected Hub architecture

### Entry/free roam

`HubSceneContextController` should remain the scene-local context owner. At Hub entry it should ensure the persistent player is in Hub command context with:

- avatar pose drive ON;
- locomotion ON.

The persistent GameFlow/spawn system should place the player at exact spawn id:

`HubEntry`

Do not create a second locomotion controller or scene-local player.

### Portal triggers

Preferred implementation is intentionally simple:

- invisible GameObject per portal;
- `BoxCollider` with `isTrigger = true`;
- aligned to the portal opening in the Unity scene;
- no Renderer;
- no mesh collider required;
- small scene-owned `HubPortalTrigger`-style behavior;
- destination scene name and destination spawn id Inspector-authored;
- identify only the Golden Needle persistent player/facade/session;
- use `GameFlowManager.TryTransitionTo(...)` or the actual existing equivalent;
- one in-flight/debounce guard;
- recover cleanly if GameFlow rejects/fails a transition;
- no direct `SceneManager.LoadScene` if the common GameFlow system owns transitions.

Keep the visual portal meshes/VFX untouched.

## Boxing environment limitation

The Boxing environment is still pending/local and was not available in the remote repository at the last verified status.

Therefore:

- Hub trigger infrastructure can be built now;
- the existing Obstacle destination can be wired once its exact scene/spawn contract is verified;
- do not fabricate a Boxing scene or guess a final Boxing scene name;
- if Boxing is still unavailable when Hub work is implemented, leave its destination cleanly authorable/disabled until the real scene is integrated.

When Boxing is eventually integrated:

1. merge its environment into `main` with current Motion Engine/project config taking priority on conflicts;
2. merge updated `main` normally into `gameplay/foundation`;
3. no rebase/history rewrite.

## Exact first task for the new Orchestrator

Perform a **read-only Hub audit** before producing Luna instructions.

Inspect at minimum:

- live `gameplay/foundation` HEAD and `main` HEAD;
- `Assets/Scenes/GoldenNeedle_Hub.unity`;
- `Assets/GoldenNeedle/Gameplay/Hub/HubSceneContextController.cs`;
- `Assets/GoldenNeedle/Gameplay/Flow/GameFlowManager.cs` or the actual equivalent path;
- `PlayerSpawnPoint` implementation;
- `GoldenNeedlePlayerFacade` control APIs;
- Build Settings;
- existing portal-related scripts/components;
- exact portal object names and collider state;
- exact existing activity scene names/spawn ids.

Answer these questions before implementation:

1. Is Hub locomotion already enabled immediately at the current head?
2. Are there already portal trigger components/colliders that can be reused?
3. Which two exact scene objects are the live visual portals?
4. Is destination mapping already encoded anywhere?
5. What exact destination scene/spawn contract is currently valid for Obstacle?
6. Is Boxing available yet on remote?

Only then prepare a narrow **Hub-only Luna handoff**. Do not combine Hub integration with Boxing gameplay, Obstacle gameplay, Motion Engine work, or additional Calibration polish.

## Minimal Hub USER QA target

Once implementation has passed static audit, USER runtime QA should be short:

1. Calibration -> Hub;
2. confirm free roam is active immediately;
3. walk around Hub;
4. cross each available portal deliberately;
5. confirm only one transition occurs;
6. confirm correct destination for each wired portal;
7. confirm calibration/player state persists;
8. confirm no duplicate player/provider/session objects;
9. confirm merely walking near the portal does not trigger prematurely.

If Boxing is still unavailable, do not claim full two-portal runtime acceptance.

## Protected files/areas

Unless the read-only audit proves a direct requirement, Hub work must not alter:

- accepted Motion Engine/OpenVINO/provider tuning;
- `GoldenNeedlePlayer.prefab` core configuration;
- Calibration presentation scene/creative layout;
- quarter-turn webcam geometry;
- locomotion algorithms/tuning;
- crouch/grounding;
- Obstacle gameplay;
- Boxing gameplay;
- packages;
- `main`.

Hub visual portal transforms/materials/VFX are also protected presentation data. Add separate invisible trigger volumes rather than rebuilding them.

## Development sequence after Hub

After USER accepts Hub functionality:

1. integrate/build Boxing when its environment is ready;
2. build Obstacle gameplay;
3. run full game-flow QA/polish:
   `Calibration -> Hub -> activity -> Hub -> activity -> Hub`;
4. only merge gameplay back to `main` when the USER explicitly approves.
