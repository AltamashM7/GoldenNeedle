# Golden Needle — Hub Integration Plan

Approved scope: 2026-09-17

Repository: `AltamashM7/GoldenNeedle`

Active branch: `gameplay/foundation`

## Purpose

The Hub is intentionally a lightweight free-roam routing scene, not a separate large gameplay mode.

The USER-approved scope is limited to:

1. free roam becomes available immediately after the Calibration -> Hub transition;
2. the player can walk into either activity portal;
3. portal collision triggers the correct course scene through the shared game-flow transition system;
4. portal interaction is implemented with simple invisible trigger volumes over the existing visual portals.

Anything beyond those requirements is out of scope unless the USER explicitly expands the phase.

## Required Hub entry behavior

When `GoldenNeedle_Hub` becomes active after Calibration:

- the same persistent calibrated player/session survives the scene change;
- avatar pose drive remains enabled;
- locomotion is enabled immediately;
- the player is placed at exact spawn id `HubEntry` through the existing GameFlow/spawn system;
- calibration state remains usable;
- no second player, provider, command host, or game-flow singleton is created;
- normal free roam requires no extra confirmation/button/voice command.

`HubSceneContextController` is the expected scene-local place to apply Hub command context and enable the already-existing player controls. Do not build a second locomotion owner.

## Portal behavior

There are two activity portals in the Hub environment. The intended destinations are:

- Boxing Course;
- Obstacle Course.

The exact mapping of the existing visual portal objects to those activities is **not to be inferred from portal color**. First inspect scene metadata, names, existing components, or documentation. If no authoritative mapping already exists, ask the USER to define which visual portal maps to which activity before final wiring.

Entering/crossing a portal with the controlled player should:

1. detect the persistent player;
2. accept the trigger once;
3. disable/debounce further portal requests while the transition is pending;
4. use the common `GameFlowManager` transition path;
5. fade out;
6. load the authored destination scene;
7. resolve that destination's authored `PlayerSpawnPoint` id;
8. place/rebase the persistent player through the existing facade/GameFlow path;
9. fade in.

Do not call `SceneManager.LoadScene` directly from portal logic if the common GameFlow API already owns transitions.

## Trigger-volume implementation

The USER specifically approved invisible cuboid trigger volumes as the simple/optimized implementation.

Preferred implementation:

- a scene-local GameObject aligned with each existing portal opening;
- `BoxCollider` with `isTrigger = true`;
- no Renderer required;
- collider sized only large enough to reliably catch a player passing through;
- trigger can be a child/sibling of the portal but should remain logically separate from the visual mesh;
- Inspector-authored destination scene name and destination spawn id;
- optional descriptive portal id/name for debugging;
- no per-frame polling;
- no physics-heavy mesh collider required;
- no modification to portal VFX/materials/geometry for trigger detection.

The component should identify the Golden Needle player/session robustly, preferably through `GoldenNeedlePlayerFacade` / persistent player-session ownership rather than tags that may drift.

## Reliability requirements

Portal triggers must not:

- transition twice from one crossing;
- react to unrelated environment rigidbodies/colliders;
- create another player/session;
- reset calibration;
- alter the accepted locomotion system;
- alter the Motion Engine provider/OpenVINO settings;
- move or restyle the visual portals;
- hardcode portal transform coordinates in source code;
- hardcode a color -> activity assumption.

A simple one-shot/in-flight guard is expected. If a transition fails, the trigger should not leave the whole Hub permanently unusable; recovery behavior should follow existing GameFlow failure semantics.

## Scene authoring rule

Hub presentation is scene-authored.

Code should own only behavior:

- recognizing the player;
- knowing which authored destination to request;
- debouncing;
- calling common GameFlow.

Unity scene/Inspector data should own:

- trigger position;
- trigger rotation;
- trigger scale/size;
- which portal receives which trigger component;
- destination scene/spawn values once mapping is known.

Do not create an authoring tool that rewrites portal transforms on every run. If a narrow authoring helper is used, it must preserve manual scene edits on rerun.

## Existing Hub environment protection

The environment integration already retained the intended visible portal set and removed an older duplicate blue portal. Do not restore deleted portal objects or normalize the scene from an older branch.

The current visual portal locations/rotations/scales should be preserved unless the USER asks for presentation changes.

## Boxing availability caveat

The Boxing environment is still pending/local at this point. Therefore Hub infrastructure can be implemented now, but final Boxing destination scene wiring may have to remain unwired/disabled or use only an Inspector placeholder until the actual scene asset is integrated.

Do not create a fake production Boxing scene to satisfy the portal.

The Obstacle Course scene already exists remotely as:

`Assets/Scenes/Obstacle Course.unity`

Do not assume the final build-scene name for Boxing until the real environment has been integrated and verified.

## Minimal testing / QA

Use targeted checks only.

Static/editor checks should establish:

- Hub context enables pose drive + locomotion;
- portal trigger ignores non-player colliders;
- portal trigger emits only one request while in flight;
- portal destinations are Inspector-authored;
- GameFlow is used instead of direct scene loading;
- no protected Motion Engine/config files are changed.

USER runtime QA should establish:

1. complete Calibration and enter Hub;
2. movement is active immediately after fade-in;
3. roam around normally;
4. cross Portal A and confirm one correct scene transition;
5. return to Hub when the target mode supports it;
6. cross Portal B and confirm one correct scene transition;
7. verify calibration/player state persists;
8. verify no duplicate player/provider/session objects appear;
9. verify walking near but not through a portal does not trigger it unexpectedly.

If Boxing is not yet integrated, QA only the available destination and trigger behavior that can be legitimately tested; do not fabricate a pass for unavailable content.

## Out of scope

Do not add any of the following unless explicitly requested:

- Hub quests;
- Hub scoring;
- tutorial overlays;
- portal interaction buttons;
- hold-to-enter prompts;
- new speech commands solely for portals;
- cutscenes;
- complex portal VFX;
- custom navigation/pathfinding;
- Hub-specific locomotion tuning;
- Boxing gameplay;
- Obstacle gameplay;
- Motion Engine changes.

## Orchestrator next step

Before sending work to Luna, independently inspect the current remote branch and actual Hub scene/code. Confirm:

- current remote HEAD;
- `HubSceneContextController` behavior;
- existing GameFlow transition API;
- exact Hub portal objects;
- whether any trigger/collider logic already exists;
- whether portal activity mapping already exists;
- Build Settings / exact available destination scene names and spawn ids.

Then prepare a **narrow Hub-only Luna handoff**. The USER has approved this Hub scope, but repository facts and portal mapping must still be verified before implementation.
