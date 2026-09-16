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

Grounded-foot corrective-pass baseline/handoff:

`3390492e9efdd72fa3bab9cc8a58910e7473ed06`

Grounded-foot implementation/tests/diagnostics checkpoint:

`a3f9e273b575146c3b56a3966d123f48177c5b5a`

Current status:

**GROUNDED FOOT CONSTRAINT IMPLEMENTED / USER QA PENDING**

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
-> Phase 4 positional/analytic-IK avatar control
-> Phase 5 root translation
-> presentation
```

Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths. The accepted low-end optimization milestone remains frozen. Phase 3 remains locomotion/calibration data authority. Phase 4 remains the USER-accepted normal pose/IK authority.

Foundation A commands/speech and Foundation B cameras remain retained. Foundation C is dormant research, Foundation D hands are deferred, Foundation E is retired from production, and coarse hands remain deferred/rolled back.

## Phase-5 authority reconstruction remains frozen

The prior reconstruction remains the horizontal baseline. USER runtime evidence after that pass showed that locomotion-caused idle/root jitter was mostly fixed, so this grounded-foot pass deliberately does not reopen horizontal ownership.

`CameraSpaceRootTracker` remains the **only stateful physical X/Z position authority**. It owns accepted displacement, filtering/velocity, bilateral support validation, recenter and tracking-loss/reacquisition continuity.

Torso center plus yaw-compensated apparent scale remain the continuous body/root candidate. Weighted ankle/heel/toe support evidence validates room relocation. Support hysteresis remains `0.12` single-enter / `0.06` dual-return. Only coherent bilateral support can validate new room-position movement.

`LocomotionFusion` remains mapping/blending only. Current fusion scales/deadzones, physical velocity cadence suppression, cadence tuning and horizontal recenter semantics are unchanged.

No behavior changes were made to:

- `CameraSpaceRootTracker.cs`;
- `LocomotionFusion.cs`;
- cadence implementation/defaults;
- tracking-loss continuity;
- horizontal recenter.

## Vertical root authority remains body-compression driven

`VerticalLocomotionInterpreter` remains the only Jump/Crouch semantic authority. Negative root Y still comes from trustworthy normalized pelvis-to-support compression relative to the runtime standing reference.

A shallow planted bend may lower root Y while semantic state is still `Standing`; deeper compression follows the same proportional path and may cross semantic `Crouch` at the existing `0.18` enter / `0.09` release thresholds. Current crouch mapping remains `1.20` world scale / `0.65` max depth.

Jump remains coherent whole-body rise and exclusively owns positive root Y while active. Existing jump thresholds, lifecycle, tracking grace and controller/fusion depth isolation are unchanged.

Critically, solved avatar foot motion does **not** choose root Y. The dependency remains:

```text
tracked pelvis-to-support compression
-> Phase-5 root Y descent
-> grounded leg residual correction
```

not:

```text
solved foot position
-> choose root Y
```

## Grounded-foot constraint corrective pass

USER QA of the reconstruction exposed one remaining crouch hierarchy defect: body descent could occur, but the avatar feet could sink below the standing floor plane even though the USER's real feet remained planted.

The new `GroundedFootConstraint` fixes only that residual geometry after root placement.

### Standing floor reference

Once calibration and the existing vertical standing reference are valid, both leg chains are available, vertical evidence is live/coherent/grounded, semantic state is `Standing`, grounded bend is inactive and root-Y offset is effectively zero, the helper captures each avatar foot tip's current world Y.

The left and right standing Y values are retained independently. The reference is not continuously rewritten during crouch or jump.

It is cleared on:

- calibration loss/new calibration session;
- player-root/binding change;
- `HumanoidRigBinding.ReferencePoseVersion` change.

Horizontal `Recenter()` does not redefine it.

### Active gate

The post-root constraint runs only when all of the following are true:

- calibration/body reference is valid;
- binding and both leg chains are available;
- vertical reference is ready and the vertical measurement is currently available;
- semantic state is not `Jump`;
- support rise remains inside existing grounded tolerance;
- bilateral foot asymmetry remains inside the existing maximum;
- grounded bend is active, or filtered negative root Y is still recovering toward standing;
- locomotion root drive is enabled.

Jump/takeoff, unilateral swing/asymmetric support, missing chains and unavailable vertical evidence immediately skip the correction rather than fabricating contact.

### Target semantics

For each leg, after Phase-5 root X/Z/Y has already been written:

```text
post-Phase4/post-root foot = (x, y, z)
grounded target          = (x, capturedStandingFootY, z)
```

Therefore Phase 4 retains live stance width and X/Z foot intent. The constraint corrects only the residual vertical floor error.

A grounded target is never constructed below the captured standing plane.

### IK reuse and execution order

`GroundedFootConstraint` uses the existing project-owned `AnalyticTwoBoneIkSolver` and existing `TwoBoneIkRequest/Result` types. It reads chain root/mid/tip transforms and measured segment lengths through `HumanoidRigBinding`.

No second IK algorithm was introduced and normal `HumanoidRetargeter` behavior was not modified.

Bend stability hierarchy is:

1. current Phase-4-solved knee plane;
2. previous reliable grounded-correction bend direction;
3. existing binding/reference chain bend direction.

The correction rotates only the two leg root/mid joints; it never writes avatar/player root position.

Runtime order is now:

```text
HumanoidRetargeter @ 100
-> normal Phase-4 solve + existing presentation smoothing
EmbodiedLocomotionController @ 150
-> authoritative Phase-5 X/Z/Y root placement
-> grounded-foot residual solve for LeftLeg + RightLeg only when gated
LocomotionPrototypeView @ 170
-> presentation diagnostics
```

Because the grounded correction runs after Phase-4's visible/smoothed pose has already been written, Phase-4 presentation smoothing does not overwrite it later in the same frame.

### Reach limits

The existing analytic solver continues to clamp unreachable endpoint requests. Grounded diagnostics expose whether either leg was reach-clamped. Endpoint residual is diagnostic only and never feeds back into root Y.

## Diagnostics

F9 locomotion diagnostics retain reconstructed physical/vertical authority data and now additionally show:

- grounded standing-plane reference ready/waiting;
- grounded lock active/idle;
- left/right post-solve Y residual from the captured standing plane;
- whether either leg target was reach-clamped.

No per-frame Console logging was added.

## Deterministic coverage

Existing reconstructed `Phase5LocomotionTests.cs` and `VerticalLocomotionTests.cs` were left untouched.

New `GroundedFootConstraintTests.cs` covers:

1. stable bilateral standing-plane capture;
2. shallow root descent with both feet restored to standing Y;
3. deeper crouch with root lower while both feet remain on-plane;
4. shallow -> deep -> recovery plane continuity;
5. finite/same-side knee bend direction through that sequence;
6. Y-only grounded target construction preserving current X/Z;
7. target never below captured plane;
8. semantic Jump release;
9. takeoff/support-rise release;
10. unilateral/swing evidence release;
11. missing leg chain safe failure;
12. explicit reset and binding-reference-version invalidation;
13. calibration-loss reference reset without root corruption.

The synthetic test rig's shallow/deep/recovery targets are all within the existing analytic solver's reachable interval, so normal expected cases do not depend on reach clamping.

## Verification status

No Unity Editor, .NET C# compiler or Test Runner is available in the Builder execution environment. The focused Editor tests are authored and the source/geometry/diff are statically audited, but they are **not claimed as executed**.

The next boundary is genuine USER Unity webcam QA focused on shallow/deep grounded bends, recovery, swing-leg release, jump release and confirmation that horizontal idle stability remains unchanged.

Do not start Phase 6, reopen performance/hands work, or merge `main` before that QA is reviewed.
