# Golden Needle OpenVINO Unity Pose Plugin

This workspace is the additive Windows x86_64 native-plugin path for the Golden Needle OpenVINO Unity experiment. It remains separate from Homuler's stock MediaPipe native library; stock MediaPipe/TFLite CPU remains the default/fallback backend.

## Current scope

The plugin now implements the U1/U2 native boundary and the managed U3 selection surface:

- distinct DLL: `golden_needle_openvino_pose.dll`;
- versioned C ABI **v1.1** with opaque context + pose engine and thread-local last-error path;
- exact OpenVINO `2026.3.0`, explicit `CPU` only;
- MediaPipe `0.10.22` / Homuler `0.16.3` semantic-generation pins;
- exact MediaPipe `PoseLandmarkerGraph` preprocessing, detector decode/NMS, ROI/tracking, landmark refinement, visibility/presence and world-landmark projection semantics;
- only detector + landmark neural execution replaced by in-process OpenVINO CPU FP32;
- no Gate B shadow-TFLite neural inference in the practical runtime calculator; TFLite is metadata-only for tensor naming/order required by MediaPipe;
- synchronous RGBA ABI result with exactly 33 normalized/world landmarks, presence/visibility flags, backend identity, detector-run state and timing telemetry;
- MSVC/Bazel Windows x64 Release build with dynamic non-debug CRT;
- additive packaging under `Assets/GoldenNeedle/Plugins/OpenVinoPose/x86_64/` plus exact generated detector/landmark staging under `Assets/StreamingAssets/GoldenNeedle/OpenVinoPoseModels/`;
- no patch, rename or replacement of Homuler's stock `mediapipe_c.dll` or managed package.

The Unity provider exposes `MediaPipeTfliteCpu` as enum value `0` and serialized default. `OpenVinoCpuFp32` must be selected explicitly for A/B QA.

## Dependency pins

- OpenVINO C++ toolkit: `2026.3.0`, official Intel Windows x86_64 archive. Bootstrap verifies Intel's adjacent SHA-256 sidecar before extraction.
- MediaPipe: `0.10.22`, commit `c54c06dd8c4314a316c14da31493bcc38ed302e2`.
- Homuler MediaPipeUnityPlugin: `0.16.3`, commit `cf4c11d8eef724fe24111b7cd795d55ba490aeec`.
- Bazel: `6.5.0` through native Bazelisk.

OpenVINO and MediaPipe are Apache-2.0. See `THIRD_PARTY_NOTICES.md`. Generated third-party/runtime binaries are intentionally not committed.

## Local Windows preparation

Keep Unity closed while the generated native package is being replaced. From repository root on Windows x64 with Visual Studio 2022 Desktop C++ tools, CMake, MSYS2 build utilities and native Bazelisk available:

```powershell
.\Tools\OpenVinoUnityPosePlugin\scripts\prepare_unity.ps1
```

For a clean regeneration:

```powershell
.\Tools\OpenVinoUnityPosePlugin\scripts\prepare_unity.ps1 -Recreate
```

The compatibility wrapper still performs the full local sequence:

```powershell
.\Tools\OpenVinoUnityPosePlugin\scripts\bootstrap.ps1
.\Tools\OpenVinoUnityPosePlugin\scripts\build.ps1
.\Tools\OpenVinoUnityPosePlugin\scripts\package_unity.ps1
```

`build.ps1` now composes two explicit stages:

```powershell
.\Tools\OpenVinoUnityPosePlugin\scripts\build_native.ps1
.\Tools\OpenVinoUnityPosePlugin\scripts\run_regression.ps1
```

Do not open Unity until the command reports `OPENVINO_UNITY_LOCAL_PREP=PASS`.

## CI recovery split

The expensive Bazel build and the post-build smoke/package proof are deliberately separated in CI.

`OpenVINO Unity native build artifact` runs only when native-impacting inputs change. It builds the MediaPipe/OpenVINO DLL and `runtime_pose_smoke.exe`, then uploads `golden-needle-u2-native` with a manifest containing the source SHA, exact dependency pins and SHA-256 hashes.

`OpenVINO Unity regression proof` is for loader, lifecycle, real-frame and packaging work. It downloads the latest successful native artifact, verifies the manifest/hashes and verifies that no native-impacting files changed after that build. It rebuilds only the small independent `gnovpose_smoke.exe` host, prints direct DLL import diagnostics, then runs lifecycle, model-identity, real-frame and packaged-DLL checks.

This allows loader/test/package fixes to iterate without repeating the roughly hour-long Bazel compilation. A fresh native artifact is still mandatory whenever native source, overlay, ABI/runtime source, native smoke target, dependency bootstrap or native-build configuration changes.

## Runtime contract

The native context accepts only `device="CPU"`. It does not use `AUTO`, `GPU`, `NPU`, HETERO or fallback device selection. Native pose-engine creation validates exact detector/landmark model identity and stream-mode processing requires strictly increasing timestamps.

The U3 managed provider keeps the existing webcam/orientation/downscale/readback/prepared-frame scheduler and `PoseObservation` trust/grace/world semantics. OpenVINO work runs behind the same one-inference/no-backlog gate and publishes through the same frozen 33→20 canonical mapper and downstream stabilization/calibration/retarget/locomotion stack.

## Validation boundary

Automated validation covers native lifecycle/package viability, the native real-frame semantic smoke once U2 completes, managed ABI layout and provider structural invariants. It does **not** replace USER Windows/Unity A/B QA. Production acceptance still requires comparable live tracking behavior plus meaningful end-to-end performance improvement without unacceptable render/CPU regressions.
