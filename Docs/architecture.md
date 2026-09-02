# Golden Needle architecture

Status: **PHASE 3 — USER ACCEPTED — PASS WITH NOTES.** Phase 2 was USER accepted with **PASS WITH NOTES** at `f5a15648607adf6034800c6a2b4d685b0e6f03ea` after confirming the upright webcam preview, aligned raw/cyan overlay, corrected upright canonical 2D/yellow overlay, plausible canonical 3D/local-space visualization, valid partial-body tracking, and passing mapper tests. Phase 3 USER QA passed calibration and visibly smoother stabilized motion with **Good** responsiveness; its checkpoint commit is pending. Phase 4 has not started.

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
    -> Rotation reconstruction
    -> Humanoid Retargeter
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

The current implementation uses `PoseObservation` only at the MediaPipe provider boundary. `CanonicalPoseFrame` exposes the fixed 20-joint engine-owned representation, per-joint trust/confidence, partial-body validity, optional image/world/local positions, and derived pelvis/chest/spine midpoints. `CanonicalPoseStabilizer` consumes that frame using actual source/received timestamps, independent per-joint One Euro position filters, confidence hysteresis, dropout grace, and reset-aware reacquisition. `MotionCalibrationSession` consumes canonical frames only and keeps its neutral/T-pose profile in memory. Canonical consumers do not need MediaPipe classes or raw landmark indices.

The camera path keeps sensor metadata, display mirroring, inference preparation, and landmark overlay conversion as separate transforms. The debug scene is the only current consumer and draws raw landmarks, canonical 2D landmarks, stabilized canonical 2D landmarks, and canonical/stabilized local-space 3D views. `POSE != LOCOMOTION` remains unchanged.

## Motion Engine maintainability rule

The Golden Needle Motion Engine must remain independently maintainable after the entire game is complete. Gameplay and courses consume stable Motion Engine contracts rather than provider internals; MediaPipe remains isolated behind its provider/mapping boundary; calibration and stabilization remain separately tunable; and later reconstruction, retargeting, and locomotion remain modular. The Motion Engine must remain testable without loading the complete game, dedicated motion-engine debug tooling/scenes must be retained for later inspection and improvement, and replacing or improving one Motion Engine layer must not require rewriting courses or unrelated gameplay.

## Ownership and performance

The USER primarily owns Motion Engine work. Other developers can later own separate environments and courses. Scene/content ownership is the primary strategy for avoiding Unity YAML conflicts.

The baseline must run acceptably without a dedicated GPU. Inference must not block Unity rendering, and processing costs must be justified by measured value. Initial environments and visuals should remain simple while the Motion Engine is proven.
