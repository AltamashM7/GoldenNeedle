# Golden Needle — Decision Log

Current authority: `Docs/current-state.md`.

Historical foundation design remains in `Docs/pre-phase5a-foundations.md`; corrective/restoration history remains in `Docs/foundation-corrective-pass-progress.md`; detailed optimization chronology remains in `Docs/optimization-orchestrator-handoff.md`. Historical evidence does not override current decisions below.

| Decision | Current status | Rationale / current meaning |
|---|---|---|
| Unity 6.5 / `6000.5.0f1` | **LOCKED** | Preserve the established Unity baseline. |
| URP 17.5.0 | **LOCKED** | Current rendering baseline; presentation remains CPU-conscious. |
| CPU-first / no required dedicated GPU | **LOCKED PRODUCT REQUIREMENT** | Golden Needle must run acceptably on ordinary hardware. |
| Integrated webcam input | **LOCKED PRODUCT REQUIREMENT** | No VR/depth hardware or external tracking application is required. |
| Replaceable pose-provider boundary | **LOCKED ARCHITECTURAL DIRECTION** | MediaPipe-specific structures remain upstream; downstream consumes project-owned canonical contracts. |
| Stock MediaPipe/TFLite CPU | **LOCKED FALLBACK / REFERENCE** | Retain the known-safe reference path. |
| OpenVINO CPU FP32 | **USER-VALIDATED BEST-TESTED LOW-END BACKEND** | Preserve the accepted accelerated body-inference path. |
| WebCamCPU/GetPixels32 acquisition | **USER ACCEPTED — PASS FOR CURRENT MILESTONE** | Reusable CPU pixels + reusable 320x240 preparation removed the dominant accepted readback bottleneck. |
| ExistingReadback | **LOCKED FALLBACK / REFERENCE** | Keep the older acquisition/readback route for fallback and comparison. |
| Body input around 320x240 | **CURRENT BEST-TESTED BASELINE** | Accepted low-end quality/performance baseline. |
| Persistent OpenVINO worker + newest-only two-slot mailbox | **USER ACCEPTED — PASS** | At most one active inference and one replaceable newest pending frame. |
| Latest useful frame wins | **LOCKED RUNTIME RULE** | Avoid stale-frame backlog/replay/catch-up. |
| CanonicalBodyV1 = 20 joints | **PRESERVE AS V1 CONTRACT** | Stable project-owned body contract with partial validity. |
| Stable Phase 3 canonical frame | **LOCKED CALIBRATION / LOCOMOTION AUTHORITY** | Calibration and Phase-5 action interpretation use the stabilized canonical path. |
| Selectable Raw/Responsive avatar-drive sources | **PRESERVE FOR OPTIONAL TUNING** | Avatar responsiveness experiments do not replace stable calibration/locomotion authority. |
| Phase 4 signed canonical-to-avatar mapping | **USER ACCEPTED — PRODUCTION AUTHORITY** | Preserve accepted orientation/axis semantics. |
| Phase 4 positional targets + analytic two-bone IK | **USER ACCEPTED — PRODUCTION LIMB AUTHORITY** | Preserve avatar-authored proportions and trusted endpoint solving. |
| Phase 4 modular calibration | **USER ACCEPTED** | Body reference and per-chain geometry remain independently usable. |
| Fabricating unobservable monocular twist | **REJECTED FOR NORMAL PRODUCTION** | Single-RGB evidence does not justify inventing free axial DOFs. |
| Production Foundation-E post-solve layer | **SUPERSEDED / FALSE FOR CURRENT RUNTIME** | Production execution remains Phase 4 solve -> Phase 5 root translation/presentation. |
| Performance optimization milestone | **USER SATISFIED / FROZEN FOR CURRENT HACKATHON MILESTONE** | Reopen only if new reproducible evidence warrants it. |
| Foundation A unified command/action layer | **RETAINED / WORKING** | Keyboard/speech/future inputs invoke the same actions. |
| Windows fixed-vocabulary speech | **USER QA PASSED / RETAINED** | Configured phrases dispatched through the shared command router. |
| Product-default speech confidence | **LOW FOR ALL 14 DEFAULT MAPPINGS** | Matches real USER noisy-environment evidence; custom mappings retain Medium default. |
| Speech wake prefix | **CONFIGURABLE / EMPTY BY DEFAULT** | Optional future accidental-trigger safeguard. |
| Foundation B camera presets | **RETAINED** | Back/Front/Left/Right/FullBody/Hands/LeftHand/RightHand remain one primary-camera system. |
| F12 Lab/Game presentation | **RETAINED / ORTHOGONAL TO CAMERA PRESET** | View mode remains separate from camera preset selection. |
| Foundation C rich anatomical orientation | **DEFERRED / DORMANT RESEARCH** | Not production pose authority. |
| Foundation D detailed Hand Landmarker | **DEFERRED** | USER testing showed unacceptable low-end cost. |
| Foundation E post-Phase-4 detail | **RETIRED FROM PRODUCTION** | Historical research may remain but no live production composition. |
| Coarse hand/fist experiment | **DEFERRED / ROLLED BACK** | USER rejected the feature/value tradeoff for the current milestone. |
| Phase 5 root translation executes after Phase 4 | **LOCKED V1 ARCHITECTURE** | Phase 4 remains bone/IK authority; Phase 5 is additive root translation only. |
| Phase 5 locomotion input | **STABILIZED CANONICAL POSE ONLY** | No additional inference/model is required for horizontal, jump or crouch. |
| Horizontal physical tracking authority | **SUPPORT-AWARE CAMERA-SPACE AUTHORITY** | Trusted ankle/heel/toe measurements feed stateful Both/Left/Right support authority. |
| Support authority hysteresis | **0.12 SINGLE ENTER / 0.06 BOTH** | Normalized foot-height separation avoids mode flapping. |
| Unconditional two-foot midpoint authority | **REJECTED** | A raised swing leg can shift the midpoint while the support foot is planted. |
| Swing-foot false translation | **BATCH 2 FIX RETAINED** | Raised/moving swing foot alone must not translate physical X/Z. |
| Support transition continuity | **LOCKED BATCH-2 BEHAVIOR** | Support-mode changes continuity-rebase to prevent teleports. |
| Batch-2R alternating-step continuity | **COMPLETE / RETAINED** | A landing rebase may release only after coherent stable dual-support relocation so a real alternating step eventually produces net room displacement. |
| Tracking-loss rebase release | **DISALLOWED** | Reacquisition continuity offsets are not auto-released as alternating-step relocation. |
| Planted-feet torso lean | **SUPPRESSED / RETAINED** | Torso-only lateral/scale changes do not create physical room translation. |
| Cadence event threshold | **0.07 / RETAINED** | Batch 2 improved acquisition without weakening event evidence. |
| Cadence acquisition | **2 EVENTS / 0.38 ACQUIRE CONFIDENCE** | Two clean alternating events acquire; one isolated event does not. |
| Cadence travel | **0.60 DISTANCE PER STEP / 3.0 MAX SPEED** | Stronger prototype baseline, still Inspector-tunable. |
| Cadence architecture in Batch 3 | **UNCHANGED** | Jump/crouch does not redesign cadence. |
| Horizontal Recenter | **IMPLEMENTED / XZ-ONLY** | Current physical X/Z becomes new physical origin while virtual world position is preserved. |
| Vertical reference coupled to Recenter | **NO** | Horizontal recenter does not silently redefine standing jump/crouch reference or root-Y origin. |
| Body heading for cadence | **IMPLEMENTED / RETAINED** | Cadence follows mapped body heading; physical room displacement uses the fixed reference map. |
| Dedicated vertical interpreter | **BATCH 3 CURRENT ARCHITECTURE** | `VerticalLocomotionInterpreter` owns standing reference, vertical action state and proportional Y output rather than burying calculations in the controller. |
| Vertical standing reference | **RUNTIME / CALIBRATION-SESSION SCOPED** | Capture only from valid calibrated, trustworthy, reasonably standing lower-body/torso geometry; do not continuously redefine during actions. |
| Jump definition | **COHERENT WHOLE-BODY RISE** | Both support feet, pelvis and chest must rise coherently with bounded foot asymmetry/spread and stable apparent scale. |
| Single-leg lift as jump | **REJECTED** | One raised leg is insufficient vertical-action evidence. |
| Jump lifecycle | **GROUNDED -> TAKEOFF -> AIRBORNE -> LANDING -> GROUNDED** | Enter/release hysteresis and bounded tracking grace prevent flicker/stuck airborne state. |
| Jump root movement | **PROPORTIONAL TRACKED ROOT-Y** | Measured normalized rise maps through Inspector scale/clamp; no fixed animation or ballistic jump. |
| Crouch definition | **GROUNDED PELVIS-TO-SUPPORT COMPRESSION** | Support remains approximately grounded while normalized pelvis/support height compresses; pelvis/chest move downward. |
| Crouch hold/release | **STATEFUL HYSTERESIS** | Separate enter/release thresholds allow indefinite valid hold and stable recovery. |
| Jump/crouch mutual exclusion | **LOCKED BATCH-3 BEHAVIOR** | One explicit vertical state machine owns the action; recovery from crouch returns through Standing. |
| Vertical root-Y origin | **CALIBRATION-SESSION ROOT ORIGIN** | Controller captures player root Y when a valid calibration session initializes. |
| Vertical root application | **`verticalOriginY + verticalSample.worldOffsetY`** | Phase-5 additive root translation after Phase 4. |
| Calibration invalidation with vertical offset | **RESET + RESTORE STANDING Y** | Do not leave the avatar stuck in jump/crouch when body reference becomes invalid. |
| Missing vertical evidence | **NO FABRICATION / BOUNDED GRACE** | Briefly hold acquired state, then return toward neutral/unavailable; horizontal/pose systems remain independent. |
| Jump/depth cross-talk | **SUPPRESS AT CONTROLLER/FUSION BOUNDARY** | Hold pre-jump physical depth and zero depth velocity only while jump/landing is active; do not rewrite Batch-2R root tracker. |
| Lateral physical motion during jump | **PRESERVED** | Jump-depth suppression affects only physical depth component, not lateral X. |
| Gameplay physics in Motion Engine V1 | **DEFERRED** | No CharacterController, Rigidbody gravity, collision/ground probing or course logic in Batch 3. |
| Batch-3 vertical joint confidence | **0.40** | Practical default consistent with current stabilized pose confidence conventions. |
| Jump thresholds | **0.12 ENTER / 0.045 RELEASE** | Normalized coherent rise hysteresis. |
| Jump world mapping | **1.60 SCALE / 0.90 MAX** | Inspector-tunable proportional visible root-Y response. |
| Maximum jump foot asymmetry | **0.08** | Reject asymmetric/single-leg rise. |
| Crouch thresholds | **0.18 ENTER / 0.09 RELEASE** | Normalized pelvis-support compression hysteresis. |
| Crouch world mapping | **1.20 SCALE / 0.65 MAX** | Inspector-tunable proportional negative root-Y response. |
| Vertical response | **18/S** | Responsive smoothing without intentionally adding large jump latency. |
| Vertical tracking grace | **0.16 S** | Brief lower-body loss after valid acquisition does not instantly cancel action. |
| Apparent-scale acquisition guard | **0.12 LOG-SCALE** | Reject camera-depth/zoom-like change as jump/crouch evidence. |
| Jump coherence spread | **0.10** | Require feet/pelvis/chest to move together. |
| Grounded support tolerance | **0.06** | Crouch requires support base to remain approximately planted. |
| Batch-3 diagnostics | **EXISTING LAB LOCOMOTION DISPLAY EXTENDED** | Show vertical state/phase, signals, Y offset and reference readiness without per-frame Console logging. |
| Existing Batch-2/2R tests in Batch 3 | **UNCHANGED** | Vertical tests are added separately; horizontal coverage is not weakened. |
| Builder Unity test evidence | **NO UNITY RUN IN CURRENT ENVIRONMENT** | NUnit Editor tests are authored/static-audited but not claimed as executed unless real runner evidence exists. |
| USER/runtime testing | **FINAL COMPREHENSIVE QA PENDING** | Batch 3 completes implementation but does not create USER acceptance. |
| Motion Engine V1 | **NOT YET USER ACCEPTED** | Requires final integrated USER Unity QA after Orchestrator review. |
| Phase 6 | **NOT STARTED** | Do not begin graybox/Hub/course work during Batch-3 closeout. |
| Motion Engine branch | **`engine/pose-tracking-spike`** | Continue engine work here; do not merge to `main` without explicit USER approval. |

## Current completion sequence

- Batch 1 — **COMPLETE**.
- Batch 2 — **COMPLETE**.
- Batch 2R — **COMPLETE**.
- Batch 3 — **IMPLEMENTED / USER QA PENDING**.
- Motion Engine V1 — **NOT YET USER ACCEPTED**.
- Phase 6 — **NOT STARTED**.

Batch-3 starting SHA: `e6044b45d94dbe4ee9da3702828c1ee73409d475`.

Batch-3 implementation/tests checkpoint: `220a4b958c8a3d280799ea3c0836fa6536a486a0`.

Current status: `BATCH 3 IMPLEMENTED / USER QA PENDING / AWAITING ORCHESTRATOR REVIEW`.
