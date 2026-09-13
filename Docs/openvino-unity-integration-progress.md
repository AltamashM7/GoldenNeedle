# Golden Needle — OpenVINO Unity Integration Progress

This is the rolling resume point for the practical OpenVINO Unity integration experiment.

The intelligent Web Builder must update this file whenever a durable checkpoint/sub-step is completed and before any anticipated execution-limit stop. A replacement Builder should read this file first, verify the current remote branch HEAD, inspect the working tree, and continue from the first unfinished action rather than reconstructing the project from scratch.

## Current status

- Branch: `engine/pose-tracking-spike`
- Integration authorization: **APPROVED BY USER**
- Gate A: **PASS**
- Gate B: **PASS WITH NOTES**
- U0 plan/recovery scaffold: **COMPLETE**
- U1 architecture resolution/native lifecycle skeleton: **COMPLETE**
- U2 native OpenVINO backend: **NOT STARTED**
- U3 Unity selection/telemetry: **NOT STARTED**
- U4 USER A/B runtime QA: **NOT STARTED**
- U5 cleanup/production recommendation: **NOT STARTED**

## Proven evidence entering Unity integration

Gate B recorded-motion proof completed all 363 frames:
- Tasks reference: ~28.74 ms steady mean, ~34.80/s offline graph capacity.
- Custom TFLite graph: ~28.09 ms, ~35.59/s; exact semantic match to Tasks on the sequence.
- OpenVINO CPU FP32 graph: ~14.03 ms, ~71.26/s.
- OpenVINO pose-presence agreement: 362/363 (~99.7245%).
- Normalized XYZ RMS: ~0.01113.
- World Euclidean 3D RMS: ~0.02188 m.
- OpenVINO bridge-copy mean: ~0.2025 ms.

These are offline VIDEO-mode measurements, not Unity LIVE_STREAM end-to-end results.

## Integration invariants

- Keep stock MediaPipe/TFLite CPU fully functional as fallback.
- OpenVINO begins as experimental/selectable CPU FP32 backend.
- Reuse MediaPipe 0.10.22 preprocessing/postprocessing/tracking/world-landmark semantics.
- Do not recreate those semantics manually in C#.
- Do not change canonical/stabilization/calibration/retarget/locomotion semantics for this experiment.
- Keep 320x240 body inference input for the initial A/B test.
- Keep latest-useful-frame scheduling and no-backlog semantics.
- Do not densify/convert detector.
- Do not force D3D12.
- Do not refactor the current fixed 33-landmark provider or 20-joint canonical topology in this spike.
- Remove/disable Gate B shadow-TFLite parity instrumentation in runtime performance builds.
- No merge to `main` without explicit USER approval.
- Phase 5A remains not USER accepted; Phase 6 remains not started.

## U1 architecture decision

The least-invasive coexistence boundary is resolved and verified:

- Keep Homuler's stock MediaPipe native/managed package untouched and available as the current TFLite CPU fallback.
- Add a distinct Windows x86_64 native library named `golden_needle_openvino_pose.dll`.
- Use a versioned C ABI rather than exposing C++ types to Unity. ABI v1.0 currently exposes version/build/runtime info, an opaque context, explicit create/destroy, self-test, and a thread-local last-error path.
- Pin OpenVINO `2026.3.0`, explicit `CPU` only; do not use AUTO/GPU/NPU/HETERO fallback.
- Record the semantic-generation pins MediaPipe `0.10.22` and Homuler `0.16.3`; U2 will reuse the already-proven Gate B MediaPipe graph/inference seam rather than reimplementing pose semantics.
- Package generated DLLs only under `Assets/GoldenNeedle/Plugins/OpenVinoPose/x86_64/`. No stock MediaPipe DLL is patched, replaced, or renamed.
- Keep third-party binaries generated/untracked. Bootstrap verifies Intel's official adjacent SHA-256 sidecar before extraction.
- A dynamic native host proves `LoadLibraryExW -> ABI/version -> OpenVINO CPU resolution/self-test -> destroy -> FreeLibrary` before any vision ABI is added.

Tracked source/build workspace: `Tools/OpenVinoUnityPosePlugin/`.
Automated Windows recovery proof: `.github/workflows/openvino-unity-plugin.yml`.

## Recovery / handoff state

### U1 — COMPLETE
- Status: **COMPLETE**
- Starting SHA: `07dc8e6d0a1f634e88b4b59459fb9094bc0aab43`
- Implementation ending SHA before this documentation checkpoint: `271d4855a00a391e817e062a98d2a335546d6717`
- Files changed: isolated native workspace under `Tools/OpenVinoUnityPosePlugin/`; additive Unity staging layout under `Assets/GoldenNeedle/Plugins/OpenVinoPose/`; Windows recovery workflow; this progress file.
- What was implemented/audited: exact dependency pins, C ABI v1.0, opaque OpenVINO context, last-error route, CPU-only lifecycle self-test, x64 Release CMake build, debug-CRT import guard, additive runtime packaging, packaged-DLL lifecycle smoke, CPU-only `plugins.xml`, and third-party notice boundary.
- Authoritative verification: GitHub Actions workflow run `34765719400`, job `103746202060`, on `windows-2022` completed **success**. Bootstrap verified OpenVINO archive SHA-256 `4b26374eb342c3e0e4488b230cf8a16b6327e22b1ef12e45e5533cead06a66e3`. The x64 Release DLL built with MSVC 2022 and both build-directory and packaged `Load/Version/SelfTest/Unload` smoke tests passed. Runtime identity reported OpenVINO build `2026.3.0-22451-8a17657b995-releases/2026/3`, `device=CPU`.
- Verified packaged runtime set: `golden_needle_openvino_pose.dll`, `openvino.dll`, `openvino_intel_cpu_plugin.dll`, `openvino_tensorflow_lite_frontend.dll`, `tbb12.dll`, `tbbbind_2_5.dll`, `tbbmalloc.dll`; GPU/NPU/AUTO/HETERO exclusion guard passed.
- Stock fallback verification: workflow explicitly confirmed stock MediaPipe/TFLite plugin files were not replaced or renamed.
- Local/generated artifacts not committed: official OpenVINO archive/extraction, build outputs, runtime DLL staging, PDBs.
- Remaining risk: U1 proves native lifecycle/package viability only; it does not yet prove MediaPipe pose graph execution inside the dedicated DLL or Unity end-to-end behavior.
- Exact next action: continue immediately into U2 by adapting the existing Gate B MediaPipe 0.10.22/OpenVINO inference seam behind the native ABI, first for one real frame and exact 33 normalized/world landmark output, with shadow-TFLite raw-parity inference removed from the practical runtime path.

Primary handoff:
`Docs/worker-briefs/openvino-unity-integration-handoff.md`

Execution plan:
`Docs/openvino-unity-integration-checkpoints.md`

## Next concrete action

1. Treat U1 as complete; do not repeat the lifecycle/package investigation.
2. Implement U2 inside the dedicated native plugin by reusing MediaPipe 0.10.22 `PoseLandmarkerGraph` and the proven optional OpenVINO `InferenceCalculatorNodeImpl` seam.
3. Preserve MediaPipe preprocessing, detector decode/NMS, ROI/tracking, landmark decode/refinement, visibility/presence, world-landmark and projection semantics.
4. Extend the versioned C ABI with fail-closed pose-engine creation/destruction and a single-frame inference result carrying all 33 normalized landmarks, world landmarks, visibility/presence and explicit backend identity/timings.
5. Remove Gate B shadow-TFLite inference/raw-parity work from the practical runtime calculator. A metadata-only mechanism may be used only if required to preserve exact tensor names/order without executing TFLite inference.
6. Build and smoke-test the U2 native artifact on Windows CI, then checkpoint the result and continue into U3 when feasible.
7. Preserve the stock MediaPipe/TFLite CPU path throughout.

## Builder update template

Replace/add sections below as work advances:

### Checkpoint / sub-step
- Status:
- Starting SHA:
- Ending SHA:
- Files changed:
- What was implemented/audited:
- Verification performed:
- Local/generated artifacts not committed:
- Risks/blockers:
- Exact next action:
