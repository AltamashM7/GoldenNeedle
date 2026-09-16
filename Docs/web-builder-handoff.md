# Golden Needle — Simple Web Builder Handoff

Use repository `AltamashM7/GoldenNeedle`, branch `gameplay/foundation`.

First verify the remote branch HEAD, then read these files in order:

1. `Docs/current-state.md` — complete current status, evidence, implementation details, and QA boundary;
2. `Docs/game-flow.md` — implemented scene flow versus future flow;
3. `Docs/gameplay-foundation-plan.md` — architecture and remaining sequence;
4. `Docs/hub-integration-plan.md` — exact Hub camera, spawn, and portal contracts;
5. `Docs/orchestrator-handoff.md` — governance and next action.

The implementation checkpoint described by the docs is:

`6c3f4c365a9730baa87aa5e411337e20bbbe8f47`

Essential context:

- Motion Engine V1 is accepted/frozen.
- Latest work fixes full-calibration timing, separates the Calibration idle character from the persistent gameplay character, restores Hub pose/locomotion authority, adds an editable `HubEntry`, and adds a Hub follow camera with eight voice-selectable presets.
- Yellow portal = Boxing, but it is disabled because Boxing is unavailable.
- Blue portal = Obstacle Course and uses `ObstacleEntry`.
- The USER must now manually retest Calibration -> Hub pose following, locomotion, camera presets, and blue portal transition.
- Do not start the next gameplay phase or merge to `main` without explicit USER authorization.

Do not rely on older handoff files for current phase status when they conflict with `Docs/current-state.md`.
