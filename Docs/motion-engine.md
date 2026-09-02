# Planned V1 Motion Engine

Status: **PHASE 3 — USER ACCEPTED — PASS WITH NOTES.** Phase 1 accepted SHA: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6`. Phase 2 accepted SHA: `f5a15648607adf6034800c6a2b4d685b0e6f03ea`. Phase 2 was USER accepted with **PASS WITH NOTES** after the canonical/debug corrections and mapper tests. Phase 3 USER QA passed neutral calibration, T-pose recognition, calibration completion, and visibly smoother stabilized motion with **Good** responsiveness. Focused EditMode coverage passed `15/15`; loss/reacquisition physical coverage remains a later integration-quality check. Rotation reconstruction, retargeting, and locomotion remain unimplemented. Phase 4 has not started.

## Phase 1 spike boundary

- `MediaPipePoseProvider` owns the WebCamTexture capture, MediaPipe Tasks API integration, CPU configuration, cadence limiting, async result callback, and per-landmark trust classification.
- `PoseObservation` is the raw provider-boundary observation type. It remains upstream-only and is not a gameplay contract.
- `MediaPipeCanonicalPoseMapper` converts the raw provider observation into the engine-owned `CanonicalPoseFrame`. It is the only Phase 2 runtime mapping location that knows the 33-landmark source indices.
- `PoseTrackingSpikePresenter` is a diagnostic consumer that draws the camera texture, raw and canonical trusted landmarks/connections, a canonical local-space 3D view, and runtime statistics.
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

Phase 2 exposes a smaller engine-owned body representation suitable for later filtering, reconstruction, retargeting, and locomotion interpretation. The current exact 20-joint set is:

`Pelvis, Spine, Chest, Head, LeftShoulder, LeftElbow, LeftWrist, RightShoulder, RightElbow, RightWrist, LeftHip, LeftKnee, LeftAnkle, LeftHeel, LeftToe, RightHip, RightKnee, RightAnkle, RightHeel, RightToe`.

Direct joints are mapped from the required MediaPipe source landmarks. Pelvis is the trusted midpoint of both hips, Chest is the trusted midpoint of both shoulders, and Spine is the trusted midpoint of Pelvis and Chest. Derived confidence is the minimum input confidence. A missing joint leaves that canonical joint unavailable without invalidating the rest of the frame.

Canonical image coordinates are x left-to-right and y bottom-to-top. Canonical 3D uses +X camera/view right, +Y up, and +Z away from the camera. World positions are converted once and are pelvis-relative when a trusted canonical pelvis exists; if the pelvis is unavailable, available source-world data remains in the provider's hip-centered frame. Phase 3 filters canonical image positions and canonical world positions independently per joint, then rebuilds stabilized local/root-relative positions from stabilized world positions. If the pelvis is unavailable, tracked joints remain usable in the available hip-centered frame.

## Camera orientation and debug views

The provider separates the Unity-to-MediaPipe input transform from display metadata and overlay conversion. The display no longer reuses the inference-only vertical flip. Display mirroring is explicit and disabled by default, while canonical left/right semantics remain unchanged by display mirroring. The debug spike exposes raw, canonical 2D, canonical 3D, and stabilized canonical 2D views with `F1`, `F2`, `F3`, and `F4`; `C` begins calibration, `X` cancels/resets it, and `R` retries camera/model startup.

## Calibration

Phase 3 provides an in-memory `MotionCalibrationSession` with the state flow `Idle -> Awaiting Neutral -> Sampling Neutral -> Awaiting T-Pose -> Sampling T-Pose -> Complete`, plus cancel/reset. Neutral sampling requires both shoulders, both hips, both knees, and both ankles; it uses a continuous stable hold of approximately `1.25 s`. The head is optional and wrists are not required for neutral capture. T-pose sampling requires both shoulders, elbows, wrists, and hips; it checks arm direction, shoulder-height wrists, extension, and an angular tolerance of approximately `25°` over approximately `0.75 s`. Invalid or moving poses reset stage progress and continue waiting.

The in-memory profile stores valid neutral pelvis/chest/shoulder/hip/knee/ankle references, shoulder/hip widths, torso length, neutral body axes, T-pose arm directions/span, timestamp/state/version, and sample counts. These are user reference measurements, not avatar bone lengths; calibration must preserve the avatar's own proportions.

## Confidence and filtering

- `CanonicalStabilizerSettings` centralizes acquire confidence `0.60`, sustain confidence `0.40`, acquire samples `2`, loss grace `0.10 s`, and reset-after-loss `0.25 s`.
- Each canonical joint has independent acquisition, filter, dropout, loss, and reacquisition state. A joint must meet the acquire threshold for consecutive samples, uses the lower sustain threshold while active, preserves the prior stabilized sample during the grace window, becomes unavailable after grace, and resets filters after the reset interval before reacquiring from the new sample.
- Project-owned pure One Euro filters use min cutoff `1.0`, beta `0.05`, and derivative cutoff `1.0`. The derivative is filtered first, then drives the dynamic cutoff for the position low-pass. Actual pose/received timestamps provide delta time; zero, negative, large, and non-finite intervals are sanitized/clamped.
- Lost or occluded joints fail gracefully without stale indefinite tracking or violent snapping. Quaternion interpolation remains a downstream rotation concern and is not part of Phase 3.

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
