# Golden Needle — OpenVINO Unity Integration Progress

This is the rolling resume point for the practical OpenVINO Unity integration experiment.

A replacement Builder should read this file first, verify the current remote `engine/pose-tracking-spike` HEAD, and continue from the first unfinished action. Do not reconstruct completed work from scratch.

## Current status

- Branch: `engine/pose-tracking-spike`
- Integration authorization: **APPROVED BY USER**
- Gate A: **PASS**
- Gate B: **PASS WITH NOTES**
- U0 plan/recovery scaffold: **COMPLETE**
- U1 architecture resolution/native lifecycle skeleton: **COMPLETE**
- U2 native OpenVINO backend: **COMPLETE — WINDOWS NATIVE BUILD, LIFECYCLE, REAL-FRAME 33-LANDMARK SEMANTIC SMOKE AND ADDITIVE PACKAGE PROOF PASS**
- U3 Unity selection/telemetry: **COMPLETE FOR IMPLEMENTATION / PRE-USER VALIDATION — MANAGED ABI, PROVIDER WIRING AND NATIVE PACKAGE PROOF PASS; LIVE UNITY QA REMAINS U4**
- U4 USER A/B runtime QA: **READY; NOT STARTED**
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

- Keep stock MediaPipe/TFLite CPU fully functional as fallback and serialized/default backend until USER acceptance.
- OpenVINO remains explicitly selectable experimental CPU FP32 until U4 USER acceptance.
- Reuse MediaPipe 0.10.22 preprocessing/postprocessing/tracking/world-landmark semantics.
- Do not recreate those semantics manually in C#.
- Do not change canonical/stabilization/calibration/retarget/locomotion semantics for this experiment.
- Keep 320x240 body inference input for the initial A/B test.
- Keep latest-useful-frame scheduling and no-backlog semantics.
- Do not densify/convert detector.
- Do not force D3D12.
- Do not refactor the fixed 33-landmark provider or 20-joint canonical topology in this spike.
- Runtime performance builds must not execute Gate B shadow-TFLite neural inference.
- No merge to `main` without explicit USER approval.
- Phase 5A remains not USER accepted; Phase 6 remains not started.

## U1 — COMPLETE

- Starting SHA: `07dc8e6d0a1f634e88b4b59459fb9094bc0aab43`.
- Completion documentation SHA: `fb1e58e175c34a777ee86344b18aec33e4fe231d`.
- Architecture: distinct additive Windows x86_64 DLL `golden_needle_openvino_pose.dll`, versioned C ABI, stock Homuler/TFLite untouched.
- OpenVINO: exact `2026.3.0`, explicit `CPU` only; no AUTO/GPU/NPU/HETERO fallback.
- MediaPipe/Homuler pins: `0.10.22` / `0.16.3`.
- Authoritative verification: GitHub Actions run `34765719400`, job `103746202060`, Windows 2022 success.
- OpenVINO archive SHA-256: `4b26374eb342c3e0e4488b230cf8a16b6327e22b1ef12e45e5533cead06a66e3`.

## U2 — COMPLETE

### Implementation

- Starting SHA: `fb1e58e175c34a777ee86344b18aec33e4fe231d`.
- Main implementation SHA: `e2efe61013fbfa14a43c2d451a21087530ae3d7e` (`feat: implement U2 MediaPipe OpenVINO native backend`).
- Native architecture: Bazel-build a renamed/additive MediaPipe/OpenVINO DLL so graph/calculator registration and the OpenVINO inference calculator live in the same native image. Stock `mediapipe_c.dll` remains untouched.
- Reused semantics: MediaPipe 0.10.22 `PoseLandmarkerGraph`, preprocessing, detector decode/NMS, ROI/tracking, landmark decode/refinement, visibility/presence, world-landmark and projection semantics.
- Replaced execution only: detector and landmark neural inference use in-process OpenVINO `CPU`, latency-oriented FP32.
- Runtime calculator does **not** execute Gate B shadow TFLite/raw-parity inference. TFLite is metadata-only for exact tensor naming/order required by MediaPipe's `InferenceIoMapper`.
- ABI v1.1 exposes pose-engine lifecycle and synchronous RGBA processing with exactly 33 normalized/world landmarks, visibility/presence flags, backend identity, detector-run state and graph/inference/copy timings.
- Exact detector/landmark model files are extracted from the existing production task bundle and identity-checked; no conversion, densification or alternate weights.

### Build/recovery history

1. Run `34767029284`, job `103749712693`: hosted MSYS2 lacked required build utilities; fixed.
2. Run `34767075477`, job `103749841159`: Bazelisk resolved to an incompatible PowerShell shim; fixed with pinned native Bazelisk.
3. Runs `34767143321` / `34769414654`: 60-minute CI budget was insufficient; no semantic failure established.
4. Commit `43e5a888b80874ace1643dcdcfc4cb5a35eb919e` widened native build budget to 120 minutes.
5. Run `34770530769`, job `103759179464`: first genuine compiler failure was MSVC object-path length.
6. Commit `36342d0982617e85972ddc4ebd36d0fe6012cb32` shortened hosted Bazel output root to `C:\b`; subsequent native builds completed all **3,675 actions** and linked the plugin.
7. Runs `34795710675` and `34800169484`: native build succeeded, but lifecycle smoke exposed Win32 loader error 126.
8. Recovery was split into an expensive immutable native build artifact plus reusable fast regression/package proof. Native artifact manifest records source SHA, dependency pins and SHA-256 hashes; regression fails closed on mismatch or native-input drift.
9. First artifact-producer run `34804276926` failed before compilation because the legacy zlib 1.2.13 URL returned bytes not matching the pinned SHA. Commit `d4c126e6f4eff089c4f3c0f433a140bd5e3bddfc` supplies the **same pinned zlib 1.2.13 archive and SHA** through Bazel `--distdir` from the official zlib GitHub release.

### Authoritative preserved native build proof

GitHub Actions:
- Workflow: `OpenVINO Unity native build artifact`
- Run: `34805228184`
- Job: `103855703203`
- Source SHA: `d4c126e6f4eff089c4f3c0f433a140bd5e3bddfc`
- Result: **SUCCESS**
- Bazel build: **3,675 actions, build completed successfully**.
- Artifact: `golden-needle-u2-native`, artifact ID `10334182410`, retained for 14 days.
- Artifact ZIP SHA-256: `c8a5dfd7e50885f78ac9f8f0e7d935dce663b211d2a62e1562e709fd1d1b9af7`.
- Plugin SHA-256: `f259186a84dd1762da99a02381cb8db7ec668f548054429476fb6bb5d580f550`.
- `runtime_pose_smoke.exe` SHA-256: `8ee5cad15a2acd30c61f10129d7191c5bade9fc5d0735d1c3a187e1a143aac8e`.

### Authoritative fast regression + package proof

GitHub Actions:
- Workflow: `OpenVINO Unity regression proof`
- Run: `34810742137`
- Job: `103871415840`
- Head SHA: `1e54b84911003841448002fdad5990c1d8bc1f76`
- Result: **SUCCESS**
- Reused the immutable native artifact from run `34805228184`; no MediaPipe/OpenVINO native rebuild occurred.

Proofs in that run:
- Native artifact source/pins/hashes verified fail-closed.
- Direct dependency diagnostics emitted with `dumpbin /DEPENDENTS`.
- Lifecycle: `GNOVPOSE_LOAD_VERSION_SELFTEST_UNLOAD=PASS`.
- Runtime identity: plugin `0.2.0`, ABI `1.1`, backend `OPENVINO_CPU_FP32`, OpenVINO `2026.3.0`, device `CPU`.
- Exact production model identity PASS:
  - detector size `2959078`, SHA-256 `46837eb883e6ec75b52c5f5ff6a9b78bd35e66c13f95e8c3566c582d146cb1d9`;
  - landmark size `2818390`, SHA-256 `ad6cfd3c903eb31a4ee788b809e45ecf9fa69923b69b9f3f2d9ae616ff433e58`.
- Exact pinned MediaPipe `pose.jpg` fixture identity PASS, SHA-256 `c8a830ed683c0276d713dd5aeda28f415f10cd6291972084a40d0d8b934ed62b`.
- Real-frame stream-mode semantic smoke PASS: `GNOVPOSE_U2_REAL_FRAME_33_NORMALIZED_WORLD=PASS`.
- Frame 1: graph `38.3346 ms`, detector `17.1675 ms`, landmark `14.9312 ms`, detector ran, `33` landmarks.
- Frame 2: graph `12.1136 ms`, detector `0 ms`, landmark `10.1009 ms`, detector skipped through tracking, `33` landmarks.
- Both frames satisfied exact 33 normalized landmarks + finite world coordinates.
- Additive Unity package smoke PASS.
- Staged CPU runtime set: `golden_needle_openvino_pose.dll`, `openvino.dll`, `openvino_intel_cpu_plugin.dll`, `openvino_tensorflow_lite_frontend.dll`, `tbb12.dll`, `tbbbind_2_5.dll`, `tbbmalloc.dll`.
- CPU-only `plugins.xml` PASS.
- GPU/NPU/AUTO/HETERO exclusion guard PASS.
- Exact detector/landmark runtime models staged.
- Stock MediaPipe/TFLite plugin files were not replaced or renamed.

The temporary System32/MSVC DLL staging in the regression workflow exists only to let the already-preserved pre-fix semantic test executable resolve standard Windows runtimes. Those system DLLs are **not** included by `package_unity.ps1`; final package loading is independently proven with the corrected lifecycle host and the explicit CPU-only runtime package.

## U3 — COMPLETE FOR IMPLEMENTATION / PRE-USER VALIDATION

### Managed interop/runtime wrapper

- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/OpenVinoPoseNative.cs`
- implementation commit `d06b547979952707c93238f38cfe261e177e5b9a`; Unity metadata commit `597101f98ed9b1673211c8b124111b30da3281e7`.
- `PoseInferenceBackend.MediaPipeTfliteCpu = 0`; stock TFLite remains serialized/default-safe.
- `PoseInferenceBackend.OpenVinoCpuFp32 = 1` is explicitly selectable.
- Managed owner validates ABI/backend identity, pins input memory, validates timestamps and exact 33-landmark output, and exposes runtime/engine timings.
- Wrapper has no Unity API calls and can execute native lifecycle/inference on a worker thread.

### Provider wiring

- Fail-closed source transformation commit `a0a09ba62b230cfb4fa33c8f7bf53dee6a691b4d`; audited transformation workflow `34769023319`, job `103755075832` success.
- Generated provider commit `aab549bf9b1a41419fa2eb16809819662e746e0b` (`feat: wire U3 selectable OpenVINO pose backend`).
- Existing stock Homuler/TFLite branch is preserved and remains default.
- OpenVINO selection reuses the same webcam/orientation/downscale/readback/prepared-frame pipeline, cadence gate, one-inference/no-backlog policy, `PoseObservation`, trust/grace/world fields and downstream canonical source.
- Explicit OpenVINO sessions do not silently fall back to another backend.
- Restart/teardown waits native inference/bootstrap work before disposing native state.

### U3 validation

- `Tools/OpenVinoUnityPosePlugin/tests/ManagedAbiSmoke/` compiles the real `OpenVinoPoseNative.cs` source with .NET 8.
- Authoritative managed validation run `34769239438`: success.
- `GNOVPOSE_MANAGED_ABI_SMOKE=PASS`.
- ABI = `65537` / v1.1; landmark count = 33; native result layout = **1280 bytes**.
- Provider static integration invariants passed.
- U2 native package/lifecycle/real-frame proof now also passes as recorded above.

This completes U3 only for implementation and pre-USER validation. It does **not** establish live Unity behavior, end-to-end latency, sustained throughput, tracking quality, teardown behavior on the USER machine, or production acceptance.

## U4 — MANDATORY USER A/B RUNTIME QA

Procedure: `Docs/openvino-unity-ab-qa.md`.

USER must now prepare the generated native package locally with Unity closed, then compare the stock `MediaPipeTfliteCpu` backend against explicitly selected `OpenVinoCpuFp32` under the same camera/readback/320x240/downstream settings. Capture F7 telemetry and physical movement behavior for both runs, including fast motion, torso movement, partial-body/occlusion recovery, restart and teardown.

Only USER/Orchestrator may classify U4 PASS / PASS WITH NOTES / FAIL. Do not claim OpenVINO production acceptance before this evidence.

Primary handoff: `Docs/worker-briefs/openvino-unity-integration-handoff.md`.
Execution plan: `Docs/openvino-unity-integration-checkpoints.md`.

## Exact next action

1. **STOP automated implementation at the U4 USER boundary.**
2. USER follows `Docs/openvino-unity-ab-qa.md` and supplies Run A (stock TFLite CPU) + Run B (OpenVINO CPU FP32) evidence.
3. Evaluate live tracking fidelity, fresh-result rate, pose age, request/frame latency, readback, render/CPU pressure, OpenVINO internal timings, detector cadence, partial-body recovery and restart/teardown behavior.
4. Only after USER evidence may U4 be classified and U5 recommendation/cleanup be considered.
5. Do not merge to `main` without explicit USER approval. Do not start Phase 6.
