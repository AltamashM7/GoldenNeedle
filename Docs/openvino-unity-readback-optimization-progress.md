# Golden Needle — Camera/Readback Latency Optimization Progress

This is the rolling resume point for the readback-latency investigation that follows the accepted OpenVINO scheduling optimization.

A replacement Web Builder must read this file first and treat it as newer authority than the original broad readback handoff.

## Current authorization and status

- USER authorization for the readback investigation: **APPROVED**.
- Branch: `engine/pose-tracking-spike`.
- Gate A OpenVINO model compatibility: **PASS**.
- Gate B MediaPipe/OpenVINO semantic parity: **PASS WITH NOTES**.
- Unity OpenVINO integration correctness: **PASS**.
- OpenVINO scheduling optimization: **USER ACCEPTED — PASS**.
- Readback optimization R1 architecture audit: **COMPLETE — READ-ONLY**.
- Readback optimization R2 implementation: **NOT AUTHORIZED YET**.
- Stock MediaPipe/TFLite CPU remains available and default-safe.
- Phase 5A remains **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## Repository safety / overstep recovery

The read-only audit originally started from:

`748a5ec1b30cdfc01b8441b3e6d01c64733e0e08`

During a continuation after an execution-limit interruption, experimental implementation was started prematurely. The USER stopped that work and required the Builder to clean the branch.

Safe cleanup checkpoint:

`3cb7752715e4e41d67087f5d02afa54373cb3628`

GitHub comparison between `748a5ec...` and `3cb7752...` reports **zero changed files**, and both commits point to the same tree. The abandoned experimental commits remain only in history. Do not continue from them and do not rewrite shared history merely to remove them.

The durable audit is recorded in:

`Docs/openvino-unity-readback-audit.md`

## Accepted bottleneck evidence

Post-scheduling USER evidence is approximately:

```text
Camera ~28-30 FPS
Fresh pose results ~12-13/s
Body input 320x240
Frame->result ~100-105 ms

DirectCPU readback:
submit->callback ~42.7 ms median / ~54.8 ms p95
callback->poll ~0.8 ms median / ~1.3 ms p95
poll->publish ~0.2 ms median / ~0.4 ms p95
```

Interpretation:

- OpenVINO inference itself is already faster than stock TFLite;
- OpenVINO worker/mailbox scheduling has been accepted and should not be reopened absent new evidence;
- callback polling and publication are already small;
- the remaining dominant pre-inference stage is GPU/readback completion.

## R1 audit result

The read-only audit evaluated the existing DirectCPU path, Homuler/MediaPipeUnityPlugin CPU read modes, Unity webcam CPU pixel access, graphics-transfer alternatives, and native Windows capture.

### Rejected / deferred

- Further coroutine/callback optimization: rejected as primary direction; it does not remove the measured GPU completion interval.
- Standard Homuler CPUAsync: rejected; it retains the same AsyncGPUReadback architecture.
- Homuler synchronous CPU texture read: do not adopt without hardware measurement because it may trade latency for a main/render-thread stall.
- D3D12/global graphics API switch: rejected for now.
- Native Windows/Media Foundation camera stack: deferred until lower-risk Unity mechanisms are measured and shown insufficient.

### Highest-priority candidate

`WebCamTexture.GetPixels32(Color32[] reusableBuffer)` plus reusable CPU 640x480 -> 320x240 resize/flip preparation.

Conceptual candidate path:

```text
WebCamTexture
  -> GetPixels32(reused camera-sized Color32[])
  -> reusable CPU resize/H-V transform to 320x240 RGBA
  -> existing bounded OpenVINO latest-frame mailbox
  -> existing OpenVINO worker
```

Why it is promising:

- reuses Unity's current camera/device lifecycle;
- preserves full-resolution `WebCamTexture` for presentation;
- can avoid per-frame camera-array allocation;
- may bypass the RenderTexture -> AsyncGPUReadback round trip;
- requires no OpenVINO model/native ABI changes;
- can remain optional and easily abandoned if measurement is poor.

Key uncertainty:

`GetPixels32` may still synchronize/copy expensively on the USER's Windows webcam/driver. Only same-machine runtime measurement can determine whether the candidate is actually lower latency.

## Invariants for any later R2 experiment

If the USER/Orchestrator explicitly approves R2 implementation later:

- keep existing DirectCPU/Homuler paths available as fallback and A/B baseline;
- keep OpenVINO worker/mailbox scheduling unchanged downstream;
- preserve max one active inference and one replaceable newest pending frame; no FIFO/history/replay/catch-up queue;
- use persistent/reusable buffers only;
- keep initial body input at 320x240;
- preserve `InferenceFlipHorizontally`, `InferenceFlipVertically`, `InferenceRotationDegrees`, display mirroring and canonical left/right semantics;
- preserve camera switching/restart/teardown safety;
- preserve canonical, stabilization, calibration, retargeting, locomotion and partial-body semantics;
- no detector conversion/densification;
- no native OpenVINO rebuild unless native inputs actually change;
- expose active acquisition/readback mode and explicit fallback reason in telemetry.

## Measurement required if R2 is later approved

Compare current DirectCPU against the CPU-webcam candidate with the same camera, OpenVINO backend, 320x240 body target, lighting/framing and downstream settings.

Measure separately:

- `GetPixels32` acquisition time;
- CPU resize/flip preparation time;
- total camera -> prepared CPU frame time;
- prepared -> OpenVINO worker launch;
- OpenVINO graph/inference time;
- frame -> result latency;
- fresh results/s and pose age;
- camera/render FPS;
- bounded buffer/allocation behavior;
- F12 fast-arm responsiveness and partial-body recovery.

## Exact next action

**STOP at the R1 decision boundary.**

Do not implement the `GetPixels32` candidate merely because the old broad handoff originally allowed continuous R1->R2 development.

The next action is for the USER/Orchestrator to review the completed audit and explicitly decide whether R2 implementation of the small selectable CPU-camera experiment is authorized.

Until that explicit approval:

- no readback implementation changes;
- no custom camera stack;
- no OpenVINO/native changes;
- no Phase 6;
- no merge to `main`.
