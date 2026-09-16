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

Authority-reconstruction baseline/handoff:

`08d7ea5785dd5935ed7240f004313b2a769d7a52`

Published reconstruction implementation/tests/diagnostics/docs checkpoint:

`dc48e9d2f18c65b1631fbdb390e60578087a1303`

Status:

**PHASE-5 AUTHORITY RECONSTRUCTION IMPLEMENTED / USER QA PENDING**

Motion Engine V1 is **not USER accepted**. Phase 6 is **not started**.

## Why reconstruction was required

The immediate pre-reconstruction runtime had multiple root-position owners and USER runtime still showed locomotion-caused idle movement plus insufficient body descent during crouch. The Builder was required to inspect historical checkpoints before editing:

- `87698948b12cd10b6fef2072d0ad0ce9eeaecdfe` — early torso/body-root prototype;
- `33698719a2907d30bb3396f66e5b79e59ccbfe9e` — support-base corrective commit;
- `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0` — last known good pre-Foundation Phase-5 reference;
- `466d65826ab747493c211e1a3250aafa305167c8` — immediate pre-reconstruction runtime.

The resulting architecture combines the useful lessons rather than reverting wholesale.

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
-> Phase 4 positional/analytic-IK pose
-> Phase 5 root translation
```

Preserve stock MediaPipe/TFLite and ExistingReadback as fallback/reference, latest-useful-frame-wins scheduling, Phase 3 stable canonical data authority, Phase 4 signed mapping/IK authority, and Phase 5 root-only translation. Do not reopen performance without new reproducible evidence.

Foundation A speech/commands and Foundation B cameras remain retained. Foundation C is dormant, D hands deferred, E retired, coarse hands deferred/rolled back.

## Reconstructed horizontal authority

`CameraSpaceRootTracker` is the only stateful physical-position owner. It owns accepted displacement, filtering/velocity, recenter and reacquisition continuity.

Torso center + yaw-compensated apparent scale provide a continuous body/root candidate. Weighted ankle/heel/toe support evidence validates whether that candidate is real room relocation.

Support mode remains `Both/Left/Right` with `0.12` single-enter / `0.06` dual-return hysteresis, but it is validation rather than independent positional authority.

Expected behavior:

- torso lean without support relocation -> reject;
- scale-only apparent depth -> reject;
- raised/swinging single foot -> reject;
- coherent body + bilateral support relocation -> accept;
- slow coherent motion accumulates relative last accepted state until commit;
- idle noise does not continuously retarget accepted root;
- tracking loss holds position and reacquisition rebases before further motion.

`LocomotionFusion` is mapping/blending only. The later downstream lateral/depth enter/continue commit gate and its measurement-rebase state are removed.

Cadence code and active values remain unchanged: `0.07` event threshold, 2-event acquisition, `0.38` acquire, `0.25` sustain, `0.50 s` stop, `0.60` distance per step, `3.0` max speed.

Horizontal recenter remains X/Z-only and preserves avatar world position.

## Reconstructed vertical authority

`VerticalLocomotionInterpreter` remains the sole semantic Jump/Crouch state machine.

Negative root Y is primarily driven from trustworthy normalized pelvis-to-support compression. A shallow planted bend can remain semantically `Standing` while lowering root Y; deeper bend follows the same continuous path and may cross semantic Crouch at the retained `0.18` enter threshold. Release remains `0.09`.

The post-Phase-4 solved-foot `GroundedCrouchFootAnchor` is removed as root-Y authority. Feet remain evidence/constraint.

Jump lifecycle/thresholds remain intact. Jump stays coherent whole-body rise and exclusively owns positive Y while active. Existing pre-jump depth hold at the controller/fusion boundary remains to prevent jump image-Y from becoming world Z.

The controller again applies:

```text
X/Z = virtual origin + fusion(mapped authoritative physical root + cadence)
Y   = vertical origin + verticalSample.worldOffsetY
```

## Tests

`Phase5LocomotionTests.cs` now verifies neutral/nonzero idle stability, lean/scale/swing rejection, coherent room movement, alternating-step completion, transition continuity, slow movement, loss/reacquisition, recenter, physical/cadence blending, depth, axis semantics and jogging-in-place.

`VerticalLocomotionTests.cs` verifies continuous shallow/deep body compression, semantic Crouch separation, planted crouch X/Z isolation, recovery, jump ownership/landing, rejection cases, reset behavior and in-place jump isolation.

`LocomotionGroundingStabilityTests.cs` is removed because it encoded the superseded downstream fusion gate and solved-foot Y authority.

No Unity Editor/Test Runner was available to the Builder. Do not report a Unity pass without actual runner evidence.

## Diagnostics

The compact F9 locomotion overlay now identifies body candidate, support mode/validation, support evidence, accepted physical displacement/velocity and per-frame commit state, plus vertical semantic state, grounded-compression activity and Y output. No per-frame Console logging is added.

## Immediate next action

After verifying the final remote HEAD and diff, run **one genuine USER Unity webcam QA** focused on:

- standing still at origin and after relocation;
- deliberate left/right and forward/back walking;
- planted torso lean/sway;
- swing leg and jogging in place;
- cadence acquire/stop/coexistence;
- tracking loss/reacquisition;
- recenter;
- shallow bend before semantic Crouch;
- deep crouch/body descent/recovery;
- jump, landing and repeat jump;
- Phase-4 pose coexistence and performance sanity.

If QA exposes a reproducible defect, fix only that defect while preserving the single-authority architecture. If QA passes, Motion Engine V1 may then be marked USER accepted and Phase 6 planned separately.

Do not start Phase 6, logging/performance investigation, hands/foundations work, or a `main` merge as part of this handoff.
