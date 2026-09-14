# Golden Needle — Responsive Avatar Stabilizer Progress

This is the rolling resume point for the avatar-only responsive stabilization experiment that follows the accepted raw-vs-stabilized avatar-drive A/B.

This file and `Docs/worker-briefs/responsive-avatar-stabilizer-handoff.md` are the newest authority for this experiment. They supersede the older raw-vs-stabilized handoff wherever the two conflict.

## Current authorization and status

- Branch: `engine/pose-tracking-spike`.
- Exact remote branch HEAD before this experiment was authorized: `4aa23cc6479d8269a6e667e5599aeeb4b7247184`.
- OpenVINO Unity integration: **USER ACCEPTED — PASS**.
- OpenVINO scheduling optimization: **USER ACCEPTED — PASS**.
- WebCamCPU/GetPixels32 acquisition path: **USER ACCEPTED — PASS**.
- Raw-vs-stabilized avatar-drive selector implementation: **COMPLETE**.
- Raw-vs-stabilized USER QA: **COMPLETE — raw is decisively more responsive, with modest extra instability/micro-jitter**.
- Responsive avatar-only stabilization experiment: **EXPLICITLY USER AUTHORIZED on 2026-09-14**.
- Current gate: implementation + non-hardware verification, then USER visual/runtime QA.
- Phase 5A remains **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.
- No merge to `main` without explicit USER approval.

## Accepted upstream best path — do not reopen

The current best upstream motion path is:

```text
WebCamTexture
  -> WebCamCPU/GetPixels32 reusable CPU acquisition
  -> reusable 320x240 preparation
  -> accepted bounded OpenVINO latest-frame mailbox
  -> one persistent OpenVINO CPU FP32 worker
  -> pose result
  -> raw canonical
```

Representative full-body USER evidence from the current best path is approximately:

```text
camera capture:        ~28.6-30.3 FPS
fresh pose results:    ~26.7-29.3/s
CPU acquisition total: ~3.9 ms
OpenVINO processing:   commonly ~25-35 ms
frame->result:         commonly ~33-62 ms
```

Do not spend this task changing camera acquisition, OpenVINO, native ABI, body input size, worker/mailbox scheduling, detector logic, or provider semantics.

## Accepted raw-vs-stabilized finding

The USER performed the authorized A/B with phone video so OBS did not add load.

USER result:

- `StabilizedCanonical` is satisfactory and visually stable.
- `RawCanonical` feels effectively instant / dramatically more responsive.
- `RawCanonical` is slightly less stable, with modest additional micro-jitter/abruptness.
- No fundamental orientation, left/right, retarget, or IK failure was reported in raw mode.

Interpretation:

- upstream acquisition/inference latency is no longer the dominant practical problem;
- the existing canonical stabilizer is now the visible latency/stability tradeoff;
- raw should be kept as the maximum-responsiveness reference, not automatically promoted to the final production default;
- the next goal is a middle path: near-raw responsiveness with materially better idle/endpoint stability.

## Current canonical endpoints

The Lab already has:

```text
RawCanonicalFrame          = yellow debug skeleton
StabilizedFrame            = cyan/blue debug skeleton
```

Current production stabilization defaults remain:

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

The existing One Euro implementation computes an adaptive cutoff from:

```text
cutoff = minCutoff + beta * abs(filtered derivative)
```

The current low `beta` is intentionally conservative and was accepted when the pipeline ran much slower.

## Required architecture for this experiment

Do **not** retune the existing canonical stabilizer globally.

Keep the current stable canonical path unchanged because calibration and locomotion depend on it.

Required conceptual split:

```text
provider result
   -> raw canonical -----------------------------------------------+
       |                                                          |
       +-> existing canonical stabilizer                           |
       |      -> StableCanonical                                   |
       |          +-> calibration                                  |
       |          +-> locomotion / support / cadence / heading     |
       |                                                          |
       +-> responsive avatar stabilizer A -> ResponsiveCanonicalA  |
       |                                                          |
       +-> responsive avatar stabilizer B -> ResponsiveCanonicalB  |
       |                                                          |
       +----------------------------------------------------------> RawCanonical
                                                                  |
Avatar Drive Source selector <------------------------------------+
   -> StableCanonical        [existing/default]
   -> ResponsiveCanonicalA   [experimental]
   -> ResponsiveCanonicalB   [experimental]
   -> RawCanonical           [latency reference]
   -> rotation solver
   -> kinematic targets / IK
   -> HumanoidRetargeter torso/source mapping
   -> avatar
```

Both responsive stabilizers should run continuously from the raw canonical frame, even when not selected. That allows live source switching in one Play session without a cold-start/warm-up discontinuity.

## Initial responsive profiles

These are **experimental starting profiles**, not final production constants.

Keep confidence/loss semantics identical to the current accepted stabilizer. Change only the One Euro positional response for the two new avatar-only candidates.

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

Retain the existing values for:

```text
acquireConfidence       0.60
sustainConfidence       0.40
acquireSamples          2
lossGraceSeconds        0.10
resetAfterLossSeconds   0.25
defaultDeltaTimeSeconds 0.05
maximumDeltaTimeSeconds 0.25
```

Do not change the existing stable profile `1.0 / 0.05 / 1.0`.

## Required implementation invariants

The implementation must preserve all of the following:

- `StabilizedCanonical` remains the serialized/default avatar source.
- `RawCanonical` remains available as the maximum-responsiveness reference.
- Add two clearly named experimental avatar-only responsive sources.
- Calibration continues to call `_calibration.Update(_stabilizedFrame, ...)` only.
- Locomotion continues to consume `runtime.StabilizedFrame` only.
- Existing stable canonical frame and its settings remain unchanged.
- Responsive A/B are separate `CanonicalPoseStabilizer` instances with separate preallocated `CanonicalPoseFrame` outputs.
- No per-frame allocations in the new stabilization path.
- Rotation solver, kinematic-target builder, and HumanoidRetargeter torso/source mapping must all consume the exact same selected `AvatarDriveFrame`.
- Yellow raw and cyan stable debug skeletons remain unchanged.
- Presentation smoothing code/settings remain unchanged.
- OpenVINO, WebCamCPU, camera acquisition, provider, canonical mapping, calibration math, IK math, partial-body semantics and Phase 5A locomotion remain untouched.
- No queue/history/replay/prediction/extrapolation is introduced.
- No scene YAML edits merely to select a test mode.
- No merge to `main`.
- No Phase 6 work.

## Recommended runtime surface

Extend the existing `AvatarDrivePoseSource` selector rather than adding a second selector.

Expected modes:

```text
StabilizedCanonical
ResponsiveCanonicalA
ResponsiveCanonicalB
RawCanonical
```

Expose enough read-only runtime information for F7 to clearly show the effective source and, for responsive modes, the profile parameters or a concise label such as:

```text
Avatar source: ResponsiveCanonicalA (1.5 / 0.25 / 1.0)
Avatar source: ResponsiveCanonicalB (2.0 / 0.50 / 1.0)
```

Do not require the USER to modify code or scene YAML to switch modes.

## Non-hardware verification requirements

Before stopping for USER QA, verify at minimum:

1. default serialized source remains `StabilizedCanonical`;
2. existing stable stabilizer settings remain byte-for-byte/semantically unchanged;
3. calibration remains hard-wired to stable canonical;
4. locomotion still directly consumes stable canonical and is not routed through the avatar source selector;
5. responsive A/B stabilizers consume raw canonical and produce separate frames;
6. responsive A/B run continuously every runtime update so live switching is warm;
7. source selector routes Stable/A/B/Raw correctly;
8. selected source feeds rotation solving and kinematic targets consistently;
9. HumanoidRetargeter uses the same selected source frame;
10. raw/stable debug skeletons remain intact;
11. presentation smoothing was not modified;
12. no OpenVINO/WebCamCPU/native/camera/scene/Package/ProjectSettings changes;
13. no additional queues or frame history;
14. cleanup/reset paths reset both responsive stabilizers and frames on coordinate-convention changes and explicit calibration reset where appropriate.

Use narrow repeatable tests/static assertions where useful. Do not perform unnecessary full Unity scene automation. Hardware behavior is a USER gate.

## USER QA target

After implementation, the USER should be able to stay in one calibrated Play session with Presentation Smoothing OFF and live-switch among:

```text
StabilizedCanonical
ResponsiveCanonicalA
ResponsiveCanonicalB
RawCanonical
```

The USER will compare:

- perceived motion-to-avatar delay;
- fast arms/reaches;
- torso response;
- knees/legs when visible;
- idle micro-jitter;
- endpoint jitter;
- snapping/IK instability;
- partial-body loss/recovery;
- orientation and left/right correctness.

The desired winner is the most stable mode that feels essentially as immediate as Raw.

If A/B are both clearly behind Raw, do not silently invent additional tuning profiles without USER approval. Report the evidence and stop.

If one responsive profile is clearly close to Raw while materially more stable, stop for USER acceptance before changing defaults.

## Recovery/checkpoint policy

Checkpoints are recovery markers, not approval gates.

The Builder should continue through implementation and non-hardware verification. At every coherent recovery point:

- commit and push;
- update this file with the exact ending SHA;
- record what is complete;
- record the next unfinished action under `CONTINUE FROM HERE`.

Stop only for:

- genuine USER visual/runtime QA;
- a real blocker needing a decision;
- execution-limit risk that requires handoff.

## CONTINUE FROM HERE

Starting remote runtime state for this experiment:

`4aa23cc6479d8269a6e667e5599aeeb4b7247184`

Next authorized action:

Implement the avatar-only responsive stabilizer A/B paths exactly as described above, preserve the existing stable path for calibration/locomotion, verify routing/reset/boundedness/scope, then stop for USER visual/runtime A/B/C/D QA.
