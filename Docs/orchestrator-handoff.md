# Golden Needle — Orchestrator Handoff

Handoff refresh: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

Primary development branch: `gameplay/foundation`

Integration branch: `main`

## First actions for the next Orchestrator

1. verify live remote HEADs for `main` and `gameplay/foundation`;
2. read `Docs/current-state.md`;
3. read `Docs/gameplay-foundation-plan.md`;
4. read `Docs/decisions.md`;
5. read `Docs/optimization-orchestrator-handoff.md` only when low-end/OpenVINO details are needed;
6. read `Docs/motion-engine.md` only when accepted Motion Engine internals are relevant.

Do not force-push, rebase, amend, reset or rewrite shared history. Do not merge gameplay work back to `main` without explicit USER approval.

## Project phase transition

**MOTION ENGINE V1 — USER ACCEPTED FOR CURRENT PROJECT SCOPE**

Accepted implementation baseline:

`140a939160530394ee5c70aa1dbc31c62f3a12e1`

The USER explicitly ended further Motion Engine tuning after confirming the avatar-relative crouch works to a usable degree. Crouch/ground-contact fidelity remains imperfect but is an accepted limitation, not an unfinished blocker.

The accepted Motion Engine was then merged into `main` with explicit USER approval.

Do not reopen crouch/grounding, general Phase-3/Phase-4 tuning, provider/OpenVINO work, hands, or broad performance investigation unless later gameplay work exposes a concrete blocker or the USER explicitly reopens it.

## Mainline environment integration already completed

`main` now includes:

- accepted Motion Engine V1;
- Calibration environment from `realcourse/arihant`;
- Obstacle Course environment from `realcourse/arihant`;
- Hub environment from `course/janhavi`.

Primary current scene assets:

- `Assets/Scenes/Caliberation.unity`;
- `Assets/Scenes/GoldenNeedle_Hub.unity`;
- `Assets/Scenes/Obstacle Course.unity`.

Relevant integration lineage before the gameplay documentation refresh:

- Motion Engine merged to `main`: `880b75cf15acd030b5474eed97a7f74126ccf4e3`;
- Calibration + Obstacle integration: `67d5de51f58d900df5df7f5b7ce91075e01f89c6`;
- Hub integration: `3fb68ecfe245ff36c16a7752108248c1c433734c`.

## Boxing environment status

The Boxing environment is still on a local branch and is undergoing baking. It is not yet visible in the remote repository.

The USER approved beginning gameplay-foundation development without waiting for it.

When the boxing environment is ready:

1. integrate the local boxing environment branch into `main`;
2. preserve current `main` Motion Engine/core/package/project configuration on conflicts unless the USER explicitly decides otherwise;
3. then merge updated `main` normally into `gameplay/foundation`;
4. do not rebase/rewrite the gameplay branch.

Until then, do not implement boxing-scene-specific placement, camera framing, enemy placement or ring wiring.

## Approved product structure

There are four intended game scenes/modes:

1. **Calibration** — idle avatar, button/speech `Begin Calibration`, fade, calibration/T-pose presentation, success message, transition to Hub.
2. **Hub** — persistent calibrated player free-roams and enters either Boxing or Obstacle portal.
3. **Boxing** — 10-second countdown, player wrist/foot strikes, enemy hand strikes, health bars, match results, return to Hub.
4. **Obstacle Course** — fenced survival arena, random hazards, warning/telegraph, health/damage, survival timer/results, return to Hub.

Full approved behavior is in `Docs/gameplay-foundation-plan.md`.

## Current milestone — Gameplay Foundation

The immediate goal is **not** to finish Boxing or Obstacle gameplay yet. Build the reusable cross-scene foundation first.

### Persistent player

Target concept:

`GoldenNeedlePlayer.prefab`

The player/session should persist across normal scene transitions so calibration is performed once per play session rather than once per activity.

Conceptual responsibilities:

```text
GoldenNeedlePlayer
├── accepted Motion Engine runtime/provider/calibration
├── Avatar
│   ├── HumanoidRigBinding
│   ├── HumanoidRetargeter
│   └── EmbodiedLocomotionController
├── GoldenNeedlePlayerFacade
├── PlayerHealth / damage receiver
├── body anchors
│   ├── LeftWrist
│   ├── RightWrist
│   ├── LeftFoot
│   └── RightFoot
└── optional gameplay capabilities
    └── boxing strike hitboxes disabled outside Boxing
```

Exact hierarchy should follow existing component dependencies rather than forcing this shape literally.

### Player facade rule

Gameplay systems should depend on one stable gameplay-facing facade rather than reaching into Motion Engine internals.

Useful initial facade surface may include:

- calibrated/ready state;
- tracking availability;
- player root transform;
- high-level vertical state (`Standing`, `Crouch`, `Jump`);
- recenter;
- enable/disable motion control;
- health/damage access;
- wrist/foot anchors;
- visible-avatar presentation drive mode where appropriate.

Boxing/Obstacle/portal code must not directly depend on `MediaPipePoseProvider`, OpenVINO internals, `CameraSpaceRootTracker`, `HumanoidRetargeter`, or filter implementation details.

## Persistent game-flow owner

Use `GameFlowManager` or equivalent as a separate persistent session-level system for:

- fade out/fade in;
- scene loading;
- return to Hub;
- destination spawn-point resolution;
- reposition/recenter on scene entry as needed;
- preventing duplicate persistent session/player objects.

Use a simple `PlayerSpawnPoint` contract in each scene.

Scene-specific controllers own mode logic:

- `CalibrationSceneController`;
- Hub/portal controller(s);
- `BoxingMatchController`;
- `ObstacleCourseController`.

## Speech-system expansion

Retain and extend Foundation-A command infrastructure.

Approved initial presentation commands:

- `Reduce latency` / `Low latency mode` -> visible avatar uses `RawCanonical`;
- `Smooth motion` / `Stabilized mode` -> visible avatar uses `StabilizedCanonical`.

Critical invariant: calibration and locomotion remain stabilized regardless of visible-avatar presentation mode.

Prefer context-aware commands:

- Calibration: `Begin Calibration`;
- post-calibration: `Recenter`;
- recovery: `Retry Tracking`;
- presentation: `Reduce latency`, `Smooth motion`;
- possible later: `Pause Tracking`, `Resume Tracking` if useful.

Do not expose lab/debug commands during normal gameplay. Do not make `Return to Hub` an always-on global phrase without context/confirmation.

## Accepted Motion Engine invariants to preserve

- stabilized canonical body remains calibration/locomotion authority;
- Phase-4 retargeting remains normal limb/leg-pose authority;
- `CameraSpaceRootTracker` remains the single stateful physical X/Z owner;
- feet/support validate translation, not a second root-position owner;
- cadence remains available for in-place travel;
- Jump/Crouch semantics remain in `VerticalLocomotionInterpreter`;
- grounded crouch root motion remains avatar-relative through `AvatarRelativeCrouchGrounding`;
- no post-root crouch-specific full leg IK;
- accepted low-end/OpenVINO pipeline remains frozen;
- detailed/rich hands remain deferred.

## Recommended implementation order

1. **Gameplay Foundation**
   - persistent player prefab/session;
   - facade;
   - health/damage;
   - body anchors;
   - `GameFlowManager`;
   - fades/scene loading;
   - `PlayerSpawnPoint`;
   - contextual speech + raw/stabilized presentation commands.

2. **Calibration Scene integration**
   - idle -> Begin Calibration -> fade -> calibration -> success -> Hub.

3. **Hub integration**
   - free roam;
   - working portals;
   - reliable cross-scene persistence.

4. **Boxing Mode** after Boxing environment is integrated.

5. **Obstacle Mode**.

6. **Full flow QA/polish**
   - Calibration -> Hub -> Boxing -> Hub -> Obstacle -> Hub;
   - calibration/session persists;
   - no duplicate player/provider/session objects.

## Immediate next action

Continue from `gameplay/foundation` and inspect the existing Motion Engine component dependencies before creating the prefab or facade.

First implementation should focus only on scene-independent foundation pieces. Do not touch Boxing scene integration until the local Boxing environment has been merged into `main` and brought into the gameplay branch.

The USER has approved starting this work now.
