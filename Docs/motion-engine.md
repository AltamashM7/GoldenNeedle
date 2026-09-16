# Golden Needle — Motion Engine V1

Status: **AVATAR-RELATIVE CROUCH GROUNDING RECONSTRUCTED / USER QA PENDING. MOTION ENGINE V1 NOT YET USER ACCEPTED. PHASE 6 NOT STARTED.**

Authoritative refresh: 2026-09-16.

## Core invariant: POSE != LOCOMOTION

Phase 4 remains the USER-accepted normal pose/bone/IK authority. Phase 5 executes later and translates the avatar/player root. No crouch-specific leg solve runs after Phase 4 in the current architecture.

The production body pipeline remains:

```text
Unity WebCamTexture
-> WebCamCPU/GetPixels32 acquisition
-> reusable 320x240 CPU preparation
-> newest-only bounded two-slot scheduling
-> persistent OpenVINO CPU FP32 worker
-> MediaPipe pose semantics
-> CanonicalBodyV1
-> Phase 3 stabilization/calibration
-> Phase 4 positional/analytic-IK pose
-> Phase 5 root translation
-> presentation
```

OpenVINO/WebCamCPU remains the accepted low-end path. Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths. Performance/provider work is frozen for this milestone.

## Current corrective lineage

USER-tested crouch-failure baseline:

`9cda38b5c47f38d91800839cdd99db1d52fb0916`

Avatar-relative reconstruction brief/baseline:

`5e90551b7045dba96787c43fb9d4562d3438257a`

Implementation/tests/diagnostics checkpoint:

`2c9c3f6eaf4801e1832b32ad7afc79bb6597807b`

The prior USER runtime showed the reconstructed horizontal locomotion was substantially improved, while deep crouch still produced a compact/tucked leg pose. Inspection verified two compounding causes: fixed-world grounded root-Y scaling did not adapt to avatar proportions, and a later full two-bone grounded-foot solve could materially re-author the Phase-4 knee pose.

Historical comparison used:

- `220a4b958c8a3d280799ea3c0836fa6536a486a0` — original Batch-3 normalized pelvis/support compression;
- `86701d0082692c17844db89c03fe4753e1876cf6` — solved-foot-driven root-Y experiment, useful as scale evidence but rejected as crouch authority;
- `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0` — pre-Foundation reference without a later general crouch-leg re-solve.

## Horizontal locomotion is frozen

`CameraSpaceRootTracker` remains the sole stateful physical X/Z owner. It still owns accepted displacement, position/velocity filtering, bilateral support validation, tracking-loss/reacquisition continuity and recenter.

`LocomotionFusion` remains mapping/blending only. Cadence, heading, support hysteresis, horizontal evidence thresholds, recenter and loss/reacquisition behavior are unchanged by this pass.

The avatar-relative crouch reconstruction does not modify `CameraSpaceRootTracker.cs`, `LocomotionFusion.cs`, cadence, heading, or horizontal tests.

## Vertical semantic authority

`VerticalLocomotionInterpreter` remains the sole Jump/Crouch semantic state machine and source of normalized body evidence.

Its normalized grounded compression remains:

```text
compression = 1 - currentPelvisSupportHeight / referencePelvisSupportHeight
```

Existing semantic behavior remains unchanged:

- shallow trustworthy compression may begin while state is still `Standing`;
- semantic Crouch enter/release remain `0.18 / 0.09`;
- support/asymmetry/apparent-scale gates remain unchanged;
- jump thresholds/lifecycle remain unchanged;
- vertical response remains `18/s` and tracking grace remains `0.16 s`.

The interpreter still publishes its historical `worldOffsetY` for compatibility and Jump. Its legacy grounded negative-Y conversion remains inside the interpreter so existing semantic tests and interfaces are not rewritten in this narrow pass. **The controller no longer uses that fixed-world negative value for grounded production crouch.**

## Avatar-relative grounded crouch mapping

`AvatarRelativeCrouchGrounding` now maps the interpreter's normalized compression onto the controlled avatar's stable reference geometry.

### Stable standing leg scale

For each leg it reads the already-cached `HumanoidRigBinding` reference pose and chooses, in order:

1. absolute reference root-to-foot displacement along the binding anatomical Up axis;
2. direct reference root-to-foot distance;
3. cached two-segment chain reach.

Left/right values are averaged. The result is cached against `HumanoidRigBinding.ReferencePoseVersion`, so live crouch rotations cannot shrink the scale while the USER bends.

No binding behavior, Phase-4 target generation or Phase-4 solver behavior changed.

### Mapping formula

For grounded non-jump production root-Y:

```text
motionDeadband = clamp(0.25 * crouchReleaseThreshold, 0.01, 0.05)
effectiveCompression = max(0, normalizedCompression - motionDeadband)
requestedDepth = effectiveCompression
                 * avatarStandingLegScale
                 * avatarCrouchDepthMultiplier
maximumDepth = avatarStandingLegScale * maximumCrouchLegFraction
rootOffsetY = -min(requestedDepth, maximumDepth)
```

Current explicit avatar-relative settings are:

- Avatar Crouch Depth Multiplier: `1.20` dimensionless;
- Maximum Crouch / Leg Fraction: `0.65` dimensionless.

A short/chibi avatar therefore gets a proportionally smaller absolute crouch translation than a tall/long-legged avatar at the same USER compression. The maximum depth also scales with avatar leg size instead of remaining a fixed world-unit cap.

The committed `Assets/Scenes/SampleScene.unity` contains no serialized locomotion/crouch override, so no scene migration was required. These are new explicitly named fields rather than silently changing the meaning of the interpreter's existing serialized fields.

### Recovery and tracking grace

The avatar-relative target uses the existing vertical response speed. Standing recovery filters smoothly back toward zero.

During the interpreter's short tracking-grace hold, the last trusted grounded root offset is held rather than recomputed from missing geometry. After the semantic sample expires to `Unavailable`, the mapper returns toward standing.

## Phase 4 remains sole leg-pose authority

The previous `GroundedFootConstraint` and its post-root `AnalyticTwoBoneIkSolver` calls are removed from runtime.

There is no crouch-specific upper-leg/lower-leg rotation write after the Phase-4 solve. The intended hierarchy is now:

```text
tracked USER pelvis/support compression
-> normalized crouch amount
-> stable avatar reference leg scale
-> Phase-5 root-Y translation
```

not:

```text
large fixed-world root descent
-> full second leg IK solve to recover floor
```

and never:

```text
solved avatar foot motion
-> decide how far USER crouched
```

No residual foot correction is currently applied. This is deliberate: the USER-visible failure implicated the full endpoint correction, and the brief explicitly preferred no residual over unnecessary complexity when avatar-relative mapping can be evaluated first. Diagnostics report `residual=off`.

If later USER QA shows a small repeatable floor error after the new scale mapping is validated, any future correction must be root-only, tightly avatar-relative/bounded and must not reintroduce knee-pose ownership.

## Jump

Jump remains the interpreter's coherent whole-body positive-Y action and keeps its existing thresholds, lifecycle and depth-isolation behavior.

When semantic Jump is active, the controller bypasses grounded crouch mapping. It uses the interpreter output clamped to non-negative Y:

```text
jumpRootY = max(0, verticalSample.worldOffsetY)
```

This narrow boundary guard prevents stale negative crouch filter state from pinning the first takeoff frame; it does not alter jump detection or tuning.

## Runtime execution order

```text
HumanoidRetargeter @ 100
  normal Phase-4 pose / analytic IK / presentation smoothing
EmbodiedLocomotionController @ 150
  authoritative Phase-5 X/Z root placement
  avatar-relative grounded Y root placement OR Jump Y
LocomotionPrototypeView @ 170
  diagnostics
```

No later crouch-specific leg solve exists.

## Diagnostics

F9 retains horizontal authority and vertical semantic information and now additionally shows:

- normalized crouch compression;
- captured avatar standing-leg scale;
- avatar-relative primary crouch target;
- filtered/final crouch root offset;
- actual applied vertical offset;
- depth-clamped/within-cap state;
- `residual=off`.

The old standing-foot lock, endpoint residual and reach-clamped diagnostics were removed because the full grounded-foot IK architecture is no longer active.

No per-frame Console logging is introduced.

## Deterministic coverage

Existing reconstructed `Phase5LocomotionTests.cs`, `VerticalLocomotionTests.cs` and Phase-4 retargeting tests are unchanged.

The rejected full-IK `GroundedFootConstraintTests.cs` was replaced by `AvatarRelativeCrouchGroundingTests.cs`. The focused suite covers:

- neutral zero offset;
- shallow grounded descent before semantic Crouch;
- deeper compression producing larger descent;
- proportional world-space descent across avatars with different standing leg scales;
- short/chibi regression versus tall/long-legged rig;
- semantic Standing/Crouch independence at equal compression;
- smooth standing recovery;
- no modification of Phase-4 leg rotations;
- unilateral/swing release;
- Jump/takeoff release;
- stable cached leg scale through live pose changes;
- reset, binding-reference-version and calibration lifecycle;
- avatar-relative maximum-depth clamping.

Shadow math verified proportional scaling and the leg-relative cap. The Builder environment has no Unity Editor, .NET C# compiler or Unity Test Runner, so the Editor tests are authored/statically audited but **not claimed as executed**.

## USER boundary

Next action is genuine USER Unity webcam QA of shallow/deep crouch shape, foot-floor proximity, recovery, one-leg freedom, preserved horizontal idle stability and Jump release.

Do not start Phase 6, reopen performance/providers/Phase3/general-Phase4/hands work, or merge `main` before this QA is reviewed.
