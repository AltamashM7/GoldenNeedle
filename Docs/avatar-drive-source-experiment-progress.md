# Golden Needle — Avatar Drive Source Experiment Progress

This file records the resolved raw-vs-stabilized avatar-drive experiment. It is historical/current-reference documentation; newer tuning work is tracked in `Docs/responsive-avatar-stabilizer-progress.md` and `Docs/responsive-avatar-beta-sweep-progress.md`.

Current status refresh: 2026-09-14.

## Resolution

- Branch: `engine/pose-tracking-spike`.
- Experiment planning checkpoint: `45e20cb7df3ea8acd4c6e2cdbce8450a87487971`.
- Authorized implementation start: `ee0db6d2497177d9b2cdab8887a435879665a4fd`.
- Runtime implementation: **COMPLETE**.
- Non-hardware verification: **PASS**.
- USER raw-vs-stabilized runtime comparison: **COMPLETE**.
- Experiment conclusion: **PASS WITH FINDING**.
- Phase 5A remains **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.

## Motivation

After OpenVINO + WebCamCPU brought the upstream path close to camera cadence, the USER could visibly see the cyan/blue stabilized canonical skeleton trail the yellow raw canonical skeleton slightly.

The experiment isolated whether avatar pose solving could use raw canonical data without changing the accepted stable path used by calibration and locomotion.

## Implemented selector

The initial selector introduced:

```text
StabilizedCanonical = 0
RawCanonical        = 1
```

`StabilizedCanonical` remained the serialized/default behavior.

The selected `AvatarDriveFrame` was wired consistently into:

- `CanonicalRotationSolver`;
- `CanonicalKinematicTargetBuilder`;
- `HumanoidRetargeter` torso/source mapping.

Calibration remained hard-wired to `_stabilizedFrame` and Phase 5A locomotion continued to consume `runtime.StabilizedFrame`.

The yellow raw and cyan stable debug skeletons remained independent and visible.

Primary runtime implementation checkpoint:

`100c3dc927314a2107b62a7acdf46037dc8f6738`.

Successful focused audits:

- run `34852740425` — PASS;
- run `34852975877` — PASS.

## USER result

The USER tested both modes on the optimized OpenVINO + WebCamCPU path.

Result:

- `StabilizedCanonical` remained satisfactory and smooth;
- `RawCanonical` felt dramatically more immediate, effectively instant / near-zero-latency subjectively;
- Raw was slightly less stable, with modest micro-jitter/abruptness;
- no fundamental orientation, left/right, IK or retarget failure was observed in the raw mode.

Conclusion:

**Raw became the latency reference; the stable path remained the stability reference.**

The correct next step was therefore not more inference/readback optimization, but avatar-only smoothing tuning between these two endpoints.

## Preserved architecture rule

This experiment established a lasting boundary:

```text
raw canonical
  -> stable canonical -> calibration + locomotion
  -> selectable avatar-drive path -> retarget/avatar
```

Avatar responsiveness may be tuned independently, but calibration and locomotion must not be silently switched to raw data as a side effect.

## Superseding work

The next experiment added responsive avatar-only One Euro profiles A/B, then a beta-only C/D sweep.

See:

- `Docs/responsive-avatar-stabilizer-progress.md`;
- `Docs/responsive-avatar-beta-sweep-progress.md`;
- `Docs/current-state.md` for current project policy.

## CONTINUE FROM HERE

**STATUS: CLOSED / USER QA COMPLETE.**

Do not reopen the raw-vs-stabilized question as if it were unresolved. Raw remains a preserved latency reference and Stable remains a preserved stability/calibration/locomotion reference.
