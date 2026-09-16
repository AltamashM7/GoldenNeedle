# Golden Needle — Motion Engine V1 Phase-5 locomotion authority reconstruction

Repository: `AltamashM7/GoldenNeedle`

Branch: `engine/pose-tracking-spike`

USER-approved implementation baseline before this brief:

`466d65826ab747493c211e1a3250aafa305167c8`

The USER explicitly approved this reconstruction after runtime QA showed that the recent stationary-gate / grounded-foot-anchor corrections did not reproduce the previously good locomotion feel.

## 1. Mission

Reconstruct Phase-5 locomotion authority around the last known good pre-Foundation behavior instead of adding another threshold patch.

Current USER runtime findings at `466d658...`:

1. while the USER remains idle, the avatar still has a constant locomotion-caused offset / displacement / jitter;
2. shallow/deep grounded bend logic now reacts, but crouching visually behaves as though the upper body is the fixed focus and the legs are being lifted/bent underneath it instead of the pelvis/upper body descending toward the ground;
3. the USER specifically reports that locomotion before Foundation A-E experimentation did not behave this way.

The goals are:

- restore the stable physical-locomotion feel of the pre-Foundation baseline;
- keep later fixes that solved real defects such as planted-feet lean, swing-leg false translation, support-loss continuity, cadence improvements and alternating-step relocation;
- make crouch/body compression lower the avatar pelvis/root as the primary vertical action, with feet acting as grounding constraints rather than as the primary source of root-Y displacement;
- preserve jump as independent positive root-Y motion;
- stop for USER Unity webcam QA when this reconstruction is ready.

Do **not** work on performance logging/optimization in this task. That investigation is explicitly deferred.

## 2. Required historical comparison before editing

Do not begin by modifying the current code.

First inspect these historical checkpoints side-by-side with current HEAD:

### A. Last known good pre-Foundation locomotion reference

`e26b33ee62305cb7d3ba9e8d929dfe7662487ea0`

This is the preferred behavioral reference because it is the optimization-era handoff immediately before Foundation A-E experimentation. Inspect at minimum:

- `CameraSpaceRootTracker.cs`
- `LocomotionFusion.cs`
- `EmbodiedLocomotionController.cs`
- `CadenceDetector.cs`
- `Phase5LocomotionTests.cs`
- the Lab scene locomotion serialization

Do **not** blindly restore the whole commit. Use it to identify which ownership and filtering properties made idle/ordinary locomotion stable.

### B. Earlier torso-position prototype

`87698948b12cd10b6fef2072d0ad0ce9eeaecdfe`

Inspect the earlier torso-center / apparent-scale physical root tracker and simple fusion path. This checkpoint is useful because it shows the original clean separation between:

- one filtered root-position estimate;
- one fusion/mapping layer;
- root X/Z application only.

### C. Support-base corrective transition

`33698719a2907d30bb3396f66e5b79e59ccbfe9e`

This commit is `fix: anchor physical locomotion to support base`. Inspect exactly why support-foot authority was introduced and what defect it fixed. Preserve the useful lesson: planted-feet torso lean must not become room translation.

### D. Current implementation

`466d65826ab747493c211e1a3250aafa305167c8`

Inspect current:

- `CameraSpaceRootTracker.cs`
- `LocomotionFusion.cs`
- `EmbodiedLocomotionController.cs`
- `VerticalLocomotionInterpreter.cs`
- `HumanoidRetargeter.cs`
- `HumanoidRigBinding.cs`
- `Phase5LocomotionTests.cs`
- `VerticalLocomotionTests.cs`
- `LocomotionGroundingStabilityTests.cs`

Document the actual current layers of displacement state/filtering/rebasing before selecting the reconstruction.

## 3. Do not mistake this for a revert

This is **not** permission to reset/revert shared history or to restore an old tree wholesale.

The current branch contains accepted improvements that must remain:

- optimized WebCamCPU/OpenVINO body pipeline;
- Phase 3 stabilized canonical authority;
- USER-accepted Phase 4 signed mapping / analytic two-bone IK;
- support-loss continuity;
- planted-feet lean suppression;
- raised/swing-leg false-translation protection;
- coherent alternating-step relocation;
- current cadence baseline: 2-event acquisition, `0.38` acquire confidence, `0.60` distance per step, `3.0` maximum cadence speed;
- heading/recenter behavior;
- Batch-3 jump state-machine intent and jump/depth cross-talk isolation;
- commands/speech and camera presets.

Reconstruct only the Phase-5 locomotion authority that is now misbehaving.

Do not force-push, rebase, amend, reset or rewrite history.

## 4. Horizontal locomotion design target — one clear position authority

The current system must no longer have multiple layers independently trying to hold/rebase/commit the same physical displacement.

USER runtime evidence says the recent stateful stationary gate did not solve the issue and locomotion itself is generating a constant idle offset/jitter.

### Required principle

Use **one stable continuous body/root displacement estimate** as the physical X/Z position authority.

Use feet/support as **validation and constraint evidence**, not as a noisy second continuous position signal that is repeatedly re-filtered and re-committed downstream.

The preferred conceptual ownership is:

- torso/body placement + apparent scale: stable continuous candidate displacement, inspired by the pre-Foundation/earlier implementation;
- feet/support state: validates whether the candidate represents real room relocation versus lean/swing articulation;
- root tracker: owns physical displacement state, filtering, continuity and support validation;
- fusion: maps the already-authoritative physical displacement and blends/suppresses cadence; it should not maintain a second independent physical-position gate.

Exact implementation may differ after inspection, but there must be one clear owner.

## 5. Preserve planted-feet lean suppression

Do not simply restore the old torso tracker unchanged.

The old torso-position implementation could interpret torso lean as translation. Later support-base work was introduced for a real reason.

Required behavior:

- if torso/body candidate X/Z moves but the planted support base has not genuinely relocated, treat that as lean/body articulation and hold physical room displacement;
- upper-body arm motion must not translate the root;
- small pelvis/torso sway while both feet remain planted must not translate the root;
- slow deliberate room relocation must still eventually register;
- do not use an ever-growing deadzone that makes small deliberate steps impossible.

Prefer support evidence as a gate/validator rather than the final displacement output.

## 6. Preserve swing-leg isolation and genuine stepping

The reconstruction must retain the accepted Batch-2/2R semantics:

- one clearly raised/swinging leg may move without directly translating the physical root;
- the planted support side remains trustworthy evidence during swing;
- landing/support transitions must not teleport;
- after a genuine alternating step causes the whole support base/body to relocate, the new room position must eventually commit;
- jogging in place should remain primarily cadence, not physical drift.

A valid implementation may commit room displacement on coherent support relocation/landing rather than following the swing foot frame-by-frame.

Do not regress to the stale behavior where one moving foot immediately drags the root midpoint.

## 7. Remove redundant stationary-position ownership

Audit the current stateful committed-displacement gate added to `LocomotionFusion` in the grounding/stability corrective batch.

If root tracking is reconstructed to produce a stable authoritative physical displacement, `LocomotionFusion` should return toward its earlier role:

- map physical camera-space displacement into world X/Z;
- apply appropriate scale/basic deadzone if still useful;
- compute physical activity from the accepted physical motion;
- suppress/blend cadence;
- hold the last trusted contribution during temporary invalidity where required.

Do not keep a second position state machine merely because tests currently cover it.

Delete/replace tests whose only purpose is to preserve the unsuccessful current gate if the architecture supersedes it. Preserve the USER-facing behavior the tests were meant to guarantee: idle stability, slow deliberate movement, no cadence suppression from noise, recenter continuity and tracking-loss continuity.

## 8. Idle stability requirement

At a stable calibrated standing pose:

- physical displacement should settle and remain visually still;
- it must not alternate around an offset because of foot landmark noise;
- it must not slowly walk/drift away;
- moving arms/upper torso without moving the support base must not move the root;
- after moving to a nonzero room position and standing still, the root should settle at that displaced position rather than hunting around it.

Use the historical pre-Foundation implementation to establish sensible filtering/deadzone behavior before inventing new thresholds.

Avoid stacking filters in tracker + fusion + controller for the same quantity.

## 9. Crouch architecture — pelvis/body descent is primary

The current post-solve `GroundedCrouchFootAnchor` approach made the visual hierarchy wrong in USER QA: crouching looks like the legs lift/bend while the upper body remains the focus.

Reconstruct vertical crouch ownership so that **tracked body compression determines pelvis/root descent**.

### Required principle

- `VerticalLocomotionInterpreter` remains the semantic jump/crouch evidence authority;
- normalized pelvis-to-support/body compression should produce the primary negative root-Y offset;
- shallow grounded bend should begin lowering the root proportionally before semantic `Crouch` necessarily acquires;
- deeper compression should lower the root further;
- the semantic `Crouch` threshold remains useful for gameplay classification but must not be the point where body descent suddenly begins;
- standing upright returns root Y smoothly to the standing origin.

The original Batch-3 proportional crouch path may be reused/reworked because USER QA showed that a sufficiently deep crouch previously lowered the body correctly. Do not let post-solve foot movement become the primary root-Y measurement again.

## 10. Feet are a grounding constraint, not the crouch driver

During grounded bend/crouch:

- the avatar pelvis/root should move down according to body compression;
- feet should remain visually near their standing ground plane;
- feet should not be used to decide how much the USER crouched;
- if foot geometry is used, use it only as bounded residual validation/correction or to constrain the grounded solve.

First inspect whether continuous proportional root descent already compensates the Phase-4 leg pose adequately. Prefer the smallest solution that produces the intended visual result.

Only if necessary after code/math inspection, introduce a narrow grounded leg-foot constraint/re-solve using existing project-owned Phase-4 IK math. Do **not** duplicate the full retargeter, create a second general-purpose IK architecture, or change accepted Phase-4 mapping.

If a post-root leg correction is required, it must be narrowly scoped to grounded leg endpoints and must be disabled during jump/takeoff.

Do not add raycasts, Rigidbody, CharacterController, gravity or Phase-6 floor physics.

## 11. Jump remains separate

Jump retains positive root-Y ownership.

Required:

- single-leg lift is not a jump;
- crouch recovery is not a jump;
- grounded bend compensation must relinquish ownership on genuine takeoff/jump;
- existing jump/depth cross-talk suppression remains unless reconstruction requires an equivalent minimal relocation of that logic;
- no crouch/grounding system may pin a genuine jump to the floor.

Do not broaden jump scope while solving the crouch issue.

## 12. Depth/crouch interaction

Historical torso apparent-scale depth estimation can confuse body-size changes with forward/back movement if used naively during crouch.

Explicitly audit this.

If the reconstructed physical depth candidate uses torso/apparent scale:

- grounded crouch/body compression must not create false forward/back travel;
- support-base evidence should help distinguish room-depth relocation from a stationary bend;
- jump and crouch vertical action states may suppress/hold physical depth when appropriate, but do not create long stale offsets.

Preserve deliberate forward/back walking.

## 13. Recenter and tracking loss

Preserve existing user-facing semantics:

- recenter is X/Z-only;
- current world position is preserved when physical origin is redefined;
- vertical standing origin is independent;
- tracking loss holds the last trusted physical contribution rather than snapping to zero;
- reacquisition must not treat a discontinuous measurement jump as deliberate translation;
- after reacquisition, future genuine motion must still work.

Keep one continuity owner whenever possible.

## 14. Deterministic tests — behavior, not obsolete implementation details

Update tests to match the reconstructed ownership instead of preserving superseded internals.

At minimum ensure deterministic coverage for:

### Horizontal / physical

1. neutral standing settles at zero with no repeated accepted drift from small noisy body/support samples;
2. after real relocation to nonzero X, idle noise does not hunt around the new position;
3. planted torso lateral lean does not translate;
4. planted torso forward/back scale/lean change does not translate;
5. one raised/swing leg moved laterally/vertically does not translate;
6. genuine bilateral/support-base relocation produces physical movement;
7. complete alternating left-step/right-step sequence eventually commits net displacement;
8. landing/support transitions remain continuous;
9. slow deliberate movement eventually registers;
10. support loss/reacquisition holds/rebases without teleport;
11. recenter preserves world position;
12. physical motion still suppresses cadence appropriately;
13. idle tracking noise does not suppress cadence;
14. accepted camera axis/heading semantics remain unchanged;
15. jogging in place remains near-zero physical displacement while cadence can activate.

### Vertical / crouch

16. shallow grounded compression while semantic state is still Standing produces a small negative root-Y target;
17. deeper compression produces a larger negative root-Y target;
18. semantic Crouch remains thresholded independently from continuous body descent;
19. planted crouch does not create false X/Z/depth travel;
20. standing recovery returns toward zero smoothly;
21. jump disables grounded crouch descent/constraint ownership;
22. one-leg lift does not create crouch root descent;
23. tracking/calibration reset clears stale vertical action state.

If grounded-foot residual correction remains, test only its constraint behavior; do not make foot-rise the primary crouch displacement expectation.

Do not weaken Phase-3 or Phase-4 tests.

## 15. Inspector / scene behavior

Prefer reusing existing serialized settings where they remain meaningful.

If the recent `Lateral Movement Enter/Continue` and `Depth Movement Enter/Continue` settings become obsolete because the redundant fusion gate is removed, remove them cleanly from code and scene serialization rather than leaving misleading controls.

Keep useful existing controls such as physical scales/deadzones, cadence controls and vertical jump/crouch controls.

Audit the final `PoseTrackingSpike.unity` YAML diff. Remove unrelated serialization churn.

## 16. Diagnostics

Update existing locomotion diagnostics only as needed so USER QA can tell:

- authoritative physical displacement;
- support validation state / support mode;
- physical movement accepted versus held as lean/swing;
- cadence active/blend;
- vertical state;
- continuous grounded bend/root-Y offset;
- jump ownership.

No per-frame Console spam.

Do not build the deferred session logger in this task.

## 17. Scope limits

Expected runtime files are primarily:

- `CameraSpaceRootTracker.cs`
- `LocomotionFusion.cs`
- `EmbodiedLocomotionController.cs`
- `VerticalLocomotionInterpreter.cs`
- narrowly related Phase-5 diagnostics if required

Tests may include:

- `Phase5LocomotionTests.cs`
- `VerticalLocomotionTests.cs`
- `LocomotionGroundingStabilityTests.cs` (rewrite/remove obsolete cases as appropriate)

The Lab scene may change only for actual Phase-5 serialized settings.

Do **not** modify:

- MediaPipe/OpenVINO providers/acquisition/scheduling;
- Phase-3 One-Euro filtering/calibration;
- accepted Phase-4 canonical mapping / general retarget IK architecture;
- hands / Foundation C-D-E;
- speech/commands;
- camera presets;
- packages/project settings;
- performance logging/optimization;
- Phase 6.

If a tiny reusable Phase-4 IK helper call is required for a grounded leg constraint, reuse existing project-owned math without changing Phase-4 behavior for normal retargeting.

## 18. Verification discipline

Do not claim USER acceptance from source reasoning.

Run the smallest available deterministic/editor tests if Unity is available. If Unity/Test Runner is unavailable, state that explicitly.

Before stopping:

- inspect the complete implementation-baseline -> ending diff;
- verify no provider/Phase3/general Phase4 scope creep;
- verify old historical code was used as behavioral reference, not copied blindly;
- verify current cadence defaults remain unless a concrete bug required a change;
- update current authoritative docs to describe the reconstructed Phase-5 ownership and the remaining USER-QA requirement.

Prefer a small number of coherent commits.

## 19. USER QA after implementation

Stop when genuine webcam QA is required.

The next USER test should focus on:

1. calibrate and stand still for 5–10 seconds;
2. move arms/upper torso while feet remain planted;
3. lean left/right and slightly forward/back without stepping;
4. deliberately step left/right and forward/back;
5. stand still at a nonzero displaced position;
6. perform several normal alternating steps;
7. jog/step in place and verify cadence;
8. shallow knee bend with feet planted;
9. progressively deepen into crouch and hold;
10. return to standing;
11. if practical, jump after fully recovering from crouch;
12. briefly lose/reacquire lower-body tracking.

Expected:

- idle root is stable;
- lean is rejected as room translation;
- real translation still works;
- one swing leg does not drag the root;
- alternating steps eventually relocate the character;
- crouch visibly lowers pelvis/upper body from shallow bend onward;
- feet remain visually grounded rather than legs merely lifting under a fixed torso;
- jump remains separate;
- no teleport after tracking loss/recenter.

## 20. Final report

Return:

1. exact live starting SHA including this brief and the implementation baseline `466d658...`;
2. ending SHA;
3. commits/files changed;
4. side-by-side historical findings from `e26b33...`, `87698948...`, `33698719...`, and current code;
5. exact chosen physical-position authority and why;
6. how support evidence rejects lean/swing without becoming the noisy output signal;
7. which redundant state/filtering layers were removed;
8. crouch root-Y authority and shallow-bend behavior;
9. how feet are constrained/validated during crouch;
10. jump/depth/recenter/tracking-loss interactions;
11. test changes and actual execution limitations;
12. scene/default changes;
13. documentation updates;
14. explicit confirmation that provider/OpenVINO, Phase 3, general Phase 4, hands, speech/camera presets, performance/logging and Phase 6 were untouched.

Final status:

`PHASE-5 LOCOMOTION AUTHORITY RECONSTRUCTION IMPLEMENTED / USER QA PENDING`

Do not merge to `main`.