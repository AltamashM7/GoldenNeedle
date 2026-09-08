# Golden Needle architecture

Status: **PHASE 4 USER ACCEPTED — PASS. PHASE 5A IMPLEMENTED / AWAITING USER QA.** Phase 2 was USER accepted with **PASS WITH NOTES** at `f5a15648607adf6034800c6a2b4d685b0e6f03ea`; Phase 3 was USER accepted with **PASS WITH NOTES** at `2ee4d6eb606a8b845183cc44126ecf9530d8280b`; Phase 4 accepted implementation SHA is `f0c81e84d0a482c40448505f2904af93ef4aa881`.

Golden Needle is a CPU-first, webcam-driven embodied-fitness application. The intended runtime uses the user's full-body movement to drive a humanoid 3D avatar while gameplay systems interpret movement separately for world-space action.

## Planned motion paths

Primary body-reproduction path:

```text
Unity Webcam
    -> Pose Provider
    -> Raw pose observations
    -> MediaPipe-to-canonical mapper
    -> Canonical Skeleton
    -> Canonical debug visualization
    -> Confidence handling / filtering
    -> Canonical rotation frame (torso/future orientation)
    -> Canonical kinematic targets
    -> Analytic two-bone IK / rig binding
    -> Player Avatar
```

Separate locomotion path:

```text
stabilized canonical/image observations
    +-> CameraSpaceRootTracker -> physical displacement
    +-> CadenceDetector
    +-> BodyHeadingEstimator
physical displacement + cadence + heading
    -> LocomotionFusion
    -> EmbodiedLocomotionController
    -> avatar root X/Z
```

**POSE != LOCOMOTION.** Phase 4 owns avatar body pose/orientation. Phase 5A owns only game-world/root translation. Physical displacement uses configurable mapping rather than pretending monocular tracking is metric; cadence provides infinite-range extension when net physical translation is small. Root Y is not copied from the body.

## Product modules

- **Core / Motion Engine** — provider abstraction, canonical body data, confidence, filtering, reconstruction, and retargeting contracts.
- **Player** — avatar, player-facing state, locomotion, and authority handoffs.
- **Game Flow** — launch, introduction, calibration, control transfer, course transitions, and return to Hub.
- **Hub** — modular home space and course selection.
- **Course System** — lifecycle, course discovery, start/completion handling, and stable player-facing abstractions.
- **Individual Courses** — independently owned fitness environments and content.
- **UI** — menus, prompts, calibration guidance, and results presentation.
- **Cinematics** — authored character introduction and deliberate handoff to live control.
- **Debug/Diagnostics** — development-only observability for pose quality, timing, confidence, and performance.

## Dependency direction

The intended dependency direction is:

```text
pose-provider implementation
    -> Motion Engine abstractions
    -> Player systems
    -> Hub / course gameplay
```

MediaPipe-specific structures stay inside the provider/integration boundary. Player, Hub, and course code consume engine-owned representations and stable abstractions, not MediaPipe internals. Changing the pose backend should not require rewriting game or course systems.

The current implementation uses `PoseObservation` only at the MediaPipe provider boundary. `MediaPipeCanonicalPoseSource` adapts it into `ICanonicalPoseSource`, and `MotionEngineRuntime` owns the canonical source -> stabilization -> calibration -> rotation pipeline. `CanonicalPoseFrame` exposes the fixed 20-joint engine-owned representation, per-joint trust/confidence, partial-body validity, optional image/world/local positions, and derived pelvis/chest/spine midpoints. `CanonicalPoseStabilizer` consumes that frame using actual source/received timestamps, independent per-joint One Euro position filters, confidence hysteresis, dropout grace, and reset-aware reacquisition. `MotionCalibrationSession` consumes canonical frames only and keeps a modular body-reference/per-chain geometry profile in memory. Canonical consumers do not need MediaPipe classes or raw landmark indices.

The camera path defines one canonical inference frame: the correctly oriented image presented to MediaPipe after sensor/storage and pixel preparation, before optional display-only mirroring. Sensor/storage metadata, pixel preparation, canonical data conversion, and display presentation remain separate. MediaPipe input H/V preparation still performs real pixel transforms through the embedded plugin. The provider no longer assumes a front-facing `WebCamTexture` needs an additional horizontal *presentation* correction; explicit display mirror OFF therefore adds no horizontal preview flip. `MediaPipeCanonicalPoseMapper` remains `x,1-y` for normalized landmarks and `x,-y,z` for world landmarks. For 2D debug presentation only, inference-frame points are explicitly inverse-mapped through the inference H/V/rotation preparation back to sensor/storage coordinates, then passed through sensor display corrections and the optional explicit display mirror. World/canonical 3D data is not involved in that presentation conversion. The debug scene is the only current consumer and draws raw landmarks, canonical 2D landmarks, stabilized canonical 2D landmarks, and canonical/stabilized local-space 3D views, with thresholded anatomical-chain 2D↔3D X/Y agreement diagnostics. `POSE != LOCOMOTION` remains unchanged.

## Motion Engine maintainability rule

The Golden Needle Motion Engine must remain independently maintainable after the entire game is complete. Gameplay and courses consume stable Motion Engine contracts rather than provider internals; MediaPipe remains isolated behind its provider/mapping boundary; calibration and stabilization remain separately tunable; and later reconstruction, retargeting, and locomotion remain modular. The Motion Engine must remain testable without loading the complete game, dedicated motion-engine debug tooling/scenes must be retained for later inspection and improvement, and replacing or improving one Motion Engine layer must not require rewriting courses or unrelated gameplay.

## Phase 4 modular calibration

Calibration profile schema version 5 separates **body reference** from **limb geometry**. A comfortable, reasonably still pose samples only pelvis/chest plus bilateral shoulders/hips for shoulder width, hip width, torso length, and neutral Right/Up/Forward. Knees, ankles, wrists, and any special arm pose are irrelevant to this body-reference gate.

LeftArm, RightArm, LeftLeg, and RightLeg then accumulate independently whenever their root/mid/tip joints are tracked with usable 3D positions and confidence. Each accepted sample measures upper segment length and lower segment length separately; reach is their sum, so a bent arm does not calibrate shorter than the same straight arm. A short fixed sample count is averaged per chain, and body-reference instability never resets another chain's accumulated progress.

`MotionCalibrationProfile.isValid` is retained only as a compatibility/global readiness flag synchronized to `bodyReferenceValid`; it no longer means all four chains are calibrated. Production consumers use body readiness plus per-chain `MotionCalibrationChainGeometry.isValid`.

## Phase 4 rotation and retargeting

The engine-owned rotation contract remains `CanonicalRotationFrame` with ten independently available bones: Pelvis, Chest, bilateral upper/lower arms, and bilateral upper/lower legs. It remains preallocated and provider-independent. Phase 4 uses it primarily for pelvis/chest orientation, diagnostics, and future orientation consumers; positional limb posing uses the separate `CanonicalKinematicTargets` contract.

`CanonicalRotationSolver` uses the body reference for torso/pelvis and, only when a chain is calibrated, that chain's sampled upper/lower segment reference directions for diagnostic limb swing. No T-pose direction is required. Torso bases remain Right = right hip/shoulder minus left hip/shoulder and Up = chest minus pelvis. Invalid or uncalibrated chains make only their affected bones unavailable.

The solver is deliberately swing-only for the retained rotation-frame path. A monocular webcam does not provide reliable forearm pronation/supination or upper-arm axial roll, so those twists are not fabricated. Head, hand, and foot orientation are not driven in this phase; Hand and Foot transforms are positional IK tips only.

`CanonicalKinematicTargetBuilder` requires a valid body reference globally but evaluates LeftArm, RightArm, LeftLeg, and RightLeg calibration independently. It emits only chains whose own geometry record is valid, using that chain's measured segment-sum reach to normalize root/mid/effector displacement. Missing or uncalibrated chains do not block usable ones.

The retargeter maps those normalized displacements into the target avatar without changing authored proportions. The source builder keeps root-to-mid and root-to-effector vectors directly in stabilized Golden Needle canonical 3D space.

Golden Needle canonical axes are camera/view axes. For an unmirrored person facing the webcam, anatomical Right appears on viewer-left, so the calibrated neutral body basis is expected to be approximately `Right=-X, Up=+Y, Forward=-Z`, with `Forward=Cross(Right, Up)` and handedness +1. The signed-basis machinery still records handedness explicitly and remains able to represent a reflected basis if one is supplied, but the current front-camera calibration contract does not intentionally construct one. Production retargeting therefore builds:

- a signed source basis that preserves canonical Right/Up/Forward and records handedness;
- a proper right-handed target basis captured once from the bound avatar's actual shoulder/hip and pelvis/chest bind/reference geometry;
- one explicit linear canonical-to-avatar map `M` between those bases.

For target root `R`, avatar chain reach `L`, normalized canonical effector vector `e`, and normalized canonical mid vector `m`:

```text
mappedEffector = M(e)
mappedHint = M(m)
desiredEffector = R + mappedEffector * L
targetHint = R + mappedHint * L
```

The target basis is immutable for the lifetime of that binding capture and is invalidated/rebuilt only when the binding/reference pose is rebuilt. The same signed map converts live canonical torso Right/Up axes into a proper target body rotation. Large yaw is derived from the live axes plus the source basis handedness; no previous/reference forward-hemisphere forcing is used in the production path. Under the corrected neutral front-camera convention both source and validated target bases are expected to be proper, so the reference map determinant is expected to be +1. Per-chain quaternion characterization/current-target-parent mapping remains only as compatibility code for older callers/tests and is not the live `LateUpdate` architecture.

`HumanoidRigBinding` captures the actual root/mid/tip Transform for each Animator Humanoid chain or explicit procedural chain, bind local rotations, upper/lower world lengths, total reach, and original local positions/scales once. For each live chain, the retargeter first applies pelvis/chest, restores that chain's root and mid to cached bind-local rotations, then solves from the current root world position. The analytic solver clamps the desired endpoint to `abs(a-b)+epsilon .. (a+b)-epsilon`, preserves its direction, and computes:

```text
c = clamp(distance(R, desiredEffector), abs(a-b)+epsilon, (a+b)-epsilon)
n = normalize(desiredEffector - R)
x = (a² - b² + c²) / (2c)
y = sqrt(max(a² - x², 0))
solvedMid = R + n*x + p*y
solvedTip = R + n*c
```

The bend direction `p` uses, in order, the current mapped bend hint, a previous valid plane, a calibrated/reference body axis, and a deterministic orthogonal fallback. Sign continuity prevents tiny near-degenerate landmark changes from flipping elbows or knees; a clearly opposite valid hint can intentionally change sides. The root is rotated toward `root -> solvedMid`, then the mid is rotated toward `mid -> solvedTip`. This drives the actual hierarchy and does not reconstruct a fake drawing or apply torso rotation twice. `CanonicalRotationFrame` remains available for torso orientation, diagnostics, and later orientation layers; it is not the primary limb pose contract.

`HumanoidRigBinding` supports explicit project-owned debug transforms and a structural Unity Humanoid path that validates `Animator`, `Avatar`, `isHuman`, and `isValid`, uses `GetBoneTransform`, and falls back from Chest to Spine. `MotionEngineRuntime` owns the preallocated kinematic output; `HumanoidRetargeter` consumes it in `LateUpdate`, preserves root position/local offsets/local scales, and returns unavailable chains toward the bind pose over a centralized approximately `0.20 s` window. Partial-body validity is per chain.

The existing `PoseTrackingSpike` scene remains the permanent Motion Engine Lab. Its presenter bootstraps the provider adapter, runtime, procedural debug rig, binding, retargeter, and a dedicated procedural-rig camera for Play Mode. The rig camera renders the actual bound hierarchy into a small panel so fullscreen webcam IMGUI cannot obscure it; distinct desired wrist/ankle and elbow/knee markers are world-space markers alongside the real joints. Compact diagnostics expose rig presence, binding, driving state, rotation solve, kinematic target state, `Source chains valid x/4`, `Targets generated x/4`, `IK chains solved x/4`, `Limb bones driven x/8`, separate retarget-fidelity, IK-endpoint-residual, and bend-plane metrics, and canonical 2D↔3D X/Y agreement. `F5` starts OFF, toggles live IK driving ON/OFF, and OFF returns the same rig to bind pose; `F6` toggles the raw-world/canonical coordinate inspector and per-pair Y deltas; `F1`–`F4`, `R`, `C`, and `X` remain available. The procedural rig is frozen and is not part of the current preview mirror/Y-agreement QA. No locomotion, CharacterController, root motion, Hub, course, or gameplay integration is part of Phase 4. Final USER QA also validated a real Neko Animator Humanoid for binding, limb response, facing/orientation, and left/right torso yaw.

## Animator authority

Phase 4 has no cinematic controller, state machine, root motion, or gameplay authority handoff. The debug retargeter is the deliberate LateUpdate rotation authority for its bound rig only; later Player/cinematic integration must define any broader handoff explicitly.

## Ownership and performance

The USER primarily owns Motion Engine work. Other developers can later own separate environments and courses. Scene/content ownership is the primary strategy for avoiding Unity YAML conflicts.

The baseline must run acceptably without a dedicated GPU. Inference must not block Unity rendering, and processing costs must be justified by measured value. Initial environments and visuals should remain simple while the Motion Engine is proven.


## Phase 5A embodied hybrid locomotion

<!-- CURRENT_LAB_OVERLAY_ARCHITECTURE:START -->
## Current Motion Engine Lab presentation architecture

The Lab remains permanent engineering infrastructure. At the current Phase 5A checkpoint, debug presentation is independently controllable and screen-aware rather than being a set of always-on fixed rectangles.

Normal panel regions are:
- top-left: Motion Engine diagnostics;
- top-right: Phase 5A locomotion diagnostics;
- bottom-left: procedural rig viewport;
- bottom-right: Phase 5A fixed-world/grid viewport.

`F3` replaces the ordinary right column with Canonical 3D inspection. `F6` is a large coordinate diagnostic focus view that suppresses normal panels without mutating their saved visibility states. `F11` hides/restores debug presentation after the webcam preview is drawn and does not alter runtime tracking, calibration, retargeting, cadence, locomotion, or recenter state.

The complete current control surface is `F1` Raw, `F2` Canonical 2D, `F3` Canonical 3D, `F4` Stabilized 2D, `F5` Retarget Drive, `F6` Coordinate Focus, `F7` Engine Diagnostics, `F8` Procedural Rig, `F9` Locomotion Diagnostics, `F10` World/Grid, `F11` Hide/Restore Debug Presentation, plus `R` Retry, `C` Calibrate, `X` Reset, and `K` Recenter.
<!-- CURRENT_LAB_OVERLAY_ARCHITECTURE:END -->

`CameraSpaceRootTracker` is deliberately independent of pelvis-relative MediaPipe pose-world coordinates. Physical translation authority is the support base, not torso center. Each left/right foot is estimated from trusted ankle/heel/toe image observations and compared with that same foot's recenter reference. Only coherent two-foot/common support relocation updates physical X/Z; gait-cycle disagreement or temporary support loss holds the previous trusted offset. Torso apparent scale remains auxiliary for normalization/cadence and for same-sign depth corroboration, so upper-body lean alone cannot initiate room translation.

`CadenceDetector` consumes stabilized lower-body image rhythm. Alternating ankle vertical separation is primary and knee separation is secondary. Valid alternating events estimate step rate, cadence confidence, and a configurable virtual speed. Acquisition requires only a short rhythm sequence; loss of events clears cadence quickly.

`BodyHeadingEstimator` reconstructs live canonical anatomical Forward from torso Right/Up and maps it through the accepted Phase 4 `CanonicalToAvatarAxisMap`. Cadence travel therefore follows the same world-space heading shown by the avatar instead of a fixed global axis.

`LocomotionFusion` keeps tracker output in fixed camera/canonical X/Z until after independent lateral/depth scaling and deadzones. It then maps the scaled camera vector through the accepted Phase 4 **reference** `CanonicalToAvatarAxisMap` and projects the result to game-world X/Z. The same reference map is used for scaled physical velocity before calculating activity. Live body heading is deliberately not used for physical room displacement. Cadence velocity is multiplied by cadence confidence and `1 - physicalActivity`, preventing obvious double-counting while still allowing in-place cadence extension.

`EmbodiedLocomotionController` runs after Phase 4 retargeting and changes only `HumanoidRigBinding.AvatarRoot.position.x/z`. It does not write root Y or rotation. Cadence integrates a persistent virtual origin; the mapped world-space physical contribution remains an offset derived from the current camera-space tracking origin.

Recenter is a public controller action. Before the root tracker zeroes its physical displacement, the controller stores the avatar's current X/Z as the new virtual origin. This preserves world position exactly while making the current physical body position the new tracking origin.

The permanent Lab now **serializes** the Phase 5A `EmbodiedLocomotionController` so root/fusion/cadence/heading tunables persist in the Inspector; the presenter keeps a defensive runtime fallback only if the component is missing. The fixed-camera grid viewport remains runtime-created debug infrastructure. This prototype environment has no progression, obstacles, scoring, speech recognition, jump, crouch gameplay state, or production collision system.
