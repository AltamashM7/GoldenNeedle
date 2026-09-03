# Golden Needle architecture

Status: **PHASE 4 CORRECTION — AWAITING USER QA / NOT USER ACCEPTED.** Phase 2 was USER accepted with **PASS WITH NOTES** at `f5a15648607adf6034800c6a2b4d685b0e6f03ea`; Phase 3 was USER accepted with **PASS WITH NOTES** at `2ee4d6eb606a8b845183cc44126ecf9530d8280b`. The pre-correction handoff HEAD was `4a26589ec2f90688b80fb6b1da0b849adda65d6b`. Phase 5 has not started.

**Important:** the Phase 4 correction below is repository-derived and mathematically explicit, but it is not frozen architecture until USER visual/motion QA and Orchestrator audit pass.

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
Canonical Skeleton / motion observations
    -> Locomotion Interpreter
    -> CharacterController / world movement
```

**POSE != LOCOMOTION.** The avatar may reproduce the user's body motion while locomotion converts actions such as jogging in place, lateral stepping, crouching, or jumping into game-world movement. Real-world physical displacement is not assumed to map directly to world distance.

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

The current implementation uses `PoseObservation` only at the MediaPipe provider boundary. `MediaPipeCanonicalPoseSource` adapts it into `ICanonicalPoseSource`, and `MotionEngineRuntime` owns the canonical source -> stabilization -> calibration -> rotation pipeline. `CanonicalPoseFrame` exposes the fixed 20-joint engine-owned representation, per-joint trust/confidence, partial-body validity, optional image/world/local positions, and derived pelvis/chest/spine midpoints. `CanonicalPoseStabilizer` consumes that frame using actual source/received timestamps, independent per-joint One Euro position filters, confidence hysteresis, dropout grace, and reset-aware reacquisition. `MotionCalibrationSession` consumes canonical frames only and keeps its neutral/T-pose profile in memory. Canonical consumers do not need MediaPipe classes or raw landmark indices.

The camera path defines one canonical inference frame: the correctly oriented image presented to MediaPipe after sensor/storage and pixel preparation, before optional display-only mirroring. Sensor/storage metadata, pixel preparation, canonical data conversion, and display presentation remain separate. MediaPipe input H/V preparation still performs real pixel transforms through the embedded plugin. The provider no longer assumes a front-facing `WebCamTexture` needs an additional horizontal *presentation* correction; explicit display mirror OFF therefore adds no horizontal preview flip. `MediaPipeCanonicalPoseMapper` remains `x,1-y` for normalized landmarks and `x,-y,z` for world landmarks. For 2D debug presentation only, inference-frame points are explicitly inverse-mapped through the inference H/V/rotation preparation back to sensor/storage coordinates, then passed through sensor display corrections and the optional explicit display mirror. World/canonical 3D data is not involved in that presentation conversion. The debug scene is the only current consumer and draws raw landmarks, canonical 2D landmarks, stabilized canonical 2D landmarks, and canonical/stabilized local-space 3D views, with thresholded anatomical-chain 2D↔3D X/Y agreement diagnostics. `POSE != LOCOMOTION` remains unchanged.

## Motion Engine maintainability rule

The Golden Needle Motion Engine must remain independently maintainable after the entire game is complete. Gameplay and courses consume stable Motion Engine contracts rather than provider internals; MediaPipe remains isolated behind its provider/mapping boundary; calibration and stabilization remain separately tunable; and later reconstruction, retargeting, and locomotion remain modular. The Motion Engine must remain testable without loading the complete game, dedicated motion-engine debug tooling/scenes must be retained for later inspection and improvement, and replacing or improving one Motion Engine layer must not require rewriting courses or unrelated gameplay.

## Phase 4 rotation and retargeting

The engine-owned rotation contract remains `CanonicalRotationFrame` with ten independently available bones: Pelvis, Chest, bilateral upper/lower arms, and bilateral upper/lower legs. It remains preallocated and provider-independent. Phase 4 uses it primarily for pelvis/chest orientation, diagnostics, and future orientation consumers; positional limb posing uses the separate `CanonicalKinematicTargets` contract.

`CanonicalRotationSolver` uses profile T-pose arm directions for both arm segments and neutral hip/knee/ankle reference directions for the legs. Torso and pelvis swing are reconstructed from robust orthonormal bases: Right is right hip minus left hip or right shoulder minus left shoulder, and Up is chest minus pelvis. Invalid, non-finite, or near-zero inputs make only the affected bone unavailable. Partial-body frames retain independently solvable limbs.

The solver is deliberately swing-only for the retained rotation-frame path. A monocular webcam does not provide reliable forearm pronation/supination or upper-arm axial roll, so those twists are not fabricated. Head, hand, and foot orientation are not driven in this phase; Hand and Foot transforms are positional IK tips only.

`CanonicalKinematicTargetBuilder` consumes only the stabilized canonical frame and valid in-memory calibration profile. It produces one sample for each of four chains—LeftArm, RightArm, LeftLeg, and RightLeg—with source root/mid/effector positions, source reach, confidence, normalized effector displacement, optional normalized bend-hint displacement, and timing. Root plus effector is the minimum valid chain; the current mid is preferred for bend selection.

The retargeter maps those normalized displacements into the target avatar without changing authored proportions. The source builder keeps root-to-mid and root-to-effector vectors directly in stabilized Golden Needle canonical 3D space.

Golden Needle calibration may describe a reflected semantic anatomical basis. For example, a reference case can be `Right=+X, Up=+Y, Forward=-Z`, whose determinant is negative. A quaternion cannot represent that reflection. Production retargeting therefore builds:

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

The target basis is immutable for the lifetime of that binding capture and is invalidated/rebuilt only when the binding/reference pose is rebuilt. The same signed map converts live canonical torso Right/Up axes into a proper target body rotation. Large yaw is derived from the live axes plus the source basis handedness; no previous/reference forward-hemisphere forcing is used in the production path. Per-chain quaternion characterization/current-target-parent mapping remains only as compatibility code for older callers/tests and is not the live `LateUpdate` architecture.

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

The existing `PoseTrackingSpike` scene remains the permanent Motion Engine Lab. Its presenter bootstraps the provider adapter, runtime, procedural debug rig, binding, retargeter, and a dedicated procedural-rig camera for Play Mode. The rig camera renders the actual bound hierarchy into a small panel so fullscreen webcam IMGUI cannot obscure it; distinct desired wrist/ankle and elbow/knee markers are world-space markers alongside the real joints. Compact diagnostics expose rig presence, binding, driving state, rotation solve, kinematic target state, `Source chains valid x/4`, `Targets generated x/4`, `IK chains solved x/4`, `Limb bones driven x/8`, separate retarget-fidelity, IK-endpoint-residual, and bend-plane metrics, and canonical 2D↔3D X/Y agreement. `F5` starts OFF, toggles live IK driving ON/OFF, and OFF returns the same rig to bind pose; `F6` toggles the raw-world/canonical coordinate inspector and per-pair Y deltas; `F1`–`F4`, `R`, `C`, and `X` remain available. The procedural rig is frozen and is not part of the current preview mirror/Y-agreement QA. No locomotion, CharacterController, root motion, Hub, course, or gameplay integration is part of Phase 4.

## Animator authority

Phase 4 has no cinematic controller, state machine, root motion, or gameplay authority handoff. The debug retargeter is the deliberate LateUpdate rotation authority for its bound rig only; later Player/cinematic integration must define any broader handoff explicitly.

## Ownership and performance

The USER primarily owns Motion Engine work. Other developers can later own separate environments and courses. Scene/content ownership is the primary strategy for avoiding Unity YAML conflicts.

The baseline must run acceptably without a dedicated GPU. Inference must not block Unity rendering, and processing costs must be justified by measured value. Initial environments and visuals should remain simple while the Motion Engine is proven.
