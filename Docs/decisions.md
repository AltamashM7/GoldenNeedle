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
