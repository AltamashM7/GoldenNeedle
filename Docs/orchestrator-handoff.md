# Golden Needle — Orchestrator Handoff

## 1. Governance

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

Latest substantive runtime/code checkpoint before this documentation refresh:
`3584e4a059937dfa80b278fe2d96334344025b76` — `feat: add landmark-first sentis spike audit`.

There are documentation-only commits after that checkpoint. Always fetch and verify the current remote branch HEAD before doing new work.

Hard rules:
- do **not** merge to `main` without explicit USER approval;
- do not force-push or rewrite history merely to clean experimental commits;
- preserve USER-local dirty files unless the USER explicitly asks otherwise;
- current known USER-local dirty files have repeatedly been:
  - `M Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpike.unity`
  - `M GoldenNeedle.slnx`
  - `?? ProjectSettings/SceneTemplateSettings.json`;
- use the intelligent Web Builder for broad architecture/inference-runtime work;
- use Luna only for tightly bounded implementation, instrumentation or read-only diagnostics;
- USER performs manual Unity runtime QA unless a worker explicitly has safe Unity access.

## 2. Current project status

- Phase 1 provider/raw overlays: PASS WITH NOTES.
- Phase 2 canonical skeleton/debug: PASS.
- Phase 3 stabilization/confidence: PASS.
- Phase 4 humanoid retargeting/calibration/orientation: **USER ACCEPTED — PASS**.
- Phase 5A locomotion/presentation: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.
- Current active work is a responsiveness / inference-architecture investigation.

Do not mark Phase 5A accepted and do not start Phase 6 until USER explicitly does so.

## 3. Accepted production motion path — keep frozen during inference experiments

Unity baseline:
- Unity 6.5 / `6000.5.0f1`;
- URP 17.5.0;
- MediaPipeUnityPlugin 0.16.3;
- Windows experimental environment.

Camera/source:
- camera request about 640x480@30 where supported;
- external camera selection through Unity WebCamDevice;
- Inspector dropdown plus `V` cycle;
- source change invalidates calibration/Phase 5 assumptions;
- after source change: `C` recalibrate then `K` recenter;
- orientation Auto / 0 / 90 / 180 / 270;
- front-camera metadata does not automatically mirror inference;
- Display Mirror presentation-only.

Canonical/retarget:
- canonical +X right, +Y up, +Z away;
- MediaPipe world conversion `(x,-y,z)` pelvis-relative;
- calibration right = rightShoulder-leftShoulder;
- up = chest-pelvis;
- forward = Cross(right,up);
- accepted Phase 4 humanoid orientation fix;
- modular torso/arms/legs calibration;
- partial-body operation accepted;
- F3 canonical debug upstream of mapping;
- F6 orientation/basis diagnostics;
- swing-only limb alignment rather than axial twist.

Phase 3 stabilization baseline:
- One Euro min 1.0, beta 0.05, deriv 1.0;
- acquire 0.60;
- sustain 0.40;
- samples 2;
- grace 0.10 s;
- reset 0.25 s.

Phase 5A implementation pending USER acceptance:
- support-foot locomotion v3;
- physical scales ~0.9 lateral / 1.5 depth;
- cadence logic;
- mapped body heading;
- safe recenter;
- avatar root X/Z locomotion only;
- F12 Lab/Game switching;
- third-person presentation camera;
- presentation-only humanoid render smoothing ~45/s, max blend 0.05 s.

## 4. Production CPU pose pipeline

Production provider remains MediaPipe Pose Landmarker Lite CPU.

Model:
`Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes`

Current runtime architecture:

```text
camera/latest frame
-> body-only 320x240 preparation
-> DirectCPU readback when enabled (Homuler fallback remains)
-> MediaPipe Pose Landmarker Lite CPU
-> PoseObservation
-> canonical/stabilization/calibration/retarget/locomotion
```

Scheduling invariants:
- <=1 active readback;
- <=1 replaceable prepared TextureFrame;
- <=1 outstanding MediaPipe inference;
- latest useful frame wins;
- no camera-frame history;
- no inference backlog;
- no pose replay/catch-up queue.

### Accepted optimization results

320x240 body path:
- modest ~8–9% throughput gain;
- ~7–8 ms frame-to-result improvement;
- no obvious quality collapse;
- keep for body tracking.

Immediate Launch After Readback:
- OFF prep->launch ~25.7 ms, frame->result ~150.4 ms;
- ON prep->launch ~0.4 ms, frame->result ~117.7 ms;
- roughly one render-frame artificial delay removed;
- **USER accepted, keep ON**.

DirectCPU readback:
- modest readback and frame-to-result gain;
- example ~58.1 -> 54.7 ms readback and ~120.7 -> 112.0 ms frame-to-result;
- keep as CPU optimization/fallback path.

DirectCPU teardown lifetime guard:
- active direct request tracked;
- teardown/restart waits safely only when needed;
- USER saw expected diagnostic and no crash/hang.

## 5. CPU scheduling optimization is closed

Readback callback timing USER measurement:
- callback->poll median ~0.6–0.9 ms;
- p95 ~1.3–1.6 ms;
- poll->publish ~0 ms.

Conclusion: callback polling not worth optimizing.

Inference-completion->next-launch measurement:
- steady state usually preparedWaiting `0/64`;
- next launches essentially all readback-continuation;
- rare large delays had very low prevalence.

Conclusion: do not add callback-driven `DetectAsync`; no meaningful steady-state win.

MediaPipe CPU audit:
- no supported public Pose Landmarker CPU thread-count knob;
- no supported high-level XNNPACK thread control;
- Lite is already the lightest compatible official model family;
- detector fixed 224x224 input;
- landmark fixed 256x256 input;
- lower upstream body image does not shrink those tensors;
- LIVE_STREAM remains appropriate;
- Homuler distributed Windows GPU mode is not the answer.

This closes small scheduling tweaks, not broader inference-runtime research.

## 6. User-visible problem that motivates the new architecture track

USER reports fast movement is not reproduced faithfully unless moving somewhat more slowly.

Current production behavior commonly observed before alternate-runtime work:
- camera ~29–30 FPS;
- fresh pose results ~10–12/s;
- DirectCPU readback ~55–65 ms;
- MediaPipe accepted-request->callback ~60–75+ ms;
- frame-to-result often ~110–140 ms.

At ~10–12 fresh pose samples/s, fast trajectories can be undersampled; some intermediate/extreme poses may never be observed.

Primary goals:
1. lower real end-to-end latency;
2. raise actual fresh pose sampling frequency;
3. preserve current 33-landmark/world-landmark behavior;
4. keep a safe CPU fallback;
5. use CPU and GPU heterogeneously according to measured strengths.

Do not confuse "use CPU+GPU together" with running duplicate pose inference on both every frame.

## 7. Actual low-end proof machine

Confirmed by USER runtime:
- Windows 10 build 19045 x64;
- Intel Core i3-7100U @ 2.40 GHz;
- 4 logical processors;
- Intel HD Graphics 620;
- no discrete GPU on this machine;
- Unity reports ~4047 MB graphics/shared memory;
- compute shaders true;
- D3D11 and D3D12 both usable in Editor sessions.

Treat this as the current low-end target. Stronger/discrete GPUs may justify different backend choices.

## 8. Sentis / Unity InferenceEngine spike

Package:
- `com.unity.ai.inference` 2.6.1.

Experimental code only:
- `Assets/GoldenNeedle/Debug/GpuInferenceSpike/`
- `Assets/GoldenNeedle/Editor/GpuInferenceSpike*.cs`

Production provider/avatar are not wired to Sentis output.

### Exact extracted models

Production task bundle:
- size 5,777,746 bytes;
- SHA-256 `59929e1d1ee95287735ddd833b19cf4ac46d29bc7afddbbf6753c459690d574a`.

Detector:
- `pose_detector.tflite`;
- 2,959,078 bytes;
- SHA-256 `46837eb883e6ec75b52c5f5ff6a9b78bd35e66c13f95e8c3566c582d146cb1d9`;
- input `[1,224,224,3]` float32;
- outputs `[1,2254,12]` and `[1,2254,1]`.

Landmark model:
- `pose_landmarks_detector.tflite`;
- 2,818,390 bytes;
- SHA-256 `ad6cfd3c903eb31a4ee788b809e45ecf9fa69923b69b9f3f2d9ae616ff433e58`;
- input `[1,256,256,3]` float32;
- outputs:
  - `[1,195]`;
  - `[1,1]`;
  - `[1,256,256,1]`;
  - `[1,64,64,39]`;
  - `[1,117]`.

No ONNX conversion was used in the Sentis spike.

## 9. Detector DENSIFY blocker is understood

Exact detector import into Sentis 2.6.1 fails with:
`Model contains unsupported operator(s): DENSIFY`.

Enhanced audit found:
- 442 tensors;
- 291 ops;
- 38 `DENSIFY@v1` ops;
- every DENSIFY input is a static constant with no producer;
- every one has sparsity metadata;
- audited outputs feed ordinary builtin ops;
- sparse value bytes: 1,361,790;
- sparse index metadata payload ~1,028,345 bytes;
- sparse values + metadata payload ~2,390,135 bytes;
- estimated dense value bytes 5,447,168.

Audit conclusion:
`STATIC_STORAGE_REWRITE_CANDIDATE_IN_PRINCIPLE`.

Meaning:
- offline densification is plausible as a storage-format rewrite;
- it is **not** approved yet;
- any rewritten detector must pass numerical equivalence before use;
- do not spend effort on detector surgery until an inference backend proves worthwhile.

## 10. Landmark Sentis import and numerical behavior

The exact landmark TFLite imports successfully in Sentis.

Deterministic zeros/gradient/seeded-random CPU-vs-GPU runs:
- no NaN/Inf values;
- small landmark/world-related outputs showed small numerical differences;
- large segmentation-like output showed larger isolated max differences on synthetic input.

This is only Sentis backend consistency, **not** an independent Google LiteRT equivalence proof and not full MediaPipe semantic equivalence.

## 11. D3D11 Sentis benchmark — USER result

Configuration:
- Intel HD 620;
- Direct3D11;
- Sentis 2.6.1;
- OBS was running during most of the test;
- GPU utilization was visible during benchmark phases.

Landmark results:
- CPU mean 64.64 ms, p50 62.43, p95 96.39, p99 112.24, 15.47/s;
- GPUCompute fixed tensor mean 123.40 ms, p50 121.11, p95 154.08, p99 170.19, 8.10/s;
- GPU-resident path mean 251.36 ms, p50 252.65, p95 306.81, p99 323.07, 3.98/s;
- selected-output readback 1,252 bytes;
- TextureConverter submit mean ~0.135 ms;
- frame mean ~14.83 ms, p95 ~73.53 ms.

Additional diagnostics:
- repeated `JobTempAlloc` lifetime warnings appeared.

Interpretation:
- D3D11 Sentis GPUCompute loses badly to CPU.
- Cross-run comparisons with D3D12 must account for OBS/load differences.

## 12. D3D12 Sentis benchmark — USER result

Unity was launched with temporary `-force-d3d12` and OBS was closed.

Confirmed:
- Graphics API Direct3D12;
- Intel HD 620;
- compute shaders true;
- Sentis 2.6.1.

Landmark results:
- CPU: mean **46.24 ms**, p50 45.60, p95 56.96, p99 64.67, **21.63/s**;
- GPUCompute fixed tensor: mean **60.48 ms**, p50 60.02, p95 64.00, p99 64.88, **16.53/s**;
- GPU-resident: mean **127.96 ms**, p50 125.76, p95 139.80, p99 145.04, **7.82/s**;
- selected-output readback 1,252 bytes;
- TextureConverter submit mean ~0.136 ms;
- frame mean ~13.98 ms, p95 ~36.78 ms.

Within the same clean D3D12 run:
- fixed-tensor GPU mean latency is ~31% worse than CPU;
- fixed-tensor GPU throughput is ~24% lower than CPU;
- GPU-resident path is far below the desired 15–20+/s gate.

Repeated messages also appeared:
`d3d12: failed to wait for fence (258)`.

Do not normalize these as harmless production behavior.

### Sentis decision

**Reject Sentis GPUCompute as the HD 620 neural-inference backend.**

Do NOT conclude "GPU acceleration is impossible".
The correct conclusion is narrower:
- on this low-end iGPU, Sentis GPUCompute is not a good workload assignment;
- CPU is faster for the landmark network;
- D3D12 additionally showed fence-timeout diagnostics;
- keep GPU focused on rendering/other work unless another inference runtime proves better.

Important positive result:
- Sentis CPU achieved ~21.63 raw landmark inferences/s.

This is faster than current full MediaPipe fresh-result throughput, but it excludes detector cadence, ROI tracking, landmark decoding/projection, confidence/world semantics and full camera pipeline. It is evidence of headroom, not a ready replacement.

## 13. Heterogeneous architecture philosophy after Sentis

The architecture should be capability-driven.

Low-end HD 620-class configuration may ultimately be:
- GPU: rendering, visual effects, camera/preprocess operations that benchmark well;
- CPU: neural pose inference if CPU remains faster, plus ROI/tracking/postprocess/canonical/retarget/game logic.

Stronger/discrete GPU configuration may eventually use GPU inference if a backend benchmark proves it wins.

Possible future product policy:
- Auto;
- CPU;
- Accelerated.

Auto should probe support and choose the lowest-latency stable backend rather than assuming GPU is always faster.

## 14. Next approved architecture experiment — OpenVINO

Next task for a **fresh intelligent Web Builder**:
perform an isolated OpenVINO benchmark with the exact landmark TFLite, comparing CPU and Intel GPU on the actual laptop.

This is the next approved direction after the Sentis decision.

The next Orchestrator must independently verify current OpenVINO 2026 documentation/device support before writing the Builder brief. Do not blindly inherit earlier Luna statements.

Desired experiment shape:
- outside the production Unity pose provider first;
- exact current landmark TFLite if supported by current OpenVINO frontend;
- measure OpenVINO CPU and Intel GPU separately;
- do not start with AUTO/HETERO because isolated attribution matters;
- verify actual selected device;
- warmup excluded;
- hundreds of measured runs if practical;
- mean/p50/p95/p99 and inferences/s;
- input/output metadata;
- required output-transfer cost;
- CPU/GPU utilization where practical;
- no detector densification yet;
- no production avatar integration yet.

Decision logic:
- if OpenVINO GPU materially beats ~46 ms / 21.63/s raw Sentis CPU and is stable, continue Intel GPU route;
- if OpenVINO CPU materially beats current alternatives, consider it as a low-end CPU inference candidate;
- if neither wins meaningfully, stop spending time on HD 620 neural GPU acceleration and reserve accelerated inference for stronger hardware.

## 15. Worker-role guidance

Use the **Web Builder** for:
- new inference-runtime architecture;
- provider abstraction changes;
- OpenVINO integration/benchmark architecture;
- broad multi-file refactors.

Use **Luna** only for:
- tightly scoped diagnostics;
- instrumentation;
- small helpers/tests;
- read-only audits;
- narrowly specified follow-up fixes.

Do not delegate broad architecture decisions to Luna without Orchestrator verification.

## 16. Immediate next steps for the new Orchestrator

1. Fetch and verify current `engine/pose-tracking-spike` remote HEAD.
2. Read `Docs/current-state.md` and this file as primary durable context.
3. Confirm the documentation-only checkpoint and ensure no USER dirty files were touched.
4. Independently research current OpenVINO support for:
   - Windows 10;
   - Intel HD 620 / Gen9 Intel graphics;
   - exact TFLite frontend support;
   - CPU and GPU device plugins;
   - Python/C++ benchmarking practicality;
   - redistribution/licensing considerations for a later Unity integration.
5. Prepare a narrow Web Builder brief for an isolated exact-landmark OpenVINO CPU-vs-Intel-GPU benchmark.
6. Do not touch production pose provider, canonical, retargeting or locomotion in that benchmark.
7. Do not densify/convert the detector yet.
8. After OpenVINO results, choose whether to continue alternate-runtime work or return to Phase 5A final USER QA.

## 17. Do not do these without new evidence/approval

- no merge to `main`;
- no Phase 5A acceptance claim;
- no Phase 6 start;
- no callback-thread `DetectAsync` launch;
- no further callback/poll micro-optimization;
- no global D3D12 production switch based on the spike;
- no detector densification simply because audit says it is possible;
- no production Sentis provider integration on HD 620;
- no duplicate CPU+GPU inference every frame;
- no removal of the existing CPU MediaPipe fallback;
- no lowering model quality just to hit an FPS target.

The current priority is evidence-driven reduction of latency and improvement of fast-motion fidelity while preserving the accepted motion engine.