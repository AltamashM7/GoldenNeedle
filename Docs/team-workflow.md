# Team workflow

Status: **CURRENT WORKFLOW DIRECTION**

## Branches

- `main` is the stable integration branch.
- Normal work uses feature/task-scoped branches such as `engine/*`, `hub/*`, `course/*`, `chore/*`, and `fix/*`.
- Permanent personal branches are not the normal workflow.

## Normal development flow

1. Start from an accepted baseline.
2. Implement a narrowly defined scope.
3. Verify locally.
4. Inspect the diff.
5. USER performs required physical/visual QA.
6. Accepted work is committed/pushed.
7. Web Sol audits the pushed checkpoint.
8. PR/merge occurs only at the intended integration boundary and only after explicit USER approval.

The USER primarily owns Motion Engine work. Web Sol is the Orchestrator for architecture/integration decisions. Codex Luna is the local Builder. GitHub Desktop is the USER's normal Git interface.

## Motion Engine phase train

Motion Engine phases intentionally stay on the long-lived `engine/pose-tracking-spike` branch through the Phase 6 graybox checkpoint. Intermediate accepted engine phases are not mechanically merged into `main`.

Accepted checkpoints:

- Phase 1: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6`
- Phase 2: `f5a15648607adf6034800c6a2b4d685b0e6f03ea`
- Phase 3: `2ee4d6eb606a8b845183cc44126ecf9530d8280b`

Current Phase 4 investigation checkpoint:

- `5e830dce7ac3de542ab159b8b90992935d9dd0b0` — **NOT USER ACCEPTED**

This Phase 4 commit preserves an unfinished investigative state. It must not be treated as an accepted phase simply because it is committed/pushed. A fresh Orchestrator must inspect the repository before directing further Phase 4 changes.

Phase 5 has not started.

## Unity conflict policy

Scene/content ownership is the primary defense against Unity YAML conflicts. Avoid multiple developers editing the same `.unity` scene or major prefab at the same time. Course developers should own separate content areas once the course framework is frozen.

## Repository governance

- Generated/cache directories stay ignored.
- Tracked Unity `.meta` files stay tracked.
- Preserve valid Git LFS configuration.
- Agents must not infer permission for commits, pushes, merges, rebases, resets, PRs, or history rewrites.
- Never merge without explicit USER approval.
- Before any future accepted Phase 4 checkpoint, inspect the currently committed `GoldenNeedle.slnx` and `ProjectSettings/ProjectSettings.asset` diffs because they were previously described as unintended editor state.
