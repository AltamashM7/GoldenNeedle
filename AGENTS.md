# Golden Needle agent instructions

Golden Needle is a Unity 6.5 / URP gamified embodied-fitness application for Smart India Hackathon 2026. Its product direction is full-body webcam-driven control of a 3D avatar, not merely a small set of gesture-triggered buttons.

## Before changing the repository

- Inspect the repository root, current Git status, active branch, remotes, tracked files, and relevant existing assets.
- Verify `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `Packages/packages-lock.json`, URP configuration, `.gitignore`, and `.gitattributes` instead of trusting a task description.
- Read the relevant files in `Docs/` before implementing work. Treat [`Docs/current-state.md`](Docs/current-state.md) as the concise durable snapshot of implemented progress.
- Check for pre-existing work that may affect the requested scope. Do not silently upgrade or downgrade Unity, change the render pipeline, or perform destructive Git operations.

## Architectural boundaries

- Preserve the replaceable Pose Provider boundary and the engine-owned canonical skeleton direction.
- Keep pose reproduction separate from locomotion: **POSE != LOCOMOTION**.
- Keep Motion Engine concerns separate from Player, Hub, course, environment, UI, and cinematic content.
- Course code must not depend on MediaPipe-specific structures or internals.
- Prefer narrow phases and stable abstractions; do not freeze speculative APIs or interfaces before prototypes justify them.

## Product and performance constraints

- CPU-first operation without a required dedicated GPU is a product requirement.
- An ordinary integrated laptop webcam is a supported baseline input device; an external, depth, or dedicated tracking camera must not be required.
- Partial-body tracking is valid: upper-body landmarks may remain usable when the lower body is outside the frame, with trust evaluated per landmark rather than by rejecting the entire pose.
- Avoid expensive processing, unnecessary frame copies, and unbounded queues unless the cost is justified and measured.
- Do not casually add packages. MediaPipe and future runtime tooling require explicit authorization. The installed Unity Pipeline/MCP integration is development-only and must never become a shipped runtime dependency.

## Unity development tooling

- When the official Unity CLI, Unity Pipeline, and Codex MCP integration are configured, use live Unity tooling for Editor-state inspection, compilation/console checks, scene or hierarchy interaction, and supported Editor actions when it materially improves correctness.
- Do not invoke MCP mechanically for every trivial text edit; filesystem and ordinary source editing remain appropriate for code changes.
- Treat GitHub/Git as authoritative for durable code review and the USER as the authority for visual or physical gameplay acceptance.

## Unity and asset safety

- Preserve Unity `.meta` files belonging to tracked assets.
- Never intentionally commit Unity cache or generated directories such as `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, or `UserSettings/`.
- Do not manually rewrite serialized scene or prefab YAML, regenerate assets without reason, or edit another developer's owned scene/content merely for convenience.
- Scene/content ownership is the primary defense against Unity YAML merge conflicts.

## Git governance

- GitHub Desktop is normally used by the USER for Git mutations.
- Future Codex tasks may explicitly authorize Git operations, but agents must follow the governance of the current Orchestrator brief rather than assuming permission.
- Core Motion Engine phases use the long-lived `engine/pose-tracking-spike` branch through the Phase 6 graybox checkpoint. Each accepted phase follows implementation -> USER QA -> accepted checkpoint commit/push -> Web Sol GitHub audit; do not merge intermediate checkpoints into `main`, and do not assume a merge is appropriate without the current Orchestrator brief.
- Do not create commits, amend, push, pull/rebase/reset, merge, or create PRs unless the current brief explicitly authorizes that operation.
- Preserve valid Git LFS configuration. Do not disable LFS or perform Git LFS history migration unless explicitly directed.
- Do not invent large sets of speculative LFS patterns.

## Verification and reporting

- Perform proportionate verification for each change; avoid excessive low-value testing for documentation-only work.
- Report failures and uncertainties truthfully.
- Never claim physical or visual Unity QA has passed unless the USER actually performed it.
- Distinguish implementation completion from USER acceptance.
- Never merge a PR without explicit USER approval.

The current durable project snapshot, architecture, decisions, roadmap, and handoff are maintained in the [`Docs/`](Docs/) directory. Phase 2 canonical skeleton and debug visualization is USER accepted with a **PASS WITH NOTES** verdict at `f5a15648607adf6034800c6a2b4d685b0e6f03ea`. Phase 3 calibration, confidence handling, and temporal smoothing is USER accepted with **PASS WITH NOTES** at `2ee4d6eb606a8b845183cc44126ecf9530d8280b`. Phase 4 kinematic-target and analytic-IK retarget architecture is implemented and requires USER QA; Phase 5 locomotion has not started and must not begin without an explicit task and Orchestrator direction.
