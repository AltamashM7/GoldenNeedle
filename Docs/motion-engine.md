# Golden Needle — Motion Engine V1

Status: **GROUNDED FOOT CONSTRAINT IMPLEMENTED / USER QA PENDING. MOTION ENGINE V1 NOT YET USER ACCEPTED. PHASE 6 NOT STARTED.**

Authoritative refresh: 2026-09-16.

## Core invariant: POSE != LOCOMOTION

Phase 4 remains the USER-accepted normal pose/bone/IK authority. Phase 5 executes later and owns avatar/player root translation. The grounded-foot corrective pass adds one narrowly gated **post-root residual leg solve**; it does not replace normal Phase-4 pose authority and it does not use solved feet to decide root translation.

## Preserved body pipeline

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
-> grounded-foot residual leg correction when trustworthy
-> presentation
```

OpenVINO/WebCamCPU remains the accepted low-end path. Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths. The optimization milestone remains frozen.

## Phase-5 reconstruction baseline remains frozen

The authority reconstruction preceding this corrective pass established:

- `CameraSpaceRootTracker` as the single stateful physical X/Z owner;
- torso center + yaw-compensated apparent scale as continuous body/root candidate;
- ankle/heel/toe support as bilateral movement validation;
- support hysteresis `0.12` single-enter / `0.06` dual-return;
- one accepted displacement filter/reacquisition continuity path;
- `LocomotionFusion` as mapping/blending only;
- cadence tuning unchanged;
- pelvis-to-support compression as primary negative root-Y signal.

USER runtime after reconstruction reported locomotion-caused idle/root jitter as mostly fixed. Therefore this grounded-foot pass does not alter `CameraSpaceRootTracker`, `LocomotionFusion`, cadence or horizontal recenter.

Grounded-foot corrective-pass baseline: `3390492e9efdd72fa3bab9cc8a58910e7473ed06`.

Implementation/tests/diagnostics checkpoint: `a3f9e273b575146c3b56a3966d123f48177c5b5a`.

## Horizontal physical locomotion

Horizontal architecture and active values are unchanged from the reconstruction.

`CameraSpaceRootTracker` alone owns physical reference, accepted displacement, filtering/velocity, bilateral support validation, recenter and tracking-loss/reacquisition continuity.

`LocomotionFusion` maps that authoritative displacement through the accepted Phase-4 reference basis, holds mapped contribution during temporary invalidity, suppresses cadence from authoritative root velocity and adds cadence along accepted body heading.

Cadence remains:

- event threshold `0.07`;
- acquisition events `2`;
- acquire confidence `0.38`;
- sustain confidence `0.25`;
- stop timeout `0.50 s`;
- step-rate range `0.8–4.5/s`;
- Distance Per Step `0.60`;
- Maximum Cadence Speed `3.0`.

Horizontal `Recenter()` remains X/Z-only and preserves avatar world position.

## Vertical root locomotion

### Body compression remains root-Y authority

`VerticalLocomotionInterpreter` remains the only semantic Jump/Crouch state authority.

Negative root Y remains proportional to normalized pelvis-to-support compression relative to the runtime standing reference. A shallow planted bend can lower root while semantic state remains `Standing`; deeper bend continues through the same path and can acquire semantic Crouch.

Current semantic thresholds remain `0.18` enter / `0.09` release. Crouch world mapping remains `1.20` scale / `0.65` max depth. The existing small neutral motion deadband remains derived from crouch-release tuning.

The controller still applies:

```text
X/Z = virtualOriginXZ + mapped authoritative physical contribution + cadence integration
Y   = verticalOriginY + verticalSample.worldOffsetY
```

### Jump remains exclusive positive-Y authority

Jump remains coherent whole-body rise from bilateral support + pelvis + chest. Existing thresholds, lifecycle, apparent-scale/asymmetry/coherence safeguards, tracking grace and maximum jump mapping remain unchanged.

While jump/landing owns vertical motion, the controller still holds the pre-jump physical depth in the copy passed to fusion and zeroes depth velocity, preventing takeoff image-Y from becoming world Z.

## Grounded-foot residual constraint

### Why it exists

After the authority reconstruction, USER QA showed the remaining crouch defect was geometric rather than a root-Y measurement failure: root/pelvis descent could be correct while the avatar feet moved below the visible standing floor plane.

The fix therefore constrains the legs **after** root descent instead of changing root descent itself.

### Standing foot plane

`GroundedFootConstraint` captures left and right foot-tip world Y separately when:

- calibration/body reference is valid;
- both leg chains are available;
- the vertical standing reference is ready;
- vertical evidence is currently available;
- support is within the existing grounded tolerance;
- bilateral foot asymmetry is within the existing bound;
- semantic state is `Standing`;
- grounded bend is inactive;
- root-Y offset is effectively zero.

The reference remains fixed through bends and jumps. It is reset by calibration/session reset, binding/player-root change or binding reference-pose-version change.

### Grounded target

After Phase-5 root placement, each leg uses:

```text
current visible foot = (x, y, z)
target foot          = (x, capturedStandingY, z)
```

Only Y is constrained. Current Phase-4 stance X/Z is preserved.

### Ground/contact gate

The constraint applies only when:

- both leg chains remain available;
- vertical evidence is live and reference-ready;
- state is not `Jump`;
- support rise is inside existing grounded tolerance;
- foot asymmetry remains inside the existing maximum;
- grounded bend is active or filtered negative root Y is still recovering;
- Phase-5 root drive is enabled.

Consequences:

- jump/takeoff releases immediately;
- a raised/swinging unilateral leg is not forced to the floor;
- unavailable lower-body evidence skips the solve;
- neutral standing captures/holds the reference but does not continuously re-solve the legs.

### IK implementation

The helper reuses the existing public/stateless Golden Needle `AnalyticTwoBoneIkSolver` and existing `TwoBoneIkRequest/Result` contract. It consumes existing chain root/mid/tip transforms, lengths and reference bend information from `HumanoidRigBinding`.

There is no second IK algorithm and no change to normal `HumanoidRetargeter` target generation, signed mapping, arm solving or presentation smoothing.

For each leg, bend direction preference is:

1. current solved knee plane from Phase 4;
2. previous reliable grounded-correction bend direction;
3. current/reference binding bend plane.

Only upper/lower leg rotations are written. The helper never writes root position.

### Reach clamping

Existing analytic solver reach clamping remains the sole impossible-target safeguard. A clamp is surfaced through diagnostics. Endpoint residual is diagnostic only and never modifies tracked crouch depth/root Y.

## Runtime execution order

```text
HumanoidRetargeter @ 100
  normal Phase-4 solve
  existing Phase-4 presentation smoothing
EmbodiedLocomotionController @ 150
  root X/Z/Y placement
  grounded LeftLeg/RightLeg residual solve when gated
LocomotionPrototypeView @ 170
  diagnostics
```

This ensures no later Phase-4 presentation restoration overwrites the grounded residual solve in the same frame.

## Diagnostics

F9 retains reconstructed physical/vertical authority information and adds:

- standing-foot reference ready/waiting;
- grounded lock ACTIVE/idle;
- left/right foot-Y residual from standing plane;
- reach-clamped/reachable status.

No per-frame Console logging is introduced.

## Deterministic coverage

The existing reconstructed horizontal and vertical suites are unchanged in this pass.

New `GroundedFootConstraintTests.cs` exercises:

- bilateral reference capture;
- shallow root descent -> both tips return to captured Y;
- deep crouch -> root remains lower while both tips remain on-plane;
- crouch recovery plane continuity;
- finite/same-side knee bend through shallow/deep/recovery;
- Y-only target construction preserving Phase-4 X/Z;
- no grounded target below the standing plane;
- semantic Jump release;
- takeoff/support-rise release;
- unilateral swing release;
- missing-chain safe failure;
- reset/binding-version invalidation;
- calibration-loss reset without root movement.

The synthetic shallow/deep/recovery requests are within the current analytic solver reach range, so ordinary expected cases are not relying on target clamping.

## Verification limitation and USER boundary

No Unity Editor, .NET C# compiler or Test Runner is available in the Builder environment. The focused tests and source are statically audited but are **not claimed as executed**.

Next action is genuine USER Unity webcam QA of grounded bends, recovery, unilateral leg lift, jump release and regression check of reconstructed horizontal idle stability.

Do not start Phase 6, performance logging/optimization, hands/foundations work or `main` merge as part of this pass.
