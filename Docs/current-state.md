# Current state

## Authoritative checkpoint — hybrid Sentis GPU inference spike

Working branch: `engine/pose-tracking-spike`.

Starting checkpoint for this architecture spike: `3a4b851b606fa9af1063cc835286a3b389d8933e` — `fix: tighten inference launch timestamp boundary`.

Status:
- Phase 4: **USER ACCEPTED — PASS**.
- Lab-camera quaternion / clear-only camera correction: **USER-runtime accepted**.
- Immediate Launch After Readback: **USER-runtime accepted** and stays ON.
- Direct Body CPU Readback: **USER-runtime PASS** as a modest optimization and retained as the known-safe CPU fallback path.
- Callback-driven readback scheduling: **CLOSED / REJECTED**. The readback callback remains diagnostic-only.
- Result-callback / inference-completion scheduling optimization: **CLOSED** after USER steady-state measurement. Do not introduce callback-driven inference launch without new reproducible evidence.
- Hybrid Sentis GPU inference spike: **IMPLEMENTED / AWAITING USER RUNTIME BENCHMARK**.
- Phase 5A: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.
- Do not merge to `main` without explicit USER approval.

## Why the GPU track exists

The current latest-frame CPU MediaPipe path is stable and avoids backlog, but USER runtime evidence shows only about `10–12` fresh pose results/s on the low-end proof laptop. Readback is commonly about `55–65 ms`, accepted Pose Landmarker request to callback commonly about `60–75+ ms`, and observed frame-to-result age is commonly about `110–140 ms` depending on load. The USER reports that fast movements can therefore lose trajectory detail unless performed more slowly.

This is now primarily a pose-sampling / neural-inference throughput problem rather than a reason to increase smoothing, queue old poses, or reopen accepted Phase 4 semantics.

## Frozen production motion path

The production `MediaPipePoseProvider`, current `PoseTrackingSpike.unity`, canonical coordinate mapping, modular calibration, retargeting/IK, locomotion, camera/orientation conventions, trust thresholds and smoothing are not changed by the GPU spike.

The CPU fallback remains:

```text
camera / latest useful frame
-> body-only 320x240 preparation
-> DirectCPU readback (Homuler fallback remains available)
-> MediaPipe Pose Landmarker Lite CPU delegate
-> existing PoseObservation / canonical / stabilization / calibration / retarget / gameplay
```

The accepted runtime bounds remain:

```text
<= 1 active readback
<= 1 replaceable prepared TextureFrame
<= 1 outstanding MediaPipe inference
no camera-frame history
no inference backlog
no delayed pose replay
no catch-up loop
```

## Isolated Sentis GPU inference spike

The spike is intentionally separate from production under `Assets/GoldenNeedle/Debug/GpuInferenceSpike/`.

`Packages/manifest.json` adds `com.unity.ai.inference` `2.6.1`. Modern Sentis directly imports LiteRT/TensorFlow Lite `.tflite` models, so this spike does **not** convert the current models to ONNX.

The editor command:

`Golden Needle > GPU Inference Spike > Prepare Exact Models + Scene`

reads the existing production bundle:

`Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes`

and extracts `pose_detector.tflite` and `pose_landmarks_detector.tflite` byte-for-byte into the isolated ignored `Generated/` area. The original bundle is never modified. Generated exact model copies are intentionally not committed and therefore do not change Git LFS policy.

The setup also writes a local audit containing bundle/submodel SHA-256 values plus raw TFLite input/output/operator/quantization metadata, imports both exact `.tflite` files through Sentis, and creates a separate `GpuInferenceSpike.unity` diagnostic scene.

The web-builder environment cannot decode the repository binary bundle through its text-only GitHub connector, so exact hashes, output shapes/operator sets and import/backend compatibility are **not claimed here**. They are established by the local setup/import step and USER runtime benchmark.

## Diagnostic modes

### Backend equivalence

For detector and landmark models independently:
- load the exact imported TFLite model;
- run deterministic zero, gradient and seeded-random float NHWC inputs;
- execute the same tensor on Sentis CPU and `BackendType.GPUCompute`;
- asynchronously read **all** outputs;
- compare output shape plus finite/non-finite count, max absolute error, mean absolute error, RMS error and mean relative error.

This mode is a neural-network equivalence check. It is not a claim of production-equivalent MediaPipe pose output because detector decode, ROI tracking and landmark/world-landmark postprocessing are intentionally outside this first spike.

### GPU-resident realistic path

The realistic path keeps the large input on the GPU:

```text
model-sized RenderTexture
-> CommandBuffer + RenderTargetIdentifier TextureConverter path
-> GPU-resident NHWC tensor
-> BackendType.GPUCompute
-> asynchronous readback of selected small float outputs
```

The RenderTexture conversion uses the command-buffer TextureConverter overload rather than the Texture2D-only direct overload. No full-frame CPU readback is required by this experimental path.

Current performance readback selects float outputs at or below the configured element cap. The exact minimum detector/landmark output subset will only be narrowed when the MediaPipe detector decode / ROI / landmark postprocess stage is reconstructed and the required tensors are proven.

Defaults are `30` warmup iterations and `300` measured iterations per model/backend, with helper statistics for mean, p50, p95, p99 and completed inferences/s. The diagnostic UI also reports actual Unity version, OS, CPU, adapter/vendor, graphics API, graphics memory, compute-shader support, Sentis assembly version and render frame-time samples.

## Intended future hybrid CPU + GPU architecture

If the spike earns a measured pass, the intended split is heterogeneous rather than GPU-only:

```text
CPU: camera/session/latest-frame state + ROI/tracking cadence
  -> issue detector or landmark work
GPU: crop/resize/channel/layout preprocessing + detector/landmark CNN
  -> keep large tensors GPU-resident
CPU: async small-output decode / ROI update / normalized + world landmark reconstruction
  -> existing PoseObservation
  -> canonical / stabilization / calibration / retarget / locomotion
```

CPU MediaPipe remains the fallback/error-recovery path. Production must not run duplicate CPU and GPU pose inference every frame merely to claim both processors are used.

## Verification state

Source/static implementation is complete enough for USER runtime validation, but this Web Builder did **not** run Unity package resolution, `.tflite` import, Unity compilation, EditMode tests, CPU worker execution, GPUCompute worker execution, output readback or the HD 620 benchmark. No Unity/runtime PASS is claimed.

`Packages/packages-lock.json` is intentionally not fabricated by the Web Builder; Unity's package resolver should create/update the correct lock entry when the USER opens the project.

The spike verdict is therefore **UNMEASURED — AWAITING USER RUNTIME BENCHMARK**. It is neither a strong pass, conditional pass nor rejection yet.

## Next USER validation

1. Pull the latest `engine/pose-tracking-spike` in GitHub Desktop.
2. Open the project in Unity 6.5 and allow package resolution/import/compilation to finish.
3. Run `Golden Needle > GPU Inference Spike > Prepare Exact Models + Scene`.
4. Confirm the Console reports successful exact TFLite extraction/import and no model-import error. If direct TFLite import fails, stop and report the exact error; do not convert to ONNX automatically.
5. Open `Assets/GoldenNeedle/Debug/GpuInferenceSpike/Generated/GpuInferenceSpike.unity`.
6. Enter Play Mode and verify the hardware line reports the actual Intel HD 620 adapter, actual graphics API and compute-shader support.
7. Click **Run benchmark** and let equivalence, CPU, GPUCompute and GPU-resident passes complete for both models.
8. Capture the final diagnostic UI/Console output plus `Generated/model-audit.txt`.
9. If practical, keep Windows Task Manager > Performance > GPU visible during GPU passes to capture Intel HD 620 utilization.
10. Send the evidence to the Orchestrator for the measured PASS / CONDITIONAL PASS / STOP decision.

No current `PoseTrackingSpike.unity` QA is required for this isolated spike because production motion code and the production scene are untouched.
