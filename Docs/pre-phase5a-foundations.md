> **SUPERSESSION NOTICE — 2026-09-16**
>
> This file is preserved as **historical design context**, not current implementation authority. Foundation A's shared command/speech architecture and Foundation B's camera preset architecture were retained. The planned production rollout of Foundations C/D/E was later reversed/deferred after USER runtime evidence and corrective pose-baseline restoration: C is dormant research, D detailed hands are deferred, and E is no longer production-wired. Current production pose authority is Phase 3 + Phase 4 and is documented in `Docs/current-state.md`. The original foundation order, rollout requirements, QA sequence and “Immediate next implementation target” below describe the earlier approved design checkpoint and must not be read as the current project plan.

---

# Golden Needle — Pre-Phase 5A Foundation Architecture

Status: **USER APPROVED DIRECTION / IMPLEMENTATION NOT STARTED**

Decision date: 2026-09-14.

This document defines the foundation work that must be completed before returning to Phase 5A locomotion acceptance. It is an architecture/requirements document, not a claim that the features below are already implemented.

Current-state authority remains `Docs/current-state.md`. This document is the authoritative design for the pre-Phase-5A foundation track.

## Why this track exists

The current Motion Engine has reached a strong latency/throughput baseline on the USER's low-end proof machine. The remaining near-term problem is no longer primarily inference speed. The next foundation work should improve:

1. hands-free operation while the USER is standing away from the laptop;
2. camera inspection/QA of fine avatar control;
3. motion fidelity beyond joint-position-only limb direction;
4. hand/finger articulation using MediaPipe-provided landmarks;
5. future extensibility without breaking the accepted Phase 2/3/4 contracts.

Phase 5A already exists and remains **IMPLEMENTED / NOT USER ACCEPTED**. This foundation track is intentionally inserted before Phase 5A acceptance resumes.

---

## Foundation order

Implement and validate in this order unless new evidence requires a change:

```text
Foundation A — Unified Command System + speech input
    ↓
Foundation B — Camera View / Focus Preset System
    ↓
Foundation C — Rich canonical motion/orientation architecture
    ↓
Foundation D — MediaPipe hand-landmark integration
    ↓
Foundation E — Orientation-aware + optional hand/finger retargeting
    ↓
Return to Phase 5A locomotion acceptance/fixes
```

Do not attempt all five foundations as one giant refactor. Each stage must preserve the known-good body pipeline and have its own focused QA boundary.

---

# Foundation A — Unified Command System

## Product requirement

The USER must be able to operate important runtime actions while standing away from the laptop. Speech is one input source, not the owner of those actions.

Examples include:

- begin calibration;
- reset/cancel calibration;
- recenter;
- retry/restart tracking where appropriate;
- switch Lab/Game presentation;
- choose camera-view presets.

## Architecture rule

Do **not** implement speech by simulating key presses.

Create one project-owned command/action layer that can be invoked by multiple input sources:

```text
Keyboard ─────┐
Speech ───────┼─> Golden Needle command/action layer ─> runtime actions
UI/future ────┘
```

Keyboard shortcuts and speech must call the same underlying actions so behavior cannot diverge.

## Speech backend rule

Speech recognition must sit behind a replaceable provider/backend boundary.

For the current Windows-first prototype, a lightweight keyword/phrase recognizer is the preferred first implementation if the installed Unity/Windows environment supports it cleanly. The implementation must fail gracefully when speech is unsupported or unavailable.

Do not architect Golden Needle around one operating-system speech API.

## Inspector requirements

The speech-command configuration must be editable without source-code changes. At minimum each mapping should expose:

- enabled/disabled;
- phrase/keyword;
- command/action;
- optional parameter when the action needs one, e.g. camera preset;
- minimum recognition confidence where the backend exposes it.

Global speech settings should include:

- speech enabled;
- command cooldown/debounce;
- optional wake prefix/activation phrase;
- useful diagnostics such as last recognized phrase, resolved command and rejection reason.

The exact default phrases are tuning/content, not architecture.

## Safety/state behavior

Commands that are invalid in the current runtime state must fail safely rather than throw or partially mutate unrelated state.

Repeated recognition within the cooldown window must not spam actions such as calibration or recenter.

---

# Foundation B — Camera View / Focus Preset System

## Product requirement

The USER should be able to inspect avatar control without returning to the laptop. Example speech commands may move the game camera from the normal behind-avatar view to the hands, left hand, right hand, front, side, or full-body view.

## Architecture rule

Use **one primary gameplay/presentation camera with multiple named view presets**, not many simultaneously rendering full-screen cameras.

This preserves low-end rendering performance and avoids camera-authority conflicts.

Initial useful preset set:

```text
Back
Front
Left
Right
FullBody
Hands
LeftHand
RightHand
```

Exact offsets are Inspector-tunable.

## Preset data

A preset should be data/configuration rather than a hardcoded method. It should be able to describe, as applicable:

- target/focus semantic;
- local/world offset;
- distance;
- height;
- field of view;
- look offset;
- position response/transition speed;
- orientation response.

## Bone-relative focus

Presets such as Hands/LeftHand/RightHand should be able to focus on an avatar semantic/bone target rather than a fixed world point.

If the requested optional bone/target is unavailable, the camera must degrade gracefully to a sensible parent/body target. Missing fingers or hands must never crash the camera system.

## Command integration

Camera selection is an action of the shared command system, for example conceptually:

```text
Action: SelectCameraPreset
Parameter: Hands
```

Speech should not directly manipulate camera transforms.

F12 Lab/Game presentation semantics must not be silently changed while adding camera presets.

---

# Foundation C — Rich Canonical Motion / Orientation Architecture

## Problem being solved

The accepted current canonical body is position-centric. A bone can point in the correct direction while its axial rotation/twist remains wrong.

Example: elbow and wrist positions can remain nearly unchanged while the forearm and hand rotate around the forearm's longitudinal axis. The current swing-only path cannot reproduce that full orientation.

This is not a forearm-specific defect. Any bone can have an orientation degree of freedom that cannot be described by its start/end positions alone.

The solution must therefore be general, not a collection of bone-specific patches.

## Preserve CanonicalBodyV1

The accepted 20-joint `CanonicalPoseFrame` / current Phase 2 semantics are a compatibility contract and must **not** be destructively expanded during this work.

Treat today's representation as `CanonicalBodyV1` in architectural terms.

Existing calibration, locomotion and accepted Phase 4 behavior must remain available while richer data is introduced additively.

Do not simply append dozens of joints to `CanonicalJointId` and change `JointCount = 20` as the first step.

## Rich representation direction

Introduce a versioned/additive richer motion representation, exact type names to be chosen after repo audit. Conceptually it must separate:

```text
Rich Motion Frame
  ├─ joint/landmark position channels
  ├─ bone/anatomical orientation channels
  ├─ orientation/twist confidence
  ├─ optional hand articulation channels
  └─ provider/timestamp/schema identity
```

The richer contract must remain provider-independent. MediaPipe landmark indices stay inside provider/mapping code.

## Orientation authority

Quaternions are **not** the canonical reasoning/source-of-truth representation.

For each supported bone/anatomical segment, reconstruct and retain a validated orthonormal anatomical basis plus confidence/observability information.

Conceptually:

```text
Bone orientation
  primary longitudinal axis
  secondary anatomical evidence
  derived third orthogonal axis
  handedness / determinant validity
  orientation confidence
  twist confidence / observability
  validity state
```

Only after a valid canonical basis has been mapped into the target avatar's bind/reference basis should Unity quaternions be produced for final transform application/interpolation.

This avoids hiding reflection/axis mistakes inside opaque quaternion manipulation.

## General orientation solver

The solver must be data-driven/general across supported bones rather than implementing "forearm rotation" as a special-case fix.

Each bone orientation is based on:

1. a primary axis: where the bone points;
2. independent secondary anatomical evidence: how it is rotated around that axis;
3. orthogonalization and handedness validation;
4. confidence and degeneracy checks.

Examples of possible evidence, subject to implementation audit:

- torso: pelvis/chest axis plus shoulder/hip lateral axis;
- upper arm: shoulder->elbow plus elbow/body/bend-plane evidence;
- forearm: elbow->wrist plus hand/palm evidence;
- thigh: hip->knee plus pelvis/knee-plane evidence;
- shin: knee->ankle plus foot orientation evidence;
- foot: heel/toe plus ankle/foot-plane evidence;
- hand: wrist plus hand landmarks.

These examples illustrate the general rule; they are not permission to hardcode an unvalidated formula without tests.

## Swing/twist observability

Treat bone orientation as observable swing plus potentially observable twist.

- Swing can update when the main bone direction is trustworthy.
- Twist must update only when independent secondary evidence is trustworthy.
- If secondary evidence becomes degenerate/nearly parallel to the primary axis, do not manufacture a new twist.
- During short ambiguity, preserve the previous trusted twist while continuing to update trustworthy swing.
- After a longer loss, use a controlled fallback toward calibrated/reference twist rather than snapping.

A single RGB camera cannot recover a rotation for which there is genuinely no visible evidence. The system must expose uncertainty instead of pretending otherwise.

## Avatar mapping

Canonical anatomical orientation and avatar bone orientation are separate coordinate systems.

For each target bone/chain that supports orientation-aware driving:

```text
canonical anatomical basis
    -> signed canonical/avatar mapping
    -> captured avatar bind/reference bone basis
    -> final proper rotation
    -> Unity quaternion/Transform application
```

Do not assume all avatars author every bone along the same local axis.

---

# Foundation D — MediaPipe Hand Landmarks

## Scope

Perfect professional finger mocap is **not required** for this milestone.

The requirement is useful hand/finger control using MediaPipe-provided landmarks, sufficient for interactions such as:

- opening/closing the hand;
- clenching/releasing a fist;
- basic individual finger flexion where reliable;
- pointing/obvious finger articulation where available;
- palm/hand orientation evidence for wrist/forearm twist.

Do not invent a custom hand-CV pipeline when the MediaPipe ecosystem already provides appropriate hand landmarks.

## Provider strategy

The optimized body path is valuable and must not be casually replaced.

Before implementation, perform a focused reuse/performance audit of the practical current-generation options available with the project's Homuler/MediaPipe/OpenVINO constraints, especially:

- a separate MediaPipe Hand Landmarker provider alongside the accepted body provider;
- MediaPipe Holistic as a richer combined provider if it can preserve the required performance/semantics.

Default architectural preference entering that audit: **separate/adaptive hand tracking**, because it minimizes risk to the accepted body path. Change that preference only if evidence shows the combined option is cleaner and fast enough.

## Scheduling/performance rule

Body pose remains the priority real-time stream.

Hand inference may run independently, adaptively, or at a lower cadence. Do not require two hand networks to run on every body frame merely for symmetry.

The hand system may activate only when hands are sufficiently visible/needed if that materially protects low-end performance.

No new unbounded queue/history/backlog is allowed.

## Synchronization rule

If body and hands are separate providers, the rich motion layer must define:

- timestamps/frame age;
- coordinate-space ownership;
- left/right handedness association;
- maximum acceptable age/skew for combining hand data with the current body frame;
- behavior when only one hand is available;
- behavior when hand cadence is lower than body cadence.

Stale hand data must not be treated as current merely because the last sample exists.

---

# Foundation E — Optional-Bone Retargeting

## Capability-based rig binding

Rich retargeting must discover and drive only bones actually present on the character.

Missing optional bones are normal, not errors.

Examples:

- avatar has full fingers -> drive supported finger chains;
- avatar has only some finger bones -> drive those supported chains;
- avatar has no finger bones -> body/hand root motion continues without finger driving;
- missing hand/finger/head/foot detail must never invalidate unrelated torso/limb retargeting.

This extends the project's existing partial-validity philosophy into richer rig capability.

## Finger articulation

Finger retargeting should start with robust useful articulation, not maximum theoretical fidelity.

The initial implementation may derive flexion/curl and other stable finger controls from MediaPipe hand landmarks and map them to available avatar finger chains. Exact per-joint twist refinement can come later if evidence supports it.

## Backward compatibility

The accepted Phase 4 positional/IK path must remain a fallback/reference during rollout.

Rich orientation/finger features should be additive and independently disable-able during development so regressions can be isolated.

---

# QA / rollout strategy

Each foundation gets a focused QA gate.

### A — Commands

Verify keyboard and speech invoke the same underlying actions; unsupported speech fails gracefully; cooldown/state guards work.

### B — Camera

Verify named presets, smooth transitions, missing-target fallback, no extra active full-screen cameras, and no regression to F12 Lab/Game behavior.

### C — Rich body orientation

First prove improved orientation using already-available body/pose evidence before adding hand inference where possible. Test axial rotation, side views, torso/pelvis orientation, legs/feet, ambiguity fallback, and left/right/handedness stability.

### D — Hands

Measure actual CPU/render/body-pose impact on the low-end USER machine. Verify handedness, open/fist/basic finger articulation, hand loss/reacquisition, partial-body operation, and body-path stability.

### E — Retarget

Test at least:

- a humanoid with usable finger bones;
- a humanoid with incomplete/no finger bones;
- body-only fallback;
- fast movement and temporary hand loss;
- orientation continuity without sudden twist flips.

Do not return to Phase 5A acceptance until the USER is satisfied that these foundations are sufficiently stable for the current milestone.

---

# Explicit non-goals / guardrails

During this foundation track:

- do not merge to `main` without explicit USER approval;
- do not mark Phase 5A accepted;
- do not start Phase 6;
- do not remove stock MediaPipe/TFLite fallback;
- do not reopen accepted OpenVINO scheduling/WebCamCPU optimization without new evidence;
- do not silently change backend/acquisition/avatar-drive serialized defaults;
- do not destructively mutate the accepted 20-joint canonical contract;
- do not use quaternions as the hidden canonical orientation authority;
- do not implement forearm-only or other bone-specific hacks in place of a general orientation architecture;
- do not require optional finger bones for rig validity;
- do not run needless duplicate inference or add unbounded frame queues;
- do not sacrifice the accepted low-end body-path responsiveness without explicit USER evidence/approval.

---

# Immediate next implementation target

**Foundation A — Unified Command System + modular speech input.**

Before editing runtime code, the next Orchestrator/Builder must inspect the current control/action ownership in the actual branch and identify the existing public methods/authorities for calibration, reset/retry, recenter, F12 presentation and camera behavior. The implementation should unify those actions rather than duplicate presenter key handling.

After Foundation A USER QA, proceed to Foundation B camera presets. Rich canonical/orientation work follows only after those operational foundations are stable.
