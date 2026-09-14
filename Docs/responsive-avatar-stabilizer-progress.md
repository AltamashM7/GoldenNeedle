# Golden Needle — Responsive Avatar Stabilizer Progress

This is the rolling resume point for the avatar-only responsive stabilization experiment that follows the accepted raw-vs-stabilized avatar-drive A/B.

This file and `Docs/worker-briefs/responsive-avatar-stabilizer-handoff.md` are the newest authority for this experiment. Do not reopen accepted OpenVINO, WebCamCPU, raw-vs-stabilized selector, retargeting, calibration, or locomotion work unless new direct evidence requires it.

## Current authorization and status

- Branch: `engine/pose-tracking-spike`.
- Remote HEAD inspected immediately before this implementation: `c07cea3801a11ba100d5a465935906895debbf63`.
- Earlier authorization/runtime base before the responsive-stabilizer documentation handoff: `4aa23cc6479d8269a6e667e5599aeeb4b7247184`.
- OpenVINO Unity integration: **USER ACCEPTED — PASS**.
- OpenVINO scheduling optimization: **USER ACCEPTED — PASS**.
- WebCamCPU/GetPixels32 acquisition path: **USER ACCEPTED — PASS**.
- Raw-vs-stabilized avatar-drive selector: **IMPLEMENTED + USER QA COMPLETE**.
- Responsive avatar-only stabilization implementation: **COMPLETE**.
- Responsive avatar-only non-hardware/static verification: **PASS**.
- Current gate: **GENUINE USER VISUAL/RUNTIME A/B/C/D QA**.
- Phase 5A remains **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## Accepted motivation

USER runtime A/B established that:

- `StabilizedCanonical` is satisfactory and stable;
- `RawCanonical` feels effectively instant / dramatically more responsive;
- raw has only modest extra micro-jitter/abruptness;
- no fundamental orientation, left/right, retarget, or IK failure was reported in raw mode.

This experiment therefore adds two avatar-only middle paths while leaving the existing stable canonical path unchanged for calibration and locomotion.

## Implemented architecture

`MotionEngineRuntime` now maintains four avatar-drive candidates:

```text
raw canonical
   |
   +-> existing stable CanonicalPoseStabilizer -> StabilizedCanonical
   |       +-> calibration (UNCHANGED / stable only)
   |       +-> locomotion  (UNCHANGED / stable only)
   |
   +-> responsive CanonicalPoseStabilizer A -> ResponsiveCanonicalA
   |
   +-> responsive CanonicalPoseStabilizer B -> ResponsiveCanonicalB
   |
   +-----------------------------------------> RawCanonical

AvatarDriveFrame selector
   -> selected frame
   -> CanonicalRotationSolver
   -> CanonicalKinematicTargetBuilder
   -> HumanoidRetargeter torso/source mapping
```

The two responsive stabilizers are persistent objects created once in `Awake()`. Their output `CanonicalPoseFrame` objects are persistent/read-only runtime fields. No responsive stabilizer or pose frame is allocated in `Update()`.

Both responsive stabilizers consume `_rawCanonicalFrame` and execute every runtime update whether selected or not. This keeps them warm for live switching in a single calibrated Play session.

The accepted stable ordering is preserved literally:

```text
_stabilizer.Stabilize(raw -> stable)
_calibration.Update(stable)
responsive A Stabilize(raw -> A)
responsive B Stabilize(raw -> B)
select AvatarDriveFrame
rotation solve(selected)
kinematic targets(selected)
```

Therefore the responsive experiment does not insert itself between the existing stable stabilizer and stable calibration update.

## Serialization-safe avatar source selector

The serialized enum meanings are explicitly fixed as:

```text
StabilizedCanonical   = 0   (existing meaning preserved / default)
RawCanonical          = 1   (existing meaning preserved)
ResponsiveCanonicalA  = 2   (new)
ResponsiveCanonicalB  = 3   (new)
```

`StabilizedCanonical` remains the serialized/default value.

`AvatarDriveFrame` routes all four modes from one authority. `HumanoidRetargeter` remains unchanged and still reads `runtime.AvatarDriveFrame`, so torso/source mapping uses the same selected frame as rotation solving and kinematic-target generation.

F7 already reads `runtime.AvatarDriveSourceLabel`. The runtime labels now identify responsive profiles explicitly:

```text
ResponsiveCanonicalA (1.5/0.25/1.0)
ResponsiveCanonicalB (2.0/0.50/1.0)
```

No extra responsive skeleton overlays were added. Existing yellow raw and cyan stable overlays remain unchanged.

## Exact stabilization profiles

### Existing stable path — unchanged

`CanonicalStabilizerSettings.cs` was not modified.

```text
acquireConfidence       0.60
sustainConfidence       0.40
acquireSamples          2
lossGraceSeconds        0.10
resetAfterLossSeconds   0.25
minCutoff               1.0
beta                    0.05
derivativeCutoff        1.0
defaultDeltaTimeSeconds 0.05
maximumDeltaTimeSeconds 0.25
```

### Responsive A — moderate

```text
acquireConfidence       0.60
sustainConfidence       0.40
acquireSamples          2
lossGraceSeconds        0.10
resetAfterLossSeconds   0.25
minCutoff               1.5
beta                    0.25
derivativeCutoff        1.0
defaultDeltaTimeSeconds 0.05
maximumDeltaTimeSeconds 0.25
```

### Responsive B — aggressive

```text
acquireConfidence       0.60
sustainConfidence       0.40
acquireSamples          2
lossGraceSeconds        0.10
resetAfterLossSeconds   0.25
minCutoff               2.0
beta                    0.50
derivativeCutoff        1.0
defaultDeltaTimeSeconds 0.05
maximumDeltaTimeSeconds 0.25
```

The responsive candidates reuse the existing `CanonicalPoseStabilizer`; no new smoothing algorithm was introduced.

## Calibration and locomotion invariants

Calibration remains hard-wired to:

```csharp
_calibration.Update(_stabilizedFrame, _lastEvaluationTimeSeconds);
```

No responsive or raw frame is used for calibration.

`EmbodiedLocomotionController.cs` was not modified. Root/support tracking, cadence, and heading still directly consume `runtime.StabilizedFrame`.

Phase 5A behavior is therefore outside this experiment.

## Reset / lifecycle behavior

Responsive A/B are reset and their output frames cleared when:

- the coordinate convention changes and the existing stable stabilizer is reset;
- `ResetCalibration()` explicitly resets the existing stable stabilizer;
- no canonical source can be resolved, in which case responsive frames are cleared alongside the existing raw/stable output frames.

This prevents stale responsive poses and keeps comparison state coherent after lifecycle resets.

## Implementation and verification checkpoints

Implementation commits/checkpoints:

- `fa1f5de64121b0b40b26c940210b57553ffa3c1e` — initial responsive avatar stabilizer candidates.
- `759729273355f96defba2b9120ca3545fff6d7a0` — added dedicated read-only experiment audit workflow.
- `fe7209bff9ac30c3a639d96929c5c60f23a548cd` — preserved the existing stable stabilizer -> calibration ordering literally, with responsive updates after calibration but before avatar selection/solve.
- `418445879e301e95a4d44aca688846032088dc47` — current verified runtime + audit checkpoint before this documentation update.

Current-head audit:

```text
GitHub Actions run: 34857834001
Workflow: Responsive avatar stabilizer audit
Result: SUCCESS
```

Passed checks include:

- old serialized enum meanings stable=0/raw=1 preserved;
- serialized default remains `StabilizedCanonical`;
- existing stable profile unchanged;
- Responsive A exactly `1.5 / 0.25 / 1.0` plus accepted confidence/loss timings;
- Responsive B exactly `2.0 / 0.50 / 1.0` plus accepted confidence/loss timings;
- persistent stabilizer/frame state, no per-update filter/frame construction;
- stable stabilizer -> stable calibration ordering preserved;
- both responsive stabilizers update continuously from raw before avatar selection/solve;
- calibration stable-only;
- locomotion stable-only;
- four-way `AvatarDriveFrame` routing;
- rotation solver and kinematic builder share the selected frame;
- HumanoidRetargeter still consumes `runtime.AvatarDriveFrame`;
- responsive reset/clear lifecycle;
- yellow raw + cyan stable debug paths preserved;
- Presentation Smoothing surface/defaults preserved;
- no queue/history/replay/prediction-like state;
- diff scope contains no scene, `Packages`, `ProjectSettings`, OpenVINO, WebCamCPU, provider, native plugin/model, locomotion, IK-math, `CanonicalPoseStabilizer`, or `CanonicalStabilizerSettings` changes.

A full Unity/webcam visual result cannot be established remotely and is intentionally the next USER gate.

## Files changed by this experiment

Implementation/verification scope before this documentation checkpoint:

- `Assets/GoldenNeedle/Core/Motion/Runtime/MotionEngineRuntime.cs`
- `.github/workflows/responsive-avatar-stabilizer-audit.yml`

This rolling progress document is also updated as the recovery checkpoint.

No USER-owned scene YAML was modified.

## USER QA procedure

Pull the latest `engine/pose-tracking-spike` and let Unity compile normally.

Use one Play session with common settings:

```text
Inference Backend = OpenVINO CPU FP32
Frame Acquisition = WebCamCPU/GetPixels32
Body input = 320x240
full-body framing where practical (~30 FPS capture)
Presentation Smoothing = OFF for the primary comparison
```

Calibrate once. Then live-switch `MotionEngineRuntime -> Avatar Drive Pose Source` among all four modes without recalibrating:

```text
StabilizedCanonical
ResponsiveCanonicalA
ResponsiveCanonicalB
RawCanonical
```

Both responsive candidates are already running continuously in the background, so switching to them should not cause filter cold-start/warm-up behavior.

Use F7 briefly before each comparison segment. For responsive modes it should show the exact profile in the `Avatar source` line.

Compare:

- perceived motion-to-avatar delay;
- whether motion feels essentially as immediate as Raw;
- idle micro-jitter;
- wrist/ankle endpoint jitter;
- fast reaches / arm swings;
- torso response;
- knees/legs when visible;
- snapping or IK instability;
- partial-body loss/recovery;
- left/right and orientation correctness.

Phone-recorded external video is preferred over OBS if recording helps.

Decision criterion:

> Prefer the most stable responsive profile that feels essentially as immediate as Raw.

If neither A nor B is close enough to Raw, do not add another profile without new USER/Orchestrator approval. If one is clearly best, do not change the serialized/default production mode yet; return for USER acceptance and the next decision.

## CONTINUE FROM HERE

**STATUS: IMPLEMENTATION + NON-HARDWARE VERIFICATION COMPLETE. GENUINE USER VISUAL/RUNTIME QA REQUIRED.**

Next action:

1. USER pulls latest `engine/pose-tracking-spike` and confirms Unity compiles.
2. USER performs the one-session four-way visual comparison above with Presentation Smoothing OFF.
3. USER reports which mode best balances near-Raw responsiveness with stability, plus any snapping, endpoint jitter, partial-body, orientation, or left/right regression.
4. Builder/Orchestrator evaluates that evidence. Do not add new tuning profiles or change the default without a new decision.

No merge to `main`. Do not start Phase 6.
