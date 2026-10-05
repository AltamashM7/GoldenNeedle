# Mocap Adventure agent instructions

Mocap Adventure (GoldenNeedle) is the Unity 6.5 / URP embodied-fitness product for SIH 2026. It consumes the owner's separately maintained HD Motion Engine as a compiled SDK.

## Before repository changes

- Inspect root, Git status, active branch, remotes, relevant tracked assets and current remote gameplay/foundation HEAD. Read Docs/current-state.md, Docs/architecture.md and Docs/motion-sdk-integration.md.
- Verify ProjectSettings/ProjectVersion.txt, Packages/manifest.json, Packages/packages-lock.json, URP, .gitignore and .gitattributes. Current baseline: Unity 6000.5.0f1, URP 17.5.0. Do not upgrade dependencies/render pipeline without authorization.
- Preserve existing user edits. Normal working branch is gameplay/foundation; codex/motion-sdk-integration records the accepted migration. Git mutations require authorization for the task. Never reset/rebase/force-push, rewrite shared history or merge main/PRs without explicit user approval.

## Product boundary

- Motion Engine source, Lab, provider implementation, algorithms, native builds and engine regression tests belong in the private hd-motion-engine repository. Do not restore Core/Motion, embedded MediaPipe source, engine debug/Lab assets or old native research tooling here.
- Install the versioned compiled SDK from Packages/HDMotionEngine through UPM. Product developers do not need the private source URL or credentials. Run python Tools/verify_motion_sdk_boundary.py for the repository boundary check.
- Products own player/session lifetime, health, scenes, spawning, terrain grounding, portals/courses, camera/UI presentation, command vocabulary/context and game rules. Use MotionEngineController and the product facade; gameplay must not implement or depend on MediaPipe inference internals.
- Legacy GoldenNeedle engine namespaces/type names and model paths remain inside the SDK for compatibility. They are not permission to recreate its implementation in the product. POSE != LOCOMOTION.
- Preserve accepted full-body/partial-body control, CPU-first webcam operation and the OpenVINO/TFLite fallback behavior when updating the SDK. SDK updates are deliberate, versioned and verified in the private Lab before product acceptance.
- The SDK Editor installer restores runtime models. Preserve tracked model metadata; duplicate model binaries are ignored generated payloads. Third-party notices remain intact.

## Unity and verification

- Preserve tracked .meta files and serialized references. Use Unity Editor APIs for scene/prefab changes; do not hand-edit serialized YAML or overwrite another developer's content for convenience.
- Never commit Library, Temp, Obj, Build/Builds, Logs, UserSettings, recovery scenes or development symbols. Keep valid Git LFS settings; do not invent broad LFS patterns.
- Unity Pipeline/MCP is development-only. Retain ExcludeDevelopmentToolingFromPlayer; never ship Pipeline/Roslyn, engine Editor, Lab or test assemblies.
- Use configured Unity tooling when it materially improves correctness. Do not invoke it mechanically for text changes.
- Full Windows builds and similarly long-running checks belong to the USER. Do not launch or repeatedly poll them. Perform proportionate bounded checks and inspect USER results.
- USER reports both Unity Editor and Windows SDK gameplay worked (2026-10-05). Do not claim additional physical QA, measured latency or successful test-runner shutdown without evidence.
- Maintain Docs/current-state.md and the product SDK handoff when boundaries or acceptance change. Private engine checkpoint history is maintained there. Licensing/activation design remains deferred until requested.
