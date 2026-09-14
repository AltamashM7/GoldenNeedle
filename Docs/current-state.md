# Golden Needle — Current State

Authoritative current-state refresh: 2026-09-14.

Last substantive runtime/code checkpoint before this documentation refresh:
`4a20b6a00bb86a023cab764407ec8c148356529d` — verified responsive-avatar beta-sweep runtime/audit state.

Last branch checkpoint before this documentation refresh:
`83ab2d15206e4563c3302cdcdccc2121ceddd741` — `docs: checkpoint beta sweep for user QA`.

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
- Phase 5A — support-foot locomotion / Lab-Game presentation: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.

The project is **not** blocked on further motion-engine latency optimization. The current engine is strong enough to continue normal development. Additional smoothing/performance tuning remains intentionally available later.

The USER has stated that a separate pre-locomotion task list must be handled before returning to Phase 5A acceptance work. Until that list is supplied and prioritized, do not resume locomotion merely because it is the numerically next phase.

## Current best-tested runtime path

The strongest same-machine USER-tested configuration is:

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

Canonical semantics remain unchanged:

- image X right, image Y up;
- 3D +X camera/view right, +Y up, +Z away;
- MediaPipe world conversion `(x, -y, z)`, pelvis-relative when pelvis is available;
- front-facing metadata does not imply horizontal inference mirroring;
- display mirror is presentation-only;
- modular calibration allows body-reference readiness plus independent arm/leg chain geometry;
- partial-body tracking remains valid;
- Phase 4 signed canonical-to-avatar mapping and analytic two-bone IK remain authoritative;
- swing-only limb alignment remains the accepted orientation policy.

Do not change these semantics as a side effect of performance or smoothing work.

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
- third-person camera behavior;
- diagnostics and world/grid views.

Known USER QA findings that remain unresolved:

- planted-feet leaning can still cause unwanted translation;
- cadence stepping while stationary is not yet robust enough.

Therefore Phase 5A remains **IMPLEMENTED / NOT USER ACCEPTED**.

The USER intends to supply a set of prerequisite tasks before locomotion work resumes. Treat those prerequisites as the next development planning input.

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

The Motion Engine is now considered sufficiently optimized for the current milestone:

```text
camera/input acquisition       strong baseline
inference backend              strong baseline
scheduling                     accepted
fresh pose throughput          near camera cadence on best tested path
canonical/retarget semantics   accepted
avatar-drive latency tuning    preserved and deferrable
locomotion                     implemented, not accepted
```

Further engine optimization is **optional future work**, not a prerequisite to move forward.

## Documentation authority

For current project truth, use this order:

1. `Docs/current-state.md` — current status and immediate governance.
2. `Docs/decisions.md` — current architectural/product decisions.
3. `Docs/architecture.md` and `Docs/motion-engine.md` — detailed architecture/phase design; read together with this current-state refresh when older phase wording appears.
4. `Docs/openvino-unity-integration-progress.md` — OpenVINO integration history/current resolution.
5. `Docs/openvino-unity-scheduling-optimization-progress.md` — scheduling optimization history/current resolution.
6. `Docs/openvino-unity-readback-optimization-progress.md` — acquisition optimization history/current resolution.
7. `Docs/avatar-drive-source-experiment-progress.md`, `Docs/responsive-avatar-stabilizer-progress.md`, and `Docs/responsive-avatar-beta-sweep-progress.md` — avatar-drive tuning experiment history/current tuning state.

Worker handoffs are execution briefs for their specific task and must not override a newer current-state/progress decision.

## Guardrails for the next Orchestrator / Builder

- Do not merge to `main` without explicit USER approval.
- Do not mark Phase 5A accepted.
- Do not start Phase 6.
- Do not delete or weaken the stock MediaPipe/TFLite fallback.
- Do not silently switch serialized/default backend, frame-acquisition mode, or avatar-drive source.
- Do not change stable calibration/locomotion inputs while tuning avatar response.
- Do not refactor provider/canonical topology during unrelated feature work.
- Do not densify the detector.
- Do not force D3D12 globally.
- Do not reopen accepted OpenVINO scheduling/WebCamCPU work without new evidence.
- Do not remove the preserved smoothing modes merely for code cleanliness until a later tuning/default decision is explicitly approved.

## Immediate next step

**Pause motion-engine optimization.**

Wait for the USER's pre-locomotion development list, organize it by dependency/risk, and complete those prerequisites before returning to Phase 5A locomotion acceptance work.
