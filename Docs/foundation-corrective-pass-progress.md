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

## Corrective Foundations Batch 3

**Purpose:** repair and expose the existing Windows fixed-vocabulary speech-input lifecycle after USER QA reported that configured spoken commands appeared to do nothing. This batch preserves direct speech commands with no wake keyword, keeps `KeywordRecognizer`, and makes one USER runtime test distinguish backend/start/microphone/recognition/policy/dispatch failure stages instead of collapsing them into silence.

**Expected pre-batch baseline from the Builder brief:** `4be712288af09bde826d12e44999215fb2dcda74`

**Actual remote HEAD at this Builder intake:** `8a555d20fd9c2620fde92e7cb54e7794a674b4e0`

The actual intake HEAD was one fast-forward commit ahead of the expected baseline. That commit, `8a555d20fd9c2620fde92e7cb54e7794a674b4e0` (`fix(speech): expose backend and recognition lifecycle`), is a direct child of `4be712288af09bde826d12e44999215fb2dcda74` and contains the coherent Batch 3 speech implementation. It was independently audited rather than overwritten or duplicated.

**Validated Batch 3 implementation SHA:** `8a555d20fd9c2620fde92e7cb54e7794a674b4e0`

### Current speech architecture and confirmed pre-batch weaknesses

Before the Batch 3 implementation, the live architecture was already:

`Windows KeywordRecognizer -> SpeechCommandInput -> SpeechCommandResolver/confidence/cooldown -> GoldenNeedleCommandRouter -> existing command targets`

Keyboard and speech therefore already converged on the shared command router, and the Windows provider contained no direct camera/gameplay behavior. The default `wakePrefix` was empty, so direct fixed-vocabulary phrases were already the intended product behavior.

One concrete lifecycle defect was present: the old `WindowsKeywordSpeechProvider.Start()` called `_recognizer.Start()` and then returned `_recognizer.IsRunning`. If `KeywordRecognizer` had accepted the start request but did not report running immediately while the shared Windows `PhraseRecognitionSystem` was still transitioning, `SpeechCommandInput.Start()` treated that `false` as a hard start failure and immediately unsubscribed/disposed the provider. That could terminate a valid start-in-progress before the phrase system reached its running state.

The old path also exposed only coarse provider status/error text and `IsRunning`; it did not subscribe to `PhraseRecognitionSystem.OnStatusChanged`, did not expose `PhraseRecognitionSystem.Status`, did not distinguish recognizer-created/starting/listening/failed/unsupported/stopped states, and did not surface raw recognition events before policy filtering. Consequently a real Low-confidence recognition, unmapped phrase, cooldown rejection, backend error, or no recognition event could all look similar to the USER as “speech did nothing.”

These findings identify real lifecycle/observability weaknesses, but they do **not** prove the original USER failure had one unique cause. Physical microphone availability, Windows microphone/privacy state, Windows Speech runtime availability, whether the recognizer receives audio, and real spoken-word recognition quality remain USER-runtime questions.

### Lifecycle and diagnostics implemented

- `SpeechCommandInput` now exposes explicit lifecycle states (`Stopped`, `Disabled`, `Created`, `Starting`, `Running`, `Failed`, `Unsupported`, `Disposed`) and retains provider diagnostics even after teardown.
- Raw recognized phrase/confidence is recorded before resolver filtering, with recognition event count, sequence/time, mapping-found state, confidence rejection, cooldown rejection, dispatch-attempt state, command result/status/message, and a compact diagnostic summary.
- Repeated `SpeechCommandInput.Start()` while already starting/running is idempotent and does not create a duplicate provider. Stop removes the phrase callback before provider teardown, blocks stale events from dispatching, and disposes provider resources. Start after Stop creates exactly one fresh provider. Dispose is idempotent.
- `WindowsKeywordSpeechProvider` retains `KeywordRecognizer` and now observes `PhraseRecognitionSystem.isSupported`, `PhraseRecognitionSystem.Status`, `PhraseRecognitionSystem.OnStatusChanged`, `PhraseRecognitionSystem.OnError`, recognizer existence, and `KeywordRecognizer.IsRunning`.
- Phrase-system static events are subscribed once per live provider session and unsubscribed during Stop/restart/Dispose. `OnPhraseRecognized` is likewise removed before native recognizer disposal.
- A successful `KeywordRecognizer.Start()` request is no longer declared failed merely because `IsRunning` is not true in the same call. The provider remains in `Starting` and lets phrase-system status/error callbacks provide the next authoritative transition. No per-frame restart loop was introduced.
- Invalid/empty keyword sets fail diagnostically before recognizer creation. Backend exceptions/errors remain nonfatal to the rest of Golden Needle.
- The Windows recognizer-wide confidence remains `Low`; the existing per-command production minimum (normally `Medium`) remains in `SpeechCommandResolver`, so a Low recognition is visible and then explicitly classified as a confidence rejection rather than silently disappearing.

### Microphone / permission approach

No `Application.RequestUserAuthorization(UserAuthorization.Microphone)` workflow was added for Windows desktop and no `Microphone.Start()` capture session was introduced. The Windows backend uses Unity's phrase-recognition status/error surfaces as the speech authority. `Microphone.devices` is sampled only when the provider starts as low-cost supporting evidence: a zero-device result is useful, while a nonzero device count is explicitly treated as **not proof** that Windows Speech has valid privacy/device access.

Diagnostics record microphone device count, the first enumerated device name when available, and a note explaining that enumeration alone does not prove phrase-recognition access. `SpeechError.MicrophoneUnavailable` is reported distinctly through the phrase-system error path.

### Material changes in the Batch 3 implementation

- `Assets/GoldenNeedle/Core/Commands/SpeechCommandInput.cs` — provider lifecycle state, raw-recognition/policy/dispatch diagnostics, idempotent Start/Stop/restart/Dispose behavior, stale-event blocking, and compact Lab diagnostic summary.
- `Assets/GoldenNeedle/Debug/PoseTrackingSpike/WindowsKeywordSpeechProvider.cs` — retained `KeywordRecognizer`; added phrase-system support/status/error observability, safe static-event ownership, start-in-progress handling, one-time microphone-device diagnostics, keyword validation, and clean native teardown.
- `Tools/FoundationACommandSmoke/Program.cs` — deterministic coverage for dispatched recognition, raw Low-confidence rejection, unmapped recognition, cooldown rejection, unsupported-provider nonfatal behavior, shared-router survival, repeated Start, Stop/restart, stale callback blocking, and idempotent Dispose.
- `.github/workflows/foundation-a-command-system.yml` — narrow Foundation A invariants for phrase-system status/error subscriptions, teardown, configured keywords, no parallel `Microphone.Start`, no fabricated Windows authorization prompt, shared command routing, and Lab diagnostic visibility.
- `Docs/foundation-corrective-pass-progress.md` — this Batch 3 record.

`PoseTrackingSpikePresenter.cs` already routed both keyboard and speech through the same `GoldenNeedleCommandRouter` and already rendered `_speechCommandInput.DiagnosticSummary` in the main Lab diagnostics panel, so no additional Presenter source edit was required by the final Batch 3 implementation diff.

### Validation actually performed

- Unity 6 speech API behavior was independently checked against current Unity documentation: Windows phrase recognition exposes `isSupported`, `Status`, `OnStatusChanged`, `OnError`; `KeywordRecognizer` inherits `IsRunning`, `Start`, `Stop`, and `Dispose`; Unity documents the phrase-recognition system/keyword recognizer as functional on Windows 10. This supports retaining the existing Windows keyword architecture rather than replacing it or inventing a separate audio-capture/permission subsystem.
- Foundation A workflow at implementation SHA `8a555d20fd9c2620fde92e7cb54e7794a674b4e0`: run `34968867815`, job `104379898399` — **SUCCESS**.
  - `FOUNDATION_A_COMMAND_SMOKE=PASS`
  - `CONFIDENCE_COOLDOWN=PASS`
  - `DEFAULT_CAMERA_SPEECH_MAPPINGS=PASS`
  - `RAW_RECOGNITION_DIAGNOSTICS=PASS`
  - `SPEECH_LIFECYCLE_IDEMPOTENT=PASS`
  - `UNSUPPORTED_SPEECH_FALLBACK=PASS`
  - `SHARED_COMMAND_ROUTING=PASS`
  - `FOUNDATION_A_STATIC_AUDIT=PASS`
  - `KEYBOARD_MIGRATION_F1_F12_R_V_C_X_K=PASS`
  - `SPEECH_POLICY_AND_PROVIDER_BOUNDARY=PASS`
  - `SPEECH_WINDOWS_STATUS_ERROR_LIFECYCLE=PASS`
  - `SPEECH_NO_PARALLEL_MIC_CAPTURE_OR_WINDOWS_AUTH_PROMPT=PASS`
  - `SPEECH_LAB_DIAGNOSTICS_SURFACE=PASS`
  - `EXISTING_RUNTIME_AUTHORITIES_PRESERVED=PASS`
  - `FOUNDATION_A_WORKFLOW_READ_ONLY=PASS`
- Foundation B workflow also completed successfully at the same speech implementation SHA (run `34968867834`), providing additional evidence that camera-preset behavior remained intact.
- The implementation commit changed only the four directly speech/Foundation-A files listed above. No body provider/OpenVINO acquisition/scheduling, Phase 3 stabilization, Phase 4 retarget math, locomotion/Phase 5A, presentation smoothing, Batch 1 hand implementation, Batch 2 rich-axial implementation, or Phase 6 source was changed.
- Automated CI cannot establish the USER's Windows microphone privacy settings, physical microphone health, Windows Speech runtime health, or spoken-word recognition quality. Real Unity/Windows speech QA therefore remains decisive and Batch 3 is **not USER accepted**.

### Required USER runtime evidence

In Lab mode with main diagnostics visible, read the `Speech:` line after entering Play Mode and after speaking one configured phrase such as `front view`. The diagnostic now distinguishes examples such as:

- backend unsupported/failed and the last phrase-system error;
- `Starting` versus `Running`, phrase-system state, recognizer running state, keyword count, and microphone-device count;
- `Events=0`, meaning no phrase-recognition event reached the project;
- a raw phrase plus confidence followed by `ConfidenceRejected` or `Unmapped`;
- `CooldownRejected`;
- `Dispatched` or `DispatchFailed` with the command result status/message.

The USER should also disable/re-enable the Presenter/GameObject or otherwise exercise the normal Stop/start lifecycle once and verify the event count does not jump from duplicate callbacks. The exact diagnostic line plus any Unity Console speech error should be returned to the Orchestrator if speech still does not act.

**Status:** `AWAITING ORCHESTRATOR REVIEW / USER UNITY QA`

**Next work:** explicitly **not authorized yet**. Do not begin coarse `Open / Closed / Unknown` hand-state detection, Foundation E CI cleanup, Phase 5A work, Phase 6, or unrelated foundation changes until the Orchestrator/USER authorizes the next batch.
