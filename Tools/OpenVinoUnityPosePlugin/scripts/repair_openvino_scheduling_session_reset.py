from pathlib import Path

PROVIDER = Path("Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipePoseProvider.cs")

text = PROVIDER.read_text(encoding="utf-8")

anchor = """            Interlocked.Exchange(ref _resultsInWindow, 0);
            Interlocked.Exchange(ref _callbacksInWindow, 0);
            _cameraFramesInWindow = 0;
"""
replacement = """            Interlocked.Exchange(ref _resultsInWindow, 0);
            Interlocked.Exchange(ref _callbacksInWindow, 0);
            Interlocked.Exchange(ref _requestsInWindow, 0);
            Interlocked.Exchange(ref _immediateLaunchesInWindow, 0);
            Interlocked.Exchange(ref _openVinoWorkerContinuationLaunchesInWindow, 0);
            Volatile.Write(ref _latestMainThreadFrameCount, 0);
            _cameraFramesInWindow = 0;
"""

if replacement in text:
    print("OPENVINO_SCHEDULING_SESSION_RESET_REPAIR=ALREADY_APPLIED")
    raise SystemExit(0)

count = text.count(anchor)
if count != 1:
    raise SystemExit(f"session-reset anchor mismatch: expected 1, found {count}")

text = text.replace(anchor, replacement, 1)

required = [
    "Interlocked.Exchange(ref _requestsInWindow, 0);",
    "Interlocked.Exchange(ref _immediateLaunchesInWindow, 0);",
    "Interlocked.Exchange(ref _openVinoWorkerContinuationLaunchesInWindow, 0);",
    "Volatile.Write(ref _latestMainThreadFrameCount, 0);",
]
missing = [item for item in required if item not in text]
if missing:
    raise SystemExit(f"session reset repair incomplete: {missing}")

PROVIDER.write_text(text, encoding="utf-8", newline="\n")
print("OPENVINO_SCHEDULING_SESSION_RESET_REPAIR=PASS")
