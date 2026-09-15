# Golden Needle — New Orchestrator Handoff

Handoff date: 2026-09-15

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## 1. First actions for the new Orchestrator

Before changing anything:

1. fetch/inspect the live remote `engine/pose-tracking-spike` branch;
2. read `Docs/current-state.md` first;
3. read `Docs/optimization-orchestrator-handoff.md` second;
4. read this handoff;
5. read `Docs/decisions.md` when a locked/current architectural decision is relevant;
6. inspect any USER QA evidence supplied with the new conversation;
7. independently verify repository/worker claims rather than accepting summaries at face value.

The previous Orchestrator owned **both** the low-end optimization/OpenVINO track and the later Foundations A–E track. Foundations A–E were built on top of the optimized body pipeline; they are not a separate replacement architecture. The dedicated optimization handoff is therefore mandatory reading before changing inference, acquisition, scheduling, responsiveness, or performance behavior.

At the time this handoff was prepared, the last runtime/code checkpoint before documentation-only commits was:

`ee5a64d479c0710a0548cdbe0bbb90bbe80efc40`

The branch then received documentation-only handoff commits. Verify the actual live remote HEAD before acting rather than assuming the SHA in this paragraph is still the branch tip.

## 2. Governance — do not violate

- Do **not** merge to `main` without explicit USER approval.
- Do not force-push, rebase, amend, reset, or rewrite shared history merely to make checkpoint history cleaner.
- GitHub is shared authoritative state; USER normally works through GitHub Desktop.
- One builder should mutate the long-lived engine branch at a time.
- USER performs decisive Unity/manual/runtime QA.
- Phase 4 is USER accepted and must be preserved.
- Phase 5A is implemented but **NOT USER accepted**.
- Phase 6 is **NOT STARTED**.
- Do not resume Phase 5A work until the comprehensive Foundations A–E QA gate has been assessed.
- Do not reopen accepted optimization decisions merely to chase small benchmark gains. Reopen them only when new reproducible evidence identifies a real bottleneck/regression.

## 3. Mandatory optimization context

Read `Docs/optimization-orchestrator-handoff.md` before touching performance code.

The current best-tested low-end body path is conceptually:

```text
Unity WebCamTexture
-> WebCamCPU/GetPixels32 reusable acquisition
-> reusable CPU resize/orientation preparation to 320x240
-> bounded two-slot latest-frame mailbox
-> one persistent OpenVINO CPU FP32 worker
-> MediaPipe 0.10.22 preprocessing/tracking/decode/world semantics
-> 33 normalized + world landmarks
-> Golden Needle canonical body
-> stable calibration/locomotion authority
-> selectable avatar-drive filtering
-> Phase 4 positional/IK retarget
-> optional Foundation E detail
-> presentation
```

Critical preserved optimization decisions:

- OpenVINO CPU FP32 is the best-tested low-end accelerated backend;
- stock MediaPipe/TFLite remains fallback/reference;
- `WebCamCpuPixels`/GetPixels32 is the best-tested OpenVINO acquisition path;
- ExistingReadback remains fallback/reference;
- Immediate Launch After Readback remains accepted;
- the OpenVINO worker/mailbox is one active inference + at most one replaceable newest pending frame;
- no FIFO/history/replay/catch-up queue;
- latest useful frame wins;
- body input remains approximately 320x240 for the current low-end baseline;
- Raw avatar drive is the subjective latency reference;
- Stable remains stability/calibration/locomotion authority;
- responsive A/B/C/D avatar-only profiles remain preserved, with final winner/default intentionally deferred;
- Presentation Smoothing is downstream visual behavior, not a substitute for inference optimization.

The optimization handoff contains the full chronology, measurements, Sentis rejection, OpenVINO Gate A/B evidence, native integration, scheduling fix, WebCamCPU/readback fix, responsiveness experiments, diagnosis order, and locked boundaries.

## 4. Current phase/foundation status

- Phase 1: **PASS WITH NOTES**.
- Phase 2: **PASS**.
- Phase 3: **PASS**.
- Phase 4: **USER ACCEPTED — PASS**.
- Motion-engine latency/performance milestone: **CURRENT MILESTONE COMPLETE; further tuning deferred**.
- Foundation A: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA PENDING**.
- Foundation B: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA PENDING**.
- Foundation C: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA PENDING**.
- Foundation D: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / USER MANUAL QA PENDING**.
- Foundation E: **IMPLEMENTED / ORCHESTRATOR CODE-AUDITED / UNITY COMPILATION PASS / USER MANUAL QA PENDING**.
- Phase 5A: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.

The USER is about to run the single comprehensive A–E Unity/manual/runtime QA pass and will provide the results to the new Orchestrator.

## 5. Current QA baseline — preserve for the first complete pass

The USER explicitly enabled and intends to keep the following fixed during the first comprehensive test pass:

- Body Reference Downscale: **ON**
- Immediate Launch After Readback: **ON**
- WebCam CPU Pixels: **ON**
- Direct Body CPU Readback: **ON**
- OpenVINO CPU: **ON**
- Presentation Smoothing: **OFF**

Do not ask the USER to change these during the first pass merely to hide a failure.

Presentation smoothing is intentionally OFF so real tracking/retarget behavior is visible directly.

This is a test of Foundations A–E **on top of the accepted optimized body baseline**. If performance regresses, use the diagnosis procedure in `Docs/optimization-orchestrator-handoff.md` before changing architecture.

## 6. Important recent Unity compilation event

The first true local Unity compilation gate caught problems that Builder-side deterministic/static CI had not caught.

Unity version: `6000.5.0f1`.

### First Safe Mode error

`RichHumanoidDetailRetargeter.cs` failed because `CanonicalBoneId` was referenced without importing:

`GoldenNeedle.Core.Motion.Rotation`

This was fixed at:

`a1a3071da5bda48b53ccfd875e3e569ed48bd6dc`

### Second Safe Mode batch

After that import was fixed, Unity exposed the remaining visible compiler errors:

- two `Object` ambiguities in `ThirdPersonLabCamera.cs` (`System.Object` vs `UnityEngine.Object`);
- three `Debug.LogWarning` namespace collisions against project namespace `GoldenNeedle.Debug` in:
  - `HandMotionRuntime.cs`;
  - `MediaPipeHandLandmarkerSource.Results.cs`;
  - `HumanoidRetargeter.cs`;
- two `CS8156` errors from passing property expression `hand.palmBasis` using `in` inside `RichHumanoidDetailRetargeter.cs`.

They were fixed mechanically, without intended runtime-semantic redesign:

- camera lookups use `UnityEngine.Object.FindAnyObjectByType(...)`;
- warnings use `UnityEngine.Debug.LogWarning(...)`;
- the illegal readonly-ref property argument pattern was removed while preserving the same E math call semantics.

Final compile-fix checkpoint:

`ee5a64d479c0710a0548cdbe0bbb90bbe80efc40`

After pulling that checkpoint, the USER confirmed **Unity opens with zero red compilation errors**.

Therefore local Unity compilation is currently **PASS**.

The earlier Visual Studio/Unity UDP port `56662` warning was non-blocking IDE integration noise and was not the Safe Mode cause.

## 7. Known CI hygiene issue at the current code checkpoint

Foundation E workflow run:

- run `34939309536`
- head `ee5a64d479c0710a0548cdbe0bbb90bbe80efc40`
- overall result: **FAILURE**

This failure is presently understood as a static scope-guard false positive, not a deterministic regression.

The workflow actually completed and passed:

- Foundation A smoke;
- Foundation B smoke;
- Foundation C smoke;
- Foundation D smoke;
- Foundation E deterministic smoke;
- all 24 E deterministic checks.

It failed later because the historical Foundation E locked-scope allowlist treats three legitimate compile-fix files as forbidden scope:

- `Assets/GoldenNeedle/Core/Motion/Hands/HandMotionRuntime.cs`
- `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipeHandLandmarkerSource.Results.cs`
- `Assets/GoldenNeedle/Debug/PoseTrackingSpike/ThirdPersonLabCamera.cs`

The exact failure was the E static audit reporting those files as unexpected Foundation-E scope.

Do not broadly disable this guard. Update it narrowly after/alongside the QA assessment so these already-reviewed compile-only compatibility fixes are allowed while real protections remain intact:

- body/OpenVINO paths;
- CanonicalBodyV1;
- Phase 4 compatibility solve;
- Phase 5A;
- scene YAML;
- ProjectSettings/packages;
- provider/application boundaries;
- read-only workflow behavior.

Foundation D run `34939208754` at `c44bd4137900876c38d2ceae475967ed53644e86` was **SUCCESS** after the relevant D-side compile corrections.

## 8. Key accepted automated evidence before the compile corrections

Retain these as useful provenance:

- Foundation C correction: run `34919859131`, job `104225315113`, SHA `9e4c4ac3a9d87eade06a43e2833481cce22b70f9`: **PASS**.
- Foundation D correction: run `34920144300`, job `104226227007`, SHA `864b39a520c78db1a0572b87a7d42c9da5cedaa4`: **PASS**.
- Foundation D E-compatible boundary migration: run `34930150745`, job `104256492165`, SHA `44a39d5ef566b800587a3be73345e843fffe5ac0`: **PASS**.
- Foundation E palm-corrected exact-head verification: run `34933354209`, job `104265988449`, SHA `087068dd14cd4bae11243a5efbd1bd5ee4cd309e`: **PASS**.
- Foundation D compatibility at same SHA: run `34933354189`, job `104265988337`: **PASS**.
- Foundation E runtime-wiring exact-head verification: run `34935841592`, job `104273421820`, SHA `c91574a7b3557bc749ed74c87e5a46107fd018c8`: **PASS**.
- Foundation D compatibility at same SHA: run `34935841602`, job `104273421255`: **PASS**.

Foundation E deterministic evidence included:

- 24/24 smoke checks;
- endpoint residual `0`;
- repeated palm target drift `0°`;
- stale palm residual `0°`;
- parent-relative palm local drift `0°`;
- bounded reacquisition step `2.68084192°`.

These do not replace the USER's live Unity/webcam/avatar QA.

## 9. Foundation A summary

Purpose: one shared command/action layer for keyboard, speech and future UI.

Important current behavior:

- keyboard meanings R/V/F1–F12/C/X/K route through project command authority;
- speech is a replaceable provider and current Windows implementation uses `KeywordRecognizer`;
- speech does not synthesize key presses;
- cooldown, confidence, optional wake prefix and mappings are configurable;
- camera preset speech goes through the same command layer.

USER QA should verify real microphone recognition, keyboard equivalence, cooldown and camera/calibration commands.

## 10. Foundation B summary

One `ThirdPersonLabCamera` remains the gameplay/presentation camera authority.

Presets:

- Back
- Front
- Left
- Right
- FullBody
- Hands
- LeftHand
- RightHand

Preset selection is orthogonal to F12 Lab/Game mode.

Focus fallbacks should safely fall from hand targets to lower-arm/body/root as appropriate.

Temporary heading loss should retain last valid heading rather than snap to a hard-coded global direction.

## 11. Foundation C summary

Foundation C adds provider-independent rich anatomical orientation while preserving CanonicalBodyV1 and the accepted Phase 4 positional path.

Important boundaries:

- `CanonicalAnatomicalBasis` is canonical orientation authority;
- no canonical Quaternion authority;
- no second body inference;
- MediaPipe 33-landmark observation is reused;
- legacy stable pose remains calibration/locomotion authority;
- partial-body channels are independent;
- twist observability states are `Observed`, `Held`, `ReferenceFallback`, `Unobservable`.

USER QA should look for useful axial detail, no sudden twist flips, sensible ambiguity fallback and no endpoint corruption.

## 12. Foundation D summary

Separate MediaPipe Hand Landmarker alongside the accepted body provider.

Important current design:

- CPU `LIVE_STREAM`;
- `numHands=2`;
- approximately 12 Hz default hand cadence;
- separate reusable 480x360 preparation;
- one active + at most one replaceable newest pending hand frame;
- no FIFO/history/replay;
- project-owned 21-landmark semantic contract;
- body-provider Stopwatch is shared semantic timing epoch;
- independent left/right freshness;
- bundled official model;
- D produces hand data only and does not drive avatar transforms.

USER QA should verify both hands, one-hand loss, body independence, stale behavior and association recovery.

## 13. Foundation E summary

Foundation E is additive and post-Phase-4.

Execution concept:

```text
exact Phase 4 positional/IK solve
-> optional Foundation E detail
-> presentation layer
```

With presentation smoothing OFF for current QA, E output is exposed directly after the exact Phase 4 solve.

Key implementation facts:

- generic `IHumanoidPostSolveDetailLayer` boundary;
- code-owned `RichHumanoidDetailRetargeter` composition in `PoseTrackingSpikePresenter.Awake()`;
- `HumanoidRetargeter.Start()` re-discovers optional detail layers;
- `HumanoidRigBinding.IsBound` remains required-body semantics only;
- optional detail/fingers never become required body validity;
- production rich channels are exactly bilateral upper/lower arms and upper/lower legs;
- pelvis/chest/feet remain deferred;
- rich axial twist is signed-map aware and downstream compensated;
- palm target is absolute/reference-based, not cumulative;
- target palm reference is parent-relative and derived from real avatar finger geometry;
- stale/master/category/reset paths remove E-owned palm/finger contribution;
- reacquisition establishes a fresh zero-delta source reference;
- no inference/camera/provider scheduling/history exists in E.

USER QA should especially watch for:

- E component auto-presence without manual Add Component;
- no palm rotation accumulation while holding a fixed pose;
- stale hand return;
- controlled reacquisition;
- independent left/right behavior;
- sensible finger articulation where avatar finger bones exist;
- E category/master toggles returning to Phase 4 baseline;
- no limb endpoint displacement from axial detail.

## 14. Phase 5A known issues — do not misclassify during foundation QA

Phase 5A is still unaccepted.

Known pre-existing issues:

- planted-feet leaning can cause unwanted translation;
- stationary cadence stepping is not robust enough.

If the USER reports these again during the foundation pass, do not automatically classify them as new A–E regressions.

## 15. Expected USER handoff to the next Orchestrator

The USER said they will provide the comprehensive testing result to the new Orchestrator.

Likely evidence includes:

- exact test SHA;
- Unity compilation/Test Runner result;
- Foundation A keyboard/speech behavior;
- Foundation B preset/fallback behavior;
- Foundation C partial-body/twist behavior;
- Foundation D hand startup/both-hands/one-hand-loss behavior;
- Foundation E auto-wiring, palm drift, stale return, reacquisition, finger articulation and toggle behavior;
- performance/endurance notes;
- screenshots/video/logs for failures.

Treat that USER evidence as the decisive runtime gate.

## 16. Recommended next-Orchestrator decision flow

After reading the USER's QA result:

1. verify live branch and exact USER-tested SHA;
2. record Unity Test Runner/compilation outcome;
3. classify each failure as:
   - foundation blocker;
   - foundation major regression;
   - minor/tuning issue;
   - known Phase 5A issue;
   - optimization/performance regression;
   - unrelated environment warning;
4. for any performance complaint, use `Docs/optimization-orchestrator-handoff.md` to identify the stage before changing code;
5. inspect repository source before proposing a fix;
6. use a Web Builder only for actual implementation work that is needed;
7. independently audit Builder results;
8. update the E workflow scope guard narrowly so the legitimate compile fixes no longer generate a false positive;
9. once A–E QA genuinely passes, update docs and only then return to Phase 5A acceptance/fixes.

Do not mark A–E USER accepted merely because Unity compiles.

## 17. Current documentation authority

Read in this order:

1. `Docs/current-state.md`
2. `Docs/optimization-orchestrator-handoff.md`
3. `Docs/orchestrator-handoff.md`
4. `Docs/decisions.md`
5. `Docs/pre-phase5a-foundations.md`
6. `Docs/architecture.md` + `Docs/motion-engine.md`

Where older documents describe pre-Foundation or pre-OpenVINO states, the newer current-state/optimization/handoff docs win.

## 18. What not to do immediately

- Do not merge to `main`.
- Do not start Phase 6.
- Do not resume Phase 5A before A–E QA is assessed.
- Do not change the locked first-pass QA settings before evidence is collected.
- Do not weaken CI guards broadly to make them green.
- Do not replace Phase 4 endpoint/IK authority with rich orientation.
- Do not make hand/finger tracking mandatory for body validity.
- Do not reopen accepted OpenVINO scheduling/acquisition architecture without a measured regression.
- Do not use presentation smoothing to conceal correctness problems during the current first-pass QA.

The next meaningful input should be the USER's comprehensive A–E Unity/manual/runtime QA result.
