# Golden Needle — Orchestrator handoff

## Governance

Repository: `AltamashM7/GoldenNeedle`

Working branch: `engine/pose-tracking-spike`

Starting checkpoint for the DirectCPU teardown-lifetime safety follow-up: `b86d182bcf79764b4d1ab66301304f18d811f14a` — `fix: support flipped direct body readback`.

Status:
- Phase 4: **USER ACCEPTED — PASS**.
- `b64a311...` Lab-camera quaternion/clear-only-camera correction: **USER-runtime accepted**.
- Immediate Launch After Readback: **USER-runtime accepted**; keep ON.
- Direct Body CPU Readback flip staging + teardown guard: **IMPLEMENTED / NOT USER ACCEPTED**.
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

No hand/finger tracking is part of this checkpoint. Do not lower body resolution or redesign completion scheduling in this safety follow-up.

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

That was an intentional selection fallback, not a DirectCPU runtime failure. No direct request was submitted, so no DirectCPU performance result exists from that run.

## DirectCPU flip-staging implementation at `b86d182`

The no-flip candidate remains:

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

For H/V-flipped inference inputs, Golden Needle owns one persistent staging RT:

```text
WebCamTexture
-> persistent body RT
-> Graphics.Blit(bodyRT, persistent direct staging RT, scale, offset)
-> AsyncGPUReadback.RequestIntoNativeArray from direct staging RT
-> pooled TextureFrame CPU buffer
-> same prepared/immediate-launch/BuildCPUImage/DetectAsync path
```

The staging transform mirrors Homuler exactly:

```text
None: scale=( 1, 1), offset=(0,0)
H:    scale=(-1, 1), offset=(1,0)
V:    scale=( 1,-1), offset=(0,1)
HV:   scale=(-1,-1), offset=(1,1)
```

Rotation remains exclusively in `ImageProcessingOptions`. The DirectCPU path remains default OFF and not USER accepted.

The Homuler package remains untouched and authoritative whenever DirectCPU is OFF or ineligible.

## Post-audit teardown hazard found after `b86d182`

A source-level audit found one concrete lifetime issue before USER A/B should proceed.

DirectCPU uses:

```text
TextureFrame.GetRawTextureData<byte>()
-> AsyncGPUReadback.RequestIntoNativeArray(...)
```

The destination NativeArray is a non-owning view into the pooled TextureFrame-owned Texture2D CPU backing memory. While the request is in flight, that memory must remain alive. The normal coroutine respected this, but `CleanupRuntime()` previously disposed the TextureFramePool and released/destroyed the body/direct-staging RenderTextures immediately. On Play-stop/`OnDestroy`, Unity can stop the coroutine before it reaches its normal `request.done` handling, allowing teardown to destroy resources while the direct request still owns/writes the CPU buffer.

This is a source-level ownership hazard, not merely an untested edge case. Do not benchmark DirectCPU before the teardown guard checkpoint.

## Teardown lifetime guard implementation

The provider now stores the exact active DirectCPU `AsyncGPUReadbackRequest` in provider-owned state plus a validity flag.

Tracking begins immediately after `RequestIntoNativeArray` successfully returns. If setup throws before a direct request is returned, no active request is recorded.

### Normal completion

The running-frame path remains asynchronous. The coroutine still yields until `request.done`. Once the request is terminal, provider-owned active-request state is cleared before the pooled TextureFrame is released or published into the existing prepared-frame path. No per-frame `WaitForCompletion()` was introduced.

### Timeout and error ownership

The existing direct timeout behavior is preserved: timeout disables DirectCPU for the current session, but the request remains tracked and the frame remains held while the coroutine keeps waiting for actual `request.done`. Only after actual terminal completion can the provider clear tracked ownership and release the frame.

A direct error is also only cleared from tracked ownership after the request is already terminal. No second readback is launched for that frame.

### Cleanup / OnDestroy

Before `CleanupRuntime()` disposes the TextureFramePool or releases the DirectCPU staging/body RenderTextures, it calls the tracked request's own `WaitForCompletion()` only when a tracked DirectCPU request is valid and not already done.

This blocking wait is strictly an exceptional teardown/restart safety path. It is not part of the normal frame loop and does not replace the asynchronous coroutine path.

After the wait, teardown verifies the request is terminal before clearing tracked ownership and releasing pooled backing memory/resources.

If request-state inspection or `WaitForCompletion()` throws, cleanup is conservative: it does not dispose the TextureFramePool or release the body/direct-staging RenderTextures while an active request may still target their TextureFrame-owned memory. A restart is blocked instead of rebuilding on top of uncertain ownership, and a teardown diagnostic is emitted.

Normal camera switching still waits until `_readbackPending` and inference are clear before restart, so the new teardown wait should normally be a no-op for safe camera switches and ordinary Retry/restart flows. Its primary purpose is Play-stop/`OnDestroy` and abnormal cleanup.

## Running-path impact

Expected normal performance impact: none.

The safety patch does not change:
- DirectCPU readback source selection;
- flip staging transform;
- body resolution;
- readback timing boundaries;
- direct eligibility/fallback policy;
- immediate-launch behavior;
- one-readback / one-prepared-frame / one-inference bounds;
- Homuler readback;
- canonical/calibration/retargeting/locomotion;
- camera orientation semantics;
- F12/Lab geometry.

`WaitForCompletion()` is never called each frame. It is only considered during cleanup when a tracked direct request still exists.

## Diagnostics

Existing F7/Status remains unchanged by this safety patch: capture/render/req/res/cb rates, RB/build/detect/~F->R, pose age, prepared->launch delay/frame delta/origin/fast rate, noFresh/int/RB/inf/pool waits, readback failures/timeouts, replacements, active readback path, DirectCPU stage, H/V state, direct submissions/s, direct failures/s and fallback reason.

One exceptional cleanup log may appear if teardown actually has to wait for a DirectCPU request. There is no per-frame Console spam.

## Focused deterministic tests

Existing scheduler, immediate-launch, body-resolution, DirectCPU path-selection and flip-transform tests remain.

Additional pure teardown-policy tests cover:
- no active DirectCPU request -> cleanup does not require wait;
- tracked valid + not done -> cleanup policy says wait;
- tracked valid + done -> no blocking wait required and ownership may clear;
- timeout state retains ownership while request is not done and only becomes clearable after terminal completion.

No brittle graphics-device-dependent automated test was added solely to call a real GPU readback.

Unity compile/Test Runner/runtime status must be stated explicitly by the Builder report; static inspection is not a Unity PASS.

## Next USER validation after this safety checkpoint

Do **not** start with the performance A/B. First run a teardown smoke on HP TrueVision.

Common settings:
- camera request `640x480 @ 30`;
- target inference `30`;
- body downscale ON;
- body long edge `320`;
- Immediate Launch After Readback ON;
- Direct Body CPU Readback ON;
- `C`, then `K`.

Confirm F7 actually says `Readback: DirectCPU stage=None|H|V|HV`, not fallback. Then repeatedly stop and restart Play Mode at arbitrary moments while DirectCPU is active. Also test one live Direct toggle and one safe camera switch. Verify no NativeArray/AsyncGPUReadback/Texture destruction errors, no crash/hang, and normal orientation/alignment remains intact.

Only after that smoke passes, run the A/B:

**Run A — accepted baseline**
- Direct Body CPU Readback OFF;
- confirm `Readback: Homuler`.

**Run B — candidate**
- change only Direct Body CPU Readback ON;
- confirm `Readback: DirectCPU stage=H`, `V`, `HV` or `None`;
- record `H=0/1 V=0/1`;
- if it reports `DirectFallback`, do not benchmark and report the exact reason.

Use the same OBS flow: Play Mode -> settle/C/K -> start OBS -> wait 5–10 seconds -> comparable arm/torso motion -> stop OBS while Play Mode remains running.

Record capture FPS, render FPS, req/s, res/s, cb/s, RB, build, detect, Prep->launch, `~F->R`, pose age, wait rates, replacement rate, DirectCPU rate, direct failures, stage mode and subjective responsiveness. Verify F1/F2/F4 alignment, upright skeleton/avatar, no new mirror inversion, full-resolution Lab preview unchanged and Console clean.

Accepted baseline remains approximately RB `56.7 ms`, detect `56.7 ms`, Prep->launch `0.4 ms`, `~F->R 117.7 ms`, results `11.7/s`.

DirectCPU remains **IMPLEMENTED / NOT USER ACCEPTED** until both the teardown smoke and a real DirectCPU performance A/B pass.
