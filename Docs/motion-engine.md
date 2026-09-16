# Golden Needle — Motion Engine V1

Status: **BATCH 3 IMPLEMENTED / USER QA PENDING. MOTION ENGINE V1 NOT YET USER ACCEPTED. PHASE 6 NOT STARTED.**

Authoritative roadmap refresh: 2026-09-16.

Golden Needle Motion Engine V1 is a CPU-first webcam-driven embodied-control system. The accepted body stack reproduces trustworthy pose through Phase 3 + Phase 4, while Phase 5 interprets deliberate physical movement into separate avatar-root locomotion.

## Current phase status

| Phase / batch | Status |
|---|---|
| Phase 1 — provider/raw pose | **PASS WITH NOTES** |
| Phase 2 — CanonicalBodyV1 | **PASS** |
| Phase 3 — stabilization/calibration | **PASS** |
| Phase 4 — humanoid retargeting | **USER ACCEPTED — PASS** |
| Low-end optimization milestone | **USER SATISFIED / FROZEN FOR CURRENT MILESTONE** |
| Batch 1 — docs synchronization | **COMPLETE** |
| Batch 2 — horizontal locomotion | **COMPLETE** |
| Batch 2R — alternating-step continuity | **COMPLETE** |
| Batch 3 — jump + crouch | **IMPLEMENTED / USER QA PENDING** |
| Motion Engine V1 | **NOT YET USER ACCEPTED** |
| Phase 6 — gameplay vertical slice | **NOT STARTED** |

## Production body pipeline

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

Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths. Latest useful frame wins; the normal pipeline does not accumulate a FIFO/history/replay/catch-up backlog.

## POSE != LOCOMOTION

Phase 4 is pose authority. Phase 5 is root-translation authority.

`HumanoidRetargeter` continues to own the accepted signed canonical-to-avatar mapping, positional targets, analytic two-bone IK and presentation of body pose. Phase 5 runs later and moves the bound avatar/player root. Batch 3 does not alter Phase-4 bone-solving behavior.

Stable Phase 3 canonical data remains the locomotion input; Batch 3 adds no new inference stream.

## Horizontal locomotion — Batch 2 + 2R

Horizontal motion remains the accepted support-aware architecture.

`CameraSpaceRootTracker` builds weighted support feet from ankle/heel/toe observations and uses a stateful `Both/Left/Right` authority. Current normalized thresholds remain:

- single-foot support enter: `0.12`;
- return to dual support: `0.06`.

A raised swing foot is excluded from physical root authority while the lower planted foot remains support. Support switches continuity-rebase so switching authority cannot teleport the avatar. Tracking-loss reacquisition also rebases safely.

Batch 2R preserves continuity while allowing a completed alternating step to become real room displacement: after coherent dual-support relocation, the temporary landing rebase can release on a subsequent stable dual-support sample. Tracking-loss rebases do not use that release path.

Cadence remains unchanged from Batch 2:

- event threshold `0.07`;
- acquisition events `2`;
- acquire confidence `0.38`;
- sustain confidence `0.25`;
- stop timeout `0.50 s`;
- step-rate range `0.8–4.5/s`;
- Distance Per Step `0.60`;
- Maximum Cadence Speed `3.0`.

`LocomotionFusion` still maps trusted camera-space physical X/Z through the accepted Phase-4 reference map and progressively suppresses cadence during meaningful physical translation.

Horizontal `Recenter()` still makes current physical X/Z the new origin while preserving virtual world position.

## Vertical locomotion — Batch 3

Batch 3 started from:

`e6044b45d94dbe4ee9da3702828c1ee73409d475`

Implementation + tests checkpoint:

`220a4b958c8a3d280799ea3c0836fa6536a486a0`

### `VerticalLocomotionInterpreter`

The dedicated vertical module consumes only:

```text
runtime.StabilizedFrame
+ MotionCalibrationProfile
+ delta time
```

It publishes `VerticalLocomotionSample` rather than burying action calculations inside the root controller.

The sample contains availability/reference readiness, high-level state, jump phase, jump signal, crouch compression, support rise, foot asymmetry, apparent-scale delta, world root-Y offset and the jump/depth-suppression flag.

### Standing reference

The interpreter keeps a runtime image/anatomical standing reference because the existing calibration profile does not store the complete image-space standing footprint required for jump/crouch interpretation.

Reference capture requires:

- valid calibrated body reference;
- both weighted support feet from ankle/heel/toe;
- pelvis, chest, both hips and both knees;
- configured joint confidence;
- reasonably symmetric foot height;
- usable leg/torso/knee geometry.

The reference is persistent during ordinary operation and is not continuously redefined while jumping or crouching. Calibration/body-reference loss resets it. A new valid calibration session establishes a new vertical root-Y origin/reference.

Horizontal `Recenter()` deliberately does not modify the vertical standing reference or root-Y origin.

### Jump

Jump is defined as coherent whole-body upward translation.

Normalized evidence is measured relative to standing pelvis-to-support height. The acquisition signal is the minimum coherent rise among:

- left support foot;
- right support foot;
- pelvis;
- chest.

Acquisition additionally requires:

- left/right foot-rise asymmetry below the configured maximum;
- bounded spread across the four rise signals;
- apparent-scale change below the camera-depth rejection threshold.

This means a single lifted leg cannot acquire jump.

Jump lifecycle:

```text
Grounded
-> Takeoff
-> Airborne
-> Landing
-> Grounded
```

Enter/release thresholds provide hysteresis. A short configured tracking grace can hold an already acquired jump through temporary lower-body loss. When grace expires, the action becomes unavailable and root-Y returns toward neutral rather than getting stuck airborne.

World jump offset is proportional to measured normalized rise:

```text
jumpOffsetY = clamp(jumpSignal * jumpWorldScale, 0, maximumJumpHeight)
```

No autonomous ballistic animation or gravity is used.

### Crouch

Crouch is defined primarily by normalized compression of:

```text
pelvis-to-support height
```

relative to the standing reference.

Acquisition additionally requires:

- support base approximately grounded;
- left/right support coherent;
- pelvis and chest moving down rather than the whole body moving up/down together;
- apparent-scale change stable enough not to indicate camera-depth change.

Enter/release thresholds provide hysteresis. Crouch can be held indefinitely while valid evidence remains above the release threshold.

World crouch offset is proportional and negative:

```text
crouchOffsetY = -clamp(compression * crouchWorldScale, 0, maximumCrouchDepth)
```

Rising from crouch returns to `Standing` before another action can acquire, which prevents normal crouch recovery from being classified as jump.

### Mutual exclusion

Vertical state is explicit rather than two unrelated booleans:

- `Unavailable`;
- `Standing`;
- `Jump`;
- `Crouch`.

Only one vertical action can be active at a time. Jump has its own sub-phase lifecycle.

### Controller/root application

`EmbodiedLocomotionController` remains the Phase-5 translation owner and executes after Phase 4. For each valid calibration session it captures the current player-root Y as the standing origin.

Final tracked root target becomes:

```text
X/Z = existing horizontal virtual origin + mapped physical contribution
Y   = verticalOriginY + verticalSample.worldOffsetY
```

The controller never copies arbitrary raw image Y directly into Unity world Y. The interpreter converts normalized measured action magnitude through Inspector-tunable scale, clamp and response settings.

On calibration invalidation, the vertical action state resets and the driven root is restored to the saved standing Y before the next calibration session establishes a new origin.

### Jump/depth isolation

The existing root tracker interprets support-image Y partly as camera-depth evidence. To avoid an in-place jump leaking into world Z, Batch 3 suppresses only that cross-talk at the controller/fusion boundary:

1. capture the pre-jump physical depth displacement when jump first acquires;
2. while jump/landing is active, pass a copy of the root sample to fusion with depth displacement held at that value and depth velocity set to zero;
3. preserve lateral physical displacement;
4. release the hold when jump ends.

`CameraSpaceRootTracker` itself is unchanged in Batch 3.

## Inspector defaults

`VerticalLocomotionSettings` defaults and the active Lab scene serialization are:

| Control | Default |
|---|---:|
| Minimum vertical joint confidence | `0.40` |
| Jump Detection Threshold | `0.12` |
| Jump Release Threshold | `0.045` |
| Jump Height / World Scale | `1.60` |
| Maximum Jump Height | `0.90` |
| Maximum Jump Foot Asymmetry | `0.08` |
| Crouch Enter Threshold | `0.18` |
| Crouch Release Threshold | `0.09` |
| Crouch Depth / World Scale | `1.20` |
| Maximum Crouch Depth | `0.65` |
| Vertical Response | `18/s` |
| Vertical Tracking Grace | `0.16 s` |
| Maximum apparent-scale change | `0.12` |
| Maximum jump coherence spread | `0.10` |
| Grounded support tolerance | `0.06` |

All detection thresholds are normalized/anatomical relationships, not opaque absolute camera pixels.

## Partial-body behavior

Vertical action interpretation requires both support sides plus pelvis/torso evidence. If those joints are unavailable, no vertical action is fabricated.

An already acquired action can hold through the short grace window. After grace expiry, state becomes unavailable and the vertical offset returns toward zero. Phase 3/4 pose reproduction and horizontal locomotion continue independently where their own evidence remains valid.

## Diagnostics

The existing Lab Phase-5 locomotion display is extended by `LocomotionPrototypeView` with a compact vertical section that obeys the existing locomotion-data/debug visibility conditions.

Displayed vertical data includes:

- state and jump phase;
- Live / grace-unavailable / unavailable;
- jump signal;
- foot asymmetry;
- apparent-scale delta;
- crouch compression;
- support rise;
- current Y offset and final root Y;
- standing-reference/root-Y-origin readiness.

No per-frame Console logging is used.

## Deterministic tests

Existing `Phase5LocomotionTests.cs` is left unchanged by Batch 3. It continues to cover Batch-2/2R swing isolation, lean suppression, alternating relocation, support continuity/loss, depth, cadence, fusion, heading and recenter.

New `VerticalLocomotionTests.cs` covers:

1. neutral standing/reference/zero Y;
2. one-leg lift rejection;
3. one-leg lateral+vertical rejection;
4. coherent both-feet+pelvis+chest jump acquisition;
5. positive jump root-Y;
6. landing/zero/no stuck jump;
7. a second jump after landing;
8. scale/depth-only rejection;
9. planted torso lean rejection;
10. grounded pelvis/support compression -> crouch;
11. held crouch -> stable negative root-Y;
12. crouch hysteresis/release;
13. jump/crouch mutual exclusion;
14. tracking loss/grace expiry -> safe unavailable/neutral;
15. in-place jump -> no meaningful root-tracker X/Z;
16. in-place crouch -> no meaningful root-tracker X/Z.

## Verification and acceptance

No Unity Editor/Test Runner is available in this Builder execution environment, so the new/retained Editor tests were **not executed here**. Static/source/diff review is useful but does not constitute a Unity test pass or USER acceptance.

The next acceptance action is one comprehensive USER Unity Motion Engine QA after Orchestrator review.

## Explicitly not part of Batch 3

Batch 3 does **not** add:

- CharacterController;
- Rigidbody gravity;
- collision/ground probing;
- autonomous jump animation/ballistics;
- course obstacles;
- gameplay animation state machine;
- Foundation C/D/E/coarse-hands work;
- new hand/body inference;
- OpenVINO/performance experiments;
- Phase 6 Hub/course/graybox work.

## Completion status

```text
Batch 1 — COMPLETE
Batch 2 — COMPLETE
Batch 2R — COMPLETE
Batch 3 — IMPLEMENTED / USER QA PENDING
Motion Engine V1 — NOT YET USER ACCEPTED
Phase 6 — NOT STARTED
```

Final current status:

`BATCH 3 IMPLEMENTED / USER QA PENDING / AWAITING ORCHESTRATOR REVIEW`
