# Golden Needle — Current State

Authoritative current-state refresh: 2026-09-15.

Latest substantive runtime/code checkpoint before this documentation refresh:
`e16a93c5fd4802d97dc081a2d5f5c9f402ec7067` — Foundation D separate MediaPipe Hand Landmarker integration plus deterministic hand/scheduler verification and permanent read-only Foundation D CI.

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
- Foundation D — MediaPipe Hand Landmarker integration: **IMPLEMENTED / BUILDER AUTOMATED/CODE VERIFICATION COMPLETE / ORCHESTRATOR AUDIT PENDING / USER MANUAL QA DEFERRED**.
- Foundation E — Orientation-aware + optional hand/finger retarget: **NOT STARTED**.
- Phase 5A — support-foot locomotion / Lab-Game presentation: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.

The project is **not** blocked on further motion-engine latency optimization. The current engine is strong enough to continue normal development. Additional smoothing/performance tuning remains intentionally available later.

The USER has chosen to continue Foundations A–E sequentially with focused implementation/code/automated verification, then perform one comprehensive manual Unity/runtime foundation QA pass after all five are built. This sequencing decision does **not** auto-accept any foundation, does not remove the deferred speech/camera/rich-orientation/hand QA requirements, and does not make visual/microphone behavior verified.

## Approved pre-Phase-5A foundation track

The development sequence remains:

```text
A — Unified Command System + modular speech input       IMPLEMENTED / Orchestrator audited / manual QA deferred
    ↓
B — Camera View / Focus Preset System                  IMPLEMENTED / Orchestrator audited / manual QA deferred
    ↓
C — Rich canonical motion/orientation architecture     IMPLEMENTED / Orchestrator audited / manual QA deferred
    ↓
D — MediaPipe hand-landmark integration                IMPLEMENTED / Builder verified / Orchestrator audit pending / manual QA deferred
    ↓
E — Orientation-aware + optional hand/finger retarget  NOT STARTED
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
- Foundation A CI is now permanently read-only: `contents: read`, no migration execution, no commit/push behavior, no frozen pre-Foundation-A diff gate.

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
- the Orchestrator corrective audit found and fixed the missing `GoldenNeedle.Core.Motion.Rotation` import required by `CanonicalBoneId` references, and the permanent Foundation B audit now guards that dependency.

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
- `MediaPipeCanonicalPoseSource` implements the optional rich capability while reusing the exact same persistent 33-landmark `PoseObservation` refreshed by the existing canonical copy. It does not perform a second provider observation copy, second inference, queue or scheduling path;
- `MediaPipeRichMotionEvidenceMapper` confines numeric MediaPipe landmark indices to the provider/mapping boundary and converts world evidence through the accepted `CanonicalCoordinateSystem.MediaPipeWorldToCanonical` convention;
- mapped semantic evidence includes pelvis/chest derived midpoints plus shoulders, elbows, wrists, hips, knees, ankles, heels, toes, thumbs and pinkies needed by the current orientation descriptors; this is pose-landmark evidence only, separate from Foundation D Hand Landmarker/finger articulation;
- supported orientation channels are pelvis, chest, left/right upper arm, left/right lower arm, left/right upper leg, left/right lower leg, left foot and right foot;
- all channels use one descriptor-driven basis reconstruction path rather than per-bone twist patches;
- basis construction normalizes the primary axis, projects independent secondary evidence away from the primary, derives the third axis with a cross product, re-orthogonalizes, checks determinant/handedness and rejects degenerate/non-finite evidence;
- swing and twist observability are separate. A trustworthy primary direction remains usable even when the secondary evidence cannot support twist;
- twist states are `Observed`, `Held`, `ReferenceFallback` and `Unobservable`;
- on short secondary-evidence ambiguity, the last trusted secondary axis is held, reprojected against the newest primary axis and reported with reduced twist confidence while swing keeps updating;
- after the longer ambiguity threshold, the solver moves through an explicit `ReferenceFallback` toward the separately held trustworthy reference secondary orientation around the current primary axis; it does not claim fresh observation, snap to zero or manufacture arbitrary roll;
- the current default rich ambiguity settings are minimum evidence confidence `0.35`, minimum vector magnitude `0.0001`, minimum projected-secondary magnitude `0.0025`, short hold `0.20 s`, reference-fallback threshold `0.55 s` and fallback response `5.0`;
- the rich reference/temporal state is separate from `MotionCalibrationProfile` and resets on calibration begin/reset, coordinate-convention changes and rich source/session discontinuity;
- `MotionEngineRuntime` exposes the rich evidence/frame read-only and updates it only after the accepted legacy rotation and positional kinematic outputs are produced;
- `HumanoidRetargeter` remains production-authoritative on the accepted `AvatarDriveFrame`/Phase 4 path. Foundation C does not apply rich orientation to the avatar; that belongs to Foundation E;
- a compact `RichMotionSummary` exposes source availability, schema version, valid-basis count and observed/held/reference-fallback/unobservable twist counts without frame-log spam.

Automated proof coverage remains green in the permanent read-only Foundation C workflow, and the independent Orchestrator code audit is complete. Unity Editor NUnit test source for the critical behaviors is present, but an actual Unity Editor compilation/Test Runner execution was **not** performed in the Builder environment. USER visual/runtime twist behavior and Unity runtime acceptance therefore remain deferred and must not be inferred from pure/static verification.

### Foundation D — MediaPipe hands

Status: **IMPLEMENTED / BUILDER AUTOMATED/CODE VERIFICATION COMPLETE / ORCHESTRATOR AUDIT PENDING / USER MANUAL QA DEFERRED**.

Foundation D preserves the optimized body provider and adds a separate optional MediaPipe Hand Landmarker stream rather than migrating body tracking to Holistic.

Current implementation:

- project-owned `CanonicalHandFrame` contains exactly 21 stable semantic hand landmarks per hand, with explicit left/right identity, normalized canonical image positions, hand-local 3D positions, tracking/confidence and per-hand source timing;
- hand-local 3D geometry is intentionally not mislabeled as body/world position; body/world fusion remains a later explicitly owned step;
- `CanonicalAnatomicalBasis` is reused for palm orientation evidence, preserving explicit finite/right-handed basis semantics rather than introducing a hidden quaternion source of truth;
- `HandFeatureSolver` derives per-finger curl features plus conservative `Open`, `Fist`, `Pointing`, `Intermediate`, and `Unknown` summaries; perfect finger mocap is not required;
- `HandAssociationSolver` prefers association to the accepted body pose wrists when both are trustworthy, supports a single-wrist case, falls back conservatively to MediaPipe handedness when necessary, and exposes ambiguous association instead of forcing an unsafe side;
- left and right hands have independent freshness. Default maximum hand age is `350 ms` and default body↔hand source skew is `200 ms`; stale/skewed samples become unavailable for live fusion without changing body tracking state;
- one missing/stale hand does not invalidate the other hand or the body stream;
- hand session state is cleared when the body provider coordinate/session convention changes so stale callbacks cannot cross a camera/session boundary;
- `ICanonicalHandSource` is an optional additive capability; CanonicalBodyV1 and `ICanonicalPoseSource` remain unchanged;
- `MediaPipeCanonicalPoseSource` code-owns the optional hand component/runtime wiring so no scene YAML migration is required;
- `MediaPipeHandLandmarkerSource` uses the existing embedded MediaPipeUnityPlugin `0.16.3` Hand Landmarker API in CPU `LIVE_STREAM` mode with `numHands=2`;
- default hand cadence is `12 Hz` at a separate `480x360` hand preparation size. It samples the same existing `WebCamTexture` only at hand cadence with its own reused `Color32[]`/RGBA buffers;
- hand acquisition uses two reusable RGBA slots with one active inference and at most one replaceable latest pending snapshot. There is no FIFO/history/replay/catch-up queue and the accepted OpenVINO body mailbox is not reused or modified;
- callback conversion is isolated from Unity scene/time APIs and copies MediaPipe task results into project-owned snapshot data before publication;
- the official Hand Landmarker bundle is locked to Google MediaPipe `float16/1`, `7,819,105` bytes, SHA-256 `fbc2a30080c3c557093b5ddfc334698132eb341044ccee322ccf8bcf3607cde1`;
- the runtime prefers an expected local StreamingAssets/cache copy and otherwise may bootstrap the exact versioned Google model into a private cache only after SHA-256 verification. Download/model failure disables the optional hand stream without invalidating body tracking;
- no finger, hand, or rich-orientation data is applied to the production avatar yet. That application belongs to Foundation E.

Builder verification is green in permanent read-only workflow run `34914680954` at `e16a93c5fd4802d97dc081a2d5f5c9f402ec7067`. The run passed Foundation A/B/C regressions, the Foundation D hand math/scheduler smoke, exact official model size/SHA verification, provider isolation, 21-landmark contract, hand-local coordinate ownership, latest-only scheduling, unchanged body/OpenVINO defaults, unchanged CanonicalBodyV1, unchanged Phase 4 production retargeting, Foundation E-not-started checks, and the final read-only dirty-tree guard.

Unity Editor NUnit test source covers the corresponding deterministic hand cases, but actual Unity Editor compilation/Test Runner and real webcam hand inference were **not** performed by this Builder. USER runtime QA remains intentionally deferred to the comprehensive A–E pass. Do not infer live hand quality, CPU cost, gesture quality, association quality, or real-world freshness behavior from the pure/static verification alone.

### Foundation E — optional-bone retargeting

Avatar binding is capability-based. Drive only bones that actually exist.

Missing finger/hand/detail bones must never crash or invalidate unrelated body retargeting. The accepted Phase 4 positional/IK path remains available as fallback/reference while richer orientation/articulation is rolled out.

Foundation E is **NOT STARTED** as of this checkpoint.

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
  -> calibration / retarget / avatar
```

Foundation D adds an optional secondary stream alongside that path; it does not replace or redefine the body pipeline.

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

The initial live OpenVINO A/B proved that OpenVINO graph/inference work was faster than stock TFLite, but its end-to-end advantage was masked by avoidable scheduling delay.

The accepted managed optimization uses:

- one persistent OpenVINO worker;
- exactly two reusable frame slots;
- at most one active frame/inference;
- at most one replaceable newest pending frame;
- latest useful frame wins;
- no FIFO/history/replay/catch-up queue;
- worker continuation launch (`OVW`) when a pending frame can immediately follow a completed inference.

USER runtime evidence after the optimization showed the former OpenVINO prepared-to-launch delay collapse to approximately 0 ms on the fast path, frame delta return to 0 in representative samples, and non-zero worker continuations. The USER also reported materially better F12 responsiveness.

Status: **USER ACCEPTED — PASS**.

Do not reopen this scheduling architecture without new evidence.

## Accepted WebCamCPU/GetPixels32 acquisition optimization

The old dominant pre-inference bottleneck was GPU/readback completion, commonly tens of milliseconds even after OpenVINO scheduling was fixed.

The selected R2 path is:

```text
WebCamTexture
  -> GetPixels32(reused Color32[])
  -> reusable CPU resize / H-V transform
  -> persistent 320x240 RGBA buffer
  -> existing OpenVINO latest-frame mailbox
  -> existing OpenVINO worker
```

No custom Windows camera stack was required.

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

Compared with the earlier ~12-13 fresh results/s and roughly ~100 ms frame-to-result behavior, this exposes close to one fresh pose per camera frame on the USER machine.

Status: **USER ACCEPTED — PASS for the current milestone**.

`ExistingReadback` remains available as fallback/reference; do not remove it.

## Canonical, calibration, and retargeting invariants

Current accepted V1 semantics remain unchanged while the rich/hand foundations are developed:

- image X right, image Y up;
- 3D +X camera/view right, +Y up, +Z away;
- MediaPipe world conversion `(x, -y, z)`, pelvis-relative when pelvis is available;
- front-facing metadata does not imply horizontal inference mirroring;
- display mirror is presentation-only;
- modular calibration allows body-reference readiness plus independent arm/leg chain geometry;
- partial-body tracking remains valid;
- Phase 4 signed canonical-to-avatar mapping and analytic two-bone IK remain authoritative fallback/reference behavior;
- current swing-only limb alignment remains the accepted V1 production policy until richer orientation is applied and accepted in a later foundation.

The rich orientation and hand tracks are additive and independently testable. Do not change these semantics as a side effect of commands, camera work, hand-provider work or unrelated tuning.

## Stabilization and avatar-drive tuning

### Stable tracking path

The accepted Phase 3 stabilizer remains:

```text
minCutoff        1.0
beta             0.05
derivativeCutoff 1.0
acquireConfidence 0.60
sustainConfidence 0.40
acquireSamples    2
lossGraceSeconds  0.10
resetAfterLoss    0.25
```

This stable path remains the authority for:

- calibration;
- Phase 5A locomotion/support/cadence/heading inputs.

### Avatar-only source selector

The avatar solve now supports a deliberately preserved tuning surface:

```text
StabilizedCanonical   = 0   -> 1.0 / 0.05 / 1.0
RawCanonical          = 1   -> unfiltered positional canonical frame
ResponsiveCanonicalA  = 2   -> 1.5 / 0.25 / 1.0
ResponsiveCanonicalB  = 3   -> 2.0 / 0.50 / 1.0
ResponsiveCanonicalC  = 4   -> 1.0 / 0.25 / 1.0
ResponsiveCanonicalD  = 5   -> 1.0 / 0.50 / 1.0
```

All existing serialized enum meanings are preserved.

For the experimental filtered modes, persistent stabilizers run continuously from raw canonical data so live switching does not cold-start the filter.

The selected `AvatarDriveFrame` consistently feeds:

- `CanonicalRotationSolver`;
- `CanonicalKinematicTargetBuilder`;
- `HumanoidRetargeter` torso/source mapping.

Calibration and locomotion remain on the stable frame regardless of the selected avatar-drive mode.

### USER findings

USER runtime testing established:

- `StabilizedCanonical` is smooth and satisfactory but perceptibly slower than Raw;
- `RawCanonical` feels effectively instant / near-zero-latency subjectively, but is slightly less stable;
- Responsive A and B improve responsiveness relative to Stable but still retain minor jitter compared with Stable;
- C and D were added as a beta-only sweep so future tuning can isolate high-motion responsiveness from low-motion smoothing.

The USER explicitly chose **not to force a final smoothing winner now**. Fine-tuning remains available later.

Current policy:

- preserve Stable, Raw, and responsive tuning modes during ongoing development;
- do not silently change the serialized/default avatar-drive mode;
- do not collapse the experiment into one hardcoded profile yet;
- a future cleanup may expose a cleaner user/developer-facing smoothing profile or intensity control once the preferred range is known.

This tuning surface is part of the current engineering baseline, not an unfinished blocker.

## Presentation smoothing

`HumanoidRetargeter` presentation smoothing remains a separate downstream visual layer. It affects visible humanoid rotations only after the exact solve and does not modify canonical tracking, calibration, IK targets, cadence, or locomotion.

Current implementation still supports direct/off behavior and the bounded smoothed behavior. Do not confuse presentation smoothing with the canonical One Euro avatar-drive modes above.

## Phase 5A — implemented, not accepted

Phase 5A already exists; it is not the next phase to "start" from scratch.

Implemented pieces include:

- support-foot camera-space locomotion;
- physical X/Z mapping;
- cadence extension;
- body-heading mapping;
- safe recenter;
- avatar root X/Z authority only;
- F12 Lab/Game presentation;
- third-person camera behavior plus Foundation B gameplay view presets;
- diagnostics and world/grid views.

Known USER QA findings that remain unresolved:

- planted-feet leaning can still cause unwanted translation;
- cadence stepping while stationary is not yet robust enough.

Therefore Phase 5A remains **IMPLEMENTED / NOT USER ACCEPTED**.

Do not return to these fixes until the approved pre-Phase-5A foundation track has reached the USER-approved stopping point for the current milestone.

## Closed / deferred performance lines

Do not spend current development time on these without new evidence:

- callback-to-poll micro-optimization;
- result-callback launch changes for the old TFLite scheduler;
- forcing D3D12 globally;
- Sentis GPUCompute on Intel HD 620;
- detector densification for Sentis;
- custom Media Foundation/native camera capture;
- duplicate CPU+GPU inference per frame;
- manual neural-layer splitting across CPU/GPU;
- further OpenVINO/readback tuning merely to chase small numbers while the current path already approaches camera cadence.

Sentis and earlier OpenVINO benchmark history remains documented in the dedicated audit/progress files for reference.

## Current development state

The Motion Engine is sufficiently optimized for the current milestone, and the current track is fidelity/usability infrastructure:

```text
camera/input acquisition       strong baseline
inference backend              strong baseline
scheduling                     accepted
fresh pose throughput          near camera cadence on best tested body path
V1 canonical/retarget          accepted compatibility baseline
avatar-drive latency tuning    preserved and deferrable
Foundation A                   implemented; Orchestrator audited; manual QA deferred
Foundation B                   implemented; Orchestrator audited; manual QA deferred
Foundation C                   implemented; Orchestrator audited; manual QA deferred
Foundation D                   implemented; Builder verified; Orchestrator audit pending; manual QA deferred
Foundation E                   not started
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
- Do not mark Foundations A, B, C, or D USER accepted until the deferred comprehensive USER manual/runtime QA pass succeeds.
- Do not start Foundation E until Foundation D receives the required independent Orchestrator audit/authorization and the USER explicitly advances the sequence.
- Do not mark Phase 5A accepted.
- Do not start Phase 6.
- Do not delete or weaken the stock MediaPipe/TFLite fallback.
- Do not silently switch serialized/default backend, frame-acquisition mode, or avatar-drive source.
- Do not change stable calibration/locomotion inputs while tuning avatar response.
- Do not destructively mutate the accepted 20-joint canonical V1 contract.
- Do not use quaternions as the hidden canonical orientation source of truth; reason from validated anatomical bases and map to quaternions only at final avatar application.
- Do not solve only forearm twist as a special-case patch; rich orientation must remain a general bone-orientation system.
- Do not switch production `HumanoidRetargeter` to Foundation C rich orientation as part of Foundation D; orientation-aware application belongs to Foundation E.
- Do not make optional finger/hand bones mandatory for rig validity.
- Do not allow the optional hand stream to regress the accepted low-end body path or introduce unbounded queues/backlogs.
- Do not treat Hand Landmarker hand-local world landmarks as body/world coordinates without an explicit validated fusion transform.
- Do not densify the detector.
- Do not force D3D12 globally.
- Do not reopen accepted OpenVINO scheduling/WebCamCPU work without new evidence.
- Do not remove the preserved smoothing modes merely for code cleanliness until a later tuning/default decision is explicitly approved.

## Immediate next step

Foundation D implementation and Builder automated/code verification are complete. The next gate is an **independent Orchestrator audit of Foundation D**. USER Unity/runtime acceptance remains intentionally deferred to the comprehensive A–E pass.

Do **not** begin Foundation E from this checkpoint until the Orchestrator and USER explicitly advance the sequence.