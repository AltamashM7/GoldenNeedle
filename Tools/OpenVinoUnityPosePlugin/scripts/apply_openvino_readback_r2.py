#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PROVIDER = ROOT / "Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipePoseProvider.cs"
EDITOR = ROOT / "Assets/GoldenNeedle/Editor/MediaPipePoseProviderEditor.cs"

def replace_once(text, old, new, label):
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one marker, found {count}")
    return text.replace(old, new, 1)

provider = PROVIDER.read_text(encoding="utf-8")
editor = EDITOR.read_text(encoding="utf-8")

provider = replace_once(
    provider,
    """    public enum BodyReadbackPath
    {
        Homuler,
        DirectCPU,
        DirectFallback,
    }

    public enum DirectReadbackStage
""",
    """    public enum BodyReadbackPath
    {
        Homuler,
        DirectCPU,
        DirectFallback,
    }

    public enum BodyFrameAcquisitionMode
    {
        ExistingReadback,
        WebCamCpuPixels,
    }

    public enum DirectReadbackStage
""",
    "body acquisition enum",
)

provider = replace_once(
    provider,
    """        [Tooltip("Attempt body-pose inference immediately when a readback finishes. If unsafe or ineligible, the frame remains prepared for the normal Update path; no extra queue or concurrent inference is created.")]
        [SerializeField] private bool enableImmediateInferenceLaunchAfterReadback = true;
        [Tooltip("Experimental body-pose-only path. When eligible it writes GPU readback directly into the pooled TextureFrame CPU buffer, bypassing Homuler staging/copy/Apply. Flipped inputs use one persistent Golden Needle staging RT with Homuler-equivalent scale/offset before the direct readback. It remains CPU pose inference and never changes CameraTexture or Lab display.")]
        [SerializeField] private bool enableDirectBodyCpuReadback = false;
""",
    """        [Tooltip("Attempt body-pose inference immediately when a readback finishes. If unsafe or ineligible, the frame remains prepared for the normal Update path; no extra queue or concurrent inference is created.")]
        [SerializeField] private bool enableImmediateInferenceLaunchAfterReadback = true;
        [Tooltip("Select the existing RenderTexture/readback baseline or the bounded WebCamTexture.GetPixels32 CPU acquisition experiment. The CPU experiment is OpenVINO-only and falls back explicitly to the existing path for the current session if it is unavailable.")]
        [SerializeField] private BodyFrameAcquisitionMode bodyFrameAcquisitionMode = BodyFrameAcquisitionMode.ExistingReadback;
        [Tooltip("Experimental baseline readback optimization. This setting is used only by ExistingReadback (including explicit R2 fallback); the WebCam CPU experiment bypasses the RenderTexture/readback path.")]
        [SerializeField] private bool enableDirectBodyCpuReadback = false;
""",
    "serialized acquisition mode",
)

provider = replace_once(
    provider,
    """        private readonly DirectReadbackTimingWindow _directReadbackTimingWindow =
            new DirectReadbackTimingWindow(64);
        private int _inferenceDiagnosticSessionId = 1;
""",
    """        private readonly DirectReadbackTimingWindow _directReadbackTimingWindow =
            new DirectReadbackTimingWindow(64);
        private readonly WebCamCpuAcquisitionTimingWindow _webCamCpuAcquisitionTimingWindow =
            new WebCamCpuAcquisitionTimingWindow(64);
        private BodyFrameAcquisitionMode _activeBodyFrameAcquisitionMode = BodyFrameAcquisitionMode.ExistingReadback;
        private bool _webCamCpuAcquisitionSessionAvailable = true;
        private string _webCamCpuAcquisitionFallbackReason = string.Empty;
        private Color32[] _webCamCpuPixels;
        private NativeArray<byte> _webCamCpuPreparedRgba;
        private int _inferenceDiagnosticSessionId = 1;
""",
    "cpu acquisition fields",
)

provider = replace_once(
    provider,
    """        public bool ImmediateInferenceLaunchAfterReadbackEnabled => enableImmediateInferenceLaunchAfterReadback;
        public bool DirectBodyCpuReadbackEnabled => enableDirectBodyCpuReadback;
        public BodyReadbackPath ActiveBodyReadbackPath => _activeBodyReadbackPath;
""",
    """        public bool ImmediateInferenceLaunchAfterReadbackEnabled => enableImmediateInferenceLaunchAfterReadback;
        public BodyFrameAcquisitionMode RequestedBodyFrameAcquisitionMode => bodyFrameAcquisitionMode;
        public BodyFrameAcquisitionMode ActiveBodyFrameAcquisitionMode => _activeBodyFrameAcquisitionMode;
        public string RequestedBodyFrameAcquisitionModeLabel =>
            bodyFrameAcquisitionMode == BodyFrameAcquisitionMode.WebCamCpuPixels
                ? "WebCamCPU/GetPixels32"
                : "ExistingReadback";
        public string ActiveBodyFrameAcquisitionModeLabel =>
            _activeBodyFrameAcquisitionMode == BodyFrameAcquisitionMode.WebCamCpuPixels
                ? "WebCamCPU/GetPixels32"
                : bodyFrameAcquisitionMode == BodyFrameAcquisitionMode.WebCamCpuPixels &&
                  !string.IsNullOrEmpty(_webCamCpuAcquisitionFallbackReason)
                    ? "ExistingReadback (R2 fallback)"
                    : "ExistingReadback";
        public string WebCamCpuAcquisitionFallbackReason => _webCamCpuAcquisitionFallbackReason;
        public int WebCamCpuAcquisitionTimingSampleCount => _webCamCpuAcquisitionTimingWindow.ValidSampleCount;
        public double WebCamCpuGetPixelsMedianMilliseconds =>
            _webCamCpuAcquisitionTimingWindow.GetMedianMilliseconds(WebCamCpuAcquisitionTimingMetric.GetPixels32);
        public double WebCamCpuGetPixelsP95Milliseconds =>
            _webCamCpuAcquisitionTimingWindow.GetP95Milliseconds(WebCamCpuAcquisitionTimingMetric.GetPixels32);
        public double WebCamCpuPreparationMedianMilliseconds =>
            _webCamCpuAcquisitionTimingWindow.GetMedianMilliseconds(WebCamCpuAcquisitionTimingMetric.Preparation);
        public double WebCamCpuPreparationP95Milliseconds =>
            _webCamCpuAcquisitionTimingWindow.GetP95Milliseconds(WebCamCpuAcquisitionTimingMetric.Preparation);
        public double WebCamCpuTotalMedianMilliseconds =>
            _webCamCpuAcquisitionTimingWindow.GetMedianMilliseconds(WebCamCpuAcquisitionTimingMetric.Total);
        public double WebCamCpuTotalP95Milliseconds =>
            _webCamCpuAcquisitionTimingWindow.GetP95Milliseconds(WebCamCpuAcquisitionTimingMetric.Total);
        public float LastWebCamCpuGetPixelsMilliseconds { get; private set; }
        public float LastWebCamCpuPreparationMilliseconds { get; private set; }
        public float LastWebCamCpuTotalMilliseconds { get; private set; }
        public bool DirectBodyCpuReadbackEnabled => enableDirectBodyCpuReadback;
        public BodyReadbackPath ActiveBodyReadbackPath => _activeBodyReadbackPath;
""",
    "cpu acquisition properties",
)

provider = replace_once(
    provider,
    """            if (!_freshCameraFramePending)
            {
                _noFreshFrameWaitsInWindow++;
                return;
            }

            if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
""",
    """            if (!_freshCameraFramePending)
            {
                _noFreshFrameWaitsInWindow++;
                return;
            }

            if (bodyFrameAcquisitionMode == BodyFrameAcquisitionMode.WebCamCpuPixels &&
                TryPrepareLatestWebCamCpuFrame())
            {
                return;
            }

            _activeBodyFrameAcquisitionMode = BodyFrameAcquisitionMode.ExistingReadback;
            if (bodyFrameAcquisitionMode != BodyFrameAcquisitionMode.WebCamCpuPixels)
            {
                _webCamCpuAcquisitionFallbackReason = string.Empty;
            }

            if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
""",
    "cpu acquisition branch",
)

provider = replace_once(
    provider,
    """            StartCoroutine(CapturePreparedFrameAsync(
                textureFrame,
                frameObservedAtSeconds,
                coordinateConventionVersion,
                flipHorizontally,
                flipVertically));
        }

        private IEnumerator BootstrapAsync()
""",
    """            StartCoroutine(CapturePreparedFrameAsync(
                textureFrame,
                frameObservedAtSeconds,
                coordinateConventionVersion,
                flipHorizontally,
                flipVertically));
        }

        private bool TryPrepareLatestWebCamCpuFrame()
        {
            if (!CanUseWebCamCpuAcquisition(out var fallbackReason))
            {
                _activeBodyFrameAcquisitionMode = BodyFrameAcquisitionMode.ExistingReadback;
                _webCamCpuAcquisitionFallbackReason = fallbackReason;
                return false;
            }

            var sourceWidth = ActualCameraWidth;
            var sourceHeight = ActualCameraHeight;
            var targetWidth = _bodyInferenceWidth;
            var targetHeight = _bodyInferenceHeight;
            try
            {
                EnsureWebCamCpuBuffers(sourceWidth, sourceHeight, targetWidth, targetHeight);
            }
            catch (Exception exception)
            {
                MarkWebCamCpuAcquisitionUnavailable(
                    $"buffer setup failed: {exception.GetType().Name}");
                return false;
            }

            var frameObservedAtSeconds = _latestFreshFrameObservedAtSeconds;
            var coordinateConventionVersion = _coordinateConventionVersion;
            var flipHorizontally = _orientation.InferenceFlipHorizontally;
            var flipVertically = _orientation.InferenceFlipVertically;
            var totalStartedTicks = Stopwatch.GetTimestamp();
            long acquisitionFinishedTicks;
            long preparationFinishedTicks;
            try
            {
                _webCamTexture.GetPixels32(_webCamCpuPixels);
                acquisitionFinishedTicks = Stopwatch.GetTimestamp();
                WebCamCpuFramePreparation.PrepareRgba(
                    _webCamCpuPixels,
                    sourceWidth,
                    sourceHeight,
                    _webCamCpuPreparedRgba,
                    targetWidth,
                    targetHeight,
                    flipHorizontally,
                    flipVertically);
                preparationFinishedTicks = Stopwatch.GetTimestamp();
            }
            catch (Exception exception)
            {
                MarkWebCamCpuAcquisitionUnavailable(
                    $"GetPixels32/prepare failed: {exception.GetType().Name}");
                StatusMessage =
                    $"WebCam CPU acquisition unavailable; using ExistingReadback ({_webCamCpuAcquisitionFallbackReason})";
                return false;
            }

            var acquisitionMilliseconds = DirectReadbackTimingMath.TicksToMilliseconds(
                acquisitionFinishedTicks - totalStartedTicks,
                Stopwatch.Frequency);
            var preparationMilliseconds = DirectReadbackTimingMath.TicksToMilliseconds(
                preparationFinishedTicks - acquisitionFinishedTicks,
                Stopwatch.Frequency);
            var totalMilliseconds = DirectReadbackTimingMath.TicksToMilliseconds(
                preparationFinishedTicks - totalStartedTicks,
                Stopwatch.Frequency);
            LastWebCamCpuGetPixelsMilliseconds = (float)acquisitionMilliseconds;
            LastWebCamCpuPreparationMilliseconds = (float)preparationMilliseconds;
            LastWebCamCpuTotalMilliseconds = (float)totalMilliseconds;
            _webCamCpuAcquisitionTimingWindow.Add(
                new WebCamCpuAcquisitionTimingSample(
                    acquisitionMilliseconds,
                    preparationMilliseconds,
                    totalMilliseconds));

            _freshCameraFramePending = false;
            _activeBodyFrameAcquisitionMode = BodyFrameAcquisitionMode.WebCamCpuPixels;
            _webCamCpuAcquisitionFallbackReason = string.Empty;

            if (_shuttingDown ||
                coordinateConventionVersion != _coordinateConventionVersion ||
                !IsActiveBackendReady())
            {
                return true;
            }

            try
            {
                PublishWebCamCpuPreparedOpenVinoFrame(
                    frameObservedAtSeconds,
                    NowSeconds(),
                    Time.frameCount,
                    coordinateConventionVersion);
            }
            catch (Exception exception)
            {
                MarkWebCamCpuAcquisitionUnavailable(
                    $"mailbox publication failed: {exception.GetType().Name}");
                StatusMessage =
                    $"WebCam CPU acquisition unavailable; using ExistingReadback ({_webCamCpuAcquisitionFallbackReason})";
            }
            return true;
        }

        private bool CanUseWebCamCpuAcquisition(out string fallbackReason)
        {
            fallbackReason = string.Empty;
            if (bodyFrameAcquisitionMode != BodyFrameAcquisitionMode.WebCamCpuPixels)
            {
                return false;
            }
            if (!_webCamCpuAcquisitionSessionAvailable)
            {
                fallbackReason = string.IsNullOrEmpty(_webCamCpuAcquisitionFallbackReason)
                    ? "session unavailable"
                    : _webCamCpuAcquisitionFallbackReason;
                return false;
            }
            if (_activeInferenceBackend != PoseInferenceBackend.OpenVinoCpuFp32)
            {
                fallbackReason = "requires OpenVINO CPU FP32 backend";
                return false;
            }
            if (Status != PoseProviderStatus.Ready ||
                _shuttingDown ||
                _cameraSwitchPending ||
                _webCamTexture == null ||
                !_webCamTexture.isPlaying ||
                _openVinoPoseRuntime == null ||
                _openVinoWorkerTask == null ||
                _openVinoWorkerSignal == null)
            {
                fallbackReason = "provider/OpenVINO worker not ready";
                return false;
            }
            if (!BodyInferenceResourcesMatchCurrentIntent() ||
                _bodyInferenceWidth <= 0 ||
                _bodyInferenceHeight <= 0 ||
                ActualCameraWidth <= 16 ||
                ActualCameraHeight <= 16)
            {
                fallbackReason = "camera/body resources are not current";
                return false;
            }
            return true;
        }

        private void EnsureWebCamCpuBuffers(
            int sourceWidth,
            int sourceHeight,
            int targetWidth,
            int targetHeight)
        {
            var sourcePixelCount = checked(sourceWidth * sourceHeight);
            if (_webCamCpuPixels == null || _webCamCpuPixels.Length != sourcePixelCount)
            {
                _webCamCpuPixels = new Color32[sourcePixelCount];
            }

            var targetByteCount = checked(checked(targetWidth * targetHeight) * 4);
            if (!_webCamCpuPreparedRgba.IsCreated ||
                _webCamCpuPreparedRgba.Length != targetByteCount)
            {
                if (_webCamCpuPreparedRgba.IsCreated)
                {
                    _webCamCpuPreparedRgba.Dispose();
                }
                _webCamCpuPreparedRgba = new NativeArray<byte>(
                    targetByteCount,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
            }
        }

        private void ReleaseWebCamCpuBuffers()
        {
            _webCamCpuPixels = null;
            if (_webCamCpuPreparedRgba.IsCreated)
            {
                _webCamCpuPreparedRgba.Dispose();
                _webCamCpuPreparedRgba = default;
            }
        }

        private void MarkWebCamCpuAcquisitionUnavailable(string reason)
        {
            _webCamCpuAcquisitionSessionAvailable = false;
            _activeBodyFrameAcquisitionMode = BodyFrameAcquisitionMode.ExistingReadback;
            _webCamCpuAcquisitionFallbackReason = reason;
        }

        private bool PublishWebCamCpuPreparedOpenVinoFrame(
            double frameObservedAtSeconds,
            double framePublishedAtSeconds,
            int framePublishedFrameCount,
            int coordinateConventionVersion)
        {
            if (_openVinoPoseRuntime == null ||
                _openVinoWorkerTask == null ||
                _openVinoWorkerSignal == null)
            {
                throw new InvalidOperationException("OpenVINO pose worker is not ready.");
            }
            if (!_webCamCpuPreparedRgba.IsCreated)
            {
                throw new InvalidOperationException("WebCam CPU RGBA buffer is not allocated.");
            }

            var width = _bodyInferenceWidth;
            var height = _bodyInferenceHeight;
            var strideBytes = checked(width * 4);
            var expectedByteCount = checked(strideBytes * height);
            if (_webCamCpuPreparedRgba.Length != expectedByteCount)
            {
                throw new InvalidOperationException(
                    $"WebCam CPU RGBA input size mismatch. expected={expectedByteCount} actual={_webCamCpuPreparedRgba.Length}");
            }

            var timestampMillisec = OpenVinoSchedulingPolicy.NextMonotonicTimestamp(
                _clock.ElapsedMilliseconds,
                _openVinoLastPublishedTimestampMillisec);
            _openVinoLastPublishedTimestampMillisec = timestampMillisec;

            var copyStartedAtSeconds = NowSeconds();
            var accepted = _openVinoMailbox.Publish(
                _webCamCpuPreparedRgba,
                width,
                height,
                strideBytes,
                _orientation.InferenceRotationDegrees,
                timestampMillisec,
                frameObservedAtSeconds,
                framePublishedAtSeconds,
                framePublishedFrameCount,
                coordinateConventionVersion,
                PreparedInferenceLaunchOrigin.ReadbackContinuation,
                out var replacedPending);
            var managedCopyMilliseconds =
                (float)((NowSeconds() - copyStartedAtSeconds) * 1000d);
            LastCpuImageBuildDurationMilliseconds = managedCopyMilliseconds;
            LastOpenVinoManagedInputCopyMilliseconds = managedCopyMilliseconds;

            if (!accepted)
            {
                return false;
            }
            if (replacedPending)
            {
                Interlocked.Increment(ref _preparedFrameReplacementsInWindow);
            }
            Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 1);
            _openVinoWorkerSignal.Set();
            return true;
        }

        private IEnumerator BootstrapAsync()
""",
    "cpu acquisition methods",
)

provider = replace_once(
    provider,
    """        private void RebuildBodyInferenceResources()
        {
            ReleaseBodyInferenceRenderTexture();
""",
    """        private void RebuildBodyInferenceResources()
        {
            ReleaseWebCamCpuBuffers();
            ReleaseBodyInferenceRenderTexture();
""",
    "cpu buffer resource rebuild cleanup",
)

provider = replace_once(
    provider,
    """            _activeDirectReadbackStage = DirectReadbackStage.None;
            _directBodyCpuReadbackSessionAvailable = true;
            _directBodyCpuReadbackFallbackReason = string.Empty;
            lock (_openVinoWorkerFailureGate)
""",
    """            _activeDirectReadbackStage = DirectReadbackStage.None;
            _directBodyCpuReadbackSessionAvailable = true;
            _directBodyCpuReadbackFallbackReason = string.Empty;
            _activeBodyFrameAcquisitionMode = BodyFrameAcquisitionMode.ExistingReadback;
            _webCamCpuAcquisitionSessionAvailable = true;
            _webCamCpuAcquisitionFallbackReason = string.Empty;
            _webCamCpuAcquisitionTimingWindow.Reset();
            LastWebCamCpuGetPixelsMilliseconds = 0f;
            LastWebCamCpuPreparationMilliseconds = 0f;
            LastWebCamCpuTotalMilliseconds = 0f;
            lock (_openVinoWorkerFailureGate)
""",
    "cpu acquisition session reset",
)

provider = replace_once(
    provider,
    """            ReleasePreparedFrame();
            CleanupOpenVinoRuntime();

            if (_poseLandmarker != null)
""",
    """            ReleasePreparedFrame();
            CleanupOpenVinoRuntime();
            ReleaseWebCamCpuBuffers();

            if (_poseLandmarker != null)
""",
    "cpu acquisition teardown",
)

provider = replace_once(
    provider,
    """                var readbackPathSummary =
                    _activeBodyReadbackPath == BodyReadbackPath.DirectCPU
                        ? $"DirectCPU stage={ActiveDirectReadbackStageLabel}"
                        : ActiveBodyReadbackPathLabel;
                var directTimingSummary = _activeBodyReadbackPath == BodyReadbackPath.DirectCPU
""",
    """                var readbackPathSummary =
                    _activeBodyReadbackPath == BodyReadbackPath.DirectCPU
                        ? $"DirectCPU stage={ActiveDirectReadbackStageLabel}"
                        : ActiveBodyReadbackPathLabel;
                var acquisitionFallbackSuffix =
                    bodyFrameAcquisitionMode == BodyFrameAcquisitionMode.WebCamCpuPixels &&
                    _activeBodyFrameAcquisitionMode != BodyFrameAcquisitionMode.WebCamCpuPixels &&
                    !string.IsNullOrEmpty(_webCamCpuAcquisitionFallbackReason)
                        ? $" fallback={_webCamCpuAcquisitionFallbackReason}"
                        : string.Empty;
                var webCamCpuTimingSummary =
                    bodyFrameAcquisitionMode == BodyFrameAcquisitionMode.WebCamCpuPixels
                        ? $"\nCPU cam: n={WebCamCpuAcquisitionTimingSampleCount} " +
                          $"GetPixels32={FormatTimingPair(WebCamCpuGetPixelsMedianMilliseconds, WebCamCpuGetPixelsP95Milliseconds)}ms " +
                          $"prep={FormatTimingPair(WebCamCpuPreparationMedianMilliseconds, WebCamCpuPreparationP95Milliseconds)}ms " +
                          $"total={FormatTimingPair(WebCamCpuTotalMedianMilliseconds, WebCamCpuTotalP95Milliseconds)}ms"
                        : string.Empty;
                var directTimingSummary = _activeBodyReadbackPath == BodyReadbackPath.DirectCPU
""",
    "acquisition telemetry variables",
)

editor = replace_once(
    editor,
    """        private SerializedProperty _bodyInferenceLongEdge;
        private SerializedProperty _enableImmediateInferenceLaunchAfterReadback;
        private SerializedProperty _enableDirectBodyCpuReadback;
""",
    """        private SerializedProperty _bodyInferenceLongEdge;
        private SerializedProperty _enableImmediateInferenceLaunchAfterReadback;
        private SerializedProperty _bodyFrameAcquisitionMode;
        private SerializedProperty _enableDirectBodyCpuReadback;
""",
    "editor acquisition property field",
)

editor = replace_once(
    editor,
    """            _enableImmediateInferenceLaunchAfterReadback =
                serializedObject.FindProperty("enableImmediateInferenceLaunchAfterReadback");
            _enableDirectBodyCpuReadback =
                serializedObject.FindProperty("enableDirectBodyCpuReadback");
""",
    """            _enableImmediateInferenceLaunchAfterReadback =
                serializedObject.FindProperty("enableImmediateInferenceLaunchAfterReadback");
            _bodyFrameAcquisitionMode =
                serializedObject.FindProperty("bodyFrameAcquisitionMode");
            _enableDirectBodyCpuReadback =
                serializedObject.FindProperty("enableDirectBodyCpuReadback");
""",
    "editor acquisition property binding",
)

editor = replace_once(
    editor,
    """            EditorGUILayout.PropertyField(
                _enableDirectBodyCpuReadback,
                new GUIContent(
                    "Direct Body CPU Readback",
                    "Experimental body-pose-only path. Eligible H/V-flipped inputs use one persistent Golden Needle staging RenderTexture with the same scale/offset convention as Homuler, then read GPU data directly into the pooled TextureFrame CPU buffer without LoadRawTextureData/Apply. CPU pose inference, CameraTexture and Lab display semantics remain unchanged."));
""",
    """            EditorGUILayout.PropertyField(
                _bodyFrameAcquisitionMode,
                new GUIContent(
                    "Frame Acquisition",
                    "ExistingReadback preserves the current RenderTexture/DirectCPU/Homuler A/B baseline. WebCamCpuPixels is the authorized OpenVINO-only GetPixels32 experiment and falls back explicitly to ExistingReadback if unavailable."));
            EditorGUILayout.PropertyField(
                _enableDirectBodyCpuReadback,
                new GUIContent(
                    "Direct Body CPU Readback",
                    "ExistingReadback-only optimization. Eligible H/V-flipped inputs use one persistent Golden Needle staging RenderTexture with the same scale/offset convention as Homuler, then read GPU data directly into the pooled TextureFrame CPU buffer. It remains the R2 fallback/A-B baseline."));
""",
    "editor acquisition selector",
)

editor = replace_once(
    editor,
    """                EditorGUILayout.LabelField(
                    "Immediate Launch",
                    provider.ImmediateInferenceLaunchAfterReadbackEnabled ? "On" : "Off");
                EditorGUILayout.LabelField(
                    "Readback Path",
                    provider.ActiveBodyReadbackPath == BodyReadbackPath.DirectCPU
                        ? $"DirectCPU stage={provider.ActiveDirectReadbackStageLabel}"
                        : provider.ActiveBodyReadbackPathLabel);
""",
    """                EditorGUILayout.LabelField(
                    "Immediate Launch",
                    provider.ImmediateInferenceLaunchAfterReadbackEnabled ? "On" : "Off");
                EditorGUILayout.LabelField(
                    "Acquisition",
                    $"{provider.ActiveBodyFrameAcquisitionModeLabel} (requested {provider.RequestedBodyFrameAcquisitionModeLabel})");
                if (provider.RequestedBodyFrameAcquisitionMode == BodyFrameAcquisitionMode.WebCamCpuPixels)
                {
                    EditorGUILayout.LabelField(
                        "CPU Get/Prep/Total",
                        $"{provider.LastWebCamCpuGetPixelsMilliseconds:0.0}/{provider.LastWebCamCpuPreparationMilliseconds:0.0}/{provider.LastWebCamCpuTotalMilliseconds:0.0} ms");
                }
                if (!string.IsNullOrEmpty(provider.WebCamCpuAcquisitionFallbackReason))
                {
                    EditorGUILayout.LabelField(
                        "Acquisition Fallback",
                        provider.WebCamCpuAcquisitionFallbackReason);
                }
                EditorGUILayout.LabelField(
                    "Readback Path",
                    provider.ActiveBodyFrameAcquisitionMode == BodyFrameAcquisitionMode.WebCamCpuPixels
                        ? "Bypassed by WebCam CPU"
                        : provider.ActiveBodyReadbackPath == BodyReadbackPath.DirectCPU
                            ? $"DirectCPU stage={provider.ActiveDirectReadbackStageLabel}"
                            : provider.ActiveBodyReadbackPathLabel);
""",
    "editor runtime acquisition telemetry",
)

PROVIDER.write_text(provider, encoding="utf-8", newline="\n")
EDITOR.write_text(editor, encoding="utf-8", newline="\n")
print("OPENVINO_READBACK_R2_TRANSFORM=PASS")
