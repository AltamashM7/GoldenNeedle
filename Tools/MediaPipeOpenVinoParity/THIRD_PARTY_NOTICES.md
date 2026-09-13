# Gate B third-party source and redistribution notes

This isolated proof does not vendor MediaPipe, Homuler MediaPipeUnityPlugin, OpenVINO, TensorFlow Lite or OpenCV binaries/sources into Golden Needle Git history. Bootstrap downloads/clones them into ignored local workspaces.

## Homuler MediaPipeUnityPlugin

- project: `homuler/MediaPipeUnityPlugin`
- Gate B pin: `v0.16.3` / `cf4c11d8eef724fe24111b7cd795d55ba490aeec`
- project license: MIT
- Gate B reuses its WORKSPACE, Windows Bazel configuration and MediaPipe patch set in an ignored build workspace.
- Its upstream/third-party notices remain authoritative for material pulled by that workspace.

## Google MediaPipe

- project: `google-ai-edge/mediapipe`
- Gate B pin: `v0.10.22` / `c54c06dd8c4314a316c14da31493bcc38ed302e2`
- project license: Apache License 2.0
- Gate B does not copy its pose graph source into this repository.
- A local ignored checkout is patched narrowly at the inference-node construction seam.

## OpenVINO

- project: OpenVINO / Intel
- Gate B C++ toolkit pin: `2026.3.0`, Windows build `22451`
- official archive and adjacent official SHA-256 sidecar are used.
- project/runtime licensing is governed by the license/third-party notice files distributed with that toolkit.
- No OpenVINO DLL/library is committed here.
- A later production native plugin must include the redistributable runtime components and notices required by the OpenVINO distribution actually shipped.

## Gate B overlay code

Files under `overlay/` are Golden Needle research code and carry Apache-2.0 notices where appropriate. The OpenVINO calculator is an original small adapter designed from public MediaPipe/OpenVINO APIs; Intel's older OpenVINO MediaPipe fork was used only as an architectural reference and is not copied wholesale.

## Models

Gate B uses the exact detector and landmark models already embedded in Golden Needle's production MediaPipe task bundle. It does not download or substitute alternate weights.

Any future RTMPose/Holistic/other model acquisition requires separate model-card, dataset and redistribution review; repository-level software licensing must not be treated as a model-weight license.
