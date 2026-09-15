# Golden Needle — Current State

Authoritative current-state refresh: 2026-09-15.

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## Governance

- Do **not** merge to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or otherwise rewrite shared branch history just to clean checkpoints.
- GitHub is the shared authoritative project state; the USER normally uses GitHub Desktop for local Git operations.
- The USER performs decisive Unity/manual/runtime QA.
- Preserve accepted Phase 4 body behavior and the optimized body inference path while later foundation/locomotion work is assessed.
- Phase 5A remains implemented but **not USER accepted**. Phase 6 remains **not started**.

## Current branch checkpoint and immediate context

The latest runtime/code checkpoint before this documentation refresh is:

`ee5a64d479c0710a0548cdbe0bbb90bbe80efc40`

That checkpoint contains the narrow Unity compilation corrections exposed by the first comprehensive Foundations A–E local Unity pass.

The USER pulled the branch, reopened the project in Unity `6000.5.0f1`, and confirmed that Unity now opens with **zero red compilation errors**.

Comprehensive Foundations A–E runtime QA has **not yet been completed**. The USER plans to perform it shortly and provide the results to the next Orchestrator.

## Current test configuration — keep fixed for the first comprehensive pass

The USER explicitly chose this QA baseline and should keep it unchanged for the first complete pass:

- Body Reference Downscale: **ON**
- Immediate Launch After Readback: **ON**
- WebCam CPU Pixels: **ON**
- Direct Body CPU Readback: **ON**
- OpenVINO CPU: **ON**
- Presentation Smoothing: **OFF**

Presentation smoothing being OFF is intentional for this pass so actual tracking/retarget behavior is exposed directly rather than hidden by visual interpolation.

## Phase/foundation status

- Phase 1 — MediaPipe provider/raw overlays: **PASS WITH NOTES**.
- Phase 2 — canonical skeleton/debug: **PASS**.
- Phase 3 — stabilization/confidence: **PASS**.
- Phase 4 — humanoid retargeting/calibration/orientation: **USER ACCEPTED — PASS**.
- Motion-engine latency/performance milestone: **CURRENT MILESTONE COMPLETE; FURTHER TUNING DEFERRED**.
- Foundation A — Unified Command System + modular speech input: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA PENDING**.
- Foundation B — Camera View / Focus Preset System: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA PENDING**.
- Foundation C — Rich Canonical Motion / Orientation Architecture: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA PENDING**.
- Foundation D — MediaPipe Hand Landmarker integration: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA PENDING**.
- Foundation E — Orientation-aware + optional hand/finger retarget: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / UNITY COMPILATION PASS / USER MANUAL QA PENDING**.
- Phase 5A — support-foot locomotion / Lab-Game presentation: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.

The approved sequence remains:

```text
Foundations A–E implemented and code-audited
    ↓
One comprehensive USER Unity/manual/runtime QA pass
    ↓
Assess/fix any foundation regressions
    ↓
Only after foundation QA is complete, return to Phase 5A acceptance/fixes
```

Deferred QA never implies USER acceptance.

## Foundation E implementation summary

Foundation E remains an additive post-Phase-4 detail/application layer.

The accepted Phase 4 `HumanoidRetargeter.ApplyMotionFrame(...)` solve remains the positional/IK authority. E does not replace endpoint solving.

Current E architecture:

- generic `IHumanoidPostSolveDetailLayer` boundary;
- `RichHumanoidDetailRetargeter` is the concrete E layer;
- live spike composition is code-owned through `PoseTrackingSpikePresenter.Awake()` rather than a required scene-YAML migration;
- `HumanoidRetargeter.Start()` re-discovers post-solve detail layers after Awake-time composition;
- optional fingers/details do not change `HumanoidRigBinding.IsBound` or required-body `BoundBoneCount` semantics;
- production rich channels are exactly bilateral upper/lower arms and bilateral upper/lower legs;
- pelvis/chest/feet remain deliberately outside E production application for this milestone;
- axial detail is reflection-aware and endpoint-preserving;
- palm orientation is absolute/reference-based rather than cumulative;
- left/right hand state is independent;
- stale/disabled palm/finger contributions return toward reference rather than freezing;
- no E inference, camera acquisition, provider scheduling, FIFO/history/backlog, or second landmark representation exists.

The palm correction uses a stable parent-relative avatar palm reference and cached chain-tip bind/reference local rotation so repeated identical source palms converge instead of accumulating rotation frame after frame.

## Foundation E/D automated evidence before Unity compilation QA

Important accepted automated checkpoints:

- Foundation C corrective workflow at `9e4c4ac3a9d87eade06a43e2833481cce22b70f9`: run `34919859131`, job `104225315113`: **PASS**.
- Foundation D accepted corrective workflow at `864b39a520c78db1a0572b87a7d42c9da5cedaa4`: run `34920144300`, job `104226227007`: **PASS**.
- Foundation D migrated E-compatible application-boundary workflow at `44a39d5ef566b800587a3be73345e843fffe5ac0`: run `34930150745`, job `104256492165`: **PASS**.
- Foundation E exact-head code/math verification at `087068dd14cd4bae11243a5efbd1bd5ee4cd309e`: run `34933354209`, job `104265988449`: **PASS**.
- Foundation D compatibility at the same SHA: run `34933354189`, job `104265988337`: **PASS**.
- Foundation E final runtime-wiring verification at `c91574a7b3557bc749ed74c87e5a46107fd018c8`: run `34935841592`, job `104273421820`: **PASS**.
- Foundation D compatibility at that same SHA: run `34935841602`, job `104273421255`: **PASS**.

The E deterministic smoke at the accepted code checkpoint passed 24/24 checks, including endpoint residual `0`, repeated palm target drift `0°`, stale palm residual `0°`, parent-local palm drift `0°`, and bounded reacquisition behavior.

## Unity compilation QA findings and fixes

The first real Unity compilation gate was valuable because Builder-side .NET/static workflows did **not** compile the complete Unity assembly.

Initial local Unity open entered Safe Mode and exposed a missing namespace import in `RichHumanoidDetailRetargeter.cs`:

- missing `GoldenNeedle.Core.Motion.Rotation` for `CanonicalBoneId`.

That was fixed and pushed at:

`a1a3071da5bda48b53ccfd875e3e569ed48bd6dc`

A second Unity open exposed the remaining visible compiler issues. They reduced to mechanical C# integration problems rather than architecture failures:

1. `ThirdPersonLabCamera.cs`
   - ambiguous `Object` reference between `System.Object` and `UnityEngine.Object`;
   - corrected by qualifying the two `FindAnyObjectByType(...)` calls as `UnityEngine.Object.FindAnyObjectByType(...)`.

2. `HandMotionRuntime.cs`
   - `Debug.LogWarning` resolved against project namespace `GoldenNeedle.Debug`;
   - corrected to `UnityEngine.Debug.LogWarning(...)`.

3. `MediaPipeHandLandmarkerSource.Results.cs`
   - same `Debug.LogWarning` namespace collision;
   - corrected to `UnityEngine.Debug.LogWarning(...)`.

4. `HumanoidRetargeter.cs`
   - same `Debug.LogWarning` namespace collision;
   - corrected to `UnityEngine.Debug.LogWarning(...)`.

5. `RichHumanoidDetailRetargeter.cs`
   - two `CS8156` readonly-reference errors from passing the property expression `hand.palmBasis` with `in`;
   - corrected without changing E math semantics.

The final compile-fix branch checkpoint is `ee5a64d479c0710a0548cdbe0bbb90bbe80efc40`.

After pulling this checkpoint, the USER confirmed **no Unity compilation errors**.

The earlier Visual Studio/Unity UDP port `56662` message was a non-blocking IDE integration warning and was not the Safe Mode cause.

## Current CI nuance after compile fixes

The Foundation E workflow triggered at `ee5a64d479c0710a0548cdbe0bbb90bbe80efc40` as run `34939309536`.

Its substantive deterministic tests passed through:

- Foundation A smoke: **PASS**;
- Foundation B smoke: **PASS**;
- Foundation C smoke: **PASS**;
- Foundation D smoke: **PASS**;
- Foundation E deterministic smoke: **PASS, 24/24**.

The run then failed only in the static locked-scope audit because the E workflow's historical allowlist still treats these legitimate compile-fix files as forbidden Foundation-E-era scope:

- `Assets/GoldenNeedle/Core/Motion/Hands/HandMotionRuntime.cs`
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeHandLandmarkerSource.Results.cs`
- `Assets/GoldenNeedle/Debug/PoseTrackingSpike/ThirdPersonLabCamera.cs`

This is currently classified as a **CI hygiene / static-scope false positive**, not a new runtime or deterministic-test failure.

The next Orchestrator should update the permanent guard narrowly so it permits these already-reviewed compile-only compatibility fixes while preserving the real provider/body/OpenVINO/Phase5A/scene/package protections. Do **not** broadly weaken or remove the guard.

Foundation D run `34939208754` at `c44bd4137900876c38d2ceae475967ed53644e86` completed **SUCCESS** after the relevant D-side compile fixes.

## Current best-tested body runtime path

The established low-end body path remains:

```text
Unity WebCamTexture
  -> WebCamCPU/GetPixels32 reusable acquisition
  -> reusable CPU preparation/downscale to 320x240
  -> one active + one replaceable newest pending body frame
  -> persistent OpenVINO CPU FP32 worker
  -> MediaPipe 0.10.22 pose semantics
  -> 33 normalized + world landmarks
  -> Golden Needle canonical mapping
  -> stable calibration/locomotion path
  -> Phase 4 positional retarget
  -> optional Foundation E post-solve detail
  -> avatar presentation
```

Current rules that must remain preserved:

- stock MediaPipe/TFLite remains available as fallback/reference;
- ExistingReadback remains available as fallback/reference;
- latest useful frame wins; no FIFO/history/replay/catch-up queue;
- Stable Phase 3 canonical filtering remains calibration/locomotion authority;
- Phase 4 signed mapping and analytic two-bone IK remain positional authority;
- hand tracking is optional and must not make body tracking fail;
- optional detail/finger bones must not become required body binding.

## Foundation D hand tracking

Foundation D remains a separate optional MediaPipe Hand Landmarker stream alongside the body provider.

Key current invariants:

- CPU `LIVE_STREAM` Hand Landmarker;
- up to 2 hands;
- default approximately 12 Hz hand cadence;
- reusable 480x360 hand preparation;
- one active hand inference + at most one replaceable newest pending snapshot;
- project-owned 21-landmark semantic contract;
- same body-provider Stopwatch epoch for body/hand semantic timing;
- left/right freshness independent;
- official bundled hand model retained;
- D produces data only; E owns optional avatar detail application.

## Foundation A/B/C current expectations for QA

Foundation A manual QA should cover keyboard command routing, real microphone speech recognition, cooldown, and shared command behavior.

Foundation B manual QA should cover all camera presets (`Back`, `Front`, `Left`, `Right`, `FullBody`, `Hands`, `LeftHand`, `RightHand`), Lab/Game orthogonality, focus fallback, retained heading, and visual camera stability.

Foundation C manual QA should cover partial-body independence, rich limb twist/ambiguity behavior, no sudden twist flips, and preservation of accepted Phase 4 endpoint geometry.

## Foundation E current expectations for QA

Manual QA should confirm at minimum:

- `RichHumanoidDetailRetargeter` is automatically present at runtime; do not manually add it;
- fixed non-neutral palm poses settle instead of accumulating rotation;
- stale/lost hands return optional palm/finger contribution toward baseline rather than freezing;
- reacquisition is controlled and establishes a new zero reference;
- left/right hand behavior remains independent;
- optional finger articulation does not corrupt body binding;
- E category/master toggles remove only E-owned detail and preserve Phase 4 body behavior;
- F5/reset/recalibration clears stale E state;
- rich axial detail does not visibly move Phase 4 limb endpoints.

## Phase 5A remains out of scope for the foundation QA decision

Known Phase 5A issues remain unresolved and should not be accidentally counted as Foundation A–E regressions:

- planted-feet leaning can still cause unwanted translation;
- stationary cadence stepping is not yet robust enough.

Do not start fixing Phase 5A during the foundation QA session. Collect foundation evidence first.

## Documentation authority

Use these documents in this order:

1. `Docs/current-state.md` — current status and immediate governance.
2. `Docs/decisions.md` — architecture/product decisions; where older wording conflicts with current-state, current-state wins.
3. `Docs/orchestrator-handoff.md` — current handoff/resume brief for the next Orchestrator.
4. `Docs/pre-phase5a-foundations.md` — foundation architecture/requirements.
5. `Docs/architecture.md` and `Docs/motion-engine.md` — accepted detailed architecture/phase design.

Historical worker briefs and experiment progress files do not override newer current-state documentation.

## Immediate next step

The USER will perform the comprehensive Foundations A–E Unity/manual/runtime QA using the fixed baseline configuration listed above and will provide the results to the next Orchestrator.

The next Orchestrator must:

1. inspect the live branch and these docs before acting;
2. ingest the USER QA result as the decisive runtime evidence;
3. distinguish Foundation A–E failures from already-known Phase 5A issues;
4. fix only genuine regressions/blockers;
5. correct the Foundation E workflow's compile-fix scope false positive narrowly;
6. only after the comprehensive foundation QA is assessed and accepted should Phase 5A work resume;
7. do not merge to `main` without explicit USER approval.
