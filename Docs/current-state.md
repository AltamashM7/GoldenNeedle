# Golden Needle — Current State

Authoritative current-state refresh: 2026-09-16.

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## Governance

- Do **not** merge to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or otherwise rewrite shared branch history.
- GitHub is the shared authoritative project state; independently inspect the live branch before implementation work.
- The USER is the decisive Unity/manual/runtime acceptance authority.
- Motion Engine Completion uses the approved staged sequence recorded below. USER/runtime testing is deliberately deferred until the implementation batches are complete; Builder/static checks do not create USER acceptance.
- Phase 6 remains **NOT STARTED**.

## Current checkpoint and post-restoration result

The corrective restoration checkpoint remains:

`f1819fda36547343bb32a972d39405d0a6be6f72`

Batch 2 started from the independently verified remote HEAD:

`21184fe89d8f4c6f7b9ec387cdab84f884ad0e41`

Batch 2 completed at:

`19e697695802459f97d488b64e7b62683c89512d`

Batch 2R started from that exact accepted Batch-2 HEAD. The Batch-2R source/test checkpoint before this documentation refresh is:

`a96cbf06437004f3f44d53383601ec1be556d767`

Batch 2R changes only the Phase-5 root tracker and Phase-5 deterministic tests, plus this minimal current documentation/handoff refresh. Cadence, the accepted optimized body path, Phase 3 authority and Phase 4 production pose code remain untouched.

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
| Phase 5A — locomotion | **BATCH 2R COMPLETE / USER QA DEFERRED / NOT YET USER ACCEPTED** |
| Phase 6 — graybox vertical-slice integration | **NOT STARTED** |

Do not describe Phase 5A or Motion Engine V1 as USER accepted yet.

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

The shared command/action architecture remains. Keyboard and speech resolve into the same project command router. Real USER microphone QA succeeded. Product policy uses `SpeechRecognitionConfidence.Low` for all 14 mappings created by `SpeechCommandConfiguration.CreateDefault()`. The generic/custom `SpeechCommandMapping` default remains Medium. The wake prefix remains configurable and empty by default.

### Foundation B — camera presets

**RETAINED.**

The unified data-driven camera preset set remains:

`Back`, `Front`, `Left`, `Right`, `FullBody`, `Hands`, `LeftHand`, `RightHand`.

Preset selection remains integrated with the shared command layer and remains orthogonal to F12 Lab/Game presentation mode.

### Foundation C — rich anatomical orientation

`DEFERRED / DORMANT RESEARCH — NOT PRODUCTION POSE AUTHORITY`

The research contracts/solver may remain in the repository for history and future study, but the corrective restoration removed C from normal production composition.

### Foundation D — detailed hands

`DEFERRED`

The independent MediaPipe Hand Landmarker experiment imposed unacceptable low-end cost in USER testing. Existing research/lifecycle code may remain, but detailed hand inference is not normal production operation or a current hackathon requirement.

### Foundation E — rich post-Phase-4 detail

`RETIRED FROM PRODUCTION / RESEARCH HISTORY ONLY`

The production `RichHumanoidDetailRetargeter` was removed. Foundation-E historical research remains useful provenance, but production execution is not `Phase 4 -> E -> presentation`.

### Coarse hands / former Batch 4A

`DEFERRED`

The zero-extra-inference coarse `Unknown/Open/Closed` experiment passed Builder/static checks but was rejected after USER runtime evaluation and rolled back. Coarse hand/fist control is not required for Motion Engine V1 completion.

## Phase 5A — Batch 2 + Batch 2R implemented state

Phase 5A horizontal infrastructure remains separate from pose reproduction. `EmbodiedLocomotionController` still consumes the stabilized Phase 3 frame, writes avatar-root **X/Z only**, and preserves current root Y and rotation.

### Support-aware physical translation

`CameraSpaceRootTracker` forms trusted left/right support-foot measurements from ankle/heel/toe observations and uses stateful physical authority:

- near-equal normalized foot heights -> `Both` authority using the midpoint;
- left foot clearly lower -> `Left` support authority;
- right foot clearly lower -> `Right` support authority;
- the hysteresis band retains the previous support authority instead of flapping.

Batch-2 thresholds remain `supportSingleFootEnter = 0.12` and `supportBothEnter = 0.06`, normalized by apparent/reference body scale. Moving a clearly raised swing foot therefore does not itself move the physical root while the lower planted foot remains the authority.

Support-authority changes and support reacquisition still continuity-rebase the newly selected raw support coordinate to the last filtered displacement. Temporary support loss still holds the last trusted displacement and marks the next valid sample for rebase. `Recenter()` still captures the current measurement as the new zero origin.

### Batch 2R — alternating physical-step continuity correction

Batch 2R fixes one remaining continuity problem in the accepted Batch-2 authority model.

Before 2R, every authority transition permanently replaced `_supportAuthorityOffset` with `currentFiltered - newRawAuthority`. That correctly prevented transition snaps, but an ordinary alternating step could perform:

`Both -> single support -> Both -> opposite single support -> Both`

and finish with both feet genuinely relocated while the last Both-mode rebase still cancelled the new common displacement indefinitely.

The 2R rule is:

- keep the transition-frame rebase exactly as before so landing cannot teleport the root;
- when entering `Both` from a single-support mode, check whether normalized left/right displacement agrees in both X and Y within the existing `supportBothEnter` tolerance;
- only a coherent dual-support landing becomes eligible to release the temporary landing offset;
- release occurs on a subsequent coherent `Both` sample, allowing the normal position filter to converge toward the genuinely relocated common support base rather than snapping on the transition frame;
- asymmetric/one-foot relocation does not qualify;
- a tracking-loss/reacquisition rebase is explicitly marked non-releasable, preserving the existing safe reacquisition behavior.

Because the authority offset is a `Vector2`, the same coherent-release mechanism applies to the support displacement used by depth. Existing depth differential attenuation and torso-scale corroboration remain the gate for whether depth displacement is actually accepted; Batch 2R did not weaken or bypass those semantics.

### Batch-2 cadence baseline

Cadence is unchanged by Batch 2R. The active Batch-2 defaults remain:

- `eventThreshold = 0.07`;
- `acquisitionEvents = 2`;
- `acquireConfidence = 0.38`;
- `sustainConfidence = 0.25`;
- `stopTimeoutSeconds = 0.50`;
- `minimumStepRate = 0.8` / `maximumStepRate = 4.5`;
- `virtualStridePerStep = 0.60`;
- `maximumVirtualSpeed = 3.0`.

Inspector labels remain **Distance Per Step** and **Maximum Cadence Speed** for the existing serialized fields.

### Deterministic coverage and verification status

Existing Batch-2 tests remain in place for planted-feet lateral/scale lean suppression, raised swing-foot isolation across multiple samples, genuine bilateral relocation, support transition/landing continuity, support loss/reacquisition, cadence acquisition/stop/travel tuning, physical/cadence fusion, accepted front-camera mapping/heading, and recenter.

Batch 2R adds `AlternatingPhysicalStepEventuallyCommitsCoherentSupportBaseRelocation()`. Its sequence is:

1. both feet at baseline;
2. left foot swings +X while raised -> no root movement;
3. left foot lands -> no landing jump;
4. right foot swings +X while raised -> no swing-induced movement;
5. right foot lands at the corresponding relocated position -> no landing jump;
6. the next coherent dual-support sample must commit net +X physical displacement.

Against the pre-2R `19e6976...` logic, step 6 remains at the old origin because the final Both-mode offset is never retired. The 2R tracker releases only the coherent landing rebase, so the same deterministic sequence can converge to the relocated support base while preserving all transition-frame continuity assertions.

Verification status for this Builder environment remains:

`IMPLEMENTED / STATICALLY VERIFIED / USER QA DEFERRED`

No Unity Editor/Test Runner is available in the current execution environment. Therefore the C# Editor tests were **not executed here**. The old/new authority sequence was independently traced, source/test diffs were audited, and final GitHub scope/status is verified. This must not be reported as a passing Unity test run or USER acceptance.

## Motion Engine V1 vertical requirements

Jump and crouch remain explicit **Motion Engine V1 requirements**, but **Batch 3 has not started**.

### Jump

A real physical jump must produce corresponding vertical game movement. The implementation must use coherent body/support evidence, distinguish a true jump from lifting only one leg, reject ordinary tracking noise, have a clear takeoff/airborne/landing lifecycle, and expose useful tuning controls where appropriate.

### Crouch

A real physical crouch must correspondingly lower/crouch the game character. The implementation must use normalized body-compression/height evidence rather than fragile raw-pixel-only thresholds, support holding a crouched state, use acquisition/release hysteresis, and expose useful tuning controls where appropriate.

## Final Motion Engine completion sequence

### Batch 1 — documentation synchronization

**COMPLETE.**

### Batch 2 — horizontal locomotion completion

**ACCEPTED AS BASIS FOR 2R.** Completed at `19e697695802459f97d488b64e7b62683c89512d`.

### Batch 2R — alternating physical-step continuity correction

**COMPLETE / USER QA DEFERRED / AWAITING ORCHESTRATOR REVIEW.**

Source/test checkpoint before docs: `a96cbf06437004f3f44d53383601ec1be556d767`.

### Batch 3 — vertical locomotion + final Motion Engine V1 completion

**NOT STARTED.** This is the next implementation batch only after Orchestrator review/authorization. It will add jump/crouch and prepare one final comprehensive USER Motion Engine QA.

## Testing policy for the completion sequence

The USER explicitly chose to defer USER/runtime testing until after Batch 3 implementation.

- No Batch-2R USER runtime QA is requested.
- Builder-side compile/static/deterministic validation is useful but never substitutes for USER acceptance.
- Untested runtime behavior must not be marked USER accepted.
- Batch 3 prepares the final integrated QA.

## Phase 6 and game development

Phase 6 remains **NOT STARTED**. Hub/course implementation has not begun merely because the Motion Engine roadmap is progressing.

**Current completion-sequence status:** `BATCH 2R COMPLETE / USER QA DEFERRED / AWAITING ORCHESTRATOR REVIEW`
