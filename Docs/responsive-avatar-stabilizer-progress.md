# Golden Needle — Responsive Avatar Stabilizer Progress

This file records the resolved A/B responsive avatar-only stabilizer experiment that followed the raw-vs-stabilized comparison. Newer beta-only tuning is tracked in `Docs/responsive-avatar-beta-sweep-progress.md`.

Current status refresh: 2026-09-14.

## Resolution

- Branch: `engine/pose-tracking-spike`.
- Authorized implementation start: `c07cea3801a11ba100d5a465935906895debbf63`.
- Responsive A/B implementation: **COMPLETE**.
- Focused non-hardware verification: **PASS**.
- USER A/B runtime comparison: **COMPLETE**.
- Experiment conclusion: **PASS WITH FINDING**.
- Phase 5A remains **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.

## Architecture preserved

The experiment added two avatar-only persistent `CanonicalPoseStabilizer` paths while leaving the original stable path unchanged for calibration and locomotion.

```text
raw canonical
   +-> Stable  -> calibration + locomotion
   +-> A       -> optional avatar drive
   +-> B       -> optional avatar drive
   +-> Raw     -> optional avatar drive
```

Stable ordering remained:

```text
_stabilizer.Stabilize(raw -> stable)
_calibration.Update(stable)
```

Only after stable calibration do the responsive avatar-only filters update and feed the avatar source selector.

The selected `AvatarDriveFrame` remains the one authority for rotation solving, kinematic targets and HumanoidRetargeter torso/source mapping.

## Preserved serialized meanings

```text
StabilizedCanonical   = 0
RawCanonical          = 1
ResponsiveCanonicalA  = 2
ResponsiveCanonicalB  = 3
```

Existing values were not reordered or repurposed.

## Profiles tested

```text
Stable  1.0 / 0.05 / 1.0
A       1.5 / 0.25 / 1.0
B       2.0 / 0.50 / 1.0
Raw     unfiltered positional canonical frame
```

All filtered profiles retain the accepted confidence/loss timing behavior.

Primary implementation checkpoints:

- `fa1f5de64121b0b40b26c940210b57553ffa3c1e` — initial A/B candidates;
- `fe7209bff9ac30c3a639d96929c5c60f23a548cd` — preserved stable->calibration ordering literally;
- `418445879e301e95a4d44aca688846032088dc47` — verified runtime/audit state.

Focused audit:

`34857834001` — **SUCCESS**.

## USER result

The USER reported:

- A and B are both more effective/responsive than Stable;
- A and B still contain minor jitter compared with Stable;
- the direction toward more responsive filtering is correct;
- the remaining question is how to keep more Stable-like low-motion steadiness while preserving high-motion response.

Interpretation:

Raising both `minCutoff` and `beta` improved response, but likely allowed more low-motion variation through as well.

The next experiment therefore isolated beta while restoring `minCutoff = 1.0`.

## Superseding beta-only sweep

New profiles were authorized:

```text
C = 1.0 / 0.25 / 1.0
D = 1.0 / 0.50 / 1.0
```

The purpose is to preserve stronger slow/idle smoothing while increasing response during deliberate/faster movement.

See `Docs/responsive-avatar-beta-sweep-progress.md`.

## Current policy

A/B remain preserved as useful reference presets. They are not declared final production defaults.

Calibration and locomotion remain on the accepted Stable frame regardless of avatar-drive selection.

## CONTINUE FROM HERE

**STATUS: A/B USER QA COMPLETE; EXPERIMENT SUPERSEDED BY THE BETA-ONLY C/D SWEEP.**

Current overall project policy is in `Docs/current-state.md`.
