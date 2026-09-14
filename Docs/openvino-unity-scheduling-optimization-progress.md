# Golden Needle — OpenVINO Unity Scheduling Optimization Progress

This file records the resolved state of the post-U4 OpenVINO scheduling optimization approved by the USER on 2026-09-14.

Current status refresh: 2026-09-14.

## Resolution

- Branch: `engine/pose-tracking-spike`.
- Scheduling-optimization start SHA: `b38ab0df09aad452af0c49b1c56613b6d7dffe8d`.
- USER authorization: **APPROVED**.
- Implementation/static/helper verification: **PASS**.
- USER runtime validation: **PASS**.
- Final classification: **USER ACCEPTED — PASS**.
- Phase 5A remains **NOT USER ACCEPTED**.
- Phase 6 remains **NOT STARTED**.
- No merge to `main` without explicit USER approval.

Older sections/commits that described USER runtime A/B as still pending are historical and superseded by this resolved status.

## Why the optimization existed

Before this optimization, OpenVINO already showed lower live graph/inference time than stock TFLite, but end-to-end latency remained similar because a completed readback could sit prepared until a later Unity `Update` after the OpenVINO worker finished.

Representative pre-fix OpenVINO symptom:

```text
camera               ~29.5 FPS
results              ~12.8/s
OpenVINO graph       ~31.7 ms
frame->result        ~98.5 ms
prepared->launch     ~19.9 ms
frame delta          1
launch origin        Update
```

The USER also reported that OpenVINO already felt smoother in F12, suggesting the neural compute advantage was real even before scheduling was fixed.

## Accepted architecture

The selected design is a persistent OpenVINO worker with a bounded two-slot latest-frame mailbox.

```text
main thread camera/readback
  -> reusable mailbox storage
  -> at most one active frame
  -> at most one replaceable newest pending frame
  -> one persistent OpenVINO worker
```

Hard invariants:

- exactly one OpenVINO inference may be active;
- at most one pending/latest frame exists;
- active storage cannot be overwritten;
- a newer frame replaces the pending frame rather than creating backlog;
- no third/history/FIFO/replay/catch-up slot exists;
- latest useful frame wins;
- stock MediaPipe/TFLite remains on its existing path;
- worker/native teardown is joined safely before disposal;
- canonical, calibration, retarget, locomotion and presentation semantics are unchanged.

The continuation launch origin is exposed as `OVW` (`OpenVinoWorkerContinuation`).

## Key implementation checkpoints

Main scheduling implementation:

`9937bb37d35ecfc50fd44e2dca0c6aa3c55ba358` — `perf: continue OpenVINO from latest-frame mailbox`.

Follow-up session-counter repair:

`c2f60ff` — `fix: reset OpenVINO scheduling session counters`.

Focused managed/source verification proved the real extracted scheduler/mailbox helper code outside Unity, including bounded ownership and cadence behavior.

Authoritative helper-validation run:

```text
run 34821509749
result SUCCESS
OPENVINO_MANAGED_SCHEDULING_SMOKE=PASS
buffers=2; pending=0; active=False
```

No native OpenVINO rebuild was required because the optimization changed only managed scheduling/ownership/telemetry.

## USER runtime result

Post-optimization USER evidence showed the exact intended change:

```text
camera               ~28.4 FPS representative sample
pose results         ~12.7/s representative sample
prepared->launch     0.0 ms
frame delta          0
launch origin        RB / OVW
U/RB/OVW             0/59/5 representative window
ovw                   ~2.9/s
pending               0
active                1
buffers               2
```

The USER reported OpenVINO was much more responsive in F12 after this change.

Interpretation:

- the avoidable normal-`Update` scheduling delay was removed;
- worker continuation occurred when a pending frame existed;
- the mailbox remained bounded;
- no new history/backlog system was introduced;
- the faster OpenVINO compute became more visible end-to-end.

Final status: **USER ACCEPTED — PASS**.

## What this optimization did not solve

After scheduling was fixed, telemetry exposed the next dominant bottleneck: DirectCPU/AsyncGPUReadback completion itself, with submit-to-callback commonly tens of milliseconds.

That led to the separate WebCamCPU/GetPixels32 acquisition experiment documented in:

`Docs/openvino-unity-readback-optimization-progress.md`.

That later experiment also passed for the current milestone.

## Current governance

Do not reopen this scheduling architecture without new reproducible evidence.

Do not:

- add a FIFO or history queue;
- allow concurrent OpenVINO inferences;
- reintroduce Update-bound waits merely for code simplicity;
- remove stock TFLite fallback;
- alter canonical/calibration/retarget/locomotion semantics as part of scheduling work;
- rebuild native OpenVINO unless native inputs/ABI genuinely change.

## CONTINUE FROM HERE

**STATUS: CLOSED / USER ACCEPTED — PASS.**

This file is historical/current-reference documentation only. The current project direction is governed by `Docs/current-state.md`.
