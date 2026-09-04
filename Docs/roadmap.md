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

USER QA historically passed the Phase 3 calibration/T-pose implementation, visible smoothing improvement and Good responsiveness. Phase 4 now deliberately supersedes hard T-pose recognition with modular measurement calibration; the Phase 3 acceptance remains historical rather than a requirement to preserve that UI/architecture.

## Phase 4 — Humanoid retargeting

**BLOCKED / INVESTIGATION CHECKPOINT — NOT USER ACCEPTED**

Pre-correction handoff HEAD: `4a26589ec2f90688b80fb6b1da0b849adda65d6b` (runtime parent `5e830dce7ac3de542ab159b8b90992935d9dd0b0`).

A repository-first correction removed the unintended front-camera presentation flip and replaced reflected/per-chain quaternion production mapping with an explicit signed canonical-to-avatar basis map feeding positional analytic IK. USER QA has now passed 2D presentation, modular calibration, procedural F3/F5 retargeting, real Animator Humanoid binding, and real Humanoid limb response. Real-avatar body/facing orientation remains unresolved. The `83239b...` HumanPose semantic-forward experiment failed USER QA and has been removed.

Phase 4 remains **NOT USER ACCEPTED**. The correction must not be merged into `main` until fresh USER motion/visual QA and Orchestrator audit pass.

**Immediate next step:** test the restored pre-HumanPose retarget behavior with the real Neko avatar deliberately placed at root Y=180, matching the procedural Lab's authored facing convention. That exact combination is not yet USER-tested. If facing is corrected without regressing limb response, proceed to the final Phase 4 audit.

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
