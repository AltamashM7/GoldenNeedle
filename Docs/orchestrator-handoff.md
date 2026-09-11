# Golden Needle — Orchestrator handoff

## Governance

Repository: `AltamashM7/GoldenNeedle`

Working branch: `engine/pose-tracking-spike`

Starting checkpoint for the responsiveness task: `f895fcd4941061950723f914e7cd815dc15c65f1`.

Motion-responsiveness implementation checkpoint: `4fc8bb2f1e5e8e61235d7b125e743698ca84087e` — `perf: overlap latest-frame preparation with pose inference`.

Status:
- Phase 4: **USER ACCEPTED — PASS**. Preserve behavior.
- Phase 5A: **NOT USER ACCEPTED**. Locomotion QA is temporarily paused while camera-to-avatar response is rechecked.
- Phase 6: **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## Why the current checkpoint exists

Real USER tests with HP TrueVision and DroidCam Video showed working camera input but low end-to-end responsiveness. Under the supplied OBS recordings, fresh camera and Unity render cadence were often in the teens/low 20s, inference requests/results were commonly around 3–5/s, DetectAsync-to-callback itself was commonly around 56–94 ms, and pose age could reach hundreds of milliseconds. Treat those as approximate USER-machine evidence, not locked benchmark values.

Presentation smoothing OFF did not noticeably improve response, so this checkpoint does not retune or remove the accepted presentation layer. Transient quaternion/upside-down observations did not reproduce; do not reopen Phase 4 orientation without new evidence.

## Source audit result

The important bottleneck was proven in `MediaPipePoseProvider`, not guessed from FPS values. The old Update gate combined `_readbackPending || _inferenceOutstanding` into one busy condition. The next camera readback could therefore begin only after the previous MediaPipe result callback cleared inference occupancy.

The actual old cycle was:

```text
fresh camera frame
-> TextureFrame
-> ReadTextureAsync
-> wait for GPU readback completion
-> BuildCPUImage
-> DetectAsync
-> wait for callback
-> next Update/readback opportunity
```

The embedded Homuler implementation confirms `ReadTextureAsync` uses a blit + `AsyncGPUReadback`, completion uploads into the pooled Texture2D, and `BuildCPUImage()` wraps that prepared CPU texture for MediaPipe. The pool already supports multiple in-use frames and Golden Needle already allocates two.

This unnecessary readback/inference serialization is significant when Unity itself is updating slowly: a 60–90 ms inference can be followed by another render-frame delay before the next readback even starts. That can produce much lower request throughput than inference duration alone predicts.

Downstream code is not the proven source of this gap. `MediaPipeCanonicalPoseSource` copies the latest observation; `MotionEngineRuntime` evaluates latest data; `CanonicalPoseStabilizer` holds duplicate samples instead of refiltering them; `HumanoidRetargeter` solves at render rate and queues no historical pose targets.

## Bounded latest-frame pipeline now

The provider now allows camera preparation to overlap one active inference while keeping strict bounds:

```text
WebCamTexture.didUpdateThisFrame
        |
        v
one fresh-frame latch
        |
        v
<= 1 AsyncGPUReadback
        |
        v
<= 1 replaceable prepared TextureFrame
        |
        v
30-FPS eligibility + inference free
        |
        v
<= 1 DetectAsync outstanding
        |
        v
latest observation
```

A newer completed readback replaces/releases an older prepared frame. There is no list/queue of frames. An inference request is never launched while another inference is outstanding. Missed cadence slots are never caught up. The newest useful frame wins.

Orientation/convention version is snapshotted for each readback. Prepared work from an obsolete coordinate convention is discarded rather than submitted. Camera switching still waits for active readback/inference, then the ordinary cleanup path releases any prepared frame before provider restart.

## Bounded body-pose inference experiment

The provider now contains one reversible, body-pose-only resolution experiment. The full-resolution `WebCamTexture` remains the authoritative `CameraTexture` and Lab display source. When enabled, the newest source frame is scale-submitted into one persistent, aspect-preserving inference `RenderTexture` before the existing `TextureFrame.ReadTextureAsync` path. The default target is a `320`-pixel long edge, producing `320x240` from `640x480` and `240x320` from `480x640`. Disabling the option uses the exact actual camera dimensions.

The `TextureFramePool` matches the body-inference texture, resource lifecycle follows camera start/restart/switch/shutdown, and the one-readback/one-prepared-frame/one-inference latest-frame policy is unchanged. Existing orientation metadata, flip arguments and `ImageProcessingOptions` rotation are preserved; the scale submission itself does not rotate or mirror. F7 reports the body input dimensions and whether the active path is `scaled` or `native`.

This checkpoint is not USER accepted; the USER must run the comparable `640x480` native versus approximately `320x240` body-inference A/B benchmark. The lower body-pose resolution is not a master camera resolution and does not constrain future high-detail hand/finger tracking, which may use the unchanged full-resolution camera or separate high-resolution hand ROIs. No hand/finger tracking was added. Phase 5A remains **NOT USER ACCEPTED** and Phase 6 remains **NOT STARTED**.

## F7 diagnostics

The normal F7 lines still show:
- camera/device, actual resolution and fresh camera FPS;
- render FPS;
- target inference FPS;
- accepted inference request rate and pose-bearing result rate;
- latest pose age;
- DetectAsync accepted -> callback duration.

The F7 Status area is refreshed only on the existing rolling metrics window and adds:
- `RB`: observed `ReadTextureAsync` request -> completion duration. Because the Homuler completion callback also loads/applies CPU texture data and Unity resumes the coroutine on its own cadence, this is intentionally an observed readback-completion duration, not falsely labelled pure GPU hardware time;
- `build`: `BuildCPUImage` duration that the current API can separate;
- `detect`: DetectAsync accepted -> callback duration;
- `~F->R`: approximate Unity-observed fresh-frame -> callback latency. WebCamTexture exposes no sensor capture timestamp, so this is not a sensor-to-result measurement;
- `cb/s`: all result callbacks, separate from pose-bearing result rate;
- wait/check rates for no fresh frame, target interval, readback busy, inference busy and TextureFrame pool unavailable;
- readback fail/timeout rates;
- prepared-frame replacements/s, showing intentional stale-frame drops.

There is no per-frame Console logging.

## Lab `No cameras rendering` cleanup

The webcam is still drawn by IMGUI and continues to use the fitted `LabCameraPresentationGeometry` from `db9c4a17…`; no crop/fill behavior was reintroduced.

`ThirdPersonLabCamera` now keeps its Camera component enabled in Lab but with `cullingMask=0`, `SolidColor` clear and black background. This removes Unity's `Display 1 — No cameras rendering` placeholder without a second world render. In F12 Game View, original culling mask, clear flags and background color are restored and the same existing third-person follow behavior runs. F12 still causes `PoseTrackingSpikePresenter.OnGUI()` to return before drawing webcam/debug IMGUI.

Before enabling the controlled Camera, `ThirdPersonLabCamera` now safely normalizes its Transform quaternion component-wise (with finite/near-zero validation). The intentional clear-only Lab presentation remains unchanged; this prevents URP `Camera.GetCullingParameters` `QuaternionToEuler` spam from slightly non-unit serialized camera rotations.

## Frozen behavior / do not reopen

Preserve unless new reproducible USER evidence requires otherwise:
- front-facing camera does not imply inference H mirror; `shouldFlipHorizontally=false`;
- Auto/0/90/180/270 orientation and display mirror semantics;
- `preferredCameraName`, device dropdown, `V` camera cycling and pending hot-switch;
- physical camera switch convention/calibration invalidation; run `C` and then `K` after a switch;
- target inference cadence 30 FPS;
- Pose Landmarker Lite, CPU, one pose, segmentation OFF;
- canonical coordinate semantics;
- modular calibration;
- signed canonical-to-avatar mapping;
- accepted Phase 4 torso/IK/retargeting and Neko Humanoid binding;
- presentation smoothing defaults 45/s and 0.05 s, presentation-only;
- Phase 5A support model v3, cadence/fusion/heading, physical scales 0.9/1.5, recenter semantics;
- root Y and root rotation exclusion;
- fitted portrait/rotated Lab preview geometry;
- F12 game camera follow settings.

## Verification caveat

Focused source/tests are present, but this builder environment did **not** run Unity compilation or Unity Test Runner. The next Orchestrator must require USER-side compile/runtime evidence before treating the checkpoint as a runtime pass.

## Next USER QA

Run clean tests without OBS first.

For HP TrueVision, then DroidCam:
1. Request `640x480 @ 30`; target inference 30.
2. After source/camera selection, run `C`; after calibration, run `K` before locomotion checks.
3. Let F7 settle.
4. Perform rapid arm/torso changes and judge visible response.
5. Record camera FPS, render FPS, req/callback/pose FPS, RB/build/detect/~F->R, pose age and dominant waits.
6. Confirm Lab has no `No cameras rendering` placeholder.
7. Optionally repeat with OBS afterward to measure recording overhead separately.

The target is convincing low-latency character control, not a cosmetic 30-FPS number.
