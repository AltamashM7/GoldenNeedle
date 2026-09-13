from __future__ import annotations

import argparse
import shutil
import subprocess
from pathlib import Path

MEDIAPIPE_COMMIT = "c54c06dd8c4314a316c14da31493bcc38ed302e2"
PACKAGE_REL = Path(
    "mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_unity_openvino"
)


def run(*args: str, cwd: Path) -> str:
    completed = subprocess.run(
        args, cwd=cwd, check=False, text=True, capture_output=True
    )
    if completed.returncode != 0:
        raise RuntimeError(
            f"Command failed ({completed.returncode}): {' '.join(args)}\n"
            f"stdout:\n{completed.stdout}\nstderr:\n{completed.stderr}"
        )
    return completed.stdout.strip()


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Install the tracked Golden Needle Unity/OpenVINO runtime overlay "
        "into the ignored pinned MediaPipe 0.10.22 workspace."
    )
    parser.add_argument("--tool-root", type=Path, required=True)
    parser.add_argument("--mediapipe", type=Path, required=True)
    args = parser.parse_args()

    tool_root = args.tool_root.resolve()
    mediapipe = args.mediapipe.resolve()
    actual = run("git", "rev-parse", "HEAD", cwd=mediapipe)
    if actual != MEDIAPIPE_COMMIT:
        raise SystemExit(
            f"FAIL CLOSED: MediaPipe HEAD must be {MEDIAPIPE_COMMIT}, got {actual}"
        )

    overlay = tool_root / "overlay" / PACKAGE_REL
    required_overlay = (
        "BUILD",
        "runtime_telemetry.h",
        "runtime_telemetry.cc",
        "runtime_openvino_inference_calculator.cc",
    )
    for name in required_overlay:
        if not (overlay / name).is_file():
            raise SystemExit(f"Runtime overlay file missing: {overlay / name}")

    header = tool_root / "include" / "golden_needle_openvino_pose.h"
    plugin_source = tool_root / "src" / "golden_needle_openvino_pose.cpp"
    runtime_smoke = tool_root / "tests" / "runtime_pose_smoke.cc"
    for path in (header, plugin_source, runtime_smoke):
        if not path.is_file():
            raise SystemExit(f"Tracked runtime source missing: {path}")

    destination = mediapipe / PACKAGE_REL
    if destination.exists():
        shutil.rmtree(destination)
    shutil.copytree(overlay, destination)
    shutil.copy2(header, destination / "golden_needle_openvino_pose.h")
    shutil.copy2(
        plugin_source, destination / "golden_needle_openvino_pose_plugin.cc"
    )
    shutil.copy2(runtime_smoke, destination / "runtime_pose_smoke.cc")

    calculator = destination / "runtime_openvino_inference_calculator.cc"
    text = calculator.read_text(encoding="utf-8")
    forbidden = (
        "Invoke()",
        "Gate B raw parity",
        "PrintRawParity",
        "shadow_compared_",
    )
    found = [token for token in forbidden if token in text]
    if found:
        raise SystemExit(
            "FAIL CLOSED: runtime calculator contains shadow-inference/parity "
            f"instrumentation: {found}"
        )
    if "InferenceCalculatorNodeImpl" not in text:
        raise SystemExit(
            "FAIL CLOSED: runtime calculator no longer uses MediaPipe's "
            "InferenceCalculatorNodeImpl seam."
        )

    print(f"[U2] installed runtime overlay: {destination}")
    print("[U2] shadow-TFLite invocation/raw-parity guard PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
