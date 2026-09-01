# Current state

This is the concise durable snapshot of what exists. It must remain exceptionally truthful.

## Current phase

**Phase 1 — Minimal Webcam + Pose Tracking Technical Spike — USER ACCEPTED**

Verdict: **PASS WITH NOTES**. USER QA is complete and accepted. Phase 2 has not started.

## Implemented and verified

- A Unity project exists at this repository root.
- Unity version is `6000.5.0f1`, corresponding to Unity 6.5.
- URP is configured: `com.unity.render-pipelines.universal` is present at `17.5.0`, and `ProjectSettings/GraphicsSettings.asset` references a `UniversalRenderPipeline` asset. URP settings/assets are present under `Assets/Settings`.
- A Git repository exists on branch `engine/pose-tracking-spike` at starting commit `d7b7c1f60cd0053816008a57d95ecee00a2f8216`, with remote `origin` configured for the published GitHub repository.
- Git LFS is installed and the local repository has LFS filters/endpoint configuration. `.gitattributes` currently contains only `* text=auto`; `git lfs ls-files` reports no LFS-managed files or patterns. This existing state was inspected and preserved.
- Unity's normal generated/cache directories are ignored, and `.meta` files are not globally ignored.
- The repository currently contains a small Unity template/sample scene and tutorial files. No product gameplay was found in the inspected project.
- Durable Phase 0D documentation exists in `AGENTS.md` and `Docs/`.
- Unity CLI `1.0.0-beta.5` is installed and discoverable.
- The official Unity Pipeline package `com.unity.pipeline` version `0.5.0-exp.1` is installed in the project. It is development tooling only, not a Golden Needle runtime dependency.
- The official Unity Codex plugin `unity@unity-agent-plugin` version `0.1.0-beta` is installed and enabled in user-level Codex configuration. Unity-specific skills are present in its installed plugin cache.
- User-level Codex MCP configuration contains an enabled `unity` server entry targeting this project through `unity mcp --project-path ...`.
- Live verification succeeded through the official Unity MCP server: handshake `unity-mcp 1.0.0-beta.5`, 142 tools discovered, and the read-only `editor_status` tool returned this project, Unity `6000.5.0f1`, `ready`, not compiling, and Play Mode stopped.
- A dedicated spike scene exists at `Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpike.unity`, created and saved through live Unity Editor tooling. Its root has `MediaPipePoseProvider` and `PoseTrackingSpikePresenter`; a separate `PoseTrackingSpikeCamera` supplies a normal Game View camera.
- The embedded MediaPipeUnityPlugin `0.16.3` runtime is integrated as a repository-local package with the Windows CPU prebuilt native library and required managed runtime assets. It is configured for Pose Landmarker Lite, one pose, CPU inference, and segmentation disabled.
- The local model is `pose_landmarker_lite.bytes` under `Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/` and is available offline at runtime.
- The live spike smoke test enumerated two devices, selected the ordinary integrated `HP TrueVision HD Camera` by default, opened it at actual `640x480` with requested `30` FPS, initialized the local CPU landmarker, accepted a live inference request, and received an asynchronous result callback. The console was clear of recurring exceptions after the local fixes.
- The provider publishes a small spike-only `PoseObservation` with per-landmark normalized/world coordinates, visibility/presence metadata, and `Tracked`/`Unavailable` trust. It uses a short trust grace period and latest-result publication without an unbounded inference queue.
- USER QA accepted the Phase 1 spike with built-in `HP TrueVision HD Camera` input at approximately `640x480`. Reviewed evidence showed Unity rendering around `56–68 FPS`, camera/capture around `17–31 FPS`, inference samples around `58–93 ms`, and commonly `25/33` trusted landmarks during tracking. Requests and results progressed continuously.
- When the subject was lost, the diagnostic state transitioned to `WAITING / UNAVAILABLE` with `0/33` trusted landmarks instead of retaining stale tracking.
- Raw landmark visualization showed jitter and loose geometry in some poses. This is an accepted Phase 1 note because canonical representation, confidence/filtering, smoothing, calibration, retargeting, and locomotion belong to later phases.
- The QA evidence came from representative frames extracted from the USER's recorded test because ChatGPT's video attachment runtime failed to mount the original MP4. This is an evidence-access limitation, not a Golden Needle application failure. The observations are not a formal latency benchmark.

## Not yet implemented

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

## Repository activity for Phase 1

This task adds the isolated webcam/pose spike, a repository-local MediaPipe runtime subset, the offline Lite model, and the dedicated diagnostic scene. It does not modify the existing sample scene or URP configuration. Unity MCP/Pipeline remain development tooling and are not part of the runtime product. No commits, branches, pushes, merges, PRs, or history migrations were performed.

The implementation remains as working-tree changes for USER inspection and commit through GitHub Desktop. USER QA is complete with a **PASS WITH NOTES** verdict; the recorded jitter/loose-geometry observations remain the explicit Phase 1 handoff notes.

## Next target

**Next planned phase: Phase 2 — Canonical Skeleton + Debug Visualization.** Do not begin Phase 2 in this follow-up; the Phase 1 changes remain uncommitted for USER/GitHub Desktop handling and require the normal Orchestrator authorization before implementation begins.
