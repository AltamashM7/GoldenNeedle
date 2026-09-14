# Golden Needle — Responsive Avatar Beta Sweep Progress

This file records the current resolution of the USER-authorized avatar-only beta sweep.

Current status refresh: 2026-09-14.

## Current status

- Branch: `engine/pose-tracking-spike`.
- Implementation start HEAD: `58f15d72f1dace179a09533fddd8eca089b8eef2`.
- C/D runtime implementation checkpoint: `a58014c6e301395d2334b59373ad2fba540b1be9`.
- Verified runtime/audit checkpoint: `4a20b6a00bb86a023cab764407ec8c148356529d`.
- Final pre-documentation USER-QA checkpoint: `83ab2d15206e4563c3302cdcdccc2121ceddd741`.
- Focused audit run `34862536608`: **SUCCESS**.
- Responsive C/D implementation: **COMPLETE**.
- Non-hardware verification: **PASS**.
- Final C/D winner/default selection: **DEFERRED BY USER**.
- Motion-engine smoothing fine-tuning: **PRESERVED FOR LATER; NOT A CURRENT BLOCKER**.
- Phase 5A remains **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.
- No merge to `main` without explicit USER approval.

Older wording in this file that required immediate USER C/D comparison before development could continue is superseded by the decision below: the tuning system is preserved, but final fine-tuning is intentionally deferred.

## Preserved serialized avatar source meanings

```text
StabilizedCanonical   = 0
RawCanonical          = 1
ResponsiveCanonicalA  = 2
ResponsiveCanonicalB  = 3
ResponsiveCanonicalC  = 4
ResponsiveCanonicalD  = 5
```

`StabilizedCanonical` remains the serialized/default avatar source. Existing enum values were not reordered, renamed or repurposed.

## Preserved profiles

```text
Stable  = 1.0 / 0.05 / 1.0
A       = 1.5 / 0.25 / 1.0
B       = 2.0 / 0.50 / 1.0
C       = 1.0 / 0.25 / 1.0
D       = 1.0 / 0.50 / 1.0
Raw     = no canonical positional filtering
```

All filtered responsive candidates retain the accepted confidence/loss/timing values:

```text
acquireConfidence       0.60
sustainConfidence       0.40
acquireSamples          2
lossGraceSeconds        0.10
resetAfterLossSeconds   0.25
defaultDeltaTimeSeconds 0.05
maximumDeltaTimeSeconds 0.25
```

## Architecture

C and D each use their own persistent/preallocated `CanonicalPoseStabilizer` and `CanonicalPoseFrame`.

All responsive filters run continuously from the raw canonical frame so live switching does not cold-start a selected filter.

The stable path remains literally authoritative for calibration:

```csharp
_stabilizer.Stabilize(_rawCanonicalFrame, _stabilizedFrame, _lastEvaluationTimeSeconds);
_calibration.Update(_stabilizedFrame, _lastEvaluationTimeSeconds);
```

Only after stable calibration do A/B/C/D update.

`AvatarDriveFrame` remains the single selected-source authority for:

- `CanonicalRotationSolver`;
- `CanonicalKinematicTargetBuilder`;
- `HumanoidRetargeter` source/torso mapping.

Phase 5A locomotion continues to consume `runtime.StabilizedFrame` directly.

No queue/history/replay/prediction/catch-up system was added.

## Why C/D were added

USER testing of the earlier A/B sweep found:

- A and B are more responsive/effective than Stable;
- A and B still have minor jitter relative to Stable.

The beta-only sweep therefore restored `minCutoff = 1.0` and varied beta only:

```text
C = 1.0 / 0.25 / 1.0
D = 1.0 / 0.50 / 1.0
```

The intended hypothesis is:

- keep stronger low-motion/idle smoothing from the stable minimum cutoff;
- increase responsiveness during deliberate motion through beta.

## USER decision after implementation

The USER decided that final smoothing fine-tuning does **not** need to block ongoing development.

The current requirement is to preserve the tuning system so the responsiveness/stability balance can be revisited later.

Therefore:

- no C/D winner is declared yet;
- no new serialized/default avatar source is selected;
- Stable/Raw/A/B/C/D remain available for engineering comparison;
- Raw remains the subjective latency reference;
- Stable remains the stability/calibration/locomotion reference;
- final smoothing intensity/profile tuning is deferred.

A future cleanup may replace the experimental preset list with a cleaner configurable smoothing profile or intensity control once the desired range is known. Do **not** perform that cleanup now merely for aesthetics.

## Current engineering conclusion

The motion-engine optimization milestone is considered complete enough to move forward:

```text
OpenVINO CPU FP32          strong best-tested backend
WebCamCPU/GetPixels32      accepted acquisition path
fresh pose rate            near 30 Hz camera cadence in healthy full-body test
Raw avatar drive           near-instant subjective response
Filtered avatar drive      adjustable via preserved Stable/A/B/C/D profiles
final filter tuning        intentionally deferred
```

The existence of tunable avatar-drive smoothing is now a preserved capability, not an unresolved gate.

## Boundaries that remain locked

Do not use later avatar smoothing work to change:

- stable calibration input;
- stable locomotion input;
- OpenVINO scheduling/mailbox architecture;
- WebCamCPU camera acquisition;
- canonical coordinate semantics;
- Phase 4 retarget/IK math;
- Presentation Smoothing semantics;
- partial-body behavior;
- serialized enum meanings without migration planning.

## CONTINUE FROM HERE

**STATUS: IMPLEMENTED + VERIFIED; FINAL FINE-TUNING DEFERRED BY USER.**

No further smoothing experiment is required before normal development continues.

Current next-step authority is `Docs/current-state.md`: pause motion-engine optimization and wait for the USER's pre-locomotion prerequisite list before returning to Phase 5A.

Do not merge to `main`. Do not start Phase 6.
