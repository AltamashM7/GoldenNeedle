from __future__ import annotations

import math
from typing import Any

EPSILON = 1e-12
POSE_RELATED_SHAPES = {
    (1, 195): "landmark-related raw output",
    (1, 117): "world-landmark-related raw output",
}


def array_difference(reference: Any, candidate: Any, reference_profile: str, candidate_profile: str) -> dict[str, Any]:
    import numpy as np

    a = np.asarray(reference)
    b = np.asarray(candidate)
    same = a.shape == b.shape
    result: dict[str, Any] = {
        "reference_profile": reference_profile,
        "candidate_profile": candidate_profile,
        "reference_shape": list(a.shape),
        "candidate_shape": list(b.shape),
        "shape_match": same,
        "reference_finite": bool(np.isfinite(a).all()),
        "candidate_finite": bool(np.isfinite(b).all()),
        "reference_nan_count": int(np.isnan(a).sum()),
        "candidate_nan_count": int(np.isnan(b).sum()),
        "reference_inf_count": int(np.isinf(a).sum()),
        "candidate_inf_count": int(np.isinf(b).sum()),
        "normalization_epsilon": EPSILON,
    }
    ref_finite = np.isfinite(a)
    if ref_finite.any():
        af = a.astype(np.float64, copy=False)[ref_finite]
        mean_abs_ref = float(np.mean(np.abs(af)))
        rms_ref = float(np.sqrt(np.mean(af * af)))
        result["reference_magnitude"] = {
            "min": float(af.min()),
            "max": float(af.max()),
            "mean_absolute_magnitude": mean_abs_ref,
            "rms_magnitude": rms_ref,
        }
    else:
        mean_abs_ref = 0.0
        rms_ref = 0.0
        result["reference_magnitude"] = None

    if same:
        finite_pair = np.isfinite(a) & np.isfinite(b)
        if finite_pair.any():
            av = a.astype(np.float64, copy=False)[finite_pair]
            bv = b.astype(np.float64, copy=False)[finite_pair]
            d = np.abs(av - bv)
            mean_abs_diff = float(d.mean())
            rms_diff = float(np.sqrt(np.mean(d * d)))
            result.update(
                max_absolute_difference=float(d.max()),
                mean_absolute_difference=mean_abs_diff,
                rms_difference=rms_diff,
                normalized_mean_absolute_difference=mean_abs_diff / max(mean_abs_ref, EPSILON),
                normalized_rms_difference=rms_diff / max(rms_ref, EPSILON),
            )
    return result


def compare_profiles(
    profiles: dict[str, dict[str, Any]],
    left: str,
    right: str,
    output_key: str,
    contract: dict[str, Any],
) -> dict[str, Any] | None:
    a = profiles.get(left, {})
    b = profiles.get(right, {})
    if a.get("status") != "SUCCESS" or b.get("status") != "SUCCESS":
        return None
    if output_key not in a or output_key not in b:
        return None
    outputs: list[dict[str, Any]] = []
    for i, (x, y) in enumerate(zip(a[output_key], b[output_key])):
        item = array_difference(x, y, left, right)
        shape = tuple(contract["outputs"][i]["shape"] or ())
        item["index"] = i
        item["shape"] = list(shape)
        if shape in POSE_RELATED_SHAPES:
            item["role_label"] = POSE_RELATED_SHAPES[shape]
        outputs.append(item)
    return {
        "reference_profile": left,
        "candidate_profile": right,
        "scope": (
            "Raw OpenVINO backend consistency only. Not independent LiteRT equivalence, full MediaPipe Tasks equivalence, "
            "decoded-landmark equivalence, or proof that raw channel semantics are reconstructed."
        ),
        "normalization": (
            "Normalized mean-absolute and RMS errors divide by the corresponding reference tensor magnitude with "
            f"epsilon={EPSILON:g} only to avoid division by zero; no PASS/FAIL threshold is assigned."
        ),
        "outputs": outputs,
    }


def numerical_set(profiles: dict[str, dict[str, Any]], output_key: str, contract: dict[str, Any]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for left, right in (
        ("CPU_DEFAULT", "GPU_DEFAULT"),
        ("CPU_DEFAULT", "GPU_ACCURACY_FP32"),
        ("GPU_DEFAULT", "GPU_ACCURACY_FP32"),
    ):
        comparison = compare_profiles(profiles, left, right, output_key, contract)
        if comparison is not None:
            result[f"{left}_vs_{right}"] = comparison
    return result


def compact_pose_related(comparison: dict[str, Any] | None) -> str:
    if not comparison:
        return "n/a"
    parts: list[str] = []
    for item in comparison.get("outputs", []):
        shape = tuple(item.get("shape") or ())
        if shape not in POSE_RELATED_SHAPES:
            continue
        nmae = item.get("normalized_mean_absolute_difference")
        nrms = item.get("normalized_rms_difference")
        if nmae is None or nrms is None:
            parts.append(f"{shape}:n/a")
        else:
            parts.append(f"{shape}:nMAE={nmae:.6g},nRMS={nrms:.6g}")
    return "; ".join(parts) if parts else "n/a"
