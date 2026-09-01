# Current state

This is the concise durable snapshot of what exists. It must remain exceptionally truthful.

## Current phase

**Phase 0 — Project Foundation and Development Tooling**

## Implemented and verified

- A Unity project exists at this repository root.
- Unity version is `6000.5.0f1`, corresponding to Unity 6.5.
- URP is configured: `com.unity.render-pipelines.universal` is present at `17.5.0`, and `ProjectSettings/GraphicsSettings.asset` references a `UniversalRenderPipeline` asset. URP settings/assets are present under `Assets/Settings`.
- A Git repository exists on branch `chore/unity-mcp-tooling`, at the expected foundation commit, with remote `origin` configured for the published GitHub repository.
- Git LFS is installed and the local repository has LFS filters/endpoint configuration. `.gitattributes` currently contains only `* text=auto`; `git lfs ls-files` reports no LFS-managed files or patterns. This existing state was inspected and preserved.
- Unity's normal generated/cache directories are ignored, and `.meta` files are not globally ignored.
- The repository currently contains a small Unity template/sample scene and tutorial files. No product gameplay was found in the inspected project.
- Durable Phase 0D documentation exists in `AGENTS.md` and `Docs/`.
- Unity CLI `1.0.0-beta.5` is installed and discoverable.
- The official Unity Pipeline package `com.unity.pipeline` version `0.5.0-exp.1` is installed in the project. It is development tooling only, not a Golden Needle runtime dependency.
- The official Unity Codex plugin `unity@unity-agent-plugin` version `0.1.0-beta` is installed and enabled in user-level Codex configuration. Unity-specific skills are present in its installed plugin cache.
- User-level Codex MCP configuration contains an enabled `unity` server entry targeting this project through `unity mcp --project-path ...`.
- Live verification succeeded through the official Unity MCP server: handshake `unity-mcp 1.0.0-beta.5`, 142 tools discovered, and the read-only `editor_status` tool returned this project, Unity `6000.5.0f1`, `ready`, not compiling, and Play Mode stopped.

## Not yet implemented

- runtime webcam pipeline;
- MediaPipe integration;
- Pose Provider;
- canonical skeleton;
- pose filtering;
- calibration;
- rotation reconstruction;
- Humanoid live retargeting;
- locomotion;
- finished player controller;
- Hub;
- fitness courses;
- final menu flow;
- cinematics;
- progression/results;
- optimization validation;

## Repository activity for Phase 0F

This tooling task installed only the first-party Unity Pipeline package in the repository and made no product code, scene, prefab, render-setting, MediaPipe, pose-tracking, webcam, gameplay, Hub, or course changes. The single unrelated `ShaderGraphSettings.asset` serialization line caused by opening/importing the project was removed. No commits, branches, pushes, merges, PRs, or history migrations were performed.

The official Codex plugin, user-level MCP configuration, and Unity CLI state live outside the repository. The repository changes are the two package manifest/lock updates above; they remain working-tree changes for USER inspection and commit through GitHub Desktop.

## Next target

**USER review and commit of the Phase 0F tooling changes, followed by Web Sol Orchestrator audit and Phase 1 pose-tracking technical spike.** Unity MCP/Pipeline remain development tooling and are not part of the runtime product.
