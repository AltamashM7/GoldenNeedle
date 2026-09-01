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

**Phase 1 minimal webcam and CPU pose-tracking spike — USER ACCEPTED.** Verdict: **PASS WITH NOTES**. The repository contains the isolated `PoseTrackingSpike` scene, a repository-local MediaPipeUnityPlugin `0.16.3` CPU runtime subset, a local Pose Landmarker Lite model, and the provider/presenter implementation. It does not contain the canonical skeleton, avatar-control, calibration, locomotion, Hub, course, or final presentation systems.

## Tooling state

The official Unity Codex plugin `unity@unity-agent-plugin` (`0.1.0-beta`) is installed and enabled in user-level Codex configuration. The official Unity CLI (`1.0.0-beta.5`) and Unity Pipeline package (`com.unity.pipeline` `0.5.0-exp.1`) are installed. Codex MCP is configured for this project with `unity mcp --project-path ...`.

Live verification passed: the MCP server initialized as `unity-mcp 1.0.0-beta.5`, exposed 142 tools, and executed editor status, compile, console, hierarchy, Play Mode, and screenshot checks against the running Golden Needle Editor. The final smoke test reported Unity `6000.5.0f1`, no compilation errors, integrated `HP TrueVision HD Camera` at `640x480`, CPU Pose Landmarker initialization, a live inference request, and an asynchronous result callback. The final Editor state was returned to `ready` with Play Mode stopped. USER physical/visual QA is complete and accepted as **PASS WITH NOTES**.

The plugin’s Windows support is documented as experimental. Treat the local smoke test as implementation evidence, not acceptance of tracking quality, partial-body behavior, orientation, or CPU responsiveness.

USER QA evidence recorded for this handoff: representative frames showed approximately `56–68 FPS` Unity rendering, `17–31 FPS` camera/capture, `58–93 ms` inference samples, commonly `25/33` trusted landmarks, and continuous request/result progression. Subject loss reached `WAITING / UNAVAILABLE` with `0/33` trusted landmarks. Raw landmarks showed some jitter/loose geometry, accepted as a later-phase concern. The evidence was extracted from the USER's recorded test because ChatGPT's video attachment runtime failed to mount the original MP4; this was not a Golden Needle application failure. These observations are not a formal latency benchmark.

Unity MCP and Pipeline are development tools only. They must not be added as runtime product dependencies, and future agents should use them when they materially improve Unity Editor inspection or verification.

## Next immediate step

Next planned phase: **Phase 2 — Canonical Skeleton + Debug Visualization**. Do not begin Phase 2 in this follow-up; wait for the Phase 1 commit/review and explicit Orchestrator authorization.

## Important governance

Future Orchestrators must inspect `AGENTS.md`, `Docs/current-state.md`, `Docs/decisions.md`, `Docs/architecture.md`, and current GitHub state before directing implementation. Do not assume old chat context is available.

The USER normally performs Git mutations through GitHub Desktop. Follow the current brief for any explicitly authorized exception. Do not claim Unity visual or physical QA without actual USER verification, and do not merge without explicit USER approval.
