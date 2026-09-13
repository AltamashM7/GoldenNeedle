# Worker Brief — OpenVINO Unity Integration U1

Role: intelligent Web Builder

Checkpoint: **U1 — Unity/native integration architecture audit**

This assignment is intentionally limited. **Do not begin U2 implementation.**

## Starting-state verification

Before editing anything:

1. Verify repository `AltamashM7/GoldenNeedle`.
2. Verify active/target branch `engine/pose-tracking-spike`.
3. Fetch/inspect the current remote branch HEAD and report the exact SHA before work.
4. Inspect current Git status if you have a local checkout. Preserve USER-local dirty files; do not clean/reset them.
5. Read:
   - `AGENTS.md`;
   - `Docs/current-state.md`;
   - `Docs/inference-architecture-reuse-audit.md`;
   - `Docs/openvino-unity-integration-checkpoints.md`;
   - Gate B implementation under `Tools/MediaPipeOpenVinoParity/`.

The latest checkpoint document supersedes older `current-state.md` wording that prohibited Unity/OpenVINO integration before Gate B evidence existed.

## Proven evidence you must preserve

Gate A: PASS.

Gate B: PASS WITH NOTES on 363 recorded frames:
- official Tasks reference complete;
- custom TFLite graph complete and exactly equal to Tasks on the sequence;
- OpenVINO CPU FP32 graph complete;
- TFLite steady offline graph capacity ~35.59/s;
- OpenVINO steady offline graph capacity ~71.26/s;
- OpenVINO pose-presence agreement vs reference 362/363 (~99.7245%);
- normalized XYZ RMS ~0.01113;
- world Euclidean 3D RMS ~0.02188 m;
- bridge-copy mean ~0.2025 ms.

These are offline VIDEO-mode numbers, not Unity LIVE_STREAM end-to-end performance.

## U1 objective

Determine the **least invasive, buildable and reversible architecture** for running the already-proven current-MediaPipe-0.10.22 + OpenVINO CPU FP32 inference seam from the existing Unity provider while keeping stock MediaPipe/TFLite CPU fully available.

This is primarily a repository/native-boundary audit. Do not write the real runtime integration in U1.

## Required inspection

Inspect the current production provider and its dependencies, including at minimum:

- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipePoseProvider.cs`
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeCanonicalPoseSource.cs`
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/PoseObservation.cs`
- relevant debug presenter/setup code under `Assets/GoldenNeedle/Debug/PoseTrackingSpike/`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- MediaPipeUnityPlugin/Homuler native plugin/binding layout actually present in this repository/package resolution
- production PoseLandmarker creation/configuration and callback path
- current readback/latest-frame scheduler and performance telemetry
- `Tools/MediaPipeOpenVinoParity/overlay/.../openvino_inference_calculator.cc`
- Gate B build/bootstrap/apply-overlay scripts and the exact MediaPipe/Homuler versions they pin
- any existing native build instructions/scripts from Homuler that are relevant to Windows x86_64 Unity Editor/player libraries.

Use upstream Homuler/MediaPipe source for the exact pinned versions where necessary. Do not assume modern/current-main APIs match 0.10.22.

## Questions U1 must answer

1. What exact native binary or binaries does Unity load today for MediaPipe Tasks on Windows x86_64, and through which managed binding/API path?
2. Where is PoseLandmarker graph construction performed relative to the Unity C# code and native library?
3. What is the narrowest integration seam for selecting `TFLITE_CPU` vs `OPENVINO_CPU_FP32` while keeping mature MediaPipe preprocessing/postprocessing/tracking/world-landmark semantics?
4. Can the Gate B inference replacement be incorporated by rebuilding the existing Homuler native library, or would a separate plugin/export be cleaner/necessary?
5. If a rebuild of the existing native library is chosen, exactly which source/build files need changes and how can stock behavior remain default?
6. If a separate native plugin is proposed, justify why it does not duplicate or reconstruct MediaPipe semantics and describe the exact data crossing the C ABI.
7. How should backend selection be passed from Unity: explicit C API/export, graph/base option, plugin configuration, environment/config side channel, or another supported mechanism? Prefer explicit and testable over hidden process-global state.
8. What Windows x86_64 OpenVINO runtime DLLs are actually required at runtime? Where should they live for Unity Editor and Windows Player loading? Do not package development-only/compiler assets.
9. Are any additional Microsoft runtime DLLs needed beyond normal Unity/Homuler requirements?
10. What licensing/third-party-notice updates are required?
11. Do intended native binaries require a narrow Git LFS rule, or are they already covered? Do not add speculative patterns.
12. What is the initialization/failure/rollback behavior? The stock TFLite backend must remain usable if OpenVINO is unavailable.
13. What exact files should U2 modify, and what exact USER-local commands/build steps will likely be required?
14. What can be validated without opening/changing the USER-owned Unity scene?

## Architecture preference

Prefer, in order:

1. **Existing Homuler/MediaPipe native task boundary + selective OpenVINO inference backend**, reusing Gate B's current-generation calculator seam.
2. A **small dedicated native bridge** only if option 1 is impractical, and only if it still returns mature MediaPipe PoseLandmarker semantics rather than rebuilding detector/ROI/landmark logic manually.
3. Manual C#/custom reconstruction of MediaPipe detector decode, ROI tracking, landmark decode/projection/world semantics is rejected unless you prove no practical mature reuse route exists.

Do not adopt Intel's old MediaPipe 0.10.3 fork wholesale.

## Things U1 must not do

- Do not implement OpenVINO runtime integration into `MediaPipePoseProvider.cs` yet.
- Do not modify canonical/stabilization/calibration/retarget/locomotion behavior.
- Do not edit `PoseTrackingSpike.unity` scene YAML.
- Do not replace the stock CPU fallback.
- Do not refactor fixed 33-landmark or 20-joint structures.
- Do not densify/convert the detector.
- Do not force D3D12.
- Do not add or update unrelated Unity packages.
- Do not begin Phase 6.
- Do not merge to `main` or open/merge a PR unless separately instructed.
- Do not run a huge native build merely to prove a hypothesis if source inspection answers it.

## Allowed changes

U1 should normally change **documentation only**. A tiny read-only diagnostic/source probe is allowed only if repository/source inspection cannot answer an essential architecture question; justify it in the report and keep it isolated.

Create one report:

`Docs/openvino-unity-u1-architecture-audit.md`

The report must contain:
- exact starting SHA;
- repository/package/native-boundary findings;
- selected architecture and rejected alternatives;
- exact proposed U2 file list;
- exact Windows native build/package strategy;
- fallback/rollback design;
- expected managed/native API shape at a high level, without prematurely freezing speculative details;
- risks/open questions;
- verification performed;
- exact ending SHA;
- explicit statement: **U2 was not started**.

You are authorized to commit/push this U1 documentation checkpoint to `engine/pose-tracking-spike` after verification. Do not touch `main`.

## Stop condition

As soon as the U1 report is committed/pushed, stop and return the report to the Orchestrator. Do not use remaining execution budget to begin U2.