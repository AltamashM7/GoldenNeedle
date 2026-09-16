# Golden Needle — Game Flow

Status refreshed: 2026-09-17

This document separates implemented production flow from future product direction.

## Implemented now

```text
Caliberation scene
    -> persistent GoldenNeedlePlayerSession is available
    -> user begins calibration by speech or click
    -> complete calibration samples are collected
    -> success preview
    -> GameFlowManager fade/load
    -> GoldenNeedle_Hub
    -> persistent player placed at HubEntry
    -> pose drive and locomotion enabled
    -> gameplay camera follows persistent player
```

From Hub:

```text
blue portal
    -> one-shot/debounced GameFlow request
    -> Obstacle Course
    -> persistent player placed at ObstacleEntry
```

Yellow portal -> Boxing is authoritative, but the yellow trigger is disabled because the real Boxing scene and spawn contract are unavailable.

## Persistence rules

- Calibration, provider state, player facade, command host, and game flow belong to one persistent session.
- Scene transitions must not instantiate duplicate runtime owners.
- Scene-local context controllers request control state through the player facade.
- Spawn placement uses exact `PlayerSpawnPoint` ids.
- Player relocation preserves calibration and rebases movement state through the existing facade/runtime path.

## Control state by scene

### Calibration intro and sampling

- persistent gameplay player hidden/staged away;
- separate scene-local presentation character plays idle;
- persistent pose drive OFF;
- locomotion OFF;
- full calibration completion gates success.

### Calibration success/transition

- external Animator authority released from the persistent player;
- pose drive ON;
- locomotion remains OFF until Hub.

### Hub

- external Animator authority OFF;
- pose drive ON;
- locomotion ON immediately;
- camera follow ON with `Back` initial preset;
- `HubEntry` controls placement.

### Activities

Activity controllers may gate movement when required, but they must use `GoldenNeedlePlayerFacade` rather than mutate provider, retargeter, or locomotion internals.

## Hub routing contracts

- Yellow -> Boxing: trigger exists but disabled; destination pending.
- Blue -> `Obstacle Course` / `ObstacleEntry`: implemented.
- Portal behavior uses `GameFlowManager.TryTransitionTo`; no portal-owned direct scene load.

## Hub camera interaction

The camera follows the persistent player's root and retained world heading. Speech-selectable views are `Back`, `Front`, `Left`, `Right`, `FullBody`, `Hands`, `LeftHand`, and `RightHand`.

Calibration intentionally does not use this gameplay camera.

## Planned but not implemented

- initial title/menu reveal and Start Fitness/Exit flow;
- character-led narrative introduction;
- Boxing scene integration and gameplay;
- Obstacle gameplay;
- result/statistics screens;
- activity -> Hub return flow;
- full cross-scene polish.

These remain future work and must not be represented as complete.
