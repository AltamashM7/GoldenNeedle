# Golden Needle — Responsive Avatar Beta Sweep Progress

This is the rolling resume point for the next avatar-only stabilization experiment. It follows the completed Responsive A/B USER comparison and narrows the tuning question to **beta only** while restoring `minCutoff` to the stable baseline.

A replacement Web Builder must read this file first, then `Docs/worker-briefs/responsive-avatar-beta-sweep-handoff.md`.

## Current authorization and status

- Branch: `engine/pose-tracking-spike`.
- Exact remote runtime/documentation checkpoint before this new authorization: `02b264efcdb3be84daed29541835447834fd7637`.
- OpenVINO integration: **USER ACCEPTED — PASS**.
- OpenVINO worker/mailbox scheduling: **USER ACCEPTED — PASS**.
- WebCamCPU/GetPixels32 acquisition: **USER ACCEPTED — PASS**.
- Raw-vs-stabilized avatar drive: **USER QA COMPLETE**.
- Responsive A/B experiment: **USER QA COMPLETE — PASS WITH FINDING**.
- Responsive C/D beta-only sweep: **EXPLICITLY USER AUTHORIZED**.
- Phase 5A remains **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## USER finding that motivates this sweep

The USER tested all four existing modes in one calibrated Play session:

```text
StabilizedCanonical
ResponsiveCanonicalA  = 1.5 / 0.25 / 1.0
ResponsiveCanonicalB  = 2.0 / 0.50 / 1.0
RawCanonical
```

USER result:

- A and B both felt more effective / more responsive than the stable baseline;
- A and B still showed minor jitter compared with the stable baseline;
- Raw remains the latency reference and feels essentially instant, but is less stable;
- the USER does not need video analysis for this tuning decision because control feel is directly perceptible.

Interpretation approved by the Orchestrator:

- the responsiveness direction is correct;
- increasing both `minCutoff` and `beta` reduced filtering even at low/idle motion;
- the extra minor jitter may therefore be caused in part by the raised `minCutoff`;
- the next clean experiment must restore `minCutoff = 1.0` and vary only `beta`.

Do not reopen OpenVINO, camera acquisition, presentation smoothing, retarget math, locomotion, or calibration in this task.

## Current runtime architecture to preserve

Current serialized avatar source values are:

```text
StabilizedCanonical  = 0
RawCanonical         = 1
ResponsiveCanonicalA = 2
ResponsiveCanonicalB = 3
```

These meanings must remain unchanged.

Current accepted data flow:

```text
raw canonical
   |
   +-> existing stable stabilizer -> StabilizedCanonical
   |       +-> calibration (stable only)
   |       +-> locomotion  (stable only)
   |
   +-> responsive stabilizer A -> ResponsiveCanonicalA
   +-> responsive stabilizer B -> ResponsiveCanonicalB
   +-> RawCanonical

AvatarDriveFrame selector
   -> rotation solver
   -> kinematic target builder
   -> HumanoidRetargeter source/torso mapping
   -> avatar
```

All responsive stabilizers are avatar-only. Calibration remains literally:

```csharp
_calibration.Update(_stabilizedFrame, _lastEvaluationTimeSeconds);
```

`EmbodiedLocomotionController` remains a consumer of `runtime.StabilizedFrame`.

## Authorized new profiles

Keep all existing Stable/A/B/Raw modes unchanged for reference. Add two new persistent avatar-only candidates:

### Responsive C — beta-only moderate

```text
acquireConfidence       0.60
sustainConfidence       0.40
acquireSamples          2
lossGraceSeconds        0.10
resetAfterLossSeconds   0.25
minCutoff               1.0
beta                    0.25
derivativeCutoff        1.0
defaultDeltaTimeSeconds 0.05
maximumDeltaTimeSeconds 0.25
```

### Responsive D — beta-only aggressive

```text
acquireConfidence       0.60
sustainConfidence       0.40
acquireSamples          2
lossGraceSeconds        0.10
resetAfterLossSeconds   0.25
minCutoff               1.0
beta                    0.50
derivativeCutoff        1.0
defaultDeltaTimeSeconds 0.05
maximumDeltaTimeSeconds 0.25
```

The purpose is to test whether stable-like low-motion behavior can be retained while higher beta restores near-Raw responsiveness during deliberate movement.

## Serialization requirement

Do not renumber or repurpose any existing enum member.

Required extension:

```text
StabilizedCanonical   = 0
RawCanonical          = 1
ResponsiveCanonicalA  = 2
ResponsiveCanonicalB  = 3
ResponsiveCanonicalC  = 4
ResponsiveCanonicalD  = 5
```

`StabilizedCanonical` must remain the serialized default.

## Implementation requirements

- Add one persistent `CanonicalPoseStabilizer` and one persistent `CanonicalPoseFrame` for C.
- Add one persistent `CanonicalPoseStabilizer` and one persistent `CanonicalPoseFrame` for D.
- Construct them once, not per update.
- Feed both from `_rawCanonicalFrame` every update whether selected or not, so live switching is warm.
- Keep the existing stable stabilizer -> calibration ordering adjacent and unchanged.
- Run C/D only after stable calibration update, like the current A/B candidates.
- Extend `AvatarDriveFrame` and `AvatarDriveSourceLabel` to route C/D.
- Use the selected frame consistently for rotation solver, kinematic target builder, and existing `HumanoidRetargeter` source/torso mapping.
- Reset/clear C/D alongside A/B on coordinate-convention change, `ResetCalibration()`, and source-unavailable lifecycle paths.
- No new smoothing algorithm. Reuse `CanonicalPoseStabilizer` exactly.
- Do not add extra debug skeleton overlays. Keep yellow Raw and cyan Stable only.
- F7 should identify C and D with their exact profile values.

## Hard preservation boundaries

Do not change:

- Stable profile `1.0 / 0.05 / 1.0`.
- Existing A profile `1.5 / 0.25 / 1.0`.
- Existing B profile `2.0 / 0.50 / 1.0`.
- Raw semantics.
- `CanonicalPoseStabilizer` algorithm.
- `CanonicalStabilizerSettings` defaults.
- calibration thresholds/math.
- locomotion/support-foot/cadence/heading logic.
- Humanoid retarget/IK math.
- Presentation Smoothing implementation/defaults.
- OpenVINO worker/native/provider code.
- WebCamCPU/GetPixels32 acquisition.
- scenes, `Packages`, `ProjectSettings`, models, native plugins.
- Phase 5A acceptance or Phase 6 state.

## Non-hardware verification expectations

Before stopping for USER QA, verify at minimum:

- enum serialization values 0-3 are preserved and C/D are 4/5;
- serialized default remains Stable;
- exact C profile `1.0 / 0.25 / 1.0`;
- exact D profile `1.0 / 0.50 / 1.0`;
- Stable/A/B settings unchanged;
- C/D filters and frames are persistent/preallocated;
- no per-update filter/frame construction;
- stable stabilizer -> calibration ordering preserved;
- A/B/C/D all warm continuously from raw before avatar selection/solve;
- calibration remains stable-only;
- locomotion remains stable-only;
- six-way source routing is correct;
- same selected frame drives rotation and kinematic targets;
- `HumanoidRetargeter` still consumes `runtime.AvatarDriveFrame`;
- lifecycle reset/clear covers C/D;
- no queue/history/replay/prediction state introduced;
- diff scope remains narrow.

A dedicated narrow audit workflow/tool may be added if useful, but do not perform unnecessary Unity scene automation.

## USER QA target after implementation

Use one Play session with:

```text
OpenVINO CPU FP32
WebCamCPU/GetPixels32
320x240 body input
full-body framing where practical
Presentation Smoothing OFF
```

Calibrate once, then primarily compare:

```text
StabilizedCanonical
ResponsiveCanonicalC
ResponsiveCanonicalD
RawCanonical
```

A/B may remain available as secondary references but are not required for every pass.

USER should judge:

- perceived motion-to-avatar delay;
- idle micro-jitter;
- wrist/ankle endpoint jitter;
- fast reach/arm response;
- torso response;
- knees/legs when visible;
- snapping/IK instability;
- partial-body recovery;
- left/right/orientation correctness.

Decision target:

> choose the most stable filtered mode that still feels essentially as immediate as Raw.

Do not automatically make any candidate the default after QA; return for USER/Orchestrator acceptance first.

## CONTINUE FROM HERE

**STATUS: USER AUTHORIZED. IMPLEMENTATION NOT STARTED.**

Start from current remote `engine/pose-tracking-spike` HEAD. Read the companion handoff. Implement only the C/D beta-only sweep above, verify it, checkpoint/push coherent recovery points, update this rolling progress file with exact SHAs, and stop only when genuine USER visual/runtime QA is required, a real blocker appears, or execution-limit risk requires handoff.

No merge to `main`. Do not start Phase 6.
