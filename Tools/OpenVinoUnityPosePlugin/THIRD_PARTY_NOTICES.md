# Third-party notices — OpenVINO Unity pose plugin

This source workspace itself does not commit third-party binaries. Its bootstrap/package scripts obtain an exact official OpenVINO distribution and stage runtime files locally for the experimental Windows Unity plugin.

## OpenVINO 2026.3.0

- Project: OpenVINO Toolkit
- Upstream: Intel / openvinotoolkit
- Version used here: 2026.3.0
- License: Apache License 2.0
- Distribution source: official OpenVINO Windows x86_64 package URL pinned in `scripts/bootstrap.ps1`
- Integrity: the adjacent official `.sha256` sidecar is downloaded and verified before extraction.

When a generated package is redistributed, preserve the OpenVINO distribution's license/third-party notices alongside the application as required by its licenses. U1 is an internal integration proof, not a shipping-license approval.

## MediaPipe 0.10.22

- Project: MediaPipe
- Version/commit: v0.10.22 / `c54c06dd8c4314a316c14da31493bcc38ed302e2`
- License: Apache License 2.0

U1 records this generation pin because U2 will reuse MediaPipe's exact preprocessing/postprocessing/tracking/world-landmark semantics. U1 does not package a second MediaPipe runtime.

## Homuler MediaPipeUnityPlugin 0.16.3

- Project: MediaPipeUnityPlugin
- Version/commit: v0.16.3 / `cf4c11d8eef724fe24111b7cd795d55ba490aeec`
- License: MIT for the plugin project; bundled/upstream dependencies retain their own licenses.

The Golden Needle OpenVINO DLL remains separate from Homuler's stock native plugin. Nothing in this workspace replaces or renames the stock MediaPipe/TFLite runtime.
