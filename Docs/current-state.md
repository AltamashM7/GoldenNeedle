# Golden Needle — Current State

Authoritative current-state refresh: 2026-09-15.

Latest independently accepted foundation code-audit checkpoint remains:
`864b39a520c78db1a0572b87a7d42c9da5cedaa4` — Foundation D corrective implementation passed independent Orchestrator re-audit with the bundled official Hand Landmarker preserved, shared body/hand provider timeline verified, protected body/OpenVINO and Phase 4 paths intact, and permanent Foundation C/D verification green.

Latest Foundation E Builder verification checkpoint:
`df4f0d62a7379984e966b29311dc2dfe02cb51f3` — Foundation E implementation and deterministic/static Builder verification are complete; independent Orchestrator audit and USER manual/runtime QA are still pending.

Verification evidence retained for this gate:
- Foundation C corrective workflow at `9e4c4ac3a9d87eade06a43e2833481cce22b70f9`: run `34919859131`, job `104225315113`: **PASS**.
- Foundation D accepted exact-head workflow at `864b39a520c78db1a0572b87a7d42c9da5cedaa4`: run `34920144300`, job `104226227007`: **PASS**.
- Foundation D migrated permanent-boundary workflow at `44a39d5ef566b800587a3be73345e843fffe5ac0`: run `34930150745`, job `104256492165`: **PASS**.
- Foundation E Builder workflow at `df4f0d62a7379984e966b29311dc2dfe02cb51f3`: run `34930272876`, job `104256849477`: **PASS**.

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
- Foundation E — Orientation-aware + optional hand/finger retarget: **IMPLEMENTED / BUILDER AUTOMATED & CODE VERIFICATION COMPLETE / ORCHESTRATOR AUDIT PENDING / USER MANUAL QA DEFERRED**.
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
E — Orientation-aware + optional hand/finger retarget  IMPLEMENTED / Builder verified / Orchestrator audit pending / manual QA deferred
    ↓
Independent Foundation E Orchestrator audit
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
- `RichMotionEvidenceFrame` carries fixed/preallocated semantic provider-independent evidence with source/provider identity, source timestamp, receive/evaluation timing, confidence and validity;
- `RichMotionFrame` carries the copied semantic evidence plus fixed/preallocated anatomical orientation channels and compact aggregate diagnostics;
- `CanonicalAnatomicalBasis` is the canonical rich orientation authority: explicit primary, secondary and third axes, determinant, handedness and validity; no quaternion is stored as rich canonical source-of-truth;
- `IRichMotionEvidenceSource` is an optional additive capability. `ICanonicalPoseSource` was not expanded and legacy-only sources continue to operate normally;
- `MediaPipeCanonicalPoseSource` implements the optional rich capability while reusing the same persistent 33-landmark `PoseObservation` refreshed by the existing canonical copy. It does not perform a second provider observation copy, inference, queue or scheduling path;
- numeric MediaPipe landmark indices remain confined to provider/mapping code;
- supported rich orientation channels remain pelvis, chest, bilateral upper/lower arms, bilateral upper/lower legs and bilateral feet;
- all channels use one descriptor-driven basis reconstruction path rather than per-bone twist patches;
- twist states remain `Observed`, `Held`, `ReferenceFallback` and `Unobservable` with separate swing/twist observability;
- the rich reference/temporal state is separate from `MotionCalibrationProfile` and resets on calibration begin/reset, coordinate-convention changes and rich source/session discontinuity;
- `MotionEngineRuntime` exposes rich data read-only after accepted legacy rotation and positional outputs are produced;
- Foundation C itself does not apply rich orientation to the avatar; Foundation E is the additive consumer.

Corrective Foundation C workflow run `34919859131`, job `104225315113`, passed at `9e4c4ac3a9d87eade06a43e2833481cce22b70f9`. Actual Unity Editor compilation/Test Runner execution remains deferred.

### Foundation D — MediaPipe hands

Status: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA DEFERRED**.

Foundation D preserves the optimized body provider and adds a separate optional MediaPipe Hand Landmarker stream rather than migrating body tracking to Holistic.

Current implementation:

- project-owned `CanonicalHandFrame` contains exactly 21 stable semantic hand landmarks per hand with explicit left/right identity, normalized image positions, hand-local 3D positions, confidence and per-hand timing;
- `CanonicalAnatomicalBasis` is reused for palm orientation evidence;
- `HandFeatureSolver` derives conservative per-finger curl and compact hand-shape summaries;
- `HandAssociationSolver` prefers accepted body-wrist proximity, supports single-wrist cases and exposes ambiguity instead of forcing unsafe identity;
- left/right hands have independent freshness; current defaults are `350 ms` maximum hand age and `200 ms` maximum body↔hand source skew;
- the existing `MediaPipePoseProvider` `Stopwatch _clock` is the single semantic/fusion timing authority for body and hands; Unity unscaled time is cadence-only;
- `ICanonicalHandSource` is additive; CanonicalBodyV1 and `ICanonicalPoseSource` remain unchanged;
- Hand Landmarker uses MediaPipeUnityPlugin `0.16.3`, CPU `LIVE_STREAM`, `numHands=2`, default `12 Hz`, separate `480x360` reusable preparation;
- one active inference plus at most one replaceable newest pending hand snapshot is allowed; there is no FIFO/history/replay/catch-up queue;
- the official Hand Landmarker `float16/1` model is bundled and locked at `7,819,105` bytes / SHA-256 `fbc2a30080c3c557093b5ddfc334698132eb341044ccee322ccf8bcf3607cde1`;
- Foundation D remains a data producer only and does not drive avatar transforms.

The accepted exact-head D verification remains run `34920144300`, job `104226227007`, result **SUCCESS** at `864b39a520c78db1a0572b87a7d42c9da5cedaa4`. After Foundation E authorization, the obsolete `FOUNDATION_E_NOT_STARTED` guard was migrated rather than removed: run `34930150745`, job `104256492165`, result **SUCCESS** at `44a39d5ef566b800587a3be73345e843fffe5ac0`, including `FOUNDATION_D_PRODUCTION_APPLICATION_BOUNDARY_PRESERVED=PASS` while retaining model/timeline/provider/body/OpenVINO/CanonicalBodyV1/scene protections.

Unity Editor NUnit test source exists, but actual Unity Editor compilation/Test Runner and real webcam hand inference were not performed by the Builder/Orchestrator code-audit environment. USER runtime QA remains intentionally deferred.

### Foundation E — optional-bone retargeting

Status: **IMPLEMENTED / BUILDER AUTOMATED & CODE VERIFICATION COMPLETE / ORCHESTRATOR AUDIT PENDING / USER MANUAL QA DEFERRED**.

Foundation E is an additive post-Phase-4 detail/application layer. The accepted Phase 4 `HumanoidRetargeter.ApplyMotionFrame(...)` solve remains byte-for-byte protected by the E workflow and remains the positional/IK authority.

Current implementation:

- `IHumanoidPostSolveDetailLayer` is a provider-independent optional hook discovered by `HumanoidRetargeter`; `HumanoidRetargeter` contains no rich-frame, hand-contract, MediaPipe, landmark-index, or finger-specific formulas;
- with presentation smoothing enabled the order is visible Phase-4 capture → exact Phase-4 `ApplyMotionFrame(...)` → optional E post-solve detail → combined solved capture → restore visible rotations → existing presentation smoothing; with smoothing disabled it is exact Phase 4 → optional E detail → visible avatar;
- disabling/unavailable E leaves the established Phase 4 body solve unchanged; layer failures are caught/degraded locally rather than invalidating body retargeting;
- `HumanoidRigBinding.IsBound` and `BoundBoneCount` remain required Phase-4-body semantics only. `HumanoidRigDetailCapabilities` caches a separate optional capability set for all 30 Animator Humanoid finger segments; zero, partial and full detail rigs are valid;
- `IExplicitHumanoidDetailRigSource` is a new optional extension; the legacy explicit body-source contract was not expanded;
- production rich limb channels are exactly left/right upper arm, lower arm, upper leg and lower leg. Pelvis, chest and both feet are intentionally deferred from production E application;
- E consumes only `MotionEngineRuntime.RichMotionFrame` and Foundation C's `CanonicalAnatomicalBasis`/`CanonicalToAvatarAxisMap` contracts; source reference is zero-delta on first trustworthy acquisition and signed axial mapping multiplies by the map determinant sign so reflected mappings invert twist correctly;
- `Observed` may update trusted axial detail, `Held` maintains the last trusted target, while `ReferenceFallback` and `Unobservable` do not fabricate observation and instead return only the E contribution toward neutral;
- parent axial twist is applied around the already solved Phase-4 segment axis and the direct child world rotation is restored, preserving downstream chain orientation/geometry. Deterministic smoke reported `FOUNDATION_E_MAX_ENDPOINT_RESIDUAL=0`, with the tight threshold set below `1e-6`;
- palm/finger input is consumed only through `HandMotionRuntime` / `CanonicalHandFrame`. Left/right hands are independent and reuse Foundation D freshness instead of inventing another hand-age authority;
- target palm orientation is characterized only when real avatar index/middle/little proximal geometry is sufficient; otherwise palm/finger refinement is skipped and body/arm control continues;
- thumb mapping uses CMC→MCP, MCP→IP, IP→Tip; Index/Middle/Ring/Pinky use MCP→PIP, PIP→DIP, DIP→Tip. Source segment direction is expressed against the live source palm basis and reconstructed against the characterized target palm basis, driving only available target segments by shortest swing without manufacturing finger axial twist;
- stale hands stop receiving live articulation and only E-owned optional finger contribution returns smoothly toward reference. Reacquisition establishes a new zero-delta reference before live articulation resumes, avoiding a large snap;
- implementation uses fixed arrays/static descriptors/latest-only state. There is no new inference, camera acquisition, provider scheduling, queue/history/replay path, or per-frame LINQ; hierarchy/capability discovery occurs at binding/reference setup rather than as a per-frame search;
- category switches exist for E master enable, rich limb axial detail, palm orientation and finger articulation.

Builder automated evidence at `df4f0d62a7379984e966b29311dc2dfe02cb51f3`:

- Foundation E workflow run `34930272876`, job `104256849477`: **SUCCESS**;
- Foundation A/B/C/D prerequisite smokes: **PASS**;
- Foundation E deterministic smoke: **PASS**, 17 checks;
- master-disabled Phase4 compatibility: **PASS**;
- zero/partial/full optional capability cases: **PASS**;
- authored position/scale immutability: **PASS**;
- left/right hand independence, stale-hand behavior and zero-delta reacquisition: **PASS**;
- proper/reflected twist sign and observability continuity: **PASS**;
- downstream compensation / endpoint residual: **PASS**, reported maximum residual `0`;
- reference-version reacquisition, provider isolation and latest-only/no-backlog checks: **PASS**;
- static exact `ApplyMotionFrame(...)` comparison, generic hook, eight-channel-only, locked-scope and read-only workflow guards: **PASS**.

Unity Editor NUnit source is present for Transform/binding-specific behavior, but **actual Unity Editor compilation/Test Runner execution was not performed in this Builder environment**. USER webcam/avatar/manual runtime QA is likewise deferred to the comprehensive A–E pass. Foundation E is not USER accepted and is not yet Orchestrator audited.

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
  -> optional Foundation E post-solve detail
  -> avatar presentation
```

Foundation D remains an optional secondary stream alongside that path; it does not replace or redefine the body pipeline. Foundation E consumes already-produced rich/hand contracts and adds retarget math only.

Important policy:

- `OpenVinoCpuFp32` is the **best-tested low-end backend configuration**, not a reason to delete the stock MediaPipe/TFLite path.
- Stock MediaPipe/TFLite CPU remains available as the safe fallback/reference backend.
- `WebCamCpuPixels` is the best-tested camera acquisition path for OpenVINO on the USER machine.
- `ExistingReadback` remains available as fallback/reference.
- Do not silently change serialized/default backend or acquisition policy merely because a faster tested path exists.

## Current low-end proof machine

USER hardware used for the main performance evidence:

- Windows 10 build 19045 x64;
- Intel Core i3-7100U @ 2.40 GHz;
- 2 cores / 4 logical processors;
- Intel HD Graphics 620;
- no dedicated/discrete GPU;
- approximately 4 GB shared graphics memory reported by Unity;
- compute shaders available.

This remains an intentionally low-end proof target. Stronger machines may later justify different backend policy.

## OpenVINO integration status

### Gate A

**PASS.** The unchanged exact detector TFLite is directly readable/executable by OpenVINO; the detector DENSIFY issue was a Sentis importer limitation, not an OpenVINO compatibility blocker.

### Gate B

**PASS WITH NOTES.** On the 363-frame recorded sequence:

```text
TASKS_REFERENCE            ~28.739 ms mean / 34.796/s offline capacity
GRAPH_TFLITE_CPU           ~28.094 ms mean / 35.595/s
GRAPH_OPENVINO_CPU_FP32     ~14.032 ms mean / 71.265/s
OpenVINO pose-presence agreement ~99.7245%
normalized XYZ RMSE        ~0.01113
world 3D RMSE              ~0.02188 m
OpenVINO bridge copy       ~0.2025 ms mean
```

These are offline VIDEO-mode capacity measurements, not Unity LIVE_STREAM end-to-end promises.

### Unity integration

The additive Windows OpenVINO plugin, managed wrapper, selectable provider backend, lifecycle, exact-model packaging, and real-frame 33-normalized/world-landmark semantic smoke are complete.

The native runtime remains additive: stock `mediapipe_c.dll` / TFLite behavior is not replaced.

OpenVINO identity used by the proven package:

- OpenVINO 2026.3.0;
- explicit CPU execution;
- backend identity `OPENVINO_CPU_FP32`;
- exact detector and landmark models extracted from the existing production task bundle;
- no detector conversion or densification.

## Accepted OpenVINO scheduling optimization

The accepted managed optimization uses one persistent OpenVINO worker, exactly two reusable frame slots, at most one active frame/inference, at most one replaceable newest pending frame, latest-useful-frame-wins semantics and no FIFO/history/replay/catch-up queue. USER evidence showed the former prepared-to-launch delay collapse to approximately 0 ms on the fast path and materially better F12 responsiveness.

Status: **USER ACCEPTED — PASS**.

Do not reopen this scheduling architecture without new evidence.

## Accepted WebCamCPU/GetPixels32 acquisition optimization

The selected R2 path is:

```text
WebCamTexture
  -> GetPixels32(reused Color32[])
  -> reusable CPU resize / H-V transform
  -> persistent 320x240 RGBA buffer
  -> existing OpenVINO latest-frame mailbox
  -> existing OpenVINO worker
```

Full-body USER phone-recorded evidence with the healthy ~30 FPS camera mode showed approximately:

```text
camera capture          ~28.6-30.3 FPS
fresh pose results      ~26.7-29.3/s
CPU GetPixels32         ~0.3 ms
CPU preparation         ~3.6 ms
CPU acquisition total   ~3.9 ms
OpenVINO graph/inference commonly ~25-35 ms
frame -> result         commonly ~33-62 ms
```

Status: **USER ACCEPTED — PASS for the current milestone**.

`ExistingReadback` remains available as fallback/reference; do not remove it.

## Canonical, calibration, and retargeting invariants

Current accepted V1 semantics remain unchanged while the rich/hand/detail foundations are developed:

- image X right, image Y up;
- 3D +X camera/view right, +Y up, +Z away;
- MediaPipe world conversion `(x, -y, z)`, pelvis-relative when pelvis is available;
- front-facing metadata does not imply horizontal inference mirroring;
- display mirror is presentation-only;
- modular calibration allows body-reference readiness plus independent arm/leg chain geometry;
- partial-body tracking remains valid;
- Phase 4 signed canonical-to-avatar mapping and analytic two-bone IK remain positional authority/fallback/reference behavior;
- Foundation E adds endpoint-preserving axial/detail refinement only after the accepted exact Phase 4 solve; rich orientation is not the source of endpoint positions.

## Stabilization and avatar-drive tuning

The accepted Phase 3 stabilizer remains `minCutoff 1.0`, `beta 0.05`, `derivativeCutoff 1.0`, acquire confidence `0.60`, sustain confidence `0.40`, acquire samples `2`, loss grace `0.10 s`, reset-after-loss `0.25 s`.

Avatar solving preserves Stable, Raw and responsive One Euro source variants. Calibration and locomotion remain on the stable frame. USER testing established that Stable is smooth but slower than Raw, Raw feels effectively instant but slightly less stable, and responsive modes trade some stability for latency. Final smoothing/default selection remains deferred.

## Presentation smoothing

`HumanoidRetargeter` presentation smoothing remains a separate downstream visual layer. The exact Phase 4 solve remains authoritative. Foundation E's optional post-solve detail executes before the solved presentation target is captured so smoothing does not fight the detail result. Presentation smoothing does not modify canonical tracking, calibration, IK targets, cadence, or locomotion.

## Phase 5A — implemented, not accepted

Implemented pieces include support-foot camera-space locomotion, physical X/Z mapping, cadence extension, body-heading mapping, safe recenter, avatar root X/Z authority only, F12 Lab/Game presentation, third-person camera behavior plus Foundation B gameplay view presets, diagnostics and world/grid views.

Known USER QA findings remain unresolved: planted-feet leaning can still cause unwanted translation, and cadence stepping while stationary is not yet robust enough. Therefore Phase 5A remains **IMPLEMENTED / NOT USER ACCEPTED**. Do not return to these fixes until the approved pre-Phase-5A foundation track reaches its current stopping point.

## Closed / deferred performance lines

Do not spend current development time on callback-to-poll micro-optimization, old-TFLite scheduling changes, global D3D12, Sentis GPUCompute on Intel HD 620, detector densification, custom Media Foundation capture, duplicate CPU+GPU inference, manual neural-layer splitting, or further OpenVINO/readback number chasing without new evidence.

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
Foundation E                   implemented; Builder automated/code verification complete; Orchestrator audit pending; manual QA deferred
locomotion                     implemented, not accepted
```

Further latency optimization is **optional future work**, not a prerequisite to move forward.

## Documentation authority

For current project truth, use this order:

1. `Docs/current-state.md` — current status and immediate governance.
2. `Docs/decisions.md` — current architectural/product decisions.
3. `Docs/pre-phase5a-foundations.md` — authoritative architecture/requirements for the foundation development track.
4. `Docs/architecture.md` and `Docs/motion-engine.md` — detailed accepted architecture/phase design; read together with the newer current-state/foundation docs when older phase wording appears.
5. `Docs/openvino-unity-integration-progress.md` — OpenVINO integration history/current resolution.
6. `Docs/openvino-unity-scheduling-optimization-progress.md` — scheduling optimization history/current resolution.
7. `Docs/openvino-unity-readback-optimization-progress.md` — acquisition optimization history/current resolution.
8. `Docs/avatar-drive-source-experiment-progress.md`, `Docs/responsive-avatar-stabilizer-progress.md`, and `Docs/responsive-avatar-beta-sweep-progress.md` — avatar-drive tuning experiment history/current tuning state.

Task/worker handoffs are execution briefs for their specific task and must not override a newer current-state/decision/foundation document.

## Guardrails for the next Orchestrator / Builder

- Do not merge to `main` without explicit USER approval.
- Do not mark Foundations A, B, C, D, or E USER accepted until the deferred comprehensive USER manual/runtime QA pass succeeds.
- Foundation E Builder implementation/verification is complete; do not extend E scope during independent Orchestrator re-audit unless a real defect is found.
- Do not begin comprehensive A–E USER QA until the independent Foundation E audit clears the code/automated gate.
- Do not return to Phase 5A fixes during Foundation E audit.
- Do not start Phase 6.
- Do not delete or weaken the stock MediaPipe/TFLite fallback.
- Do not silently switch serialized/default backend, frame-acquisition mode, or avatar-drive source.
- Do not change stable calibration/locomotion inputs while tuning avatar response.
- Do not destructively mutate the accepted 20-joint canonical V1 contract.
- Do not use quaternions as the hidden canonical orientation source of truth; reason from validated anatomical bases and map to quaternions only at final avatar application.
- Do not solve only forearm twist as a special-case patch; rich orientation application remains general across supported limb segments.
- Do not replace the Phase 4 positional/IK solve with Foundation C/E rich orientation.
- Do not make optional finger/hand/detail bones mandatory for rig validity.
- Do not allow optional detail work to regress the accepted low-end body path or introduce queues/backlogs.
- Do not treat Hand Landmarker hand-local world landmarks as body/world coordinates without an explicit validated fusion transform.
- Do not densify the detector.
- Do not force D3D12 globally.
- Do not reopen accepted OpenVINO scheduling/WebCamCPU work without new evidence.
- Do not remove the preserved smoothing modes merely for code cleanliness until a later tuning/default decision is explicitly approved.

## Immediate next step

Perform the independent **Foundation E Orchestrator re-audit** against the exact branch state. If that audit passes, the next project gate is the previously deferred comprehensive A–E USER Unity/manual/runtime QA pass. Do not return to Phase 5A or start Phase 6 before those gates are explicitly advanced.