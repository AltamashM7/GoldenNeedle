# Decision log

These decisions describe the current product and architecture direction. A later Orchestrator instruction may explicitly change an **OPEN / MAY CHANGE** item.

| Decision | Status | Rationale |
|---|---|---|
| Unity 6.5 | **LOCKED** | Establishes the engine baseline for SIH 2026 development and avoids version drift. |
| URP | **LOCKED** | Provides the current Unity rendering baseline while supporting a deliberately simple, CPU-conscious presentation target. |
| CPU-first / no required dedicated GPU | **LOCKED** | The application must run acceptably on an ordinary computer without requiring a discrete GPU. |
| Camera experience integrated into Unity | **LOCKED** | The user-facing product must provide one cohesive application rather than require an external tracking program. |
| MediaPipe Pose Landmarker | **PLANNED V1 / replaceable implementation** | It is the current local single-person pose-tracking direction, but the provider boundary protects the project from backend changes. |
| Full-body digital embodiment, not merely gesture-triggered buttons | **LOCKED PRODUCT DIRECTION** | Continuous body-driven avatar control is the core innovation and fitness interaction model. |
| Engine-owned canonical skeleton | **LOCKED ARCHITECTURAL DIRECTION** | Downstream systems need a stable project-owned body representation independent of provider-specific results. |
| Pose and locomotion are separate systems | **LOCKED ARCHITECTURAL DIRECTION** | Body reproduction and conversion of actions such as jogging in place into world movement have different semantics and tuning needs. |
| Modular Hub/course structure | **LOCKED ARCHITECTURAL DIRECTION** | Independent environments and courses reduce coupling and support parallel content ownership. |
| Git LFS enabled from initial repository setup | **CURRENT REPOSITORY DECISION** | The local Git LFS client and repository filters/endpoint are present; current `.gitattributes` has no LFS-managed patterns, so this state is preserved and reported rather than expanded speculatively. |
| GitHub Desktop for user's normal Git operations | **CURRENT WORKFLOW DECISION** | The USER uses GitHub Desktop for normal branch, commit, push, and PR-related mutations. |
| Web Sol Orchestrator + Codex Luna local Builder | **CURRENT DEVELOPMENT WORKFLOW** | Separates architecture/integration review from local implementation and verification. |
| Official Unity Codex plugin `unity@unity-agent-plugin` | **CURRENT DEVELOPMENT TOOLING DECISION** | Provides Unity-specific Codex skills through the first-party Unity Technologies integration; it remains outside the Golden Needle runtime. |
| Official Unity CLI + Pipeline package `com.unity.pipeline` `0.5.0-exp.1` + Unity MCP | **CURRENT DEVELOPMENT TOOLING DECISION** | Gives Codex a supported path to inspect and perform harmless operations in the live Unity Editor while keeping the integration out of shipped product dependencies. |
| Integrated laptop webcam is a supported baseline input | **LOCKED SPIKE REQUIREMENT** | The first technical spike must work with an ordinary built-in webcam; external or depth hardware is optional. |
| Partial-body tracking is valid | **LOCKED SPIKE REQUIREMENT** | Upper-body control data remains usable when legs are outside the frame; trust is evaluated per landmark and missing lower-body landmarks do not reject the whole pose. |
| MediaPipeUnityPlugin `0.16.3` embedded CPU runtime + local Pose Landmarker Lite model | **CURRENT SPIKE IMPLEMENTATION** | Uses the latest stable release observed during the spike with repository-portable Windows prebuilt assets, offline model loading, one pose, CPU inference, and segmentation disabled. The provider boundary keeps this replaceable. |
| Latest-result live stream with bounded capture | **CURRENT SPIKE IMPLEMENTATION** | WebCamTexture capture is requested around 640x480/30 FPS, inference is cadence-limited near 20 FPS, at most one readback and one inference are outstanding, and stale work is skipped instead of queued indefinitely. |
| Motion Engine long-lived phase branch | **CURRENT WORKFLOW DECISION** | `engine/pose-tracking-spike` carries the core Motion Engine phases through the Phase 6 graybox checkpoint. Each phase follows implementation -> USER QA -> accepted checkpoint commit/push -> Web Sol GitHub audit; intermediate checkpoints are not mechanically merged into `main`. |
| Phase 2 canonical joint set and partial validity | **CURRENT PHASE 2 IMPLEMENTATION** | The engine-owned frame currently contains 20 named joints: Pelvis, Spine, Chest, Head, bilateral shoulders/elbows/wrists, and bilateral hip/knee/ankle/heel/toe joints. Pelvis, Chest, and Spine are trusted midpoint derivations; missing joints do not invalidate the rest of a frame. |
| Phase 2 canonical coordinates | **CURRENT PHASE 2 IMPLEMENTATION** | Image coordinates use x left-to-right and y bottom-to-top. Canonical 3D uses +X camera/view right, +Y up, and +Z away from the camera. Source-world conversion and pelvis-relative positioning occur once at the provider-to-canonical boundary. |
| Single canonical camera / inference frame | **PHASE 4 EXPERIMENTAL / NOT ACCEPTED** | The current checkpoint attempts to separate sensor/pixel preparation, canonical normalized/world mapping, and display mirroring, but USER QA still shows unresolved presentation behavior. Treat the implementation as investigative until a fresh repository-first audit proves the coordinate contract. |

| Phase 4 humanoid retarget approach | **UNDER INVESTIGATION / NOT ACCEPTED** | Direct rotation mapping, bind-axis reconciliation, positional chain targets, analytic two-bone IK, and current-parent/per-chain mapping were explored. Repeated USER QA still showed procedural-rig mismatch versus F3, so the current checkpoint must not be treated as frozen retarget architecture. |
