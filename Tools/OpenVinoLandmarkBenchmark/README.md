# Golden Needle — OpenVINO exact-landmark precision benchmark

This directory contains an **external architecture-proof harness** for the exact landmark TFLite already embedded in Golden Needle. It runs outside Unity and does not replace `MediaPipePoseProvider`, decode landmarks, or drive the avatar.

## Current evidence and purpose

The first USER run on the actual low-end proof laptop (Windows 10, i3-7100U, Intel HD Graphics 620, OpenVINO 2026.3.0) established strong raw-network feasibility:

- `CPU`: mean **11.062 ms**, **90.403/s**;
- explicit `GPU.0` / Intel HD Graphics 620: mean **9.067 ms**, **110.284/s**;
- exact bundle/model identity and OpenVINO tensor contract: PASS;
- Sentis CPU reference on the same laptop: 46.24 ms / 21.63/s.

That is not full pose-pipeline throughput. Detector cadence, MediaPipe ROI tracking, raw-output decoding, projection, confidence/tracking semantics, world-landmark semantics, and production camera flow are not reconstructed here. Production OpenVINO integration is therefore **not approved** by this benchmark.

The current proof answers a narrower follow-up: what execution/precision properties are actually effective, whether an accuracy/FP32 GPU profile reduces CPU-vs-GPU raw-output differences, what latency cost it has, and whether the same pattern holds on a representative human-image-derived tensor.

## Exact-model fail-closed gate

The harness refuses to benchmark a substituted model. It verifies:

- bundle: `Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes`
  - 5,777,746 bytes
  - SHA-256 `59929e1d1ee95287735ddd833b19cf4ac46d29bc7afddbbf6753c459690d574a`
- exactly one ZIP member named `pose_landmarks_detector.tflite`
  - 2,818,390 bytes
  - SHA-256 `ad6cfd3c903eb31a4ee788b809e45ecf9fa69923b69b9f3f2d9ae616ff433e58`
- OpenVINO input: float32 `[1,256,256,3]`
- OpenVINO outputs: `[1,195]`, `[1,1]`, `[1,256,256,1]`, `[1,64,64,39]`, `[1,117]`.

Any identity or contract mismatch fails closed before timing. No model download, ONNX conversion, detector densification, or model rewrite is performed.

## Reproducible environment

`requirements.txt` pins:

- `openvino==2026.3.0`
- `Pillow==12.3.0`

`run.ps1` creates/reuses the ignored `.venv` and leaves global Python unchanged. It accepts 64-bit Python 3.10–3.14 and needs no Administrator rights.

## Profiles

A default run executes the following profiles **serially**, never concurrently:

1. `CPU_DEFAULT`
   - explicit CPU;
   - `PERFORMANCE_HINT=LATENCY` when the CPU exposes that property.
2. `GPU_DEFAULT`
   - explicit GPU / resolved `GPU.0`;
   - `PERFORMANCE_HINT=LATENCY` when exposed;
   - no precision override.
3. `GPU_ACCURACY_FP32`
   - explicit GPU only;
   - `PERFORMANCE_HINT=LATENCY`;
   - `EXECUTION_MODE_HINT=ACCURACY`;
   - `INFERENCE_PRECISION_HINT=f32`;
   - the two precision-control properties must both be exposed or the profile is reported as `UNSUPPORTED_CONFIGURATION`.

The implementation uses the typed OpenVINO 2026.3 hint API for compile configuration. Requested configuration, device-supported properties, applied configuration, and post-compile effective properties are reported separately. Successful profiles query `EXECUTION_DEVICES`, `PERFORMANCE_HINT`, `EXECUTION_MODE_HINT`, `INFERENCE_PRECISION_HINT`, `NUM_STREAMS`, and `SUPPORTED_PROPERTIES`; query failures are preserved instead of guessed.

No `AUTO`, `HETERO`, `MULTI`, CPU fallback, batching, or parallel CPU/GPU inference is used.

## Comparable timing methodology

Each profile uses the same deterministic seeded float32 NHWC tensor for the headline timing run:

- 30 warmups excluded;
- 300 serial measured `infer()` calls;
- one infer request;
- `time.perf_counter_ns()`;
- compile/model-read time excluded from steady-state latency;
- mean, p50, p95, p99, min, max, population standard deviation, and serial inferences/s.

The existing output-copy proxy is retained. It separately times independent NumPy copies of the selected small outputs (313 float32 values / 1,252 bytes) and of all outputs. This is called a **post-infer host materialization/copy proxy**, not pure device-to-host transfer.

## Numerical comparisons

For each matching raw output, comparisons report:

- shape match;
- finite state, NaN counts, Inf counts;
- reference minimum/maximum;
- reference mean absolute magnitude and RMS magnitude;
- max/mean/RMS absolute difference;
- normalized mean absolute difference;
- normalized RMS difference.

Normalization uses the corresponding reference tensor magnitude and an epsilon of `1e-12` only to avoid division by zero. No arbitrary numerical PASS/FAIL threshold is assigned.

The harness compares:

- `CPU_DEFAULT` vs `GPU_DEFAULT`;
- `CPU_DEFAULT` vs `GPU_ACCURACY_FP32`;
- `GPU_DEFAULT` vs `GPU_ACCURACY_FP32` as secondary context.

These are raw OpenVINO backend-consistency checks only. They are not independent Google LiteRT equivalence or full MediaPipe Tasks equivalence.

## Optional representative human-image input

Pass a local image with one clearly visible, preferably centered/full-body person:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\OpenVinoLandmarkBenchmark\run.ps1 -PoseImage "C:\path\pose.jpg"
```

The image path is optional; the default command still runs the three precision profiles on the seeded input.

### What preprocessing is actually supported by evidence

The repository identifies the bundled package as **MediaPipe Unity Plugin 0.16.3** in `Packages/com.github.homuler.mediapipe/package.json`. The corresponding MediaPipe pose-landmark graph source (`mediapipe/modules/pose_landmark/pose_landmark_by_roi_cpu.pbtxt`) specifies `ImageToTensor` with:

- output 256×256;
- ROI aspect ratio preserved (`keep_aspect_ratio: true`);
- float range `[0.0, 1.0]`.

MediaPipe's image-format definition describes SRGB as interleaved R, G, B. The production Tasks path, however, supplies a detector/tracker-derived normalized ROI and may include rotation/projection details; the exact production ROI transform and exact resampling implementation are **not reconstructed** by this tool.

Therefore `--pose-image` is deliberately labeled `REPRESENTATIVE_IMAGE_DERIVED_INPUT`, not MediaPipe-equivalent preprocessing. It performs deterministic:

1. EXIF orientation handling;
2. RGB conversion;
3. whole-image aspect-preserving resize into 256×256;
4. centered black letterbox;
5. Pillow bilinear resize;
6. float32 scaling by `1/255` to `[0,1]`.

This is suitable for asking whether backend precision differences remain similar on human-image-derived values, but it must not be used as proof of production pose semantics.

The exact extracted TFLite's standard metadata entry names/buffer sizes are also inspected read-only when possible. The harness does **not** infer normalization/ROI semantics from opaque metadata payloads.

## Raw output naming

The tool calls `[1,195]` the **landmark-related raw output** and `[1,117]` the **world-landmark-related raw output** for reporting/highlighting. It does not call their channels decoded landmarks. No per-channel semantic mapping has been independently confirmed in this harness, so no grouped-coordinate accuracy metric is added.

## Privacy and ignored data

`.gitignore` keeps these local-only:

- `.venv/`
- `artifacts/` (extracted exact TFLite)
- `results/`
- `inputs/`
- Python caches.

USER pose images must remain local. Reports store the image **filename only**, SHA-256, dimensions, mode, and preprocessing description; they do not intentionally store the full absolute path or image pixels.

## Running

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\OpenVinoLandmarkBenchmark\run.ps1
```

With a representative image:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\OpenVinoLandmarkBenchmark\run.ps1 -PoseImage ".\Tools\OpenVinoLandmarkBenchmark\inputs\pose.jpg"
```

Do not put a USER image into a tracked directory. `inputs/` is provided as an ignored convenience location.

The harness writes ignored TXT and JSON files under `Tools/OpenVinoLandmarkBenchmark/results/` and ends the console output with:

```text
=== GOLDEN NEEDLE OPENVINO PRECISION SUMMARY ===
...
=== END GOLDEN NEEDLE OPENVINO PRECISION SUMMARY ===
```

Return the newest matching TXT + JSON and that console block to the Orchestrator. Do not infer production acceptance from the result by itself.
