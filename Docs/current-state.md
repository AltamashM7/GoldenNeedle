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

The code/runtime checkpoint entering Motion Engine Completion Batch 1 is:

`f1819fda36547343bb32a972d39405d0a6be6f72`

That commit removed obsolete Foundation-E Editor tests after the production `RichHumanoidDetailRetargeter` had already been retired. The corrective restoration returned normal production pose composition to the accepted pre-Foundation-C optimization-era Phase 3 + Phase 4 architecture.

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
| Phase 5A — locomotion | **IMPLEMENTED / NOT YET USER ACCEPTED / NOW ACTIVE DEVELOPMENT TARGET** |
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

## Phase 5A — actual current state

Phase 5A is the main unfinished Motion Engine work. Most horizontal infrastructure already exists:

- camera-space physical/root displacement;
- lateral motion;
- toward/away motion;
- body heading for cadence travel;
- cadence/in-place movement;
- physical + cadence fusion;
- recenter;
- Lab/Game presentation;
- third-person follow/preset camera integration.

Current code consumes the stabilized Phase 3 frame. `EmbodiedLocomotionController` currently writes avatar-root **X/Z only** and preserves the current root Y and rotation.

Known issues/observations to carry into Batch 2:

1. **Planted-feet lean:** the earlier suppression work appears mostly successful in current USER observation, but final integrated testing is intentionally deferred, so this is not yet accepted.
2. **Raised/swing-leg false translation:** when one leg is lifted/moved while the other remains planted, physical locomotion can be triggered. Current `CameraSpaceRootTracker` uses the common/midpoint displacement of both support-foot measurements, so one-foot motion can shift that midpoint. Batch 2 must distinguish support/planted motion from swing-leg movement instead of interpreting every two-foot-centroid change as room translation.
3. **Cadence responsiveness:** cadence works, but acquisition takes longer than desired.
4. **Cadence travel distance:** distance/speed after activation is not yet satisfactory.
5. **Existing cadence controls:** the code already exposes `eventThreshold`, `acquisitionEvents`, `acquireConfidence`, `sustainConfidence`, `virtualStridePerStep`, `maximumVirtualSpeed`, and related rate/timeout settings. Batch 2 must audit and expose/use these coherently so the USER can tune responsiveness and travel distance from the Inspector rather than by code edits.
6. Other horizontal locomotion appears largely implemented, but Batch 2 must audit lateral/depth/heading/recenter/fusion consistency rather than assume acceptance.

## Motion Engine V1 vertical requirements

Jump and crouch are now explicit **Motion Engine V1 requirements**.

### Jump

A real physical jump must produce corresponding vertical game movement. The implementation must use coherent body/support evidence, distinguish a true jump from lifting only one leg, reject ordinary tracking noise, have a clear takeoff/airborne/landing lifecycle, and expose useful tuning controls where appropriate. The final detection algorithm is intentionally not specified in Batch 1.

### Crouch

A real physical crouch must correspondingly lower/crouch the game character. The implementation must use normalized body-compression/height evidence rather than fragile raw-pixel-only thresholds, support holding a crouched state, use acquisition/release hysteresis, and expose useful tuning controls where appropriate. The final detection algorithm is intentionally not specified in Batch 1.

## Final Motion Engine completion sequence

### Batch 1 — documentation synchronization

Current task. Documentation only; no runtime/code changes.

### Batch 2 — horizontal locomotion completion

- audit the existing Phase 5A implementation;
- fix raised/swing-leg false physical translation;
- preserve/regression-check the mostly successful planted-feet lean suppression;
- improve cadence acquisition responsiveness;
- make cadence travel distance/speed clearly Inspector-tunable;
- verify lateral/depth/heading/recenter/fusion remain coherent;
- do **not** add jump/crouch yet.

### Batch 3 — vertical locomotion + final Motion Engine V1 completion

- implement jump detection/application;
- implement crouch detection/application;
- distinguish jump from single-leg lift;
- expose appropriate Inspector tuning;
- integrate vertical locomotion without corrupting Phase 4 body pose;
- prepare one final comprehensive USER Motion Engine QA.

After Batch 3, all Motion Engine testing will be performed together. If that integrated USER QA is accepted, Motion Engine V1 will be considered essentially complete for the hackathon and the USER will provide the next game-development direction.

## Testing policy for the completion sequence

The USER explicitly chose to defer USER/runtime testing until all three completion batches are implemented.

- Do not ask for a separate Batch 1 runtime test.
- Batch 2 should not stop waiting for USER QA.
- Batch 3 prepares the final integrated QA.
- Builder-side compile/static/deterministic validation remains useful in implementation batches but never substitutes for USER acceptance.
- Untested runtime behavior must not be marked USER accepted.

## Phase 6 and game development

Phase 6 remains **NOT STARTED** and must not begin during Batch 1 or Batch 2. It is the later graybox/playable vertical-slice integration step after Motion Engine V1 completion. Hub/course implementation has not begun merely because the Motion Engine roadmap is being synchronized.

**Current completion-sequence status:** `BATCH 1 COMPLETE / AWAITING ORCHESTRATOR REVIEW BEFORE BATCH 2`
