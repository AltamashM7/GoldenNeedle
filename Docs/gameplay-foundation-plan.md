# Golden Needle — Gameplay Foundation and Scene Integration Plan

Plan authority refreshed: 2026-09-17

Repository: `AltamashM7/GoldenNeedle`

Active branch: `gameplay/foundation`

## Purpose

Motion Engine V1 and the reusable gameplay/session foundation are already implemented. This document now describes the approved production scene flow and the remaining scene-integration sequence.

For live status, read `Docs/current-state.md` first. For the immediate Hub phase, read `Docs/hub-integration-plan.md`.

## Product structure

### 1. Calibration

Current intended production flow:

1. Calibration scene opens with scene-local presentation;
2. the user can say or click `Begin Calibration` through the same gameplay command path;
3. calibration runs while locomotion remains disabled;
4. once calibration becomes usable, the visible production pose can become live while locomotion remains disabled;
5. a short success preview is shown;
6. the common GameFlow fade transitions to the Hub;
7. calibration/session state persists.

The USER has already manually verified the functional Calibration -> Hub path: webcam, Begin Calibration, calibration, fade, and Hub load all work.

Current remote presentation architecture includes world-space TMP presentation, a reusable webcam preview group, success/failure groups, scene-authored camera/staging, and split pose-drive/locomotion control.

A later animation-authority issue was intentionally deferred. The USER's current local presentation workaround hides the persistent Golden Needle Player during Calibration and uses a separate raw character with the same Animator Controller for the looping idle presentation. Do not overwrite that local creative state without explicit approval.

Current environment asset:

`Assets/Scenes/Caliberation.unity`

### 2. Hub

The Hub is intentionally simple.

Required behavior:

- the persistent calibrated player arrives at `HubEntry`;
- avatar pose drive and free-roam locomotion are enabled immediately;
- the user can walk anywhere the environment allows;
- two visual portals route to Boxing Course and Obstacle Course;
- simple invisible trigger cuboids detect the player crossing each portal;
- triggers use the shared GameFlow transition/fade/spawn system;
- no extra Hub gameplay system is required.

Do not infer portal activity mapping from portal color. Verify existing metadata/code first; ask the USER if the mapping is not already authoritative.

Full Hub scope: `Docs/hub-integration-plan.md`.

Current environment asset:

`Assets/Scenes/GoldenNeedle_Hub.unity`

### 3. Boxing Course

The Boxing environment remains pending/local at the time of this refresh.

Planned gameplay remains:

1. player spawns in the ring facing the enemy;
2. visible 10-second countdown;
3. player strikes through wrist/foot hitboxes;
4. enemy attacks through its hand hitboxes;
5. player/enemy health bars;
6. win/loss condition;
7. results/statistics;
8. return to Hub.

Do not start scene-specific Boxing implementation until the actual Boxing environment is integrated and verified.

### 4. Obstacle Course

Planned gameplay remains:

1. player spawns inside the bounded arena;
2. randomized incoming hazards target locations in the arena;
3. warning/telegraph indicates incoming danger;
4. player physically dodges;
5. survival time and health/damage are tracked;
6. results are shown on defeat/end;
7. return to Hub.

Current environment asset:

`Assets/Scenes/Obstacle Course.unity`

## Reusable gameplay foundation — already implemented

The persistent player/session layer is now the production base rather than future work.

Key responsibilities include:

- accepted Motion Engine runtime/provider/calibration;
- avatar/Humanoid binding and retargeting;
- embodied locomotion;
- persistent calibration/session state;
- `GoldenNeedlePlayerFacade` gameplay-facing API;
- health/damage foundation;
- body anchors;
- optional mode-gated capabilities.

Gameplay systems must depend on the facade/session boundary rather than provider/OpenVINO/filter/retarget internals.

## Persistent game-flow system — already implemented

`GameFlowManager` or the existing equivalent owns:

- fade out/fade in;
- scene loading;
- destination spawn resolution;
- persistent player placement/rebasing;
- normal Hub returns;
- duplicate persistent-session prevention.

Scene-specific code should request transitions through this common owner rather than directly loading scenes.

## Player control authority by scene

### Calibration intro/calibrating

- pose drive OFF where idle presentation owns the visible avatar;
- locomotion OFF.

### Calibration usable/success preview

- pose drive ON;
- locomotion OFF.

### Hub

- pose drive ON;
- locomotion ON immediately.

### Activities

Activity controllers may gate movement or capabilities when their rules require it, but they must use the gameplay facade rather than mutating Motion Engine internals.

## Speech / command behavior

Existing production commands are retained.

User-facing presentation commands include:

- `Reduce latency` / `Low latency mode` -> raw visible-avatar presentation;
- `Smooth motion` / `Stabilized mode` -> stabilized visible-avatar presentation.

Calibration and locomotion remain stabilized regardless of visible presentation preference.

Command availability remains contextual. Do not expose lab/debug commands in normal gameplay, and do not make `Return to Hub` an always-on global speech command without a later explicit design decision.

## Scene authoring principle

Code decides **what happens**. Unity scene/Inspector data decides **how it is placed and presented**.

For scene-specific integration:

- keep portal/trigger positions and sizes Inspector-authored;
- keep camera staging/presentation transforms Inspector-authored;
- keep destination scene/spawn fields authored in the scene/component;
- avoid hardcoded environment coordinates;
- authoring tools must preserve user creative edits on rerun.

## Current implementation sequence

Completed:

1. Motion Engine V1;
2. Gameplay Foundation;
3. functional Calibration -> Hub integration;
4. Calibration world-space presentation architecture.

Current:

5. **Hub integration** — immediate free roam + portal triggers + correct common transitions.

Then:

6. Boxing Mode after environment integration;
7. Obstacle Mode;
8. full cross-scene QA/polish.

## Branch / integration strategy

- Gameplay work remains on `gameplay/foundation`.
- `main` must not receive gameplay changes without explicit USER approval.
- Boxing environment can later be integrated into `main`, then merged normally into the gameplay branch.
- Do not rebase or rewrite shared branch history.

## Motion Engine boundary

Motion Engine V1 is USER accepted for current scope.

Do not reopen general crouch/grounding, provider/OpenVINO, Phase 3/4, hand tracking, or locomotion tuning merely because scene work continues. Reopen only for a concrete gameplay-blocking defect or explicit USER request.
