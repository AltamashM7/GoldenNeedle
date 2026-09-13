from __future__ import annotations

import argparse
import json
import math
import statistics
from pathlib import Path
from typing import Any, Iterable

MODES = ("TASKS_REFERENCE", "GRAPH_TFLITE_CPU", "GRAPH_OPENVINO_CPU_FP32")
PAIRS = (
    ("TASKS_REFERENCE", "GRAPH_TFLITE_CPU", "tasks-vs-tflite"),
    ("GRAPH_TFLITE_CPU", "GRAPH_OPENVINO_CPU_FP32", "tflite-vs-openvino"),
    ("TASKS_REFERENCE", "GRAPH_OPENVINO_CPU_FP32", "tasks-vs-openvino"),
)


def percentile(values: list[float], q: float) -> float | None:
    if not values:
        return None
    xs = sorted(values)
    pos = (len(xs) - 1) * q
    lo, hi = math.floor(pos), math.ceil(pos)
    if lo == hi:
        return xs[lo]
    return xs[lo] * (hi - pos) + xs[hi] * (pos - lo)


def stats(values: Iterable[float]) -> dict[str, Any]:
    xs = [float(x) for x in values if x is not None and math.isfinite(float(x))]
    if not xs:
        return {"count": 0}
    mean = statistics.fmean(xs)
    return {
        "count": len(xs),
        "mean": mean,
        "p50": percentile(xs, 0.50),
        "p95": percentile(xs, 0.95),
        "p99": percentile(xs, 0.99),
        "min": min(xs),
        "max": max(xs),
        "stddev": statistics.pstdev(xs) if len(xs) > 1 else 0.0,
        "rate_per_second": (1000.0 / mean) if mean > 0 else None,
    }


def error_stats(values: list[float]) -> dict[str, Any]:
    base = stats(values)
    if not values:
        return base
    base["mae"] = statistics.fmean(abs(x) for x in values)
    base["rms"] = math.sqrt(statistics.fmean(x * x for x in values))
    base["p95_abs"] = percentile([abs(x) for x in values], 0.95)
    base["max_abs"] = max(abs(x) for x in values)
    return base


def load_jsonl(path: Path) -> dict[str, Any]:
    header = None
    footer = None
    frames: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8") as stream:
        for line_no, raw in enumerate(stream, 1):
            raw = raw.strip()
            if not raw:
                continue
            item = json.loads(raw)
            kind = item.get("type")
            if kind == "header":
                if header is not None:
                    raise ValueError(f"{path}: duplicate header")
                header = item
            elif kind == "frame":
                frames.append(item)
            elif kind == "footer":
                footer = item
            else:
                raise ValueError(f"{path}:{line_no}: unknown record type {kind!r}")
    if not header or not footer:
        raise ValueError(f"{path}: header/footer missing")
    if header.get("mode") not in MODES or footer.get("mode") != header.get("mode"):
        raise ValueError(f"{path}: mode mismatch")
    expected = int(header.get("frame_count", -1))
    if expected != len(frames):
        raise ValueError(f"{path}: expected {expected} frames, got {len(frames)}")
    for i, frame in enumerate(frames):
        if frame.get("index") != i:
            raise ValueError(f"{path}: frame sequence mismatch at {i}")
    return {"path": str(path), "header": header, "frames": frames, "footer": footer}


def backend_summary(run: dict[str, Any]) -> dict[str, Any]:
    frames = run["frames"]
    latencies = [float(f["latency_ms"]) for f in frames]
    steady = latencies[1:] if len(latencies) > 1 else latencies
    bridge = [f.get("bridge_copy_ms") for f in frames if f.get("bridge_copy_ms") is not None]
    detector_inf = [f.get("detector_inference_ms") for f in frames if f.get("detector_inference_ms") not in (None, 0)]
    landmark_inf = [f.get("landmark_inference_ms") for f in frames if f.get("landmark_inference_ms") not in (None, 0)]
    return {
        "status": "COMPLETE",
        "startup_ms": run["header"].get("startup_ms"),
        "first_frame_ms": run["footer"].get("first_frame_ms"),
        "all_frame_latency_ms": stats(latencies),
        "steady_state_frame_latency_ms": stats(steady),
        "pose_frames": sum(bool(f.get("pose_present")) for f in frames),
        "detector_calls": run["footer"].get("detector_calls"),
        "landmark_calls": run["footer"].get("landmark_calls"),
        "detector_inference_ms": stats(detector_inf),
        "landmark_inference_ms": stats(landmark_inf),
        "bridge_copy_ms": stats(bridge),
        "timing_scope": run["header"].get("timing_scope"),
    }


def optional_error(a: Any, b: Any, out: list[float]) -> None:
    if a is None or b is None:
        return
    out.append(float(b) - float(a))


def compare_landmarks(ref_frames: list[dict[str, Any]], cand_frames: list[dict[str, Any]], field: str) -> dict[str, Any]:
    coord_errors = {axis: [] for axis in ("x", "y", "z")}
    all_coord: list[float] = []
    visibility: list[float] = []
    presence: list[float] = []
    euclidean: list[float] = []
    per_landmark: dict[int, list[float]] = {}
    matched_frames = 0
    shape_mismatch_frames: list[int] = []

    for rf, cf in zip(ref_frames, cand_frames):
        if not rf.get("pose_present") or not cf.get("pose_present"):
            continue
        a = rf.get(field) or []
        b = cf.get(field) or []
        if len(a) != len(b) or len(a) == 0:
            shape_mismatch_frames.append(int(rf["index"]))
            continue
        matched_frames += 1
        for idx, (la, lb) in enumerate(zip(a, b)):
            components = []
            for axis in ("x", "y", "z"):
                diff = float(lb[axis]) - float(la[axis])
                coord_errors[axis].append(diff)
                all_coord.append(diff)
                components.append(diff)
            distance = math.sqrt(sum(x * x for x in components))
            euclidean.append(distance)
            per_landmark.setdefault(idx, []).append(distance)
            optional_error(la.get("visibility"), lb.get("visibility"), visibility)
            optional_error(la.get("presence"), lb.get("presence"), presence)

    worst = []
    for idx, values in per_landmark.items():
        worst.append({
            "index": idx,
            "rms_3d": math.sqrt(statistics.fmean(v * v for v in values)),
            "max_3d": max(values),
            "samples": len(values),
        })
    worst.sort(key=lambda x: x["rms_3d"], reverse=True)
    return {
        "matched_pose_frames": matched_frames,
        "shape_mismatch_frames": shape_mismatch_frames,
        "coordinates": {axis: error_stats(values) for axis, values in coord_errors.items()},
        "overall_xyz": error_stats(all_coord),
        "euclidean_3d": error_stats(euclidean),
        "visibility": error_stats(visibility),
        "presence": error_stats(presence),
        "worst_landmarks": worst[:10],
    }


def compare_roi(ref_frames: list[dict[str, Any]], cand_frames: list[dict[str, Any]]) -> dict[str, Any]:
    center: list[float] = []
    width: list[float] = []
    height: list[float] = []
    rotation: list[float] = []
    matched = 0
    for a, b in zip(ref_frames, cand_frames):
        ra, rb = a.get("next_roi"), b.get("next_roi")
        if not ra or not rb:
            continue
        matched += 1
        dx = float(rb["x_center"]) - float(ra["x_center"])
        dy = float(rb["y_center"]) - float(ra["y_center"])
        center.append(math.sqrt(dx * dx + dy * dy))
        width.append(float(rb["width"]) - float(ra["width"]))
        height.append(float(rb["height"]) - float(ra["height"]))
        dr = float(rb["rotation"]) - float(ra["rotation"])
        dr = math.atan2(math.sin(dr), math.cos(dr))
        rotation.append(dr)
    return {
        "matched_frames": matched,
        "center_distance": error_stats(center),
        "width_error": error_stats(width),
        "height_error": error_stats(height),
        "rotation_error_radians": error_stats(rotation),
    }


def compare_pair(ref: dict[str, Any], cand: dict[str, Any]) -> dict[str, Any]:
    a, b = ref["frames"], cand["frames"]
    if len(a) != len(b):
        raise ValueError("frame-count mismatch")
    agreements = 0
    false_positive: list[int] = []
    false_negative: list[int] = []
    for rf, cf in zip(a, b):
        rp, cp = bool(rf.get("pose_present")), bool(cf.get("pose_present"))
        if rp == cp:
            agreements += 1
        elif cp and not rp:
            false_positive.append(int(rf["index"]))
        else:
            false_negative.append(int(rf["index"]))
    detector_disagreement = []
    for rf, cf in zip(a, b):
        if rf.get("detector_ran") is None or cf.get("detector_ran") is None:
            continue
        if bool(rf["detector_ran"]) != bool(cf["detector_ran"]):
            detector_disagreement.append(int(rf["index"]))
    return {
        "pose_presence_agreement_count": agreements,
        "pose_presence_agreement_rate": agreements / len(a) if a else None,
        "false_positive_frames_relative_to_reference": false_positive,
        "false_negative_frames_relative_to_reference": false_negative,
        "normalized_landmarks": compare_landmarks(a, b, "normalized"),
        "world_landmarks": compare_landmarks(a, b, "world"),
        "tracking_roi": compare_roi(a, b),
        "detector_ran_disagreement_frames": detector_disagreement,
    }


def validate_inputs(runs: dict[str, dict[str, Any]]) -> dict[str, Any]:
    headers = [runs[m]["header"] for m in MODES]
    keys = ("input_label", "input_sha256", "fps", "width", "height", "frame_count")
    mismatches = {}
    for key in keys:
        values = [h.get(key) for h in headers]
        if any(v != values[0] for v in values[1:]):
            mismatches[key] = values
    timestamps = [[f.get("timestamp_ms") for f in runs[m]["frames"]] for m in MODES]
    if any(ts != timestamps[0] for ts in timestamps[1:]):
        mismatches["timestamps"] = "backend frame timestamps differ"
    if mismatches:
        raise ValueError(f"Backends did not process identical input sequence: {mismatches}")
    return {k: headers[0].get(k) for k in keys}


def build_report(paths: list[Path]) -> dict[str, Any]:
    loaded = [load_jsonl(p) for p in paths]
    runs = {item["header"]["mode"]: item for item in loaded}
    missing = [m for m in MODES if m not in runs]
    if missing:
        raise ValueError(f"missing backend run(s): {missing}")
    input_info = validate_inputs(runs)
    comparisons = {}
    for ref_mode, cand_mode, label in PAIRS:
        comparisons[label] = compare_pair(runs[ref_mode], runs[cand_mode])
    return {
        "overall": "EVIDENCE_READY",
        "meaning": "Comparison completed; no Golden Needle semantic acceptance threshold is applied by this tool.",
        "input": input_info,
        "backends": {m: backend_summary(runs[m]) for m in MODES},
        "comparisons": comparisons,
    }


def fmt(value: Any) -> str:
    if value is None:
        return "n/a"
    if isinstance(value, float):
        return f"{value:.9g}"
    return str(value)


def text_report(report: dict[str, Any]) -> str:
    lines = [
        "Golden Needle — MediaPipe 0.10.22 / OpenVINO Gate B parity report",
        f"overall: {report['overall']}",
        report["meaning"],
        "",
        "Input:",
        json.dumps(report["input"], indent=2, sort_keys=True),
        "",
        "Backend performance (offline VIDEO-mode graph capacity; not Unity LIVE_STREAM frame-to-result):",
    ]
    for mode in MODES:
        b = report["backends"][mode]
        steady = b["steady_state_frame_latency_ms"]
        lines.append(
            f"- {mode}: startup={fmt(b['startup_ms'])} ms, first={fmt(b['first_frame_ms'])} ms, "
            f"steady mean={fmt(steady.get('mean'))} ms p95={fmt(steady.get('p95'))} ms "
            f"p99={fmt(steady.get('p99'))} ms rate={fmt(steady.get('rate_per_second'))}/s; "
            f"detector_calls={fmt(b.get('detector_calls'))}, landmark_calls={fmt(b.get('landmark_calls'))}"
        )
    lines += ["", "Semantic comparisons:"]
    for label, comp in report["comparisons"].items():
        n = comp["normalized_landmarks"]["overall_xyz"]
        w = comp["world_landmarks"]["euclidean_3d"]
        lines.append(
            f"- {label}: presence agreement={fmt(comp['pose_presence_agreement_rate'])}; "
            f"normalized xyz RMS={fmt(n.get('rms'))}; "
            f"world 3D RMS={fmt(w.get('rms'))} m; "
            f"false+={comp['false_positive_frames_relative_to_reference']}; "
            f"false-={comp['false_negative_frames_relative_to_reference']}"
        )
    lines += [
        "",
        "Detailed JSON contains per-coordinate MAE/RMS/p95/max, world Euclidean distributions,",
        "visibility/presence errors, per-landmark worst cases, ROI errors, detector continuity,",
        "startup/first-frame/steady-state latency, and OpenVINO inference/bridge-copy statistics.",
        "",
        "No arbitrary PASS/FAIL tolerance is applied. The Orchestrator decides semantic acceptance.",
    ]
    return "\n".join(lines) + "\n"


def console_summary(report: dict[str, Any], text_path: Path, json_path: Path) -> str:
    lines = [
        "=== GOLDEN NEEDLE MEDIAPIPE OPENVINO PARITY SUMMARY ===",
        f"overall={report['overall']}",
        f"input={report['input'].get('input_label')}",
        f"frames={report['input'].get('frame_count')}",
    ]
    for mode in MODES:
        b = report["backends"][mode]["steady_state_frame_latency_ms"]
        lines.append(
            f"{mode}: status=COMPLETE mean={fmt(b.get('mean'))} p95={fmt(b.get('p95'))} rate={fmt(b.get('rate_per_second'))}"
        )
    tvt = report["comparisons"]["tasks-vs-tflite"]
    tvo = report["comparisons"]["tasks-vs-openvino"]
    tfo = report["comparisons"]["tflite-vs-openvino"]
    lines += [
        f"tasks-vs-tflite pose_presence_agreement={fmt(tvt['pose_presence_agreement_rate'])}",
        f"tasks-vs-openvino pose_presence_agreement={fmt(tvo['pose_presence_agreement_rate'])}",
        f"tasks-vs-openvino normalized_xyz_rmse={fmt(tvo['normalized_landmarks']['overall_xyz'].get('rms'))}",
        f"tasks-vs-openvino world_3d_rmse_m={fmt(tvo['world_landmarks']['euclidean_3d'].get('rms'))}",
        f"tflite-vs-openvino normalized_xyz_rmse={fmt(tfo['normalized_landmarks']['overall_xyz'].get('rms'))}",
        f"tflite-vs-openvino world_3d_rmse_m={fmt(tfo['world_landmarks']['euclidean_3d'].get('rms'))}",
        "detector_calls: "
        + ", ".join(f"{m}={fmt(report['backends'][m].get('detector_calls'))}" for m in MODES),
        "landmark_calls: "
        + ", ".join(f"{m}={fmt(report['backends'][m].get('landmark_calls'))}" for m in MODES),
        f"openvino_bridge_copy_mean_ms={fmt(report['backends']['GRAPH_OPENVINO_CPU_FP32']['bridge_copy_ms'].get('mean'))}",
        f"text_report={text_path}",
        f"json_report={json_path}",
        "=== END GOLDEN NEEDLE MEDIAPIPE OPENVINO PARITY SUMMARY ===",
    ]
    return "\n".join(lines)


def self_test() -> int:
    import tempfile
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        paths = []
        for mi, mode in enumerate(MODES):
            path = tmp / f"{mode}.jsonl"
            frames = []
            for i in range(3):
                delta = 0.001 * mi
                lm = [{"x": 0.1 + i + delta, "y": 0.2 + delta, "z": -0.3 + delta,
                       "visibility": 0.9, "presence": 0.95} for _ in range(33)]
                frames.append({
                    "type": "frame", "index": i, "timestamp_ms": i * 33,
                    "latency_ms": 10 + mi + i, "pose_present": True,
                    "normalized": lm, "world": lm, "next_roi": {
                        "x_center": .5 + delta, "y_center": .5, "width": .8, "height": .8, "rotation": 0.0
                    },
                    "detector_ran": i == 0, "auxiliary_available": True,
                    "detector_inference_ms": 2.0 if mi == 2 and i == 0 else None,
                    "landmark_inference_ms": 5.0 if mi == 2 else None,
                    "bridge_copy_ms": 0.1 if mi == 2 else None,
                })
            header = {"type":"header","mode":mode,"input_label":"fixture","input_sha256":"abc",
                      "fps":30.0,"width":640,"height":480,"frame_count":3,"startup_ms":1.0,
                      "timing_scope":"offline_video_mode_graph_capacity"}
            footer = {"type":"footer","mode":mode,"startup_ms":1.0,"first_frame_ms":10+mi,
                      "detector_calls":1 if mi else None,"landmark_calls":3 if mi==2 else None}
            path.write_text("\n".join(json.dumps(x) for x in [header, *frames, footer])+"\n", encoding="utf-8")
            paths.append(path)
        report = build_report(paths)
        assert report["overall"] == "EVIDENCE_READY"
        assert report["comparisons"]["tasks-vs-openvino"]["pose_presence_agreement_rate"] == 1.0
        assert report["comparisons"]["tasks-vs-openvino"]["normalized_landmarks"]["overall_xyz"]["rms"] > 0
    print("SELF-TEST PASS")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Compare Gate B TASKS/TFLite/OpenVINO JSONL runs.")
    parser.add_argument("--runs", type=Path, nargs=3)
    parser.add_argument("--json-out", type=Path)
    parser.add_argument("--text-out", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        return self_test()
    if not args.runs or not args.json_out or not args.text_out:
        parser.error("--runs (3 files), --json-out and --text-out are required")
    try:
        report = build_report(args.runs)
    except Exception as exc:
        print(f"Gate B comparison FAILED: {type(exc).__name__}: {exc}")
        return 2
    args.json_out.parent.mkdir(parents=True, exist_ok=True)
    args.text_out.parent.mkdir(parents=True, exist_ok=True)
    args.json_out.write_text(json.dumps(report, indent=2, sort_keys=True)+"\n", encoding="utf-8")
    args.text_out.write_text(text_report(report), encoding="utf-8")
    print(console_summary(report, args.text_out, args.json_out))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
