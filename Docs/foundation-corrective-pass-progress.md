# Foundation Corrective Pass Progress

## Corrective Foundations Batch 1

**Purpose:** protect the accepted low-end body baseline by making the full MediaPipe Hand Landmarker stream an explicit experimental opt-in, and repair the reusable hand-capture buffer lifetime that caused the USER-reported `ArgumentNullException`.

**Starting remote SHA:** `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0`

**Implementation ending SHA:** `d84daed488ebe689f3dd47aa283f1f1073578e43`

### Material changes

- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeCanonicalPoseSource.cs` — detailed hands now default OFF in the code-owned live spike composition; the expensive hand source is created lazily only after explicit opt-in, while disabling shuts the optional source/runtime down.
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeHandLandmarkerSource.cs` — detailed tracking also defaults OFF at the source; enable/disable/retry/restart/destruction now share explicit optional-runtime start/stop lifecycle, including coroutine/init cancellation, scheduler/session invalidation, task shutdown, and reusable-buffer release.
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeHandLandmarkerSource.Results.cs` — `_capturePixels` ownership is separated from prepared NativeArray slot ownership. Slot recreation no longer disposes/nulls the managed camera capture array.
- `Assets/GoldenNeedle/Core/Motion/Hands/HandMotionRuntime.cs` — disabling the optional hand runtime clears its cached hand frame so downstream detail cannot retain a stale sample.
- `Docs/foundation-corrective-pass-progress.md` — this Batch 1 record.

### Confirmed null-source root cause

The original `EnsureBuffers(...)` could allocate `_capturePixels` and then call `DisposeBuffers()` while recreating `_slotA` / `_slotB`. `DisposeBuffers()` also set `_capturePixels = null`, after which `CaptureLatestHandFrame()` continued into `WebCamCpuFramePreparation.PrepareRgba(_capturePixels, ...)`. The fix separates prepared NativeArray disposal from managed camera-capture storage and performs full disposal only at real lifecycle shutdown/restart boundaries.

### Effective detailed-hand default

`PoseTrackingSpike.unity` does not serialize `MediaPipeCanonicalPoseSource`; `PoseTrackingSpikePresenter` creates it in code. Therefore `enableDetailedHands = false` is the effective live runtime default without a scene migration. In that default state no `MediaPipeHandLandmarkerSource` is created, so there is no Hand Landmarker task/model verification or download, independent hand `GetPixels32`, hand-frame preparation, inference submission, or recurring detailed-hand CPU work. A lightweight disabled `HandMotionRuntime` remains available so downstream optional-detail discovery does not repeatedly search for it.

Explicit `SetDetailedHandsEnabled(true)` lazily creates/enables the existing optional Hand Landmarker path. Disabling it again stops initialization, invalidates callback/scheduler state, disposes the task and reusable buffers, disables the hand runtime, and clears cached hand data. Retry, coordinate-convention restart, timeout restart, camera-size buffer recreation, and destruction use the corrected ownership/lifecycle rules.

### Validation actually performed

- Foundation D workflow at implementation SHA: run `34958576168`, job `104346448292` — **SUCCESS**.
  - `FOUNDATION_D_HAND_SMOKE=PASS`
  - bundled and official model identity checks PASS
  - shared provider timeline and monotonic timestamp guards PASS
  - bounded latest-only scheduler PASS
  - `BODY_OPENVINO_DEFAULTS_UNCHANGED=PASS`
  - `CANONICAL_BODY_V1_UNCHANGED=PASS`
  - `PHASE4_COMPATIBILITY_SOLVE_PRESERVED=PASS`
  - `FOUNDATION_D_PRODUCTION_APPLICATION_BOUNDARY_PRESERVED=PASS`
  - read-only verification PASS
- Foundation C managed rich-motion smoke at the same implementation SHA — **PASS**. Its separate static audit still fails on pre-existing numeric landmark access in untouched `RichHumanoidDetailRetargeter.cs`; that Foundation E/axial-area issue is outside Batch 1 and was intentionally not changed.
- Start-to-implementation diff contains only the four hand-related source files listed above; no body provider/OpenVINO, scene, CanonicalBodyV1, Phase 4, locomotion, package, or ProjectSettings file was changed.
- Unity Editor compilation/Test Runner and real webcam performance were **not** run by this Builder environment. USER Unity/webcam QA remains decisive.

**Status:** `AWAITING ORCHESTRATOR REVIEW / USER UNITY QA`

**Next work:** explicitly **not authorized yet**. Do not begin speech/microphone repair, rich axial-orientation/Foundation E twist correction, coarse fist/open-hand replacement, Phase 5A work, Phase 6, or broad CI cleanup until the Orchestrator/USER advances the next batch.
