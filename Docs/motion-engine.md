# Planned V1 Motion Engine

Status: **PHASE 4 CORRECTION — AWAITING USER QA / NOT USER ACCEPTED.** Phase 1 accepted SHA: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6`. Phase 2 accepted SHA: `f5a15648607adf6034800c6a2b4d685b0e6f03ea`. Phase 3 accepted SHA: `2ee4d6eb606a8b845183cc44126ecf9530d8280b`. The pre-correction Phase 4 handoff HEAD was `4a26589ec2f90688b80fb6b1da0b849adda65d6b`. Phase 5 locomotion has not started.

> **Phase 4 warning:** the correction has not yet passed USER visual/motion QA or Orchestrator audit. Treat it as the current candidate architecture, not an accepted checkpoint.

## Phase 1 spike boundary

- `MediaPipePoseProvider` owns the WebCamTexture capture, MediaPipe Tasks API integration, CPU configuration, cadence limiting, async result callback, and per-landmark trust classification.
- `PoseObservation` is the raw provider-boundary observation type. It remains upstream-only and is not a gameplay contract.
- `MediaPipeCanonicalPoseMapper` converts the raw provider observation into the engine-owned `CanonicalPoseFrame`. It is the only Phase 2 runtime mapping location that knows the 33-landmark source indices.
- `PoseTrackingSpikePresenter` is a diagnostic consumer that draws the camera texture, raw and canonical trusted landmarks/connections, a canonical local-space 3D view, and runtime statistics.
- The current Windows integration uses the repository-local MediaPipeUnityPlugin `0.16.3` CPU prebuilt runtime and a local Pose Landmarker Lite model. Windows support is documented as experimental by the plugin, so the Orchestrator should treat the USER’s physical QA as the acceptance authority.
- The spike accepts partial bodies through per-landmark trust. Missing or untrusted lower-body landmarks do not invalidate trusted upper-body observations.

## Motion Engine runtime boundary

`ICanonicalPoseSource` is the narrow provider-independent source contract. The MediaPipe adapter maps the latest provider observation into `CanonicalPoseFrame`; `MotionEngineRuntime` then owns the single canonical source -> stabilization -> calibration -> rotation path. The presenter, retargeter, and future gameplay consumers read runtime-owned outputs rather than rebuilding stages or depending on MediaPipe structures.

## Camera

- Webcam capture is integrated into the Unity application.
- Approximately 640x480 is the initial processing target.
- Camera capture may run around 30 FPS, independently of pose inference and Unity rendering.
- Raw webcam video is not stored or recorded by default.

## Pose Provider

- MediaPipe Pose Landmarker is the planned V1 backend.
- Processing is local, CPU-oriented, and single-person.
- Approximately 20 usable pose results per second is the initial target.
- Pose inference frequency remains independent from Unity's rendering frequency.
- Consumers should use the latest usable pose rather than allowing an unbounded inference backlog.
- MediaPipe is replaceable; downstream game and course systems must not be rewritten when the backend changes.

## Raw pose boundary

The upstream MediaPipe model provides approximately 33 pose landmarks. MediaPipe-specific result structures must remain inside the provider/integration boundary. They are not the canonical data contract for downstream systems.

## Canonical skeleton

Phase 2 exposes a smaller engine-owned body representation suitable for later filtering, reconstruction, retargeting, and locomotion interpretation. The current exact 20-joint set is:

`Pelvis, Spine, Chest, Head, LeftShoulder, LeftElbow, LeftWrist, RightShoulder, RightElbow, RightWrist, LeftHip, LeftKnee, LeftAnkle, LeftHeel, LeftToe, RightHip, RightKnee, RightAnkle, RightHeel, RightToe`.

Direct joints are mapped from the required MediaPipe source landmarks. Pelvis is the trusted midpoint of both hips, Chest is the trusted midpoint of both shoulders, and Spine is the trusted midpoint of Pelvis and Chest. Derived confidence is the minimum input confidence. A missing joint leaves that canonical joint unavailable without invalidating the rest of the frame.

Canonical image coordinates are x left-to-right and y bottom-to-top. Canonical 3D uses +X camera/view right, +Y up, and +Z away from the camera. The canonical inference frame is the correctly oriented image presented to MediaPipe after sensor/storage and pixel preparation, before optional display-only mirroring. `MediaPipeCanonicalPoseMapper` performs only `canonicalImage=(x,1-y)` and `canonicalWorld=(x,-y,z)`; it does not inverse-transform returned data using input H/V/rotation. World positions are converted once and are pelvis-relative when a trusted canonical pelvis exists; if the pelvis is unavailable, available source-world data remains in the provider's hip-centered frame. Phase 3 filters canonical image positions and canonical world positions independently per joint, then rebuilds stabilized local/root-relative positions from stabilized world positions. If the pelvis is unavailable, tracked joints remain usable in the available hip-centered frame.

### Explicit coordinate spaces

1. **WebCamTexture / sensor space** — physical camera pixels and Unity texture orientation.
2. **Pixel transport space** — the texture memory/readback layout after Unity-to-MediaPipe H/V preparation.
3. **Canonical inference/image frame** — the correctly oriented image actually interpreted by MediaPipe.
4. **Raw MediaPipe normalized coordinates** — normalized landmark output in the canonical inference frame, with Y top-down.
5. **Raw MediaPipe world coordinates** — metric pose-world output in the canonical inference frame, with Y negative above the hip-centered origin.
6. **Golden Needle canonical image space** — normalized X right, Y up.
7. **Golden Needle canonical 3D space** — X right, Y up, Z away.
8. **Display space** — GUI presentation after optional user-facing mirror/layout.

Every conversion names its input and output space. Pixel preparation is used to construct the canonical inference frame; it is not automatically reapplied to raw world coordinates.

## Camera orientation and debug views

The provider separates sensor/storage metadata, MediaPipe pixel preparation, canonical data conversion, and display presentation. Front-facing status still participates in MediaPipe's input transformation, whose H/V flags perform real pixel transforms in the embedded plugin. It no longer implies an additional horizontal *presentation* correction. Raw/canonical 2D overlays are converted from inference coordinates back through the inverse inference preparation to sensor/display coordinates before GUI placement. The resulting display-texture normalized coordinate is then converted once into Unity IMGUI's top-down screen convention by `guiY = 1 - displayY`. Display mirroring is explicit and disabled by default; OFF adds no X flip, while ON mirrors preview and overlays together at presentation only and never changes canonical left/right or world semantics. The debug spike exposes raw, canonical 2D, canonical 3D, and stabilized canonical 2D views with `F1`, `F2`, `F3`, and `F4`; `C` begins calibration, `X` cancels/resets it, `R` retries camera/model startup, and `F6` toggles the raw-world/canonical coordinate inspector with per-pair anatomical and legacy-bilateral Y diagnostics. The Lab reports thresholded anatomical-chain 2D↔3D X/Y agreement counts and PASS/FAIL/PENDING state.

## Calibration

Phase 4 supersedes the old hard bilateral T-pose gate with modular measurement calibration. The state flow is `Idle -> AwaitingBodyReference -> SamplingBodyReference -> AcquiringGeometry -> Ready`. **Usability begins when the body reference is valid**, even if the state remains `AcquiringGeometry` because some optional chains are still missing.

Body reference uses only pelvis/chest plus bilateral shoulders/hips and a short comfortable stable hold. It captures shoulder width, hip width, torso length, neutral pelvis/chest/shoulder/hip positions, and semantic Right/Up/Forward. Knees, ankles, elbows, wrists, and a special T-pose are not required for this stage.

LeftArm, RightArm, LeftLeg, and RightLeg accumulate independently on stabilized frames when their own root/mid/tip joints have trustworthy 3D positions. Each sample measures `upperLength = |root-mid|` and `lowerLength = |mid-tip|`; `reach = upperLength + lowerLength`. Segment lengths and optional segment reference directions are confidence-weighted across a short fixed sample count (default 8). A bent arm is therefore not shortened to the shoulder→wrist chord, and one unavailable chain never resets or invalidates another.

The profile schema is version `5`. It carries `bodyReferenceValid` plus four independent `MotionCalibrationChainGeometry` records. Compatibility `isValid` now means body-reference usability only, not all-chain completeness. The Lab displays body readiness and per-chain READY/sample/waiting reasons instead of an opaque permanent T-pose 0%.

## Confidence and filtering

- `CanonicalStabilizerSettings` centralizes acquire confidence `0.60`, sustain confidence `0.40`, acquire samples `2`, loss grace `0.10 s`, and reset-after-loss `0.25 s`.
- Each canonical joint has independent acquisition, filter, dropout, loss, and reacquisition state. A joint must meet the acquire threshold for consecutive samples, uses the lower sustain threshold while active, preserves the prior stabilized sample during the grace window, becomes unavailable after grace, and resets filters after the reset interval before reacquiring from the new sample.
- Project-owned pure One Euro filters use min cutoff `1.0`, beta `0.05`, and derivative cutoff `1.0`. The derivative is filtered first, then drives the dynamic cutoff for the position low-pass. Actual pose/received timestamps provide delta time; zero, negative, large, and non-finite intervals are sanitized/clamped.
- Lost or occluded joints fail gracefully without stale indefinite tracking or violent snapping. Quaternion interpolation remains a downstream rotation concern and is not part of Phase 3.

## Phase 4 canonical rotation reconstruction

The project-owned `CanonicalRotationFrame` has ten bones: Pelvis, Chest, LeftUpperArm, LeftLowerArm, RightUpperArm, RightLowerArm, LeftUpperLeg, LeftLowerLeg, RightUpperLeg, and RightLowerLeg. Each output includes tracking, confidence, optional vectors, and a `rotationDeltaFromCalibration`; the frame is preallocated and independently partial-body valid. Phase 4 retains this frame for torso orientation, diagnostics, and future orientation consumers; it is not the primary limb-pose contract.

For diagnostic `CanonicalRotationFrame` limb swing, each calibrated chain contributes independently sampled upper/lower reference segment directions. Runtime directions remain shoulder→elbow/elbow→wrist or hip→knee/knee→ankle. Uncalibrated chains simply omit those diagnostic bones. Production limb posing continues to use positional `CanonicalKinematicTargets`, not these diagnostic rotations.

Pelvis and chest rotation diagnostics use Right = right hip/shoulder minus left hip/shoulder and Up = chest minus pelvis, followed by finite validation and orthonormalization. The calibration profile still records canonical +X right, +Y up, +Z away with semantic frontal forward at -Z. Because that semantic triple may be reflected, `CanonicalRotationSolver` no longer tries to force the semantic forward into a quaternion; its body quaternion is a proper rotation derived from Right + Up. The signed semantic forward is consumed by the explicit retarget map instead.

All limb and torso outputs are swing-only. Monocular webcam input does not reliably observe forearm pronation/supination or upper-arm axial roll, so the implementation does not invent those twists. Head, hand, and foot orientation are not driven in Phase 4.

## Phase 4 humanoid retargeting

`CanonicalKinematicTargetBuilder` consumes stabilized canonical positions once the body reference is usable, then evaluates calibration per chain. A chain is emitted only when its own `MotionCalibrationChainGeometry` is valid; its source reach is the calibrated upper+lower segment sum. Root plus effector remains the minimum live positional target, and the current mid is preferred for the bend plane. An uncalibrated or currently unavailable chain does not block other chains.

`HumanoidRigBinding` captures the actual root/mid/tip Transform for each explicit or Animator Humanoid chain, bind local rotations, upper/lower world lengths, total reach, original local positions/scales, and an immutable target semantic reference basis. Explicit/procedural binding derives that basis from bound joint geometry exactly as before. Animator Humanoid preserves geometric Right/Up but obtains semantic Forward from a reference-time `HumanPoseHandler.GetHumanPose` body orientation. The cached basis is cleared/rebuilt only with the binding so driven transforms cannot feed back into reference characterization. `MotionEngineRuntime` owns the preallocated target output, and `HumanoidRetargeter` consumes stabilized canonical positions plus those targets in `LateUpdate`.

Production mapping uses one explicit signed-axis transform `M`. The source basis preserves calibration Right/Up/Forward even when its handedness is negative. Explicit/procedural target binding retains its deterministic geometry-only proper basis. Animator Humanoid target binding preserves anatomical Right/Up from bound joints but disambiguates semantic Forward from a reference-time `HumanPoseHandler.GetHumanPose` body orientation; the target semantic basis may therefore also be reflected. This is a linear vector map, not a quaternion pretending to represent a reflection.

```text
normalizedEffector = (sourceEffector - sourceRoot) / sourceReach
normalizedHint = (sourceMid - sourceRoot) / sourceReach

mappedEffector = M(normalizedEffector)
mappedHint = M(normalizedHint)

targetEffector = currentTargetRootPosition + mappedEffector * targetReach
targetHint = currentTargetRootPosition + mappedHint * targetReach

c = clamp(distance(root, targetEffector), abs(a-b)+epsilon, (a+b)-epsilon)
n = normalize(targetEffector - root)
x = (a² - b² + c²) / (2c)
y = sqrt(max(a² - x², 0))
solvedMid = root + n*x + p*y
solvedTip = root + n*c
```

Pelvis/chest are derived from live canonical lateral + up axes and converted through the same signed map. Torso application computes a proper Quaternion **delta between the cached target semantic reference basis and the mapped live semantic basis**, so it remains valid whether the Animator target basis is proper or reflected. This avoids both the old reflected-source quaternion problem and any live/reference-forward hemisphere forcing during large yaw/side views. The previous per-chain quaternion characterization/current-parent implementation remains compatibility-only and is not the production `LateUpdate` path.

The bend direction `p` uses the current mapped hint, previous valid plane, calibrated/reference body axis, then a deterministic orthogonal fallback. Sign continuity protects near-degenerate planes while a clearly opposite valid hint can intentionally change sides. Pelvis/chest are applied first; each live chain restores root/mid bind-local rotations, rotates the root toward `root -> solvedMid`, then rotates the mid toward `mid -> solvedTip`. The actual Transform hierarchy—not a fake 2D drawing—is the result. Invalid chains return toward bind/reference and do not freeze forever. Diagnostics keep retarget fidelity, IK endpoint residual, and bend-plane error as separate metrics. `CanonicalRotationFrame` remains available for torso orientation, diagnostics, and future orientation layers.

Only rotations are written. Avatar root position, authored local bone positions, and local scales remain unchanged. An unavailable bone returns toward its bind/reference rotation over a centralized approximately `0.20 s` fallback window. No general quaternion smoothing was added on top of Phase 3 positional stabilization.

The procedural `DebugAvatarRoot` remains the primary acceptance harness and has passed USER F3/F5 retarget QA. Its authored hierarchy and explicit binding behavior are unchanged by the Animator semantic-forward correction. The first real Animator Humanoid validation has also been performed with NekoLegends: structural binding and limb responsiveness PASS, while the initial body-forward orientation failed and is the focused correction pending USER recheck.

## Locomotion

Locomotion is a separate subsystem, likely CharacterController-based initially. Candidate interpreted actions include:

- walking or jogging in place;
- body heading;
- lateral stepping;
- crouching;
- jumping.

Exact movement mapping is **OPEN / MAY CHANGE** until prototypes establish responsive, safe, and CPU-appropriate behavior.

## Performance boundaries

- The baseline must not require a dedicated GPU.
- There is no face model in V1.
- There is no detailed finger/hand model in V1.
- There is no segmentation in V1 unless later justified.
- Expensive inference must not stall Unity's rendering loop.
- Unnecessary allocations and frame copies should be avoided where practical.
- Phase 4 adds no inference/capture queue, pose history, or per-frame reflection. Rotation output and rig references are preallocated/cached, and the debug hierarchy is built once at startup rather than reconstructed per frame.
