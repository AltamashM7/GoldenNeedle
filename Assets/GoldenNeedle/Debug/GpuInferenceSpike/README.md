# Golden Needle GPU Inference Spike

This folder is an isolated architecture proof. It does **not** replace `MediaPipePoseProvider`, alter the production pose pipeline, or drive the production avatar.

## Prepare exact models

After Unity resolves `com.unity.ai.inference` 2.6.1, use:

`Golden Needle > GPU Inference Spike > Prepare Exact Models + Scene`

The editor tool reads the existing production bundle at
`Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes`, extracts
`pose_detector.tflite` and `pose_landmarks_detector.tflite` byte-for-byte into the ignored
`Generated/` folder, computes SHA-256 plus TFLite input/output/operator/quantization metadata, imports
the exact `.tflite` files with Sentis, and creates a separate diagnostic scene. It never converts the
models to ONNX.

The generated model copies, audit and scene are intentionally git-ignored because they are exact local
derivatives of the production bundle. The production model and production `PoseTrackingSpike` scene
are not modified.

## Run

Open `Generated/GpuInferenceSpike.unity`, enter Play Mode, then click **Run benchmark**. The runner performs:

- Sentis CPU vs GPUCompute output equivalence on deterministic zero/gradient/random tensors;
- CPU and GPUCompute warmup + timing for detector and landmark models;
- a model-sized `RenderTexture -> TextureConverter -> GPUCompute -> asynchronous selected-output readback` path with one inference outstanding;
- hardware, graphics API, model/layer, output transfer, and frame-time diagnostics.

The default is 30 warmup + 300 measured iterations per model/backend. Reduce measured iterations no
lower than 100 only if the low-end machine makes 300 impractical.

The performance readback currently selects all float outputs at or below the configured element cap,
which intentionally excludes large segmentation-like outputs. Exact minimum output selection belongs
to the later MediaPipe detector/ROI/postprocess reconstruction step and must be based on that decode.

This is not production-equivalent pose inference: MediaPipe detector decode, ROI tracking,
landmark/world-landmark postprocessing, and exact image normalization are intentionally deferred until
the raw neural GPU path proves worthwhile.
