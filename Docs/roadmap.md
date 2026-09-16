# Golden Needle — Roadmap

Status refreshed: 2026-09-17

This is a high-level sequence, not permission to start later phases. Use `Docs/current-state.md` for exact implementation and acceptance details.

## Phase 0 — Project/repository foundation

**COMPLETE**

Durable documentation, Unity project, URP, packages, repository workflow, and development tooling are established.

## Phase 1 — Webcam and pose tracking spike

**USER ACCEPTED — PASS WITH NOTES**

Historical accepted SHA: `88ff29bfe6b8b89536e6b3b274177f8f8f0e8fd6`.

## Phase 2 — Canonical skeleton and debug visualization

**USER ACCEPTED — PASS WITH NOTES**

Historical accepted SHA: `f5a15648607adf6034800c6a2b4d685b0e6f03ea`.

## Phase 3 — Calibration, confidence, and stabilization

**USER ACCEPTED — PASS WITH NOTES**

Historical accepted SHA: `2ee4d6eb606a8b845183cc44126ecf9530d8280b`.

Later work superseded the old hard-T-pose UI with modular measurement calibration while preserving the accepted stabilized canonical-data direction.

## Phase 4 — Humanoid retargeting

**USER ACCEPTED — PASS**

Historical accepted SHA: `f0c81e84d0a482c40448505f2904af93ef4aa881`.

The accepted solution includes measurement calibration, canonical-to-avatar basis mapping, analytical limb targets, Humanoid binding, and corrected frontal-camera orientation semantics.

## Phase 5 — Motion Engine V1 locomotion completion

**USER ACCEPTED / FROZEN FOR CURRENT PROJECT SCOPE**

Accepted implementation baseline: `140a939160530394ee5c70aa1dbc31c62f3a12e1`.

The production stack includes physical support-base translation, cadence extension, body-heading steering, recenter/reacquisition continuity, vertical semantics, and the CPU-first selectable OpenVINO path. Crouch/ground contact remains approximate but was explicitly accepted.

Older Phase 5A text that described locomotion as awaiting acceptance is historical and superseded by the accepted Motion Engine V1 baseline.

## Phase 6 — Gameplay/session foundation and Calibration integration

**IMPLEMENTED; CORE CALIBRATION -> HUB FLOW PREVIOUSLY USER VERIFIED**

Implemented:

- persistent player/session/facade;
- health/body-anchor foundation;
- shared fade/load/spawn GameFlow;
- contextual commands;
- world-space Calibration presentation;
- persistent Calibration -> Hub transition.

The latest complete-sample and Animator-authority corrections await a fresh USER runtime retest.

## Phase 7 — Minimal production Hub

**IMPLEMENTED; CURRENT USER QA GATE**

Implemented:

- editable `HubEntry` spawn;
- immediate Hub pose drive and locomotion;
- persistent-player follow camera;
- eight camera presets and speech phrases;
- separate portal trigger infrastructure;
- yellow -> Boxing mapping, safely disabled;
- blue -> Obstacle Course / `ObstacleEntry` wiring.

Current checkpoint: `6c3f4c365a9730baa87aa5e411337e20bbbe8f47`.

The USER must validate pose following, locomotion, camera presets, and blue portal transition before the project advances.

## Phase 8 — Activity modes

**NOT STARTED**

Order:

1. integrate the real Boxing environment and define its spawn contract;
2. wire the yellow portal;
3. implement Boxing gameplay;
4. implement Obstacle gameplay;
5. add activity results and return-to-Hub flow.

Do not invent Boxing content to satisfy the roadmap.

## Phase 9 — Cross-scene presentation and polish

**NOT STARTED**

- full `Calibration -> Hub -> activity -> Hub` flow;
- UI/results polish;
- cinematic/narrative presentation where approved;
- camera and portal polish;
- progression/statistics if later scoped.

## Phase 10 — Hardening

**NOT STARTED**

- low-end CPU validation;
- robustness and recovery;
- end-to-end performance verification;
- build validation;
- SIH demonstration hardening.

## Branch rule

Gameplay work remains on `gameplay/foundation`. Merge to `main` only after explicit USER approval.
