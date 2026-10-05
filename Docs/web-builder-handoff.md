# Mocap Adventure quick handoff

Use AltamashM7/GoldenNeedle, branch gameplay/foundation. Check remote HEAD and local edits, then read AGENTS.md, Docs/current-state.md, Docs/architecture.md and Docs/motion-sdk-integration.md.

Motion Engine source and its Unity Lab are private and separate. This product consumes compiled SDK preview 2, accepted by the USER in Unity and Windows. Product developers do not need private source access. Do not restore embedded provider/engine code or follow obsolete engine experiment workflows. Run python Tools/verify_motion_sdk_boundary.py for static boundary verification.

Read Docs/game-flow.md, gameplay-foundation-plan.md, hub-integration-plan.md and course-contract.md for product direction, checking live scenes rather than treating old pending status as current. Preserve user asset/settings edits. USER owns full builds and physical QA; main/PR merges require explicit approval.
