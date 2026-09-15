# Golden Needle — Current State

Authoritative current-state refresh: 2026-09-15.

Latest fully cleared foundation code-audit checkpoint remains:
`864b39a520c78db1a0572b87a7d42c9da5cedaa4` — Foundation D corrective implementation passed independent Orchestrator re-audit with the bundled official Hand Landmarker preserved, shared body/hand provider timeline verified, protected body/OpenVINO and Phase 4 paths intact, and permanent Foundation C/D verification green.

Latest Foundation E Builder verification checkpoint:
`c7b84f33c5ca5329d387d279762a5f82600b5049` — the narrow runtime-wiring correction is implemented and Builder automated/code verification is green. `PoseTrackingSpikePresenter.Awake()` now ensures a `RichHumanoidDetailRetargeter` exists before its retargeter composition block, while `HumanoidRetargeter` remains generic and re-discovers `IHumanoidPostSolveDetailLayer` components in `Start()`. Foundation E now requires independent Orchestrator re-audit before comprehensive USER A–E runtime QA.

Verification evidence retained for this gate:
- Foundation C corrective workflow at `9e4c4ac3a9d87eade06a43e2833481cce22b70f9`: run `34919859131`, job `104225315113`: **PASS**.
- Foundation D accepted exact-head workflow at `864b39a520c78db1a0572b87a7d42c9da5cedaa4`: run `34920144300`, job `104226227007`: **PASS**.
- Foundation D migrated permanent-boundary workflow at `44a39d5ef566b800587a3be73345e843fffe5ac0`: run `34930150745`, job `104256492165`: **PASS**.
- Original Foundation E exact-final-head workflow at `f2e3041ee52e8e13d7b30c692b52c2b956d2664c`: run `34930482322`, job `104257473527`: **PASS**; Foundation D at that same SHA: run `34930482323`, job `104257473801`: **PASS**.
- Foundation E palm-correction workflow at `5ed50f876c4e887137f2d44020c65fb5efb4a015`: run `34933034414`, job `104265052576`: **PASS**.
- Foundation D palm-correction compatibility workflow at the same SHA: run `34933034400`, job `104265052498`: **PASS**.
- Final Foundation E code/math verification at `087068dd14cd4bae11243a5efbd1bd5ee4cd309e`: run `34933354209`, job `104265988449`: **PASS**.
- Final Foundation D compatibility workflow at that same SHA: run `34933354189`, job `104265988337`: **PASS**.
- Foundation E runtime-wiring Builder verification at `c7b84f33c5ca5329d387d279762a5f82600b5049`: run `34935548314`, job `104272530207`: **PASS**, including `FOUNDATION_E_LIVE_SPIKE_COMPOSITION=PASS` and `FOUNDATION_E_POST_SOLVE_DISCOVERY_TIMING=PASS`.

Working branch: `engine/pose-tracking-spike`.

This document records what is true **now**. Historical experiment details remain in the dedicated progress/audit documents and should not override this file when they describe an earlier pending gate that has since been completed.

## Governance and branch policy

- Repository: `AltamashM7/GoldenNeedle`.
- Active Motion Engine development branch: `engine/pose-tracking-spike`.
- Do **not** merge to `main` without explicit USER approval.
- Do not force-push or rewrite shared branch history merely to clean checkpoint/experimental commits.
- GitHub Desktop is the USER's normal Git workflow.
- USER-local Unity/solution/settings changes may exist independently of the remote repository; do not clean, revert, stage, or overwrite them casually.
- Core engine work remains on the long-lived phase branch until the USER explicitly approves a later merge strategy.

## Phase status

- Phase 1 — MediaPipe provider/raw overlays: **PASS WITH NOTES**.
- Phase 2 — canonical skeleton/debug: **PASS**.
- Phase 3 — stabilization/confidence: **PASS**.
- Phase 4 — humanoid retargeting/calibration/orientation: **USER ACCEPTED — PASS**.
- Motion Engine latency/performance optimization milestone: **CURRENT MILESTONE COMPLETE; FURTHER TUNING DEFERRED**.
- Foundation A — Unified Command System + modular speech input: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA DEFERRED**.
- Foundation B — Camera View / Focus Preset System: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA DEFERRED**.
- Foundation C — Rich Canonical Motion / Orientation Architecture: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA DEFERRED**.
- Foundation D — MediaPipe Hand Landmarker integration: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA DEFERRED**.
- Foundation E — Orientation-aware + optional hand/finger retarget: **IMPLEMENTED / BUILDER AUTOMATED & CODE VERIFICATION COMPLETE / ORCHESTRATOR RE-AUDIT PENDING / USER MANUAL QA DEFERRED**.
- Phase 5A — support-foot locomotion / Lab-Game presentation: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.

The project is **not** blocked on further motion-engine latency optimization. The current engine is strong enough to continue normal development. Additional smoothing/performance tuning remains intentionally available later.

The USER has chosen to continue Foundations A–E sequentially with focused implementation/code/automated verification, then perform one comprehensive manual Unity/runtime foundation QA pass after all five are built. This sequencing decision does **not** auto-accept any foundation, does not remove the deferred speech/camera/rich-orientation/hand/detail QA requirements, and does not make visual/microphone behavior verified.

## Approved pre-Phase-5A foundation track

The development sequence remains:

```text
A — Unified Command System + modular speech input       IMPLEMENTED / Orchestrator audited / manual QA deferred
    ↓
B — Camera View / Focus Preset System                  IMPLEMENTED / Orchestrator audited / manual QA deferred
    ↓
C — Rich canonical motion/orientation architecture     IMPLEMENTED / Orchestrator audited / manual QA deferred
    ↓
D — MediaPipe hand-landmark integration                IMPLEMENTED / Orchestrator audited / manual QA deferred
    ↓
E — Orientation-aware + optional hand/finger retarget  IMPLEMENTED / Builder verified / Orchestrator re-audit pending / manual QA deferred
    ↓
Independent Foundation E Orchestrator re-audit
    ↓
Comprehensive A–E manual/runtime QA
    ↓
Return to Phase 5A locomotion acceptance/fixes
```

These are foundation tasks, not a replacement for Phase 5A. Phase 5A remains implemented but unaccepted while the prerequisite work is completed.

### Foundation A — commands and speech

Status: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA DEFERRED**.

Current implementation:

- one project-owned `GoldenNeedleCommandRouter` shared by keyboard, speech, and future UI callers;
- existing R/V/F1–F12/C/X/K keyboard meanings route through the command system rather than duplicating runtime behavior;
- speech remains behind replaceable `ISpeechInputProvider`;
- current Windows prototype uses platform-guarded Unity `KeywordRecognizer` without keyboard simulation;
- phrase mappings, command parameters, confidence thresholds, cooldown, enablement, and optional wake prefix remain configurable;
- calibration/reset/recenter/retry/Lab-Game presentation/capture-camera commands reuse their established runtime authorities;
- Foundation A CI is permanently read-only: `contents: read`, no migration execution, no commit/push behavior, no frozen pre-Foundation-A diff gate.

Deferred USER QA still includes local Unity compilation, real microphone recognition, wake-prefix behavior, cooldown behavior, and keyboard/runtime equivalence. Do not mark speech USER-verified until that pass occurs.

### Foundation B — camera presets

Status: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA DEFERRED**.

Current implementation:

- the existing `ThirdPersonLabCamera` remains the single gameplay/presentation Camera authority;
- no extra simultaneously rendering gameplay cameras and no `PoseTrackingSpike.unity` YAML migration were added;
- presets are serialized/data-driven and Inspector-editable;
- initial presets are `Back`, `Front`, `Left`, `Right`, `FullBody`, `Hands`, `LeftHand`, and `RightHand`;
- Back is seeded from the existing third-person compatibility values (`4.0` distance, `2.2` height, `1.15` look height, response `7.0`, FOV `55`) so existing Game View behavior remains the baseline;
- placement is relative to the retained valid world/body heading; temporary heading loss retains the prior valid heading and no hard-coded global direction is invented before heading exists;
- focus semantics support avatar/body, both hands, left hand, and right hand with safe hand→lower-arm/body/root fallbacks;
- preset changes use independent exponential position/orientation/FOV response values;
- `GoldenNeedleCommand.SelectCameraViewPreset` is live and parameterized; command routing contains no camera-transform math;
- default speech mappings include `back/front/left/right/full body/hands/left hand/right hand view` while `game view` and `lab view` keep their existing meanings;
- selecting a preset does **not** toggle Lab/Game mode; the selection is retained and applies when Game View is active;
- F12 remains `ToggleLabGamePresentation -> ThirdPersonLabCamera.ToggleGameView()` and Lab still uses the persistent clear-only, culling-mask-zero Camera;
- the Orchestrator corrective audit found and fixed the missing `GoldenNeedle.Core.Motion.Rotation` import required by `CanonicalBoneId` references, and the permanent Foundation B audit guards that dependency.

Builder verification and the independent Orchestrator code audit are complete. Unity Editor compilation, actual camera visuals/transitions, and speech-driven preset selection remain part of the deferred comprehensive USER QA pass.

### Foundation C — rich canonical orientation

Status: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA DEFERRED**.

Foundation C is additive. The accepted 20-joint `CanonicalPoseFrame` / `CanonicalJointId` compatibility contract remains exactly 20 joints with the existing numeric meanings, coordinate semantics, stabilization/calibration consumers and Phase 4 production retarget path unchanged.

Concrete implementation:

- `RichMotionSchema` defines project-owned schema identity `GoldenNeedle.RichMotion`, version `1`;
- `RichMotionEvidenceFrame` and `RichMotionFrame` provide fixed/preallocated provider-independent evidence/orientation contracts;
- `CanonicalAnatomicalBasis` remains the rich orientation authority: explicit primary/secondary/third axes, determinant, handedness and validity; no canonical Quaternion authority was introduced;
- `IRichMotionEvidenceSource` remains optional and additive; legacy `ICanonicalPoseSource` was not expanded;
- MediaPipe rich evidence reuses the existing persistent 33-landmark observation and does not add a second inference, observation copy, queue or scheduling path;
- numeric MediaPipe indices remain confined to provider/mapping code;
- supported rich orientation channels remain pelvis, chest, bilateral upper/lower arms, bilateral upper/lower legs and bilateral feet;
- twist states remain `Observed`, `Held`, `ReferenceFallback` and `Unobservable` with separate swing/twist observability;
- rich temporal/reference state remains separate from `MotionCalibrationProfile`;
- `MotionEngineRuntime` exposes rich data read-only after accepted legacy outputs; Foundation C itself does not apply rich orientation to the avatar.

Corrective Foundation C workflow run `34919859131`, job `104225315113`, passed at `9e4c4ac3a9d87eade06a43e2833481cce22b70f9`. Actual Unity Editor compilation/Test Runner execution remains deferred.

### Foundation D — MediaPipe hands

Status: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA DEFERRED**.

Foundation D preserves the optimized body provider and adds a separate optional MediaPipe Hand Landmarker stream rather than migrating body tracking to Holistic.

Current implementation:

- `CanonicalHandFrame` contains exactly 21 stable semantic landmarks per hand plus side/freshness/timing, palm basis and conservative articulation features;
- the existing `MediaPipePoseProvider` Stopwatch remains the shared semantic/fusion timeline; Unity unscaled time is cadence-only;
- left/right hands are independently fresh with the existing maximum-age/body-skew policy;
- Hand Landmarker remains CPU `LIVE_STREAM`, `numHands=2`, default `12 Hz`, separate `480x360` reusable preparation;
- one active hand inference plus at most one replaceable newest pending snapshot is allowed; there is no FIFO/history/replay/catch-up queue;
- the official bundled model identity remains locked and unchanged;
- Foundation D remains a data producer only and does not drive avatar transforms.

The accepted D audit remains `34920144300` / `104226227007` at `864b39a5...`. The E-compatible permanent D boundary also remains green at `087068dd14cd4bae11243a5efbd1bd5ee4cd309e`: run `34933354189`, job `104265988337`, **SUCCESS**. A new exact-final-head D compatibility run is required after the present documentation checkpoint and is reported separately in the Builder handoff rather than self-referentially editing this file again.

Unity Editor NUnit source exists, but actual Unity Editor compilation/Test Runner and real webcam hand inference were not performed by this Builder environment. USER runtime QA remains intentionally deferred.

### Foundation E — optional-bone retargeting

Status: **IMPLEMENTED / BUILDER AUTOMATED & CODE VERIFICATION COMPLETE / ORCHESTRATOR RE-AUDIT PENDING / USER MANUAL QA DEFERRED**.

Foundation E remains an additive post-Phase-4 detail/application layer. The accepted Phase 4 `HumanoidRetargeter.ApplyMotionFrame(...)` solve remains byte-for-byte protected by the E workflow and remains the positional/IK authority.

Current implementation and preserved invariants:

- `IHumanoidPostSolveDetailLayer` is the generic provider-independent optional hook; `HumanoidRetargeter` contains no E-implementation, rich/hand/provider/finger-specific construction or application logic;
- execution, when a detail layer exists, is exact Phase 4 solve → optional E refinement → combined solved target → existing presentation smoothing;
- `HumanoidRigBinding.IsBound` and `BoundBoneCount` remain required-body semantics only; optional detail capability remains separate;
- production rich channels remain exactly bilateral upper/lower arms and upper/lower legs; pelvis/chest/feet remain deferred;
- rich axial twist uses the signed canonical-to-avatar map and downstream compensation, with deterministic endpoint residual `0` under the `<1e-6` guard;
- Foundation E owns no inference, camera acquisition, provider scheduling, frame history or backlog.

Palm-reference correction:

- the previous palm path was cumulative because it characterized the target palm from live, already E-rotated hand/finger geometry and premultiplied the complete source-relative delta onto the current hand rotation every frame;
- `HumanoidRigBinding` now caches each chain tip's bind/reference local rotation in addition to its existing position/scale measurements; this cache does not participate in `IsBound` or make optional fingers mandatory;
- each hand keeps a stable target palm zero as three axes in the hand-parent's local coordinate frame, derived from actual index/middle/little proximal avatar geometry;
- the runtime math produces one absolute desired hand local rotation from the parent-relative target reference plus cached chain-tip baseline, so repeated identical source palms converge rather than accumulate;
- stale/category/master/reset behavior returns/removes the E-owned palm contribution and reacquisition establishes a fresh zero-delta reference.

Runtime-wiring correction:

- `PoseTrackingSpike.unity` remains unchanged and still does not need a serialized E component;
- `PoseTrackingSpikePresenter.Awake()` now checks for `RichHumanoidDetailRetargeter` and adds it only when absent, using the same code-owned spike composition path that already ensures runtime, binding, retargeter, locomotion and related components;
- the E detail component is ensured before the presenter's retargeter ensure block, so newly composed retargeters can discover it immediately;
- the serialized/existing `HumanoidRetargeter` remains generic and E-agnostic. Its existing `Start()` re-runs `ResolvePostSolveDetailLayers()`, guaranteeing discovery after Awake-time composition even if its own `Awake()` happened earlier;
- no Foundation E motion/palm/finger math, scene YAML, provider, OpenVINO, Foundation D scheduler/model, Phase 5A, ProjectSettings or package code was changed by the runtime-wiring correction.

Automated/code evidence:

- Foundation E code/math run `34933354209`, job `104265988449` at `087068dd14cd4bae11243a5efbd1bd5ee4cd309e`: **SUCCESS**;
- Foundation D compatibility run `34933354189`, job `104265988337` at the same SHA: **SUCCESS**;
- Foundation E deterministic smoke: **PASS**, 24 checks;
- repeated palm-target drift `0°`, stale residual `0°`, parent-relative local drift `0°`, endpoint residual `0`, reacquisition bounded step `2.68084192°`;
- runtime-wiring Builder verification at `c7b84f33c5ca5329d387d279762a5f82600b5049`: run `34935548314`, job `104272530207`: **SUCCESS**;
- that runtime-wiring run passed `FOUNDATION_E_LIVE_SPIKE_COMPOSITION`, `FOUNDATION_E_POST_SOLVE_DISCOVERY_TIMING`, exact Phase 4 method comparison, eight-rich-channel guard, optional binding semantics, provider/inference/history isolation, strict scope allowlist, D production boundary, palm guards and the read-only dirty-tree guard.

Actual Unity Editor compilation/Test Runner execution and USER webcam/avatar/manual runtime QA were **not** performed by this Builder environment. Foundation E is not USER accepted and has not yet cleared the independent Orchestrator re-audit.

## Current best-tested runtime path

The strongest same-machine USER-tested body configuration remains:

```text
Unity WebCamTexture
  -> WebCamCPU/GetPixels32 reusable acquisition
  -> reusable CPU resize / H-V preparation to 320x240 RGBA
  -> bounded latest-frame OpenVINO mailbox
  -> one persistent OpenVINO CPU FP32 worker
  -> MediaPipe 0.10.22 pose graph semantics
  -> 33 normalized + world pose landmarks
  -> Golden Needle provider observation
  -> canonical body mapping
  -> selectable avatar-drive filtering
  -> calibration / Phase 4 retarget
  -> code-owned Foundation E post-solve detail component
  -> avatar presentation
```

Foundation D remains an optional secondary stream alongside that path; it does not replace or redefine the body pipeline. Foundation E consumes already-produced rich/hand contracts and adds retarget math only.

Important policy:

- `OpenVinoCpuFp32` is the best-tested low-end backend configuration, not a reason to delete the stock MediaPipe/TFLite path.
- Stock MediaPipe/TFLite CPU remains the safe fallback/reference backend.
- `WebCamCpuPixels` is the best-tested camera acquisition path for OpenVINO on the USER machine.
- `ExistingReadback` remains available as fallback/reference.
- Do not silently change serialized/default backend or acquisition policy merely because a faster tested path exists.

## Current low-end proof machine

USER hardware used for the main performance evidence remains Windows 10 x64, Intel Core i3-7100U, Intel HD Graphics 620, no dedicated GPU, with compute-shader support. This is intentionally a low-end proof target.

## OpenVINO integration status

OpenVINO 2026.3.0 CPU FP32 remains additive and uses the exact detector/landmark models extracted from the production task bundle. The accepted persistent-worker/two-slot latest-frame scheduling and WebCamCPU/GetPixels32 acquisition optimizations remain **USER ACCEPTED — PASS** for the current milestone. Stock MediaPipe/TFLite and `ExistingReadback` remain preserved fallbacks/references. Do not reopen these accepted lines without new evidence.

## Canonical, calibration, and retargeting invariants

Current accepted V1 semantics remain unchanged:

- image X right, image Y up;
- canonical 3D +X camera/view right, +Y up, +Z away;
- MediaPipe world conversion `(x, -y, z)`, pelvis-relative when pelvis is available;
- front-facing metadata does not imply horizontal inference mirroring;
- display mirror is presentation-only;
- modular calibration and partial-body validity remain intact;
- Phase 4 signed mapping and analytic two-bone IK remain positional authority;
- Foundation E adds only endpoint-preserving/post-solve orientation detail and optional hand/finger articulation through the code-owned live spike detail component.

## Stabilization and presentation tuning

The accepted Phase 3 stabilizer remains calibration/locomotion authority. Stable, Raw and responsive avatar-drive variants remain available and the final responsiveness/default selection is deferred. `HumanoidRetargeter` presentation smoothing remains a downstream visual layer; E detail executes before the combined solved target is captured when the layer is enabled.

## Phase 5A — implemented, not accepted

Known USER QA findings remain unresolved: planted-feet leaning can still produce unwanted translation, and cadence stepping while stationary is not yet robust enough. Phase 5A remains **IMPLEMENTED / NOT USER ACCEPTED** and is not part of the Foundation E wiring correction.

## Current development state

```text
camera/input acquisition       strong baseline
inference backend              strong baseline
scheduling                     accepted
fresh pose throughput          near camera cadence on best tested body path
V1 canonical/retarget          accepted positional compatibility baseline
avatar-drive latency tuning    preserved and deferrable
Foundation A                   implemented; Orchestrator audited; manual QA deferred
Foundation B                   implemented; Orchestrator audited; manual QA deferred
Foundation C                   implemented; Orchestrator audited; manual QA deferred
Foundation D                   implemented; Orchestrator code-audited; manual QA deferred
Foundation E                   implemented; Builder verified; Orchestrator re-audit pending; manual QA deferred
locomotion                     implemented, not accepted
```

## Documentation authority

For current project truth, use this order:

1. `Docs/current-state.md` — current status and immediate governance.
2. `Docs/decisions.md` — current architectural/product decisions.
3. `Docs/pre-phase5a-foundations.md` — authoritative architecture/requirements for the foundation development track.
4. `Docs/architecture.md` and `Docs/motion-engine.md` — detailed accepted architecture/phase design; read together with the newer current-state/foundation docs when older phase wording appears.
5. OpenVINO and avatar-drive progress documents — historical/current experiment detail.

Task/worker handoffs are execution briefs for their specific task and must not override a newer current-state/decision/foundation document.

## Guardrails for the next Orchestrator / Builder

- Do not merge to `main` without explicit USER approval.
- Do not mark Foundations A, B, C, D, or E USER accepted until the deferred comprehensive USER manual/runtime QA pass succeeds.
- Foundation E runtime wiring is now corrected and Builder verified; the next gate is an independent Orchestrator re-audit of the exact branch state.
- Preserve the generic `IHumanoidPostSolveDetailLayer` boundary inside `HumanoidRetargeter`; E-specific construction belongs to code-owned spike composition, not the generic retargeter.
- Do not extend Foundation E motion scope during re-audit; the verified rich/palm/finger math remains unchanged.
- Do not begin comprehensive A–E USER QA until the Foundation E re-audit clears the code/runtime-composition gate.
- Do not return to Phase 5A fixes until that foundation QA pass is completed and explicitly assessed.
- Do not start Phase 6.
- Preserve stock MediaPipe/TFLite, OpenVINO scheduling/acquisition, CanonicalBodyV1, stable calibration/locomotion inputs and serialized/default backend/acquisition/avatar-drive meanings.
- Rich orientation must not replace Phase 4 positional/IK authority; optional hand/finger/detail bones must not become mandatory.
- Do not allow optional detail to introduce queues/backlogs or provider/camera/inference work.
- Do not treat Hand Landmarker hand-local geometry as body/world coordinates without an explicit validated fusion transform.

## Immediate next step

Perform the independent **Foundation E Orchestrator re-audit** against the exact branch state, including the corrected code-owned live-spike composition. If that passes, perform the comprehensive Foundations A–E USER Unity/manual/runtime QA pass. Do not return to Phase 5A or start Phase 6 before these gates are explicitly assessed.
