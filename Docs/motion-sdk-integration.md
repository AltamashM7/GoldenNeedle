# HD Motion Engine SDK connection

Mocap Adventure consumes the compiled SDK 0.1.0-preview.2 through Packages/HDMotionEngine/hd-motion-engine-sdk-0.1.0-preview.2-windows-x64.tgz. The private source Git URL is not a product dependency. SDK archive SHA256: f3fec3e5e55355d936d4eebe54d715a2806845243968d7495ed13659fa3da859.

## Acceptance and repository promotion

USER ACCEPTED — PASS (2026-10-05): the USER tested the isolated Unity project, built Downloads/GoldenNeedle_MotionSDK and tested the executable; everything worked in both. motion-sdk-user-acceptance.json records read-only payload hash verification for both managed runtime DLLs, eight native DLLs and four models, with no development-tool assemblies. No additional measured FPS/latency or BuildReport is claimed.

Game migration commit: 522e29a0de8b07e59639136600233742afd6a08d. Merge 0dd4bde preserves the newer team documentation checkpoint. The USER authorizes pushing and applying the separation to the original gameplay/foundation checkout. Existing asset/settings edits are preserved separately. The USER explicitly approves merging this verified separation into main, so default clones use the same SDK-only product tree. The original checkout remains on gameplay/foundation. Working-branch GitHub boundary check [37312826569](https://github.com/AltamashM7/GoldenNeedle/actions/runs/37312826569) passed at 160560f; runtime and SDK are unchanged.

Source, vendor implementation, engine Lab, native research and obsolete CI are excluded from the active product. Historical plans/workflows are preserved in private Docs/HistoricalGoldenNeedle; existing Git history and historical branches remain intact. Product boundary verification is python Tools/verify_motion_sdk_boundary.py, also run by GitHub without Unity/native builds.

## Open and run

Open the original GoldenNeedle project with Unity 6000.5.0f1 / URP 17.5.0 after branch promotion. Allow UPM to resolve the bundled archive. The compiled SDK Editor installer automatically restores its runtime models; HD Motion Engine / Install SDK Runtime Models is also available. Existing model metadata is preserved and generated binaries are ignored. Open Assets/Scenes/Caliberation.unity and use the existing authored flow. No new scene or portal behavior is introduced by this separation.

The engine owns capture/inference, canonical pose/trust, stabilization, calibration, retargeting, locomotion interpretation and reusable speech recognition. The product owns persistent player/session, health, scene/spawn/terrain rules, courses, camera/UI, command meanings and contextual permissions. GoldenNeedlePlayerFacade/GoldenNeedleBodyAnchors wrap MotionEngineController. GameplayCommandHost supplies product phrases to SpeechCommandService and preserves the common Execute path for voice/UI. POSE != LOCOMOTION.

Keep CPU OpenVINO and the stock TFLite fallback packaged. Native/model binaries and third-party notices are required SDK dependencies. Legacy engine namespaces and model paths remain for compatibility and do not require source in the game. ExcludeDevelopmentToolingFromPlayer retains Unity Pipeline/Roslyn for Editor development and excludes them from Player builds.

## Evidence and updates

The actual prefab migration preserved 633 authored engine fields/references; four enabled scenes were checked with no missing scripts. Static inspection found no tracked asset references to 92 removed engine scripts. Earlier source and independent compiled SDK suites each passed 243/243. Product XML has 33/33 passing assertions; its CLI shutdown timed out, so clean runner completion is not claimed. Earlier packaging failure in motion-sdk-windows-build-result.json is historical and superseded by the accepted USER build/payload record.

Published private SDK: [v0.1.0-preview.2](https://github.com/AltamashM7/hd-motion-engine/releases/tag/v0.1.0-preview.2), release checkpoint 9dce2c1103e6c6515a2b0a954f6af1f324b4ca19, runtime baseline d453c3563319151f82ddc8d020011597046c23ee. Immutable archive/receipts are unchanged.

For improvements, develop/test the private source Lab, compile Player runtime DLLs, verify a new versioned archive and publish it privately with source/native provenance and notices. Update this product's archive/UPM dependency deliberately, retain prior version evidence and perform product acceptance. Do not use Editor-compiled vendor runtime DLLs in a Player or install source and compiled modes together.

Full Windows builds and similarly long-running checks remain USER owned. No build/compile/test suite is started for repository promotion. Licensing/activation and further product tasks are discussed separately when requested.
