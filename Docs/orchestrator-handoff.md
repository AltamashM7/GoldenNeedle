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

**Phase 3 Calibration + Confidence Handling + Temporal Smoothing — USER ACCEPTED — PASS WITH NOTES.** Phase 1 accepted SHA: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6` (`feat: add CPU pose-tracking spike`). Phase 2 accepted SHA: `f5a15648607adf6034800c6a2b4d685b0e6f03ea` (`feat: add canonical pose skeleton and debug visualization`). Phase 2 was committed, pushed, USER accepted with verdict **PASS WITH NOTES**, and audited by Web Sol. USER QA confirmed the webcam preview is upright, the raw/cyan overlay visually aligns, the canonical 2D/yellow overlay is now upright after fixing the double Y inversion, canonical 3D/local-space visualization behaves plausibly, partial-body canonical tracking remains valid, and the Phase 2 mapper tests passed. Phase 3 USER QA passed neutral calibration, T-pose recognition, calibration completion, and the visibly smoother stabilized `F4` pose. USER-rated responsiveness was **Good**; observed evidence was approximately `60+ FPS` rendering, `7–8/s` pose requests/results, and approximately `60 ms` inference. Focused EditMode coverage passed `15/15`. Loss/reacquisition edge cases were not exhaustively physically tested and remain a later integration-quality check, not a Phase 3 blocker. Phase 4 has not started.

## Tooling state

The official Unity Codex plugin `unity@unity-agent-plugin` (`0.1.0-beta`) is installed and enabled in user-level Codex configuration. The official Unity CLI (`1.0.0-beta.5`) and Unity Pipeline package (`com.unity.pipeline` `0.5.0-exp.1`) are installed. Codex MCP is configured for this project with `unity mcp --project-path ...`.

Live verification passed: the MCP server initialized as `unity-mcp 1.0.0-beta.5`, exposed 142 tools, and executed editor status, compile, console, hierarchy, Play Mode, and screenshot checks against the running Golden Needle Editor. Phase 1 smoke evidence reported Unity `6000.5.0f1`, no compilation errors, integrated `HP TrueVision HD Camera` at `640x480`, CPU Pose Landmarker initialization, a live inference request, and an asynchronous result callback. Phase 2's focused EditMode mapper suite is 5/5 passing. Phase 3's focused EditMode suite is 15/15 passing together with the Phase 2 mapper tests. USER physical QA accepted Phase 3 with **PASS WITH NOTES**. The two `UnityEditor.ShaderGraph.ShaderGraphProjectSettings` warnings may occur once during script recompilation or Unity exit, but do not recur during normal Play Mode and are not considered a Golden Needle runtime blocker. Loss/reacquisition physical coverage remains a later integration-quality check. Codex records the USER's acceptance and does not independently claim it.

The plugin’s Windows support is documented as experimental. Treat the local smoke test as implementation evidence, not acceptance of tracking quality, partial-body behavior, orientation, or CPU responsiveness.

USER QA evidence recorded for this handoff: representative frames showed approximately `56–68 FPS` Unity rendering, `17–31 FPS` camera/capture, `58–93 ms` inference samples, commonly `25/33` trusted landmarks, and continuous request/result progression. Subject loss reached `WAITING / UNAVAILABLE` with `0/33` trusted landmarks. Raw landmarks showed some jitter/loose geometry, accepted as a later-phase concern. The evidence was extracted from the USER's recorded test because ChatGPT's video attachment runtime failed to mount the original MP4; this was not a Golden Needle application failure. These observations are not a formal latency benchmark.

Unity MCP and Pipeline are development tools only. They must not be added as runtime product dependencies, and future agents should use them when they materially improve Unity Editor inspection or verification.

## Next immediate step

The immediate step is the **Phase 3 checkpoint commit**, which will represent the USER-accepted Phase 3 implementation. The next planned phase after that checkpoint is **Phase 4 — Humanoid retargeting**. Phase 4 has not started; do not begin it from this handoff.

## Important governance

Future Orchestrators must inspect `AGENTS.md`, `Docs/current-state.md`, `Docs/decisions.md`, `Docs/architecture.md`, and current GitHub state before directing implementation. Do not assume old chat context is available.

Core Motion Engine phases use the long-lived `engine/pose-tracking-spike` branch through the Phase 6 graybox checkpoint. Each accepted phase follows implementation -> USER QA -> accepted checkpoint commit/push -> Web Sol GitHub audit. Intermediate checkpoints are not mechanically merged into `main`; the intended merge boundary is after Phase 6 graybox acceptance, subject to the current Orchestrator brief.

The USER normally performs Git mutations through GitHub Desktop. Follow the current brief for any explicitly authorized exception. Do not claim Unity visual or physical QA without actual USER verification, and do not merge without explicit USER approval.
