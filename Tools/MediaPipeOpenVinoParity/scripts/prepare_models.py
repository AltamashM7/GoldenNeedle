from __future__ import annotations

import argparse
import hashlib
import json
import zipfile
from pathlib import Path

BUNDLE_REL = Path("Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes")
BUNDLE_SIZE = 5_777_746
BUNDLE_SHA256 = "59929e1d1ee95287735ddd833b19cf4ac46d29bc7afddbbf6753c459690d574a"
MODELS = {
    "pose_detector.tflite": {
        "size": 2_959_078,
        "sha256": "46837eb883e6ec75b52c5f5ff6a9b78bd35e66c13f95e8c3566c582d146cb1d9",
    },
    "pose_landmarks_detector.tflite": {
        "size": 2_818_390,
        "sha256": "ad6cfd3c903eb31a4ee788b809e45ecf9fa69923b69b9f3f2d9ae616ff433e58",
    },
}


class IdentityError(RuntimeError):
    pass


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require_identity(label: str, actual_size: int, actual_sha: str, expected_size: int, expected_sha: str) -> None:
    if actual_size != expected_size or actual_sha.lower() != expected_sha.lower():
        raise IdentityError(
            f"{label} identity mismatch. expected size={expected_size}, sha256={expected_sha}; "
            f"actual size={actual_size}, sha256={actual_sha}. FAIL CLOSED."
        )


def extract(repo_root: Path, output_dir: Path) -> dict:
    bundle = (repo_root / BUNDLE_REL).resolve()
    if not bundle.is_file():
        raise IdentityError(f"Production task bundle not found: {bundle}")
    bundle_size = bundle.stat().st_size
    bundle_sha = sha256_file(bundle)
    require_identity("task bundle", bundle_size, bundle_sha, BUNDLE_SIZE, BUNDLE_SHA256)

    output_dir.mkdir(parents=True, exist_ok=True)
    report = {
        "bundle": {
            "path": str(BUNDLE_REL).replace("\\", "/"),
            "size": bundle_size,
            "sha256": bundle_sha,
        },
        "models": {},
    }
    with zipfile.ZipFile(bundle) as archive:
        by_basename = {}
        for info in archive.infolist():
            by_basename.setdefault(Path(info.filename).name, []).append(info)
        for name, expected in MODELS.items():
            hits = by_basename.get(name, [])
            if len(hits) != 1:
                raise IdentityError(f"Expected exactly one {name} in task bundle; found {len(hits)}.")
            data = archive.read(hits[0])
            actual_sha = sha256_bytes(data)
            require_identity(name, len(data), actual_sha, expected["size"], expected["sha256"])
            destination = output_dir / name
            if not destination.exists() or destination.read_bytes() != data:
                destination.write_bytes(data)
            report["models"][name] = {
                "size": len(data),
                "sha256": actual_sha,
                "output": destination.name,
            }
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description="Fail-closed extraction of the exact Golden Needle detector and landmark TFLite models.")
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()

    try:
        report = extract(args.repo_root.resolve(), args.output_dir.resolve())
    except Exception as exc:
        print(f"[Gate B] model extraction FAILED: {type(exc).__name__}: {exc}")
        return 2

    text = json.dumps(report, indent=2, sort_keys=True)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(text + "\n", encoding="utf-8")
    print("[Gate B] exact task bundle/model identity PASS")
    for name, item in report["models"].items():
        print(f"[Gate B] {name}: size={item['size']} sha256={item['sha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
