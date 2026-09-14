# Golden Needle — OpenVINO Scheduling Optimization USER A/B QA

Status: **mandatory USER runtime QA boundary** for the post-U4 OpenVINO scheduling optimization.

This test is not a new backend-correctness gate. U4 correctness already passed. Its purpose is to determine whether the bounded OpenVINO continuation/mailbox change converts the proven OpenVINO compute advantage into a measurable end-to-end Unity scheduling/latency improvement without introducing backlog, concurrency, restart, camera-switch, or motion-quality regressions.

Only the USER/Orchestrator may classify the result.

## Before Unity

1. Update `engine/pose-tracking-spike` to the latest remote checkpoint.
2. Keep Unity closed while updating/preparing files.
3. If the generated OpenVINO runtime/model staging is absent on this machine, or local ignored generated files were removed, run from the repository root:

```powershell
.\Tools\OpenVinoUnityPosePlugin\scripts\prepare_unity.ps1
```

Require:

```text
OPENVINO_UNITY_LOCAL_PREP=PASS
```

The scheduling optimization changes managed provider code only. It does **not** require a new native OpenVINO binary when the existing generated package is already present and valid.

Do not commit generated native/model/package output or the USER-owned scene merely to perform this test.

## Keep the A/B conditions identical

For both runs keep unchanged:

- `PoseTrackingSpike` scene;
- same physical webcam/device;
- same lighting, framing and distance;
- same 320x240 body inference target;
- DirectCPU readback enabled as in the accepted baseline;
- Immediate Launch After Readback unchanged;
- same target inference FPS;
- same confidence/stabilization/calibration/retarget/presentation settings;
- no OBS or other heavy recording workload during the numerical comparison if it can be avoided.

A phone/external recording is preferable if visual comparison footage is wanted without perturbing laptop performance.

## Run A — stock MediaPipe/TFLite CPU

1. Set **Inference Backend** to `MediaPipe Tflite Cpu`.
2. Enter Play Mode.
3. Confirm F7 reports the stock MediaPipe/TFLite backend and provider Ready.
4. Let values settle for roughly 15–20 seconds.
5. Perform the common movement sequence below.
6. Capture one clear F7 diagnostics screenshot during steady operation.
7. Note any Console warnings/errors, stalls, motion skips or recovery problems.
8. Exit Play Mode normally.

Stock behavior is intentionally unchanged by this optimization and serves as the same-machine reference.

## Run B — optimized OpenVINO CPU FP32

1. Set **Inference Backend** to `Open Vino Cpu Fp32` without changing any other tracking setting.
2. Enter Play Mode.
3. Confirm F7 reports **OpenVINO CPU FP32** and does not silently fall back.
4. Let values settle for roughly 15–20 seconds.
5. Perform the same movement sequence.
6. Capture one clear F7 diagnostics screenshot during steady operation.
7. Keep Play Mode running long enough to verify the mailbox allocation count stabilizes rather than climbing continuously.
8. Perform the camera-switch/restart checks below.
9. Exit Play Mode and confirm teardown does not hang or crash.

## Common movement sequence

Use the same order for both backends:

1. neutral stance and slow arm movement;
2. repeated fast arm swings/reaches, including toward frame edges;
3. torso lean/rotation and return to neutral;
4. knee/leg movement when visible;
5. brief partial-body loss/occlusion, then return to normal framing;
6. repeat several fast arm motions while watching F12/avatar behavior.

Record whether OpenVINO still feels smoother, becomes more responsive, or introduces any instability versus stock.

## Scheduling telemetry to capture

For **both** backends capture from F7:

- camera FPS;
- render FPS;
- inference requests/s;
- fresh pose results/s;
- latest pose age;
- last request/inference duration;
- approximate frame-to-result latency;
- readback duration/path/stage;
- prepared-to-launch delay;
- prepared-to-launch frame delta;
- last launch origin;
- immediate/fast launch rate;
- wait/drop/replacement pressure;
- continuation timing sample count and missing count;
- continuation `U/RB/OVW` origin counts;
- prepared-waiting continuation rate;
- result-to-next-launch median/p95, especially the prepared-only values.

For **OpenVINO** also capture:

- managed RGBA copy time;
- graph processing time;
- detector inference time;
- landmark inference time;
- bridge/copy time;
- detector-ran state;
- scheduling suffix: `pending`, `active`, `ovw=/s`, `buffers`.

### Expected bounded mailbox behavior

The OpenVINO scheduling line should remain bounded:

- `pending` must be only `0` or `1`;
- `active` must be only `0` or `1`;
- at a fixed resolution the reusable buffer-allocation count should settle (normally at two once both slots have been used), not rise continuously;
- `OVW`/`ovw=/s` should become non-zero when a newer frame was already waiting as the previous OpenVINO inference completed;
- no FIFO/history/catch-up behavior should appear.

## What improvement would support acceptance

Do **not** require a particular requests/s number in advance; use same-session evidence.

The optimization is doing its intended job if, compared with the previous OpenVINO scheduling evidence and the same-session stock run:

- OpenVINO no longer commonly waits an extra render-frame-sized delay after a result when a newer frame is already available;
- prepared-to-launch delay materially drops from the previous ~19.9 ms symptom when continuation is possible;
- frame delta is more often `0` instead of the previous `1` symptom;
- `OVW` worker-continuation launches appear and prepared-only result-to-next-launch timing materially drops;
- request/result throughput and/or pose freshness improve enough to expose more of OpenVINO's already-proven ~32 ms graph/inference-side advantage;
- fast motion remains at least as stable/useful as before;
- no backlog, growing allocation count, double inference, crash, hang, or native error appears.

Cadence may legitimately impose a small remaining wait when the configured target-inference interval has not elapsed. The goal is to remove the avoidable Unity `Update` wait, not bypass the configured target FPS.

## Camera switch and restart safety

During the OpenVINO run:

1. While tracking is active, request one camera switch if an alternate usable webcam/device exists.
2. Confirm the provider does not hang/crash and resumes Ready on the selected camera.
3. If no alternate camera exists, use the existing retry/restart path instead.
4. Exit and re-enter Play Mode once.
5. Confirm no native-plugin error, stale pose burst, runaway backlog, or teardown hang occurs.

A camera switch/restart is allowed to discard the single pending latest frame. It must never replay old frames afterward.

## Evidence to return

Return to the Orchestrator/Builder:

- stock F7 screenshot;
- optimized OpenVINO F7 screenshot;
- any Console warning/error text;
- short observations on responsiveness and fast-arm fidelity;
- partial-body recovery result;
- camera-switch or retry/restart result;
- Play Mode stop/re-enter result;
- whether the OpenVINO `buffers` count stayed stable;
- optional short external recording if visual behavior differs materially.

## Result ownership

Only the USER/Orchestrator may classify this optimization as:

- **PASS — scheduling gain accepted**;
- **PASS WITH NOTES — improvement accepted with remaining limits**;
- **FAIL — optimization regressed behavior / revert or revise**;
- **NO MATERIAL GAIN — correctness retained but scheduling change is not worth keeping**.

Do not change the production/default backend policy, merge to `main`, reclassify Phase 5A, or begin Phase 6 from this document alone.
