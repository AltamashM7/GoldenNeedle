# Golden Needle — Decision Log

Current authority: `Docs/current-state.md`.

Historical foundation design remains available in `Docs/pre-phase5a-foundations.md`; corrective/restoration history remains in `Docs/foundation-corrective-pass-progress.md`; detailed optimization chronology remains in `Docs/optimization-orchestrator-handoff.md`. Historical implementation evidence does not override the current decisions below.

| Decision | Current status | Rationale / current meaning |
|---|---|---|
| Unity 6.5 / `6000.5.0f1` | **LOCKED** | Preserve the established Unity baseline. |
| URP 17.5.0 | **LOCKED** | Current rendering baseline; presentation remains intentionally CPU-conscious. |
| CPU-first / no required dedicated GPU | **LOCKED PRODUCT REQUIREMENT** | Golden Needle must run acceptably on ordinary hardware. |
| Integrated webcam input | **LOCKED PRODUCT REQUIREMENT** | The normal product does not require VR/depth hardware or an external tracking application. |
| Replaceable pose-provider boundary | **LOCKED ARCHITECTURAL DIRECTION** | MediaPipe-specific structures stay upstream; downstream consumes project-owned canonical contracts. |
| Stock MediaPipe/TFLite CPU | **LOCKED FALLBACK / REFERENCE** | Retain the known-safe reference path. |
| OpenVINO CPU FP32 | **USER-VALIDATED BEST-TESTED LOW-END BACKEND** | Preserve the accepted accelerated body inference path for the current proof hardware. |
| WebCamCPU/GetPixels32 acquisition | **USER ACCEPTED — PASS FOR CURRENT MILESTONE** | Reusable CPU pixels + reusable 320x240 preparation removed the dominant accepted readback bottleneck. |
| ExistingReadback | **LOCKED FALLBACK / REFERENCE** | Keep the older readback architecture available for fallback/comparison/hardware compatibility. |
| Body input around 320x240 | **CURRENT BEST-TESTED BASELINE** | Preserves the accepted low-end body path without unnecessary quality loss. |
| Persistent OpenVINO worker + two-slot newest-frame mailbox | **USER ACCEPTED — PASS** | At most one active inference and one replaceable newest pending frame; no FIFO/history/replay/catch-up queue. |
| Latest useful frame wins | **LOCKED RUNTIME RULE** | Avoid stale-frame backlog and preserve responsiveness. |
| CanonicalBodyV1 = 20 joints | **PRESERVE AS V1 CONTRACT** | Existing canonical meanings/partial validity remain the stable downstream body contract. |
| Stable Phase 3 canonical frame | **LOCKED CALIBRATION / LOCOMOTION AUTHORITY** | Calibration and Phase 5 locomotion continue to use the stable canonical path. |
| Selectable Raw/Responsive avatar-drive sources | **PRESERVE FOR OPTIONAL TUNING** | Avatar responsiveness experiments remain available without changing calibration/locomotion authority. |
| Phase 4 signed canonical-to-avatar mapping | **USER ACCEPTED — PRODUCTION AUTHORITY** | Preserve accepted orientation/axis semantics. |
| Phase 4 positional targets + analytic two-bone IK | **USER ACCEPTED — PRODUCTION LIMB AUTHORITY** | Maps normalized canonical targets while preserving avatar-authored proportions. |
| Phase 4 modular calibration | **USER ACCEPTED** | Body reference and per-chain geometry remain independently usable for partial bodies. |
| Fabricating unobservable monocular axial/twist DOFs | **REJECTED FOR NORMAL PRODUCTION** | Single-RGB evidence does not justify inventing free axial rotation. Normal production stays with trusted Phase 3 + Phase 4 behavior. |
| Production pose path includes Foundation E after Phase 4 | **SUPERSEDED / FALSE FOR CURRENT RUNTIME** | Production `RichHumanoidDetailRetargeter` was removed. Current execution is `Phase 4 solve -> presentation`. |
| Motion-engine performance optimization milestone | **USER SATISFIED FOR CURRENT HACKATHON MILESTONE / FURTHER PERFORMANCE WORK DEFERRED** | USER reports restored performance after corrective baseline work and explicitly chose not to continue optimization now. Reopen only on new evidence. |
| Foundation A unified command/action layer | **RETAINED / WORKING** | Keyboard, speech and future inputs invoke the same runtime actions instead of duplicating behavior. |
| Windows fixed-vocabulary speech backend | **USER QA PASSED / RETAINED** | Phrase system + `KeywordRecognizer` ran and spoken configured commands dispatched through the shared router. |
| Product-default speech mapping confidence | **LOW FOR ALL 14 DEFAULT MAPPINGS** | Real USER QA showed Low-confidence recognition is common in noisy environments. The generic/custom `SpeechCommandMapping` default remains Medium. |
| Speech wake prefix | **CONFIGURABLE / EMPTY BY DEFAULT** | No mandatory wake phrase. USER may add one later if accidental activation becomes a problem. |
| Foundation B camera presets | **RETAINED** | One primary camera owns Back/Front/Left/Right/FullBody/Hands/LeftHand/RightHand presets. |
| F12 Lab/Game presentation | **RETAINED / ORTHOGONAL TO CAMERA PRESET** | View preset selection must not silently change Lab/Game mode. |
| Foundation C rich anatomical orientation | **DEFERRED / DORMANT RESEARCH — NOT PRODUCTION POSE AUTHORITY** | Research types may remain, but production composition was restored to the pre-C Phase 3 + Phase 4 baseline. |
| Foundation D detailed Hand Landmarker | **DEFERRED** | USER testing showed unacceptable low-end impact in roughly the 10–15 FPS class. It is not a current hackathon requirement. |
| Foundation E post-Phase-4 detail | **RETIRED FROM PRODUCTION** | Production component removed; historical math/research may remain as provenance only. |
| Coarse hand/fist signal (former Batch 4A) | **DEFERRED / ROLLED BACK** | Builder/static validation did not justify retaining the feature after USER runtime evaluation. No claim is made that coarse arithmetic alone caused the observed slowdown. |
| Phase 5A hybrid locomotion | **BATCH 2 IMPLEMENTED / NOT USER ACCEPTED** | Horizontal completion work is implemented; USER/runtime acceptance remains deferred until the final integrated Motion Engine QA. |
| Phase 5A physical tracking authority | **SUPPORT-AWARE CAMERA-SPACE AUTHORITY** | Trusted ankle/heel/toe measurements feed a stateful Both/Left/Right support authority. Near-equal heights use both feet; a clearly raised swing foot is excluded from physical root authority. |
| Support authority hysteresis | **BATCH 2 DEFAULT: 0.12 ENTER / 0.06 BOTH** | Foot-height difference is normalized by body/reference scale. Separate enter/return thresholds keep support classification from flapping near equality. |
| Support transition/reacquisition continuity | **LOCKED BATCH-2 BEHAVIOR** | On support-mode change or tracking reacquisition, the new raw support coordinate is rebased to the last filtered physical displacement so the authority switch itself cannot teleport the root. |
| Two-foot midpoint is always physical authority | **SUPERSEDED / REJECTED** | USER evidence showed a raised/moving swing leg could shift the midpoint while the support foot stayed planted. Midpoint remains valid for near-equal dual support, not as unconditional authority. |
| Phase 5A controller root writes | **CURRENT IMPLEMENTATION: X/Z ONLY** | Existing horizontal controller preserves root Y and rotation. Batch 2 did not add vertical gameplay. |
| Planted-feet lean suppression | **PRESERVED / DETERMINISTIC COVERAGE UPDATED / USER QA DEFERRED** | Torso-only lateral/scale lean remains excluded from physical room translation; final integrated runtime acceptance is still USER-owned. |
| Raised/swing-leg false translation | **BATCH 2 IMPLEMENTED FIX / USER QA DEFERRED** | Clearly raised foot movement no longer drives physical X/Z while the lower support foot remains authority; deterministic coverage was added but not executed in this Builder environment. |
| Cadence event threshold | **PRESERVE AT 0.07** | Batch 2 improves acquisition without globally weakening the alternating-event threshold. |
| Cadence acquisition baseline | **BATCH 2: 2 EVENTS / 0.38 ACQUIRE CONFIDENCE** | Supersedes the 3-event / 0.50 baseline so two clean alternating events can acquire while one isolated event still cannot. |
| Cadence travel baseline | **BATCH 2: 0.60 DISTANCE PER STEP / 3.0 MAX SPEED** | Supersedes 0.42 / 2.5 to provide a stronger hackathon prototype baseline; remains Inspector-tunable rather than a universal physical measurement. |
| Cadence Inspector naming | **DISTANCE PER STEP / MAXIMUM CADENCE SPEED** | Serialized field names remain compatible; labels/tooltips expose the user-facing concepts without a custom Inspector. |
| Recenter | **IMPLEMENTED / PRESERVED** | Current physical location can become the new tracking origin while preserving virtual position. |
| Body heading for cadence | **IMPLEMENTED / PRESERVED** | Cadence follows mapped body heading; physical room displacement uses the fixed reference map rather than live heading. |
| Batch-2 automated verification | **AVAILABLE EVIDENCE ONLY / NO UNITY RUN IN BUILDER ENVIRONMENT** | Editor tests were updated and source/diff/serialization were statically audited. No Unity Editor/Test Runner and no attached GitHub Actions/status run were available; do not claim a passing automated Unity run. |
| Jump | **MOTION ENGINE V1 REQUIREMENT / BATCH 3** | Physical jump must drive vertical game movement using coherent support/body evidence, distinguish single-leg lift, reject noise, have takeoff/airborne/landing lifecycle and useful tuning. |
| Crouch | **MOTION ENGINE V1 REQUIREMENT / BATCH 3** | Physical crouch must lower the character using normalized body-compression/height evidence, held state, hysteresis and useful tuning. |
| Final Motion Engine completion sequence | **APPROVED: BATCH 1 DOCS -> BATCH 2 HORIZONTAL -> BATCH 3 VERTICAL/FINAL** | Prevents deferred foundations from re-entering scope and keeps locomotion completion staged. |
| USER/runtime testing during completion sequence | **DEFER UNTIL ALL THREE BATCHES ARE IMPLEMENTED** | No Batch-2 USER QA is requested; Batch 3 prepares one final integrated Motion Engine QA. |
| Builder/static validation | **USEFUL BUT NOT ACCEPTANCE** | Deterministic/compile/static checks can catch implementation defects but cannot create USER runtime acceptance. |
| Phase 6 | **NOT STARTED** | Graybox/playable vertical-slice integration follows Motion Engine V1 completion; do not start it during the completion batches without new USER direction. |
| Hub/course implementation | **NOT STARTED BY THIS ENGINE ROADMAP** | Documentation synchronization does not imply Hub/course gameplay has begun. |
| Motion Engine branch | **CURRENT WORKFLOW DECISION** | Continue core engine work on `engine/pose-tracking-spike`; do not merge to `main` without explicit USER approval. |

## Current completion plan

### Batch 1 — documentation synchronization

**COMPLETE.**

### Batch 2 — horizontal locomotion completion

**IMPLEMENTED / AUTOMATED-VERIFIED AS AVAILABLE / USER QA DEFERRED.**

Starting SHA: `21184fe89d8f4c6f7b9ec387cdab84f884ad0e41`.

Implementation/tests/scene checkpoint before docs: `e40e326e3f9a6fa8c9675dcf60fb1c0b2e2904c9`.

### Batch 3 — vertical locomotion + final Motion Engine V1 completion

**NOT STARTED.** Implement jump and crouch only after Orchestrator review/authorization, then prepare the final integrated USER Motion Engine QA.

Phase 5A and Motion Engine V1 remain **not USER accepted** until that final QA succeeds.
