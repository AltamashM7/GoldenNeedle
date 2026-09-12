# Golden Needle — Orchestrator handoff

## Governance

Repository: `AltamashM7/GoldenNeedle`

Working branch: `engine/pose-tracking-spike`

Starting checkpoint for the immediate-launch experiment: `92cc144be975611cd5a3c26c94441eb36c860e0e` — `fix: expose and validate body inference controls`.

Relevant prior checkpoints:
- `4fc8bb2f1e5e8e61235d7b125e743698ca84087e` — overlap latest-frame preparation with pose inference;
- `b64a3115401689b94cf5f86d691fc5e6c763f510` — Lab camera normalization / clear-only camera correction;
- `e56bfbb1be7333c26677ab36c977010c3e843029` — lower-resolution body-pose inference path;
- `92cc144be975611cd5a3c26c94441eb36c860e0e` — expose/validate body-inference controls.

Status:
- Phase 4: **USER ACCEPTED — PASS**. Preserve behavior.
- b64a311 camera corrective checkpoint: **USER-runtime accepted** for its four targeted checks.
- Phase 5A: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## Accepted b64a311 camera correction

The temporary upside-down Neko observation remained transient, but the `QuaternionToEuler` warning later became reproducible on the active URP camera-culling path. `b64a311...` normalized the controlled Lab Camera quaternion before enabling the Camera while preserving clear-only Lab rendering and F12 behavior.

The USER subsequently runtime-verified all four targeted checks:
1. `QuaternionToEuler` warning stayed gone;
2. `No cameras rendering` stayed gone;
3. Lab webcam/overlays remained correct;
4. F12 Game -> Lab switching worked without restarting the warning.

Treat that specific correction as USER-runtime accepted. Do not confuse it with responsiveness acceptance or Phase 5A acceptance.

## Bounded latest-frame pipeline

Golden Needle keeps these hard bounds:

```text
<= 1 active AsyncGPUReadback
<= 1 replaceable prepared TextureFrame
<= 1 outstanding MediaPipe DetectAsync
```

Readback may overlap active inference. Newer completed readbacks replace/release older prepared work. There is no camera-frame history, inference backlog, catch-up loop, delayed pose replay, or interpolation buffer. The newest useful frame wins.

The full-resolution `WebCamTexture` remains authoritative for `CameraTexture`, Lab display, and future high-detail hand/finger work.

## Body-pose inference downscale state

The current body-pose-only default is `320x240` from a `640x480` source. The normal `MediaPipePoseProvider` Inspector exposes the downscale toggle and a `160..640` long-edge slider. Runtime Camera/F7 report actual active body-input dimensions and `scaled/native` state.

Latest USER laptop A/B, approximately:

### Native 640x480 body input
- Capture ~29.7 FPS
- Render ~33.0 FPS
- Inference req ~10.85/s
- Pose results ~10.85/s
- RB ~63.3 ms
- Detect ~58.7 ms
- ~F->R ~144.9 ms
- RB-busy ~19.5/s

### Scaled 320x240 body input
- Capture ~29.8 FPS
- Render ~34.3 FPS
- Inference req ~11.85/s
- Pose results ~11.75/s
- RB ~58.3 ms
- Detect ~55.2 ms
- ~F->R ~137.5 ms
- RB-busy ~21.2/s

Conclusion: keep `320x240` for current body-pose responsiveness work. The gain is modest but positive; do not pursue `160x120` in the current experiment.

## Source-proven remaining structural delay

At the `92cc144...` baseline, `MediaPipePoseProvider.Update()` performs:

```text
UpdateMetrics
-> camera-switch priority
-> EnsureBodyInferenceResources
-> observe didUpdateThisFrame
-> UpdateInputTransform
-> TryLaunchPreparedInference
-> TryStartLatestReadback
```

`TryStartLatestReadback()` starts `CapturePreparedFrameAsync(...)`. After the readback completes, that coroutine validates success/convention/shutdown, replaces any older prepared frame, publishes `_preparedTextureFrame` plus its observation/convention metadata, clears `_readbackPending`, and returns.

Before this experiment, the newly prepared frame is not launched from that continuation. Even when inference is already free and the target interval has elapsed, the frame waits for a later ordinary `Update()` call to `TryLaunchPreparedInference(...)`.

That prepared-publication -> later-Update -> DetectAsync boundary is the exact hypothesis under test.

## Immediate inference launch experiment

The experiment is **IMPLEMENTED / NOT USER ACCEPTED**.

A serialized normal-Inspector option is added:

`Immediate Launch After Readback`

Default: ON.

Tooltip meaning: attempt body-pose inference immediately when readback finishes; if unsafe/ineligible, leave the frame prepared for the normal Update path; create no extra queue or concurrent inference.

On successful readback completion, ordering is:

```text
validate readback / source convention
-> replace older prepared slot if needed
-> publish prepared frame metadata
-> record prepared publication time + main-thread frame count
-> clear _readbackPending
-> side-effect-free immediate-launch eligibility probe
-> if eligible, reuse the same prepared-frame inference launch authority
-> otherwise return with prepared frame intact
```

The ordinary Update launch remains the fallback and retains its existing wait-counter behavior.

## Immediate-launch guards

Immediate launch requires all of:
- experiment enabled;
- provider status still `Ready`;
- not shutting down;
- no camera switch pending;
- Pose Landmarker available;
- prepared frame present;
- prepared coordinate-convention version equals current convention;
- body-inference resources match the current serialized downscale/long-edge intent and actual camera dimensions;
- no inference outstanding;
- target inference interval elapsed.

The fast-path probe never waits/spins and does not increment the ordinary Update wait counters.

### Camera-switch priority

`_cameraSwitchPending` is a mandatory blocker. A readback that completes after a switch request may publish/retain its prepared frame, but the continuation cannot start another old-camera inference. The next authoritative camera-switch handling path waits for active stages to become idle and cleanup releases the prepared work.

### Live body-input configuration changes

If the USER changes `Enable Body Inference Downscale` or `Body Inference Long Edge` while readback is in flight, the existing configured-resource snapshot no longer matches current serialized intent. The continuation therefore cannot fast-launch that frame. It leaves the frame prepared; ordinary Update/`EnsureBodyInferenceResources()` owns stale prepared-frame release and resource rebuild after active work drains.

No body-resource rebuild is performed inside the readback continuation.

### Coordinate convention

Existing convention snapshot behavior remains. Readback work whose captured convention no longer matches current provider convention is discarded before publication. The immediate path also requires the published prepared convention to remain current.

## Single inference-launch authority / ownership

The old prepared-frame launch method was factored so Update and readback-continuation origins reuse the same actual launch implementation for:
- coordinate-version validation;
- busy/interval gating;
- consuming the prepared slot;
- `BuildCPUImage()`;
- `TextureFrame.Release()`;
- `_inferenceOutstanding` transition;
- timestamps;
- `DetectAsync`;
- accepted-request scheduler state;
- request metrics;
- failure handling.

TextureFrame ownership remains single-owner. At any instant a frame is in exactly one state: active readback, the one prepared slot, BuildCPUImage/inference handoff, or pool after `Release()`.

## Experiment instrumentation / F7

Existing metrics remain: RB, build, detect, ~F->R, req/s, res/s, cb/s, pose age, wait rates, readback failures/timeouts, and replacement rate.

New low-overhead diagnostics:
- last prepared -> accepted inference launch delay;
- main-thread frame-count delta from prepared publication to accepted launch;
- last accepted launch origin (`Update`, `RB`, or none yet);
- accepted immediate launches per second.

F7 Status adds a compact line such as:

```text
Prep->launch: 1.2 ms Δf=0 origin=RB fast=11.4/s
```

With the experiment OFF, accepted launches should originate from `Update` and `fast` should remain `0.0/s`.

`RB` keeps its previous definition: observed `ReadTextureAsync` request -> completion, including Unity callback/upload/scheduling effects. It is not labelled pure GPU time. This experiment optimizes only the delay after that timer ends, so unchanged RB is expected and is not failure.

## Frozen behavior

Do not reopen without new evidence:
- canonical coordinate semantics;
- modular calibration;
- signed canonical-to-avatar mapping;
- Phase 4 Humanoid torso/IK/retargeting and Neko binding;
- Phase 5A support model, cadence, heading, fusion, recenter and physical scales `0.9 / 1.5`;
- root Y / root rotation exclusion;
- render-rate presentation smoothing;
- `preferredCameraName`, camera dropdown, `V` switching and safe pending switch;
- Auto/0/90/180/270 orientation;
- display mirror presentation-only semantics;
- front-facing metadata does not imply H inference mirror (`shouldFlipHorizontally=false`);
- fitted Lab webcam/F1/F2/F4 geometry;
- F12 Lab/Game behavior;
- clear-only Lab Camera and b64a311 quaternion normalization;
- full-resolution `CameraTexture`;
- camera request default `640x480 @ 30`;
- target inference default `30 FPS`;
- Pose Landmarker Lite, CPU, one pose, segmentation OFF.

The Homuler/MediaPipe package source is not modified by this experiment.

## Verification caveat

Focused deterministic policy tests are present for:
- immediate launch allowed with all conditions valid;
- blocked by camera-switch pending;
- blocked by outstanding inference;
- blocked by target interval;
- blocked by body-resource configuration mismatch;
- blocked by coordinate-convention mismatch;
- experiment OFF while ordinary Update launch policy remains available.

Existing scheduler and body-resolution tests remain.

This Web Builder environment did **not** run Unity compilation or Unity Test Runner. USER-side compile/runtime evidence is required before treating the experiment as a runtime pass.

## Next USER QA — isolate only immediate scheduling

Use HP TrueVision first.

Fixed for both runs:
- Camera request: `640x480 @ 30`;
- Target inference: `30`;
- Body inference downscale: ON;
- Body long edge: `320` (`320x240 scaled` actual body input expected);
- run `C`, then `K`, before comparing motion;
- same recording method and similar rapid arm/torso motion.

### Run A — baseline
`Immediate Launch After Readback = OFF`

### Run B — candidate
`Immediate Launch After Readback = ON`

Record:
- Capture FPS
- Render FPS
- req/s
- res/s
- cb/s
- RB
- build
- detect
- Prep->launch
- ~F->R
- pose age
- wait rates
- replacement rate
- fast-launch rate/origin
- subjective responsiveness

Do **not** compare native-vs-scaled again for this experiment.

A strong result is a prepared->launch delay reduction by roughly one render-frame boundary on eligible launches and a material ~F->R improvement without throughput, ownership, camera-switch, body-config, orientation, or stability regressions. RB itself does not need to fall.
