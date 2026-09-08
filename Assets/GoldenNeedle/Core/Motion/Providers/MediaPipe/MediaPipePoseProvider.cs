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

    /// <summary>
    /// Main-thread request cadence helper. Busy frames never advance timing state, so once the
    /// previous readback/inference finishes, the next Update may launch immediately if the minimum
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
            bool freshFrameAvailable = true)
        {
            return freshFrameAvailable &&
                !busy &&
                IsIntervalElapsed(
                    nowSeconds,
                    minimumIntervalSeconds);
        }

        public void MarkAccepted(double nowSeconds)
        {
            _hasAcceptedRequest = true;
            _lastAcceptedRequestAtSeconds = nowSeconds;
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
        [SerializeField] private CameraRotationOverride manualRotationOverride =
            CameraRotationOverride.Auto;
        [Tooltip("Optional selfie-style display mirror. Canonical left/right semantics are not changed.")]
        [SerializeField] private bool mirrorFrontFacingDisplay;

        [Header("Pose Landmarker")]
        [SerializeField] private float targetInferenceFps = 30f;
        [SerializeField] private float readbackTimeoutSeconds = 2f;
        [SerializeField] private PoseTrustSettings trustSettings = new PoseTrustSettings();

        private readonly object _observationGate = new object();
        private readonly double[] _lastTrackedAtSeconds = new double[PoseObservation.LandmarkCount];
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly InferenceLaunchScheduler _inferenceScheduler =
            new InferenceLaunchScheduler();

        private PoseObservation _pendingObservation;
        private PoseObservation _latestObservation;
        private WebCamTexture _webCamTexture;
        private WebCamDevice _selectedDevice;
        private TextureFramePool _textureFramePool;
        private PoseLandmarker _poseLandmarker;
        private Coroutine _bootstrapCoroutine;
        private bool _readbackPending;
        private bool _freshCameraFramePending;
        private bool _shuttingDown;
        private bool _cameraSwitchPending;
        private string _pendingCameraName = string.Empty;
        private string _pendingPreferredCameraName = string.Empty;
        private double _inferenceStartedAtSeconds;
        private double _metricsWindowStartedAtSeconds;
        private int _inferenceOutstanding;
        private int _requestsInWindow;
        private int _resultsInWindow;
        private int _skippedInWindow;
        private int _cameraFramesInWindow;
        private int _resultCallbackReceived;
        private int _resultCallbackLogPublished;
        private int _totalInferenceRequests;
        private int _totalResultCallbacks;
        private bool _inferenceRequestLogPublished;
        private ImageProcessingOptions _imageProcessingOptions;
        private CameraOrientationState _orientation;
        private bool _hasPublishedCoordinateConvention;
        private int _coordinateConventionVersion;

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
        public float CameraFramesPerSecond { get; private set; }
        public float InferenceRequestsPerSecond { get; private set; }
        public float PoseResultsPerSecond { get; private set; }
        public bool HasReceivedResult => Volatile.Read(ref _resultCallbackReceived) != 0;
        public int TotalInferenceRequests => Volatile.Read(ref _totalInferenceRequests);
        public int TotalResultCallbacks => Volatile.Read(ref _totalResultCallbacks);
        public int SkippedInferenceOpportunities { get; private set; }
        public float LastInferenceDurationMilliseconds { get; private set; }
        public double LatestPoseAgeMilliseconds
        {
            get
            {
                lock (_observationGate)
                {
                    return _latestObservation.receivedAtSeconds <= 0
                        ? double.PositiveInfinity
                        : Mathf.Max(0f, (float)((NowSeconds() - _latestObservation.receivedAtSeconds) * 1000d));
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

                // Once a switch is requested, do not start any additional old-camera work.
                return;
            }

            if (Status != PoseProviderStatus.Ready || _webCamTexture == null || _poseLandmarker == null || _textureFramePool == null)
            {
                return;
            }

            var freshCameraFrame = _webCamTexture.didUpdateThisFrame;
            if (freshCameraFrame)
            {
                _cameraFramesInWindow++;
                // One-bit latest-frame latch: multiple arrivals collapse to the newest
                // WebCamTexture image. This is not a frame queue.
                _freshCameraFramePending = true;
            }

            UpdateInputTransform();

            var now = NowSeconds();
            var interval = 1d / Mathf.Max(1f, targetInferenceFps);
            var busy =
                _readbackPending ||
                Volatile.Read(ref _inferenceOutstanding) != 0;

            if (!_inferenceScheduler.CanLaunch(
                    now,
                    interval,
                    busy,
                    _freshCameraFramePending))
            {
                if (busy &&
                    _freshCameraFramePending &&
                    _inferenceScheduler.IsIntervalElapsed(
                        now,
                        interval))
                {
                    _skippedInWindow++;
                }

                return;
            }

            if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
            {
                _skippedInWindow++;
                return;
            }

            _freshCameraFramePending = false;
            _readbackPending = true;
            StartCoroutine(CaptureAndInferAsync(textureFrame));
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
                _textureFramePool = new TextureFramePool(ActualCameraWidth, ActualCameraHeight, TextureFormat.RGBA32, 2);
                UpdateInputTransform();
                _inferenceScheduler.Reset();
                Status = PoseProviderStatus.Ready;
                StatusMessage = "Camera and CPU Pose Landmarker are ready";
                UnityEngine.Debug.Log($"[PoseTrackingSpike] Ready: camera={SelectedCameraName}, resolution={ActualCameraWidth}x{ActualCameraHeight}, requestedFps={requestedCameraFps}, model={ModelFileName}, delegate=CPU, poses=1, segmentation=false");
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

        private IEnumerator CaptureAndInferAsync(TextureFrame textureFrame)
        {
            AsyncGPUReadbackRequest request = default;
            try
            {
                // Readback flips and ImageProcessingOptions rotation construct the canonical
                // inference image. MediaPipe landmark outputs stay in that frame; they are not
                // mapped back to WebCamTexture/sensor coordinates downstream.
                request = textureFrame.ReadTextureAsync(
                    _webCamTexture,
                    _orientation.InferenceFlipHorizontally,
                    _orientation.InferenceFlipVertically);
            }
            catch (Exception exception)
            {
                textureFrame.Release();
                _readbackPending = false;
                SetFailure(PoseProviderStatus.InferenceFailed, $"Camera readback setup failed: {exception.Message}");
                UnityEngine.Debug.LogException(exception, this);
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + Mathf.Max(0.25f, readbackTimeoutSeconds);
            while (!request.done && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!request.done || request.hasError)
            {
                textureFrame.Release();
                _readbackPending = false;
                _skippedInWindow++;
                StatusMessage = request.hasError ? "Camera frame readback failed; retrying" : "Camera frame readback timed out; retrying";
                yield break;
            }

            if (Volatile.Read(ref _inferenceOutstanding) != 0 || _poseLandmarker == null || _shuttingDown)
            {
                textureFrame.Release();
                _readbackPending = false;
                _skippedInWindow++;
                yield break;
            }

            var frameReleased = false;
            try
            {
                using (var image = textureFrame.BuildCPUImage())
                {
                    textureFrame.Release();
                    frameReleased = true;
                    var timestampMillisec = _clock.ElapsedMilliseconds;
                    Interlocked.Exchange(ref _inferenceOutstanding, 1);
                    _inferenceStartedAtSeconds = NowSeconds();

                    try
                    {
                        // DetectAsync takes ownership of the image through the input packet.
                        // LIVE_STREAM mode invokes OnPoseLandmarkerResult off the Unity main thread.
                        _poseLandmarker.DetectAsync(
                            image,
                            timestampMillisec,
                            _imageProcessingOptions);
                        _inferenceScheduler.MarkAccepted(
                            NowSeconds());
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
            finally
            {
                _readbackPending = false;
            }
        }

        private void OnPoseLandmarkerResult(PoseLandmarkerResult result, Image _, long timestampMillisec)
        {
            if (_shuttingDown)
            {
                return;
            }

            Interlocked.Exchange(ref _resultCallbackReceived, 1);
            Interlocked.Increment(ref _totalResultCallbacks);

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
            if (_bootstrapCoroutine != null ||
                _readbackPending ||
                Volatile.Read(ref _inferenceOutstanding) != 0 ||
                _cameraSwitchPending)
            {
                return;
            }

            RestartProvider(
                invalidateCameraSession: false);
        }

        public bool RequestCycleCamera()
        {
            var devices = WebCamTexture.devices;
            CameraDeviceCount = devices.Length;
            var names = GetDeviceNames(devices);
            var currentName = !string.IsNullOrWhiteSpace(SelectedCameraName)
                ? SelectedCameraName
                : preferredCameraName;
            var nextName = CameraDeviceSelection.GetNextUsableDeviceName(
                names,
                currentName);
            if (string.IsNullOrWhiteSpace(nextName) ||
                string.Equals(
                    nextName,
                    currentName,
                    StringComparison.OrdinalIgnoreCase))
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
            var selectedIndex =
                CameraDeviceSelection.SelectPreferredOrFallbackIndex(
                    names,
                    preferredName);
            if (selectedIndex < 0)
            {
                StatusMessage = "No webcam devices are available for switching";
                return false;
            }

            var targetName = names[selectedIndex];
            var preferredIdentity =
                string.IsNullOrWhiteSpace(preferredName)
                    ? string.Empty
                    : targetName;

            if (!_cameraSwitchPending &&
                Status == PoseProviderStatus.Ready &&
                string.Equals(
                    SelectedCameraName,
                    targetName,
                    StringComparison.OrdinalIgnoreCase))
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

            RestartProvider(
                invalidateCameraSession: true);
            StatusMessage = $"Switching camera: {targetName}";
        }

        private void RestartProvider(bool invalidateCameraSession)
        {
            CleanupRuntime();
            ClearProviderSessionState();

            if (invalidateCameraSession)
            {
                // A different physical camera/framing invalidates old calibration and room origin
                // even when its H/V/rotation convention happens to match the previous device.
                _coordinateConventionVersion++;
            }

            Status = PoseProviderStatus.Starting;
            _bootstrapCoroutine = StartCoroutine(BootstrapAsync());
        }

        private void ClearProviderSessionState()
        {
            _pendingObservation.Clear();
            _latestObservation.Clear();
            Array.Clear(
                _lastTrackedAtSeconds,
                0,
                _lastTrackedAtSeconds.Length);
            Interlocked.Exchange(
                ref _resultCallbackReceived,
                0);
            Interlocked.Exchange(
                ref _resultCallbackLogPublished,
                0);
            Interlocked.Exchange(
                ref _totalInferenceRequests,
                0);
            Interlocked.Exchange(
                ref _totalResultCallbacks,
                0);
            Interlocked.Exchange(
                ref _resultsInWindow,
                0);
            _requestsInWindow = 0;
            _cameraFramesInWindow = 0;
            _skippedInWindow = 0;
            _freshCameraFramePending = false;
            InferenceRequestsPerSecond = 0f;
            PoseResultsPerSecond = 0f;
            CameraFramesPerSecond = 0f;
            LastInferenceDurationMilliseconds = 0f;
            _metricsWindowStartedAtSeconds = NowSeconds();
            _inferenceRequestLogPublished = false;
        }

        private int SelectCameraIndex(WebCamDevice[] devices)
        {
            return CameraDeviceSelection.SelectPreferredOrFallbackIndex(
                GetDeviceNames(devices),
                preferredCameraName);
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

            // These are pixel/input preparation flags only. Front-facing is camera metadata,
            // not a semantic request to mirror the inference image. Golden Needle presents this
            // WebCamTexture as an unmirrored physical camera view, and MediaPipe's landmark IDs
            // are anatomical. Mirroring the inference pixels here would make those semantic IDs
            // describe the opposite physical side. Keep only the transport/orientation correction.
            var effectiveRotationDegrees =
                CameraRotationPolicy.ResolveEffectiveRotationDegrees(
                    _webCamTexture.videoRotationAngle,
                    manualRotationOverride);
            var transformation = ImageTransformationOptions.Build(
                shouldFlipHorizontally: false,
                isVerticallyFlipped: _webCamTexture.videoVerticallyMirrored,
                rotation: (RotationAngle)effectiveRotationDegrees);

            var nextOrientation = new CameraOrientationState(
                // This field is the effective rotation used by both inference and Lab display.
                // VideoRotationAngle separately exposes the driver's reported raw metadata.
                sensorRotationDegrees: effectiveRotationDegrees,
                sensorVerticallyMirrored: _webCamTexture.videoVerticallyMirrored,
                frontFacing: _selectedDevice.isFrontFacing,
                // WebCamTexture is presented from its sensor metadata as-is. Do not infer an
                // extra display mirror merely because the selected device is front-facing.
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
            var results = Interlocked.Exchange(ref _resultsInWindow, 0);
            var cameraFrames = _cameraFramesInWindow;
            InferenceRequestsPerSecond = (float)(requests / elapsed);
            PoseResultsPerSecond = (float)(results / elapsed);
            CameraFramesPerSecond = (float)(cameraFrames / elapsed);
            SkippedInferenceOpportunities += _skippedInWindow;
            _requestsInWindow = 0;
            _cameraFramesInWindow = 0;
            _skippedInWindow = 0;
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

        private void CleanupRuntime()
        {
            _shuttingDown = true;
            _readbackPending = false;
            _freshCameraFramePending = false;

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
            StopCamera();
            _inferenceScheduler.Reset();
            Interlocked.Exchange(ref _inferenceOutstanding, 0);
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
