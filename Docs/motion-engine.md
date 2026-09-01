# Planned V1 Motion Engine

Status: **PHASE 1 SPIKE IMPLEMENTED; USER QA PENDING.** The isolated webcam and CPU MediaPipe Pose Landmarker spike exists. The canonical skeleton, calibration, filtering, reconstruction, retargeting, and locomotion runtime remain unimplemented.

## Phase 1 spike boundary

- `MediaPipePoseProvider` owns the WebCamTexture capture, MediaPipe Tasks API integration, CPU configuration, cadence limiting, async result callback, and per-landmark trust classification.
- `PoseObservation` is a small spike-only observation type. It is not the final engine-owned canonical skeleton and must not become a gameplay contract.
- `PoseTrackingSpikePresenter` is a diagnostic consumer that draws the camera texture, trusted landmarks/connections, unavailable landmarks, and runtime statistics.
- The current Windows integration uses the repository-local MediaPipeUnityPlugin `0.16.3` CPU prebuilt runtime and a local Pose Landmarker Lite model. Windows support is documented as experimental by the plugin, so the Orchestrator should treat the USER’s physical QA as the acceptance authority.
- The spike accepts partial bodies through per-landmark trust. Missing or untrusted lower-body landmarks do not invalidate trusted upper-body observations.

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

The Motion Engine will expose a smaller engine-owned body representation suitable for filtering, reconstruction, retargeting, and locomotion interpretation. The exact final joint list is **OPEN / MAY CHANGE** and is not frozen by this document.

## Calibration

The planned calibration sequence uses:

- a natural neutral stance;
- a brief T-pose or T-pose-like stance;
- derived orientation, scale, and rest offsets.

Calibration should preserve the avatar's own proportions. It should not simply copy the user's measured limb lengths into a character with different proportions.

## Confidence and filtering

- Confidence handling is required at landmark and derived-joint boundaries.
- Temporal smoothing is planned.
- One Euro Filter is currently planned for landmark filtering.
- Quaternion interpolation is planned downstream for rotations.
- Lost or occluded landmarks must fail gracefully and avoid instant violent snapping.

## 3D rotation reconstruction

Rotation reconstruction is planned to use pose/world landmarks with vector and quaternion reasoning. Shoulder and hip relationships can assist torso orientation, while adjacent joints determine limb directions. Unreliable axial twist should be constrained or stabilized rather than treated as perfectly observable from a monocular webcam.

## Retargeting

Unity Humanoid retargeting is planned, with an eventual goal of supporting multiple humanoid-compatible avatars. Cutscene animation and live Motion Engine control need a deliberate authority handoff so neither system fights the other.

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
