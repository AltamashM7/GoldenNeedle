# Golden Needle — Orchestrator Handoff

Handoff refresh: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## First actions

Before new work:

1. verify the live remote `engine/pose-tracking-spike` HEAD;
2. read `Docs/current-state.md`;
3. read `Docs/optimization-orchestrator-handoff.md` for frozen low-end/OpenVINO invariants;
4. read `Docs/decisions.md`;
5. read `Docs/motion-engine.md`;
6. inspect the avatar-relative reconstruction baseline -> final diff and focused tests independently.

Do not merge to `main` without explicit USER approval. Do not force-push, rebase, amend, reset or rewrite shared history.

## Current corrective checkpoint

USER-tested crouch-failure runtime:

`9cda38b5c47f38d91800839cdd99db1d52fb0916`

Avatar-relative reconstruction brief/baseline:

`5e90551b7045dba96787c43fb9d4562d3438257a`

Implementation/tests/diagnostics checkpoint:

`2c9c3f6eaf4801e1832b32ad7afc79bb6597807b`

Required status:

**AVATAR-RELATIVE CROUCH GROUNDING RECONSTRUCTED / USER QA PENDING**

Motion Engine V1 remains **NOT USER ACCEPTED**. Phase 6 remains **NOT STARTED**.

## USER evidence that triggered reconstruction

At `9cda38b5...`, the reconstructed horizontal locomotion was substantially improved, but deep crouch still produced an unnatural compact/tucked squat. The pelvis/body descended while the knees/legs folded aggressively underneath it rather than retaining the natural tracked Phase-4 bend.

The intended behavior remains:

- shallow planted bend begins natural body descent;
- deeper crouch descends further;
- feet remain visually near the standing floor;
- Phase-4 tracked knee pose is preserved;
- recovery is smooth;
- one-leg lift and Jump remain free;
- horizontal idle/lean stability does not regress.

## Verified cause

The audit confirmed two compounding issues:

1. `VerticalLocomotionInterpreter` correctly produced normalized USER pelvis/support compression, but the production grounded root path used a fixed-world negative-Y mapping rather than scaling by the controlled avatar's lower-body geometry.
2. The later `GroundedFootConstraint` ran a complete two-bone IK solve on both legs after root movement to restore standing foot Y. That second solve could materially replace the Phase-4 knee bend and create the USER-observed tucked pose.

Historical checkpoints were inspected before editing:

- `220a4b958c8a3d280799ea3c0836fa6536a486a0` confirmed the useful Batch-3 normalized compression signal without a later general crouch-leg solve;
- `86701d0082692c17844db89c03fe4753e1876cf6` showed avatar leg geometry can inform scaling but also why solved feet must not decide crouch depth;
- `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0` provided a simpler pre-Foundation reference with Phase 5 primarily translating root rather than re-authoring crouch legs.

## Frozen horizontal architecture

Do not reopen horizontal locomotion for this issue.

`CameraSpaceRootTracker` remains the single stateful physical X/Z owner. `LocomotionFusion` remains mapping/blending only. Cadence, heading, support validation, horizontal recenter and tracking-loss/reacquisition continuity are unchanged.

The avatar-relative reconstruction does not modify horizontal runtime files or tests.

If later USER QA reports an X/Z regression, first prove it is caused by the new root-Y mapping before considering any horizontal edit.

## Preserved vertical semantic authority

`VerticalLocomotionInterpreter` remains the sole Jump/Crouch semantic state machine and source of normalized grounded compression.

Semantic thresholds and safety gates are unchanged. Shallow trustworthy compression may begin while state is still `Standing`; deeper compression may acquire semantic Crouch at the existing `0.18` enter threshold and release at `0.09`.

The interpreter's historical fixed-world negative `worldOffsetY` remains in the class for compatibility/tests, but the production controller does **not** use it for grounded non-Jump root placement anymore.

## Avatar-relative production crouch path

`AvatarRelativeCrouchGrounding` replaces `GroundedFootConstraint`.

It reads only stable binding reference geometry. For each leg it prefers the reference root-to-foot vertical span in the binding anatomical parent frame, then direct span, then cached total reach. Left/right are averaged and cached against `HumanoidRigBinding.ReferencePoseVersion`.

Production grounded root mapping is:

```text
motionDeadband = clamp(0.25 * crouchReleaseThreshold, 0.01, 0.05)
effectiveCompression = max(0, crouchCompression - motionDeadband)
requestedDepth = effectiveCompression
                 * avatarStandingLegScale
                 * 1.20
maximumDepth = avatarStandingLegScale * 0.65
rootOffsetY = -min(requestedDepth, maximumDepth)
```

`1.20` is now an explicitly named dimensionless `Avatar Crouch Depth Multiplier` in the new mapper. `0.65` is an explicitly named `Maximum Crouch / Leg Fraction`. The old interpreter settings retain their existing semantics; no serialized field was silently repurposed.

The only committed scene, `Assets/Scenes/SampleScene.unity`, has no serialized locomotion/crouch override, so no scene migration was needed.

## Phase 4 owns the legs

The old full `GroundedFootConstraint` is removed from runtime. There is no post-root analytic IK solve for grounded crouch and no crouch-specific leg rotation write after `HumanoidRetargeter`.

Current frame order is:

```text
HumanoidRetargeter @ 100
  -> normal Phase-4 pose / IK / smoothing
EmbodiedLocomotionController @ 150
  -> X/Z root placement
  -> avatar-relative grounded Y OR Jump Y
LocomotionPrototypeView @ 170
  -> diagnostics
```

This means Phase 4 is again the sole normal knee/leg pose authority through crouch.

## Residual grounding

There is currently **no** foot-residual correction. This is intentional and should not be treated as unfinished code.

The USER-observed failure specifically implicated the large second endpoint solve, and the authoritative brief allowed no residual if avatar-relative root mapping is sufficient. The new diagnostics explicitly report `residual=off`.

If webcam QA later proves a small consistent foot-plane error remains, any future residual must be root-only, small, avatar-relative and bounded. It must not rotate knees, run another general leg solve, feed foot error into normalized compression, or become a new crouch-depth authority.

## Jump and tracking safety

Semantic Jump immediately bypasses grounded crouch mapping. Controller Jump Y is `max(0, verticalSample.worldOffsetY)` so stale negative crouch filter state cannot pin the first takeoff frame. Jump detection, thresholds and depth isolation are otherwise unchanged.

During the interpreter's short missing-tracking grace interval, the avatar-relative mapper holds the last trusted crouch offset. After the sample expires to `Unavailable`, it filters back toward standing.

Binding/reference-pose changes and calibration reset clear/rebuild the avatar-scale session.

## Diagnostics

F9 now exposes:

- normalized crouch compression;
- avatar standing-leg scale;
- avatar-relative primary root target;
- filtered/final crouch root offset;
- actual applied Y;
- depth-clamped/within-cap;
- `residual=off`.

Old `GROUND FEET` lock, endpoint-residual and reach-clamped diagnostics are removed because that architecture is no longer authoritative.

## Focused deterministic coverage

`AvatarRelativeCrouchGroundingTests.cs` replaces the obsolete full-IK grounded-foot suite and covers:

- neutral zero offset;
- shallow descent before semantic Crouch;
- deeper proportional descent;
- avatar-size proportionality across synthetic rigs;
- short/chibi versus tall-rig absolute-depth regression;
- semantic-state independence at equal compression;
- smooth recovery;
- no Phase-4 leg-rotation mutation;
- unilateral/swing release;
- Jump/takeoff release;
- stable reference scale through live leg rotations;
- reset/binding-version/calibration lifecycle;
- avatar-relative depth cap.

Existing `Phase5LocomotionTests.cs`, `VerticalLocomotionTests.cs` and normal Phase-4 tests remain untouched.

Shadow math confirmed proportional scale behavior and leg-relative cap behavior. The Builder environment has no Unity Editor, .NET C# compiler or Unity Test Runner; do not claim these Editor tests executed without USER/runner evidence.

## Immediate next action — USER webcam QA

Use the final live branch HEAD after independently verifying it, then run one concise webcam QA:

1. confirm Unity compiles with no red errors;
2. calibrate while standing normally;
3. shallow planted bend: upper body/pelvis should begin descending naturally;
4. continue to deep crouch: body should descend further without the previous tucked/folded-knee failure;
5. feet should remain visually near the standing floor plane without a large second IK pose change;
6. stand back up: smooth recovery, no abrupt pop;
7. lift one leg: no grounding pin;
8. recheck idle/lean X/Z stability: horizontal reconstruction should remain unchanged;
9. Jump once if practical: takeoff must not be pinned and applied Y must become non-negative/positive as tracking permits.

Record the result as:

- body descent: `correct / too little / too much`;
- feet: `grounded / sink / float`;
- knee pose: `natural / tucked / flips`;
- horizontal idle stability: `preserved / regressed`;
- Jump release: `pass / fail / not tested`.

If QA passes, review Motion Engine V1 acceptance separately. Do not start Phase 6, performance work, providers, Phase 3, general Phase 4, hands/foundations, or a `main` merge as part of this handoff.
