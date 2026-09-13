# Golden Needle — Inference Architecture Reuse Audit + Modular Skeleton Design

Date: 2026-09-13

Status: architecture/research checkpoint only. No production provider, canonical runtime, retargeting, locomotion, scene, or Phase 5A behavior is changed by this audit.

## 1. Problem statement

Golden Needle's accepted motion engine currently gets useful semantics from MediaPipe Pose Landmarker Lite: 33 normalized pose landmarks, 33 world landmarks, visibility/presence behavior, detector-driven ROI acquisition, landmark-driven ROI tracking, and MediaPipe's decode/projection logic. The responsiveness problem is that the complete production Tasks pipeline commonly produces only about 10–12 fresh pose results/s on the low-end proof laptop, while isolated OpenVINO execution of the exact recurring landmark network is much faster.

The wrong response would be to rebuild the complete MediaPipe detector/ROI/tracking/landmark-decoding pipeline by hand simply to reach OpenVINO. That would duplicate mature calculators, create a large semantic-equivalence burden, and make future landmark expansion harder.

The required direction is therefore reuse-first:

1. preserve mature MediaPipe graph/calculator semantics when practical;
2. replace only neural inference execution where OpenVINO has already proven valuable;
3. make provider outputs schema-driven so richer landmark providers can be added without forcing the whole engine to adopt one fixed landmark count;
4. keep the accepted current 33-pose -> 20-canonical path as a compatibility profile, not as the forever topology.

## 2. Current USER benchmark evidence

Authoritative low-end proof machine:

- Windows 10 build 19045 x64;
- Intel Core i3-7100U @ 2.40 GHz;
- Intel HD Graphics 620;
- Python 3.14 x64;
- OpenVINO 2026.3.0.

Exact production model identity passed:

- task bundle SHA-256 `59929e1d1ee95287735ddd833b19cf4ac46d29bc7afddbbf6753c459690d574a`;
- exact `pose_landmarks_detector.tflite` SHA-256 `ad6cfd3c903eb31a4ee788b809e45ecf9fa69923b69b9f3f2d9ae616ff433e58`.

Precision-controlled raw-landmark results:

| Profile | Effective precision/mode | Mean | p50 | p95 | p99 | Serial rate |
|---|---|---:|---:|---:|---:|---:|
| OpenVINO `CPU_DEFAULT` | FP32 / PERFORMANCE | 10.355 ms | 9.988 ms | 12.233 ms | 15.809 ms | 96.569/s |
| OpenVINO `GPU_DEFAULT` | FP16 / PERFORMANCE | 8.793 ms | 8.738 ms | 9.131 ms | 9.564 ms | 113.726/s |
| OpenVINO `GPU_ACCURACY_FP32` | FP32 / ACCURACY | 12.858 ms | 12.717 ms | 14.012 ms | 14.260 ms | 77.775/s |

Representative human-pose input consistency:

- CPU FP32 vs GPU default FP16:
  - `[1,195]` normalized MAE ~0.00337486; normalized RMS ~0.00376357;
  - `[1,117]` normalized MAE ~0.0129529; normalized RMS ~0.0145266.
- CPU FP32 vs GPU forced FP32:
  - `[1,195]` normalized MAE ~1.00341e-06; normalized RMS ~1.50298e-06;
  - `[1,117]` normalized MAE ~2.09536e-06; normalized RMS ~2.38752e-06.

Current interpretation:

- raw OpenVINO landmark feasibility: **PASS**;
- OpenVINO Intel HD 620 GPU compatibility: **PASS**;
- default GPU differences are predominantly reduced-precision behavior;
- OpenVINO CPU FP32 is the leading low-end inference candidate because it is substantially faster than the current raw Sentis result while keeping FP32 behavior;
- GPU FP16 remains a possible accelerated profile for stronger hardware after semantic validation;
- forced GPU FP32 is not attractive on this HD 620 because it is slower than CPU FP32;
- these are raw recurring-landmark timings, **not** complete detector + tracking + pose semantics throughput;
- production integration remains **NOT APPROVED**.

## 3. Current production constraints and modularity gap

Production behavior that must stay frozen through the next proofs:

- current MediaPipe Tasks CPU provider remains the safe fallback;
- one pose, segmentation disabled, world landmarks required;
- latest useful frame wins; no backlog/replay/catch-up queue;
- accepted canonical coordinates remain +X right, +Y up, +Z away;
- accepted Phase 3 stabilization, Phase 4 calibration/orientation/retargeting, and Phase 5A implementation are not to be changed merely to accommodate a new inference runtime;
- Phase 5A remains not USER accepted; Phase 6 is not started.

The source audit shows a good seam but two topology locks:

- `PoseObservation` is explicitly MediaPipe-specific and allocates exactly 33 samples through `LandmarkCount = 33`;
- `MediaPipeCanonicalPoseMapper` correctly centralizes MediaPipe source indices and derives pelvis/chest/spine, which is good separation;
- `CanonicalPoseFrame` defines the current 20-joint `CanonicalJointId` enum and allocates exactly `JointCount = 20`;
- `MediaPipeCanonicalPoseSource` directly performs the current observation -> mapper -> stabilization/calibration flow.

The problem is not that the current implementation is wrong. The problem is that generic future providers/consumers could accidentally inherit `33` and `20` as universal constants. The migration must preserve current behavior while moving those counts behind explicit schemas/definitions.

## 4. Reuse alternatives evaluated

### 4.1 MediaPipe graph/calculators + OpenVINO inference in-process — preferred proof direction

This is technically credible and best aligned with the USER requirement.

Current Google MediaPipe source still makes neural inference a narrow node inside larger mature graphs. At MediaPipe `master` commit `d4f197be5af7c0db497a9eea6ee07d1d4710d2be` (2026-09-11):

- `pose_detector_graph.cc` performs MediaPipe image preprocessing, calls `AddInference(...)`, then keeps SSD anchors, tensor-to-detection decode, NMS, letterbox removal, detection-to-rect and rect transformation as MediaPipe calculators;
- `pose_landmarks_detector_graph.cc` performs MediaPipe image preprocessing, calls `AddInference(...)`, then keeps tensor splitting, pose presence thresholding, landmark decode, heatmap refinement, world-landmark decode, visibility/presence copy, letterbox removal, image/world projection, and next-frame ROI derivation as MediaPipe calculators.

This is exactly the reuse boundary Golden Needle wants: replace the inference node, not the pose semantics around it.

Intel's OpenVINO MediaPipe work provides two useful references:

1. an OVMS-backed `OpenVINOInferenceCalculator` / session path; and
2. a direct OpenVINO Runtime calculator under `mediapipe/calculators/openvino/` that includes `<openvino/openvino.hpp>` and compiles through `ov::Core` in-process.

The Intel pose graph demonstrates the important semantic point: it replaced the inference node while retaining MediaPipe `ImageToTensorCalculator`, `TensorsToPoseLandmarksAndSegmentation`, ROI projection, auxiliary landmarks and world-landmark processing.

However, do **not** adopt the Intel fork wholesale. Its README states that it is based on MediaPipe v0.10.3, while MediaPipeUnityPlugin 0.16.3 uses MediaPipe 0.10.22. The Intel direct calculator is also a 2023 prototype: it hard-codes `CPU`, uses synchronous inference and has TODOs for device/performance configuration. The fork's default branch head checked during this audit is `b27f0ca7d4fe36175a9f725844cb64b3d3e18959`, with its last commit dating to 2023, even though Intel's current 2026 docs still reference the integration concept.

**Recommendation:** port/adapt the small direct OpenVINO inference-calculator concept into the MediaPipe 0.10.22/Homuler native build used by Golden Needle, then patch/copy only the minimal current Tasks graph builder pieces needed to substitute inference. Reuse current MediaPipe calculators and semantics unchanged wherever possible.

### 4.2 OpenVINO Model Server / MediaPipe sidecar — technically viable, not preferred

Current OVMS 2026 documentation supports MediaPipe graphs, stateful streaming, native/portable Windows packaging and OpenVINO inference. It is a valid fallback/reference architecture.

For a local latency-sensitive Unity product it adds costs that the direct in-process route does not need:

- another process/service lifecycle;
- gRPC/REST/streaming IPC and serialization boundaries;
- startup/readiness/error-recovery coordination;
- a larger packaging/install surface;
- a less natural offline game UX;
- host-memory graph exchanges plus device transfers when GPU inference is selected.

Use OVMS only if the in-process native build proves impractical or if later product requirements justify process isolation/model serving. Do not choose it merely because Intel's first MediaPipe examples used OVMS infrastructure.

### 4.3 TFLite/LiteRT OpenVINO delegate — rejected as primary route

`openvinotoolkit/tflite_openvino_delegate` is archived/read-only. Its README explicitly says Intel no longer guarantees maintenance, fixes or releases and notes a port toward the LiteRT API.

A current maintained Intel/Google LiteRT path exists through LiteRT CompiledModel for newer Intel NPUs, but the current documentation targets newer NPU platforms rather than this Intel HD 620 GPU/CPU use case. No maintained drop-in delegate was found that makes the archived delegate a better Golden Needle path than direct OpenVINO Runtime.

Therefore the archived delegate is not an acceptable primary production dependency.

### 4.4 Current official MediaPipe GPU path on Windows — not a safe target

Current MediaPipeUnityPlugin documentation still marks Windows runtime as experimental and states that GPU inference mode is unsupported on Windows in its supported desktop distribution. Current Google Tasks documentation likewise does not provide the relevant supported Windows desktop GPU delegate path for this project.

Do not spend time forcing an unsupported MediaPipe GPU delegate on Windows while OpenVINO has already demonstrated supported CPU/GPU execution on the actual machine.

### 4.5 RTMPose / MMPose + MMDeploy/OpenVINO — future provider, not first migration

MMPose currently documents RTMPose deployment through MMDeploy/OpenVINO, and RTMPose WholeBody model families provide 133 2D keypoints covering body/face/hands.

Advantages:

- substantially richer 2D keypoint topology;
- mature OpenMMLab pose ecosystem;
- documented OpenVINO deployment path;
- Apache-2.0 project licensing.

Migration costs:

- current WholeBody output is a 2D keypoint contract, not the same real-world 3D/world-landmark contract Golden Needle currently consumes from MediaPipe;
- detector, preprocessing, keypoint decoding, tracking/temporal ROI behavior and confidence semantics differ;
- the existing Phase 4 calibration/retarget assumptions would need an explicit semantic migration rather than an index swap;
- published performance claims are on much stronger hardware and cannot be projected onto the i3-7100U;
- MMDeploy's Windows documentation is less mature for OpenVINO than its Linux path: its Windows build guide identifies ONNX Runtime/TensorRT as verified while OpenVINO remains a weaker Windows integration path.

Keep RTMPose WholeBody as an additive future provider candidate after Golden Needle has a schema-driven provider/canonical boundary. Do not use it to justify discarding working MediaPipe world semantics today.

### 4.6 MediaPipe Holistic / richer MediaPipe provider — preferred future richness path to investigate

Current Holistic Landmarker exposes pose, face, left/right hand landmarks and world-space pose/hand outputs. MediaPipe's classic Holistic topology is 33 pose + 468 face + 21 landmarks per hand; active Tasks model/schema counts should still be read from the provider schema rather than hard-coded globally.

This is architecturally attractive because it expands hands/fingers/face while remaining within MediaPipe semantics and graph infrastructure. MediaPipeUnityPlugin 0.16.3 already includes Holistic Landmarker support (its latest release includes a Holistic bug fix), reducing ecosystem risk.

The concern is low-end compute cost. Holistic should be a future provider benchmark, not silently replace the current pose-only path. The new provider schema must allow its extra groups to coexist without forcing existing body consumers to understand them.

### 4.7 Manual reconstruction of MediaPipe pose pipeline — last-resort fallback

Manual reconstruction can theoretically be optimized, but it would require Golden Needle to own detector decode, SSD anchors/NMS, ROI generation/rotation, previous-landmark tracking, tensor preprocessing, 39-to-33 landmark refinement, heatmap refinement, presence/visibility behavior, world-landmark handling and image/world projection equivalence.

Current MediaPipe source already implements and maintains those pieces. Rebuilding them now adds correctness and maintenance risk with no evidence that the mature calculators themselves are the bottleneck. Manual implementation should be limited to small, well-specified pieces only if a reuse proof demonstrates that a particular calculator cannot be retained.

## 5. Decision matrix

Legend: Low/Medium/High describes burden/risk/overhead qualitatively; performance claims not measured on the USER machine are not treated as facts.

| Alternative | Preserves current 33/world semantics | Reuses mature ROI/tracking/decoding | Windows feasibility | Unity integration burden | Expected extra latency overhead | Rich-keypoint extensibility | Low-end suitability | Licensing/redistribution complexity | Maintenance risk | Recommendation |
|---|---|---|---|---|---|---|---|---|---|---|
| Current MediaPipe Tasks CPU | Yes | Yes | Already working | Low | Existing full-pipeline cost | Medium | Known-safe but ~10–12 fresh/s | Low/known | Low | **Keep as fallback/reference** |
| Current MediaPipe graph + direct OpenVINO inference, in-process | **Yes, if graph parity proof passes** | **Yes** | Plausible; custom Windows native build required | Medium/High once, then contained | **Lowest new architecture overhead; no IPC** | High through provider schemas | **Leading research path** | Medium native DLL/notices | Medium | **Preferred next architecture proof** |
| MediaPipe graph + OVMS local sidecar | Yes if graph matches | Yes | Supported/packagable | High | Medium/High due process + IPC | High | Possible but unnecessary overhead | High package/service surface | Medium | **Fallback only** |
| Archived TFLite/OpenVINO delegate | Potentially | Yes through existing TFLite graph | Historical | Medium | Potentially low | Medium | Unsupported maintenance story | Medium | **High** | **Reject primary path** |
| RTMPose/WholeBody + OpenVINO | No drop-in world-semantic parity | Different mature stack | Possible; Windows MMDeploy OpenVINO less polished | High | Unknown until target benchmark | **Very high 2D coverage** | Unknown on i3-7100U | Medium; check weights/data terms | Medium/High | **Future alternative provider** |
| MediaPipe Holistic richer provider | Pose semantics closely aligned; richer hand/face semantics | Yes | Same current ecosystem | Medium | Higher model/graph workload, must benchmark | **Very high** | Unknown; likely heavier | Low/Medium; new model assets/notices | Low/Medium | **Preferred future richness provider** |
| Manual MediaPipe-pipeline reimplementation | Only after large equivalence effort | **No** | Technically possible | **Very high** | Could be low, unproven | Depends on our work | Unproven | Our code + model/runtime terms | **Very high** | **Last resort only** |

## 6. Recommended next architecture

### 6.1 Inference/runtime layer

Preferred experimental architecture:

```text
Unity camera/latest frame
        |
        v
Current MediaPipe graph/calculators
  - preprocessing / ImageToTensor
  - detector decode + NMS + ROI
  - previous-landmark ROI tracking
        |
        v
Replaceable inference node
  - MediaPipe/TFLite CPU fallback (existing)
  - OpenVINO CPU FP32 (leading HD620 profile)
  - OpenVINO accelerated profile (optional future hardware path)
        |
        v
Current MediaPipe landmark decode/refinement
  - pose presence
  - heatmap refinement
  - visibility/presence
  - world-landmark handling
  - image/world projection
        |
        v
Provider Landmark Frame
```

On the current HD 620-class target, choose OpenVINO CPU FP32 as the first semantic-equivalence candidate. Do not choose GPU default FP16 merely because its raw latency is ~1.6 ms lower; the precision-controlled evidence shows a meaningful numerical tradeoff on pose-related outputs, while CPU FP32 is already far faster than the current complete Tasks throughput bottleneck. Keep GPU FP16 as a selectable accelerated profile for later hardware after end-to-end semantic and contention testing.

### 6.2 Why in-process is plausible on Windows

It is plausible, not yet proven:

- Homuler already ships/builds a Windows native MediaPipe plugin and supports custom calculators/graphs;
- OpenVINO Runtime has native Windows C++ binaries and the exact landmark model already works on the USER laptop;
- Intel's direct `OpenVINOInferenceCalculator` reference proves the conceptual `mediapipe::Tensor/ov::Tensor -> ov::Core -> ov::Tensor` in-process shape;
- current MediaPipe graphs expose a narrow inference seam through `AddInference(...)` while preserving surrounding calculators.

The real engineering gate is build/link/package compatibility between MediaPipe 0.10.22/Homuler's native plugin and OpenVINO 2026.x, not whether the architecture concept exists.

## 7. Rejected/deferred alternatives

- **Whole Intel MediaPipe fork:** deferred/rejected because its baseline is MediaPipe 0.10.3 and its source branch is old. Reuse concepts/calculators selectively, not the entire fork.
- **OVMS sidecar:** technically valid but rejected as first choice because it adds process/IPC/setup burden without a demonstrated need.
- **Archived TFLite OpenVINO delegate:** rejected as primary dependency; no maintained HD620-target drop-in successor found.
- **Official MediaPipe Windows GPU delegate:** not the current supported path for this desktop/Unity setup.
- **RTMPose/WholeBody replacement:** deferred until provider schemas exist and 3D/world-semantic migration is explicitly justified.
- **Holistic replacement now:** deferred pending low-end workload benchmark; architecturally attractive as an additive richer provider.
- **Manual pipeline reconstruction:** last resort only.

## 8. Modular provider landmark schema design

The provider layer should preserve what the provider actually knows instead of collapsing immediately to the current 20 joints.

Conceptual API:

```text
ProviderLandmarkFrame
  SchemaId / SchemaVersion
  ProviderId
  Timestamp
  CapabilityFlags
  LandmarkGroup[]
  LandmarkSample[]  // capacity/count comes from schema

ProviderLandmarkSchema
  SchemaId / SchemaVersion
  LandmarkDefinition[]
    SemanticId          // stable semantic key, not raw integer index
    DisplayName
    Group               // Body / LeftHand / RightHand / Face / etc.
    ProviderNativeIndex // mapper implementation detail, optional here
    AvailableChannels   // normalized image, model depth, world, confidence...
```

Each `ProviderLandmarkSample` should support independent optional channels:

- normalized image X/Y;
- optional model-relative/depth Z;
- optional world X/Y/Z;
- visibility;
- presence;
- confidence/score;
- validity flags.

Capabilities are explicit rather than implied:

- `HasNormalizedImagePosition`;
- `HasModelDepth`;
- `HasWorldPosition`;
- `HasVisibility`;
- `HasPresence`;
- groups available (Body, LeftHand, RightHand, Face, etc.).

Rules:

- a 2D provider must **not** invent fake world coordinates;
- no generic engine code may assume landmark count 33;
- raw provider integer indices stay in the provider-specific adapter/mapper boundary;
- source detail is retained long enough for later hand/gesture/face consumers even if the active body canonical definition uses only a subset.

The current `PoseObservation` remains untouched initially and becomes the legacy MediaPipe Pose 33 adapter source during migration.

## 9. Modular canonical skeleton design

Introduce explicit versioned canonical definitions rather than one forever enum-sized array.

Compatibility definition:

```text
CanonicalBodyV1
  exactly the current 20 semantic joints
  same coordinate convention: +X right, +Y up, +Z away
  same derived pelvis/chest/spine behavior
  same availability/confidence semantics required by current downstream code
```

Future definitions may include:

- `CanonicalBodyV2`: extra neck/spine/clavicle/foot detail;
- `CanonicalBodyHandsV1`: body plus articulated hand/finger joints;
- specialized definitions only if real consumers require them.

Conceptual types:

```text
CanonicalSkeletonDefinition
  DefinitionId / Version
  JointDefinition[]
    SemanticJointId
    ParentSemanticId? / segment relationships
    RequiredOrOptional
    DerivationRule?      // provider-independent where possible

CanonicalSkeletonFrame
  Definition reference/id
  Timestamp
  JointSample[]          // count from active definition
```

Derived-joint rules such as pelvis midpoint, chest midpoint or spine interpolation should belong to canonical mapping/definition logic, not leak provider indices downstream.

Do not require every definition to be a strict superset of every earlier one. Compatibility is achieved through named semantics and consumer profiles, not array-position coincidence.

## 10. Retarget/game consumer profiles

Consumers should declare semantic requirements instead of assuming a fixed entire canonical array.

Examples:

```text
HumanoidBodyRetargetProfileV1
  requires pelvis, chest, head,
  shoulders/elbows/wrists,
  hips/knees/ankles
  optional feet detail

LocomotionBodyProfileV1
  requires pelvis, left/right hip, left/right foot/ankle support joints

FingerRetargetProfileV1
  requires hand/finger semantics only

GestureProfile
  requests exactly the joints/groups needed by the gesture
```

A consumer can validate `Supports(profile)` against the active canonical definition before running. Adding finger joints to a richer canonical definition then does not require changing the existing torso/arm/leg retarget solver.

Stabilization should also become definition-aware over time: filter available joints/channels by semantic identity, while retaining the current Phase 3 settings for `CanonicalBodyV1` unless a separate evidence gate changes them.

## 11. Mapping layer and `CanonicalBodyV1` migration plan

Target flow:

```text
Provider Landmark Frame
        |
        v
Provider-Specific Semantic Mapper
        |
        v
Canonical Skeleton Definition / Canonical Frame
        |
        +--> Stabilization
        +--> Calibration
        +--> Retarget consumer profiles
        +--> Locomotion profiles
        +--> Gesture systems
```

Provider-specific numeric indices do not pass this boundary.

Migration should be additive and staged:

1. **Freeze current production path.** No behavior change now.
2. **Define schemas/interfaces alongside legacy classes.** Add `MediaPipePose33Schema` and `CanonicalBodyV1Definition` representations without replacing runtime data yet.
3. **Legacy adapter.** Adapt current `PoseObservation` to a schema-driven `ProviderLandmarkFrame` without changing the data values it carries.
4. **Compatibility mapper.** Implement `MediaPipePose33 -> CanonicalBodyV1` with the exact existing `MediaPipeCanonicalPoseMapper` indices and derived pelvis/chest/spine math. Add deterministic parity tests against the legacy mapper.
5. **Compatibility consumer adapter.** Let current stabilization/calibration/retarget/locomotion continue to receive an equivalent 20-joint view. No visual change is expected or accepted merely because the container became schema-driven.
6. **Only after parity:** migrate consumers one-by-one to semantic profiles instead of `JointCount=20` assumptions.
7. **Add richer providers/topologies later** (Holistic first candidate; RTMPose optional) without changing `CanonicalBodyV1` behavior.

The accepted path therefore remains exactly:

```text
MediaPipe Pose 33
-> existing semantics
-> CanonicalBodyV1 (current 20 joints)
-> current stabilization/calibration/retarget/locomotion
```

The architecture work changes where counts live, not what the current 20 joints mean.

## 12. Gate sequence and current execution status

### Gate A — exact detector OpenVINO direct compatibility: COMPLETE / PASS

The planned cheap detector gate has now completed successfully on the exact unchanged production detector:

- exact identity: `pose_detector.tflite`, 2,959,078 bytes, SHA-256 `46837eb883e6ec75b52c5f5ff6a9b78bd35e66c13f95e8c3566c582d146cb1d9` — PASS;
- OpenVINO 2026.3 `Core.read_model()` direct read — SUCCESS;
- contract float32 `[1,224,224,3]` -> `[1,2254,12]` + `[1,2254,1]` — TRUE;
- explicit CPU compile + finite-output sanity inference — SUCCESS;
- explicit GPU compile + finite-output sanity inference — SUCCESS;
- no densification, conversion, alternate model, AUTO/HETERO/MULTI or hidden fallback.

Therefore the exact detector's DENSIFY import failure is a Sentis limitation for this project, **not** an OpenVINO compatibility blocker.

### Gate B — current-MediaPipe standalone graph parity proof: IMPLEMENTED / USER RUN PENDING

Tracked proof:
`Tools/MediaPipeOpenVinoParity/`

The implementation deliberately uses the exact current generation rather than current master for the build proof:
- Homuler `v0.16.3` -> commit `cf4c11d8eef724fe24111b7cd795d55ba490aeec`;
- Homuler pins MediaPipe `v0.10.22` -> commit `c54c06dd8c4314a316c14da31493bcc38ed302e2`;
- Bazel 6.5.0;
- official OpenVINO Runtime C++ 2026.3.0 Windows toolkit.

The exact 0.10.22 audit confirms the same desired seam exists there: detector and landmark graphs route neural execution through `AddInference(...)`, while the surrounding MediaPipe calculators retain preprocessing, detector anchors/decode/NMS/ROI, previous-landmark tracking, landmark tensor split/decode, heatmap refinement, 33-landmark public output, presence/visibility, world-landmark processing, projection and auxiliary semantics.

Three deterministic comparison modes are implemented:
1. `TASKS_REFERENCE` — official 0.10.22 PoseLandmarker CPU on the exact production task bundle;
2. `GRAPH_TFLITE_CPU` — expanded current MediaPipe graph with standard TFLite CPU inference;
3. `GRAPH_OPENVINO_CPU_FP32` — the same graph/calculators with **both detector and landmark inference nodes** switched to an in-process OpenVINO CPU FP32 calculator.

Gate B preserves the exact production task settings: one pose, thresholds 0.5 / 0.5 / 0.5, segmentation false. VIDEO mode is used only for deterministic offline sequence parity; its timing is labelled offline graph-processing capacity, not Unity LIVE_STREAM frame-to-result latency.

The OpenVINO calculator keeps the MediaPipe tensor boundary, compiles once, reuses its request, preserves expected output order/shapes, uses safe host copies for this first proof, and measures bridge-copy time separately. No model conversion, detector densification, quantization change or alternate model family is used.

The runner processes the same fixed decoded frames and deterministic timestamps independently through all backends, retaining each backend's temporal/tracking state. It captures full 33 normalized/world landmarks plus pose presence, visibility/presence, auxiliary availability, ROI/detector continuity where available, startup/first-frame/steady-state timing, and OpenVINO detector/landmark inference/bridge-copy telemetry.

The comparator reports A-vs-B, B-vs-C and A-vs-C semantic/numerical evidence and intentionally does not invent a Golden Needle acceptance tolerance. Final acceptance remains an Orchestrator decision.

The next action is a USER Windows build/run with a short recorded human-motion sequence. Gate B must not be called PASS until those reports exist and have been reviewed.

## 13. Licensing and redistribution notes

Checked project-level licenses:

- OpenVINO Runtime: Apache-2.0;
- Google MediaPipe: Apache-2.0;
- Intel OpenVINO MediaPipe fork additions: Apache-2.0 headers/repository lineage;
- OpenVINO Model Server: Apache-2.0;
- MediaPipeUnityPlugin: MIT, with bundled third-party notices including MediaPipe Apache-2.0;
- MMPose: Apache-2.0;
- MMDeploy: Apache-2.0.

This is favorable for source reuse, but redistribution is not just the top-level license name. A production native plugin must carry applicable OpenVINO/MediaPipe/Homuler third-party notices and include only redistributable DLL/runtime components. New model weights (especially alternative OpenMMLab models) must be checked separately for model-card, dataset and weight-specific terms before shipping; do not assume repository Apache-2.0 automatically covers every downloaded weight/dataset lineage.

Avoid copying the whole Intel MediaPipe fork. If calculator code is reused, preserve its Apache-2.0 headers/notice obligations and document the adaptation.

## 14. Risks and blockers

1. **Native Windows build/link packaging remains unproven in this Builder environment.** Gate B supplies bootstrap/build scripts, but the actual MSVC/Bazel/OpenVINO build must run on the USER Windows machine.
2. **Full-pipeline performance remains unknown until Gate B runs.** Raw neural speed is excellent, but detector cadence and retained MediaPipe calculators may materially change the full result.
3. **Semantic parity is the decisive gate.** Raw tensor consistency is insufficient; Gate B compares final MediaPipe pose/world outputs on real frame sequences.
4. **GPU contention remains a later question.** HD620 is shared with rendering. Gate B intentionally starts with OpenVINO CPU FP32.
5. **Richer-provider cost remains unknown.** Holistic and WholeBody extensibility does not mean they are free on low-end hardware.
6. **Topology migration risk remains.** Genericizing 33/20 too early could destabilize accepted Phase 4 behavior, so the provider/canonical refactor remains design-only until later explicit parity work.
7. **No production approval yet.** Neither Gate A nor the Gate B scaffold authorizes replacing MediaPipe Tasks in Unity.

## 15. Sources checked / provenance

Architecture alternatives were originally audited against current 2026 sources including Google MediaPipe, OpenVINO/OVMS, Intel's MediaPipe reference fork, MediaPipeUnityPlugin v0.16.3, the archived OpenVINO TFLite delegate, MMPose/MMDeploy, and MediaPipe Holistic.

Gate B additionally pinned the executable proof to the exact relevant source generation:
- Homuler MediaPipeUnityPlugin v0.16.3 commit `cf4c11d8eef724fe24111b7cd795d55ba490aeec`;
- Google MediaPipe v0.10.22 commit `c54c06dd8c4314a316c14da31493bcc38ed302e2`;
- OpenVINO Runtime 2026.3.0 official Windows C++ toolkit;
- production task bundle and exact detector/landmark hashes listed above.

Reference URLs retained from the architecture audit:
- Google MediaPipe:
  - https://github.com/google-ai-edge/mediapipe
  - https://github.com/google-ai-edge/mediapipe/blob/master/mediapipe/tasks/cc/vision/pose_detector/pose_detector_graph.cc
  - https://github.com/google-ai-edge/mediapipe/blob/master/mediapipe/tasks/cc/vision/pose_landmarker/pose_landmarks_detector_graph.cc
- OpenVINO MediaPipe / OVMS:
  - https://docs.openvino.ai/2026/model-server/ovms_docs_mediapipe.html
  - https://docs.openvino.ai/2026/model-server/ovms_docs_mediapipe_inference.html
  - https://docs.openvino.ai/2026/model-server/ovms_docs_mediapipe_how_to.html
  - https://github.com/openvinotoolkit/mediapipe
  - https://github.com/openvinotoolkit/model_server
- MediaPipeUnityPlugin:
  - https://github.com/homuler/MediaPipeUnityPlugin
  - https://github.com/homuler/MediaPipeUnityPlugin/releases/tag/v0.16.3
- Archived OpenVINO TFLite delegate:
  - https://github.com/openvinotoolkit/tflite_openvino_delegate
- MMPose/RTMPose/MMDeploy:
  - https://github.com/open-mmlab/mmpose
  - https://mmpose.readthedocs.io/en/latest/user_guides/how_to_deploy.html
  - https://mmdeploy.readthedocs.io/en/latest/04-supported-codebases/mmpose.html
  - https://github.com/open-mmlab/mmdeploy
- MediaPipe Holistic:
  - https://github.com/google-ai-edge/mediapipe/blob/master/mediapipe/tasks/cc/vision/holistic_landmarker/holistic_landmarker_result.h
  - https://github.com/google-ai-edge/mediapipe/blob/master/mediapipe/tasks/cc/vision/holistic_landmarker/holistic_landmarker_graph.cc

## 16. Governance remains unchanged

- no merge to `main` without explicit USER approval;
- no Phase 5A acceptance;
- no Phase 6;
- no production provider integration yet;
- no canonical runtime refactor yet;
- no detector densification;
- no removal of the current MediaPipe CPU fallback.
