# Golden Needle — Motion Engine V1 locomotion grounding + stationary stability worker brief

Repository: `AltamashM7/GoldenNeedle`

Branch: `engine/pose-tracking-spike`

Authoritative starting remote HEAD verified by Orchestrator immediately before this brief:

`e121e957ad42927a6fca3796e9ea22ba5efe3dee`

The USER explicitly approved this corrective implementation batch on 2026-09-16.

## 1. Mission

Implement one narrow corrective batch for the currently observed Motion Engine V1 runtime issues:

1. avatar/root jitter while the user is nominally stationary, where Phase-5 physical locomotion appears too sensitive to small support-foot fluctuations;
2. crouch root-Y behavior that can make the avatar feet appear to move through / away from the ground instead of remaining visually planted during a grounded crouch.

Do not work on logging, provider performance, OpenVINO optimization, hand tracking, rich axial detail, Phase 3 smoothing, or Phase 6 in this batch.

Stop only when implementation + deterministic verification are complete and genuine USER Unity webcam QA is required, or if a real blocker appears.

## 2. Read / inspect before editing

First verify the live remote branch HEAD. If it is no longer the commit containing this brief, inspect intervening commits before touching anything.

Read and inspect at minimum:

- `Docs/current-state.md`
- `Docs/orchestrator-handoff.md`
- `Docs/optimization-orchestrator-handoff.md`
- `Docs/motion-engine.md`
- `Assets/GoldenNeedle/Core/Motion/Locomotion/CameraSpaceRootTracker.cs`
- `Assets/GoldenNeedle/Core/Motion/Locomotion/LocomotionFusion.cs`
- `Assets/GoldenNeedle/Core/Motion/Locomotion/VerticalLocomotionInterpreter.cs`
- `Assets/GoldenNeedle/Core/Motion/Locomotion/EmbodiedLocomotionController.cs`
- `Assets/GoldenNeedle/Core/Motion/Retargeting/HumanoidRetargeter.cs`
- `Assets/GoldenNeedle/Core/Motion/Retargeting/HumanoidRigBinding.cs`
- `Assets/GoldenNeedle/Tests/Editor/Phase5LocomotionTests.cs`
- `Assets/GoldenNeedle/Tests/Editor/VerticalLocomotionTests.cs`

Independently inspect the actual current code before choosing the exact implementation. Treat the technical direction below as required behavior/architecture, not as permission to blindly patch by description.

## 3. Preserved invariants — do not regress

This batch must preserve all accepted production invariants:

- Phase 3 stabilized canonical pose remains calibration and locomotion authority.
- Do not change Phase 3 One-Euro settings or calibration semantics.
- Phase 4 signed canonical-to-avatar mapping and analytic two-bone IK remain pose/bone authority.
- Do not change Phase 4 retargeting math, bone targets, binding semantics, or accepted orientation behavior.
- Phase 5 remains root translation only and executes after Phase 4.
- Keep the accepted WebCamCPU/OpenVINO body pipeline untouched.
- Do not touch MediaPipe/OpenVINO acquisition, scheduling, inference, model semantics, provider fallback, or readback paths.
- Do not introduce new inference.
- Do not introduce FIFO/history/catch-up behavior.
- Do not reopen detailed hands, coarse hands, Foundation C/E, or rich limb axial rotation.
- Preserve existing cadence, heading, recenter, jump state-machine intent, and Batch-2/2R support-authority continuity unless a strictly necessary correction is required for this batch.
- Do not merge to `main`.
- Do not force-push, rebase, amend, reset, or rewrite shared history.

## 4. USER-observed runtime problems

### A. Stationary locomotion jitter / sensitivity

The USER suspects visible character jitter may be caused by locomotion sensitivity rather than provider performance. Logging/optimization investigation is explicitly deferred.

Current relevant design:

- `CameraSpaceRootTracker` continuously produces filtered physical support displacement/velocity.
- `LocomotionFusion` has origin-relative X/Z deadzones, but once the user is displaced away from the origin, small frame-to-frame fluctuations around that displaced position can still move the avatar root.
- `physicalActivity` currently derives from live mapped root velocity and therefore can react to small tracking fluctuations even when they should be treated as stationary noise.

The goal is not to make Phase 3 more sluggish. The correction belongs in Phase-5 locomotion interpretation/fusion.

### B. Crouch feet should remain grounded

Current vertical flow computes crouch compression in `VerticalLocomotionInterpreter`, converts it to a negative root-Y offset, and `EmbodiedLocomotionController` applies that after Phase 4.

Because Phase 4 already bends/poses the legs, simply lowering the whole avatar root from an independently scaled crouch compression can make the feet visually drift below/above their standing ground plane.

The desired behavior is: while an acquired crouch is a grounded crouch, Phase-5 root Y should compensate from the already-solved Phase-4 foot geometry so the planted feet remain visually anchored to the standing ground reference.

## 5. Required correction A — stationary physical-locomotion hysteresis

Implement a Phase-5-only stationary-motion gate/deadband with hysteresis for physical X/Z locomotion.

Required behavior:

- Maintain a last accepted/committed physical displacement state, not only an origin-relative deadzone.
- While the newest trusted physical displacement differs only slightly from the last accepted displacement, hold the accepted physical contribution instead of following measurement noise frame-by-frame.
- Slow deliberate motion must still work: displacement relative to the held state should accumulate until it crosses the movement-entry threshold.
- Once movement is active, use a lower continuation/release threshold so locomotion does not chatter between moving/stationary near one boundary.
- Lateral and depth thresholds may differ; depth may reasonably require a stronger gate because the camera-depth proxy is noisier.
- The state must reset/rebase safely on calibration loss/new calibration, explicit recenter, support tracking loss/reacquisition, and any other existing root-reference rebase path.
- Do not create teleports when switching support authority or reacquiring tracking.
- Do not break the existing Batch-2R behavior where a coherent dual-support relocation after an alternating step can become genuine room displacement.
- Do not make jogging-in-place support asymmetry itself become physical translation.
- Cadence suppression / `physicalActivity` should be based on the gated/accepted physical motion rather than raw tiny measurement noise, so nominally stationary jitter does not suppress or modulate cadence spuriously.

Choose the smallest clean ownership boundary after inspection. It may live in `CameraSpaceRootTracker`, `LocomotionFusion`, or be split carefully, but do not duplicate physical-root state across multiple layers without need.

Prefer normalized camera/body-relative thresholds consistent with the existing Phase-5 design. Do not add raw pixel magic values.

## 6. Required correction B — grounded crouch foot anchoring

Preserve `VerticalLocomotionInterpreter` as the single crouch/jump detector and state-machine authority. Do not create a second crouch detector.

While the existing vertical state says a valid grounded `Crouch` is active, compute root-Y correction from the post-Phase-4 humanoid foot/leg result so planted feet remain at the standing ground reference.

Important architecture facts to preserve/use:

- `HumanoidRetargeter` executes at `[DefaultExecutionOrder(100)]`.
- `EmbodiedLocomotionController` executes at `[DefaultExecutionOrder(150)]`.
- Therefore locomotion runs after the Phase-4 solve in the same frame.
- `HumanoidRigBinding` already exposes chain tips / bound humanoid transforms and should be reused rather than adding a parallel avatar-bone lookup system.

Required behavior:

- Establish a stable grounded foot-height reference while standing, relative to the root / standing vertical origin in a way that avoids frame-to-frame feedback accumulation.
- During a grounded crouch, derive the root-Y adjustment needed to keep the reliable planted-foot ground height at that reference after the Phase-4 leg solve.
- Prefer a robust bilateral planted-foot reference. If one side is temporarily less usable, fail safely rather than producing a large correction.
- Bound and response-filter the correction; no root-Y teleports or oscillatory feedback.
- Standing -> crouch should transition smoothly.
- Held crouch should keep feet visually planted while pelvis/body lower naturally through the tracked Phase-4 pose.
- Crouch -> standing should return smoothly without a root pop.
- A real jump/takeoff/airborne/landing must keep the existing jump vertical authority; do not apply crouch grounding compensation during jump ownership.
- Tracking/calibration loss must not leave a stale grounding offset.
- Horizontal recenter remains X/Z-only and must not redefine standing vertical ground unintentionally.
- Preserve current jump/depth cross-talk suppression.

The existing proportional `crouchWorldScale` path may remain as fallback/diagnostic if useful, but the normal grounded crouch result should not depend on a character-specific magic depth scale when post-solve foot anchoring can provide the correct root correction.

Do not add Rigidbody, CharacterController, gravity, raycasts, collision probing, floor physics, animation-state machinery, or Phase-6 gameplay systems in this batch.

## 7. Deterministic tests required

Extend the existing editor tests rather than replacing them.

At minimum add deterministic coverage for the following.

### Stationary locomotion

- after moving to a nonzero physical X displacement, sub-threshold frame-to-frame support jitter around that displaced location does not move the accepted physical contribution;
- equivalent depth jitter does not move the accepted depth contribution;
- slow deliberate displacement accumulated across samples eventually crosses the entry threshold and moves the root;
- after movement acquisition, hysteresis permits continued small same-direction motion without chatter;
- once movement settles below the release condition, physical activity returns to stationary;
- gated velocity/activity does not falsely suppress cadence from tiny support noise;
- recenter resets/rebases the stationary gate without a teleport;
- support loss/reacquisition preserves continuity and does not treat the reacquisition jump as deliberate movement;
- existing raised swing-foot / alternating-step relocation tests continue to pass.

### Crouch grounding

Add tests around the pure math/state that you introduce. Do not require a full Unity scene if the behavior can be factored deterministically.

Cover at least:

- standing establishes the vertical ground/foot reference with approximately zero extra root correction;
- post-retarget feet moving upward relative to root during a crouch produces the corresponding downward root correction needed to preserve ground height;
- correction is bounded;
- bilateral foot noise does not cause large Y jitter;
- crouch release returns toward standing without overshoot/pop;
- jump state disables grounded-crouch compensation;
- calibration/reference reset clears stale crouch grounding state.

Preserve all existing `VerticalLocomotionTests` jump/crouch state-machine coverage and Phase-5 horizontal tests.

## 8. Runtime/manual QA target after Builder work

Do not claim USER acceptance from static/editor tests. Stop for USER webcam QA once implementation is ready.

The USER should then be able to test this short sequence:

1. calibrate normally;
2. stand still for several seconds and move arms/upper torso without intentionally translating feet;
3. stand at a nonzero physical offset and remain still;
4. slowly step left/right and forward/back;
5. jog/step in place;
6. perform a shallow crouch with feet planted;
7. perform a deeper crouch with feet planted;
8. return to standing;
9. crouch, recover, then perform a genuine jump.

Expected results:

- avatar/root no longer visibly hunts/jitters from tiny Phase-5 physical displacement changes while stationary;
- deliberate slow and normal room movement still works;
- support switching/reacquisition does not teleport;
- jogging in place still behaves as cadence rather than physical drift;
- crouch is detected normally but the avatar feet remain visually planted instead of being dragged through/away from ground by root-Y scaling;
- standing recovery is smooth;
- jump remains positive root-Y motion and is not cancelled by grounding compensation;
- Phase-4 body pose quality/orientation remains unchanged.

If the independent canonical/retarget pose still jitters after the root is visibly stable, report that separately. Do not respond by modifying Phase 3 in this batch.

## 9. Scope limits

Allowed runtime files should be kept as narrow as possible, expected primarily among:

- `Assets/GoldenNeedle/Core/Motion/Locomotion/CameraSpaceRootTracker.cs`
- `Assets/GoldenNeedle/Core/Motion/Locomotion/LocomotionFusion.cs`
- `Assets/GoldenNeedle/Core/Motion/Locomotion/EmbodiedLocomotionController.cs`
- `Assets/GoldenNeedle/Core/Motion/Locomotion/VerticalLocomotionInterpreter.cs` only if the existing interface lacks a small piece of required state

Expected test files:

- `Assets/GoldenNeedle/Tests/Editor/Phase5LocomotionTests.cs`
- `Assets/GoldenNeedle/Tests/Editor/VerticalLocomotionTests.cs`

Scene/prefab serialization may be changed only if new serialized Phase-5 settings genuinely require it. Do not touch provider/OpenVINO, Phase 3, Phase 4 retarget/IK, speech, camera presets, hands, packages, project settings, or Phase 6.

Do not do broad cleanup/refactors while here.

## 10. Verification discipline

Run the smallest useful deterministic/editor verification available for the touched locomotion/vertical code. Do not spend time on unrelated exhaustive test suites or automated scene runs unless a compile/test failure requires broader diagnosis.

Inspect the final diff for accidental scope creep.

Commit and push the coherent implementation checkpoint to `engine/pose-tracking-spike`.

Do not merge to `main`.

## 11. Final Builder report

Return a concise but complete report containing:

1. exact starting remote SHA;
2. exact ending remote SHA;
3. files changed;
4. root cause confirmed for stationary jitter sensitivity;
5. exact gating/hysteresis design implemented and its defaults;
6. root cause confirmed for crouch foot drift;
7. exact foot-grounding compensation design implemented;
8. how jump/recenter/tracking-loss interactions are protected;
9. tests added/updated and results;
10. any compile/static limitations;
11. exact USER Unity QA steps now required;
12. explicit statement that no provider/OpenVINO, Phase 3, Phase 4, hands, speech, camera preset, performance/logging, or Phase-6 work was included.

Status vocabulary at handoff should be:

`LOCOMOTION GROUNDING/STABILITY CORRECTIVE BATCH IMPLEMENTED / USER QA PENDING`

Do not describe Motion Engine V1 as accepted until the USER completes runtime QA.