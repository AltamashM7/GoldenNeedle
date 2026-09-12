from __future__ import annotations

import struct
from pathlib import Path
from typing import Any

PILLOW_PIN = "12.3.0"
INPUT_SHAPE = (1, 256, 256, 3)


def _u16(data: bytes, off: int) -> int:
    return struct.unpack_from("<H", data, off)[0]


def _u32(data: bytes, off: int) -> int:
    return struct.unpack_from("<I", data, off)[0]


def _field(data: bytes, table: int, field_index: int) -> int | None:
    vtable = table - struct.unpack_from("<i", data, table)[0]
    vlen = _u16(data, vtable)
    slot = 4 + field_index * 2
    if slot + 2 > vlen:
        return None
    rel = _u16(data, vtable + slot)
    return table + rel if rel else None


def _indirect(data: bytes, off: int) -> int:
    return off + _u32(data, off)


def _vector(data: bytes, field_off: int) -> tuple[int, int]:
    vec = _indirect(data, field_off)
    return vec + 4, _u32(data, vec)


def _string(data: bytes, field_off: int) -> str:
    pos = _indirect(data, field_off)
    n = _u32(data, pos)
    return data[pos + 4 : pos + 4 + n].decode("utf-8", errors="replace")


def inspect_tflite_metadata(model_path: Path) -> dict[str, Any]:
    """Best-effort read-only listing of standard TFLite Model.metadata entries."""
    try:
        data = model_path.read_bytes()
        if len(data) < 8:
            raise ValueError("file too short")
        root = _u32(data, 0)
        metadata_field = _field(data, root, 6)
        buffers_field = _field(data, root, 4)
        buffer_sizes: dict[int, int] = {}
        entries: list[dict[str, Any]] = []
        if buffers_field is not None:
            start, count = _vector(data, buffers_field)
            for i in range(count):
                table = _indirect(data, start + i * 4)
                payload = _field(data, table, 0)
                size = 0
                if payload is not None:
                    _, size = _vector(data, payload)
                buffer_sizes[i] = size
        if metadata_field is not None:
            start, count = _vector(data, metadata_field)
            for i in range(count):
                table = _indirect(data, start + i * 4)
                name_field = _field(data, table, 0)
                buffer_field = _field(data, table, 1)
                name = _string(data, name_field) if name_field is not None else ""
                buffer_index = _u32(data, buffer_field) if buffer_field is not None else None
                entries.append(
                    {
                        "name": name,
                        "buffer_index": buffer_index,
                        "buffer_bytes": buffer_sizes.get(buffer_index) if buffer_index is not None else None,
                    }
                )
        names = [x["name"] for x in entries]
        return {
            "status": "INSPECTED",
            "metadata_entries": entries,
            "metadata_names": names,
            "tflite_metadata_present": "TFLITE_METADATA" in names,
            "normalization_semantics_confirmed_from_exact_metadata": False,
            "note": (
                "Standard metadata entry names/buffer sizes were inspected. Opaque payloads are not used to infer ROI or "
                "normalization semantics; representative preprocessing is grounded in MediaPipe graph source instead."
            ),
        }
    except Exception as e:
        return {
            "status": "INSPECTION_ERROR",
            "error": f"{type(e).__name__}: {e}",
            "normalization_semantics_confirmed_from_exact_metadata": False,
        }


def representative_pose_input(path: Path, sha_file) -> tuple[Any, dict[str, Any]]:
    import numpy as np
    from PIL import Image, ImageOps

    if not path.is_file():
        raise ValueError(f"Pose image not found: {path.name}")
    image_sha = sha_file(path)
    try:
        with Image.open(path) as source:
            source = ImageOps.exif_transpose(source)
            original_mode = source.mode
            rgb = source.convert("RGB")
            width, height = rgb.size
            if width <= 0 or height <= 0:
                raise ValueError("invalid dimensions")
            scale = min(256.0 / width, 256.0 / height)
            rw = max(1, min(256, int(round(width * scale))))
            rh = max(1, min(256, int(round(height * scale))))
            resized = rgb.resize((rw, rh), resample=Image.Resampling.BILINEAR)
            canvas = Image.new("RGB", (256, 256), (0, 0, 0))
            left, top = (256 - rw) // 2, (256 - rh) // 2
            canvas.paste(resized, (left, top))
            tensor = np.expand_dims(np.asarray(canvas, dtype=np.float32) / np.float32(255.0), axis=0)
    except ValueError:
        raise
    except Exception as e:
        raise ValueError(f"Could not decode pose image {path.name} ({type(e).__name__})") from None
    if tensor.shape != INPUT_SHAPE or tensor.dtype != np.float32:
        raise ValueError(f"Representative image tensor contract failure: shape={tensor.shape} dtype={tensor.dtype}")
    return tensor, {
        "status": "USED",
        "classification": "REPRESENTATIVE_IMAGE_DERIVED_INPUT",
        "input_label": path.name,
        "image_sha256": image_sha,
        "original_dimensions": [width, height],
        "original_mode": original_mode,
        "tensor_shape": list(tensor.shape),
        "tensor_dtype": str(tensor.dtype),
        "preprocessing": {
            "color_order": "RGB",
            "source_orientation": "EXIF orientation applied before RGB conversion",
            "spatial_transform": "whole image as representative ROI; aspect-preserving resize into 256x256 with centered black letterbox",
            "resize_filter": "Pillow BILINEAR",
            "numeric_range": "float32 [0,1] via uint8/255",
        },
        "provenance": [
            "Repository package manifest: MediaPipe Unity Plugin 0.16.3.",
            "MediaPipe pose_landmark_by_roi_cpu graph: ImageToTensor 256x256, keep_aspect_ratio=true, float range [0,1].",
            "MediaPipe SRGB image format is interleaved R,G,B.",
        ],
        "equivalence_warning": (
            "NOT exact MediaPipe Tasks/production ROI equivalence. Detector/tracker ROI, rotation/projection and exact "
            "ImageToTensor interpolation are not reconstructed. This input is only for backend consistency testing."
        ),
    }
