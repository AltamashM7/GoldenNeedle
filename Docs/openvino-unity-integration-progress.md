# Golden Needle — OpenVINO Unity Integration Progress

This is the rolling resume point for the practical OpenVINO Unity integration experiment.

The intelligent Web Builder must update this file whenever a durable checkpoint/sub-step is completed and before any anticipated execution-limit stop. A replacement Builder should read this file first, verify the current remote branch HEAD, inspect the working tree, and continue from the first unfinished action rather than reconstructing the project from scratch.

## Current status

- Branch: `engine/pose-tracking-spike`
- Integration authorization: **APPROVED BY USER**
- Gate A: **PASS**
- Gate B: **PASS WITH NOTES**
- U0 plan/recovery scaffold: **COMPLETE**
- U1 architecture resolution: **NOT STARTED**
- U2 native OpenVINO backend: **NOT STARTED**
- U3 Unity selection/telemetry: **NOT STARTED**
- U4 USER A/B runtime QA: **NOT STARTED**
- U5 cleanup/production recommendation: **NOT STARTED**

## Proven evidence entering Unity integration

Gate B recorded-motion proof completed all 363 frames:
- Tasks reference: ~28.74 ms steady mean, ~34.80/s offline graph capacity.
- Custom TFLite graph: ~28.09 ms, ~35.59/s; exact semantic match to Tasks on the sequence.
- OpenVINO CPU FP32 graph: ~14.03 ms, ~71.26/s.
- OpenVINO pose-presence agreement: 362/363 (~99.7245%).
- Normalized XYZ RMS: ~0.01113.
- World Euclidean 3D RMS: ~0.02188 m.
- OpenVINO bridge-copy mean: ~0.2025 ms.

These are offline VIDEO-mode measurements, not Unity LIVE_STREAM end-to-end results.

## Integration invariants

- Keep stock MediaPipe/TFLite CPU fully functional as fallback.
- OpenVINO begins as experimental/selectable CPU FP32 backend.
- Reuse MediaPipe 0.10.22 preprocessing/postprocessing/tracking/world-landmark semantics.
- Do not recreate those semantics manually in C#.
- Do not change canonical/stabilization/calibration/retarget/locomotion semantics for this experiment.
- Keep 320x240 body inference input for the initial A/B test.
- Keep latest-useful-frame scheduling and no-backlog semantics.
- Do not densify/convert detector.
- Do not force D3D12.
- Do not refactor the current fixed 33-landmark provider or 20-joint canonical topology in this spike.
- Remove/disable Gate B shadow-TFLite parity instrumentation in runtime performance builds.
- No merge to `main` without explicit USER approval.
- Phase 5A remains not USER accepted; Phase 6 remains not started.

## Recovery / handoff state

No Unity/OpenVINO integration runtime code has been implemented yet.

The next Builder should begin at U1 by inspecting the actual Unity ↔ Homuler ↔ MediaPipe native boundary, then continue directly into U2/U3 in the same run whenever feasible. Checkpoints are recovery markers, not approval gates.

Primary handoff:
`Docs/worker-briefs/openvino-unity-integration-handoff.md`

Execution plan:
`Docs/openvino-unity-integration-checkpoints.md`

## Next concrete action

1. Verify remote `engine/pose-tracking-spike` HEAD and local Git status.
2. Read `AGENTS.md`, this file, the integration checkpoint plan, relevant architecture docs, Gate B tooling, provider code, package/native plugin layout.
3. Resolve the least-invasive native/managed integration architecture (U1).
4. Commit/push a coherent U1 checkpoint and update this file.
5. Continue directly into native backend implementation (U2) unless a genuine blocker requires the USER/Orchestrator.

## Builder update template

Replace/add sections below as work advances:

### Checkpoint / sub-step
- Status:
- Starting SHA:
- Ending SHA:
- Files changed:
- What was implemented/audited:
- Verification performed:
- Local/generated artifacts not committed:
- Risks/blockers:
- Exact next action:
