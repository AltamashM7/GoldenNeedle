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
- U2 native OpenVINO backend: **IMPLEMENTED; NATIVE BUILD NOW REPEATABLY SUCCEEDS; REUSABLE BUILD-ARTIFACT PROOF IN PROGRESS; POST-BUILD LOADER REGRESSION STILL OPEN**
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

- Keep stock MediaPipe/TFLite CPU fully functional as fallback and serialized/default backend until USER acceptance.
- OpenVINO remains explicitly selectable experimental CPU FP32.
- Reuse MediaPipe 0.10.22 preprocessing/postprocessing/tracking/world-landmark semantics.
- Do not recreate those semantics manually in C#.
- Do not change canonical/stabilization/calibration/retarget/locomotion semantics for this experiment.
- Keep 320x240 body inference input for the initial A/B test.
- Keep latest-useful-frame scheduling and no-backlog semantics.
- Do not densify/convert detector.
- Do not force D3D12.
- Do not refactor the current fixed 33-landmark provider or 20-joint canonical topology in this spike.
- Runtime performance builds must not execute the Gate B shadow-TFLite neural inference.
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
- Build-directory and packaged `Load/Version/SelfTest/Unload` passed.

## U2 — IMPLEMENTED; WINDOWS PROOF IN PROGRESS

- Starting SHA: `fb1e58e175c34a777ee86344b18aec33e4fe231d`.
- Main implementation SHA: `e2efe61013fbfa14a43c2d451a21087530ae3d7e` (`feat: implement U2 MediaPipe OpenVINO native backend`).
- Native architecture: Bazel-build a renamed/additive MediaPipe/OpenVINO DLL so graph/calculator registration and the OpenVINO inference calculator live in the same native image. Stock `mediapipe_c.dll` remains untouched.
- Reused semantics: MediaPipe 0.10.22 `PoseLandmarkerGraph`, preprocessing, detector decode/NMS, ROI/tracking, landmark decode/refinement, visibility/presence, world-landmark and projection semantics.
- Replaced execution only: detector and landmark neural inference use in-process OpenVINO `CPU`, latency-oriented FP32 through the proven inference seam.
- Runtime calculator does **not** execute Gate B shadow TFLite/raw-parity inference. TFLite is metadata-only for exact tensor names/order required by MediaPipe's `InferenceIoMapper`.
- ABI v1.1 exposes pose-engine lifecycle and synchronous RGBA processing with exactly 33 normalized/world landmarks, visibility/presence flags, backend identity, detector-run state and graph/inference/copy timings.
- Exact detector/landmark model files are extracted from the existing production task bundle and identity-checked; no conversion, densification or alternate weights.

### U2 CI history

1. Run `34767029284`, job `103749712693`: failed before C++ build because hosted MSYS2 lacked `git`, `patch`, `unzip`, `zip`; fixed.
2. Run `34767075477`, job `103749841159`: bootstrap reached Bazel but `bazelisk` resolved to a PowerShell shim; fixed by pinning native Bazelisk.
3. Run `34767143321`, job `103750021491`: actual native build was cancelled by the 60-minute workflow limit; no compiler/link/runtime failure surfaced before cancellation.
4. Run `34769414654`, job `103756144884`: another 60-minute run entered native build and was superseded.
5. Commit `43e5a888b80874ace1643dcdcfc4cb5a35eb919e` widened the workflow limit from 60 to 120 minutes without changing source/pins/models/backend.
6. Run `34770530769`, job `103759179464`: first full-budget build exposed the first genuine compiler failure. MSVC could not create an object at the generated MediaPipe path because the Bazel output path was too long.
7. Commit `36342d0982617e85972ddc4ebd36d0fe6012cb32` shortened the hosted-runner Bazel output root to `C:\b` only. Run `34795710675` then completed the entire Bazel build successfully (**3,675 actions**) and linked `golden_needle_openvino_pose.dll`, proving the path-length build blocker fixed. Post-build lifecycle smoke failed before `gnovpose_create` with `LoadLibraryExW` Win32 error 126.
8. Commit `42c8a9a8372088285da99d5d4547223900ad20a4` imported the full proven OpenVINO `setupvars.bat` process environment before lifecycle smoke. Run `34800169484` again completed the entire Bazel build successfully (**3,675 actions**) but produced the same immediate `LoadLibraryExW` error 126. Therefore the current open issue is a post-build dependency/loader problem, not compilation and not yet an inference-semantic failure.

### U2 recovery-method split — CURRENT

Repeated one-hour rebuilds are no longer required for loader/smoke/package-only fixes.

Implemented recovery split:
- `2b531bfb3d9ba5603640418df8eb7f635abbf2fc`: repurposed the old monolithic workflow into a regression consumer.
- `4d93c639e755eeb16ce12ada2f419685fcde9294`: added `scripts/build_native.ps1`, which performs only the expensive native Bazel build and writes an immutable manifest containing source SHA, exact dependency pins and SHA-256 hashes.
- `84d6e478bb9cb37b86c1d9e802af11de8b8bdede`: added `scripts/run_regression.ps1`, which restores/verifies a preserved build, rebuilds only the tiny lifecycle host, emits `dumpbin /DEPENDENTS` diagnostics when available, and runs lifecycle + exact-model + real-frame smoke.
- `78459d886ea4003b40d354e08e5fb894f6ffc7b4`: kept `scripts/build.ps1` as a compatibility wrapper that composes native build + regression for local preparation.
- `9d4d3230f0440116ee6892e1ee0c73b335cf6a10`: added workflow `OpenVINO Unity native build artifact`, which uploads the successful native DLL/runtime-smoke pair plus manifest as `golden-needle-u2-native` for 14 days.
- `683bfcee06fe378ddb9afee42b29c58ab59f2586`: configured `OpenVINO Unity regression proof` so regression-only changes download the latest successful native artifact instead of rebuilding MediaPipe/OpenVINO.
- The regression runner fails closed if artifact hashes/pins mismatch, if the artifact source is not an ancestor, or if native-impacting files changed after the preserved build.

Current artifact-producing run:
- Workflow: `OpenVINO Unity native build artifact`
- Run: `34804276926`
- Job: `103852914474`
- Build source SHA: `9d4d3230f0440116ee6892e1ee0c73b335cf6a10`
- At last check: checkout, MSYS2 utilities, native Bazelisk and exact bootstrap all **PASS**; `Build immutable U2 x64 Release artifact` is **IN PROGRESS**; artifact upload is pending.

U2 remains **NOT COMPLETE** until preserved native build + lifecycle smoke + exact model identity + real-frame 33-landmark semantic smoke + additive Unity package smoke all pass.

## U3 — IMPLEMENTED; MANAGED VALIDATION PASS; UNITY RUNTIME QA PENDING

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

### U3 managed validation

- `Tools/OpenVinoUnityPosePlugin/tests/ManagedAbiSmoke/` compiles the real `OpenVinoPoseNative.cs` source with .NET 8.
- Authoritative validation run `34769239438`: success.
- `GNOVPOSE_MANAGED_ABI_SMOKE=PASS`.
- ABI = `65537` / v1.1; landmark count = 33; native result layout = **1280 bytes**.
- Provider static integration invariants passed.
- This does not replace Unity Editor/Windows hardware validation.

### U3/U4 preparation

- One-command local preparation: `Tools/OpenVinoUnityPosePlugin/scripts/prepare_unity.ps1`.
- USER A/B procedure: `Docs/openvino-unity-ab-qa.md`.
- The A/B procedure preserves stock/default backend and identical camera/readback/320x240/downstream settings; it captures F7 backend/result-rate/latency/pressure telemetry, OpenVINO timings, fast-motion fidelity, partial-body recovery and restart/teardown behavior.
- Generated native/model/package artifacts remain ignored/untracked.

Primary handoff: `Docs/worker-briefs/openvino-unity-integration-handoff.md`.
Execution plan: `Docs/openvino-unity-integration-checkpoints.md`.

## Exact next action

1. Follow native-artifact run `34804276926`, job `103852914474`, to completion.
2. If the native build succeeds, confirm artifact `golden-needle-u2-native` exists and record its manifest/source SHA/hashes. This should be the last expensive rebuild unless a native-impacting input changes.
3. Run `OpenVINO Unity regression proof` against that preserved artifact. Use the emitted `dumpbin /DEPENDENTS` and loader output to identify the specific missing dependency behind Win32 error 126; make loader/test/package-only fixes without rebuilding the native DLL.
4. Continue fast regression-only iterations until lifecycle, exact model identity, real-frame 33-landmark smoke and package smoke pass.
5. Only then mark U2 COMPLETE and U3 COMPLETE for implementation/pre-USER validation, and stop at the genuine U4 USER Windows/Unity A/B boundary.
6. Do not start U5 from CI alone. Do not merge to `main`. Do not start Phase 6.
