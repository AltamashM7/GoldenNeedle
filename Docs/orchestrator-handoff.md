# Golden Needle — Orchestrator Handoff

Handoff refresh: 2026-09-16

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

## 1. First actions for the next Orchestrator

Before changing anything:

1. verify the live remote `engine/pose-tracking-spike` HEAD;
2. read `Docs/current-state.md` as the primary current authority;
3. read `Docs/optimization-orchestrator-handoff.md` for optimization history and preserved invariants;
4. read `Docs/decisions.md` for current/superseded architectural decisions;
5. inspect the current source before writing any Builder brief;
6. independently verify Builder/repository claims rather than accepting summaries at face value.

Do **not** merge to `main` without explicit USER approval. Do not force-push, rebase, amend, reset, or rewrite published/shared history.

## 2. Current code/runtime baseline

The corrective restoration checkpoint remains:

`f1819fda36547343bb32a972d39405d0a6be6f72`

Motion Engine Completion Batch 2 completed at:

`19e697695802459f97d488b64e7b62683c89512d`

Batch 2R was explicitly authorized as a narrow correction from that exact HEAD. Its source/test checkpoint before documentation is:

`a96cbf06437004f3f44d53383601ec1be556d767`

The USER previously reopened Unity after corrective restoration with zero red errors and reported restored low-end performance. Further performance optimization remains deferred for the current hackathon milestone.

Current performance milestone:

`USER SATISFIED FOR CURRENT HACKATHON MILESTONE / FURTHER PERFORMANCE WORK DEFERRED`

Do not reopen performance architecture unless new reproducible evidence justifies it.

## 3. Preserved optimized body path

Current best-tested normal body path:

```text
Unity WebCamTexture
-> reusable WebCamCPU/GetPixels32 acquisition
-> reusable 320x240 CPU preparation
-> bounded newest-only two-slot scheduling
-> persistent OpenVINO CPU FP32 worker
-> MediaPipe 0.10.22 pose semantics
-> 33 normalized + world landmarks
-> CanonicalBodyV1
-> Phase 3 stabilization/calibration authority
-> Phase 4 positional/IK avatar control
-> presentation
```

Preserve these boundaries:

- stock MediaPipe/TFLite remains fallback/reference;
- ExistingReadback remains fallback/reference;
- at most one active body inference plus one replaceable newest pending frame;
- latest useful frame wins; no FIFO/history/replay/catch-up backlog;
- stable Phase 3 canonical data remains calibration/locomotion authority;
- Phase 4 signed canonical-to-avatar mapping and analytic two-bone IK remain accepted production pose authority;
- monocularly unobservable free axial/twist motion is not fabricated in normal production operation.

Batch 2R did not modify this path, Phase 3 stabilization/calibration, or Phase 4 production pose code.

## 4. Current phase status

- Phase 1 — provider/raw pose: **PASS WITH NOTES**.
- Phase 2 — canonical skeleton: **PASS**.
- Phase 3 — stabilization/confidence/calibration foundation: **PASS**.
- Phase 4 — humanoid retargeting: **USER ACCEPTED — PASS**.
- Low-end optimization milestone: **USER SATISFIED / FROZEN FOR CURRENT MILESTONE**.
- Phase 5A — locomotion: **BATCH 2R COMPLETE / USER QA DEFERRED / NOT YET USER ACCEPTED**.
- Phase 6 — graybox vertical slice: **NOT STARTED**.

Do not mark Phase 5A or Motion Engine V1 USER accepted before final integrated USER QA.

## 5. Foundation disposition

Foundation A commands/speech and Foundation B camera presets remain retained. Foundation C remains deferred/dormant research, detailed hands remain deferred for low-end cost, Foundation E remains retired from production, and the former coarse-hand experiment remains deferred/rolled back. None were touched by Batch 2R.

## 6. Phase 5A Batch 2 retained behavior

The accepted Batch-2 support-aware root model remains in place:

- `Both` for near-equal foot heights, using the midpoint;
- `Left` when the left foot is clearly lower/supporting;
- `Right` when the right foot is clearly lower/supporting;
- hysteresis retains the prior single-foot authority through the ambiguous band.

Current thresholds remain `supportSingleFootEnter = 0.12` and `supportBothEnter = 0.06`.

Raised/moving swing feet remain isolated from physical translation. Planted-feet torso lean remains suppressed. Support-mode transitions remain continuity-rebased. Temporary support loss still holds the last trusted physical displacement and reacquisition remains rebased. `Recenter()` remains preserved.

Cadence was **not modified by Batch 2R**. The Batch-2 defaults and Inspector labels remain unchanged.

## 7. Batch 2R — alternating physical-step continuity correction

### Failure mode at `19e6976...`

The Batch-2 tracker correctly rebased every authority change to prevent snapping, but the resulting `_supportAuthorityOffset` persisted indefinitely. An ordinary physical sequence could therefore do:

`Both -> Right/Left -> Both -> opposite single support -> Both`

while each transition kept the root continuous. When the second foot finally landed at the relocated position, the final Both-mode offset still cancelled the newly relocated common support base. Subsequent stable Both samples therefore stayed near the old physical origin instead of converging to the new room position.

### Correction strategy

`CameraSpaceRootTracker` now separates ordinary authority-transition rebases from tracking-loss rebases and introduces a narrow coherent-landing release path.

On every authority transition, the transition frame still computes:

`offset = currentFilteredDisplacement - newRawAuthority`

so the transition itself cannot teleport the root.

When the new mode is `Both` and the previous mode was a single-support mode, the landing is marked releasable only if the normalized left/right displacement agrees in X and Y within the existing `supportBothEnter` tolerance. This distinguishes a coherent relocated support base from a one-foot/asymmetric reposition.

On the **next** coherent `Both` sample, that temporary landing offset is cleared. Normal filtering can then converge toward the common support-base displacement. The landing frame remains continuous; real net relocation is no longer permanently cancelled.

A support-loss/reacquisition rebase explicitly clears the release eligibility, so reacquiring both feet at a different raw position cannot immediately auto-release into a teleport/drift. A later genuine alternating support cycle can establish a new coherent relocation normally.

Because the offset and agreement check operate on the existing normalized `Vector2` support displacement, coherent depth relocation can use the same release mechanism, while the existing depth-differential reliability and torso-scale corroboration still decide whether depth is accepted. No depth threshold or cadence rule was weakened.

## 8. Batch-2R deterministic test

The existing Batch-2 test suite remains intact. Batch 2R adds exactly one focused regression test:

`AlternatingPhysicalStepEventuallyCommitsCoherentSupportBaseRelocation()`

Sequence:

1. baseline both feet;
2. left foot raised and moved +X — root stays near unchanged;
3. left lands — no discontinuous jump;
4. right foot raised/moved to the corresponding +X location — swing movement does not falsely translate;
5. right lands — no discontinuous jump;
6. next coherent dual-support sample — root must now show meaningful positive X displacement (`> 0.15` normalized in the deterministic fixture).

Against `19e697695802459f97d488b64e7b62683c89512d`, the test's final assertion fails conceptually because the final Both rebase persists and settled displacement remains at the old origin. After the 2R correction, the final coherent sample releases that landing rebase and can converge to the relocated common support position.

Existing coverage remains for:

- swing-foot isolation;
- planted-feet lateral and scale lean suppression;
- support transition continuity;
- bilateral relocation;
- temporary support loss and reacquisition;
- cadence acquisition/stop/distance/max-speed behavior;
- physical/cadence fusion;
- front-camera axis mapping and body heading;
- recenter.

No existing Batch-2 assertion was weakened or deleted for 2R.

## 9. Verification status

No Unity Editor/Test Runner is available in this Builder execution environment, so the C# Editor tests were **not executed here**. The alternating sequence was independently traced against the old and corrected authority equations, and GitHub source/test diffs were statically audited.

Do not describe this as an executed Unity test pass or USER acceptance.

Status vocabulary:

`BATCH 2R COMPLETE / USER QA DEFERRED / AWAITING ORCHESTRATOR REVIEW`

## 10. Jump and crouch remain Batch 3 requirements

Jump, crouch, root-Y gameplay, gravity, CharacterController work and Phase 6 were **not started** in Batch 2R.

## 11. Immediate next action

**STOP after Batch 2R.** The next Orchestrator should independently inspect the live branch and the narrow 2R diff before authorizing any Batch-3 work.

Do not ask for Batch-2R USER QA. USER/runtime testing remains deferred until after Batch 3 implementation. Do not reopen cadence, optimization, Foundations C/D/E/coarse hands, Phase 6, or unrelated cleanup. Do not merge to `main` without explicit USER approval.
