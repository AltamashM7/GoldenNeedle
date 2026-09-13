# Superseded Worker Brief — OpenVINO Unity U1

This file is retained only so older chat references do not silently point to a now-wrong execution model.

The previous brief required the Web Builder to stop after U1. The USER has explicitly replaced that workflow because it made development unnecessarily slow.

Use this handoff instead:

`Docs/worker-briefs/openvino-unity-integration-handoff.md`

And use the rolling resume state here:

`Docs/openvino-unity-integration-progress.md`

Authoritative execution plan:

`Docs/openvino-unity-integration-checkpoints.md`

Current rule: U1/U2/U3 checkpoints are durable recovery markers. The intelligent Web Builder should continue normally from one checkpoint into the next in the same run whenever possible, committing/pushing coherent recovery points and updating the progress file. It stops only for genuine USER hardware/visual QA, a real decision blocker, or execution-limit risk.
