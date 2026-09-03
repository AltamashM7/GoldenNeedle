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

The provider separates sensor/storage metadata, MediaPipe pixel preparation, canonical data conversion, and display presentation. Front-facing status still participates in MediaPipe's input transformation, whose H/V flags perform real pixel transforms in the embedded plugin. It no longer implies an additional horizontal *presentation* correction. Raw/canonical 2D overlays are converted from inference coordinates back through the inverse inference preparation to sensor coordinates before the existing display transformation is applied. Display mirroring is explicit and disabled by default; OFF adds no X flip, while ON mirrors preview and overlays together at presentation only and never changes canonical left/right or world semantics. The debug spike exposes raw, canonical 2D, canonical 3D, and stabilized canonical 2D views with `F1`, `F2`, `F3`, and `F4`; `C` begins calibration, `X` cancels/resets it, `R` retries camera/model startup, and `F6` toggles the raw-world/canonical coordinate inspector with per-pair anatomical and legacy-bilateral Y diagnostics. The Lab reports thresholded anatomical-chain 2D↔3D X/Y agreement counts and PASS/FAIL/PENDING state.

## Calibration

Phase 3 provides an in-memory `MotionCalibrationSession` with the state flow `Idle -> Awaiting Neutral -> Sampling Neutral -> Awaiting T-Pose -> Sampling T-Pose -> Complete`, plus cancel/reset. Neutral sampling requires both shoulders, both hips, both knees, and both ankles; it uses a continuous stable hold of approximately `1.25 s`. The head is optional and wrists are not required for neutral capture. T-pose sampling requires both shoulders, elbows, wrists, and hips; it checks arm direction, shoulder-height wrists, extension, and an angular tolerance of approximately `25°` over approximately `0.75 s`. Invalid or moving poses reset stage progress and continue waiting.

The in-memory profile stores valid neutral pelvis/chest/shoulder/hip/knee/ankle references, shoulder/hip widths, torso length, neutral body axes, confidence-weighted T-pose shoulder/elbow/wrist geometry for both arms, T-pose arm directions/span, independent left/right arm and leg reaches, timestamp/state/version, and sample counts. The profile schema is version `4`. These are user reference measurements, not avatar bone lengths; calibration must preserve the avatar's own proportions.

## Confidence and filtering

- `CanonicalStabilizerSettings` centralizes acquire confidence `0.60`, sustain confidence `0.40`, acquire samples `2`, loss grace `0.10 s`, and reset-after-loss `0.25 s`.
- Each canonical joint has independent acquisition, filter, dropout, loss, and reacquisition state. A joint must meet the acquire threshold for consecutive samples, uses the lower sustain threshold while active, preserves the prior stabilized sample during the grace window, becomes unavailable after grace, and resets filters after the reset interval before reacquiring from the new sample.
- Project-owned pure One Euro filters use min cutoff `1.0`, beta `0.05`, and derivative cutoff `1.0`. The derivative is filtered first, then drives the dynamic cutoff for the position low-pass. Actual pose/received timestamps provide delta time; zero, negative, large, and non-finite intervals are sanitized/clamped.
- Lost or occluded joints fail gracefully without stale indefinite tracking or violent snapping. Quaternion interpolation remains a downstream rotation concern and is not part of Phase 3.

## Phase 4 canonical rotation reconstruction

The project-owned `CanonicalRotationFrame` has ten bones: Pelvis, Chest, LeftUpperArm, LeftLowerArm, RightUpperArm, RightLowerArm, LeftUpperLeg, LeftLowerLeg, RightUpperLeg, and RightLowerLeg. Each output includes tracking, confidence, optional vectors, and a `rotationDeltaFromCalibration`; the frame is preallocated and independently partial-body valid. Phase 4 retains this frame for torso orientation, diagnostics, and future orientation consumers; it is not the primary limb-pose contract.

Arms use the Phase 3 profile T-pose left/right arm directions as reference directions for both upper and lower segments. Runtime current directions are shoulder -> elbow, elbow -> wrist, and the corresponding right-side pairs. Legs use neutral hip -> knee, knee -> ankle directions. Each solve requires tracked finite joints and uses the minimum required confidence. Limb posing consumes the separate positional `CanonicalKinematicTargets` output.

Pelvis and chest rotation diagnostics use Right = right hip/shoulder minus left hip/shoulder and Up = chest minus pelvis, followed by finite validation and orthonormalization. The calibration profile still records canonical +X right, +Y up, +Z away with semantic frontal forward at -Z. Because that semantic triple may be reflected, `CanonicalRotationSolver` no longer tries to force the semantic forward into a quaternion; its body quaternion is a proper rotation derived from Right + Up. The signed semantic forward is consumed by the explicit retarget map instead.

All limb and torso outputs are swing-only. Monocular webcam input does not reliably observe forearm pronation/supination or upper-arm axial roll, so the implementation does not invent those twists. Head, hand, and foot orientation are not driven in Phase 4.

## Phase 4 humanoid retargeting

`CanonicalKinematicTargetBuilder` consumes stabilized canonical positions and the valid in-memory profile to produce four independent positional targets: LeftArm, RightArm, LeftLeg, and RightLeg. Each target stores source root/mid/effector positions, source reach, confidence, normalized effector displacement, optional normalized bend-hint displacement, and timing. Root plus effector is the minimum valid chain; the current mid is preferred for the bend plane.

`HumanoidRigBinding` captures the actual root/mid/tip Transform for each explicit or Animator Humanoid chain, bind local rotations, upper/lower world lengths, total reach, original local positions/scales, and a proper target anatomical reference basis from actual bound joint positions. That target basis is cached during `CaptureReferencePose()`, returned immutably during live retargeting, and cleared/rebuilt with the binding so already-driven transforms cannot feed back into the next canonical-to-avatar map. `MotionEngineRuntime` owns the preallocated target output, and `HumanoidRetargeter` consumes stabilized canonical positions plus those targets in `LateUpdate`.

Production mapping uses one explicit signed-axis transform `M`. The source basis preserves calibration Right/Up/Forward even when its handedness is negative; the target basis is proper/right-handed. This is a linear vector map, not a quaternion pretending to represent a reflection.

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

Pelvis/chest are derived from live canonical lateral + up axes, converted through the same signed map to a proper target-body rotation, and applied as a delta from the avatar's reference body basis. This avoids the old reflected source quaternion and avoids forward-hemisphere forcing during large yaw/side views. The previous per-chain quaternion characterization/current-parent implementation remains compatibility-only and is not the production `LateUpdate` path.

The bend direction `p` uses the current mapped hint, previous valid plane, calibrated/reference body axis, then a deterministic orthogonal fallback. Sign continuity protects near-degenerate planes while a clearly opposite valid hint can intentionally change sides. Pelvis/chest are applied first; each live chain restores root/mid bind-local rotations, rotates the root toward `root -> solvedMid`, then rotates the mid toward `mid -> solvedTip`. The actual Transform hierarchy—not a fake 2D drawing—is the result. Invalid chains return toward bind/reference and do not freeze forever. Diagnostics keep retarget fidelity, IK endpoint residual, and bend-plane error as separate metrics. `CanonicalRotationFrame` remains available for torso orientation, diagnostics, and future orientation layers.

Only rotations are written. Avatar root position, authored local bone positions, and local scales remain unchanged. An unavailable bone returns toward its bind/reference rotation over a centralized approximately `0.20 s` fallback window. No general quaternion smoothing was added on top of Phase 3 positional stabilization.

The procedural `DebugAvatarRoot` is an acceptance harness, not an art asset. Its T-pose hierarchy uses authored local offsets and simple primitive segment visuals. A dedicated runtime camera renders that actual hierarchy into the Lab's procedural-rig panel, with distinct desired wrist/ankle and elbow/knee world-space markers. Compact diagnostics identify rig presence, binding, driving, rotation solve, `Kinematic targets`, `Source chains valid x/4`, `Targets generated x/4`, `IK chains solved x/4`, `Limb bones driven x/8`, and the separate retarget-fidelity, IK-endpoint-residual, and bend-plane metrics. `F5` starts OFF, enables/disables live IK driving, and returns the same rig to bind pose when disabled; `F1`–`F4`, `R`, `C`, and `X` remain available. Directional and geometric EditMode tests inspect actual wrist/ankle positions, elbow/knee bend geometry, asymmetric sides, chain isolation, fixed root, unchanged local positions/scales, current-parent behavior, and torso ordering. The Animator Humanoid path is structural only in this phase and has not been physically tested against a model asset.

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
