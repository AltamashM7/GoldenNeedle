# Golden Needle — New Web Orchestrator Handoff

Handoff date: 2026-09-14.

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

Documentation state immediately before this handoff file was created:

`c34bbd42b898b69ca6b1e169942953a176f4cf93` — `docs: advance current state to pre-Phase 5A foundations`

Always verify the current remote branch HEAD before doing new work because this handoff itself and later documentation-only checkpoints may sit after that SHA.

## Role

You are the next **Web Orchestrator** for Golden Needle.

Your job is to:

- independently inspect the repository before directing implementation;
- preserve accepted architecture and USER-approved phase status;
- create precise Builder handoffs for implementation work;
- audit Builder claims against the actual remote diff/code before asking the USER for QA;
- update durable documentation after important decisions/acceptance;
- never merge to `main` without explicit USER approval.

Do not treat Builder reports as authority without checking the repo yourself.

## Read first, in this order

1. `Docs/current-state.md`
2. `Docs/decisions.md`
3. `Docs/pre-phase5a-foundations.md`
4. this handoff
5. `Docs/architecture.md` and `Docs/motion-engine.md` only as detailed accepted-history/architecture references

For OpenVINO/history when needed:

- `Docs/openvino-unity-integration-progress.md`
- `Docs/openvino-unity-scheduling-optimization-progress.md`
- `Docs/openvino-unity-readback-optimization-progress.md`
- `Docs/avatar-drive-source-experiment-progress.md`
- `Docs/responsive-avatar-stabilizer-progress.md`
- `Docs/responsive-avatar-beta-sweep-progress.md`

The newer current-state/decision/foundation documents override stale historical "pending QA" wording in older task-specific files.

---

# Current project state

## Phase status

- Phase 1: **PASS WITH NOTES**.
- Phase 2: **PASS**.
- Phase 3: **PASS**.
- Phase 4: **USER ACCEPTED — PASS**.
- Motion-engine latency/performance optimization milestone: **CURRENT MILESTONE COMPLETE; further tuning deferred**.
- Pre-Phase-5A foundation track: **USER APPROVED; implementation not started**.
- Phase 5A: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.

Do not incorrectly describe Phase 5A as something that still needs to be started from scratch.

Known unresolved Phase 5A USER findings remain:

- planted-feet leaning can produce unwanted translation;
- stationary cadence stepping is not robust enough.

Those issues are intentionally deferred until the pre-Phase-5A foundation track is complete enough for the current milestone.

## Current low-end performance baseline

Best USER-tested body path:

```text
WebCamTexture
 -> WebCamCPU/GetPixels32 reused pixels
 -> CPU resize/H-V preparation to 320x240
 -> bounded two-slot latest-frame mailbox
 -> persistent OpenVINO CPU FP32 worker
 -> MediaPipe 0.10.22 graph semantics
 -> 33 normalized/world landmarks
 -> canonical mapping
 -> selectable avatar-drive filtering
 -> retarget/avatar
```

Representative USER evidence on the low-end proof laptop:

```text
camera               ~28.6-30.3 FPS
fresh pose results   ~26.7-29.3/s
CPU acquisition      ~3.9 ms total
OpenVINO graph       commonly ~25-35 ms live
frame -> result      commonly ~33-62 ms
```

Proof hardware:

- Windows 10 x64;
- Intel i3-7100U, 2C/4T;
- Intel HD 620;
- no dedicated GPU.

Accepted scheduling invariants:

- max one active inference;
- max one replaceable newest pending frame;
- two reusable mailbox frame slots;
- latest useful frame wins;
- no FIFO/history/replay/catch-up backlog.

Stock MediaPipe/TFLite remains available as a safe fallback/reference.

Do not reopen OpenVINO scheduling/readback optimization without new evidence.

## Avatar smoothing state

Preserved avatar source selector:

```text
StabilizedCanonical   = 0  -> 1.0 / 0.05 / 1.0
RawCanonical          = 1  -> no positional filtering
ResponsiveCanonicalA  = 2  -> 1.5 / 0.25 / 1.0
ResponsiveCanonicalB  = 3  -> 2.0 / 0.50 / 1.0
ResponsiveCanonicalC  = 4  -> 1.0 / 0.25 / 1.0
ResponsiveCanonicalD  = 5  -> 1.0 / 0.50 / 1.0
```

USER findings:

- Stable is smooth but more delayed;
- Raw feels effectively instant but slightly less stable;
- A/B improve response but retain minor jitter;
- C/D exist for later beta-only fine tuning.

The USER explicitly deferred choosing a final smoothing winner. Preserve the tuning surface; it is not a current blocker.

Calibration and locomotion remain on the stable Phase 3 frame.

---

# USER-approved pre-Phase-5A foundations

The USER approved the following staged development direction and authorized moving into development after this orchestrator handoff.

```text
A — Unified Command System + modular speech input
    ↓
B — Camera View / Focus Preset System
    ↓
C — Rich canonical motion/orientation architecture
    ↓
D — MediaPipe hand-landmark integration
    ↓
E — Orientation-aware + optional hand/finger retargeting
    ↓
Return to Phase 5A acceptance/fixes
```

Do not collapse these into one huge refactor.

## Foundation A — first implementation target

Purpose: USER should not have to repeatedly walk to/from the laptop to calibrate, recenter, switch F12, change camera focus, etc.

Architecture requirement:

```text
Keyboard ─────┐
Speech ───────┼─> shared Golden Needle command/action layer ─> runtime actions
UI/future ────┘
```

Speech must **not** simulate keyboard presses.

The speech backend must be replaceable. For the present Windows-first prototype, a lightweight keyword/phrase recognizer is a sensible first backend if repo/Unity inspection confirms support. It must fail gracefully if unsupported.

Inspector speech mappings must be editable without code changes, including:

- enable/disable;
- phrase;
- action;
- optional action parameter;
- recognition-confidence threshold when supported.

Global settings should include cooldown/debounce, optional wake prefix and useful last-command diagnostics.

Commands invalid for the current runtime state must no-op/fail safely rather than partially mutate unrelated state.

### Exact first Orchestrator action

Before writing a Builder prompt, inspect the actual current repo and map the existing control/action ownership for:

- begin calibration;
- reset/cancel calibration;
- recenter;
- retry/restart tracking;
- F12 Lab/Game presentation;
- current camera controller/presentation behavior;
- key handling currently living in `PoseTrackingSpikePresenter` or other debug/presentation classes.

The first Builder handoff should then instruct implementation of the **shared command/action layer plus modular speech input** using existing public authorities rather than duplicating business logic.

Keep the task focused. Camera preset implementation is Foundation B and should not be opportunistically bundled into Foundation A beyond defining the command/action surface needed for later camera selection.

## Foundation B — camera presets

After Foundation A passes USER QA:

- use one primary gameplay/presentation camera;
- add named data-driven presets such as Back, Front, Left, Right, FullBody, Hands, LeftHand, RightHand;
- allow semantic/bone-relative focus;
- smooth transitions;
- fall back safely if optional targets are missing;
- speech/keyboard/UI select presets only through the shared command layer;
- preserve F12 Lab/Game semantics;
- do not keep many full-screen cameras rendering simultaneously.

## Foundation C — proper rich canonical orientation

The USER explicitly rejected a forearm-only workaround.

The problem is general: a position-only bone direction cannot represent all axial/twist rotation.

Preserve today's accepted 20-joint `CanonicalPoseFrame` as `CanonicalBodyV1` in architectural terms. Do **not** start by expanding `CanonicalJointId` and changing `JointCount=20` everywhere.

Introduce a versioned/additive provider-independent richer motion representation for:

- positions/joints;
- bone/anatomical orientation;
- orientation confidence;
- twist confidence/observability;
- optional hand articulation;
- timestamps/schema identity.

Canonical orientation source of truth is **not a quaternion**.

Use validated orthonormal anatomical bases:

```text
primary longitudinal axis
+ independent secondary anatomical evidence
-> orthogonalized complete basis
-> handedness/determinant validation
-> orientation + twist confidence
```

Quaternions are trustworthy only as the final Unity rotation representation after a valid canonical basis has been mapped into the avatar's captured bind/reference basis.

General rule:

- solve orientation for every supported bone using the same architecture;
- do not patch only the forearm;
- swing may update while twist is temporarily unobservable;
- twist updates only when independent secondary evidence is trustworthy;
- short ambiguity preserves previous trusted twist;
- longer loss uses controlled fallback toward reference/calibrated twist;
- never fabricate sudden axial rotation from degenerate geometry.

A monocular RGB camera cannot recover genuinely invisible rotation; represent uncertainty explicitly.

## Foundation D — MediaPipe hands

The USER does **not** require perfect finger mocap now.

Required useful scope includes:

- open/close hand;
- fist clench/release;
- basic individual finger flexion where reliable;
- pointing/obvious articulation where available;
- palm/hand orientation for wrist/forearm twist evidence.

Use MediaPipe-provided hand landmarks rather than a custom hand-CV solution.

Before choosing implementation, audit the current practical MediaPipe options with this exact project generation:

- separate Hand Landmarker alongside the optimized body provider;
- Holistic if it can preserve semantics/performance cleanly.

Default preference: separate/adaptive hand tracking because the current body path is already strongly optimized. Change only with evidence.

Body pose remains priority. Hand inference may be lower cadence/adaptive/visibility-driven. No new unbounded queue.

If body and hand streams are separate, define timestamp skew, coordinate ownership, handedness matching, stale-data rejection and one-hand-only behavior before combining them.

## Foundation E — optional-bone retargeting

Capability-based binding is mandatory.

- full finger rig -> drive supported fingers;
- partial finger rig -> drive supported subset;
- no finger rig -> skip fingers and continue body normally;
- missing optional hand/head/foot detail must not invalidate torso/limbs.

The accepted Phase 4 positional/analytic IK path remains fallback/reference during rollout.

Do not assume avatar bones share identical local-axis conventions. Capture avatar bind/reference bone bases and map canonical anatomical bases into them before final quaternion application.

---

# Important current code facts

At the documentation handoff point:

- `PoseObservation` is still fixed at 33 pose landmarks.
- `MediaPipeCanonicalPoseMapper` is still the only mapper that knows current MediaPipe pose indices.
- current `CanonicalPoseFrame` is still a fixed 20-joint position-centric representation.
- current `CanonicalPoseJoint` stores image/world/local positions and confidence, not a full bone orientation basis.
- current Phase 4 limb policy remains swing-only.

Do not misrepresent richer orientation/hands as already implemented.

---

# Hard guardrails

- Do not merge to `main` without explicit USER approval.
- Do not start Phase 6.
- Do not mark Phase 5A accepted.
- Do not silently change serialized/default inference backend, frame acquisition or avatar-drive source.
- Do not remove stock MediaPipe/TFLite fallback.
- Do not alter the stable calibration/locomotion authority while tuning avatar/rich motion.
- Do not destructively mutate CanonicalBodyV1 during command/camera work or as an easy shortcut for rich motion.
- Do not use opaque quaternion construction as the canonical orientation authority.
- Do not implement forearm-only twist or similar local patches in place of the general orientation architecture.
- Do not make optional finger bones mandatory for rig validity.
- Do not sacrifice accepted body latency/throughput merely to run hands every body frame.
- Do not add historical pose queues/replay/catch-up systems.
- Do not densify the detector.
- Do not force D3D12 globally.
- Do not reopen accepted OpenVINO scheduling/WebCamCPU work without new evidence.
- Do not remove Stable/Raw/A/B/C/D smoothing modes merely for cleanup.

---

# Workflow expectations

The USER uses a Web Orchestrator + Web Builder + Luna workflow.

- Use the intelligent Web Builder for architecture-heavy/runtime work.
- Use Luna only for truly routine/scoped tasks.
- Builder checkpoints are recovery markers, not automatic USER approval gates.
- Avoid unnecessary automated Unity scene runs; USER performs final physical/visual QA.
- Before asking USER to test, independently inspect the remote diff and verify the Builder stayed in scope.
- Keep documentation current after accepted findings.

USER-local files may be dirty. Do not casually revert/clean scene/solution/settings files based only on remote assumptions.

---

# What the new Orchestrator should do now

1. Verify the current remote `engine/pose-tracking-spike` HEAD.
2. Read the four primary documents listed at the top.
3. Inspect the current code ownership for existing keyboard actions/public runtime methods.
4. Design a minimal shared command/action boundary that reuses those authorities.
5. Prepare the first **Web Builder handoff for Foundation A only**.
6. Have the Builder implement through its normal checkpoints until genuine USER QA is required.
7. Independently audit the resulting branch before giving the USER QA instructions.
8. Do not begin Foundation B until Foundation A has reached the appropriate USER-accepted checkpoint or the Orchestrator explicitly determines a narrow dependency requires otherwise.

The USER has already approved beginning this development track; do not ask again merely to start the Foundation A planning/Builder handoff.