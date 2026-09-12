# Golden Needle — Orchestrator handoff

## Governance

Repository: `AltamashM7/GoldenNeedle`

Working branch: `engine/pose-tracking-spike`

Starting checkpoint for the DirectCPU flip-staging follow-up: `00cbb1b6024dea293443b2c4a505d86ca5c1ade4` — `perf: add direct cpu body readback experiment`.

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
- body pose at `320x240` for the current `640x480` QA path;
- camera request default `640x480 @ 30`, target inference `30`;
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

No hand/finger tracking is part of this checkpoint. Do not lower the body input further and do not change completion scheduling in this follow-up.

## Accepted USER evidence before this follow-up

The `b64a3115401689b94cf5f86d691fc5e6c763f510` camera corrective checkpoint received USER runtime QA and all four targeted checks passed:
1. `QuaternionToEuler` warning stayed gone.
2. Unity's `No cameras rendering` placeholder stayed gone.
3. Lab webcam/overlays remained correct.
4. F12 Game -> Lab switching worked and did not restart the warning.

The body-pose downscale decision remains `320x240` for a `640x480` source. USER laptop A/B was approximately:
- native 640x480: RB `63.3 ms`, `~F->R 144.9 ms`, results `10.85/s`;
- scaled 320x240: RB `58.3 ms`, `~F->R 137.5 ms`, results `11.75/s`.

Conclusion: keep `320x240`; do not chase `160x120` yet.

The immediate-launch experiment at `87b68999...` is USER-runtime accepted. Both runs used `320x240` body input:

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

Conclusion: **keep Immediate Launch After Readback ON**. Do not reopen the prepared-frame -> DetectAsync scheduling boundary without new evidence.

## USER evidence from `00cbb1`

The USER pulled `00cbb1`, enabled Direct Body CPU Readback on HP TrueVision, entered Play Mode, and F7 reported:

```text
Readback: DirectFallback direct=0.0/s fail=0.0/s (inference flip required)
```

This is important:
- the provider did **not** attempt DirectCPU;
- `direct=0.0/s` confirms no direct request was submitted;
- `fail=0.0/s` confirms there was no DirectCPU runtime failure;
- the fallback guard intentionally rejected the current H and/or V inference-flipped camera convention;
- the accepted Homuler path continued running.

Therefore `00cbb1` produced **no DirectCPU performance result**. Do not benchmark or describe that fallback run as the candidate. The guard worked as designed.

## Source / ownership facts retained from `00cbb1`

Embedded plugin remains MediaPipeUnityPlugin 0.16.3. Homuler package source is untouched.

Homuler `TextureFrame.ReadTextureAsync` remains the accepted general-purpose fallback:
1. temporary staging RenderTexture;
2. `Graphics.Blit` with optional H/V scale+offset flip;
3. `AsyncGPUReadback`;
4. callback `GetData<byte>()`;
5. `Texture2D.LoadRawTextureData(...)`;
6. `Texture2D.Apply()`;
7. temp RT release / native pointer bookkeeping.

The DirectCPU experiment continues to use the pooled `TextureFrame`'s `GetRawTextureData<byte>()` NativeArray view as the readback destination and never disposes that NativeArray manually. The frame stays owned by the active readback until request completion, is published only after completion, then enters the existing `BuildCPUImage` -> accepted immediate-launch -> `DetectAsync` path. No second writer/readback is created for the same frame.

## Flip-staging follow-up implementation

The no-flip DirectCPU path is unchanged:

```text
WebCamTexture
-> persistent 320x240 body RT
-> AsyncGPUReadback.RequestIntoNativeArray directly into pooled TextureFrame CPU buffer
-> no LoadRawTextureData
-> no Texture2D.Apply
-> prepared frame
-> accepted immediate launch
-> BuildCPUImage
-> DetectAsync
```

For inputs requiring an inference H and/or V flip, Golden Needle now owns one additional **persistent** staging RenderTexture with the same width/height and basic texture settings as the active scaled body RT:

```text
WebCamTexture
-> persistent body RT
-> Graphics.Blit(bodyRT, persistent direct staging RT, scale, offset)
-> AsyncGPUReadback.RequestIntoNativeArray from direct staging RT
-> pooled TextureFrame CPU buffer
-> no LoadRawTextureData
-> no Texture2D.Apply
-> same prepared/immediate-launch/BuildCPUImage/DetectAsync path
```

This deliberately does **not** combine camera downscale and flip into one new blit. The established camera -> body RT downscale remains the first stage so the experiment changes only readback strategy.

The staging transform mirrors Homuler exactly:

```text
None: scale=( 1, 1), offset=(0,0)
H:    scale=(-1, 1), offset=(1,0)
V:    scale=( 1,-1), offset=(0,1)
HV:   scale=(-1,-1), offset=(1,1)
```

Quarter-turn rotation remains exclusively in `ImageProcessingOptions`. No canonical, camera, display or anatomical semantics were changed.

## DirectCPU selection after the follow-up

DirectCPU requires:
- toggle ON;
- provider Ready / normal capture session alive;
- persistent scaled body RT active and created;
- body RT dimensions matching the pooled `TextureFrame`;
- body resources matching current serialized intent;
- if H/V flip is required, persistent direct staging RT created and matching body-input dimensions;
- TextureFrame format `RGBA32` and correct raw byte count;
- async GPU readback support;
- chosen direct source / RGBA32-compatible formats supporting `ReadPixels`;
- current provider session still allowing DirectCPU.

H/V flip is **no longer itself a rejection reason**. If a flip is required but staging is missing/invalid, selection becomes `DirectFallback` with reason `direct flip staging unavailable`, and the existing Homuler `ReadTextureAsync(readbackSource, flipH, flipV)` path remains authoritative.

DirectCPU request exceptions/errors/timeouts retain the `00cbb1` behavior: mark direct unavailable for the current provider session, preserve the serialized toggle, and use Homuler on subsequent frames. A timed-out direct request continues holding its TextureFrame/raw memory until Unity reports completion before that frame is released.

## Persistent resource lifecycle

The direct staging RT is created deterministically whenever scaled body-inference resources are created, even if DirectCPU is currently OFF. It performs no blit when unused.

It is released together with the body RT when body-inference resources are rebuilt or the provider session is cleaned up. Ordinary live resource rebuild waits until active readback/inference drains; camera switching already waits on the same readback/inference gates. The staging RT therefore follows the same resource lifetime authority as the body RT and TextureFramePool.

Play-stop/OnDestroy behavior remains subject to USER runtime smoke testing because this Web Builder cannot run Unity. No completion-scheduling redesign was added for this follow-up.

## Diagnostics

RB timing boundaries remain comparable across baseline and candidate: start before the existing webcam -> body RT blit, end when Golden Needle observes request completion.

For flipped DirectCPU, RB includes:
- webcam -> body RT blit;
- body RT -> persistent direct staging RT flip blit;
- direct GPU readback;
- coroutine completion observation.

Baseline Homuler RB still includes:
- webcam -> body RT blit;
- Homuler temp staging acquisition;
- Homuler flip blit;
- readback;
- callback `GetData`;
- `LoadRawTextureData`;
- `Texture2D.Apply`;
- temp release;
- coroutine completion observation.

F7/Status retains capture/render/req/res/cb rates, RB/build/detect/~F->R, pose age, prepared->launch delay/frame delta/origin/fast rate, noFresh/int/RB/inf/pool waits, readback failures/timeouts and prepared replacements.

It now exposes the actual candidate state compactly:

```text
Readback: DirectCPU stage=None|H|V|HV H=0/1 V=0/1 direct=X.X/s fail=Y.Y/s
```

or, when candidate selection is not valid:

```text
Readback: DirectFallback H=0/1 V=0/1 ... (reason)
```

The normal custom Inspector also shows runtime readback path/stage plus explicit inference flip state. There is no per-frame Console spam.

## Focused deterministic tests

Required path-policy coverage now includes:
- experiment OFF -> Homuler;
- valid no-flip -> DirectCPU without staging requirement;
- valid H flip + staging available -> DirectCPU;
- valid V flip + staging available -> DirectCPU;
- valid HV flip + staging available -> DirectCPU;
- flip required + staging unavailable -> DirectFallback;
- dimension mismatch -> DirectFallback;
- body-resource mismatch -> DirectFallback;
- format unsupported -> DirectFallback;
- session unavailable -> DirectFallback;
- serialized Direct Body CPU Readback default -> OFF.

Pure transform coverage asserts:
- None -> scale `(1,1)`, offset `(0,0)`;
- H -> scale `(-1,1)`, offset `(1,0)`;
- V -> scale `(1,-1)`, offset `(0,1)`;
- HV -> scale `(-1,-1)`, offset `(1,1)`.

Existing immediate-launch, scheduler and body-resolution tests remain.

Unity compilation/Test Runner/runtime status must be stated explicitly by the Builder report. Static source review must not be converted into a Unity PASS.

## Next USER A/B

Use **HP TrueVision** and isolate only readback strategy.

Common settings:
- camera request `640x480 @ 30`;
- target inference `30`;
- body downscale ON;
- body long edge `320` (`320x240` expected);
- Immediate Launch After Readback ON;
- run `C`, then `K`.

**Run A — accepted baseline**
- Direct Body CPU Readback OFF;
- confirm F7 says `Readback: Homuler`.

**Run B — candidate**
- change only Direct Body CPU Readback to ON;
- confirm F7 says `Readback: DirectCPU stage=H`, `V`, `HV` or `None`;
- record displayed `H=0/1 V=0/1`.
- if F7 still says `DirectFallback`, **do not benchmark**; record the exact fallback reason and flip state.

Use the same OBS flow: Play Mode -> settle/C/K -> start OBS -> wait 5–10 seconds -> comparable arm/torso motion -> stop OBS while Play Mode remains running.

Record capture FPS, render FPS, req/s, res/s, cb/s, RB, build, detect, Prep->launch, `~F->R`, pose age, wait rates, replacement rate, DirectCPU rate, direct failures, stage mode and subjective responsiveness.

Also verify:
- F1/F2/F4 alignment;
- upright skeleton/avatar;
- no new mirror inversion;
- full-resolution Lab preview unchanged;
- Console clean;
- live Direct toggle safe;
- Play stop/restart safe;
- camera switch safe.

Accepted baseline remains approximately: RB `56.7 ms`, detect `56.7 ms`, Prep->launch `0.4 ms`, `~F->R 117.7 ms`, results `11.7/s`.

DirectCPU passes only if the actual runtime path is `DirectCPU`, orientation is correct, RB and `~F->R` reproducibly improve without meaningful result-rate regression, and there are no direct errors/timeouts or resource-lifetime regressions. DirectCPU remains **IMPLEMENTED / NOT USER ACCEPTED** until that real candidate run exists.