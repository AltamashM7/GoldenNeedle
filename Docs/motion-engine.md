# Planned V1 Motion Engine

Status: **PHASE 4 USER ACCEPTED — PASS. PHASE 5A IMPLEMENTED / AWAITING USER QA.** Phase 1 accepted SHA: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6`. Phase 2 accepted SHA: `f5a15648607adf6034800c6a2b4d685b0e6f03ea`. Phase 3 accepted SHA: `2ee4d6eb606a8b845183cc44126ecf9530d8280b`. Phase 4 accepted implementation SHA: `f0c81e84d0a482c40448505f2904af93ef4aa881`.

<!-- PHASE5A_LATEST_RUNTIME_CHECKPOINT:START -->
## Current Phase 5A runtime checkpoint

Starting correction checkpoint: `33698719a2907d30bb3396f66e5b79e59ccbfe9e`.

Second USER QA confirmed the v2 planted-feet fix: idle remained stable, torso leaning with planted feet no longer translated the root, and Phase 4 remained usable. The new blocker was intermittent real walking because v2 required left/right foot displacement to agree before publishing any physical update.

Support tracking v3 preserves composite ankle/heel/toe feet and each foot's recenter reference, but removes the hard consensus gate. It computes:

```text
common       = (leftDelta + rightDelta) / 2
differential = (leftDelta - rightDelta) / 2
```

Common displacement is physical support-centroid motion. Differential displacement is gait/asymmetry evidence. Lateral common X updates continuously; depth common Y remains monocular, requires body-scale corroboration for meaningful movement, and is attenuated as differential foot-Y grows.

Physical scales remain `0.9 / 1.5`. Cadence timing is unchanged.

The Lab adds `F12` as a presentation-only Lab/Game toggle. Lab View keeps the webcam/debug interface. Game View hides webcam/IMGUI and enables the persistent third-person camera that follows avatar root position plus retained mapped Phase 5A heading. Existing F1–F11 states are preserved.

Phase 5A remains **NOT USER ACCEPTED**.
<!-- PHASE5A_LATEST_RUNTIME_CHECKPOINT:END -->

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

Every conversion names its input and output space. Pixel preparation is used to construct the canonical inference frame; it is not automatically reapplied to raw world coordinates. A user-facing display mirror is presentation-only. A horizontal inference mirror is likewise not inferred from camera-facing metadata because it would change the physical image handed to the anatomical landmark model and can reverse Golden Needle's semantic Left/Right identity.

## Camera orientation and debug views

The provider separates sensor/storage metadata, MediaPipe pixel preparation, canonical data conversion, and display presentation. **Front-facing status does not imply a horizontal inference mirror or a horizontal presentation mirror.** For the integrated front-camera baseline, `ImageTransformationOptions.Build` is invoked with `shouldFlipHorizontally=false`; vertical/rotation transport correction remains active as required by Unity texture orientation. This preserves MediaPipe's fixed anatomical landmark IDs against the unmirrored physical camera source. Raw/canonical 2D overlays are converted from inference coordinates back through the inverse inference preparation to sensor/display coordinates before GUI placement. The resulting display-texture normalized coordinate is then converted once into Unity IMGUI's top-down screen convention by `guiY = 1 - displayY`. Display mirroring is explicit and disabled by default; OFF adds no X flip, while ON mirrors preview and overlays together at presentation only and never changes canonical left/right or world semantics. The debug spike exposes raw, canonical 2D, canonical 3D, and stabilized canonical 2D views with `F1`, `F2`, `F3`, and `F4`; `C` begins calibration, `X` cancels/resets it, and `R` retries startup. `F6` is the read-only coordinate/Z-yaw inspector: it retains the raw-world/canonical sample and now also prints calibration/live source bases, semantic shoulder/hip depth dZ, immutable target basis, signed source→target map evidence, mapped live basis, source/mapped/applied torso yaw, and optional Animator foot/toe forward evidence. None of those F6 calculations mutate runtime state or participate in production retarget decisions.

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

Pelvis and chest rotation diagnostics use anatomical Right = right hip/shoulder minus left hip/shoulder and Up = chest minus pelvis, followed by finite validation and orthonormalization. Golden Needle canonical +X means **viewer/camera right**. Therefore, for an unmirrored person facing the webcam, anatomical Right is approximately -X, Up approximately +Y, and frontal anatomical Forward approximately -Z. Calibration now constructs that Forward as `Cross(Right, Up)`, matching `CanonicalRotationSolver.TryBuildBodyRotation()`. The resulting neutral source basis is expected to be proper/handedness +1.

All limb and torso outputs are swing-only. Monocular webcam input does not reliably observe forearm pronation/supination or upper-arm axial roll, so the implementation does not invent those twists. Head, hand, and foot orientation are not driven in Phase 4.

## Phase 4 humanoid retargeting

`CanonicalKinematicTargetBuilder` consumes stabilized canonical positions once the body reference is usable, then evaluates calibration per chain. A chain is emitted only when its own `MotionCalibrationChainGeometry` is valid; its source reach is the calibrated upper+lower segment sum. Root plus effector remains the minimum live positional target, and the current mid is preferred for the bend plane. An uncalibrated or currently unavailable chain does not block other chains.

`HumanoidRigBinding` captures the actual root/mid/tip Transform for each explicit or Animator Humanoid chain, bind local rotations, upper/lower world lengths, total reach, original local positions/scales, and a proper target anatomical reference basis from actual bound joint positions. That target basis is cached during `CaptureReferencePose()`, returned immutably during live retargeting, and cleared/rebuilt with the binding so already-driven transforms cannot feed back into the next canonical-to-avatar map. `MotionEngineRuntime` owns the preallocated target output, and `HumanoidRetargeter` consumes stabilized canonical positions plus those targets in `LateUpdate`.

Production mapping uses one explicit signed-axis transform `M`. The source basis preserves calibration Right/Up/Forward and records handedness explicitly; the target basis is proper/right-handed. The current unmirrored front-camera calibration is expected to produce source handedness +1, so with the validated proper target basis the neutral reference map determinant is expected to be +1. The signed representation remains general enough to diagnose unexpected reflected inputs rather than silently encoding them in a quaternion.

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

The procedural `DebugAvatarRoot` is an acceptance harness, not an art asset. Its T-pose hierarchy uses authored local offsets and simple primitive segment visuals. A dedicated runtime camera renders that actual hierarchy into the Lab's procedural-rig panel, with distinct desired wrist/ankle and elbow/knee world-space markers. Compact diagnostics identify rig presence, binding, driving, rotation solve, `Kinematic targets`, `Source chains valid x/4`, `Targets generated x/4`, `IK chains solved x/4`, `Limb bones driven x/8`, and the separate retarget-fidelity, IK-endpoint-residual, and bend-plane metrics. `F5` starts OFF, enables/disables live IK driving, and returns the same rig to bind pose when disabled; `F1`–`F4`, `R`, `C`, and `X` remain available. Directional and geometric EditMode tests inspect actual wrist/ankle positions, elbow/knee bend geometry, asymmetric sides, chain isolation, fixed root, unchanged local positions/scales, current-parent behavior, and torso ordering. The Animator Humanoid path has been physically tested with the NekoLegends `android01.fbx`: binding, limb response, neutral facing/orientation, and approximately 45° left/right torso yaw all passed USER QA.

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


## Phase 5A embodied hybrid locomotion

Phase 4 canonical pose/retargeting remains accepted and unchanged. Phase 5A adds a parallel locomotion interpretation path.

### Camera-space root tracking

MediaPipe pose-world and canonical local positions remain body/pelvis-relative and are **not** treated as absolute room coordinates. `CameraSpaceRootTracker` uses stabilized canonical **support-foot image positions** as physical room-translation authority.

Each left/right support foot is a weighted centroid of available ankle/heel/toe observations; at least two trusted points are required per foot. Recenter stores each foot's own image reference plus a body-scale reference. Current per-foot X/Y displacement is normalized by that fixed reference scale.

The tracker derives common displacement `(L+R)/2` and differential displacement `(L-R)/2`. Common X is the lateral room-position authority and updates continuously, including the first half of a normal step. Differential motion is retained as gait evidence rather than being used as a global hard gate.

Camera-depth authority remains support-based. Common support-foot Y is the depth candidate. Meaningful nonzero depth still requires same-sign torso apparent-scale evidence, so torso lean cannot initiate Z by itself. Strong differential foot-Y progressively lowers depth reliability between the Inspector thresholds `depthDifferentialStart` and `depthDifferentialFull`, reducing foot-lift contamination without blocking lateral motion. If feet/support disappear temporarily, the tracker holds instead of falling back to torso motion.

A lightweight exponential response filters only trusted support-base displacement/velocity. The first valid calibrated sample with support + body-scale evidence establishes the physical tracking origin automatically.

### Cadence and heading

`CadenceDetector` builds a normalized alternating signal from left/right ankle image-Y separation with knee separation as support. Alternating threshold events produce step intervals; valid interval consistency raises confidence. Cadence acquires after a short event sequence and clears after a short no-event timeout. Virtual cadence speed is step rate times a configurable virtual stride, clamped to a prototype maximum.

`BodyHeadingEstimator` derives live torso Right/Up from stabilized canonical 3D, reconstructs Forward with the source handedness, maps it through the accepted Phase 4 signed source-to-avatar basis, and projects the mapped Forward onto world X/Z. Cadence therefore follows torso/avatar heading.

### Fusion and application

`LocomotionFusion` scales the trusted camera-space support X/Z displacement independently (current defaults `0.9` lateral / `1.5` depth), embeds it as `(cameraX, 0, cameraZ)`, maps it through the accepted Phase 4 reference `CanonicalToAvatarAxisMap`, then projects the mapped result to game-world X/Z. The same mapping is applied to scaled physical velocity before computing `physicalActivity`. Live body heading is used only for cadence travel; it never rotates physical room displacement. Cadence blend is approximately:

```text
cadenceBlend = cadenceConfidence * (1 - physicalActivity)
```

so real translation dominates while in-place rhythm extends range.

`EmbodiedLocomotionController` integrates cadence into a virtual origin and applies:

```text
GamePositionXZ = VirtualOriginXZ + MappedPhysicalWorldDisplacementXZ
```

to the bound avatar root. Root Y and root rotation are preserved.

`Recenter()` preserves the current world X/Z as the new virtual origin, then resets the physical origin. This keeps the avatar stationary in the virtual world during recenter and provides a clean future discrete-command API. Speech recognition itself is excluded.

The Lab uses **K** for recenter and shows physical displacement/confidence, cadence state/rate, heading, physical/cadence contributions, final frame motion, and recenter state. A runtime-created fixed grid viewport provides visual world-reference feedback.


### Phase 5A Inspector tuning

The permanent Motion Engine Lab serializes a real `EmbodiedLocomotionController` on the `PoseTrackingSpike` GameObject. The presenter resolves/reuses it and retains runtime `AddComponent` only as a defensive fallback.

Inspector groups are:

- **Root / Physical Tracking** — support joint confidence, minimum image measurement, yaw floor, support agreement tolerance, depth corroboration thresholds, position/velocity response, depth clamp.
- **Physical Locomotion / Fusion** — lateral/depth scale, X/Z deadzones, physical suppression start/full thresholds, minimum trusted support confidence.
- **Cadence** — lower-body confidence, signal response, event threshold, step-rate limits, acquisition events, acquire/sustain confidence, stop timeout, virtual stride, maximum virtual speed.
- **Heading** — heading response.

These are the same settings objects consumed by the runtime modules, so Play Mode edits affect the active prototype without a duplicate tuning system.


### Phase 5A Lab/Game presentation mode

`F12` toggles presentation mode only.

- **Lab View:** the existing webcam fullscreen presentation, F1–F11 overlays, diagnostics, and RenderTexture debug views behave as before.
- **Game View:** webcam IMGUI and all debug IMGUI are skipped; the existing full-screen `PoseTrackingSpikeCamera` is enabled and controlled by `ThirdPersonLabCamera`.
- Engine components are never disabled by F12.

`ThirdPersonLabCamera` is not parented to the avatar. It follows the bound avatar root from behind the retained mapped Phase 5A heading:

```text
camera = avatarPosition - heading * followDistance + up * cameraHeight
look   = avatarPosition + up * lookHeight
```

The camera smooths heading and position. If current heading data disappears, it retains the last valid heading rather than snapping to global Forward. Inspector settings expose Follow Distance, Camera Height, Look Height, Position Response, Heading Response, and Field Of View.

The screen camera is serialized disabled in Lab View. RenderTexture cameras for the procedural rig and Phase 5A world viewport remain separate debug cameras, so there is no second active full-screen game camera in Lab View.
