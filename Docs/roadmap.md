# Roadmap

This is a high-level roadmap. Distant phases are intentionally not detailed implementation commitments.

## Phase 0 — Project/repository foundation

Complete: durable documentation, Unity project/tooling foundation.

## Phase 1 — Minimal Webcam + Pose Tracking Technical Spike

**USER ACCEPTED — PASS WITH NOTES**

Accepted SHA: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6`.

## Phase 2 — Canonical Skeleton + Debug Visualization

**USER ACCEPTED — PASS WITH NOTES**

Accepted SHA: `f5a15648607adf6034800c6a2b4d685b0e6f03ea`.

## Phase 3 — Calibration, confidence handling, and smoothing

**USER ACCEPTED — PASS WITH NOTES**

Accepted SHA: `2ee4d6eb606a8b845183cc44126ecf9530d8280b`.

USER QA passed calibration, T-pose recognition, visible smoothing improvement and Good responsiveness. Loss/reacquisition edge cases were not exhaustively physically tested and remain a later integration-quality check.

## Phase 4 — Humanoid retargeting

**BLOCKED / INVESTIGATION CHECKPOINT — NOT USER ACCEPTED**

Current investigative SHA: `5e830dce7ac3de542ab159b8b90992935d9dd0b0`.

The branch contains exploratory runtime/retargeting, analytic IK, procedural-rig, coordinate-foundation and presentation work. Repeated USER QA exposed unresolved retarget accuracy and camera/presentation issues. The latest visible state has F3 substantially improved and canonical 2D aligned, while the webcam preview still appears horizontally mirrored. The procedural rig remains inaccurate/unaccepted from prior multi-pose QA.

The Phase 4 checkpoint exists to preserve the exact working state for repository-first diagnosis. It is not an accepted architecture checkpoint and must not be merged into `main`.

**Immediate next step:** a fresh Web Orchestrator performs a read-only audit of the actual branch/code, then chooses the smallest clean correction or rewrite. Do not continue from old chat hypotheses.

## Phase 5 — Locomotion prototype

**NOT STARTED.**

Must not begin until Phase 4 is deliberately resolved/accepted. POSE and locomotion remain separate.

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
- presentation/demo preparation.
