# Golden Needle — Gate B MediaPipe 0.10.22 + OpenVINO semantic-parity proof

This directory is an **isolated research proof**. It does not modify the installed Unity MediaPipe package and it does not connect OpenVINO output to the Golden Needle avatar.

Gate B asks one narrow question:

> Can Golden Needle keep MediaPipe's detector/ROI/tracking/landmark/world-landmark semantics and replace only detector + landmark neural inference with in-process OpenVINO CPU FP32?

A successful run is evidence for a later Unity coexistence spike. It is **not** production approval.

## Pinned source/runtime generations

The scripts fail closed on these pins:

- Homuler MediaPipeUnityPlugin `v0.16.3`
  - tag commit: `cf4c11d8eef724fe24111b7cd795d55ba490aeec`
  - `.bazelversion`: `6.5.0`
- Google MediaPipe `v0.10.22`
  - commit: `c54c06dd8c4314a316c14da31493bcc38ed302e2`
  - Homuler WORKSPACE archive SHA-256: `aaeb0f6da437d8aeb51e77193d85a7e7f8e811a9fde1659102e8a812b7d2a65b`
- OpenVINO C++ toolkit `2026.3.0`
  - official Intel Windows archive: `openvino_toolkit_windows_2026.3.0.22451.bd8d6542e3c_x86_64.zip`
  - bootstrap downloads the official adjacent `.sha256` sidecar and verifies it before extraction.

No fallback MediaPipe/OpenVINO version is permitted.

## What is reused

The exact MediaPipe 0.10.22 Tasks graphs remain responsible for ImageToTensor preprocessing/letterbox handling, detector anchors/decode/NMS/detection-to-ROI, ROI transform/rotation and previous-landmark tracking, landmark tensor split/39-landmark decode/heatmap refinement/33 public split, presence/visibility, world-landmark decode/projection, auxiliary landmarks, next-frame ROI, and stream-mode smoothing/tracking.

The isolated patch changes only the inference plumbing when `GOLDEN_NEEDLE_GATE_B_BACKEND=OPENVINO_CPU_FP32`. The normal Tasks/TFLite path stays unchanged.

MediaPipe's `TaskRunner` normally installs `ModelResourcesCacheService`, and `ModelTaskGraph::AddInference` may therefore pass a model-resource tag rather than a filename into `InferenceSubgraph`. The OpenVINO calculator needs the exact file-backed TFLite. Gate B consequently makes two narrow, OpenVINO-only changes in the ignored external MediaPipe checkout:

1. `ModelTaskGraph::AddInference` passes `model_resources.GetModelFile()` into the inference subgraph only when the Gate B OpenVINO backend is selected;
2. `InferenceSubgraph` replaces only its inference node with `GoldenNeedleOpenVinoInferenceCalculator` in that mode.

Every other mode retains MediaPipe's stock resource/inference path. No detector/ROI/postprocessing math is manually reimplemented.

## Three modes

Each mode processes the same predecoded frame directory with the same monotonic timestamps.

### `TASKS_REFERENCE`

Official MediaPipe 0.10.22 C++ `PoseLandmarker` in VIDEO mode using the exact production task bundle, CPU delegate, one pose, detection/presence/tracking thresholds 0.5, segmentation disabled.

### `GRAPH_TFLITE_CPU`

An expanded `PoseLandmarkerGraph` using the exact extracted detector/landmark TFLites and standard MediaPipe/TFLite CPU inference. Extra outputs expose auxiliary landmarks, next-frame ROI and detector-output cadence.

### `GRAPH_OPENVINO_CPU_FP32`

The **same expanded graph** as `GRAPH_TFLITE_CPU`, but both inference nodes are replaced by `GoldenNeedleOpenVinoInferenceCalculator`.

Graph contract stays `std::vector<mediapipe::Tensor> -> OpenVINO CPU FP32/LATENCY -> std::vector<mediapipe::Tensor>`.

For Gate B the bridge is correctness-first/lifetime-safe: MediaPipe CPU read view is copied into an OpenVINO-owned reusable input tensor; inference is synchronous; each output is copied into a newly allocated MediaPipe CPU tensor. Copy cost is timed separately. No unsafe aliasing/zero-copy claim is used.

Both exact neural models are replaced: `pose_detector.tflite` and `pose_landmarks_detector.tflite`. Output order is reconstructed by their exact unique output shapes.

## Exact model identity gate

Before any run `scripts/prepare_models.py` verifies:

- task bundle: 5,777,746 bytes, SHA-256 `59929e1d1ee95287735ddd833b19cf4ac46d29bc7afddbbf6753c459690d574a`
- detector: 2,959,078 bytes, SHA-256 `46837eb883e6ec75b52c5f5ff6a9b78bd35e66c13f95e8c3566c582d146cb1d9`
- landmark model: 2,818,390 bytes, SHA-256 `ad6cfd3c903eb31a4ee788b809e45ecf9fa69923b69b9f3f2d9ae616ff433e58`

No conversion, densification, quantization change, alternate model or hidden fallback is allowed.

## Windows prerequisites

Keep Unity closed while building/running.

Gate B deliberately follows the Homuler v0.16.3 Windows build generation rather than the Python 3.14 environment used by the earlier external OpenVINO benchmark. Required locally:

- Windows 10/11 x64;
- Git for Windows;
- **64-bit Python 3.12**. Homuler's MediaPipe 0.10.22 workspace carries hermetic Python locks only through 3.12 and its Windows CI uses 3.12; Python 3.14 alone is not accepted for this native proof;
- Visual Studio 2022 Build Tools with **Desktop development with C++**, MSVC v143 and a Windows SDK;
- CMake on `PATH` (Gate B uses Homuler's OpenCV CMake source-build path);
- MSYS2, normally `C:\msys64`, with `git`, `patch`, `unzip` and `zip` installed;
- Bazelisk on `PATH` (recommended), or Bazel itself, resolving to exactly Bazel 6.5.0 from Homuler's `.bazelversion`.

No Administrator rights are intended and no global Python package is modified. `bootstrap.ps1` checks these prerequisites and stops with a targeted message instead of silently changing toolchain generations.

The build uses Homuler's Windows conventions: `MEDIAPIPE_DISABLE_GPU=1`, OpenCV `cmake`, Python 3.12 repository/action environment, the exact Homuler MediaPipe patches, MSYS2 `BAZEL_SH`, and a low-end-safe `--jobs=2` override instead of Homuler's default 128 jobs.

## Commands

From repo root, run one stage at a time:

```powershell
.\Tools\MediaPipeOpenVinoParity\bootstrap.ps1
```

Only after that prints `[Gate B] bootstrap PASS`:

```powershell
.\Tools\MediaPipeOpenVinoParity\build.ps1
```

Only after the build passes:

```powershell
.\Tools\MediaPipeOpenVinoParity\run.ps1 -InputVideo "C:\path\golden-needle-motion.mp4"
```

Frames are also supported:

```powershell
.\Tools\MediaPipeOpenVinoParity\run.ps1 -InputFrames "C:\path\frames" -Fps 30
```

For an order-reversal confirmation pass:

```powershell
.\Tools\MediaPipeOpenVinoParity\run.ps1 -InputVideo "C:\path\golden-needle-motion.mp4" -Order OpenVinoFirst
```

`bootstrap.ps1 -Recreate` resets/recreates only the ignored Gate B Homuler/MediaPipe/OpenVINO workspaces. It does not touch the Unity package or production source.

## Input/timing semantics

Recommended video is ~8–20 seconds, one person, mostly full body, normal plus deliberately fast movement; torso/leg motion and brief occlusion are useful. Video is decoded **once** to ignored PNGs; every backend consumes the same files/timestamps. Input data is never embedded in reports; only label/hash/dimensions/FPS/frame count are retained.

Gate B uses synchronous VIDEO-mode temporal processing. Reported timing is **offline VIDEO-mode graph processing capacity**, not Unity LIVE_STREAM camera-to-avatar latency. Startup/model compilation, first frame, steady-state frame processing, detector/landmark OpenVINO inference, and Tensor↔OpenVINO bridge copies are separated.

## Semantic comparison

`scripts/compare.py` compares Tasks↔TFLite, TFLite↔OpenVINO and Tasks↔OpenVINO. It reports pose-present agreement/disagreement frames; normalized x/y/z MAE/RMS/p95/max; overall xyz error; visibility/presence differences; world-coordinate errors and 3D Euclidean errors in meters; per-landmark worst cases; ROI center/size/rotation error where available; detector-cadence disagreement; startup/first/steady timing; mean/p50/p95/p99/min/max/population stddev/rate; and OpenVINO inference/bridge timings.

`TASKS_REFERENCE` does not expose internal ROI/detector packets through its public API, so those fields are unavailable rather than invented.

For `GRAPH_OPENVINO_CPU_FP32`, `detector_calls` and `landmark_calls` come from the Gate B OpenVINO calculator telemetry and are exact inference-node invocation counts. In the current `GRAPH_TFLITE_CPU` raw footer, the legacy `detector_calls` label is only a **detector output-packet/frame proxy**, not an instrumented exact TFLite inference count; interpret it accordingly. Per-frame `detector_ran` is likewise output-observability evidence. This limitation does not affect the final landmark/world semantic comparison.

No arbitrary Golden Needle PASS/FAIL tolerance is applied.

Comparator self-test can be run with the Python 3.12 path recorded by bootstrap; it does not require external Python packages.

## Reports to return

Ignored final reports:

`Tools/MediaPipeOpenVinoParity/results/parity-<timestamp>.txt`
`Tools/MediaPipeOpenVinoParity/results/parity-<timestamp>.json`

Return both plus the console block between `=== GOLDEN NEEDLE MEDIAPIPE OPENVINO PARITY SUMMARY ===` and `=== END GOLDEN NEEDLE MEDIAPIPE OPENVINO PARITY SUMMARY ===`.

## Local-only and source adaptation boundary

`.work/`, downloads, external clones, extracted models, Bazel/build outputs, inputs/decoded frames/raw runs/results are ignored. No third-party binary is committed.

No MediaPipe pose graph-builder source is copied into Golden Needle. Bootstrap checks out exact MediaPipe 0.10.22, applies the exact five patches referenced by Homuler v0.16.3 (`mediapipe_opencv.diff`, `mediapipe_visibility.diff`, `mediapipe_model_path.diff`, `mediapipe_extension.diff`, `mediapipe_workaround.diff`), then `scripts/apply_overlay.py` makes the two narrow OpenVINO-only adaptations described above and copies the research calculator/runner into the ignored MediaPipe tree. If any expected source seam changes, patching fails instead of guessing.

See `THIRD_PARTY_NOTICES.md`. Gate B does not mean OpenVINO is production-ready.
