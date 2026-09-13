from __future__ import annotations

import argparse
import importlib.metadata
import json
import math
import platform
import struct
import sys
import tempfile
import time
import traceback
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence

import benchmark as base
import pose_input
import precision_metrics

VERSION = "2.0.0"
OV_PIN = "2026.3.0"
PILLOW_PIN = "12.3.0"
PROFILE_ORDER = ("CPU_DEFAULT", "GPU_DEFAULT", "GPU_ACCURACY_FP32")
COMPILED_KEYS = (
    "EXECUTION_DEVICES",
    "PERFORMANCE_HINT",
    "EXECUTION_MODE_HINT",
    "INFERENCE_PRECISION_HINT",
    "NUM_STREAMS",
    "SUPPORTED_PROPERTIES",
)
DEVICE_KEYS = (
    "FULL_DEVICE_NAME",
    "DEVICE_ARCHITECTURE",
    "DEVICE_TYPE",
    "OPTIMIZATION_CAPABILITIES",
    "SUPPORTED_PROPERTIES",
    "NUM_STREAMS",
    "PERFORMANCE_HINT",
    "EXECUTION_MODE_HINT",
    "INFERENCE_PRECISION_HINT",
)


def query(obj: Any, name: str) -> dict[str, Any]:
    try:
        return {"ok": True, "value": base.safe(obj.get_property(name))}
    except Exception as e:
        return {"ok": False, "error": f"{type(e).__name__}: {e}"}


def core_query(core: Any, device: str, name: str) -> dict[str, Any]:
    try:
        return {"ok": True, "value": base.safe(core.get_property(device, name))}
    except Exception as e:
        return {"ok": False, "error": f"{type(e).__name__}: {e}"}


def supported_names(core: Any, device: str) -> tuple[set[str], dict[str, Any]]:
    result = core_query(core, device, "SUPPORTED_PROPERTIES")
    names: set[str] = set()
    if result.get("ok"):
        value = result.get("value") or []
        if isinstance(value, dict):
            items = value.keys()
        elif isinstance(value, (list, tuple, set)):
            items = value
        else:
            items = [value]
        names = {str(x).upper() for x in items}
    return names, result


def validate_profiles(values: Sequence[str]) -> list[str]:
    out: list[str] = []
    for raw in values:
        name = raw.upper()
        if any(token in name for token in ("AUTO", "HETERO", "MULTI")):
            raise ValueError(f"Profile/device {raw!r} forbidden; AUTO/HETERO/MULTI are not permitted.")
        if name not in PROFILE_ORDER:
            raise ValueError(f"Unknown profile {raw!r}; choose from {', '.join(PROFILE_ORDER)}")
        if name not in out:
            out.append(name)
    if not out:
        raise ValueError("At least one profile is required")
    return out


def profile_plan(name: str, supported: set[str]) -> dict[str, Any]:
    device = "CPU" if name == "CPU_DEFAULT" else "GPU"
    requested = {"PERFORMANCE_HINT": "LATENCY"}
    if name == "GPU_ACCURACY_FP32":
        requested.update({"EXECUTION_MODE_HINT": "ACCURACY", "INFERENCE_PRECISION_HINT": "f32"})
    required = {"EXECUTION_MODE_HINT", "INFERENCE_PRECISION_HINT"} if name == "GPU_ACCURACY_FP32" else set()
    return {
        "profile": name,
        "device_request": device,
        "requested_config": requested,
        "supported_property_names": sorted(supported),
        "compile_config_applied": {k: v for k, v in requested.items() if k in supported},
        "missing_required_properties": sorted(required - supported),
    }


def typed_config(ov: Any, plan: dict[str, Any]) -> dict[Any, Any]:
    import openvino.properties.hint as hints

    applied = plan["compile_config_applied"]
    cfg: dict[Any, Any] = {}
    if "PERFORMANCE_HINT" in applied:
        cfg[hints.performance_mode] = hints.PerformanceMode.LATENCY
    if "EXECUTION_MODE_HINT" in applied:
        cfg[hints.execution_mode] = hints.ExecutionMode.ACCURACY
    if "INFERENCE_PRECISION_HINT" in applied:
        cfg[hints.inference_precision] = ov.Type.f32
    return cfg


def snapshot(req: Any, count: int) -> list[Any]:
    import numpy as np

    return [np.array(req.get_output_tensor(i).data, copy=True) for i in range(count)]


def run_profile(
    ov: Any,
    core: Any,
    model: Any,
    name: str,
    available: Sequence[str],
    random_data: Any,
    pose_data: Any | None,
    contract: dict[str, Any],
    warmup: int,
    iterations: int,
    copy_iterations: int,
    config_builder: Any = None,
) -> dict[str, Any]:
    result: dict[str, Any] = {
        "profile": name,
        "status": "NOT_RUN",
        "warmup_iterations": warmup,
        "measured_iterations": iterations,
        "copy_iterations": copy_iterations,
    }
    requested_device = "CPU" if name == "CPU_DEFAULT" else "GPU"
    device = base.resolve(requested_device, available)
    result.update(requested_device=requested_device, resolved_device=device)
    if not device:
        result.update(
            status="UNAVAILABLE",
            error=f"No explicit {requested_device} in core.available_devices; no AUTO/HETERO/MULTI/fallback attempted.",
        )
        return result

    result["device_properties"] = {key: core_query(core, device, key) for key in DEVICE_KEYS}
    supported, supported_query = supported_names(core, device)
    plan = profile_plan(name, supported)
    result["supported_properties_query"] = supported_query
    result["requested_config"] = plan["requested_config"]
    result["supported_property_names"] = plan["supported_property_names"]
    result["compile_config_applied"] = plan["compile_config_applied"]
    if plan["missing_required_properties"]:
        result.update(
            status="UNSUPPORTED_CONFIGURATION",
            error=(
                "Required GPU accuracy/FP32 properties are not exposed: "
                + ", ".join(plan["missing_required_properties"])
                + ". No fallback to GPU_DEFAULT or CPU was attempted."
            ),
        )
        return result

    try:
        cfg = (config_builder or typed_config)(ov, plan)
    except Exception as e:
        result.update(
            status="CONFIGURATION_ERROR",
            error=f"Typed OpenVINO property construction failed: {type(e).__name__}: {e}",
            traceback=traceback.format_exc(),
        )
        return result

    try:
        start = time.perf_counter_ns()
        compiled = core.compile_model(model, device, cfg)
        result["compile_time_ms"] = (time.perf_counter_ns() - start) / 1e6
    except Exception as e:
        result.update(status="COMPILE_FAILED", error=f"{type(e).__name__}: {e}", traceback=traceback.format_exc())
        return result

    result["effective_compiled_properties"] = {key: query(compiled, key) for key in COMPILED_KEYS}
    try:
        req = compiled.create_infer_request()
        req.set_input_tensor(0, ov.Tensor(random_data))
        for _ in range(warmup):
            req.infer(share_outputs=True)
        timings: list[float] = []
        for _ in range(iterations):
            start = time.perf_counter_ns()
            req.infer(share_outputs=True)
            timings.append((time.perf_counter_ns() - start) / 1e6)
        result["latency"] = base.stats(timings)
        result["delta_vs_sentis_cpu"] = base.sentis_delta(result["latency"])
        small = contract["small_output_indices"]
        all_indices = list(range(len(contract["outputs"])))
        result["small_output_copy_proxy"] = base.copy_proxy(req, small, copy_iterations)
        result["all_output_copy_proxy"] = base.copy_proxy(req, all_indices, copy_iterations)
        result["_random_outputs"] = snapshot(req, len(all_indices))
        if pose_data is not None:
            req.set_input_tensor(0, ov.Tensor(pose_data))
            req.infer(share_outputs=True)
            result["_pose_outputs"] = snapshot(req, len(all_indices))
        result["status"] = "SUCCESS"
    except Exception as e:
        result.update(status="RUNTIME_FAILED", error=f"{type(e).__name__}: {e}", traceback=traceback.format_exc())
    return result


def clean_result(result: dict[str, Any]) -> dict[str, Any]:
    out = dict(result)
    profiles: dict[str, Any] = {}
    for name, profile in result.get("profiles", {}).items():
        item = dict(profile)
        item.pop("_random_outputs", None)
        item.pop("_pose_outputs", None)
        profiles[name] = item
    out["profiles"] = profiles
    return base.safe(out)


def fmt(stats: dict[str, Any]) -> str:
    return (
        f"mean={stats['mean_ms']:.3f}ms p50={stats['p50_ms']:.3f} p95={stats['p95_ms']:.3f} "
        f"p99={stats['p99_ms']:.3f} min={stats['min_ms']:.3f} max={stats['max_ms']:.3f} "
        f"std={stats['stddev_ms']:.3f} rate={stats['rate_per_second']:.3f}/s"
    )


def text_report(result: dict[str, Any]) -> str:
    lines = [
        "Golden Needle — OpenVINO Precision + Representative-Pose Validation",
        f"status: {result.get('status')}",
        f"timestamp_utc: {result.get('timestamp_utc')}",
        f"harness_version: {result.get('harness_version')}",
        "",
    ]
    for section in ("environment", "config", "model_identity", "model_read", "tflite_metadata_inspection", "pose_input"):
        if section in result:
            lines.append(section + ":")
            lines += [f"  {k}: {v}" for k, v in result[section].items()]
            lines.append("")
    contract = result.get("model_contract")
    if contract:
        lines.append(f"model_contract: match={contract['contract_match']}")
        for item in contract["inputs"]:
            lines.append(f"  input[{item['index']}] dtype={item['dtype']} shape={item['shape']} elements={item['element_count']} bytes={item['byte_size']}")
        for item in contract["outputs"]:
            lines.append(f"  output[{item['index']}] dtype={item['dtype']} shape={item['shape']} elements={item['element_count']} bytes={item['byte_size']}")
        lines.append(f"  small outputs indices={contract['small_output_indices']} values={contract['small_output_values']} bytes={contract['small_output_bytes']}")
        lines.append("  semantic_channel_mapping_confirmed=False")
        lines.append("")
    lines.append(f"available_devices: {result.get('available_devices', [])}")
    for device, props in result.get("device_inventory", {}).items():
        lines.append(f"  {device}: {props}")
    lines.append("")
    for name in PROFILE_ORDER:
        profile = result.get("profiles", {}).get(name)
        if not profile:
            continue
        lines.extend([
            f"{name}: status={profile.get('status')} resolved={profile.get('resolved_device')} compile_ms={profile.get('compile_time_ms')}",
            f"  requested_config={profile.get('requested_config')}",
            f"  supported_properties={profile.get('supported_property_names')}",
            f"  compile_config_applied={profile.get('compile_config_applied')}",
            f"  effective_compiled_properties={profile.get('effective_compiled_properties')}",
        ])
        if profile.get("latency"):
            lines.append("  latency: " + fmt(profile["latency"]))
        for key in ("small_output_copy_proxy", "all_output_copy_proxy"):
            if profile.get(key):
                lines.append(f"  {key}: bytes={profile[key]['bytes_per_set']} {fmt(profile[key]['statistics'])}")
        if profile.get("error"):
            lines.append(f"  error: {profile['error']}")
        if profile.get("traceback"):
            lines.append("  traceback:\n" + profile["traceback"])
        lines.append("")
    for section in ("random_numerical_comparisons", "pose_numerical_comparisons"):
        comparisons = result.get(section) or {}
        if comparisons:
            lines.append(section + ":")
            for pair, comparison in comparisons.items():
                lines.append(f"  {pair}:")
                lines.append("    " + comparison["scope"])
                lines.append("    " + comparison["normalization"])
                for item in comparison["outputs"]:
                    lines.append("    " + str(item))
            lines.append("")
    if result.get("fatal_error"):
        lines.extend(["fatal_error: " + result["fatal_error"], result.get("fatal_traceback", "")])
    return "\n".join(lines).rstrip() + "\n"


def write_reports(result: dict[str, Any], directory: Path, stem: str) -> tuple[Path, Path]:
    directory.mkdir(parents=True, exist_ok=True)
    clean = clean_result(result)
    text_path = directory / f"{stem}.txt"
    json_path = directory / f"{stem}.json"
    text_path.write_text(text_report(clean), encoding="utf-8")
    json_path.write_text(json.dumps(clean, indent=2, sort_keys=True), encoding="utf-8")
    return text_path, json_path


def effective(profile: dict[str, Any], key: str) -> str:
    value = (profile.get("effective_compiled_properties") or {}).get(key) or {}
    return str(value.get("value")) if value.get("ok") else "QUERY_ERROR:" + str(value.get("error", "not available"))


def summary(result: dict[str, Any], text_path: Path, json_path: Path) -> str:
    lines = [
        "=== GOLDEN NEEDLE OPENVINO PRECISION SUMMARY ===",
        f"overall={result.get('status')}",
        f"exact_model_identity={result.get('model_identity', {}).get('status', 'n/a')}",
    ]
    for name in PROFILE_ORDER:
        profile = result.get("profiles", {}).get(name, {})
        line = f"{name}: status={profile.get('status', 'NOT_RUN')} resolved={profile.get('resolved_device')}"
        if profile.get("latency"):
            s = profile["latency"]
            line += f" mean={s['mean_ms']:.3f}ms p50={s['p50_ms']:.3f} p95={s['p95_ms']:.3f} p99={s['p99_ms']:.3f} rate={s['rate_per_second']:.3f}/s"
        if profile.get("error"):
            line += f" error={profile['error']}"
        lines.append(line)
    gd = result.get("profiles", {}).get("GPU_DEFAULT", {})
    ga = result.get("profiles", {}).get("GPU_ACCURACY_FP32", {})
    lines.append("GPU_DEFAULT effective_precision=" + effective(gd, "INFERENCE_PRECISION_HINT") + " execution_mode=" + effective(gd, "EXECUTION_MODE_HINT"))
    lines.append("GPU_ACCURACY_FP32 effective_precision=" + effective(ga, "INFERENCE_PRECISION_HINT") + " execution_mode=" + effective(ga, "EXECUTION_MODE_HINT"))
    random_cmp = result.get("random_numerical_comparisons") or {}
    lines.append("random CPU-vs-GPU small-output normalized errors=" + precision_metrics.compact_pose_related(random_cmp.get("CPU_DEFAULT_vs_GPU_DEFAULT")))
    lines.append("random CPU-vs-GPU-FP32 small-output normalized errors=" + precision_metrics.compact_pose_related(random_cmp.get("CPU_DEFAULT_vs_GPU_ACCURACY_FP32")))
    pose_used = result.get("pose_input", {}).get("status") == "USED"
    lines.append(f"pose_input={'USED' if pose_used else 'NOT_USED'}")
    if pose_used:
        pose_cmp = result.get("pose_numerical_comparisons") or {}
        lines.append("pose CPU-vs-GPU small-output normalized errors=" + precision_metrics.compact_pose_related(pose_cmp.get("CPU_DEFAULT_vs_GPU_DEFAULT")))
        lines.append("pose CPU-vs-GPU-FP32 small-output normalized errors=" + precision_metrics.compact_pose_related(pose_cmp.get("CPU_DEFAULT_vs_GPU_ACCURACY_FP32")))
    lines.extend([f"text_report={text_path}", f"json_report={json_path}", "=== END GOLDEN NEEDLE OPENVINO PRECISION SUMMARY ==="])
    return "\n".join(lines)


def benchmark(args: argparse.Namespace) -> tuple[dict[str, Any], Path, Path]:
    tool = Path(__file__).resolve().parent
    root = args.repo_root.resolve()
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    result: dict[str, Any] = {
        "harness_version": VERSION,
        "timestamp_utc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "status": "STARTED",
        "config": {
            "openvino_pin": OV_PIN,
            "pillow_pin": PILLOW_PIN,
            "warmup": args.warmup,
            "iterations": args.iterations,
            "copy_iterations": args.copy_iterations,
            "profiles": args.profiles,
            "serial_requests": 1,
            "batching": False,
            "auto_hetero_multi": False,
            "random_seed": 20260913,
            "numerical_normalization_epsilon": precision_metrics.EPSILON,
        },
        "environment": {
            "os": platform.platform(),
            "python": sys.version.replace("\n", " "),
            "architecture": platform.machine(),
            "pointer_bits": struct.calcsize("P") * 8,
        },
        "profiles": {},
        "pose_input": {"status": "NOT_USED"},
    }
    try:
        base.ensure_python()
        model_path, identity = base.extract_exact(root / base.BUNDLE_REL, tool / "artifacts")
        result["model_identity"] = identity
        result["tflite_metadata_inspection"] = pose_input.inspect_tflite_metadata(model_path)
        try:
            import openvino as ov
        except Exception as e:
            raise base.BenchError(f"OpenVINO import failed: {type(e).__name__}: {e}; use run.ps1") from e
        result["environment"]["openvino_version"] = getattr(ov, "__version__", "unknown")
        result["environment"]["openvino_package_version"] = importlib.metadata.version("openvino")
        result["environment"]["pillow_package_version"] = importlib.metadata.version("Pillow")
        if result["environment"]["openvino_package_version"] != OV_PIN:
            raise base.BenchError(f"Expected OpenVINO package {OV_PIN}, got {result['environment']['openvino_package_version']}")
        if result["environment"]["pillow_package_version"] != PILLOW_PIN:
            raise base.BenchError(f"Expected Pillow package {PILLOW_PIN}, got {result['environment']['pillow_package_version']}")

        core = ov.Core()
        available = [str(x) for x in core.available_devices]
        result["available_devices"] = available
        result["device_inventory"] = {device: {key: core_query(core, device, key) for key in DEVICE_KEYS} for device in available}
        start = time.perf_counter_ns()
        try:
            model = core.read_model(str(model_path))
        except Exception as e:
            result["model_read"] = {"status": "FAILED", "time_ms": (time.perf_counter_ns() - start) / 1e6, "error": f"{type(e).__name__}: {e}", "traceback": traceback.format_exc()}
            raise base.BenchError("Core.read_model failed for exact TFLite") from e
        result["model_read"] = {"status": "SUCCESS", "time_ms": (time.perf_counter_ns() - start) / 1e6}
        contract = base.inspect_model(model)
        result["model_contract"] = contract
        if not contract["contract_match"]:
            raise base.BenchError("OpenVINO contract mismatch; failing closed before timing")
        if contract["small_output_values"] != 313 or contract["small_output_bytes"] != 1252:
            raise base.BenchError("Small output proxy is not 313 float32 / 1,252 bytes")

        random_data = base.input_data()
        pose_data = None
        if args.pose_image is not None:
            pose_data, result["pose_input"] = pose_input.representative_pose_input(args.pose_image, base.sha_file)

        for name in args.profiles:
            result["profiles"][name] = run_profile(ov, core, model, name, available, random_data, pose_data, contract, args.warmup, args.iterations, args.copy_iterations)
        result["random_numerical_comparisons"] = precision_metrics.numerical_set(result["profiles"], "_random_outputs", contract)
        if pose_data is not None:
            result["pose_numerical_comparisons"] = precision_metrics.numerical_set(result["profiles"], "_pose_outputs", contract)
        result["status"] = "COMPLETE" if any(x.get("status") == "SUCCESS" for x in result["profiles"].values()) else "NO_PROFILE_SUCCEEDED"
    except Exception as e:
        result["status"] = "FAILED_CLOSED"
        result["fatal_error"] = f"{type(e).__name__}: {e}"
        result["fatal_traceback"] = traceback.format_exc()
        result.setdefault("model_identity", {"status": "FAILED_OR_NOT_VERIFIED"})

    text_path, json_path = write_reports(result, tool / "results", f"openvino-precision-{stamp}")
    clean = clean_result(result)
    print(text_report(clean))
    print(summary(clean, text_path, json_path))
    return result, text_path, json_path


def self_test() -> int:
    import numpy as np
    from PIL import Image

    failures: list[str] = []
    try:
        validate_profiles(["AUTO"])
        failures.append("AUTO rejection")
    except ValueError:
        pass
    for forbidden in ("HETERO", "MULTI"):
        try:
            validate_profiles([forbidden])
            failures.append(forbidden + " rejection")
        except ValueError:
            pass
    full = {"PERFORMANCE_HINT", "EXECUTION_MODE_HINT", "INFERENCE_PRECISION_HINT"}
    if profile_plan("GPU_ACCURACY_FP32", full)["missing_required_properties"]:
        failures.append("accuracy profile construction")
    if set(profile_plan("GPU_ACCURACY_FP32", {"PERFORMANCE_HINT"})["missing_required_properties"]) != {"EXECUTION_MODE_HINT", "INFERENCE_PRECISION_HINT"}:
        failures.append("accuracy unsupported-property construction")
    diff = precision_metrics.array_difference(np.array([1, 2, 3], dtype=np.float32), np.array([1, 2.5, 2], dtype=np.float32), "CPU_DEFAULT", "GPU_DEFAULT")
    if not math.isclose(diff.get("mean_absolute_difference", -1), 0.5) or "normalized_rms_difference" not in diff:
        failures.append("numerical metrics")

    with tempfile.TemporaryDirectory() as td:
        directory = Path(td)
        image_path = directory / "pose.png"
        Image.new("RGB", (320, 180), (64, 128, 192)).save(image_path)
        try:
            tensor, metadata = pose_input.representative_pose_input(image_path, base.sha_file)
            if tuple(tensor.shape) != base.INPUT_SHAPE or tensor.dtype != np.float32 or metadata.get("classification") != "REPRESENTATIVE_IMAGE_DERIVED_INPUT":
                failures.append("pose image preprocessing")
        except Exception:
            failures.append("pose image preprocessing")
        write_reports({"status": "SELF_TEST", "profiles": {}, "pose_input": {"status": "NOT_USED"}}, directory / "r", "test")

    class FakeCore:
        def __init__(self, supported: Any, compile_error: bool = False):
            self.supported = supported
            self.compile_error = compile_error
        def get_property(self, _device: str, name: str) -> Any:
            return self.supported if name == "SUPPORTED_PROPERTIES" else "fake"
        def compile_model(self, *_args: Any, **_kwargs: Any) -> Any:
            if self.compile_error:
                raise RuntimeError("synthetic compile failure")
            raise AssertionError("compile_model should not be reached")

    dict_supported = FakeCore({"PERFORMANCE_HINT": "RW", "EXECUTION_MODE_HINT": "RW", "INFERENCE_PRECISION_HINT": "RW"})
    parsed, _ = supported_names(dict_supported, "GPU.0")
    if parsed != {"PERFORMANCE_HINT", "EXECUTION_MODE_HINT", "INFERENCE_PRECISION_HINT"}:
        failures.append("dictionary supported-properties parsing")

    contract = {"small_output_indices": [], "outputs": []}
    unsupported = run_profile(None, FakeCore(["PERFORMANCE_HINT"]), object(), "GPU_ACCURACY_FP32", ["GPU.0"], None, None, contract, 1, 1, 1)
    if unsupported.get("status") != "UNSUPPORTED_CONFIGURATION":
        failures.append("accuracy unsupported behavior")
    missing = run_profile(None, FakeCore(["PERFORMANCE_HINT"]), object(), "GPU_DEFAULT", ["CPU"], None, None, contract, 1, 1, 1)
    if missing.get("status") != "UNAVAILABLE":
        failures.append("missing GPU behavior")
    passthrough = lambda _ov, plan: dict(plan["compile_config_applied"])
    for name, supported in (("GPU_DEFAULT", ["PERFORMANCE_HINT"]), ("GPU_ACCURACY_FP32", ["PERFORMANCE_HINT", "EXECUTION_MODE_HINT", "INFERENCE_PRECISION_HINT"])):
        failed = run_profile(None, FakeCore(supported, True), object(), name, ["GPU.0"], None, None, contract, 1, 1, 1, config_builder=passthrough)
        if failed.get("status") != "COMPILE_FAILED":
            failures.append(name + " compile-failure behavior")

    if failures:
        print("SELF-TEST FAILED: " + ", ".join(failures), file=sys.stderr)
        return 1
    print("SELF-TEST PASS")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Golden Needle OpenVINO precision + representative-pose validation")
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parent.parent.parent)
    parser.add_argument("--warmup", type=int, default=30)
    parser.add_argument("--iterations", type=int, default=300)
    parser.add_argument("--copy-iterations", type=int, default=100)
    parser.add_argument("--profiles", nargs="+", default=list(PROFILE_ORDER))
    parser.add_argument("--pose-image", type=Path, default=None)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        return self_test()
    try:
        args.profiles = validate_profiles(args.profiles)
    except ValueError as e:
        parser.error(str(e))
    if args.warmup < 0:
        parser.error("--warmup must be >= 0")
    if args.iterations <= 0 or args.copy_iterations <= 0:
        parser.error("--iterations and --copy-iterations must be > 0")
    result, _, _ = benchmark(args)
    return 0 if result.get("status") == "COMPLETE" else 2


if __name__ == "__main__":
    raise SystemExit(main())
