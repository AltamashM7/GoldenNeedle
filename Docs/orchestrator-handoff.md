# Golden Needle — Orchestrator Handoff

Handoff refresh: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## 1. First actions for the next Orchestrator

Before changing anything:

1. verify the live remote `engine/pose-tracking-spike` HEAD;
2. read `Docs/current-state.md` as the primary current authority;
3. read `Docs/optimization-orchestrator-handoff.md` for preserved low-end/OpenVINO invariants;
4. read `Docs/decisions.md` for current/superseded architectural decisions;
5. read `Docs/motion-engine.md` for Motion Engine V1 behavior/defaults;
6. inspect the current source and full Batch-3 start->end diff;
7. independently verify Builder/repository claims rather than accepting this summary at face value.

Do **not** merge to `main` without explicit USER approval. Do not force-push, rebase, amend, reset, or rewrite published/shared history.

## 2. Batch-3 checkpoint

Batch 3 started from the independently verified remote HEAD:

`e6044b45d94dbe4ee9da3702828c1ee73409d475`

Implementation + deterministic-test checkpoint:

`220a4b958c8a3d280799ea3c0836fa6536a486a0`

Finalization adds only Lab vertical diagnostics/scene serialization and current documentation on top of that implementation checkpoint.

Current required status vocabulary:

`BATCH 3 IMPLEMENTED / USER QA PENDING / AWAITING ORCHESTRATOR REVIEW`

Motion Engine V1 is **not USER accepted yet**. Final comprehensive USER Unity QA comes only after this Orchestrator review.

## 3. Preserved production baseline

The accepted low-end body path remains:

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

Preserve these invariants:

- stock MediaPipe/TFLite remains fallback/reference;
- ExistingReadback remains fallback/reference;
- latest useful frame wins; no backlog/catch-up architecture;
- stable Phase 3 canonical data remains calibration and locomotion authority;
- Phase 4 signed mapping, positional targets and analytic two-bone IK remain production pose authority;
- Phase 5 moves the avatar root after Phase 4; it does not become bone/IK authority;
- no fabricated free axial/twist DOFs;
- no new inference was introduced for jump/crouch.

Batch 3 did not edit OpenVINO/provider code, Phase 3 filtering/calibration code, Phase 4 retarget/IK code, speech, camera presets, hands, packages or project settings.

## 4. Current status

- Phase 1 — provider/raw pose: **PASS WITH NOTES**.
- Phase 2 — canonical skeleton: **PASS**.
- Phase 3 — stabilization/confidence/calibration: **PASS**.
- Phase 4 — humanoid retargeting: **USER ACCEPTED — PASS**.
- Low-end optimization milestone: **USER SATISFIED / FROZEN FOR CURRENT MILESTONE**.
- Batch 1 — **COMPLETE**.
- Batch 2 — **COMPLETE**.
- Batch 2R — **COMPLETE**.
- Batch 3 — **IMPLEMENTED / USER QA PENDING**.
- Motion Engine V1 — **NOT YET USER ACCEPTED**.
- Phase 6 — **NOT STARTED**.

## 5. Retained/deferred foundations

- Foundation A commands/speech remains retained and working.
- Foundation B camera presets remains retained.
- Foundation C remains deferred/dormant research.
- Foundation D detailed hands remains deferred due low-end cost.
- Foundation E remains retired from production.
- Coarse hands remain deferred/rolled back.

Do not reopen them during Motion Engine acceptance work unless the USER explicitly changes scope.

## 6. Batch 2 + Batch 2R horizontal invariants

Batch 3 intentionally leaves horizontal locomotion source unchanged.

`CameraSpaceRootTracker` still uses trusted ankle/heel/toe support measurements and stateful `Both/Left/Right` authority. Current normalized thresholds remain:

- `supportSingleFootEnter = 0.12`;
- `supportBothEnter = 0.06`.

Raised swing-foot motion alone must not translate the root. Landing/support switches must not teleport. Batch 2R additionally requires coherent dual-support relocation after an alternating step sequence to become real physical displacement rather than remaining cancelled by a permanent transition offset.

Tracking-loss reacquisition remains continuity-rebased and safe.

Cadence remains unchanged from Batch 2: 2-event acquisition, `0.38` acquire confidence, `0.60` distance per step, `3.0` maximum cadence speed, `0.07` event threshold and existing stop/sustain/step-rate behavior.

Existing Phase-5 horizontal tests were **not modified** in Batch 3.

## 7. Batch-3 vertical architecture

### Dedicated module

New core file:

`Assets/GoldenNeedle/Core/Motion/Locomotion/VerticalLocomotionInterpreter.cs`

It contains:

- `VerticalLocomotionSettings`;
- `VerticalLocomotionState` (`Unavailable`, `Standing`, `Jump`, `Crouch`);
- `VerticalJumpPhase` (`Grounded`, `Takeoff`, `Airborne`, `Landing`);
- `VerticalLocomotionSample` diagnostics/output;
- `VerticalLocomotionInterpreter` state/reference logic.

Input is only `runtime.StabilizedFrame` plus the accepted `MotionCalibrationProfile` and delta time.

### Standing reference

A runtime vertical reference is captured only when:

- body reference/calibration is valid;
- both left/right support-foot groups are usable;
- pelvis/chest/hips/knees are tracked and confident;
- support feet are reasonably symmetric;
- leg/torso/knee geometry is usable as a standing-like starting footprint.

The reference is not continuously updated while actions occur. Calibration/body-reference loss resets it. Ordinary horizontal `Recenter()` deliberately does not redefine it.

### Jump evidence and lifecycle

Jump is coherent whole-body rise, not foot lift. Acquisition requires:

- both weighted support feet rise together;
- left/right normalized rise remains within `maximumJumpFootAsymmetry`;
- pelvis and chest rise with the feet;
- total four-signal spread remains below `maximumJumpCoherenceSpread`;
- apparent-scale change remains below `maximumApparentScaleChange`.

Lifecycle is `Grounded -> Takeoff -> Airborne -> Landing -> Grounded` using enter/release hysteresis. Temporary required-joint loss holds an acquired action for the tracking grace; expiry returns toward neutral and publishes unavailable.

### Crouch evidence and lifecycle

Crouch uses normalized loss of pelvis-to-support height relative to standing reference. Acquisition requires:

- support base remains approximately grounded;
- feet remain coherent;
- pelvis/chest move downward;
- compression exceeds crouch-enter threshold;
- apparent scale remains stable.

Crouch holds while compression stays above the lower release threshold. Rise out of crouch returns to `Standing` before another action can acquire, preventing recovery motion from being misclassified as jump.

Jump and crouch are mutually exclusive because one shared state machine owns vertical state.

## 8. Root-Y application and lifecycle

`EmbodiedLocomotionController` remains `[DefaultExecutionOrder(150)]`, after Phase 4. It now owns X/Y/Z root translation while still leaving root rotation to existing systems.

For each valid calibration session it captures the current bound player root Y as `_verticalOriginY`. Runtime output is:

```text
desiredY = verticalOriginY + verticalSample.worldOffsetY
```

Jump yields positive proportional/clamped Y; crouch yields negative proportional/clamped Y. The same response filter returns to zero/standing smoothly.

On calibration invalidation after a valid session, the vertical interpreter resets and, when locomotion drive is active, the controller restores the root to the stored standing Y before waiting for the next calibration session. Missing vertical evidence does not stop horizontal locomotion or Phase 3/4 pose systems.

Horizontal `Recenter()` remains X/Z-only and preserves the separate vertical reference/origin.

No gravity, Rigidbody, CharacterController, collision probing, animation state machine or Phase-6 course code was added.

## 9. Jump/depth cross-talk handling

The accepted camera-space root tracker uses support image Y as one input to camera-depth displacement. A real in-place jump can therefore present a transient depth-like support rise.

Batch 3 handles this at the Phase-5 controller/fusion boundary instead of rewriting the tracker:

- on first acquired jump, store pre-jump physical depth displacement;
- while vertical jump/landing state is active, pass a copy of `CameraSpaceRootSample` to fusion with depth displacement held at that pre-jump value and depth velocity set to zero;
- leave lateral physical displacement untouched;
- release the hold when vertical jump state ends.

This is intentionally narrow. The Batch-2R root tracker is unchanged.

## 10. Inspector/defaults

Serialized Batch-3 Lab defaults:

- minimum vertical joint confidence `0.40`;
- Jump Detection Threshold `0.12`;
- Jump Release Threshold `0.045`;
- Jump Height / World Scale `1.60`;
- Maximum Jump Height `0.90`;
- Maximum Jump Foot Asymmetry `0.08`;
- Crouch Enter Threshold `0.18`;
- Crouch Release Threshold `0.09`;
- Crouch Depth / World Scale `1.20`;
- Maximum Crouch Depth `0.65`;
- Vertical Response `18/s`;
- Vertical Tracking Grace `0.16 s`;
- Maximum apparent-scale change `0.12`;
- Maximum jump coherence spread `0.10`;
- Grounded support tolerance `0.06`.

Thresholds are normalized/anatomical relationships, not raw-pixel constants.

## 11. Diagnostics

`LocomotionPrototypeView` extends the existing Phase-5 Lab locomotion display with a low-cost compact vertical block. It obeys the existing locomotion-data visibility and debug-presentation/F3/F6/F12 conditions and reports:

- vertical state + jump phase;
- Live / grace-unavailable / unavailable;
- jump signal, foot asymmetry, apparent-scale delta;
- crouch compression and support rise;
- root-Y offset/final Y;
- standing-reference/root-Y-origin readiness.

No per-frame Console logging was added.

## 12. Deterministic coverage

New file:

`Assets/GoldenNeedle/Tests/Editor/VerticalLocomotionTests.cs`

It covers:

- neutral standing/reference/zero offset;
- single-leg lift rejection;
- single-leg lateral+vertical rejection;
- coherent whole-body jump acquisition;
- positive jump Y;
- landing and return to zero;
- second jump after landing;
- scale/depth-only rejection;
- planted torso lean rejection;
- crouch acquisition from grounded compression;
- held crouch negative offset;
- crouch hysteresis/release;
- jump/crouch mutual exclusion;
- bounded tracking-loss grace and safe action exit;
- in-place jump horizontal-root isolation;
- in-place crouch horizontal-root isolation.

The existing `Phase5LocomotionTests.cs` remains unchanged and still contains Batch-2/2R swing-foot, alternating-step, lean, loss/reacquisition, depth, cadence, fusion, heading and recenter coverage.

## 13. Verification limitation

No Unity Editor/Test Runner is available in this Builder execution environment. The new NUnit Editor tests therefore have **not been executed here**. Static/source reasoning, commit scope, scene serialization and start->end diff should be independently reviewed. Check final GitHub workflow/status evidence before reporting; absence of CI is not a pass.

USER runtime testing was intentionally not performed during Builder implementation. The next USER test is the comprehensive Motion Engine QA after Orchestrator review.

## 14. Immediate next action

**STOP after Batch 3.**

The next Orchestrator should:

1. verify final remote HEAD;
2. audit the start `e6044...` -> final diff, especially scene YAML and horizontal/Phase-3/Phase-4 isolation;
3. inspect the vertical state/reference logic and deterministic tests;
4. verify CI/test evidence accurately;
5. if the implementation is accepted for runtime QA, prepare one comprehensive USER Unity Motion Engine QA pass covering horizontal locomotion, cadence, jump, crouch, tracking loss, recenter and Phase-4 coexistence.

Do **not** start Phase 6, merge `main`, resume hands/foundations, or reopen performance optimization as part of this handoff.
