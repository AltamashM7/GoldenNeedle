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
- U3 Unity selection/telemetry: **IMPLEMENTED; MANAGED ABI/STATIC VALIDATION PASS; UNITY RUNTIME QA PENDING**
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

- Keep stock MediaPipe/TFLite CPU fully functional as fallback and default until USER acceptance.
- OpenVINO begins as explicitly selectable experimental CPU FP32 backend.
- Reuse MediaPipe 0.10.22 preprocessing/postprocessing/tracking/world-landmark semantics.
- Do not recreate those semantics manually in C#.
- Do not change canonical/stabilization/calibration/retarget/locomotion semantics for this experiment.
- Keep 320x240 body inference input for the initial A/B test.
- Keep latest-useful-frame scheduling and no-backlog semantics.
- Do not densify/convert detector.
- Do not force D3D12.
- Do not refactor the current fixed 33-landmark provider or 20-joint canonical topology in this spike.
- Remove/disable Gate B shadow-TFLite parity inference in runtime performance builds.
- No merge to `main` without explicit USER approval.
- Phase 5A remains not USER accepted; Phase 6 remains not started.

## U1 — COMPLETE

- Starting SHA: `07dc8e6d0a1f634e88b4b59459fb9094bc0aab43`.
- Completion documentation SHA: `fb1e58e175c34a777ee86344b18aec33e4fe231d`.
- Architecture: distinct additive Windows x86_64 DLL `golden_needle_openvino_pose.dll`, versioned C ABI, stock Homuler/TFLite untouched.
- OpenVINO: exact `2026.3.0`, explicit `CPU` only; no AUTO/GPU/NPU/HETERO fallback.
- MediaPipe/Homuler generation pins: `0.10.22` / `0.16.3`.
- Authoritative verification: GitHub Actions run `34765719400`, job `103746202060`, Windows 2022 success.
- OpenVINO archive SHA-256: `4b26374eb342c3e0e4488b230cf8a16b6327e22b1ef12e45e5533cead06a66e3`.
- Build-directory and packaged `Load/Version/SelfTest/Unload` passed.
- Stock MediaPipe/TFLite plugin files were not replaced or renamed.

## U2 — IMPLEMENTED; WINDOWS PROOF IN PROGRESS

- Starting SHA: `fb1e58e175c34a777ee86344b18aec33e4fe231d`.
- Main implementation SHA: `e2efe61013fbfa14a43c2d451a21087530ae3d7e` (`feat: implement U2 MediaPipe OpenVINO native backend`).
- CI environment fixes: `33d57e20c1a3ee7c5fa8322fc68f738e3691183a` (MSYS2 utilities) and `fcb3ba53b1847afc6e1e5e3ffd36a5d8e61315f6` (native Bazelisk launcher).
- Native architecture: Bazel-build a renamed/additive MediaPipe/OpenVINO DLL so MediaPipe graph/calculator registration and the OpenVINO inference calculator live in the same native image. Stock `mediapipe_c.dll` remains untouched.
- Reused semantics: MediaPipe 0.10.22 `PoseLandmarkerGraph`, preprocessing, detector decode/NMS, ROI/tracking, landmark decode/refinement, visibility/presence, world-landmark and projection semantics.
- Replaced execution only: detector and landmark neural inference use in-process OpenVINO `CPU`, latency-oriented FP32 through the proven inference seam.
- Runtime calculator does **not** execute the Gate B shadow TFLite/raw-parity inference. TFLite is metadata-only for exact tensor names/order required by MediaPipe's `InferenceIoMapper`.
- ABI v1.1 exposes pose-engine lifecycle and synchronous RGBA processing with exactly 33 normalized/world landmarks, visibility/presence flags, backend identity, detector-run state and graph/inference/copy timings.
- Exact detector/landmark model files are extracted from the existing production task bundle and identity-checked; no conversion, densification or alternate weights.
- CI semantic smoke dynamically loads the DLL and processes MediaPipe's real `pose.jpg` twice to verify lifecycle, exact model identity, 33 normalized/world landmark output and stream-mode state execution.

### U2 CI history

1. Run `34767029284`, job `103749712693`: failed before C++ build because the hosted MSYS2 install lacked `git`, `patch`, `unzip`, `zip`; fixed in workflow.
2. Run `34767075477`, job `103749841159`: bootstrap reached Bazel but hosted `bazelisk` resolved to a PowerShell shim incompatible with the native-process bootstrap; fixed by pinning native Bazelisk.
3. Active authoritative run: `34767143321`, job `103750021491`, implementation head `fcb3ba53b1847afc6e1e5e3ffd36a5d8e61315f6`. Checkout, MSYS2 utilities, native Bazelisk and exact pinned source/OpenVINO bootstrap have passed. The actual Bazel build + lifecycle + real-frame semantic smoke remains in progress; additive packaging is pending.

- U2 is **not COMPLETE** until the native build/real-frame smoke and package complete successfully.
- If the active run fails, use its first actual compiler/runtime failure as the source of truth and make a narrow fix; do not change pins or backend.

## U3 — IMPLEMENTED; MANAGED VALIDATION PASS; UNITY RUNTIME QA PENDING

### Managed interop/runtime wrapper

- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/OpenVinoPoseNative.cs`
- implementation commit `d06b547979952707c93238f38cfe261e177e5b9a`; Unity metadata commit `597101f98ed9b1673211c8b124111b30da3281e7`.
- `PoseInferenceBackend.MediaPipeTfliteCpu = 0`; stock TFLite therefore remains the serialized/default-safe value.
- `PoseInferenceBackend.OpenVinoCpuFp32 = 1` is explicitly selectable.
- Managed owner creates/self-tests/destroys the v1.1 native context and pose engine, requires explicit CPU/backend identity, pins input memory for P/Invoke, validates timestamp and exact 33-landmark output, and exposes runtime/engine identity plus graph/detector/landmark/copy timings.
- The wrapper has no Unity API calls and can execute its native lifecycle/inference on a worker thread.

### Provider wiring

- Fail-closed source transformation commit `a0a09ba62b230cfb4fa33c8f7bf53dee6a691b4d`; audited transformation workflow `34769023319`, job `103755075832` completed success.
- Generated provider commit: `aab549bf9b1a41419fa2eb16809819662e746e0b` (`feat: wire U3 selectable OpenVINO pose backend`).
- The transformation required the exact pre-U3 provider Git blob `7c9886a8bd888a72ed2405c450351e3c6cf1926f` and failed closed on drift.
- Existing stock Homuler/TFLite branch is preserved and remains default.
- OpenVINO selection reuses the same webcam, orientation state, body downscale, readback/prepared-frame pipeline, inference cadence gate, no-backlog policy, `PoseObservation`, `PoseTrustClassifier`, grace/stale behavior, world fields and downstream canonical source.
- The same prepared RGBA pixels are copied once into one reusable managed worker buffer; there is still at most one inference outstanding, so that buffer cannot race a subsequent launch.
- Native worker failure is published back to the main-thread provider failure path; no automatic silent backend fallback is introduced during an explicitly selected OpenVINO session.
- Restart/teardown waits any native inference/bootstrap work before disposing the native engine/context.
- Telemetry now includes selected/effective backend, OpenVINO managed input-copy, graph, detector, landmark, bridge timings and detector-run state in addition to the existing camera/readback/request→result/frame→result/pressure diagnostics.

### U3 managed validation

- `Tools/OpenVinoUnityPosePlugin/tests/ManagedAbiSmoke/` compiles the real `OpenVinoPoseNative.cs` source with .NET 8.
- Clean authoritative validation run: `34769239438` completed success.
- ABI smoke output: `GNOVPOSE_MANAGED_ABI_SMOKE=PASS`.
- Managed ABI = `65537` / v1.1; landmark count = 33; native result layout = **1280 bytes**.
- Test also asserts key field offsets, inline 33-landmark array initialization, Cdecl entry points, library name, and stock/default backend enum value `0`.
- Provider static integration invariants passed.
- This is not a substitute for Unity Editor/Windows hardware runtime validation.

### U3 commits after provider wiring

- ABI smoke project: `1bd7c85ae945845ebbcee53cb8b819c5155eab8f`.
- ABI assertions: `3647e59aec555cfb63c26ee20f38624ba702c013`, corrected enum-safe test `192646ad6225aa68a953cc18bf1505b8770854f4`.
- Managed validation workflow: `f10bd788da13d6638d7986aae8271a0822fe7d93`; noise-free workflow fix: `63cd710a404452802e454480ddfbe2c1ba5da6a9`.

Primary handoff: `Docs/worker-briefs/openvino-unity-integration-handoff.md`.
Execution plan: `Docs/openvino-unity-integration-checkpoints.md`.

## Exact next action

1. Continue checking U2 run `34767143321`, job `103750021491` until definitive success/failure.
2. On U2 failure, fix the first real compiler/runtime issue narrowly. On success, record U2 COMPLETE with exact native build/smoke/package evidence.
3. Before USER U4, perform final read-only audit of the U3 provider diff and generated package/run instructions; do not modify the USER scene merely to select a backend.
4. Prepare a minimal USER A/B procedure that preserves stock TFLite as baseline and explicitly selects OpenVINO for the second run, gathering backend identity, result rate, request→result, frame→result, readback, render and OpenVINO internal timings.
5. Stop at the genuine USER Windows/Unity runtime QA boundary; do not claim OpenVINO production acceptance before that evidence.
