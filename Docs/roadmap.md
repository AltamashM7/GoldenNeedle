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

**STARTED — PHASE 5A IMPLEMENTED / NOT USER ACCEPTED.**

### Phase 5A — Embodied Hybrid Locomotion Prototype

Implemented prototype combines:

- finite camera-space physical X/Z displacement from image placement + yaw-compensated apparent scale;
- cadence-based infinite-range extension from alternating lower-body rhythm;
- body-heading steering through the accepted Phase 4 source-to-avatar basis;
- physical/cadence fusion that suppresses cadence during meaningful actual translation;
- no-jump recenter via public `Recenter()`;
- no direct root-Y copying;
- minimal runtime grid/view and compact diagnostics.

Runtime USER QA is authoritative for scale, noise, cadence acquisition/stop, steering, and blend feel. Phase 5 remains unaccepted until that QA passes.

<!-- PHASE5A_CHECKPOINT_2026_09_08:START -->
### Current Phase 5A checkpoint

Runtime-test HEAD before the docs checkpoint: `a4b0ffb7b817f42bd3901da7e0676b68abca70a3`.

Implementation is complete enough for first USER QA and has been Orchestrator code-audited. The camera-to-avatar physical coordinate mapping defect found in the first audit was corrected at `5ed42bc1c15b058016ea24036aa8dd490a5f67f4`. The Lab overlay/control pass at `a4b0ffb7b817f42bd3901da7e0676b68abca70a3` removes debug-panel overlap as a QA blocker.

**Acceptance gate still pending:** one USER runtime session covering idle drift, physical X/Z/diagonal displacement, Phase 4 pose regression, cadence activation/heading/stop, physical-vs-cadence double-counting, and recenter.

No Phase 6 work should begin before this evidence is reviewed.
<!-- PHASE5A_CHECKPOINT_2026_09_08:END -->

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
