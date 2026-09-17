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

`08b02c24946e5d1ec9a88a168584aaaf0106ce7f`

## Current truth in one page

- Motion Engine V1 is USER accepted and frozen.
- Gameplay/session/GameFlow foundations are implemented.
- Calibration -> Hub previously worked end-to-end.
- Calibration now waits for full `IsCalibrationComplete`, not early `IsCalibrationUsable`.
- Calibration contains a separate presentation `android01` with the idle controller.
- The serialized Calibration presentation reference points to the scene-local `android01` Animator; the persistent player's scene Animator override is null so the retargeter can control it after transition.
- Hub places the persistent player at editable `HubEntry`.
- Hub enables external-animation OFF, pose drive ON, and locomotion ON without requiring the command host, with bounded readiness diagnostics and one narrow unbound-rig recovery attempt.
- Hub has a scene-local `HubTerrainGroundingController` on `GoldenNeedle_HubIntegration`, assigned to `GoldenNeedle_Island`'s `TerrainCollider`, running after locomotion and modifying only root world Y.
- Terrain grounding waits until GameFlow placement/transition completion, preserves `FinalWorldPositionY - VerticalOriginY` Jump/Crouch semantics, recaptures after explicit X/Z relocation, and preserves locomotion Y when sampling is unavailable.
- Hub Main Camera has `GameplayCameraController` and eight authored presets.
- USER has observed camera follow working.
- Yellow portal means Boxing and is intentionally disabled until Boxing exists.
- Blue portal means Obstacle Course and is wired to `ObstacleEntry`.
- Boxing environment/gameplay and Obstacle gameplay are not implemented.
- The latest Animator/pose/locomotion correction awaits USER runtime retest.

## Do not misread the acceptance state

Implemented does not mean runtime accepted.

USER evidence currently establishes that Calibration waited longer after the gate fix and that the Hub camera followed before this latest correction. The new scene wiring, terrain component, and scripts have passed focused static checks and clean Unity batch compilation, but end-to-end pose/locomotion behavior, uneven-terrain grounding, every camera preset, and the blue portal transition still require USER manual QA.

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
- Do not fabricate Boxing or guess its scene/spawn names.
- Do not infer portal mapping: yellow is Boxing; blue is Obstacle.
- Do not modify portal visuals to implement collision.
- Do not begin Boxing or Obstacle gameplay until the USER closes the current QA gate and authorizes the next phase.

## Immediate next action

The implementation commit above is the code checkpoint for this handoff. After the branch is pushed, wait for the USER's manual Calibration -> Hub test. If the USER reports success, record acceptance and proceed only to the next explicitly approved phase. If the USER reports failure, request the first relevant Console message and diagnose the narrow player authority/grounding path issue before changing Motion Engine.

Manual QA sequence is in `Docs/current-state.md`.

## Relevant files

- `Assets/GoldenNeedle/Gameplay/Calibration/CalibrationSceneController.cs`
- `Assets/GoldenNeedle/Gameplay/Calibration/CalibrationPresentationController.cs`
- `Assets/GoldenNeedle/Gameplay/Hub/HubSceneContextController.cs`
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
