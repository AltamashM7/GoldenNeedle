# Golden Needle — OpenVINO Unity Integration Checkpoints

Status date: 2026-09-13

Starting integration baseline: `a2e4a576628e1ca18056fa4a7cb29b995bf3fe20` on `engine/pose-tracking-spike`.

This document is the authoritative execution plan for the first practical Unity/OpenVINO integration experiment. It supersedes older wording that blocked all Unity/OpenVINO integration before Gate B evidence was available.

## Important execution model

These checkpoints are **durable recovery markers, not mandatory stop-and-wait gates**.

The intelligent Web Builder should continue development normally through as many checkpoints as its execution budget safely allows. It should not wait for Orchestrator approval between ordinary implementation checkpoints.

At every checkpoint it must:
- make the current work durable on `engine/pose-tracking-spike` with a focused commit/push;
- update `Docs/openvino-unity-integration-progress.md` with exact state and next action;
- continue immediately into the next checkpoint when feasible;
- stop only when USER hardware/visual QA is genuinely required, a real blocker needs a decision, or execution-limit risk makes continuation unsafe.

If an execution limit is reached, the next Web Builder should read the rolling progress file, inspect the current remote HEAD and working tree, then continue from the first unfinished item. It should not restart the integration from scratch.

## Evidence that unlocks this work

Gate A: **PASS**.

Gate B: **PASS WITH NOTES** on the USER's recorded 363-frame motion sequence.

Gate B results:
- `TASKS_REFERENCE`: complete, steady mean ~28.74 ms, ~34.80 frames/s offline graph capacity.
- `GRAPH_TFLITE_CPU`: complete, steady mean ~28.09 ms, ~35.59 frames/s offline graph capacity.
- `GRAPH_OPENVINO_CPU_FP32`: complete, steady mean ~14.03 ms, ~71.26 frames/s offline graph capacity.
- Tasks vs custom TFLite: exact semantic match on the recorded sequence.
- Tasks/TFLite vs OpenVINO: pose-presence agreement 362/363 (~99.7245%).
- OpenVINO normalized XYZ RMS ~0.01113.
- OpenVINO world Euclidean 3D RMS ~0.02188 m.
- OpenVINO bridge-copy mean ~0.2025 ms.

These are offline VIDEO-mode graph-capacity results, not Unity LIVE_STREAM end-to-end latency. They justify a controlled Unity integration experiment; they do not justify silently replacing the accepted production CPU path.

## Non-negotiable governance

- Work remains on `engine/pose-tracking-spike`.
- Do not merge to `main` without explicit USER approval.
- Phase 5A remains implemented but **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.
- The accepted MediaPipe CPU provider remains the known-safe fallback and must continue to work.
- Do not alter accepted canonical, stabilization, calibration, retargeting, locomotion or presentation semantics merely to accommodate OpenVINO.
- Keep the current 320x240 body-inference input and native-resolution camera/display path for the A/B test unless a later finding proves a change is necessary.
- Keep latest-useful-frame scheduling semantics: at most one active readback, one replaceable prepared frame and one outstanding inference; no backlog/replay/catch-up queue.
- Do not densify or convert the detector.
- Do not force D3D12 globally.
- Do not refactor the 33-landmark provider frame or 20-joint canonical topology as part of this performance spike.
- Do not ship the Gate B shadow-TFLite/raw-parity diagnostic in the Unity runtime path.
- Preserve known USER-local dirty files. Never clean/revert/stage them casually.

## Integration objective

Answer one practical question on the USER's low-end laptop:

> Does a current-MediaPipe-semantics OpenVINO CPU FP32 backend materially increase fresh pose-result rate and reduce real webcam frame-to-pose-result latency inside Unity without breaking accepted motion behavior?

The experiment must remain reversible. OpenVINO starts as an experimental selectable backend, not an unconditional replacement.

---

## Checkpoint U0 — Plan / recovery scaffold

Owner: Orchestrator.

Purpose:
- record Gate B acceptance;
- define implementation invariants;
- create the rolling progress/handoff mechanism.

U0 does not block the Builder from immediately proceeding into U1.

---

## Checkpoint U1 — Architecture resolved

Owner: intelligent Web Builder.

Goal:
- inspect the real Unity ↔ Homuler ↔ MediaPipe native boundary and select the least-invasive practical integration route before large implementation changes.

Required inspection includes:
- `MediaPipePoseProvider.cs`, `MediaPipeCanonicalPoseSource.cs`, `PoseObservation.cs`;
- current scheduler/readback/result callback path and timing diagnostics;
- actual `Packages/manifest.json` / lock and Homuler native-plugin/binding layout;
- Gate B OpenVINO calculator and exact MediaPipe 0.10.22 seams;
- Windows x86_64 native build/package path and runtime dependency loading.

Architecture preference:
1. existing Homuler/MediaPipe native task boundary with selective OpenVINO inference replacement;
2. small dedicated native bridge only if option 1 is impractical and mature MediaPipe semantics are still preserved;
3. manual recreation of MediaPipe detector/ROI/landmark/world semantics is rejected absent strong evidence that no reusable route exists.

Checkpoint marker should record:
- chosen architecture;
- exact files expected to change;
- native build/package strategy;
- managed/native selection mechanism;
- stock fallback/rollback behavior;
- unresolved risks.

**After recording U1, continue directly into U2 when feasible.**

---

## Checkpoint U2 — Native OpenVINO backend implemented

Goal:
- create the Windows x86_64 native path that preserves MediaPipe 0.10.22 semantics while optionally replacing detector + landmark inference with OpenVINO CPU FP32.

Requirements:
- stock TFLite CPU remains available and unchanged as fallback;
- reuse the Gate B `InferenceCalculatorNodeImpl`-compatible OpenVINO calculator design;
- remove/compile out shadow-TFLite raw-parity instrumentation from practical runtime benchmarking;
- exact models remain unchanged; no detector conversion/densification;
- OpenVINO device explicit `CPU`, latency-oriented, FP32;
- explicit initialization/backend identity; no false reporting of OpenVINO when fallback is active;
- package only required Windows x86_64 runtime dependencies;
- avoid touching USER-owned scene YAML.

Checkpoint marker should record:
- native source/build/package implementation completed;
- exact local build command(s);
- produced binary/runtime-dependency layout;
- native verification status;
- remaining managed integration work.

**After recording U2, continue directly into U3 if the required native artifacts/build can be produced without USER intervention. If a long USER-local native build is required, stop only at that genuine hardware/build handoff.**

---

## Checkpoint U3 — Unity backend selection + telemetry implemented

Goal:
- allow the existing Unity MediaPipe provider to choose stock TFLite CPU or experimental OpenVINO CPU FP32 without changing downstream semantics.

Requirements:
- stock MediaPipe/TFLite remains the default until USER acceptance says otherwise;
- backend identity and initialization/failure state visible in Inspector/debug diagnostics;
- same camera source, orientation, 320x240 body input, latest-useful-frame scheduler and PoseObservation publication semantics;
- same canonical mapper, stabilization, calibration, retargeting and locomotion consumers;
- preserve partial-body behavior and world landmarks;
- maintain safe teardown/restart and DirectCPU readback behavior;
- add only useful A/B telemetry.

Required telemetry:
- selected/effective backend;
- camera capture rate;
- readback latency;
- accepted inference request → result latency;
- capture/frame → published-result latency;
- fresh pose results/s;
- dropped/replaced prepared frames or equivalent latest-frame pressure counters;
- Unity render FPS/frame time;
- OpenVINO detector/landmark inference timing when available cheaply;
- native/managed bridge overhead when measurable.

Checkpoint marker should record:
- compiling managed/native integration state;
- exact files changed;
- verification performed;
- exact remaining setup/QA actions.

**After recording U3, continue into U4-prep and automate all non-physical verification possible. Stop only when USER runtime/visual QA is actually needed.**

---

## Checkpoint U4 — USER A/B runtime QA

Owner: USER with Orchestrator guidance.

This is the first normal mandatory human stop because the result depends on the USER's actual webcam, laptop load, visual motion fidelity and physical movement.

Compare stock MediaPipe/TFLite CPU vs OpenVINO CPU FP32 in the same Unity scene and same conditions.

Baseline expectations:
- camera ~29–30 FPS;
- fresh pose results ~10–12/s;
- readback ~55–65 ms;
- frame-to-result often ~110–140 ms.

Test:
- neutral/slow motion;
- deliberately fast arm swings/reaches;
- torso movement;
- leg/knee movement when visible;
- temporary partial-body/occlusion recovery if practical.

Review:
- fresh pose sample rate;
- frame-to-result mean/p95;
- Unity frame stability;
- initialization/restart/teardown stability;
- fast-motion fidelity;
- pose quality/orientation/world-coordinate/calibration/retarget regressions.

U4 outcome:
- `PASS`;
- `PASS WITH NOTES`;
- `FAIL / FALLBACK TO STOCK`.

Only the USER can provide visual/physical QA acceptance.

---

## Checkpoint U5 — Cleanup / docs / production recommendation

After U4 evidence:

If OpenVINO is accepted:
- remove remaining spike-only diagnostics;
- document runtime dependencies/licenses;
- document selection/fallback policy;
- update `Docs/current-state.md` and architecture docs;
- preserve stock CPU fallback;
- recommend separately whether OpenVINO should become Auto/default on supported Windows machines.

If OpenVINO fails practical QA:
- retain evidence and isolated implementation only if useful;
- keep stock MediaPipe CPU production path;
- do not hide a negative result with smoothing changes.

Neither outcome starts Phase 6 automatically.

## Rolling progress / execution-limit rule

`Docs/openvino-unity-integration-progress.md` is the single resume point.

At each U1/U2/U3 checkpoint and whenever a substantial sub-step within a checkpoint has completed, the Builder should update it with:
- current remote HEAD;
- last completed checkpoint/sub-step;
- exact implementation status;
- files changed/added;
- commands/builds/tests already performed and their outcomes;
- known USER-local/generated artifacts that are not committed;
- unresolved problems;
- the exact next action to take.

Checkpoint commits are encouraged whenever they leave the repository in a coherent recoverable state. Do not artificially fragment every tiny edit into a commit.

If execution budget is becoming low, prioritize making the current state durable and updating the progress file before attempting another broad change. A replacement Web Builder should be able to resume simply by reading the handoff + progress file and inspecting the current branch.
