# Golden Needle — Camera/Readback Latency Read-Only Audit

Status: **R1 COMPLETE — READ-ONLY AUDIT ONLY**

This document records the accepted architecture audit performed after the OpenVINO scheduling optimization. It contains no implementation authorization.

## Repository safety outcome

The audit originally started from:

`748a5ec1b30cdfc01b8441b3e6d01c64733e0e08`

During a continuation attempt, experimental implementation work was started prematurely. That work was abandoned and undone with a normal forward cleanup commit. The safe cleanup checkpoint was:

`3cb7752715e4e41d67087f5d02afa54373cb3628`

GitHub comparison between the original starting checkpoint and the cleanup checkpoint reports **zero changed files**. The transient implementation commits remain only in branch history and are not part of the active tree. Do not continue from those abandoned experimental commits.

`main` was not merged. Phase 6 was not started.

## Accepted bottleneck evidence

Post-OpenVINO-scheduling USER evidence was approximately:

```text
Camera              ~28-30 FPS
Fresh pose results  ~12-13/s
Body input           320x240
Frame -> result      ~100-105 ms

DirectCPU GPU readback:
submit -> callback   ~42.7 ms median
                    ~54.8 ms p95
callback -> poll     ~0.8 ms median
poll -> publish      ~0.2 ms median
```

The accepted OpenVINO worker/mailbox scheduling already removed the avoidable prepared-frame continuation delay. Callback observation and publication are now small relative to GPU readback completion.

## Current frame path

```text
WebCamTexture
  -> Graphics.Blit to persistent 320x240 RenderTexture
  -> AsyncGPUReadback.RequestIntoNativeArray
  -> pooled TextureFrame CPU buffer
  -> bounded latest-frame OpenVINO mailbox
  -> persistent OpenVINO worker
  -> native pose graph
```

For flipped inputs, a persistent staging RenderTexture may be used before readback.

The current DirectCPU path is already leaner than simply reverting to Homuler CPUAsync because it writes directly into the pooled TextureFrame raw CPU buffer and avoids unnecessary post-readback staging/application work.

## Candidate findings

### Continue optimizing AsyncGPUReadback callbacks/coroutines

**Rejected as the primary direction.**

The measured expensive interval is submission -> GPU completion/callback. Callback polling and publication are already near ~1 ms or below. Rearranging Awaitable/coroutine/callback observation does not remove the GPU->CPU availability delay.

### Standard Homuler CPUAsync

**Rejected.**

It still relies on AsyncGPUReadback and does not remove the architectural bottleneck.

### Homuler synchronous CPU texture read

**Not recommended without measurement.**

It may trade asynchronous latency for a synchronous main/render-thread stall, which is risky on the target low-end laptop.

### `WebCamTexture.GetPixels32(Color32[] buffer)` + reusable CPU preparation

**Highest-priority next candidate.**

Conceptual path:

```text
WebCamTexture
  -> GetPixels32(reused camera-sized Color32[])
  -> reusable CPU resize/H-V transform to 320x240 RGBA
  -> existing bounded OpenVINO latest-frame mailbox
  -> existing OpenVINO worker
```

Advantages:

- reuses Unity's existing camera/device handling;
- keeps the full-resolution WebCamTexture available for preview/presentation;
- caller-owned buffer can avoid per-frame camera-array allocation;
- can potentially bypass the RenderTexture -> AsyncGPUReadback round trip;
- is managed-side and easy to abandon if measurement is poor;
- preserves the accepted OpenVINO/native graph unchanged.

Critical uncertainty:

Unity's API availability does **not** prove `GetPixels32` is cheap on this specific Windows webcam/driver. It may still synchronize/copy internally. Same-machine measurement is required before accepting it.

### CPU resize feasibility

Current camera is approximately 640x480 and body input is 320x240, a convenient 2:1 reduction in each dimension. A bounded experiment can use persistent buffers with no frame queue and no per-frame allocation.

Initial implementation should preserve the current filtered-image semantics closely enough for a fair motion/pose comparison rather than intentionally sacrificing image quality for speed.

### Alternate graphics API / forcing D3D12

**Rejected for now.**

There is no evidence that a graphics API switch solves the camera->CPU architecture, and forcing D3D12 introduces unnecessary compatibility risk.

### Native Windows camera capture / Media Foundation

**Deferred.**

A native CPU capture stream could ultimately be faster, but it significantly expands camera enumeration, format negotiation, ownership, switching, orientation, mirroring, lifecycle and preview integration complexity. It becomes reasonable only if the low-risk Unity CPU-pixel experiment fails to improve the measured stage.

## Recommended next experiment — NOT YET AUTHORIZED BY THIS DOCUMENT

If the USER/Orchestrator explicitly approves implementation, begin fresh from the then-current safe branch and implement a **small selectable CPU-camera experiment**:

```text
Mode A — current accepted baseline
WebCamTexture -> 320x240 RT -> AsyncGPUReadback -> existing OpenVINO mailbox

Mode B — CPU camera experiment
WebCamTexture -> reused GetPixels32 buffer -> reused CPU 320x240 RGBA preparation -> existing OpenVINO mailbox
```

Requirements:

- current DirectCPU/Homuler path remains available as fallback and A/B baseline;
- no silent fallback in telemetry;
- persistent buffers only;
- max one active inference and one replaceable latest pending frame;
- no FIFO/history/replay/catch-up queue;
- preserve 320x240 initial body input;
- preserve `InferenceFlipHorizontally`, `InferenceFlipVertically`, and `InferenceRotationDegrees` semantics;
- preserve canonical, calibration, retargeting, locomotion and partial-body semantics;
- no native OpenVINO rebuild/model change should be needed for this experiment.

## Required measurement if implementation is later approved

Measure separately:

- `GetPixels32` acquisition time;
- CPU resize/flip preparation time;
- total camera -> prepared CPU frame time;
- prepared -> OpenVINO worker launch;
- OpenVINO graph/inference time;
- frame -> result latency;
- fresh results/s and pose age;
- camera/render FPS;
- buffer/allocation boundedness;
- F12 fast-motion responsiveness and partial-body recovery.

The key comparison is current DirectCPU submit->callback versus CPU acquisition + CPU preparation under the same camera, 320x240 target, OpenVINO backend, lighting/framing and downstream settings.

## Decision boundary

R1 architecture audit is complete.

**Do not implement R2 from this document or from the old broad handoff until the USER/Orchestrator gives a new explicit implementation approval.**
