# Golden Needle — Responsive Avatar Beta Sweep Progress

This is the rolling resume point for the USER-authorized avatar-only beta sweep. Read this file first, then `Docs/worker-briefs/responsive-avatar-beta-sweep-handoff.md`.

## Current authorization and status

- Branch: `engine/pose-tracking-spike`.
- Exact remote branch HEAD when this implementation began: `58f15d72f1dace179a09533fddd8eca089b8eef2`.
- Runtime implementation checkpoint: `a58014c6e301395d2334b59373ad2fba540b1be9` — `feat: add responsive avatar beta sweep candidates`.
- Implementation recovery-doc checkpoint: `b3c099672c0ab6b85cbacc15798b54bf380c84f9`.
- Verified pre-final-documentation checkpoint: `4a20b6a00bb86a023cab764407ec8c148356529d` — includes runtime implementation + progress checkpoint + dedicated read-only audit workflow.
- Focused audit run: `34862536608` — **SUCCESS**.
- Responsive C/D implementation + focused non-hardware verification: **COMPLETE**.
- Current gate: **genuine USER visual/runtime QA**.
- OpenVINO integration/scheduling and WebCamCPU/GetPixels32 remain USER ACCEPTED and out of scope.
- Responsive A/B USER QA is complete; this C/D beta-only sweep remains experimental pending USER comparison.
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

New beta-only candidates are exactly:

```text
C: 1.0 / 0.25 / 1.0
D: 1.0 / 0.50 / 1.0
```

Both C and D retain the accepted confidence/loss/timing values:

```text
acquireConfidence       0.60
sustainConfidence       0.40
acquireSamples          2
lossGraceSeconds        0.10
resetAfterLossSeconds   0.25
defaultDeltaTimeSeconds 0.05
maximumDeltaTimeSeconds 0.25
```

## Final implementation architecture

Runtime implementation changed only `Assets/GoldenNeedle/Core/Motion/Runtime/MotionEngineRuntime.cs`.

Implemented:

- appended `ResponsiveCanonicalC = 4` and `ResponsiveCanonicalD = 5`;
- added exact C/D fixed profile constants and F7/source labels;
- added one persistent `CanonicalPoseStabilizer` and one persistent/preallocated `CanonicalPoseFrame` for each of C and D;
- C/D are constructed once in `Awake()` via the existing `CanonicalPoseStabilizer` and existing responsive settings factory;
- the accepted stable ordering remains literally adjacent and unchanged:

```csharp
_stabilizer.Stabilize(_rawCanonicalFrame, _stabilizedFrame, _lastEvaluationTimeSeconds);
_calibration.Update(_stabilizedFrame, _lastEvaluationTimeSeconds);
```

- after that stable calibration update, A/B/C/D all run continuously from `_rawCanonicalFrame` every runtime update before avatar selection/solve, so live switching is warm;
- `AvatarDriveFrame` now routes Stable/Raw/A/B/C/D through one authority;
- rotation solver and kinematic-target builder still consume the same local `avatarDriveFrame` selected from that authority;
- `HumanoidRetargeter` was not modified and still consumes `runtime.AvatarDriveFrame` for source/torso mapping;
- C/D were added to the same responsive reset helper used on coordinate-convention change and `ResetCalibration()`;
- source-unavailable clearing now includes C/D output frames;
- no per-frame stabilizer or pose-frame construction was introduced;
- no queues/history/replay/prediction/catch-up logic was introduced.

## Preserved boundaries

No changes were made to:

- `CanonicalPoseStabilizer` algorithm;
- `CanonicalStabilizerSettings` defaults;
- Stable/A/B profile values;
- Raw semantics;
- calibration math/settings;
- `EmbodiedLocomotionController` or Phase 5A locomotion semantics;
- `HumanoidRetargeter` / IK / retarget math;
- Presentation Smoothing implementation/defaults;
- OpenVINO/WebCamCPU/provider/native/model/camera code;
- existing yellow Raw / cyan Stable debug skeleton paths;
- scenes / USER scene YAML;
- `Packages`;
- `ProjectSettings`;
- Phase 6.

Calibration remains stable-only:

```csharp
_calibration.Update(_stabilizedFrame, _lastEvaluationTimeSeconds);
```

Locomotion root tracking, cadence and heading remain direct consumers of `runtime.StabilizedFrame`.

## Focused non-hardware verification

Dedicated workflow:

`.github/workflows/responsive-avatar-beta-sweep-audit.yml`

Workflow run:

`34862536608` — **SUCCESS**

All workflow steps passed:

1. checkout exact branch head;
2. validate beta-sweep invariants;
3. validate beta-sweep diff scope;
4. show focused beta-sweep diff;
5. cleanup/complete.

The audit verified:

- enum values 0-3 preserved; C=4 and D=5;
- serialized default remains Stable;
- stable defaults remain `1.0 / 0.05 / 1.0`;
- A remains `1.5 / 0.25 / 1.0`;
- B remains `2.0 / 0.50 / 1.0`;
- C is exactly `1.0 / 0.25 / 1.0`;
- D is exactly `1.0 / 0.50 / 1.0`;
- C/D confidence/loss/timing settings remain accepted values;
- A/B/C/D stabilizers and frames are persistent/preallocated;
- no stabilizer/frame construction occurs in `Update()`;
- Stable stabilization -> calibration literal adjacency is preserved;
- A/B/C/D all run from raw after stable calibration and before avatar source selection;
- calibration remains stable-only;
- locomotion remains stable-only;
- six-way `AvatarDriveFrame` routing is present;
- rotation and kinematic targets share one selected frame;
- `HumanoidRetargeter` still reads `runtime.AvatarDriveFrame`;
- C/D reset and clear with A/B on lifecycle resets;
- source-unavailable clearing includes A/B/C/D;
- F7 continues to use `AvatarDriveSourceLabel`, with exact C/D labels;
- raw/stable debug paths remain present;
- Presentation Smoothing surface/defaults remain unchanged;
- no queue/history/replay/prediction-like runtime state was introduced;
- experiment diff scope contains only the runtime file, this progress file and the dedicated audit workflow.

No unnecessary automated Unity scene run was performed. Static/CI verification cannot substitute for USER webcam/visual QA.

## USER QA procedure

Use one Play session with:

```text
Inference Backend = OpenVINO CPU FP32
Frame Acquisition = WebCamCPU/GetPixels32
Body input = 320x240
full-body framing where practical
Presentation Smoothing = OFF
```

Calibrate once. Then live-switch `MotionEngineRuntime -> Avatar Drive Pose Source`, primarily comparing:

```text
StabilizedCanonical
ResponsiveCanonicalC
ResponsiveCanonicalD
RawCanonical
```

A/B remain available as optional secondary references.

F7 should identify C/D as:

```text
ResponsiveCanonicalC (1.0/0.25/1.0)
ResponsiveCanonicalD (1.0/0.50/1.0)
```

Judge primarily:

- perceived motion-to-avatar delay;
- whether C/D feel essentially as immediate as Raw;
- idle micro-jitter;
- wrist/ankle endpoint jitter;
- fast reach/arm response;
- torso response;
- knees/legs when visible;
- snapping or IK instability;
- partial-body loss/recovery;
- left/right and orientation correctness.

Decision target: choose the most stable filtered mode that still feels essentially as immediate as Raw.

Do not automatically change the production/default source after QA. Return the USER result for the next decision. Do not invent additional tuning profiles without new authorization.

## Remaining risk

The only intended unresolved question is subjective/runtime behavior on USER hardware: whether C or D provides the best response/stability tradeoff. That cannot be resolved by static inspection or CI.

## CONTINUE FROM HERE

**IMPLEMENTATION + NON-HARDWARE VERIFICATION COMPLETE. STOP AT USER QA GATE.**

Verified code/audit checkpoint:

`4a20b6a00bb86a023cab764407ec8c148356529d`

Next action is USER visual/runtime comparison of Stable vs C vs D vs Raw using the procedure above. After receiving USER observations, record the result and return to the Orchestrator for acceptance/next decision.

Do not retune further, change the default, merge `main`, or start Phase 6 without new authorization.
