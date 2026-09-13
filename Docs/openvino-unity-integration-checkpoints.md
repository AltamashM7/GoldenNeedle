# Golden Needle — OpenVINO Unity Integration Checkpoints

Status date: 2026-09-13

Starting integration baseline: `a2e4a576628e1ca18056fa4a7cb29b995bf3fe20` on `engine/pose-tracking-spike`.

This document is the authoritative execution plan for the first practical Unity/OpenVINO integration experiment. It supersedes older wording that blocked all Unity/OpenVINO integration before Gate B evidence was available.

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
- Keep the current 320x240 body-inference input and native-resolution camera/display path for the A/B test unless a later checkpoint explicitly authorizes otherwise.
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

## Checkpoint U0 — Durable plan and branch freeze

Owner: Orchestrator.

Purpose:
- record Gate B acceptance;
- freeze the starting branch SHA;
- define checkpoint boundaries before implementation begins.

Exit condition:
- this document exists on `engine/pose-tracking-spike`;
- the next worker starts from the exact current remote HEAD and reports it before making changes.

No Unity runtime implementation belongs in U0.

---

## Checkpoint U1 — Unity/native integration architecture audit

Owner: intelligent Web Builder.

This is a **read-mostly architecture checkpoint**. Do not implement the runtime backend yet.

Required inspection:
1. Verify exact remote HEAD and active branch.
2. Read `AGENTS.md`, `Docs/current-state.md`, `Docs/inference-architecture-reuse-audit.md`, this document, and the Gate B tooling under `Tools/MediaPipeOpenVinoParity/`.
3. Inspect the existing Unity provider boundary, especially:
   - `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipePoseProvider.cs`;
   - `MediaPipeCanonicalPoseSource.cs`;
   - `PoseObservation.cs`;
   - current scheduler/readback/result callback flow;
   - model loading and PoseLandmarker creation;
   - existing diagnostics used for capture/readback/inference/frame-to-result timing.
4. Inspect the installed Homuler/MediaPipeUnityPlugin native-plugin and C# binding layout in the repo/package manifest/lock as actually present.
5. Inspect the Gate B OpenVINO calculator and exact MediaPipe 0.10.22 seams already proven.
6. Determine the least invasive way to expose the proven OpenVINO inference replacement to Unity while keeping the stock MediaPipe CPU route selectable.
7. Determine how Windows x86_64 OpenVINO runtime DLLs and the modified/rebuilt MediaPipe native binary would be packaged for Editor/player loading without polluting unrelated platforms.
8. Identify licensing/third-party notice consequences and whether Git LFS patterns need any narrow addition for shipped native binaries.

Architecture preference order:
- First choice: reuse the existing Homuler/MediaPipe native task/graph boundary and selectively add the OpenVINO inference backend inside that native generation.
- Second choice only if the first is impractical: a small dedicated native bridge that still returns mature MediaPipe PoseLandmarker semantics to the existing Unity provider.
- Reject manual reimplementation of detector decode, ROI tracking, landmark decode/projection or world-landmark semantics in C#.

U1 must explicitly answer:
- Which binary/library is actually loaded by Unity today?
- Where can the backend selection enter without duplicating the accepted provider pipeline?
- Does selection require a new C API/export, graph option, environment/config side channel, or separate native library?
- Can the stock TFLite path remain the serialized/default fallback?
- What exact source/build/package files would U2 need to change?
- What exact USER-local build step, if any, will be required on Windows?
- What are the rollback steps if OpenVINO fails to initialize?

U1 deliverable:
- one concise architecture report committed to `Docs/`;
- no production/runtime source changes unless a tiny diagnostic is strictly required to answer the audit and is separately justified;
- report exact ending SHA and stop.

**STOP after U1. Do not begin U2 in the same worker run.** The Orchestrator reviews the report first.

---

## Checkpoint U2 — Native OpenVINO backend prototype

Starts only after Orchestrator accepts U1.

Goal:
- make a Windows x86_64 native build path that preserves the current MediaPipe 0.10.22/Homuler semantics while optionally replacing detector + landmark inference with OpenVINO CPU FP32.

Requirements:
- stock TFLite CPU remains available and unchanged as fallback;
- reuse the Gate B `InferenceCalculatorNodeImpl`-compatible OpenVINO calculator design;
- remove/compile out one-time shadow TFLite raw-parity instrumentation for runtime benchmarking;
- keep model identity exact; no detector conversion/densification;
- OpenVINO device is explicit `CPU`, latency-oriented, FP32;
- fail cleanly if OpenVINO or required DLLs are missing;
- no silent fallback while a benchmark explicitly requests OpenVINO; fallback policy must be visible to Unity diagnostics;
- package only required runtime dependencies for Windows x86_64;
- do not touch scene YAML.

U2 deliverable:
- buildable source/scripts and packaging layout;
- exact build instructions for the USER if a local Windows native build is required;
- no C# provider behavior switch yet unless strictly necessary to load-test the library;
- report exact ending SHA and stop.

**STOP after U2.** The USER/Orchestrator verifies native build/load before U3.

---

## Checkpoint U3 — Unity backend selection + telemetry

Starts only after U2 native load/build is verified.

Goal:
- allow the existing Unity MediaPipe provider to choose between the accepted stock TFLite CPU path and experimental OpenVINO CPU FP32 path without changing downstream pose semantics.

Requirements:
- default remains the accepted stock MediaPipe/TFLite CPU backend until USER acceptance says otherwise;
- backend identity is visible in Inspector/debug diagnostics;
- same camera source, orientation, 320x240 body input, latest-useful-frame scheduler and PoseObservation publication path;
- same canonical mapper, stabilization, calibration, retargeting and locomotion consumers;
- preserve partial-body behavior and world landmarks;
- maintain safe teardown/restart and DirectCPU readback behavior;
- add only the telemetry needed for A/B comparison.

Required telemetry:
- selected backend / initialization result;
- camera capture rate;
- readback latency;
- accepted inference request -> result latency;
- frame-capture -> published-result latency;
- fresh pose results per second;
- dropped/replaced prepared frames or equivalent latest-frame pressure counters;
- Unity render FPS/frame time;
- OpenVINO detector and landmark inference timing if available without costly per-frame logging;
- native/managed bridge overhead where measurable.

U3 deliverable:
- compiling Unity integration;
- concise USER manual QA procedure;
- exact ending SHA and stop.

**STOP after U3. Do not declare performance success.** USER runtime evidence is required.

---

## Checkpoint U4 — USER A/B runtime QA

Owner: USER with Orchestrator guidance.

Compare stock MediaPipe/TFLite CPU vs OpenVINO CPU FP32 in the same Unity scene and same physical conditions.

Baseline expectations from prior accepted runs:
- camera ~29–30 FPS;
- fresh pose results ~10–12/s;
- readback ~55–65 ms;
- frame-to-result often ~110–140 ms.

The test should include:
- neutral/slow motion;
- deliberately fast arm swings/reaches;
- torso movement;
- leg/knee movement when visible;
- temporary partial-body/occlusion recovery if practical.

Acceptance evidence is not a single FPS number. Review:
- fresh pose sample rate;
- frame-to-result latency mean/p95;
- Unity frame stability;
- initialization/restart/teardown stability;
- visible fast-motion fidelity;
- no unacceptable regression in pose quality, orientation, world coordinates, calibration or avatar retargeting.

U4 result states:
- `PASS`;
- `PASS WITH NOTES`;
- `FAIL / FALLBACK TO STOCK`.

Only the USER can provide visual/physical QA acceptance.

---

## Checkpoint U5 — Cleanup, documentation and production decision

Starts only after U4 evidence is reviewed.

If OpenVINO is accepted:
- remove remaining spike-only diagnostics;
- document exact runtime dependencies and licenses;
- document backend selection/fallback policy;
- update `Docs/current-state.md` and other authoritative docs;
- preserve the stock CPU fallback;
- decide separately whether OpenVINO should become Auto/default on supported Windows machines.

If OpenVINO fails practical QA:
- retain the evidence and isolated implementation only if it remains useful;
- keep stock MediaPipe CPU as production;
- do not hide the negative result with smoothing changes.

Neither outcome starts Phase 6 automatically.

## Execution-limit rule

Each checkpoint is intentionally a separate worker assignment. A worker must stop and report when its checkpoint exit condition is reached, even if token/execution budget remains. The next checkpoint begins only after Orchestrator review. This prevents long native/Unity work from being lost to execution limits and creates durable recovery points in Git history.