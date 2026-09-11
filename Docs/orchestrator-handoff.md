# Golden Needle — Orchestrator handoff

## Governance

Repository: `AltamashM7/GoldenNeedle`

Working branch: `engine/pose-tracking-spike`

Starting checkpoint for the responsiveness task: `f895fcd4941061950723f914e7cd815dc15c65f1`.

Motion-responsiveness implementation checkpoint: `4fc8bb2f1e5e8e61235d7b125e743698ca84087e` — `perf: overlap latest-frame preparation with pose inference`.

Body-inference implementation checkpoint: `e56bfbb1be7333c26677ab36c977010c3e843029` — `perf: add lower-resolution body pose inference path`.

Status:
- Phase 4: **USER ACCEPTED — PASS**. Preserve behavior.
- Phase 5A: **NOT USER ACCEPTED**. Locomotion QA is temporarily paused while camera-to-avatar response is rechecked.
- Phase 6: **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## Why the current checkpoint exists

Real USER tests with HP TrueVision and DroidCam Video showed working camera input but low end-to-end responsiveness. Under the supplied OBS recordings, fresh camera and Unity render cadence were often in the teens/low 20s, inference requests/results were commonly around 3–5/s, DetectAsync-to-callback itself was commonly around 56–94 ms, and pose age could reach hundreds of milliseconds. Treat those as approximate USER-machine evidence, not locked benchmark values.

Presentation smoothing OFF did not noticeably improve response, so this checkpoint does not retune or remove the accepted presentation layer. The temporary upside-down Neko observation remained transient in later tests. The `QuaternionToEuler` warning later became reproducible on the active URP camera-culling path and was addressed at `b64a3115401689b94cf5f86d691fc5e6c763f510` by normalizing the Lab camera Transform quaternion before enabling the Camera. That correction is implemented but is not USER runtime accepted yet; do not reopen Phase 4 orientation without new evidence.

## Source audit result

The important responsiveness bottleneck was proven in `MediaPipePoseProvider`, not guessed from FPS values. The old Update gate combined `_readbackPending || _inferenceOutstanding` into one busy condition. The next camera readback could therefore begin only after the previous MediaPipe result callback cleared inference occupancy.

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

The provider allows camera preparation to overlap one active inference while keeping strict bounds:

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

The provider contains one reversible, body-pose-only resolution experiment. The full-resolution `WebCamTexture` remains the authoritative `CameraTexture` and Lab display source. When enabled, the newest source frame is scale-submitted into one persistent, aspect-preserving inference `RenderTexture` before the existing `TextureFrame.ReadTextureAsync` path. The default target is a `320`-pixel long edge, producing `320x240` from `640x480` and `240x320` from `480x640`. Disabling the option uses the exact actual camera dimensions.

The normal custom `MediaPipePoseProvider` Inspector exposes a **Body Pose Inference** section with `Enable Body Inference Downscale` and `Body Inference Long Edge`. The current USER-facing long-edge range is `160..640`; the value remains stored but the long-edge control is disabled/greyed while downscale is off. Both controls remain available in normal Play Mode Inspector use, so no Debug Inspector is required for the USER A/B test. The normal Runtime Camera section shows the actual active `Body Input` dimensions and `scaled/native` state from provider resources.

The `TextureFramePool` matches the body-inference texture, resource lifecycle follows camera start/restart/switch/shutdown, and the one-readback/one-prepared-frame/one-inference latest-frame policy is unchanged. Existing orientation metadata, flip arguments and `ImageProcessingOptions` rotation are preserved; the scale submission itself does not intentionally rotate or mirror. F7 reports the actual active body input dimensions and whether the path is `scaled` or `native`.

`BodyInferenceResolution` keeps its broader defensive runtime clamp for malformed/legacy serialized data. The normal Inspector does not advertise that broad safety range; it constrains the experiment to `160..640`. A target at or above the source long edge remains native rather than upscaling.

This checkpoint is not USER accepted; the USER must run the comparable `640x480` native versus `320x240` body-inference A/B benchmark. The lower body-pose resolution is not a master camera resolution and does not constrain future high-detail hand/finger tracking, which may use the unchanged full-resolution camera or separate high-resolution hand ROIs. No hand/finger tracking was added. Phase 5A remains **NOT USER ACCEPTED** and Phase 6 remains **NOT STARTED**.

## Independent audit of the e56bfbb1 body-inference checkpoint

The body-inference diff was audited against parent `b64a3115401689b94cf5f86d691fc5e6c763f510` before this bounded Inspector correction.

Source-level findings:
- full-resolution `WebCamTexture` remains `CameraTexture`; Lab presentation still reads that texture and was not coupled to body-inference dimensions;
- `BodyInferenceResolution.Calculate` preserves source aspect ratio, returns `320x240` for `640x480`, `240x320` for `480x640`, uses exact source dimensions when disabled, and refuses to upscale when target long edge is at/above source;
- the persistent inference `RenderTexture` exists only when inference dimensions differ from the actual camera dimensions;
- the `TextureFramePool` is rebuilt to exactly the active inference dimensions;
- live configuration changes stop admitting new body work while readback/inference is active, release any prepared frame after those stages drain, then rebuild resources;
- camera switch/restart cleanup releases prepared work before disposing the pool and downscale RenderTexture;
- normalized MediaPipe landmarks are consumed downstream as normalized coordinates, so changing input pixel dimensions does not require a pixel-resolution remap;
- F7 `Body input` dimensions and `scaled/native` status come from active provider resources, not merely from configured checkbox intent.

No source-proven orientation regression was found. Downscale happens in the raw source texture dimensions first; the existing H/V flags remain arguments to Homuler `ReadTextureAsync`, and quarter-turn rotation remains in `ImageProcessingOptions`. This preserves the established contract for landscape and portrait/rotated inputs at source level.

One runtime-only convention risk remains to be checked on hardware: the scaled path inserts a neutral `Graphics.Blit(WebCamTexture -> RenderTexture)` before Homuler performs its own `Graphics.Blit` with the established flip scale/offset into a temporary RenderTexture. Static source shows no intentional extra mirror/rotation, but Unity backend texture-origin/color-space behavior across this extra blit is not strong enough evidence to claim a visual orientation PASS without the USER A/B.

Shutdown during an already in-flight Unity async readback still uses the project's existing immediate teardown pattern. The body-inference RenderTexture itself is only the source of Homuler's blit; the AsyncGPUReadback targets Homuler's internal temporary RenderTexture. Camera switch/rebuild explicitly wait for readback/inference to drain. Full teardown callback ordering is therefore treated as an existing runtime-only uncertainty rather than a new confirmed e56 defect.

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

Before enabling the controlled Camera, `ThirdPersonLabCamera` safely normalizes its Transform quaternion component-wise (with finite/near-zero validation). The intentional clear-only Lab presentation remains unchanged; this addresses the reproducible URP `Camera.GetCullingParameters` `QuaternionToEuler` spam from slightly non-unit serialized camera rotations. Fresh USER runtime verification of that correction is still pending.

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

Run the native-vs-scaled A/B **without OBS first** and do not change camera between the two laptop runs.

For HP TrueVision:
1. Request `640x480 @ 30`; target inference `30`.
2. After source/camera selection, run `C`; after calibration, run `K` before locomotion checks.
3. In the normal `MediaPipePoseProvider` Inspector, turn **Enable Body Inference Downscale OFF**. Confirm Runtime Camera/F7 reports `Body Input: 640x480 native`, let F7 settle, perform rapid arm/torso motion, and record camera FPS, render FPS, req/callback/pose FPS, RB/build/detect/~F->R, pose age, dominant waits and visible response.
4. On the same camera with the same `640x480 @ 30` capture and target inference `30`, turn downscale **ON** and set **Body Inference Long Edge = 320**. Confirm `Body Input: 320x240 scaled`, let F7 settle, and repeat the identical motion/metrics check.
5. Compare native versus scaled before changing cameras.
6. Only after the laptop comparison, switch to DroidCam, run `C` then `K` for the new source, and repeat the same OFF/native versus ON/320 A/B.
7. Optionally repeat with OBS afterward to measure recording overhead separately.

Also confirm the Lab has no `No cameras rendering` placeholder and watch for any orientation/overlay regression on the scaled path. The target is convincing low-latency character control, not a cosmetic 30-FPS number.
