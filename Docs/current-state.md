# Current state

## Authoritative checkpoint — DirectCPU teardown lifetime guard

Working branch: `engine/pose-tracking-spike`.

Starting checkpoint for this task: `b86d182bcf79764b4d1ab66301304f18d811f14a` — `fix: support flipped direct body readback`.

Status:
- Phase 4: **USER ACCEPTED — PASS**.
- `b64a3115401689b94cf5f86d691fc5e6c763f510` Lab-camera quaternion / clear-only camera correction: **USER-runtime accepted**.
- Immediate Launch After Readback at `87b68999cf73f6dee09cace6c8ef264697158de4`: **USER-runtime accepted** and kept ON.
- Direct Body CPU Readback flip staging + teardown guard: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 5A: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.
- Do not merge to `main` without explicit USER approval.

## Accepted response improvements so far

The body-pose-only inference downscale remains enabled at a `320`-pixel long edge. The full-resolution `WebCamTexture` remains the authoritative `CameraTexture` and Lab-preview source. A `640x480` source therefore stays full resolution for camera presentation while the current body-pose experiment uses `320x240`.

The USER's laptop native-vs-scaled comparison showed only a modest benefit from the body-only downscale:

- Native `640x480`: RB about `63.3 ms`, `~F->R` about `144.9 ms`, pose results about `10.85/s`.
- Scaled `320x240`: RB about `58.3 ms`, `~F->R` about `137.5 ms`, pose results about `11.75/s`.

Conclusion: keep `320x240` for body pose, but do not reduce to `160x120` merely to chase latency. No obvious body-tracking collapse was visible in the supplied recording.

The next scheduling experiment removed a real render-frame boundary after readback completion. With `320x240` body input in both runs, the USER measured approximately:

| Metric | Immediate Launch OFF | Immediate Launch ON |
| --- | ---: | ---: |
| Capture | 29.9 FPS | 29.5 FPS |
| Render | 33.1 FPS | 33.5 FPS |
| Requests/results | 11.0/s | 11.7/s |
| RB | 60.4 ms | 56.7 ms |
| Detect | 61.8 ms | 56.7 ms |
| Prepared -> launch | 25.7 ms | 0.4 ms |
| Approx. frame -> result | 150.4 ms | 117.7 ms |
| Frame delta | usually 1 | usually 0 |
| Launch origin | Update | RB |
| Fast-launch rate | ~0/s | ~10–12/s |

This is accepted USER runtime evidence. **Immediate Launch After Readback stays ON.** It removed roughly one Unity render-frame scheduling delay. Do not reopen prepared-frame -> DetectAsync scheduling unless new reproducible evidence requires it.

The remaining major timing on the USER laptop is now roughly:
- observed readback preparation (`RB`): `55–60 ms`;
- DetectAsync -> callback: `55–60 ms`;
- approximate fresh-frame -> result: `115–120 ms`.

## Bounded latest-frame pipeline

The provider preserves the following hard bounds:

```text
<= 1 active camera/body readback
<= 1 replaceable prepared TextureFrame
<= 1 outstanding MediaPipe inference
```

The newest useful camera frame wins. Readback may overlap one active inference. A newer prepared frame replaces/releases the previous prepared slot. There is no camera-frame history, inference backlog, delayed pose replay or catch-up loop.

When a readback successfully publishes a prepared frame, the accepted immediate-launch path attempts the same shared inference launch immediately if all safety/cadence conditions allow it. If not, the frame remains prepared for the ordinary Update path. The actual BuildCPUImage/Release/DetectAsync code remains a single launch authority.

Physical camera switching still has priority. Pending switches do not allow the readback continuation to start another inference. Coordinate-convention versions are snapshotted with readback work and stale work is discarded. Live body-input configuration changes cannot launch frames prepared under obsolete resources; the ordinary Update resource-rebuild path releases stale prepared work after active readback/inference drains.

## Direct Body CPU Readback experiment

The serialized control is **Direct Body CPU Readback**, and its default remains **OFF** so the USER-accepted Homuler/Immediate-Launch checkpoint remains the default behavior.

The accepted baseline remains unchanged:

```text
persistent body RenderTexture
-> TextureFrame.ReadTextureAsync(...)
-> Homuler temporary staging RenderTexture + Graphics.Blit
-> AsyncGPUReadback
-> callback GetData<byte>()
-> Texture2D.LoadRawTextureData(...)
-> Texture2D.Apply()
-> prepared TextureFrame
-> accepted immediate launch
-> BuildCPUImage
-> DetectAsync
```

The DirectCPU candidate continues to write directly into the pooled TextureFrame-owned CPU buffer:

```text
persistent 320x240 body RenderTexture
-> pooled RGBA32 TextureFrame
-> TextureFrame.GetRawTextureData<byte>()
-> AsyncGPUReadback.RequestIntoNativeArray(..., TextureFormat.RGBA32, null)
-> same coroutine request.done observation
-> no LoadRawTextureData copy
-> no Texture2D.Apply
-> prepared TextureFrame
-> accepted immediate launch
-> existing BuildCPUImage
-> DetectAsync
```

The Homuler package is not modified. The direct path uses the TextureFrame-owned CPU raw buffer rather than allocating a second persistent NativeArray. The NativeArray view is never manually disposed.

`TextureFrame.BuildCPUImage()` constructs `Mediapipe.Image` from the Texture2D, and the installed `Mediapipe.Image(ImageFormat, Texture2D)` constructor reads `Texture2D.GetRawTextureData<byte>()`. Golden Needle's prepared-frame handoff uses `BuildCPUImage`, not `BuildGPUImage`, so the direct candidate does not need `Texture2D.Apply` for the current CPU body-pose handoff.

### USER evidence from `00cbb1`

`00cbb1` compiled and ran on USER hardware. With HP TrueVision, `640x480 @ 30`, body input `320x240`, and Direct Body CPU Readback enabled, F7 reported:

```text
Readback: DirectFallback direct=0.0/s fail=0.0/s (inference flip required)
```

This was **not a DirectCPU runtime failure**. `direct=0.0/s` and `fail=0.0/s` show that no direct request was attempted. The `00cbb1` policy intentionally rejected any H/V inference-flipped input before submitting DirectCPU. Therefore there is still **no DirectCPU performance result** from that run and the fallback must not be benchmarked as the candidate.

### Flip-staging follow-up at `b86d182`

`b86d182` keeps the no-flip DirectCPU path unchanged and extends the candidate to flipped inputs using one persistent Golden Needle-owned staging RenderTexture. The staging RT is created/released with the scaled body-inference resources and has the same active body-input dimensions, no mipmaps, AA 1, Clamp wrap and matching body RT format.

For no inference flip:

```text
WebCamTexture
-> persistent body RT
-> RequestIntoNativeArray directly from body RT
```

For H and/or V inference flip:

```text
WebCamTexture
-> persistent body RT
-> Graphics.Blit(bodyRT, persistent direct staging RT, scale, offset)
-> RequestIntoNativeArray from direct staging RT
```

The staging transform mirrors Homuler exactly:

```text
None: scale=( 1, 1), offset=(0,0)
H:    scale=(-1, 1), offset=(1,0)
V:    scale=( 1,-1), offset=(0,1)
HV:   scale=(-1,-1), offset=(1,1)
```

Quarter-turn rotation remains exclusively in `ImageProcessingOptions`; it is not baked into the staging RT. Camera/canonical/display semantics therefore remain unchanged.

DirectCPU eligibility remains: experiment ON, provider Ready, active persistent scaled body RT, matching TextureFrame/body dimensions, current body-resource intent, valid flip staging if needed, RGBA32 frame/raw buffer, supported async GPU readback/ReadPixels formats, and a DirectCPU session not already disabled by an error. H/V flip is no longer itself a rejection reason.

## Post-audit DirectCPU teardown lifetime guard

A post-audit of `b86d182` found a concrete Play-stop/`OnDestroy` ownership hazard before any DirectCPU benchmark was accepted. `RequestIntoNativeArray` writes directly into the pooled TextureFrame-owned Texture2D CPU memory. The normal coroutine kept that frame alive until `request.done`, but `CleanupRuntime()` previously disposed the TextureFramePool and body/staging RenderTextures immediately. If Unity stopped the coroutine during Play-stop while the direct request was still in flight, the backing Texture2D could be destroyed while the GPU/Unity request still owned its NativeArray target.

The provider now tracks the exact active DirectCPU `AsyncGPUReadbackRequest` at provider scope. The tracked request becomes valid immediately after `RequestIntoNativeArray` successfully returns. Normal asynchronous behavior is unchanged: the coroutine still waits without blocking the frame loop, and only after the direct request reaches a terminal `done` state does it clear provider-owned request tracking and continue with timeout/error handling or normal publication.

Timeout semantics remain conservative. Timing out disables DirectCPU for the current session, but it does **not** clear active request ownership; the coroutine continues waiting for actual `request.done`, then clears tracking and releases the frame. An error is also considered safe to clear only after the request is terminal.

Before `CleanupRuntime()` disposes the TextureFramePool or releases the direct staging/body RenderTextures, it now checks the tracked DirectCPU request. If the tracked request is valid and not done, teardown calls that request's own `WaitForCompletion()` exactly once in this exceptional cleanup path. This synchronous wait is not used in the normal frame loop and does not replace the asynchronous runtime path. Only after the request is confirmed terminal are the pooled TextureFrame backing memory and RenderTextures destroyed.

If request-state inspection or `WaitForCompletion()` throws, cleanup acts conservatively: it does not dispose the TextureFramePool or body/direct-staging RenderTextures while the provider still knows a DirectCPU request may be writing to them. Restart is blocked rather than rebuilding on top of uncertain ownership. A diagnostic is emitted for that exceptional teardown failure. Normal camera switching already waits until `_readbackPending` and inference are clear, so under expected operation this guard is a no-op for camera switching and ordinary restarts.

The guard changes teardown safety only. It does not change the DirectCPU running path, persistent flip transform, readback eligibility, body resolution, Homuler fallback, accepted immediate-launch behavior, canonical coordinates, calibration, retargeting, locomotion, F12, Lab geometry or readback timing boundaries.

## Readback observability

The RB timer keeps the same boundaries for Homuler and DirectCPU: it starts before the full-resolution -> body RenderTexture blit and ends when Golden Needle observes readback completion.

For flipped DirectCPU, RB includes camera -> body RT, body RT -> persistent direct staging RT flip blit, direct AsyncGPUReadback, and coroutine completion observation. For Homuler, RB continues to include camera -> body RT plus Homuler's temporary staging/flip/readback/copy/Apply path. This keeps the A/B meaningful. RB remains an observed end-to-end preparation duration, not a pure GPU-time claim.

F7 continues to expose body input, capture/render cadence, request/result/callback rates, RB/build/detect/~F->R, pose age, prepared->launch delay/frame delta/origin/fast rate, waits, readback failures/timeouts, prepared replacements, active readback path, DirectCPU stage (`None/H/V/HV`), current `H=0/1 V=0/1`, direct submissions/s, direct failures/s and fallback reason. No per-frame diagnostic logging was added by the teardown guard.

## Camera / orientation / Lab behavior remains frozen

External cameras remain one Unity `WebCamTexture` / `WebCamDevice` path. Scene defaults remain automatic device, `640x480 @ 30`, target inference 30 FPS, Orientation Auto and display mirror OFF. `480x640 @ 30` remains supported for portrait capture.

Front-facing metadata still does not imply inference horizontal mirror: production remains `shouldFlipHorizontally=false`. Display mirror remains presentation-only. Auto/0/90/180/270 orientation behavior, fitted whole-frame Lab geometry, F1/F2/F4 overlay mapping and F12 Lab/Game behavior are unchanged.

The clear-only Lab Camera and quaternion normalization correction at `b64a311...` received USER runtime QA and all four targeted checks passed:
1. `QuaternionToEuler` warning stayed gone.
2. `No cameras rendering` stayed gone.
3. Lab webcam/overlays remained correct.
4. F12 Game -> Lab switching worked and did not restart the warning.

That acceptance is specific to the camera correction and does not imply Phase 5A acceptance.

## Motion Engine behavior preserved

No canonical-coordinate semantics, modular calibration, signed canonical-to-avatar mapping, accepted Phase 4 Humanoid retarget/IK, Neko binding, presentation smoothing, Phase 5A support-foot model, cadence/fusion/heading, physical scales `0.9 / 1.5`, K recenter behavior, root-Y exclusion or root-rotation exclusion is redesigned by this experiment.

Pose Landmarker remains Lite, CPU delegate, one pose, segmentation OFF. No hand/finger tracking was added.

## Verification state / next USER QA

Focused deterministic tests retain scheduler/immediate-launch, body-resolution, DirectCPU path-selection and None/H/V/HV flip-transform coverage. Additional teardown policy coverage verifies:
- no active DirectCPU request -> cleanup does not require a wait;
- tracked valid + not done -> cleanup policy requires a wait and ownership cannot clear;
- tracked valid + done -> no blocking wait is needed and ownership can clear;
- a timeout state does not permit ownership clear until actual completion.

Unity compilation and Unity Test Runner have not been run by the Web Builder environment unless a later report explicitly says otherwise.

Before benchmarking, first perform the DirectCPU safety smoke on HP TrueVision: compile, enter Play with DirectCPU ON, verify the actual path is `DirectCPU`, then stop/restart Play repeatedly at arbitrary moments while the path is active. Confirm no NativeArray/AsyncGPUReadback/Texture destruction errors. Also verify live Direct toggle and camera switch remain safe.

Only after that smoke passes, run the readback A/B with identical settings: HP TrueVision, camera request `640x480 @ 30`, target inference `30`, body downscale ON, long edge `320`, Immediate Launch After Readback ON, then `C` and `K`.

- **Run A:** Direct Body CPU Readback OFF; F7 must show `Readback: Homuler`.
- **Run B:** change only Direct Body CPU Readback ON; F7 must show `Readback: DirectCPU stage=H/V/HV/None`. Record `H=0/1 V=0/1`. If it says `DirectFallback`, do not benchmark; report the exact reason.

Use the same OBS sequence for both runs and record capture FPS, render FPS, req/s, res/s, cb/s, RB, build, detect, Prep->launch, ~F->R, pose age, wait rates, replacement rate, direct/s, direct failures/s, stage and subjective responsiveness. Verify F1/F2/F4 alignment, upright skeleton/avatar, no mirror inversion, unchanged full-resolution Lab preview and a clean Console.

DirectCPU remains **IMPLEMENTED / NOT USER ACCEPTED** until the safety smoke and a real DirectCPU A/B are completed. No DirectCPU performance improvement is claimed yet.
