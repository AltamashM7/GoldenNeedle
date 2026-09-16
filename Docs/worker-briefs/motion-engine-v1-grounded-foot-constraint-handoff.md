# Golden Needle — Motion Engine V1 grounded-foot constraint corrective pass

Repository: `AltamashM7/GoldenNeedle`

Branch: `engine/pose-tracking-spike`

Expected starting remote HEAD:

`92b6a36547e0ab2db043331c4b7cc9c8363e6d90`

The USER has explicitly approved this narrow corrective pass after runtime QA of the Phase-5 authority reconstruction.

## 1. USER runtime findings

At `92b6a365...` the USER reported:

1. the previous locomotion-caused idle jitter is **mostly fixed**;
2. crouch still has a vertical hierarchy problem: the upper body often appears to remain the focus while the feet/legs adjust underneath it;
3. one observed crouch did lower the body in the intended direction, but the feet then moved **below the floor plane** even though the USER's real feet remained planted;
4. desired behavior is explicit: during a grounded bend/crouch, the avatar pelvis/upper body should lower while the planted feet remain at the same ground level.

This means the horizontal authority reconstruction is considered successful enough to freeze for this pass. The remaining issue is a narrow grounded-leg constraint problem.

## 2. Mission

Add the missing post-root grounded-foot constraint without reopening the reconstructed horizontal locomotion architecture.

Required visual/physical relationship:

- pelvis/root Y follows existing tracked body-compression authority;
- while both real feet remain grounded, avatar feet remain on the captured standing floor plane;
- leg joints bend/re-solve to connect the lowered pelvis to those fixed-height feet;
- jump/takeoff immediately releases the ground constraint;
- the foot constraint must not become another source that decides how far the USER crouched.

The intended execution order is conceptually:

```text
Phase 4 normal pose / leg solve
-> Phase 5 computes and applies root X/Z/Y
-> if grounded bend/crouch is trustworthy:
     re-solve only LeftLeg and RightLeg toward grounded foot targets
-> presentation
```

Do not alter the USER-accepted general Phase-4 solve for normal motion.

## 3. Freeze the successful horizontal reconstruction

Do **not** modify `CameraSpaceRootTracker.cs` or the reconstructed physical-position ownership unless a compile dependency genuinely requires a tiny signature-only change.

Do **not** change:

- torso/body movement candidate logic;
- bilateral support validation;
- support-mode hysteresis;
- accepted displacement filtering/rebase;
- `LocomotionFusion` position ownership;
- lateral/depth scales/deadzones;
- cadence behavior/tuning;
- recenter semantics;
- tracking-loss continuity.

USER evidence says idle/root jitter is now mostly fixed. Do not risk regressing it while solving crouch.

## 4. Preserve current vertical motion authority

Do not redesign `VerticalLocomotionInterpreter` unless a tiny exposed state/value is genuinely needed.

Current intended authority remains:

- normalized pelvis-to-support compression drives negative root Y;
- shallow grounded compression may lower root before semantic `Crouch` acquires;
- deeper compression continues through the same proportional path;
- semantic Crouch remains thresholded at current values;
- coherent Jump remains exclusive positive root-Y authority;
- the existing jump/depth cross-talk hold remains.

The USER's current failure is **not** evidence that crouch compression should be measured from solved avatar foot motion again.

Feet are a ground constraint only.

## 5. Inspect current runtime before editing

Before changing code, inspect at minimum:

- `EmbodiedLocomotionController.cs`
- `VerticalLocomotionInterpreter.cs`
- `HumanoidRetargeter.cs`
- `HumanoidRigBinding.cs`
- existing two-bone IK interfaces/solver/helper types used by Phase 4
- `VerticalLocomotionTests.cs`
- `Phase5LocomotionTests.cs`
- `LocomotionPrototypeView.cs`

Confirm the actual order of operations and identify the smallest reusable IK surface.

The current project already exposes leg chains through `HumanoidRigBinding`:

- `CanonicalKinematicChainId.LeftLeg`
- `CanonicalKinematicChainId.RightLeg`
- chain root / mid / tip transforms;
- upper/lower/total reach;
- reference bend information.

The Phase-4 retargeter already uses project-owned analytic two-bone IK. Reuse that math/solver rather than introducing a second IK implementation.

## 6. Capture a stable standing floor reference

The grounded constraint needs a floor-height reference that does not follow the feet downward during crouch.

Preferred behavior:

- once calibration/vertical standing reference is valid and the avatar is in a trustworthy neutral grounded state, capture the avatar's standing foot-tip world Y;
- use both leg tips where available;
- a single floor plane may be the bilateral average if both feet are at the same standing level;
- if per-foot standing offsets are required for an asymmetric avatar, preserve each foot's standing Y separately but do not let the reference continuously drift during a crouch;
- reset/reacquire this reference on binding change, calibration reset/loss, explicit vertical reinitialization, or other lifecycle event where the current standing origin itself is reset.

Do **not** update the floor reference from already-crouched or jumping feet.

Do not add raycasts, colliders, Rigidbody, CharacterController, physics-floor detection, or Phase-6 world grounding.

This is an avatar-relative standing-foot reference, not gameplay terrain handling.

## 7. Grounded-foot target semantics

When the grounded constraint is active, each leg foot target should preserve the normal Phase-4 solved horizontal intent while enforcing the standing floor height.

Preferred target construction:

```text
current post-Phase4 solved foot position = (x, y, z)
grounded target = (x, standingFootY, z)
```

This deliberately constrains primarily **Y**, not full XYZ.

Reasons:

- the USER only requires planted feet to remain on the same floor plane;
- Phase 4 should remain free to represent stance width and ordinary tracked X/Z foot placement;
- hard full-world-position pinning would make natural stance transitions brittle.

If there is a small residual Y tolerance/deadband, keep it bounded and body-relative. It must never allow a grounded foot to sink visibly below the standing plane.

## 8. Active-condition / safety gate

Apply the grounded leg constraint only when evidence says both legs should be planted.

At minimum require:

- calibration/body reference valid;
- binding and both leg chains available;
- vertical reference ready;
- vertical measurement available/live enough for ownership;
- semantic state is **not Jump**;
- current vertical evidence says support is approximately grounded;
- bilateral feet are coherent enough for grounded bend;
- `groundedBendActive` or equivalent trustworthy positive compression evidence indicates root descent is occurring or returning from a grounded bend;
- source lower-body confidence is sufficient.

The constraint should remain active while recovering from crouch back toward standing until the root is effectively back at vertical origin, so the feet do not rise/sink during recovery.

It should not engage merely because the avatar is standing neutral with zero vertical correction unless that is necessary for a seamless transition. Avoid constantly re-solving legs when no vertical correction exists.

## 9. Jump / single-leg protection

Ground lock must disengage immediately for genuine jump/takeoff.

Specifically:

- if semantic `Jump` is active, do not constrain the feet to standing Y;
- if existing support-rise evidence has already left the grounded range strongly enough to indicate takeoff, release even before any residual crouch-recovery state could hold it;
- do not pin landing while the body is still airborne;
- single-leg lift/swing must not cause the raised foot to be forced back to the floor;
- when bilateral grounded support is not trustworthy, skip the constraint instead of fabricating contact.

Do not alter Batch-3 jump thresholds/state-machine logic in this pass.

## 10. Re-solve only the two legs

After root Y has been applied, re-solve only:

- `LeftLeg`
- `RightLeg`

Use the existing leg root (hip), mid (knee), and tip (foot) transforms from `HumanoidRigBinding`.

Use existing project-owned analytic two-bone IK structures and math. If the solver is currently private to `HumanoidRetargeter`, expose or extract the smallest reusable helper/API necessary.

Do **not**:

- duplicate the analytic solver;
- copy the entire Phase-4 retargeter;
- re-run all four chains;
- change arm solving;
- replace the Phase-4 canonical-to-avatar mapping;
- introduce a second general retargeting architecture.

The new solve is a **post-root residual grounded-leg correction only**.

## 11. Knee / bend-direction stability

Preserve natural knee bending.

For each leg:

- prefer the currently solved knee/bend plane from Phase 4 as the bend hint/direction;
- otherwise use the leg's existing/reference bend direction from `HumanoidRigBinding`;
- retain per-leg previous reliable bend direction only if needed to prevent knee flips;
- never derive a new anatomical convention that conflicts with Phase 4.

The grounded correction should alter as little as possible beyond what is necessary to reach the foot Y target.

## 12. Root remains the crouch driver

This is a critical invariant.

Do not recreate the removed `GroundedCrouchFootAnchor` behavior where solved foot rise determines root Y.

Correct dependency:

```text
tracked body compression
-> root Y descent
-> grounded leg re-solve keeps feet on floor
```

Forbidden dependency:

```text
solved foot movement
-> choose root Y
```

The USER's previous runtime evidence showed that making feet decide root height creates the wrong visual hierarchy.

## 13. Avoid under-plane feet

A grounded foot target must not be lower than its captured standing floor Y while the USER's corresponding foot remains part of coherent grounded support.

The primary runtime invariant is:

```text
abs(avatarFootY - standingFootY) <= small visual tolerance
```

through shallow bend, deep crouch, and standing recovery.

If root compression exceeds the avatar's reachable leg geometry, do not force an impossible IK solve that flips or explodes. Clamp/bound the leg target using existing solver reach behavior and expose that limitation through diagnostics/tests if necessary.

But within normal crouch range, feet should remain visually on the plane, not pass below it.

## 14. Execution-order / presentation audit

Current `HumanoidRetargeter` executes at order 100 and `EmbodiedLocomotionController` at 150.

The Builder must verify that the grounded correction occurs after:

- Phase-4 pose solve;
- final Phase-5 root-Y placement;

and before the frame is presented to the USER.

Be careful with presentation smoothing: do not let a downstream/visible rotation restoration overwrite the grounded leg correction. Inspect actual LateUpdate ordering and visible transform state rather than assuming the order from comments.

Do not change presentation-smoothing policy globally in this task.

## 15. Diagnostics

Extend existing low-cost locomotion diagnostics only if useful.

Useful values may include:

- grounded-foot constraint active/inactive;
- captured left/right/average standing foot Y;
- current left/right foot Y residual from standing plane;
- whether a leg solve was skipped because support/chain data was unavailable;
- whether a target was reach-clamped.

No per-frame Console spam.

Do not build the deferred session logger.

## 16. Deterministic test requirements

Add focused deterministic coverage around a small reusable grounded-leg constraint helper/API where possible.

At minimum cover:

1. standing reference captures stable foot-plane Y;
2. shallow negative root-Y movement followed by grounded correction returns both foot tips to standing Y;
3. deeper crouch keeps both foot tips at standing Y while hip/root is lower;
4. grounded recovery toward standing keeps feet on the plane and does not pop below/above it;
5. foot target preserves current Phase-4 X/Z while correcting Y;
6. the constraint never asks for a target below the captured standing plane for grounded support;
7. semantic Jump disables the grounded correction;
8. takeoff/support-rise evidence disables it;
9. unilateral/swing-leg state does not force the raised foot to the floor;
10. missing/unavailable leg chain fails safely without corrupting the other leg/root;
11. knee/bend direction remains finite/stable across shallow->deep->recovery sequence;
12. calibration/binding reset clears stale standing-foot reference;
13. existing shallow/deep `VerticalLocomotionInterpreter` root-Y tests remain valid and unchanged in meaning;
14. existing reconstructed horizontal tests remain untouched/passing in source logic.

If exact Transform-based IK tests are cumbersome in the existing Editor test structure, split the work so target/reference/gating math is deterministic and use the smallest possible integration test for actual leg solve.

Do not weaken or delete the reconstructed horizontal test expectations merely to make this patch compile.

## 17. Expected file scope

Preferred runtime scope:

- `Assets/GoldenNeedle/Core/Motion/Locomotion/EmbodiedLocomotionController.cs`
- one small new Phase-5 grounded-leg constraint/helper file if that is cleaner
- a tiny reusable retarget/IK helper/API only if required
- `HumanoidRigBinding.cs` only if a minimal read-only accessor is genuinely missing

Preferred test scope:

- new focused grounded-foot constraint Editor test file, or narrow additions to `VerticalLocomotionTests.cs`

Diagnostics/docs as needed:

- `LocomotionPrototypeView.cs`
- `Docs/current-state.md`
- `Docs/motion-engine.md`
- `Docs/decisions.md`
- `Docs/orchestrator-handoff.md`

Avoid scene changes unless a genuinely new serialized setting is required. Prefer sensible internal constants/reuse of existing response/tolerance values over adding a large new Inspector surface.

## 18. Hard scope exclusions

Do not modify:

- `CameraSpaceRootTracker.cs` behavior;
- `LocomotionFusion.cs` behavior;
- cadence implementation/defaults;
- OpenVINO/MediaPipe/provider/acquisition/scheduling;
- Phase-3 calibration/One-Euro/stabilization;
- general Phase-4 mapping or normal chain target generation;
- hands/Foundation C-D-E;
- speech/commands;
- camera presets;
- performance logging/optimization;
- packages/project settings;
- Phase 6.

No merge to `main`.

No force-push/rebase/amend/reset/history rewrite.

## 19. Verification discipline

Before editing, verify live remote HEAD exactly.

After implementation:

- inspect the full starting-HEAD -> ending-HEAD diff;
- confirm horizontal locomotion files were not behaviorally altered;
- confirm normal Phase-4 behavior remains unchanged outside the narrow reusable IK surface;
- verify no unrelated scene/provider/package churn;
- run the smallest available deterministic/editor checks if Unity is available;
- if Unity/Test Runner is unavailable, state that honestly and do not claim execution;
- update authoritative docs to record the USER finding and new grounded-foot constraint architecture.

Prefer one coherent implementation commit plus one documentation commit if needed.

## 20. Stop boundary / USER QA

Stop when genuine USER Unity webcam QA is required.

The USER QA should then focus on:

1. compile with no red errors;
2. calibrate and stand neutral — feet establish the standing plane;
3. shallow planted knee bend — pelvis/upper body descends and both avatar feet remain at original floor Y;
4. continue into deep crouch — pelvis descends further while feet remain on the plane, not below it;
5. recover to standing — feet stay on the plane throughout and body rises smoothly;
6. lean/sway without crouching — no new horizontal regression;
7. lift one leg — raised leg remains free and is not snapped to the floor;
8. jump if practical — foot lock releases and jump is not pinned;
9. confirm idle X/Z stability remains as good as the reconstruction QA;
10. report whether either foot still sinks below or floats above the plane during grounded bends.

Final Builder status should be:

`GROUNDED FOOT CONSTRAINT IMPLEMENTED / USER QA PENDING`

Do not start performance logging/optimization or Phase 6.