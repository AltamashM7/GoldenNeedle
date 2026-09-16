# Golden Needle — Current State

Authoritative refresh: 2026-09-17

Repository: `AltamashM7/GoldenNeedle`

Active development branch: `gameplay/foundation`

Integration branch: `main`

## Read this first

This file is the durable status authority. Historical plans remain useful for rationale, but where they disagree with this file, use this file and verify the live repository.

Implementation checkpoint immediately preceding this documentation refresh:

`6c3f4c365a9730baa87aa5e411337e20bbbe8f47` — Hub gameplay camera, camera speech presets, editable Hub spawn presentation, complete-calibration gate, and restoration of persistent-player pose/locomotion authority.

Prior pushed Hub portal checkpoint:

`b8afaae8a8cf20ec5bf17a45c85d5b494e6f6151` — Hub portal trigger infrastructure, blue-to-Obstacle wiring, and `ObstacleEntry` spawn contract.

Before the new commits were pushed, remote refs were independently verified as:

- `gameplay/foundation`: `b8afaae8a8cf20ec5bf17a45c85d5b494e6f6151`;
- `main`: `82a8475752826a7e07d446adcbe33b0b67e8f0d0`.

Always verify live refs before new work. `main` has not been modified by this gameplay work.

## Governance and safety

- Do not merge gameplay work to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or rewrite shared history.
- USER Unity/manual/runtime evidence is the authority for gameplay and visual acceptance.
- Keep Motion Engine internals separate from scene, camera, Hub, and activity logic.
- Preserve scene-authored presentation and USER creative edits; do not run broad authoring tools over them.
- Do not enter Play Mode or run broad Unity test/compile cycles merely to manufacture confidence when the USER has reserved runtime testing for themselves.
- Distinguish implementation completion, static verification, and USER runtime acceptance.

## Platform and project baseline

- Unity: `6000.5.0f1`.
- Render pipeline: URP `17.5.0`.
- Input System: `1.19.0`.
- AI Inference: `2.6.1`.
- MediaPipe is embedded at `Packages/com.github.homuler.mediapipe`.
- Unity Pipeline is development tooling only and must not become a shipped runtime dependency.
- Production scenes enabled in Build Settings:
  - `Assets/Scenes/Caliberation.unity`;
  - `Assets/Scenes/GoldenNeedle_Hub.unity`;
  - `Assets/Scenes/Obstacle Course.unity`.
- `SampleScene` is present but disabled.

## Motion Engine V1 — USER ACCEPTED / FROZEN

Accepted Motion Engine implementation baseline:

`140a939160530394ee5c70aa1dbc31c62f3a12e1`

The accepted stack includes the CPU-first OpenVINO path, canonical body, stabilization/calibration, Humanoid retargeting, physical/cadence locomotion, recenter/reacquisition continuity, and Jump/Crouch semantics.

Preserved invariants:

- calibration and locomotion consume stabilized canonical data;
- Raw/Stabilized commands change visible-avatar presentation only;
- `CameraSpaceRootTracker` remains physical X/Z authority;
- pose reproduction and locomotion remain separate (`POSE != LOCOMOTION`);
- the replaceable Pose Provider boundary remains intact;
- accepted provider/OpenVINO scheduling, Phase 3/4 behavior, and locomotion tuning are outside current scene-integration scope;
- no dedicated GPU is required.

Crouch/ground contact is approximate but accepted. Do not reopen Motion Engine work unless the USER explicitly requests it or a concrete gameplay-blocking defect is demonstrated.

## Gameplay/session foundation — IMPLEMENTED

The reusable production foundation exists on `gameplay/foundation`:

- one persistent `GoldenNeedlePlayerSession` and player prefab;
- `GoldenNeedlePlayerFacade` as the gameplay-facing boundary;
- body anchors and health/damage foundation;
- persistent `GameFlowManager` with fade, asynchronous scene loading, exact spawn resolution, placement/rebasing, and failure diagnostics;
- contextual command/speech routing;
- separate avatar pose-drive, external animation-authority, and locomotion controls;
- Calibration and Hub scene-context controllers;
- duplicate persistent-session prevention.

Normal Hub/activity transitions must preserve calibration and must not create another player, provider, command host, or flow manager.

## Implemented production flow

```text
Caliberation
    -> complete body calibration
    -> short success preview
    -> shared fade/load transition
    -> GoldenNeedle_Hub at HubEntry
    -> immediate pose drive + locomotion
    -> blue portal
    -> Obstacle Course at ObstacleEntry
```

The yellow portal has trigger infrastructure but intentionally remains disabled because the real Boxing scene and spawn contract do not yet exist remotely.

Returning from an activity to Hub and activity-specific gameplay are later phases.

## Calibration scene — current architecture

Scene: `Assets/Scenes/Caliberation.unity` (the repository intentionally uses the `Caliberation` spelling).

The scene contains two conceptually different characters:

1. the persistent production player/session, whose visual body is intentionally hidden/staged out of view during Calibration but whose tracking, calibration, and runtime state persist into Hub;
2. a scene-local presentation character named `android01`, which supplies the looping `warmUp` idle presentation.

Required asset state is committed:

- `Assets/GoldenNeedle/anim/Warming Up.fbx`;
- `Assets/GoldenNeedle/anim/calibration_idle.controller`;
- `Assets/GoldenNeedle/fontss/Oswald-Regular SDF.asset`;
- their Unity `.meta` files and parent folder metadata.

The presentation character retains `calibration_idle.controller`. The persistent player's Animator has an explicit scene override with `m_Controller = null`. This separation is intentional: assigning the idle controller to the persistent player caused it to keep animation authority after Hub transition, producing a looping idle or, after partial workarounds, a T-pose with no pose/locomotion response.

`CalibrationPresentationController` remains responsible for scene-local presentation groups, idle presentation, camera staging, success timing, and control-state requests. `CalibrationSceneController` remains the flow authority.

### Complete-calibration gate

Calibration transition gating now uses `GoldenNeedlePlayerFacade.IsCalibrationComplete`, backed by `MotionCalibrationProfile.IsComplete`, at all three relevant checks:

- leaving `Calibrating`;
- initial Hub-transition request;
- transition retry.

The prior `IsCalibrationUsable` gate became true as soon as an early valid body reference existed, which could finish Calibration much faster than the intended per-chain geometry sample collection. The new gate waits for the full calibration session.

### Calibration evidence boundary

Previously USER-verified:

- webcam preview appears;
- `Begin Calibration` works;
- fade transition works;
- Hub loads.

Latest USER observation after the gate change:

- Calibration now waits noticeably instead of completing almost immediately.

Still awaiting a fresh end-to-end USER retest after the persistent Animator correction:

- full sample completion;
- transition to Hub;
- persistent player follows body pose;
- persistent player locomotion responds normally.

## Hub scene — current architecture

Scene: `Assets/Scenes/GoldenNeedle_Hub.unity`.

### Hub entry and player control

- Exact spawn id: `HubEntry`.
- `HubEntry` is a scene-owned `PlayerSpawnPoint` and can be moved/rotated in the Inspector to control where the persistent player appears.
- `PlayerSpawnPoint` draws a cyan wire-sphere gizmo for authoring visibility; it does not run as gameplay behavior.
- `HubSceneContextController` applies Hub command context when the command host exists.
- Enabling player pose drive and locomotion no longer depends on the optional command host. Once the persistent facade exists, Hub disables external animation authority, enables avatar pose drive, and enables locomotion.

This addresses the observed Hub T-pose/nonresponsive movement failure without modifying Motion Engine algorithms.

### Gameplay camera

`GameplayCameraController` is attached to the Hub Main Camera. It is scene-local and follows only `GoldenNeedlePlayerSession.Instance` when it is the persistent production session.

It:

- resolves the player through `GoldenNeedlePlayerFacade`;
- uses retained locomotion world heading exposed by `HasWorldHeading` / `WorldHeadingXZ`;
- updates in `LateUpdate` after player motion;
- applies an immediate first pose, then damped position, orientation, and FOV response;
- reuses existing `CameraViewPreset` geometry/fallback semantics;
- supports avatar-root, both-hands, left-hand, and right-hand focus with existing fallbacks;
- registers/unregisters through `GoldenNeedleCommandRuntimeServices`;
- is intentionally absent from Calibration because the USER hides/stages the persistent player there.

Hub scene-authored presets:

- `Back` (initial);
- `Front`;
- `Left`;
- `Right`;
- `FullBody`;
- `Hands`;
- `LeftHand`;
- `RightHand`.

Hub camera speech commands:

- `back view`;
- `front view`;
- `left view`;
- `right view`;
- `full body view`;
- `hands view`;
- `left hand view`;
- `right hand view`.

Camera-preset commands are enabled in Hub and disabled in Calibration and Activity contexts. Existing `recenter`, `retry tracking`, Raw presentation, and Stabilized presentation commands retain their prior context policy.

USER evidence:

- the USER confirmed the camera follows the character in Hub.

Still requiring USER QA:

- all view presets and voice phrases;
- hand-preset framing/focus;
- tracking loss/reacquisition;
- camera behavior with the latest pose/locomotion correction.

### Portal routing

The mapping is explicitly USER-authoritative:

- yellow portal -> Boxing;
- blue portal -> Obstacle Course.

Current scene configuration:

- `PortalYellowTrigger`: separate invisible `BoxCollider`; `HubPortalTrigger`; `transitionEnabled = false`; destination empty. This is intentionally inert until real Boxing scene/spawn contracts exist.
- `PortalBlueTrigger`: separate invisible `BoxCollider`; `HubPortalTrigger`; `transitionEnabled = true`; destination `Obstacle Course`; spawn `ObstacleEntry`.
- `Obstacle Course.unity` contains `ObstacleEntry` and is enabled in Build Settings.

`HubPortalTrigger` does not require a player physics collider. It polls the persistent player-root point against the scene-owned box, recognizes only the persistent session/facade, consumes an enter edge, debounces in-flight transitions, routes through `GameFlowManager.TryTransitionTo`, and permits retry only after leaving/re-entering following a rejected/failed request.

Portal meshes, VFX, materials, and visual transforms remain presentation-owned. Triggers are separate objects.

## Boxing and Obstacle status

### Boxing

- Real environment: not available remotely.
- Yellow trigger infrastructure: present but disabled.
- Scene name/spawn id: intentionally not guessed.
- Gameplay: not started.

### Obstacle Course

- Environment: available.
- Build Settings: enabled.
- Spawn contract: `ObstacleEntry`.
- Blue Hub portal: wired.
- Gameplay: not started.
- Portal transition: awaits fresh USER runtime acceptance.

## Verification record for `6c3f4c3...`

Performed:

- repository, branch, package, Unity version, URP, Build Settings, and remote-ref inspection;
- focused source/scene diff review;
- serialized verification that the presentation Animator keeps `calibration_idle.controller` and the persistent Animator override is null;
- static verification of Hub camera presets, portal contracts, spawn ids, and control-context behavior;
- `git diff --check` on source files passed. Unity scene YAML includes normal empty serialized `value:` lines that Git flags as trailing whitespace.

Intentionally not performed:

- Play Mode;
- Unity compilation/build;
- broad EditMode suite;
- physical webcam/pose QA;
- visual preset QA;
- portal runtime QA.

This follows the USER's request to minimize low-value testing and leave Unity runtime testing to them. Do not report the latest checkpoint as runtime accepted.

Earlier accepted/reported evidence remains historical only, including the prior full EditMode `286/286` result and focused portal/presentation checks. It does not substitute for manual QA of the new camera and Animator correction.

## Immediate USER QA boundary

1. Open `Caliberation`.
2. Begin Calibration and confirm it does not finish at the early usable threshold.
3. Complete calibration and transition to Hub.
4. Confirm the persistent avatar follows body poses rather than looping idle or remaining in a T-pose.
5. Confirm locomotion responds.
6. Confirm spawn occurs at `HubEntry`; move that object if a different spawn is desired.
7. Confirm the camera follows.
8. Try all eight camera phrases/presets.
9. Cross the blue portal and confirm one transition to `Obstacle Course` at `ObstacleEntry`.
10. Confirm the yellow portal remains inert.
11. Confirm no duplicate player/session/provider/flow objects.

If pose or locomotion still fails, capture the first relevant Console error/warning and inspect the persistent player's `HumanoidRetargeter`, locomotion drive flag, and pose-source availability before changing Motion Engine code.

## Next development order

1. USER runtime acceptance of Calibration -> Hub pose/locomotion/camera.
2. USER runtime acceptance of blue portal -> Obstacle.
3. Integrate the real Boxing environment and define its spawn contract.
4. Wire and verify the yellow portal.
5. Implement Boxing gameplay.
6. Implement Obstacle gameplay.
7. Full cross-scene QA/polish, including activity -> Hub returns.
8. Merge to `main` only with explicit USER approval.

Do not begin activity gameplay merely because infrastructure exists.
