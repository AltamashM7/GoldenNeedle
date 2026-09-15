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
| Phase 5A hybrid locomotion | **IMPLEMENTED / NOT USER ACCEPTED / ACTIVE DEVELOPMENT TARGET** | Existing horizontal infrastructure is substantial; completion is an audit/fix/tuning task, not a rewrite. |
| Phase 5A physical tracking authority | **CURRENT IMPLEMENTATION: SUPPORT-FOOT CAMERA-SPACE MODEL** | Ankle/heel/toe support evidence and torso scale corroboration drive physical displacement. |
| Phase 5A controller root writes | **CURRENT IMPLEMENTATION: X/Z ONLY** | Existing horizontal controller preserves root Y and rotation. This is a current implementation fact, not a prohibition on the new V1 jump/crouch requirements. |
| Planted-feet lean suppression | **MOSTLY SUCCESSFUL IN CURRENT USER OBSERVATION / FINAL QA DEFERRED** | Preserve/regression-check in Batch 2; do not call it accepted before final integrated QA. |
| Raised/swing-leg false translation | **OPEN / BATCH 2 BLOCKER** | One moving leg can shift the current two-foot support midpoint and trigger physical locomotion while the other leg remains planted. Batch 2 must distinguish support/planted motion from swing-leg movement. |
| Cadence acquisition responsiveness | **OPEN / BATCH 2 TUNING TARGET** | Cadence works but takes longer than desired to activate. |
| Cadence travel distance/speed | **OPEN / BATCH 2 TUNING TARGET** | Existing `virtualStridePerStep` and `maximumVirtualSpeed` settings need a coherent Inspector-tuning path for desired travel. |
| Existing cadence settings | **PRESERVE / AUDIT IN BATCH 2** | Event threshold, acquisition events, acquire/sustain confidence, step-rate range, timeout, stride and max virtual speed already exist. |
| Recenter | **IMPLEMENTED / PRESERVE** | Current physical location can become the new tracking origin while preserving virtual position. |
| Body heading for cadence | **IMPLEMENTED / PRESERVE** | Cadence follows mapped body heading; physical room displacement uses the fixed reference map rather than live heading. |
| Jump | **MOTION ENGINE V1 REQUIREMENT / BATCH 3** | Physical jump must drive vertical game movement using coherent support/body evidence, distinguish single-leg lift, reject noise, have takeoff/airborne/landing lifecycle and useful tuning. |
| Crouch | **MOTION ENGINE V1 REQUIREMENT / BATCH 3** | Physical crouch must lower the character using normalized body-compression/height evidence, held state, hysteresis and useful tuning. |
| Final Motion Engine completion sequence | **APPROVED: BATCH 1 DOCS -> BATCH 2 HORIZONTAL -> BATCH 3 VERTICAL/FINAL** | Prevents deferred foundations from re-entering scope and keeps locomotion completion staged. |
| USER/runtime testing during completion sequence | **DEFER UNTIL ALL THREE BATCHES ARE IMPLEMENTED** | Batch 1 needs no USER runtime test; Batch 2 should not stop awaiting USER QA; Batch 3 prepares one final integrated Motion Engine QA. |
| Builder/static validation | **USEFUL BUT NOT ACCEPTANCE** | Deterministic/compile/static checks can catch implementation defects but cannot create USER runtime acceptance. |
| Phase 6 | **NOT STARTED** | Graybox/playable vertical-slice integration follows Motion Engine V1 completion; do not start it during the completion batches without new USER direction. |
| Hub/course implementation | **NOT STARTED BY THIS ENGINE ROADMAP** | Documentation synchronization does not imply Hub/course gameplay has begun. |
| Motion Engine branch | **CURRENT WORKFLOW DECISION** | Continue core engine work on `engine/pose-tracking-spike`; do not merge to `main` without explicit USER approval. |

## Current completion plan

### Batch 1 — documentation synchronization

Documentation only. No runtime/source/workflow/package/project-setting changes.

### Batch 2 — horizontal locomotion completion

Audit existing Phase 5A, fix swing-leg false translation, preserve/regression-check lean suppression, improve cadence acquisition, make cadence distance/speed clearly Inspector-tunable, and verify lateral/depth/heading/recenter/fusion coherence. Do not add jump/crouch yet.

### Batch 3 — vertical locomotion + final Motion Engine V1 completion

Implement jump and crouch, distinguish jump from single-leg lift, expose appropriate tuning, preserve Phase 4 body-pose authority, and prepare the final integrated USER Motion Engine QA.

Phase 5A and Motion Engine V1 remain **not USER accepted** until that final QA succeeds.
