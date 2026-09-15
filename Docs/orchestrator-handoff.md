# Golden Needle — Orchestrator Handoff

Handoff refresh: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## 1. First actions for the next Orchestrator

Before changing anything:

1. verify the live remote `engine/pose-tracking-spike` HEAD;
2. read `Docs/current-state.md` as the primary current authority;
3. read `Docs/optimization-orchestrator-handoff.md` for optimization history and preserved invariants;
4. read `Docs/decisions.md` for current/superseded architectural decisions;
5. inspect the current source before writing any Builder brief;
6. independently verify Builder/repository claims rather than accepting summaries at face value.

Do **not** merge to `main` without explicit USER approval. Do not force-push, rebase, amend, reset, or rewrite published/shared history.

## 2. Current code/runtime baseline

The code/runtime checkpoint entering Motion Engine Completion Batch 1 is:

`f1819fda36547343bb32a972d39405d0a6be6f72`

That commit removed obsolete Foundation-E Editor tests after production `RichHumanoidDetailRetargeter` had already been retired. The preceding corrective pose-baseline work restored normal production composition to the accepted optimization-era Phase 3 + Phase 4 architecture.

The USER subsequently opened Unity with zero red errors and reports that performance appears restored. The USER explicitly decided not to continue performance optimization now.

Current performance milestone:

`USER SATISFIED FOR CURRENT HACKATHON MILESTONE / FURTHER PERFORMANCE WORK DEFERRED`

Do not reopen performance architecture unless new reproducible evidence justifies it.

## 3. Preserved optimized body path

Current best-tested normal body path:

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

Preserve these boundaries:

- stock MediaPipe/TFLite remains fallback/reference;
- ExistingReadback remains fallback/reference;
- at most one active body inference plus one replaceable newest pending frame;
- latest useful frame wins; no FIFO/history/replay/catch-up backlog;
- stable Phase 3 canonical data remains calibration/locomotion authority;
- Phase 4 signed canonical-to-avatar mapping and analytic two-bone IK remain accepted production pose authority;
- monocularly unobservable free axial/twist motion is not fabricated in normal production operation.

The normal downstream production path is now `Phase 4 solve -> presentation`, not `Phase 4 -> Foundation E -> presentation`.

## 4. Current phase status

- Phase 1 — provider/raw pose: **PASS WITH NOTES**.
- Phase 2 — canonical skeleton: **PASS**.
- Phase 3 — stabilization/confidence/calibration foundation: **PASS**.
- Phase 4 — humanoid retargeting: **USER ACCEPTED — PASS**.
- Low-end optimization milestone: **USER SATISFIED / FROZEN FOR CURRENT MILESTONE**.
- Phase 5A — locomotion: **IMPLEMENTED / NOT YET USER ACCEPTED / NOW ACTIVE DEVELOPMENT TARGET**.
- Phase 6 — graybox vertical slice: **NOT STARTED**.

Do not mark Phase 5A or Motion Engine V1 accepted before final integrated USER QA.

## 5. Foundation disposition

### Foundation A — commands/speech

**Retained and working.** The shared command router remains the authority for keyboard and speech actions. USER microphone QA succeeded. The Windows phrase system/`KeywordRecognizer` recognized and dispatched configured commands; Low-confidence results were common in noisy conditions and clearer/louder speech produced successful cases.

All 14 product-default mappings created by `SpeechCommandConfiguration.CreateDefault()` now explicitly use `SpeechRecognitionConfidence.Low`. The generic/custom mapping default remains Medium. `wakePrefix` remains configurable and empty by default.

### Foundation B — camera presets

**Retained.** One primary camera owns the data-driven presets:

`Back`, `Front`, `Left`, `Right`, `FullBody`, `Hands`, `LeftHand`, `RightHand`.

Preset commands share the command architecture. F12 Lab/Game presentation remains a separate mode toggle.

### Foundation C — rich orientation

`DEFERRED / DORMANT RESEARCH — NOT PRODUCTION POSE AUTHORITY`

Research contracts/solver may remain in source history/current tree, but `MotionEngineRuntime` and the production pose composition were restored to the pre-C baseline. Do not treat C as a required next milestone.

### Foundation D — detailed hands

`DEFERRED`

The independent Hand Landmarker caused unacceptable low-end impact in USER testing, with the full detailed stream entering roughly the 10–15 FPS class. Existing research/lifecycle fixes may remain, but detailed hands are not normal production operation or a current hackathon requirement.

### Foundation E — post-Phase-4 detail

`RETIRED FROM PRODUCTION`

Production `RichHumanoidDetailRetargeter` was removed; stale Editor tests were removed at `f1819fda36547343bb32a972d39405d0a6be6f72`. Historical E research remains provenance only.

### Coarse hands / former Batch 4A

`DEFERRED`

The zero-extra-inference coarse hand signal was Builder/static validated, then rejected after USER runtime evaluation and rolled back. Do not claim the coarse arithmetic itself was uniquely proven to cause the performance drop; the USER chose the known optimized baseline over this feature/value tradeoff.

## 6. Phase 5A is the active unfinished Motion Engine target

Most horizontal locomotion infrastructure already exists:

- physical camera-space/root displacement;
- lateral and toward/away movement;
- body heading;
- cadence/in-place movement;
- physical/cadence fusion;
- recenter;
- Lab/Game presentation;
- third-person follow/preset camera.

The current controller consumes the stabilized Phase 3 frame and writes avatar-root X/Z only, preserving root Y and rotation.

Known current issues/observations:

1. Planted-feet lean suppression appears mostly successful, but is not finally accepted because integrated runtime testing is deferred.
2. **Raised/swing-leg false translation:** lifting/moving one leg while the other remains planted can trigger physical locomotion. The current support midpoint/common-displacement model allows one-foot movement to shift the midpoint. Batch 2 must distinguish support/planted motion from swing-leg motion.
3. Cadence works but acquires more slowly than desired.
4. Cadence travel distance/speed after activation is not satisfactory.
5. The source already contains Inspector-facing cadence settings including event threshold, acquisition-event count, acquire/sustain confidence, virtual stride per step, maximum virtual speed, step-rate range, timeout and signal response. Batch 2 should audit/use/expose them coherently rather than rebuild cadence from scratch.
6. Existing lateral/depth/heading/recenter/fusion behavior should be audited for consistency, not assumed accepted.

## 7. Jump and crouch are Motion Engine V1 requirements

### Jump

Physical jumping must produce corresponding vertical game movement. The eventual implementation must use coherent body/support evidence, distinguish a true jump from single-leg lift, reject ordinary tracking noise, have a clear takeoff/airborne/landing lifecycle and expose useful tuning controls. Do not pre-commit to a final algorithm before Batch 3 implementation audit.

### Crouch

Physical crouching must correspondingly lower/crouch the character. The eventual implementation must use normalized body-compression/height evidence rather than fragile raw-pixel-only thresholds, support held crouch state, use acquire/release hysteresis and expose useful tuning controls. Do not pre-commit to a final algorithm before Batch 3.

## 8. Approved three-batch Motion Engine completion sequence

### Batch 1 — documentation synchronization

Documentation only. This batch must finish and receive Orchestrator review before implementation resumes.

### Batch 2 — horizontal locomotion completion

- audit existing Phase 5A;
- fix raised/swing-leg false physical translation;
- regression-check the mostly successful lean suppression;
- improve cadence acquisition responsiveness;
- make cadence travel distance/speed clearly Inspector-tunable;
- verify lateral/depth/heading/recenter/fusion coherence;
- do not add jump/crouch yet.

### Batch 3 — vertical locomotion + final Motion Engine V1 completion

- implement jump detection/application;
- implement crouch detection/application;
- distinguish jump from single-leg lift;
- expose appropriate Inspector tuning;
- integrate with Phase 5A without corrupting Phase 4 body pose;
- prepare one final comprehensive USER Motion Engine QA.

After Batch 3, all Motion Engine testing is performed together. If accepted, Motion Engine V1 is essentially complete for the hackathon and the USER will provide the next game-development direction.

## 9. Testing policy

The USER explicitly deferred USER/runtime testing until all three batches are implemented.

- Do not request Batch 1 runtime QA.
- Batch 2 must not stop merely to wait for USER QA.
- Batch 3 prepares the final integrated QA.
- Builder compile/static/deterministic checks remain useful where appropriate but never create USER acceptance.
- Do not mark untested runtime behavior accepted.

## 10. Phase 6 / game boundary

Phase 6 remains **NOT STARTED**. Do not begin graybox/playable vertical-slice work from Batch 1 or Batch 2. Hub/course implementation has not begun merely because the Motion Engine roadmap is synchronized.

## 11. Immediate next action after this documentation batch

**STOP after Batch 1.** The next Orchestrator must independently review the synchronized docs and source, then issue a separate Batch 2 Builder brief if the state is coherent.

Do not resume Foundation C/D/E/coarse-hand work, performance optimization, Phase 6, or unrelated CI cleanup as part of the locomotion completion sequence unless the USER explicitly changes scope.
