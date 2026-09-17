# Golden Needle — Orchestrator / Web Builder Handoff

Refresh: 2026-09-17

Repository: `AltamashM7/GoldenNeedle`

Work branch: `gameplay/foundation`

Integration branch: `main`

## Start here

1. Fetch the repository and check out `gameplay/foundation`.
2. Verify the live remote branch SHA; do not assume a SHA from prose.
3. Read `Docs/current-state.md` completely.
4. Read `Docs/game-flow.md`, `Docs/gameplay-foundation-plan.md`, and `Docs/hub-integration-plan.md`.
5. Inspect the real scene/code diff before proposing changes.

Implementation base for this handoff:

`b3b9f298abc94c0d2620d4696ae53e3a2cddc3ff`

## Current truth in one page

- Motion Engine V1 is USER accepted and frozen.
- Gameplay/session/GameFlow foundations are implemented.
- Calibration -> Hub previously worked end-to-end.
- Calibration now waits for full `IsCalibrationComplete`, not early `IsCalibrationUsable`.
- Calibration contains a separate presentation `android01` with the idle controller.
- The serialized Calibration presentation reference points to the scene-local `android01` Animator; the persistent player's scene Animator override is null so the retargeter can control it after transition.
- Hub places the persistent player at editable `HubEntry`.
- Hub enables external-animation OFF, pose drive ON, and locomotion ON without requiring the command host, with bounded readiness diagnostics and one narrow unbound-rig recovery attempt.
- Hub now performs one automatic provider recovery after structural authority and completed calibration are healthy but body tracking remains unavailable for 0.75 seconds. It retries the request only during a bounded 1-second provider-busy window, performs at most one actual restart, preserves the coordinate convention/calibration, and gives the restarted provider approximately 9 seconds to become ready with the current camera timeout.
- The one-shot Hub diagnostic now reports provider status/message, selected camera, camera texture/playing state, bootstrap/result freshness, active backend, and recovery request/accept/performed counters.
- Hub has a scene-local `HubTerrainGroundingController` on `GoldenNeedle_HubIntegration`, assigned to `GoldenNeedle_Island`'s `TerrainCollider`, running after locomotion and modifying only root world Y.
- Terrain grounding waits until GameFlow placement/transition completion, preserves `FinalWorldPositionY - VerticalOriginY` Jump/Crouch semantics, recaptures after explicit X/Z relocation, and preserves locomotion Y when sampling is unavailable.
- Hub Main Camera has `GameplayCameraController` and eight authored presets.
- Hub Main Camera follows the persistent player with retained live heading, previous-heading retention, or a root-forward/+Z fallback; it no longer returns solely because live heading is unavailable and transitions to live heading when it appears.
- Earlier USER evidence observed camera follow working in a prior Hub presentation state; the latest regression report was that camera follow stopped when live heading was unavailable.
- Yellow portal means Boxing and is intentionally disabled until Boxing exists.
- Blue portal means Obstacle Course and is wired to `ObstacleEntry`.
- Boxing environment/gameplay and Obstacle gameplay are not implemented.
- The latest provider/camera hotfix awaits USER runtime retest.

## Do not misread the acceptance state

Implemented does not mean runtime accepted.

USER evidence currently establishes that Calibration remained complete and Hub structural authority was healthy, but body tracking was unavailable (`bodyTrackingAvailable=False`), downstream retarget counts were zero, and the camera did not follow because live heading was unavailable. The provider/camera hotfix passed focused static checks and regenerated the script assembly without a captured C# compiler diagnostic; Unity batch wrappers timed out during Licensing Client recovery, so the exit was not clean. End-to-end pose/locomotion behavior, recovery, camera fallback, uneven-terrain grounding, every camera preset, and the blue portal transition still require USER manual QA.

No Play Mode, player build, broad tests, webcam/pose test, or runtime terrain/portal test was run. The focused batch compilation was performed only to verify source and scene import integrity; the USER remains the authority for runtime acceptance.

## Hard boundaries

- Do not merge to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or rewrite shared history.
- Do not reopen provider/OpenVINO/retarget/locomotion tuning without concrete evidence.
- Do not create duplicate player/session/provider/command/flow objects.
- Do not add a gameplay follow camera to Calibration.
- Do not assign the Calibration idle controller to the persistent player.
- Do not make Hub terrain grounding a second locomotion owner: it may sample only the assigned TerrainCollider and change only root world Y after locomotion.
- Do not replace the accepted Jump/Crouch semantics; preserve them through `FinalWorldPositionY - VerticalOriginY`.
- Do not turn bounded binding recovery/readiness diagnostics into continuous rebuild or retry loops.
- Do not add a second provider restart after the one Hub recovery attempt, and do not invalidate the coordinate convention or completed calibration during recovery.
- Do not add Rigidbody, CharacterController, gravity, or another terrain/movement owner for this hotfix.
- Do not fabricate Boxing or guess its scene/spawn names.
- Do not infer portal mapping: yellow is Boxing; blue is Obstacle.
- Do not modify portal visuals to implement collision.
- Do not begin Boxing or Obstacle gameplay until the USER closes the current QA gate and authorizes the next phase.

## Immediate next action

The implementation commit above is the code checkpoint for this handoff. After the branch and documentation are pushed, wait for the USER's manual Calibration -> Hub test. If the USER reports success, record acceptance and proceed only to the next explicitly approved phase. If the USER reports failure, request the first relevant Console message and diagnose the narrow provider/recovery/camera path before changing Motion Engine.

Manual QA sequence is in `Docs/current-state.md`.

## Relevant files

- `Assets/GoldenNeedle/Gameplay/Calibration/CalibrationSceneController.cs`
- `Assets/GoldenNeedle/Gameplay/Calibration/CalibrationPresentationController.cs`
- `Assets/GoldenNeedle/Gameplay/Hub/HubSceneContextController.cs`
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipePoseProvider.cs`
- `Assets/GoldenNeedle/Gameplay/Hub/HubTerrainGroundingController.cs`
- `Assets/GoldenNeedle/Gameplay/Hub/HubPortalTrigger.cs`
- `Assets/GoldenNeedle/Gameplay/Presentation/GameplayCameraController.cs`
- `Assets/GoldenNeedle/Gameplay/Flow/GameFlowManager.cs`
- `Assets/GoldenNeedle/Gameplay/Flow/PlayerSpawnPoint.cs`
- `Assets/GoldenNeedle/Gameplay/Player/GoldenNeedlePlayerFacade.cs`
- `Assets/GoldenNeedle/Editor/GoldenNeedleCalibrationPresentationAuthoring.cs`
- `Assets/GoldenNeedle/Editor/GoldenNeedleCalibrationHubAuthoring.cs`
- `Assets/Scenes/Caliberation.unity`
- `Assets/Scenes/GoldenNeedle_Hub.unity`
- `Assets/Scenes/Obstacle Course.unity`
- `ProjectSettings/EditorBuildSettings.asset`

Treat `Docs/current-state.md` as the status authority rather than reconstructing state from old worker briefs.
