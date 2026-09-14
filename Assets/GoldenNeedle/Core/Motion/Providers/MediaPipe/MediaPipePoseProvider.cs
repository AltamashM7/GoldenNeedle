using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

    public enum BodyReadbackPath
    {
        Homuler,
        DirectCPU,
        DirectFallback,
    }

    public enum DirectReadbackStage
    {
        None,
        H,
        V,
        HV,
    }

    public enum DirectReadbackTimingMetric
    {
        SubmitToCallback,
        CallbackEnterToPoll,
        CallbackExitToPoll,
        PollToPublish,
        SubmitCall,
    }

    public readonly struct DirectReadbackTimingSample
    {
        public DirectReadbackTimingSample(
            long serial,
            double submitToCallbackMilliseconds,
            double callbackEnterToPollMilliseconds,
            double callbackExitToPollMilliseconds,
            double pollToPublishMilliseconds,
            double submitCallMilliseconds,
            bool isError)
        {
            Serial = serial;
            SubmitToCallbackMilliseconds = submitToCallbackMilliseconds;
            CallbackEnterToPollMilliseconds = callbackEnterToPollMilliseconds;
            CallbackExitToPollMilliseconds = callbackExitToPollMilliseconds;
            PollToPublishMilliseconds = pollToPublishMilliseconds;
            SubmitCallMilliseconds = submitCallMilliseconds;
            IsError = isError;
        }

        public long Serial { get; }
        public double SubmitToCallbackMilliseconds { get; }
        public double CallbackEnterToPollMilliseconds { get; }
        public double CallbackExitToPollMilliseconds { get; }
        public double PollToPublishMilliseconds { get; }
        public double SubmitCallMilliseconds { get; }
        public bool IsError { get; }
    }

    public static class DirectReadbackTimingMath
    {
        public static double TicksToMilliseconds(long ticks, long frequency)
        {
            return frequency <= 0 ? double.NaN : ticks * 1000d / frequency;
        }

        public static bool TryCreateSample(
            long expectedSerial,
            long callbackSerial,
            long submitStartTicks,
            long submitReturnTicks,
            long callbackEnterTicks,
            long callbackExitTicks,
            long coroutineObserveTicks,
            long publishTicks,
            bool isError,
            out DirectReadbackTimingSample sample,
            long frequency = 0)
        {
            sample = default;
            if (expectedSerial <= 0 || callbackSerial != expectedSerial ||
                submitStartTicks <= 0 || submitReturnTicks < submitStartTicks ||
                callbackEnterTicks < submitStartTicks || callbackExitTicks < callbackEnterTicks ||
                coroutineObserveTicks <= 0 || publishTicks < coroutineObserveTicks)
            {
                return false;
            }

            frequency = frequency > 0 ? frequency : Stopwatch.Frequency;
            if (frequency <= 0)
            {
                return false;
            }

            sample = new DirectReadbackTimingSample(
                expectedSerial,
                TicksToMilliseconds(callbackEnterTicks - submitStartTicks, frequency),
                TicksToMilliseconds(coroutineObserveTicks - callbackEnterTicks, frequency),
                TicksToMilliseconds(coroutineObserveTicks - callbackExitTicks, frequency),
                TicksToMilliseconds(publishTicks - coroutineObserveTicks, frequency),
                TicksToMilliseconds(submitReturnTicks - submitStartTicks, frequency),
                isError);
            return true;
        }
    }

    public sealed class DirectReadbackTimingWindow
    {
        private readonly DirectReadbackTimingSample[] _samples;
        private readonly double[] _scratch;
        private int _nextIndex;
        private int _count;

        public DirectReadbackTimingWindow(int capacity = 64)
        {
            capacity = Math.Max(1, capacity);
            _samples = new DirectReadbackTimingSample[capacity];
            _scratch = new double[capacity];
        }

        public int Capacity => _samples.Length;
        public int ValidSampleCount => _count;
        public int MissingSampleCount { get; private set; }
        public int ExcludedSampleCount { get; private set; }

        public void Reset()
        {
            _nextIndex = 0;
            _count = 0;
            MissingSampleCount = 0;
            ExcludedSampleCount = 0;
        }

        public void RecordMissingSample()
        {
            MissingSampleCount++;
        }

        public void RecordExcludedSample()
        {
            ExcludedSampleCount++;
        }

        public void Add(in DirectReadbackTimingSample sample)
        {
            if (sample.IsError)
            {
                RecordExcludedSample();
                return;
            }

            _samples[_nextIndex] = sample;
            _nextIndex = (_nextIndex + 1) % _samples.Length;
            _count = Math.Min(_count + 1, _samples.Length);
        }

        public double GetMedianMilliseconds(DirectReadbackTimingMetric metric)
        {
            if (_count == 0)
            {
                return double.NaN;
            }

            CopyMetric(metric);
            Array.Sort(_scratch, 0, _count);
            var middle = _count / 2;
            return _count % 2 == 0
                ? (_scratch[middle - 1] + _scratch[middle]) * 0.5d
                : _scratch[middle];
        }

        public double GetP95Milliseconds(DirectReadbackTimingMetric metric)
        {
            if (_count == 0)
            {
                return double.NaN;
            }

            CopyMetric(metric);
            Array.Sort(_scratch, 0, _count);
            var index = Math.Min(_count - 1, Math.Max(0, (int)Math.Ceiling(_count * 0.95d) - 1));
            return _scratch[index];
        }

        private void CopyMetric(DirectReadbackTimingMetric metric)
        {
            for (var i = 0; i < _count; i++)
            {
                _scratch[i] = metric switch
                {
                    DirectReadbackTimingMetric.SubmitToCallback => _samples[i].SubmitToCallbackMilliseconds,
                    DirectReadbackTimingMetric.CallbackEnterToPoll => _samples[i].CallbackEnterToPollMilliseconds,
                    DirectReadbackTimingMetric.CallbackExitToPoll => _samples[i].CallbackExitToPollMilliseconds,
                    DirectReadbackTimingMetric.PollToPublish => _samples[i].PollToPublishMilliseconds,
                    DirectReadbackTimingMetric.SubmitCall => _samples[i].SubmitCallMilliseconds,
                    _ => double.NaN,
                };
            }
        }
    }

    public enum InferenceContinuationTimingMetric
    {
        SubmitCall,
        RequestToResult,
        ResultToNextLaunch,
    }

    public readonly struct InferenceContinuationTimingSample
    {
        public InferenceContinuationTimingSample(
            long serial,
            int sessionId,
            double submitCallMilliseconds,
            double requestToResultMilliseconds,
            double resultToNextLaunchMilliseconds,
            bool preparedWaitingAtResult,
            PreparedInferenceLaunchOrigin nextLaunchOrigin)
        {
            Serial = serial;
            SessionId = sessionId;
            SubmitCallMilliseconds = submitCallMilliseconds;
            RequestToResultMilliseconds = requestToResultMilliseconds;
            ResultToNextLaunchMilliseconds = resultToNextLaunchMilliseconds;
            PreparedWaitingAtResult = preparedWaitingAtResult;
            NextLaunchOrigin = nextLaunchOrigin;
        }

        public long Serial { get; }
        public int SessionId { get; }
        public double SubmitCallMilliseconds { get; }
        public double RequestToResultMilliseconds { get; }
        public double ResultToNextLaunchMilliseconds { get; }
        public bool PreparedWaitingAtResult { get; }
        public PreparedInferenceLaunchOrigin NextLaunchOrigin { get; }
    }

    public static class InferenceContinuationTimingMath
    {
        public static bool TryCreateSample(
            int expectedSessionId,
            int completedSessionId,
            long expectedSerial,
            long completedSerial,
            long submitStartTicks,
            long submitReturnTicks,
            long callbackEntryTicks,
            long completionTicks,
            long nextAcceptedLaunchTicks,
            bool preparedWaitingAtResult,
            PreparedInferenceLaunchOrigin nextLaunchOrigin,
            out InferenceContinuationTimingSample sample,
            long frequency = 0)
        {
            sample = default;
            if (expectedSessionId <= 0 || completedSessionId != expectedSessionId ||
                expectedSerial <= 0 || completedSerial != expectedSerial ||
                submitStartTicks <= 0 || submitReturnTicks < submitStartTicks ||
                callbackEntryTicks < submitReturnTicks || completionTicks < callbackEntryTicks ||
                nextAcceptedLaunchTicks <= completionTicks ||
                (nextLaunchOrigin != PreparedInferenceLaunchOrigin.Update &&
                 nextLaunchOrigin != PreparedInferenceLaunchOrigin.ReadbackContinuation))
            {
                return false;
            }

            frequency = frequency > 0 ? frequency : Stopwatch.Frequency;
            if (frequency <= 0)
            {
                return false;
            }

            sample = new InferenceContinuationTimingSample(
                completedSerial,
                completedSessionId,
                TicksToMilliseconds(submitReturnTicks - submitStartTicks, frequency),
                TicksToMilliseconds(callbackEntryTicks - submitReturnTicks, frequency),
                TicksToMilliseconds(nextAcceptedLaunchTicks - completionTicks, frequency),
                preparedWaitingAtResult,
                nextLaunchOrigin);
            return true;
        }

        public static double TicksToMilliseconds(long ticks, long frequency)
        {
            return frequency <= 0 ? double.NaN : ticks * 1000d / frequency;
        }
    }

    public sealed class InferenceContinuationTimingWindow
    {
        private readonly InferenceContinuationTimingSample[] _samples;
        private readonly double[] _scratch;
        private int _nextIndex;
        private int _count;

        public InferenceContinuationTimingWindow(int capacity = 64)
        {
            capacity = Math.Max(1, capacity);
            _samples = new InferenceContinuationTimingSample[capacity];
            _scratch = new double[capacity];
        }

        public int Capacity => _samples.Length;
        public int ValidSampleCount => _count;
        public int MissingSampleCount { get; private set; }
        public int PreparedWaitingSampleCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _count; i++)
                {
                    if (_samples[i].PreparedWaitingAtResult)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public double PreparedWaitingRate => _count == 0
            ? double.NaN
            : PreparedWaitingSampleCount / (double)_count;

        public void Reset()
        {
            _nextIndex = 0;
            _count = 0;
            MissingSampleCount = 0;
        }

        public void RecordMissingSample()
        {
            MissingSampleCount++;
        }

        public void Add(in InferenceContinuationTimingSample sample)
        {
            _samples[_nextIndex] = sample;
            _nextIndex = (_nextIndex + 1) % _samples.Length;
            _count = Math.Min(_count + 1, _samples.Length);
        }

        public int GetOriginSampleCount(PreparedInferenceLaunchOrigin origin, bool preparedOnly = false)
        {
            var count = 0;
            for (var i = 0; i < _count; i++)
            {
                if (_samples[i].NextLaunchOrigin == origin && (!preparedOnly || _samples[i].PreparedWaitingAtResult))
                {
                    count++;
                }
            }
            return count;
        }

        public double GetMedianMilliseconds(InferenceContinuationTimingMetric metric, bool preparedOnly = false)
        {
            var count = CopyMetric(metric, preparedOnly);
            if (count == 0)
            {
                return double.NaN;
            }

            Array.Sort(_scratch, 0, count);
            var middle = count / 2;
            return count % 2 == 0
                ? (_scratch[middle - 1] + _scratch[middle]) * 0.5d
                : _scratch[middle];
        }

        public double GetP95Milliseconds(InferenceContinuationTimingMetric metric, bool preparedOnly = false)
        {
            var count = CopyMetric(metric, preparedOnly);
            if (count == 0)
            {
                return double.NaN;
            }

            Array.Sort(_scratch, 0, count);
            var index = Math.Min(count - 1, Math.Max(0, (int)Math.Ceiling(count * 0.95d) - 1));
            return _scratch[index];
        }

        private int CopyMetric(InferenceContinuationTimingMetric metric, bool preparedOnly)
        {
            var count = 0;
            for (var i = 0; i < _count; i++)
            {
                if (preparedOnly && !_samples[i].PreparedWaitingAtResult)
                {
                    continue;
                }

                _scratch[count++] = metric switch
                {
                    InferenceContinuationTimingMetric.SubmitCall => _samples[i].SubmitCallMilliseconds,
                    InferenceContinuationTimingMetric.RequestToResult => _samples[i].RequestToResultMilliseconds,
                    InferenceContinuationTimingMetric.ResultToNextLaunch => _samples[i].ResultToNextLaunchMilliseconds,
                    _ => double.NaN,
                };
            }
            return count;
        }
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

        public static BodyReadbackPath SelectBodyReadbackPath(
            bool experimentEnabled,
            bool providerReady,
            bool hasDownscaledBodyRenderTexture,
            bool sourceDimensionsMatchTextureFrame,
            bool bodyResourcesCurrent,
            bool flipHorizontally,
            bool flipVertically,
            bool flipStagingAvailable,
            bool formatSupported,
            bool sessionAvailable)
        {
            if (!experimentEnabled)
            {
                return BodyReadbackPath.Homuler;
            }

            var flipRequired = flipHorizontally || flipVertically;
            return providerReady &&
                hasDownscaledBodyRenderTexture &&
                sourceDimensionsMatchTextureFrame &&
                bodyResourcesCurrent &&
                (!flipRequired || flipStagingAvailable) &&
                formatSupported &&
                sessionAvailable
                    ? BodyReadbackPath.DirectCPU
                    : BodyReadbackPath.DirectFallback;
        }

        public static DirectReadbackStage GetDirectReadbackStage(
            bool flipHorizontally,
            bool flipVertically)
        {
            if (flipHorizontally && flipVertically)
            {
                return DirectReadbackStage.HV;
            }
            if (flipHorizontally)
            {
                return DirectReadbackStage.H;
            }
            if (flipVertically)
            {
                return DirectReadbackStage.V;
            }
            return DirectReadbackStage.None;
        }

        public static void GetDirectReadbackFlipTransform(
            bool flipHorizontally,
            bool flipVertically,
            out Vector2 scale,
            out Vector2 offset)
        {
            scale = new Vector2(
                flipHorizontally ? -1f : 1f,
                flipVertically ? -1f : 1f);
            offset = new Vector2(
                flipHorizontally ? 1f : 0f,
                flipVertically ? 1f : 0f);
        }

        public static bool DirectReadbackCleanupRequiresWait(
            bool activeRequestValid,
            bool requestDone)
        {
            return activeRequestValid && !requestDone;
        }

        public static bool DirectReadbackOwnershipCanClear(
            bool activeRequestValid,
            bool requestDone)
        {
            return !activeRequestValid || requestDone;
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
        private const string OpenVinoModelDirectory = "GoldenNeedle/OpenVinoPoseModels";
        private const string OpenVinoDetectorModelFileName = "pose_detector.tflite";
        private const string OpenVinoLandmarkModelFileName = "pose_landmarks_detector.tflite";

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
        [Tooltip("Stock MediaPipe/TFLite remains the default. OpenVINO CPU FP32 is experimental and must be selected explicitly for A/B QA.")]
        [SerializeField] private PoseInferenceBackend inferenceBackend = PoseInferenceBackend.MediaPipeTfliteCpu;
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
        [Tooltip("Experimental body-pose-only path. When eligible it writes GPU readback directly into the pooled TextureFrame CPU buffer, bypassing Homuler staging/copy/Apply. Flipped inputs use one persistent Golden Needle staging RT with Homuler-equivalent scale/offset before the direct readback. It remains CPU pose inference and never changes CameraTexture or Lab display.")]
        [SerializeField] private bool enableDirectBodyCpuReadback = false;

        private readonly object _observationGate = new object();
        private readonly double[] _lastTrackedAtSeconds = new double[PoseObservation.LandmarkCount];
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly InferenceLaunchScheduler _inferenceScheduler = new InferenceLaunchScheduler();
        private readonly InferenceContinuationTimingWindow _inferenceContinuationTimingWindow =
            new InferenceContinuationTimingWindow(64);

        private PoseObservation _pendingObservation;
        private PoseObservation _latestObservation;
        private WebCamTexture _webCamTexture;
        private WebCamDevice _selectedDevice;
        private TextureFramePool _textureFramePool;
        private RenderTexture _bodyInferenceRenderTexture;
        private RenderTexture _directBodyReadbackStagingRenderTexture;
        private int _bodyInferenceWidth;
        private int _bodyInferenceHeight;
        private int _configuredCameraWidth;
        private int _configuredCameraHeight;
        private bool _configuredBodyInferenceDownscale;
        private int _configuredBodyInferenceLongEdge;
        private PoseLandmarker _poseLandmarker;
        private OpenVinoPoseRuntime _openVinoPoseRuntime;
        private Task<OpenVinoPoseRuntime> _openVinoBootstrapTask;
        private Task _openVinoInferenceTask;
        private byte[] _openVinoRgbaBuffer;
        private readonly object _openVinoWorkerFailureGate = new object();
        private Exception _openVinoWorkerFailure;
        private PoseInferenceBackend _activeInferenceBackend = PoseInferenceBackend.MediaPipeTfliteCpu;
        private Coroutine _bootstrapCoroutine;
        private bool _readbackPending;
        private bool _freshCameraFramePending;
        private double _latestFreshFrameObservedAtSeconds;
        private TextureFrame _preparedTextureFrame;
        private double _preparedFrameObservedAtSeconds;
        private int _preparedCoordinateConventionVersion;
        private double _preparedFramePublishedAtSeconds;
        private int _preparedFramePublishedFrameCount = -1;
        private int _preparedTextureFrameDiagnosticOccupied;
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
        private int _directReadbacksInWindow;
        private int _directReadbackFailuresInWindow;
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
        private BodyReadbackPath _activeBodyReadbackPath = BodyReadbackPath.Homuler;
        private DirectReadbackStage _activeDirectReadbackStage = DirectReadbackStage.None;
        private bool _directBodyCpuReadbackSessionAvailable = true;
        private string _directBodyCpuReadbackFallbackReason = string.Empty;
        private AsyncGPUReadbackRequest _activeDirectReadbackRequest;
        private bool _activeDirectReadbackRequestValid;
        private long _directReadbackDiagnosticSerial;
        private long _directReadbackCallbackSerial;
        private long _directReadbackCallbackEnterTicks;
        private long _directReadbackCallbackExitTicks;
        private readonly DirectReadbackTimingWindow _directReadbackTimingWindow =
            new DirectReadbackTimingWindow(64);
        private int _inferenceDiagnosticSessionId = 1;
        private long _nextInferenceDiagnosticSerial;
        private long _activeInferenceDiagnosticSerial;
        private int _activeInferenceDiagnosticSessionId;
        private long _activeInferenceDiagnosticTimestampMilliseconds;
        private long _activeInferenceDiagnosticSubmitStartTicks;
        private long _activeInferenceDiagnosticSubmitReturnTicks;
        private int _activeInferenceDiagnosticAccepted;
        private long _completedInferenceDiagnosticSerial;
        private int _completedInferenceDiagnosticSessionId;
        private long _completedInferenceDiagnosticSubmitStartTicks;
        private long _completedInferenceDiagnosticSubmitReturnTicks;
        private long _completedInferenceDiagnosticCallbackEntryTicks;
        private long _completedInferenceDiagnosticCompletionTicks;
        private int _completedInferenceDiagnosticPreparedWaiting;
        private int _hasCompletedInferenceDiagnostic;
        private long _lastRecordedInferenceDiagnosticSerial;
        private int _inferenceDiagnosticMissingSamples;

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
        public PoseInferenceBackend RequestedInferenceBackend => inferenceBackend;
        public PoseInferenceBackend ActiveInferenceBackend => _activeInferenceBackend;
        public string ActiveInferenceBackendLabel => _activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32
            ? "OpenVINO CPU FP32"
            : "MediaPipe TFLite CPU";
        public string OpenVinoRuntimeInfo { get; private set; } = string.Empty;
        public string OpenVinoEngineInfo { get; private set; } = string.Empty;
        public float LastOpenVinoManagedInputCopyMilliseconds { get; private set; }
        public float LastOpenVinoGraphProcessMilliseconds { get; private set; }
        public float LastOpenVinoDetectorInferenceMilliseconds { get; private set; }
        public float LastOpenVinoLandmarkInferenceMilliseconds { get; private set; }
        public float LastOpenVinoBridgeCopyMilliseconds { get; private set; }
        public bool LastOpenVinoDetectorRan { get; private set; }
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
        public bool DirectBodyCpuReadbackEnabled => enableDirectBodyCpuReadback;
        public BodyReadbackPath ActiveBodyReadbackPath => _activeBodyReadbackPath;
        public string ActiveBodyReadbackPathLabel =>
            _activeBodyReadbackPath == BodyReadbackPath.DirectCPU
                ? "DirectCPU"
                : _activeBodyReadbackPath == BodyReadbackPath.DirectFallback
                    ? "DirectFallback"
                    : "Homuler";
        public DirectReadbackStage ActiveDirectReadbackStage => _activeDirectReadbackStage;
        public string ActiveDirectReadbackStageLabel =>
            _activeDirectReadbackStage == DirectReadbackStage.H
                ? "H"
                : _activeDirectReadbackStage == DirectReadbackStage.V
                    ? "V"
                    : _activeDirectReadbackStage == DirectReadbackStage.HV
                        ? "HV"
                        : "None";
        public string DirectBodyCpuReadbackFallbackReason => _directBodyCpuReadbackFallbackReason;
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
        public float DirectReadbacksPerSecond { get; private set; }
        public float DirectReadbackFailuresPerSecond { get; private set; }
        public int DirectReadbackTimingSampleCount => _directReadbackTimingWindow.ValidSampleCount;
        public int DirectReadbackTimingMissingSampleCount => _directReadbackTimingWindow.MissingSampleCount;
        public int DirectReadbackTimingExcludedSampleCount => _directReadbackTimingWindow.ExcludedSampleCount;
        public double DirectReadbackTimingCallbackToPollMedianMilliseconds =>
            _directReadbackTimingWindow.GetMedianMilliseconds(DirectReadbackTimingMetric.CallbackExitToPoll);
        public double DirectReadbackTimingCallbackToPollP95Milliseconds =>
            _directReadbackTimingWindow.GetP95Milliseconds(DirectReadbackTimingMetric.CallbackExitToPoll);
        public double DirectReadbackTimingSubmitToCallbackMedianMilliseconds =>
            _directReadbackTimingWindow.GetMedianMilliseconds(DirectReadbackTimingMetric.SubmitToCallback);
        public double DirectReadbackTimingSubmitToCallbackP95Milliseconds =>
            _directReadbackTimingWindow.GetP95Milliseconds(DirectReadbackTimingMetric.SubmitToCallback);
        public double DirectReadbackTimingPollToPublishMedianMilliseconds =>
            _directReadbackTimingWindow.GetMedianMilliseconds(DirectReadbackTimingMetric.PollToPublish);
        public double DirectReadbackTimingPollToPublishP95Milliseconds =>
            _directReadbackTimingWindow.GetP95Milliseconds(DirectReadbackTimingMetric.PollToPublish);
        public double DirectReadbackTimingSubmitCallMedianMilliseconds =>
            _directReadbackTimingWindow.GetMedianMilliseconds(DirectReadbackTimingMetric.SubmitCall);
        public double DirectReadbackTimingSubmitCallP95Milliseconds =>
            _directReadbackTimingWindow.GetP95Milliseconds(DirectReadbackTimingMetric.SubmitCall);
        public int InferenceContinuationTimingSampleCount => _inferenceContinuationTimingWindow.ValidSampleCount;
        public int InferenceContinuationTimingMissingSampleCount =>
            _inferenceContinuationTimingWindow.MissingSampleCount + Volatile.Read(ref _inferenceDiagnosticMissingSamples);
        public int InferenceContinuationTimingPreparedWaitingSampleCount =>
            _inferenceContinuationTimingWindow.PreparedWaitingSampleCount;
        public double InferenceContinuationTimingPreparedWaitingRate =>
            _inferenceContinuationTimingWindow.PreparedWaitingRate;
        public int InferenceContinuationTimingUpdateLaunchCount =>
            _inferenceContinuationTimingWindow.GetOriginSampleCount(PreparedInferenceLaunchOrigin.Update);
        public int InferenceContinuationTimingReadbackLaunchCount =>
            _inferenceContinuationTimingWindow.GetOriginSampleCount(PreparedInferenceLaunchOrigin.ReadbackContinuation);
        public double InferenceContinuationTimingSubmitCallMedianMilliseconds =>
            _inferenceContinuationTimingWindow.GetMedianMilliseconds(InferenceContinuationTimingMetric.SubmitCall);
        public double InferenceContinuationTimingSubmitCallP95Milliseconds =>
            _inferenceContinuationTimingWindow.GetP95Milliseconds(InferenceContinuationTimingMetric.SubmitCall);
        public double InferenceContinuationTimingRequestToResultMedianMilliseconds =>
            _inferenceContinuationTimingWindow.GetMedianMilliseconds(InferenceContinuationTimingMetric.RequestToResult);
        public double InferenceContinuationTimingRequestToResultP95Milliseconds =>
            _inferenceContinuationTimingWindow.GetP95Milliseconds(InferenceContinuationTimingMetric.RequestToResult);
        public double InferenceContinuationTimingResultToNextLaunchMedianMilliseconds =>
            _inferenceContinuationTimingWindow.GetMedianMilliseconds(InferenceContinuationTimingMetric.ResultToNextLaunch);
        public double InferenceContinuationTimingResultToNextLaunchP95Milliseconds =>
            _inferenceContinuationTimingWindow.GetP95Milliseconds(InferenceContinuationTimingMetric.ResultToNextLaunch);
        public double InferenceContinuationTimingPreparedResultToNextLaunchMedianMilliseconds =>
            _inferenceContinuationTimingWindow.GetMedianMilliseconds(
                InferenceContinuationTimingMetric.ResultToNextLaunch,
                preparedOnly: true);
        public double InferenceContinuationTimingPreparedResultToNextLaunchP95Milliseconds =>
            _inferenceContinuationTimingWindow.GetP95Milliseconds(
                InferenceContinuationTimingMetric.ResultToNextLaunch,
                preparedOnly: true);
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
            ConsumeOpenVinoWorkerFailure();

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

            if (Status != PoseProviderStatus.Ready || _webCamTexture == null || !IsActiveBackendReady() || _textureFramePool == null)
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
            Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 0);
            _preparedFrameObservedAtSeconds = 0d;
            _preparedCoordinateConventionVersion = 0;
            _preparedFramePublishedAtSeconds = 0d;
            _preparedFramePublishedFrameCount = -1;

            if (_activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32)
            {
                LaunchPreparedOpenVinoInference(
                    textureFrame,
                    frameObservedAtSeconds,
                    framePublishedAtSeconds,
                    framePublishedFrameCount,
                    origin);
                return;
            }

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

                    // Capture the prior completion before replacing the active request token. The
                    // following call-start tick is I4 for that completion and I0 for this request.
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

                    try
                    {
                        _poseLandmarker.DetectAsync(image, timestampMillisec, _imageProcessingOptions);
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
                            UnityEngine.Debug.Log("[PoseTrackingSpike] Live inference request accepted");
                        }
                    }
                    catch (Exception exception)
                    {
                        Volatile.Write(ref _activeInferenceDiagnosticAccepted, 0);
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

            Task<OpenVinoPoseRuntime> bootstrapTask = null;
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
                _bodyInferenceRenderTexture = CreateBodyInferenceRenderTexture(
                    "GoldenNeedle Body Pose Inference");
                _directBodyReadbackStagingRenderTexture = CreateBodyInferenceRenderTexture(
                    "GoldenNeedle Direct CPU Readback Staging");
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
            _activeDirectReadbackStage = DirectReadbackStage.None;
        }

        private RenderTexture CreateBodyInferenceRenderTexture(string textureName)
        {
            var renderTexture = new RenderTexture(
                _bodyInferenceWidth,
                _bodyInferenceHeight,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };
            renderTexture.Create();
            return renderTexture;
        }

        private bool DirectReadbackStagingMatchesBodyInput()
        {
            return _directBodyReadbackStagingRenderTexture != null &&
                _directBodyReadbackStagingRenderTexture.IsCreated() &&
                _directBodyReadbackStagingRenderTexture.width == _bodyInferenceWidth &&
                _directBodyReadbackStagingRenderTexture.height == _bodyInferenceHeight;
        }

        private BodyReadbackPath SelectBodyReadbackPath(
            TextureFrame textureFrame,
            bool flipHorizontally,
            bool flipVertically,
            out string fallbackReason)
        {
            fallbackReason = string.Empty;
            if (!enableDirectBodyCpuReadback)
            {
                return BodyReadbackPath.Homuler;
            }

            var providerReady =
                Status == PoseProviderStatus.Ready &&
                !_shuttingDown &&
                _webCamTexture != null &&
                _textureFramePool != null;
            var hasDownscaledBodyRenderTexture =
                _bodyInferenceRenderTexture != null &&
                _bodyInferenceRenderTexture.IsCreated() &&
                BodyInferenceUsesScaledTexture;
            var sourceDimensionsMatchTextureFrame =
                hasDownscaledBodyRenderTexture &&
                _bodyInferenceRenderTexture.width == textureFrame.width &&
                _bodyInferenceRenderTexture.height == textureFrame.height;
            var bodyResourcesCurrent = BodyInferenceResourcesMatchCurrentIntent();
            var flipRequired = flipHorizontally || flipVertically;
            var flipStagingAvailable = !flipRequired || DirectReadbackStagingMatchesBodyInput();
            var readbackSource = flipRequired
                ? (Texture)_directBodyReadbackStagingRenderTexture
                : _bodyInferenceRenderTexture;
            var expectedGraphicsFormat =
                UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetGraphicsFormat(
                    TextureFormat.RGBA32,
                    true);
            var formatSupported =
                SystemInfo.supportsAsyncGPUReadback &&
                textureFrame.format == TextureFormat.RGBA32 &&
                hasDownscaledBodyRenderTexture &&
                readbackSource != null &&
                SystemInfo.IsFormatSupported(
                    readbackSource.graphicsFormat,
                    UnityEngine.Experimental.Rendering.GraphicsFormatUsage.ReadPixels) &&
                SystemInfo.IsFormatSupported(
                    expectedGraphicsFormat,
                    UnityEngine.Experimental.Rendering.GraphicsFormatUsage.ReadPixels);

            var selected = LatestFramePipelinePolicy.SelectBodyReadbackPath(
                enableDirectBodyCpuReadback,
                providerReady,
                hasDownscaledBodyRenderTexture,
                sourceDimensionsMatchTextureFrame,
                bodyResourcesCurrent,
                flipHorizontally,
                flipVertically,
                flipStagingAvailable,
                formatSupported,
                _directBodyCpuReadbackSessionAvailable);
            if (selected != BodyReadbackPath.DirectFallback)
            {
                return selected;
            }

            if (!_directBodyCpuReadbackSessionAvailable)
            {
                fallbackReason = string.IsNullOrEmpty(_directBodyCpuReadbackFallbackReason)
                    ? "session unavailable"
                    : _directBodyCpuReadbackFallbackReason;
            }
            else if (!providerReady)
            {
                fallbackReason = "provider not Ready";
            }
            else if (!hasDownscaledBodyRenderTexture)
            {
                fallbackReason = "body input is not persistent scaled RT";
            }
            else if (!sourceDimensionsMatchTextureFrame)
            {
                fallbackReason = "body RT/frame dimensions differ";
            }
            else if (!bodyResourcesCurrent)
            {
                fallbackReason = "body resources changed";
            }
            else if (flipRequired && !flipStagingAvailable)
            {
                fallbackReason = "direct flip staging unavailable";
            }
            else if (!formatSupported)
            {
                fallbackReason = "RGBA32 ReadPixels unsupported";
            }
            else
            {
                fallbackReason = "direct path ineligible";
            }

            return selected;
        }

        private void MarkDirectBodyCpuReadbackUnavailable(string reason)
        {
            _directBodyCpuReadbackSessionAvailable = false;
            _activeBodyReadbackPath = BodyReadbackPath.DirectFallback;
            _activeDirectReadbackStage = DirectReadbackStage.None;
            _directBodyCpuReadbackFallbackReason = reason;
        }

        private long BeginDirectReadbackDiagnostic()
        {
            var serial = Interlocked.Increment(ref _directReadbackDiagnosticSerial);
            Volatile.Write(ref _directReadbackCallbackSerial, 0L);
            Interlocked.Exchange(ref _directReadbackCallbackEnterTicks, 0L);
            Interlocked.Exchange(ref _directReadbackCallbackExitTicks, 0L);
            return serial;
        }

        private void RecordDirectReadbackCallback(long serial, AsyncGPUReadbackRequest completedRequest)
        {
            _ = completedRequest;
            var enterTicks = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref _directReadbackCallbackEnterTicks, enterTicks);
            var exitTicks = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref _directReadbackCallbackExitTicks, exitTicks);
            Volatile.Write(ref _directReadbackCallbackSerial, serial);
        }

        private void RecordDirectReadbackTimingSample(
            long serial,
            long submitStartTicks,
            long submitReturnTicks,
            long coroutineObserveTicks,
            long publishTicks,
            bool isError)
        {
            var callbackSerial = Volatile.Read(ref _directReadbackCallbackSerial);
            var callbackEnterTicks = Interlocked.Read(ref _directReadbackCallbackEnterTicks);
            var callbackExitTicks = Interlocked.Read(ref _directReadbackCallbackExitTicks);
            if (!DirectReadbackTimingMath.TryCreateSample(
                    serial,
                    callbackSerial,
                    submitStartTicks,
                    submitReturnTicks,
                    callbackEnterTicks,
                    callbackExitTicks,
                    coroutineObserveTicks,
                    publishTicks,
                    isError,
                    out var sample))
            {
                _directReadbackTimingWindow.RecordMissingSample();
                return;
            }

            _directReadbackTimingWindow.Add(sample);
        }

        private void RecordDirectReadbackTerminalWithoutPublish(long serial)
        {
            if (Volatile.Read(ref _directReadbackCallbackSerial) == serial &&
                Interlocked.Read(ref _directReadbackCallbackEnterTicks) > 0L &&
                Interlocked.Read(ref _directReadbackCallbackExitTicks) > 0L)
            {
                _directReadbackTimingWindow.RecordExcludedSample();
            }
            else
            {
                _directReadbackTimingWindow.RecordMissingSample();
            }
        }

        private void TrackActiveDirectReadbackRequest(AsyncGPUReadbackRequest request)
        {
            _activeDirectReadbackRequest = request;
            _activeDirectReadbackRequestValid = true;
        }

        private void ClearActiveDirectReadbackRequestIfTerminal(bool requestDone)
        {
            if (!LatestFramePipelinePolicy.DirectReadbackOwnershipCanClear(
                    _activeDirectReadbackRequestValid,
                    requestDone))
            {
                return;
            }

            _activeDirectReadbackRequest = default;
            _activeDirectReadbackRequestValid = false;
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
            var directRequest = false;
            var directTimedOut = false;
            var directDiagnosticSerial = 0L;
            var directSubmitStartTicks = 0L;
            var directSubmitReturnTicks = 0L;
            try
            {
                Texture readbackSource = _webCamTexture;
                if (_bodyInferenceRenderTexture != null)
                {
                    Graphics.Blit(_webCamTexture, _bodyInferenceRenderTexture);
                    readbackSource = _bodyInferenceRenderTexture;
                }

                var selectedReadbackPath = SelectBodyReadbackPath(
                    textureFrame,
                    flipHorizontally,
                    flipVertically,
                    out var fallbackReason);
                _activeBodyReadbackPath = selectedReadbackPath;
                _activeDirectReadbackStage = DirectReadbackStage.None;
                _directBodyCpuReadbackFallbackReason =
                    selectedReadbackPath == BodyReadbackPath.DirectFallback
                        ? fallbackReason
                        : string.Empty;

                Texture directReadbackSource = readbackSource;
                if (selectedReadbackPath == BodyReadbackPath.DirectCPU)
                {
                    var stage = LatestFramePipelinePolicy.GetDirectReadbackStage(
                        flipHorizontally,
                        flipVertically);
                    if (stage != DirectReadbackStage.None)
                    {
                        if (!DirectReadbackStagingMatchesBodyInput())
                        {
                            _activeBodyReadbackPath = BodyReadbackPath.DirectFallback;
                            _directBodyCpuReadbackFallbackReason = "direct flip staging unavailable";
                        }
                        else
                        {
                            LatestFramePipelinePolicy.GetDirectReadbackFlipTransform(
                                flipHorizontally,
                                flipVertically,
                                out var scale,
                                out var offset);
                            Graphics.Blit(
                                _bodyInferenceRenderTexture,
                                _directBodyReadbackStagingRenderTexture,
                                scale,
                                offset);
                            directReadbackSource = _directBodyReadbackStagingRenderTexture;
                            _activeDirectReadbackStage = stage;
                        }
                    }
                }

                if (_activeBodyReadbackPath == BodyReadbackPath.DirectCPU)
                {
                    var rawData = textureFrame.GetRawTextureData<byte>();
                    var expectedByteCount = (long)textureFrame.width * textureFrame.height * 4L;
                    if (rawData.Length != expectedByteCount)
                    {
                        _activeBodyReadbackPath = BodyReadbackPath.DirectFallback;
                        _activeDirectReadbackStage = DirectReadbackStage.None;
                        _directBodyCpuReadbackFallbackReason = "RGBA32 raw buffer size mismatch";
                        request = textureFrame.ReadTextureAsync(
                            readbackSource,
                            flipHorizontally,
                            flipVertically);
                    }
                    else
                    {
                        directDiagnosticSerial = BeginDirectReadbackDiagnostic();
                        directSubmitStartTicks = Stopwatch.GetTimestamp();
                        request = AsyncGPUReadback.RequestIntoNativeArray(
                            ref rawData,
                            directReadbackSource,
                            0,
                            TextureFormat.RGBA32,
                            completedRequest => RecordDirectReadbackCallback(
                                directDiagnosticSerial,
                                completedRequest));
                        directSubmitReturnTicks = Stopwatch.GetTimestamp();
                        TrackActiveDirectReadbackRequest(request);
                        directRequest = true;
                        _directReadbacksInWindow++;
                    }
                }
                else
                {
                    request = textureFrame.ReadTextureAsync(
                        readbackSource,
                        flipHorizontally,
                        flipVertically);
                }
            }
            catch (Exception exception)
            {
                textureFrame.Release();
                _readbackPending = false;
                _readbackFailuresInWindow++;
                _skippedInWindow++;
                if (directRequest || _activeBodyReadbackPath == BodyReadbackPath.DirectCPU)
                {
                    _directReadbackFailuresInWindow++;
                    MarkDirectBodyCpuReadbackUnavailable(
                        $"direct request exception: {exception.GetType().Name}");
                    StatusMessage = $"Direct CPU readback unavailable; using Homuler next frame ({exception.GetType().Name})";
                }
                else
                {
                    SetFailure(PoseProviderStatus.InferenceFailed, $"Camera readback setup failed: {exception.Message}");
                    UnityEngine.Debug.LogException(exception, this);
                }
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + Mathf.Max(0.25f, readbackTimeoutSeconds);
            if (directRequest)
            {
                while (!request.done)
                {
                    if (!directTimedOut && Time.realtimeSinceStartup >= deadline)
                    {
                        directTimedOut = true;
                        _readbackTimeoutsInWindow++;
                        _directReadbackFailuresInWindow++;
                        MarkDirectBodyCpuReadbackUnavailable("direct request timed out");
                        StatusMessage = "Direct CPU readback timed out; holding buffer until completion, then using Homuler";
                    }
                    yield return null;
                }
            }
            else
            {
                while (!request.done && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
            }

            var requestDoneAtCoroutineObservation = request.done;
            var coroutineObserveTicks = Stopwatch.GetTimestamp();
            if (directRequest)
            {
                ClearActiveDirectReadbackRequestIfTerminal(requestDoneAtCoroutineObservation);
            }

            LastGpuReadbackDurationMilliseconds = (float)((NowSeconds() - readbackStartedAtSeconds) * 1000d);

            if (directRequest && directTimedOut)
            {
                RecordDirectReadbackTerminalWithoutPublish(directDiagnosticSerial);
                textureFrame.Release();
                _readbackPending = false;
                _skippedInWindow++;
                yield break;
            }

            if (!requestDoneAtCoroutineObservation || request.hasError)
            {
                if (directRequest)
                {
                    RecordDirectReadbackTerminalWithoutPublish(directDiagnosticSerial);
                }
                textureFrame.Release();
                _readbackPending = false;
                _skippedInWindow++;
                if (request.hasError)
                {
                    _readbackFailuresInWindow++;
                    if (directRequest)
                    {
                        _directReadbackFailuresInWindow++;
                        MarkDirectBodyCpuReadbackUnavailable("direct request returned error");
                        StatusMessage = "Direct CPU readback failed; using Homuler on subsequent frames";
                    }
                    else
                    {
                        StatusMessage = "Camera frame readback failed; retrying";
                    }
                }
                else
                {
                    _readbackTimeoutsInWindow++;
                    StatusMessage = "Camera frame readback timed out; retrying";
                }
                yield break;
            }

            if (_shuttingDown || coordinateConventionVersion != _coordinateConventionVersion || !IsActiveBackendReady())
            {
                if (directRequest)
                {
                    RecordDirectReadbackTerminalWithoutPublish(directDiagnosticSerial);
                }
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
            Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 1);
            _preparedFrameObservedAtSeconds = frameObservedAtSeconds;
            _preparedCoordinateConventionVersion = coordinateConventionVersion;
            _preparedFramePublishedAtSeconds = NowSeconds();
            _preparedFramePublishedFrameCount = Time.frameCount;
            if (directRequest)
            {
                RecordDirectReadbackTimingSample(
                    directDiagnosticSerial,
                    directSubmitStartTicks,
                    directSubmitReturnTicks,
                    coroutineObserveTicks,
                    Stopwatch.GetTimestamp(),
                    isError: false);
            }
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
                IsActiveBackendReady(),
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

            // I2: callback entry. Keep this diagnostic-only and safe for an uncertain callback thread.
            var callbackEntryTicks = Stopwatch.GetTimestamp();
            var preparedWaitingAtCallbackEntry = Volatile.Read(ref _preparedTextureFrameDiagnosticOccupied) != 0;

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
            // I3: timestamp immediately adjacent to the existing outstanding-clear operation.
            var completionTicks = Stopwatch.GetTimestamp();
            var preparedWaitingAtCompletion = Volatile.Read(ref _preparedTextureFrameDiagnosticOccupied) != 0;
            PublishInferenceCompletionDiagnostic(
                callbackEntryTicks,
                completionTicks,
                preparedWaitingAtCallbackEntry || preparedWaitingAtCompletion,
                timestampMillisec);
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
            if (RestartProvider(invalidateCameraSession: true))
            {
                StatusMessage = $"Switching camera: {targetName}";
            }
        }

        private bool RestartProvider(bool invalidateCameraSession)
        {
            if (!CleanupRuntime())
            {
                SetFailure(
                    PoseProviderStatus.InferenceFailed,
                    "Provider restart blocked because an active DirectCPU readback could not be completed safely");
                return false;
            }

            ClearProviderSessionState();
            if (invalidateCameraSession)
            {
                _coordinateConventionVersion++;
            }
            Status = PoseProviderStatus.Starting;
            _bootstrapCoroutine = StartCoroutine(BootstrapAsync());
            return true;
        }

        private void ClearProviderSessionState()
        {
            Interlocked.Increment(ref _inferenceDiagnosticSessionId);
            ResetInferenceContinuationDiagnostics();
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
            _directReadbacksInWindow = 0;
            _directReadbackFailuresInWindow = 0;
            _directReadbackTimingWindow.Reset();
            Volatile.Write(ref _directReadbackCallbackSerial, 0L);
            Interlocked.Exchange(ref _directReadbackCallbackEnterTicks, 0L);
            Interlocked.Exchange(ref _directReadbackCallbackExitTicks, 0L);
            _freshCameraFramePending = false;
            _latestFreshFrameObservedAtSeconds = 0d;
            _activeInferenceFrameObservedAtSeconds = 0d;
            _preparedFramePublishedAtSeconds = 0d;
            _preparedFramePublishedFrameCount = -1;
            Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 0);
            _activeBodyReadbackPath = BodyReadbackPath.Homuler;
            _activeDirectReadbackStage = DirectReadbackStage.None;
            _directBodyCpuReadbackSessionAvailable = true;
            _directBodyCpuReadbackFallbackReason = string.Empty;
            lock (_openVinoWorkerFailureGate)
            {
                _openVinoWorkerFailure = null;
            }
            LastOpenVinoManagedInputCopyMilliseconds = 0f;
            LastOpenVinoGraphProcessMilliseconds = 0f;
            LastOpenVinoDetectorInferenceMilliseconds = 0f;
            LastOpenVinoLandmarkInferenceMilliseconds = 0f;
            LastOpenVinoBridgeCopyMilliseconds = 0f;
            LastOpenVinoDetectorRan = false;
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
            DirectReadbacksPerSecond = 0f;
            DirectReadbackFailuresPerSecond = 0f;
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
            DirectReadbacksPerSecond = (float)(_directReadbacksInWindow / elapsed);
            DirectReadbackFailuresPerSecond = (float)(_directReadbackFailuresInWindow / elapsed);
            SkippedInferenceOpportunities += _skippedInWindow;

            if (Status == PoseProviderStatus.Ready && !_cameraSwitchPending)
            {
                var fallbackSuffix =
                    _activeBodyReadbackPath == BodyReadbackPath.DirectFallback &&
                    !string.IsNullOrEmpty(_directBodyCpuReadbackFallbackReason)
                        ? $" ({_directBodyCpuReadbackFallbackReason})"
                        : string.Empty;
                var readbackPathSummary =
                    _activeBodyReadbackPath == BodyReadbackPath.DirectCPU
                        ? $"DirectCPU stage={ActiveDirectReadbackStageLabel}"
                        : ActiveBodyReadbackPathLabel;
                var directTimingSummary = _activeBodyReadbackPath == BodyReadbackPath.DirectCPU
                    ? $"\nDirect cb: n={DirectReadbackTimingSampleCount} " +
                      $"submit→cb={FormatTimingPair(DirectReadbackTimingSubmitToCallbackMedianMilliseconds, DirectReadbackTimingSubmitToCallbackP95Milliseconds)}ms " +
                      $"cb→poll={FormatTimingPair(DirectReadbackTimingCallbackToPollMedianMilliseconds, DirectReadbackTimingCallbackToPollP95Milliseconds)}ms " +
                      $"poll→pub={FormatTimingPair(DirectReadbackTimingPollToPublishMedianMilliseconds, DirectReadbackTimingPollToPublishP95Milliseconds)}ms"
                    : string.Empty;
                var inferenceContinuationTimingSummary =
                    $"\nInf next: n={InferenceContinuationTimingSampleCount} " +
                    $"prep={InferenceContinuationTimingPreparedWaitingSampleCount}/{InferenceContinuationTimingSampleCount} " +
                    $"({FormatTimingRate(InferenceContinuationTimingPreparedWaitingRate)}) " +
                    $"cb→next={FormatTimingPair(InferenceContinuationTimingPreparedResultToNextLaunchMedianMilliseconds, InferenceContinuationTimingPreparedResultToNextLaunchP95Milliseconds)}ms " +
                    $"U/RB={InferenceContinuationTimingUpdateLaunchCount}/{InferenceContinuationTimingReadbackLaunchCount} " +
                    $"missing={InferenceContinuationTimingMissingSampleCount}";
                var openVinoTimingSummary = _activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32
                    ? $"\nOV ms managed/graph/det/lmk/bridge: {LastOpenVinoManagedInputCopyMilliseconds:0.0}/{LastOpenVinoGraphProcessMilliseconds:0.0}/{LastOpenVinoDetectorInferenceMilliseconds:0.0}/{LastOpenVinoLandmarkInferenceMilliseconds:0.0}/{LastOpenVinoBridgeCopyMilliseconds:0.0} detRan={(LastOpenVinoDetectorRan ? 1 : 0)}"
                    : string.Empty;
                StatusMessage =
                    $"Camera/Pose ready | Backend: {ActiveInferenceBackendLabel} | Body input: {BodyInferenceWidth}x{BodyInferenceHeight} {(BodyInferenceUsesScaledTexture ? "scaled" : "native")} | Pipe ms RB/build/detect/~F→R: {LastGpuReadbackDurationMilliseconds:0.0}/{LastCpuImageBuildDurationMilliseconds:0.0}/{LastInferenceDurationMilliseconds:0.0}/{LastApproxFrameToResultMilliseconds:0.0}\n" +
                    $"Prep→launch: {LastPreparedToInferenceLaunchMilliseconds:0.0} ms Δf={LastPreparedToInferenceLaunchFrameDelta} origin={LastAcceptedLaunchOriginLabel} fast={ImmediateLaunchesPerSecond:0.0}/s\n" +
                    $"Readback: {readbackPathSummary} H={(FlipInputHorizontally ? 1 : 0)} V={(FlipInputVertically ? 1 : 0)} direct={DirectReadbacksPerSecond:0.0}/s fail={DirectReadbackFailuresPerSecond:0.0}/s{fallbackSuffix}\n" +
                    $"Wait/s noFresh/int/RB/inf/pool: {NoFreshFrameWaitsPerSecond:0.0}/{TargetIntervalWaitsPerSecond:0.0}/{ReadbackBusyWaitsPerSecond:0.0}/{InferenceBusyWaitsPerSecond:0.0}/{TextureFramePoolWaitsPerSecond:0.0}   RB fail/to={ReadbackFailuresPerSecond:0.0}/{ReadbackTimeoutsPerSecond:0.0}   replace={PreparedFrameReplacementsPerSecond:0.0}/s   cb={ResultCallbacksPerSecond:0.0}/s" +
                    inferenceContinuationTimingSummary +
                    directTimingSummary +
                    openVinoTimingSummary;
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
            _directReadbacksInWindow = 0;
            _directReadbackFailuresInWindow = 0;
            _metricsWindowStartedAtSeconds = now;
        }

        private static string FormatTimingPair(double medianMilliseconds, double p95Milliseconds)
        {
            return double.IsNaN(medianMilliseconds) || double.IsNaN(p95Milliseconds)
                ? "-/-"
                : $"{medianMilliseconds:0.0}/{p95Milliseconds:0.0}";
        }

        private static string FormatTimingRate(double rate)
        {
            return double.IsNaN(rate) ? "-" : $"{rate:0%}";
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

        private readonly struct InferenceDiagnosticCompletion
        {
            public InferenceDiagnosticCompletion(
                long serial,
                int sessionId,
                long submitStartTicks,
                long submitReturnTicks,
                long callbackEntryTicks,
                long completionTicks,
                bool preparedWaitingAtResult)
            {
                Serial = serial;
                SessionId = sessionId;
                SubmitStartTicks = submitStartTicks;
                SubmitReturnTicks = submitReturnTicks;
                CallbackEntryTicks = callbackEntryTicks;
                CompletionTicks = completionTicks;
                PreparedWaitingAtResult = preparedWaitingAtResult;
            }

            public long Serial { get; }
            public int SessionId { get; }
            public long SubmitStartTicks { get; }
            public long SubmitReturnTicks { get; }
            public long CallbackEntryTicks { get; }
            public long CompletionTicks { get; }
            public bool PreparedWaitingAtResult { get; }
            public bool IsAvailable => Serial > 0;
        }

        private void ResetInferenceContinuationDiagnostics()
        {
            _inferenceContinuationTimingWindow.Reset();
            Volatile.Write(ref _hasCompletedInferenceDiagnostic, 0);
            Volatile.Write(ref _activeInferenceDiagnosticAccepted, 0);
            Interlocked.Exchange(ref _activeInferenceDiagnosticSerial, 0L);
            Interlocked.Exchange(ref _activeInferenceDiagnosticSessionId, 0);
            Interlocked.Exchange(ref _activeInferenceDiagnosticTimestampMilliseconds, 0L);
            Interlocked.Exchange(ref _activeInferenceDiagnosticSubmitStartTicks, 0L);
            Interlocked.Exchange(ref _activeInferenceDiagnosticSubmitReturnTicks, 0L);
            Interlocked.Exchange(ref _completedInferenceDiagnosticSerial, 0L);
            Interlocked.Exchange(ref _completedInferenceDiagnosticSessionId, 0);
            Interlocked.Exchange(ref _completedInferenceDiagnosticSubmitStartTicks, 0L);
            Interlocked.Exchange(ref _completedInferenceDiagnosticSubmitReturnTicks, 0L);
            Interlocked.Exchange(ref _completedInferenceDiagnosticCallbackEntryTicks, 0L);
            Interlocked.Exchange(ref _completedInferenceDiagnosticCompletionTicks, 0L);
            Interlocked.Exchange(ref _completedInferenceDiagnosticPreparedWaiting, 0);
            _lastRecordedInferenceDiagnosticSerial = 0L;
            Interlocked.Exchange(ref _inferenceDiagnosticMissingSamples, 0);
        }

        private InferenceDiagnosticCompletion CapturePendingInferenceCompletion()
        {
            if (Volatile.Read(ref _hasCompletedInferenceDiagnostic) == 0)
            {
                return default;
            }

            var currentSessionId = Volatile.Read(ref _inferenceDiagnosticSessionId);
            var sessionId = Interlocked.CompareExchange(ref _completedInferenceDiagnosticSessionId, 0, 0);
            var serial = Interlocked.Read(ref _completedInferenceDiagnosticSerial);
            if (serial <= 0 || sessionId != currentSessionId || serial == _lastRecordedInferenceDiagnosticSerial)
            {
                return default;
            }

            return new InferenceDiagnosticCompletion(
                serial,
                sessionId,
                Interlocked.Read(ref _completedInferenceDiagnosticSubmitStartTicks),
                Interlocked.Read(ref _completedInferenceDiagnosticSubmitReturnTicks),
                Interlocked.Read(ref _completedInferenceDiagnosticCallbackEntryTicks),
                Interlocked.Read(ref _completedInferenceDiagnosticCompletionTicks),
                Volatile.Read(ref _completedInferenceDiagnosticPreparedWaiting) != 0);
        }

        private void PublishInferenceCompletionDiagnostic(
            long callbackEntryTicks,
            long completionTicks,
            bool preparedWaitingAtResult,
            long callbackTimestampMilliseconds)
        {
            var currentSessionId = Volatile.Read(ref _inferenceDiagnosticSessionId);
            var sessionId = Interlocked.CompareExchange(ref _activeInferenceDiagnosticSessionId, 0, 0);
            var serial = Interlocked.Read(ref _activeInferenceDiagnosticSerial);
            var activeTimestampMilliseconds = Interlocked.Read(ref _activeInferenceDiagnosticTimestampMilliseconds);
            if (Volatile.Read(ref _activeInferenceDiagnosticAccepted) == 0 ||
                serial <= 0 || sessionId != currentSessionId ||
                callbackTimestampMilliseconds != activeTimestampMilliseconds)
            {
                Interlocked.Increment(ref _inferenceDiagnosticMissingSamples);
                return;
            }

            if (Volatile.Read(ref _hasCompletedInferenceDiagnostic) != 0 &&
                Interlocked.Read(ref _completedInferenceDiagnosticSerial) == serial)
            {
                Interlocked.Increment(ref _inferenceDiagnosticMissingSamples);
                return;
            }

            Interlocked.Exchange(ref _completedInferenceDiagnosticSerial, serial);
            Interlocked.Exchange(ref _completedInferenceDiagnosticSessionId, sessionId);
            Interlocked.Exchange(
                ref _completedInferenceDiagnosticSubmitStartTicks,
                Interlocked.Read(ref _activeInferenceDiagnosticSubmitStartTicks));
            Interlocked.Exchange(
                ref _completedInferenceDiagnosticSubmitReturnTicks,
                Interlocked.Read(ref _activeInferenceDiagnosticSubmitReturnTicks));
            Interlocked.Exchange(ref _completedInferenceDiagnosticCallbackEntryTicks, callbackEntryTicks);
            Interlocked.Exchange(ref _completedInferenceDiagnosticCompletionTicks, completionTicks);
            Volatile.Write(ref _completedInferenceDiagnosticPreparedWaiting, preparedWaitingAtResult ? 1 : 0);
            Volatile.Write(ref _hasCompletedInferenceDiagnostic, 1);
        }

        private void RecordInferenceContinuationSample(
            InferenceDiagnosticCompletion completion,
            int nextLaunchSessionId,
            long nextAcceptedLaunchTicks,
            PreparedInferenceLaunchOrigin nextLaunchOrigin)
        {
            if (!completion.IsAvailable || completion.Serial == _lastRecordedInferenceDiagnosticSerial)
            {
                return;
            }

            var recorded = InferenceContinuationTimingMath.TryCreateSample(
                nextLaunchSessionId,
                completion.SessionId,
                completion.Serial,
                completion.Serial,
                completion.SubmitStartTicks,
                completion.SubmitReturnTicks,
                completion.CallbackEntryTicks,
                completion.CompletionTicks,
                nextAcceptedLaunchTicks,
                completion.PreparedWaitingAtResult,
                nextLaunchOrigin,
                out var sample);
            if (recorded)
            {
                _inferenceContinuationTimingWindow.Add(sample);
            }
            else
            {
                _inferenceContinuationTimingWindow.RecordMissingSample();
            }

            _lastRecordedInferenceDiagnosticSerial = completion.Serial;
        }

        private void ReleasePreparedFrame()
        {
            Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 0);
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

        private bool EnsureActiveDirectReadbackCompletedBeforeTeardown()
        {
            if (!_activeDirectReadbackRequestValid)
            {
                return true;
            }

            try
            {
                var request = _activeDirectReadbackRequest;
                var requestDone = request.done;
                if (LatestFramePipelinePolicy.DirectReadbackCleanupRequiresWait(
                        activeRequestValid: true,
                        requestDone: requestDone))
                {
                    UnityEngine.Debug.Log(
                        "[PoseTrackingSpike] Waiting for active DirectCPU readback before body resource teardown");
                    request.WaitForCompletion();
                    requestDone = request.done;
                }

                if (!LatestFramePipelinePolicy.DirectReadbackOwnershipCanClear(
                        activeRequestValid: true,
                        requestDone: requestDone))
                {
                    UnityEngine.Debug.LogError(
                        "[PoseTrackingSpike] DirectCPU teardown guard could not prove readback completion; body readback resources will be retained");
                    return false;
                }

                _activeDirectReadbackRequest = default;
                _activeDirectReadbackRequestValid = false;
                return true;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError(
                    $"[PoseTrackingSpike] DirectCPU teardown wait failed ({exception.GetType().Name}); body readback resources will be retained");
                return false;
            }
        }


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

        private bool CleanupRuntime()
        {
            _shuttingDown = true;
            _freshCameraFramePending = false;
            var directReadbackTeardownSafe = EnsureActiveDirectReadbackCompletedBeforeTeardown();
            ReleasePreparedFrame();
            CleanupOpenVinoRuntime();

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

            if (directReadbackTeardownSafe)
            {
                _textureFramePool?.Dispose();
                _textureFramePool = null;
                ReleaseBodyInferenceRenderTexture();
                _bodyInferenceWidth = 0;
                _bodyInferenceHeight = 0;
                _configuredCameraWidth = 0;
                _configuredCameraHeight = 0;
                _readbackPending = false;
            }
            else
            {
                StatusMessage = "Cleanup retained DirectCPU body resources because GPU readback completion was not confirmed";
            }

            StopCamera();
            _inferenceScheduler.Reset();
            Interlocked.Exchange(ref _inferenceOutstanding, 0);
            return directReadbackTeardownSafe;
        }

        private void ReleaseBodyInferenceRenderTexture()
        {
            ReleaseRenderTexture(ref _directBodyReadbackStagingRenderTexture);
            ReleaseRenderTexture(ref _bodyInferenceRenderTexture);
        }

        private void ReleaseRenderTexture(ref RenderTexture renderTexture)
        {
            if (renderTexture == null)
            {
                return;
            }

            if (renderTexture.IsCreated())
            {
                renderTexture.Release();
            }

            Destroy(renderTexture);
            renderTexture = null;
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
