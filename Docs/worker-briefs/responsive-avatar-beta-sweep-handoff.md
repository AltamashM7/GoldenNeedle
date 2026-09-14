# Web Builder Handoff — Responsive Avatar Beta-Only Sweep

## Mission

Continue Golden Needle on `AltamashM7/GoldenNeedle`, branch `engine/pose-tracking-spike`.

The USER has explicitly approved the next narrow avatar-only stabilization experiment after testing Stable, Responsive A, Responsive B and Raw.

Read first:

1. `Docs/responsive-avatar-beta-sweep-progress.md` — newest rolling authority for this task.
2. This handoff.
3. `Docs/responsive-avatar-stabilizer-progress.md` — immediately preceding experiment/history.
4. Current implementations of:
   - `Assets/GoldenNeedle/Core/Motion/Runtime/MotionEngineRuntime.cs`
   - `Assets/GoldenNeedle/Core/Motion/Stabilization/CanonicalPoseStabilizer.cs`
   - `Assets/GoldenNeedle/Core/Motion/Stabilization/CanonicalStabilizerSettings.cs`
   - `Assets/GoldenNeedle/Core/Motion/Retargeting/HumanoidRetargeter.cs`
   - `Assets/GoldenNeedle/Core/Motion/Locomotion/EmbodiedLocomotionController.cs`
   - `Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpikePresenter.cs`

Start from the **current remote branch HEAD**. Do not use an older checkout or restart from prior implementation commits.

Checkpoints are recovery markers, not approval gates. Work continuously through implementation and non-hardware verification until genuine USER visual/runtime QA is required, a real blocker appears, or execution-limit risk requires a handoff.

Do **not** merge to `main`. Do **not** start Phase 6.

---

## Accepted context — do not reopen

The accepted best upstream runtime is:

```text
WebCamTexture
 -> WebCamCPU/GetPixels32
 -> reusable 320x240 RGBA preparation
 -> accepted two-slot OpenVINO mailbox
 -> one persistent OpenVINO CPU FP32 worker
 -> raw canonical
```

With full-body framing the USER previously observed roughly ~30 camera FPS and ~27-29 fresh poses/s. Raw avatar drive feels essentially instant. OpenVINO integration, scheduling and WebCamCPU acquisition are accepted and outside this task.

The current downstream architecture has Stable/A/B/Raw avatar sources while calibration and locomotion remain stabilized.

Do not tune or modify OpenVINO, camera acquisition, provider scheduling, inference cadence, native code, retarget math, IK math, locomotion, calibration, Presentation Smoothing, scene assets or packages.

---

## New USER evidence

The USER tested all existing modes directly by feel and reported:

- `StabilizedCanonical` is the smoothest but more delayed;
- Responsive A and Responsive B both feel more effective / responsive than Stable;
- A and B still contain minor jitter compared with Stable;
- Raw remains the responsiveness reference and feels essentially latency-free, but is less stable.

This task is not another broad parameter search. It isolates the suspected cause of the A/B jitter by restoring `minCutoff` to the Stable value and sweeping **beta only**.

---

## Exact current profiles — preserve

Existing Stable:

```text
minCutoff        = 1.0
beta             = 0.05
derivativeCutoff = 1.0
```

Existing Responsive A:

```text
minCutoff        = 1.5
beta             = 0.25
derivativeCutoff = 1.0
```

Existing Responsive B:

```text
minCutoff        = 2.0
beta             = 0.50
derivativeCutoff = 1.0
```

Raw:

```text
no canonical positional filtering
```

Do not alter any of those meanings or values in this task.

---

## Authorized new candidates

Add exactly two new avatar-only responsive candidates.

### Responsive C

```text
minCutoff        = 1.0
beta             = 0.25
derivativeCutoff = 1.0
```

### Responsive D

```text
minCutoff        = 1.0
beta             = 0.50
derivativeCutoff = 1.0
```

For both C and D, preserve the accepted confidence/loss/timing values exactly:

```text
acquireConfidence       = 0.60
sustainConfidence       = 0.40
acquireSamples          = 2
lossGraceSeconds        = 0.10
resetAfterLossSeconds   = 0.25
defaultDeltaTimeSeconds = 0.05
maximumDeltaTimeSeconds = 0.25
```

Reuse the existing `CanonicalPoseStabilizer`. Do not create a different smoothing algorithm.

---

## Serialization safety — mandatory

Existing enum values are already serialized. Preserve them exactly:

```text
StabilizedCanonical   = 0
RawCanonical          = 1
ResponsiveCanonicalA  = 2
ResponsiveCanonicalB  = 3
```

Append only:

```text
ResponsiveCanonicalC  = 4
ResponsiveCanonicalD  = 5
```

Do not reorder, renumber, rename, or repurpose existing values. `StabilizedCanonical` stays the serialized/default selection.

---

## Required runtime architecture

Target flow:

```text
raw canonical
   |
   +-> Stable stabilizer -> StabilizedCanonical
   |      +-> calibration ONLY
   |      +-> locomotion ONLY
   |
   +-> Responsive A -> ResponsiveCanonicalA
   +-> Responsive B -> ResponsiveCanonicalB
   +-> Responsive C -> ResponsiveCanonicalC
   +-> Responsive D -> ResponsiveCanonicalD
   +-> RawCanonical

AvatarDriveFrame selector
   -> one selected canonical frame
   -> CanonicalRotationSolver
   -> CanonicalKinematicTargetBuilder
   -> HumanoidRetargeter via runtime.AvatarDriveFrame
```

### Stable calibration ordering must remain literal

Current accepted ordering has the Stable stabilizer and calibration update adjacent. Preserve that:

```text
_stabilizer.Stabilize(raw -> stable)
_calibration.Update(stable)
```

Then run A/B/C/D responsive stabilizers before avatar source selection/solve.

Do not insert responsive work between Stable stabilization and calibration.

### Warm switching

C and D must be persistent objects and persistent output frames created once, like A/B. They must run continuously from raw every update even when not selected. The USER must be able to switch modes live during one calibration without a cold-start transient.

### Single-authority solve

`AvatarDriveFrame` must remain the sole source selector. The exact selected frame must feed:

- rotation solver;
- kinematic target builder;
- `HumanoidRetargeter` torso/source mapping through its existing `runtime.AvatarDriveFrame` read.

No hybrid raw/stable/responsive solve.

---

## Lifecycle/reset requirements

C/D must be handled everywhere A/B currently are:

- initialize once in `Awake()`;
- update every runtime update from `_rawCanonicalFrame`;
- reset on coordinate convention changes;
- reset during `ResetCalibration()`;
- clear output frames when no source can be resolved;
- avoid stale frames after lifecycle resets;
- no per-frame filter/frame allocations.

Prefer extending existing helper structure cleanly rather than duplicating unrelated logic.

---

## Debug/UI requirements

Keep the existing debug overlays unchanged:

- yellow = raw canonical;
- cyan = existing Stable canonical.

Do not add C/D skeleton overlays unless a real blocker requires them; they are not needed for this experiment.

F7 must identify the active source and exact profile, for example:

```text
ResponsiveCanonicalC (1.0/0.25/1.0)
ResponsiveCanonicalD (1.0/0.50/1.0)
```

Keep Presentation Smoothing independent and unchanged.

---

## Hard scope guardrails

Do not modify:

- `CanonicalPoseStabilizer` algorithm unless a true compile/blocker requires it;
- `CanonicalStabilizerSettings` stable defaults;
- Stable/A/B parameter values;
- Raw semantics;
- calibration math/settings;
- locomotion/support-foot/cadence/heading;
- Humanoid retarget/IK math;
- Presentation Smoothing behavior/defaults;
- OpenVINO integration/native ABI/worker/mailbox;
- WebCamCPU acquisition;
- provider orientation/camera semantics;
- scenes or USER-owned scene YAML;
- `Packages` or `ProjectSettings`;
- models/native plugin artifacts;
- Phase 5A acceptance;
- Phase 6.

No queues, history buffers, replay, prediction or catch-up logic.

---

## Verification expectations

Perform focused static/helper verification sufficient to establish the implementation before USER visual QA. At minimum verify:

1. Existing enum values 0-3 unchanged; C=4, D=5.
2. Serialized default remains Stable.
3. Stable profile unchanged at `1.0/0.05/1.0`.
4. A unchanged at `1.5/0.25/1.0`.
5. B unchanged at `2.0/0.50/1.0`.
6. C exactly `1.0/0.25/1.0`.
7. D exactly `1.0/0.50/1.0`.
8. Accepted confidence/loss/timing values preserved for C/D.
9. C/D stabilizers and output frames are persistent/preallocated.
10. No per-update stabilizer/frame construction.
11. Stable stabilization -> calibration adjacency preserved.
12. A/B/C/D all run continuously from raw before avatar selection/solve.
13. Calibration remains stable-only.
14. Locomotion remains stable-only and its file should not need modification.
15. Six-way `AvatarDriveFrame` routing is correct.
16. Rotation and kinematic targets share the same selected frame.
17. `HumanoidRetargeter` still consumes `runtime.AvatarDriveFrame` unchanged.
18. Reset/clear lifecycle covers C/D.
19. Existing yellow/cyan debug paths preserved.
20. Presentation Smoothing surface/defaults untouched.
21. No queue/history/prediction state introduced.
22. Final diff scope contains only files justified by this experiment.

A small dedicated CI/static audit is acceptable. Do not run unnecessary automated Unity scene tests merely to create activity.

---

## Recovery/checkpoint workflow

At each coherent recovery point:

1. commit and push to `engine/pose-tracking-spike`;
2. update `Docs/responsive-avatar-beta-sweep-progress.md` with:
   - exact ending remote SHA;
   - what changed;
   - verification performed/results;
   - any remaining risks;
   - exact `CONTINUE FROM HERE` state;
3. continue without waiting for USER approval unless genuine hardware QA or a real decision is required.

If execution-limit risk appears, leave a clean checkpoint and accurate rolling progress rather than rushing speculative changes.

---

## Stop boundary

Stop when one of these occurs:

- implementation + non-hardware verification are complete and genuine USER visual/runtime QA is required;
- a real blocker needs USER/Orchestrator decision;
- execution-limit risk requires a handoff.

Do not stop merely because a checkpoint commit was created.

---

## USER QA after Builder stop

Use one Play session with:

```text
OpenVINO CPU FP32
WebCamCPU/GetPixels32
320x240 body input
full-body framing where practical
Presentation Smoothing OFF
```

Calibrate once. Primary comparison:

```text
StabilizedCanonical
ResponsiveCanonicalC
ResponsiveCanonicalD
RawCanonical
```

A/B remain optional secondary references.

The USER should choose by feel, prioritizing:

- near-Raw responsiveness;
- Stable-like idle steadiness;
- reduced micro-jitter compared with A/B/Raw;
- wrist/ankle endpoint behavior;
- torso/leg response;
- no snapping or IK regression;
- correct partial-body recovery and orientation.

No candidate becomes the default automatically. Return the USER result to the Orchestrator for the next decision.

---

## Required stop report

When stopping, report:

- exact starting remote SHA;
- exact ending remote SHA;
- implementation checkpoint SHAs;
- exact C/D profiles implemented;
- enum/serialization preservation;
- architecture/lifecycle changes;
- files changed;
- verification performed and exact workflow/run IDs if applicable;
- unresolved risks;
- why USER QA is required;
- exact USER test steps;
- exact `CONTINUE FROM HERE` state recorded in the rolling progress file.

Confirm explicitly that no merge to `main` occurred and Phase 6 was not started.
