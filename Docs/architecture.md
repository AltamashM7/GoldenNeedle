# Mocap Adventure architecture

Current boundary: 2026-10-05. Engine connection USER accepted in Unity Editor and Windows.

The game uses a versioned compiled Unity SDK from the separately maintained private HD Motion Engine repository. Private source and the owner's Unity Lab are not product dependencies.

```text
private engine source + Unity Lab
    -> verified compiled SDK release
    -> local UPM archive in Mocap Adventure
    -> MotionEngineController / SpeechCommandService
    -> product facade, session and command host
    -> Calibration, Hub, courses and presentation
```

The engine owns webcam/inference, canonical pose and per-joint trust, stabilization, calibration, humanoid retargeting, locomotion interpretation and reusable speech recognition. Pose reproduction and locomotion remain separate. CPU-first operation and the known TFLite fallback remain supported.

The product owns GoldenNeedlePlayerSession, GoldenNeedlePlayerFacade, health/body-anchor adapters, GameFlow/fades, scene lifetime, spawn locations, terrain grounding, activity rules, camera/UI presentation and command vocabulary/context. A persistent player maintains accepted tracking/calibration across normal scene transitions. Course code must not depend on MediaPipe structures or inference implementation.

GoldenNeedlePlayerFacade forwards motion requests to one MotionEngineController. GoldenNeedleBodyAnchors wraps the SDK anchor representation. GameplayCommandHost supplies product phrases to SpeechCommandService and routes voice/UI commands through the same contextual Execute path. Camera view meanings remain in the product.

The authored player prefab contains compiled SDK components with preserved serialized references. Legacy engine type namespaces and StreamingAssets paths remain for compatibility; this does not place their implementation in the game. Product Editor authoring may configure SDK components, but must not recreate private algorithms or inference code.

Packages/HDMotionEngine holds the immutable preview-2 archive; Packages/manifest.json and packages-lock.json record its UPM dependency. The compiled Editor installer restores runtime models and the product bootstrap checks them before builds. Generated model binaries are ignored while metadata/provenance is retained. ExcludeDevelopmentToolingFromPlayer removes Pipeline/Roslyn from Player builds.

For future updates, improve/test the engine in the private source Lab, publish a new verified SDK, update the product archive/dependency deliberately, and perform product regression acceptance. Read motion-sdk-integration.md for setup and evidence. Historical implementation plans and obsolete workflows are preserved privately in Docs/HistoricalGoldenNeedle rather than executed in this product.
