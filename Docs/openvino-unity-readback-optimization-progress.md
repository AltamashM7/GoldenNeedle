# Golden Needle — Camera/Readback Latency Optimization Progress

This file records the resolved state of the camera/readback-latency optimization that followed the accepted OpenVINO scheduling work.

Current status refresh: 2026-09-14.

## Resolution

- USER readback investigation authorization: **APPROVED**.
- R1 architecture audit: **COMPLETE — READ-ONLY**.
- R2 WebCamCPU/GetPixels32 experiment: **EXPLICITLY USER AUTHORIZED**.
- R2 implementation/static/helper verification: **PASS**.
- USER runtime validation: **PASS**.
- Final classification: **USER ACCEPTED — PASS FOR CURRENT MILESTONE**.
- OpenVINO scheduling optimization: **USER ACCEPTED — PASS**.
- Stock MediaPipe/TFLite CPU and ExistingReadback remain available as fallback/reference.
- Phase 5A remains **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.
- No merge to `main` without explicit USER approval.

Older wording in this track that said USER webcam QA was still pending is superseded by this resolved status.

## Why the experiment existed

After OpenVINO scheduling was fixed, the dominant remaining pre-inference cost was the GPU/readback path.

Representative accepted bottleneck evidence before R2:

```text
camera               ~28-30 FPS
fresh pose results   ~12-13/s
frame->result        ~100-105 ms

DirectCPU readback:
submit->callback     ~42.7 ms median / ~54.8 ms p95
callback->poll       ~0.8 ms median / ~1.3 ms p95
poll->publish        ~0.2 ms median / ~0.4 ms p95
```

The conclusion was that callback polling/publication were already small. The major avoidable cost was waiting for GPU readback completion.

## R1 audit conclusion

The read-only audit rejected/deferred the following as primary next steps:

- more coroutine/callback micro-optimization;
- standard Homuler CPUAsync, because it still uses AsyncGPUReadback;
- forcing D3D12;
- synchronous texture read without hardware measurement;
- custom Windows/Media Foundation capture before measuring a lower-risk Unity CPU-pixel path.

The selected candidate was:

`WebCamTexture.GetPixels32(Color32[] reusableBuffer)` plus reusable CPU resize/flip preparation.

## Implemented R2 architecture

```text
WebCamTexture
  -> GetPixels32(reused camera-sized Color32[])
  -> reusable CPU resize / H-V transform
  -> persistent 320x240 RGBA NativeArray<byte>
  -> existing OpenVinoLatestFrameMailbox
  -> existing persistent OpenVINO worker
```

The implementation preserves two acquisition modes:

### `ExistingReadback`

- existing RenderTexture/readback path;
- DirectCPU/Homuler behavior retained;
- fallback/reference path.

### `WebCamCpuPixels`

- explicit/selectable;
- active only for OpenVINO CPU FP32;
- uses the reusable `GetPixels32(Color32[])` overload;
- persistent/reused camera and body RGBA buffers;
- 2:1 box-average fast path for 640x480 -> 320x240;
- general bilinear fallback for other valid dimensions;
- applies the existing inference H/V semantics;
- publishes directly to the accepted OpenVINO mailbox;
- adds no queue/history/replay/second worker;
- exposes Get/Prep/Total CPU timing and fallback reason.

Implementation checkpoint:

`2343e629ab00ff16b8a52b844697f11911ca6b82` — `perf: add selectable webcam CPU acquisition`.

A later telemetry string-literal compile defect was repaired at:

`16831bc5a445c376704dd868240391e8a118394b` — `fix: repair R2 telemetry string literal`.

The one-off repair workflow was then removed; the safe post-repair checkpoint was:

`45e20cb7df3ea8acd4c6e2cdbce8450a87487971`.

## Runtime findings

Early half-body tests already showed WebCamCPU acquisition around:

```text
GetPixels32      ~0.3 ms
CPU preparation  ~3.6 ms
CPU total        ~3.9 ms
```

The USER also reported lower perceived latency than ExistingReadback.

A later full-body phone-recorded test, avoiding OBS overhead, provided the strongest evidence. Representative behavior was approximately:

```text
camera capture          ~28.6-30.3 FPS
fresh pose results      ~26.7-29.3/s
render                  ~30-37 FPS
CPU GetPixels32         ~0.3 ms
CPU preparation         ~3.6 ms
CPU acquisition total   ~3.9 ms
OpenVINO graph/inference commonly ~25-35 ms
frame->result           commonly ~33-62 ms
```

The camera returned to roughly 30 FPS when full body was visible. The earlier ~15 FPS half-body behavior was not caused by a Golden Needle capture-rate cap; it appears to be camera/environment behavior and is not a blocker for the accepted full-body path.

## Interpretation

The R2 candidate removed the old dominant GPU/readback latency on the USER machine and exposed close to one fresh pose result per camera frame under healthy ~30 FPS full-body conditions.

Compared with the earlier representative ~12-13 results/s and ~100 ms frame-to-result behavior:

- practical fresh-pose throughput roughly doubled;
- frame-to-result latency was roughly halved in representative samples;
- the old readback stage became effectively bypassed for the best-tested path;
- OpenVINO + WebCamCPU now operates near the 30 Hz camera ceiling on the proof machine.

Final status: **USER ACCEPTED — PASS FOR CURRENT MILESTONE**.

## Preserved invariants

- ExistingReadback remains available as fallback/reference.
- Stock MediaPipe/TFLite remains available.
- OpenVINO worker/mailbox scheduling remains unchanged and accepted.
- At most one active inference + one replaceable newest pending frame.
- No FIFO/history/replay/catch-up queue.
- Persistent/reusable buffers only.
- Body input remains 320x240 for the current baseline.
- Camera rotation/H-V/display-mirror/canonical left-right semantics remain unchanged.
- No native OpenVINO rebuild was required.
- No custom Windows camera stack was introduced.
- No calibration, retargeting, locomotion or partial-body semantics were changed.

## What remains optional later

Future hardware-specific work may still evaluate:

- a custom/native camera stack if Unity CPU access regresses on another machine;
- additional camera formats/resolutions;
- stronger/discrete-GPU backend policy;
- minor acquisition/inference refinements.

None of these is a current blocker.

## CONTINUE FROM HERE

**STATUS: CLOSED FOR CURRENT MILESTONE — USER ACCEPTED PASS.**

Do not reopen camera/readback optimization merely to chase small additional numbers while the current best path already approaches camera cadence.

Current project direction is governed by `Docs/current-state.md`.
