# Golden Needle — Camera/Readback Latency Optimization Progress

This is the rolling resume point for the readback-latency optimization that follows the accepted OpenVINO scheduling optimization.

A replacement Web Builder must read this file first and treat it, plus `Docs/worker-briefs/openvino-unity-readback-r2-implementation-handoff.md`, as newer authority than the original broad readback handoff.

## Current authorization and status

- USER authorization for the readback investigation: **APPROVED**.
- R1 architecture audit: **COMPLETE — READ-ONLY**.
- R2 small selectable CPU-webcam implementation experiment: **EXPLICITLY USER AUTHORIZED on 2026-09-14**.
- R2-A ownership seam inspection: **COMPLETE**.
- R2-B selectable CPU webcam implementation: **COMPLETE**.
- R2-C telemetry/fallback/lifecycle/boundedness verification: **COMPLETE pending USER runtime QA**.
- R2-D managed/static/helper verification: **PASS; USER A/B QA is now the next genuine gate**.
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

The Orchestrator then added documentation-only decision checkpoints. No runtime/provider/native/scene implementation from the abandoned experiment was active when R2 began.

Authorized R2 implementation started from safe remote HEAD:

`f17577f10d4a2f78d46437705522dc271e6de18d`

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
- avoids per-frame camera-array allocation in Golden Needle code;
- bypasses the RenderTexture -> AsyncGPUReadback round trip when selected and eligible;
- requires no OpenVINO model/native ABI change;
- remains optional and can be abandoned cleanly if measurement is poor.

Key uncertainty remains runtime-specific:

`GetPixels32` may still synchronize/copy expensively on the USER's Windows webcam/driver. Same-machine runtime measurement is the purpose of the next QA gate.

## R2 implementation checkpoint

Implementation checkpoint SHA:

`2343e629ab00ff16b8a52b844697f11911ca6b82`

The implementation was generated and verified by the R2 workflow from the authorized safe tree. The workflow passed its transform, managed CPU-helper smoke, accepted OpenVINO scheduling smoke, managed ABI smoke, static/boundedness invariants and changed-file-scope checks before committing the provider/Inspector changes.

Files introduced for the authorized experiment and verification:

- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/WebCamCpuFramePreparation.cs`
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/WebCamCpuFramePreparation.cs.meta`
- `Assets/GoldenNeedle/Tests/Editor/WebCamCpuFramePreparationTests.cs`
- `Assets/GoldenNeedle/Tests/Editor/WebCamCpuFramePreparationTests.cs.meta`
- `Tools/OpenVinoUnityPosePlugin/tests/ManagedWebCamCpuSmoke/ManagedWebCamCpuSmoke.csproj`
- `Tools/OpenVinoUnityPosePlugin/tests/ManagedWebCamCpuSmoke/UnityStubs.cs`
- `Tools/OpenVinoUnityPosePlugin/tests/ManagedWebCamCpuSmoke/Program.cs`
- `Tools/OpenVinoUnityPosePlugin/scripts/apply_openvino_readback_r2.py`
- `.github/workflows/openvino-readback-r2.yml`

Runtime integration files changed at the implementation checkpoint:

- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipePoseProvider.cs`
- `Assets/GoldenNeedle/Editor/MediaPipePoseProviderEditor.cs`

No scene YAML, `ProjectSettings`, `Packages`, native OpenVINO source/binary or Phase 6 files were changed.

### Implemented acquisition modes

`BodyFrameAcquisitionMode.ExistingReadback`

- remains the serialized default;
- preserves the accepted existing RenderTexture/readback path;
- preserves DirectCPU/Homuler behavior and settings as fallback/A-B baseline.

`BodyFrameAcquisitionMode.WebCamCpuPixels`

- is explicit/selectable;
- is eligible only with active OpenVINO CPU FP32;
- calls `WebCamTexture.GetPixels32(reused Color32[])` on the main thread;
- prepares a persistent RGBA `NativeArray<byte>` at the existing body target size;
- uses a dedicated 2:1 box-average fast path for the expected 640x480 -> 320x240 case and a general bilinear path for other valid source/target sizes;
- applies the provider's existing inference horizontal/vertical flip semantics during CPU preparation;
- passes the prepared RGBA bytes into the existing `OpenVinoLatestFrameMailbox` and existing persistent OpenVINO worker;
- does not add a queue, history, replay or second worker;
- exposes requested/active acquisition mode, last GetPixels32/preparation/total timing and explicit fallback reason in the custom Inspector.

### Fallback and lifecycle behavior

- If the CPU mode is selected while OpenVINO is not active, the provider explicitly reports that the experiment requires OpenVINO and continues through `ExistingReadback`.
- If CPU buffer setup, `GetPixels32`, preparation or mailbox publication throws, the CPU experiment is disabled for that provider session and `ExistingReadback` remains available.
- CPU camera and prepared-RGBA buffers are persistent/reused while dimensions are stable.
- CPU buffers are released on body-resource rebuild and provider cleanup.
- CPU experiment availability, fallback reason and timing window reset with provider session state, so camera switch/restart starts from a clean acquisition session.
- Existing camera-switch mailbox pause/discard and OpenVINO worker shutdown/join ordering remain unchanged.

### Verification completed

R2 workflow verification passed before `2343e629...` was pushed:

- fail-closed provider/Inspector transform: PASS;
- managed `WebCamCpuFramePreparation` smoke: PASS;
- existing `ManagedSchedulingSmoke`: PASS;
- existing `ManagedAbiSmoke`: PASS;
- R2 static integration invariants: PASS;
- boundedness checks: PASS;
- exactly one persistent OpenVINO worker remains: PASS;
- exactly two reusable mailbox frame slots remain: PASS;
- no FIFO/ConcurrentQueue introduced: PASS;
- reusable `GetPixels32(Color32[])` overload only: PASS;
- CPU preparation helper performs no per-frame buffer construction: PASS;
- generated runtime diff restricted to provider + custom Inspector: PASS.

A native OpenVINO rebuild was intentionally not performed because the native inputs/ABI did not change.

## R2 invariants

R2 implementation must continue to preserve:

- existing DirectCPU/Homuler path as explicit fallback and A/B baseline;
- existing OpenVINO worker/mailbox scheduling downstream;
- max one active inference and one replaceable newest pending frame; no FIFO/history/replay/catch-up queue;
- persistent/reusable buffers only;
- initial body input at 320x240;
- inference H/V flip, rotation, display-mirror and canonical left/right semantics;
- camera switching/restart/teardown safety;
- canonical, stabilization, calibration, retargeting, locomotion and partial-body semantics;
- no detector conversion/densification;
- no native OpenVINO rebuild unless native inputs genuinely change;
- visible active acquisition mode and explicit fallback reason;
- no USER-owned scene YAML edits merely for convenience.

## R2 required USER measurement

Compare current DirectCPU against the CPU-webcam candidate with the same camera, OpenVINO backend, 320x240 body target, lighting/framing and downstream settings.

Measure/observe:

- `GetPixels32` acquisition time;
- CPU resize/flip preparation time;
- total camera -> prepared CPU frame time;
- OpenVINO graph/inference time;
- frame -> result latency;
- fresh results/s and pose age;
- camera/render FPS;
- stable bounded behavior/no errors;
- F12 fast-arm responsiveness and partial-body recovery;
- orientation/left-right parity versus ExistingReadback.

## CONTINUE FROM HERE

**STOP REASON: genuine USER webcam QA is now required.**

Remote implementation checkpoint to validate:

`2343e629ab00ff16b8a52b844697f11911ca6b82`

Before QA, pull the latest `engine/pose-tracking-spike` branch and allow Unity to recompile. Do not edit the scene YAML just to set the experiment.

USER A/B procedure:

1. Open the existing Pose Tracking Spike Lab scene and select the object containing `MediaPipePoseProvider`.
2. Keep `Inference Backend = OpenVinoCpuFp32`, body inference downscale enabled, long edge `320`, and the same camera/settings used for the accepted scheduling QA.
3. Baseline run: set **Frame Acquisition = ExistingReadback**. Keep the existing Direct Body CPU Readback setting at the same value used in the accepted baseline (normally enabled for the current DirectCPU comparison). Run long enough for telemetry to stabilize and capture the usual F7/F12 evidence.
4. Candidate run: stop Play Mode, set **Frame Acquisition = WebCamCpuPixels**, then run under the same lighting/framing. The Inspector should show active acquisition `WebCamCPU/GetPixels32`; if it falls back, capture the exact **Acquisition Fallback** text.
5. For the candidate, record the Inspector `CPU Get/Prep/Total` values after warm-up plus the normal F7 metrics: camera FPS, fresh pose results/s, frame->result, graph/inference timing, pose age and any errors.
6. Run F12 fast-arm motion and partial-body loss/recovery once on each mode. Confirm webcam presentation, skeleton orientation, left/right semantics and recovery remain equivalent.
7. Send the baseline and candidate screenshots/metrics back to the Orchestrator/Builder. Do not accept R2 solely from managed/static tests; same-machine webcam latency is the experiment's decision evidence.

Next Builder action after USER evidence:

- independently compare DirectCPU/ExistingReadback vs WebCamCPU/GetPixels32;
- reject/revert the candidate if it regresses latency, FPS, semantics, stability or boundedness;
- if it clearly improves the dominant pre-inference latency without regressions, document the result and return to the Orchestrator for the next approval decision;
- do not start custom Windows capture or Phase 6 from this checkpoint.
