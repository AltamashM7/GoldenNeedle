# Foundation Corrective Pass Progress

## Corrective Foundations Batch 1

**Purpose:** protect the accepted low-end body baseline by making the full MediaPipe Hand Landmarker stream an explicit experimental opt-in, and repair the reusable hand-capture buffer lifetime that caused the USER-reported `ArgumentNullException`.

**Starting remote SHA:** `e26b33ee62305cb7d3ba9e8d929dfe7662487ea0`

**Implementation ending SHA:** `d84daed488ebe689f3dd47aa283f1f1073578e43`

### Material changes

- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeCanonicalPoseSource.cs` — detailed hands now default OFF in the code-owned live spike composition; the expensive hand source is created lazily only after explicit opt-in, while disabling shuts the optional source/runtime down.
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeHandLandmarkerSource.cs` — detailed tracking also defaults OFF at the source; enable/disable/retry/restart/destruction now share explicit optional-runtime start/stop lifecycle, including coroutine/init cancellation, scheduler/session invalidation, task shutdown, and reusable-buffer release.
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeHandLandmarkerSource.Results.cs` — `_capturePixels` ownership is separated from prepared NativeArray slot ownership. Slot recreation no longer disposes/nulls the managed camera capture array.
- `Assets/GoldenNeedle/Core/Motion/Hands/HandMotionRuntime.cs` — disabling the optional hand runtime clears its cached hand frame so downstream detail cannot retain a stale sample.
- `Docs/foundation-corrective-pass-progress.md` — this Batch 1 record.

### Confirmed null-source root cause

The original `EnsureBuffers(...)` could allocate `_capturePixels` and then call `DisposeBuffers()` while recreating `_slotA` / `_slotB`. `DisposeBuffers()` also set `_capturePixels = null`, after which `CaptureLatestHandFrame()` continued into `WebCamCpuFramePreparation.PrepareRgba(_capturePixels, ...)`. The fix separates prepared NativeArray disposal from managed camera-capture storage and performs full disposal only at real lifecycle shutdown/restart boundaries.

### Effective detailed-hand default

`PoseTrackingSpike.unity` does not serialize `MediaPipeCanonicalPoseSource`; `PoseTrackingSpikePresenter` creates it in code. Therefore `enableDetailedHands = false` is the effective live runtime default without a scene migration. In that default state no `MediaPipeHandLandmarkerSource` is created, so there is no Hand Landmarker task/model verification or download, independent hand `GetPixels32`, hand-frame preparation, inference submission, or recurring detailed-hand CPU work. A lightweight disabled `HandMotionRuntime` remains available so downstream optional-detail discovery does not repeatedly search for it.

Explicit `SetDetailedHandsEnabled(true)` lazily creates/enables the existing optional Hand Landmarker path. Disabling it again stops initialization, invalidates callback/scheduler state, disposes the task and reusable buffers, disables the hand runtime, and clears cached hand data. Retry, coordinate-convention restart, timeout restart, camera-size buffer recreation, and destruction use the corrected ownership/lifecycle rules.

### Validation actually performed

- Foundation D workflow at implementation SHA: run `34958576168`, job `104346448292` — **SUCCESS**.
  - `FOUNDATION_D_HAND_SMOKE=PASS`
  - bundled and official model identity checks PASS
  - shared provider timeline and monotonic timestamp guards PASS
  - bounded latest-only scheduler PASS
  - `BODY_OPENVINO_DEFAULTS_UNCHANGED=PASS`
  - `CANONICAL_BODY_V1_UNCHANGED=PASS`
  - `PHASE4_COMPATIBILITY_SOLVE_PRESERVED=PASS`
  - `FOUNDATION_D_PRODUCTION_APPLICATION_BOUNDARY_PRESERVED=PASS`
  - read-only verification PASS
- Foundation C managed rich-motion smoke at the same implementation SHA — **PASS**. Its separate static audit still fails on pre-existing numeric landmark access in untouched `RichHumanoidDetailRetargeter.cs`; that Foundation E/axial-area issue is outside Batch 1 and was intentionally not changed.
- Start-to-implementation diff contains only the four hand-related source files listed above; no body provider/OpenVINO, scene, CanonicalBodyV1, Phase 4, locomotion, package, or ProjectSettings file was changed.
- Unity Editor compilation/Test Runner and real webcam performance were **not** run by this Builder environment. USER Unity/webcam QA remains decisive.

**Status:** `AWAITING ORCHESTRATOR REVIEW / USER UNITY QA`

**Next work:** explicitly **not authorized yet**. Do not begin speech/microphone repair, rich axial-orientation/Foundation E twist correction, coarse fist/open-hand replacement, Phase 5A work, Phase 6, or broad CI cleanup until the Orchestrator/USER advances the next batch.

## Corrective Foundations Batch 2

**Purpose:** enforce the single-RGB-camera production trust boundary after USER QA showed excessive limb axial fluctuation, anatomically impossible rotations, continuous raised-arm/hand fluctuation, and unreasonable visual foot/leg twisting. Phase 4 remains the authoritative production body orientation solve; Foundation C/E rich limb axial twist is retained only as an experimental/research opt-in.

**Starting remote SHA:** `858e2e2af3e0dbde99bfc213369659f77b935e6e`

**Validated implementation ending SHA:** `41acbb949876e56386df6236d70d32bcee333c35`

### Architectural conclusion and production default

The current Foundation C bases can be mathematically valid from confident non-degenerate landmark geometry while still not uniquely observing free axial rotation. In particular, vectors such as shoulder-to-elbow plus elbow-to-wrist, or hip-to-knee plus knee-to-ankle, constrain a limb bending plane but do not uniquely determine rotation about the proximal limb's own long axis from one RGB view. Foundation E had been treating every full `Observed` basis as actionable production axial twist across eight limb channels, so ambiguous/noisy evidence could become visible axial motion.

`RichHumanoidDetailRetargeter.enableRichLimbAxialDetail` now defaults to `false`, with an explicit experimental/research tooltip. The live `PoseTrackingSpike.unity` scene does not serialize `RichHumanoidDetailRetargeter`; `PoseTrackingSpikePresenter` code-composes it at runtime. Therefore the C# default is authoritative for normal Lab/runtime composition without a scene migration.

This is a sensing/trust-boundary correction, not a smoothing change. Existing `detailResponse`, axial measurement, reflection mapping, endpoint-preservation math, rich orientation channels, and Foundation C solver/schema remain available unchanged for deliberate experiments or future stronger sensing. No Foundation C semantic/code/schema change was made in Batch 2; only the production E application policy and its regression guard changed.

When experimental axial detail is OFF, `HumanoidRetargeter` first rebuilds the accepted Phase 4 body solve for the frame. Foundation E then applies no rich axial twist and clears its rich reference/target/current contribution state. No inverse twist is required because Phase 4 is reconstructed before the optional post-solve layer. If a developer explicitly enables `enableRichLimbAxialDetail`, the existing eight-channel rich axial path still runs. Disabling it again clears E-owned axial state so the next Phase 4 solve remains authoritative and any later re-enable establishes fresh rich references rather than freezing prior E state.

### Material changes

- `Assets/GoldenNeedle/Core/Motion/Retargeting/RichHumanoidDetailRetargeter.cs` — rich limb axial detail is now experimental/default-OFF; existing opt-in path and math are preserved; disabled-path comments document Phase 4 authority and state clearing.
- `Tools/FoundationERetargetingSmoke/ProductionAxialPolicyGuard.cs` — focused deterministic/static guard proves production axial default OFF, explicit opt-in still exists, code-owned scene composition leaves that default authoritative, Phase 4 is applied before E detail, and Batch 1 detailed-hand default OFF remains intact.
- `Docs/foundation-corrective-pass-progress.md` — this Batch 2 record.

### Validation actually performed

- Foundation E workflow at validated implementation SHA `41acbb949876e56386df6236d70d32bcee333c35`: run `34965187181`, job `104367853170`.
  - Foundation A smoke — PASS.
  - Foundation B camera smoke — PASS.
  - Foundation C rich-motion smoke — PASS.
  - Foundation D hand smoke — PASS.
  - Foundation E deterministic retarget smoke — PASS, all 24 checks.
  - `FOUNDATION_E_PRODUCTION_AXIAL_DEFAULT_OFF=PASS`
  - `FOUNDATION_E_EXPERIMENTAL_AXIAL_OPT_IN_AVAILABLE=PASS`
  - `FOUNDATION_E_PHASE4_REBASE_BEFORE_DETAIL=PASS`
  - `FOUNDATION_D_DETAILED_HAND_DEFAULT_OFF_PRESERVED=PASS`
  - endpoint-preservation residual remained `0`.
- The workflow's architecture/locked-scope step still reports the known historical false positive because it compares current HEAD against the old Foundation E base and therefore flags legitimate later files including Batch 1 hand changes and newer handoff/progress documents. No scope guard was weakened. No new substantive smoke, axial-policy, endpoint, or math failure was observed.
- Start-to-validated-implementation diff contains only `RichHumanoidDetailRetargeter.cs` and the focused `ProductionAxialPolicyGuard.cs`. `HumanoidRetargeter.ApplyMotionFrame(...)`, Foundation C solver/schema, `FoundationERetargetMath`, Presenter, scene YAML, body/OpenVINO acquisition/scheduling, Phase 4 math, locomotion/Phase 5A, presentation smoothing, and Batch 1 hand implementation were not modified.
- Unity Editor compilation/Test Runner and real Unity/webcam visual QA were **not** run by this Builder environment. USER Unity QA remains decisive; do not treat the reported visible axial/foot issue as USER-confirmed solved until that QA passes.

**Status:** `AWAITING ORCHESTRATOR REVIEW / USER UNITY QA`

**Next work:** explicitly **not authorized yet**. Do not begin speech/microphone repair, coarse `Open / Closed / Unknown` hand-state work, Phase 5A, Phase 6, broad CI cleanup, or any later corrective batch until the Orchestrator/USER authorizes it.
