# Mocap Adventure — current state

Authoritative refresh: 2026-10-05.

Repository: AltamashM7/GoldenNeedle. Working branch: gameplay/foundation. Main remains at its existing integration checkpoint; no main merge is part of this separation.

## Accepted engine connection

Mocap Adventure consumes HD Motion Engine 0.1.0-preview.2 as a compiled, source-free UPM archive in Packages/HDMotionEngine. The owner develops the engine and Unity Lab in private AltamashM7/hd-motion-engine. Product developers need the bundled SDK, not access to private source.

USER ACCEPTED — PASS: the USER tested the isolated project in Unity, built Downloads/GoldenNeedle_MotionSDK and tested that executable; everything worked in both. Read motion-sdk-user-acceptance.json. Read-only payload inspection matched both runtime DLLs, all eight native DLLs and four models against the SDK, with no Pipeline/Roslyn/engine Editor/Lab/test assemblies. No new measured latency or BuildReport is claimed.

The accepted migration is 522e29a0de8b07e59639136600233742afd6a08d. The newer team direction commit 643d57a46177f14c8fd477522aa89ef8f0e63c3e is preserved through merge 0dd4bde5dc1a024e2459904f2a9f8e1e836a6d53. The USER now authorizes publishing and applying this separation to the original working repository.

## Boundaries and setup

- Unity 6000.5.0f1 / URP 17.5.0; dependencies and render pipeline are unchanged.
- Product: persistent player/session, health, GameFlow, Calibration/Hub/activity scenes, spawning, grounding, camera/UI and command meanings/context.
- Engine SDK: capture/inference, canonical pose/trust, calibration, retargeting, locomotion interpretation and reusable speech recognition. POSE != LOCOMOTION.
- Start at Assets/Scenes/Caliberation.unity. The existing four enabled scenes are Calibration, GoldenNeedle_Hub, Obstacle Course and BoxingArena_Baked. Existing authored flow is preserved; no new activity rules or portal changes are introduced.
- The SDK Editor installer restores runtime models automatically. Generated payloads are ignored; tracked metadata/provenance is preserved. Runtime/native/model binaries in the compiled SDK are necessary dependencies, not engine source leftovers.
- Core/Motion, embedded vendor source, engine Lab/debug assets and native research tools are absent from the active product tree. Engine-only plans, worker briefs and obsolete workflows are preserved in private Docs/HistoricalGoldenNeedle. Git history/historical branches are retained.
- Product boundary guard: python Tools/verify_motion_sdk_boundary.py. GitHub runs this bounded static check without launching Unity/native builds.

## Verification and preserved work

633 serialized fields/references survived the Editor-API prefab migration; four enabled scenes were checked with no missing scripts. Static inspection found zero tracked asset references to the 92 removed engine scripts. Earlier source and compiled SDK regressions each passed 243/243; product XML records 33/33 assertions, but clean CLI shutdown is not claimed. See the versioned SDK and product evidence.

Original user locomotion edits were carried into the private engine and published SDK, with separate regressions. Original source edits are backed up before the working checkout advances; copied terrain/render/settings edits remain local and unstaged. Unity recovery backups in the managed checkout remain local. No user content is discarded.

Full Windows builds and similarly long-running checks remain USER owned. No new build/compile/test suite is required for documentation/branch promotion; the accepted SDK and game runtime are unchanged. Licensing/activation and further gameplay development remain separate next decisions.
