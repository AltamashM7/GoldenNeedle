> **CURRENT-STATUS ADDENDUM — 2026-09-16**
>
> The optimization chronology and measurements below are preserved as historical evidence. After later Foundation C/D/E/coarse-hand experiments, corrective pose-baseline restoration returned normal production pose composition to Phase 3 + Phase 4, and the USER subsequently reported that low-end performance appears restored. Current optimization status is `USER SATISFIED FOR CURRENT HACKATHON MILESTONE / FURTHER PERFORMANCE WORK DEFERRED`. The normal downstream production path is now `Phase 3 -> Phase 4 -> presentation`; Foundation E is not active production detail. Stock MediaPipe/TFLite and ExistingReadback remain fallback/reference paths. References below to a then-current comprehensive Foundations A–E QA, Foundation E in the then-current pipeline, or Foundation E as a then-current post-solve invariant describe the historical checkpoint at which this handoff was first written and are superseded for present-state authority by `Docs/current-state.md`.

---

# Golden Needle — Optimization Orchestrator Handoff

Handoff date: 2026-09-15

Repository: `AltamashM7/GoldenNeedle`

Active branch: `engine/pose-tracking-spike`

This document exists so a new Orchestrator can continue the Golden Needle optimization pipeline with the same architectural context as the Orchestrator that performed the work. It consolidates the accepted optimization chronology, why each decision was made, what evidence was measured on the USER's low-end machine, what is now considered closed, what is intentionally preserved as fallback/reference, and what must not be casually redesigned.

For current project status and immediate QA/governance, read `Docs/current-state.md` first. For optimization-specific reasoning, use this document before reopening any performance work.

## 1. Optimization objective and proof machine

Golden Needle must provide responsive camera-driven body control on ordinary hardware without requiring a dedicated GPU.

The main proof machine used during the optimization track is intentionally low-end:

- Windows 10 x64;
- Intel Core i3-7100U @ 2.40 GHz;
- Intel HD Graphics 620;
- no dedicated GPU;
- Unity 6.5 / `6000.5.0f1`;
- URP 17.5.0;
- MediaPipeUnityPlugin `0.16.3` / MediaPipe `0.10.22`;
- OpenVINO `2026.3.0` for the accepted accelerated CPU backend.

The optimization goal was never simply “maximize benchmark FPS.” The requirement was to increase **fresh useful pose samples** and reduce **real camera-frame-to-pose/avatar latency** without breaking MediaPipe semantics, world landmarks, partial-body behavior, calibration, Phase 4 orientation/IK, or safe fallback behavior.

## 2. Starting problem — why optimization was needed

Before the final optimization path, the body provider could capture the webcam around camera cadence but fresh pose output was much lower.

Representative early production observations were approximately:

```text
camera capture         ~29-30 FPS
fresh pose results     ~10-12/s
DirectCPU readback     ~55-65 ms in representative runs
frame -> pose result   commonly ~110-140 ms depending on load
```

Fast body/arm trajectories were visibly undersampled. The USER had to move somewhat slower for motion to be reproduced faithfully.

This led to a layered optimization effort rather than a single backend swap:

1. reduce body input work/downscale;
2. remove avoidable scheduling delay;
3. improve inference execution while preserving MediaPipe semantics;
4. remove the dominant camera/readback bottleneck;
5. isolate any remaining apparent lag caused by avatar filtering rather than inference.

## 3. Early body-input and scheduling improvements

### Body reference/body inference downscale

The body path was reduced to the current approximately `320x240` inference/preparation target while the display/webcam source remains native enough for presentation/debug use.

The downscale gave a modest, repeatable performance gain without an obvious tracking-quality collapse. It is part of the current best-tested low-end baseline.

Do not lower quality/resolution further merely to chase an FPS number without new same-machine evidence. The neural detector/landmark tensor sizes are fixed downstream, so blindly lowering the source has diminishing returns and can hurt tracking quality.

### Immediate Launch After Readback

An avoidable extra render-frame delay existed between a completed readback/prepared frame and inference launch. The Immediate Launch After Readback change removed that normal-Update-bound wait and was USER accepted.

This decision matters because future refactors must not accidentally reintroduce a one-frame scheduling delay for code simplicity.

### Direct body CPU readback

The DirectCPU path was an accepted improvement over the more indirect Homuler CPUAsync flow because it wrote readback data directly into the pooled CPU buffer and avoided some unnecessary staging/application work.

However, later telemetry proved that the underlying AsyncGPUReadback completion itself was still expensive. DirectCPU was therefore useful, but it was not the final solution to camera-to-CPU latency.

The current USER QA configuration still has Direct Body CPU Readback enabled, but when `WebCamCpuPixels` is the effective OpenVINO acquisition mode, the CPU-pixel path is the important fast path. Preserve DirectCPU/ExistingReadback as fallback/reference behavior rather than deleting it.

## 4. Why Sentis GPUCompute was rejected on the proof machine

The project investigated Unity/Sentis as a possible way to move neural inference onto the Intel HD 620.

The exact detector also exposed a Sentis importer limitation around `DENSIFY`, while the landmark model could be benchmarked.

Representative clean D3D12 landmark findings were approximately:

```text
Sentis CPU              ~46.24 ms / 21.63/s
Sentis GPUCompute       ~60.48 ms / 16.53/s
GPU-resident variant    ~127.96 ms / 7.82/s
```

D3D12 also produced fence-wait diagnostics.

Decision:

**Sentis GPUCompute is rejected as the low-end Intel HD 620 inference backend.**

This is hardware-specific evidence, not a claim that GPU acceleration is always bad. Stronger/discrete GPUs may be evaluated later. Do not switch the current low-end path back to Sentis GPUCompute without new evidence.

## 5. OpenVINO raw feasibility and precision decision

The exact recurring landmark TFLite model was tested directly with OpenVINO on the USER machine.

Precision-controlled representative results:

```text
OpenVINO CPU_DEFAULT
FP32 / PERFORMANCE       mean ~10.355 ms   ~96.57/s serial

OpenVINO GPU_DEFAULT
FP16 / PERFORMANCE       mean ~8.793 ms    ~113.73/s serial

OpenVINO GPU_ACCURACY_FP32
FP32 / ACCURACY          mean ~12.858 ms   ~77.78/s serial
```

The default Intel GPU path was slightly faster but used reduced precision. CPU FP32 versus forced GPU FP32 was numerically extremely close, but GPU FP32 was slower than CPU FP32 on this HD 620.

Decision for the low-end baseline:

**OpenVINO CPU FP32 is the preferred accelerated inference profile.**

GPU FP16 remains a possible future accelerated profile for stronger hardware after full end-to-end semantic/visual testing. Do not make GPU FP16 the low-end default merely because an isolated raw network benchmark is slightly faster.

## 6. Reuse-first architecture — critical design decision

The optimization track explicitly rejected rebuilding MediaPipe pose semantics manually just to use OpenVINO.

The accepted direction was:

```text
MediaPipe preprocessing / detector decode / NMS / ROI / tracking
        ↓
replace only neural inference execution
        ↓
MediaPipe landmark decode / refinement / presence / visibility
world-landmark processing / projection
        ↓
Golden Needle provider observation
        ↓
canonical / calibration / retarget consumers
```

Why this matters:

- MediaPipe already owns mature detector/ROI/tracking/decode/world semantics;
- reimplementing them in C#/custom code would create a large semantic-equivalence burden;
- Golden Needle's accepted Phase 2–4 behavior depends on those semantics;
- replacing only inference keeps the optimization additive and reversible.

`Docs/inference-architecture-reuse-audit.md` is the detailed research source for this decision.

The Intel MediaPipe/OpenVINO work was used only as architectural reference. Its older MediaPipe generation was not adopted wholesale. The Golden Needle integration targets Homuler/MediaPipe `0.10.22` semantics.

## 7. Gate A and Gate B — evidence before Unity integration

### Gate A

The exact production detector was proven directly compatible with OpenVINO without densification/conversion/substitute weights.

The exact detector contract and finite CPU/GPU inference passed. This established that the Sentis `DENSIFY` issue was not an OpenVINO model-compatibility blocker.

### Gate B — recorded-sequence semantic parity/capacity proof

A 363-frame recorded sequence compared:

```text
TASKS_REFERENCE
GRAPH_TFLITE_CPU
GRAPH_OPENVINO_CPU_FP32
```

Representative results:

```text
TASKS_REFERENCE mean             ~28.739 ms / 34.796/s offline capacity
GRAPH_TFLITE_CPU mean            ~28.094 ms / 35.595/s
GRAPH_OPENVINO_CPU_FP32 mean     ~14.032 ms / 71.265/s
pose-presence agreement          362/363 (~99.7245%)
normalized XYZ RMSE              ~0.01113
world 3D RMSE                    ~0.02188 m
OpenVINO bridge-copy mean        ~0.2025 ms
```

These were VIDEO-mode/offline graph-capacity results, not Unity LIVE_STREAM latency promises.

Gate B was enough to authorize a controlled Unity integration because it showed a large compute advantage with close semantic agreement while keeping MediaPipe calculators around the inference node.

## 8. Native OpenVINO Unity integration

The completed additive Windows integration uses:

- Homuler MediaPipeUnityPlugin `0.16.3`;
- MediaPipe `0.10.22`;
- OpenVINO `2026.3.0`;
- exact production detector + landmark TFLite models;
- explicit OpenVINO CPU FP32;
- MediaPipe preprocessing/tracking/decode/world semantics.

The native plugin is additive and does **not** replace stock `mediapipe_c.dll`.

The managed provider exposes selectable neural backends:

```text
MediaPipeTfliteCpu
OpenVinoCpuFp32
```

Important policy:

- explicit OpenVINO selection fails closed rather than silently pretending fallback is OpenVINO;
- stock MediaPipe/TFLite remains available as the known-safe fallback/reference;
- no serialized/default backend policy change is implied just because OpenVINO is the best-tested low-end path.

Useful native proof provenance includes workflow run `34805228184`, artifact `golden-needle-u2-native`, successful lifecycle self-test and a real-frame 33-normalized/world-landmark semantic smoke.

## 9. U4 live Unity A/B — compute advantage was initially hidden

Same-machine USER testing established that OpenVINO was semantically usable in the live Unity app.

Representative pre-scheduling-fix evidence:

```text
Stock MediaPipe/TFLite CPU:
camera                 ~30.3 FPS
fresh results          ~13.7/s
inference/result       ~44.4 ms
frame -> result        ~97.7 ms

OpenVINO CPU FP32:
camera                 ~29.5 FPS
fresh results          ~12.8/s
graph/inference        ~31.9 ms
frame -> result        ~98.5 ms
prepared -> launch     ~19.9 ms
```

The USER reported that OpenVINO already felt smoother in F12.

Interpretation:

**OpenVINO compute was genuinely faster, but avoidable scheduling/readback latency was masking the end-to-end gain.**

The correct response was not to abandon OpenVINO or alter pose semantics. The optimization moved upstream into scheduling and acquisition.

## 10. Accepted scheduling architecture — persistent worker + two-slot mailbox

The post-U4 scheduling optimization is **USER ACCEPTED — PASS**.

Architecture:

```text
camera / prepared CPU frame
    ↓
reusable two-slot latest-frame mailbox
    ↓
one persistent OpenVINO worker
```

Hard invariants:

- maximum one active OpenVINO inference;
- maximum one replaceable newest pending frame;
- exactly two reusable frame-storage slots for this scheduling model;
- active storage cannot be overwritten;
- newer input replaces pending input instead of creating backlog;
- no third/history/FIFO/replay/catch-up queue;
- latest useful frame wins;
- worker/native teardown is joined safely before disposal.

The worker may continue immediately from the newest pending frame after an inference completes. Telemetry identifies this as `OVW` / OpenVinoWorkerContinuation.

Representative post-fix evidence:

```text
prepared -> launch    ~0.0 ms on fast path
frame delta           0
launch origin         RB / OVW
pending               0 representative snapshot
active                1
buffers               2
```

The USER reported a major responsiveness improvement in F12.

Do not reintroduce Update-bound waiting or add a queue. This scheduling architecture is considered closed/accepted absent new reproducible evidence.

Detailed source: `Docs/openvino-unity-scheduling-optimization-progress.md`.

## 11. Readback bottleneck discovered after scheduling was fixed

Once prepared-to-launch delay collapsed, telemetry made the next bottleneck clear: AsyncGPUReadback completion.

Representative post-scheduling evidence:

```text
camera                 ~28-30 FPS
fresh pose results     ~12-13/s
frame -> result        ~100-105 ms

DirectCPU readback
submit -> callback     ~42.7 ms median
                       ~54.8 ms p95
callback -> poll       ~0.8 ms median
poll -> publish        ~0.2 ms median
```

This proved that callback/coroutine micro-optimization was not the main answer. The expensive interval was waiting for GPU-to-CPU availability.

A read-only audit therefore rejected/deferred as primary solutions:

- more callback/coroutine rearrangement;
- standard Homuler CPUAsync, because it still uses AsyncGPUReadback;
- forcing D3D12;
- a custom Windows/Media Foundation camera stack before trying a lower-risk Unity CPU-pixel path.

The selected experiment was `WebCamTexture.GetPixels32(Color32[] reusableBuffer)` with reusable CPU preparation.

## 12. Accepted WebCamCPU/GetPixels32 acquisition path

The R2 acquisition experiment is **USER ACCEPTED — PASS FOR CURRENT MILESTONE**.

Best-tested path:

```text
WebCamTexture
  -> GetPixels32(reused camera-sized Color32[])
  -> reusable CPU resize + H/V transform
  -> persistent 320x240 RGBA buffer
  -> accepted OpenVINO latest-frame mailbox
  -> persistent OpenVINO worker
```

Important implementation properties:

- explicit/selectable `WebCamCpuPixels` mode;
- active for the OpenVINO CPU FP32 path;
- reusable camera and body buffers;
- optimized 2:1 box-average path for 640x480 -> 320x240;
- general bilinear fallback for other valid dimensions;
- existing H/V/rotation/canonical semantics preserved;
- no queue, history, replay, second worker, or native-model change;
- ExistingReadback remains available as fallback/reference.

Representative measured acquisition cost:

```text
GetPixels32             ~0.3 ms
CPU preparation         ~3.6 ms
CPU acquisition total   ~3.9 ms
```

Strongest full-body USER phone-recorded evidence, avoiding OBS overhead:

```text
camera capture          ~28.6-30.3 FPS
fresh pose results      ~26.7-29.3/s
render                  ~30-37 FPS
CPU acquisition total   ~3.9 ms
OpenVINO graph/inference commonly ~25-35 ms
frame -> result         commonly ~33-62 ms
```

Compared with the earlier ~12-13 fresh results/s and ~100 ms frame-to-result behavior, practical fresh-pose throughput roughly doubled and representative end-to-end latency was roughly halved.

The optimized body stream now approaches the ~30 Hz camera ceiling in healthy full-body conditions on the proof machine.

Detailed source: `Docs/openvino-unity-readback-optimization-progress.md`.

## 13. Camera-framing caveat discovered during optimization

At one point half-body framing showed camera behavior around ~15 FPS, while later healthy full-body tests returned around ~30 FPS.

The accepted interpretation is that this was camera/environment/driver behavior, not a Golden Needle code cap. Do not redesign the inference scheduler merely because one camera/framing condition changes capture FPS.

When diagnosing lower result cadence, first separate:

- actual camera capture FPS;
- CPU acquisition/preparation time;
- prepared-to-launch delay;
- inference/graph time;
- published-result rate;
- pose age/frame-to-result latency.

The system cannot produce 30 fresh results/s if the camera itself is currently delivering ~15 frames/s.

## 14. Current best-tested body pipeline

This is the current optimization baseline that a new Orchestrator must understand before changing performance code:

```text
Unity WebCamTexture
  -> WebCamCPU/GetPixels32 reusable acquisition
  -> reusable CPU resize / orientation preparation to 320x240
  -> bounded two-slot latest-frame mailbox
  -> one persistent OpenVINO CPU FP32 worker
  -> MediaPipe 0.10.22 preprocessing/tracking/decode/world semantics
  -> 33 normalized + world landmarks
  -> Golden Needle provider observation
  -> canonical body mapping
  -> stable calibration/locomotion path
  -> selectable avatar-drive filtering
  -> Phase 4 positional/IK retarget
  -> optional Foundation E detail
  -> presentation
```

The USER's first comprehensive Foundations A–E QA baseline currently has these toggles/settings enabled:

```text
Body Reference Downscale        ON
Immediate Launch After Readback ON
WebCam CPU Pixels               ON
Direct Body CPU Readback        ON
OpenVINO CPU                    ON
Presentation Smoothing          OFF
```

Keep these fixed during the first complete QA pass. If diagnostics disagree with the intended effective acquisition/backend, investigate rather than silently changing settings.

## 15. Downstream latency isolation — Raw vs Stable avatar drive

Once OpenVINO + WebCamCPU brought the upstream stream near camera cadence, the USER could visibly see the stabilized canonical skeleton trail the raw canonical skeleton slightly.

This was an important finding: not all remaining perceived avatar lag was inference latency.

The project therefore separated avatar-drive responsiveness from calibration/locomotion stability.

Preserved architecture:

```text
raw canonical
  +-> Stable -> calibration + locomotion
  +-> selectable avatar-drive source -> rotation/kinematic/retarget
```

USER result:

- Stable remained smooth and satisfactory;
- Raw felt dramatically more immediate / effectively near-zero-latency subjectively;
- Raw had modest additional micro-jitter/abruptness;
- no fundamental orientation/left-right/IK failure was observed in Raw.

Conclusion:

**Raw is the subjective latency reference. Stable is the stability and calibration/locomotion reference.**

Do not “optimize latency” later by silently switching calibration or locomotion to Raw.

Detailed source: `Docs/avatar-drive-source-experiment-progress.md`.

## 16. Avatar-only responsive One Euro profiles

To find a middle ground between Stable and Raw, persistent avatar-only filters were added while preserving the Stable path.

Preserved serialized meanings:

```text
StabilizedCanonical   = 0
RawCanonical          = 1
ResponsiveCanonicalA  = 2
ResponsiveCanonicalB  = 3
ResponsiveCanonicalC  = 4
ResponsiveCanonicalD  = 5
```

Profiles:

```text
Stable  = 1.0 / 0.05 / 1.0
A       = 1.5 / 0.25 / 1.0
B       = 2.0 / 0.50 / 1.0
C       = 1.0 / 0.25 / 1.0
D       = 1.0 / 0.50 / 1.0
Raw     = unfiltered positional canonical frame
```

A/B USER testing found both more responsive than Stable but still somewhat more jittery. C/D were then added to isolate beta while restoring Stable's `minCutoff=1.0`.

The USER explicitly deferred choosing a final C/D winner/default. This is **not a project blocker**.

Rules that remain locked:

- Stable remains calibration input;
- Stable remains Phase 5A locomotion input;
- responsive/Raw selection affects avatar drive only;
- do not reorder or repurpose serialized enum values without migration planning;
- no prediction/history/catch-up queue was introduced;
- final smoothing intensity/profile tuning may resume later if useful.

Detailed sources:

- `Docs/responsive-avatar-stabilizer-progress.md`;
- `Docs/responsive-avatar-beta-sweep-progress.md`.

## 17. Presentation smoothing is a separate downstream layer

Humanoid presentation smoothing is downstream of the exact Phase 4 solve and later Foundation E detail. It is not a substitute for fixing inference latency.

For the current comprehensive Foundations A–E QA, the USER intentionally has Presentation Smoothing **OFF** so actual tracking/retarget behavior is visible directly.

Do not turn presentation smoothing on merely to hide a tracking/retarget regression during the first pass.

Future visual tuning may use it after correctness is established.

## 18. Current accepted/rejected/deferred optimization decisions

### Accepted / preserve

- body input around `320x240` for current low-end baseline;
- Immediate Launch After Readback;
- DirectCPU readback path as useful fallback/reference improvement;
- OpenVINO CPU FP32 as best-tested low-end accelerated backend;
- persistent one-worker / two-slot latest-frame mailbox;
- `WebCamCpuPixels` / GetPixels32 reusable acquisition as best-tested OpenVINO path;
- stock MediaPipe/TFLite fallback/reference;
- ExistingReadback fallback/reference;
- Raw avatar drive as latency reference;
- Stable avatar drive as stability/calibration/locomotion reference;
- A/B/C/D responsive avatar profiles as preserved engineering comparison points;
- exact MediaPipe/canonical/Phase 4 semantics around the optimization path.

### Rejected for current low-end baseline

- Sentis GPUCompute on Intel HD 620;
- forcing D3D12 as a performance solution;
- rebuilding MediaPipe detector/ROI/tracking/decode logic manually without a proven need;
- adding FIFO/history/replay/catch-up queues;
- concurrent multiple OpenVINO body inferences;
- replacing exact detector weights through densification/conversion merely for importer compatibility;
- removing stock CPU fallback.

### Deferred / optional future work

- final avatar smoothing profile/default selection;
- GPU FP16 OpenVINO policy for stronger hardware;
- custom/native Windows camera capture if Unity CPU-pixel acquisition regresses on another machine;
- other camera formats/resolutions after evidence;
- minor additional inference/acquisition tuning if a new real bottleneck is measured.

The current optimization milestone is considered **complete enough for normal project development**. Do not reopen it just to chase small benchmark gains.

## 19. Performance-diagnosis order for the next Orchestrator

If future QA reports “lag,” “lower FPS,” or “skipped motion,” do not immediately modify inference code. Diagnose the pipeline stage first.

Recommended order:

1. **Camera capture rate** — is the webcam actually producing ~30 FPS in this framing/environment?
2. **Acquisition mode/effective backend** — confirm `WebCamCpuPixels` and OpenVINO CPU FP32 are actually active when expected.
3. **CPU acquisition/prep time** — GetPixels32 + resize/orientation.
4. **Prepared -> launch delay** — should remain near zero on the accepted fast path; check launch origin and worker-continuation counters.
5. **OpenVINO graph/inference time** — compare with established ~25-35 ms live representative range rather than raw 10 ms landmark-only benchmark.
6. **Fresh result rate and pose age** — verify output cadence relative to camera cadence.
7. **Raw canonical visual response** — if raw data is immediate but avatar lags, the issue may be filtering/presentation rather than inference.
8. **Stable/responsive avatar source and presentation smoothing** — only after upstream timing is healthy.
9. **Phase 4/E correctness** — separate positional/orientation bugs from latency.

Always distinguish:

- capture bottleneck;
- acquisition bottleneck;
- scheduling bottleneck;
- neural/graph compute bottleneck;
- filtering/presentation latency;
- camera/environment behavior.

## 20. Hard architecture invariants — do not casually break

The next Orchestrator should treat these as locked unless new evidence and USER approval justify a change:

- max one active body inference;
- max one replaceable latest pending body frame;
- no FIFO/history/replay/catch-up queue;
- latest useful frame wins;
- OpenVINO worker remains persistent for the accepted path;
- stock MediaPipe/TFLite remains available;
- ExistingReadback remains available;
- exact detector/landmark identity remains preserved;
- MediaPipe graph semantics remain around neural execution;
- canonical coordinates and left/right/orientation conventions remain unchanged;
- Stable Phase 3 frame remains calibration/locomotion authority;
- Phase 4 analytic IK remains positional endpoint authority;
- Foundation E remains additive post-solve detail only;
- hand tracking remains optional and cannot starve/replace body tracking;
- no optimization may silently make optional detail/finger bones mandatory;
- no default/backend/serialized policy migration without explicit review.

## 21. Relationship to Foundations A–E

Foundations A–E were built **on top of** this optimized motion pipeline; they are not a separate replacement architecture.

Foundation A commands/speech and Foundation B cameras must not accidentally change body scheduling.

Foundation C rich orientation reuses the accepted body observation and adds no second body inference.

Foundation D hand tracking is a separate lower-cadence optional stream and body performance remains priority.

Foundation E consumes already-produced rich/hand contracts and adds retarget math only; it owns no camera/inference/scheduling path.

Therefore, if A–E QA shows a body throughput regression, investigate whether foundation integration introduced overhead or lifecycle contention before reopening the accepted OpenVINO architecture itself.

## 22. Relationship to Phase 5A

Phase 5A locomotion remains implemented but not USER accepted.

Known issues:

- planted-feet leaning can still produce unwanted translation;
- stationary cadence stepping is not yet robust enough.

Do not interpret those as inference-optimization failures.

Stable canonical data remains Phase 5A locomotion authority even when Raw or responsive avatar drive is selected.

## 23. Current QA baseline and next decision point

At this handoff, Unity compilation is clean after the recent Foundation E integration compile fixes, and the USER is preparing the comprehensive Foundations A–E runtime pass.

The first pass is intentionally run with:

```text
Body Reference Downscale        ON
Immediate Launch After Readback ON
WebCam CPU Pixels               ON
Direct Body CPU Readback        ON
OpenVINO CPU                    ON
Presentation Smoothing          OFF
```

The new Orchestrator should treat this as a test of Foundations A–E **on the accepted optimized body baseline**.

If the pass shows a performance regression, compare the new evidence against the accepted pipeline measurements before changing architecture.

## 24. Authoritative optimization source documents

Read these when deeper detail is needed:

1. `Docs/openvino-unity-integration-progress.md` — resolved OpenVINO Unity integration status.
2. `Docs/openvino-unity-scheduling-optimization-progress.md` — accepted worker/mailbox optimization.
3. `Docs/openvino-unity-readback-optimization-progress.md` — accepted WebCamCPU/GetPixels32 acquisition optimization.
4. `Docs/openvino-unity-readback-audit.md` — why CPU camera pixels were chosen over other readback/capture directions.
5. `Docs/inference-architecture-reuse-audit.md` — reuse-first MediaPipe/OpenVINO design and alternative-provider analysis.
6. `Docs/openvino-unity-integration-checkpoints.md` — integration/recovery history and Gate B evidence.
7. `Docs/openvino-unity-ab-qa.md` — original Unity A/B procedure and pre-optimization reference metrics.
8. `Docs/avatar-drive-source-experiment-progress.md` — Raw vs Stable latency isolation.
9. `Docs/responsive-avatar-stabilizer-progress.md` — A/B avatar-only smoothing experiment.
10. `Docs/responsive-avatar-beta-sweep-progress.md` — C/D beta-only tuning and deferred final winner.

Use `Docs/current-state.md` for what the project is doing **now**. Use this file to preserve optimization continuity and avoid repeating already-resolved experiments.

## 25. Handoff rule

A new Orchestrator should not assume “optimization is done forever,” but it also must not restart the track from first principles.

Reopen a closed optimization decision only when there is new reproducible evidence of a bottleneck or regression. When doing so:

1. identify the actual slow stage using telemetry;
2. compare against the accepted same-machine evidence above;
3. preserve fallbacks and semantic boundaries;
4. prefer the least-invasive reversible experiment;
5. obtain USER runtime evidence before declaring a new path accepted.

No merge to `main` without explicit USER approval. Do not start Phase 6 from optimization work alone.
