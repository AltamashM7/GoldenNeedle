# Golden Needle — Orchestrator Handoff

Handoff refresh: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## 1. First actions for the next Orchestrator

Before changing anything:

1. verify the live remote `engine/pose-tracking-spike` HEAD;
2. read `Docs/current-state.md` as the primary current authority;
3. read `Docs/optimization-orchestrator-handoff.md` for optimization history and preserved invariants;
4. read `Docs/decisions.md` for current/superseded architectural decisions;
5. inspect the current source before writing any Builder brief;
6. independently verify Builder/repository claims rather than accepting summaries at face value.

Do **not** merge to `main` without explicit USER approval. Do not force-push, rebase, amend, reset, or rewrite published/shared history.

## 2. Current code/runtime baseline

The corrective restoration checkpoint remains:

`f1819fda36547343bb32a972d39405d0a6be6f72`

Motion Engine Completion Batch 2 started from the independently verified remote HEAD:

`21184fe89d8f4c6f7b9ec387cdab84f884ad0e41`

The Batch-2 implementation/tests/scene checkpoint before documentation is:

`e40e326e3f9a6fa8c9675dcf60fb1c0b2e2904c9`

The USER previously reopened Unity after corrective restoration with zero red errors and reported restored low-end performance. Further performance optimization remains deferred for the current hackathon milestone.

Current performance milestone:

`USER SATISFIED FOR CURRENT HACKATHON MILESTONE / FURTHER PERFORMANCE WORK DEFERRED`

Do not reopen performance architecture unless new reproducible evidence justifies it.

## 3. Preserved optimized body path

Current best-tested normal body path:

```text
Unity WebCamTexture
-> reusable WebCamCPU/GetPixels32 acquisition
-> reusable 320x240 CPU preparation
-> bounded newest-only two-slot scheduling
-> persistent OpenVINO CPU FP32 worker
-> MediaPipe 0.10.22 pose semantics
-> 33 normalized + world landmarks
-> CanonicalBodyV1
-> Phase 3 stabilization/calibration authority
-> Phase 4 positional/IK avatar control
-> presentation
```

Preserve these boundaries:

- stock MediaPipe/TFLite remains fallback/reference;
- ExistingReadback remains fallback/reference;
- at most one active body inference plus one replaceable newest pending frame;
- latest useful frame wins; no FIFO/history/replay/catch-up backlog;
- stable Phase 3 canonical data remains calibration/locomotion authority;
- Phase 4 signed canonical-to-avatar mapping and analytic two-bone IK remain accepted production pose authority;
- monocularly unobservable free axial/twist motion is not fabricated in normal production operation.

Batch 2 did not modify this path, Phase 3 stabilization/calibration, or Phase 4 production pose code.

## 4. Current phase status

- Phase 1 — provider/raw pose: **PASS WITH NOTES**.
- Phase 2 — canonical skeleton: **PASS**.
- Phase 3 — stabilization/confidence/calibration foundation: **PASS**.
- Phase 4 — humanoid retargeting: **USER ACCEPTED — PASS**.
- Low-end optimization milestone: **USER SATISFIED / FROZEN FOR CURRENT MILESTONE**.
- Phase 5A — locomotion: **BATCH 2 IMPLEMENTED / USER QA DEFERRED / NOT YET USER ACCEPTED**.
- Phase 6 — graybox vertical slice: **NOT STARTED**.

Do not mark Phase 5A or Motion Engine V1 accepted before final integrated USER QA.

## 5. Foundation disposition

### Foundation A — commands/speech

**Retained and working.** The shared command router remains the authority for keyboard and speech actions. USER microphone QA succeeded. All 14 product-default mappings created by `SpeechCommandConfiguration.CreateDefault()` use `SpeechRecognitionConfidence.Low`; custom mappings retain their generic Medium default. The wake prefix remains configurable and empty by default.

### Foundation B — camera presets

**Retained.** One primary camera owns `Back`, `Front`, `Left`, `Right`, `FullBody`, `Hands`, `LeftHand`, `RightHand`. F12 Lab/Game presentation remains separate.

### Foundation C / D / E and coarse hands

Foundation C remains deferred/dormant research, detailed hands remain deferred for low-end cost, Foundation E remains retired from production, and the former coarse-hand experiment remains deferred/rolled back. None were reopened in Batch 2.

## 6. Phase 5A Batch 2 implementation

Batch 2 completed the authorized horizontal-locomotion changes without adding vertical gameplay.

### Support-aware root authority

The old two-foot-midpoint assumption was the raised/swing-leg defect: a moving airborne foot shifted `(leftDelta + rightDelta) / 2` even though the other foot was planted.

`CameraSpaceRootTracker` now keeps a support-authority mode based on body-scale-normalized foot-height separation:

- `Both` for near-equal foot heights, using the midpoint;
- `Left` when the left foot is clearly lower/supporting;
- `Right` when the right foot is clearly lower/supporting;
- hysteresis retains the prior single-foot authority through the ambiguous band.

Current thresholds are `supportSingleFootEnter = 0.12` and `supportBothEnter = 0.06`.

On support-mode changes, landing or post-loss reacquisition, the tracker continuity-rebases the new raw authority coordinate to the last filtered displacement. Temporary support loss holds the last trusted root and flags the next valid sample for rebase. This prevents support switching itself from snapping/teleporting the avatar. Genuine bilateral/support-base relocation still moves the physical root, and `Recenter()` still zeroes the current measurement while preserving the controller's virtual position behavior.

No jump/airborne gameplay state was added; the support classifier exists only to make horizontal translation support-aware.

### Cadence baseline and Inspector tuning

Cadence keeps the existing alternating ankle/knee detector and unchanged event threshold. Batch-2 defaults are:

- `acquisitionEvents: 3 -> 2`;
- `acquireConfidence: 0.50 -> 0.38`;
- `virtualStridePerStep: 0.42 -> 0.60`;
- `maximumVirtualSpeed: 2.5 -> 3.0`;
- `eventThreshold = 0.07` unchanged;
- sustain confidence, stop timeout and step-rate limits unchanged.

The serialized field names remain unchanged. Inspector labels now expose `virtualStridePerStep` as **Distance Per Step** and `maximumVirtualSpeed` as **Maximum Cadence Speed**. Existing activation settings remain directly serialized/Inspector-visible.

The active Motion Engine Lab scene had serialized old values, so its relevant locomotion block was updated. Net comparison with the Batch-2 starting SHA shows only the two new support thresholds and the four authorized cadence value changes in that scene; unrelated Unity YAML changes were explicitly restored.

### Existing horizontal systems

Heading estimation, signed canonical-to-world mapping, `LocomotionFusion`, physical-motion cadence suppression, F12 Lab/Game separation and the third-person camera architecture were not redesigned. Deterministic coverage for mapping/heading/fusion/recenter remains in the Phase-5 suite.

## 7. Batch-2 verification status

`Assets/GoldenNeedle/Tests/Editor/Phase5LocomotionTests.cs` was updated rather than creating a new test framework. The obsolete `SingleStepOnsetMovesSupportMidpointWithoutHardHolding` expectation was superseded because it conflicted with current USER evidence.

Coverage now includes:

- planted-feet lateral lean and torso-scale lean produce no root translation;
- clearly raised swing-foot lateral/vertical movement does not accumulate physical translation;
- genuine bilateral relocation still translates and recenter still zeros the tracker;
- support hysteresis/landing and tracking reacquisition avoid discontinuities;
- temporary support loss holds last trusted physical displacement;
- jogging in place keeps physical contribution near zero while cadence can activate;
- default cadence acquires after two clean alternating events and stops after rhythm loss;
- one isolated event does not activate;
- distance-per-step changes virtual speed up to the max-speed clamp;
- physical/cadence fusion, front-camera axes and heading remain covered.

Environment limitation: no Unity Editor/Test Runner is available to this Builder session and GitHub reports no Actions run or combined status attached to `e40e326e3f9a6fa8c9675dcf60fb1c0b2e2904c9`. Therefore these updated Editor tests were **not executed here**. Source/diff/serialization scope was statically audited. Do not report an automated Unity pass that did not occur.

Batch-2 status vocabulary:

`IMPLEMENTED / AUTOMATED-VERIFIED AS AVAILABLE / USER QA DEFERRED`

No USER runtime QA was requested, per policy.

## 8. Jump and crouch remain Batch 3 requirements

### Jump

Physical jumping must eventually drive vertical game movement using coherent support/body evidence, distinguish a true jump from single-leg lift, reject noise, and implement takeoff/airborne/landing lifecycle.

### Crouch

Physical crouching must eventually lower the character using normalized body-compression/height evidence, held state and hysteresis.

**Neither jump nor crouch implementation was started in Batch 2.** Root-Y gameplay, CharacterController/gravity work and Phase 6 remain untouched.

## 9. Approved completion sequence

- **Batch 1 — COMPLETE:** documentation synchronization.
- **Batch 2 — IMPLEMENTED / USER QA DEFERRED:** horizontal locomotion completion described above.
- **Batch 3 — NOT STARTED:** vertical locomotion + final Motion Engine V1 completion and final integrated USER QA preparation.

## 10. Immediate next action

**STOP after Batch 2.** The next Orchestrator must independently audit the live branch, implementation diff, docs and available verification evidence. Only after that review should it issue the Batch-3 Builder handoff.

Do not ask for Batch-2 USER QA. Do not resume Foundation C/D/E/coarse-hand work, performance optimization, Phase 6, or unrelated CI cleanup. Do not merge to `main` without explicit USER approval.
