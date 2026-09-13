#!/usr/bin/env python3
"""Apply the narrow U3 selectable-backend integration to MediaPipePoseProvider.cs.

This intentionally targets one audited provider blob and fails closed if the source
has drifted. The generated provider keeps the existing TFLite path as enum value 0
and default; OpenVINO is an explicitly selected experimental backend.
"""

from __future__ import annotations

import hashlib
from pathlib import Path
import sys

REPO_ROOT = Path(__file__).resolve().parents[3]
TARGET = REPO_ROOT / "Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipePoseProvider.cs"
EXPECTED_BLOB = "7c9886a8bd888a72ed2405c450351e3c6cf1926f"
PATCH_MARKER = "private void LaunchPreparedOpenVinoInference("


def git_blob_sha(data: bytes) -> str:
    header = f"blob {len(data)}\0".encode("ascii")
    return hashlib.sha1(header + data).hexdigest()


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{label}: expected exactly one source match, found {count}")
    return text.replace(old, new, 1)


def main() -> int:
    raw = TARGET.read_bytes()
    text = raw.decode("utf-8")
    if PATCH_MARKER in text:
        print("U3 provider patch already present; no changes required.")
        return 0

    actual_blob = git_blob_sha(raw)
    if actual_blob != EXPECTED_BLOB:
        raise RuntimeError(
            f"FAIL CLOSED: provider blob drifted. expected={EXPECTED_BLOB} actual={actual_blob}"
        )

    text = replace_once(
        text,
        "using System.Threading;\n",
        "using System.Threading;\nusing System.Threading.Tasks;\n",
        "Task namespace",
    )

    text = replace_once(
        text,
        '        private const string ModelDirectory = "GoldenNeedle/PoseTrackingSpike/Models";\n',
        '        private const string ModelDirectory = "GoldenNeedle/PoseTrackingSpike/Models";\n'
        '        private const string OpenVinoModelDirectory = "GoldenNeedle/OpenVinoPoseModels";\n'
        '        private const string OpenVinoDetectorModelFileName = "pose_detector.tflite";\n'
        '        private const string OpenVinoLandmarkModelFileName = "pose_landmarks_detector.tflite";\n',
        "OpenVINO model constants",
    )

    text = replace_once(
        text,
        '        [Header("Pose Landmarker")]\n'
        '        [SerializeField] private float targetInferenceFps = 30f;\n',
        '        [Header("Pose Landmarker")]\n'
        '        [Tooltip("Stock MediaPipe/TFLite remains the default. OpenVINO CPU FP32 is experimental and must be selected explicitly for A/B QA.")]\n'
        '        [SerializeField] private PoseInferenceBackend inferenceBackend = PoseInferenceBackend.MediaPipeTfliteCpu;\n'
        '        [SerializeField] private float targetInferenceFps = 30f;\n',
        "backend selector",
    )

    text = replace_once(
        text,
        '        private PoseLandmarker _poseLandmarker;\n'
        '        private Coroutine _bootstrapCoroutine;\n',
        '        private PoseLandmarker _poseLandmarker;\n'
        '        private OpenVinoPoseRuntime _openVinoPoseRuntime;\n'
        '        private Task<OpenVinoPoseRuntime> _openVinoBootstrapTask;\n'
        '        private Task _openVinoInferenceTask;\n'
        '        private byte[] _openVinoRgbaBuffer;\n'
        '        private readonly object _openVinoWorkerFailureGate = new object();\n'
        '        private Exception _openVinoWorkerFailure;\n'
        '        private PoseInferenceBackend _activeInferenceBackend = PoseInferenceBackend.MediaPipeTfliteCpu;\n'
        '        private Coroutine _bootstrapCoroutine;\n',
        "OpenVINO runtime fields",
    )

    text = replace_once(
        text,
        '        public float TargetInferenceFps => targetInferenceFps;\n'
        '        public bool CameraSwitchPending => _cameraSwitchPending;\n',
        '        public float TargetInferenceFps => targetInferenceFps;\n'
        '        public PoseInferenceBackend RequestedInferenceBackend => inferenceBackend;\n'
        '        public PoseInferenceBackend ActiveInferenceBackend => _activeInferenceBackend;\n'
        '        public string ActiveInferenceBackendLabel => _activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32\n'
        '            ? "OpenVINO CPU FP32"\n'
        '            : "MediaPipe TFLite CPU";\n'
        '        public string OpenVinoRuntimeInfo { get; private set; } = string.Empty;\n'
        '        public string OpenVinoEngineInfo { get; private set; } = string.Empty;\n'
        '        public float LastOpenVinoManagedInputCopyMilliseconds { get; private set; }\n'
        '        public float LastOpenVinoGraphProcessMilliseconds { get; private set; }\n'
        '        public float LastOpenVinoDetectorInferenceMilliseconds { get; private set; }\n'
        '        public float LastOpenVinoLandmarkInferenceMilliseconds { get; private set; }\n'
        '        public float LastOpenVinoBridgeCopyMilliseconds { get; private set; }\n'
        '        public bool LastOpenVinoDetectorRan { get; private set; }\n'
        '        public bool CameraSwitchPending => _cameraSwitchPending;\n',
        "backend telemetry properties",
    )

    text = replace_once(
        text,
        '            UpdateMetrics();\n\n'
        '            if (HasReceivedResult && Interlocked.Exchange(ref _resultCallbackLogPublished, 1) == 0)\n',
        '            UpdateMetrics();\n'
        '            ConsumeOpenVinoWorkerFailure();\n\n'
        '            if (HasReceivedResult && Interlocked.Exchange(ref _resultCallbackLogPublished, 1) == 0)\n',
        "worker failure consumption",
    )

    text = replace_once(
        text,
        '            if (Status != PoseProviderStatus.Ready || _webCamTexture == null || _poseLandmarker == null || _textureFramePool == null)\n',
        '            if (Status != PoseProviderStatus.Ready || _webCamTexture == null || !IsActiveBackendReady() || _textureFramePool == null)\n',
        "backend-ready Update guard",
    )

    text = replace_once(
        text,
        '            _preparedFramePublishedFrameCount = -1;\n\n'
        '            var frameReleased = false;\n',
        '            _preparedFramePublishedFrameCount = -1;\n\n'
        '            if (_activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32)\n'
        '            {\n'
        '                LaunchPreparedOpenVinoInference(\n'
        '                    textureFrame,\n'
        '                    frameObservedAtSeconds,\n'
        '                    framePublishedAtSeconds,\n'
        '                    framePublishedFrameCount,\n'
        '                    origin);\n'
        '                return;\n'
        '            }\n\n'
        '            var frameReleased = false;\n',
        "native launch branch",
    )

    text = replace_once(
        text,
        '                _cameraSwitchPending,\n'
        '                _poseLandmarker != null,\n'
        '                _preparedTextureFrame != null,\n',
        '                _cameraSwitchPending,\n'
        '                IsActiveBackendReady(),\n'
        '                _preparedTextureFrame != null,\n',
        "immediate-launch backend guard",
    )

    text = replace_once(
        text,
        '            if (_shuttingDown || coordinateConventionVersion != _coordinateConventionVersion || _poseLandmarker == null)\n',
        '            if (_shuttingDown || coordinateConventionVersion != _coordinateConventionVersion || !IsActiveBackendReady())\n',
        "readback publication backend guard",
    )

    native_methods = r'''
        private bool IsActiveBackendReady()
        {
            return _activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32
                ? _openVinoPoseRuntime != null
                : _poseLandmarker != null;
        }

        private void LaunchPreparedOpenVinoInference(
            TextureFrame textureFrame,
            double frameObservedAtSeconds,
            double framePublishedAtSeconds,
            int framePublishedFrameCount,
            PreparedInferenceLaunchOrigin origin)
        {
            var frameReleased = false;
            try
            {
                if (_openVinoPoseRuntime == null)
                {
                    throw new InvalidOperationException("OpenVINO pose runtime is not ready.");
                }

                var width = textureFrame.width;
                var height = textureFrame.height;
                var strideBytes = checked(width * 4);
                var expectedByteCount = checked(strideBytes * height);
                var rawData = textureFrame.GetRawTextureData<byte>();
                if (rawData.Length != expectedByteCount)
                {
                    throw new InvalidOperationException(
                        $"OpenVINO RGBA input size mismatch. expected={expectedByteCount} actual={rawData.Length}");
                }

                if (_openVinoRgbaBuffer == null || _openVinoRgbaBuffer.Length != expectedByteCount)
                {
                    _openVinoRgbaBuffer = new byte[expectedByteCount];
                }

                var copyStartedAtSeconds = NowSeconds();
                rawData.CopyTo(_openVinoRgbaBuffer);
                var managedCopyMilliseconds = (float)((NowSeconds() - copyStartedAtSeconds) * 1000d);
                LastCpuImageBuildDurationMilliseconds = managedCopyMilliseconds;
                LastOpenVinoManagedInputCopyMilliseconds = managedCopyMilliseconds;
                textureFrame.Release();
                frameReleased = true;

                var timestampMillisec = _clock.ElapsedMilliseconds;
                Interlocked.Exchange(ref _inferenceOutstanding, 1);
                _inferenceStartedAtSeconds = NowSeconds();
                _activeInferenceFrameObservedAtSeconds = frameObservedAtSeconds;

                var priorCompletion = CapturePendingInferenceCompletion();
                var diagnosticSessionId = Volatile.Read(ref _inferenceDiagnosticSessionId);
                var diagnosticSerial = Interlocked.Increment(ref _nextInferenceDiagnosticSerial);
                Interlocked.Exchange(ref _activeInferenceDiagnosticSessionId, diagnosticSessionId);
                Interlocked.Exchange(ref _activeInferenceDiagnosticSerial, diagnosticSerial);
                Interlocked.Exchange(ref _activeInferenceDiagnosticTimestampMilliseconds, timestampMillisec);
                Interlocked.Exchange(ref _activeInferenceDiagnosticSubmitReturnTicks, 0L);
                Volatile.Write(ref _activeInferenceDiagnosticAccepted, 0);
                var diagnosticSubmitStartTicks = Stopwatch.GetTimestamp();
                Interlocked.Exchange(ref _activeInferenceDiagnosticSubmitStartTicks, diagnosticSubmitStartTicks);

                var runtime = _openVinoPoseRuntime;
                var rgba = _openVinoRgbaBuffer;
                var rotationDegrees = _orientation.InferenceRotationDegrees;
                _openVinoInferenceTask = Task.Run(() =>
                        runtime.ProcessRgba(
                            rgba,
                            width,
                            height,
                            strideBytes,
                            rotationDegrees,
                            timestampMillisec))
                    .ContinueWith(
                        task =>
                        {
                            if (task.IsFaulted)
                            {
                                PublishOpenVinoWorkerFailure(task.Exception?.GetBaseException() ??
                                    new InvalidOperationException("OpenVINO pose worker failed."));
                                return;
                            }
                            if (task.IsCanceled)
                            {
                                PublishOpenVinoWorkerFailure(
                                    new OperationCanceledException("OpenVINO pose worker was canceled."));
                                return;
                            }

                            try
                            {
                                OnOpenVinoPoseResult(task.Result);
                            }
                            catch (Exception exception)
                            {
                                PublishOpenVinoWorkerFailure(exception);
                            }
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);

                var diagnosticSubmitReturnTicks = Stopwatch.GetTimestamp();
                Interlocked.Exchange(
                    ref _activeInferenceDiagnosticSubmitReturnTicks,
                    diagnosticSubmitReturnTicks);
                Volatile.Write(ref _activeInferenceDiagnosticAccepted, 1);
                RecordInferenceContinuationSample(
                    priorCompletion,
                    diagnosticSessionId,
                    diagnosticSubmitStartTicks,
                    origin);

                var acceptedAtSeconds = NowSeconds();
                _inferenceScheduler.MarkAccepted(acceptedAtSeconds);
                LastPreparedToInferenceLaunchMilliseconds = framePublishedAtSeconds > 0d
                    ? (float)((acceptedAtSeconds - framePublishedAtSeconds) * 1000d)
                    : 0f;
                LastPreparedToInferenceLaunchFrameDelta = framePublishedFrameCount >= 0
                    ? Mathf.Max(0, Time.frameCount - framePublishedFrameCount)
                    : 0;
                _lastAcceptedLaunchOrigin = origin;
                if (origin == PreparedInferenceLaunchOrigin.ReadbackContinuation)
                {
                    _immediateLaunchesInWindow++;
                }
                Interlocked.Increment(ref _totalInferenceRequests);
                _requestsInWindow++;
                if (!_inferenceRequestLogPublished)
                {
                    _inferenceRequestLogPublished = true;
                    UnityEngine.Debug.Log("[PoseTrackingSpike] OpenVINO inference request accepted");
                }
            }
            catch (Exception exception)
            {
                if (!frameReleased)
                {
                    textureFrame.Release();
                }
                Volatile.Write(ref _activeInferenceDiagnosticAccepted, 0);
                Interlocked.Exchange(ref _inferenceOutstanding, 0);
                SetFailure(PoseProviderStatus.InferenceFailed, $"OpenVINO frame/inference launch failed: {exception.Message}");
                UnityEngine.Debug.LogException(exception, this);
            }
        }

        private void OnOpenVinoPoseResult(OpenVinoPoseFrameResult result)
        {
            if (_shuttingDown)
            {
                Interlocked.Exchange(ref _inferenceOutstanding, 0);
                return;
            }

            var callbackEntryTicks = Stopwatch.GetTimestamp();
            var preparedWaitingAtCallbackEntry = Volatile.Read(ref _preparedTextureFrameDiagnosticOccupied) != 0;
            Interlocked.Exchange(ref _resultCallbackReceived, 1);
            Interlocked.Increment(ref _totalResultCallbacks);
            Interlocked.Increment(ref _callbacksInWindow);

            var receivedAtSeconds = NowSeconds();
            var hasPose = result.HasPose && result.Landmarks != null &&
                result.Landmarks.Length == PoseObservation.LandmarkCount;

            lock (_observationGate)
            {
                _pendingObservation.Begin(result.TimestampMillisec, receivedAtSeconds, hasPose);
                if (hasPose)
                {
                    for (var i = 0; i < PoseObservation.LandmarkCount; i++)
                    {
                        var source = result.Landmarks[i];
                        var candidate = PoseTrustClassifier.IsCandidate(
                            source.X,
                            source.Y,
                            source.Z,
                            source.Visibility,
                            source.HasVisibility,
                            source.Presence,
                            source.HasPresence,
                            trustSettings);
                        var tracked = candidate || WasRecentlyTracked(i, receivedAtSeconds);
                        var observation = new PoseLandmarkObservation
                        {
                            index = i,
                            x = source.X,
                            y = source.Y,
                            z = source.Z,
                            visibility = source.Visibility,
                            presence = source.Presence,
                            hasVisibility = source.HasVisibility,
                            hasPresence = source.HasPresence,
                            trust = tracked ? LandmarkTrust.Tracked : LandmarkTrust.Unavailable,
                        };
                        if (source.HasWorld)
                        {
                            observation.worldX = source.WorldX;
                            observation.worldY = source.WorldY;
                            observation.worldZ = source.WorldZ;
                            observation.hasWorldCoordinates = true;
                        }
                        if (candidate)
                        {
                            _lastTrackedAtSeconds[i] = receivedAtSeconds;
                        }
                        _pendingObservation.SetLandmark(in observation);
                    }
                }

                var swap = _latestObservation;
                _latestObservation = _pendingObservation;
                _pendingObservation = swap;
            }

            LastOpenVinoGraphProcessMilliseconds = (float)result.GraphProcessMilliseconds;
            LastOpenVinoDetectorInferenceMilliseconds = (float)result.DetectorInferenceMilliseconds;
            LastOpenVinoLandmarkInferenceMilliseconds = (float)result.LandmarkInferenceMilliseconds;
            LastOpenVinoBridgeCopyMilliseconds = (float)result.BridgeCopyMilliseconds;
            LastOpenVinoDetectorRan = result.DetectorRan;
            LastInferenceDurationMilliseconds = (float)((receivedAtSeconds - _inferenceStartedAtSeconds) * 1000d);
            LastApproxFrameToResultMilliseconds = _activeInferenceFrameObservedAtSeconds > 0d
                ? (float)((receivedAtSeconds - _activeInferenceFrameObservedAtSeconds) * 1000d)
                : 0f;

            var completionTicks = Stopwatch.GetTimestamp();
            var preparedWaitingAtCompletion = Volatile.Read(ref _preparedTextureFrameDiagnosticOccupied) != 0;
            PublishInferenceCompletionDiagnostic(
                callbackEntryTicks,
                completionTicks,
                preparedWaitingAtCallbackEntry || preparedWaitingAtCompletion,
                result.TimestampMillisec);
            Interlocked.Exchange(ref _inferenceOutstanding, 0);
            if (hasPose)
            {
                Interlocked.Increment(ref _resultsInWindow);
            }
        }

        private void PublishOpenVinoWorkerFailure(Exception exception)
        {
            Volatile.Write(ref _activeInferenceDiagnosticAccepted, 0);
            Interlocked.Increment(ref _inferenceDiagnosticMissingSamples);
            if (!_shuttingDown)
            {
                lock (_openVinoWorkerFailureGate)
                {
                    _openVinoWorkerFailure = exception;
                }
            }
            Interlocked.Exchange(ref _inferenceOutstanding, 0);
        }

        private void ConsumeOpenVinoWorkerFailure()
        {
            Exception failure = null;
            lock (_openVinoWorkerFailureGate)
            {
                if (_openVinoWorkerFailure != null)
                {
                    failure = _openVinoWorkerFailure;
                    _openVinoWorkerFailure = null;
                }
            }
            if (failure == null || _shuttingDown)
            {
                return;
            }

            SetFailure(PoseProviderStatus.InferenceFailed, $"OpenVINO pose worker failed: {failure.Message}");
            UnityEngine.Debug.LogException(failure, this);
        }

'''

    text = replace_once(
        text,
        '        private void TryStartLatestReadback()\n',
        native_methods + '        private void TryStartLatestReadback()\n',
        "native runtime methods",
    )

    old_bootstrap = '''            Status = PoseProviderStatus.PreparingModel;
            StatusMessage = $"Preparing local CPU model: {ModelFileName}";

            var modelPath = Path.Combine(Application.streamingAssetsPath, ModelDirectory, ModelFileName);
            if (!File.Exists(modelPath))
            {
                SetFailure(PoseProviderStatus.ModelFailed, $"Local model was not found: {modelPath}");
                CleanupRuntime();
                yield break;
            }

            try
            {
                var options = new PoseLandmarkerOptions(
                    baseOptions: new BaseOptions(BaseOptions.Delegate.CPU, modelAssetPath: modelPath),
                    runningMode: RunningMode.LIVE_STREAM,
                    numPoses: 1,
                    minPoseDetectionConfidence: 0.5f,
                    minPosePresenceConfidence: 0.5f,
                    minTrackingConfidence: 0.5f,
                    outputSegmentationMasks: false,
                    resultCallback: OnPoseLandmarkerResult);

                _poseLandmarker = PoseLandmarker.CreateFromOptions(options);
                RebuildBodyInferenceResources();
                UpdateInputTransform();
                _inferenceScheduler.Reset();
                Status = PoseProviderStatus.Ready;
                StatusMessage = "Camera and CPU Pose Landmarker are ready";
                UnityEngine.Debug.Log($"[PoseTrackingSpike] Ready: camera={SelectedCameraName}, resolution={ActualCameraWidth}x{ActualCameraHeight}, bodyInference={BodyInferenceWidth}x{BodyInferenceHeight}, bodyMode={(BodyInferenceUsesScaledTexture ? "scaled" : "native")}, requestedFps={requestedCameraFps}, model={ModelFileName}, delegate=CPU, poses=1, segmentation=false");
            }
            catch (Exception exception)
            {
                SetFailure(PoseProviderStatus.ModelFailed, $"Pose Landmarker initialization failed: {exception.Message}");
                UnityEngine.Debug.LogException(exception, this);
                CleanupRuntime();
            }
'''
    new_bootstrap = '''            Status = PoseProviderStatus.PreparingModel;
            StatusMessage = inferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32
                ? "Preparing experimental OpenVINO CPU FP32 pose backend"
                : $"Preparing local CPU model: {ModelFileName}";

            var modelPath = Path.Combine(Application.streamingAssetsPath, ModelDirectory, ModelFileName);
            if (inferenceBackend == PoseInferenceBackend.MediaPipeTfliteCpu && !File.Exists(modelPath))
            {
                SetFailure(PoseProviderStatus.ModelFailed, $"Local model was not found: {modelPath}");
                CleanupRuntime();
                yield break;
            }

            try
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
                {
                    var options = new PoseLandmarkerOptions(
                        baseOptions: new BaseOptions(BaseOptions.Delegate.CPU, modelAssetPath: modelPath),
                        runningMode: RunningMode.LIVE_STREAM,
                        numPoses: 1,
                        minPoseDetectionConfidence: 0.5f,
                        minPosePresenceConfidence: 0.5f,
                        minTrackingConfidence: 0.5f,
                        outputSegmentationMasks: false,
                        resultCallback: OnPoseLandmarkerResult);

                    _poseLandmarker = PoseLandmarker.CreateFromOptions(options);
                    _activeInferenceBackend = PoseInferenceBackend.MediaPipeTfliteCpu;
                }

                RebuildBodyInferenceResources();
                UpdateInputTransform();
                _inferenceScheduler.Reset();
                Status = PoseProviderStatus.Ready;
                StatusMessage = $"Camera and {ActiveInferenceBackendLabel} pose backend are ready";
                UnityEngine.Debug.Log($"[PoseTrackingSpike] Ready: camera={SelectedCameraName}, resolution={ActualCameraWidth}x{ActualCameraHeight}, bodyInference={BodyInferenceWidth}x{BodyInferenceHeight}, bodyMode={(BodyInferenceUsesScaledTexture ? "scaled" : "native")}, requestedFps={requestedCameraFps}, backend={ActiveInferenceBackendLabel}, poses=1, segmentation=false");
            }
            catch (Exception exception)
            {
                SetFailure(PoseProviderStatus.ModelFailed, $"Pose backend initialization failed: {exception.Message}");
                UnityEngine.Debug.LogException(exception, this);
                CleanupRuntime();
            }
'''
    text = replace_once(text, old_bootstrap, new_bootstrap, "backend bootstrap")

    text = replace_once(
        text,
        '            _directBodyCpuReadbackFallbackReason = string.Empty;\n'
        '            InferenceRequestsPerSecond = 0f;\n',
        '            _directBodyCpuReadbackFallbackReason = string.Empty;\n'
        '            lock (_openVinoWorkerFailureGate)\n'
        '            {\n'
        '                _openVinoWorkerFailure = null;\n'
        '            }\n'
        '            LastOpenVinoManagedInputCopyMilliseconds = 0f;\n'
        '            LastOpenVinoGraphProcessMilliseconds = 0f;\n'
        '            LastOpenVinoDetectorInferenceMilliseconds = 0f;\n'
        '            LastOpenVinoLandmarkInferenceMilliseconds = 0f;\n'
        '            LastOpenVinoBridgeCopyMilliseconds = 0f;\n'
        '            LastOpenVinoDetectorRan = false;\n'
        '            InferenceRequestsPerSecond = 0f;\n',
        "OpenVINO session reset",
    )

    text = replace_once(
        text,
        '                var inferenceContinuationTimingSummary =\n'
        '                    $"\\nInf next: n={InferenceContinuationTimingSampleCount} " +\n'
        '                    $"prep={InferenceContinuationTimingPreparedWaitingSampleCount}/{InferenceContinuationTimingSampleCount} " +\n'
        '                    $"({FormatTimingRate(InferenceContinuationTimingPreparedWaitingRate)}) " +\n'
        '                    $"cb→next={FormatTimingPair(InferenceContinuationTimingPreparedResultToNextLaunchMedianMilliseconds, InferenceContinuationTimingPreparedResultToNextLaunchP95Milliseconds)}ms " +\n'
        '                    $"U/RB={InferenceContinuationTimingUpdateLaunchCount}/{InferenceContinuationTimingReadbackLaunchCount} " +\n'
        '                    $"missing={InferenceContinuationTimingMissingSampleCount}";\n'
        '                StatusMessage =\n'
        '                    $"Camera/Pose ready | Body input: {BodyInferenceWidth}x{BodyInferenceHeight} {(BodyInferenceUsesScaledTexture ? "scaled" : "native")} | Pipe ms RB/build/detect/~F→R: {LastGpuReadbackDurationMilliseconds:0.0}/{LastCpuImageBuildDurationMilliseconds:0.0}/{LastInferenceDurationMilliseconds:0.0}/{LastApproxFrameToResultMilliseconds:0.0}\\n" +\n',
        '                var inferenceContinuationTimingSummary =\n'
        '                    $"\\nInf next: n={InferenceContinuationTimingSampleCount} " +\n'
        '                    $"prep={InferenceContinuationTimingPreparedWaitingSampleCount}/{InferenceContinuationTimingSampleCount} " +\n'
        '                    $"({FormatTimingRate(InferenceContinuationTimingPreparedWaitingRate)}) " +\n'
        '                    $"cb→next={FormatTimingPair(InferenceContinuationTimingPreparedResultToNextLaunchMedianMilliseconds, InferenceContinuationTimingPreparedResultToNextLaunchP95Milliseconds)}ms " +\n'
        '                    $"U/RB={InferenceContinuationTimingUpdateLaunchCount}/{InferenceContinuationTimingReadbackLaunchCount} " +\n'
        '                    $"missing={InferenceContinuationTimingMissingSampleCount}";\n'
        '                var openVinoTimingSummary = _activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32\n'
        '                    ? $"\\nOV ms managed/graph/det/lmk/bridge: {LastOpenVinoManagedInputCopyMilliseconds:0.0}/{LastOpenVinoGraphProcessMilliseconds:0.0}/{LastOpenVinoDetectorInferenceMilliseconds:0.0}/{LastOpenVinoLandmarkInferenceMilliseconds:0.0}/{LastOpenVinoBridgeCopyMilliseconds:0.0} detRan={(LastOpenVinoDetectorRan ? 1 : 0)}"\n'
        '                    : string.Empty;\n'
        '                StatusMessage =\n'
        '                    $"Camera/Pose ready | Backend: {ActiveInferenceBackendLabel} | Body input: {BodyInferenceWidth}x{BodyInferenceHeight} {(BodyInferenceUsesScaledTexture ? "scaled" : "native")} | Pipe ms RB/build/detect/~F→R: {LastGpuReadbackDurationMilliseconds:0.0}/{LastCpuImageBuildDurationMilliseconds:0.0}/{LastInferenceDurationMilliseconds:0.0}/{LastApproxFrameToResultMilliseconds:0.0}\\n" +\n',
        "backend status timing header",
    )

    text = replace_once(
        text,
        '                    inferenceContinuationTimingSummary +\n'
        '                    directTimingSummary;\n',
        '                    inferenceContinuationTimingSummary +\n'
        '                    directTimingSummary +\n'
        '                    openVinoTimingSummary;\n',
        "OpenVINO timing status tail",
    )

    cleanup_helper = r'''
        private void CleanupOpenVinoRuntime()
        {
            var inferenceTask = _openVinoInferenceTask;
            if (inferenceTask != null)
            {
                try
                {
                    inferenceTask.Wait();
                }
                catch (AggregateException)
                {
                    // Worker errors are surfaced through the provider failure channel while active.
                }
                _openVinoInferenceTask = null;
            }

            var bootstrapTask = _openVinoBootstrapTask;
            if (bootstrapTask != null)
            {
                try
                {
                    bootstrapTask.Wait();
                    if (bootstrapTask.Status == TaskStatus.RanToCompletion &&
                        bootstrapTask.Result != null &&
                        !ReferenceEquals(bootstrapTask.Result, _openVinoPoseRuntime))
                    {
                        bootstrapTask.Result.Dispose();
                    }
                }
                catch (AggregateException)
                {
                    // Initialization failure is already represented by bootstrap/provider status.
                }
                _openVinoBootstrapTask = null;
            }

            _openVinoPoseRuntime?.Dispose();
            _openVinoPoseRuntime = null;
            _openVinoRgbaBuffer = null;
            OpenVinoRuntimeInfo = string.Empty;
            OpenVinoEngineInfo = string.Empty;
            lock (_openVinoWorkerFailureGate)
            {
                _openVinoWorkerFailure = null;
            }
        }

'''
    text = replace_once(
        text,
        '        private bool CleanupRuntime()\n',
        cleanup_helper + '        private bool CleanupRuntime()\n',
        "OpenVINO cleanup helper",
    )

    text = replace_once(
        text,
        '            ReleasePreparedFrame();\n\n'
        '            if (_poseLandmarker != null)\n',
        '            ReleasePreparedFrame();\n'
        '            CleanupOpenVinoRuntime();\n\n'
        '            if (_poseLandmarker != null)\n',
        "OpenVINO cleanup call",
    )

    TARGET.write_text(text, encoding="utf-8", newline="\n")
    print(f"U3 provider patch applied: {TARGET}")
    print(f"new_blob={git_blob_sha(TARGET.read_bytes())}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"U3 provider patch FAILED: {exc}", file=sys.stderr)
        raise
