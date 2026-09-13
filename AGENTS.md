# Golden Needle agent instructions

Golden Needle is a Unity 6.5 / URP gamified embodied-fitness application for Smart India Hackathon 2026. Its product direction is full-body webcam-driven control of a 3D avatar, not merely a small set of gesture-triggered buttons.

## Before changing the repository

- Inspect the repository root, current Git status, active branch, remotes, tracked files, and relevant existing assets.
- Verify `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `Packages/packages-lock.json`, URP configuration, `.gitignore`, and `.gitattributes` instead of trusting a task description.
- Read the relevant files in `Docs/` before implementing work. Treat `Docs/current-state.md` as the durable project snapshot, and treat `Docs/openvino-unity-integration-checkpoints.md` as the authoritative execution plan for the current OpenVINO Unity integration experiment.
- Always verify the current remote `engine/pose-tracking-spike` HEAD before new work because documentation and checkpoint commits may advance it.
- Check for pre-existing work that may affect the requested scope. Do not silently upgrade or downgrade Unity, change the render pipeline, or perform destructive Git operations.

## Current phase / experiment status

- Phase 1 — MediaPipe provider/raw overlays: **PASS WITH NOTES**.
- Phase 2 — canonical skeleton/debug: **PASS**.
- Phase 3 — stabilization/confidence: **PASS**.
- Phase 4 — humanoid retargeting/calibration/orientation: **USER ACCEPTED — PASS**.
- Phase 5A — support-foot locomotion / Lab-Game presentation: **IMPLEMENTED / NOT USER ACCEPTED**.
- Phase 6: **NOT STARTED**.
- Gate A OpenVINO exact-model compatibility: **PASS**.
- Gate B current-MediaPipe/OpenVINO recorded-sequence semantic proof: **PASS WITH NOTES**.
- Unity/OpenVINO practical integration: **AUTHORIZED AS A CHECKPOINTED EXPERIMENT**, not yet production accepted.

Gate B evidence on the USER's 363-frame recorded motion sequence:
- official Tasks reference and custom TFLite graph completed and matched exactly;
- OpenVINO CPU FP32 graph completed all frames;
- offline steady graph capacity improved from roughly 35.6/s TFLite to 71.3/s OpenVINO;
- pose-presence agreement vs reference was 362/363 (~99.7245%);
- normalized XYZ RMS ~0.01113 and world Euclidean 3D RMS ~0.02188 m;
- these are offline VIDEO-mode graph-capacity results, **not** Unity LIVE_STREAM end-to-end latency.

## Architectural boundaries

- Preserve the replaceable Pose Provider boundary and the engine-owned canonical skeleton direction.
- Keep pose reproduction separate from locomotion: **POSE != LOCOMOTION**.
- Keep Motion Engine concerns separate from Player, Hub, course, environment, UI, and cinematic content.
- Course code must not depend on MediaPipe-specific structures or internals.
- Prefer narrow phases and stable abstractions; do not freeze speculative APIs or interfaces before prototypes justify them.
- Reuse mature MediaPipe semantics for preprocessing, detector decode/NMS, ROI generation/tracking, landmark refinement, visibility/presence, world landmarks and projection. Do not manually recreate those semantics in C# merely to integrate OpenVINO.
- For the current experiment, replace only the proven inference seam where practical. The accepted downstream canonical/stabilization/calibration/retarget/locomotion behavior is not the target of the experiment.
- Do not refactor the current fixed 33-landmark provider frame or 20-joint canonical topology during this performance spike.

## Product and performance constraints

- CPU-first operation without a required dedicated GPU is a product requirement.
- An ordinary integrated laptop webcam is a supported baseline input device; an external, depth, or dedicated tracking camera must not be required.
- Partial-body tracking is valid: upper-body landmarks may remain usable when the lower body is outside the frame, with trust evaluated per landmark rather than by rejecting the entire pose.
- Avoid expensive processing, unnecessary frame copies, and unbounded queues unless the cost is justified and measured.
- Current production scheduling semantics are: at most one active readback, one replaceable prepared frame and one outstanding inference; latest useful frame wins; no backlog/history/replay/catch-up queue.
- Current body-only inference input is 320x240 while camera/display remain native resolution. Keep this unchanged for the initial OpenVINO A/B integration test.
- Current accepted production observations are approximately camera 29–30 FPS, fresh pose results 10–12/s, readback 55–65 ms and frame-to-result 110–140 ms depending on load. The OpenVINO Unity experiment must be evaluated against real end-to-end measurements, not the offline Gate B 71/s rate.
- Do not casually add packages. MediaPipe and future runtime tooling require explicit authorization. The installed Unity Pipeline/MCP integration is development-only and must never become a shipped runtime dependency.

## OpenVINO Unity experiment constraints

- The existing MediaPipe/TFLite CPU path is the known-safe fallback and must remain functional.
- OpenVINO CPU FP32 begins as an experimental/selectable backend, not an unconditional replacement.
- Do not ship the Gate B shadow-TFLite/raw-parity diagnostic in the Unity runtime path.
- Do not densify or convert the detector.
- Do not force D3D12 globally.
- Explicit benchmark requests for OpenVINO must not silently fall back while reporting themselves as OpenVINO; backend identity/failure must be visible in diagnostics.
- Windows x86_64 native dependencies must be packaged narrowly and documented, including third-party/license implications.
- Follow `Docs/openvino-unity-integration-checkpoints.md` checkpoint by checkpoint. **Stop after each checkpoint and report; do not roll directly into the next checkpoint.**

## Unity development tooling

- When the official Unity CLI, Unity Pipeline, and Codex MCP integration are configured, use live Unity tooling for Editor-state inspection, compilation/console checks, scene or hierarchy interaction, and supported Editor actions when it materially improves correctness.
- Do not invoke MCP mechanically for every trivial text edit; filesystem and ordinary source editing remain appropriate for code changes.
- Treat GitHub/Git as authoritative for durable code review and the USER as the authority for visual or physical gameplay acceptance.

## Unity and asset safety

- Preserve Unity `.meta` files belonging to tracked assets.
- Never intentionally commit Unity cache or generated directories such as `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, or `UserSettings/`.
- Do not manually rewrite serialized scene or prefab YAML, regenerate assets without reason, or edit another developer's owned scene/content merely for convenience.
- Scene/content ownership is the primary defense against Unity YAML merge conflicts.
- Known USER-local dirty files have repeatedly included `Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpike.unity`, `GoldenNeedle.slnx`, and `ProjectSettings/SceneTemplateSettings.json`. Never clean, revert, stage, or overwrite these casually. Package-lock changes can also be legitimate; inspect before touching them.

## Git governance

- GitHub Desktop is normally used by the USER for Git mutations.
- Future Codex tasks may explicitly authorize Git operations, but agents must follow the governance of the current Orchestrator brief rather than assuming permission.
- Core Motion Engine work remains on the long-lived `engine/pose-tracking-spike` branch. Do not merge intermediate checkpoints into `main`, and do not assume a merge is appropriate without explicit USER approval.
- Do not create commits, amend, push, pull/rebase/reset, merge, or create PRs unless the current brief explicitly authorizes that operation.
- Preserve valid Git LFS configuration. Do not disable LFS or perform Git LFS history migration unless explicitly directed.
- Do not invent large sets of speculative LFS patterns.

## Verification and reporting

- Perform proportionate verification for each change; avoid excessive low-value testing.
- Report failures and uncertainties truthfully.
- Never claim physical or visual Unity QA has passed unless the USER actually performed it.
- Distinguish implementation completion from USER acceptance.
- Distinguish offline graph capacity from Unity LIVE_STREAM end-to-end performance.
- Never merge a PR without explicit USER approval.
- For the current OpenVINO Unity experiment, every worker assignment must end at its checkpoint boundary with: exact starting SHA, exact ending SHA, files changed, implementation/audit summary, verification performed, unresolved risks, and explicit statement that the next checkpoint was not started.