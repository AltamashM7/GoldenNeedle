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
6. inspect the grounded-foot baseline -> final diff and focused tests independently.

Do not merge to `main` without explicit USER approval. Do not force-push, rebase, amend, reset, or rewrite shared history.

## Current corrective checkpoint

Grounded-foot handoff/baseline:

`3390492e9efdd72fa3bab9cc8a58910e7473ed06`

Implementation/tests/diagnostics checkpoint:

`a3f9e273b575146c3b56a3966d123f48177c5b5a`

Required status:

**GROUNDED FOOT CONSTRAINT IMPLEMENTED / USER QA PENDING**

Motion Engine V1 remains **NOT USER ACCEPTED**. Phase 6 remains **NOT STARTED**.

## USER evidence that triggered this pass

After the Phase-5 authority reconstruction, USER runtime QA found:

- previous locomotion-caused idle/root jitter was mostly fixed;
- crouch/body descent still had a vertical hierarchy defect;
- at least one crouch lowered the body in the intended direction but the avatar feet moved below the visible floor while the USER's feet remained planted.

Therefore horizontal reconstruction is intentionally frozen. The grounded-foot corrective pass addresses only post-root leg geometry.

## Preserved production invariants

```text
WebCamTexture
-> WebCamCPU/GetPixels32
-> reusable 320x240 CPU prep
-> bounded newest-only two-slot scheduling
-> persistent OpenVINO CPU FP32 worker
-> MediaPipe pose semantics
-> CanonicalBodyV1
-> Phase 3 stabilization/calibration
-> Phase 4 normal positional/analytic-IK pose
-> Phase 5 authoritative root translation
-> gated grounded-leg residual solve
```

Preserve stock MediaPipe/TFLite and ExistingReadback as fallback/reference, latest-useful-frame-wins scheduling, Phase 3 stable canonical authority and the accepted normal Phase-4 mapping/IK behavior.

Foundation A speech/commands and Foundation B cameras remain retained. Foundation C is dormant, D hands deferred, E retired, coarse hands deferred/rolled back.

## Frozen reconstructed horizontal authority

`CameraSpaceRootTracker` remains the only stateful physical X/Z position owner. It continues to own accepted displacement, filtering/velocity, bilateral support validation, recenter and tracking-loss/reacquisition continuity.

`LocomotionFusion` remains mapping/blending only. Cadence and horizontal recenter are unchanged.

The grounded-foot pass does **not** modify:

- `CameraSpaceRootTracker.cs`;
- `LocomotionFusion.cs`;
- cadence implementation/defaults;
- horizontal recenter;
- horizontal reconstructed tests.

If later QA shows an X/Z regression, first prove it is caused by the new residual leg correction before reopening horizontal authority.

## Preserved vertical root authority

`VerticalLocomotionInterpreter` remains the only Jump/Crouch semantic state owner and the primary negative root-Y source remains normalized pelvis-to-support compression.

Shallow grounded compression may lower root while semantic state remains `Standing`. Deeper compression continues through the same path into semantic Crouch. Existing Crouch enter/release, world mapping, response and safeguards are unchanged.

Jump remains coherent whole-body rise and exclusively owns positive root Y while active. Existing jump/depth isolation remains.

The critical dependency is:

```text
tracked body compression
-> root Y descent
-> residual grounded leg correction
```

Never reintroduce:

```text
solved foot position
-> choose root Y
```

## Grounded-foot architecture

### Reference capture

`GroundedFootConstraint` captures left/right foot-tip world Y separately only when:

- calibration is valid;
- both leg chains are available;
- the existing vertical standing reference is ready;
- vertical evidence is currently available;
- support is approximately grounded;
- left/right foot evidence is coherent;
- semantic state is `Standing`;
- grounded bend is inactive;
- root-Y offset is effectively zero.

The reference is fixed through crouch/jump and resets on calibration/session reset, player-root/binding change or `HumanoidRigBinding.ReferencePoseVersion` change. Horizontal recenter does not reset it.

### Active gate

The residual solve requires:

- stored reference;
- both leg chains available;
- live/reference-ready vertical sample;
- state not `Jump`;
- support rise within existing `groundedSupportTolerance`;
- foot asymmetry within existing `maximumJumpFootAsymmetry`;
- grounded bend active or negative filtered root Y still recovering;
- Phase-5 root drive enabled.

Jump/takeoff, unilateral swing/asymmetry, unavailable vertical evidence and missing chains skip the correction.

### Target and solver

After Phase-5 root position is written:

```text
current foot = (x, y, z)
target       = (x, capturedStandingY, z)
```

Current X/Z intent remains Phase-4-owned. Only residual Y is corrected.

The helper reuses the public/stateless project `AnalyticTwoBoneIkSolver`, `TwoBoneIkRequest/Result`, and existing `HumanoidRigBinding` root/mid/tip/length/reference data. No duplicate IK algorithm is introduced.

Knee/bend preference is:

1. current Phase-4-solved knee plane;
2. previous reliable residual bend direction;
3. binding current/reference bend plane.

The helper rotates only upper/lower leg transforms. It has no Transform position writes and no root-Y feedback path.

Existing solver reach clamping remains the impossible-target safeguard and is diagnostic only.

## Execution-order audit

Verified current orders:

- `HumanoidRetargeter`: `DefaultExecutionOrder(100)`;
- `EmbodiedLocomotionController`: `DefaultExecutionOrder(150)`;
- `LocomotionPrototypeView`: `DefaultExecutionOrder(170)`.

`HumanoidRetargeter` completes its normal solve and presentation smoothing at 100. At 150 the controller writes final root X/Z/Y, then immediately runs the grounded residual leg solve. Therefore no later Phase-4 visible-rotation restoration overwrites the correction in the same frame.

## Diagnostics

F9 retains reconstructed authority/vertical diagnostics and adds:

- standing-foot reference ready/waiting;
- grounded lock active/idle;
- left/right foot-Y residual from plane;
- reach-clamped/reachable indication.

No per-frame Console logging is added.

## Focused deterministic coverage

New `Assets/GoldenNeedle/Tests/Editor/GroundedFootConstraintTests.cs` covers:

- bilateral standing plane capture;
- shallow root descent -> both feet restored to plane;
- deep crouch -> lower root + plane feet;
- recovery plane continuity;
- finite same-side knee bend through shallow/deep/recovery;
- current Phase-4 X/Z preservation in target construction;
- no grounded target below plane;
- semantic Jump release;
- support-rise/takeoff release;
- unilateral/swing release;
- missing-chain safe failure;
- reset and binding-version stale-reference invalidation;
- calibration-loss reset without root corruption.

Existing reconstructed `Phase5LocomotionTests.cs` and `VerticalLocomotionTests.cs` are untouched by this pass.

The synthetic shallow/deep/recovery target distances are inside the current analytic solver reach interval, so the normal deterministic cases are not dependent on reach clamping.

## Verification limitation

The Builder environment has no Unity Editor, .NET C# compiler or Unity Test Runner. Do not claim the Editor tests executed. Source/diff/geometry were statically audited only.

Check final GitHub status/workflow evidence at live HEAD; no CI run is a pass only if an actual run exists.

## Immediate next action — USER webcam QA

After independently verifying live HEAD and the baseline->final diff, USER QA should focus on:

1. compile with no red errors;
2. calibrate and stand neutral until F9 `GROUND FEET` reference is ready;
3. shallow planted knee bend: root/pelvis descends and both avatar feet stay at original floor Y;
4. continue into deep crouch: pelvis descends further while feet remain on-plane;
5. recover to standing: feet remain on-plane throughout and body rises smoothly;
6. lean/sway without crouching: no new horizontal regression;
7. lift one leg: raised leg remains free and is not snapped to floor;
8. genuine jump: grounded lock releases and takeoff is not pinned;
9. confirm reconstructed idle X/Z stability remains as good as previous QA;
10. report any foot sinking/floating, knee flip, `reach-clamped` diagnostic, compile error or new warning.

If this QA passes, review Motion Engine V1 acceptance separately. Do not start Phase 6, logging/performance investigation, hands/foundations work or `main` merge as part of this handoff.
