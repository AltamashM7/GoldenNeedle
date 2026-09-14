# Web Builder Handoff — R2 CPU Webcam Acquisition Experiment

Role: intelligent Web Builder

Repository: `AltamashM7/GoldenNeedle`

Branch: `engine/pose-tracking-spike`

This brief supersedes the old broad readback handoff for the implementation stage. R1 architecture audit is complete. The USER/Orchestrator has now explicitly authorized R2 implementation of the bounded `WebCamTexture.GetPixels32` CPU-acquisition experiment described in `Docs/openvino-unity-readback-audit.md`.

## Starting-state procedure

Before editing anything:

1. Verify the current remote `engine/pose-tracking-spike` HEAD.
2. Inspect local Git status if a checkout is available; preserve USER-local dirty/generated files.
3. Read, in this order:
   - `AGENTS.md`
   - `Docs/openvino-unity-readback-optimization-progress.md`
   - `Docs/openvino-unity-readback-audit.md`
   - `Docs/openvino-unity-scheduling-optimization-progress.md`
   - current `MediaPipePoseProvider.cs`
   - current custom Inspector/debug UI that exposes readback/inference settings and F7 telemetry.
4. Treat `Docs/openvino-unity-readback-optimization-progress.md` and this R2 handoff as newer authority than the older broad `openvino-unity-readback-optimization-handoff.md`.
5. Do not continue code from the abandoned transient implementation commits that were later reverted. Begin from the active branch tree only.

## Authorization / scope

R2 implementation is **AUTHORIZED**.

The authorized implementation is narrowly scoped to a selectable CPU-camera acquisition experiment:

```text
Existing baseline
WebCamTexture
  -> 320x240 RenderTexture
  -> AsyncGPUReadback / DirectCPU
  -> existing OpenVINO mailbox/worker

R2 candidate
WebCamTexture
  -> GetPixels32(reused camera-sized Color32[])
  -> reusable CPU 640x480 -> 320x240 resize + current inference H/V transform
  -> existing OpenVINO mailbox/worker
```

Do not turn R2 into a custom camera-stack rewrite. Native Windows/Media Foundation capture remains deferred unless the USER/Orchestrator later authorizes a separate investigation after this experiment is measured.

## Accepted reason for this experiment

Current accepted DirectCPU measurements are approximately:

```text
submit -> callback  ~42.7 ms median
                    ~54.8 ms p95
callback -> poll    ~0.8 ms median
poll -> publish     ~0.2 ms median
```

The accepted OpenVINO scheduling work already removed the downstream render-frame continuation delay. OpenVINO now feels materially more responsive to the USER, but total frame->result remains around ~100-105 ms because pre-inference frame acquisition/readback dominates.

R1 found `GetPixels32(Color32[] reusableBuffer)` to be the lowest-risk reuse-first candidate that might bypass the RenderTexture -> AsyncGPUReadback round trip. Its actual cost on the USER's Windows webcam/driver is unknown and must be measured rather than assumed.

## Non-negotiable invariants

- Keep all work on `engine/pose-tracking-spike`.
- Do not merge to `main`.
- No force push, rebase/reset shared history, or cleanup of USER-local changes.
- Keep stock MediaPipe/TFLite CPU functional.
- Keep OpenVINO CPU FP32 functional and preserve the accepted persistent worker/latest-frame mailbox.
- Preserve max one active inference and max one replaceable newest pending frame; no FIFO/history/replay/catch-up queue.
- Preserve existing DirectCPU/Homuler paths as baseline/fallback until USER acceptance.
- Keep body target at 320x240 for the first A/B.
- Keep full-resolution `WebCamTexture` presentation behavior unchanged.
- Preserve `InferenceFlipHorizontally`, `InferenceFlipVertically`, `InferenceRotationDegrees`, display mirror and canonical left/right semantics exactly.
- Preserve camera selection/switch, retry/restart and teardown safety.
- Preserve canonical mapping, confidence, stabilization, calibration, retargeting, locomotion, partial-body behavior and F12 presentation semantics.
- No detector conversion/densification.
- No native OpenVINO model/ABI changes and no full native OpenVINO rebuild unless a real native dependency is independently changed.
- Do not edit USER-owned `PoseTrackingSpike.unity` YAML merely to turn on the experiment.
- Phase 5A remains not USER accepted. Phase 6 remains not started.

## Implementation requirements

### 1. Selectable acquisition mode

Add the R2 path as an explicit selectable experiment rather than replacing DirectCPU silently.

The Inspector and F7/runtime telemetry must make the active mode unambiguous. Suggested conceptual identities are:

- existing GPU/DirectCPU path;
- experimental WebCam CPU path.

The exact enum/property names may follow the existing code style.

If the experimental path cannot run for the current session, expose the fallback/failure reason. Do not report the experimental path as active while silently using DirectCPU/Homuler.

### 2. Persistent CPU buffers

Use reusable storage only.

Expected resources are approximately:

- one camera-sized `Color32[]` reused by `WebCamTexture.GetPixels32`;
- reusable 320x240 RGBA storage sufficient to publish into the existing bounded OpenVINO mailbox;
- no per-frame `new Color32[]`, `byte[]`, LINQ buffers, queues or history structures.

If existing mailbox publication necessarily copies into its own two reusable slots, do not add another queue merely to avoid that bounded copy.

### 3. CPU resize / transform

For the first experiment, prioritize semantic comparability over clever image-quality shortcuts.

The CPU preparation must:

- map the camera-sized frame into the existing 320x240 body target;
- preserve the current inference horizontal/vertical flip semantics;
- keep rotation handling consistent with the existing OpenVINO input contract unless source inspection proves physical rotation is required;
- avoid changing display mirror/canonical semantics;
- be deterministic and covered by pure/helper tests where practical.

Because the common baseline is 640x480 -> 320x240, a 2:1 optimized path is reasonable, but do not hardcode an implementation that catastrophically breaks when the physical camera negotiates a different valid size. Provide a correct general fallback or bounded general mapping.

### 4. Fresh-frame / bounded scheduling behavior

The CPU path must use the same latest-frame philosophy as the accepted engine:

- only act on fresh camera frames;
- no capture backlog;
- no replay/catch-up;
- if a new CPU-prepared frame arrives while one pending OpenVINO frame exists, newest useful frame wins under the existing mailbox policy;
- never run two OpenVINO inferences concurrently.

Do not redesign the accepted OpenVINO mailbox/worker merely because the input source changed.

### 5. Session fail-closed behavior

The experimental CPU acquisition path should be easy to disable for the current camera session if it fails or proves unsupported.

Failure must not corrupt camera/display state. Preserve a clean explicit fallback/comparison route to the existing path and expose the reason in status/telemetry.

Do not swallow repeated exceptions every frame.

## Required telemetry

Add only enough telemetry to answer the R2 question without materially perturbing performance.

For the CPU path, separately expose at least:

- `GetPixels32` acquisition time;
- CPU resize/flip preparation time;
- total camera -> prepared CPU frame time;
- active acquisition/readback mode;
- fallback reason when applicable.

Reuse existing telemetry for:

- camera/render FPS;
- requests/s and results/s;
- pose age;
- prepared->worker launch;
- OpenVINO graph/detector/landmark/bridge time;
- frame->result;
- OpenVINO mailbox pending/active/buffer bounds.

Do not remove the existing DirectCPU timing telemetry; it is the A/B baseline.

## Verification before USER QA

Perform proportionate verification and continue automatically through recovery checkpoints when feasible.

At minimum:

1. Ensure managed code compiles / static checks pass.
2. Add deterministic tests for CPU pixel mapping/resize/flip/orientation helper logic if the implementation introduces pure helpers.
3. Verify no per-frame unbounded allocation/queue was introduced by source inspection/tests.
4. Verify existing DirectCPU path remains selectable and unchanged in behavior.
5. Verify stock TFLite/OpenVINO backend selection remains intact.
6. Verify camera switch/restart/cleanup code correctly clears/reuses CPU buffers and does not retain stale frame ownership.
7. Verify F7/Inspector accurately identify active acquisition mode and fallback reason.
8. Do not trigger a full native OpenVINO rebuild just for managed R2 work.

If automated Unity compilation is unavailable, prepare the code to the USER runtime boundary and report that compilation must be confirmed locally. Do not claim USER/hardware QA passed.

## Checkpoint / execution-limit workflow

Checkpoints are recovery markers, not approval gates.

Work continuously through:

- **R2-A** — source/ownership plan confirmed against current active code;
- **R2-B** — selectable CPU acquisition + reusable preparation implemented;
- **R2-C** — telemetry/fallback/lifecycle verification completed;
- **R2-D** — USER A/B instructions prepared.

At each coherent recovery point:

- commit/push to `engine/pose-tracking-spike`;
- update `Docs/openvino-unity-readback-optimization-progress.md` with exact ending SHA, work completed, verification, risks and next action;
- then continue immediately if no USER input is genuinely required.

If execution budget becomes unsafe, checkpoint first and write an exact `CONTINUE FROM HERE` section. A replacement Builder must resume from the active branch and rolling progress file, not from abandoned transient history.

## USER runtime A/B boundary

Stop when physical webcam evidence is genuinely required.

Prepare a concise comparison that keeps OpenVINO constant and changes only acquisition mode:

```text
Run A: current accepted DirectCPU / AsyncGPUReadback path
Run B: experimental WebCam GetPixels32 CPU path
```

Keep identical:

- physical camera;
- 320x240 body target;
- OpenVINO backend;
- target inference FPS;
- Direct downstream scheduling/settings;
- lighting/framing/calibration/presentation settings.

Prefer no OBS for numerical comparison.

The USER should capture F7 evidence and report fast-arm/F12 responsiveness, partial-body recovery, camera-switch/restart behavior and Play Mode teardown.

The R2 experiment is promising only if CPU acquisition + preparation materially improves the pre-inference stage and/or frame freshness/end-to-end responsiveness without unacceptable render FPS stalls or semantic regressions.

If `GetPixels32 + CPU preparation` remains around the old ~40-55 ms cost or causes unacceptable main-thread/render stalls, do not keep optimizing it indefinitely. Report the evidence and stop for an architecture decision; native Windows/Media Foundation capture can then be considered separately.

## Final stop report

Return:

- exact starting remote SHA;
- exact final remote SHA;
- checkpoint(s) completed;
- files changed/added;
- implementation design;
- buffer/ownership boundedness;
- explicit fallback behavior;
- compile/test/static results;
- telemetry added;
- any generated/local artifacts;
- unresolved risks;
- exact USER QA procedure or exact `CONTINUE FROM HERE` action;
- confirmation that `main` was not merged and Phase 6 was not started.
