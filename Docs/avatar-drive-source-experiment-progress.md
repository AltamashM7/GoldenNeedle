# Golden Needle — Avatar Drive Source Experiment Progress

This is the rolling resume point for the latency/stability experiment that follows the accepted OpenVINO + WebCamCPU optimization work.

A replacement Web Builder must read this file first together with `Docs/worker-briefs/raw-vs-stabilized-avatar-drive-handoff.md`. These two files are the newest authority for this experiment. Do not reopen the accepted OpenVINO scheduling or WebCamCPU acquisition work unless new evidence directly requires it.

## Current authorization and status

- Branch: `engine/pose-tracking-spike`.
- Original experiment planning HEAD: `45e20cb7df3ea8acd4c6e2cdbce8450a87487971`.
- Exact remote HEAD at the start of the authorized implementation run: `ee0db6d2497177d9b2cdab8887a435879665a4fd`.
- OpenVINO Unity integration correctness: **PASS**.
- OpenVINO scheduling optimization: **USER ACCEPTED — PASS**.
- WebCamCPU/GetPixels32 R2 acquisition experiment: **USER ACCEPTED — PASS**.
- Raw-vs-stabilized avatar-drive A/B experiment: **EXPLICITLY USER AUTHORIZED on 2026-09-14**.
- Avatar-drive selector implementation: **COMPLETE — NON-HARDWARE VERIFICATION PASS**.
- Current gate: **USER visual/runtime A/B QA REQUIRED**.
- Phase 5A remains **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## Accepted R2 evidence

The USER tested the current best path on the same Windows/Unity hardware with phone-recorded external video so OBS did not consume CPU/GPU time:

```text
WebCamCPU/GetPixels32
+ OpenVINO CPU FP32
+ body input 320x240
+ full-body framing
```

Representative full-body runtime behavior from the USER evidence was approximately:

```text
Camera capture:       ~28.6-30.3 FPS
Fresh pose results:   ~26.7-29.3/s
Render:               ~30-37 FPS
CPU GetPixels32:      ~0.3 ms
CPU preparation:      ~3.6 ms
CPU acquisition total:~3.9 ms
OpenVINO processing:  commonly ~25-35 ms
Frame->result:        commonly ~33-62 ms
```

Earlier ExistingReadback evidence was around ~40-50+ ms for GPU readback and ~100 ms or worse frame->result under representative conditions. The USER also reported clearly lower perceived latency with WebCamCPU while motion replication remained satisfactory.

Interpretation:

- the old GPU readback bottleneck has been effectively removed for the accepted best path;
- with full-body camera delivery near 30 FPS, OpenVINO + WebCamCPU can produce close to one fresh pose result per camera frame;
- further inference/readback work is not the immediate next target;
- residual perceived lag is now small enough that downstream filtering can be visually observed.

## Experiment motivation

The Lab draws two canonical skeletons over the webcam:

- **yellow** = `MotionEngineRuntime.RawCanonicalFrame`;
- **cyan/blue** = `MotionEngineRuntime.StabilizedFrame`.

The USER can visibly see the cyan/blue stabilized skeleton trail the yellow raw canonical skeleton slightly during motion.

Current One Euro defaults remain the accepted Phase 3 values:

```text
minCutoff = 1.0
beta = 0.05
derivativeCutoff = 1.0
```

The separate HumanoidRetargeter presentation-smoothing toggle was previously tested by the USER and the difference was too small to distinguish reliably at the current fast upstream rate. The authorized experiment therefore compares the two existing canonical endpoints directly without tuning One Euro or presentation smoothing.

## Implemented architecture

The runtime now exposes:

```text
AvatarDrivePoseSource
  StabilizedCanonical   [serialized/default]
  RawCanonical          [experimental]

MotionEngineRuntime.AvatarDriveSource
MotionEngineRuntime.AvatarDriveFrame
MotionEngineRuntime.AvatarDriveSourceLabel
```

The effective data flow is:

```text
provider
  -> raw canonical -----------------------------+
  -> One Euro stabilization                     |
  -> stabilized canonical                       |
          |                                      |
          +-> calibration (always stabilized)   |
          +-> locomotion (always stabilized)     |
                                                 |
Avatar Drive Source selector --------------------+
          |
          +-> StabilizedCanonical [DEFAULT]
          +-> RawCanonical [EXPERIMENTAL]
          |
          -> rotation solver
          -> kinematic targets / IK
          -> HumanoidRetargeter source/torso mapping
          -> avatar
```

### Calibration invariant

`MotionEngineRuntime.Update()` still contains:

```text
_calibration.Update(_stabilizedFrame, _lastEvaluationTimeSeconds)
```

The selector is resolved only after stabilization and calibration update. Both raw and stabilized avatar-drive modes therefore use the same stabilized-derived calibration profile.

### Rotation / kinematic invariant

`MotionEngineRuntime.Update()` resolves one `avatarDriveFrame = AvatarDriveFrame`, and that same frame is passed to both:

```text
_rotationSolver.Solve(avatarDriveFrame, ...)
_kinematicTargetBuilder.Build(avatarDriveFrame, ...)
```

No mixed raw/stabilized limb solve was introduced.

### HumanoidRetargeter torso/source invariant

`HumanoidRetargeter.LateUpdate()` now gets its source mapping frame from:

```text
runtime.AvatarDriveFrame
```

Therefore torso/source-frame mapping uses the same selected canonical frame as rotation solving and kinematic-target generation.

### Locomotion invariant

`EmbodiedLocomotionController` was not modified. Root/support tracking, cadence, and heading continue to read `runtime.StabilizedFrame` directly in all avatar-drive modes.

### Debug / presentation invariants

- yellow raw canonical rendering remains `runtime.RawCanonicalFrame`;
- cyan stabilized canonical rendering remains `runtime.StabilizedFrame`;
- both skeletons continue to be generated/drawn simultaneously;
- F7 now reports the effective avatar source as `RawCanonical` or `StabilizedCanonical`;
- presentation-smoothing implementation and settings were not changed;
- One Euro settings and implementation were not changed.

## Implementation checkpoints

### Runtime implementation

Generated implementation commit:

`100c3dc927314a2107b62a7acdf46037dc8f6738`

Runtime files changed in that commit only:

- `Assets/GoldenNeedle/Core/Motion/Runtime/MotionEngineRuntime.cs`
- `Assets/GoldenNeedle/Core/Motion/Retargeting/HumanoidRetargeter.cs`
- `Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpikePresenter.cs`

### Fail-closed audit tooling

Audit/recovery tooling added:

- `Tools/AvatarDriveExperiment/apply_avatar_drive_source_experiment.py`
- `.github/workflows/avatar-drive-source-experiment.yml`

The current-head transformer is idempotent so it can verify an already-applied implementation without trying to duplicate the selector.

Verified pre-documentation checkpoint:

`d12e7ad1aa420338f1233cd32b055373b3a1c267`

## Verification performed

GitHub Actions workflow `Avatar drive source experiment` performed a fail-closed static/diff-scope audit.

Successful implementation run:

- run `34852740425` — **PASS**.

Successful current-HEAD/idempotent audit:

- run `34852975877` — **PASS**.

Verified conditions:

- `StabilizedCanonical` is the serialized default;
- `RawCanonical` resolves to the raw frame and stabilized mode resolves to the stabilized frame;
- calibration remains hard-wired to `_stabilizedFrame`;
- rotation solver and kinematic target builder consume the same selected `avatarDriveFrame`;
- HumanoidRetargeter source/torso mapping consumes `runtime.AvatarDriveFrame`;
- locomotion retains its direct `runtime.StabilizedFrame` consumers and has no raw/avatar-drive dependency;
- raw and stabilized debug skeleton references remain intact;
- presentation smoothing surface remains unchanged;
- One Euro defaults remain `1.0 / 0.05 / 1.0`;
- no queue/history/replay/prediction was added;
- generated runtime diff is limited to the three intended runtime/debug files;
- branch comparison from `ee0db6d...` to `d12e7ad...` shows no scene, `Packages`, `ProjectSettings`, OpenVINO, WebCamCPU, native plugin, model, locomotion, stabilizer, or IK-math edits.

A transient second CI run failed only because the original one-shot transformer correctly refused to reapply to an already-modified tree. No runtime file changed in that failure. The transformer was then made explicitly idempotent and the current-HEAD audit passed.

No full Unity Editor hardware/runtime test was run remotely. The remaining behavior question is intentionally visual and must be judged on the USER's camera/avatar setup.

## USER A/B QA

Use identical common settings in both runs:

```text
Inference Backend = OpenVINO CPU FP32
Frame Acquisition = WebCamCPU/GetPixels32
Body input = 320x240
full-body framing where practical (~30 FPS capture)
same camera / lighting / calibration / retarget settings
```

Keep Presentation Smoothing identical between runs. Prefer **OFF** for the primary source-isolation comparison if practical.

### Run A — baseline

```text
MotionEngineRuntime
  Avatar Drive Pose Source = StabilizedCanonical
```

Confirm F7 reports:

```text
Avatar source: StabilizedCanonical
```

### Run B — experimental

```text
MotionEngineRuntime
  Avatar Drive Pose Source = RawCanonical
```

Confirm F7 reports:

```text
Avatar source: RawCanonical
```

Compare:

- perceived motion-to-avatar latency;
- fast arms/reaches;
- torso response;
- legs/knees when visible;
- idle jitter / micro-jitter;
- snapping or IK instability;
- partial-body loss/recovery;
- orientation and left/right parity.

The yellow raw and cyan stabilized skeletons should still both be visible according to the existing debug toggles. The selector should affect only the avatar drive, not which debug skeletons exist.

If raw mode is clearly faster but slightly noisy, an optional second comparison may repeat both source modes with Presentation Smoothing ON. Do not tune One Euro yet.

Phone-recorded external video is preferred if recording helps; avoid OBS unless necessary.

## Risks / interpretation

- Static verification proves routing/invariants, not subjective stability or latency on the USER's avatar.
- Raw canonical data intentionally bypasses One Euro positional smoothing for avatar pose solving, so increased micro-jitter or sharper loss/recovery behavior is possible and is exactly what the A/B must evaluate.
- Calibration and locomotion remain protected by stabilized canonical input, so this test does not evaluate raw-drive suitability for those subsystems.
- Do not accept RawCanonical solely because it feels faster; it must also remain acceptably stable.

## CONTINUE FROM HERE

**STATUS: IMPLEMENTATION COMPLETE; USER VISUAL/RUNTIME A/B QA IS THE NEXT GATE.**

Current verified code checkpoint before this documentation commit:

`d12e7ad1aa420338f1233cd32b055373b3a1c267`

Next action:

1. USER pulls the latest `engine/pose-tracking-spike` branch and lets Unity compile.
2. Run the stabilized baseline and raw experiment exactly as described above.
3. Capture F7 source label and report qualitative responsiveness/stability observations, plus screenshots/video if useful.
4. On the next Builder/Orchestrator continuation, read this file first and evaluate the USER A/B evidence.
5. Do not tune One Euro, alter presentation smoothing logic, reopen OpenVINO/WebCamCPU, merge to `main`, or start Phase 6 unless separately authorized.
