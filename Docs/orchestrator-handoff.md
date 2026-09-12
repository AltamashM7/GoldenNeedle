# Golden Needle — Orchestrator handoff

## Governance

Repository: `AltamashM7/GoldenNeedle`

Working branch: `engine/pose-tracking-spike`

Starting checkpoint for the hybrid GPU architecture spike: `3a4b851b606fa9af1063cc835286a3b389d8933e` — `fix: tighten inference launch timestamp boundary`.

Current status:
- Phase 4: **USER ACCEPTED — PASS**.
- Lab-camera quaternion / clear-only camera correction: **USER-runtime accepted**.
- Immediate Launch After Readback: **USER-runtime accepted**; keep ON.
- Direct Body CPU Readback: **USER-runtime PASS** as a modest optimization; retain as known-safe CPU fallback.
- Callback-driven readback scheduling: **CLOSED / REJECTED**.
- Result-callback / inference-completion scheduling optimization: **CLOSED** after USER steady-state measurement; do not introduce callback-driven inference launch without new evidence.
- Sentis hybrid GPU inference spike: **IMPLEMENTED / AWAITING USER RUNTIME BENCHMARK**.
- Phase 5A: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## Problem driving this track

The optimized MediaPipe CPU path remains stable and backlog-free but yields roughly `10–12` fresh pose results/s on the low-end proof laptop. Current representative USER evidence is approximately `55–65 ms` body readback, `60–75+ ms` accepted-request-to-result callback, and `110–140 ms` frame-to-result age depending on load. Fast movement can therefore lose trajectory detail unless the USER moves more slowly.

The GPU track is meant to test whether neural inference can be accelerated enough to raise fresh pose sampling toward `20–30/s` without sacrificing the existing world-landmark/canonical/retarget behavior or harming render performance.

## Frozen production architecture

Do not alter for this spike:
- production `MediaPipePoseProvider`;
- current `PoseTrackingSpike.unity`;
- camera/orientation conventions;
- latest-frame / no-backlog scheduling;
- body-only `320x240` CPU fallback preparation;
- DirectCPU + Homuler fallback readback;
- Pose Landmarker Lite CPU delegate fallback;
- canonical mapping and modular calibration;
- accepted Phase 4 Humanoid retargeting/IK and Neko binding;
- presentation smoothing;
- Phase 5A support/cadence/fusion/heading/recenter behavior;
- F12 Lab/Game behavior.

No GPU neural output is connected to the production avatar in this checkpoint.

## Sentis package and direct LiteRT strategy

`Packages/manifest.json` now requests `com.unity.ai.inference` `2.6.1`.

Modern Sentis directly imports LiteRT/TensorFlow Lite `.tflite` models. Therefore this checkpoint intentionally does **not** convert the current model to ONNX. If exact `.tflite` import fails in Unity, stop and report that failure; do not silently convert or substitute a model.

Unity package resolution has not been executed by the Web Builder, so the real `Packages/packages-lock.json` update is deliberately left to Unity rather than fabricated.

## Exact-model extraction and audit

The editor menu command:

`Golden Needle > GPU Inference Spike > Prepare Exact Models + Scene`

reads the existing production bundle at:

`Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes`

and extracts exactly one `pose_detector.tflite` plus exactly one `pose_landmarks_detector.tflite` into:

`Assets/GoldenNeedle/Debug/GpuInferenceSpike/Generated/`

The generated copies are byte-for-byte local derivatives, are git-ignored, and do not change the original bundle or Git LFS policy. The setup computes SHA-256 for the bundle and both submodels and writes raw TFLite audit data: inputs, outputs, dtypes/shapes, operator set and whether tensor quantization scales are present.

The connected Web Builder cannot decode this repository binary through its text-only GitHub interface, so those exact hash/metadata values are **pending the USER local setup run** rather than guessed.

The same setup imports both exact `.tflite` files as Sentis `ModelAsset`s and generates an isolated `GpuInferenceSpike.unity` scene. Import/backend compatibility is therefore measured on the USER's real Unity + Intel HD 620 machine.

## Diagnostic implementation

### Mode A — backend equivalence

For detector and landmark independently, deterministic zeros, gradient and seeded pseudo-random float NHWC tensors are run on both Sentis CPU and `BackendType.GPUCompute`.

Every output is asynchronously read and compared for:
- shape;
- finite/non-finite values;
- max absolute error;
- mean absolute error;
- RMS error;
- mean relative error.

This compares the exact same imported TFLite neural network across Sentis backends. It is not yet full MediaPipe pipeline equivalence because detector decode, ROI tracking and landmark/world-landmark postprocessing have not been reconstructed.

### Mode B — GPU-resident realistic path

The large input remains GPU-side:

```text
model-sized RenderTexture
-> CommandBuffer + RenderTargetIdentifier TextureConverter
-> GPU-resident float NHWC tensor
-> BackendType.GPUCompute
-> async readback of selected small float outputs
```

The command-buffer overload is important: the direct Texture overload is Texture2D-oriented, while the spike source is a RenderTexture. No full image is first read to CPU for this path.

Current readback selection is intentionally conservative: all float outputs at or below the configured element cap are read. The exact minimum future detector/landmark output subset must be determined from the eventual MediaPipe decode/postprocess reconstruction rather than guessed now.

Defaults: 30 warmup iterations, 300 measured iterations per model/backend. Statistics include mean, p50, p95, p99 and completed inferences/s. The UI reports actual Unity/OS/CPU/GPU/vendor/graphics API/graphics memory/compute support/Sentis assembly version plus frame-time samples.

## Intended hybrid CPU + GPU production shape if the spike passes

```text
CPU
  camera/session/latest-frame state
  detector-vs-tracked-ROI decision
  issue GPU work

GPU
  crop/resize/channel/layout/value preprocessing where compatible
  detector CNN when acquisition/reacquisition is needed
  landmark CNN on tracked ROI
  keep large image/tensor data GPU-resident

CPU
  asynchronously receive only small required outputs
  detector decode / ROI update or landmark/world-landmark postprocess
  confidence/trust + coordinate conversion
  existing PoseObservation
  existing canonical/stabilization/calibration/retarget/locomotion/gameplay
```

The CPU remains free for ordinary game/motion work while GPU inference is outstanding. CPU MediaPipe remains fallback/error recovery. Do **not** run duplicate CPU and GPU pose inference every frame.

## Verification / evidence boundary

Implemented source is ready for USER validation, but no Unity package resolution, compile, model import, worker execution, EditMode test run, GPU readback or HD 620 benchmark was executed by the Web Builder. No runtime success or numeric/performance result is claimed.

Current verdict: **UNMEASURED / AWAITING USER RUNTIME BENCHMARK**.

Decision thresholds from the spike brief:
- **Strong pass:** exact TFLite GPUCompute works with sane equivalent outputs; recurring landmark GPU-resident path including required output readback `>=20/s`, preferably `>=25/s`; clearly better than Sentis CPU; acceptable render impact.
- **Conditional pass:** roughly `15–20/s` equivalent recurring landmark path with meaningful benefit and acceptable render impact.
- **Stop/reject:** direct TFLite/GPUCompute fails, outputs materially disagree, GPU is slower after warmup, preprocessing/readback erases the gain, or render performance becomes unacceptable.

Do not lower model quality merely to achieve a pass.

## USER benchmark procedure

1. In GitHub Desktop, Fetch/Pull `engine/pose-tracking-spike`.
2. Open with Unity 6.5 and wait for package resolution/import/compilation.
3. Run `Golden Needle > GPU Inference Spike > Prepare Exact Models + Scene`.
4. If exact `.tflite` import fails, capture the full error and stop; no ONNX fallback in this task.
5. Open `Assets/GoldenNeedle/Debug/GpuInferenceSpike/Generated/GpuInferenceSpike.unity`.
6. Enter Play Mode.
7. Verify the UI hardware line identifies Intel HD Graphics 620, the actual graphics API, compute support and Sentis assembly version.
8. Click **Run benchmark**.
9. Wait for detector and landmark equivalence, CPU, GPUCompute and GPU-resident passes to finish. This can take several minutes on the i3-7100U/HD 620 machine.
10. If practical, show Windows Task Manager > Performance > GPU during GPU passes and note utilization without changing Unity's graphics API.
11. Capture final UI/Console output and `Generated/model-audit.txt`, plus a screenshot/short video and Task Manager evidence if practical.
12. Send all evidence to the Orchestrator for the measured decision.

No current production PoseTrackingSpike-scene QA is required for this checkpoint because production motion code and the user's dirty production scene are untouched.

## If the measured spike passes

The next narrow architecture phase is to reconstruct only the MediaPipe glue around the exact neural models: detector decode/NMS as needed, ROI acquisition/tracking/cadence, landmark decode, normalized + world landmark postprocessing and confidence semantics. Keep image/tensor neural work GPU-resident, transfer only proven-required small outputs to CPU, emit the existing `PoseObservation` contract, and keep the existing CPU MediaPipe provider as fallback. Do not begin Phase 6 as part of that work.
