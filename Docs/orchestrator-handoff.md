# Golden Needle — Orchestrator Handoff

Handoff refresh: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## First actions

Before new work:

1. verify the live remote `engine/pose-tracking-spike` HEAD;
2. read `Docs/current-state.md`;
3. read `Docs/optimization-orchestrator-handoff.md` for frozen low-end/OpenVINO invariants;
4. read `Docs/decisions.md`;
5. read `Docs/motion-engine.md` when Motion Engine implementation detail is needed.

Do not merge to `main` without explicit USER approval. Do not force-push, rebase, amend, reset or rewrite shared history.

## Current milestone

Motion Engine V1 accepted implementation baseline:

`140a939160530394ee5c70aa1dbc31c62f3a12e1`

Current status:

**MOTION ENGINE V1 — USER ACCEPTED FOR CURRENT PROJECT SCOPE**

The USER has explicitly decided to stop further Motion Engine refinement and move toward gameplay. Crouch now works to a usable degree but remains imperfect; that limitation is accepted/deferred and is not a blocker for the next stage.

Do not reopen crouch/grounding, general Phase-3/Phase-4 tuning, provider/OpenVINO work, hands, or broad performance investigation unless later gameplay work exposes a concrete blocking defect or the USER explicitly reopens it.

## Accepted Motion Engine boundaries

The accepted runtime remains:

```text
WebCamTexture / CPU acquisition
-> bounded low-latency OpenVINO pose pipeline
-> canonical body
-> Phase 3 stabilization/calibration
-> Phase 4 humanoid pose/analytic IK
-> Phase 5 root locomotion
-> presentation/gameplay consumer
```

Important frozen invariants:

- stabilized canonical body remains calibration/locomotion authority;
- normal Phase-4 retargeting remains the avatar limb/leg pose authority;
- `CameraSpaceRootTracker` remains the single stateful physical X/Z position authority;
- support feet validate physical translation but do not own a second continuous root-position state machine;
- cadence remains available for in-place travel;
- Jump/Crouch semantics remain in `VerticalLocomotionInterpreter`;
- grounded crouch root movement is avatar-relative through `AvatarRelativeCrouchGrounding`;
- no post-root crouch-specific leg IK is active;
- recenter and tracking-loss/reacquisition continuity remain preserved;
- accepted low-end/OpenVINO architecture remains frozen;
- Foundation A commands/speech and Foundation B camera work remain retained;
- detailed/rich hands remain deferred.

## Known accepted limitation

Crouch/ground-contact fidelity is not perfect. The latest avatar-relative reconstruction improved the behavior enough for the USER to end Motion Engine V1 work, but it should not be documented as mathematically perfect foot locking.

Treat this as a known limitation, not an unfinished corrective task.

## Next stage — modular gameplay integration foundation

The USER intends to explain the actual fitness gameplay later. Before game-specific mechanics are built, establish a reusable player-control prefab that can be placed into any fitness-field scene and immediately provide the accepted camera-driven character-control stack.

Working concept:

`GoldenNeedleMotionPlayer.prefab`

The goal is **scene portability**, not new movement behavior.

A clean gameplay scene should be able to:

1. add the prefab;
2. place it at the intended spawn point;
3. enter Play mode;
4. calibrate;
5. control the avatar through the accepted Motion Engine without manually rebuilding or rewiring the tracking stack.

## Recommended prefab architecture

Keep one obvious top-level player prefab with internally owned dependencies. A sensible hierarchy is conceptually:

```text
GoldenNeedleMotionPlayer
├── MotionRuntime
│   ├── body/camera provider
│   ├── canonical/stabilization/calibration runtime
│   └── command/recenter support
├── Avatar
│   ├── current humanoid model
│   ├── HumanoidRigBinding
│   ├── HumanoidRetargeter
│   └── EmbodiedLocomotionController
└── PlayerFacade
    └── stable gameplay-facing API/status
```

Exact hierarchy should follow the existing component dependencies rather than forcing this shape literally.

### The prefab should own

- the accepted motion runtime/provider wiring required for one controlled player;
- the humanoid/avatar instance;
- rig binding and Phase-4 retargeting;
- Phase-5 locomotion;
- calibration/recenter access;
- a small stable gameplay-facing facade/API;
- optional diagnostics that can be disabled for normal gameplay.

### The prefab should not own

- fitness-field level geometry;
- obstacles or exercise-specific rules;
- scoring, progression or timers;
- scene lighting;
- arbitrary field UI;
- game-specific camera behavior unless the later gameplay design explicitly needs it;
- speculative Rigidbody/CharacterController/physics redesign before gameplay requirements are known.

## Gameplay-facing facade

Prefer game systems depending on one stable facade rather than directly reaching into Motion Engine internals.

The first facade can expose only what is already trustworthy and broadly useful, for example:

- whether calibration/body control is ready;
- tracking availability;
- controlled player/avatar root transform;
- current high-level vertical state (`Standing`, `Crouch`, `Jump`);
- recenter command;
- enable/disable player motion if needed;
- optional read-only locomotion/tracking diagnostics for gameplay/debug use.

Do not expose every internal filter/provider/retargeter setting through the gameplay API.

## Scene-dependency rule

The reusable prefab must not depend on manually assigned objects that only exist in `PoseTrackingSpike` or another specific scene.

Prefer prefab-internal serialized references or deterministic self-resolution among children/components. If a scene-level dependency is truly unavoidable, expose one clear documented integration slot rather than several hidden `Find...` dependencies.

Keep `PoseTrackingSpike` as the development/diagnostic lab. Do not convert that scene itself into the gameplay architecture.

## Validation target for the modular foundation

Before actual gameplay mechanics begin, validate the prefab in a minimal clean test scene containing only:

- basic ground/lighting;
- the motion-player prefab;
- only the smallest camera/presentation setup genuinely required.

Acceptance should prove:

- prefab instantiates without missing references;
- Unity compiles without red errors;
- webcam/body tracking initializes;
- calibration works;
- Phase-4 avatar control works;
- Phase-5 locomotion works;
- recenter works;
- no `PoseTrackingSpike`-specific scene object is required;
- deleting/re-adding the prefab does not require a manual rewiring checklist.

## Next boundary

Do **not** invent fitness gameplay mechanics yet. The USER will provide those requirements later.

The next implementation task should therefore be only the reusable gameplay-player prefab/facade foundation and a minimal portability validation scene. After that passes, actual fitness fields, obstacles, exercise mechanics, scoring and progression can be designed against the stable player interface.

No merge to `main` has been approved.
