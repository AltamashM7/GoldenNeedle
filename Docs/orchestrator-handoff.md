# Golden Needle — Orchestrator Handoff

## 1. Governance

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

Latest substantive runtime/code checkpoint before this documentation refresh:
`3584e4a059937dfa80b278fe2d96334344025b76` — `feat: add landmark-first sentis spike audit`.

There are documentation-only and isolated benchmark commits after that checkpoint. Always fetch and verify the current remote branch HEAD before doing new work.

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

## 14. OpenVINO exact-landmark benchmark — USER result

The previously planned first OpenVINO experiment is complete. Do not treat it as a future task.

Environment:
- Windows 10 build 19045 x64;
- Intel Core i3-7100U @ 2.40 GHz;
- Intel HD Graphics 620;
- Python 3.14 x64;
- OpenVINO 2026.3.0.

Exact identity/contract:
- production task bundle 5,777,746 bytes, SHA-256 `59929e1d1ee95287735ddd833b19cf4ac46d29bc7afddbbf6753c459690d574a`;
- exact landmark TFLite 2,818,390 bytes, SHA-256 `ad6cfd3c903eb31a4ee788b809e45ecf9fa69923b69b9f3f2d9ae616ff433e58`;
- input float32 `[1,256,256,3]`;
- outputs `[1,195]`, `[1,1]`, `[1,256,256,1]`, `[1,64,64,39]`, `[1,117]`;
- exact-model identity and OpenVINO contract: **PASS**.

Method:
- explicit CPU and GPU only;
- no AUTO/HETERO;
- LATENCY performance hint;
- one serial request;
- 30 warmups excluded;
- 300 measured calls;
- deterministic seeded-random input;
- output materialization measured separately.

Results:
- OpenVINO CPU: mean **11.062 ms**, p50 10.511, p95 14.451, p99 18.878, **90.403/s**, execution device CPU;
- OpenVINO GPU: actual Intel HD Graphics 620 / `GPU.0`, mean **9.067 ms**, p50 8.981, p95 9.749, p99 10.100, **110.284/s**;
- selected 1,252-byte output copy/materialization cost was negligible relative to neural inference latency;
- Sentis CPU reference: 46.24 ms / 21.63/s.

Decision state:
- OpenVINO raw-landmark performance feasibility: **PASS**;
- OpenVINO HD 620 GPU compatibility: **PASS**;
- OpenVINO production integration: **NOT APPROVED**.

The seeded-random CPU-vs-GPU comparison kept all outputs finite but showed non-trivial differences on some outputs. Representative examples from the first run include `[1,195]` max abs ~14.22 / mean abs ~1.57 and `[1,256,256,1]` max abs ~533.7 / mean abs ~4.46. This does not prove GPU semantic failure because seeded random noise is not a representative pose crop; it creates the current precision/representative-input follow-up requirement.

The OpenVINO numbers are raw recurring-landmark network speed, not full pose-pipeline throughput. Detector cadence, ROI tracking, exact ROI rotation/projection, raw-output decoding, confidence/tracking semantics, world-landmark semantics and the full camera/preprocess flow remain unreconstructed outside MediaPipe. The MediaPipe CPU path remains the safe production fallback.

## 15. Current approved architecture experiment — precision + representative-pose validation

Current Web Builder harness location:
`Tools/OpenVinoLandmarkBenchmark/`

The current proof compares three profiles independently:
- `CPU_DEFAULT`: CPU + LATENCY;
- `GPU_DEFAULT`: explicit GPU + LATENCY;
- `GPU_ACCURACY_FP32`: explicit GPU + LATENCY + requested ACCURACY execution mode + f32 inference precision, only when both required precision-control properties are supported.

For every successful profile the harness records requested configuration, device-supported properties and effective compiled properties separately, including `EXECUTION_DEVICES`, `PERFORMANCE_HINT`, `EXECUTION_MODE_HINT`, `INFERENCE_PRECISION_HINT`, `NUM_STREAMS` and `SUPPORTED_PROPERTIES` where queryable.

Numerical comparisons now include reference magnitude plus absolute and normalized mean/RMS differences. No arbitrary numerical PASS/FAIL threshold is assigned.

Optional `--pose-image` input is deliberately labelled `REPRESENTATIVE_IMAGE_DERIVED_INPUT`. Source evidence supports RGB input, 256x256 ImageToTensor, preserved ROI aspect ratio and float `[0,1]`; the production detector/tracker-derived ROI, rotation/projection and exact interpolation are **not** reconstructed. Therefore this is not exact MediaPipe Tasks equivalence. Raw `[1,195]` and `[1,117]` are reported as landmark-related/world-landmark-related raw outputs, not decoded landmarks.

After the USER run, the Orchestrator must decide:
- what precision/execution mode GPU default actually reports;
- whether forced accuracy/f32 materially reduces raw differences;
- whether the accuracy/f32 performance cost remains acceptable;
- whether a representative human-image-derived input gives enough numerical confidence to justify any later Unity coexistence/prod-integration experiment.

## 16. Worker-role guidance and immediate next step

Use the **Web Builder** for inference-runtime architecture and broad provider work. Use Luna only for tightly scoped diagnostics/instrumentation/read-only audits.

Immediate next step:
1. fetch/pull the latest `engine/pose-tracking-spike`;
2. run the OpenVINO precision harness on the USER laptop, ideally with a representative centered/full-body pose image;
3. return the generated TXT + JSON plus the compact `GOLDEN NEEDLE OPENVINO PRECISION SUMMARY` block to the Orchestrator;
4. keep production provider/canonical/retarget/locomotion frozen until the Orchestrator reviews the evidence.

## 17. Do not do these without new evidence/approval

- no merge to `main`;
- no Phase 5A acceptance claim;
- no Phase 6 start;
- no callback-thread `DetectAsync` launch;
- no further callback/poll micro-optimization;
- no global D3D12 production switch based on the Sentis spike;
- no detector densification simply because audit says it is possible;
- no production Sentis/OpenVINO provider integration yet;
- no duplicate CPU+GPU inference every frame;
- no removal of the existing CPU MediaPipe fallback;
- no lowering model quality just to hit an FPS target.

The current priority is evidence-driven reduction of latency and improvement of fast-motion fidelity while preserving the accepted motion engine.

## 18. Reuse-first architecture audit — authoritative continuation

The precision/representative-pose experiment described in sections 15–16 has now completed. Treat those sections as historical context; the current next step is below.

### Precision-controlled USER result

Same USER machine and OpenVINO 2026.3.0:
- `CPU_DEFAULT`: effective FP32 / PERFORMANCE; mean **10.355 ms**, p50 9.988, p95 12.233, p99 15.809, **96.569/s**;
- `GPU_DEFAULT`: explicit Intel HD 620 `GPU.0`, effective FP16 / PERFORMANCE; mean **8.793 ms**, p50 8.738, p95 9.131, p99 9.564, **113.726/s**;
- `GPU_ACCURACY_FP32`: explicit `GPU.0`, effective FP32 / ACCURACY; mean **12.858 ms**, p50 12.717, p95 14.012, p99 14.260, **77.775/s**.

Representative human-image-derived consistency:
- CPU FP32 vs GPU default FP16:
  - `[1,195]` normalized MAE/RMS ~0.00337486 / ~0.00376357;
  - `[1,117]` normalized MAE/RMS ~0.0129529 / ~0.0145266;
- CPU FP32 vs GPU forced FP32:
  - `[1,195]` ~1.00341e-06 / ~1.50298e-06;
  - `[1,117]` ~2.09536e-06 / ~2.38752e-06.

Interpretation approved for architecture work:
- OpenVINO raw landmark performance feasibility: **PASS**;
- Intel HD 620 OpenVINO compatibility: **PASS**;
- GPU default differences are primarily reduced-precision behavior;
- **OpenVINO CPU FP32 is the leading low-end inference candidate**;
- GPU FP16 remains a possible accelerated profile for stronger hardware;
- GPU FP32 is not worthwhile on this HD620 because it is slower than CPU FP32;
- production integration remains **NOT APPROVED** because full detector/ROI/tracking/decode/world semantics and Unity coexistence are not proven.

### USER architecture requirement

The USER explicitly requires a reuse-first, modular architecture:
- reuse mature MediaPipe calculators/framework components where they are a better fit;
- do not manually reconstruct detector/ROI/tracking/decoding merely because it is possible;
- preserve all provider landmarks until a semantic mapper decides what a canonical definition needs;
- do not make generic engine code permanently assume provider count 33 or canonical count 20;
- preserve current accepted Phase 4 behavior as a stable compatibility definition, `CanonicalBodyV1`;
- future providers/topologies must be additive.

Authoritative audit document:
`Docs/inference-architecture-reuse-audit.md`.

### Reuse decision

Current 2026 source audit supports this leading route:

```text
Current MediaPipe graph/calculators
  preprocessing + detector decode/NMS + ROI/tracking
        |
        v
replaceable inference node
  existing TFLite CPU fallback
  OpenVINO CPU FP32 (first HD620 candidate)
  optional future accelerated OpenVINO profile
        |
        v
Current MediaPipe landmark/heatmap/presence/world/projection calculators
        |
        v
schema-driven Provider Landmark Frame
        |
        v
provider-specific semantic mapper
        |
        v
versioned canonical definition (CanonicalBodyV1 first)
        |
        +--> stabilization/calibration
        +--> retarget consumer profiles
        +--> locomotion profiles
        +--> gestures/future hand systems
```

Why this is credible:
- current Google MediaPipe detector and landmark Tasks graphs still isolate neural execution behind `AddInference(...)` while keeping detector decode/NMS/ROI and landmark refinement/world projection in MediaPipe calculators;
- Intel's OpenVINO MediaPipe references include an in-process direct `OpenVINOInferenceCalculator` using `ov::Core`, proving the integration shape exists;
- the old Intel fork should **not** be adopted wholesale: it is based on MediaPipe 0.10.3 while Homuler 0.16.3 uses MediaPipe 0.10.22, and the direct calculator is an old prototype with hard-coded CPU/TODO configuration;
- selectively porting/adapting the calculator concept into the current Homuler/MediaPipe native build is therefore preferred.

Alternative status:
- **OVMS sidecar:** technically viable on current Windows/MediaPipe graph tooling, but not preferred for this local latency-sensitive game because it adds process/IPC/startup/packaging complexity. Keep only as fallback/reference if in-process build proves impractical.
- **TFLite/OpenVINO delegate:** Intel's repository is archived/read-only; no maintained HD620-target drop-in successor was found. Do not choose as primary route.
- **official MediaPipe GPU on Windows:** still not a supported production route for this Homuler/desktop setup.
- **RTMPose/WholeBody:** useful future rich-2D provider (133 keypoints; OpenVINO deployment documented), but it lacks the current MediaPipe world-landmark contract and has real tracking/3D/Windows-integration migration cost.
- **MediaPipe Holistic:** preferred future rich-MediaPipe provider to benchmark for hands/fingers/face because it retains MediaPipe-style semantics and exposes pose/hand world outputs; low-end cost must be measured.
- **manual pipeline reimplementation:** last resort only.

### Modular data design to preserve

Provider layer:
- versioned provider/schema ID;
- schema-driven landmark count;
- semantic landmark IDs/names and groups (body/left hand/right hand/face/etc.);
- optional normalized image position, optional model depth, optional world position;
- visibility/presence/confidence and capability flags;
- never force a 2D provider to invent world coordinates.

Canonical layer:
- versioned schema-driven `CanonicalSkeletonDefinition`;
- `CanonicalBodyV1` is exactly the current 20-joint meaning, coordinate convention and derived pelvis/chest/spine behavior;
- future `CanonicalBodyV2` / `CanonicalBodyHandsV1` can add joints without changing V1;
- frame capacity comes from the active definition rather than a global `JointCount=20` assumption.

Consumer layer:
- retarget/locomotion/gesture systems declare the semantic joints they require;
- current humanoid body profile continues to consume only the current body joints;
- later fingers/gestures consume richer joints independently.

Do **not** refactor production `PoseObservation`, `CanonicalPoseFrame` or downstream runtime in the next proof. First prove the inference reuse path and later introduce schema adapters with deterministic parity against the current mapper.

### Current next isolated proof

Gate A — cheap external detector compatibility:
- new tool: `Tools/OpenVinoLandmarkBenchmark/detector_probe.py`;
- fail-closed exact detector identity: size 2,959,078 bytes, SHA-256 `46837eb883e6ec75b52c5f5ff6a9b78bd35e66c13f95e8c3566c582d146cb1d9`;
- direct OpenVINO 2026.3 `Core.read_model()` on the unchanged TFLite;
- require `[1,224,224,3]` float32 -> `[1,2254,12]` + `[1,2254,1]` float32;
- explicit CPU/GPU compile and one finite-output sanity inference;
- no DENSIFY rewrite, conversion, substitute model, AUTO/HETERO/MULTI or hidden fallback;
- it is a compatibility probe, not a performance benchmark.

Run from repository root after pulling the branch:

```powershell
.\Tools\OpenVinoLandmarkBenchmark\.venv\Scripts\python.exe .\Tools\OpenVinoLandmarkBenchmark\detector_probe.py --repo-root .
```

If Gate A passes, Gate B is the decisive architecture proof:
- standalone native build based on the MediaPipe 0.10.22 generation used by Homuler;
- port a minimal direct OpenVINO inference calculator rather than adopting the old fork;
- substitute detector + landmark inference only;
- retain current MediaPipe preprocessing, detector decode/NMS, ROI/tracking, landmark/heatmap refinement, visibility/presence, world decode and projection;
- run the same short recorded frame sequence through baseline MediaPipe Tasks CPU and the OpenVINO-substituted graph;
- compare final 33 normalized landmarks, 33 world landmarks, visibility/presence, ROI continuity, full graph latency and fresh-result cadence;
- no Unity avatar connection until semantic parity + end-to-end performance are strong.

### Licensing / packaging

Project-level licenses audited are favorable: OpenVINO, MediaPipe, Intel's fork, OVMS, MMPose and MMDeploy are Apache-2.0; Homuler is MIT with third-party notices. A production native plugin still must preserve applicable notices and ship only redistributable runtime DLLs. Alternative downloaded model weights require separate model-card/dataset-license review; do not infer their terms solely from the repository license.

### Governance remains unchanged

- no merge to `main` without explicit USER approval;
- no Phase 5A acceptance;
- no Phase 6;
- no production provider integration yet;
- no canonical runtime refactor yet;
- no detector densification;
- no removal of the current MediaPipe CPU fallback.
