# Golden Needle — Orchestrator Handoff

## 1. Governance

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

Do **not** merge to `main` without explicit USER approval. Do not force-push, rebase or rewrite branch history merely to clean experimental work.

Known USER-local dirty files have repeatedly existed and must not be reverted, cleaned, staged or overwritten casually:
- `M Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpike.unity`
- `M GoldenNeedle.slnx`
- `?? ProjectSettings/SceneTemplateSettings.json`

Unity/package resolution may also create legitimate USER-local package-lock changes. Inspect before touching them rather than assuming they are disposable.

The USER performs manual Unity/runtime QA. Use the intelligent Web Builder for broad inference/runtime architecture work; use Luna only for tightly scoped diagnostics, instrumentation or read-only follow-ups.

## 2. Current project status

- Phase 1 provider/raw overlays: **PASS WITH NOTES**.
- Phase 2 canonical skeleton/debug: **PASS**.
- Phase 3 stabilization/confidence: **PASS**.
- Phase 4 humanoid retargeting/calibration/orientation: **USER ACCEPTED — PASS**.
- Phase 5A locomotion/presentation: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.
- Current active track: responsiveness / inference-architecture proof.

Do not mark Phase 5A accepted and do not start Phase 6 until the USER explicitly does so.

## 3. Accepted production motion path — keep frozen during inference experiments

Unity baseline:
- Unity 6.5 / `6000.5.0f1`;
- URP 17.5.0;
- MediaPipeUnityPlugin 0.16.3;
- Windows experimental environment.

Production provider remains MediaPipe Pose Landmarker Lite CPU using:
`Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes`

Production task semantics:
- BaseOptions delegate CPU;
- `RunningMode.LIVE_STREAM`;
- one pose;
- min pose detection confidence 0.5;
- min pose presence confidence 0.5;
- min tracking confidence 0.5;
- segmentation false;
- world landmarks required.

Current path:

```text
camera/latest frame
-> body-only ~320x240 preparation
-> DirectCPU readback when enabled (Homuler fallback remains)
-> MediaPipe Pose Landmarker Lite CPU
-> PoseObservation
-> canonical mapping
-> stabilization/calibration
-> humanoid retargeting/locomotion
```

Scheduling invariants:
- <=1 active readback;
- <=1 replaceable prepared TextureFrame;
- <=1 outstanding inference;
- latest useful frame wins;
- no camera-frame history;
- no inference backlog;
- no pose replay/catch-up queue.

Accepted production behavior that experimental inference work must not disturb:
- canonical +X right, +Y up, +Z away;
- MediaPipe world conversion `(x,-y,z)` pelvis-relative;
- modular torso/arms/legs calibration and partial-body behavior;
- Phase 4 orientation correction and swing-only limb alignment;
- Phase 3 One Euro baseline: min 1.0, beta 0.05, deriv 1.0, acquire 0.60, sustain 0.40, 2 samples, grace 0.10 s, reset 0.25 s;
- Phase 5A implementation remains frozen pending USER acceptance.

Accepted responsiveness improvements:
- body path downscale to ~320x240 gave a modest repeatable gain without obvious quality collapse;
- Immediate Launch After Readback removed roughly one render-frame of artificial scheduling delay and is USER accepted;
- DirectCPU readback is a modest accepted CPU-path optimization with a teardown lifetime guard.

## 4. Why the inference track exists

The USER reports fast body/arm movement is undersampled unless movement is performed somewhat more slowly.

Representative production behavior before alternate-runtime experiments:
- camera ~29–30 FPS;
- fresh pose results ~10–12/s;
- DirectCPU readback ~55–65 ms;
- accepted MediaPipe request->callback commonly ~60–75+ ms;
- frame->pose result often ~110–140 ms.

At ~10–12 fresh pose samples/s, fast trajectories can move significantly between inference results. The objective is to reduce real latency and raise fresh pose sampling while preserving current MediaPipe semantics and a safe CPU fallback.

## 5. Sentis result — useful evidence but rejected HD620 GPU backend

Exact production submodels:
- bundle: 5,777,746 bytes, SHA-256 `59929e1d1ee95287735ddd833b19cf4ac46d29bc7afddbbf6753c459690d574a`;
- detector: `pose_detector.tflite`, 2,959,078 bytes, SHA-256 `46837eb883e6ec75b52c5f5ff6a9b78bd35e66c13f95e8c3566c582d146cb1d9`;
- landmark: `pose_landmarks_detector.tflite`, 2,818,390 bytes, SHA-256 `ad6cfd3c903eb31a4ee788b809e45ecf9fa69923b69b9f3f2d9ae616ff433e58`.

Sentis detector import fails on `DENSIFY`. Audit found 38 DENSIFY nodes with static sparse constants and concluded densification is a plausible storage rewrite **in principle**, not an approved change.

Clean D3D12 USER landmark result:
- Sentis CPU: 46.24 ms / 21.63/s;
- GPUCompute fixed tensor: 60.48 ms / 16.53/s;
- GPU-resident: 127.96 ms / 7.82/s;
- repeated D3D12 fence-wait diagnostics occurred.

Decision: Sentis GPUCompute is rejected as the low-end HD620 neural inference backend. This does **not** mean all GPU acceleration is impossible.

## 6. OpenVINO landmark evidence — strong raw feasibility, not production approval

USER machine:
- Windows 10 build 19045 x64;
- Intel Core i3-7100U @ 2.40 GHz;
- Intel HD Graphics 620;
- OpenVINO 2026.3.0.

First exact-landmark proof:
- CPU mean 11.062 ms / 90.403/s;
- GPU.0 mean 9.067 ms / 110.284/s;
- exact identity and tensor contract PASS;
- output copy cost negligible.

Precision-controlled follow-up:
- `CPU_DEFAULT`: effective FP32/PERFORMANCE, mean **10.355 ms**, p95 12.233 ms, **96.569/s**;
- `GPU_DEFAULT`: effective FP16/PERFORMANCE, mean **8.793 ms**, p95 9.131 ms, **113.726/s**;
- `GPU_ACCURACY_FP32`: effective FP32/ACCURACY, mean **12.858 ms**, p95 14.012 ms, **77.775/s**.

Representative human-image-derived consistency:
- CPU FP32 vs GPU default FP16:
  - `[1,195]` normalized MAE/RMS ~0.00337486 / ~0.00376357;
  - `[1,117]` ~0.0129529 / ~0.0145266;
- CPU FP32 vs GPU forced FP32:
  - `[1,195]` ~1.00341e-06 / ~1.50298e-06;
  - `[1,117]` ~2.09536e-06 / ~2.38752e-06.

Interpretation:
- OpenVINO raw-landmark performance feasibility: **PASS**;
- Intel HD620 OpenVINO compatibility: **PASS**;
- default GPU differences are predominantly reduced-precision behavior;
- **OpenVINO CPU FP32 is the leading low-end candidate**;
- GPU FP16 remains a possible accelerated profile for stronger hardware after end-to-end evidence;
- GPU FP32 is not worthwhile on this HD620 because it is slower than CPU FP32;
- raw neural timing is not complete MediaPipe pose-pipeline throughput;
- production integration remains **NOT APPROVED**.

## 7. Reuse-first architecture decision

The USER explicitly requires reuse of mature framework/calculator components rather than manually recreating MediaPipe semantics when a practical reuse path exists.

Authoritative architecture document:
`Docs/inference-architecture-reuse-audit.md`

Leading direction:

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
Current MediaPipe landmark/refinement/presence/world/projection calculators
        |
        v
schema-driven Provider Landmark Frame
        |
        v
provider-specific semantic mapper
        |
        v
versioned Canonical Skeleton Definition
  CanonicalBodyV1 first
        |
        +--> stabilization/calibration
        +--> retarget consumer profiles
        +--> locomotion profiles
        +--> gestures/future hand systems
```

Key decisions:
- do not adopt Intel's old MediaPipe fork wholesale; it is based on 0.10.3 while Homuler 0.16.3 pins MediaPipe 0.10.22;
- selectively adapt the in-process OpenVINO calculator concept into the current Homuler/MediaPipe generation;
- OVMS sidecar is technically viable but rejected as the preferred local game path because process/IPC/startup/packaging overhead is unnecessary if in-process succeeds;
- the archived TFLite/OpenVINO delegate is not a primary production route;
- MediaPipe Holistic is the preferred future richer-MediaPipe provider to benchmark for hands/fingers/face;
- RTMPose WholeBody remains a possible future rich-2D provider but lacks the current MediaPipe world-coordinate contract;
- manual MediaPipe pipeline reimplementation is a last resort.

Modular design remains **DESIGN-ONLY**. Do not refactor production runtime yet:
- provider frame uses schema/provider ID, semantic landmark IDs/groups, schema-driven count, optional normalized/depth/world channels, visibility/presence/confidence and capability flags;
- canonical definitions are versioned/schema-driven;
- `CanonicalBodyV1` means exactly today's accepted 20-joint topology/derived-joint semantics;
- retarget/locomotion/gesture consumers declare only the semantic joints they require;
- richer providers/topologies are additive and do not rewrite `CanonicalBodyV1`.

## 8. Gate A — exact detector OpenVINO compatibility: COMPLETE / PASS

Gate A is no longer a future task.

Exact detector result:
- identity PASS: 2,959,078 bytes, SHA-256 `46837eb883e6ec75b52c5f5ff6a9b78bd35e66c13f95e8c3566c582d146cb1d9`;
- OpenVINO 2026.3 direct `Core.read_model()` on unchanged TFLite: **SUCCESS**;
- exact contract float32 `[1,224,224,3]` -> `[1,2254,12]` + `[1,2254,1]`: **TRUE**;
- explicit CPU compile + finite-output inference: **SUCCESS**;
- explicit GPU compile + finite-output inference: **SUCCESS**;
- no conversion, densification, substitute model, AUTO/HETERO/MULTI or hidden fallback.

Conclusion: the exact detector's DENSIFY problem is a Sentis importer limitation for Golden Needle's purposes. It is **not** an OpenVINO compatibility blocker.

## 9. Gate B — MediaPipe/OpenVINO parity proof: scaffold implemented, USER run pending

Tracked proof location:
`Tools/MediaPipeOpenVinoParity/`

Starting source pins are fail-closed:
- Homuler MediaPipeUnityPlugin `v0.16.3` -> commit `cf4c11d8eef724fe24111b7cd795d55ba490aeec`;
- that workspace pins Google MediaPipe `v0.10.22` -> commit `c54c06dd8c4314a316c14da31493bcc38ed302e2`;
- Bazel 6.5.0;
- OpenVINO Runtime C++ 2026.3.0, official Windows toolkit archive/checksum.

Read-only audit on the exact 0.10.22 generation confirmed the required seam: pose detector and pose landmark graphs isolate neural execution behind `AddInference(...)`, while MediaPipe calculators retain preprocessing, detector decode/NMS/ROI, tracking, landmark decode/refinement, pose presence, visibility/presence, world decode and projection.

Three proof modes are implemented:
1. `TASKS_REFERENCE` — official MediaPipe 0.10.22 PoseLandmarker CPU, exact production task bundle, one pose, 0.5/0.5/0.5, segmentation false, deterministic VIDEO mode;
2. `GRAPH_TFLITE_CPU` — expanded current MediaPipe graph with standard TFLite CPU inference;
3. `GRAPH_OPENVINO_CPU_FP32` — the same graph/calculators, with **both detector and landmark neural inference** replaced by an in-process OpenVINO CPU FP32 calculator.

The OpenVINO bridge contract is `std::vector<mediapipe::Tensor> -> OpenVINO -> std::vector<mediapipe::Tensor>`. For this architecture proof it uses safe host copies and measures input/output bridge-copy cost separately. Model compilation occurs at graph open, not per frame; the infer request is reused and inference is synchronous to keep the VIDEO-mode comparison deterministic.

The runner:
- accepts a fixed recorded video or prepared frame sequence;
- decodes a source video once and reuses the exact same ordered frames/timestamps for each backend;
- keeps backend tracking state independent;
- preserves full 33 normalized and world landmarks rather than prematurely mapping down to the current 20-joint canonical body;
- records final landmark semantics, visibility/presence where available, auxiliary availability, next ROI, detector cadence where exposed, startup/first-frame/frame timing, and OpenVINO detector/landmark inference + bridge-copy timing;
- labels VIDEO-mode timing as **offline graph-processing capacity**, not Unity LIVE_STREAM frame-to-result latency.

The comparator reports A-vs-B, B-vs-C and A-vs-C:
- pose presence agreement plus false-positive/false-negative frame indices;
- normalized x/y/z MAE/RMS/p95/max, visibility/presence and per-landmark worst cases;
- world x/y/z and 3D Euclidean error in meters;
- ROI center/size/rotation comparisons where available;
- detector-continuity/cadence comparisons where available;
- startup, first-frame, steady-state mean/p50/p95/p99/min/max/population-stddev and processing rate;
- OpenVINO detector/landmark inference and bridge-copy statistics.

It intentionally applies **no arbitrary Golden Needle PASS/FAIL threshold**. The Orchestrator decides semantic acceptance from the evidence.

USER video/frames, extracted models/sources, native build products, toolchain downloads and reports remain local/ignored.

Builder-side validation completed:
- Python helper syntax compilation: PASS;
- synthetic parity-comparator self-test: PASS;
- exact source/model/version/mode static gates: inspected;
- native Windows MSVC/Bazel/OpenVINO build and actual recorded-sequence run: **NOT run in the Builder environment** and must not be claimed as successful before USER evidence.

## 10. Current authoritative next step

Keep Unity closed and run from repository root on the USER Windows machine:

```powershell
.\Tools\MediaPipeOpenVinoParity\bootstrap.ps1
.\Tools\MediaPipeOpenVinoParity\build.ps1
.\Tools\MediaPipeOpenVinoParity\run.ps1 -InputVideo "C:\path\golden-needle-motion.mp4"
```

Recommended sequence:
- ~8–20 seconds;
- one person;
- full body mostly visible;
- normal movement plus deliberately fast arm/body motion;
- some torso turn/leg motion if practical;
- brief partial loss/occlusion is useful but optional.

If the result is close/suspicious and a second order check is useful:

```powershell
.\Tools\MediaPipeOpenVinoParity\run.ps1 -InputVideo "C:\path\golden-needle-motion.mp4" -Order OpenVinoFirst
```

Return to the Orchestrator:
1. newest `Tools/MediaPipeOpenVinoParity/results/parity-*.txt`;
2. matching `parity-*.json`;
3. console block from `=== GOLDEN NEEDLE MEDIAPIPE OPENVINO PARITY SUMMARY ===` through `=== END GOLDEN NEEDLE MEDIAPIPE OPENVINO PARITY SUMMARY ===`;
4. any bootstrap/build/runtime error verbatim if the proof cannot complete.

Do **not** declare Gate B semantic parity PASS until those reports are reviewed.

## 11. Decision after USER Gate B evidence

If semantic parity is strong and end-to-end offline graph capacity materially beats the current reference without hidden regressions, the Orchestrator may authorize the next isolated step: a Windows native-plugin/Unity coexistence proof with measured rendering contention and live frame-to-result behavior.

If A-vs-B is poor, fix the custom graph/configuration before judging OpenVINO. If B-vs-C is poor while A-vs-B is strong, investigate the backend bridge/model output mapping/precision. If performance improvement disappears at full graph level, do not integrate merely because raw neural benchmarks were fast.

Even a strong Gate B result does **not** itself approve production replacement.

## 12. Hard stop list

Until new evidence/USER approval:
- no merge to `main`;
- no Phase 5A acceptance claim;
- no Phase 6 start;
- no production OpenVINO/Sentis provider integration;
- no canonical/provider runtime refactor;
- no detector densification;
- no duplicate CPU+GPU inference every frame;
- no removal or weakening of current MediaPipe CPU fallback;
- no global D3D12 production switch based on the Sentis spike;
- no lowering model quality merely to hit an FPS number.
