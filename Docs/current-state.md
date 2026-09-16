# Golden Needle — Current State

Authoritative refresh: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## Governance

- Do **not** merge to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or rewrite shared history.
- Independently verify the live remote branch before new work.
- USER Unity/manual/runtime evidence is the acceptance authority.
- Phase 6 remains **NOT STARTED**.

## Current checkpoint

USER-tested crouch-failure baseline before this reconstruction:

`9cda38b5c47f38d91800839cdd99db1d52fb0916`

Avatar-relative reconstruction brief/baseline:

`5e90551b7045dba96787c43fb9d4562d3438257a`

Implementation/tests/diagnostics checkpoint:

`2c9c3f6eaf4801e1832b32ad7afc79bb6597807b`

Current status:

**AVATAR-RELATIVE CROUCH GROUNDING RECONSTRUCTED / USER QA PENDING**

Motion Engine V1 remains **NOT YET USER ACCEPTED**.

## Preserved production body pipeline

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
-> Phase 4 positional/analytic-IK avatar pose
-> Phase 5 root translation
-> presentation
```

Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths. The accepted low-end optimization milestone remains frozen. Phase 3 remains locomotion/calibration data authority. Phase 4 remains the USER-accepted normal pose/IK authority.

Foundation A commands/speech and Foundation B cameras remain retained. Foundation C is dormant research, Foundation D hands are deferred, Foundation E is retired from production, and coarse hands remain deferred/rolled back.

## USER evidence and verified cause

At the USER-tested `9cda38b5...` runtime, horizontal idle/root stability was substantially improved, but deep crouch still produced an exaggerated compact/tucked squat. The body descended while both legs were aggressively re-folded underneath it.

Inspection verified two compounding causes:

1. `VerticalLocomotionInterpreter` measures crouch as **normalized USER pelvis-to-support compression**, while the production root path was applying a fixed-world-space negative-Y mapping that did not adapt to the controlled avatar's lower-body proportions.
2. The later `GroundedFootConstraint` then re-solved both legs after root translation toward hard standing-plane foot-Y targets. This second full two-bone solve could materially replace Phase-4's already-natural tracked knee bend with a much more compact knee configuration.

Historical inspection supported this diagnosis:

- `220a4b958c8a3d280799ea3c0836fa6536a486a0` — original Batch-3 established the useful normalized pelvis/support compression signal without a later general crouch-leg re-solve;
- `86701d0082692c17844db89c03fe4753e1876cf6` — historical solved-foot-driven root-Y experiment showed avatar leg geometry was useful scale evidence, but also why solved feet must not decide crouch depth;
- `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0` — pre-Foundation acceptable basic locomotion likewise did not add a second large crouch IK authority after normal Phase 4.

## Frozen horizontal authority

The successful Phase-5 horizontal reconstruction is unchanged.

`CameraSpaceRootTracker` remains the **only stateful physical X/Z position authority**. It owns accepted displacement, filtering/velocity, bilateral support validation, recenter and tracking-loss/reacquisition continuity.

`LocomotionFusion` remains mapping/blending only. Cadence, heading, support validation, horizontal recenter, tracking-loss behavior and all associated tuning are unchanged.

No behavior changes were made to:

- `CameraSpaceRootTracker.cs`;
- `LocomotionFusion.cs`;
- cadence implementation/defaults;
- heading;
- horizontal recenter;
- horizontal reconstructed tests.

## Vertical semantic authority remains unchanged

`VerticalLocomotionInterpreter` remains the sole Jump/Crouch semantic state machine and the source of normalized grounded compression.

Its existing semantic behavior remains intact:

- shallow trustworthy compression may begin before semantic `Crouch` acquisition;
- semantic Crouch enter/release remain `0.18 / 0.09`;
- grounded support/asymmetry/apparent-scale safeguards remain unchanged;
- jump thresholds, lifecycle, tracking grace and jump/depth isolation remain unchanged.

The interpreter still publishes its historical `worldOffsetY` for compatibility and for jump output. For **grounded non-jump production root placement**, the controller no longer uses the interpreter's fixed-world negative-Y value. Instead it maps `crouchCompression` through the new avatar-relative layer below.

## Avatar-relative crouch grounding

### Stable avatar scale

`AvatarRelativeCrouchGrounding` replaces the removed `GroundedFootConstraint`.

It derives each leg's stable standing scale from `HumanoidRigBinding` reference-pose geometry:

1. prefer the captured leg root-to-foot vertical span in the binding's anatomical parent frame;
2. fall back to the captured direct root-to-foot span if necessary;
3. fall back to the existing cached two-segment chain reach for unusual rigs.

The left/right values are averaged into one avatar standing-leg scale. The scale is cached against `HumanoidRigBinding.ReferencePoseVersion`, so live crouch rotations cannot make the scale shrink while the USER is already crouching.

No new binding accessor or Phase-4 change was required.

### Production crouch mapping

The applied non-jump Phase-5 root-Y mapping is now:

```text
motionDeadband = clamp(0.25 * crouchReleaseThreshold, 0.01, 0.05)
effectiveCompression = max(0, normalizedCompression - motionDeadband)
requestedDepth = effectiveCompression * avatarStandingLegScale * depthMultiplier
maximumDepth = avatarStandingLegScale * maximumDepthFraction
rootOffsetY = -min(requestedDepth, maximumDepth)
```

New explicit avatar-relative settings are:

- `Avatar Crouch Depth Multiplier = 1.20` (dimensionless);
- `Maximum Crouch / Leg Fraction = 0.65` (dimensionless fraction of standing leg scale).

They are separate from the legacy fixed-world fields inside `VerticalLocomotionSettings`, so no serialized field silently changed meaning. The committed `SampleScene.unity` contains no locomotion/crouch overrides, therefore no scene-YAML migration was needed.

The avatar-relative offset uses the existing vertical response speed and tracking-grace behavior. A short/chibi rig therefore receives a proportionally smaller world-space descent than a tall/long-legged rig for the same normalized USER compression.

### Phase 4 remains the sole leg-pose owner

The old `GroundedFootConstraint` and its full post-root two-bone IK correction have been removed.

Normal runtime order is now:

```text
HumanoidRetargeter @ 100
-> Phase-4 normal pose / leg solve / presentation smoothing
EmbodiedLocomotionController @ 150
-> authoritative X/Z root placement
-> avatar-relative Y root placement only
LocomotionPrototypeView @ 170
-> diagnostics
```

There is **no crouch-specific leg rotation write after Phase 4**. The knees therefore retain the tracked Phase-4 pose instead of being re-authored to satisfy a hard foot endpoint constraint.

### Residual grounding decision

No foot-residual correction is applied in this reconstruction.

This is intentional: the brief allowed no residual when the avatar-relative root mapping is sufficient, and USER evidence showed the previous large endpoint correction was the source of the tucked-leg hierarchy failure. `residualGroundCorrectionY` is explicitly zero and F9 reports `residual=off`.

If USER QA later shows a small consistent floor error after the new scale mapping is validated, any future correction must be root-only, avatar-relative and tightly bounded; it must not reintroduce knee/leg IK ownership or make feet choose crouch depth.

### Jump ownership

Semantic Jump bypasses the grounded crouch mapper immediately. The controller uses the interpreter's jump output and clamps it to non-negative Y so stale negative crouch-filter state cannot pin the first takeoff frame.

Jump detection, thresholds and the existing X/Z depth-isolation behavior are otherwise unchanged.

## Diagnostics

F9 now shows:

- normalized crouch compression;
- captured avatar standing-leg scale;
- avatar-relative primary crouch target;
- filtered/final crouch root offset;
- actual applied vertical offset;
- whether the avatar-relative cap was reached;
- `residual=off`, making it explicit that there is no post-root foot lock/IK authority.

The previous `GROUND FEET lock/residual/reach-clamped` diagnostics were removed because that architecture no longer exists.

No per-frame Console logging was added.

## Deterministic coverage

Existing reconstructed `Phase5LocomotionTests.cs`, `VerticalLocomotionTests.cs` and Phase-4 retargeting tests remain unchanged.

The obsolete full-IK `GroundedFootConstraintTests.cs` suite was removed and replaced with `AvatarRelativeCrouchGroundingTests.cs`, covering:

- neutral compression -> zero root offset;
- shallow pre-semantic-Crouch negative root descent;
- deeper compression -> larger but leg-relative bounded descent;
- same normalized compression scales proportionally across different avatar leg sizes;
- chibi/short rig receives much smaller absolute descent than tall rig;
- semantic Standing/Crouch state does not change mapping for the same continuous compression;
- smooth recovery toward vertical origin;
- mapper does not modify any Phase-4 leg rotations;
- unilateral/swing evidence releases grounded mapping;
- Jump/takeoff clears grounded offset immediately;
- standing leg scale stays stable through live crouch rotations;
- reset/binding-version/calibration changes clear or refresh the scale session;
- maximum crouch cap scales with avatar leg size rather than fixed meters.

Deterministic shadow math also confirmed proportionality and leg-relative clamping. No Unity Editor, .NET C# compiler or Unity Test Runner is available in the Builder execution environment, so the Editor tests are **not claimed as executed**.

## Next boundary

The next action is genuine USER Unity webcam QA focused on shallow/deep crouch shape, foot-floor proximity, recovery, one-leg freedom, horizontal regression check and jump release.

Do not start Phase 6, reopen performance/hands/Phase-3/general-Phase-4/provider work, or merge `main` before that QA is reviewed.
