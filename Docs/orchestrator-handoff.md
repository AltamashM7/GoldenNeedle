# Orchestrator handoff

## Project

**Golden Needle**

## Purpose

Gamified embodied-fitness application for SIH 2026.

## Core innovation direction

Use real-time full-body motion as continuous control of a 3D avatar rather than merely recognizing a few fitness gestures.

## Target platform

Unity 6.5 + URP.

## Hard constraint

Baseline operation must not require a dedicated GPU.

## Planned motion stack

```text
Webcam
    -> MediaPipe Pose Landmarker
    -> provider abstraction
    -> canonical skeleton
    -> filtering
    -> rotation reconstruction
    -> Unity Humanoid avatar
```

Locomotion is handled separately from pose reproduction.

## Project experience

```text
Begin
    -> menu
    -> character introduction
    -> calibration
    -> control handoff
    -> Hub
    -> course
    -> Hub
```

This is planned experience, not implemented runtime behavior.

## Team model

The USER primarily owns Motion Engine work. Other developers later own courses and environments.

## Development model

```text
Web Sol Orchestrator
    + Codex Luna local Builder
    + GitHub Desktop
    + GitHub PR review
    + USER Unity QA
```

## Current phase

**PREVIEW MIRROR CORRECTED / Y-AGREEMENT AUDITED — READY FOR USER QA.** Phase 1 accepted SHA: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6` (`feat: add CPU pose-tracking spike`). Phase 2 accepted SHA: `f5a15648607adf6034800c6a2b4d685b0e6f03ea` (`feat: add canonical pose skeleton and debug visualization`). Phase 3 accepted SHA: `2ee4d6eb606a8b845183cc44126ecf9530d8280b` (`feat: add motion calibration and pose stabilization`). Phase 1–3 were committed, pushed, USER accepted with verdict **PASS WITH NOTES**, and audited by Web Sol. Phase 4 remains blocked/not USER accepted pending preview mirror/Y-agreement QA; retargeting is frozen. Phase 5 has not started.

## Tooling state

The official Unity Codex plugin `unity@unity-agent-plugin` (`0.1.0-beta`) is installed and enabled in user-level Codex configuration. The official Unity CLI (`1.0.0-beta.5`) and Unity Pipeline package (`com.unity.pipeline` `0.5.0-exp.1`) are installed. Codex MCP is configured for this project with `unity mcp --project-path ...`.

Live verification passed for the Phase 3 baseline: the MCP server initialized as `unity-mcp 1.0.0-beta.5`, exposed 142 tools, and executed editor status, compile, console, hierarchy, Play Mode, and screenshot checks against the running Golden Needle Editor. Phase 1 smoke evidence reported Unity `6000.5.0f1`, no compilation errors, integrated `HP TrueVision HD Camera` at `640x480`, CPU Pose Landmarker initialization, a live inference request, and an asynchronous result callback. Phase 2's focused EditMode mapper suite is 5/5 passing. Phase 3's focused EditMode suite is 15/15 passing together with the Phase 2 mapper tests. USER physical QA accepted Phase 3 with **PASS WITH NOTES**. The two `UnityEditor.ShaderGraph.ShaderGraphProjectSettings` warnings may occur once during script recompilation or Unity exit, but do not recur during normal Play Mode and are not considered a Golden Needle runtime blocker. Loss/reacquisition physical coverage remains a later integration-quality check. Codex records the USER's acceptance and does not independently claim it. Phase 4 requires fresh Unity compile/Play Mode and USER visual QA; this follow-up could not use a connected Pipeline Editor instance.

The plugin’s Windows support is documented as experimental. Treat the local smoke test as implementation evidence, not acceptance of tracking quality, partial-body behavior, orientation, or CPU responsiveness.

USER QA evidence recorded for this handoff: representative frames showed approximately `56–68 FPS` Unity rendering, `17–31 FPS` camera/capture, `58–93 ms` inference samples, commonly `25/33` trusted landmarks, and continuous request/result progression. Subject loss reached `WAITING / UNAVAILABLE` with `0/33` trusted landmarks. Raw landmarks showed some jitter/loose geometry, accepted as a later-phase concern. The evidence was extracted from the USER's recorded test because ChatGPT's video attachment runtime failed to mount the original MP4; this was not a Golden Needle application failure. These observations are not a formal latency benchmark.

Unity MCP and Pipeline are development tools only. They must not be added as runtime product dependencies, and future agents should use them when they materially improve Unity Editor inspection or verification.

## Phase 4 implementation boundary

`ICanonicalPoseSource` isolates MediaPipe behind an adapter, and `MotionEngineRuntime` owns the reusable canonical -> stabilized -> calibrated -> rotation plus kinematic-target pipeline. The canonical foundation now defines the correctly oriented MediaPipe inference image as the common camera frame. The provider applies H/V/rotation only while preparing pixels; the preview explicitly corrects the tested front-facing raw-source mirror and uses sensor rotation/vertical metadata, the presenter restores the texture-only GUI transform before overlays, and the mapper performs only normalized `x, 1-y` and world `x, -y, z` conversion. Display mirroring remains explicit presentation policy, canonical Z remains away, and coordinate-convention changes reset in-memory stabilization/calibration state. `CanonicalRotationFrame` remains the ten-bone provider-independent frame for pelvis/chest orientation, diagnostics, and future orientation. The existing `CanonicalKinematicTargetBuilder`, chain characterization, `M_chain`, analytic IK, `HumanoidRigBinding`, `HumanoidRetargeter`, target frames, and procedural rig are frozen until this audit is accepted. Diagnostics retain canonical 2D↔3D X/Y agreement, use anatomical Y pairs with independent separation thresholds, and add a toggleable raw-world/canonical coordinate inspector.

The permanent `PoseTrackingSpike` Motion Engine Lab retains the existing `R`, `C`, `X`, and `F1`–`F5` controls and adds `F6` for the raw-world/canonical coordinate inspector. The preview uses sensor/display metadata, while overlays use the canonical inference-frame data through the shared content rectangle; an optional display mirror is applied consistently to both presentation paths. The procedural rig remains present but is not part of this foundation's USER QA. No model asset, locomotion, CharacterController, root motion, Hub, course, cinematic, or gameplay integration is included.

## Next immediate step

The immediate step is **USER QA of the Phase 4 preview mirror/Y-agreement audit**. The USER should judge the preview, canonical 2D, and F3 only; retargeting and the procedural rig remain frozen. After USER acceptance, the Phase 4 checkpoint commit will represent the accepted implementation. The next planned phase after that checkpoint is **Phase 5 — Locomotion prototype**; do not begin Phase 5 from this handoff.

## Important governance

Future Orchestrators must inspect `AGENTS.md`, `Docs/current-state.md`, `Docs/decisions.md`, `Docs/architecture.md`, and current GitHub state before directing implementation. Do not assume old chat context is available.

Core Motion Engine phases use the long-lived `engine/pose-tracking-spike` branch through the Phase 6 graybox checkpoint. Each accepted phase follows implementation -> USER QA -> accepted checkpoint commit/push -> Web Sol GitHub audit. Intermediate checkpoints are not mechanically merged into `main`; the intended merge boundary is after Phase 6 graybox acceptance, subject to the current Orchestrator brief.

The USER normally performs Git mutations through GitHub Desktop. Follow the current brief for any explicitly authorized exception. Do not claim Unity visual or physical QA without actual USER verification, and do not merge without explicit USER approval.
