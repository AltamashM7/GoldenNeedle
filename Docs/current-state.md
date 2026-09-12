# Current state

## Authoritative checkpoint — motion responsiveness hardening

Working branch: `engine/pose-tracking-spike`.

Starting handoff checkpoint for the responsiveness work: `f895fcd4941061950723f914e7cd815dc15c65f1` — `docs: finalize phase 5a orchestrator handoff`.

Motion-responsiveness implementation checkpoint: `4fc8bb2f1e5e8e61235d7b125e743698ca84087e` — `perf: overlap latest-frame preparation with pose inference`.

Body-inference implementation checkpoint: `e56bfbb1be7333c26677ab36c977010c3e843029` — `perf: add lower-resolution body pose inference path`.

Normal-Inspector body-inference control checkpoint: `92cc144be975611cd5a3c26c94441eb36c860e0e` — `fix: expose and validate body inference controls`.

Status:
- Phase 4: **USER ACCEPTED — PASS**.
- Phase 5A: **IMPLEMENTED / NOT USER ACCEPTED**. Locomotion acceptance QA remains paused while camera-to-avatar responsiveness is hardened.
- Phase 6: **NOT STARTED**.
- Do not merge to `main` without explicit USER approval.

This file is the concise authoritative runtime snapshot. Older investigation narrative remains available in Git history and the other Motion Engine documents.

## Runtime evidence that motivated responsiveness work

The USER tested both the HP TrueVision laptop webcam and DroidCam Video over USB. Both feeds work in Golden Needle. Earlier OBS-recorded tests showed camera/render cadence below ideal, inference requests/results around 3–5/s, DetectAsync-to-callback roughly 56–94 ms, and pose age reaching hundreds of milliseconds. Presentation smoothing OFF produced no noticeable response improvement, so responsiveness work remains focused on the camera/readback/inference path rather than hiding delay with smoothing.

The temporary upside-down Neko observation remained transient. The `QuaternionToEuler` warning later became reproducible on the active URP camera-culling path and was addressed at `b64a3115401689b94cf5f86d691fc5e6c763f510` by normalizing the Lab camera Transform quaternion before enabling the Camera. That specific camera corrective checkpoint subsequently received USER runtime QA and all four checks passed:
1. the `QuaternionToEuler` warning stayed gone;
2. `No cameras rendering` stayed gone;
3. Lab webcam/overlays remained correct;
4. F12 Game -> Lab switching worked and did not restart the warning.

That acceptance is specific to the b64a311 camera correction. It does not imply overall responsiveness acceptance or Phase 5A acceptance.

## Bounded latest-frame pipeline

The earlier provider serialized camera preparation behind MediaPipe inference. The correction at `4fc8bb2...` separated these stages while preserving strict bounds:

```text
at most one AsyncGPUReadback in progress
+
at most one replaceable prepared TextureFrame
+
at most one MediaPipe inference outstanding
```

Readback preparation may overlap the active MediaPipe inference. A completed readback occupies one prepared slot; a newer completed readback replaces/releases an older prepared frame rather than creating a queue. There is no historical-frame queue, inference backlog, catch-up loop, delayed pose replay, or pose interpolation buffer.

The 30 FPS target remains a requested maximum cadence, not a result-rate guarantee. Actual throughput remains limited by camera delivery, Unity/render cadence, readback/CPU preparation, MediaPipe CPU time, and USER hardware load.

## Body-pose-only inference resolution

The provider has a reversible body-pose-only downscale path. The original full-resolution `WebCamTexture` remains authoritative for `CameraTexture`, Lab presentation, and future detailed hand/finger work. The current default is a `320`-pixel body-input long edge, producing `320x240` from a `640x480` camera source. Disabling downscale restores exact native source dimensions.

The normal custom `MediaPipePoseProvider` Inspector exposes **Body Pose Inference** controls. `Enable Body Inference Downscale` can be toggled in Play Mode; `Body Inference Long Edge` uses the current USER-facing range `160..640`. The long-edge value is retained but greyed while downscale is off. The Runtime Camera section reports actual active body-input dimensions and `scaled/native` state from runtime resources.

The `TextureFramePool` matches the selected body-inference dimensions. A persistent inference `RenderTexture` is used only when dimensions differ from the actual camera size. Full-resolution webcam/Lab output is not downscaled.

The latest USER laptop A/B at the same `640x480 @ 30` camera request and target inference `30` was approximately:

### Native body input — 640x480
- Capture: ~29.7 FPS
- Render: ~33.0 FPS
- Inference requests: ~10.85/s
- Pose results: ~10.85/s
- RB: ~63.3 ms
- Detect: ~58.7 ms
- ~F->R: ~144.9 ms
- RB-busy: ~19.5/s

### Scaled body input — 320x240
- Capture: ~29.8 FPS
- Render: ~34.3 FPS
- Inference requests: ~11.85/s
- Pose results: ~11.75/s
- RB: ~58.3 ms
- Detect: ~55.2 ms
- ~F->R: ~137.5 ms
- RB-busy: ~21.2/s

Conclusion: keep `320x240` as the body-pose input for current responsiveness work. It is modestly beneficial and showed no obvious body-tracking collapse in the supplied USER recording, but it is not the main remaining latency solution. Do not chase `160x120` in the current task.

## Immediate inference launch after readback experiment

A new reversible scheduling experiment is implemented after the `92cc144...` baseline and is **IMPLEMENTED / NOT USER ACCEPTED**.

At the baseline, successful `CapturePreparedFrameAsync` completion publishes the newest prepared `TextureFrame`, clears `_readbackPending`, and returns. The prepared frame is normally consumed only by a later ordinary `Update()` call to `TryLaunchPreparedInference(...)`. That creates a real prepared-publication -> later-Update -> DetectAsync boundary, potentially adding roughly one render-frame of latency even when inference is already free and the target interval has elapsed.

The experiment adds `Immediate Launch After Readback`, default ON. After successful readback publication, the same coroutine continuation may immediately reuse the single existing prepared-frame launch path if all guards are satisfied. If any guard fails, no wait/spin occurs: the frame stays prepared exactly as before and the ordinary Update path remains the fallback.

Immediate launch requires:
- experiment enabled;
- provider still `Ready`;
- provider not shutting down;
- no camera switch pending;
- Pose Landmarker still available;
- prepared frame still present;
- prepared coordinate convention still current;
- active body-inference resources still match current serialized intent;
- no MediaPipe inference outstanding;
- target inference interval elapsed.

The fast-path eligibility probe is side-effect-free and does not increment ordinary wait counters. The actual `BuildCPUImage` / `TextureFrame.Release` / inferenceOutstanding / timestamp / `DetectAsync` / scheduler / request-metric logic remains single-authority code shared by Update and readback-continuation launches.

Camera-switch priority is preserved: if `_cameraSwitchPending` is true, a completed readback may publish a prepared frame but cannot start another old-source inference. Cleanup on the authoritative switch path releases that prepared frame.

Live body-input changes are also protected. If downscale enable/long-edge intent changes while readback is in flight, the continuation does not immediate-launch the obsolete prepared frame. The next ordinary Update owns stale prepared-frame release and resource rebuild through `EnsureBodyInferenceResources()`.

Coordinate-convention snapshots remain authoritative. Readback work whose convention no longer matches the provider is discarded before publication, and the immediate path additionally requires the prepared convention to equal the current convention.

TextureFrame ownership remains bounded and singular: a frame belongs to active readback, the one replaceable prepared slot, the BuildCPUImage/inference handoff, or the pool after Release—never two at once.

## F7 observability

Existing F7 diagnostics remain, including camera/source resolution, capture/render FPS, request/result rates, pose age, RB/build/detect/~F->R, result callback rate, wait rates, readback fail/timeout rates, and prepared-frame replacement rate.

The immediate-launch experiment adds:
- `Prep->launch`: time from prepared-frame publication to the accepted inference request;
- `Δf`: main-thread `Time.frameCount` delta between publication and accepted launch;
- launch origin: `RB` for readback continuation, `Update` for ordinary Update, `-` before any accepted request;
- `fast`: accepted immediate launches per second.

The new fast-path probe does not inflate wait counters. `RB` retains its existing meaning: observed `ReadTextureAsync` request -> completion duration, not pure GPU hardware time.

## Camera / orientation foundation preserved

External camera support remains one Unity `WebCamTexture` / `WebCamDevice` provider path. `preferredCameraName`, normal Inspector selection, `V` camera cycling, safe hot-switch behavior, Auto/0/90/180/270 orientation, presentation-only display mirror, and the no-front-facing-auto-H-mirror rule remain unchanged. Production still uses `shouldFlipHorizontally=false` for front-facing metadata alone.

Physical camera changes still invalidate the source/session convention. After changing physical cameras, run `C` and then `K` before locomotion QA.

Scene defaults remain `640x480 @ 30` capture request and target inference `30 FPS`.

## Lab presentation

The fitted whole-frame Lab preview remains unchanged: webcam texture and F1/F2/F4 overlays use the same `LabCameraPresentationGeometry` with letterbox/pillarbox rather than crop for rotated portrait feeds.

The clear-only persistent Lab Camera correction remains USER-runtime accepted as described above. F12 restores the same third-person Game View behavior and does not alter the webcam/inference pipeline.

## Motion Engine behavior preserved

No canonical coordinate semantics, calibration math, signed-axis mapping, Phase 4 torso/IK/retargeting, Neko Humanoid binding, presentation-smoothing behavior, Phase 5A support model/cadence/fusion/heading/recenter logic, physical scales `0.9 / 1.5`, root-Y/root-rotation exclusion, or fitted Lab geometry is redesigned by this experiment.

Pose Landmarker Lite remains CPU, one pose, segmentation OFF.

## Verification state and next action

Focused deterministic scheduling tests cover immediate-launch eligibility and blocking by camera-switch pending, active inference, target interval, stale body-resource configuration, coordinate-convention mismatch, plus experiment-OFF fallback to the ordinary Update launch policy. Existing scheduler/body-resolution tests remain.

Unity compilation and Unity Test Runner have **not** been executed by this Web Builder environment. Do not convert source/static checks into a Unity PASS.

Next USER QA isolates only this scheduling experiment on HP TrueVision:
1. Camera request `640x480 @ 30`.
2. Target inference `30`.
3. Body inference ON at long edge `320` so both runs use `320x240 scaled`.
4. Run `C`, then `K`, before the motion comparison.
5. **Run A — baseline:** `Immediate Launch After Readback = OFF`.
6. **Run B — candidate:** `Immediate Launch After Readback = ON`.
7. Use the same recording method and similar rapid arm/torso motion.
8. Record Capture FPS, Render FPS, req/s, res/s, cb/s, RB, build, detect, Prep->launch, ~F->R, pose age, wait rates, replacement rate, fast-launch rate/origin, and subjective responsiveness.

Do not repeat native-vs-scaled for this experiment. RB itself may remain unchanged; the target is to remove the prepared->later-Update delay and materially improve ~F->R without regressions or ownership/switch/configuration errors.
