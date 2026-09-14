#!/usr/bin/env python3
"""Repair the U3 OpenVINO bootstrap coroutine so Unity C# can compile it.

C# iterator methods cannot yield from a try block that has a catch clause. The
original U3 integration awaited the OpenVINO Task with `yield return null` inside
that try/catch. This migration moves only the asynchronous wait before the
catchable initialization block while preserving the existing failure handling.
"""

from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
TARGET = REPO_ROOT / "Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipePoseProvider.cs"
FIX_MARKER = "Task<OpenVinoPoseRuntime> bootstrapTask = null;"

OLD = '''            try
            {
                if (inferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32)
                {
                    var detectorPath = Path.Combine(
                        Application.streamingAssetsPath,
                        OpenVinoModelDirectory,
                        OpenVinoDetectorModelFileName);
                    var landmarkPath = Path.Combine(
                        Application.streamingAssetsPath,
                        OpenVinoModelDirectory,
                        OpenVinoLandmarkModelFileName);
                    if (!File.Exists(detectorPath) || !File.Exists(landmarkPath))
                    {
                        throw new FileNotFoundException(
                            "Generated OpenVINO detector/landmark files are missing. Run Tools/OpenVinoUnityPosePlugin/scripts/package_unity.ps1 before selecting the experimental backend.");
                    }

                    var bootstrapTask = Task.Run(() => OpenVinoPoseRuntime.Create(detectorPath, landmarkPath));
                    _openVinoBootstrapTask = bootstrapTask;
                    while (!bootstrapTask.IsCompleted)
                    {
                        yield return null;
                    }
                    _openVinoBootstrapTask = null;
                    if (bootstrapTask.IsCanceled)
                    {
                        throw new OperationCanceledException("OpenVINO pose runtime initialization was canceled.");
                    }
                    if (bootstrapTask.IsFaulted)
                    {
                        throw bootstrapTask.Exception?.GetBaseException() ??
                            new InvalidOperationException("OpenVINO pose runtime initialization failed.");
                    }

                    _openVinoPoseRuntime = bootstrapTask.Result;
                    _activeInferenceBackend = PoseInferenceBackend.OpenVinoCpuFp32;
                    OpenVinoRuntimeInfo = _openVinoPoseRuntime.RuntimeInfo;
                    OpenVinoEngineInfo = _openVinoPoseRuntime.EngineInfo;
                }
                else
'''

NEW = '''            Task<OpenVinoPoseRuntime> bootstrapTask = null;
            if (inferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32)
            {
                var detectorPath = Path.Combine(
                    Application.streamingAssetsPath,
                    OpenVinoModelDirectory,
                    OpenVinoDetectorModelFileName);
                var landmarkPath = Path.Combine(
                    Application.streamingAssetsPath,
                    OpenVinoModelDirectory,
                    OpenVinoLandmarkModelFileName);
                if (!File.Exists(detectorPath) || !File.Exists(landmarkPath))
                {
                    SetFailure(
                        PoseProviderStatus.ModelFailed,
                        "Generated OpenVINO detector/landmark files are missing. Run Tools/OpenVinoUnityPosePlugin/scripts/package_unity.ps1 before selecting the experimental backend.");
                    CleanupRuntime();
                    yield break;
                }

                bootstrapTask = Task.Run(() => OpenVinoPoseRuntime.Create(detectorPath, landmarkPath));
                _openVinoBootstrapTask = bootstrapTask;
                while (!bootstrapTask.IsCompleted)
                {
                    yield return null;
                }
                _openVinoBootstrapTask = null;
            }

            try
            {
                if (inferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32)
                {
                    if (bootstrapTask.IsCanceled)
                    {
                        throw new OperationCanceledException("OpenVINO pose runtime initialization was canceled.");
                    }
                    if (bootstrapTask.IsFaulted)
                    {
                        throw bootstrapTask.Exception?.GetBaseException() ??
                            new InvalidOperationException("OpenVINO pose runtime initialization failed.");
                    }

                    _openVinoPoseRuntime = bootstrapTask.Result;
                    _activeInferenceBackend = PoseInferenceBackend.OpenVinoCpuFp32;
                    OpenVinoRuntimeInfo = _openVinoPoseRuntime.RuntimeInfo;
                    OpenVinoEngineInfo = _openVinoPoseRuntime.EngineInfo;
                }
                else
'''


def main() -> int:
    text = TARGET.read_text(encoding="utf-8")
    if FIX_MARKER in text:
        print("U3 bootstrap coroutine compile repair already present; no changes required.")
        return 0

    count = text.count(OLD)
    if count != 1:
        raise RuntimeError(
            f"FAIL CLOSED: expected exactly one unfixed U3 BootstrapAsync block, found {count}"
        )

    patched = text.replace(OLD, NEW, 1)
    if FIX_MARKER not in patched:
        raise RuntimeError("FAIL CLOSED: coroutine repair marker missing after replacement")

    TARGET.write_text(patched, encoding="utf-8")
    print("U3_BOOTSTRAP_COROUTINE_COMPILE_REPAIR=PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
