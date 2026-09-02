# Roadmap

This is a high-level roadmap. Distant phases are intentionally not detailed implementation commitments.

## Phase 0 — Project/repository foundation

- durable documentation;
- Unity MCP development tooling setup.

## Phase 1 — Minimal Webcam + Pose Tracking Technical Spike

Primary question: can the target PC obtain sufficiently responsive body landmarks without a dedicated GPU?

Current state: **USER ACCEPTED — PASS WITH NOTES**. Built-in laptop webcam, partial-body behavior, continuous request/result progression, and representative performance evidence were accepted. Notes: raw landmark geometry remains jittery/loose in some poses; the observations are not a formal latency benchmark.

## Phase 2 — Canonical Skeleton + Debug Visualization

Checkpoint: **USER ACCEPTED — PASS WITH NOTES** at `f5a15648607adf6034800c6a2b4d685b0e6f03ea`. USER QA confirmed the webcam preview is upright, the raw/cyan overlay visually aligns, the corrected canonical 2D/yellow overlay is upright after fixing the double Y inversion, the canonical 3D/local-space visualization behaves plausibly, and partial-body canonical tracking remains valid. The Phase 2 mapper tests passed. The two `UnityEditor.ShaderGraph.ShaderGraphProjectSettings` warnings may occur once during script recompilation or Unity exit, but do not recur during normal Play Mode and are not considered a Golden Needle runtime blocker.

Phase 2 was committed, pushed, USER accepted, and audited by Web Sol at `f5a15648607adf6034800c6a2b4d685b0e6f03ea`. The current implementation is Phase 3 — Calibration + Confidence Handling + Temporal Smoothing — **USER ACCEPTED — PASS WITH NOTES**. Phase 4 has not started.

## Phase 3 — Calibration, confidence handling, and smoothing

Current status: **USER ACCEPTED — PASS WITH NOTES**. Neutral calibration, T-pose recognition, calibration completion, and the visibly smoother stabilized `F4` pose passed USER QA. USER-rated responsiveness was **Good**; observed evidence was approximately `60+ FPS` rendering, `7–8/s` pose requests/results, and approximately `60 ms` inference. Focused EditMode tests passed `15/15`. Loss/reacquisition edge cases were not exhaustively physically tested and remain a later integration-quality check, not a Phase 3 blocker. The next checkpoint commit will represent the accepted Phase 3 implementation. Phase 4 has not started.

## Phase 4 — Humanoid retargeting

Primary question: does a humanoid convincingly reproduce the user's full-body movement?

## Phase 5 — Locomotion prototype

## Phase 6 — End-to-end graybox vertical slice

```text
Launch
    -> Start Fitness
    -> calibration
    -> avatar control
    -> minimal Hub
    -> test course
    -> completion
    -> return to Hub
```

## Phase 7 — Production Hub

## Phase 8 — Course framework freeze and parallel course development

## Phase 9 — Presentation systems

- UI;
- cinematics;
- character interaction;
- progression/results;
- presentation polish.

## Phase 10 — Hardening

- optimization;
- low-end-hardware validation;
- robustness;
- SIH demonstration hardening;
- presentation and demo preparation.
