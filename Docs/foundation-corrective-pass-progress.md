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

**Status:** `CODE-AUDIT PASS / USER POSE-QUALITY QA DEFERRED`

**Next work:** pose-quality runtime QA remains deliberately deferred until the USER is in a stable environment. Do not alter the Batch 2 production trust boundary without new USER evidence.

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

These findings identify real lifecycle/observability weaknesses, but they do **not** prove the original USER failure had one unique cause. Physical microphone availability, Windows microphone/privacy state, Windows Speech runtime availability, whether the recognizer receives audio, and real spoken-word recognition quality were USER-runtime questions at implementation time.

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

### Post-Batch-3 USER QA

On the Windows proof machine the phrase-recognition backend and `KeywordRecognizer` ran successfully. Spoken configured commands were recognized and dispatched through the shared command router. Low-confidence recognition was common mainly under substantial bus/environment noise; speaking louder and more clearly produced successful command cases. The USER accepted Batch 3 / Foundation A speech.

**Final Batch 3 status:** `USER ACCEPTED`

## Corrective Foundations Batch 4A

**Purpose:** establish a zero-extra-inference, provider-independent coarse hand-state signal (`Unknown / Open / Closed`) from the already-produced body pose observation and make the signal observable for USER QA. No avatar finger deformation is applied in this batch.

**Starting remote SHA:** `479229daaa39578c10a69d440c03ccc641e30cd2`

**Validated implementation SHA:** `d060083395918c19cb302346dc0938dc3f7835eb`

### Data source and verified semantic landmarks

`PoseObservation` already contains the accepted 33 MediaPipe Pose landmarks with normalized coordinates, world coordinates, visibility, presence, tracked/unavailable trust, source timestamp, and receive time. Batch 4A reuses that already-produced observation; it does not expand `CanonicalPoseFrame V1`.

The exact body-pose semantic landmarks used for coarse hands are:

- left elbow `13`, left wrist `15`, left pinky `17`, left index `19`, left thumb `21`;
- right elbow `14`, right wrist `16`, right pinky `18`, right index `20`, right thumb `22`.

Those numeric MediaPipe indices are isolated in `MediaPipeCoarseHandEvidenceMapper.cs`. Core hand-state logic consumes semantic elbow/wrist/pinky/index/thumb evidence and contains no MediaPipe provider knowledge.

### Provider-independent contract and estimator

`Core/Motion/Hands` now contains an optional coarse-hand capability with:

- `CoarseHandState`: `Unknown`, `Open`, `Closed`;
- per-side source timestamp and receive time;
- stable and instantaneous state;
- Unknown reason;
- evidence strength;
- normalized diagnostic metrics;
- `ICoarseHandStateSource` so future providers can supply the same capability without MediaPipe knowledge downstream.

The deterministic estimator uses world-space geometry converted through the existing canonical coordinate convention. Forearm length `|wrist - elbow|` is the scale denominator. The normalized metrics are:

- wrist-to-index extension / forearm length;
- wrist-to-pinky extension / forearm length;
- index-to-pinky spread / forearm length;
- wrist-to-thumb extension / forearm length;
- minimum landmark confidence across elbow/wrist/pinky/index/thumb.

Default Open evidence requires both index and pinky extension to be at least `0.55` forearm lengths and index-pinky spread at least `0.30`. Default Closed evidence is deliberately stricter: minimum landmark confidence at least `0.70`, both index/pinky extensions no greater than `0.38`, spread no greater than `0.22`, and supporting thumb extension no greater than `0.45`. The interval between Open and Closed thresholds is intentionally `Unknown`.

The general minimum landmark confidence is `0.55`. Missing/untracked landmarks, missing world positions, non-finite values, forearm length below `0.04 m`, insufficient confidence, and ambiguous geometry all produce explicit `Unknown` reasons rather than forcing a hand state. False Closed is intentionally disfavored.

### Temporal stability and freshness

Left and right states are independent. The tracker consumes a body source timestamp only once; reprocessing the same timestamp refreshes age only and cannot satisfy acquisition/change confirmation. If timestamps move backwards after a provider restart, temporal confirmation state resets.

Default acquisition/change requires `2` fresh consistent samples. Therefore one contradictory frame does not normally flip a stable Open hand directly to Closed or vice versa. Two fresh Unknown/lost samples clear a held stable state. Any result older than `350 ms` is immediately exposed as `Unknown / Stale`. No history queue, replay buffer, or long smoothing window exists.

### Zero-extra-inference proof and integration

`MediaPipeCanonicalPoseSource.TryCopyLatestCanonicalPose(...)` continues to perform the single existing `provider.CopyLatestObservation(_observation)` call. Immediately after that copy, Batch 4A maps coarse semantic evidence from the same in-memory observation and performs only vector/confidence arithmetic before normal canonical mapping. Duplicate-timestamp handling prevents render-frame reuse from becoming fake new evidence.

The Batch 4A path introduces:

- zero additional webcam reads;
- zero additional `WebCamTexture.GetPixels32` calls;
- zero RenderTexture/AsyncGPUReadback operations;
- zero Hand Landmarker tasks/model initialization;
- zero additional neural inference;
- zero inference queues/mailboxes/workers.

The architecture guard verifies those capture/inference mechanisms are absent from the coarse contract, estimator, provider mapper, source update method, and Lab diagnostics. It also verifies `PoseObservation`, `MediaPipePoseProvider`, WebCam CPU preparation, CanonicalBodyV1, Phase 4 rotation, locomotion, `HandMotionRuntime`, Batch 2 retarget detail, and Batch 3 speech files were not changed relative to the Batch 4A baseline.

Batch 1 remains intact: `enableDetailedHands = false` is unchanged, and the lightweight coarse path does not instantiate `MediaPipeHandLandmarkerSource`. Batch 2 remains intact: `enableRichLimbAxialDetail = false` is unchanged. Batch 3 speech implementation is functionally untouched.

### Lab diagnostics

A small read-only `CoarseHandLabDiagnostics` component attaches once to the existing live pose-source object after scene load. It resolves its references once and displays only while the existing main Lab diagnostics are visible and Game View is not active. It does not write to the avatar or command system.

The strip reports stable state, evidence strength, instantaneous/raw state, Unknown reason when applicable, and short normalized index/pinky/spread metrics, for example:

`Coarse hands: L=Open 0.78 raw=Open i/p/s=... | R=Unknown 0.00 raw=Unknown(LowConfidence) i/p/s=...`

### Material changes

- `Assets/GoldenNeedle/Core/Motion/Hands/CoarseHandState.cs` — provider-independent coarse contract/settings/frame/source interface.
- `Assets/GoldenNeedle/Core/Motion/Hands/CoarseHandStateEstimator.cs` — deterministic geometry estimator and lightweight per-side temporal stability/staleness policy.
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeCoarseHandEvidenceMapper.cs` — provider-boundary semantic extraction and the only Batch 4A numeric MediaPipe hand indices.
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeCanonicalPoseSource.cs` — reuses the already-copied body observation to update coarse state and exposes the optional coarse source boundary.
- `Assets/GoldenNeedle/Debug/PoseTrackingSpike/CoarseHandLabDiagnostics.cs` — compact read-only Lab signal display; no rig application.
- `Tools/FoundationDHandSmoke/CoarseHandStateSmoke.cs` — deterministic Open/Closed/Unknown, confidence, invalid geometry, scale, independence, duplicate timestamp, hysteresis, loss, and stale-state tests.
- `Tools/FoundationDHandSmoke/FoundationDHandSmoke.csproj` — includes the provider-independent coarse contract/estimator in the managed smoke build.
- `.github/workflows/foundation-d-coarse-hand.yml` — focused zero-extra-inference/prior-batch/no-avatar-application architecture guard.
- Unity `.meta` files for the newly added runtime scripts.
- `Docs/foundation-corrective-pass-progress.md` — Batch 2 status clarification, Batch 3 USER acceptance, and this Batch 4A record.

### Validation actually performed

- Focused Batch 4A workflow at implementation SHA `d060083395918c19cb302346dc0938dc3f7835eb`: run `34977574085`, job `104409135966` — **SUCCESS**.
  - Foundation A command/speech smoke PASS, preserving the accepted shared speech/command behavior.
  - `COARSE_HAND_GEOMETRY=PASS`
  - `COARSE_HAND_CONFIDENCE_AMBIGUITY=PASS`
  - `COARSE_HAND_SCALE_INVARIANT=PASS`
  - `COARSE_HAND_LEFT_RIGHT_INDEPENDENT=PASS`
  - `COARSE_HAND_TEMPORAL_STABILITY=PASS`
  - `COARSE_HAND_DUPLICATE_TIMESTAMP=PASS`
  - `COARSE_HAND_STALE_TO_UNKNOWN=PASS`
  - existing `FOUNDATION_D_HAND_SMOKE=PASS`
  - `COARSE_HAND_ZERO_EXTRA_INFERENCE=PASS`
  - `COARSE_HAND_POSE_SEMANTIC_BOUNDARY=PASS`
  - `COARSE_HAND_CANONICAL_BODY_V1_UNCHANGED=PASS`
  - `BATCH1_DETAILED_HAND_DEFAULT_OFF=PASS`
  - `BATCH2_RICH_AXIAL_DEFAULT_OFF=PASS`
  - `BATCH3_SPEECH_UNCHANGED=PASS`
  - `BATCH4A_NO_AVATAR_FINGER_APPLICATION=PASS`
  - `COARSE_HAND_LAB_DIAGNOSTICS=PASS`
  - `FOUNDATION_D_COARSE_WORKFLOW_READ_ONLY=PASS`
- Existing full Foundation D workflow at the same implementation SHA: run `34977574058`, job `104409134678` — **SUCCESS**, including Foundation A/B/C/D managed smokes, model identity checks, shared-timeline/bounded-scheduler checks, permanent application-boundary audit, and read-only verification.
- Concurrent Foundation C workflow run `34977574116`, job `104409135807` ran Foundation A, B, and C managed smokes successfully, then failed only on the already-known historical static audit `raw numeric/provider landmark-index access leaked downstream: .../RichHumanoidDetailRetargeter.cs`. Batch 4A did not modify that file or weaken/fix the unrelated guard.
- Builder-side CI proves deterministic estimator behavior and the zero-extra-inference architecture. It cannot prove that MediaPipe Pose's sparse pinky/index/thumb body landmarks reliably distinguish real fists from open hands under the USER's webcam, side-on views, foreshortening, occlusion, subject distance, motion blur, or lighting. Real Unity signal QA remains decisive.

No avatar finger/palm/wrist rotation is driven from `CoarseHandState` in Batch 4A. Existing detailed-hand articulation is untouched.

**Status:** `AWAITING ORCHESTRATOR REVIEW / USER COARSE-HAND SIGNAL QA`

**Batch 4B avatar finger application NOT AUTHORIZED YET.**

Do not begin Batch 4B finger deformation, further detailed-hand work, Batch 2 pose changes, CI hygiene cleanup, Phase 5A, or Phase 6 until the Orchestrator/USER explicitly advances the next batch.

## Corrective Pose Baseline Restoration

**Purpose:** restore normal production pose/body execution to the previously accepted optimization-era Phase 3 + Phase 4 architecture, remove the USER-rejected Batch 4A coarse-hand experiment, disconnect later Foundation C/D/E pose-detail systems from normal runtime, clarify F7 body acquisition/readback diagnostics, and lower all product-default speech mappings to Low confidence.

**Starting remote SHA:** `346002cdecef8a17609a9a757ea6f0015d705172`

**Implementation SHA before this documentation commit:** `494cc865d569a418ad184b948269553bafca6055`

### USER runtime evidence and decision

After Batch 4A, the USER observed approximately `15 FPS` camera capture while testing with the full body visible, compared with the previously accepted optimized behavior in the high-20s / approximately `28–30 FPS` fresh-pose range. This observation is sufficient for the product decision to stop extending the later pose/detail/hand stack, but it does **not** prove that Batch 4A's coarse vector arithmetic by itself caused the slowdown. No controlled A/B isolation was performed at Builder time.

The USER explicitly chose to restore production pose mechanics to the accepted optimization-era baseline rather than continue Batch 4B. Batch 4B was never implemented, and Batch 4A never contained avatar fist/finger deformation.

### Historical baseline references

The principal historical composition reference is commit `8908ca580c8c7251238f75191345d4bf2b5fd04e`, the direct state immediately before Foundation C first entered the branch. Foundation C begins in the following commit, `39eb0b589c43a0b624f1e6e810a4adfa80f908a6` (`feat: add Foundation C rich motion contracts`). The exact pre-Foundation-C production file blobs restored from `8908ca...` are:

- `MotionEngineRuntime.cs` — `766632dd5c08698a1bdde66fd36f322bfdcd9723`;
- `MediaPipeCanonicalPoseSource.cs` — `6e599a2e931deba3dc23bd587005fff7c7a6a6c9`;
- `HumanoidRetargeter.cs` — `ecc95aaf5104bcb011cb0436d16ceb7531104336`;
- `PoseTrackingSpikePresenter.cs` — `41a8fe8adb1a750fbf570d3473e30a34073ab5fe`.

This reference matches `Docs/optimization-orchestrator-handoff.md` because its `MediaPipePoseProvider.cs` is already the exact same optimized provider blob used at restoration intake: `e3f7a55caef9bbe27cd5d4485fc67e096b28c5b8`. Therefore WebCamCPU/GetPixels32 acquisition, reusable CPU preparation/downscale, accepted 320×240 body input, persistent OpenVINO CPU FP32 worker, immediate/newest-only scheduling, two-slot mailbox semantics, MediaPipe Pose semantics, and the accepted optimized provider path were already present before Foundation C/D/E composition was added. The accepted `CanonicalRotationSolver.cs` also remains the same historical/current blob `286442132f864e10907d1c68db68e6e0903aa6b1`, and `CanonicalPoseFrame.cs` remains the 20-joint CanonicalBodyV1 blob `9a0d4c99fb33f2192bcd3937d0ce72de3f2756a5`.

No old whole tree was checked out. These individual files were restored with new forward commits so later unrelated Foundation A/B command/camera work and Batch 3 speech lifecycle/diagnostics remain available.

### Restored production pose authority

Production pose execution is again the optimization-era authority chain:

`optimized body acquisition/inference -> canonical mapping -> Phase 3 stabilization -> stable calibration/avatar-drive authority -> Phase 4 retargeting -> presentation`.

- **Foundation C:** `MotionEngineRuntime` no longer resolves `IRichMotionEvidenceSource`, stores a rich-evidence frame, runs `RichAnatomicalOrientationSolver`, or calls `UpdateRichMotion()` every body frame. `MediaPipeCanonicalPoseSource` no longer implements or maps rich evidence. Foundation C research types/files remain in the repository but are dormant in normal production composition.
- **Foundation D:** the normal `MediaPipeCanonicalPoseSource` no longer implements `ICanonicalHandSource`, automatically creates `HandMotionRuntime`, or creates/enables `MediaPipeHandLandmarkerSource`. Therefore normal runtime has no detailed-hand model setup/download/verification, separate hand `GetPixels32`, 480×360 hand preparation, hand scheduler, periodic Hand Landmarker inference, or hand-related body binding requirement. The detailed-hand research files remain in the repository, preserving the useful Batch 1 lifecycle/buffer fixes, but are not automatically instantiated by normal production composition.
- **Foundation E:** `HumanoidRetargeter` is restored to the Phase 4 + presentation form with no post-solve detail-layer registry/application. `PoseTrackingSpikePresenter` no longer auto-adds `RichHumanoidDetailRetargeter`. Foundation E research files remain available for explicit future research only; they do not sit in the normal `Phase 4 solve -> presentation` path.

### Batch 4A rejection and forward rollback

Batch 4A's Builder-side deterministic/architecture checks had passed at `d060083395918c19cb302346dc0938dc3f7835eb`, but subsequent USER runtime evaluation rejected the feature in the low-end production experience. Approximately `15 FPS` camera capture was observed during full-body testing, and the USER chose baseline restoration instead of further coarse-hand work. This does not establish Batch 4A arithmetic as the sole performance cause.

The Batch 4A runtime/test/workflow files were removed in a forward rollback, its `FoundationDHandSmoke.csproj` wiring was restored to the pre-4A form, and all coarse-hand interfaces/wiring were removed from `MediaPipeCanonicalPoseSource`. Historical Batch 4A documentation above is intentionally retained. Coarse-hand work is deferred. Avatar fist deformation was never part of Batch 4A.

### Speech sensitivity policy

Batch 3's `KeywordRecognizer`, phrase-system lifecycle/error/status handling, raw recognition diagnostics, cooldown, shared command routing, microphone diagnostics, and configurable empty `wakePrefix` remain intact.

Only the product-default mapping policy changed: all 14 mappings created by `SpeechCommandConfiguration.CreateDefault()` now explicitly require `SpeechRecognitionConfidence.Low`:

- `begin calibration`
- `reset calibration`
- `recenter`
- `retry tracking`
- `game view`
- `lab view`
- `back view`
- `front view`
- `left view`
- `right view`
- `full body view`
- `hands view`
- `left hand view`
- `right hand view`

The generic/custom `SpeechCommandMapping` default remains `Medium`; only the product defaults were deliberately lowered. No wake phrase was added. `wakePrefix` remains configurable and empty by default.

### F7 acquisition/readback clarification

The provider contains two distinct concepts: body-frame acquisition (`WebCamCPU/GetPixels32` versus `ExistingReadback`) and the implementation used only by ExistingReadback (`Homuler`, `DirectCPU`, or fallback). The existing provider status text can still include an ExistingReadback implementation label even when that implementation is not the active body acquisition source, which made `Readback: Homuler` ambiguous.

A presentation-only `PoseAcquisitionLabDiagnostics` F7 overlay now reads the provider's requested/active acquisition mode, fallback reason, active ExistingReadback implementation, active backend, and body input size without changing acquisition behavior. Its intended semantics are:

- optimized active path: `Acquisition: WebCamCPU/GetPixels32`, `Readback: N/A (WebCamCPU active)`, `Fallback: none`;
- requested WebCamCPU with real fallback: `Acquisition: ExistingReadback (WebCamCPU fallback)`, actual `Readback: <implementation>`, and the actual fallback reason;
- deliberately selected ExistingReadback: `Acquisition: ExistingReadback` plus its genuine implementation.

The diagnostic performs no camera read, neural inference, queueing, or body-path mutation.

### Files changed / removed

Restored/surgically changed production files:

- `Assets/GoldenNeedle/Core/Motion/Runtime/MotionEngineRuntime.cs`
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeCanonicalPoseSource.cs`
- `Assets/GoldenNeedle/Core/Motion/Retargeting/HumanoidRetargeter.cs`
- `Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpikePresenter.cs`
- `Assets/GoldenNeedle/Core/Commands/SpeechCommandPolicy.cs`
- `Tools/FoundationACommandSmoke/Program.cs`
- `Tools/FoundationDHandSmoke/FoundationDHandSmoke.csproj`
- `.github/workflows/foundation-a-command-system.yml`
- `Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseAcquisitionLabDiagnostics.cs` and `.meta`
- `.github/workflows/pose-baseline-restoration.yml`
- `Docs/foundation-corrective-pass-progress.md`

Removed rejected Batch 4A files:

- `.github/workflows/foundation-d-coarse-hand.yml`
- `Assets/GoldenNeedle/Core/Motion/Hands/CoarseHandState.cs` and `.meta`
- `Assets/GoldenNeedle/Core/Motion/Hands/CoarseHandStateEstimator.cs` and `.meta`
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeCoarseHandEvidenceMapper.cs` and `.meta`
- `Assets/GoldenNeedle/Debug/PoseTrackingSpike/CoarseHandLabDiagnostics.cs` and `.meta`
- `Tools/FoundationDHandSmoke/CoarseHandStateSmoke.cs`

`MediaPipePoseProvider.cs`, `WebCamCpuFramePreparation.cs`, `CanonicalPoseFrame.cs`, `CanonicalRotationSolver.cs`, locomotion/Phase 5A files, Batch 3 lifecycle/backend files, scene YAML, packages, and ProjectSettings were not modified.

### Validation and CI status

Builder-side repository inspection confirms exact historical blob identity for the restored production-composition files and unchanged optimized provider/Phase 4/CanonicalBodyV1 files. A new read-only `pose-baseline-restoration.yml` guard was added to check the exact baseline blobs, optimized WebCamCPU/OpenVINO/two-slot scheduling markers, absence of Foundation C/D/E production composition, Batch 4A removal, all-14-Low speech defaults, and F7 acquisition/readback semantics. Foundation A deterministic smoke was extended to verify the exact 14 phrases/commands/parameters, empty wake prefix, Low resolution for every product-default phrase, and preserved Batch 3 lifecycle tests.

GitHub Actions did **not** execute the triggered jobs during this restoration window. Runs at pose-restoration commit `5f5fdd5c60b4f55b364b067f4e32a308f58ba637` and implementation commit `494cc865d569a418ad184b948269553bafca6055` all terminated before checkout with empty step lists. This affected established Foundation A/B and historical C/D/E workflows as well as the new restoration workflow, so it is a runner/platform-start failure rather than a substantive test assertion failure. A manual rerun of the restoration job reproduced the same zero-step termination. No historical C/D/E scope guard actually executed in these runs; therefore no new C/D/E guard result is claimed.

Relevant run IDs at `494cc865...`:

- Corrective pose baseline restoration: run `35007661103`, initial job `104511392005`; manual rerun job `104512149328` — both failed before any step executed.
- Foundation A command system: run `35007660921`, job `104511391227` — failed before any step executed.
- Foundation B camera presets: run `35007661041`, job `104511391679` — failed before any step executed.

Relevant restoration-commit runs at `5f5fdd5c...` likewise terminated before execution, including Foundation C run `35007178264`, Foundation D run `35007178478`, and Foundation E run `35007178361`. This task intentionally did not alter those historical research workflows merely to make them green after their runtime integrations were retired.

Because the Actions runner did not start, the new/updated managed smoke assertions remain **not executed in CI for this restoration commit**. Previous accepted workflow results recorded above remain historical evidence only, not proof of this new restoration. Unity Editor compilation/Test Runner, actual webcam cadence, and Windows speech sensitivity after the Low-default change also remain USER/runtime checks.

### Required USER runtime QA

The decisive USER test is:

1. Pull the final `engine/pose-tracking-spike` HEAD and open the normal Pose Tracking Spike scene.
2. Press F7 and verify the authoritative body-path block reports `Acquisition: WebCamCPU/GetPixels32`, `Readback: N/A (WebCamCPU active)`, OpenVINO CPU backend, and the accepted `320x240` body input. If it instead reports `ExistingReadback (WebCamCPU fallback)`, capture the exact fallback reason rather than assuming the implementation label is the active acquisition path.
3. With the full body visible, measure camera capture cadence and fresh-pose cadence and compare with the previously accepted approximately `28–30 FPS` / fresh-pose range. No Builder claim of restored FPS is made before this test.
4. Verify the original accepted Phase 4 body behavior, signed-axis orientation, analytic IK, partial-body calibration, and stable calibration behavior are restored without later rich/hand/detail influence.
5. Speak the configured commands at normal volume and verify Low-confidence recognitions now resolve/dispatch where mapping and cooldown permit, while the default wake prefix remains absent.
6. Confirm no coarse-hand diagnostic appears and no detailed-hand/rich/post-Phase-4 experimental subsystem is visibly affecting normal production pose behavior.

**Status:** `AWAITING ORCHESTRATOR REVIEW / USER OPTIMIZED-BASELINE RUNTIME QA`

Do not begin another coarse-hand implementation, Batch 4B, detailed-hand optimization, new rich-orientation work, Phase 5A fixes, Phase 6, or general CI cleanup until the Orchestrator/USER explicitly advances the project.