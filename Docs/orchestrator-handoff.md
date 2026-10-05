# Mocap Adventure development handoff

Refresh: 2026-10-05. Work on gameplay/foundation; verify its live remote HEAD and local changes before starting. Read current-state.md, architecture.md, motion-sdk-integration.md and AGENTS.md first.

The product uses the private engine's compiled SDK preview 2. USER accepted both Unity and Windows gameplay. Do not restart extraction or provider/retargeting experiments in this repository. Engine-only historical plans/workflows now live in private Docs/HistoricalGoldenNeedle; their old paths are not current implementation instructions.

Preserve the persistent player and authored scenes. Product work owns health, session, flow, grounding, activity rules, camera/UI and speech context. Use the existing facade and SDK APIs; engine algorithms and Lab development belong in hd-motion-engine. Run python Tools/verify_motion_sdk_boundary.py for bounded repository verification.

Preserve local asset/settings edits. Full builds and similarly long-running checks are USER owned. Physical QA and measured performance require actual USER evidence. Do not merge main/PRs or rewrite history without explicit authorization. Current repository separation/publication is USER authorized; licensing/activation remains a later discussion.
