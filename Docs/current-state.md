# Golden Needle — Current State

Authoritative current-state refresh: 2026-09-16.

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## Governance

- Do **not** merge to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or otherwise rewrite shared branch history.
- GitHub is the shared authoritative project state; independently inspect the live branch before implementation work.
- The USER is the decisive Unity/manual/runtime acceptance authority.
- Motion Engine Completion uses the approved three-batch sequence recorded below. USER/runtime testing is deliberately deferred until the implementation batches are complete; Builder/static checks do not create USER acceptance.
- Phase 6 remains **NOT STARTED**.

## Current checkpoint and post-restoration result

The corrective restoration checkpoint remains:

`f1819fda36547343bb32a972d39405d0a6be6f72`

Batch 2 started from the independently verified remote HEAD:

`21184fe89d8f4c6f7b9ec387cdab84f884ad0e41`

The Batch-2 implementation/tests/scene checkpoint before this documentation refresh is:

`e40e326e3f9a6fa8c9675dcf60fb1c0b2e2904c9`

That Batch-2 implementation changes only Phase-5 locomotion code/tests plus the relevant Motion Engine Lab locomotion serialization. The accepted optimized body path, Phase 3 authority and Phase 4 production pose code remain untouched.

After the corrective restoration and compile-closure work, the USER reopened Unity with zero red errors and reports that low-end performance appears restored. The USER explicitly closed further performance work for the present hackathon milestone.

Current optimization status:

`USER SATISFIED FOR CURRENT HACKATHON MILESTONE / FURTHER PERFORMANCE WORK DEFERRED`

This is not a claim that performance can never be improved. Golden Needle remains modular and optimization may resume later if new evidence makes it useful.

## Phase status

| Area | Current status |
|---|---|
| Phase 1 — provider/raw pose | **PASS WITH NOTES** |
| Phase 2 — canonical skeleton | **PASS** |
| Phase 3 — stabilization/confidence/calibration foundation | **PASS** |
| Phase 4 — humanoid retargeting | **USER ACCEPTED — PASS** |
| Low-end optimization milestone | **USER SATISFIED / FROZEN FOR CURRENT MILESTONE** |
| Phase 5A — locomotion | **BATCH 2 IMPLEMENTED / USER QA DEFERRED / NOT YET USER ACCEPTED** |
| Phase 6 — graybox vertical-slice integration | **NOT STARTED** |

Do not describe Phase 5A or Motion Engine V1 as accepted yet.

## Current production body/pose pipeline

The normal low-end production path is:

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

Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths.

The production pose authority is deliberately **Phase 3 + Phase 4**:

- Stable Phase 3 canonical filtering remains calibration and locomotion authority.
- Phase 4 signed canonical-to-avatar mapping remains accepted.
- Phase 4 positional targets and project-owned analytic two-bone IK remain production limb authority.
- Phase 4 modular calibration remains accepted.
- Monocularly unobservable free axial/twist DOFs are not fabricated in normal production operation.
- Normal execution is `Phase 4 solve -> presentation`; there is no active Foundation-E post-solve layer.

The current `MotionEngineRuntime` has no production `RichMotionFrame`, `MediaPipeCanonicalPoseSource` is body-only, `HumanoidRetargeter` is Phase-4-only, and the live presenter does not compose `RichHumanoidDetailRetargeter`.

## Foundation status

### Foundation A — commands and speech

**RETAINED / WORKING OPERATIONAL FEATURE.**

The shared command/action architecture remains. Keyboard and speech resolve into the same project command router. Real USER microphone QA succeeded: the Windows phrase system and `KeywordRecognizer` ran, configured spoken commands were recognized and dispatched, and noisy conditions frequently produced Low-confidence recognition while clearer/louder speech produced successful cases.

Product policy now uses `SpeechRecognitionConfidence.Low` for **all 14 mappings created by `SpeechCommandConfiguration.CreateDefault()`**. The generic/custom `SpeechCommandMapping` default remains Medium. The wake prefix remains configurable and empty by default; it may be added later if accidental activation becomes a problem.

### Foundation B — camera presets

**RETAINED.**

The unified data-driven camera preset set remains:

`Back`, `Front`, `Left`, `Right`, `FullBody`, `Hands`, `LeftHand`, `RightHand`.

Preset selection remains integrated with the shared command layer and remains orthogonal to F12 Lab/Game presentation mode.

### Foundation C — rich anatomical orientation

`DEFERRED / DORMANT RESEARCH — NOT PRODUCTION POSE AUTHORITY`

The research contracts/solver may remain in the repository for history and future study, but the corrective restoration removed C from normal production composition. It is not the next required milestone.

### Foundation D — detailed hands

`DEFERRED`

The independent MediaPipe Hand Landmarker experiment imposed unacceptable low-end cost in USER testing, with detailed-hand operation entering roughly the 10–15 FPS class while accepted body performance returned when that stream was disabled/disconnected. Existing research/lifecycle code may remain, but detailed hand inference is not normal production operation or a current hackathon requirement.

### Foundation E — rich post-Phase-4 detail

`RETIRED FROM PRODUCTION / RESEARCH HISTORY ONLY`

The production `RichHumanoidDetailRetargeter` was removed. Its obsolete Editor tests were subsequently removed at `f1819fda36547343bb32a972d39405d0a6be6f72`. Foundation-E historical research remains useful provenance, but production execution is not `Phase 4 -> E -> presentation`.

### Coarse hands / former Batch 4A

`DEFERRED`

The zero-extra-inference coarse `Unknown/Open/Closed` experiment passed Builder/static checks but was rejected after USER runtime evaluation and rolled back. The project does not claim its arithmetic alone caused the performance drop; the USER rejected the feature/value tradeoff and chose to restore the known optimized body baseline. Coarse hand/fist control is not required for Motion Engine V1 completion.

## Phase 5A — Batch 2 implemented state

Phase 5A horizontal infrastructure remains separate from pose reproduction. `EmbodiedLocomotionController` still consumes the stabilized Phase 3 frame, writes avatar-root **X/Z only**, and preserves current root Y and rotation.

### Support-aware physical translation

The obsolete assumption that every two-foot midpoint change is room translation has been superseded. `CameraSpaceRootTracker` still forms trusted left/right support-foot measurements from ankle/heel/toe observations, but physical authority is now stateful:

- near-equal normalized foot heights -> `Both` authority using the midpoint;
- left foot clearly lower -> `Left` support authority;
- right foot clearly lower -> `Right` support authority;
- the hysteresis band retains the previous support authority instead of flapping.

Batch-2 thresholds are `supportSingleFootEnter = 0.12` and `supportBothEnter = 0.06`, normalized by apparent/reference body scale. Moving a clearly raised swing foot therefore does not itself move the physical root while the lower planted foot remains the authority.

Support-authority changes, landing, and support reacquisition use continuity rebasing: the newly selected raw support coordinate is offset to the last filtered displacement at the transition. Temporary support loss holds the last trusted displacement and marks the next valid sample for rebase. `Recenter()` still captures the current measurement as the new zero origin.

The existing common and differential foot signals remain available for diagnostics/depth reliability. Existing depth corroboration, signed mapping, heading, fusion and cadence-suppression architecture were not redesigned.

### Batch-2 cadence baseline

Cadence architecture remains the existing alternating ankle/knee rhythm detector. The active defaults are now:

- `eventThreshold = 0.07` — unchanged;
- `acquisitionEvents = 2` — previously 3;
- `acquireConfidence = 0.38` — previously 0.50;
- `sustainConfidence = 0.25` — unchanged;
- `stopTimeoutSeconds = 0.50` — unchanged;
- `minimumStepRate = 0.8` / `maximumStepRate = 4.5` — unchanged;
- `virtualStridePerStep = 0.60` — previously 0.42;
- `maximumVirtualSpeed = 3.0` — previously 2.5.

The existing serialized fields remain compatible. Inspector presentation now labels `virtualStridePerStep` as **Distance Per Step** and `maximumVirtualSpeed` as **Maximum Cadence Speed**. The Motion Engine Lab scene contained old serialized overrides, so only the two new support thresholds and four changed cadence values were updated there.

### Batch-2 deterministic coverage and verification status

`Assets/GoldenNeedle/Tests/Editor/Phase5LocomotionTests.cs` now covers planted-feet lateral/scale lean suppression, clearly raised swing-foot suppression across multiple samples, genuine bilateral relocation, support transition/landing continuity, support loss/reacquisition, jogging-in-place with cadence, two-event default cadence acquisition, isolated-event rejection, cadence stop, distance-per-step/max-speed behavior, physical/cadence fusion, accepted front-camera mapping/heading, and recenter.

The superseded `SingleStepOnsetMovesSupportMidpointWithoutHardHolding` expectation was removed/reframed because USER runtime evidence established that an airborne/swing leg must not cause physical world translation.

Verification status for this Builder environment:

`IMPLEMENTED / AUTOMATED-VERIFIED AS AVAILABLE / USER QA DEFERRED`

No Unity Editor/Test Runner is available in the current execution environment and GitHub reports no workflow/status run attached to the Batch-2 implementation checkpoint, so the updated deterministic tests were **not executed here**. Source, serialization, branch scope and net diffs were independently/static audited. This must not be reported as a passing Unity test run or USER acceptance.

## Motion Engine V1 vertical requirements

Jump and crouch remain explicit **Motion Engine V1 requirements**, but **Batch 3 has not started**.

### Jump

A real physical jump must produce corresponding vertical game movement. The implementation must use coherent body/support evidence, distinguish a true jump from lifting only one leg, reject ordinary tracking noise, have a clear takeoff/airborne/landing lifecycle, and expose useful tuning controls where appropriate.

### Crouch

A real physical crouch must correspondingly lower/crouch the game character. The implementation must use normalized body-compression/height evidence rather than fragile raw-pixel-only thresholds, support holding a crouched state, use acquisition/release hysteresis, and expose useful tuning controls where appropriate.

## Final Motion Engine completion sequence

### Batch 1 — documentation synchronization

**COMPLETE.** Documentation only.

### Batch 2 — horizontal locomotion completion

**IMPLEMENTED / AUTOMATED-VERIFIED AS AVAILABLE / USER QA DEFERRED.**

Implementation checkpoint before documentation: `e40e326e3f9a6fa8c9675dcf60fb1c0b2e2904c9`.

### Batch 3 — vertical locomotion + final Motion Engine V1 completion

**NOT STARTED.** This is the next implementation batch only after Orchestrator review/authorization. It will add jump/crouch and prepare one final comprehensive USER Motion Engine QA.

After Batch 3, all Motion Engine testing will be performed together. If that integrated USER QA is accepted, Motion Engine V1 will be considered essentially complete for the hackathon and the USER will provide the next game-development direction.

## Testing policy for the completion sequence

The USER explicitly chose to defer USER/runtime testing until all three completion batches are implemented.

- No Batch-2 USER runtime QA was requested.
- Builder-side compile/static/deterministic validation is useful but never substitutes for USER acceptance.
- Untested runtime behavior must not be marked USER accepted.
- Batch 3 prepares the final integrated QA.

## Phase 6 and game development

Phase 6 remains **NOT STARTED**. Hub/course implementation has not begun merely because the Motion Engine roadmap is progressing.

**Current completion-sequence status:** `BATCH 2 IMPLEMENTED / USER QA DEFERRED / AWAITING ORCHESTRATOR REVIEW FOR BATCH 3`
