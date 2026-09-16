# Golden Needle — Current State

Authoritative current-state refresh: 2026-09-16.

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## Governance

- Do **not** merge to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or otherwise rewrite shared branch history.
- GitHub is the shared authoritative project state; independently inspect the live branch before implementation work.
- The USER is the decisive Unity/manual/runtime acceptance authority.
- Builder/static checks do not create USER acceptance.
- Phase 6 remains **NOT STARTED**.

## Current checkpoint

Batch 3 started from the independently verified remote HEAD:

`e6044b45d94dbe4ee9da3702828c1ee73409d475`

Batch-3 implementation + deterministic-test checkpoint:

`220a4b958c8a3d280799ea3c0836fa6536a486a0`

The Batch-3 work extends only Phase-5 locomotion/root-translation behavior, Phase-5 tests, Lab diagnostics/serialization, and current documentation. The accepted optimized body provider, Phase 3 authority, and Phase 4 production retarget/IK code remain untouched.

## Phase status

| Area | Current status |
|---|---|
| Phase 1 — provider/raw pose | **PASS WITH NOTES** |
| Phase 2 — canonical skeleton | **PASS** |
| Phase 3 — stabilization/confidence/calibration foundation | **PASS** |
| Phase 4 — humanoid retargeting | **USER ACCEPTED — PASS** |
| Low-end optimization milestone | **USER SATISFIED / FROZEN FOR CURRENT MILESTONE** |
| Motion Engine Batch 1 | **COMPLETE** |
| Motion Engine Batch 2 | **COMPLETE / USER QA DEFERRED INTO FINAL PASS** |
| Motion Engine Batch 2R | **COMPLETE / USER QA DEFERRED INTO FINAL PASS** |
| Motion Engine Batch 3 | **IMPLEMENTED / USER QA PENDING** |
| Motion Engine V1 | **NOT YET USER ACCEPTED** |
| Phase 6 — graybox vertical slice | **NOT STARTED** |

Do not describe Motion Engine V1 as accepted until the final integrated USER Unity QA succeeds.

## Preserved production body pipeline

The accepted normal low-end path remains:

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
-> Phase 4 positional/IK avatar control
-> Phase 5 root translation
-> presentation
```

Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths. The normal scheduling rule remains one active inference plus at most one replaceable newest pending frame; no FIFO/history/replay/catch-up backlog is introduced.

Stable Phase 3 canonical data remains calibration and locomotion authority. Phase 4 signed canonical-to-avatar mapping, positional targets, project-owned analytic two-bone IK, and modular calibration remain accepted pose authority. Phase 5 does not replace or modify those responsibilities.

## Foundation disposition

- Foundation A commands/speech: **RETAINED / WORKING**.
- Foundation B camera presets: **RETAINED**.
- Foundation C rich anatomical orientation: **DEFERRED / DORMANT RESEARCH**.
- Foundation D detailed hands: **DEFERRED** after unacceptable low-end cost in USER testing.
- Foundation E post-Phase-4 detail: **RETIRED FROM PRODUCTION**.
- Coarse-hand/fist experiment: **DEFERRED / ROLLED BACK**.

None of those deferred systems were reopened in Batch 3.

## Phase 5 horizontal locomotion — Batch 2 + 2R retained

`CameraSpaceRootTracker` remains support-aware:

- near-equal foot heights -> `Both` midpoint authority;
- left foot clearly lower -> `Left` support authority;
- right foot clearly lower -> `Right` support authority;
- hysteresis retains the previous single-foot authority through the ambiguous band.

Current thresholds remain `supportSingleFootEnter = 0.12` and `supportBothEnter = 0.06`, normalized by body/reference scale.

Batch 2R remains intact: landing transitions continuity-rebase so they cannot teleport, but coherent dual-support relocation can subsequently release the temporary landing offset so a genuine alternating physical step eventually produces net room displacement. Tracking-loss reacquisition rebases remain non-releasable for safety.

Cadence remains the accepted Batch-2 baseline:

- `eventThreshold = 0.07`;
- `acquisitionEvents = 2`;
- `acquireConfidence = 0.38`;
- `sustainConfidence = 0.25`;
- `stopTimeoutSeconds = 0.50`;
- `minimumStepRate = 0.8`;
- `maximumStepRate = 4.5`;
- `virtualStridePerStep = 0.60` (**Distance Per Step**);
- `maximumVirtualSpeed = 3.0` (**Maximum Cadence Speed**).

Batch 3 does not change cadence, heading, horizontal recenter, horizontal fusion defaults, or the Batch-2/2R root-tracker implementation.

## Phase 5 vertical locomotion — Batch 3

### Dedicated interpreter

Batch 3 adds `VerticalLocomotionInterpreter` and `VerticalLocomotionSettings` under `Assets/GoldenNeedle/Core/Motion/Locomotion/`.

The interpreter consumes only the existing stabilized canonical pose plus the accepted calibration profile. It performs no additional inference and does not write bones.

It exposes deterministic state/data through `VerticalLocomotionSample`:

- availability/reference-ready state;
- `Standing`, `Jump`, `Crouch`, or `Unavailable`;
- jump phase (`Grounded`, `Takeoff`, `Airborne`, `Landing`);
- normalized jump signal;
- normalized crouch compression;
- support rise;
- left/right foot asymmetry;
- apparent-scale change;
- tracked world root-Y offset;
- jump/depth-suppression flag.

### Standing reference

A runtime standing reference is captured only after body calibration is valid and the required stabilized lower-body/torso image joints are trustworthy.

Required evidence includes both ankle/heel/toe support-foot groups, pelvis, chest, hips and knees. The capture check also rejects badly asymmetric support and obviously unusable body geometry. Once captured, the reference is not continuously rewritten while crouching or jumping.

Calibration/body-reference loss resets the vertical interpreter. A new valid calibration session establishes a new vertical root origin and standing reference. Ordinary horizontal `Recenter()` remains independent and does **not** redefine the vertical standing reference or root-Y origin.

### Jump

Jump acquisition requires coherent upward evidence rather than a single raised foot:

- left and right weighted support feet rise together;
- normalized foot asymmetry remains below the configured maximum;
- pelvis and chest rise in the same direction;
- the spread among left-foot, right-foot, pelvis and chest rise remains bounded;
- apparent-scale change remains small enough that camera-depth change is not the better explanation.

The jump lifecycle is stateful: `Grounded -> Takeoff -> Airborne -> Landing -> Grounded`. A short tracking grace may hold an already acquired jump through temporary lower-body loss; after grace expires the state becomes unavailable and the root-Y offset returns toward neutral rather than remaining stuck airborne.

Single-leg lift therefore does not acquire jump.

### Crouch

Crouch uses normalized reduction in pelvis-to-support height relative to the standing reference. Acquisition additionally requires the support base to remain approximately grounded, left/right support to remain coherent, pelvis/chest to move downward, and apparent scale to remain stable.

Crouch has separate enter/release thresholds so it can be held without flicker. Rising from crouch first returns to `Standing`; it does not immediately reinterpret the upward recovery as a jump.

Single-leg lift and planted torso lean do not acquire crouch.

### Mutual exclusion

Jump and crouch are not independent booleans. They share one explicit vertical state machine. Only one vertical action can own the root offset at a time.

### Root-Y application

`EmbodiedLocomotionController` remains the Phase-5 root-translation owner and still executes after Phase 4. It now captures a stable player-root Y origin for each valid calibration session and applies:

```text
desiredY = verticalOriginY + verticalSample.worldOffsetY
```

alongside the existing X/Z result. The vertical offset is measured/proportional, response-filtered, world-scaled, and clamped; it is not a fixed animation or ballistic jump.

No `CharacterController`, Rigidbody gravity, ground probing, collision system, animation state machine, or Phase-6 gameplay physics was introduced.

### Jump/depth cross-talk isolation

Support image-Y rises during a real jump can resemble camera-depth support movement. Batch 3 therefore adds the smallest boundary suppression at `EmbodiedLocomotionController`: while an acquired jump/landing owns vertical motion, the copy of `CameraSpaceRootSample` passed to `LocomotionFusion` holds the pre-jump depth displacement and zeroes depth velocity. Lateral physical displacement is left untouched.

`CameraSpaceRootTracker` itself is not redesigned or modified. Once vertical jump state releases, normal root-depth behavior resumes.

### Partial-body/unavailable behavior

Vertical actions require both lower-body support sides plus pelvis/torso evidence. Missing evidence does not fabricate a jump/crouch and does not invalidate Phase 3/4 pose or horizontal systems. An acquired vertical action can hold briefly through the configured grace period, then returns safely toward neutral and publishes `Unavailable`.

## Batch-3 Inspector defaults

The active defaults serialized in the Lab are:

- Minimum vertical joint confidence: `0.40`;
- Jump Detection Threshold: `0.12` normalized rise;
- Jump Release Threshold: `0.045`;
- Jump Height / World Scale: `1.60`;
- Maximum Jump Height: `0.90` world units;
- Maximum Jump Foot Asymmetry: `0.08`;
- Crouch Enter Threshold: `0.18` normalized compression;
- Crouch Release Threshold: `0.09`;
- Crouch Depth / World Scale: `1.20`;
- Maximum Crouch Depth: `0.65` world units;
- Vertical Response: `18/s`;
- Vertical Tracking Grace: `0.16 s`;
- Maximum apparent-scale change during vertical acquisition: `0.12` log-scale;
- Maximum jump coherence spread: `0.10`;
- Grounded support tolerance: `0.06`.

The first ten action-facing concepts use direct Inspector labels/tooltips; the final three are supporting normalized safeguards rather than raw camera-pixel thresholds.

## Lab diagnostics

The existing Phase-5 locomotion Lab display is extended by `LocomotionPrototypeView` with a compact vertical readout that follows the existing locomotion/UI visibility controls. It shows:

- vertical state and jump phase;
- availability/grace state;
- jump signal, foot asymmetry and apparent-scale delta;
- crouch compression and support rise;
- current root-Y offset/final Y;
- standing-reference and root-Y-origin readiness.

No per-frame Console logging was added.

## Deterministic coverage

Existing `Phase5LocomotionTests.cs` remains unchanged in Batch 3, preserving Batch-2/2R coverage for swing-foot isolation, alternating-step relocation, lean suppression, support loss/reacquisition, depth, cadence, fusion, heading and recenter.

New `VerticalLocomotionTests.cs` adds synthetic stabilized-canonical coverage for:

1. neutral standing captures reference and produces approximately zero Y offset;
2. one leg lifted -> no jump;
3. one leg moved laterally/vertically -> no jump/crouch;
4. coherent both-feet + pelvis/chest rise -> jump acquisition;
5. jump -> positive Y offset;
6. landing -> zero/standing without sticking;
7. second jump after landing;
8. apparent-scale/depth change alone -> no vertical action;
9. planted torso lean -> no jump/crouch;
10. grounded pelvis/support compression -> crouch acquisition;
11. held crouch -> stable negative offset;
12. crouch release hysteresis -> standing/zero;
13. jump/crouch mutual exclusion;
14. tracking loss/grace expiry -> no permanently stuck action;
15. in-place jump -> no meaningful accepted root-tracker X/Z displacement;
16. in-place crouch -> no meaningful accepted root-tracker X/Z displacement.

## Verification status

Builder status:

`BATCH 3 IMPLEMENTED / USER QA PENDING`

This execution environment does not provide a Unity Editor/Test Runner. The deterministic Editor tests were authored and source/diff semantics were statically audited, but they were **not executed here**. GitHub CI/status evidence must be checked at the final Batch-3 HEAD and reported accurately; absent a real run, do not claim an automated Unity pass.

Final comprehensive USER Unity QA is now the next acceptance step after Orchestrator review. Motion Engine V1 remains **NOT YET USER ACCEPTED** until that QA succeeds.

## Completion sequence

- Batch 1 — **COMPLETE**.
- Batch 2 — **COMPLETE**.
- Batch 2R — **COMPLETE**.
- Batch 3 — **IMPLEMENTED / USER QA PENDING**.
- Motion Engine V1 — **NOT YET USER ACCEPTED**.
- Phase 6 — **NOT STARTED**.

**Current status:** `BATCH 3 IMPLEMENTED / USER QA PENDING / AWAITING ORCHESTRATOR REVIEW`
