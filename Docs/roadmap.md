# Mocap Adventure roadmap

Refresh: 2026-10-05. Read current-state.md for verified status.

The reusable motion engine has been separated into private hd-motion-engine. Its compiled SDK connection is USER accepted in Unity Editor and Windows. The remaining repository work is publication/promotion of the accepted migration and removal of stale engine-only plans/workflows from the product current tree. Main integration is a separate explicit approval.

Existing product foundations are preserved: persistent player/session/facade, health/body anchors, GameFlow, contextual speech/UI commands, Calibration presentation and Hub integration. The existing enabled activity scenes are retained without changing rules or portal wiring during extraction.

After repository separation, choose the next product task from the actual current scenes and contracts: activity gameplay/entry and return flow, results/presentation polish, recovery robustness and SIH demonstration hardening. Read game-flow.md, gameplay-foundation-plan.md, hub-integration-plan.md and course-contract.md as product design/history; inspect current assets before interpreting historical pending status.

Future engine capabilities/performance work belongs in the private Lab and reaches the game through versioned SDK updates. Licensing/activation design will be discussed separately when requested. Full Windows builds and similarly long-running checks remain USER owned.
