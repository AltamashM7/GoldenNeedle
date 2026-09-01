# Roadmap

This is a high-level roadmap. Distant phases are intentionally not detailed implementation commitments.

## Phase 0 — Project/repository foundation

- durable documentation;
- Unity MCP development tooling setup.

## Phase 1 — Minimal Webcam + Pose Tracking Technical Spike

Primary question: can the target PC obtain sufficiently responsive body landmarks without a dedicated GPU?

Current state: **USER ACCEPTED — PASS WITH NOTES**. Built-in laptop webcam, partial-body behavior, continuous request/result progression, and representative performance evidence were accepted. Notes: raw landmark geometry remains jittery/loose in some poses; the observations are not a formal latency benchmark.

## Phase 2 — Canonical Skeleton + Debug Visualization

Next planned phase. It remains gated until the Phase 1 changes are committed/reviewed and the Orchestrator authorizes implementation.

## Phase 3 — Calibration, confidence handling, and smoothing

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
