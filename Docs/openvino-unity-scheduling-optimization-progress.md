# Golden Needle — OpenVINO Unity Scheduling Optimization Progress

This is the rolling resume point for the post-U4 OpenVINO scheduling optimization approved by the USER on 2026-09-14.

A replacement Web Builder must read this file first, verify the current remote `engine/pose-tracking-spike` HEAD, inspect existing work, and continue from the first unfinished action. Checkpoints are recovery markers, not stop-and-wait approval gates.

## Authorization / current status

- Branch: `engine/pose-tracking-spike`.
- Exact scheduling-optimization starting SHA: `b38ab0df09aad452af0c49b1c56613b6d7dffe8d`.
- USER explicitly approved this focused optimization.
- Gate A: PASS.
- Gate B: PASS WITH NOTES.
- U1/U2/U3 Unity integration implementation: COMPLETE.
- U4 live Unity correctness: PASS.
- Prior U4 performance interpretation: OPENVINO COMPUTE ADVANTAGE PROVEN, END-TO-END GAIN MASKED BY SCHEDULING.
- Scheduling optimization implementation: **COMPLETE FOR BUILDER/PRE-USER VALIDATION**.
- Scheduling optimization USER runtime A/B: **REQUIRED NEXT; NOT YET PERFORMED**.
- Production/default backend decision: NOT YET MADE.
- Phase 5A remains NOT USER ACCEPTED.
- Phase 6 remains NOT STARTED.
- No merge to `main` is authorized or performed.

## USER evidence that triggered this optimization

The USER performed clean same-machine Unity A/B runs without OBS after the native/package/managed integration had passed.

Stock MediaPipe/TFLite CPU screenshot:
- camera: ~30.3 FPS;
- render: ~36.5 FPS;
- inference requests/results: ~13.7 / 13.7 per second;
- latest pose age: ~49 ms;
- last inference/result time: ~52.8 ms;
- pipeline readback/build/inference/frame-to-result approximately `47.1 / 0.0 / 44.4 / 97.7 ms`;
- prepared-to-launch: ~0.1 ms;
- frame delta: 0;
- launch origin: `RB`;
- immediate/fast launches: ~13.7/s;
- DirectCPU readback active, stage V.

OpenVINO CPU FP32 screenshot before this optimization:
- camera: ~29.5 FPS;
- render: ~39.0 FPS;
- inference requests/results: ~12.8 / 12.8 per second;
- latest pose age: ~90 ms at the captured instant;
- last inference/result time: ~37.2 ms;
- pipeline readback/build/inference/frame-to-result approximately `46.6 / 0.3 / 31.9 / 98.5 ms`;
- prepared-to-launch: ~19.9 ms;
- frame delta: 1;
- launch origin: `Update`;
- immediate/fast launches: ~7.9/s;
- DirectCPU readback active, stage V;
- OpenVINO internal timing: managed copy ~0.3 ms, graph ~31.7 ms, detector ~0.0 ms, landmark ~28.4 ms, bridge ~0.4 ms;
- detector reused tracking on the captured frame.

The USER additionally reported that OpenVINO subjectively felt smoother when controlling the avatar in F12.

Accepted interpretation:
- OpenVINO materially reduces neural/graph-side time versus stock TFLite in live Unity.
- The gain was being lost when readback completed while OpenVINO inference was still active: one prepared frame then waited for a later Unity `Update` after the worker task finished.
- The symptom was OpenVINO `Prep->launch ~19.9 ms`, `df=1`, `origin=Update` despite faster inference.

## Architecture audit result

The audit was performed against the actual provider/runtime code before implementation.

Important findings:
- The old pending representation was a Unity-owned `TextureFrame`; it is not safe to hand that Unity-owned lifecycle to a background continuation.
- `OpenVinoPoseRuntime.ProcessRgba(byte[])` is intentionally Unity-free and safe to call from a worker.
- The safe seam is therefore **after completed readback bytes are available on the main thread but before native OpenVINO processing**.

Chosen design:

### Persistent OpenVINO worker + two-buffer managed latest-frame mailbox

- Main thread performs the existing camera/readback work.
- For OpenVINO only, completed RGBA readback bytes are copied into a managed mailbox buffer and the `TextureFrame` is released immediately.
- One persistent worker owns OpenVINO native processing.
- The mailbox owns exactly two reusable storage slots:
  - at most one slot may be active/native-owned;
  - the other slot may be the single replaceable newest pending frame.
- If a newer readback completes while a pending frame already exists, it overwrites/reuses that pending slot.
- No third/history/FIFO/catch-up slot exists.
- After active OpenVINO inference completes, the same worker may claim the newest pending frame immediately when the configured cadence interval allows, without waiting for a normal Unity `Update`.
- If target-inference cadence has not elapsed, the worker waits only for the remaining cadence time while the single pending slot remains replaceable by newer frames.
- Stock TFLite remains on the existing `TextureFrame -> PoseLandmarker.DetectAsync` path.

This design was selected instead of launching Unity-owned TextureFrames from the worker.

## Non-negotiable invariants preserved

- Stock MediaPipe/TFLite CPU remains fully functional, selectable, and the serialized/default-safe backend.
- 320x240 body inference target remains unchanged.
- At most one active readback.
- At most one active OpenVINO inference.
- At most one replaceable pending/latest frame.
- No `Queue<>`, `ConcurrentQueue<>`, history, replay, FIFO backlog, or catch-up processing.
- Latest useful frame wins.
- No two OpenVINO inferences run concurrently.
- Explicit OpenVINO selection still fails closed rather than silently falling back.
- DirectCPU/fallback behavior is preserved.
- Camera-switch/restart/teardown wait for owned worker/native state safely.
- No canonical/stabilization/confidence/calibration/retarget/locomotion/presentation semantic changes.
- No scene YAML changes.
- No model/native OpenVINO implementation changes.
- No native rebuild was required or triggered by this managed scheduling optimization.

## Implementation checkpoints

### Generator/recovery scaffold

`b7efe151cb2e8d3f5d8db70b8fbdf0015d1689ea`
- `build: add fail-closed OpenVINO scheduling transform`
- Added `Tools/OpenVinoUnityPosePlugin/scripts/apply_openvino_scheduling_optimization.py`.

`a1b9047a8b418508b01475431e7902844cef7664`
- `ci: apply and audit OpenVINO scheduling optimization`
- Added `.github/workflows/openvino-scheduling-optimization.yml`.

Initial dry-run:
- run `34820708627`, job `103901420369`;
- failed safely before provider code was committed because the transform's reset-pattern assertion saw two intentional matching reset sites;
- no runtime/provider implementation from that failed run landed.

`f80bc9a472367b849fd51361be52155dcc2b6dda`
- `ci: harden scheduling transform preflight`
- corrected the fail-closed generator preflight/boundary handling.

Successful implementation workflow:
- run `34820796031`, job `103901690234`;
- `OPENVINO_SCHEDULING_TRANSFORM=PASS`;
- `OPENVINO_SCHEDULING_STATIC_AUDIT=PASS`;
- changed-file scope and `git diff --check` passed.

### Main scheduling implementation

`9937bb37d35ecfc50fd44e2dca0c6aa3c55ba358`
- `perf: continue OpenVINO from latest-frame mailbox`

Main provider changes:
- added `PreparedInferenceLaunchOrigin.OpenVinoWorkerContinuation` (`OVW`);
- made `InferenceLaunchScheduler` thread-safe and added remaining-cadence calculation;
- made continuation timing window thread-safe;
- added `OpenVinoLatestFrameMailbox` with exactly two reusable frame slots;
- added monotonic OpenVINO timestamp policy;
- replaced per-frame OpenVINO task launch with one persistent `OpenVinoWorkerLoop`;
- OpenVINO readback completion now publishes managed RGBA into the single replaceable mailbox slot and releases the Unity `TextureFrame` immediately;
- worker claims pending frame only when no inference is active and cadence allows;
- active slot cannot be overwritten;
- stale coordinate-convention pending frames are discarded;
- coordinate/resource changes discard pending OpenVINO frame;
- camera-switch request pauses OpenVINO mailbox and discards pending frame;
- teardown stops intake, clears pending, wakes/waits worker, then disposes native runtime;
- worker failure stops intake and surfaces through existing main-thread failure handling;
- worker-visible request/launch counters use `Interlocked`;
- main-thread frame number is published through `Volatile` so worker telemetry does not call Unity `Time`.

### Post-landing session reset repair

Post-generation review found a telemetry/session reset defect: the first transform pass had removed a duplicate plain reset at the wrong reset site, leaving worker-owned request/continuation window counters without explicit reset in `ClearProviderSessionState()`.

`56770176349230df9cf9fc53898f0b875a1fd86e`
- added fail-closed session reset repair script.

`326919f6d840c14fbf14f1be4a4c8c55d38f384c`
- added repair/audit workflow.

Repair run:
- run `34821074087`, job `103902546121`;
- `OPENVINO_SCHEDULING_SESSION_RESET_REPAIR=PASS`;
- `OPENVINO_SCHEDULING_SESSION_RESET_STATIC_AUDIT=PASS`.

Generated repair commit:

`c2f60ff` (`fix: reset OpenVINO scheduling session counters`)
- resets request-window counter;
- resets immediate-launch counter;
- resets OpenVINO-worker-continuation counter;
- resets published main-thread frame index on provider session clear.

## Telemetry changes

Existing diagnostics remain visible. New scheduling evidence includes:

- launch origin `OVW` for worker continuation;
- `U/RB/OVW` continuation-origin counts;
- `OpenVinoWorkerContinuationsPerSecond` / `ovw=/s`;
- OpenVINO scheduling suffix:
  - `pending`;
  - `active`;
  - `ovw=/s`;
  - `buffers`;
- prepared-to-launch delay and frame delta remain available;
- continuation result-to-next-launch median/p95 and prepared-only variants remain available.

Expected bounded telemetry under fixed-resolution steady state:
- `pending` is 0 or 1;
- `active` is 0 or 1;
- buffer allocation count settles (normally two once both reusable slots have been used), rather than increasing every frame;
- `OVW` should appear when a newer frame was already pending at completion of the previous OpenVINO inference.

## Deterministic test additions

`Assets/GoldenNeedle/Tests/Editor/MediaPipeInferenceSchedulingTests.cs` now includes focused tests for:

- remaining cadence time for worker continuation;
- pending latest-frame replacement without storage growth;
- prohibition of taking a second frame while one mailbox frame is active;
- newest-frame-wins behavior;
- active-buffer ownership protection;
- stale coordinate-convention discard;
- stop/clear/reject behavior;
- strictly monotonic OpenVINO timestamps;
- `OVW` continuation timing origin.

Unity Editor tests themselves were not claimed as executed in hosted CI because a licensed Unity Editor runner is not configured here.

## Managed/source verification

The existing managed U3 validation was expanded so a real branch push executes it rather than relying on bot-generated pushes to recursively trigger Actions.

`4743142192de88368c966a1f7ece791308e28d91`
- `ci: validate bounded OpenVINO scheduling integration`

Run `34821249662`, job `103903108325`: **SUCCESS**
- `GNOVPOSE_MANAGED_ABI_SMOKE=PASS`;
- ABI `65537` / v1.1;
- 33 landmarks;
- native result layout 1280 bytes;
- `U3_PROVIDER_STATIC_INVARIANTS=PASS`;
- `OPENVINO_BOUNDED_SCHEDULING_STATIC_INVARIANTS=PASS`.

To execute actual scheduling helper code without pretending to run Unity, a focused managed scheduling smoke was added under:

`Tools/OpenVinoUnityPosePlugin/tests/ManagedSchedulingSmoke/`

It extracts the **actual** `InferenceLaunchScheduler`, `OpenVinoLatestFrameMailbox`, and `OpenVinoSchedulingPolicy` source blocks from `MediaPipePoseProvider.cs`, supplies only a minimal `NativeArray<T>` stub, compiles them with .NET 8, and executes the ownership/cadence tests.

Relevant commits:
- `84783dbc7748b4602029bb54543c7670068e9333`
- `f546cc39531e4d4f9539480ca107eafb52c5a3cc`
- `689310fc023b46111ac68e817fcf678359a17b96`
- `ecb950ef43d636ab91474314c09f6c27092213a5`
- `87805c18e14505ab7bd760c61384313af855717b` (`ci: execute real managed scheduling helpers`).

Authoritative validation run:
- run `34821509749`, job `103903917499`: **SUCCESS**;
- `GNOVPOSE_MANAGED_ABI_SMOKE=PASS`;
- `MANAGED_SCHEDULING_SOURCE_EXTRACTED=.../GeneratedScheduling.cs`;
- `OPENVINO_MANAGED_SCHEDULING_SMOKE=PASS`;
- smoke terminal state: `buffers=2; pending=0; active=False`;
- `U3_PROVIDER_STATIC_INVARIANTS=PASS`;
- `OPENVINO_BOUNDED_SCHEDULING_STATIC_INVARIANTS=PASS`.

This verifies the real mailbox/scheduler helper implementation compiles and enforces the bounded ownership behavior outside Unity. It does not replace the required Unity runtime QA.

## Native/package rebuild decision

**No native OpenVINO rebuild was performed.**

Reason:
- no native C++ source changed;
- no ABI changed;
- no OpenVINO/MediaPipe/Homuler pin changed;
- no model changed;
- no native package dependency changed;
- the optimization is entirely in managed provider scheduling/ownership and diagnostics.

The previously proven native/package artifacts remain authoritative for native correctness. If generated ignored OpenVINO runtime files are absent on the USER machine, `prepare_unity.ps1` may be run again only to restage the already-proven package before runtime QA.

## USER runtime QA preparation

Dedicated instructions are now in:

`Docs/openvino-unity-scheduling-optimization-qa.md`

The next A/B must compare same-session:
- stock `MediaPipe Tflite Cpu`;
- optimized `Open Vino Cpu Fp32`;

with identical camera, 320x240 body input, DirectCPU/readback, target inference FPS and downstream motion settings.

Key proof targets:
- OpenVINO prepared-to-launch delay materially reduced from the previous ~19.9 ms symptom when a pending frame exists;
- frame delta more often 0 rather than the previous 1 symptom;
- `OVW` / `ovw=/s` non-zero for worker continuations;
- prepared-only result-to-next-launch timing materially reduced;
- request/result rate and/or pose freshness exposes more of the already-proven OpenVINO compute advantage;
- mailbox remains bounded (`pending<=1`, `active<=1`, buffer allocations stable at fixed resolution);
- no fast-motion, partial-body, camera-switch, retry/restart or teardown regression.

Cadence may still intentionally impose a small wait if the configured target inference interval has not elapsed. The optimization removes the avoidable normal-`Update` delay; it does not bypass target FPS.

## Current exact next action

**Builder-owned implementation and automated verification are complete. Stop here for genuine USER Windows/Unity runtime A/B QA.**

1. USER updates `engine/pose-tracking-spike` to the latest remote checkpoint.
2. If ignored generated OpenVINO package/model files are absent, close Unity and run `Tools/OpenVinoUnityPosePlugin/scripts/prepare_unity.ps1`, requiring `OPENVINO_UNITY_LOCAL_PREP=PASS`.
3. Follow `Docs/openvino-unity-scheduling-optimization-qa.md` exactly.
4. Return stock and optimized OpenVINO F7 screenshots plus observations/errors and camera-switch/restart/teardown result.
5. USER/Orchestrator classifies the optimization as PASS, PASS WITH NOTES, FAIL, or NO MATERIAL GAIN.
6. Do not make a production/default backend decision from pre-USER evidence alone.
7. Do not merge to `main` or start Phase 6.
