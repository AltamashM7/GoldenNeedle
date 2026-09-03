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

Phase 2 was committed, pushed, USER accepted, and audited by Web Sol at `f5a15648607adf6034800c6a2b4d685b0e6f03ea`. The current implementation is **PREVIEW MIRROR CORRECTED / Y-AGREEMENT AUDITED — READY FOR USER QA**. Phase 3 is represented by accepted SHA `2ee4d6eb606a8b845183cc44126ecf9530d8280b`; it was committed, pushed, USER accepted, and audited by Web Sol. Phase 4 retargeting remains frozen pending preview mirror/Y-agreement acceptance. Phase 5 has not started.

## Phase 3 — Calibration, confidence handling, and smoothing

Current status: **USER ACCEPTED — PASS WITH NOTES**. Neutral calibration, T-pose recognition, calibration completion, and the visibly smoother stabilized `F4` pose passed USER QA. USER-rated responsiveness was **Good**; observed evidence was approximately `60+ FPS` rendering, `7–8/s` pose requests/results, and approximately `60 ms` inference. Focused EditMode tests passed `15/15`. Loss/reacquisition edge cases were not exhaustively physically tested and remain a later integration-quality check, not a Phase 3 blocker. Accepted SHA: `2ee4d6eb606a8b845183cc44126ecf9530d8280b`.

## Phase 4 — Humanoid retargeting

Primary question: does a humanoid convincingly reproduce the user's full-body movement?

Current status: **PREVIEW MIRROR CORRECTED / Y-AGREEMENT AUDITED — READY FOR USER QA**. The phase retains `MotionEngineRuntime`, the provider-independent canonical source boundary, calibration, stabilization, canonical rotation-frame torso reconstruction, explicit/Animator Humanoid binding, the procedural debug rig, RenderTexture rig view, partial-body behavior, fixed root, authored proportions, and F1–F6 controls. The current foundation defines one canonical inference frame shared by canonical 2D, canonical 3D, calibration, stabilization, and future retargeting input. The physical preview explicitly corrects the tested front-facing source mirror and uses sensor rotation/vertical metadata; its texture transform is kept separate from canonical overlay mapping, and the same net presentation X transform is applied to preview and overlays. MediaPipe normalized/world mapping uses only `x, 1-y` and `x, -y, z`; input H/V/rotation is used for pixel preparation and not mechanically applied to returned world data. F6 exposes raw-world/canonical coordinate samples plus anatomical and legacy-bilateral Y-pair diagnostics, while existing retargeting and IK remain frozen. No Animation Rigging package, external humanoid asset, HumanPoseHandler primary path, locomotion, or Phase 5 work is included. Phase 4 remains uncommitted pending USER QA and acceptance. Phase 5 has not started.

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
