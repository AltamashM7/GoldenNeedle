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

`5693997f6ad85b58aff8b5d611dee621c4e08615`

Source implementation checkpoint:

`8026852`

## Current truth in one page

- Motion Engine V1 is USER accepted and frozen.
- Gameplay/session/GameFlow foundations are implemented.
- Calibration -> Hub previously worked end-to-end.
- Calibration now waits for full `IsCalibrationComplete`, not early `IsCalibrationUsable`.
- Calibration contains a separate presentation `android01` with the idle controller.
- The serialized Calibration presentation reference points to the scene-local `android01` Animator; the persistent player's scene Animator override is null so the retargeter can control it after transition.
- Hub places the persistent player at editable `HubEntry`.
- Hub enables external-animation OFF, pose drive ON, and locomotion ON without requiring the command host, with bounded readiness diagnostics and one narrow unbound-rig recovery attempt.
- Hub now preserves the same persistent webcam/provider/OpenVINO session through normal `LoadSceneMode.Single` Calibration -> Hub entry. Fresh camera frames cause no stop/destroy/reconstruction, and Calibration remains scene-local rather than persistent or additive.
- Hub now gates recovery on camera continuity, not body tracking alone. It observes the existing fresh-frame timestamp with a 1-second freshness threshold, waits 0.75 seconds for genuine camera staleness, attempts one same-texture `Play` or `Stop` -> `Play` soft resume, and waits 1 second for a fresh frame.
- Only after soft recovery fails can Hub request one idle-safe `RestartProvider(false)` hard fallback per entry. The accepted fallback preserves calibration and coordinate convention and gives the restarted provider approximately 9 seconds to become ready with the current camera timeout.
- `StartCameraAsync` now requires a real fresh frame after `Play` before Ready. The Hub readiness coroutine continues a 1-second full-readiness/camera stabilization window, so a retained Calibration frame cannot suppress the diagnostic before continuity is checked.
- The one-shot Hub diagnostic now reports calibration/drive flags, provider status/message, selected camera, texture/playing/FPS/freshness/age, acquisition/fallback, bootstrap/result freshness, OpenVINO runtime/worker state, retarget counts, and soft/hard recovery outcomes.
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

Latest USER continuity evidence established Calibration on `HP TrueVision HD Camera` (three devices; `640x480 @ approximately 18.8 fps`; `320x240 scaled`; `OpenVINO CPU FP32`; `WebCamCPU/GetPixels32`; Ready/live result callbacks), then Hub on the same selected camera at `0.0 fps` with `ExistingReadback (fallback)` and `provider/OpenVINO worker not ready`, T-pose/no locomotion, and no Hub readiness warning. The missing warning is explained by the old coroutine exiting on retained `HasRuntimeMotionReadiness()` before observing camera death; this is a strong code-based inference, not a hardware diagnosis.

The source hotfix passed focused static checks and Unity rebuilt `Assembly-CSharp.dll` and `Assembly-CSharp-Editor.dll` with no captured C# compiler diagnostic. Unity later crashed in native worker/session teardown during shutdown after compilation, so this remains source/import verification only. End-to-end pose/locomotion behavior, continuous camera recovery, uneven-terrain grounding, every camera preset, and the blue portal transition still require USER manual QA.

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
- Do not turn bounded binding recovery/readiness/camera-continuity diagnostics into continuous rebuild or retry loops.
- Do not hard-restart the provider for a body/result gap while camera frames are fresh. Do not add a second hard provider restart after the one camera-continuity fallback attempt, and do not invalidate the coordinate convention or completed calibration during recovery.
- Do not add Rigidbody, CharacterController, gravity, or another terrain/movement owner for this hotfix.
- Do not fabricate Boxing or guess its scene/spawn names.
- Do not infer portal mapping: yellow is Boxing; blue is Obstacle.
- Do not modify portal visuals to implement collision.
- Do not begin Boxing or Obstacle gameplay until the USER closes the current QA gate and authorizes the next phase.

## Immediate next action

The source implementation checkpoint above is complete and the rolling documentation records the exact evidence and verification boundary. After the branch and documentation are pushed, wait for the USER's manual Calibration -> Hub continuity test. If the USER reports success, record acceptance and proceed only to the next explicitly approved phase. If the USER reports failure, request the single Hub warning and Runtime Camera screenshot, then diagnose the narrow provider/continuity path before changing Motion Engine.

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
