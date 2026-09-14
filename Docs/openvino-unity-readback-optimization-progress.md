# Golden Needle — Camera/Readback Latency Optimization Progress

This is the rolling resume point for the readback-latency optimization that follows the accepted OpenVINO scheduling optimization.

A replacement Web Builder must read this file first and treat it, plus `Docs/worker-briefs/openvino-unity-readback-r2-implementation-handoff.md`, as newer authority than the original broad readback handoff.

## Current authorization and status

- USER authorization for the readback investigation: **APPROVED**.
- R1 architecture audit: **COMPLETE — READ-ONLY**.
- R2 small selectable CPU-webcam implementation experiment: **EXPLICITLY USER AUTHORIZED on 2026-09-14**.
- Branch: `engine/pose-tracking-spike`.
- Gate A OpenVINO model compatibility: **PASS**.
- Gate B MediaPipe/OpenVINO semantic parity: **PASS WITH NOTES**.
- Unity OpenVINO integration correctness: **PASS**.
- OpenVINO scheduling optimization: **USER ACCEPTED — PASS**.
- Stock MediaPipe/TFLite CPU remains available and default-safe.
- Phase 5A remains **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## Repository safety / overstep recovery

The original read-only audit started from:

`748a5ec1b30cdfc01b8441b3e6d01c64733e0e08`

During a continuation after an execution-limit interruption, experimental implementation was started prematurely. The USER stopped that work and required cleanup.

Safe cleanup checkpoint:

`3cb7752715e4e41d67087f5d02afa54373cb3628`

GitHub comparison between `748a5ec...` and `3cb7752...` reports **zero changed files**, and both commits point to the same tree. The abandoned transient implementation commits remain only in branch history. Do not continue from them and do not rewrite shared history merely to erase them.

The Orchestrator then added documentation-only decision checkpoints. No runtime/provider/native/scene implementation from the abandoned experiment is active.

Durable R1 audit:

`Docs/openvino-unity-readback-audit.md`

Primary R2 implementation brief:

`Docs/worker-briefs/openvino-unity-readback-r2-implementation-handoff.md`

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

- OpenVINO inference is already faster than stock TFLite;
- OpenVINO worker/mailbox scheduling has been accepted and must not be reopened absent new evidence;
- callback polling and publication are already small;
- the remaining dominant pre-inference stage is GPU/readback completion.

## R1 audit result

R1 evaluated the existing DirectCPU path, Homuler/MediaPipeUnityPlugin CPU read modes, Unity webcam CPU pixel access, graphics-transfer alternatives and native Windows capture.

Rejected/deferred:

- further coroutine/callback optimization as primary direction;
- standard Homuler CPUAsync because it retains AsyncGPUReadback;
- synchronous Homuler CPU texture read without hardware measurement because it may simply trade latency for main/render-thread stall;
- forcing D3D12;
- custom/native Windows/Media Foundation capture until the lower-risk Unity CPU-pixel experiment is measured.

Highest-priority candidate:

`WebCamTexture.GetPixels32(Color32[] reusableBuffer)` plus reusable CPU camera->320x240 resize/flip preparation.

Conceptual candidate path:

```text
WebCamTexture
  -> GetPixels32(reused camera-sized Color32[])
  -> reusable CPU resize/H-V transform to 320x240 RGBA
  -> existing bounded OpenVINO latest-frame mailbox
  -> existing OpenVINO worker
```

Why it is worth R2:

- reuses Unity camera/device lifecycle;
- preserves full-resolution `WebCamTexture` presentation;
- can avoid per-frame camera-array allocation;
- may bypass the RenderTexture -> AsyncGPUReadback round trip;
- requires no OpenVINO model/native ABI change;
- can remain optional and be abandoned cleanly if measurement is poor.

Key uncertainty:

`GetPixels32` may still synchronize/copy expensively on the USER's Windows webcam/driver. Same-machine runtime measurement is the purpose of R2.

## R2 invariants

R2 implementation must:

- keep existing DirectCPU/Homuler path available as explicit fallback and A/B baseline;
- keep OpenVINO worker/mailbox scheduling unchanged downstream;
- preserve max one active inference and one replaceable newest pending frame; no FIFO/history/replay/catch-up queue;
- use persistent/reusable buffers only;
- keep initial body input at 320x240;
- preserve inference H/V flip, rotation, display-mirror and canonical left/right semantics;
- preserve camera switching/restart/teardown safety;
- preserve canonical, stabilization, calibration, retargeting, locomotion and partial-body semantics;
- avoid detector conversion/densification;
- avoid native OpenVINO rebuild unless native inputs genuinely change;
- expose active acquisition/readback mode and explicit fallback reason in Inspector/F7 telemetry;
- not edit USER-owned scene YAML merely for convenience.

## R2 required measurement

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

## Development / recovery flow

Checkpoints are recovery markers, not approval gates.

The Builder should work continuously through:

1. **R2-A** — independently inspect the current active provider and confirm the exact ownership seam before editing;
2. **R2-B** — implement selectable `GetPixels32` CPU acquisition with persistent camera buffer and reusable 320x240 preparation;
3. **R2-C** — add/verify minimal telemetry, explicit fallback, lifecycle/teardown/camera-switch safety and bounded ownership;
4. **R2-D** — perform proportionate managed/static/helper verification and prepare concise USER A/B instructions.

At each coherent recovery point:

- commit/push to `engine/pose-tracking-spike`;
- update this file with exact ending SHA, files changed, verification, risks and next concrete action;
- continue immediately when no USER input is genuinely required.

If execution budget becomes low, checkpoint first and write an exact `CONTINUE FROM HERE` section. A replacement Builder must resume from this file and the active branch tree, not from abandoned transient commits.

## Exact next action

R2 implementation is now authorized.

The next Web Builder should:

1. verify current remote HEAD and read `AGENTS.md`, this progress file, the durable R1 audit and the R2 implementation handoff;
2. inspect current `MediaPipePoseProvider.cs` and relevant Inspector/F7 code before editing;
3. implement the small selectable `WebCamTexture.GetPixels32` CPU-acquisition experiment only;
4. keep DirectCPU/Homuler and stock TFLite paths intact;
5. continue through verification and USER A/B preparation;
6. stop only for genuine USER runtime QA, a real blocker, or execution-limit risk;
7. do not start a custom Windows camera stack, Phase 6, or merge to `main` from R2.
