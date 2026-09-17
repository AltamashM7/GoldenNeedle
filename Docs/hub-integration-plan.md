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
- After structural authority and completed calibration are healthy, Hub observes unavailable body tracking for 0.75 seconds, then makes recovery requests for at most a bounded 1-second window while the provider becomes idle. At most one actual provider restart is performed per Hub entry.
- Recovery reuses `RestartProvider(invalidateCameraSession: false)`, so the completed calibration and coordinate convention remain intact. On acceptance, the readiness timer restarts and waits for the provider camera startup timeout plus a 1-second margin (approximately 9 seconds with the current 8-second timeout), rather than failing at the old 2-second window.
- If readiness still fails, one warning reports provider status/message, selected camera, texture/playing state, bootstrap/result freshness, backend, and recovery request/accept/performed counters. No calibration, camera, orientation, retarget, or locomotion reset is attempted.

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

The latest USER evidence established that Calibration remained complete, Hub drive flags were on, the Humanoid rig was bound, but `bodyTrackingAvailable=False` and all downstream retarget counts were zero. The camera also stopped following because it returned when no live heading was available. The implementation checkpoint `b3b9f298abc94c0d2620d4696ae53e3a2cddc3ff` adds the narrow provider recovery and camera fallback described above.

Focused Unity batch import/domain reload regenerated the script assembly and produced no captured C# compiler diagnostic. The Unity wrapper timed out during repeated Licensing Client initialization/reconnection rather than exiting cleanly, so this is source/import verification only.

Static inspection also confirms serialized presets, camera registration, Hub control calls, trigger destinations, and spawn ids. No Play Mode, player build, webcam/pose test, uneven-terrain test, or fresh runtime portal test was performed; runtime acceptance remains with the USER.

## Required USER QA

1. Complete Calibration and enter Hub without pressing a drive-motion/debug key.
2. Confirm the camera follows immediately even before a live body heading exists.
3. Confirm the provider recovery is automatic and the avatar leaves the T-pose when body tracking returns.
4. Confirm pose following and locomotion.
5. Confirm spawn at `HubEntry`.
6. Walk across uneven Hub terrain and confirm the avatar stays grounded.
7. Confirm Jump/Crouch vertical motion remains relative to the sampled ground.
8. Confirm camera follow and all presets.
9. Cross blue trigger and confirm exactly one transition to `ObstacleEntry`.
10. Confirm yellow remains inert.
11. Confirm no duplicate persistent objects.

Stop and diagnose evidence if this fails; do not broaden into activity implementation.
