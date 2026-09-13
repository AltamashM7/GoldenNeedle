from __future__ import annotations

import argparse
import hashlib
import shutil
import subprocess
from pathlib import Path

HOMULER_COMMIT = "cf4c11d8eef724fe24111b7cd795d55ba490aeec"
MEDIAPIPE_COMMIT = "c54c06dd8c4314a316c14da31493bcc38ed302e2"
PATCHES = (
    "mediapipe_opencv.diff",
    "mediapipe_visibility.diff",
    "mediapipe_model_path.diff",
    "mediapipe_extension.diff",
    "mediapipe_workaround.diff",
)

OPENVINO_BRANCH = r'''
    const char* gate_b_backend = std::getenv("GOLDEN_NEEDLE_GATE_B_BACKEND");
    if (gate_b_backend != nullptr &&
        std::string(gate_b_backend) == "OPENVINO_CPU_FP32") {
      const auto& model_asset =
          subgraph_options->base_options().model_asset();
      if (!model_asset.has_file_name() || model_asset.file_name().empty()) {
        return absl::InvalidArgumentError(
            "Gate B OpenVINO inference requires model_asset.file_name; "
            "embedded/file-pointer models are intentionally rejected.");
      }
      auto& inference_node =
          graph.AddNode("GoldenNeedleOpenVinoInferenceCalculator");
      inference_node.GetOptions<mediapipe::InferenceCalculatorOptions>()
          .set_model_path(model_asset.file_name());
      graph.In(kTensorsTag) >> inference_node.In(kTensorsTag);
      inference_node.Out(kTensorsTag) >> graph.Out(kTensorsTag);
      return graph.GetConfig();
    }
'''

ADD_INFERENCE_BRANCH = r'''
  // GOLDEN_NEEDLE_GATE_B_MODEL_ASSET: the OpenVINO proof requires an actual
  // file path. TaskRunner installs ModelResourcesCacheService, so normal Tasks
  // execution would otherwise pass only a model-resources tag into the
  // InferenceSubgraph. Preserve the stock tag behavior for every other mode.
  const char* gate_b_backend = std::getenv("GOLDEN_NEEDLE_GATE_B_BACKEND");
  if (gate_b_backend != nullptr &&
      std::string(gate_b_backend) == "OPENVINO_CPU_FP32") {
    inference_subgraph_opts.mutable_base_options()
        ->mutable_model_asset()
        ->CopyFrom(model_resources.GetModelFile());
  } else if (!model_resources.GetTag().empty()) {
    inference_subgraph_opts.set_model_resources_tag(model_resources.GetTag());
  } else {
    inference_subgraph_opts.mutable_base_options()
        ->mutable_model_asset()
        ->CopyFrom(model_resources.GetModelFile());
  }
'''


def run(*args: str, cwd: Path) -> str:
    completed = subprocess.run(args, cwd=cwd, check=False, text=True, capture_output=True)
    if completed.returncode != 0:
        raise RuntimeError(
            f"Command failed ({completed.returncode}): {' '.join(args)}\n"
            f"stdout:\n{completed.stdout}\nstderr:\n{completed.stderr}"
        )
    return completed.stdout.strip()


def git_head(path: Path) -> str:
    return run("git", "rev-parse", "HEAD", cwd=path)


def apply_homuler_patches(homuler: Path, mediapipe: Path) -> None:
    # These are the exact five patches referenced by Homuler v0.16.3 WORKSPACE.
    patch_dir = homuler / "third_party"
    for name in PATCHES:
        patch = patch_dir / name
        if not patch.is_file():
            raise RuntimeError(f"Required Homuler v0.16.3 patch missing: {patch}")
        check = subprocess.run(
            ["git", "apply", "--check", "--ignore-space-change", "--ignore-whitespace", str(patch)],
            cwd=mediapipe,
            text=True,
            capture_output=True,
        )
        if check.returncode == 0:
            run("git", "apply", "--ignore-space-change", "--ignore-whitespace", str(patch), cwd=mediapipe)
            print(f"[Gate B] applied Homuler patch: {name}")
            continue
        reverse = subprocess.run(
            ["git", "apply", "--reverse", "--check", "--ignore-space-change", "--ignore-whitespace", str(patch)],
            cwd=mediapipe,
            text=True,
            capture_output=True,
        )
        if reverse.returncode == 0:
            print(f"[Gate B] Homuler patch already applied: {name}")
            continue
        raise RuntimeError(
            f"Homuler patch does not apply cleanly: {name}\n"
            f"check stderr:\n{check.stderr}\nreverse stderr:\n{reverse.stderr}"
        )


def patch_model_task_graph(mediapipe: Path) -> None:
    source = mediapipe / "mediapipe" / "tasks" / "cc" / "core" / "model_task_graph.cc"
    if not source.is_file():
        raise RuntimeError(f"Missing pinned source: {source}")
    text = source.read_text(encoding="utf-8")
    if (
        "GOLDEN_NEEDLE_GATE_B_BACKEND" in text
        and "GOLDEN_NEEDLE_GATE_B_MODEL_ASSET" in text
    ):
        print("[Gate B] model_task_graph.cc Gate B seams already applied")
        return

    pristine = run(
        "git",
        "show",
        f"{MEDIAPIPE_COMMIT}:mediapipe/tasks/cc/core/model_task_graph.cc",
        cwd=mediapipe,
    )
    pristine_hash = hashlib.sha256((pristine + "\n").encode("utf-8")).hexdigest()
    print(f"[Gate B] pristine model_task_graph.cc sha256={pristine_hash}")

    include_anchor = '#include <algorithm>\n'
    if include_anchor not in text:
        raise RuntimeError("Unexpected model_task_graph.cc include layout; refusing to improvise.")
    if '#include <cstdlib>\n' not in text:
        text = text.replace(include_anchor, include_anchor + '#include <cstdlib>\n', 1)

    body_anchor = '    Graph graph;\n    auto& model_resources_node = graph.AddNode("ModelResourcesCalculator");\n'
    if "GOLDEN_NEEDLE_GATE_B_BACKEND" not in text:
        if body_anchor not in text:
            raise RuntimeError(
                "Expected InferenceSubgraph graph/model-resources seam not found; refusing to patch."
            )
        replacement = (
            "    Graph graph;\n"
            + OPENVINO_BRANCH
            + '    auto& model_resources_node = graph.AddNode("ModelResourcesCalculator");\n'
        )
        text = text.replace(body_anchor, replacement, 1)

    stock_asset_branch = '''  if (!model_resources.GetTag().empty()) {
    inference_subgraph_opts.set_model_resources_tag(model_resources.GetTag());
  } else {
    inference_subgraph_opts.mutable_base_options()
        ->mutable_model_asset()
        ->CopyFrom(model_resources.GetModelFile());
  }
'''
    if "GOLDEN_NEEDLE_GATE_B_MODEL_ASSET" not in text:
        if stock_asset_branch not in text:
            raise RuntimeError(
                "Expected ModelTaskGraph::AddInference model-resource branch not found; refusing to patch."
            )
        text = text.replace(stock_asset_branch, ADD_INFERENCE_BRANCH, 1)

    source.write_text(text, encoding="utf-8", newline="\n")
    print(
        "[Gate B] patched optional OpenVINO inference node plus file-backed "
        "model handoff; stock Tasks/TFLite behavior remains unchanged"
    )


def copy_overlay(overlay_root: Path, mediapipe: Path) -> None:
    src = (
        overlay_root
        / "mediapipe"
        / "tasks"
        / "cc"
        / "vision"
        / "pose_landmarker"
        / "golden_needle_gate_b"
    )
    dst = (
        mediapipe
        / "mediapipe"
        / "tasks"
        / "cc"
        / "vision"
        / "pose_landmarker"
        / "golden_needle_gate_b"
    )
    if not src.is_dir():
        raise RuntimeError(f"Overlay source missing: {src}")
    if dst.exists():
        shutil.rmtree(dst)
    shutil.copytree(src, dst)
    print(f"[Gate B] installed isolated overlay: {dst}")


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Apply exact Homuler 0.16.3 MediaPipe patches plus the isolated Gate B overlay."
    )
    parser.add_argument("--homuler", type=Path, required=True)
    parser.add_argument("--mediapipe", type=Path, required=True)
    parser.add_argument("--overlay-root", type=Path, required=True)
    args = parser.parse_args()

    homuler = args.homuler.resolve()
    mediapipe = args.mediapipe.resolve()
    actual_homuler = git_head(homuler)
    actual_mediapipe = git_head(mediapipe)
    if actual_homuler != HOMULER_COMMIT:
        raise SystemExit(
            f"FAIL CLOSED: Homuler HEAD must be {HOMULER_COMMIT}, got {actual_homuler}"
        )
    if actual_mediapipe != MEDIAPIPE_COMMIT:
        raise SystemExit(
            f"FAIL CLOSED: MediaPipe HEAD must be {MEDIAPIPE_COMMIT}, got {actual_mediapipe}"
        )

    apply_homuler_patches(homuler, mediapipe)
    patch_model_task_graph(mediapipe)
    copy_overlay(args.overlay_root.resolve(), mediapipe)
    print("[Gate B] overlay application PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
