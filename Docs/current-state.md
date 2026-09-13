# Golden Needle — Current State

Last substantive runtime/code checkpoint before this documentation refresh:
`3584e4a059937dfa80b278fe2d96334344025b76` — `feat: add landmark-first sentis spike audit`.

Working branch: `engine/pose-tracking-spike`.

Always verify the current remote branch HEAD before new implementation work because documentation-only commits may exist after the runtime/code checkpoint above.

## Governance and branch policy

- Repository: `AltamashM7/GoldenNeedle`.
- Active development branch: `engine/pose-tracking-spike`.
- Do **not** merge to `main` without explicit USER approval.
- Do not force-push or rewrite branch history merely to clean experimental/checkpoint commits.
- GitHub Desktop is the USER's normal Git workflow.
- Known USER-local dirty files that have repeatedly existed and must not be reverted, cleaned, staged or overwritten casually:
  - `M Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpike.unity`
  - `M GoldenNeedle.slnx`
  - `?? ProjectSettings/SceneTemplateSettings.json`
- Unity/package resolution may also create legitimate USER-local package-lock changes; inspect before touching them rather than assuming they are disposable.

## Phase status

- Phase 1 — MediaPipe provider/raw overlays: **PASS WITH NOTES**.
- Phase 2 — canonical skeleton/debug: **PASS** after orientation correction.
- Phase 3 — stabilization/confidence: **PASS**.
- Phase 4 — humanoid retargeting/calibration/orientation: **USER ACCEPTED — PASS**.
- Phase 5A — support-foot locomotion / Lab-Game presentation work: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.
- Current optimization track: responsiveness / high-speed-motion fidelity. CPU scheduling optimization is closed; GPU/alternate-inference-runtime feasibility is being investigated.

## Accepted production motion-engine behavior

The accepted production path remains the reference and must not be casually disturbed by experimental inference work.

### Camera and source

- Unity 6.5 (`6000.5.0f1`) with URP 17.5.0.
- MediaPipeUnityPlugin 0.16.3.
- Windows experimental environment.
- Camera request approximately `640x480 @ 30` where supported.
- External cameras are selected through Unity `WebCamDevice`; Inspector dropdown plus `V` cycle are available.
- Camera switching is safe/pending and invalidates source-dependent calibration/Phase 5 assumptions.
- After source change: recalibrate with `C`, then recenter with `K`.
- Orientation modes Auto / 0 / 90 / 180 / 270 affect inference/display as implemented.
- Front-facing metadata does not automatically mirror inference.
- Display Mirror is presentation-only.
- Full-resolution camera/display texture is retained independently from the body-pose inference resolution.

### Canonical and retarget semantics

- Canonical image/world convention uses the correctly oriented inference frame.
- Canonical 3D: +X camera/view right, +Y up, +Z away.
- MediaPipe world conversion uses `(x, -y, z)`, pelvis-relative.
- Calibration body basis:
  - right = rightShoulder - leftShoulder;
  - up = chest - pelvis;
  - forward = Cross(right, up).
- `HumanoidRetargetingMath` / `HumanoidRetargeter` map stabilized canonical positions through the signed canonical-to-avatar basis and then torso/analytic IK.
- Phase 4 uses swing-only limb alignment rather than axial twist.
- F3 canonical debug remains upstream of humanoid mapping.
- F6 provides orientation/basis/target diagnostics.
- Modular calibration supports torso/arm/leg segments and partial-body operation.
- Phase 4 Animator Humanoid orientation was corrected and USER accepted.

### Stabilization and confidence

Accepted Phase 3 baseline:
- One Euro filter: min 1.0, beta 0.05, derivative 1.0.
- acquire confidence 0.60;
- sustain confidence 0.40;
- acquire samples 2;
- grace 0.10 s;
- reset 0.25 s.

Do not change these merely to hide inference latency.

### Phase 5A frozen implementation pending final USER acceptance

- support-foot locomotion v3;
- physical scales approximately 0.9 lateral / 1.5 depth;
- cadence logic;
- mapped body heading;
- safe recenter;
- avatar root X/Z locomotion only;
- `K` makes the current physical location the new tracking origin while preserving virtual X/Z without a jump;
- F12 switches Lab/Game presentation;
- third-person Lab/Game camera behavior is implemented;
- Humanoid render-rate smoothing is presentation-only after the exact Phase 4 solve, defaults 45/s with max blend 0.05 s.

Phase 5A is still **not USER accepted**.

## Production CPU pose architecture

Current production provider remains MediaPipe Pose Landmarker Lite on CPU.

Model:
`Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes`

Production goals/constraints:
- Pose Landmarker Lite;
- one pose;
- segmentation disabled;
- world landmarks required;
- target inference request rate 30 FPS;
- body-only inference target 320x240 by default;
- full camera/display path remains native resolution;
- at most one readback, one replaceable prepared frame and one outstanding inference;
- latest useful frame wins;
- no camera-frame history;
- no inference backlog;
- no pose replay/catch-up queue.

### Body inference downscale

The current body-only path defaults to 320x240 while preserving the full camera/display texture.

USER native-vs-scaled evidence showed only a modest but repeatable improvement, roughly:
- throughput gain around 8–9%;
- frame-to-result improvement around 7–8 ms / ~5%;
- no obvious tracking-quality collapse.

Keep 320x240 for the current body path; do not assume that dropping to 160x120 will materially reduce neural inference because the underlying model uses fixed neural tensor sizes.

### Immediate Launch After Readback

Accepted USER A/B result:
- OFF: prepared-to-launch roughly 25.7 ms and frame-to-result around 150.4 ms;
- ON: prep-to-launch roughly 0.4 ms and frame-to-result around 117.7 ms;
- launch origin changes from Update to readback continuation in the normal fast path;
- roughly one render-frame of artificial scheduling latency was removed.

Status: **USER-runtime accepted; keep ON**.

### Direct Body CPU Readback

The DirectCPU path bypasses Homuler's extra temporary-RT / Texture2D LoadRawTextureData / Apply sequence for the CPU Pose Landmarker path.

The accepted path supports the required H/V flip through a persistent staging RenderTexture, then reads directly into the pooled TextureFrame CPU buffer.

USER A/B showed a modest improvement, approximately:
- readback ~58.1 ms OFF vs ~54.7 ms ON;
- frame-to-result ~120.7 ms OFF vs ~112.0 ms ON;
- no DirectCPU failures in that run.

Status: **USER-runtime PASS as a modest optimization; retain it for the CPU fallback**.

The serialized code default may still be OFF even though the USER test configuration enables it; do not claim the serialized default is ON without checking the current source/scene.

### DirectCPU teardown guard

A real lifetime hazard existed because `RequestIntoNativeArray` writes into TextureFrame-owned memory while pool disposal could otherwise occur during an in-flight request.

A teardown guard now tracks the active direct request and waits only during teardown/restart when necessary. USER observed the expected stop-time diagnostic and no reported crash/hang/resource error.

## CPU scheduling diagnostics — closed lines of investigation

### Readback callback polling

USER measurement showed approximately:
- callback-to-poll median ~0.6–0.9 ms;
- callback-to-poll p95 ~1.3–1.6 ms;
- poll-to-publish ~0 ms.

Conclusion: callback/coroutine polling is not worth optimizing.

### Inference-completion to next launch

Instrumentation measured whether a prepared frame was already waiting when MediaPipe inference completed.

Steady-state USER evidence showed prepared-waiting frames were usually `0/64`; next launches were essentially all readback-continuation launches. A rare prepared-waiting sample could have a large delay, but prevalence was around only a few percent in the sampled region.

Conclusion: result-callback-driven inference launch is not a meaningful steady-state optimization. Do **not** call `DetectAsync` from the uncertain MediaPipe callback thread without fundamentally new evidence.

### MediaPipe CPU tuning audit

Read-only audit established:
- no supported high-level Pose Landmarker option for CPU thread count;
- no supported direct XNNPACK thread knob through the current Tasks API;
- Lite is already the lightest compatible official model family used by this project;
- detector input is 224x224 and landmark input is 256x256 internally;
- lowering the upstream 320x240 image further does not shrink those fixed neural tensors;
- LIVE_STREAM remains the appropriate task mode for the current architecture;
- Homuler's distributed Windows GPU path is not a safe production answer.

That result closed further small CPU-scheduling tweaks, but it did **not** mean the whole laptop/GPU had reached its absolute performance limit.

## Why the heterogeneous CPU+GPU track exists

The USER reports that fast body/arm movements are not faithfully reproduced unless movements are performed somewhat more slowly.

Current production observations before the alternate-runtime experiments were commonly:
- camera ~29–30 FPS;
- fresh pose results around 10–12/s;
- DirectCPU readback roughly 55–65 ms;
- MediaPipe accepted-request-to-result callback roughly 60–75+ ms;
- frame-to-result often around 110–140 ms depending on load.

At only 10–12 fresh pose samples/s, fast limb trajectories can move significantly between inference results. This is not merely display smoothing; the system may never observe some intermediate/extreme poses.

The architecture goal is therefore:
1. reduce real frame-to-result latency;
2. raise actual fresh pose sampling frequency;
3. preserve the accepted 33-landmark/world-landmark behavior and quality;
4. use CPU and GPU heterogeneously, not as mutually exclusive marketing modes.

The intended philosophy is game-like workload assignment: use each processor for work it actually performs well. Do not run duplicate CPU+GPU inference on every frame just to claim both processors are active.

## Actual low-end proof machine

USER hardware established during the GPU spike:
- Windows 10 build 19045, 64-bit;
- Intel Core i3-7100U @ 2.40 GHz;
- 2 cores / 4 logical processors;
- Intel HD Graphics 620;
- no discrete GPU on this machine;
- Unity reports about 4047 MB graphics/shared memory;
- compute shaders supported;
- D3D11 and D3D12 both available in the tested Editor sessions.

This machine is intentionally treated as a low-end proof target. Stronger/discrete GPUs may justify a different inference backend later.

## Sentis / Unity InferenceEngine architecture spike

Experimental package:
- `com.unity.ai.inference` 2.6.1.

Experimental code is isolated under:
`Assets/GoldenNeedle/Debug/GpuInferenceSpike/`

Editor setup lives under:
`Assets/GoldenNeedle/Editor/GpuInferenceSpike*.cs`

The production provider/canonical/retarget/locomotion path is not connected to these neural outputs.

### Exact model extraction

The setup extracts exact byte-for-byte submodels from the production task bundle into an ignored local `Generated/` area.

Verified exact bundle/submodel data from the USER-generated audit:
- task bundle size: 5,777,746 bytes;
- bundle SHA-256: `59929e1d1ee95287735ddd833b19cf4ac46d29bc7afddbbf6753c459690d574a`;
- `pose_detector.tflite`: 2,959,078 bytes, SHA-256 `46837eb883e6ec75b52c5f5ff6a9b78bd35e66c13f95e8c3566c582d146cb1d9`;
- `pose_landmarks_detector.tflite`: 2,818,390 bytes, SHA-256 `ad6cfd3c903eb31a4ee788b809e45ecf9fa69923b69b9f3f2d9ae616ff433e58`.

No ONNX conversion was used in the Sentis spike.

### Exact landmark model contract observed by audit

Input:
- float32 `[1,256,256,3]`.

Outputs:
- `[1,195]` — landmark-related output;
- `[1,1]` — presence/score-like output;
- `[1,256,256,1]` — large segmentation-like output;
- `[1,64,64,39]` — large refinement/heatmap-like output;
- `[1,117]` — world-landmark-related output.

The exact MediaPipe semantic decoding of every raw tensor is not yet reconstructed in Golden Needle. Raw neural throughput must not be claimed as full production pose throughput.

## Sentis detector import result and DENSIFY audit

The exact detector TFLite fails Sentis 2.6.1 import with:
`Model contains unsupported operator(s): DENSIFY`.

This is an importer/model-storage limitation and occurs before GPU execution, so it is **not** evidence that the HD 620 cannot execute the detector.

The landmark TFLite imports successfully and can run through Sentis CPU and GPUCompute.

Enhanced exact detector audit established:
- 442 tensors;
- 291 operators;
- 38 `DENSIFY@v1` operators;
- all 38 DENSIFY inputs are static constants with no producer op;
- all 38 have sparsity metadata;
- all audited DENSIFY outputs feed ordinary builtin consumers;
- dense storage estimates are known;
- sparse stored value bytes: 1,361,790;
- sparse index metadata payload: approximately 1,028,345 bytes;
- combined sparse values + index payload: approximately 2,390,135 bytes;
- estimated dense value bytes: 5,447,168 bytes.

Audit conclusion:
`STATIC_STORAGE_REWRITE_CANDIDATE_IN_PRINCIPLE`.

This means offline densification looks like a plausible storage-format rewrite in principle, **not** that it is already approved or numerically proven. A later equivalence gate would be mandatory before using a rewritten detector.

Do not spend effort densifying the detector until an inference backend demonstrates a worthwhile performance win.

## Sentis landmark benchmark — Direct3D11 USER result

First landmark-only benchmark ran in Unity 6.5 with:
- Intel HD Graphics 620;
- Direct3D11;
- Sentis/InferenceEngine 2.6.1;
- compute shaders true.

USER reported OBS was running during most of this benchmark and GPU utilization was visible throughout the phases, so cross-run comparison with later tests must be treated cautiously.

Measured recurring landmark network results:
- Sentis CPU: mean 64.64 ms, p50 62.43 ms, p95 96.39 ms, p99 112.24 ms, 15.47/s;
- GPUCompute fixed tensor: mean 123.40 ms, p50 121.11 ms, p95 154.08 ms, p99 170.19 ms, 8.10/s;
- GPU-resident path: mean 251.36 ms, p50 252.65 ms, p95 306.81 ms, p99 323.07 ms, 3.98/s;
- GPU-resident selected-output readback: 1,252 bytes;
- TextureConverter submission mean ~0.135 ms;
- diagnostic render frame mean ~14.83 ms, p95 ~73.53 ms.

The D3D11 GPU path lost decisively to CPU.

During this run Unity also reported several temporary-allocation lifetime warnings, including `JobTempAlloc has allocations that are more than the maximum lifespan of 4 frames old`. Record this as diagnostic-spike behavior; do not normalize it as acceptable production behavior.

## Sentis landmark benchmark — Direct3D12 USER result

A second run launched Unity with a temporary `-force-d3d12` session. The screenshot/report confirms:
- Graphics API: Direct3D12;
- Intel HD Graphics 620;
- compute shaders true;
- Sentis/InferenceEngine 2.6.1.

Measured results:
- Sentis CPU: mean 46.24 ms, p50 45.60 ms, p95 56.96 ms, p99 64.67, **21.63/s**;
- GPUCompute fixed tensor: mean 60.48 ms, p50 60.02 ms, p95 64.00 ms, p99 64.88, **16.53/s**;
- GPU-resident path: mean 127.96 ms, p50 125.76 ms, p95 139.80 ms, p99 145.04, **7.82/s**;
- GPU-resident selected-output readback: 1,252 bytes;
- TextureConverter submission mean ~0.136 ms;
- diagnostic render frame mean ~13.98 ms, p95 ~36.78 ms.

Within the same D3D12 run, CPU still beats GPUCompute decisively:
- GPU fixed-tensor mean latency is ~31% worse than Sentis CPU;
- GPU fixed-tensor throughput is ~24% lower than Sentis CPU;
- GPU-resident roundtrip is far below the required 15–20+/s gate.

The D3D12 run also emitted repeated:
`d3d12: failed to wait for fence (258)`
messages. Treat this as an additional stability warning for this low-end iGPU/Editor path, not as acceptable production noise.

Cross-run D3D11-vs-D3D12 numbers are affected by different runtime load/OBS conditions, so do not attribute every improvement purely to the graphics API. The decisive evidence is the **same-run CPU-vs-GPU comparison** in D3D12.

## Sentis CPU/GPU numerical comparison

For deterministic zero, gradient and seeded-random inputs:
- all tested landmark outputs remained finite;
- no NaN/Inf values were reported;
- CPU/GPU differences for the small landmark/world-landmark outputs were generally small;
- the large segmentation-like tensor showed larger isolated max-absolute differences on synthetic inputs.

This is only a Sentis CPU-vs-GPU backend-consistency check. It is **not** an independent Google LiteRT equivalence proof and it is not full MediaPipe pose-pipeline equivalence.

## Sentis decision

### Intel HD 620

**Sentis GPUCompute is rejected as the low-end neural-inference backend on the HD 620.**

Reasons:
- fixed-tensor GPU inference is slower than Sentis CPU in the clean D3D12 run;
- GPU-resident roundtrip is substantially slower;
- D3D12 produced repeated fence timeout messages;
- D3D11 produced JobTempAlloc lifetime warnings in the spike;
- the path fails the previously defined throughput gates.

This does **not** mean the GPU should be unused. It means neural inference is not the correct workload assignment for this specific iGPU/backend combination.

### Important positive finding

Sentis CPU ran the raw recurring landmark network at about **21.63/s** in the D3D12-session benchmark, which is materially above the roughly 10–12 complete pose results/s of the current full MediaPipe Tasks pipeline.

Do not interpret this as a ready production replacement: Sentis raw-network timing excludes the full MediaPipe detector cadence, ROI tracking, raw-output decoding, landmark projection, world-landmark semantics and confidence/tracking behavior. But it proves there may be room outside the current full MediaPipe Tasks runtime.

## Heterogeneous architecture direction after Sentis

The goal remains CPU+GPU cooperation, but assignment should be capability-driven.

For HD 620-class machines, a plausible future split may be:
- GPU: Unity rendering, visual effects and any camera/preprocess operations that benchmark well;
- CPU: pose neural inference if CPU remains faster, plus detector/ROI/tracking/postprocess/canonical/retarget/game logic.

For stronger/discrete GPUs, a GPU neural backend may still win and should remain architecturally possible.

Future product policy should eventually support capability-based selection rather than one hardcoded backend, e.g. Auto / CPU / Accelerated, with a safe CPU fallback.

Do not manually split individual neural layers across CPU/GPU without evidence. Do not run duplicate CPU and GPU pose inference every frame.

## Historical OpenVINO-next wording — superseded

Earlier revisions of this document described an isolated OpenVINO exact-TFLite benchmark as the next future experiment. That experiment has now been completed on the USER's actual low-end laptop. The authoritative current status is below; any older "OpenVINO is next" wording should be read only as historical context.

## OpenVINO exact-landmark USER benchmark — 2026-09-13

The first external OpenVINO proof used the exact production landmark TFLite, OpenVINO 2026.3.0, 30 excluded warmups, 300 measured serial inferences, explicit devices, `PERFORMANCE_HINT=LATENCY`, no batching, and no AUTO/HETERO fallback.

Exact identity and contract passed:
- production bundle: 5,777,746 bytes, SHA-256 `59929e1d1ee95287735ddd833b19cf4ac46d29bc7afddbbf6753c459690d574a`;
- exact landmark model: 2,818,390 bytes, SHA-256 `ad6cfd3c903eb31a4ee788b809e45ecf9fa69923b69b9f3f2d9ae616ff433e58`;
- input float32 `[1,256,256,3]`;
- outputs `[1,195]`, `[1,1]`, `[1,256,256,1]`, `[1,64,64,39]`, `[1,117]`.

USER hardware/runtime:
- Windows 10 build 19045 x64;
- Intel Core i3-7100U @ 2.40 GHz;
- Intel HD Graphics 620;
- Python 3.14 x64;
- OpenVINO 2026.3.0.

Measured raw recurring-landmark results:
- OpenVINO CPU: mean **11.062 ms**, p50 10.511 ms, p95 14.451 ms, p99 18.878 ms, **90.403/s**; compiled execution device CPU;
- OpenVINO GPU: mean **9.067 ms**, p50 8.981 ms, p95 9.749 ms, p99 10.100 ms, **110.284/s**; actual Intel HD Graphics 620 / `GPU.0`;
- selected 1,252-byte output-copy cost was negligible relative to inference latency;
- reference Sentis CPU raw landmark result remains 46.24 ms / 21.63/s.

Interpretation:
- OpenVINO raw-network performance feasibility: **PASS**;
- Intel HD 620 OpenVINO GPU compatibility for this exact landmark TFLite: **PASS**;
- OpenVINO production integration: **NOT APPROVED**.

The initial deterministic seeded-random CPU-vs-GPU comparison kept all outputs finite but showed non-trivial raw differences on some tensors, especially `[1,195]` and the large segmentation-like output. Random noise is not a representative pose crop, so this is a precision/semantic follow-up requirement rather than proof that the GPU backend is wrong.

Strong raw-landmark speed is **not** full pose-pipeline throughput. The OpenVINO proof still does not reconstruct the complete MediaPipe detector cadence, ROI tracking, ROI rotation/projection, raw-output decode, confidence/tracking semantics, world-landmark semantics, or full camera/preprocess flow. Keep the accepted MediaPipe CPU path as the safe fallback and do not connect OpenVINO raw outputs to the avatar yet.

## Current experiment — OpenVINO precision + representative-pose validation

The current isolated benchmark continuation lives under `Tools/OpenVinoLandmarkBenchmark/` and compares:
- `CPU_DEFAULT`: CPU + LATENCY;
- `GPU_DEFAULT`: explicit GPU + LATENCY;
- `GPU_ACCURACY_FP32`: explicit GPU + LATENCY + ACCURACY execution mode + requested f32 inference precision, only when both required precision properties are exposed.

The harness records requested, supported and effective compiled OpenVINO properties separately, adds normalized numerical-difference metrics, and optionally accepts a local human-pose image as a clearly labelled `REPRESENTATIVE_IMAGE_DERIVED_INPUT`.

The representative-image preprocessing is intentionally **not** claimed as exact production MediaPipe ROI equivalence. The MediaPipe pose-landmark graph supports 256x256, preserved ROI aspect ratio and float `[0,1]`, but the production detector/tracker ROI transform, rotation/projection and exact interpolation path are not reconstructed in this proof. The `[1,195]` and `[1,117]` outputs remain labelled landmark-related/world-landmark-related raw outputs rather than decoded landmarks.

After the USER runs this precision proof, the Orchestrator—not the harness—decides whether default GPU differences are reduced-precision behavior, whether forced accuracy/f32 improves consistency at acceptable speed, and whether a later Unity coexistence/integration experiment is justified.

## Things the next Orchestrator must not do

- Do not merge to `main` without explicit USER approval.
- Do not mark Phase 5A accepted; final USER acceptance is still pending.
- Do not start Phase 6 yet.
- Do not connect experimental Sentis/OpenVINO outputs to the avatar before semantic/precision evidence supports it.
- Do not densify/convert the detector merely because the audit says it is possible in principle.
- Do not reopen callback-poll or result-callback launch optimizations without new reproducible evidence.
- Do not delete or weaken the accepted CPU MediaPipe path; it remains the known-safe fallback.
- Do not force D3D12 globally based on the Sentis spike.
- Do not equate GPU utilization with useful acceleration.
- Do not use Luna for broad inference-architecture design; use the intelligent Web Builder for architecture changes and Luna only for tightly scoped follow-ups.

## Immediate next step

Run the precision-controlled OpenVINO proof on the USER laptop, preferably with a representative centered/full-body pose image in addition to the seeded input. Return the generated TXT/JSON report and compact precision summary to the Orchestrator. Production-provider integration remains blocked until that evidence is reviewed.

## Reuse-first architecture checkpoint — 2026-09-13

The precision-controlled OpenVINO continuation is now complete; the preceding "Immediate next step" paragraph is historical and superseded by this section.

Second USER OpenVINO result on the same low-end laptop:
- `CPU_DEFAULT`: effective FP32 / PERFORMANCE, mean **10.355 ms**, p50 9.988 ms, p95 12.233 ms, p99 15.809 ms, **96.569/s**;
- `GPU_DEFAULT`: explicit Intel HD 620 `GPU.0`, effective FP16 / PERFORMANCE, mean **8.793 ms**, p50 8.738 ms, p95 9.131 ms, p99 9.564 ms, **113.726/s**;
- `GPU_ACCURACY_FP32`: explicit `GPU.0`, effective FP32 / ACCURACY, mean **12.858 ms**, p50 12.717 ms, p95 14.012 ms, p99 14.260 ms, **77.775/s**.

Representative human-pose consistency:
- CPU FP32 vs GPU default FP16: `[1,195]` normalized MAE/RMS ~0.00337486 / ~0.00376357; `[1,117]` ~0.0129529 / ~0.0145266;
- CPU FP32 vs GPU forced FP32: `[1,195]` ~1.00341e-06 / ~1.50298e-06; `[1,117]` ~2.09536e-06 / ~2.38752e-06.

Current interpretation:
- OpenVINO raw landmark performance feasibility and HD620 compatibility are **PASS**;
- default GPU differences are primarily reduced-precision behavior;
- **OpenVINO CPU FP32 is the leading low-end inference candidate**;
- GPU FP16 remains an optional accelerated profile for stronger hardware after semantic/end-to-end validation;
- GPU FP32 is not worthwhile on this HD620 because it is slower than CPU FP32;
- production integration is still **NOT APPROVED** because raw landmark inference is not the complete MediaPipe pose pipeline.

The USER architecture rule is now explicit: **reuse mature components instead of manually recreating MediaPipe semantics when a practical reusable path exists, and make provider/canonical topology schema-driven so later joints/providers are additive.**

Current source audit confirms:
- `PoseObservation` is MediaPipe-specific and fixed at 33 landmarks;
- `MediaPipeCanonicalPoseMapper` appropriately centralizes MediaPipe index knowledge;
- `CanonicalPoseFrame` is fixed at the current 20-joint topology;
- current production behavior must remain a stable compatibility definition, `CanonicalBodyV1`, before any topology refactor is attempted.

Authoritative architecture audit:
`Docs/inference-architecture-reuse-audit.md`.

Leading reuse direction from current 2026 evidence:
1. keep current MediaPipe graph/calculators for preprocessing, detector decode/NMS, ROI generation/tracking, landmark/heatmap refinement, visibility/presence, world-landmark processing and projection;
2. replace only inference nodes with a current-compatible, in-process OpenVINO Runtime calculator where the parity proof supports it;
3. do **not** adopt Intel's old MediaPipe fork wholesale: its reference fork is based on MediaPipe 0.10.3, while MediaPipeUnityPlugin 0.16.3 uses MediaPipe 0.10.22;
4. prefer a selective port of the direct OpenVINO calculator concept into the current Homuler/MediaPipe native build;
5. keep OVMS sidecar as a fallback/reference only because it adds process/IPC/startup/packaging overhead;
6. reject the archived TFLite OpenVINO delegate as the primary production route;
7. treat MediaPipe Holistic as the preferred future richer-MediaPipe provider to benchmark, and RTMPose WholeBody as a possible future rich-2D provider with a real world-coordinate migration cost;
8. manual pipeline reconstruction is the last resort.

The generic future motion-data design must separate:
- provider landmark schema/frame: schema-driven count, semantic IDs, optional image/depth/world channels, visibility/presence/confidence and groups such as body/hands/face;
- versioned canonical skeleton definitions: `CanonicalBodyV1` preserves the exact current 20-joint meaning; future definitions may add spine/neck/clavicle/finger joints without rewriting V1;
- retarget/game consumer profiles: each consumer declares only the semantic joints it needs, so richer topologies do not force changes to existing torso/arm/leg retargeting or locomotion.

Current authoritative next proof:
1. run the isolated `Tools/OpenVinoLandmarkBenchmark/detector_probe.py` against the exact unchanged `pose_detector.tflite`; it verifies exact identity, direct OpenVINO read/contract, explicit CPU/GPU compile and one finite-output sanity inference with **no** conversion/densification/fallback;
2. if that passes, build a standalone current-MediaPipe-0.10.22-generation graph proof with a minimal direct OpenVINO inference calculator and compare final 33 normalized landmarks, 33 world landmarks, visibility/presence, ROI continuity and complete graph latency against baseline MediaPipe Tasks on the same recorded frames;
3. do not connect the proof to the Unity avatar until semantic parity and end-to-end latency are demonstrated.

## Gate A complete / Gate B implemented-for-user-proof — 2026-09-13

The preceding "Current authoritative next proof" list is now historical. Gate A has been completed successfully and the isolated Gate B scaffold is implemented under `Tools/MediaPipeOpenVinoParity/`.

Gate A exact-detector result: **COMPLETE / PASS**.
- exact detector identity PASS: `pose_detector.tflite`, 2,959,078 bytes, SHA-256 `46837eb883e6ec75b52c5f5ff6a9b78bd35e66c13f95e8c3566c582d146cb1d9`;
- OpenVINO 2026.3 direct `read_model()` on the unchanged TFLite: SUCCESS;
- exact contract `[1,224,224,3]` float32 -> `[1,2254,12]` + `[1,2254,1]` float32: TRUE;
- explicit CPU compile/inference: SUCCESS;
- explicit Intel GPU compile/inference: SUCCESS;
- outputs finite;
- no conversion, densification, alternate model or hidden fallback was used.

Conclusion: the detector's DENSIFY failure is a Sentis importer limitation for this model; it is **not** an OpenVINO compatibility blocker.

Gate B scaffold status: **IMPLEMENTED / USER WINDOWS BUILD + RECORDED-SEQUENCE RUN PENDING**.
- exact source base is Homuler `v0.16.3` commit `cf4c11d8eef724fe24111b7cd795d55ba490aeec`, which pins MediaPipe `v0.10.22` commit `c54c06dd8c4314a316c14da31493bcc38ed302e2` and Bazel 6.5.0;
- source audit on that exact MediaPipe generation confirmed detector and landmark neural execution remain behind the inference seam while MediaPipe retains preprocessing, detector decode/NMS/ROI, tracking, landmark decode/refinement, presence/visibility, world-landmark and projection semantics;
- `TASKS_REFERENCE` runs the official 0.10.22 PoseLandmarker CPU path on the exact task bundle;
- `GRAPH_TFLITE_CPU` runs the expanded/current MediaPipe graph with standard TFLite CPU inference;
- `GRAPH_OPENVINO_CPU_FP32` runs that same graph while replacing **both** detector and landmark inference with a local OpenVINO CPU FP32 calculator;
- exact production bundle/detector/landmark hashes are fail-closed before execution;
- the OpenVINO bridge uses safe host copies for this proof and measures input/output bridge-copy cost separately rather than hiding it;
- the proof accepts one fixed recorded human-motion sequence, preserves independent temporal/tracking state per backend, records final 33 normalized/world landmarks plus ROI/cadence/timing where exposed, and compares A-vs-B, B-vs-C and A-vs-C without inventing Golden Needle acceptance thresholds;
- VIDEO-mode rates are explicitly offline graph-processing capacity, not Unity LIVE_STREAM frame-to-result latency;
- USER inputs, extracted sources/models, native build products and reports remain ignored/local.

Current authoritative next step is now to run the Gate B Windows proof on the USER machine. Do **not** declare semantic parity or approve production OpenVINO integration until the generated TXT/JSON evidence has been reviewed by the Orchestrator.

Production governance is unchanged:
- MediaPipe CPU fallback remains intact;
- Phase 5A remains **NOT USER ACCEPTED**;
- Phase 6 remains **NOT STARTED**;
- modular provider/canonical work remains **DESIGN-ONLY**; `CanonicalBodyV1` remains exactly today's accepted 20-joint semantics;
- no detector densification;
- no Unity avatar/provider integration;
- no merge to `main` without explicit USER approval.
