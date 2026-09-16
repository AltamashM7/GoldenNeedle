# Golden Needle — Motion Engine V1 avatar-relative crouch grounding reconstruction

Repository: `AltamashM7/GoldenNeedle`

Branch: `engine/pose-tracking-spike`

USER-approved implementation baseline before this brief:

`9cda38b5c47f38d91800839cdd99db1d52fb0916`

The USER explicitly approved this narrow vertical/crouch reconstruction after runtime QA of the grounded-foot constraint showed that the crouch remained visually wrong.

## 1. USER runtime evidence

At `9cda38b5...` Unity compiled after the small test-harness fix, but runtime crouch QA still failed.

The attached USER screenshot showed the avatar in a very compact/tucked squat: the pelvis/body had descended, but the legs were being folded aggressively underneath the torso instead of preserving a natural tracked crouch relationship.

This is now the authoritative visual failure to solve.

The USER's intended behavior remains:

- shallow bend: pelvis/upper body begins descending naturally;
- deeper crouch: pelvis/upper body descends further;
- planted feet remain near the standing floor plane;
- knees retain the natural Phase-4 tracked bend/pose instead of being re-solved into an exaggerated tucked pose;
- standing recovery is smooth;
- jump/single-leg motion remain free;
- the successful horizontal locomotion reconstruction must not regress.

## 2. Root-cause hypothesis to verify before editing

Do not blindly implement the proposed formula. First inspect and verify the current geometry/scale relationships.

The current vertical path computes a normalized human-body compression in `VerticalLocomotionInterpreter`, then maps it to root Y using the serialized `crouchWorldScale` (currently nominally `1.20`) and `maximumCrouchDepth` (`0.65`). That mapping is expressed in fixed world units rather than directly in the controlled avatar's own standing leg scale.

The current `GroundedFootConstraint` then takes the already-moved root and re-solves both legs with analytic two-bone IK toward standing-plane Y targets. Runtime evidence indicates this residual solver can absorb too much correction by folding the knees, especially for the current avatar's proportions.

Verify at minimum:

- how `crouchCompression` is normalized;
- the active Lab scene's serialized vertical values;
- the current avatar/binding leg dimensions exposed by `HumanoidRigBinding`;
- Phase-4 solved leg/foot geometry before Phase-5 root translation;
- how much of the visible crouch is already represented by Phase-4 leg rotations;
- whether the fixed world-space root mapping is disproportionate relative to the avatar's standing hip-to-foot/leg span;
- how the current post-root `GroundedFootConstraint` changes the Phase-4 knee pose.

If inspection disproves part of the hypothesis, preserve the USER-facing goal and document the actual cause in the final report. Do not force a predetermined formula merely because this brief proposes it.

## 3. Freeze horizontal locomotion

The horizontal authority reconstruction is out of scope for this pass.

Do not modify unless an unavoidable compile-only dependency exists:

- `CameraSpaceRootTracker.cs`;
- `LocomotionFusion.cs`;
- cadence implementation or tuning;
- heading;
- X/Z recenter behavior;
- support-validation ownership;
- support-loss/reacquisition continuity;
- horizontal scene settings.

The USER previously reported the locomotion-caused idle jitter as mostly fixed. Preserve that behavior.

## 4. Preserve Phase 3 and normal Phase 4 authority

Do not modify:

- Phase-3 One-Euro/stabilized canonical filtering;
- provider/OpenVINO/acquisition/scheduling;
- normal `HumanoidRetargeter` canonical mapping;
- accepted Phase-4 analytic IK behavior for ordinary pose tracking;
- arms/hands/foundations;
- cameras/speech/commands;
- packages/project settings;
- Phase 6.

Phase 4 remains the normal leg-pose owner. This pass must stop overriding its knee pose with a second large crouch solve.

## 5. Required crouch ownership

The intended authority hierarchy is:

```text
USER pelvis/support compression
-> normalized crouch amount
-> avatar-relative root-Y descent
-> at most a small bounded residual floor correction
```

Not:

```text
normalized compression
-> fixed large world-unit descent
-> full second leg IK solve forced to recover the floor
```

And not:

```text
solved avatar foot movement
-> primary decision of how far the USER crouched
```

`VerticalLocomotionInterpreter` remains the semantic Jump/Crouch authority and source of normalized grounded compression.

Semantic Crouch thresholds remain independent from continuous shallow-bend descent unless concrete runtime/math evidence requires a narrowly justified change.

## 6. Make crouch descent avatar-relative

Replace or reinterpret the current fixed-world-unit negative-Y mapping so the same normalized USER compression scales to the controlled avatar's own standing lower-body geometry.

Preferred scale source after inspection:

- capture/derive a stable bilateral avatar standing leg-height measure from `HumanoidRigBinding`;
- prefer a measure corresponding to actual standing hip/root-of-leg to foot-tip vertical span if reliable;
- a bilateral average is preferred;
- existing upper/lower/total chain reach may be used as fallback/evidence;
- the scale must be stable across the crouch and must not be re-measured from already-crouched geometry.

Conceptually:

```text
avatarCrouchDepth ~= effectiveNormalizedCompression
                    * avatarStandingLegScale
                    * tuningMultiplier
```

The existing `1.20` value may become a dimensionless multiplier if that provides a clean serialized migration, but do not silently leave a field/tool-tip claiming meters if its semantics change. Use an explicit migration (`FormerlySerializedAs`) or preserve the field with correctly documented semantics after auditing the scene serialization.

`maximumCrouchDepth` should likewise be audited: a fixed `0.65` world-unit cap may be inappropriate across differently proportioned avatars. Prefer a leg-relative cap or a safe hybrid cap if necessary.

Do not hard-code values specifically for NekoLegends/current character. This must work for a different humanoid with different leg proportions.

## 7. Remove full post-root leg re-solve from normal crouch

The current `GroundedFootConstraint` full left/right analytic IK re-solve is not accepted as the normal crouch solution because USER QA showed it can visibly fold the legs into the wrong pose.

The final normal crouch path should preserve Phase-4 leg rotations/pose as much as possible.

Preferred outcome:

- remove the full post-root two-bone IK correction from normal grounded crouch; or
- reduce/restructure `GroundedFootConstraint` so it does not perform a second full leg solve that materially changes knee pose.

Do not retain a large second IK authority merely to force endpoint Y to zero residual.

If the class becomes obsolete, remove it and its tests cleanly rather than leaving dormant misleading architecture. If a narrow residual helper remains, rename/document it according to its actual responsibility.

## 8. Bounded residual grounding only

After avatar-relative root descent, small differences between Phase-4 leg pose and the captured standing floor may remain.

Feet may be used only as a **bounded residual correction signal**, not primary crouch authority.

Preferred geometry is feedback-safe and root-relative:

- capture stable standing foot-plane/reference data at neutral calibration;
- compare current post-Phase-4 foot positions relative to the avatar root against standing reference geometry;
- derive the root-Y adjustment needed to reduce bilateral grounded-foot residual;
- apply only a small avatar-relative bounded correction around the body-compression-derived root target;
- do not accumulate `root += error` frame-over-frame;
- do not let endpoint error feed back into `crouchCompression`;
- do not independently rotate knees/legs unless inspection proves a tiny residual rotation is absolutely necessary.

A good invariant is:

```text
finalRootY = bodyCompressionRootY + clamp(residualGroundCorrection,
                                          -smallLegRelativeBound,
                                          +smallLegRelativeBound)
```

The exact bound must be justified from avatar scale and should be small enough that the residual system cannot cancel/redefine the crouch or create a visibly different knee pose.

If the body-relative mapping is correct enough that no residual correction is required, prefer no correction over unnecessary complexity.

## 9. Ground/jump/single-leg safety

Any residual floor correction must run only when bilateral support is trustworthy.

At minimum:

- calibration/reference valid;
- both leg chains available;
- vertical measurement/reference available;
- not semantic Jump;
- support rise within grounded tolerance;
- foot asymmetry coherent;
- grounded bend/recovery evidence valid.

Release immediately for:

- jump/takeoff;
- raised/swinging single leg;
- incoherent bilateral support;
- missing/unavailable lower-body evidence;
- tracking/calibration reset.

Do not pin airborne feet or the raised leg to the floor.

## 10. Preserve Phase-4 pose while translating root

This pass should make the visual hierarchy correct:

- Phase 4 determines the leg bend from the tracked pose;
- Phase 5 lowers the entire avatar root by an amount appropriate for this avatar's lower-body scale;
- only a small residual Y correction may shift the root to keep trustworthy planted feet visually close to the floor;
- the residual correction must not visibly re-author knee flexion.

This is deliberately closer to the pre-Foundation/Batch-3 behavior, where Phase 5 primarily translated the root rather than running a second general leg solve.

Inspect historical `220a4b958c8a3d280799ea3c0836fa6536a486a0` and the last-known-good pre-Foundation `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0` for behavioral reference, but do not wholesale revert.

## 11. Deterministic tests — include avatar-proportion regression

Update tests around observable behavior, not the superseded full-foot-IK implementation.

At minimum cover:

1. shallow normalized compression produces a small negative root offset before semantic Crouch;
2. deeper compression produces larger descent;
3. root descent scales with avatar standing leg size: two rigs with different leg lengths and the same normalized compression receive proportionally different world-space descent;
4. a short-legged/chibi-style synthetic rig does **not** receive the same large absolute crouch descent as a tall/long-legged rig;
5. semantic Crouch thresholds remain independent from the continuous root descent;
6. standing recovery returns smoothly to vertical origin;
7. bilateral grounded-foot residual correction, if retained, is bounded relative to avatar leg scale;
8. residual correction cannot override the primary body-compression descent;
9. normal grounded correction does not modify/re-author the Phase-4 knee pose materially (test the chosen invariant rather than implementation internals);
10. one-leg lift disables residual grounding;
11. Jump/takeoff disables residual grounding and retains positive root-Y ownership;
12. calibration/binding reset clears standing leg/floor scale references;
13. planted crouch still does not create X/Z travel;
14. existing horizontal locomotion tests remain untouched and passing/source-compatible.

If `GroundedFootConstraintTests.cs` encodes the now-rejected full two-leg solve, replace/remove those cases rather than preserving obsolete behavior.

## 12. Diagnostics

Keep diagnostics minimal and useful for USER QA.

F9 should make it possible to see, as applicable:

- normalized crouch compression;
- avatar standing leg scale used for mapping;
- primary body-derived root-Y target;
- bounded residual grounding correction;
- final root-Y offset;
- whether grounding correction is active/released;
- whether the residual hit its clamp/bound.

Remove diagnostics that imply the full post-root leg lock remains authoritative if that architecture is removed.

No per-frame Console spam. Do not build the deferred session logger.

## 13. Expected files / scope

Likely runtime files:

- `VerticalLocomotionInterpreter.cs` if mapping responsibility remains there;
- `EmbodiedLocomotionController.cs` if avatar-scale/root residual composition belongs at the controller boundary;
- `GroundedFootConstraint.cs` only if it is being removed or narrowed into a residual helper;
- narrowly related diagnostics.

Tests likely include:

- `VerticalLocomotionTests.cs`;
- `GroundedFootConstraintTests.cs` if retained/rewritten;
- possibly a focused avatar-scale crouch test file.

`HumanoidRigBinding.cs` may receive only the smallest read-only accessor/helper needed to expose already-cached standing leg geometry. Do not change normal binding or Phase-4 solve behavior.

Do not modify scene YAML unless a serialized vertical field must be migrated or renamed. If scene serialization changes, audit it carefully and avoid unrelated churn.

## 14. Verification discipline

Before committing:

- inspect the complete `9cda38b5... -> ending` diff;
- verify horizontal runtime files are unchanged;
- verify provider/Phase3/general Phase4 are unchanged;
- verify no full second leg IK authority remains in normal crouch unless explicitly justified by evidence;
- verify the mapping uses avatar-relative lower-body scale rather than a hard-coded character-specific value;
- verify no stale test or diagnostic still asserts the rejected tucked-leg behavior;
- run the smallest available Unity/editor tests if possible;
- if Unity/Test Runner/compiler is unavailable, state that clearly and do not claim an executed pass;
- update `Docs/current-state.md`, `Docs/motion-engine.md`, `Docs/decisions.md`, and `Docs/orchestrator-handoff.md` only as needed to make the new ownership authoritative.

Prefer a small number of coherent commits.

Do not force-push, rebase, amend, reset or rewrite shared history.

Do not merge to `main`.

## 15. USER QA boundary

Stop when real webcam QA is required.

The next USER QA should be concise:

1. confirm no red compile errors;
2. calibrate standing normally;
3. shallow planted bend — upper body/pelvis should begin descending naturally;
4. deep crouch — further body descent, without the screenshot's tucked-leg/folded-knee failure;
5. feet should remain visually near the standing floor plane without a large second IK pose change;
6. stand back up — smooth recovery/no foot pop;
7. lift one leg — no grounding pin;
8. recheck idle/lean X/Z stability to confirm horizontal reconstruction stayed unchanged;
9. jump once if practical — no grounding pin and positive root Y remains intact.

Useful USER result categories:

- body descent: correct / too little / too much;
- feet: grounded / sink / float;
- knee pose: natural / tucked / flips;
- horizontal idle stability: preserved / regressed;
- jump release: pass / fail / not tested.

Final implementation status vocabulary:

`AVATAR-RELATIVE CROUCH GROUNDING RECONSTRUCTED / USER QA PENDING`

Motion Engine V1 remains **NOT USER ACCEPTED** until USER runtime QA passes.