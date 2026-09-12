using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Experimental;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    public enum PoseProviderStatus
    {
        Starting,
        WaitingForCamera,
        StartingCamera,
        PreparingModel,
        Ready,
        NoCamera,
        CameraPermissionDenied,
        CameraFailed,
        ModelFailed,
        InferenceFailed,
        Stopped,
    }

    public enum PreparedInferenceLaunchOrigin
    {
        None,
        Update,
        ReadbackContinuation,
    }

    /// <summary>
    /// Main-thread request cadence helper. Busy frames never advance timing state, so once the
    /// previous inference finishes, the next prepared frame may launch immediately if the minimum
    /// interval since the last accepted request has already elapsed. There is no queue/backlog.
    /// </summary>
    public sealed class InferenceLaunchScheduler
    {
        private bool _hasAcceptedRequest;
        private double _lastAcceptedRequestAtSeconds;

        public bool HasAcceptedRequest => _hasAcceptedRequest;
        public double LastAcceptedRequestAtSeconds => _lastAcceptedRequestAtSeconds;

        public void Reset()
        {
            _hasAcceptedRequest = false;
            _lastAcceptedRequestAtSeconds = 0d;
        }

        public bool IsIntervalElapsed(double nowSeconds, double minimumIntervalSeconds)
        {
            if (!_hasAcceptedRequest)
            {
                return true;
            }

            return nowSeconds - _lastAcceptedRequestAtSeconds >=
                Math.Max(0d, minimumIntervalSeconds);
        }

        public bool CanLaunch(
            double nowSeconds,
            double minimumIntervalSeconds,
            bool busy,
            bool preparedFrameAvailable = true)
        {
            return preparedFrameAvailable &&
                !busy &&
                IsIntervalElapsed(nowSeconds, minimumIntervalSeconds);
        }

        public void MarkAccepted(double nowSeconds)
        {
            _hasAcceptedRequest = true;
            _lastAcceptedRequestAtSeconds = nowSeconds;
        }
    }

    /// <summary>
    /// Pure policy surface for deterministic tests of the bounded latest-frame pipeline.
    /// Readback may overlap one active inference, but neither stage can have more than one item.
    /// </summary>
    public static class LatestFramePipelinePolicy
    {
        public static bool CanStartReadback(bool readbackPending, bool freshFramePending)
        {
            return !readbackPending && freshFramePending;
        }

        public static bool CanLaunchInference(
            bool preparedFrameAvailable,
            bool inferenceOutstanding,
            bool intervalElapsed)
        {
            return preparedFrameAvailable && !inferenceOutstanding && intervalElapsed;
        }

        public static bool CanImmediateLaunchAfterReadback(
            bool experimentEnabled,
            bool providerReady,
            bool shuttingDown,
            bool cameraSwitchPending,
            bool poseLandmarkerAvailable,
            bool preparedFrameAvailable,
            bool coordinateConventionCurrent,
            bool bodyResourcesCurrent,
            bool inferenceOutstanding,
            bool intervalElapsed)
        {
            return experimentEnabled &&
                providerReady &&
                !shuttingDown &&
                !cameraSwitchPending &&
                poseLandmarkerAvailable &&
                preparedFrameAvailable &&
                coordinateConventionCurrent &&
                bodyResourcesCurrent &&
                !inferenceOutstanding &&
                intervalElapsed;
        }
    }

    /// <summary>
    /// Selects the texture dimensions used only by the body-pose inference path.
    /// The source webcam dimensions remain authoritative and are never changed here.
    /// </summary>
    public static class BodyInferenceResolution
    {
        public const int MinimumLongEdge = 2;
        public const int MaximumLongEdge = 4096;

        public static int ClampLongEdge(int requestedLongEdge)
        {
            var bounded = Math.Min(MaximumLongEdge, Math.Max(MinimumLongEdge, requestedLongEdge));
            if (bounded > MinimumLongEdge && (bounded & 1) != 0)
            {
                bounded--;
            }
            return bounded;
        }

        public static Vector2Int Calculate(
            int sourceWidth,
            int sourceHeight,
            bool downscaleEnabled,
            int requestedLongEdge)
        {
            var safeWidth = Math.Max(1, sourceWidth);
            var safeHeight = Math.Max(1, sourceHeight);
            if (!downscaleEnabled)
            {
                return new Vector2Int(safeWidth, safeHeight);
            }

            var sourceLongEdge = Math.Max(safeWidth, safeHeight);
            var targetLongEdge = ClampLongEdge(requestedLongEdge);
            if (targetLongEdge >= sourceLongEdge)
            {
                return new Vector2Int(safeWidth, safeHeight);
            }

            if (safeWidth >= safeHeight)
            {
                return new Vector2Int(
                    targetLongEdge,
                    RoundToSafeDimension(safeHeight * (double)targetLongEdge / safeWidth));
            }

            return new Vector2Int(
                RoundToSafeDimension(safeWidth * (double)targetLongEdge / safeHeight),
                targetLongEdge);
        }

        private static int RoundToSafeDimension(double value)
        {
            var rounded = Math.Max(1, (int)Math.Round(value, MidpointRounding.AwayFromZero));
            if (rounded > 1 && (rounded & 1) != 0)
            {
                rounded--;
            }
            return Math.Max(1, rounded);
        }
    }

    public sealed class MediaPipePoseProvider : MonoBehaviour
    {
        private const string ModelFileName = "pose_landmarker_lite.bytes";
        private const string ModelDirectory = "GoldenNeedle/PoseTrackingSpike/Models";

        [Header("Camera")]
        [SerializeField] private string preferredCameraName = string.Empty;
        [SerializeField] private int requestedCameraWidth = 640;
        [SerializeField] private int requestedCameraHeight = 480;
        [SerializeField] private int requestedCameraFps = 30;
        [SerializeField] private float cameraStartupTimeoutSeconds = 8f;
        [Tooltip("Use Auto for WebCamTexture metadata, or override quarter-turn rotation for USB/virtual camera drivers with incorrect orientation metadata.")]
        [SerializeField] private CameraRotationOverride manualRotationOverride = CameraRotationOverride.Auto;
        [Tooltip("Optional selfie-style display mirror. Canonical left/right semantics are not changed.")]
        [SerializeField] private bool mirrorFrontFacingDisplay;

        [Header("Pose Landmarker")]
        [SerializeField] private float targetInferenceFps = 30f;
        [SerializeField] private float readbackTimeoutSeconds = 2f;
        [SerializeField] private PoseTrustSettings trustSettings = new PoseTrustSettings();

        [Header("Body Pose Inference")]
        [Tooltip("Scale only the body-pose inference input. CameraTexture remains the original full-resolution WebCamTexture.")]
        [SerializeField] private bool enableBodyInferenceDownscale = true;
        [Tooltip("Target long edge for body-pose inference. The source aspect ratio is preserved and the value is safely bounded.")]
        [SerializeField] private int bodyInferenceLongEdge = 320;
        [Tooltip("Attempt body-pose inference immediately when a readback finishes. If unsafe or ineligible, the frame remains prepared for the normal Update path; no extra queue or concurrent inference is created.")]
        [SerializeField] private bool enableImmediateInferenceLaunchAfterReadback = true;

        private readonly object _observationGate = new object();
        private readonly double[] _lastTrackedAtSeconds = new double[PoseObservation.LandmarkCount];
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly InferenceLaunchScheduler _inferenceScheduler = new InferenceLaunchScheduler();

        private PoseObservation _pendingObservation;
        private PoseObservation _latestObservation;
        private WebCamTexture _webCamTexture;
        private WebCamDevice _selectedDevice;
        private TextureFramePool _textureFramePool;
        private RenderTexture _bodyInferenceRenderTexture;
        private int _bodyInferenceWidth;
        private int _bodyInferenceHeight;
        private int _configuredCameraWidth;
        private int _configuredCameraHeight;
        private bool _configuredBodyInferenceDownscale;
        private int _configuredBodyInferenceLongEdge;
        private PoseLandmarker _poseLandmarker;
        private Coroutine _bootstrapCoroutine;
        private bool _readbackPending;
        private bool _freshCameraFramePending;
        private double _latestFreshFrameObservedAtSeconds;
        private TextureFrame _preparedTextureFrame;
        private double _preparedFrameObservedAtSeconds;
        private int _preparedCoordinateConventionVersion;
        private double _preparedFramePublishedAtSeconds;
        private int _preparedFramePublishedFrameCount = -1;
        private bool _shuttingDown;
        private bool _cameraSwitchPending;
        private string _pendingCameraName = string.Empty;
        private string _pendingPreferredCameraName = string.Empty;
        private double _inferenceStartedAtSeconds;
        private double _activeInferenceFrameObservedAtSeconds;
        private double _metricsWindowStartedAtSeconds;
        private int _inferenceOutstanding;
        private int _requestsInWindow;
        private int _resultsInWindow;
        private int _callbacksInWindow;
        private int _skippedInWindow;
        private int _cameraFramesInWindow;
        private int _noFreshFrameWaitsInWindow;
        private int _targetIntervalWaitsInWindow;
        private int _readbackBusyWaitsInWindow;
        private int _inferenceBusyWaitsInWindow;
        private int _textureFramePoolWaitsInWindow;
        private int _readbackFailuresInWindow;
        private int _readbackTimeoutsInWindow;
        private int _preparedFrameReplacementsInWindow;
        private int _immediateLaunchesInWindow;
        private int _resultCallbackReceived;
        private int _resultCallbackLogPublished;
        private int _totalInferenceRequests;
        private int _totalResultCallbacks;
        private bool _inferenceRequestLogPublished;
        private ImageProcessingOptions _imageProcessingOptions;
        private CameraOrientationState _orientation;
        private bool _hasPublishedCoordinateConvention;
        private int _coordinateConventionVersion;
        private PreparedInferenceLaunchOrigin _lastAcceptedLaunchOrigin;

        public PoseProviderStatus Status { get; private set; } = PoseProviderStatus.Starting;
        public string StatusMessage { get; private set; } = "Starting pose-tracking spike";
        public string SelectedCameraName => _selectedDevice.name;
        public string PreferredCameraName => preferredCameraName;
        public int CameraDeviceCount { get; private set; }
        public int ActualCameraWidth => _webCamTexture == null ? 0 : _webCamTexture.width;
        public int ActualCameraHeight => _webCamTexture == null ? 0 : _webCamTexture.height;
        public int RequestedCameraFps => requestedCameraFps;
        public int VideoRotationAngle => _webCamTexture == null ? 0 : _webCamTexture.videoRotationAngle;
        public int EffectiveRotationDegrees => _orientation.SensorRotationDegrees;
        public CameraRotationOverride ManualRotationOverride => manualRotationOverride;
        public float TargetInferenceFps => targetInferenceFps;
        public bool CameraSwitchPending => _cameraSwitchPending;
        public string PendingCameraName => _pendingCameraName;
        public bool VideoVerticallyMirrored => _webCamTexture != null && _webCamTexture.videoVerticallyMirrored;
        public CameraOrientationState Orientation => _orientation;
        public int CoordinateConventionVersion => _coordinateConventionVersion;
        public bool FlipInputHorizontally => _orientation.InferenceFlipHorizontally;
        public bool FlipInputVertically => _orientation.InferenceFlipVertically;
        public int InputRotationDegrees => _orientation.InferenceRotationDegrees;
        public bool DisplayMirror => _orientation.DisplayMirrored;
        public Texture CameraTexture => _webCamTexture;
        public int BodyInferenceWidth => _bodyInferenceWidth;
        public int BodyInferenceHeight => _bodyInferenceHeight;
        public bool BodyInferenceDownscaleEnabled => enableBodyInferenceDownscale;
        public bool BodyInferenceUsesScaledTexture =>
            _bodyInferenceRenderTexture != null &&
            (_bodyInferenceWidth != ActualCameraWidth || _bodyInferenceHeight != ActualCameraHeight);
        public bool ImmediateInferenceLaunchAfterReadbackEnabled => enableImmediateInferenceLaunchAfterReadback;
        public float CameraFramesPerSecond { get; private set; }
        public float InferenceRequestsPerSecond { get; private set; }
        public float ResultCallbacksPerSecond { get; private set; }
        public float PoseResultsPerSecond { get; private set; }
        public float NoFreshFrameWaitsPerSecond { get; private set; }
        public float TargetIntervalWaitsPerSecond { get; private set; }
        public float ReadbackBusyWaitsPerSecond { get; private set; }
        public float InferenceBusyWaitsPerSecond { get; private set; }
        public float TextureFramePoolWaitsPerSecond { get; private set; }
        public float ReadbackFailuresPerSecond { get; private set; }
        public float ReadbackTimeoutsPerSecond { get; private set; }
        public float PreparedFrameReplacementsPerSecond { get; private set; }
        public float ImmediateLaunchesPerSecond { get; private set; }
        public bool HasPreparedFrame => _preparedTextureFrame != null;
        public bool ReadbackPending => _readbackPending;
        public bool InferencePending => Volatile.Read(ref _inferenceOutstanding) != 0;
        public bool HasReceivedResult => Volatile.Read(ref _resultCallbackReceived) != 0;
        public int TotalInferenceRequests => Volatile.Read(ref _totalInferenceRequests);
        public int TotalResultCallbacks => Volatile.Read(ref _totalResultCallbacks);
        public int SkippedInferenceOpportunities { get; private set; }
        public float LastGpuReadbackDurationMilliseconds { get; private set; }
        public float LastCpuImageBuildDurationMilliseconds { get; private set; }
        public float LastInferenceDurationMilliseconds { get; private set; }
        public float LastApproxFrameToResultMilliseconds { get; private set; }
        public float LastPreparedToInferenceLaunchMilliseconds { get; private set; }
        public int LastPreparedToInferenceLaunchFrameDelta { get; private set; }
        public PreparedInferenceLaunchOrigin LastAcceptedLaunchOrigin => _lastAcceptedLaunchOrigin;
        public string LastAcceptedLaunchOriginLabel =>
            _lastAcceptedLaunchOrigin == PreparedInferenceLaunchOrigin.ReadbackContinuation
                ? "RB"
                : _lastAcceptedLaunchOrigin == PreparedInferenceLaunchOrigin.Update
                    ? "Update"
                    : "-";
        public double LatestPoseAgeMilliseconds
        {
            get
            {
                lock (_observationGate)
                {
                    return _latestObservation.receivedAtSeconds <= 0
                        ? double.PositiveInfinity
                        : Math.Max(0d, (NowSeconds() - _latestObservation.receivedAtSeconds) * 1000d);
                }
            }
        }

        private void Awake()
        {
            _pendingObservation = new PoseObservation();
            _latestObservation = new PoseObservation();
            _metricsWindowStartedAtSeconds = NowSeconds();
        }

        private void Start()
        {
            _bootstrapCoroutine = StartCoroutine(BootstrapAsync());
        }

        private void Update()
        {
            if (_bootstrapCoroutine != null && (Status == PoseProviderStatus.Ready || IsFailureStatus(Status)))
            {
                _bootstrapCoroutine = null;
            }

            UpdateMetrics();

            if (HasReceivedResult && Interlocked.Exchange(ref _resultCallbackLogPublished, 1) == 0)
            {
                UnityEngine.Debug.Log("[PoseTrackingSpike] Live result callback active");
            }

            if (_cameraSwitchPending)
            {
                if (CameraDeviceSelection.CanProcessPendingSwitch(
                        true,
                        _bootstrapCoroutine != null,
                        _readbackPending,
                        Volatile.Read(ref _inferenceOutstanding)))
                {
                    ExecutePendingCameraSwitch();
                }
                return;
            }

            if (Status != PoseProviderStatus.Ready || _webCamTexture == null || _poseLandmarker == null || _textureFramePool == null)
            {
                return;
            }

            if (!EnsureBodyInferenceResources())
            {
                return;
            }

            var now = NowSeconds();
            if (_webCamTexture.didUpdateThisFrame)
            {
                _cameraFramesInWindow++;
                _freshCameraFramePending = true;
                _latestFreshFrameObservedAtSeconds = now;
            }

            UpdateInputTransform();

            var interval = 1d / Mathf.Max(1f, targetInferenceFps);
            TryLaunchPreparedInference(
                now,
                interval,
                PreparedInferenceLaunchOrigin.Update,
                countWaits: true);
            TryStartLatestReadback();
        }

        private void TryLaunchPreparedInference(
            double now,
            double interval,
            PreparedInferenceLaunchOrigin origin,
            bool countWaits)
        {
            if (_preparedTextureFrame == null)
            {
                return;
            }

            if (_preparedCoordinateConventionVersion != _coordinateConventionVersion)
            {
                ReleasePreparedFrame();
                return;
            }

            if (Volatile.Read(ref _inferenceOutstanding) != 0)
            {
                if (countWaits)
                {
                    _inferenceBusyWaitsInWindow++;
                    _skippedInWindow++;
                }
                return;
            }

            if (!_inferenceScheduler.IsIntervalElapsed(now, interval))
            {
                if (countWaits)
                {
                    _targetIntervalWaitsInWindow++;
                    _skippedInWindow++;
                }
                return;
            }

            LaunchPreparedInference(origin);
        }

        private void LaunchPreparedInference(PreparedInferenceLaunchOrigin origin)
        {
            var textureFrame = _preparedTextureFrame;
            var frameObservedAtSeconds = _preparedFrameObservedAtSeconds;
            var framePublishedAtSeconds = _preparedFramePublishedAtSeconds;
            var framePublishedFrameCount = _preparedFramePublishedFrameCount;
            _preparedTextureFrame = null;
            _preparedFrameObservedAtSeconds = 0d;
            _preparedCoordinateConventionVersion = 0;
            _preparedFramePublishedAtSeconds = 0d;
            _preparedFramePublishedFrameCount = -1;

            var frameReleased = false;
            try
            {
                var buildStartedAtSeconds = NowSeconds();
                using (var image = textureFrame.BuildCPUImage())
                {
                    LastCpuImageBuildDurationMilliseconds = (float)((NowSeconds() - buildStartedAtSeconds) * 1000d);
                    textureFrame.Release();
                    frameReleased = true;

                    var timestampMillisec = _clock.ElapsedMilliseconds;
                    Interlocked.Exchange(ref _inferenceOutstanding, 1);
                    _inferenceStartedAtSeconds = NowSeconds();
                    _activeInferenceFrameObservedAtSeconds = frameObservedAtSeconds;

                    try
                    {
                        _poseLandmarker.DetectAsync(image, timestampMillisec, _imageProcessingOptions);
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
                            UnityEngine.Debug.Log("[PoseTrackingSpike] Live inference request accepted");
                        }
                    }
                    catch (Exception exception)
                    {
                        Interlocked.Exchange(ref _inferenceOutstanding, 0);
                        SetFailure(PoseProviderStatus.InferenceFailed, $"Pose inference request failed: {exception.Message}");
                        UnityEngine.Debug.LogException(exception, this);
                    }
                }
            }
            catch (Exception exception)
            {
                if (!frameReleased)
                {
                    textureFrame.Release();
                }
                SetFailure(PoseProviderStatus.InferenceFailed, $"Camera frame preparation failed: {exception.Message}");
                UnityEngine.Debug.LogException(exception, this);
            }
        }

        private void TryStartLatestReadback()
        {
            if (_readbackPending)
            {
                if (_freshCameraFramePending)
                {
                    _readbackBusyWaitsInWindow++;
                }
                return;
            }

            if (!_freshCameraFramePending)
            {
                _noFreshFrameWaitsInWindow++;
                return;
            }

            if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
            {
                _textureFramePoolWaitsInWindow++;
                _skippedInWindow++;
                return;
            }

            var frameObservedAtSeconds = _latestFreshFrameObservedAtSeconds;
            var coordinateConventionVersion = _coordinateConventionVersion;
            var flipHorizontally = _orientation.InferenceFlipHorizontally;
            var flipVertically = _orientation.InferenceFlipVertically;

            _freshCameraFramePending = false;
            _readbackPending = true;
            StartCoroutine(CapturePreparedFrameAsync(
                textureFrame,
                frameObservedAtSeconds,
                coordinateConventionVersion,
                flipHorizontally,
                flipVertically));
        }

        private IEnumerator BootstrapAsync()
        {
            _shuttingDown = false;
            Status = PoseProviderStatus.WaitingForCamera;
            StatusMessage = "Checking webcam permission";

#if UNITY_2018_1_OR_NEWER
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
                if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                {
                    SetFailure(PoseProviderStatus.CameraPermissionDenied, "Webcam permission was not granted");
                    yield break;
                }
            }
#endif

            yield return StartCameraAsync();
            if (Status != PoseProviderStatus.StartingCamera && Status != PoseProviderStatus.WaitingForCamera)
            {
                yield break;
            }

            Status = PoseProviderStatus.PreparingModel;
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
        }

        private IEnumerator StartCameraAsync()
        {
            Status = PoseProviderStatus.StartingCamera;
            StatusMessage = "Enumerating webcam devices";

            var devices = WebCamTexture.devices;
            CameraDeviceCount = devices.Length;
            if (devices.Length == 0)
            {
                SetFailure(PoseProviderStatus.NoCamera, "No webcam devices were found");
                yield break;
            }

            var selectedIndex = SelectCameraIndex(devices);
            _selectedDevice = devices[selectedIndex];
            for (var i = 0; i < devices.Length; i++)
            {
                UnityEngine.Debug.Log($"[PoseTrackingSpike] Webcam device {i + 1}/{devices.Length}: {devices[i].name} (frontFacing={devices[i].isFrontFacing})");
            }

            StatusMessage = $"Starting webcam: {_selectedDevice.name}";
            _webCamTexture = new WebCamTexture(_selectedDevice.name, requestedCameraWidth, requestedCameraHeight, requestedCameraFps);
            _webCamTexture.Play();

            var deadline = Time.realtimeSinceStartup + Mathf.Max(1f, cameraStartupTimeoutSeconds);
            while (Time.realtimeSinceStartup < deadline && (_webCamTexture.width <= 16 || _webCamTexture.height <= 16 || !_webCamTexture.didUpdateThisFrame))
            {
                yield return null;
            }

            if (_webCamTexture.width <= 16 || _webCamTexture.height <= 16 || !_webCamTexture.isPlaying)
            {
                SetFailure(PoseProviderStatus.CameraFailed, $"Selected webcam did not produce a valid frame: {_selectedDevice.name}");
                StopCamera();
                yield break;
            }

            UpdateInputTransform();
            UnityEngine.Debug.Log($"[PoseTrackingSpike] Webcam started: {_selectedDevice.name}, actualResolution={ActualCameraWidth}x{ActualCameraHeight}, requestedFps={requestedCameraFps}, rotation={VideoRotationAngle}, verticallyMirrored={VideoVerticallyMirrored}");
        }

        private bool BodyInferenceResourcesMatchCurrentIntent()
        {
            if (_webCamTexture == null || _textureFramePool == null)
            {
                return false;
            }

            var desiredDimensions = BodyInferenceResolution.Calculate(
                ActualCameraWidth,
                ActualCameraHeight,
                enableBodyInferenceDownscale,
                bodyInferenceLongEdge);
            return _configuredCameraWidth == ActualCameraWidth &&
                _configuredCameraHeight == ActualCameraHeight &&
                _configuredBodyInferenceDownscale == enableBodyInferenceDownscale &&
                _configuredBodyInferenceLongEdge == BodyInferenceResolution.ClampLongEdge(bodyInferenceLongEdge) &&
                _bodyInferenceWidth == desiredDimensions.x &&
                _bodyInferenceHeight == desiredDimensions.y;
        }

        private bool EnsureBodyInferenceResources()
        {
            if (BodyInferenceResourcesMatchCurrentIntent())
            {
                return true;
            }

            if (_readbackPending || Volatile.Read(ref _inferenceOutstanding) != 0)
            {
                return false;
            }

            if (_preparedTextureFrame != null)
            {
                ReleasePreparedFrame();
            }

            try
            {
                RebuildBodyInferenceResources();
                return _textureFramePool != null;
            }
            catch (Exception exception)
            {
                SetFailure(PoseProviderStatus.InferenceFailed, $"Body inference resource setup failed: {exception.Message}");
                UnityEngine.Debug.LogException(exception, this);
                return false;
            }
        }

        private void RebuildBodyInferenceResources()
        {
            ReleaseBodyInferenceRenderTexture();
            _textureFramePool?.Dispose();
            _textureFramePool = null;

            var dimensions = BodyInferenceResolution.Calculate(
                ActualCameraWidth,
                ActualCameraHeight,
                enableBodyInferenceDownscale,
                bodyInferenceLongEdge);
            _bodyInferenceWidth = dimensions.x;
            _bodyInferenceHeight = dimensions.y;

            if (_bodyInferenceWidth != ActualCameraWidth || _bodyInferenceHeight != ActualCameraHeight)
            {
                _bodyInferenceRenderTexture = new RenderTexture(
                    _bodyInferenceWidth,
                    _bodyInferenceHeight,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default)
                {
                    name = "GoldenNeedle Body Pose Inference",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false,
                    antiAliasing = 1
                };
                _bodyInferenceRenderTexture.Create();
            }

            _textureFramePool = new TextureFramePool(
                _bodyInferenceWidth,
                _bodyInferenceHeight,
                TextureFormat.RGBA32,
                2);
            _configuredCameraWidth = ActualCameraWidth;
            _configuredCameraHeight = ActualCameraHeight;
            _configuredBodyInferenceDownscale = enableBodyInferenceDownscale;
            _configuredBodyInferenceLongEdge = BodyInferenceResolution.ClampLongEdge(bodyInferenceLongEdge);
        }

        private IEnumerator CapturePreparedFrameAsync(
            TextureFrame textureFrame,
            double frameObservedAtSeconds,
            int coordinateConventionVersion,
            bool flipHorizontally,
            bool flipVertically)
        {
            AsyncGPUReadbackRequest request = default;
            var readbackStartedAtSeconds = NowSeconds();
            try
            {
                Texture readbackSource = _webCamTexture;
                if (_bodyInferenceRenderTexture != null)
                {
                    Graphics.Blit(_webCamTexture, _bodyInferenceRenderTexture);
                    readbackSource = _bodyInferenceRenderTexture;
                }

                request = textureFrame.ReadTextureAsync(readbackSource, flipHorizontally, flipVertically);
            }
            catch (Exception exception)
            {
                textureFrame.Release();
                _readbackPending = false;
                _readbackFailuresInWindow++;
                SetFailure(PoseProviderStatus.InferenceFailed, $"Camera readback setup failed: {exception.Message}");
                UnityEngine.Debug.LogException(exception, this);
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + Mathf.Max(0.25f, readbackTimeoutSeconds);
            while (!request.done && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            LastGpuReadbackDurationMilliseconds = (float)((NowSeconds() - readbackStartedAtSeconds) * 1000d);

            if (!request.done || request.hasError)
            {
                textureFrame.Release();
                _readbackPending = false;
                _skippedInWindow++;
                if (request.hasError)
                {
                    _readbackFailuresInWindow++;
                    StatusMessage = "Camera frame readback failed; retrying";
                }
                else
                {
                    _readbackTimeoutsInWindow++;
                    StatusMessage = "Camera frame readback timed out; retrying";
                }
                yield break;
            }

            if (_shuttingDown || coordinateConventionVersion != _coordinateConventionVersion || _poseLandmarker == null)
            {
                textureFrame.Release();
                _readbackPending = false;
                yield break;
            }

            if (_preparedTextureFrame != null)
            {
                _preparedTextureFrame.Release();
                _preparedFrameReplacementsInWindow++;
            }

            _preparedTextureFrame = textureFrame;
            _preparedFrameObservedAtSeconds = frameObservedAtSeconds;
            _preparedCoordinateConventionVersion = coordinateConventionVersion;
            _preparedFramePublishedAtSeconds = NowSeconds();
            _preparedFramePublishedFrameCount = Time.frameCount;
            _readbackPending = false;

            TryImmediateLaunchAfterReadback();
        }

        private void TryImmediateLaunchAfterReadback()
        {
            var now = NowSeconds();
            var interval = 1d / Mathf.Max(1f, targetInferenceFps);
            var canImmediateLaunch = LatestFramePipelinePolicy.CanImmediateLaunchAfterReadback(
                enableImmediateInferenceLaunchAfterReadback,
                Status == PoseProviderStatus.Ready,
                _shuttingDown,
                _cameraSwitchPending,
                _poseLandmarker != null,
                _preparedTextureFrame != null,
                _preparedCoordinateConventionVersion == _coordinateConventionVersion,
                BodyInferenceResourcesMatchCurrentIntent(),
                Volatile.Read(ref _inferenceOutstanding) != 0,
                _inferenceScheduler.IsIntervalElapsed(now, interval));
            if (!canImmediateLaunch)
            {
                return;
            }

            TryLaunchPreparedInference(
                now,
                interval,
                PreparedInferenceLaunchOrigin.ReadbackContinuation,
                countWaits: false);
        }

        private void OnPoseLandmarkerResult(PoseLandmarkerResult result, Image _, long timestampMillisec)
        {
            if (_shuttingDown)
            {
                return;
            }

            Interlocked.Exchange(ref _resultCallbackReceived, 1);
            Interlocked.Increment(ref _totalResultCallbacks);
            Interlocked.Increment(ref _callbacksInWindow);

            var receivedAtSeconds = NowSeconds();
            var hasPose = result.poseLandmarks != null && result.poseLandmarks.Count > 0 && result.poseLandmarks[0].landmarks != null;

            lock (_observationGate)
            {
                _pendingObservation.Begin(timestampMillisec, receivedAtSeconds, hasPose);

                if (hasPose)
                {
                    var normalized = result.poseLandmarks[0].landmarks;
                    var world = result.poseWorldLandmarks != null && result.poseWorldLandmarks.Count > 0
                        ? result.poseWorldLandmarks[0].landmarks
                        : null;

                    for (var i = 0; i < PoseObservation.LandmarkCount; i++)
                    {
                        var hasLandmark = i < normalized.Count;
                        var normalizedLandmark = hasLandmark ? normalized[i] : default;
                        var hasVisibility = hasLandmark && normalizedLandmark.visibility.HasValue;
                        var hasPresence = hasLandmark && normalizedLandmark.presence.HasValue;
                        var visibility = hasVisibility ? normalizedLandmark.visibility.Value : 0f;
                        var presence = hasPresence ? normalizedLandmark.presence.Value : 0f;
                        var candidate = hasLandmark && PoseTrustClassifier.IsCandidate(
                            normalizedLandmark.x,
                            normalizedLandmark.y,
                            normalizedLandmark.z,
                            visibility,
                            hasVisibility,
                            presence,
                            hasPresence,
                            trustSettings);
                        var tracked = hasPose && (candidate || WasRecentlyTracked(i, receivedAtSeconds));

                        var observation = new PoseLandmarkObservation
                        {
                            index = i,
                            x = normalizedLandmark.x,
                            y = normalizedLandmark.y,
                            z = normalizedLandmark.z,
                            visibility = visibility,
                            presence = presence,
                            hasVisibility = hasVisibility,
                            hasPresence = hasPresence,
                            trust = tracked ? LandmarkTrust.Tracked : LandmarkTrust.Unavailable,
                        };

                        if (world != null && i < world.Count)
                        {
                            observation.worldX = world[i].x;
                            observation.worldY = world[i].y;
                            observation.worldZ = world[i].z;
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

            LastInferenceDurationMilliseconds = (float)((receivedAtSeconds - _inferenceStartedAtSeconds) * 1000d);
            LastApproxFrameToResultMilliseconds = _activeInferenceFrameObservedAtSeconds > 0d
                ? (float)((receivedAtSeconds - _activeInferenceFrameObservedAtSeconds) * 1000d)
                : 0f;
            Interlocked.Exchange(ref _inferenceOutstanding, 0);
            if (hasPose)
            {
                Interlocked.Increment(ref _resultsInWindow);
            }
        }

        public void CopyLatestObservation(PoseObservation destination)
        {
            lock (_observationGate)
            {
                destination.CopyFrom(_latestObservation);
            }

            if (destination.hasPose && destination.receivedAtSeconds > 0 && NowSeconds() - destination.receivedAtSeconds > trustSettings.staleResultSeconds)
            {
                destination.MarkUnavailable();
            }
        }

        public void Retry()
        {
            if (_bootstrapCoroutine != null || _readbackPending || Volatile.Read(ref _inferenceOutstanding) != 0 || _cameraSwitchPending)
            {
                return;
            }
            RestartProvider(invalidateCameraSession: false);
        }

        public bool RequestCycleCamera()
        {
            var devices = WebCamTexture.devices;
            CameraDeviceCount = devices.Length;
            var names = GetDeviceNames(devices);
            var currentName = !string.IsNullOrWhiteSpace(SelectedCameraName) ? SelectedCameraName : preferredCameraName;
            var nextName = CameraDeviceSelection.GetNextUsableDeviceName(names, currentName);
            if (string.IsNullOrWhiteSpace(nextName) || string.Equals(nextName, currentName, StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "No alternate usable color/webcam device is available";
                return false;
            }
            return RequestCameraSwitch(nextName);
        }

        public bool RequestCameraSwitch(string preferredName)
        {
            var devices = WebCamTexture.devices;
            CameraDeviceCount = devices.Length;
            var names = GetDeviceNames(devices);
            var selectedIndex = CameraDeviceSelection.SelectPreferredOrFallbackIndex(names, preferredName);
            if (selectedIndex < 0)
            {
                StatusMessage = "No webcam devices are available for switching";
                return false;
            }

            var targetName = names[selectedIndex];
            var preferredIdentity = string.IsNullOrWhiteSpace(preferredName) ? string.Empty : targetName;
            if (!_cameraSwitchPending && Status == PoseProviderStatus.Ready && string.Equals(SelectedCameraName, targetName, StringComparison.OrdinalIgnoreCase))
            {
                preferredCameraName = preferredIdentity;
                StatusMessage = $"Camera already active: {targetName}";
                return false;
            }

            _pendingCameraName = targetName;
            _pendingPreferredCameraName = preferredIdentity;
            _cameraSwitchPending = true;
            StatusMessage = $"Camera switch pending: {targetName}";
            return true;
        }

        private void ExecutePendingCameraSwitch()
        {
            if (!_cameraSwitchPending)
            {
                return;
            }

            var targetName = _pendingCameraName;
            preferredCameraName = _pendingPreferredCameraName;
            _cameraSwitchPending = false;
            _pendingCameraName = string.Empty;
            _pendingPreferredCameraName = string.Empty;
            RestartProvider(invalidateCameraSession: true);
            StatusMessage = $"Switching camera: {targetName}";
        }

        private void RestartProvider(bool invalidateCameraSession)
        {
            CleanupRuntime();
            ClearProviderSessionState();
            if (invalidateCameraSession)
            {
                _coordinateConventionVersion++;
            }
            Status = PoseProviderStatus.Starting;
            _bootstrapCoroutine = StartCoroutine(BootstrapAsync());
        }

        private void ClearProviderSessionState()
        {
            _pendingObservation.Clear();
            _latestObservation.Clear();
            Array.Clear(_lastTrackedAtSeconds, 0, _lastTrackedAtSeconds.Length);
            Interlocked.Exchange(ref _resultCallbackReceived, 0);
            Interlocked.Exchange(ref _resultCallbackLogPublished, 0);
            Interlocked.Exchange(ref _totalInferenceRequests, 0);
            Interlocked.Exchange(ref _totalResultCallbacks, 0);
            Interlocked.Exchange(ref _resultsInWindow, 0);
            Interlocked.Exchange(ref _callbacksInWindow, 0);
            _requestsInWindow = 0;
            _cameraFramesInWindow = 0;
            _skippedInWindow = 0;
            _noFreshFrameWaitsInWindow = 0;
            _targetIntervalWaitsInWindow = 0;
            _readbackBusyWaitsInWindow = 0;
            _inferenceBusyWaitsInWindow = 0;
            _textureFramePoolWaitsInWindow = 0;
            _readbackFailuresInWindow = 0;
            _readbackTimeoutsInWindow = 0;
            _preparedFrameReplacementsInWindow = 0;
            _immediateLaunchesInWindow = 0;
            _freshCameraFramePending = false;
            _latestFreshFrameObservedAtSeconds = 0d;
            _activeInferenceFrameObservedAtSeconds = 0d;
            _preparedFramePublishedAtSeconds = 0d;
            _preparedFramePublishedFrameCount = -1;
            InferenceRequestsPerSecond = 0f;
            ResultCallbacksPerSecond = 0f;
            PoseResultsPerSecond = 0f;
            CameraFramesPerSecond = 0f;
            NoFreshFrameWaitsPerSecond = 0f;
            TargetIntervalWaitsPerSecond = 0f;
            ReadbackBusyWaitsPerSecond = 0f;
            InferenceBusyWaitsPerSecond = 0f;
            TextureFramePoolWaitsPerSecond = 0f;
            ReadbackFailuresPerSecond = 0f;
            ReadbackTimeoutsPerSecond = 0f;
            PreparedFrameReplacementsPerSecond = 0f;
            ImmediateLaunchesPerSecond = 0f;
            LastGpuReadbackDurationMilliseconds = 0f;
            LastCpuImageBuildDurationMilliseconds = 0f;
            LastInferenceDurationMilliseconds = 0f;
            LastApproxFrameToResultMilliseconds = 0f;
            LastPreparedToInferenceLaunchMilliseconds = 0f;
            LastPreparedToInferenceLaunchFrameDelta = 0;
            _lastAcceptedLaunchOrigin = PreparedInferenceLaunchOrigin.None;
            _metricsWindowStartedAtSeconds = NowSeconds();
            _inferenceRequestLogPublished = false;
        }

        private int SelectCameraIndex(WebCamDevice[] devices)
        {
            return CameraDeviceSelection.SelectPreferredOrFallbackIndex(GetDeviceNames(devices), preferredCameraName);
        }

        private static string[] GetDeviceNames(WebCamDevice[] devices)
        {
            var names = new string[devices == null ? 0 : devices.Length];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = devices[i].name;
            }
            return names;
        }

        private void UpdateInputTransform()
        {
            if (_webCamTexture == null)
            {
                return;
            }

            var effectiveRotationDegrees = CameraRotationPolicy.ResolveEffectiveRotationDegrees(
                _webCamTexture.videoRotationAngle,
                manualRotationOverride);
            var transformation = ImageTransformationOptions.Build(
                shouldFlipHorizontally: false,
                isVerticallyFlipped: _webCamTexture.videoVerticallyMirrored,
                rotation: (RotationAngle)effectiveRotationDegrees);

            var nextOrientation = new CameraOrientationState(
                sensorRotationDegrees: effectiveRotationDegrees,
                sensorVerticallyMirrored: _webCamTexture.videoVerticallyMirrored,
                frontFacing: _selectedDevice.isFrontFacing,
                sourceTextureHorizontallyMirrored: false,
                displayMirrored: mirrorFrontFacingDisplay,
                inferenceFlipHorizontally: transformation.flipHorizontally,
                inferenceFlipVertically: transformation.flipVertically,
                inferenceRotationDegrees: (int)transformation.rotationAngle);
            if (!_hasPublishedCoordinateConvention || !SameCoordinateConvention(_orientation, nextOrientation))
            {
                _coordinateConventionVersion++;
                _hasPublishedCoordinateConvention = true;
            }

            _orientation = nextOrientation;
            _imageProcessingOptions = new ImageProcessingOptions(rotationDegrees: _orientation.InferenceRotationDegrees);
        }

        private static bool SameCoordinateConvention(CameraOrientationState first, CameraOrientationState second)
        {
            return first.InferenceFlipHorizontally == second.InferenceFlipHorizontally &&
                first.InferenceFlipVertically == second.InferenceFlipVertically &&
                first.InferenceRotationDegrees == second.InferenceRotationDegrees;
        }

        private void UpdateMetrics()
        {
            var now = NowSeconds();
            var elapsed = now - _metricsWindowStartedAtSeconds;
            if (elapsed < 1d)
            {
                return;
            }

            var requests = _requestsInWindow;
            var callbacks = Interlocked.Exchange(ref _callbacksInWindow, 0);
            var results = Interlocked.Exchange(ref _resultsInWindow, 0);
            var cameraFrames = _cameraFramesInWindow;
            InferenceRequestsPerSecond = (float)(requests / elapsed);
            ResultCallbacksPerSecond = (float)(callbacks / elapsed);
            PoseResultsPerSecond = (float)(results / elapsed);
            CameraFramesPerSecond = (float)(cameraFrames / elapsed);
            NoFreshFrameWaitsPerSecond = (float)(_noFreshFrameWaitsInWindow / elapsed);
            TargetIntervalWaitsPerSecond = (float)(_targetIntervalWaitsInWindow / elapsed);
            ReadbackBusyWaitsPerSecond = (float)(_readbackBusyWaitsInWindow / elapsed);
            InferenceBusyWaitsPerSecond = (float)(_inferenceBusyWaitsInWindow / elapsed);
            TextureFramePoolWaitsPerSecond = (float)(_textureFramePoolWaitsInWindow / elapsed);
            ReadbackFailuresPerSecond = (float)(_readbackFailuresInWindow / elapsed);
            ReadbackTimeoutsPerSecond = (float)(_readbackTimeoutsInWindow / elapsed);
            PreparedFrameReplacementsPerSecond = (float)(_preparedFrameReplacementsInWindow / elapsed);
            ImmediateLaunchesPerSecond = (float)(_immediateLaunchesInWindow / elapsed);
            SkippedInferenceOpportunities += _skippedInWindow;

            if (Status == PoseProviderStatus.Ready && !_cameraSwitchPending)
            {
                StatusMessage =
                    $"Camera/Pose ready | Body input: {BodyInferenceWidth}x{BodyInferenceHeight} {(BodyInferenceUsesScaledTexture ? "scaled" : "native")} | Pipe ms RB/build/detect/~F→R: {LastGpuReadbackDurationMilliseconds:0.0}/{LastCpuImageBuildDurationMilliseconds:0.0}/{LastInferenceDurationMilliseconds:0.0}/{LastApproxFrameToResultMilliseconds:0.0}\n" +
                    $"Prep→launch: {LastPreparedToInferenceLaunchMilliseconds:0.0} ms Δf={LastPreparedToInferenceLaunchFrameDelta} origin={LastAcceptedLaunchOriginLabel} fast={ImmediateLaunchesPerSecond:0.0}/s\n" +
                    $"Wait/s noFresh/int/RB/inf/pool: {NoFreshFrameWaitsPerSecond:0.0}/{TargetIntervalWaitsPerSecond:0.0}/{ReadbackBusyWaitsPerSecond:0.0}/{InferenceBusyWaitsPerSecond:0.0}/{TextureFramePoolWaitsPerSecond:0.0}   RB fail/to={ReadbackFailuresPerSecond:0.0}/{ReadbackTimeoutsPerSecond:0.0}   replace={PreparedFrameReplacementsPerSecond:0.0}/s   cb={ResultCallbacksPerSecond:0.0}/s";
            }

            _requestsInWindow = 0;
            _cameraFramesInWindow = 0;
            _skippedInWindow = 0;
            _noFreshFrameWaitsInWindow = 0;
            _targetIntervalWaitsInWindow = 0;
            _readbackBusyWaitsInWindow = 0;
            _inferenceBusyWaitsInWindow = 0;
            _textureFramePoolWaitsInWindow = 0;
            _readbackFailuresInWindow = 0;
            _readbackTimeoutsInWindow = 0;
            _preparedFrameReplacementsInWindow = 0;
            _immediateLaunchesInWindow = 0;
            _metricsWindowStartedAtSeconds = now;
        }

        private bool WasRecentlyTracked(int index, double nowSeconds)
        {
            var lastTrackedAt = _lastTrackedAtSeconds[index];
            return lastTrackedAt > 0 && nowSeconds - lastTrackedAt <= trustSettings.trackingGraceSeconds;
        }

        private void SetFailure(PoseProviderStatus failureStatus, string message)
        {
            Status = failureStatus;
            StatusMessage = message;
            UnityEngine.Debug.LogWarning($"[PoseTrackingSpike] {message}");
        }

        private static bool IsFailureStatus(PoseProviderStatus status)
        {
            return status == PoseProviderStatus.NoCamera ||
                   status == PoseProviderStatus.CameraPermissionDenied ||
                   status == PoseProviderStatus.CameraFailed ||
                   status == PoseProviderStatus.ModelFailed ||
                   status == PoseProviderStatus.InferenceFailed ||
                   status == PoseProviderStatus.Stopped;
        }

        private void ReleasePreparedFrame()
        {
            if (_preparedTextureFrame == null)
            {
                return;
            }
            _preparedTextureFrame.Release();
            _preparedTextureFrame = null;
            _preparedFrameObservedAtSeconds = 0d;
            _preparedCoordinateConventionVersion = 0;
            _preparedFramePublishedAtSeconds = 0d;
            _preparedFramePublishedFrameCount = -1;
        }

        private void CleanupRuntime()
        {
            _shuttingDown = true;
            _freshCameraFramePending = false;
            ReleasePreparedFrame();

            if (_poseLandmarker != null)
            {
                try
                {
                    ((IDisposable)_poseLandmarker).Dispose();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogWarning($"[PoseTrackingSpike] Pose Landmarker cleanup failed: {exception.Message}");
                }
                _poseLandmarker = null;
            }

            _textureFramePool?.Dispose();
            _textureFramePool = null;
            ReleaseBodyInferenceRenderTexture();
            _bodyInferenceWidth = 0;
            _bodyInferenceHeight = 0;
            _configuredCameraWidth = 0;
            _configuredCameraHeight = 0;
            StopCamera();
            _inferenceScheduler.Reset();
            _readbackPending = false;
            Interlocked.Exchange(ref _inferenceOutstanding, 0);
        }

        private void ReleaseBodyInferenceRenderTexture()
        {
            if (_bodyInferenceRenderTexture == null)
            {
                return;
            }

            if (_bodyInferenceRenderTexture.IsCreated())
            {
                _bodyInferenceRenderTexture.Release();
            }

            Destroy(_bodyInferenceRenderTexture);
            _bodyInferenceRenderTexture = null;
        }

        private void StopCamera()
        {
            if (_webCamTexture == null)
            {
                return;
            }
            _webCamTexture.Stop();
            Destroy(_webCamTexture);
            _webCamTexture = null;
        }

        private double NowSeconds()
        {
            return _clock.ElapsedTicks / (double)Stopwatch.Frequency;
        }

        private void OnDestroy()
        {
            _bootstrapCoroutine = null;
            _cameraSwitchPending = false;
            _pendingCameraName = string.Empty;
            _pendingPreferredCameraName = string.Empty;
            CleanupRuntime();
            Status = PoseProviderStatus.Stopped;
        }
    }
}
