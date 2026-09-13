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
- U3 Unity selection/telemetry: **IMPLEMENTED; MANAGED ABI/STATIC VALIDATION PASS; NATIVE PACKAGE PROOF + UNITY RUNTIME QA PENDING**
- U4 USER A/B runtime QA: **READY PROCEDURE WRITTEN; NOT STARTED**
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
3. Run `34767143321`, job `103750021491`, head `fcb3ba53b1847afc6e1e5e3ffd36a5d8e61315f6`: checkout, MSYS2 utilities, native Bazelisk and exact pinned bootstrap all passed. The actual U2 build/smoke step was **cancelled by the workflow's 60-minute job limit** and packaging was skipped. The surfaced step evidence did not identify a compiler/link/runtime failure before cancellation, so this is treated as an insufficient CI budget, not a U2 PASS or backend failure.
4. Later same-60-minute proof run `34769414654`, job `103756144884`, head `bf73a6b738f5d843e7c06fd5d9b4ac330dbb425a`: prerequisites passed and the native build/smoke entered `in_progress`. It is superseded as the final proof by the widened-budget recovery run below.
5. Timeout-only recovery commit: `43e5a888b80874ace1643dcdcfc4cb5a35eb919e` (`ci: allow full U2 Windows native build`). The only workflow change is `timeout-minutes: 60` -> `120`; source, dependency pins, models, backend and build commands are unchanged.
6. Current authoritative recovery run: `34770530769`, job `103759179464`, head `43e5a888b80874ace1643dcdcfc4cb5a35eb919e`. This must prove the actual native build, lifecycle smoke, real-frame 33-landmark semantic smoke and additive packaging before U2 can be marked COMPLETE.

- U2 is **not COMPLETE** until the native build/real-frame smoke and package complete successfully.
- If the 120-minute proof exposes a real compiler/link/runtime error, use that first actual failure as the source of truth and make a narrow fix; do not change pins, models or backend.
- Do not classify timeout/cancellation as a semantic PASS.

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
- Telemetry includes selected/effective backend, OpenVINO managed input-copy, graph, detector, landmark, bridge timings and detector-run state in addition to existing camera/readback/request-to-result/frame-to-result/pressure diagnostics.

### U3 managed validation

- `Tools/OpenVinoUnityPosePlugin/tests/ManagedAbiSmoke/` compiles the real `OpenVinoPoseNative.cs` source with .NET 8.
- Clean authoritative validation run: `34769239438` completed success.
- ABI smoke output: `GNOVPOSE_MANAGED_ABI_SMOKE=PASS`.
- Managed ABI = `65537` / v1.1; landmark count = 33; native result layout = **1280 bytes**.
- Test asserts key field offsets, inline 33-landmark array initialization, Cdecl entry points, library name, and stock/default backend enum value `0`.
- Provider static integration invariants passed.
- This is not a substitute for Unity Editor/Windows hardware runtime validation.

### U3/U4 preparation

- One-command local additive preparation: `Tools/OpenVinoUnityPosePlugin/scripts/prepare_unity.ps1` (commit `588ef5df0f3d812b53d95d976e7e8b9bad87c105`). It bootstraps/builds/packages, checks stock MediaPipe files are not replaced, checks exact extracted model identities, and prints `OPENVINO_UNITY_LOCAL_PREP=PASS` only after successful preparation.
- Updated tool documentation: `bf73a6b738f5d843e7c06fd5d9b4ac330dbb425a`.
- USER A/B procedure: `Docs/openvino-unity-ab-qa.md`, commit `17d9aced86e17a1dca757a6ee549e3bbb1571057`.
- The A/B procedure preserves the stock backend/default, requires identical camera/readback/320x240/downstream settings, captures F7 backend/result-rate/latency/pressure telemetry, OpenVINO internal timings, fast-motion fidelity, partial-body recovery and restart/teardown behavior.
- Generated `.dll`, `.tflite`, `plugins.xml`, generated `.meta`, build and bootstrap artifacts remain ignored/untracked.

Primary handoff: `Docs/worker-briefs/openvino-unity-integration-handoff.md`.
Execution plan: `Docs/openvino-unity-integration-checkpoints.md`.

## Exact next action

1. Follow current authoritative U2 recovery run `34770530769`, job `103759179464`, to a definitive result.
2. If it fails with a real compiler/link/runtime error, fix the first actual error narrowly; if it succeeds, record U2 COMPLETE with exact native build/smoke/package evidence.
3. On U2 native/package success, classify U3 as COMPLETE for implementation/pre-USER validation and stop at the genuine U4 USER Windows/Unity A/B boundary.
4. USER U4 must follow `Docs/openvino-unity-ab-qa.md`; do not claim runtime/production acceptance before USER evidence.
5. Do not start U5 recommendation/cleanup decisions from CI alone, do not merge to `main`, and do not start Phase 6.
