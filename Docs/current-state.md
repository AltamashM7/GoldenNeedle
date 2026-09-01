# Current state

This is the concise durable snapshot of what exists. It must remain exceptionally truthful.

## Current phase

**Phase 0 — Project Foundation**

## Implemented and verified

- A Unity project exists at this repository root.
- Unity version is `6000.5.0f1`, corresponding to Unity 6.5.
- URP is configured: `com.unity.render-pipelines.universal` is present at `17.5.0`, and `ProjectSettings/GraphicsSettings.asset` references a `UniversalRenderPipeline` asset. URP settings/assets are present under `Assets/Settings`.
- A Git repository exists on branch `main`, with remote `origin` configured for the published GitHub repository.
- Git LFS is installed and the local repository has LFS filters/endpoint configuration. `.gitattributes` currently contains only `* text=auto`; `git lfs ls-files` reports no LFS-managed files or patterns. This existing state was inspected and preserved.
- Unity's normal generated/cache directories are ignored, and `.meta` files are not globally ignored.
- The repository currently contains a small Unity template/sample scene and tutorial files. No product gameplay was found in the inspected project.
- Durable Phase 0D documentation now exists in `AGENTS.md` and `Docs/`.

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
- Unity MCP development tooling setup.

## Repository activity for Phase 0D

This documentation task made no commits, branches, pushes, merges, package installations, Unity asset edits, scene edits, prefab edits, or history migrations. The new Markdown files are intentionally left as working-tree changes for USER inspection and commit through GitHub Desktop.

## Next target

**Phase 0 tooling completion / Unity MCP setup and verification, followed by Phase 1 pose-tracking technical spike.** Unity MCP must be configured as a separate, explicitly authorized tooling step; it is not installed by this task.
