# Golden Needle — OpenVINO Unity Integration Progress

This file records the current resolution of the OpenVINO Unity integration track. Detailed build/recovery history remains in Git history, `Docs/openvino-unity-integration-checkpoints.md`, the worker handoff, and the dedicated scheduling/readback progress files.

Current status refresh: 2026-09-14.

## Current status

- Branch: `engine/pose-tracking-spike`.
- Integration authorization: **APPROVED BY USER**.
- Gate A: **PASS**.
- Gate B: **PASS WITH NOTES**.
- U0 planning/recovery scaffold: **COMPLETE**.
- U1 architecture/native lifecycle skeleton: **COMPLETE**.
- U2 native OpenVINO backend: **COMPLETE**.
- U3 managed Unity selection/telemetry: **COMPLETE**.
- U4 USER live Unity correctness/A-B validation: **PASS**.
- Post-U4 OpenVINO scheduling optimization: **USER ACCEPTED — PASS**.
- Post-U4 WebCamCPU/GetPixels32 acquisition optimization: **USER ACCEPTED — PASS FOR CURRENT MILESTONE**.
- U5 production/default cleanup decision: **DEFERRED**; no serialized/default backend change is implied.
- Motion-engine latency/performance optimization milestone: **CURRENT MILESTONE COMPLETE; FURTHER TUNING OPTIONAL**.
- Phase 5A: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## Proven evidence entering Unity integration

Gate B completed the full 363-frame recorded sequence:

```text
TASKS_REFERENCE mean            ~28.739 ms / 34.796/s offline capacity
GRAPH_TFLITE_CPU mean           ~28.094 ms / 35.595/s
GRAPH_OPENVINO_CPU_FP32 mean    ~14.032 ms / 71.265/s
OpenVINO pose-presence agreement 362/363 (~99.7245%)
normalized XYZ RMSE             ~0.01113
world 3D RMSE                   ~0.02188 m
OpenVINO bridge-copy mean       ~0.2025 ms
```

These are offline VIDEO-mode graph-processing measurements, not Unity LIVE_STREAM end-to-end promises.

## Native integration that is now complete

The additive Windows native integration uses:

- Homuler MediaPipeUnityPlugin `0.16.3`;
- MediaPipe `0.10.22`;
- OpenVINO `2026.3.0`;
- exact production detector and landmark TFLite models;
- explicit CPU FP32 execution;
- MediaPipe graph/calculator semantics for preprocessing, detector decode/NMS/ROI, tracking, landmark decode/refinement, visibility/presence, world-landmark processing and projection.

The OpenVINO backend replaces neural inference execution only. It does **not** manually recreate MediaPipe pose semantics in C#.

The native plugin remains additive and does not replace stock `mediapipe_c.dll`.

Proven native/package evidence includes:

- native build workflow run `34805228184`;
- preserved artifact `golden-needle-u2-native`;
- lifecycle `GNOVPOSE_LOAD_VERSION_SELFTEST_UNLOAD=PASS`;
- semantic smoke `GNOVPOSE_U2_REAL_FRAME_33_NORMALIZED_WORLD=PASS`;
- plugin identity `OPENVINO_CPU_FP32` / ABI v1.1;
- exact detector/landmark identity checks;
- CPU-only OpenVINO runtime packaging;
- stock MediaPipe/TFLite plugin files preserved.

## Managed Unity integration that is now complete

`MediaPipePoseProvider` exposes selectable inference backends:

```text
MediaPipeTfliteCpu
OpenVinoCpuFp32
```

The managed OpenVINO wrapper validates ABI/backend identity, timestamps and exact 33-landmark output. The provider reuses the existing webcam/orientation/body-input/provider observation/canonical pipeline and preserves one-inference/latest-frame semantics.

Explicit OpenVINO selection fails closed rather than silently switching to another neural backend.

Stock MediaPipe/TFLite remains available as the compatibility fallback/reference.

## U4 USER live Unity result

USER same-machine live testing established that OpenVINO is semantically usable in the Unity application and that the OpenVINO graph/inference portion is materially faster than the stock TFLite path.

Representative pre-scheduling-fix evidence:

```text
Stock TFLite CPU:
  camera               ~30.3 FPS
  results              ~13.7/s
  inference/result     ~44.4 ms
  frame->result        ~97.7 ms

OpenVINO CPU FP32:
  camera               ~29.5 FPS
  results              ~12.8/s
  graph/inference      ~31.9 ms
  frame->result        ~98.5 ms
  prepared->launch     ~19.9 ms symptom
```

The USER also reported that OpenVINO felt smoother in F12.

Interpretation: **OpenVINO compute advantage was real, but avoidable scheduling/readback latency initially hid the end-to-end gain.**

U4 correctness therefore passed, and the remaining work moved to focused scheduling and camera-acquisition optimization rather than reopening the native semantic integration.

## Accepted scheduling follow-up

The accepted OpenVINO worker/mailbox design uses:

- one persistent worker;
- one active inference maximum;
- one replaceable newest pending frame maximum;
- two reusable storage slots total;
- latest useful frame wins;
- no FIFO/history/replay/catch-up queue;
- `OVW` worker continuation when a pending frame can immediately follow a completed inference.

USER runtime evidence showed the former OpenVINO prepared-to-launch delay collapse to approximately 0 ms on the fast path and exposed more of the OpenVINO compute advantage.

Status: **USER ACCEPTED — PASS**.

Authoritative detail: `Docs/openvino-unity-scheduling-optimization-progress.md`.

## Accepted camera/readback follow-up

The best-tested acquisition path for OpenVINO is now:

```text
WebCamTexture
  -> GetPixels32(reused Color32[])
  -> reusable CPU resize / H-V transform to 320x240 RGBA
  -> existing OpenVINO latest-frame mailbox
  -> existing OpenVINO worker
```

This bypasses the old dominant AsyncGPUReadback completion delay without introducing a custom Windows capture stack.

Representative full-body USER phone-recorded evidence:

```text
camera capture          ~28.6-30.3 FPS
fresh pose results      ~26.7-29.3/s
CPU acquisition total   ~3.9 ms
OpenVINO graph/inference commonly ~25-35 ms
frame->result           commonly ~33-62 ms
```

Status: **USER ACCEPTED — PASS FOR CURRENT MILESTONE**.

Authoritative detail: `Docs/openvino-unity-readback-optimization-progress.md`.

## Current backend policy

The integration milestone is complete enough for normal development, but this does **not** mean every experimental selector should be collapsed into a single hardcoded production default now.

Current policy:

- preserve `OpenVinoCpuFp32` as the best-tested low-end backend path;
- preserve stock `MediaPipeTfliteCpu` as fallback/reference;
- preserve `WebCamCpuPixels` as the best-tested OpenVINO acquisition path;
- preserve `ExistingReadback` as fallback/reference;
- keep 320x240 body input for the current baseline;
- keep exact MediaPipe/canonical/retarget semantics unchanged;
- do not densify/convert detector models;
- do not force D3D12;
- do not remove the bounded latest-frame architecture;
- do not change serialized/default backend/acquisition policy without a separate explicit decision.

## What is no longer pending

Older versions of this file said U4 live Unity QA had not started. That wording is superseded.

The following are no longer blockers:

- native OpenVINO lifecycle/package proof;
- live OpenVINO correctness;
- OpenVINO worker scheduling;
- dominant camera/readback latency on the tested machine.

Further OpenVINO optimization is optional future work, not the current project blocker.

## CONTINUE FROM HERE

**STATUS: OPENVINO UNITY INTEGRATION + CURRENT PERFORMANCE FOLLOW-UPS COMPLETE FOR THIS MILESTONE.**

Do not reopen this track merely to chase small additional latency gains.

Next project planning authority is `Docs/current-state.md`. The USER intends to provide a pre-locomotion task list before Phase 5A work resumes.

No merge to `main`. Do not start Phase 6.
