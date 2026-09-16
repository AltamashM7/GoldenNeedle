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
| Phase 4 signed mapping + positional analytic IK | **USER ACCEPTED — POSE AUTHORITY** | Do not move Phase-5 corrections into bone/IK ownership. |
| Phase 4 modular calibration | **USER ACCEPTED** | Preserve body/chain calibration separation. |
| Fabricated monocular free twist | **REJECTED** | Do not invent unobservable axial DOFs. |
| Foundation A commands/speech | **RETAINED / WORKING** | Shared action router remains. |
| Foundation B camera presets | **RETAINED** | Keep current camera/presentation controls. |
| Foundation C | **DEFERRED / DORMANT** | Not production authority. |
| Foundation D detailed hands | **DEFERRED** | Low-end runtime cost unacceptable. |
| Foundation E | **RETIRED FROM PRODUCTION** | No live extra pose layer. |
| Coarse hands | **DEFERRED / ROLLED BACK** | Not part of Motion Engine acceptance. |
| Phase 5 after Phase 4 | **LOCKED V1 ARCHITECTURE** | Phase 5 moves root only. |
| Phase-5 input | **STABILIZED CANONICAL POSE ONLY** | No extra model/inference. |
| Physical position state | **ONE OWNER: `CameraSpaceRootTracker`** | Tracker alone owns accepted displacement, filtering, recenter and reacquisition continuity. |
| Body/root candidate | **TORSO CENTER + YAW-COMPENSATED APPARENT SCALE** | Continuous camera-space candidate reconstructed from historical working path. |
| Support feet | **VALIDATION / CONSTRAINT** | Ankle/heel/toe evidence validates body candidate; not a second downstream position owner. |
| Support mode | **BOTH/LEFT/RIGHT VALIDATION HYSTERESIS** | `0.12` single-enter / `0.06` dual-return retained. |
| Single/swing foot as room translation | **REJECTED** | New physical commit requires trustworthy coherent dual support. |
| Torso lean without support relocation | **REJECTED** | Body candidate alone cannot commit. |
| Scale change without support relocation | **REJECTED** | Prevent false forward/back translation. |
| Coherent body + bilateral support relocation | **ACCEPTED PHYSICAL EVIDENCE** | Genuine room movement can commit. |
| Horizontal commit state | **FROM LAST ACCEPTED POSITION** | Small deliberate increments accumulate; idle noise does not continuously retarget root. |
| Lateral evidence floor | **0.012 NORMALIZED** | Reuses long-standing pre-Foundation physical floor, not a downstream gate. |
| Depth evidence | **BODY + SUPPORT DIRECTION AGREEMENT** | Body scale `0.012`, support `0.018`, differential reliability `0.05–0.20`. |
| Root position/velocity response | **10/S / 8/S** | One filter pair in root tracker. |
| Tracking loss | **HOLD + REBASE ON REACQUISITION** | No teleport; later coherent motion remains possible. |
| `LocomotionFusion` authority | **MAPPING/BLENDING ONLY** | No second committed-position/stationary/rebase state machine. |
| Fusion scales | **0.9 LATERAL / 1.5 DEPTH** | Retained. |
| Fusion origin deadzones | **0.012 / 0.012** | Mapping deadzones only, not state ownership. |
| Physical velocity cadence suppression | **RETAINED** | Prevent double-counting cadence while physically translating. |
| Cadence event threshold | **0.07** | Retained. |
| Cadence acquisition | **2 EVENTS / 0.38 ACQUIRE** | Retained. |
| Cadence sustain/stop | **0.25 / 0.50 S** | Retained. |
| Cadence travel | **0.60 PER STEP / 3.0 MAX SPEED** | Retained. |
| Horizontal recenter | **XZ-ONLY / WORLD-POSITION PRESERVING** | Physical origin resets without teleporting avatar. |
| Vertical standing reference | **CALIBRATION-SESSION SCOPED** | Horizontal recenter does not redefine it. |
| Vertical semantic authority | **`VerticalLocomotionInterpreter` ONLY** | Jump/Crouch state remains one state machine. |
| Semantic Crouch thresholds | **0.18 ENTER / 0.09 RELEASE** | Gameplay state separate from continuous body descent. |
| Negative root-Y signal | **PELVIS-TO-SUPPORT COMPRESSION** | Primary continuous grounded bend/crouch translation signal. |
| Shallow grounded bend | **MAY LOWER ROOT WHILE STATE=`Standing`** | Motion/state intentionally separated. |
| Compression motion deadband | **`clamp(0.25*crouchRelease, 0.01, 0.05)`** | Neutral noise suppression without waiting for semantic Crouch. |
| Crouch world mapping | **1.20 SCALE / 0.65 MAX** | Retained. |
| Post-Phase-4 solved-foot root-Y anchor | **REMOVED AS AUTHORITY** | Feet are evidence/constraint, not primary Y position. |
| Jump definition | **COHERENT WHOLE-BODY RISE** | Bilateral support feet + pelvis + chest with scale/asymmetry/spread safeguards. |
| Single-leg lift as jump | **REJECTED** | Retained. |
| Jump lifecycle | **GROUNDED -> TAKEOFF -> AIRBORNE -> LANDING -> GROUNDED** | Retained. |
| Jump thresholds | **0.12 ENTER / 0.045 RELEASE** | Retained. |
| Jump world mapping | **1.60 SCALE / 0.90 MAX** | Retained. |
| Jump positive-Y authority | **EXCLUSIVE WHILE JUMP ACTIVE** | Grounded compression cannot pin takeoff. |
| Jump/depth cross-talk | **CONTROLLER/FUSION BOUNDARY HOLD** | Pre-jump depth held and depth velocity zeroed while jump/landing active. |
| Lateral motion during jump | **PRESERVED** | Depth isolation does not zero X. |
| Vertical response / grace | **18/S / 0.16 S** | Retained. |
| Apparent-scale guard / coherence spread / grounded tolerance | **0.12 / 0.10 / 0.06** | Retained. |
| Gameplay physics | **DEFERRED** | No CharacterController, Rigidbody gravity, collision or course logic in Motion Engine V1. |
| Authority diagnostics | **F9 SHOWS CANDIDATE / SUPPORT VALIDATION / ACCEPTED ROOT + VERTICAL STATE** | No per-frame logging. |
| Obsolete stability/grounding tests | **REMOVED** | They encoded removed fusion-gate and solved-foot-Y authority. |
| Unity Editor tests in Builder environment | **UNAVAILABLE** | Do not claim automated Unity pass without real runner evidence. |
| Motion Engine V1 | **NOT YET USER ACCEPTED** | Requires genuine integrated USER Unity webcam QA. |
| Phase 6 | **NOT STARTED** | Do not begin until Motion Engine result is reviewed. |
| `main` merge | **EXPLICIT USER APPROVAL REQUIRED** | Engine remains on `engine/pose-tracking-spike`. |

## Reconstruction lineage

- early body-root prototype: `87698948b12cd10b6fef2072d0ad0ce9eeaecdfe`;
- support-base correction: `33698719a2907d30bb3396f66e5b79e59ccbfe9e`;
- last known good pre-Foundation Phase 5: `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0`;
- immediate pre-reconstruction runtime: `466d65826ab747493c211e1a3250aafa305167c8`;
- reconstruction baseline/handoff: `08d7ea5785dd5935ed7240f004313b2a769d7a52`;
- published reconstruction checkpoint: `dc48e9d2f18c65b1631fbdb390e60578087a1303`.

Current status: **PHASE-5 AUTHORITY RECONSTRUCTION IMPLEMENTED / USER QA PENDING**.
