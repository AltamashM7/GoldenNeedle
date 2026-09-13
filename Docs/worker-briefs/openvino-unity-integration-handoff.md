# Web Builder Handoff — OpenVINO Unity Integration

Role: intelligent Web Builder

Repository: `AltamashM7/GoldenNeedle`

Branch: `engine/pose-tracking-spike`

This handoff authorizes you to continue the practical Unity/OpenVINO integration through multiple durable checkpoints in one worker run. **The checkpoints are recovery markers, not mandatory approval gates.** Work continuously until you reach a real USER-hardware/visual-QA handoff, a genuine blocker requiring a decision, or execution-limit risk.

If you are a replacement Builder after another Builder hit its execution limit, do not restart the task. Read `Docs/openvino-unity-integration-progress.md`, inspect the current remote HEAD and working tree, then continue from the first unfinished action.

## Starting-state procedure

Before changing anything:

1. Verify repository and active/target branch.
2. Fetch/inspect current remote `engine/pose-tracking-spike` HEAD and report the exact SHA internally/in your eventual report. Do not assume a stale SHA from an older chat.
3. Inspect local Git status if a checkout is available. Preserve USER-local dirty files; do not clean/reset/revert them.
4. Read, in this order:
   - `AGENTS.md`
   - `Docs/openvino-unity-integration-progress.md`
   - `Docs/openvino-unity-integration-checkpoints.md`
   - `Docs/current-state.md`
   - `Docs/inference-architecture-reuse-audit.md`
   - Gate B implementation under `Tools/MediaPipeOpenVinoParity/`
5. Treat the OpenVINO integration progress/checkpoint docs as newer authority where older `current-state.md` wording still describes Gate B as pending or Unity integration as blocked.

## Current authorization and evidence

The USER has approved beginning practical Unity integration.

Gate A: **PASS**.

Gate B: **PASS WITH NOTES** on the USER's recorded 363-frame motion sequence:
- official Tasks reference completed;
- custom MediaPipe graph using stock TFLite CPU completed and matched the Tasks output exactly on the sequence;
- same graph with OpenVINO CPU FP32 replacing detector + landmark neural inference completed all 363 frames;
- stock custom TFLite steady offline graph capacity ~35.59/s;
- OpenVINO steady offline graph capacity ~71.26/s;
- OpenVINO pose-presence agreement vs reference 362/363 (~99.7245%);
- normalized XYZ RMS ~0.01113;
- world Euclidean 3D RMS ~0.02188 m;
- OpenVINO native bridge-copy mean ~0.2025 ms.

These are offline VIDEO-mode graph-capacity results. Do not claim Unity LIVE_STREAM will reach ~71/s until measured.

## Goal

Integrate the already-proven current-MediaPipe-0.10.22 + OpenVINO CPU FP32 inference seam into the existing Unity motion provider in the least invasive, reversible way, then prepare a true Unity A/B test against the accepted stock MediaPipe/TFLite CPU backend.

The practical question is:

> Does OpenVINO materially raise fresh pose-result rate and lower real webcam frame-to-result latency on the USER's low-end laptop without breaking accepted motion semantics or Unity frame stability?

## Non-negotiable invariants

- Keep all work on `engine/pose-tracking-spike`.
- Do not merge to `main`; no PR merge or force push.
- Stock MediaPipe/TFLite CPU must remain fully usable as the known-safe fallback.
- OpenVINO starts as an experimental/selectable backend, not an unconditional replacement.
- Reuse mature MediaPipe preprocessing, detector decode/NMS, ROI generation/tracking, landmark decode/refinement, visibility/presence, world-landmark and projection semantics.
- Do not manually recreate those semantics in C#.
- Preserve existing canonical mapping, stabilization, confidence, calibration, retargeting, locomotion and presentation behavior.
- Do not refactor current fixed 33-landmark `PoseObservation` or current 20-joint canonical topology during this performance spike.
- Keep body inference input 320x240 and full-resolution camera/display for the initial A/B test.
- Preserve latest-useful-frame scheduling: max one active readback, one replaceable prepared frame and one outstanding inference; no backlog/history/replay/catch-up queue.
- Preserve DirectCPU fallback/readback and teardown safety unless the architecture makes a narrowly justified change necessary.
- Do not densify/convert the detector.
- Do not force D3D12 globally.
- Do not ship Gate B shadow-TFLite/raw-parity diagnostics in the runtime benchmark path.
- Do not touch USER-owned `PoseTrackingSpike.unity` scene YAML merely for convenience.
- Known local dirty files may include `Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpike.unity`, `GoldenNeedle.slnx`, `ProjectSettings/SceneTemplateSettings.json`, and legitimate package-lock changes. Never clean/reset/stage them indiscriminately.
- Phase 5A remains **NOT USER ACCEPTED**. Phase 6 remains **NOT STARTED**.

## Development flow

Work normally and continuously. Use checkpoints as durable Git/recovery markers:

### U1 — Resolve architecture

Inspect the actual production/provider/native boundary before broad implementation. At minimum inspect:
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipePoseProvider.cs`
- `MediaPipeCanonicalPoseSource.cs`
- `PoseObservation.cs`
- relevant `Debug/PoseTrackingSpike` setup/presenter code
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- actual Homuler/MediaPipeUnityPlugin native binaries, managed bindings and native build layout
- PoseLandmarker construction/callback path
- readback/latest-frame scheduler and existing telemetry
- Gate B OpenVINO calculator and build/overlay scripts
- exact pinned Homuler v0.16.3 / MediaPipe v0.10.22 native source/build path.

Choose the least-invasive architecture. Prefer:
1. rebuild/extend the existing Homuler/MediaPipe native task boundary with an explicit selectable OpenVINO inference path;
2. a small dedicated native bridge only if option 1 is impractical and mature MediaPipe semantics remain inside the native side;
3. reject manual detector/ROI/landmark/world-semantic reconstruction absent proof no reusable path exists.

Resolve:
- exact native binary Unity loads today;
- exact managed binding/API path;
- where backend selection enters;
- whether explicit C API/export, graph option, config object, or other mechanism is best;
- runtime DLL/package layout for Windows x86_64;
- license/notice implications;
- rollback/fallback behavior.

When U1 is coherent, update the rolling progress file and commit/push the checkpoint. **Then keep going into U2.**

### U2 — Implement native OpenVINO backend

Port the proven Gate B inference concept into the practical native build path:
- same current MediaPipe generation;
- detector + landmark inference replaced only when OpenVINO backend selected;
- OpenVINO explicit CPU + FP32 + latency-oriented configuration;
- exact production model identity/semantics preserved;
- no shadow TFLite diagnostic in benchmark build;
- stock TFLite path remains intact;
- explicit error/backend identity, no silent false-positive OpenVINO mode;
- narrow Windows x86_64 runtime dependency packaging.

Use proportionate compile/build verification. Avoid a giant clean rebuild when an incremental build or source-level proof is sufficient, but do perform the native build needed to establish a real usable artifact when possible.

When U2 reaches a coherent recoverable state, update the progress file and commit/push. If no USER-local intervention is required, **continue into U3 immediately**.

If a long native build must run on the USER machine and cannot be completed by you, stop only at that genuine handoff and return exact commands plus expected outputs. Do not create an artificial approval pause.

### U3 — Wire Unity selection and telemetry

Integrate into the existing provider without changing downstream semantics.

Requirements:
- default stock TFLite until USER acceptance changes policy;
- OpenVINO selectable explicitly for A/B;
- effective backend + init/failure visible in diagnostics;
- same camera/orientation/320x240/latest-frame path;
- same `PoseObservation` publication and existing consumers;
- same world-landmark and partial-body behavior;
- safe initialization/restart/teardown;
- no scene YAML editing if runtime/editor setup can be code-driven or configured through existing objects.

A/B telemetry should expose, with low logging overhead:
- selected/effective backend;
- camera capture rate;
- readback latency;
- accepted request → result latency;
- capture/frame → published result latency;
- fresh pose results per second;
- prepared-frame replacement/drop pressure;
- Unity render FPS/frame time;
- OpenVINO detector/landmark timings if cheaply available;
- native/managed bridge overhead if measurable.

Compile and perform any non-physical verification available through source/build/Unity tooling. Update progress and checkpoint commit when coherent.

Then proceed as far as possible into runtime-test preparation.

### U4 — Mandatory USER runtime A/B handoff

This is normally where you stop and return instructions, because the USER must physically test webcam motion and visual behavior.

Prepare a concise A/B procedure comparing stock TFLite vs OpenVINO under identical scene/camera/physical conditions.

Baseline reference:
- camera ~29–30 FPS;
- fresh pose results ~10–12/s;
- DirectCPU readback ~55–65 ms;
- frame-to-result commonly ~110–140 ms.

Test fast arms/reaches, torso movement, legs/knees where visible, and partial-body/occlusion recovery. The USER—not the Builder—owns visual/physical acceptance.

Do not claim U4 PASS yourself.

### U5 — After USER evidence only

Cleanup, docs, licensing and production/default recommendation happen after Orchestrator/USER review of U4. Do not start Phase 6 automatically.

## Checkpoint / execution-limit discipline

The purpose of checkpoints is continuity, not bureaucracy.

At each coherent milestone/sub-milestone:
1. ensure the repository is in a recoverable state;
2. update `Docs/openvino-unity-integration-progress.md`;
3. make a focused commit and push to `engine/pose-tracking-spike` when appropriate;
4. continue development immediately.

If execution budget is getting low, do not start another broad refactor. First checkpoint the current state and update the rolling progress file with the exact next action.

If the execution limit interrupts unexpectedly, the next Builder must be able to recover from remote Git + the progress file.

## Git authorization for this handoff

You are authorized to create focused commits and push them to `engine/pose-tracking-spike` as checkpoint/recovery commits for this integration work.

You are **not** authorized to:
- merge to `main`;
- force-push;
- rewrite/rebase/reset shared history;
- merge/open a PR unless later instructed;
- discard USER-local changes.

## Final/stop report format

When you genuinely stop—because U4 USER QA is ready, a real blocker needs a decision, or execution limit is near—return one detailed report containing:

- exact starting remote SHA;
- exact final remote SHA;
- checkpoints/sub-steps completed;
- files changed/added;
- architecture chosen and why;
- implementation summary;
- native build/package result;
- Unity compile/load verification result;
- telemetry added;
- tests/commands run and results;
- local/generated artifacts the USER must know about;
- unresolved risks/blockers;
- exact next action or USER QA steps;
- confirmation that `main` was not merged and Phase 6 was not started.

If you stopped due execution-limit risk, explicitly say **CONTINUE FROM HERE** and point to the exact section in `Docs/openvino-unity-integration-progress.md` that records the next action.
