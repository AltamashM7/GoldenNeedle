# Golden Needle — Decision Log

Current authority: `Docs/current-state.md`.

Historical foundation design remains in `Docs/pre-phase5a-foundations.md`; optimization chronology remains in `Docs/optimization-orchestrator-handoff.md`. Historical evidence can explain regressions but does not override current decisions below.

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
| Phase 4 signed mapping + positional analytic IK | **USER ACCEPTED — NORMAL POSE AUTHORITY** | General Phase-4 mapping, target generation and solve remain unchanged. |
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
| Body/root candidate | **TORSO CENTER + YAW-COMPENSATED APPARENT SCALE** | Continuous camera-space candidate from reconstructed authority path. |
| Support feet for horizontal movement | **VALIDATION** | Bilateral ankle/heel/toe evidence validates body candidate; not another X/Z owner. |
| Support mode | **BOTH/LEFT/RIGHT VALIDATION HYSTERESIS** | `0.12` single-enter / `0.06` dual-return retained. |
| Single/swing foot as room translation | **REJECTED** | New physical commit requires trustworthy coherent dual support. |
| Torso lean without support relocation | **REJECTED** | Body candidate alone cannot commit. |
| Scale change without support relocation | **REJECTED** | Prevent false forward/back translation. |
| Coherent body + bilateral support relocation | **ACCEPTED PHYSICAL EVIDENCE** | Genuine room movement can commit. |
| Horizontal commit state | **FROM LAST ACCEPTED POSITION** | Slow deliberate increments accumulate; idle noise does not continuously retarget root. |
| Tracking loss | **HOLD + REBASE ON REACQUISITION** | No teleport; later coherent motion remains possible. |
| `LocomotionFusion` authority | **MAPPING/BLENDING ONLY** | No second committed-position/stationary/rebase state machine. |
| Cadence tuning | **RETAINED** | `0.07` event; 2 events / `0.38` acquire; `0.25` sustain; `0.50 s` stop; `0.60` per step; `3.0` max. |
| Horizontal recenter | **XZ-ONLY / WORLD-POSITION PRESERVING** | Physical origin resets without teleporting avatar. |
| Vertical standing reference | **CALIBRATION-SESSION SCOPED** | Horizontal recenter does not redefine it. |
| Vertical semantic authority | **`VerticalLocomotionInterpreter` ONLY** | Jump/Crouch state remains one state machine. |
| Semantic Crouch thresholds | **0.18 ENTER / 0.09 RELEASE** | Gameplay state separate from continuous body descent. |
| Negative root-Y signal | **PELVIS-TO-SUPPORT COMPRESSION** | Primary continuous grounded bend/crouch translation signal. |
| Shallow grounded bend | **MAY LOWER ROOT WHILE STATE=`Standing`** | Motion/state intentionally separated. |
| Crouch world mapping | **1.20 SCALE / 0.65 MAX** | Retained. |
| Solved avatar foot motion as root-Y input | **REJECTED** | Feet must never decide tracked crouch depth/root position. |
| Jump definition | **COHERENT WHOLE-BODY RISE** | Bilateral support + pelvis + chest with scale/asymmetry/spread safeguards. |
| Single-leg lift as jump | **REJECTED** | Retained. |
| Jump lifecycle | **GROUNDED -> TAKEOFF -> AIRBORNE -> LANDING -> GROUNDED** | Retained. |
| Jump thresholds | **0.12 ENTER / 0.045 RELEASE** | Retained. |
| Jump world mapping | **1.60 SCALE / 0.90 MAX** | Retained. |
| Jump positive-Y authority | **EXCLUSIVE WHILE JUMP ACTIVE** | Grounded correction cannot pin takeoff. |
| Jump/depth cross-talk | **CONTROLLER/FUSION BOUNDARY HOLD** | Pre-jump depth held and depth velocity zeroed while jump/landing active. |
| Vertical response / grace | **18/S / 0.16 S** | Retained. |
| Grounded support tolerance | **0.06** | Reused by grounded-foot constraint; not a new contact detector. |
| Maximum vertical foot asymmetry | **0.08** | Reused to reject unilateral/swing contact from grounded lock. |
| Grounded-foot standing reference | **PER-FOOT WORLD Y / SESSION-BINDING SCOPED** | Capture only from trustworthy neutral standing; never drift during bend/jump. |
| Grounded-foot target | **CURRENT PHASE-4 X/Z + CAPTURED STANDING Y** | Constrain floor height while preserving live stance X/Z. |
| Grounded-foot active condition | **BILATERAL TRUSTWORTHY GROUNDED BEND/RECOVERY ONLY** | Live vertical evidence, not Jump, support within tolerance, coherent feet, negative/root-recovery Y. |
| Neutral standing leg re-solve | **AVOIDED** | Capture/hold reference but do not continuously solve with zero vertical correction. |
| Jump/takeoff grounded lock | **IMMEDIATE RELEASE** | Never pin airborne feet. |
| Single-leg/swing grounded lock | **RELEASE/SKIP** | Raised foot remains free. |
| Missing leg chain/evidence | **SAFE SKIP** | Do not fabricate contact or alter root. |
| Grounded residual solver | **REUSE `AnalyticTwoBoneIkSolver`** | Existing project IK math/contract reused; no second IK algorithm. |
| Normal `HumanoidRetargeter` behavior | **UNCHANGED** | General Phase-4 solve, arms, mapping, target generation and smoothing untouched. |
| Grounded residual execution order | **AFTER PHASE-5 ROOT WRITE** | Phase 4 @100 -> Phase5 root + residual legs @150 -> diagnostics/presentation @170. |
| Grounded residual bend preference | **CURRENT KNEE -> PREVIOUS RELIABLE -> BINDING REFERENCE** | Preserve knee-side continuity and Phase-4 anatomical convention. |
| Grounded residual root authority | **NONE** | Helper rotates leg root/mid only; endpoint residual never writes root Y. |
| Grounded reach limits | **EXISTING ANALYTIC CLAMP** | Impossible targets clamp safely and are exposed diagnostically. |
| Gameplay physics | **DEFERRED** | No CharacterController, Rigidbody gravity, collision, raycast grounding or course logic. |
| Grounded diagnostics | **F9 REFERENCE / LOCK / Y RESIDUAL / CLAMP** | No per-frame Console logging. |
| Existing reconstructed horizontal tests | **UNTOUCHED IN GROUNDED PASS** | Do not weaken successful authority-reconstruction expectations. |
| Existing reconstructed vertical tests | **UNTOUCHED IN GROUNDED PASS** | Root-Y semantics remain unchanged. |
| Grounded constraint Editor tests | **ADDED / STATICALLY AUDITED** | Actual Unity execution unavailable in Builder environment. |
| Motion Engine V1 | **NOT YET USER ACCEPTED** | Requires genuine integrated USER Unity webcam QA. |
| Phase 6 | **NOT STARTED** | Do not begin until Motion Engine result is reviewed. |
| `main` merge | **EXPLICIT USER APPROVAL REQUIRED** | Engine remains on `engine/pose-tracking-spike`. |

## Current corrective lineage

- Phase-5 authority-reconstruction final docs HEAD before grounded handoff: `92b6a36547e0ab2db043331c4b7cc9c8363e6d90`;
- grounded-foot handoff/baseline: `3390492e9efdd72fa3bab9cc8a58910e7473ed06`;
- grounded-foot implementation/tests/diagnostics: `a3f9e273b575146c3b56a3966d123f48177c5b5a`.

Current status: **GROUNDED FOOT CONSTRAINT IMPLEMENTED / USER QA PENDING**.
