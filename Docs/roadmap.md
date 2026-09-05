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

**USER ACCEPTED — PASS**

Accepted implementation SHA: `f0c81e84d0a482c40448505f2904af93ef4aa881`.

Phase 4 now includes modular measurement calibration, stabilized positional chain targets, explicit canonical-to-avatar basis mapping, analytic two-bone IK, procedural rig validation, and structural Animator Humanoid binding. Repository-first debugging localized the final real-avatar orientation defect to front-camera source semantics: the automatic inference H mirror reversed anatomical Left/Right, and the body basis then used the wrong cross-product order for the unmirrored frontal convention. The accepted correction keeps front-camera inference unmirrored and derives semantic Forward with `Cross(Right, Up)`, giving approximately `R≈-X, U≈+Y, F≈-Z` for a frontal user.

Final USER QA passed real Neko facing/orientation and approximately 45° torso yaw left/right without avatar-side rotation compensation. The failed `83239b...` HumanPose semantic-forward experiment remains removed.

## Phase 5 — Locomotion prototype

**NOT STARTED.**

Phase 4 is accepted, so Phase 5 may now begin. POSE and locomotion remain separate.

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
