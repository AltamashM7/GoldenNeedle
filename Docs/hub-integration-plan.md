# Golden Needle — Hub Integration Status and Contracts

Status refreshed: 2026-09-17

Repository: `AltamashM7/GoldenNeedle`

Branch: `gameplay/foundation`

## Scope

The Hub is a lightweight free-roam router. It is not a separate gameplay mode.

Approved responsibilities:

- receive the existing persistent calibrated player;
- place it at an authored Hub spawn;
- enable pose drive and locomotion immediately;
- conform the persistent player to the authored Hub TerrainCollider without taking ownership of locomotion;
- keep a gameplay camera following it;
- expose authored camera views;
- route portals through shared GameFlow;
- preserve one player/session/provider/flow stack.

No Hub quests, scoring, tutorials, cutscenes, special navigation, activity gameplay, or Motion Engine tuning are in scope.

## Implementation checkpoints

- `b8afaae8a8cf20ec5bf17a45c85d5b494e6f6151`: portal trigger behavior, Hub trigger objects, and Obstacle spawn contract.
- `6c3f4c365a9730baa87aa5e411337e20bbbe8f47`: follow camera/presets, Hub control-authority correction, editable spawn gizmo, complete-calibration gate, persistent Animator correction, and required Calibration presentation assets.
- `08b02c24946e5d1ec9a88a168584aaaf0106ce7f`: scene-local Calibration Animator binding repair, bounded Hub motion-readiness diagnostics, one-shot rig-binding recovery, and post-placement Hub TerrainCollider grounding.
- `b3b9f298abc94c0d2620d4696ae53e3a2cddc3ff`: one-shot automatic tracking recovery through the existing provider restart path, provider failure diagnostics, and camera follow fallback without a live body heading.
- `8026852`: continuous Calibration -> Hub webcam/provider preservation, fresh-camera startup validation, bounded same-texture soft resume, camera-health-gated hard fallback, and stable Hub readiness diagnostics.

## Hub entry contract

- Destination scene: `GoldenNeedle_Hub`.
- Spawn id: `HubEntry`.
- `HubEntry` is a scene-owned `PlayerSpawnPoint`; its transform is the authoring control for spawn placement.
- `GameFlowManager` resolves exactly one matching id and asks `GoldenNeedlePlayerFacade.TryPlacePlayerAt` to relocate/rebase the persistent player.
- `HubSceneContextController` waits for the persistent facade, applies idempotent Hub authority requests, and marks structural application separately from runtime readiness.
- Command context is applied when `GameplayCommandHost` exists.
- Pose/locomotion activation does not depend on command-host discovery.
- Hub state sets external animation authority OFF, avatar pose drive ON, locomotion ON.
- If the rig is unbound, Hub permits one narrow `TryEnsureRigBinding` recovery attempt; healthy bindings are never rebuilt and failures are not retried continuously.
- Calibration is scene-local. The persistent `GoldenNeedlePlayerSession` owns the provider and survives the normal `GameFlowManager` `LoadSceneMode.Single` replacement; Hub does not instantiate or rebind a second provider/session and does not use additive loading.
- After successful camera startup, normal Hub entry does not stop, destroy, reconstruct, switch, or invalidate the existing webcam/provider/OpenVINO session. Fresh-camera continuity is checked from the provider's `_latestFreshFrameObservedAtSeconds`, updated on `WebCamTexture.didUpdateThisFrame`; the fixed freshness threshold is 1 second.
- A body/result gap alone never requests recovery. Only a stale/missing camera frame after structural authority and completed calibration have become healthy enters the bounded continuity path.
- After a 0.75-second camera-staleness grace period, Hub requests one soft same-texture resume: `Play` when the existing texture is not playing, or same-object `Stop` -> `Play` when it is playing but stalled. It does not tear down OpenVINO, the worker, scheduler, calibration, or coordinate convention, and waits up to 1 second for a fresh frame.
- Only after the soft attempt fails may Hub request the existing `RestartProvider(invalidateCameraSession: false)` hard fallback. The request is bounded to a 1-second provider-idle window and at most one actual hard restart is accepted per Hub entry. On acceptance, the readiness timer waits for the provider camera startup timeout plus a 1-second margin (approximately 9 seconds with the current 8-second timeout).
- `StartCameraAsync` declares Ready only after valid dimensions, `isPlaying`, and a fresh frame observed after `Play`; a timeout without that real frame is a clear CameraFailed state.
- After the first full runtime readiness result, Hub continues for a 1-second stable camera/readiness window. If readiness still fails, one warning reports calibration/drive flags, provider status/message, selected camera, camera FPS/freshness/age, acquisition/fallback, OpenVINO runtime/worker state, pose freshness, retarget counts, and soft/hard recovery outcomes. No calibration, camera, orientation, retarget, locomotion, or terrain reset is attempted.

## Terrain grounding contract

Component: `Assets/GoldenNeedle/Gameplay/Hub/HubTerrainGroundingController.cs`.

Scene owner: `GoldenNeedle_HubIntegration` in `GoldenNeedle_Hub`.

Rules:

- use the explicitly assigned `GoldenNeedle_Island` `TerrainCollider`; do not scan arbitrary scene colliders;
- run at execution order 200, after the accepted locomotion controller's order 150;
- wait for GameFlow placement/transition completion before capturing the neutral root-to-ground offset, with a short direct-scene fallback only for direct Hub opening;
- after locomotion, sample terrain and compute `semanticVerticalOffset = FinalWorldPositionY - VerticalOriginY`;
- set `targetY = currentGroundY + rootToGroundOffset + semanticVerticalOffset`;
- modify only the persistent player root's world Y; locomotion remains the owner of X/Z, cadence, thresholds, and semantic vertical state;
- recapture the neutral offset after a large explicit X/Z relocation;
- when the assigned terrain cannot be sampled, preserve the accepted locomotion Y and emit one bounded diagnostic; never substitute arbitrary geometry.

## Camera contract

Component: `Assets/GoldenNeedle/Gameplay/Presentation/GameplayCameraController.cs`.

Scene owner: Hub Main Camera.

Rules:

- resolve only the persistent production session;
- use facade/root/body-anchor APIs, never provider internals;
- use retained locomotion world heading;
- retain the last valid heading during temporary source loss;
- if no valid heading has ever been acquired, use persistent player-root forward projected to X/Z, with deterministic +Z `(0, 1)` fallback for the existing Back-preset convention;
- continue resolving focus/preset/root and following the player when heading is absent; smoothly adopt live heading when it returns;
- update in `LateUpdate`;
- use existing `CameraViewPreset` and `CameraViewPresetMath`;
- keep distances, heights, focus offsets, FOV, and response values serialized in the scene;
- register with the shared command runtime only while enabled;
- do not run this component in Calibration.

Presets: `Back`, `Front`, `Left`, `Right`, `FullBody`, `Hands`, `LeftHand`, `RightHand`.

Speech phrases are the lower-case view names with `view` suffix, including `full body view`, `hands view`, `left hand view`, and `right hand view`.

## Portal contract

Authoritative mapping:

- `Portal yellow` -> Boxing;
- `Portal blue` -> Obstacle Course.

The visual portal prefab/mesh is not the trigger. Each portal uses a separate scene-owned GameObject with an invisible `BoxCollider` and `HubPortalTrigger`.

### Yellow

- Object: `PortalYellowTrigger`.
- `transitionEnabled = false`.
- Destination values empty.
- Reason: no real Boxing scene/spawn contract exists remotely.

### Blue

- Object: `PortalBlueTrigger`.
- `transitionEnabled = true`.
- Destination scene: `Obstacle Course`.
- Destination spawn: `ObstacleEntry`.
- `ObstacleEntry` exists in the destination scene and the scene is enabled in Build Settings.

### Trigger behavior

`HubPortalTrigger`:

- requires a positive enabled BoxCollider volume;
- recognizes only `GoldenNeedlePlayerSession.Instance` when persistent;
- resolves `GoldenNeedlePlayerFacade.PlayerRoot`;
- checks the root point in the oriented/scaled box;
- consumes only the outside -> inside edge;
- ignores disabled or incomplete destinations;
- uses active `GameFlowManager.TryTransitionTo`;
- blocks repeats while GameFlow is transitioning;
- after failure/rejection, requires exit and re-entry before retry;
- does not instantiate anything and does not call `SceneManager.LoadScene` directly.

## Presentation protection

- Do not move/rebuild portal meshes or VFX to change trigger behavior.
- Do not restore the old removed duplicate blue portal.
- Do not hardcode scene coordinates in source.
- Do not replace the Hub camera system with a second locomotion/player owner.
- Do not modify accepted Motion Engine tuning.

## Current evidence

The latest USER continuity evidence recorded Calibration on the selected `HP TrueVision HD Camera` from three devices at `640x480 @ approximately 18.8 fps`, body input `320x240 scaled`, `OpenVINO CPU FP32`, and `WebCamCPU/GetPixels32`, with Ready/live result-callback logs. After the automatic transition, the same persistent player and selected camera remained, but Hub showed `640x480 @ 0.0 fps`, `320x240 scaled`, `OpenVINO CPU FP32`, `ExistingReadback (fallback)`, fallback reason `provider/OpenVINO worker not ready`, T-pose/no locomotion, and no Hub readiness warning.

Static inspection identifies the previous failure path: the old Hub coroutine exited on the first `HasRuntimeMotionReadiness()` result, while the previous body-unavailable recovery could call destructive `RestartProvider(false)`. That provider path stopped/destroyed the `WebCamTexture` and tore down OpenVINO before rebuilding. The transition-gap -> destructive-restart -> 0 FPS/T-pose explanation is a strong code-based inference, not a hardware root-cause claim. Checkpoint `8026852` implements the camera-continuity contract above.

Focused Unity 6.5 batch import/domain reload rebuilt `Assembly-CSharp.dll` and `Assembly-CSharp-Editor.dll` successfully with no captured C# compiler diagnostic. The wrapper later crashed in native worker/session teardown during shutdown after compilation, so this is source/import verification only.

Static inspection also confirms serialized Hub continuity timing compatibility, persistent-session ownership, non-additive GameFlow loading, existing camera registration, trigger destinations, and spawn ids. No Play Mode, player build, webcam/pose test, uneven-terrain test, or fresh runtime portal test was performed; runtime acceptance remains with the USER.

## Required USER QA

1. Start `Caliberation` and confirm Runtime Camera shows `HP TrueVision HD Camera`, actual FPS above zero, and `WebCamCPU/GetPixels32`.
2. Complete calibration and enter Hub automatically without pressing Retry or a drive-motion/debug key.
3. Confirm the same camera remains selected, FPS resumes/remains above zero, acquisition remains or re-enters `WebCamCPU/GetPixels32`, and no persistent worker-not-ready fallback appears.
4. Confirm the avatar leaves the T-pose, follows pose, and basic locomotion responds.
5. Only after the continuity gate passes, confirm spawn/terrain/camera presets/portals and duplicate-object invariants.
6. If continuity fails, capture the one Hub warning and Runtime Camera screenshot with camera name, FPS, acquisition, and fallback reason.

Stop and diagnose evidence if this fails; do not broaden into activity implementation.
