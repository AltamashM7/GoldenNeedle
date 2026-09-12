# Golden Needle — Orchestrator handoff

## Governance

Repository: `AltamashM7/GoldenNeedle`

Working branch: `engine/pose-tracking-spike`

Starting checkpoint for the current task: `87b68999cf73f6dee09cace6c8ef264697158de4` — `perf: launch prepared pose inference without extra update delay`.

Status:
- Phase 4: **USER ACCEPTED — PASS**.
- `b64a311...` Lab-camera quaternion/clear-only-camera correction: **USER-runtime accepted**.
- Immediate Launch After Readback: **USER-runtime accepted**; keep ON.
- Direct Body CPU Readback: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 5A: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## Frozen architecture

Do not redesign without new reproducible USER evidence:
- at most one active readback;
- at most one replaceable prepared `TextureFrame`;
- at most one outstanding MediaPipe inference;
- newest useful frame wins;
- no camera-frame history, inference backlog, delayed replay or catch-up loop;
- full-resolution `CameraTexture` / Lab preview;
- body pose at the current `320x240` experiment for a `640x480` source;
- 640x480 camera request default and target inference 30 FPS;
- Pose Landmarker Lite, CPU delegate, one pose, segmentation OFF;
- camera switching and physical-source convention invalidation;
- Auto/0/90/180/270 orientation;
- display mirror presentation-only;
- no automatic front-facing inference H mirror;
- canonical mapping and modular calibration;
- accepted Phase 4 Humanoid retarget/IK and Neko binding;
- presentation smoothing;
- Phase 5A support/cadence/fusion/heading/recenter behavior;
- F12 Lab/Game behavior;
- fitted Lab webcam/overlay geometry.

No hand/finger tracking is part of this checkpoint.

## Accepted USER evidence before this task

The `b64a3115401689b94cf5f86d691fc5e6c763f510` camera corrective checkpoint received USER runtime QA and all four targeted checks passed:
1. `QuaternionToEuler` warning stayed gone.
2. Unity's `No cameras rendering` placeholder stayed gone.
3. Lab webcam/overlays remained correct.
4. F12 Game -> Lab switching worked and did not restart the warning.

The lower-resolution body-pose experiment remains at `320x240`. USER laptop A/B was approximately:
- native 640x480: RB `63.3 ms`, `~F->R 144.9 ms`, results `10.85/s`;
- scaled 320x240: RB `58.3 ms`, `~F->R 137.5 ms`, results `11.75/s`.

Conclusion: keep `320x240`; modest benefit; do not chase `160x120` yet.

The immediate-launch experiment at `87b68999...` is now USER-runtime accepted. Both runs used `320x240` body input:

**Immediate Launch OFF**
- capture ~29.9 FPS;
- render ~33.1 FPS;
- requests/results ~11.0/s;
- RB ~60.4 ms;
- detect ~61.8 ms;
- prepared->launch ~25.7 ms;
- `~F->R` ~150.4 ms;
- frame delta generally 1;
- origin Update;
- fast ~0/s.

**Immediate Launch ON**
- capture ~29.5 FPS;
- render ~33.5 FPS;
- requests/results ~11.7/s;
- RB ~56.7 ms;
- detect ~56.7 ms;
- prepared->launch ~0.4 ms;
- `~F->R` ~117.7 ms;
- frame delta generally 0;
- origin RB;
- fast ~10–12/s.

Conclusion: **keep Immediate Launch After Readback ON**. It removed approximately one Unity render-frame scheduling boundary. Do not reopen this scheduling point without new evidence.

The current dominant lower-level timings are now roughly RB `55–60 ms`, detect `55–60 ms`, and `~F->R 115–120 ms`.

## Source audit for the direct readback experiment

Embedded plugin version remains MediaPipeUnityPlugin 0.16.3. Homuler package source is intentionally untouched.

`TextureFrame.ReadTextureAsync` currently performs a general-purpose path:
1. allocate/get a temporary RenderTexture;
2. `Graphics.Blit` the source into that temporary texture, including optional H/V flips;
3. request an async GPU readback;
4. callback obtains `GetData<byte>()`;
5. callback calls `Texture2D.LoadRawTextureData(...)`;
6. callback calls `Texture2D.Apply()`;
7. callback revokes Homuler's cached native texture pointer and releases the temporary RT.

This work is performed even after Golden Needle has already downscaled the full-resolution webcam into its persistent, matching body inference RenderTexture.

Homuler exposes `TextureFrame.GetRawTextureData<byte>()`. `TextureFrame.BuildCPUImage()` constructs `new Mediapipe.Image(imageFormat, Texture2D)`. The installed `Mediapipe.Image(ImageFormat, Texture2D)` constructor in turn passes `texture.GetRawTextureData<byte>()` to the CPU image constructor. Therefore the current Golden Needle body-pose handoff consumes the Texture2D's CPU raw-data buffer.

The current prepared-frame owner in `MediaPipePoseProvider` calls `BuildCPUImage()` only; there is no Golden Needle `BuildGPUImage` consumer between readback completion and frame release. This supports a narrow no-`Apply` experiment without changing Homuler's general-purpose implementation.

Unity project version is `6000.5.0f1`. Unity 6 exposes the required overload:

```csharp
AsyncGPUReadback.RequestIntoNativeArray(
    ref NativeArray<T> output,
    Texture src,
    int mipIndex,
    TextureFormat dstFormat,
    Action<AsyncGPUReadbackRequest> callback)
```

The candidate uses `TextureFormat.RGBA32` explicitly. Capability is checked using `SystemInfo.supportsAsyncGPUReadback` and `SystemInfo.IsFormatSupported(..., GraphicsFormatUsage.ReadPixels)` before selecting DirectCPU.

## Direct Body CPU Readback implementation

The serialized control is **Direct Body CPU Readback** and defaults **OFF**. This preserves the accepted Homuler + immediate-launch path as the default until USER QA accepts the candidate.

When eligible, the Golden Needle-owned candidate is:

```text
full-resolution WebCamTexture
-> existing Graphics.Blit into persistent 320x240 body RT
-> pooled RGBA32 TextureFrame
-> TextureFrame.GetRawTextureData<byte>()
-> AsyncGPUReadback.RequestIntoNativeArray(..., TextureFormat.RGBA32, null)
-> same coroutine request.done observation
-> NO LoadRawTextureData copy
-> NO Texture2D.Apply
-> publish same TextureFrame
-> accepted immediate-launch path
-> existing BuildCPUImage
-> DetectAsync
```

No separate persistent NativeArray is allocated. The returned NativeArray is a view into the pooled TextureFrame's Texture2D CPU memory and is never disposed manually.

### Strict path selection

DirectCPU requires:
- toggle ON;
- provider Ready / ordinary capture state;
- active persistent downscaled body RenderTexture;
- source RT width/height exactly matching `TextureFrame.width/height`;
- body resources matching current serialized intent;
- inference H flip false;
- inference V flip false;
- pooled frame format exactly RGBA32 and raw byte length matching width*height*4;
- async GPU readback support;
- source and RGBA32-compatible formats reporting `ReadPixels` capability;
- DirectCPU not previously marked unavailable for this provider session.

The policy exposes three runtime states:
- `Homuler`: direct experiment OFF;
- `DirectCPU`: direct request actually selected;
- `DirectFallback`: direct requested but current conditions/session require Homuler.

No custom flip shader is implemented. Rotation remains in the existing `ImageProcessingOptions` path. If backend row/origin behavior causes inversion/mirroring, treat that as experiment failure rather than changing canonical semantics.

### Lifetime / failure behavior

One-readback ownership remains authoritative. While RequestIntoNativeArray is pending:
- the TextureFrame stays owned by the readback;
- it is not released;
- it is not handed to BuildCPUImage;
- its raw NativeArray is not disposed;
- body resources / frame pool cannot rebuild because `_readbackPending` remains true;
- camera switching waits for the active readback through the existing pending-switch gate.

On successful completion the same prepared-frame publication and accepted immediate-launch code runs.

If direct submission throws or the request completes with an error, the frame is safely released and DirectCPU is marked unavailable for the current provider session. Future frames automatically use Homuler. If a direct request exceeds the configured timeout, it is marked failed/fallback immediately but the TextureFrame remains held until the outstanding GPU request actually completes before release, so Unity never writes into recycled/disposed Texture2D memory. No second readback is launched for that source frame.

The serialized toggle is not rewritten when session fallback occurs. A new provider session may try DirectCPU again if the USER still has the toggle enabled.

## Diagnostics

RB timing remains fair across both paths: it starts before the existing full-res -> body-RT blit and ends when Golden Needle observes request completion. It is not labelled pure GPU time.

F7/Status retains:
- capture/render/req/res/cb rates;
- RB/build/detect/~F->R;
- pose age;
- prepared->launch delay/frame delta/origin/fast rate;
- noFresh/int/RB/inf/pool waits;
- readback failures/timeouts;
- prepared replacements.

It now adds a compact readback line with:
- active readback path (`Homuler`, `DirectCPU`, `DirectFallback`);
- DirectCPU submissions/s;
- DirectCPU failures/s;
- fallback reason when applicable.

The normal custom Inspector exposes the Direct Body CPU Readback toggle and current runtime readback path/fallback reason. No Debug Inspector and no per-frame Console logging is required.

## Verification boundary

Focused deterministic Editor tests cover readback-path policy:
- direct OFF -> Homuler;
- no downscaled body RT -> fallback;
- H flip -> fallback;
- V flip -> fallback;
- dimension mismatch -> fallback;
- unsupported format -> fallback;
- all conditions valid -> DirectCPU;
- session unavailable -> fallback;
- serialized default -> OFF.

Existing immediate-launch, scheduler and body-resolution tests remain.

Unity compile/Test Runner/runtime status must be stated by the Builder report. Do not convert static source inspection into a Unity PASS.

## Next USER A/B

Use **HP TrueVision** and isolate only readback strategy:

Common settings for both runs:
- camera request `640x480 @ 30`;
- target inference `30`;
- body downscale ON;
- body long edge `320` (`320x240` expected);
- Immediate Launch After Readback ON;
- run `C`, then `K`.

**Run A — accepted baseline**
- Direct Body CPU Readback OFF;
- confirm F7 readback path = `Homuler`.

**Run B — candidate**
- Direct Body CPU Readback ON;
- confirm F7 readback path = `DirectCPU`.
- If F7 says `DirectFallback`, do not treat it as a candidate benchmark; record the fallback reason instead.

Use the same OBS sequence for both: enter Play Mode, settle/C/K, start OBS, wait 5–10 seconds, perform similar arm/torso motion, stop OBS while Play Mode remains running.

Record capture FPS, render FPS, req/s, res/s, cb/s, RB, build, detect, Prep->launch, `~F->R`, pose age, wait rates, replacement rate, DirectCPU rate, direct failures, active path and subjective responsiveness. Also verify webcam/Lab image, F1/F2/F4 alignment, no H/V inversion, stable body tracking and no Console errors.

The current accepted baseline is approximately capture `29.5 FPS`, render `33.5 FPS`, results `11.7/s`, RB `56.7 ms`, detect `56.7 ms`, Prep->launch `0.4 ms`, `~F->R 117.7 ms`.

Direct Body CPU Readback remains **IMPLEMENTED / NOT USER ACCEPTED** until this A/B is completed.