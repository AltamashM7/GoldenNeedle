# Web Builder Handoff — Responsive Avatar Stabilizer A/B/C/D

## Mission

Continue Golden Needle on `AltamashM7/GoldenNeedle`, branch `engine/pose-tracking-spike`.

The USER has explicitly approved a narrow avatar-only responsive stabilization experiment after direct runtime evidence showed:

- current stabilized avatar motion is satisfactory and stable;
- raw canonical avatar motion feels effectively instant / dramatically more responsive;
- raw canonical has only modest additional micro-jitter/instability.

The goal is to build a **middle path** that preserves the raw mode's responsiveness as much as possible while recovering stability.

Read first:

1. `Docs/responsive-avatar-stabilizer-progress.md` — newest rolling authority for this experiment.
2. This handoff.
3. `Docs/avatar-drive-source-experiment-progress.md` — previous selector experiment context only.
4. Current implementations of:
   - `Assets/GoldenNeedle/Core/Motion/Runtime/MotionEngineRuntime.cs`
   - `Assets/GoldenNeedle/Core/Motion/Stabilization/CanonicalPoseStabilizer.cs`
   - `Assets/GoldenNeedle/Core/Motion/Stabilization/CanonicalStabilizerSettings.cs`
   - `Assets/GoldenNeedle/Core/Motion/Retargeting/HumanoidRetargeter.cs`
   - `Assets/GoldenNeedle/Core/Motion/Locomotion/EmbodiedLocomotionController.cs`
   - `Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpikePresenter.cs`

Start from the current remote branch HEAD. Do not continue from a stale local checkout.

Checkpoints are recovery markers, not approval gates. Work continuously through implementation and non-hardware verification until genuine USER runtime/visual QA is required, a real blocker appears, or execution-limit risk requires a handoff.

Do **not** merge to `main`. Do **not** start Phase 6.

---

## Accepted context — do not reopen

Treat all of the following as accepted unless new direct evidence proves otherwise:

- OpenVINO CPU FP32 integration and semantic compatibility;
- OpenVINO persistent worker/latest-frame mailbox scheduling;
- WebCamCPU/GetPixels32 acquisition path;
- 320x240 body inference input;
- canonical orientation and left/right semantics;
- modular calibration;
- accepted Phase 4 retargeting/IK math;
- partial-body semantics;
- raw-vs-stabilized avatar source selector architecture.

The current upstream best path can deliver close to one fresh pose result per ~30 FPS camera frame under full-body conditions. This task is **not** an inference/readback optimization.

Do not change:

- OpenVINO native code/models/ABI;
- camera acquisition/readback;
- worker/mailbox cadence or boundedness;
- provider result semantics;
- detector behavior;
- canonical mapper semantics;
- calibration thresholds/math;
- locomotion behavior;
- IK math;
- Presentation Smoothing implementation or defaults.

---

## Existing selector state

The branch already contains:

```csharp
public enum AvatarDrivePoseSource
{
    StabilizedCanonical,
    RawCanonical,
}
```

`StabilizedCanonical` is the serialized/default behavior.

`MotionEngineRuntime.AvatarDriveFrame` is the single source used by:

- rotation solving;
- kinematic-target generation;
- HumanoidRetargeter source/torso mapping.

Calibration remains explicitly on the existing stable frame, and locomotion reads `runtime.StabilizedFrame` directly.

Preserve that separation.

---

## Required experiment architecture

Do **not** globally retune the existing canonical stabilizer.

Add two separate avatar-only responsive canonical stabilizers that run in parallel from the raw canonical frame.

Required conceptual data flow:

```text
provider
  -> raw canonical ---------------------------------------------+
      |                                                        |
      +-> existing stable CanonicalPoseStabilizer               |
      |      -> StabilizedCanonical                             |
      |          +-> calibration                                |
      |          +-> locomotion / support / cadence / heading   |
      |                                                        |
      +-> responsive stabilizer A -> ResponsiveCanonicalA      |
      |                                                        |
      +-> responsive stabilizer B -> ResponsiveCanonicalB      |
      |                                                        |
      +--------------------------------------------------------> RawCanonical
                                                               |
Avatar Drive Pose Source selector <----------------------------+
  -> StabilizedCanonical          [existing/default]
  -> ResponsiveCanonicalA         [experimental]
  -> ResponsiveCanonicalB         [experimental]
  -> RawCanonical                 [responsiveness reference]
             |
             -> rotation solver
             -> kinematic targets / IK
             -> HumanoidRetargeter source/torso mapping
             -> avatar
```

The two responsive stabilizers must run continuously every runtime update, whether selected or not, so the USER can live-switch sources during one calibrated Play session without cold-start filter behavior.

---

## Initial responsive profiles

These are experimental starting points for USER comparison, not final production values.

### Existing stable baseline — DO NOT MODIFY

```text
minCutoff        1.0
beta             0.05
derivativeCutoff 1.0
```

### Responsive A — moderate

```text
minCutoff        1.5
beta             0.25
derivativeCutoff 1.0
```

### Responsive B — aggressive

```text
minCutoff        2.0
beta             0.50
derivativeCutoff 1.0
```

Both responsive profiles must retain the accepted confidence/loss semantics:

```text
acquireConfidence       0.60
sustainConfidence       0.40
acquireSamples          2
lossGraceSeconds        0.10
resetAfterLossSeconds   0.25
defaultDeltaTimeSeconds 0.05
maximumDeltaTimeSeconds 0.25
```

Do not alter `derivativeCutoff` in this first sweep.

Do not silently add further profiles or tune these values after seeing static tests. Runtime visual behavior is the USER decision gate.

---

## Implementation guidance

Prefer reuse over new filter code.

The project already owns `CanonicalPoseStabilizer` and `CanonicalStabilizerSettings`. Reuse them for both responsive candidates rather than creating a second smoothing algorithm.

A clean implementation will likely include:

- two serialized or otherwise explicit responsive settings objects with the fixed starting parameters above;
- two `CanonicalPoseStabilizer` instances;
- two persistent/preallocated `CanonicalPoseFrame` outputs;
- additional enum values in the existing `AvatarDrivePoseSource`;
- `AvatarDriveFrame` routing for all four sources;
- clear source/profile labels for diagnostics.

Do not allocate filter objects or pose frames every update.

Do not require scene YAML edits. Existing serialized enum values must remain compatible: the old default value must continue to mean `StabilizedCanonical` and the existing raw value must not be accidentally remapped by enum reordering. Prefer explicit enum values if necessary for serialization safety.

The existing raw and stable frame properties must continue to refer to exactly the existing frames.

It is acceptable to expose read-only responsive frame properties for tests/diagnostics, but do not reroute calibration or locomotion through them.

---

## Serialization safety requirement

The previous enum is serialized in Unity. Preserve old serialized numeric meanings.

Do not insert new enum members in a way that changes the stored meaning of `RawCanonical`.

For example, use explicit values such as:

```csharp
StabilizedCanonical = 0,
RawCanonical = 1,
ResponsiveCanonicalA = 2,
ResponsiveCanonicalB = 3,
```

or another serialization-safe ordering that preserves existing 0/1 semantics.

Diagnostics/UI ordering can be handled separately if desired.

---

## Runtime/reset behavior

Responsive stabilizers must be reset/cleared whenever the existing stable stabilizer is reset due to coordinate convention/source lifecycle changes.

Also inspect explicit calibration reset behavior. If the existing `ResetCalibration()` resets the stable stabilizer, reset both responsive stabilizers and clear their frames too so subsequent live comparison starts from coherent state.

When no canonical source is available, responsive output frames must not retain stale poses indefinitely; mirror the existing runtime's clear/unavailable behavior appropriately.

Do not introduce history, prediction, extrapolation, queues, or replay.

---

## Calibration invariant — hard requirement

The following semantic behavior must remain true in every avatar source mode:

```csharp
_calibration.Update(_stabilizedFrame, _lastEvaluationTimeSeconds);
```

Calibration is not part of this experiment.

Responsive A/B and Raw may drive only avatar pose solving after the stable calibration update.

Do not create separate calibration profiles for responsive/raw modes.

---

## Locomotion invariant — hard requirement

Do not modify `EmbodiedLocomotionController` unless a compile-only reference change is absolutely unavoidable, which is not expected.

The following consumers must continue to use `runtime.StabilizedFrame`:

- support/root tracking;
- cadence;
- heading;
- Phase 5A physical locomotion semantics.

The avatar source selector must not affect locomotion.

Phase 5A remains NOT USER ACCEPTED.

---

## Retarget consistency — hard requirement

For whichever avatar source is selected, use the same selected frame consistently for:

- `CanonicalRotationSolver.Solve(...)`;
- `CanonicalKinematicTargetBuilder.Build(...)`;
- HumanoidRetargeter's torso/source mapping.

Do not produce a hybrid pose where torso, rotation, or IK targets come from different canonical source modes.

The current `runtime.AvatarDriveFrame` seam is the intended single authority.

---

## Debug/UI requirements

Keep existing debug overlays unchanged:

- yellow = raw canonical;
- cyan/blue = existing stabilized canonical.

Do not repurpose either color for the new responsive candidates.

A third/fourth skeleton overlay is **not required** and should be avoided unless it is genuinely necessary; visual clutter is not the goal.

F7 must clearly identify the effective avatar source. For responsive modes, include enough information to distinguish the profiles, e.g.:

```text
Avatar source: ResponsiveCanonicalA (1.5/0.25/1.0)
Avatar source: ResponsiveCanonicalB (2.0/0.50/1.0)
```

The USER should be able to change the source from the existing `MotionEngineRuntime` Inspector during Play Mode.

Presentation Smoothing must remain independently controllable and unchanged.

---

## Required non-hardware verification

Before stopping for USER QA, verify all of the following:

1. current remote starting SHA was inspected before edits;
2. enum serialization preserves old stable/raw numeric meanings;
3. `StabilizedCanonical` remains serialized default;
4. stable profile remains exactly `1.0 / 0.05 / 1.0` with existing confidence/loss settings;
5. Responsive A is exactly `1.5 / 0.25 / 1.0` with existing confidence/loss settings;
6. Responsive B is exactly `2.0 / 0.50 / 1.0` with existing confidence/loss settings;
7. both responsive stabilizers read raw canonical and update continuously each runtime update;
8. responsive frames are persistent/preallocated; no per-frame frame/filter allocations;
9. calibration remains stable-only;
10. locomotion remains stable-only;
11. all four avatar sources route correctly;
12. rotation solver and kinematic builder use the same selected `AvatarDriveFrame`;
13. HumanoidRetargeter uses the same selected source;
14. coordinate/source reset clears/resets both responsive stabilizers;
15. explicit calibration reset also leaves responsive paths coherent;
16. raw/stable debug skeletons remain intact;
17. Presentation Smoothing implementation/settings unchanged;
18. no OpenVINO/WebCamCPU/provider/native/model/camera/IK/locomotion/scene/Packages/ProjectSettings changes;
19. no queue/history/replay/prediction introduced.

Use narrow editor/helper/static tests or existing project verification patterns. Avoid unnecessary full-scene automated runs.

A compile/static pass is not a substitute for USER visual QA.

---

## USER QA procedure to prepare

Stop once Unity-hardware visual comparison is the only remaining question.

The USER should pull the latest branch and use one Play session with:

```text
Inference Backend = OpenVINO CPU FP32
Frame Acquisition = WebCamCPU/GetPixels32
Body input = 320x240
full-body framing where practical (~30 FPS)
Presentation Smoothing = OFF for the primary comparison
```

Calibrate once, then live-switch the existing `Avatar Drive Pose Source` among:

```text
StabilizedCanonical
ResponsiveCanonicalA
ResponsiveCanonicalB
RawCanonical
```

Have F7 visible briefly before each F12 section so the active source is unambiguous.

Phone video is preferable to OBS if recording is useful.

Ask the USER to compare:

- perceived motion-to-avatar latency;
- whether motion still feels effectively instant;
- idle micro-jitter;
- wrist/ankle endpoint jitter;
- fast reaches/arm swings;
- torso response;
- knees/legs;
- snapping/IK instability;
- partial-body loss/recovery;
- left/right and orientation correctness.

Decision criterion:

> Prefer the most stable responsive profile that feels essentially as immediate as Raw.

If neither responsive profile is close enough to Raw, report that result. Do not add a third tuning profile without a new USER/Orchestrator decision.

If one responsive profile is clearly the winner, do not silently make it the production/default mode. Return to the Orchestrator for acceptance and the next decision.

---

## Checkpoint / recovery policy

Checkpoints are recovery markers, not approval gates.

At each coherent checkpoint:

- commit and push;
- update `Docs/responsive-avatar-stabilizer-progress.md`;
- record exact starting/ending SHA;
- record completed work and verification;
- keep a precise `CONTINUE FROM HERE` section.

If execution limit approaches, leave the branch in a coherent pushed state and make the progress file sufficient for another Builder to resume without repeating completed work.

---

## Stop conditions

Stop only when one of these is true:

1. implementation + non-hardware verification are complete and genuine USER visual QA is required;
2. a real blocker requires USER/Orchestrator decision;
3. execution-limit risk requires a recovery handoff.

Do not stop merely because a recovery checkpoint was reached.

---

## Required stop report

When stopping, return:

- exact starting remote SHA;
- exact ending remote SHA;
- implementation commit SHA(s);
- files changed;
- exact responsive profile values implemented;
- confirmation that stable/raw serialized meanings were preserved;
- confirmation calibration remains stable-only;
- confirmation locomotion remains stable-only;
- confirmation retarget/rotation/IK source consistency;
- reset/lifecycle behavior;
- tests/audits run and results;
- any warnings/risks;
- exact USER QA instructions;
- explicit confirmation that `main` was not merged and Phase 6 was not started.

## CONTINUE FROM HERE

Begin from the current remote branch state after this documentation checkpoint. Implement the two parallel responsive avatar-only `CanonicalPoseStabilizer` candidates, extend the existing serialization-safe avatar source selector, verify invariants and scope, then stop at the USER visual A/B/C/D gate.
