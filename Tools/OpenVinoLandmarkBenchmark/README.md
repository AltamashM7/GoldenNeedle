# Golden Needle — OpenVINO Exact-Landmark Benchmark

This is an **external architecture proof** for the exact landmark neural network already shipped inside Golden Needle's production MediaPipe task bundle. It runs outside Unity and does not modify or replace `MediaPipePoseProvider`, the production scene, canonical mapping, stabilization, calibration, retargeting, locomotion, Phase 5A, or the existing Sentis experiment.

## What it proves

The harness compares OpenVINO `CPU` and Intel `GPU` **separately** using the same exact `pose_landmarks_detector.tflite` bytes. It uses one serial inference request with a `LATENCY` performance hint, 30 excluded warmups and 300 measured iterations by default.

It deliberately does **not** use `AUTO`, `HETERO`, batching, multi-request throughput, ONNX conversion, detector densification, or any hidden CPU fallback for a failed GPU run.

This is only a raw landmark-network benchmark. It does not reproduce MediaPipe detector cadence, ROI tracking, landmark decoding/projection, confidence/tracking semantics, world-landmark semantics, or the full camera/preprocess pipeline.

## Exact model identity — fail closed

The script reads the repository production bundle directly:

`Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes`

It requires:

- bundle size `5,777,746` bytes;
- bundle SHA-256 `59929e1d1ee95287735ddd833b19cf4ac46d29bc7afddbbf6753c459690d574a`;
- exactly one ZIP entry named `pose_landmarks_detector.tflite`;
- landmark size `2,818,390` bytes;
- landmark SHA-256 `ad6cfd3c903eb31a4ee788b809e45ecf9fa69923b69b9f3f2d9ae616ff433e58`.

Only after both hashes match is the landmark TFLite written to the ignored local `artifacts/` directory. A mismatch stops the benchmark. The tool never downloads a substitute model and never converts it.

OpenVINO must then see the expected contract:

- input float32 `[1,256,256,3]`;
- outputs `[1,195]`, `[1,1]`, `[1,256,256,1]`, `[1,64,64,39]`, `[1,117]`.

A contract mismatch also fails closed before timing.

## Reproducible environment

`requirements.txt` pins:

`openvino==2026.3.0`

The PowerShell runner creates/reuses `.venv/` next to the tool and leaves global Python untouched. It accepts a 64-bit Python 3.10 through 3.14 and does not require Administrator rights. If the current Intel graphics/OpenCL driver does not expose an OpenVINO GPU device, the GPU result is reported as unavailable; the script does not install or alter graphics drivers.

## Run on the USER laptop

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\OpenVinoLandmarkBenchmark\run.ps1
```

Optional explicit device-only runs:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\OpenVinoLandmarkBenchmark\run.ps1 -Devices CPU
powershell -ExecutionPolicy Bypass -File .\Tools\OpenVinoLandmarkBenchmark\run.ps1 -Devices GPU
```

The default run is the preferred comparison because it benchmarks CPU and GPU separately in one reproducible invocation while still using explicit devices.

## Methodology

For each requested backend, the harness records OpenVINO/device identity and available properties, then compiles the exact TFLite explicitly on that device with `PERFORMANCE_HINT=LATENCY` when that property is exposed. Model-read time and compile time are reported separately and excluded from steady-state latency.

The primary input is a deterministic seeded float32 NHWC `[1,256,256,3]` tensor. The input tensor is bound once before timing. The request is warmed up 30 times, then `infer(share_outputs=True)` is timed serially 300 times with `time.perf_counter_ns()` so the headline timing does not intentionally force independent NumPy output copies; those copies are measured separately below. Headline statistics are mean, p50, p95, p99, min, max, population standard deviation, and `1000 / mean_ms` serial inferences/s.

The report also calculates deltas against the clean D3D12 Sentis CPU raw-landmark reference: `46.24 ms` mean / `21.63/s`. The harness does not decide architecture acceptance; that remains an Orchestrator decision after USER hardware evidence.

## Host materialization/copy proxy

After normal inference, the harness separately times NumPy copies from OpenVINO output tensors into independent arrays.

It measures:

- selected small outputs `[1,195]`, `[1,1]`, `[1,117]` = 313 float32 values / 1,252 bytes;
- all outputs as a contextual upper bound.

These timers are intentionally labeled **post-infer host materialization/copy proxy**. They are not claimed to be pure device-to-host transfer time because OpenVINO may already expose host-accessible output tensors after `infer()`.

## Numerical sanity

If both CPU and GPU succeed, the harness compares one deterministic same-input raw output snapshot across every output and records shape match, finite status, NaN/Inf counts, max absolute difference, mean absolute difference, and RMS difference.

This proves only OpenVINO CPU-vs-GPU raw-network consistency. It is not independent Google LiteRT equivalence, MediaPipe Tasks equivalence, pose semantic equivalence, or proof that Golden Needle has reconstructed raw output decoding correctly.

## Results to return

Generated items remain ignored:

- `.venv/`
- `artifacts/pose_landmarks_detector.tflite`
- `results/*.txt`
- `results/*.json`
- Python caches/temp files

After the run, send the Orchestrator:

1. the final console block between `=== GOLDEN NEEDLE OPENVINO SUMMARY ===` and `=== END GOLDEN NEEDLE OPENVINO SUMMARY ===`;
2. the newest `Tools/OpenVinoLandmarkBenchmark/results/openvino-landmark-*.txt`;
3. the matching `.json` report;
4. if GPU fails, the full error already preserved in those reports.

No production Unity QA is required merely to run this external benchmark.
