# Web Builder Handoff — OpenVINO Unity Scheduling Optimization

Role: intelligent Web Builder

Repository: `AltamashM7/GoldenNeedle`

Branch: `engine/pose-tracking-spike`

This handoff authorizes a focused continuation of the already-integrated OpenVINO Unity backend. The USER has approved optimizing the live scheduling/continuation path because OpenVINO is demonstrably faster on the inference side but the current Unity pipeline is losing much of that gain before the next inference starts.

Checkpoints are **recovery markers, not approval gates**. Work continuously until USER runtime QA is genuinely required, a real blocker needs a decision, or execution-limit risk makes it unsafe to continue. Keep checkpoint commits and the rolling progress file current so another Builder can resume without reconstructing the session.

## Starting procedure

Before editing:

1. Verify the current remote `engine/pose-tracking-spike` HEAD. Do not assume the SHA in this brief is still current.
2. Inspect local Git status if a checkout is available. Preserve USER-local dirty/generated files; do not clean/reset/revert them.
3. Read in this order:
   - `AGENTS.md`
   - `Docs/openvino-unity-scheduling-optimization-progress.md`
   - `Docs/openvino-unity-integration-progress.md`
   - `Docs/openvino-unity-ab-qa.md`
   - `Docs/openvino-unity-integration-checkpoints.md`
4. Inspect the actual current implementation, especially:
   - `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipePoseProvider.cs`
   - `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/OpenVinoPoseNative.cs`
   - existing scheduling/readback policy helpers and tests;
   - F7/main diagnostics code;
   - teardown/restart/camera-switch paths.
5. Treat this brief and `Docs/openvino-unity-scheduling-optimization-progress.md` as the newest authority for the post-U4 optimization work.

## Starting evidence / why this task exists

The live Unity integration is already functionally correct. Gate A and Gate B passed, native package/lifecycle/real-frame smoke passed, Unity managed integration compiled and ran, and USER A/B testing confirmed correct pose behavior for both backends.

Clean same-machine screenshots without OBS showed:

### Stock MediaPipe/TFLite CPU

- camera ~30.3 FPS;
- render ~36.5 FPS;
- requests/results ~13.7/13.7 per second;
- latest pose age ~49 ms;
- last inference/result ~52.8 ms;
- displayed pipeline approximately `47.1 / 0.0 / 44.4 / 97.7 ms` for readback/build/inference/frame-to-result;
- prepared-to-launch ~0.1 ms;
- frame delta 0;
- launch origin `RB`;
- immediate/fast launches ~13.7/s.

### OpenVINO CPU FP32

- camera ~29.5 FPS;
- render ~39.0 FPS;
- requests/results ~12.8/12.8 per second;
- latest pose age ~90 ms at the screenshot instant;
- last inference/result ~37.2 ms;
- displayed pipeline approximately `46.6 / 0.3 / 31.9 / 98.5 ms`;
- prepared-to-launch ~19.9 ms;
- frame delta 1;
- launch origin `Update`;
- immediate/fast launches ~7.9/s;
- OpenVINO internal timing at the screenshot instant: managed copy ~0.3 ms, graph ~31.7 ms, detector 0.0 ms, landmark ~28.4 ms, bridge ~0.4 ms, detector skipped because tracking was active.

The USER also reported OpenVINO felt smoother when controlling the avatar in F12.

Interpretation: OpenVINO's compute path is materially faster, but a prepared frame can miss the readback-completion immediate-launch opportunity while inference is still busy. When the OpenVINO worker finishes, `OnOpenVinoPoseResult()` clears the outstanding flag, but an already-prepared frame is then typically consumed by a later normal `Update`, costing roughly one render frame in the captured case. This masks the backend's compute advantage.

## Goal

Implement the smallest safe bounded scheduling change that lets the newest already-available frame begin OpenVINO processing as soon as the previous OpenVINO inference completes, instead of unnecessarily waiting for the next regular `Update` opportunity.

The practical target is to reduce the OpenVINO pattern:

```text
Prep->launch ~= 20 ms
frame delta = 1
origin = Update
```

closer to immediate/latest-frame continuation while preserving all accepted queue/concurrency semantics.

Do not promise or hardcode a numerical pass threshold. The next USER A/B run will decide whether the change is useful.

## Non-negotiable architecture constraints

- Keep all work on `engine/pose-tracking-spike`.
- Do not merge to `main`.
- Stock MediaPipe/TFLite CPU must remain fully usable and behaviorally unchanged unless a shared bug fix is strictly necessary.
- Explicit OpenVINO selection must remain explicit; no silent fallback reported as OpenVINO.
- Do not change canonical mapping, stabilization, confidence/trust, calibration, retargeting, locomotion, or presentation smoothing.
- Keep 320x240 body inference for this experiment.
- Keep max one active readback.
- Keep max one outstanding inference.
- Keep max one replaceable pending/prepared latest frame. If representation changes, the semantic bound is still exactly one latest pending frame.
- No history queue, FIFO backlog, replay, catch-up queue, or two-inference overlap.
- Latest useful frame wins; replacing an older pending frame with a newer one is allowed and expected.
- Preserve DirectCPU readback and its fallback behavior.
- Preserve camera switching and teardown safety. Do not free buffers/native runtime while worker/readback operations can still touch them.
- Do not touch the USER-owned scene YAML merely to wire the experiment.
- Do not densify/convert detector models, change model identities, or reopen Gate B semantics.
- Do not force D3D12.
- Phase 5A remains NOT USER ACCEPTED; Phase 6 remains NOT STARTED.

## Candidate architecture to evaluate

The Orchestrator's current hypothesis is an OpenVINO-specific persistent worker plus a **one-slot latest-frame mailbox**. This is a candidate, not a mandated implementation.

A sound design could look like:

1. Unity/main-thread readback produces the newest usable RGBA frame + timestamp/orientation metadata.
2. If OpenVINO worker is idle, that frame becomes active immediately.
3. If worker is busy, there is at most one pending frame; a newer frame replaces the older pending one.
4. When current inference finishes, the same worker immediately takes the newest pending frame if one exists, with no extra normal-`Update` wait.
5. Result publication remains safe for the existing `PoseObservation` path.
6. Shutdown/restart/camera-switch can cancel/stop intake, wait for the active worker as needed, and dispose deterministically.

But inspect the existing Unity threading/lifetime boundaries first. Do not call Unity APIs from a worker thread unless the API is documented/safe in this context. If the safest design uses another bounded mechanism, choose it and document why.

Important detail: the current OpenVINO path uses one managed `_openVinoRgbaBuffer` for the active task. A true latest-frame mailbox may require separate active and pending storage or another ownership scheme so a new frame cannot overwrite memory still being read by the active native call. Any extra storage must remain bounded and intentional.

## What to inspect before coding

Audit these exact behaviors in current source:

- `Update()` ordering: metrics, backend failure consumption, camera switch, fresh-frame detection, `TryLaunchPreparedInference()`, then `TryStartLatestReadback()`.
- `CapturePreparedFrameAsync()` publication of `_preparedTextureFrame` and its final `TryImmediateLaunchAfterReadback()`.
- `TryImmediateLaunchAfterReadback()` eligibility and cadence gate.
- `LaunchPreparedOpenVinoInference()` buffer copy, outstanding flag, diagnostic tokens, `Task.Run`, continuation, request accounting.
- `OnOpenVinoPoseResult()` result publication, diagnostics, outstanding-clear timing.
- `CapturePendingInferenceCompletion()` / `RecordInferenceContinuationSample()` semantics so telemetry remains truthful after the change.
- camera switch and `CleanupRuntime()` / OpenVINO teardown ordering.
- existing tests for `LatestFramePipelinePolicy`, direct-readback ownership, restart/teardown, and managed ABI.

Do not merely move a call to `TryLaunchPreparedInference()` onto a worker continuation if that would touch Unity-owned `TextureFrame`, `Time`, `MonoBehaviour`, or other main-thread-sensitive state unsafely.

## Implementation expectations

1. **Architecture first, then code.** Choose a bounded mechanism and document ownership/threading semantics in code comments/tests, not a broad design essay.
2. Preserve stock TFLite path.
3. Preserve/extend telemetry so the next USER run can see whether continuation actually changed.
4. Avoid allocations in the steady hot path where practical, especially per-result/per-frame task churn if a persistent worker eliminates it cleanly.
5. Keep failure behavior fail-closed: a worker/native failure must surface as `InferenceFailed` rather than silently spinning or falling back.
6. Keep lifecycle deterministic across Play Mode stop/restart and camera switch.
7. Do not make unrelated refactors.

## Deterministic verification expected before USER QA

Use proportionate tests. At minimum establish through unit/static/managed/native checks where practical that:

- there is never more than one active OpenVINO inference;
- there is never more than one pending latest frame;
- pending replacement drops/releases the older pending frame safely;
- completion immediately claims a pending frame through the new mechanism rather than relying solely on normal `Update`;
- no queue can grow;
- timestamps remain monotonic for native stream-mode processing;
- restart/teardown cannot dispose buffers/runtime while the worker still owns them;
- camera switch cannot let stale-coordinate-convention frames leak into the new session;
- stock TFLite code path remains present/default-safe;
- managed/native package/ABI assumptions remain unchanged unless explicitly justified.

If Unity compilation can be checked without requiring the USER's physical webcam, do so. Avoid unnecessary full native rebuilds: this task should primarily be managed/scheduling code unless a genuine native change is required.

## Checkpoint / execution-limit workflow

Do not stop after each checkpoint. Use checkpoint commits only so progress is recoverable.

At each coherent recovery point:

1. update `Docs/openvino-unity-scheduling-optimization-progress.md` with exact SHA/status/next action;
2. make a focused commit and push to `engine/pose-tracking-spike`;
3. continue immediately if no USER action is needed.

If execution budget becomes risky, checkpoint first, then stop with `CONTINUE FROM HERE` and the exact unfinished action.

## USER runtime handoff boundary

Stop when the implementation is compiled/verified enough for another live A/B test.

The USER should then compare stock TFLite and OpenVINO again under identical settings, ideally without OBS, and capture F7 evidence. The critical post-change OpenVINO values are:

- prepared-to-launch delay;
- frame delta;
- launch origin / immediate continuation rate;
- requests/s and results/s;
- latest pose age;
- inference/request-to-result;
- frame-to-result;
- readback;
- OpenVINO graph/detector/landmark/bridge timings;
- subjective F12 control responsiveness;
- Play Mode start/stop stability.

Do not claim final U4 performance acceptance yourself.

## Git authorization

You are authorized to create focused commits and push them to `engine/pose-tracking-spike` for this approved optimization.

You are not authorized to:
- merge to `main`;
- force-push;
- rewrite/rebase/reset shared history;
- merge a PR;
- discard USER-local changes.

## Stop report format

When you genuinely stop, return one report containing:

- exact starting remote SHA;
- exact final remote SHA;
- architecture chosen and why;
- checkpoint commits;
- files changed;
- scheduling/ownership behavior before vs after;
- how the one-inference/one-latest-frame/no-backlog invariants are enforced;
- telemetry changes;
- tests/verification run and results;
- any native/package rebuild performed or explicitly avoided;
- unresolved risks/blockers;
- exact USER QA instructions;
- confirmation that stock TFLite remains usable, `main` was not merged, Phase 5A was not reclassified, and Phase 6 was not started.
