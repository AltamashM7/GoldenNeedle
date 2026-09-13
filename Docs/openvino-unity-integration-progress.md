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
- U2 native OpenVINO backend: **IMPLEMENTED; WINDOWS BUILD/REAL-FRAME PROOF IN PROGRESS**
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

## U1 — COMPLETE

- Starting SHA: `07dc8e6d0a1f634e88b4b59459fb9094bc0aab43`.
- Implementation ending SHA before completion documentation: `271d4855a00a391e817e062a98d2a335546d6717`.
- Completion documentation SHA: `fb1e58e175c34a777ee86344b18aec33e4fe231d`.
- Architecture: distinct additive Windows x86_64 DLL `golden_needle_openvino_pose.dll`, versioned C ABI, stock Homuler/TFLite untouched.
- OpenVINO: exact `2026.3.0`, explicit `CPU` only; no AUTO/GPU/NPU/HETERO fallback.
- Semantic-generation pins: MediaPipe `0.10.22`, Homuler `0.16.3`.
- Authoritative verification: GitHub Actions run `34765719400`, job `103746202060`, Windows 2022 success.
- Verified OpenVINO archive SHA-256: `4b26374eb342c3e0e4488b230cf8a16b6327e22b1ef12e45e5533cead06a66e3`.
- Both build-directory and packaged `Load/Version/SelfTest/Unload` passed.
- Packaged CPU runtime set: `golden_needle_openvino_pose.dll`, `openvino.dll`, `openvino_intel_cpu_plugin.dll`, `openvino_tensorflow_lite_frontend.dll`, `tbb12.dll`, `tbbbind_2_5.dll`, `tbbmalloc.dll`; generated CPU-only `plugins.xml`.
- Stock MediaPipe/TFLite plugin files were not replaced or renamed.

## U2 — IMPLEMENTED; WINDOWS PROOF IN PROGRESS

- Starting SHA: `fb1e58e175c34a777ee86344b18aec33e4fe231d`.
- Main implementation SHA: `e2efe61013fbfa14a43c2d451a21087530ae3d7e` (`feat: implement U2 MediaPipe OpenVINO native backend`).
- CI environment fixes: `33d57e20c1a3ee7c5fa8322fc68f738e3691183a` (MSYS2 utilities) and `fcb3ba53b1847afc6e1e5e3ffd36a5d8e61315f6` (native Bazelisk launcher).
- Native architecture: Bazel-build a renamed/additive MediaPipe/OpenVINO DLL so MediaPipe graph/calculator registration and the OpenVINO inference calculator live in the same native image. Stock `mediapipe_c.dll` remains untouched.
- Reused semantics: MediaPipe 0.10.22 `PoseLandmarkerGraph`, preprocessing, detector decode/NMS, ROI/tracking, landmark decode/refinement, visibility/presence, world-landmark and projection semantics.
- Replaced execution only: detector and landmark neural inference use in-process OpenVINO `CPU`, latency-oriented FP32 through the proven `InferenceCalculatorNodeImpl` seam.
- Runtime calculator does **not** execute the Gate B shadow TFLite/raw-parity inference. TFLite is retained only as metadata authority for exact tensor names/order needed by MediaPipe's `InferenceIoMapper`.
- ABI advanced to v1.1. It exposes fail-closed pose-engine create/destroy/info and synchronous RGBA processing with exactly 33 normalized/world landmarks, visibility/presence flags, backend identity, detector-run state and graph/inference/copy timings.
- Exact model inputs are extracted from the existing production task bundle with the established identity gate; no conversion, densification or alternate model weights.
- CI semantic smoke dynamically loads the built DLL and processes MediaPipe's real `pose.jpg` twice to verify lifecycle, exact production model identity, 33 normalized/world landmark output and stream-mode state execution.
- Generated binaries/runtime dependencies remain untracked and are staged only under the additive `Assets/GoldenNeedle/Plugins/OpenVinoPose/x86_64/` package path.

### U2 CI history so far

1. Run `34767029284`, job `103749712693`: failed before C++ build because the Windows runner's MSYS2 install lacked required `git`, `patch`, `unzip`, `zip`. Fixed by explicit package installation in the workflow.
2. Run `34767075477`, job `103749841159`: MSYS2 setup passed; bootstrap then failed because `bazelisk` resolved to GitHub runner PowerShell shim `C:\npm\prefix\bazelisk.ps1`, while the proven bootstrap launches Bazelisk as a native process. Fixed by installing/pinning a native Bazelisk executable ahead of the shim.
3. Active authoritative run: `34767143321`, job `103750021491`, head `fcb3ba53b1847afc6e1e5e3ffd36a5d8e61315f6`. Checkout, MSYS2 setup, native Bazelisk setup, and exact pinned Homuler/MediaPipe/OpenVINO bootstrap have all passed. The actual U2 Bazel build + lifecycle + real-frame semantic smoke is currently running. Packaging has not yet completed.

- U2 is **not** yet COMPLETE and must not be described as PASS until run `34767143321` (or a focused successor) proves the actual native build/real-frame smoke and additive package.
- If the active build fails, diagnose the first actual compiler/runtime error from its completed job logs and make a focused fix. Do not change pins or fall back to another backend.
- If it passes, update this file to **U2 COMPLETE**, record the run/job evidence and continue directly into U3.

## U3 preparation already audited, but implementation not started

The U3 contract remains the one in `Docs/openvino-unity-integration-checkpoints.md`:
- stock MediaPipe/TFLite remains default until USER acceptance;
- selectable experimental OpenVINO CPU FP32 backend;
- same webcam source, orientation, 320x240 body input, latest-useful-frame scheduling and `PoseObservation` publication semantics;
- same canonical mapper, stabilization, calibration, retargeting and locomotion consumers;
- DirectCPU readback and safe restart/teardown behavior preserved;
- expose selected/effective backend plus camera/readback/request→result/frame→result/result-rate/pressure/render/OpenVINO timing/bridge telemetry useful for U4 A/B testing.

Primary handoff: `Docs/worker-briefs/openvino-unity-integration-handoff.md`.
Execution plan: `Docs/openvino-unity-integration-checkpoints.md`.

## Exact next action

1. Inspect GitHub Actions run `34767143321`, job `103750021491` until it reaches a definitive result.
2. If it fails, use the completed job log's first actual U2 compiler/runtime failure as the source of truth and commit a narrow fix.
3. If it passes, record U2 COMPLETE with exact build/smoke/package evidence.
4. Continue immediately into U3 managed backend selection and telemetry without changing downstream motion semantics.
5. Stop only at the first genuine USER hardware/visual A/B QA requirement (U4) or a real architectural blocker.
