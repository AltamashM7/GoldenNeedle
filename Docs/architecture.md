# Golden Needle architecture

Status: **PHASE 2 USER ACCEPTED — PASS WITH NOTES.** USER QA confirmed the upright webcam preview, visually aligned raw/cyan overlay, upright canonical 2D/yellow overlay after the double Y inversion fix, plausible canonical 3D/local-space visualization, valid partial-body canonical tracking, and passing Phase 2 mapper tests. The next checkpoint commit will represent the accepted Phase 2 implementation. Phase 3 has not started.

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

The current Phase 2 implementation uses `PoseObservation` only at the MediaPipe provider boundary. `CanonicalPoseFrame` exposes the fixed 20-joint Phase 2 representation, per-joint trust/confidence, partial-body validity, optional image/world/local positions, and derived pelvis/chest/spine midpoints. Canonical consumers do not need MediaPipe classes or raw landmark indices.

The camera path keeps sensor metadata, display mirroring, inference preparation, and landmark overlay conversion as separate transforms. The debug scene is the only current consumer and draws raw landmarks, canonical 2D landmarks, and a lightweight canonical local-space 3D view. `POSE != LOCOMOTION` remains unchanged.

## Ownership and performance

The USER primarily owns Motion Engine work. Other developers can later own separate environments and courses. Scene/content ownership is the primary strategy for avoiding Unity YAML conflicts.

The baseline must run acceptably without a dedicated GPU. Inference must not block Unity rendering, and processing costs must be justified by measured value. Initial environments and visuals should remain simple while the Motion Engine is proven.
