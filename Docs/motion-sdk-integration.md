# HD Motion Engine SDK connection

Mocap Adventure uses the compiled HD Motion Engine SDK, version 0.1.0-preview.2, through a local UPM archive in Packages/HDMotionEngine. Product developers do not need access to the private engine repository. Implementation source and native build/research tools are removed from this checkout's current tree; Git history is preserved.

## Current acceptance (2026-10-05)

USER ACCEPTED — PASS. The USER reports testing this isolated project in Unity, then building and testing Downloads/GoldenNeedle_MotionSDK: everything worked in both. Read motion-sdk-user-acceptance.json. Inspection of that actual build verifies eight native DLLs, four models and both SDK runtime DLL hashes against the SDK, with no Pipeline/Roslyn/engine Editor/Lab/test assemblies. This supersedes the earlier pending gameplay/rebuild and failed development-tool packaging status recorded below. No additional build, recompile or test suite was run by Codex. The previous CLI test-runner shutdown remains an unverified historical process result, separate from accepted gameplay.

The USER authorized saving this accepted migration on codex/motion-sdk-integration. The checkpoint preserves the SDK connection, required metadata and acceptance evidence; pre-existing terrain/render/settings edits and the Unity recovery scene remain local. No product-main merge has occurred. The USER wants a discussion before moving to licensing or activation design; do not begin that work yet. Release archive/receipts remain unchanged; acceptance is supplemental evidence.

## Product and engine responsibilities

GoldenNeedlePlayerFacade and GoldenNeedleBodyAnchors forward motion operations to one HDMotionEngine.MotionEngineController on the existing persistent player. The engine owns capture/inference, canonical pose, stabilization, calibration, retargeting, locomotion interpretation and reusable speech recognition. The product owns health, scene/session lifetime, spawning, terrain grounding, courses, camera framing, command meanings and context permissions.

GameplayCommandHost retains Calibration/Hub/Activity permissions and CommandProcessed notifications. UI and speech enter the same product Execute path. The product supplies its existing phrases to the SDK SpeechCommandService; microphone lifecycle follows the host's enabled state. Camera view selection stays product owned. Pose and locomotion remain separate; raw/stabilized presentation commands do not switch calibration or locomotion input.

## Open and run

Use Unity 6000.5.0f1 / URP 17.5.0. Open this project and allow UPM to resolve its local compiled SDK archive. The Editor setup bridge restores bundled models automatically and verifies them before builds. The HD Motion Engine / Install SDK Runtime Models menu is also available. Existing model .meta files are preserved. CPU OpenVINO and the TFLite fallback remain packaged; the previous costly native build is reused.

Start from Assets/Scenes/Caliberation.unity. The four enabled build scenes remain Calibration, Hub, Obstacle Course and BoxingArena_Baked. Check backend identity in the existing diagnostics. This checkout is isolated from the original GoldenNeedle directory, which retains all eight original local edits.

## Earlier verification and pending-check history (superseded by current acceptance above)

- Actual player prefab's 633 serialized engine fields/references compared successfully with the untouched authored prefab using Editor APIs. All four enabled scenes opened without missing scripts.
- 243/243 engine regressions pass against the final Player-compiled SDK, including the carried sideways-step cases. Runtime/vendor DLLs have zero UnityEditor assembly references.
- Native OpenVINO CPU graph and blank-frame inference pass in the SDK-only game. This is not webcam/visual acceptance or a measured live latency result.
- Product XML reports 33/33 tests pass. The initial CLI invocation timed out during shutdown; the clean rerun is delegated to the USER.
- The earlier Windows x64 build completed: Unity reported zero errors, 504 warnings and 1,124,093,058 bytes. All eight native DLL and four model hashes match the SDK, but packaging FAILED because Unity.Pipeline.dll and its two Roslyn compiler DLLs were included. The Editor assembly filter now excludes those tools; it compiles against the installed Unity API. A rebuilt Player has not been verified. Existing build output is retained for evidence; do not treat it as the final package.
- USER gameplay acceptance remains pending: webcam/calibration completion, transition to Hub, persistent tracking, pose/locomotion including sideways steps, camera voice presets, activity transitions and return/recovery behavior. The previous USER blue-skeleton Lab acceptance covered the earlier source baseline only.

Activation, device restrictions and the usage agreement are deferred until the product connection is accepted.

## Updating the engine

The owner edits and tests the private source Lab, compiles runtime DLLs for StandaloneWindows64 with PlayerBuildInterface.CompilePlayerScripts, and builds a new versioned SDK archive with preserved DLL importer GUIDs. Do not use Editor-compiled vendor runtime DLLs in a shipped SDK. Run compiled-consumer regressions and retain source SHA, target, file hashes, native bundle provenance and notices.

Replace the product's archive with the newly verified version and update its UPM dependency through Unity Package Manager. Keep the previous archive/version for rollback. Regular SDK updates retain compiled script identities; replacing old .cs components with DLL components required the one-time migration already verified here.

No publication/merge is performed by the model-restoration bootstrap. The USER explicitly authorized private main publication on 2026-10-04, resolving the earlier automatic approval rejection. SDK preview 2 is published privately: [v0.1.0-preview.2](https://github.com/AltamashM7/hd-motion-engine/releases/tag/v0.1.0-preview.2). Release checkpoint: 9dce2c1103e6c6515a2b0a954f6af1f324b4ca19. The archive, manifest and receipt hashes match GitHub; USER game acceptance and the fresh Player rebuild remain pending. No product-main merge has occurred.

## USER-owned check instructions from 2026-10-04 (gameplay/build now accepted)

Full Windows builds and similarly long-running tasks belong to the USER. Codex should inspect results and make bounded fixes, without launching or repeatedly waiting for these jobs. No new build or test suite was started after this instruction.

Open this isolated project in Unity 6000.5.0f1 and start from Assets/Scenes/Caliberation.unity. Complete calibration and enter the Hub; check body pose, sideways steps, tracking continuity, camera speech (for example front view/back view), and the existing blue Obstacle portal. Raw/stabilized presentation uses the existing speech commands; the earlier Lab F12 test is separate evidence.

For Windows verification, build the four enabled scenes into a fresh output folder using Unity's Build Profiles. ExcludeDevelopmentToolingFromPlayer runs automatically in the Editor and retains Pipeline for Editor development. The recorded built DLLs have no other assembly references to Pipeline or its two Roslyn compiler DLLs. Re-check the rebuilt output for required SDK/native/model files and absence of development tooling. Keep the old output as evidence; using a fresh directory avoids leftover DLLs.

For a clean product test result, run the Editor Test Runner's EditMode suite (33 cases) and retain the final result/runner completion. The recorded initial assertions all pass; successful CLI shutdown has not yet been established. Share new results with Codex for inspection and bounded follow-up fixes.
