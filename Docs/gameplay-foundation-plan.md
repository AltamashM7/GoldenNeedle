# Golden Needle — Gameplay Foundation and Scene Integration Plan

Plan authority refreshed: 2026-09-17

Repository: `AltamashM7/GoldenNeedle`

Active branch: `gameplay/foundation`

Read `Docs/current-state.md` first for evidence and acceptance status.

## Completed foundations

The following are implemented and must not be restarted:

1. Motion Engine V1 and CPU-first provider stack;
2. persistent Golden Needle player/session;
3. gameplay facade, health/damage, and body anchors;
4. shared fade/scene/spawn `GameFlowManager`;
5. contextual command/speech routing;
6. Calibration scene flow and world-space presentation;
7. Calibration -> Hub transition;
8. Hub context, exact `HubEntry` spawn, and immediate pose/locomotion enablement;
9. Hub portal infrastructure;
10. blue portal -> `Obstacle Course` / `ObstacleEntry` wiring;
11. Hub follow camera with scene-authored view presets and speech selection.

Implementation checkpoint: `6c3f4c365a9730baa87aa5e411337e20bbbe8f47`.

## Production scene model

### Calibration

Scene: `Assets/Scenes/Caliberation.unity`.

- Collect the complete calibration session before success/transition.
- Keep locomotion off.
- Use the scene-local `android01` and `calibration_idle.controller` for idle presentation.
- Keep the persistent player's Animator controller null so Humanoid retargeting can own it after transition.
- Preserve quarter-turn webcam geometry and USER-authored layout/font work.
- Persist the player/session/calibration into Hub.

The latest Animator and complete-sample corrections await USER runtime acceptance.

### Hub

Scene: `Assets/Scenes/GoldenNeedle_Hub.unity`.

- Place the persistent player at scene-authored `HubEntry`.
- Enable pose drive and locomotion as soon as the facade is available, independently of optional command-host discovery.
- Follow the persistent player with `GameplayCameraController`.
- Keep view geometry Inspector-authored.
- Route activities through separate invisible trigger boxes and shared GameFlow.

Authoritative portal mapping:

- yellow -> Boxing;
- blue -> Obstacle Course.

Current wiring:

- yellow is safely disabled because Boxing is unavailable;
- blue routes to `Obstacle Course` at `ObstacleEntry`.

### Boxing

Environment and spawn contract are not available remotely. Do not invent them.

Planned gameplay remains countdown, player/enemy strikes, health, win/loss, results, and return to Hub, but none of this should begin until the real environment is integrated and USER approves the phase.

### Obstacle Course

Environment and `ObstacleEntry` exist. Gameplay is not implemented.

Planned gameplay remains incoming hazards, telegraphs, embodied dodging, survival/health tracking, results, and return to Hub.

## Architectural boundaries

- Gameplay depends on `GoldenNeedlePlayerFacade`, not MediaPipe/OpenVINO/retarget internals.
- `POSE != LOCOMOTION` remains enforced.
- Scene controllers own context/rules; Motion Engine owns tracking and motion interpretation.
- `GameFlowManager` owns fade, scene load, exact spawn resolution, and persistent-player placement.
- Scene/Inspector data owns spawn, portal-trigger, and camera-preset placement.
- Never create scene-local duplicate players, providers, command hosts, or flow managers.
- Do not directly load scenes from portal code.
- Preserve `main` until explicit USER merge approval.

## Commands

Production presentation commands remain:

- `Reduce latency` / `Low latency mode` -> Raw visible-avatar presentation;
- `Smooth motion` / `Stabilized mode` -> Stabilized visible-avatar presentation.

Hub camera commands are:

- `back view`, `front view`, `left view`, `right view`;
- `full body view`, `hands view`, `left hand view`, `right hand view`.

Camera preset selection is Hub-only at this stage. Calibration and locomotion continue to use stabilized data regardless of visible presentation choice.

## Verification and acceptance gates

Current implementation received static inspection only by explicit USER preference. Do not claim runtime acceptance until the USER performs the manual flow documented in `Docs/current-state.md`.

The next gate is not more implementation. It is USER validation of:

1. complete calibration timing;
2. Hub pose following and locomotion;
3. Hub spawn placement;
4. camera follow/presets;
5. blue portal transition;
6. yellow portal inertness;
7. session persistence/no duplicates.

## Remaining sequence

1. close the current USER QA gate;
2. integrate Boxing environment and spawn contract;
3. wire yellow portal;
4. Boxing gameplay;
5. Obstacle gameplay;
6. activity-return flow and full cross-scene QA;
7. presentation polish as separately approved;
8. explicit USER-approved merge to `main`.
