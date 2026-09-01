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

## Unity conflict policy

Scene/content ownership is the primary defense against Unity YAML merge conflicts. Avoid having several developers edit the same `.unity` scene or major prefab simultaneously. Course developers should primarily own their own course area once the final structure exists. Do not rely on frequent manual scene merging as the normal workflow.

## Repository governance

Generated/cache directories remain ignored, tracked Unity `.meta` files remain tracked, and valid Git LFS configuration is preserved. A task brief governs which Git mutations are authorized; agents must not infer permission to commit, push, merge, or rewrite history.
