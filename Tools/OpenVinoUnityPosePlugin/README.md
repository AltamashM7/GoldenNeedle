# Golden Needle OpenVINO Unity Pose Plugin

This workspace is the additive Windows x86_64 native-plugin path approved for the Golden Needle OpenVINO Unity integration experiment. It is deliberately separate from Homuler's stock MediaPipe native library and from the existing production `MediaPipePoseProvider`.

## U1 scope

U1 proves the native/managed packaging boundary before any vision code is exposed:

- distinct DLL: `golden_needle_openvino_pose.dll`;
- versioned C ABI (`1.0`) with an opaque context and thread-local last-error path;
- exact OpenVINO `2026.3.0` pin, explicit `CPU` device only;
- MediaPipe semantic-generation pin recorded as `0.10.22` / Homuler `0.16.3`;
- MSVC x64 Release build using the dynamic non-debug CRT (`/MD`);
- same-process `LoadLibraryExW -> ABI/version -> OpenVINO CPU self-test -> destroy -> FreeLibrary` proof;
- additive packaging under `Assets/GoldenNeedle/Plugins/OpenVinoPose/x86_64/`;
- no patch, rename, or replacement of `Mediapipe.Unity.dll` or the stock TFLite CPU path.

The U1 plugin does **not** process images. U2 extends this ABI with the exact MediaPipe 0.10.22 pose graph semantics already proven by `Tools/MediaPipeOpenVinoParity`.

## Dependency pins

- OpenVINO C++ toolkit: `2026.3.0`, official Intel Windows x86_64 archive. Bootstrap verifies Intel's adjacent SHA-256 sidecar before extraction.
- MediaPipe semantic generation: `0.10.22`, commit `c54c06dd8c4314a316c14da31493bcc38ed302e2`.
- Homuler MediaPipeUnityPlugin: `0.16.3`, commit `cf4c11d8eef724fe24111b7cd795d55ba490aeec`.

OpenVINO and MediaPipe are Apache-2.0. See `THIRD_PARTY_NOTICES.md`. No third-party binary is committed by this workspace; packaging is generated locally/CI from the verified official archive.

## Build

From repository root on Windows x64 with Visual Studio 2022 Desktop C++ tools and CMake:

```powershell
.\Tools\OpenVinoUnityPosePlugin\scripts\bootstrap.ps1
.\Tools\OpenVinoUnityPosePlugin\scripts\build.ps1
.\Tools\OpenVinoUnityPosePlugin\scripts\package_unity.ps1
```

The build fails closed if the dependency state no longer matches the approved pins. `package_unity.ps1` copies generated files only into the dedicated OpenVINO plugin folder and then runs the dynamic-load smoke test against that packaged copy.

## Runtime contract at U1

The ABI accepts only `device="CPU"`. It does not use `AUTO`, `GPU`, HETERO, or fallback device selection. `gnovpose_self_test` forces the CPU plugin to resolve and rejects an OpenVINO runtime whose build number does not contain `2026.3.0`.

U2/U3 must preserve the exact current `PoseObservation` 33-landmark semantics and route both backends through the existing frozen 33→20 canonical mapper and downstream stack.
