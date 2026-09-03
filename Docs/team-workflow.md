# Team workflow

Status: **CURRENT WORKFLOW DIRECTION.**

## Branches

- **MAIN** is the stable integration branch.
- Branches are feature/task scoped.
- Appropriate examples include `engine/*`, `hub/*`, `course/*`, `chore/*`, and `fix/*`.
- Long-lived permanent personal branches such as `person1`, `person2`, or `person3` are not the normal workflow.

## Normal development flow

1. Synchronize with the latest accepted `main`.
2. Create a narrowly scoped branch.
3. Implement the requested phase.
4. Verify locally.
5. Inspect the diff.
6. Commit.
7. Push.
8. Create a Draft PR.
9. The Web Sol Orchestrator reviews the implementation and diff.
10. Make corrections if necessary.
11. The USER performs Unity visual/physical QA when required.
12. The USER explicitly approves.
13. Merge.
14. Begin the next work from the latest `main`.

The USER primarily owns Motion Engine work. Other developers will later own interactive environments and fitness courses. Web Sol Orchestrator handles architecture, implementation briefs, GitHub review, and integration decisions. Codex Luna handles local implementation, inspection, verification, and precise reports. GitHub Desktop is the USER's preferred interface for normal Git mutations.

## Motion Engine phase train

Motion Engine implementation phases use the long-lived `engine/pose-tracking-spike` branch through the Phase 6 graybox checkpoint. For each phase, the sequence is implementation, USER QA, accepted checkpoint commit/push, and Web Sol GitHub audit. Intermediate phase checkpoints are not mechanically merged into `main`; `main` remains the stable integration branch, and the intended merge boundary is after the Phase 6 graybox is accepted. The current accepted Phase 1 checkpoint is `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6`.

Phase 2 accepted SHA is `f5a15648607adf6034800c6a2b4d685b0e6f03ea`; Phase 3 accepted SHA is `2ee4d6eb606a8b845183cc44126ecf9530d8280b`, and Phase 4's IK retarget architecture is implemented and awaiting USER QA. Phase 5 has not started.

## Unity conflict policy

Scene/content ownership is the primary defense against Unity YAML merge conflicts. Avoid having several developers edit the same `.unity` scene or major prefab simultaneously. Course developers should primarily own their own course area once the final structure exists. Do not rely on frequent manual scene merging as the normal workflow.

## Repository governance

Generated/cache directories remain ignored, tracked Unity `.meta` files remain tracked, and valid Git LFS configuration is preserved. A task brief governs which Git mutations are authorized; agents must not infer permission to commit, push, merge, or rewrite history.
