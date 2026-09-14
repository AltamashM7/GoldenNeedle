# Golden Needle — Avatar Drive Source Experiment Progress

This is the rolling resume point for the latency/stability experiment that follows the accepted OpenVINO + WebCamCPU optimization work.

A replacement Web Builder must read this file first together with `Docs/worker-briefs/raw-vs-stabilized-avatar-drive-handoff.md`. These two files are the newest authority for this experiment. Do not reopen the accepted OpenVINO scheduling or WebCamCPU acquisition work unless new evidence directly requires it.

## Current authorization and status

- Branch: `engine/pose-tracking-spike`.
- Starting remote HEAD for this experiment: `45e20cb7df3ea8acd4c6e2cdbce8450a87487971`.
- OpenVINO Unity integration correctness: **PASS**.
- OpenVINO scheduling optimization: **USER ACCEPTED — PASS**.
- WebCamCPU/GetPixels32 R2 acquisition experiment: **USER ACCEPTED — PASS**.
- Raw-vs-stabilized avatar-drive A/B experiment: **EXPLICITLY USER AUTHORIZED on 2026-09-14**.
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

## New USER observation that motivates this experiment

The Lab draws two canonical skeletons over the webcam:

- **yellow** = `MotionEngineRuntime.RawCanonicalFrame`;
- **cyan/blue** = `MotionEngineRuntime.StabilizedFrame`.

The USER can visibly see the cyan/blue stabilized skeleton trail the yellow raw canonical skeleton slightly during motion.

Repository inspection confirms the current production pipeline is:

```text
provider result
  -> raw canonical
  -> CanonicalPoseStabilizer / One Euro
  -> stabilized canonical
  -> calibration
  -> rotation solver
  -> kinematic target builder
  -> HumanoidRetargeter
```

Current One Euro defaults remain the accepted Phase 3 values:

```text
minCutoff = 1.0
beta = 0.05
derivativeCutoff = 1.0
```

The user also tested the separate HumanoidRetargeter presentation-smoothing toggle and could not reliably distinguish the minute difference by eye at the current fast upstream rate. Therefore the next controlled question is whether the avatar can be driven from the raw canonical pose itself without unacceptable jitter or instability.

## Experiment goal

Add a **narrow, selectable avatar-drive source** while keeping stabilization alive for calibration and locomotion.

Required conceptual split:

```text
raw canonical (yellow)
      |\
      | \---- optional avatar-drive source
      v
stabilization
      |
stabilized canonical (cyan)
      |\
      | \---- default avatar-drive source
      |
      +------ calibration (MUST remain stabilized)
      +------ locomotion/support-foot/cadence/heading (MUST remain stabilized)
```

Then the retarget solve uses the selected avatar drive frame plus the same calibration profile:

```text
[ StabilizedCanonical | RawCanonical ]
        +
stabilized-derived calibration profile
        -> rotation solve
        -> kinematic targets / IK
        -> humanoid
```

## Required design invariants

- `StabilizedCanonical` remains the serialized/default production behavior.
- `RawCanonical` is experimental/selectable only.
- Calibration must continue to update from `StabilizedFrame` in both modes.
- `EmbodiedLocomotionController` and its root/cadence/heading inputs must continue using `runtime.StabilizedFrame` in both modes.
- The yellow and cyan debug skeletons must continue to be generated/drawn simultaneously.
- The selected avatar-drive frame must be used consistently for BOTH:
  - rotation solving / kinematic-target generation; and
  - HumanoidRetargeter torso/source-frame mapping.
  Do not create a mixed raw-limbs/stabilized-torso comparison.
- Do not modify One Euro parameters yet. This experiment tests the existing raw-vs-existing-stabilized endpoints first.
- Do not modify OpenVINO, WebCamCPU, camera acquisition, inference cadence, mailbox scheduling, calibration thresholds, IK math, canonical coordinate semantics, or locomotion behavior.
- Do not add queues/history/replay/prediction.
- Do not edit USER-owned scene YAML merely to set the experimental selector.
- No `main` merge. No Phase 6.

## Intended implementation seam

Prefer the smallest reusable change in `MotionEngineRuntime`:

- introduce an enum similar to `AvatarDrivePoseSource { StabilizedCanonical, RawCanonical }`;
- serialize the selector on `MotionEngineRuntime`, defaulting to `StabilizedCanonical`;
- expose a read-only property returning the effective drive frame, e.g. `AvatarDriveFrame`;
- keep `_calibration.Update(_stabilizedFrame, ...)` unchanged;
- choose the frame passed to `_rotationSolver.Solve(...)` and `_kinematicTargetBuilder.Build(...)` based on the selector;
- change `HumanoidRetargeter` to use the runtime's effective avatar-drive frame rather than hard-coding `runtime.StabilizedFrame` for torso/source mapping;
- leave locomotion explicitly on `runtime.StabilizedFrame`.

Expose the active/requested drive source clearly in Inspector and/or F7 diagnostics so USER evidence cannot be misidentified.

## Verification expectations before USER QA

Builder should verify at minimum:

- default behavior remains stabilized;
- raw mode actually selects `RawCanonicalFrame` for all avatar-retarget pose inputs;
- calibration remains stabilized in raw mode;
- locomotion remains stabilized in raw mode;
- raw/stabilized debug skeletons remain unchanged;
- presentation smoothing remains independent and unchanged;
- no scene/package/project/native/OpenVINO changes;
- no per-frame allocations introduced by the selector;
- compile/static/unit/helper checks appropriate to the changed files pass.

Checkpoints are recovery markers, not approval gates. Continue through implementation and non-hardware verification until genuine USER visual/runtime A/B QA is required, a real blocker appears, or execution-limit risk requires a handoff.

## Planned USER A/B after Builder verification

Use the accepted best upstream path in both runs:

```text
Inference Backend = OpenVINO CPU FP32
Frame Acquisition = WebCamCPU/GetPixels32
Body input = 320x240
Full-body framing = ~30 FPS camera where practical
```

Primary source-isolation comparison should keep presentation smoothing **the same in both runs**. Prefer OFF initially if practical so it does not mask source differences; if raw mode is visibly jittery, optionally repeat with presentation smoothing ON to judge whether minimal downstream smoothing makes raw drive usable.

Compare:

A. `Avatar Drive Source = StabilizedCanonical` (existing/default)
B. `Avatar Drive Source = RawCanonical` (experimental)

Observe:

- perceived motion-to-avatar latency;
- fast arm/reach response;
- torso response;
- leg/knee response when visible;
- idle jitter / micro-jitter;
- sudden snapping or IK instability;
- partial-body loss/recovery;
- orientation and left/right correctness;
- whether raw mode creates unacceptable instability that the cyan skeleton had been suppressing.

The decision is qualitative first because the remaining difference may be below easy human timing measurement. Phone video is acceptable and preferred over OBS if recording is useful.

## CONTINUE FROM HERE

**STATUS: USER has authorized implementation.**

Next Builder action:

1. inspect current remote HEAD and the exact `MotionEngineRuntime`/`HumanoidRetargeter`/locomotion data flow before editing;
2. implement the narrow default-safe selector described above;
3. preserve calibration and locomotion on stabilized canonical data;
4. add explicit runtime diagnostics for the selected avatar-drive source;
5. run appropriate managed/static/Unity-safe verification;
6. checkpoint/push coherent recovery commits and update this file with exact SHAs and state;
7. stop only when genuine USER A/B visual QA is required, a real blocker appears, or execution-limit risk requires handoff.
