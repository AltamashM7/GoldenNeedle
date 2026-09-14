# Golden Needle — Camera/Readback Latency Optimization Progress

This is the rolling resume point for the next approved Golden Needle performance optimization after the OpenVINO scheduling optimization.

A replacement Web Builder should read this file first, verify current remote `engine/pose-tracking-spike` HEAD, inspect the actual provider/runtime code, and continue from the first unfinished action. Do not restart completed OpenVINO work.

## Current authorization and status

- USER authorization: **APPROVED**.
- Branch: `engine/pose-tracking-spike`.
- Gate A OpenVINO model compatibility: **PASS**.
- Gate B MediaPipe/OpenVINO semantic parity: **PASS WITH NOTES**.
- Unity OpenVINO integration correctness: **PASS**.
- OpenVINO scheduling optimization: **USER ACCEPTED — PASS, scheduling gain accepted**.
- Stock MediaPipe/TFLite CPU remains available and default-safe; do not remove it.
- Phase 5A remains **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## Accepted scheduling evidence entering this optimization

The prior OpenVINO scheduling issue was approximately:

```text
Prep->launch ~19.9 ms
frame delta = 1
origin = Update
immediate/fast launches ~7.9/s
```

After the bounded persistent-worker/latest-frame mailbox optimization, USER runtime evidence showed approximately:

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

The USER reported OpenVINO now feels substantially better and more responsive in F12/avatar control.

This is enough to accept the scheduling optimization. Do not reopen it absent new evidence.

## New bottleneck evidence

The current DirectCPU readback telemetry is now the dominant visible upstream latency component. Recent USER evidence showed approximately:

```text
DirectCPU submit->callback median ~42.7 ms
DirectCPU submit->callback p95 ~54.8 ms
callback->poll ~0.8 ms median / ~1.3 ms p95
poll->publish ~0.2 ms median / ~0.4 ms p95
```

Interpretation:

- callback polling overhead is already small;
- publish/continuation overhead is already small;
- the expensive interval is overwhelmingly GPU readback submission -> GPU callback;
- further optimization of callback polling or OpenVINO worker continuation is not the priority;
- the next target is the camera/body-pose input path before inference, especially the GPU->CPU transfer/readback architecture.

Current practical end-to-end observations are still roughly:

- camera: ~28-30 FPS;
- fresh pose results: ~12-13/s;
- body input: 320x240;
- readback: commonly ~43-55+ ms;
- OpenVINO graph/inference side: materially faster than stock TFLite, but total frame->result remains around ~100 ms because upstream frame acquisition/readback dominates.

## Goal

Determine and implement, if justified by evidence, the lowest-risk architecture that materially reduces the camera/body-pose input latency before OpenVINO while preserving Golden Needle's accepted motion semantics and low-end CPU-first product constraint.

The practical question is:

> Can we substantially reduce the current ~43-55 ms GPU readback stage, ideally by avoiding or restructuring the GPU round-trip, without breaking webcam compatibility, orientation/mirroring semantics, 320x240 pose input, partial-body behavior, frame freshness policy, Unity stability, or the stock fallback path?

## Architectural invariants

- Keep all work on `engine/pose-tracking-spike`.
- Do not merge to `main`.
- Keep stock MediaPipe/TFLite CPU fully functional.
- Keep OpenVINO CPU FP32 selectable and preserve the accepted persistent-worker/latest-frame mailbox scheduling.
- Preserve one active readback max, one active inference max, one replaceable latest pending frame max; no backlog/history/replay/catch-up queue.
- Preserve canonical mapping, stabilization, confidence, calibration, retargeting, locomotion, F12 presentation behavior and partial-body semantics.
- Keep full-resolution camera/display behavior unchanged unless a measured implementation explicitly proves an equivalent presentation path.
- Keep initial body inference target at 320x240 for fair A/B comparison.
- Preserve orientation, rotation and H/V flip semantics exactly.
- Preserve camera-switch/restart/teardown safety.
- Do not force D3D12 globally.
- Do not densify/convert detector models.
- Do not refactor fixed 33-landmark provider or 20-joint canonical structures during this performance spike.
- Do not touch USER-owned scene YAML merely for convenience.
- Do not casually clean/revert local dirty files.

## Required investigation order

The next Web Builder should audit before implementing.

At minimum inspect:

1. Current `MediaPipePoseProvider.cs` readback path and accepted DirectCPU implementation.
2. `TextureFrame`, `TextureFramePool`, `ReadTextureAsync`, Homuler's image-source/readback helpers, and any native image/frame APIs available in MediaPipeUnityPlugin 0.16.3.
3. Unity `WebCamTexture` data-access options actually available in Unity 6.5, including whether CPU pixel access is synchronous/copy-heavy and what thread restrictions exist.
4. The current 640x480 camera -> 320x240 body RenderTexture/blit -> `AsyncGPUReadback.RequestIntoNativeArray` flow.
5. Whether avoiding GPU readback is realistically possible while preserving webcam/display behavior, for example through:
   - direct CPU camera pixels if Unity exposes them efficiently;
   - one CPU-owned webcam/frame buffer reused across frames;
   - CPU downscale/conversion measured against GPU readback cost;
   - native Windows camera capture only if there is a mature reusable path and it does not duplicate large camera/orientation semantics unnecessarily;
   - any lower-latency Unity/graphics transfer API supported on this hardware.
6. Whether the existing ~43-55 ms submit->callback is actual GPU transfer latency, render/GPU synchronization, coroutine observation artifact, or a combination. Instrument only if the current telemetry cannot distinguish this.

## Reuse-first rule

Do not immediately write a custom Windows camera stack.

Prefer mature existing Unity/Homuler/MediaPipe mechanisms if they can expose a lower-latency CPU frame path. A native capture path is justified only if evidence shows Unity's current texture/readback architecture is the unavoidable bottleneck and a mature alternative can be integrated without destabilizing camera/orientation/device handling.

## Architecture audit checkpoint — COMPLETE

Audit performed against the current branch implementation plus Unity 6.x and Homuler 0.16.3 APIs.

### Existing Golden Needle path

The active DirectCPU path is:

```text
WebCamTexture
  -> persistent body RenderTexture (320x240 in the current configuration)
  -> persistent orientation staging RenderTexture when H/V flip is required
  -> AsyncGPUReadback.RequestIntoNativeArray
  -> pooled TextureFrame RGBA32 CPU storage
```

The USER's last run reported `DirectCPU stage=V`, so that run used both the body downscale blit and the vertical-flip staging blit before readback.

The existing high-resolution telemetry is sufficient to localize the dominant delay: submit->callback is about 42.7 ms median while callback->coroutine observation is sub-millisecond. Therefore a callback-only/coroutine scheduling rewrite is not justified.

### Homuler 0.16.3 audit

`TextureFrame.ReadTextureAsync` does not provide a hidden lower-latency webcam CPU path. Its no-flip path uses `AsyncGPUReadback.RequestIntoNativeArray`; with flips it additionally allocates a temporary RenderTexture and blits before the same async readback. After completion it calls `LoadRawTextureData` and `Apply` on the pooled Texture2D.

Golden Needle's accepted DirectCPU path is already leaner than that because it writes the readback directly into the pooled TextureFrame raw storage and avoids Homuler's post-readback `LoadRawTextureData`/`Apply` copy/upload.

Homuler's `WebCamSource` remains a `WebCamTexture` wrapper and does not expose a separate native/CPU capture buffer that Golden Needle can reuse.

### Unity 6.x audit

Unity's `WebCamTexture` exposes `GetPixels32(Color32[])`. Supplying a correctly sized caller-owned array allows reuse rather than allocating a new array every frame. Pixel rows use normal Unity texture-coordinate ordering (left-to-right, bottom-to-top).

Unity's `AsyncGPUReadback` documentation explicitly describes the API as avoiding CPU/GPU stalls at the cost of a few frames of latency. That behavior is consistent with the measured ~42.7 ms submit->callback interval and supports treating the current delay as an architectural async GPU transfer/frame-availability cost rather than coroutine polling overhead.

No `WebCamTexture.GetPixelData<T>`-style direct NativeArray API was found in the audited Unity 6.x webcam surface.

### Selected implementation candidate

Implement a bounded **WebCam CPU frame path** before the existing GPU readback path:

```text
WebCamTexture.GetPixels32(reused Color32[])
  -> CPU resize + exact H/V transform into pooled TextureFrame RGBA32 raw NativeArray
  -> existing TFLite BuildCPUImage OR existing OpenVINO latest-frame mailbox
```

Properties of the candidate:

- keeps the existing `WebCamTexture`; this is not a custom camera stack;
- leaves full-resolution camera/display presentation untouched;
- uses one reusable source `Color32[]`; resize only occurs when camera dimensions change;
- writes directly into the existing pooled TextureFrame CPU buffer; no `Texture2D.Apply` is needed for the inference consumers because both stock `BuildCPUImage` and OpenVINO consume the TextureFrame raw CPU bytes;
- preserves inference rotation as the existing MediaPipe/OpenVINO rotation option and applies only the same H/V transform currently performed by the GPU staging blit;
- retains the existing DirectCPU AsyncGPUReadback path as automatic fallback/comparison;
- retains Homuler CPUAsync as the lower fallback;
- does not change OpenVINO worker/mailbox scheduling or add any queue/readback/inference concurrency;
- records CPU acquisition and resize/orientation time separately so USER QA can determine whether synchronous webcam CPU access is actually a net win on the target laptop.

CPU resize must be bilinear and use destination pixel centers mapped to source texture coordinates. H/V mapping must match the existing GPU scale/offset convention for None/H/V/HV. A same-size/no-flip fast copy is allowed.

If `GetPixels32` or CPU preparation fails on a camera/driver, disable only this candidate for the current provider session and immediately retain the proven DirectCPU GPU path; do not fail the provider or silently alter orientation semantics.

## Development flow

Checkpoints are recovery markers, not stop-and-wait gates.

The Web Builder should work continuously through:

1. architecture/readback audit;
2. targeted measurement if necessary;
3. implementation of the best justified candidate;
4. boundedness/ownership/lifecycle verification;
5. telemetry updates needed for fair A/B;
6. USER runtime-QA preparation.

At coherent recovery points, commit/push and update this file. Stop only for a genuine blocker, USER hardware/runtime QA, or execution-limit risk.

## Success criteria for implementation stage

Do not set an arbitrary final latency threshold before measurement. A candidate is worth USER QA if it:

- preserves correctness and all scheduling/lifecycle invariants;
- measurably reduces the pre-inference camera/readback stage or removes the expensive GPU round-trip;
- does not introduce per-frame unbounded allocation;
- does not create a second frame queue/backlog;
- keeps frame freshness/latest-frame-wins semantics;
- retains stock fallback;
- provides enough diagnostics to distinguish capture/prep/readback/copy/inference latency.

The USER/orchestrator owns final runtime acceptance.

## Exact next action

Architecture audit is complete. Continue directly with implementation:

1. Add an isolated/testable CPU RGBA resize + H/V transform helper.
2. Add the WebCam CPU candidate to the provider without changing the accepted readback/inference scheduling gates.
3. Retain DirectCPU AsyncGPUReadback and Homuler fallback paths.
4. Add path/acquire/resize telemetry needed for USER A/B.
5. Run managed/static verification and update this document with exact commit/result evidence.
6. Stop only when USER runtime A/B is genuinely required.
7. Do not start Phase 6 or merge to `main`.
