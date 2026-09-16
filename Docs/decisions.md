# Golden Needle — Decision Log

Current authority: `Docs/current-state.md`.

Gameplay architecture authority: `Docs/gameplay-foundation-plan.md`.

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
| Foundation A commands/speech | **RETAINED / EXPAND FOR GAMEPLAY** | Shared action router remains; add contextual gameplay commands rather than replacing it. |
| Foundation B camera presets | **RETAINED** | Keep current camera/presentation controls. |
| Foundation C | **DEFERRED / DORMANT** | Not production authority. |
| Foundation D detailed hands | **DEFERRED** | Low-end runtime cost unacceptable. |
| Foundation E | **RETIRED FROM PRODUCTION** | No live extra pose layer. |
| Coarse hands | **DEFERRED / ROLLED BACK** | Not part of Motion Engine acceptance. |
| Phase-5 input | **STABILIZED CANONICAL POSE ONLY** | No extra model/inference. |
| Physical X/Z position state | **ONE OWNER: `CameraSpaceRootTracker`** | Tracker alone owns accepted displacement, filtering, recenter and reacquisition continuity. |
| Horizontal authority reconstruction | **FROZEN** | USER reported idle/root stability substantially improved; do not reopen without new gameplay evidence. |
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
| Legacy interpreter grounded `worldOffsetY` | **COMPATIBILITY / DIAGNOSTIC, NOT PRODUCTION GROUNDED ROOT-Y** | Controller uses avatar-relative mapping for non-Jump grounded Y. |
| Production grounded root-Y scale | **AVATAR-RELATIVE STANDING LEG GEOMETRY** | Same normalized USER compression scales by controlled avatar reference-leg size. |
| Avatar standing leg scale | **BILATERAL REFERENCE-POSE ROOT-TO-FOOT UP SPAN** | Prefer anatomical vertical span; fallback direct span then cached chain reach. Stable across live crouch. |
| Avatar crouch depth multiplier | **1.20 DIMENSIONLESS** | Applied after normalized compression and avatar standing-leg scale. |
| Maximum crouch depth | **0.65 OF AVATAR STANDING LEG SCALE** | Leg-relative cap replaces fixed absolute production cap. |
| Solved avatar foot motion as crouch-depth input | **REJECTED** | Feet do not decide how far the USER crouched. |
| Post-root full crouch leg IK | **REMOVED / REJECTED** | Runtime no longer calls a second two-bone leg solve after Phase-5 root movement. |
| Grounded residual correction | **NONE IN ACCEPTED V1** | Current accepted behavior uses avatar-relative root mapping without another corrective leg solve. |
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
| F9 crouch diagnostics | **COMPRESSION / AVATAR LEG SCALE / PRIMARY TARGET / FINAL Y / CAP / RESIDUAL=OFF** | Diagnostics reflect accepted V1 authority. |
| Old `GroundedFootConstraint` | **REMOVED** | Full post-root foot endpoint IK encoded rejected tucked-leg behavior. |
| Existing reconstructed horizontal tests | **UNTOUCHED** | Do not weaken successful authority expectations. |
| Existing semantic vertical tests | **UNTOUCHED** | Interpreter semantics remain source-compatible. |
| Avatar-relative crouch tests | **ADDED / STATICALLY AUDITED** | Includes avatar-proportion/chibi regression and Phase-4 leg-rotation preservation. |
| Unity Editor tests in Builder environment | **UNAVAILABLE** | Do not claim automated Unity pass without real runner evidence. |
| Motion Engine V1 | **USER ACCEPTED FOR CURRENT PROJECT SCOPE** | Accepted with an acknowledged crouch/ground-contact fidelity limitation. |
| Accepted Motion Engine implementation baseline | **`140a939160530394ee5c70aa1dbc31c62f3a12e1`** | Frozen production-motion reference unless explicitly reopened. |
| Motion Engine -> `main` integration | **COMPLETED WITH USER APPROVAL** | Accepted pose branch was merged to `main`; core motion is now mainline foundation. |
| Gameplay development branch | **`gameplay/foundation`** | Create from refreshed `main`; keep scene-independent gameplay foundation work here. |
| Persistent player | **APPROVED** | One reusable `GoldenNeedlePlayer.prefab` should own motion/avatar/session-facing player state and survive scene changes. |
| Calibration lifecycle | **ONCE PER NORMAL PLAY SESSION** | Calibration should persist from Calibration -> Hub -> activities instead of repeating on every scene load. |
| Gameplay facade | **APPROVED** | Game systems depend on `GoldenNeedlePlayerFacade` or equivalent, not Motion Engine internals. |
| Player health/damage | **GENERIC PLAYER CAPABILITY** | Reusable health/damage receiver belongs with player-facing gameplay foundation; mode-specific damage rules stay in modes. |
| Body gameplay anchors | **APPROVED** | Expose wrist/foot anchors for gameplay without coupling boxing logic to pose-provider internals. |
| Boxing strike hitboxes | **OPTIONAL PLAYER CAPABILITY / MODE-GATED** | Can live as disabled capability on/under player but must only become active in Boxing. |
| Persistent scene flow | **SEPARATE `GameFlowManager`-STYLE OWNER** | Fades, scene loading, Hub return, spawn placement and duplicate-session prevention are not Motion Engine responsibilities. |
| Scene-specific gameplay | **SCENE CONTROLLERS** | Calibration, Hub, Boxing and Obstacle rules stay outside Motion Engine/player internals. |
| Speech low-latency command | **APPROVED** | `Reduce latency` / `Low latency mode` switches visible avatar presentation to `RawCanonical`. |
| Speech smooth-motion command | **APPROVED** | `Smooth motion` / `Stabilized mode` switches visible avatar presentation to `StabilizedCanonical`. |
| Calibration/locomotion authority during raw presentation | **STILL STABILIZED** | Presentation mode must not change stabilized calibration/locomotion authority. |
| Speech command availability | **CONTEXT-AWARE** | Enable commands appropriate to scene/state; keep lab/debug phrases out of normal gameplay. |
| Global `Return to Hub` voice command | **NOT APPROVED AS ALWAYS-ON** | Accidental recognition during activities is too disruptive; require context/confirmation if added later. |
| Four-scene product structure | **APPROVED** | Calibration -> Hub -> Boxing/Obstacle -> Hub. |
| Calibration environment | **MERGED TO `main`** | `Assets/Scenes/Caliberation.unity`. |
| Obstacle environment | **MERGED TO `main`** | `Assets/Scenes/Obstacle Course.unity`. |
| Hub environment | **MERGED TO `main`** | `Assets/Scenes/GoldenNeedle_Hub.unity`. |
| Boxing environment | **PENDING LOCAL BAKE/MERGE** | Not a blocker for gameplay-foundation work; integrate into `main` when ready. |
| Environment merge conflict priority | **CURRENT `main` MOTION/PROJECT CONFIG WINS BY DEFAULT** | Do not let older environment branches overwrite accepted Motion Engine or current project/package settings without explicit decision. |
| Boxing pending while foundation starts | **APPROVED** | Start environment-independent gameplay foundation now; avoid boxing-scene-specific wiring until its environment is integrated. |
| Later boxing integration into gameplay branch | **NORMAL MERGE FROM UPDATED `main`** | No rebase/history rewrite. |

## Motion Engine corrective lineage

- USER-tested tucked-crouch runtime: `9cda38b5c47f38d91800839cdd99db1d52fb0916`;
- avatar-relative reconstruction handoff: `5e90551b7045dba96787c43fb9d4562d3438257a`;
- avatar-relative implementation/tests/diagnostics: `2c9c3f6eaf4801e1832b32ad7afc79bb6597807b`;
- final accepted Motion Engine documentation baseline: `140a939160530394ee5c70aa1dbc31c62f3a12e1`.

Historical comparison points used during diagnosis:

- original Batch-3 vertical implementation: `220a4b958c8a3d280799ea3c0836fa6536a486a0`;
- solved-foot root-Y experiment: `86701d0082692c17844db89c03fe4753e1876cf6`;
- pre-Foundation basic locomotion reference: `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0`.

## Mainline integration lineage

- accepted Motion Engine merged/fast-forwarded into `main`: `880b75cf15acd030b5474eed97a7f74126ccf4e3`;
- Calibration + Obstacle environment integration: `67d5de51f58d900df5df7f5b7ce91075e01f89c6`;
- Hub environment integration: `3fb68ecfe245ff36c16a7752108248c1c433734c`.

Current status: **MOTION ENGINE V1 ACCEPTED / GAMEPLAY FOUNDATION NEXT / BOXING ENVIRONMENT MERGE PENDING**.
