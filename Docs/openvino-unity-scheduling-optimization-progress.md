# Golden Needle — OpenVINO Unity Scheduling Optimization Progress

This is the rolling resume point for the post-U4 OpenVINO scheduling optimization approved by the USER on 2026-09-14.

A replacement Web Builder must read this file first, verify the current remote `engine/pose-tracking-spike` HEAD, inspect existing work, and continue from the first unfinished action. Checkpoints are recovery markers, not stop-and-wait approval gates.

## Authorization / status

- Branch: `engine/pose-tracking-spike`.
- USER explicitly approved this focused optimization.
- Gate A: PASS.
- Gate B: PASS WITH NOTES.
- U1/U2/U3 Unity integration implementation: COMPLETE.
- U4 live Unity correctness: PASS.
- U4 performance result: OPENVINO COMPUTE ADVANTAGE PROVEN, END-TO-END GAIN CURRENTLY MASKED BY SCHEDULING.
- Production/default backend decision: NOT YET MADE.
- Phase 5A remains NOT USER ACCEPTED.
- Phase 6 remains NOT STARTED.
- No merge to `main` is authorized.

## USER evidence that triggered this optimization

The USER performed clean same-machine Unity A/B runs without OBS after the native/package/managed integration had passed.

Stock MediaPipe/TFLite CPU screenshot:
- camera: ~30.3 FPS;
- render: ~36.5 FPS;
- inference requests/results: ~13.7 / 13.7 per second;
- latest pose age: ~49 ms;
- last inference/result time: ~52.8 ms;
- pipeline readback/build/inference/frame-to-result values shown approximately as `47.1 / 0.0 / 44.4 / 97.7 ms`;
- prepared-to-launch: ~0.1 ms;
- frame delta: 0;
- launch origin: `RB`;
- immediate/fast launches: ~13.7/s;
- DirectCPU readback active, stage V.

OpenVINO CPU FP32 screenshot:
- camera: ~29.5 FPS;
- render: ~39.0 FPS;
- inference requests/results: ~12.8 / 12.8 per second;
- latest pose age: ~90 ms at the captured instant;
- last inference/result time: ~37.2 ms;
- pipeline readback/build/inference/frame-to-result values shown approximately as `46.6 / 0.3 / 31.9 / 98.5 ms`;
- prepared-to-launch: ~19.9 ms;
- frame delta: 1;
- launch origin: `Update`;
- immediate/fast launches: ~7.9/s;
- DirectCPU readback active, stage V;
- OpenVINO internal timing at the captured instant: managed copy ~0.3 ms, graph ~31.7 ms, detector ~0.0 ms, landmark ~28.4 ms, bridge ~0.4 ms, detector not run on that tracked frame.

The USER additionally reported that OpenVINO subjectively felt smoother when controlling the avatar in F12.

Interpretation accepted by the Orchestrator:
- OpenVINO materially reduces neural/graph-side time versus stock TFLite in live Unity.
- The gain is currently lost because a prepared frame can miss the exact readback-completion immediate-launch opportunity while OpenVINO inference is still busy, then sit until a later Unity `Update` after the previous OpenVINO worker task completes.
- This is visible as OpenVINO `Prep->launch ~19.9 ms`, `df=1`, `origin=Update`, despite faster inference.
- Therefore do not discard OpenVINO yet; first remove this continuation/scheduling loss and retest.

## Non-negotiable invariants

- Keep stock MediaPipe/TFLite CPU fully functional and selectable.
- Do not change canonical mapping, stabilization, confidence, calibration, retargeting, locomotion, or presentation smoothing as part of this optimization.
- Keep 320x240 body inference input for comparison.
- Preserve latest-useful-frame semantics: no history/replay/catch-up queue.
- At most one active readback.
- At most one outstanding inference.
- At most one replaceable pending/prepared latest frame. If the internal representation changes from `TextureFrame` to another one-slot mailbox, the semantic bound must remain one replaceable latest frame.
- Do not create an unbounded task queue.
- Do not run two OpenVINO inferences concurrently.
- Do not silently fall back from explicitly selected OpenVINO.
- Preserve DirectCPU fallback and teardown safety.
- Do not touch the USER-owned `PoseTrackingSpike.unity` scene YAML merely for convenience.
- Do not merge to `main`, force-push, rebase/reset shared history, or start Phase 6.

## Optimization objective

Remove the avoidable delay between completion of one OpenVINO inference and launch of the newest already-available frame.

The target behavior is conceptually:

1. one OpenVINO inference is active;
2. camera/readback may produce one newer replaceable frame while inference is active;
3. as soon as the active inference finishes, the newest pending frame becomes the next inference without waiting an extra Unity render-frame-sized `Update` delay;
4. no queue/backlog forms;
5. older pending frames are replaced by newer ones;
6. Unity/native lifetime and shutdown remain safe.

The previous Orchestrator hypothesis was an OpenVINO-specific persistent worker plus a one-slot latest-frame mailbox. Treat that as a candidate design, not a mandate. Audit Unity-thread restrictions and the existing provider first and choose the least invasive safe implementation that achieves the objective.

## Measurements that must remain visible

Preserve existing F7 diagnostics and add/adjust only what is needed to prove the optimization. At minimum retain:
- backend label;
- requests/s and results/s;
- latest pose age;
- readback duration;
- inference/request-to-result duration;
- approximate frame-to-result latency;
- prepared-to-launch delay and frame delta;
- launch origin / immediate-launch rate;
- wait/drop/replacement pressure;
- OpenVINO managed copy / graph / detector / landmark / bridge timings.

The post-change USER test should make it possible to determine whether OpenVINO `Prep->launch` and `df=1/origin=Update` behavior was reduced without increasing concurrency or backlog.

## Builder checkpoint discipline

Work continuously through architecture audit, implementation, focused automated verification, and runtime-test preparation. At coherent recovery points, commit/push to `engine/pose-tracking-spike` and update this file. Do not stop merely because a checkpoint was reached.

Stop only for:
- genuine USER hardware/visual QA;
- a real blocker requiring a decision;
- execution-limit risk after first making the current state recoverable.

## Current exact next action

1. Read `Docs/worker-briefs/openvino-unity-scheduling-optimization-handoff.md`.
2. Inspect the current `MediaPipePoseProvider` scheduling/readback/OpenVINO worker code and related diagnostics/tests.
3. Design and implement the smallest safe bounded continuation mechanism that can consume one newest pending frame immediately after OpenVINO completion without waiting for the next normal `Update` opportunity.
4. Add deterministic tests for the one-inference/one-latest-frame/no-backlog semantics and teardown/restart behavior where practical.
5. Preserve stock TFLite behavior.
6. Update this progress file and push coherent checkpoint commits while continuing.
7. Stop at the next USER A/B runtime boundary and return exact test instructions plus a detailed implementation report.
