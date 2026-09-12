# Current state

## Authoritative checkpoint — DirectCPU flip-staging follow-up

Working branch: `engine/pose-tracking-spike`.

Starting checkpoint for this task: `00cbb1b6024dea293443b2c4a505d86ca5c1ade4` — `perf: add direct cpu body readback experiment`.

Status:
- Phase 4: **USER ACCEPTED — PASS**.
- `b64a3115401689b94cf5f86d691fc5e6c763f510` Lab-camera quaternion / clear-only camera correction: **USER-runtime accepted**.
- Immediate Launch After Readback at `87b68999cf73f6dee09cace6c8ef264697158de4`: **USER-runtime accepted** and kept ON.
- Direct Body CPU Readback: **IMPLEMENTED / NOT USER ACCEPTED**.
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

The Homuler package is not modified. The direct path uses the TextureFrame-owned CPU raw buffer rather than allocating a second persistent NativeArray. The TextureFrame stays owned by the active readback until Unity reports completion; the NativeArray view is never manually disposed. Existing readback/inference gates remain the lifetime authority for the TextureFramePool and body resources.

`TextureFrame.BuildCPUImage()` constructs `Mediapipe.Image` from the Texture2D, and the installed `Mediapipe.Image(ImageFormat, Texture2D)` constructor reads `Texture2D.GetRawTextureData<byte>()`. Golden Needle's prepared-frame handoff uses `BuildCPUImage`, not `BuildGPUImage`, so the direct candidate does not need `Texture2D.Apply` for the current CPU body-pose handoff.

### USER evidence from `00cbb1`

`00cbb1` compiled and ran on USER hardware. With HP TrueVision, `640x480 @ 30`, body input `320x240`, and Direct Body CPU Readback enabled, F7 reported:

```text
Readback: DirectFallback direct=0.0/s fail=0.0/s (inference flip required)
```

This was **not a DirectCPU runtime failure**. `direct=0.0/s` and `fail=0.0/s` show that no direct request was attempted. The `00cbb1` policy intentionally rejected any H/V inference-flipped input before submitting DirectCPU. Therefore there is still **no DirectCPU performance result** from that run and the fallback must not be benchmarked as the candidate. The guard worked as designed.

### Flip-staging follow-up

This follow-up keeps the no-flip DirectCPU path unchanged and extends the candidate to flipped inputs using one persistent Golden Needle-owned staging RenderTexture. The staging RT is created/released with the scaled body-inference resources and has the same active body-input dimensions, no mipmaps, AA 1, Clamp wrap and matching body RT format.

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

### DirectCPU eligibility

DirectCPU is used only when all of the following are true:
- the experiment is enabled;
- provider/capture state is Ready;
- a persistent downscaled body RenderTexture is active;
- body-source dimensions exactly match the pooled TextureFrame dimensions;
- the body-inference resources still match current serialized intent;
- if H and/or V inference flip is required, the persistent direct staging RT exists, is created and matches the body-input dimensions;
- the pooled frame format is `TextureFormat.RGBA32` with the expected raw-buffer size;
- asynchronous GPU readback is supported;
- the chosen direct readback source and RGBA32-compatible readback format report `ReadPixels` capability;
- the current provider session has not already marked DirectCPU unavailable.

H/V flip is no longer itself a reason to reject DirectCPU. If a flip is required but the persistent staging RT is absent/invalid, the provider uses `DirectFallback` with reason `direct flip staging unavailable` and executes the accepted Homuler readback path.

If a DirectCPU request itself throws, completes with an error, or exceeds the configured readback timeout, DirectCPU remains marked unavailable for the current provider session. Subsequent frames use Homuler and F7/Inspector expose the fallback reason. The serialized toggle is not rewritten. A DirectCPU timeout keeps the TextureFrame/native-array memory alive until the outstanding request actually completes before releasing it, preserving RequestIntoNativeArray lifetime requirements.

## Readback observability

The RB timer keeps the same boundaries for Homuler and DirectCPU: it starts before the full-resolution -> body RenderTexture blit and ends when Golden Needle observes readback completion.

For flipped DirectCPU, RB therefore includes:
- camera -> body RT blit;
- body RT -> persistent direct staging RT flip blit;
- direct AsyncGPUReadback;
- coroutine completion observation.

For Homuler, RB continues to include camera -> body RT plus Homuler's temporary staging/flip/readback/copy/Apply path. This keeps the next A/B meaningful. RB remains an observed end-to-end preparation duration, not a pure GPU-time claim.

Existing F7 metrics remain: body input, capture/render cadence, request/result/callback rates, RB/build/detect/~F->R, pose age, prepared->launch delay/frame delta/origin/fast rate, waits, readback failures/timeouts and prepared replacements.

The readback diagnostic now also exposes:
- active path: `Homuler`, `DirectCPU`, or `DirectFallback`;
- actual DirectCPU staging mode: `None`, `H`, `V`, or `HV`;
- current inference flip state as `H=0/1 V=0/1`;
- DirectCPU submissions per second;
- DirectCPU failures per second;
- current fallback reason when applicable.

There is no per-frame diagnostic logging.

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

Pose Landmarker remains Lite, CPU delegate, one pose, segmentation OFF. No hand/finger tracking was added. Future hand/finger work can still consume the unchanged full-resolution camera or high-resolution ROIs independently of the `320x240` body-pose path.

## Verification state / next USER QA

Focused deterministic tests cover scheduler/immediate-launch policy, body resolution, DirectCPU path selection for None/H/V/HV staging, fallback conditions, and the exact Homuler-equivalent flip scale/offset calculation. Unity compilation and Unity Test Runner have not been run by the Web Builder environment unless a later report explicitly says otherwise.

The next USER benchmark isolates **readback strategy only** on HP TrueVision:
1. Camera request `640x480 @ 30`; target inference `30`.
2. Body downscale ON; long edge `320` for both runs.
3. Immediate Launch After Readback ON for both runs.
4. Run `C`, then `K`.
5. Baseline A: Direct Body CPU Readback OFF; confirm F7 path `Homuler`.
6. Candidate B: Direct Body CPU Readback ON; confirm F7 path `DirectCPU stage=H`, `V`, `HV`, or `None`. Record the displayed `H=0/1 V=0/1` state. If it still says `DirectFallback`, do not benchmark; report the exact fallback reason.
7. Use the same OBS procedure and similar arm/torso motion. Record capture/render FPS, req/res/cb rates, RB/build/detect, Prep->launch, ~F->R, pose age, wait rates, replacement rate, DirectCPU rate/failures/stage and subjective responsiveness.
8. Verify webcam/Lab presentation, F1/F2/F4 alignment, upright skeleton/avatar, no mirror inversion, stable body tracking and no Console errors.
9. Also verify live Direct toggle, Play stop/restart and camera switch remain safe.

DirectCPU remains **IMPLEMENTED / NOT USER ACCEPTED** until a real `DirectCPU` run is recorded and compared. No performance improvement is claimed from the previous fallback run.