# Golden Needle — OpenVINO Unity U4 A/B QA

Status: **USER runtime/visual QA procedure**. Do not mark PASS until the USER performs this test on the actual Windows laptop/webcam setup.

This procedure compares the accepted stock MediaPipe/TFLite CPU backend against the experimental OpenVINO CPU FP32 backend under the same Unity scene, camera, body-inference resolution and physical movement conditions.

## Before opening Unity

1. Use GitHub Desktop to update `engine/pose-tracking-spike` to the latest remote checkpoint.
2. Close Unity before preparing the native package.
3. From the repository root in PowerShell, run:

```powershell
.\Tools\OpenVinoUnityPosePlugin\scripts\prepare_unity.ps1
```

Do not open Unity until the script prints:

```text
OPENVINO_UNITY_LOCAL_PREP=PASS
```

The generated experimental runtime is staged under:
- `Assets/GoldenNeedle/Plugins/OpenVinoPose/x86_64/`
- `Assets/StreamingAssets/GoldenNeedle/OpenVinoPoseModels/`

Generated `.dll`, `.tflite`, related `.meta`, `plugins.xml`, build and bootstrap artifacts are intentionally ignored. Do not add them to Git. Stock Homuler/MediaPipe files are not replaced.

## Keep the A/B conditions identical

For both runs use:
- the same `PoseTrackingSpike` scene and same camera;
- body inference downscale unchanged at the current 320x240 target;
- the same DirectCPU readback setting used by the current baseline;
- Immediate Launch After Readback unchanged;
- the same calibration/retarget/presentation settings;
- approximately the same lighting, camera distance and visible portion of the body;
- F7/main diagnostics visible when taking the measurement screenshot.

Do not change stabilization, confidence, calibration, retargeting, locomotion or presentation smoothing to make either backend look better.

## Run A — stock MediaPipe/TFLite CPU

1. In the `MediaPipePoseProvider` Inspector, set **Inference Backend** to `MediaPipe Tflite Cpu`.
2. Enter Play Mode.
3. Confirm the diagnostics report the effective backend as **MediaPipe TFLite CPU** and the provider reaches Ready.
4. Allow the metrics to settle, then perform the movement sequence below.
5. Capture one clear diagnostics screenshot near the end of the run and note any warnings/errors.
6. Exit Play Mode.

## Run B — experimental OpenVINO CPU FP32

1. Without changing the camera or other tracking settings, set **Inference Backend** to `Open Vino Cpu Fp32`.
2. Enter Play Mode.
3. Confirm the diagnostics report the effective backend as **OpenVINO CPU FP32**. If initialization fails, stop and return the full status/error instead of treating the run as a fallback success.
4. Perform the same movement sequence.
5. Capture one clear diagnostics screenshot near the end and note any warnings/errors.
6. Exit Play Mode and verify teardown does not hang or crash.

The backend switch may make the USER-owned scene locally dirty. That is acceptable for the experiment; do not commit the scene merely for this A/B selection.

## Movement sequence for each backend

Use the same sequence for both runs:

1. Neutral stance and slow arm movement.
2. Several deliberately fast arm swings/reaches, including reaching toward the edge of the camera frame.
3. Torso lean/rotation and return to neutral.
4. Knee/leg movement when the lower body is visible.
5. Brief partial-body loss/occlusion, then return to normal framing.

Pay particular attention to whether fast motion is visibly sampled more faithfully or still skips important intermediate/extreme poses.

## Evidence to capture from F7 diagnostics

For each backend record or screenshot:
- effective backend label;
- camera capture FPS;
- Unity render FPS;
- inference requests/s and fresh pose results/s;
- latest pose age;
- last accepted request -> result/inference duration;
- approximate frame -> result latency;
- GPU/readback duration;
- prepared-frame replacement/drop pressure and other wait/pressure counters;
- tracked raw/canonical/stabilized counts;
- initialization/restart/teardown warnings or failures.

For OpenVINO also capture:
- managed RGBA input-copy time;
- graph processing time;
- detector inference time;
- landmark inference time;
- native bridge/copy time;
- detector-ran state.

Reference stock observations before this experiment were commonly about:
- camera 29–30 FPS;
- fresh pose results 10–12/s;
- DirectCPU readback 55–65 ms;
- frame-to-result 110–140 ms depending on load.

Those numbers are reference context only. Judge the A/B from the same-session measurements on the USER machine.

## What to return to the Orchestrator/Builder

Return:
- the stock-backend diagnostics screenshot;
- the OpenVINO-backend diagnostics screenshot;
- any Console warnings/errors or initialization failure text;
- a short note on fast-arm fidelity, torso/leg behavior, partial-body recovery and whether either backend looked less stable;
- whether stopping/restarting Play Mode and changing camera/retrying caused any crash, hang or native-plugin error.

A short recording of the two movement runs is useful when visual fidelity differs, but the screenshots plus explicit observations above are the minimum evidence.

## U4 result ownership

Only the USER/Orchestrator may classify U4 as:
- **PASS**;
- **PASS WITH NOTES**;
- **FAIL / FALLBACK TO STOCK**.

Do not change the production/default backend policy or begin Phase 6 from this document alone.
