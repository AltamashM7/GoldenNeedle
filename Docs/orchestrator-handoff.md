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

**Phase 0 foundation.** The repository contains Unity/URP baseline assets, a template/sample scene, and durable documentation. It does not contain the runtime webcam, pose, avatar-control, Hub, course, or final presentation systems.

## Next immediate step

Configure and verify Unity MCP development tooling after the foundation commit, then begin the Phase 1 pose-tracking technical spike.

## Important governance

Future Orchestrators must inspect `AGENTS.md`, `Docs/current-state.md`, `Docs/decisions.md`, `Docs/architecture.md`, and current GitHub state before directing implementation. Do not assume old chat context is available.

The USER normally performs Git mutations through GitHub Desktop. Follow the current brief for any explicitly authorized exception. Do not claim Unity visual or physical QA without actual USER verification, and do not merge without explicit USER approval.
