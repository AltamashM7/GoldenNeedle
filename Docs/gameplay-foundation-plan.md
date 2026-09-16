# Golden Needle — Gameplay Foundation Plan

Plan approved by USER: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

## Purpose

Motion Engine V1 is complete for the current project scope. The next stage is to turn that accepted motion stack into a reusable game player and then build the four-scene fitness game around it.

This document is the authoritative gameplay-direction plan until the USER changes it.

## Four-scene game structure

### Scene 1 — Calibration

The first scene shown to the user.

Intended flow:

1. the avatar is visible in an idle animation;
2. UI shows `Say 'Begin Calibration' to start.` and also supports button activation;
3. button activation and speech recognition must call the same gameplay action;
4. quick fade out;
5. calibration presentation begins and the avatar demonstrates the T-pose/calibration pose;
6. fade in;
7. Motion Engine calibration runs;
8. on success, show a calibration-success message;
9. fade out slowly;
10. load the Hub.

Calibration should happen once for the play session. The calibrated player/session should persist into later scenes rather than requiring recalibration for every activity.

Current environment asset: `Assets/Scenes/Caliberation.unity`.

### Scene 2 — Hub

The Hub is the free-roam scene.

The player can move freely and enter either of two portals:

- Boxing Course;
- Obstacle Course.

Entering a portal should use the common scene-transition/fade system, load the requested activity, and place the persistent player at that scene's spawn point.

Current environment asset: `Assets/Scenes/GoldenNeedle_Hub.unity`.

### Scene 3 — Boxing Course

The boxing environment is being prepared on a local branch and is not yet merged into `main`.

Planned gameplay loop:

1. player spawns inside the boxing ring facing the enemy;
2. a visible 10-second countdown runs;
3. match begins at zero;
4. player strike hitboxes are available on both wrists and both feet;
5. enemy strike hitboxes are available on its hands;
6. player and enemy both have visible health bars;
7. match continues until the win/loss condition;
8. results/feedback show useful statistics such as completion time, punches/kicks thrown, successful hits, damage, etc.;
9. return to the Hub.

Boxing-specific logic must not be embedded into Motion Engine internals.

### Scene 4 — Obstacle Course

The player remains inside a bounded square arena formed by fences.

Planned gameplay loop:

1. player spawns in the center;
2. hazards such as cars/rocks are thrown into random locations inside the bounded area;
3. a warning/telegraph or "spidey sense" style indicator shows where the next danger will arrive;
4. the player physically dodges hazards;
5. survival time continuously increases while alive;
6. hazard contact applies damage (initial design example: 25% health per hit, subject to gameplay tuning);
7. on player defeat, show results such as survival time, obstacles dodged and hits taken;
8. return to the Hub.

Current environment asset: `Assets/Scenes/Obstacle Course.unity`.

## Persistent player architecture

The reusable player is a session-level object, not a scene-specific recreation.

Working concept:

`GoldenNeedlePlayer.prefab`

Conceptual responsibilities:

```text
GoldenNeedlePlayer
├── Motion runtime / webcam / pose pipeline
├── Calibration state
├── Avatar
│   ├── HumanoidRigBinding
│   ├── HumanoidRetargeter
│   └── EmbodiedLocomotionController
├── GoldenNeedlePlayerFacade
├── PlayerHealth / damage receiver
├── Body anchors
│   ├── LeftWrist
│   ├── RightWrist
│   ├── LeftFoot
│   └── RightFoot
└── Optional gameplay capabilities
    └── boxing strike hitboxes disabled outside Boxing
```

The prefab must preserve the accepted Motion Engine behavior and should not require scene-specific manual rewiring.

## Player facade boundary

Gameplay code should interact with one stable facade instead of reaching into pose/provider/filter/retargeting internals.

Initial useful facade surface may include:

- calibrated/ready state;
- tracking availability;
- controlled player root transform;
- current high-level vertical state (`Standing`, `Crouch`, `Jump`);
- recenter;
- enable/disable motion control;
- health/damage access;
- left/right wrist and foot anchors;
- presentation drive mode where appropriate.

Boxing, portals and obstacle logic must not directly locate `MediaPipePoseProvider`, `CameraSpaceRootTracker`, `HumanoidRetargeter`, OpenVINO objects, or other internal Motion Engine implementation types.

## Persistent game-flow system

Use a separate persistent `GameFlowManager` or equivalent session-level controller for:

- fade out/fade in;
- scene loading;
- returning to Hub;
- locating the destination `PlayerSpawnPoint`;
- repositioning/recentering the persistent player on scene entry;
- preventing duplicate persistent player/session objects.

Scene-specific gameplay belongs to scene-specific controllers such as:

- `CalibrationSceneController`;
- `HubController` / `PortalTrigger`;
- `BoxingMatchController`;
- `ObstacleCourseController`.

## Speech-system expansion

Foundation-A speech/command infrastructure should be improved rather than replaced.

### Presentation latency commands

The Motion Engine already supports raw and stabilized canonical presentation drive modes. Add user-facing commands that switch the visible avatar drive source while preserving stabilized calibration/locomotion authority:

- `Reduce latency` / `Low latency mode` -> `RawCanonical` presentation;
- `Smooth motion` / `Stabilized mode` -> `StabilizedCanonical` presentation.

This must not change the authority used for calibration or locomotion.

### Context-aware commands

Do not expose every command globally. Prefer command availability based on current gameplay context.

Initial command plan:

- Calibration scene: `Begin Calibration`;
- after calibration: `Recenter`;
- recovery: `Retry Tracking`;
- presentation preference: `Reduce latency`, `Smooth motion`;
- possible later commands: `Pause Tracking`, `Resume Tracking` if evidence shows they are useful.

Debug/lab-only commands should remain out of normal gameplay.

Do not make `Return to Hub` a globally always-active phrase without a confirmation or appropriate gameplay context because accidental recognition during a match/course would be disruptive.

## Development order

1. **Gameplay Foundation**
   - persistent `GoldenNeedlePlayer` prefab;
   - `GoldenNeedlePlayerFacade`;
   - persistent calibration/session lifecycle;
   - health/damage foundation;
   - wrist/foot body anchors;
   - `GameFlowManager`;
   - fade/scene loading;
   - `PlayerSpawnPoint` contract;
   - speech presentation-mode commands and contextual command routing.

2. **Calibration Scene Integration**
   - idle -> Begin Calibration -> fade -> calibration -> success -> Hub.

3. **Hub Integration**
   - free roam;
   - portals;
   - reliable activity/Hub scene transitions.

4. **Boxing Mode**
   - begin only after the boxing environment has been merged and the combined project has been checked;
   - countdown, combat, hitboxes, health, enemy, results, return to Hub.

5. **Obstacle Mode**
   - hazard spawning, warnings, collision damage, survival timer/results, return to Hub.

6. **Full Game-Flow QA and Polish**
   - Calibration -> Hub -> Boxing -> Hub -> Obstacle -> Hub;
   - no recalibration between normal scene transitions;
   - no duplicate player/provider/session objects;
   - tracking/calibration survive scene changes correctly.

## Branch/integration strategy

The gameplay foundation may begin before the boxing environment is available.

Expected flow:

```text
main
├── accepted Motion Engine V1
├── Calibration environment
├── Obstacle environment
└── Hub environment

main -> gameplay/foundation

later:
local boxing environment branch -> main
main -> merge into gameplay/foundation
```

Use a normal merge from the later updated `main` into `gameplay/foundation`; do not rebase or rewrite shared history.

Until Boxing is merged, avoid changes that depend directly on the boxing scene, including ring spawn coordinates, boxing camera framing, enemy placement or scene-specific boxing wiring.

## Environment integration status

Integrated into `main`:

- Motion Engine V1;
- Calibration environment from `realcourse/arihant`;
- Obstacle Course environment from `realcourse/arihant`;
- Hub environment from `course/janhavi`.

Pending:

- Boxing environment — currently local and undergoing baking.

When environment branches diverge from Motion Engine/main, current `main` Motion Engine/core/project configuration remains authoritative unless the USER explicitly decides otherwise.

## Motion Engine boundary

Motion Engine V1 is accepted for the current project scope. Crouch works to a usable degree but retains an accepted fidelity limitation.

Do not reopen crouch, provider/OpenVINO, Phase 3/4, hands or general motion tuning merely because gameplay work has started. Reopen only for a concrete gameplay-blocking defect or explicit USER request.
