# Web Builder Handoff — Raw vs Stabilized Avatar Drive A/B

## Mission

Continue Golden Needle on `AltamashM7/GoldenNeedle`, branch `engine/pose-tracking-spike`.

The USER has explicitly approved a narrow experiment to compare avatar retargeting driven from the existing **stabilized canonical frame** versus the existing **raw canonical frame**.

Read first:

1. `Docs/avatar-drive-source-experiment-progress.md` — rolling resume point and newest authority for this experiment.
2. This handoff.
3. The current implementation of:
   - `Assets/GoldenNeedle/Core/Motion/Runtime/MotionEngineRuntime.cs`
   - `Assets/GoldenNeedle/Core/Motion/Retargeting/HumanoidRetargeter.cs`
   - `Assets/GoldenNeedle/Core/Motion/Locomotion/EmbodiedLocomotionController.cs`
   - `Assets/GoldenNeedle/Core/Motion/Stabilization/CanonicalPoseStabilizer.cs`
   - `Assets/GoldenNeedle/Core/Motion/Stabilization/CanonicalStabilizerSettings.cs`
   - `Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpikePresenter.cs`

Start from the **current remote branch HEAD**, not from an older local checkout or abandoned transient commit.

Checkpoints are recovery markers, not approval gates. Work continuously through implementation and non-hardware verification until genuine USER visual/runtime QA is required, a real blocker appears, or execution-limit risk requires a handoff.

Do **not** merge to `main`. Do **not** start Phase 6.

---

## Accepted context — do not reopen

The accepted best upstream path is now:

```text
WebCamTexture
  -> WebCamCPU/GetPixels32 reusable CPU acquisition
  -> reusable 320x240 RGBA preparation
  -> accepted two-slot latest-frame OpenVINO mailbox
  -> one persistent OpenVINO CPU FP32 worker
  -> provider result
```

USER runtime evidence with full-body framing is approximately:

```text
camera capture       ~28.6-30.3 FPS
fresh pose results   ~26.7-29.3/s
CPU GetPixels32      ~0.3 ms
CPU preparation      ~3.6 ms
CPU acquisition      ~3.9 ms total
OpenVINO processing  commonly ~25-35 ms
frame->result        commonly ~33-62 ms
```

The USER reports motion replication is now satisfactory and the WebCamCPU path has clearly lower latency than ExistingReadback. Treat the following as accepted unless new direct evidence proves otherwise:

- OpenVINO model/native integration;
- OpenVINO worker/mailbox scheduling;
- WebCamCPU/GetPixels32 R2 acquisition path;
- 320x240 body input;
- canonical orientation/left-right semantics;
- modular calibration architecture;
- accepted Phase 4 retarget math;
- existing partial-body semantics.

Do not spend this task tuning OpenVINO, camera/readback, worker cadence, detector logic or native ABI.

---

## Why this experiment exists

The Lab already overlays two canonical skeletons:

- yellow = raw canonical;
- cyan/blue = stabilized canonical.

The USER can visibly see the cyan/blue skeleton lag the yellow skeleton slightly during motion. Repository inspection confirms the runtime currently stabilizes the canonical pose before calibration, rotation solving and kinematic-target generation.

Current One Euro defaults are approximately:

```text
minCutoff = 1.0
beta = 0.05
derivativeCutoff = 1.0
```

At the old ~10-13 result/s rate this stabilization was useful and accepted. At the new ~27-29 result/s rate the filter's delay is now visible enough to investigate.

The experiment must answer only:

> Can the humanoid be driven from the raw canonical frame with meaningfully better responsiveness and still acceptable stability?

Do not tune the filter yet. First compare the two existing endpoints cleanly.

---

## Required architecture

Current conceptual path:

```text
provider
  -> raw canonical
  -> One Euro stabilization
  -> stabilized canonical
  -> calibration
  -> rotation solver
  -> kinematic targets
  -> humanoid
```

Required experiment split:

```text
provider
  -> raw canonical --------------------------+
  -> One Euro stabilization                  |
  -> stabilized canonical                    |
          |                                   |
          +-> calibration (always stabilized) |
          +-> locomotion (always stabilized)  |
                                              |
Avatar Drive Source selector -----------------+
          |
          +-> StabilizedCanonical [DEFAULT]
          +-> RawCanonical [EXPERIMENTAL]
          |
          -> rotation solver
          -> kinematic targets
          -> HumanoidRetargeter source/torso mapping
          -> avatar
```

The selector must affect **all avatar pose-solving inputs consistently**. A mixed test where raw drives limbs but stabilized drives torso (or vice versa) is invalid.

---

## Preferred implementation seam

Prefer a small change centered in `MotionEngineRuntime`.

A suitable design is:

```csharp
public enum AvatarDrivePoseSource
{
    StabilizedCanonical,
    RawCanonical,
}
```

with a serialized field defaulting to `StabilizedCanonical`.

Expose read-only runtime properties such as:

```text
AvatarDriveSource
AvatarDriveFrame
AvatarDriveSourceLabel
```

Naming may differ if there is a cleaner existing convention.

In `MotionEngineRuntime.Update()` preserve this ordering:

```text
copy source -> _rawCanonicalFrame
stabilize raw -> _stabilizedFrame
calibration.Update(_stabilizedFrame, ...)
select avatarDriveFrame = raw OR stabilized
rotationSolver.Solve(avatarDriveFrame, calibration.Profile, ...)
kinematicTargetBuilder.Build(avatarDriveFrame, calibration.Profile, ...)
```

This is important: **calibration remains stabilized regardless of selected avatar drive source**.

In `HumanoidRetargeter`, replace the hard-coded use of `runtime.StabilizedFrame` for the source/torso frame with the runtime's selected `AvatarDriveFrame`, so torso/body-basis mapping uses the same selected frame as the rotation/kinematic outputs.

Do not alter the accepted IK solver, calibration profile math, return-to-reference semantics or presentation smoothing algorithm.

`EmbodiedLocomotionController` must continue using `runtime.StabilizedFrame` exactly as today for root tracking, cadence and heading. Do not route locomotion through the experimental raw selector.

---

## UI / diagnostics requirement

The USER must be able to choose the source without editing scene YAML manually.

At minimum expose a clear Inspector field:

```text
Avatar Drive Source
  Stabilized Canonical
  Raw Canonical
```

`Stabilized Canonical` must remain the serialized/default behavior.

Also make the effective drive source visible in F7 or another existing runtime diagnostic surface, e.g.:

```text
Avatar source: Stabilized
```

or

```text
Avatar source: Raw
```

Do not rely only on an Inspector value if runtime behavior could differ from the requested value.

Do not create a large new debug UI system for this experiment.

---

## Hard invariants

Preserve all of the following:

- `StabilizedCanonical` is default-safe and reproduces current behavior.
- raw and stabilized canonical frames continue to be generated simultaneously.
- yellow/cyan Lab skeleton rendering remains unchanged.
- calibration always consumes stabilized canonical data.
- locomotion/support-foot/cadence/heading always consume stabilized canonical data.
- raw mode affects only avatar pose solve/retarget source.
- canonical coordinate convention and left/right semantics unchanged.
- confidence/trust mapping unchanged.
- partial-body behavior and unavailable return semantics unchanged except for the natural consequence of choosing a less-filtered pose source.
- presentation smoothing remains separately selectable and otherwise untouched.
- OpenVINO/WebCamCPU/ExistingReadback/TFLite paths untouched.
- no queue/history/replay/prediction.
- no per-frame allocation introduced solely by the selector.
- no scene YAML, `Packages`, `ProjectSettings`, native plugin or model changes merely for convenience.
- no merge to `main`.
- no Phase 6 work.

If implementing the selector cleanly would require violating one of these, stop and report the blocker instead of expanding scope.

---

## Verification before USER QA

Use the lightest verification that gives strong confidence. Do not waste time on unrelated testing.

Verify at minimum:

1. `StabilizedCanonical` default resolves to `runtime.StabilizedFrame`.
2. `RawCanonical` resolves to `runtime.RawCanonicalFrame`.
3. calibration receives `_stabilizedFrame` in both modes.
4. rotation solver and kinematic-target builder receive the selected drive frame.
5. HumanoidRetargeter torso/source mapping receives the same selected drive frame.
6. `EmbodiedLocomotionController` still directly uses `runtime.StabilizedFrame` for root/cadence/heading.
7. debug yellow/cyan rendering remains untouched.
8. presentation smoothing logic remains unchanged.
9. project compiles / appropriate managed or Unity-safe tests pass.
10. diff scope contains no unrelated scene/project/package/native changes.

If useful, add small deterministic tests around source selection, but do not create a large testing framework.

---

## Recovery checkpoints

At each coherent checkpoint:

1. commit and push to `engine/pose-tracking-spike`;
2. update `Docs/avatar-drive-source-experiment-progress.md` with:
   - exact starting SHA;
   - exact ending SHA;
   - files changed;
   - verification performed/results;
   - unresolved risks;
   - explicit `CONTINUE FROM HERE` state.

These are recovery markers, **not approval gates**. Continue automatically unless USER hardware/visual QA is genuinely required.

---

## USER QA boundary

Stop when implementation and non-hardware verification are complete and the only remaining question is visual/runtime comparison on the USER's machine.

Prepare this comparison:

```text
Common settings for both runs:
Inference Backend = OpenVINO CPU FP32
Frame Acquisition = WebCamCPU/GetPixels32
Body input = 320x240
full-body framing where practical (~30 FPS capture)
same camera / lighting / calibration / retarget settings
```

For the cleanest source comparison, keep Presentation Smoothing identical between runs. Prefer **OFF for the primary A/B** if doing so is straightforward, because it avoids masking the source difference. It can later be repeated ON if raw mode is responsive but slightly noisy.

Run A:

```text
Avatar Drive Source = StabilizedCanonical
```

Run B:

```text
Avatar Drive Source = RawCanonical
```

USER should compare:

- perceived motion-to-avatar latency;
- fast arms/reaches;
- torso response;
- legs/knees when visible;
- idle jitter / micro-jitter;
- snapping or IK instability;
- partial-body loss/recovery;
- orientation and left/right parity.

Phone-recorded external video is useful if differences are too subtle to judge live; avoid OBS unless necessary.

Do not mark raw drive accepted merely because it feels faster. It must also remain acceptably stable.

---

## Stop report format

When stopping, report:

- exact starting remote SHA;
- exact ending remote SHA;
- implementation summary;
- exact selector/default behavior;
- files changed;
- proof calibration remained stabilized;
- proof locomotion remained stabilized;
- proof retarget torso + rotation/kinematic solve use the same selected frame;
- verification performed and results;
- any warnings/risks;
- whether genuine USER A/B QA is now the only remaining gate;
- exact USER QA steps;
- `CONTINUE FROM HERE` state.
