# Current state

## Authoritative checkpoint — motion responsiveness hardening

Working branch: `engine/pose-tracking-spike`.

Starting handoff checkpoint for this task: `f895fcd4941061950723f914e7cd815dc15c65f1` — `docs: finalize phase 5a orchestrator handoff`.

Motion-responsiveness implementation checkpoint: `4fc8bb2f1e5e8e61235d7b125e743698ca84087e` — `perf: overlap latest-frame preparation with pose inference`.

Status:
- Phase 4: **USER ACCEPTED — PASS**.
- Phase 5A: **IMPLEMENTED / NOT USER ACCEPTED**. Locomotion acceptance QA is paused until the lower-level camera-to-avatar response foundation is rechecked on USER hardware.
- Phase 6: **NOT STARTED**.
- Do not merge to `main` without explicit USER approval.

This file is the concise authoritative runtime snapshot. Older investigation narrative remains available in Git history and the other Motion Engine documents.

## Runtime evidence that motivated this checkpoint

The USER tested both the HP TrueVision laptop webcam and DroidCam Video over USB. Both feeds work in Golden Needle. In recorded tests, actual camera and Unity render cadence were substantially below ideal while OBS was active, inference requests/results were commonly around 3–5/s, individual DetectAsync-to-callback duration was commonly roughly 56–94 ms, and pose age could reach hundreds of milliseconds. These are USER-machine observations, not synthetic locked benchmarks.

Turning Humanoid presentation smoothing OFF produced no noticeable response improvement. The one-off `QuaternionToEuler` warning and temporary upside-down Neko observation did not reproduce and are not active targets. Phase 4 orientation/retargeting remains frozen unless new reproducible evidence appears.

## Source-proven frame pipeline and correction

Before this checkpoint the provider serialized the complete cycle:

```text
fresh WebCamTexture frame
-> scheduler eligibility
-> TextureFrame acquisition
-> AsyncGPUReadback / Texture2D upload
-> BuildCPUImage
-> DetectAsync
-> wait for result callback
-> only then allow the next readback
```

`MediaPipePoseProvider.Update()` treated either readback or inference as one combined busy state. That meant a new GPU readback could not begin while MediaPipe inference was outstanding. On a low-render-cadence USER session this adds avoidable frame-sized gaps around a 60–90 ms inference and helps explain why request throughput can be much lower than the measured DetectAsync-to-callback duration alone suggests.

The embedded Homuler `TextureFrame.ReadTextureAsync` path was audited. It performs a GPU blit plus `AsyncGPUReadback`, then loads the returned bytes into the pooled `Texture2D`. `BuildCPUImage()` is performed only when a prepared frame is selected for MediaPipe. The existing pool size of two is sufficient for a bounded latest-frame pipeline.

The provider now uses these bounds:

```text
at most one AsyncGPUReadback in progress
+
at most one replaceable prepared TextureFrame
+
at most one MediaPipe inference outstanding
```

Readback preparation may overlap the active MediaPipe inference. A completed readback occupies one prepared slot. A newer completed readback replaces and releases an older prepared slot rather than creating a queue. Once MediaPipe becomes free and the 30-FPS target interval is eligible, the newest prepared frame is consumed. There is still no historical-frame queue, no inference backlog, no catch-up loop, and no delayed pose replay.

The 30 FPS value remains a requested maximum cadence, not a result-rate guarantee. Actual throughput remains limited by camera delivery, Unity/render cadence, readback/CPU preparation, MediaPipe CPU time, and USER hardware load.

## F7 observability

The existing F7 Engine panel keeps its camera name/device count, actual source resolution, fresh camera FPS, render FPS, target inference FPS, request/result FPS, pose age and DetectAsync-to-callback duration.

The provider now publishes an additional once-per-metrics-window pipeline diagnostic through the existing F7 Status area. It reports:
- readback-completion duration (`RB`), measured from `ReadTextureAsync` request until Unity observes completion; this includes callback/upload/scheduling overhead and is not claimed as pure hardware GPU time;
- `BuildCPUImage` preparation duration (`build`), as separately exposed by the current API;
- DetectAsync accepted -> callback duration (`detect`);
- approximate fresh-camera-frame -> result duration (`~F->R`), based on the time Unity observed `WebCamTexture.didUpdateThisFrame` because no sensor capture timestamp is available;
- result callback rate (`cb/s`) separately from pose-bearing result rate;
- wait/check rates for no fresh camera frame, target interval, readback busy, inference busy and TextureFrame-pool unavailable;
- readback failure and timeout rates;
- prepared-frame replacement rate, which makes deliberate stale-frame dropping visible.

No frame-by-frame diagnostic logging was added, and diagnostic counters do not control scheduling decisions.

## Camera / orientation foundation preserved

External camera support remains one generic Unity `WebCamTexture` / `WebCamDevice` provider path. `preferredCameraName` stores device identity by name; the custom Inspector enumerates devices; `V` cycles ordinary non-depth/non-IR devices through the safe pending-switch path. Phones require Windows/Unity UVC or virtual-webcam exposure; Golden Needle has no phone networking protocol.

Scene defaults remain automatic device, `640x480 @ 30`, target inference 30 FPS, Orientation Auto and display mirror OFF. `480x640 @ 30` remains a supported portrait request for full-body capture.

Auto/0/90/180/270 orientation behavior is unchanged. Front-facing metadata still does not imply an inference horizontal mirror: production remains `shouldFlipHorizontally=false`. Display mirror remains presentation-only. Physical camera changes still invalidate the source/session convention so calibration/root assumptions are not reused; after switching camera the USER must run `C` and then `K` before locomotion QA.

## Lab presentation

The fitted whole-frame Lab preview introduced at `db9c4a175f5a1bf607ec25e182d06c76648370ff` remains unchanged: webcam texture and F1/F2/F4 overlays use the same `LabCameraPresentationGeometry`, with letterbox/pillarbox rather than crop for rotated portrait feeds.

Unity's `Display 1 — No cameras rendering` message was a Lab presentation artifact, not a webcam-resolution problem. The persistent third-person Camera used to be disabled in Lab while the webcam was drawn only by IMGUI. It now stays enabled in Lab as a lightweight clear-only camera with `cullingMask = 0`; therefore Unity has a valid camera without rendering the world a second time. F12 Game View restores the original camera culling/clear/background settings and keeps the existing follow behavior. The IMGUI webcam/fitted geometry is not changed by this fix.

`ThirdPersonLabCamera` normalizes the controlled camera Transform quaternion immediately before enabling the Camera. This preserves the intentional clear-only Lab mode while preventing URP `Camera.GetCullingParameters` `QuaternionToEuler` spam from slightly non-unit serialized camera rotations.

## Motion Engine behavior preserved

The source audit confirmed the downstream latest-result model is already appropriate:
- `MediaPipeCanonicalPoseSource` copies only the provider's latest observation.
- `MotionEngineRuntime` evaluates the latest canonical result and resets stabilization/calibration/targets when coordinate convention changes.
- `CanonicalPoseStabilizer` recognizes duplicate source timestamp/received-time samples and returns its last output instead of repeatedly filtering the same result.
- `HumanoidRetargeter` keeps the accepted Phase 4 exact solve; optional render-rate presentation smoothing has no target history queue.

No canonical coordinate semantics, calibration math, signed-axis mapping, Phase 4 IK/retargeting, Phase 5A locomotion algorithm, physical scale defaults `0.9 / 1.5`, cadence logic, root-Y/root-rotation exclusion, Neko binding, 30-FPS target, camera selection, hot switching, or fitted-preview geometry was redesigned in this task.

## Bounded body-pose inference resolution experiment

The provider now has a reversible body-pose-only downscale experiment. The original full-resolution `WebCamTexture` remains authoritative for `CameraTexture` and Lab presentation. When enabled, the newest full-resolution frame is scaled into one persistent, aspect-preserving inference `RenderTexture` before the existing `TextureFrame.ReadTextureAsync` path. The default target is an approximately `320`-pixel long edge, so a `640x480` source uses `320x240`; disabling the option restores the exact actual camera dimensions.

The `TextureFramePool` matches the selected body-inference dimensions, while the existing one-readback, one-prepared-frame, one-inference latest-frame bounds and accepted orientation/flip/rotation semantics remain unchanged. F7 identifies the active body input as `scaled` or `native` alongside the existing timing and wait diagnostics.

This is an implementation checkpoint for USER A/B measurement, not USER acceptance. Lower body-pose resolution does not constrain future high-detail hand/finger tracking: later hand systems may consume the unchanged full-resolution camera or separate high-resolution hand ROIs. No hand/finger tracking was added. Phase 5A remains **IMPLEMENTED / NOT USER ACCEPTED** and Phase 6 remains **NOT STARTED**.

## Verification state and next action

Focused deterministic tests were added/updated for the bounded scheduler policy and Lab clear-only/Game restoration behavior. The protected Phase 4, Phase 5A and fitted-preview files are outside the implementation diff.

Unity compilation and Unity Test Runner have **not** been executed by this Web Builder environment. Do not convert source/static checks into a Unity PASS.

Next USER QA: run the laptop webcam and DroidCam separately **without OBS first**, use `640x480 @ 30` and target inference 30, run `C` after camera/source selection and `K` after calibration, let F7 settle, then record camera/render FPS, req/callback/pose rates, RB/build/detect/~F->R timings, pose age and wait reasons while doing rapid arm/torso motion. Repeat with OBS only afterward to characterize demo overhead separately. Confirm the Lab no longer shows `No cameras rendering`.
