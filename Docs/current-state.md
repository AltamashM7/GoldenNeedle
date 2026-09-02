# Current state

This is the concise durable snapshot of what exists. It must remain exceptionally truthful.

## Current phase

**Phase 2 — Canonical Skeleton + Debug Visualization — USER ACCEPTED — PASS WITH NOTES**

Phase 1 accepted SHA: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6` (`feat: add CPU pose-tracking spike`). Phase 2 is USER accepted with verdict **PASS WITH NOTES**; its accepted implementation remains in the working tree pending the next checkpoint commit.

## Implemented and verified

- A Unity project exists at this repository root.
- Unity version is `6000.5.0f1`, corresponding to Unity 6.5.
- URP is configured: `com.unity.render-pipelines.universal` is present at `17.5.0`, and `ProjectSettings/GraphicsSettings.asset` references a `UniversalRenderPipeline` asset. URP settings/assets are present under `Assets/Settings`.
- A Git repository exists on the long-lived Motion Engine branch `engine/pose-tracking-spike`; the accepted Phase 1 checkpoint is `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6`, with remote `origin` configured for the published GitHub repository. Phase 2 changes are intentionally uncommitted working-tree changes.
- Git LFS is installed and the local repository has LFS filters/endpoint configuration. `.gitattributes` currently contains only `* text=auto`; `git lfs ls-files` reports no LFS-managed files or patterns. This existing state was inspected and preserved.
- Unity's normal generated/cache directories are ignored, and `.meta` files are not globally ignored.
- The repository currently contains a small Unity template/sample scene and tutorial files. No product gameplay was found in the inspected project.
- Durable Phase 0D documentation exists in `AGENTS.md` and `Docs/`.
- Unity CLI `1.0.0-beta.5` is installed and discoverable.
- The official Unity Pipeline package `com.unity.pipeline` version `0.5.0-exp.1` is installed in the project. It is development tooling only, not a Golden Needle runtime dependency.
- The official Unity Codex plugin `unity@unity-agent-plugin` version `0.1.0-beta` is installed and enabled in user-level Codex configuration. Unity-specific skills are present in its installed plugin cache.
- User-level Codex MCP configuration contains an enabled `unity` server entry targeting this project through `unity mcp --project-path ...`.
- Live verification succeeded through the official Unity MCP server: handshake `unity-mcp 1.0.0-beta.5`, 142 tools discovered, and the read-only `editor_status` tool returned this project, Unity `6000.5.0f1`, `ready`, not compiling, and Play Mode stopped.
- A dedicated spike scene exists at `Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpike.unity`, created and saved through live Unity Editor tooling. Its root has `MediaPipePoseProvider` and `PoseTrackingSpikePresenter`; a separate `PoseTrackingSpikeCamera` supplies a normal Game View camera. The provider/raw observation boundary now lives under `Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/`, while the presenter remains under Debug.
- The embedded MediaPipeUnityPlugin `0.16.3` runtime is integrated as a repository-local package with the Windows CPU prebuilt native library and required managed runtime assets. It is configured for Pose Landmarker Lite, one pose, CPU inference, and segmentation disabled.
- The local model is `pose_landmarker_lite.bytes` under `Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/` and is available offline at runtime.
- The live spike smoke test enumerated two devices, selected the ordinary integrated `HP TrueVision HD Camera` by default, opened it at actual `640x480` with requested `30` FPS, initialized the local CPU landmarker, accepted a live inference request, and received an asynchronous result callback. The console was clear of recurring exceptions after the local fixes.
- The provider publishes a small spike-only `PoseObservation` with per-landmark normalized/world coordinates, visibility/presence metadata, and `Tracked`/`Unavailable` trust. It uses a short trust grace period and latest-result publication without an unbounded inference queue.
- USER QA accepted the Phase 1 spike with built-in `HP TrueVision HD Camera` input at approximately `640x480`. Reviewed evidence showed Unity rendering around `56–68 FPS`, camera/capture around `17–31 FPS`, inference samples around `58–93 ms`, and commonly `25/33` trusted landmarks during tracking. Requests and results progressed continuously.
- When the subject was lost, the diagnostic state transitioned to `WAITING / UNAVAILABLE` with `0/33` trusted landmarks instead of retaining stale tracking.
- Raw landmark visualization showed jitter and loose geometry in some poses. This is an accepted Phase 1 note because canonical representation, confidence/filtering, smoothing, calibration, retargeting, and locomotion belong to later phases.
- The QA evidence came from representative frames extracted from the USER's recorded test because ChatGPT's video attachment runtime failed to mount the original MP4. This is an evidence-access limitation, not a Golden Needle application failure. The observations are not a formal latency benchmark.
- Phase 2 adds the engine-owned 20-joint `CanonicalPoseFrame` and a MediaPipe-to-canonical mapper. Direct joints preserve per-joint trust and confidence; pelvis, chest, and spine are derived only from their specified trusted midpoint inputs. Missing hips/legs or head landmarks do not invalidate other trusted joints.
- Canonical image coordinates use x left-to-right and y bottom-to-top; canonical 3D uses +X camera/view right, +Y up, and +Z away from the camera. MediaPipe world positions are converted once at the provider boundary and made pelvis-relative when a trusted canonical pelvis exists.
- The camera orientation path now separates MediaPipe input preparation, display metadata, and landmark overlay conversion. The inference-only Unity-to-MediaPipe vertical flip is no longer reused for the preview, and display mirroring is explicit and disabled by default. USER QA confirmed the webcam preview is upright and the raw/cyan overlay visually aligns.
- The Phase 2 QA correction preserves the provider's project-normalized image coordinates (`x` left-to-right, `y` bottom-to-top) through the canonical mapper and applies the canonical-image-to-Unity-IMGUI Y conversion exactly once at the screen boundary. USER QA confirmed the canonical 2D/yellow overlay is now upright after fixing the double Y inversion; canonical 3D/local-space visualization behaves plausibly and partial-body canonical tracking remains valid.
- The debug presenter now exposes raw, canonical 2D, and canonical 3D/local-space views with `F1`, `F2`, and `F3` toggles plus `R` retry. Diagnostics include canonical tracked count, pelvis/3D availability, and separate sensor/inference/display orientation values.
- Focused EditMode coverage contains five passing canonical mapper tests. USER QA accepted the Phase 2 result with verdict **PASS WITH NOTES**. The two `UnityEditor.ShaderGraph.ShaderGraphProjectSettings` warnings may occur once during script recompilation or Unity exit, but did not recur during normal Play Mode and are not considered a Golden Needle runtime blocker.

## Not yet implemented

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
- Phase 3 confidence handling and smoothing;

## Repository activity for Phase 2

This task adds the engine-owned canonical layer, orientation correction, focused mapper tests, and canonical debug views on top of the accepted isolated webcam/pose spike. It does not modify the existing sample scene or URP configuration. Unity MCP/Pipeline remain development tooling and are not part of the runtime product. No commits, branches, pushes, merges, PRs, or history migrations were performed.

The implementation remains as working-tree changes for the accepted Phase 2 checkpoint commit through GitHub Desktop. Phase 1 and Phase 2 USER QA are complete with **PASS WITH NOTES** verdicts; the recorded jitter/loose-geometry observations remain explicit Phase 1 handoff notes. The next checkpoint commit will represent the accepted Phase 2 implementation. Phase 3 has not started.

## Next target

**Next immediate step: the Phase 2 checkpoint commit.** That commit will represent the accepted Phase 2 implementation. The next planned phase after that checkpoint is Phase 3 — Calibration, confidence handling, and smoothing. Phase 3 has not started; do not begin it in this follow-up.
