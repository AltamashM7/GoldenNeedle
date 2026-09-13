from __future__ import annotations

import argparse
import importlib.metadata
import json
import platform
import struct
import sys
import time
import traceback
import zipfile
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import benchmark as base

VERSION = "1.0.0"
OV_PIN = "2026.3.0"
ENTRY = "pose_detector.tflite"
MODEL_SIZE = 2_959_078
MODEL_SHA = "46837eb883e6ec75b52c5f5ff6a9b78bd35e66c13f95e8c3566c582d146cb1d9"
INPUT_SHAPE = (1, 224, 224, 3)
OUTPUT_SHAPES = ((1, 2254, 12), (1, 2254, 1))


class ProbeError(RuntimeError):
    pass


def extract_exact_detector(bundle: Path, out_dir: Path) -> tuple[Path, dict[str, Any]]:
    if not bundle.is_file():
        raise ProbeError(f"Production bundle not found: {bundle}")
    bundle_size, bundle_sha = bundle.stat().st_size, base.sha_file(bundle)
    base.fail_identity("bundle", bundle_size, bundle_sha, base.BUNDLE_SIZE, base.BUNDLE_SHA)
    try:
        with zipfile.ZipFile(bundle) as archive:
            hits = [item for item in archive.infolist() if Path(item.filename).name == ENTRY]
            if len(hits) != 1:
                raise ProbeError(f"Expected exactly one {ENTRY}; found {len(hits)}")
            data = archive.read(hits[0])
    except zipfile.BadZipFile as exc:
        raise ProbeError(f"Production bundle is not a readable ZIP: {exc}") from exc

    size, sha = len(data), base.sha_bytes(data)
    base.fail_identity("detector model", size, sha, MODEL_SIZE, MODEL_SHA)
    out_dir.mkdir(parents=True, exist_ok=True)
    model_path = out_dir / ENTRY
    if not model_path.exists() or model_path.read_bytes() != data:
        model_path.write_bytes(data)
    return model_path, {
        "status": "PASS",
        "bundle_size": bundle_size,
        "bundle_sha256": bundle_sha,
        "model_size": size,
        "model_sha256": sha,
    }


def inspect_detector(model: Any) -> dict[str, Any]:
    inputs = [base.port_desc(i, port) for i, port in enumerate(model.inputs)]
    outputs = [base.port_desc(i, port) for i, port in enumerate(model.outputs)]
    input_ok = (
        len(inputs) == 1
        and tuple(inputs[0].get("shape") or ()) == INPUT_SHAPE
        and str(inputs[0].get("dtype", "")).lower() in {"f32", "float32"}
    )
    output_ok = Counter(tuple(item.get("shape") or ()) for item in outputs) == Counter(OUTPUT_SHAPES)
    dtype_ok = all(str(item.get("dtype", "")).lower() in {"f32", "float32"} for item in outputs)
    return {
        "inputs": inputs,
        "outputs": outputs,
        "contract_match": input_ok and output_ok and dtype_ok,
        "input_contract_match": input_ok,
        "output_shape_contract_match": output_ok,
        "outputs_float32": dtype_ok,
        "small_output_indices": [],
    }


def clean_device(result: dict[str, Any]) -> dict[str, Any]:
    import numpy as np

    out = dict(result)
    sanity = out.pop("_sanity", None)
    if sanity is not None:
        checks = []
        for index, array in enumerate(sanity):
            value = np.asarray(array)
            checks.append(
                {
                    "index": index,
                    "shape": list(value.shape),
                    "finite": bool(np.isfinite(value).all()),
                    "nan_count": int(np.isnan(value).sum()),
                    "inf_count": int(np.isinf(value).sum()),
                }
            )
        out["output_sanity"] = checks
        if not all(item["finite"] for item in checks):
            out["status"] = "NONFINITE_OUTPUT"
    return base.safe(out)


def text_report(result: dict[str, Any]) -> str:
    lines = [
        "Golden Needle — Exact Detector OpenVINO Direct-Compatibility Probe",
        f"status: {result.get('status')}",
        f"timestamp_utc: {result.get('timestamp_utc')}",
        "compatibility probe only; one inference per explicit device, not a performance benchmark",
        "no model rewrite, densification, conversion, AUTO/HETERO/MULTI, or hidden fallback",
        "",
        f"environment: {result.get('environment')}",
        f"model_identity: {result.get('model_identity')}",
        f"model_read: {result.get('model_read')}",
        f"model_contract: {result.get('model_contract')}",
        f"available_devices: {result.get('available_devices', [])}",
        "",
    ]
    for name, device in result.get("devices", {}).items():
        lines.append(
            f"{name}: status={device.get('status')} resolved={device.get('resolved_device')} "
            f"compile_ms={device.get('compile_time_ms')} one_infer_ms={device.get('latency', {}).get('mean_ms')}"
        )
        lines.append(f"  compiled_properties={device.get('compiled_properties')}")
        lines.append(f"  output_sanity={device.get('output_sanity')}")
        if device.get("error"):
            lines.append(f"  error={device['error']}")
    if result.get("fatal_error"):
        lines.extend(["", "fatal_error: " + result["fatal_error"], result.get("fatal_traceback", "")])
    return "\n".join(lines).rstrip() + "\n"


def probe(args: argparse.Namespace) -> tuple[dict[str, Any], Path, Path]:
    import numpy as np

    tool = Path(__file__).resolve().parent
    root = args.repo_root.resolve()
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    result: dict[str, Any] = {
        "probe_version": VERSION,
        "timestamp_utc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "status": "STARTED",
        "environment": {
            "os": platform.platform(),
            "python": sys.version.replace("\n", " "),
            "architecture": platform.machine(),
            "pointer_bits": struct.calcsize("P") * 8,
        },
        "devices": {},
    }
    try:
        base.ensure_python()
        model_path, identity = extract_exact_detector(root / base.BUNDLE_REL, tool / "artifacts")
        result["model_identity"] = identity
        try:
            import openvino as ov
        except Exception as exc:
            raise ProbeError(f"OpenVINO import failed: {type(exc).__name__}: {exc}") from exc
        package_version = importlib.metadata.version("openvino")
        result["environment"].update(openvino_version=getattr(ov, "__version__", "unknown"), openvino_package_version=package_version)
        if package_version != OV_PIN:
            raise ProbeError(f"Expected OpenVINO package {OV_PIN}, got {package_version}")

        core = ov.Core()
        available = [str(item) for item in core.available_devices]
        result["available_devices"] = available
        started = time.perf_counter_ns()
        try:
            model = core.read_model(str(model_path))
        except Exception as exc:
            result["model_read"] = {"status": "FAILED", "time_ms": (time.perf_counter_ns() - started) / 1e6, "error": f"{type(exc).__name__}: {exc}"}
            raise ProbeError("Core.read_model failed for exact detector TFLite") from exc
        result["model_read"] = {"status": "SUCCESS", "time_ms": (time.perf_counter_ns() - started) / 1e6}
        contract = inspect_detector(model)
        result["model_contract"] = contract
        if not contract["contract_match"]:
            raise ProbeError("Exact detector OpenVINO tensor contract mismatch")

        data = np.random.default_rng(20260913).random(INPUT_SHAPE, dtype=np.float32)
        run_contract = {"small_output_indices": [], "outputs": contract["outputs"]}
        for device in args.devices:
            raw = base.run_device(ov, core, model, device, available, data, run_contract, 0, 1, 1)
            result["devices"][device] = clean_device(raw)
        result["status"] = "COMPLETE" if any(item.get("status") == "SUCCESS" for item in result["devices"].values()) else "NO_DEVICE_SUCCEEDED"
    except Exception as exc:
        result["status"] = "FAILED_CLOSED"
        result["fatal_error"] = f"{type(exc).__name__}: {exc}"
        result["fatal_traceback"] = traceback.format_exc()
        result.setdefault("model_identity", {"status": "FAILED_OR_NOT_VERIFIED"})

    clean = base.safe(result)
    out_dir = tool / "results"
    out_dir.mkdir(parents=True, exist_ok=True)
    text_path = out_dir / f"openvino-detector-probe-{stamp}.txt"
    json_path = out_dir / f"openvino-detector-probe-{stamp}.json"
    text_path.write_text(text_report(clean), encoding="utf-8")
    json_path.write_text(json.dumps(clean, indent=2, sort_keys=True), encoding="utf-8")
    print(text_report(clean))
    print("=== GOLDEN NEEDLE OPENVINO DETECTOR PROBE SUMMARY ===")
    print(f"overall={clean.get('status')}")
    print(f"exact_model_identity={clean.get('model_identity', {}).get('status', 'n/a')}")
    print(f"model_read={clean.get('model_read', {}).get('status', 'n/a')}")
    print(f"contract_match={clean.get('model_contract', {}).get('contract_match', False)}")
    for name, device in clean.get("devices", {}).items():
        print(f"{name}: status={device.get('status')} resolved={device.get('resolved_device')}")
    print(f"text_report={text_path}")
    print(f"json_report={json_path}")
    print("=== END GOLDEN NEEDLE OPENVINO DETECTOR PROBE SUMMARY ===")
    return result, text_path, json_path


def main() -> int:
    parser = argparse.ArgumentParser(description="Fail-closed exact pose-detector OpenVINO direct compatibility probe")
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parent.parent.parent)
    parser.add_argument("--devices", nargs="+", default=["CPU", "GPU"])
    args = parser.parse_args()
    try:
        args.devices = base.validate_devices(args.devices)
    except ValueError as exc:
        parser.error(str(exc))
    result, _, _ = probe(args)
    return 0 if result.get("status") == "COMPLETE" else 2


if __name__ == "__main__":
    raise SystemExit(main())
