# Golden Needle — Orchestrator Handoff

Handoff refresh: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## First actions

Before new work:

1. verify the live remote branch HEAD;
2. read `Docs/current-state.md`;
3. read `Docs/optimization-orchestrator-handoff.md` for frozen low-end/OpenVINO invariants;
4. read `Docs/decisions.md`;
5. read `Docs/motion-engine.md`;
6. inspect the Phase-5 reconstruction start->end diff and tests independently.

Do not merge to `main` without explicit USER approval. Do not force-push, rebase, amend, reset, or rewrite shared history.

## Reconstruction checkpoint

Authority-reconstruction handoff/baseline:

`08d7ea5785dd5935ed7240f004313b2a769d7a52`

Implementation + deterministic-test checkpoint:

`753ef564c253c48d6d2c71e9095d1e6e7878e2fe`

Status:

**PHASE-5 AUTHORITY RECONSTRUCTION IMPLEMENTED / USER QA PENDING**

Motion Engine V1 is **not USER accepted**. Phase 6 is **not started**.

## Why reconstruction was required

The immediate pre-reconstruction runtime had accumulated multiple root-position owners. USER runtime still showed constant locomotion-caused idle movement and visually weak body descent during crouch.

The Builder was required to inspect history instead of adding more thresholds. Important historical checkpoints:

- `87698948b12cd10b6fef2072d0ad0ce9eeaecdfe`: early continuous torso/body-root prototype;
- `33698719a2907d30bb3396f66e5b79e59ccbfe9e`: support-base corrective commit that fixed torso-lean false translation;
- `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0`: last known good pre-Foundation Phase-5 reference;
- `466d65826ab747493c211e1a3250aafa305167c8`: current pre-reconstruction Batch-3/stability architecture.

The reconstruction combines the useful properties rather than reverting to any one checkpoint wholesale.

## Preserved production invariants

The accepted low-end body pipeline remains untouched:

```text
WebCamTexture
-> WebCamCPU/GetPixels32
-> reusable 320x240 CPU prep
-> bounded newest-only two-slot scheduling
-> persistent OpenVINO CPU FP32 worker
-> MediaPipe pose semantics
-> CanonicalBodyV1
-> Phase 3 stabilization/calibration
-> Phase 4 positional/analytic-IK pose
-> Phase 5 root translation
```

Preserve:

- stock MediaPipe/TFLite and ExistingReadback as fallback/reference;
- latest useful frame wins; no backlog/catch-up architecture;
- Phase 3 stable canonical frame as locomotion/calibration authority;
- Phase 4 signed mapping and analytic two-bone IK as USER-accepted pose authority;
- Phase 5 as root translation only;
- no fabricated axial twist;
- no new inference for locomotion;
- no performance reopening without new evidence.

Foundations remain: A commands/speech retained, B cameras retained, C dormant, D hands deferred, E retired, coarse hands deferred/rolled back.

## Reconstructed horizontal authority

### Single owner

`CameraSpaceRootTracker` is the only stateful physical-position authority. It owns accepted displacement, filtering, recenter and reacquisition continuity.

`LocomotionFusion` is deliberately reduced to mapping/blending. It does **not** own another committed-position/stationary gate or measurement-rebase offset.

### Candidate vs validation

Torso center + yaw-compensated apparent scale produce a continuous body/root candidate. Bilateral ankle/heel/toe measurements validate whether that candidate represents actual room relocation.

Support mode remains `Both/Left/Right` with `0.12` single-enter / `0.06` dual-return hysteresis, but support mode is validation rather than an independent positional authority.

New commits require coherent dual support and matching body/support direction:

- torso lean without support relocation -> reject;
- scale-only apparent depth -> reject;
- raised/swinging single foot -> reject;
- coherent body + bilateral support relocation -> accept;
- slow coherent motion accumulates relative to last accepted state until it can commit;
- small idle fluctuations do not rewrite accepted position.

Tracking loss holds accepted physical displacement. Reacquisition rebases the incoming body/support observations onto that accepted displacement before new movement can commit.

### Cadence/recenter retained

Cadence code and active values are unchanged: `0.07` event threshold, 2-event acquisition, `0.38` acquire confidence, `0.60` distance per step, `3.0` max cadence speed, existing sustain/timeout/step-rate values.

Horizontal recenter remains X/Z-only and preserves virtual world position.

## Reconstructed vertical authority

`VerticalLocomotionInterpreter` remains the sole semantic Jump/Crouch state machine.

Negative root Y is now continuously driven from trustworthy normalized pelvis-to-support compression, independent of whether semantic `Crouch` has crossed its gameplay threshold.

Thus:

- shallow planted bend may remain `Standing` but lowers root Y;
- deeper bend follows the same continuous path and may acquire semantic `Crouch` at existing threshold;
- upright recovery returns smoothly toward zero;
- feet remain evidence/constraint rather than another root-Y position owner.

The post-Phase-4 `GroundedCrouchFootAnchor` solved-foot correction is removed from runtime root-Y authority.

Jump state/lifecycle is retained. Jump remains coherent whole-body rise and owns positive Y exclusively while active. The existing pre-jump depth hold at the controller/fusion boundary remains to prevent takeoff from leaking into world Z.

## Runtime controller

`EmbodiedLocomotionController` remains execution order 150 after Phase 4.

Runtime root target is now straightforward again:

```text
X/Z = virtual origin + fusion(mapped authoritative root + cadence)
Y   = calibration-session vertical origin + verticalSample.worldOffsetY
```

There is no second crouch-foot Y owner in the controller.

## Tests

`Phase5LocomotionTests.cs` was rewritten to test behavior rather than superseded implementation details. It covers idle stability, lean/scale/swing rejection, coherent room movement, alternating steps, transition continuity, slow movement, loss/reacquisition, recenter, physical/cadence blending, depth, axis semantics and jogging-in-place.

`VerticalLocomotionTests.cs` covers continuous shallow/deep compression, semantic crouch separation, planted crouch X/Z isolation, recovery, jump ownership, rejection cases, reset behavior and in-place jump isolation.

`LocomotionGroundingStabilityTests.cs` is removed because its primary assertions encoded the now-removed downstream fusion gate and solved-foot Y owner.

No Unity Editor/Test Runner was available to the Builder. Do not report a Unity pass unless actual runner evidence exists.

## Diagnostics

The compact F9 locomotion overlay identifies:

- body candidate;
- support mode/validation and support evidence;
- authoritative accepted physical displacement/velocity and commit state;
- vertical semantic state/jump phase;
- grounded compression activity and Y output.

No per-frame Console logging is added.

## Immediate next action

After verifying the final remote HEAD and diff, the Orchestrator should prepare **one genuine USER Unity webcam QA** focused on the reconstructed Motion Engine:

- standing still at origin and after physical relocation;
- deliberate left/right and forward/back walking;
- planted torso lean/sway;
- single swing leg and jogging in place;
- cadence acquire/stop/coexistence;
- tracking loss/reacquisition;
- recenter;
- shallow bend before semantic crouch;
- deep crouch/body descent/recovery;
- jump, landing and repeat jump;
- Phase-4 pose coexistence and performance sanity.

If USER QA exposes a reproducible defect, fix only that defect and preserve the single-authority architecture. If QA passes, Motion Engine V1 may then be marked USER accepted and Phase 6 can be planned separately.

Do not start Phase 6, logging/performance investigation, hands/foundations work, or a `main` merge as part of this handoff.
