# Web Builder Handoff — Camera/Readback Latency Optimization

Role: intelligent Web Builder

Repository: `AltamashM7/GoldenNeedle`

Branch: `engine/pose-tracking-spike`

This handoff authorizes the next approved Golden Needle performance optimization after the OpenVINO scheduling optimization. The checkpoints are **recovery markers, not mandatory approval gates**. Work continuously until USER runtime/hardware QA is genuinely required, a real blocker requires a decision, or execution-limit risk makes a handoff necessary.

If you are a replacement Builder after an execution-limit interruption, do not restart the task. Read `Docs/openvino-unity-readback-optimization-progress.md`, inspect current remote HEAD and the existing implementation, then continue from the first unfinished action.

## Starting-state procedure

Before changing anything:

1. Verify repository, branch and current remote `engine/pose-tracking-spike` HEAD.
2. Inspect local Git status if you have a checkout; preserve USER-local dirty files and generated package state.
3. Read, in this order:
   - `AGENTS.md`
   - `Docs/openvino-unity-readback-optimization-progress.md`
   - `Docs/openvino-unity-scheduling-optimization-progress.md`
   - `Docs/openvino-unity-integration-progress.md`
   - `Docs/current-state.md`
   - current `MediaPipePoseProvider.cs`
   - relevant Homuler/MediaPipeUnityPlugin readback/image-source code.
4. Treat the new readback optimization progress file as the newest authority for this task.

## Accepted evidence entering this task

The OpenVINO backend is already integrated correctly and the USER has accepted the latest scheduling optimization.

Accepted post-scheduling USER evidence is approximately:

```text
Camera ~28.4 FPS
Pose results ~12.7/s
Readback ~50 ms
Frame->result ~105 ms
Prep->launch = 0.0 ms
frame delta = 0
origin = RB
fast launches ~12.7/s
U/RB/OVW = 0/59/5
ovw ~2.9/s
pending = 0
active = 1
buffers = 2
```

The USER reports OpenVINO now feels substantially better and more responsive in F12/avatar control.

The OpenVINO scheduling optimization is therefore **accepted**. Do not reopen or redesign it without new evidence.

The current dominant visible bottleneck is the body-frame GPU readback:

```text
DirectCPU submit->callback median ~42.7 ms
DirectCPU submit->callback p95 ~54.8 ms
callback->poll ~0.8 / ~1.3 ms
poll->publish ~0.2 / ~0.4 ms
```

This strongly suggests that polling/publishing overhead is already small and the expensive stage is GPU submission -> callback / GPU->CPU availability.

## Goal

Find and implement the lowest-risk architecture that materially reduces camera/body-pose input latency before OpenVINO, ideally avoiding or restructuring the current GPU round-trip, while preserving Golden Needle's accepted motion semantics and low-end-laptop product constraints.

The practical question is:

> Can we substantially reduce the current ~43-55 ms pre-inference readback delay without breaking webcam compatibility, orientation/mirroring semantics, 320x240 pose input, frame freshness policy, partial-body tracking, camera switching, teardown safety, or stock fallback?

## Non-negotiable invariants

- Keep all work on `engine/pose-tracking-spike`.
- Do not merge to `main`; no force push/rebase/reset/shared-history rewrite.
- Stock MediaPipe/TFLite CPU must remain usable.
- OpenVINO CPU FP32 must remain usable and keep the accepted persistent-worker/latest-frame mailbox scheduling.
- Preserve max one active readback, one active inference and one replaceable newest pending frame; no FIFO/history/replay/catch-up queue.
- Preserve existing canonical mapping, stabilization, confidence, calibration, retargeting, locomotion, presentation/F12 behavior and partial-body semantics.
- Keep full-resolution camera/display behavior unchanged unless a measured replacement proves equivalent visual/input semantics.
- Keep body inference at 320x240 for initial A/B.
- Preserve exact orientation, rotation, H/V flip and mirror semantics.
- Preserve camera-switch/restart/teardown safety.
- Do not force D3D12 globally.
- Do not densify/convert detector models.
- Do not refactor the fixed 33-landmark provider frame or current 20-joint canonical topology in this optimization.
- Do not edit USER-owned `PoseTrackingSpike.unity` YAML merely for convenience.
- Do not casually clean/revert known USER-local changes or generated package state.
- Phase 5A remains not USER accepted. Phase 6 remains not started.

## Reuse-first architecture audit

Do not jump directly to a custom camera stack.

First inspect and compare the actual available paths in Unity 6.5 / Homuler 0.16.3 / MediaPipe 0.10.22.

At minimum investigate:

### Current path

```text
WebCamTexture (GPU-visible texture)
  -> optional 320x240 RenderTexture blit
  -> AsyncGPUReadback.RequestIntoNativeArray
  -> TextureFrame CPU/raw memory
  -> managed latest-frame mailbox
  -> OpenVINO worker/native graph
```

Determine where the ~43-55 ms submit->callback cost comes from on the current laptop/hardware.

### Candidate classes of alternative

Audit, with source/API evidence, whether any of these are realistically lower latency:

1. **Unity CPU pixel access from WebCamTexture**
   - `GetPixels32`, raw/pixel APIs, reusable arrays, synchronous cost, main-thread requirements.
   - Determine whether the camera frame already exists on CPU internally or whether these calls simply cause an equally expensive readback.

2. **CPU-owned frame + CPU resize/conversion**
   - If Unity can expose fresh camera pixels cheaply, benchmark 640x480 -> 320x240 conversion/copy cost on this low-end CPU.
   - Reuse fixed buffers; no per-frame allocations.

3. **Homuler/MediaPipe image-source/native helpers**
   - Inspect whether existing MediaPipeUnityPlugin code has a CPU image/camera path or a more direct transfer seam we can reuse.

4. **Graphics-transfer alternatives**
   - Any Unity API or format/layout change that avoids the observed stall while retaining compatibility with Intel HD 620/D3D11.
   - Do not force D3D12 merely to chase a different readback behavior.

5. **Native Windows camera capture**
   - Consider only if the Unity/Homuler paths are demonstrably unable to avoid the readback bottleneck.
   - Prefer a mature Windows API/library path over writing a fragile camera stack from scratch.
   - Must preserve camera enumeration/selection, rotation/orientation/mirroring behavior and full-resolution preview compatibility or cleanly separate the pose input stream from presentation.

## Measurement discipline

Do not over-instrument immediately. The existing telemetry already tells us callback polling and publish are tiny.

Only add diagnostics if needed to answer a concrete unresolved question, such as distinguishing:

- render/blit submission delay;
- GPU queue/fence completion;
- readback transfer itself;
- CPU copy/downscale cost;
- Unity main-thread observation delay.

Any diagnostic must have low enough overhead not to destroy the measurement.

## Implementation guidance

Once the audit identifies the best justified candidate:

1. Implement it as an **optional/selectable experiment or narrowly scoped path**, preserving the current DirectCPU/Homuler path as fallback/comparison until USER acceptance.
2. Keep latest-frame-wins semantics and bounded ownership.
3. Reuse persistent buffers and avoid per-frame GC allocations.
4. Preserve the accepted OpenVINO mailbox/worker path downstream.
5. Make backend/readback identity visible in F7 diagnostics so A/B evidence cannot silently compare different paths.
6. Add only the telemetry needed to compare:
   - camera/frame acquisition;
   - body prep/downscale;
   - transfer/copy/readback;
   - publish-to-worker;
   - OpenVINO graph/inference;
   - frame->result.
7. Preserve explicit fallback/failure reasons; do not silently claim a new path is active when it fell back.

## Performance objective

Do **not** promise a specific final number before hardware measurement.

A strong candidate should materially improve one or more of:

- readback/pre-inference latency;
- frame freshness/pose age;
- frame->result latency;
- useful fresh pose result rate;
- fast-motion responsiveness;

without degrading correctness or Unity stability.

As a directional target only, cutting the current ~43-55 ms readback stage toward ~15-20 ms would be highly valuable, but this is not a hard pass threshold.

## Development/checkpoint flow

Checkpoints are for recovery, not bureaucracy.

Work continuously through:

1. **R1 — architecture audit / evidence**
   - determine which alternatives are real and which merely move the same readback cost;
   - update progress file and commit when coherent;
   - continue immediately if no USER input needed.

2. **R2 — candidate implementation**
   - implement the best justified path, preserving fallback;
   - keep ownership bounded and buffers reusable;
   - update progress + checkpoint commit.

3. **R3 — verification/telemetry**
   - compile/static/helper tests as appropriate;
   - verify fallback/default paths remain;
   - no unnecessary full native rebuild if native inputs did not change;
   - update progress + checkpoint.

4. **R4 — USER runtime A/B preparation**
   - prepare concise same-machine test comparing current accepted path vs new readback path, preferably with OpenVINO kept constant;
   - stop here when physical webcam/runtime evidence is required.

If execution budget becomes low, checkpoint first and write an exact CONTINUE FROM HERE section into the progress file.

## USER A/B expectation

The eventual USER test should isolate the readback change as much as possible.

Prefer:

- same OpenVINO backend for both runs;
- same camera/device, lighting/framing, 320x240 target and downstream settings;
- old accepted DirectCPU path vs new candidate path;
- no OBS during numerical comparison unless visual footage is specifically needed.

Capture at least:

- camera FPS;
- render FPS;
- readback/acquisition path identity;
- acquisition/readback/copy/downscale timing;
- requests/s and results/s;
- pose age;
- frame->result;
- OpenVINO graph/landmark timing;
- scheduling mailbox bounds;
- fast-arm/F12 responsiveness;
- partial-body recovery;
- camera-switch/restart/teardown stability.

Only USER/Orchestrator may classify the result.

## Git authorization

You are authorized to make focused commits and push them to `engine/pose-tracking-spike` for this readback optimization and its recovery checkpoints.

You are **not** authorized to:

- merge to `main`;
- force push;
- rebase/reset shared history;
- merge/open a PR unless later requested;
- discard USER-local changes.

## Final/stop report format

When you genuinely stop, return one detailed report containing:

- exact starting remote SHA;
- exact final remote SHA;
- checkpoints/substeps completed;
- files changed/added;
- architecture options investigated and evidence for rejecting/choosing each;
- implementation summary;
- bounded ownership/buffer behavior;
- fallback behavior;
- compile/build/static/test results;
- telemetry added;
- native/package implications;
- generated/local artifacts the USER must preserve or regenerate;
- unresolved risks/blockers;
- exact USER QA procedure or exact CONTINUE FROM HERE action;
- confirmation that `main` was not merged and Phase 6 was not started.
