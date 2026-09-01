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

**Phase 0 foundation and development tooling.** The repository contains Unity/URP baseline assets, a template/sample scene, durable documentation, and the official `com.unity.pipeline` package. It does not contain the runtime webcam, pose, avatar-control, Hub, course, or final presentation systems.

## Tooling state

The official Unity Codex plugin `unity@unity-agent-plugin` (`0.1.0-beta`) is installed and enabled in user-level Codex configuration. The official Unity CLI (`1.0.0-beta.5`) and Unity Pipeline package (`com.unity.pipeline` `0.5.0-exp.1`) are installed. Codex MCP is configured for this project with `unity mcp --project-path ...`.

Live verification passed: the MCP server initialized as `unity-mcp 1.0.0-beta.5`, exposed 142 tools, and executed the read-only `editor_status` tool against the running Golden Needle Editor. It reported Unity `6000.5.0f1`, project path, `ready`, not compiling, and Play Mode stopped. Unity CLI's separate discovery/status probe still reports no reachable instance even though the authenticated Pipeline/MCP server works; treat the authenticated MCP path as authoritative and investigate the CLI probe if it affects future work.

Unity MCP and Pipeline are development tools only. They must not be added as runtime product dependencies, and future agents should use them when they materially improve Unity Editor inspection or verification.

## Next immediate step

USER reviews and commits the Phase 0F tooling changes through GitHub Desktop, then Web Sol Orchestrator audits the final branch before the Phase 1 pose-tracking technical spike begins.

## Important governance

Future Orchestrators must inspect `AGENTS.md`, `Docs/current-state.md`, `Docs/decisions.md`, `Docs/architecture.md`, and current GitHub state before directing implementation. Do not assume old chat context is available.

The USER normally performs Git mutations through GitHub Desktop. Follow the current brief for any explicitly authorized exception. Do not claim Unity visual or physical QA without actual USER verification, and do not merge without explicit USER approval.
