from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
PROVIDER = ROOT / "Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipePoseProvider.cs"
OUTPUT = Path(__file__).resolve().parent / "GeneratedScheduling.cs"

text = PROVIDER.read_text(encoding="utf-8")


def extract_class(name: str) -> str:
    marker = f"public sealed class {name}"
    if name == "OpenVinoSchedulingPolicy":
        marker = f"public static class {name}"
    start = text.find(marker)
    if start < 0:
        raise SystemExit(f"missing provider class: {name}")
    brace = text.find("{", start)
    if brace < 0:
        raise SystemExit(f"missing opening brace: {name}")
    depth = 0
    end = None
    for index in range(brace, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                end = index + 1
                break
    if end is None:
        raise SystemExit(f"unterminated provider class: {name}")
    return text[start:end]

classes = [
    extract_class("InferenceLaunchScheduler"),
    extract_class("OpenVinoLatestFrameMailbox"),
    extract_class("OpenVinoSchedulingPolicy"),
]

source = """using System;
using Unity.Collections;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    public enum PreparedInferenceLaunchOrigin
    {
        None,
        Update,
        ReadbackContinuation,
        OpenVinoWorkerContinuation,
    }

""" + "\n\n".join("    " + block.replace("\n", "\n    ") for block in classes) + "\n}\n"

OUTPUT.write_text(source, encoding="utf-8", newline="\n")
print(f"MANAGED_SCHEDULING_SOURCE_EXTRACTED={OUTPUT}")
