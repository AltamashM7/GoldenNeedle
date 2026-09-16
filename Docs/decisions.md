# Golden Needle — Decision Log

Current authority: `Docs/current-state.md`.

Historical foundation design remains in `Docs/pre-phase5a-foundations.md`; optimization chronology remains in `Docs/optimization-orchestrator-handoff.md`. Historical evidence may explain regressions but does not override current decisions below.

| Decision | Current status | Current meaning |
|---|---|---|
| Unity 6.5 / `6000.5.0f1` | **LOCKED** | Preserve established Unity baseline. |
| URP 17.5.0 | **LOCKED** | Preserve current render baseline. |
| CPU-first / no dedicated GPU required | **LOCKED PRODUCT REQUIREMENT** | Must remain viable on ordinary hardware. |
| Integrated webcam input | **LOCKED PRODUCT REQUIREMENT** | No VR/depth hardware required. |
| CanonicalBodyV1 + replaceable provider boundary | **LOCKED** | Provider-specific data stays upstream. |
| OpenVINO CPU FP32 + WebCamCPU/GetPixels32 | **USER-VALIDATED LOW-END PATH / FROZEN** | Preserve accepted optimized path unless new evidence warrants reopening. |
| Stock MediaPipe/TFLite + ExistingReadback | **FALLBACK / REFERENCE** | Keep known-safe comparison/fallback paths. |
| Newest-only two-slot mailbox | **USER ACCEPTED** | No stale-frame FIFO/catch-up. |
| Stable Phase 3 frame | **LOCKED CALIBRATION / LOCOMOTION AUTHORITY** | Phase 5 consumes stabilized canonical evidence. |
| Phase 4 signed mapping + positional analytic IK | **USER ACCEPTED — SOLE NORMAL LEG-POSE AUTHORITY** | General Phase-4 mapping/targets/IK remain unchanged; crouch no longer re-solves legs afterward. |
| Phase 4 modular calibration | **USER ACCEPTED** | Preserve body/chain calibration separation. |
| Fabricated monocular free twist | **REJECTED** | Do not invent unobservable axial DOFs. |
| Foundation A commands/speech | **RETAINED / WORKING** | Shared action router remains. |
| Foundation B camera presets | **RETAINED** | Keep current camera/presentation controls. |
| Foundation C | **DEFERRED / DORMANT** | Not production authority. |
| Foundation D detailed hands | **DEFERRED** | Low-end runtime cost unacceptable. |
| Foundation E | **RETIRED FROM PRODUCTION** | No live extra pose layer. |
| Coarse hands | **DEFERRED / ROLLED BACK** | Not part of Motion Engine acceptance. |
| Phase-5 input | **STABILIZED CANONICAL POSE ONLY** | No extra model/inference. |
| Physical X/Z position state | **ONE OWNER: `CameraSpaceRootTracker`** | Tracker alone owns accepted displacement, filtering, recenter and reacquisition continuity. |
| Horizontal authority reconstruction | **FROZEN FOR THIS PASS** | USER reported idle/root stability substantially improved; do not reopen without new evidence. |
| Body/root candidate | **TORSO CENTER + YAW-COMPENSATED APPARENT SCALE** | Continuous camera-space candidate from reconstructed authority path. |
| Support feet for horizontal movement | **VALIDATION** | Bilateral ankle/heel/toe evidence validates body candidate; not another X/Z owner. |
| Support mode | **BOTH/LEFT/RIGHT VALIDATION HYSTERESIS** | `0.12` single-enter / `0.06` dual-return retained. |
| Single/swing foot as room translation | **REJECTED** | New physical commit requires trustworthy coherent dual support. |
| Torso lean without support relocation | **REJECTED** | Body candidate alone cannot commit. |
| Scale change without support relocation | **REJECTED** | Prevent false forward/back translation. |
| `LocomotionFusion` authority | **MAPPING/BLENDING ONLY** | No second position state machine. |
| Cadence tuning | **RETAINED** | `0.07` event; 2 events / `0.38` acquire; `0.25` sustain; `0.50 s` stop; `0.60` per step; `3.0` max. |
| Horizontal recenter | **XZ-ONLY / WORLD-POSITION PRESERVING** | Physical origin resets without teleporting avatar. |
| Vertical semantic authority | **`VerticalLocomotionInterpreter` ONLY** | Jump/Crouch semantic state remains one state machine. |
| Semantic Crouch thresholds | **0.18 ENTER / 0.09 RELEASE** | Gameplay state remains separate from continuous shallow-bend descent. |
| Primary grounded crouch evidence | **NORMALIZED PELVIS-TO-SUPPORT COMPRESSION** | USER body compression remains the source of crouch amount. |
| Shallow grounded bend | **MAY LOWER ROOT WHILE STATE=`Standing`** | Motion/state intentionally separated. |
| Legacy interpreter grounded `worldOffsetY` | **COMPATIBILITY / DIAGNOSTIC, NOT PRODUCTION GROUNDED ROOT-Y** | Interpreter remains unchanged in this narrow pass; controller uses avatar-relative mapping for non-Jump grounded Y. |
| Production grounded root-Y scale | **AVATAR-RELATIVE STANDING LEG GEOMETRY** | Same normalized USER compression scales by controlled avatar reference-leg size. |
| Avatar standing leg scale | **BILATERAL REFERENCE-POSE ROOT-TO-FOOT UP SPAN** | Prefer anatomical vertical span; fallback direct span then cached chain reach. Stable across live crouch. |
| Avatar crouch depth multiplier | **1.20 DIMENSIONLESS** | Applied after normalized compression and avatar standing-leg scale. |
| Maximum crouch depth | **0.65 OF AVATAR STANDING LEG SCALE** | Leg-relative cap replaces fixed absolute production cap. |
| Solved avatar foot motion as crouch-depth input | **REJECTED** | Feet do not decide how far the USER crouched. |
| Post-root full crouch leg IK | **REMOVED / REJECTED** | Runtime no longer calls a second two-bone leg solve after Phase-5 root movement. |
| Grounded residual correction | **NONE IN CURRENT RECONSTRUCTION** | Evaluate avatar-relative root mapping first; diagnostics explicitly report `residual=off`. |
| Future residual, if evidence requires it | **ROOT-ONLY / SMALL / AVATAR-RELATIVE / BOUNDED** | Must not re-author knees or become primary crouch authority. |
| Phase-4 knee pose during crouch | **PRESERVED** | No crouch-specific leg rotation writes after Phase 4. |
| Jump definition | **COHERENT WHOLE-BODY RISE** | Existing bilateral support + pelvis + chest safeguards retained. |
| Single-leg lift as jump | **REJECTED** | Existing behavior retained. |
| Jump lifecycle | **GROUNDED -> TAKEOFF -> AIRBORNE -> LANDING -> GROUNDED** | Retained. |
| Jump thresholds | **0.12 ENTER / 0.045 RELEASE** | Retained. |
| Jump positive-Y authority | **EXCLUSIVE WHILE JUMP ACTIVE** | Controller bypasses grounded crouch mapper and clamps jump Y non-negative. |
| Jump/depth cross-talk | **CONTROLLER/FUSION BOUNDARY HOLD** | Pre-jump depth held and depth velocity zeroed while jump owns vertical motion. |
| Vertical response / grace | **18/S / 0.16 S** | Retained and reused by avatar-relative crouch mapping. |
| Grounded support/asymmetry gating | **INTERPRETER-OWNED / RETAINED** | Existing evidence determines whether grounded bend is trustworthy. |
| Gameplay physics | **DEFERRED** | No CharacterController, Rigidbody gravity, raycast terrain grounding or course logic. |
| F9 crouch diagnostics | **COMPRESSION / AVATAR LEG SCALE / PRIMARY TARGET / FINAL Y / CAP / RESIDUAL=OFF** | Diagnostics reflect current authority; no stale grounded-foot-lock claims. |
| Old `GroundedFootConstraint` | **REMOVED** | Full post-root foot endpoint IK encoded rejected tucked-leg behavior. |
| Existing reconstructed horizontal tests | **UNTOUCHED** | Do not weaken successful authority expectations. |
| Existing semantic vertical tests | **UNTOUCHED** | Interpreter semantics remain source-compatible. |
| Avatar-relative crouch tests | **ADDED / STATICALLY AUDITED** | Includes avatar-proportion/chibi regression and Phase-4 leg-rotation preservation. |
| Unity Editor tests in Builder environment | **UNAVAILABLE** | Do not claim automated Unity pass without real runner evidence. |
| Motion Engine V1 | **NOT YET USER ACCEPTED** | Requires genuine USER Unity webcam QA. |
| Phase 6 | **NOT STARTED** | Do not begin until Motion Engine result is reviewed. |
| `main` merge | **EXPLICIT USER APPROVAL REQUIRED** | Engine remains on `engine/pose-tracking-spike`. |

## Current corrective lineage

- USER-tested tucked-crouch runtime: `9cda38b5c47f38d91800839cdd99db1d52fb0916`;
- avatar-relative reconstruction handoff: `5e90551b7045dba96787c43fb9d4562d3438257a`;
- avatar-relative implementation/tests/diagnostics: `2c9c3f6eaf4801e1832b32ad7afc79bb6597807b`.

Historical comparison points used during diagnosis:

- original Batch-3 vertical implementation: `220a4b958c8a3d280799ea3c0836fa6536a486a0`;
- solved-foot root-Y experiment: `86701d0082692c17844db89c03fe4753e1876cf6`;
- pre-Foundation basic locomotion reference: `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0`.

Current status: **AVATAR-RELATIVE CROUCH GROUNDING RECONSTRUCTED / USER QA PENDING**.
