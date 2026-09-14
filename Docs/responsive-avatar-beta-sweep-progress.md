# Golden Needle — Responsive Avatar Beta Sweep Progress

This is the rolling resume point for the USER-authorized avatar-only beta sweep. Read this file first, then `Docs/worker-briefs/responsive-avatar-beta-sweep-handoff.md`.

## Current authorization and status

- Branch: `engine/pose-tracking-spike`.
- Exact remote branch HEAD when this implementation began: `58f15d72f1dace179a09533fddd8eca089b8eef2`.
- Runtime implementation checkpoint: `a58014c6e301395d2334b59373ad2fba540b1be9` — `feat: add responsive avatar beta sweep candidates`.
- OpenVINO integration/scheduling and WebCamCPU/GetPixels32 remain USER ACCEPTED and out of scope.
- Responsive A/B USER QA is complete; this C/D beta-only sweep is explicitly USER authorized.
- Phase 5A remains NOT USER ACCEPTED.
- Phase 6 remains NOT STARTED.
- No merge to `main` without explicit USER approval.

## Preserved serialized avatar source meanings

```text
StabilizedCanonical   = 0
RawCanonical          = 1
ResponsiveCanonicalA  = 2
ResponsiveCanonicalB  = 3
ResponsiveCanonicalC  = 4
ResponsiveCanonicalD  = 5
```

`StabilizedCanonical` remains the serialized/default avatar source. Existing values 0-3 were not reordered, renamed, or repurposed.

## Profiles

Existing profiles remain unchanged:

```text
Stable: 1.0 / 0.05 / 1.0
A:      1.5 / 0.25 / 1.0
B:      2.0 / 0.50 / 1.0
Raw:    no canonical positional filtering
```

New beta-only candidates implemented exactly as authorized:

```text
C: 1.0 / 0.25 / 1.0
D: 1.0 / 0.50 / 1.0
```

Both C and D use the same accepted confidence/loss/timing values:

```text
acquireConfidence       0.60
sustainConfidence       0.40
acquireSamples          2
lossGraceSeconds        0.10
resetAfterLossSeconds   0.25
defaultDeltaTimeSeconds 0.05
maximumDeltaTimeSeconds 0.25
```

## Implementation checkpoint

Only `Assets/GoldenNeedle/Core/Motion/Runtime/MotionEngineRuntime.cs` was changed for the runtime implementation.

Implemented:

- appended `ResponsiveCanonicalC = 4` and `ResponsiveCanonicalD = 5`;
- added fixed C/D profile constants and F7/source labels;
- added one persistent `CanonicalPoseStabilizer` instance for C and one for D;
- added one persistent/preallocated `CanonicalPoseFrame` for C and one for D;
- constructed C/D once in `Awake()` using the existing `CanonicalPoseStabilizer` and existing settings factory;
- kept the accepted Stable stabilizer -> calibration ordering literally adjacent:

```csharp
_stabilizer.Stabilize(_rawCanonicalFrame, _stabilizedFrame, _lastEvaluationTimeSeconds);
_calibration.Update(_stabilizedFrame, _lastEvaluationTimeSeconds);
```

- A/B/C/D then all update continuously from `_rawCanonicalFrame` before avatar source selection/solve;
- extended `AvatarDriveFrame` to six-way Stable/Raw/A/B/C/D routing;
- extended `AvatarDriveSourceLabel` with exact C/D profile labels;
- kept rotation solver and kinematic-target builder on the single selected `avatarDriveFrame` authority;
- extended the existing responsive reset helper so A/B/C/D filters reset and A/B/C/D frames clear together;
- source-unavailable clearing now includes C/D frames;
- `ResetCalibration()` and coordinate-convention reset continue to use the same responsive reset helper.

No per-frame stabilizer or pose-frame construction was introduced.

## Preserved hard boundaries

The implementation did not edit:

- `CanonicalPoseStabilizer`;
- `CanonicalStabilizerSettings`;
- calibration math/settings;
- `EmbodiedLocomotionController`;
- `HumanoidRetargeter` or IK/retarget math;
- Presentation Smoothing;
- OpenVINO/WebCamCPU/provider/native/model/camera code;
- scenes;
- `Packages`;
- `ProjectSettings`;
- Phase 6.

Current inspected downstream invariants before implementation were still:

```text
Calibration -> _stabilizedFrame only
Locomotion root/cadence/heading -> runtime.StabilizedFrame only
HumanoidRetargeter source/torso -> runtime.AvatarDriveFrame
```

## Verification state

Implementation inspection is complete. Focused repeatable non-hardware audit is the next unfinished action. The audit must verify enum serialization, all Stable/A/B/C/D profiles, stable-calibration adjacency, continuous warm A/B/C/D updates, six-way routing, stable-only calibration/locomotion, retarget source consistency, lifecycle reset/clear behavior, debug/Presentation Smoothing preservation, no queue/history/prediction state, and narrow diff scope.

## Remaining risk / USER gate

Static verification cannot determine whether C or D gives the desired subjective latency/stability tradeoff. USER webcam/Unity visual QA is required after non-hardware verification passes.

## CONTINUE FROM HERE

Current recovery state after implementation checkpoint:

`a58014c6e301395d2334b59373ad2fba540b1be9`

Next authorized action:

Add/run a focused non-hardware audit for this exact beta-only sweep, update this file with the verified SHA/workflow result, then stop for genuine USER visual/runtime comparison of Stable vs C vs D vs Raw. Do not tune further, change defaults, merge `main`, or start Phase 6.
