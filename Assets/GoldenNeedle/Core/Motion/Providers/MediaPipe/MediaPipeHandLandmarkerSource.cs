using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Hands;
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    [Serializable]
    public struct MediaPipeHandPointSnapshot
    {
        public bool valid;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class MediaPipeHandDetectionSnapshot
    {
        public readonly MediaPipeHandPointSnapshot[] normalized =
            new MediaPipeHandPointSnapshot[CanonicalHandSchema.LandmarkCount];
        public readonly MediaPipeHandPointSnapshot[] handLocal =
            new MediaPipeHandPointSnapshot[CanonicalHandSchema.LandmarkCount];

        public long sourceTimestampMillisec;
        public int landmarkCount;
        public CanonicalHandSide handedness;
        public float handednessScore;

        public void Clear()
        {
            sourceTimestampMillisec = 0;
            landmarkCount = 0;
            handedness = CanonicalHandSide.Unknown;
            handednessScore = 0f;
            Array.Clear(normalized, 0, normalized.Length);
            Array.Clear(handLocal, 0, handLocal.Length);
        }

        public void CopyFrom(MediaPipeHandDetectionSnapshot source)
        {
            sourceTimestampMillisec = source.sourceTimestampMillisec;
            landmarkCount = source.landmarkCount;
            handedness = source.handedness;
            handednessScore = source.handednessScore;
            Array.Copy(source.normalized, normalized, normalized.Length);
            Array.Copy(source.handLocal, handLocal, handLocal.Length);
        }
    }

    internal sealed class MediaPipeHandResultSnapshot
    {
        public readonly MediaPipeHandDetectionSnapshot[] hands =
        {
            new MediaPipeHandDetectionSnapshot(),
            new MediaPipeHandDetectionSnapshot(),
        };

        public int sessionVersion;
        public long sourceTimestampMillisec;
        public int handCount;
        public long callbackStopwatchTicks;

        public void Clear()
        {
            sessionVersion = 0;
            sourceTimestampMillisec = 0;
            handCount = 0;
            callbackStopwatchTicks = 0;
            hands[0].Clear();
            hands[1].Clear();
        }

        public void CopyFrom(MediaPipeHandResultSnapshot source)
        {
            sessionVersion = source.sessionVersion;
            sourceTimestampMillisec = source.sourceTimestampMillisec;
            handCount = source.handCount;
            callbackStopwatchTicks = source.callbackStopwatchTicks;
            hands[0].CopyFrom(source.hands[0]);
            hands[1].CopyFrom(source.hands[1]);
        }
    }

    /// <summary>
    /// Optional secondary Hand Landmarker stream. It never owns or mutates body buffers/mailboxes.
    /// Camera pixels are sampled only at the independently throttled hand cadence into dedicated buffers.
    /// </summary>
    [DefaultExecutionOrder(-95)]
    [RequireComponent(typeof(MediaPipePoseProvider))]
    public sealed partial class MediaPipeHandLandmarkerSource : MonoBehaviour
    {
        public const string SourceProviderId = "MediaPipe.HandLandmarker.CPU";
        public const string OfficialModelFileName = "hand_landmarker.task";
        public const string OfficialModelVersion = "float16/1";
        public const string OfficialModelUrl = "https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task";
        public const string OfficialModelSha256 = "fbc2a30080c3c557093b5ddfc334698132eb341044ccee322ccf8bcf3607cde1";

        [Header("Foundation D Hands")]
        [SerializeField] private bool handTrackingEnabled = true;
        [SerializeField, Range(5f, 30f)] private float targetHandInferenceFps = 12f;
        [SerializeField, Min(64)] private int handInputWidth = 480;
        [SerializeField, Min(64)] private int handInputHeight = 360;
        [SerializeField, Range(0f, 1f)] private float minimumDetectionConfidence = 0.5f;
        [SerializeField, Range(0f, 1f)] private float minimumPresenceConfidence = 0.5f;
        [SerializeField, Range(0f, 1f)] private float minimumTrackingConfidence = 0.5f;
        [SerializeField] private HandFreshnessSettings freshnessSettings = default;
        [SerializeField, Min(250f)] private float activeRequestTimeoutMilliseconds = 1500f;

        [Header("Foundation D Diagnostics")]
        [SerializeField, TextArea(2, 5)] private string diagnosticSummary = "Hands: not initialized";

        private MediaPipePoseProvider _bodyProvider;
        private HandLandmarker _landmarker;
        private string _verifiedModelPath;
        private ImageProcessingOptions _imageProcessingOptions;
        private Color32[] _capturePixels;
        private NativeArray<byte> _slotA;
        private NativeArray<byte> _slotB;
        private int _activeSlot = -1;
        private int _pendingSlot = -1;
        private int _lastCoordinateConventionVersion = -1;
        private int _sessionVersion;
        private bool _started;
        private bool _initializing;
        private bool _modelReady;
        private double _nextCaptureTimeSeconds;
        private long _lastSubmittedTimestampMillisec;
        private long _activeSubmittedTimestampMillisec;
        private long _activeStartedStopwatchTicks;
        private long _completedSequence;
        private double _lastAcquisitionMilliseconds;
        private double _lastInferenceMilliseconds;
        private double _lastResultReceivedAtSeconds;
        private int _submittedCount;
        private int _callbackCount;
        private int _droppedCaptureCount;
        private int _sessionResetCount;
        private int _latestDetectedHandCount;
        private double _rateWindowStartSeconds;
        private int _rateWindowSubmittedStart;
        private int _rateWindowCallbackStart;
        private float _actualSubmissionRate;
        private float _actualCallbackRate;
        private readonly BoundedHandInferenceScheduler _scheduler = new BoundedHandInferenceScheduler();
        private readonly object _resultLock = new object();
        private readonly MediaPipeHandResultSnapshot _callbackSnapshot = new MediaPipeHandResultSnapshot();
        private readonly MediaPipeHandResultSnapshot _mainSnapshot = new MediaPipeHandResultSnapshot();
        private bool _hasPendingResult;
        private readonly CanonicalHand _heldLeft = new CanonicalHand(CanonicalHandSide.Left);
        private readonly CanonicalHand _heldRight = new CanonicalHand(CanonicalHandSide.Right);
        private readonly CanonicalHand _mappedFirst = new CanonicalHand(CanonicalHandSide.Left);
        private readonly CanonicalHand _mappedSecond = new CanonicalHand(CanonicalHandSide.Right);

        public bool HandTrackingEnabled => handTrackingEnabled;
        public bool IsReady => handTrackingEnabled && _landmarker != null && _modelReady;
        public float TargetHandInferenceFps => targetHandInferenceFps;
        public double LastAcquisitionMilliseconds => _lastAcquisitionMilliseconds;
        public double LastInferenceMilliseconds => _lastInferenceMilliseconds;
        public int ReplacedPendingCount => _scheduler.ReplacedPendingCount;
        public int TimedOutCount => _scheduler.TimedOutCount;
        public string DiagnosticSummary => diagnosticSummary;
        public HandFreshnessSettings FreshnessSettings => freshnessSettings;

        private void Awake()
        {
            _bodyProvider = GetComponent<MediaPipePoseProvider>();
            if (freshnessSettings.maximumHandAgeMilliseconds <= 0f || freshnessSettings.maximumBodyHandSkewMilliseconds <= 0f)
            {
                freshnessSettings = HandFreshnessSettings.CreateDefault();
            }
            SanitizeSettings();
        }

        private void Start()
        {
            _started = true;
            if (handTrackingEnabled)
            {
                StartCoroutine(EnsureInitialized());
            }
        }

        private void OnValidate()
        {
            SanitizeSettings();
        }

        private void OnDisable()
        {
            ShutdownHandTask(clearModelPath: false);
            ClearSessionState();
        }

        private void OnDestroy()
        {
            ShutdownHandTask(clearModelPath: true);
            DisposeBuffers();
        }

        private void Update()
        {
            if (!handTrackingEnabled)
            {
                diagnosticSummary = "Hands: disabled (body unaffected)";
                return;
            }
            if (_landmarker == null)
            {
                if (_started && !_initializing)
                {
                    StartCoroutine(EnsureInitialized());
                }
                return;
            }
            if (_bodyProvider == null)
            {
                _bodyProvider = GetComponent<MediaPipePoseProvider>();
            }
            if (_bodyProvider == null)
            {
                diagnosticSummary = "Hands: waiting for body camera provider";
                return;
            }

            if (_lastCoordinateConventionVersion != _bodyProvider.CoordinateConventionVersion)
            {
                if (_lastCoordinateConventionVersion >= 0)
                {
                    RestartForCoordinateConvention();
                    return;
                }
                _lastCoordinateConventionVersion = _bodyProvider.CoordinateConventionVersion;
            }

            ProcessCompletedActive();
            if (_scheduler.HasActive && _activeStartedStopwatchTicks > 0)
            {
                var elapsedMs = StopwatchElapsedMilliseconds(_activeStartedStopwatchTicks, Stopwatch.GetTimestamp());
                if (elapsedMs > activeRequestTimeoutMilliseconds)
                {
                    diagnosticSummary = $"Hands: task timeout {elapsedMs:0} ms; restarting optional hand task";
                    RestartTaskAfterTimeout();
                    return;
                }
            }

            var now = Time.unscaledTimeAsDouble;
            if (now < _nextCaptureTimeSeconds)
            {
                UpdateDiagnostics(now);
                return;
            }
            _nextCaptureTimeSeconds = now + 1d / Math.Max(1f, targetHandInferenceFps);
            CaptureLatestHandFrame(now);
            UpdateDiagnostics(now);
        }

        public void SetTrackingEnabled(bool enabled)
        {
            handTrackingEnabled = enabled;
            if (!enabled)
            {
                StopAllCoroutines();
                ShutdownHandTask(clearModelPath: false);
                ClearSessionState();
                diagnosticSummary = "Hands: disabled (body unaffected)";
            }
            else if (_started && _landmarker == null && !_initializing)
            {
                StartCoroutine(EnsureInitialized());
            }
        }

        public bool TryCopyLatestCanonicalHands(
            CanonicalPoseFrame bodyFrame,
            double evaluationTimeSeconds,
            CanonicalHandFrame destination)
        {
            if (destination == null)
            {
                return false;
            }

            ConsumePendingResult(bodyFrame);
            var bodyTimestamp = bodyFrame == null ? 0L : bodyFrame.sourceTimestampMillisec;
            freshnessSettings.Sanitize();
            _heldLeft.EvaluateFreshness(evaluationTimeSeconds, bodyTimestamp, freshnessSettings);
            _heldRight.EvaluateFreshness(evaluationTimeSeconds, bodyTimestamp, freshnessSettings);

            destination.Begin(SourceProviderId, bodyTimestamp, evaluationTimeSeconds, IsReady);
            destination.CopyHand(_heldLeft);
            destination.CopyHand(_heldRight);
            return destination.freshHandCount > 0;
        }

        public void RetryHands()
        {
            if (!handTrackingEnabled)
            {
                return;
            }
            ShutdownHandTask(clearModelPath: false);
            ClearSessionState();
            if (_started && !_initializing)
            {
                StartCoroutine(EnsureInitialized());
            }
        }

        private IEnumerator EnsureInitialized()
        {
            if (_initializing || !handTrackingEnabled)
            {
                yield break;
            }
            _initializing = true;
            diagnosticSummary = "Hands: verifying official Hand Landmarker model";

            var streamingPath = Path.Combine(
                Application.streamingAssetsPath,
                "GoldenNeedle",
                "PoseTrackingSpike",
                "Models",
                OfficialModelFileName);
            var cachedDirectory = Path.Combine(Application.persistentDataPath, "GoldenNeedle", "Models");
            var cachedPath = Path.Combine(cachedDirectory, OfficialModelFileName);

            if (File.Exists(streamingPath) && VerifyFileSha256(streamingPath, OfficialModelSha256))
            {
                _verifiedModelPath = streamingPath;
            }
            else if (File.Exists(cachedPath) && VerifyFileSha256(cachedPath, OfficialModelSha256))
            {
                _verifiedModelPath = cachedPath;
            }
            else
            {
                diagnosticSummary = "Hands: acquiring verified official model (body continues)";
                using var request = UnityWebRequest.Get(OfficialModelUrl);
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success || request.downloadHandler == null)
                {
                    diagnosticSummary = $"Hands: model unavailable ({request.error}); body continues";
                    _initializing = false;
                    yield break;
                }

                var bytes = request.downloadHandler.data;
                if (!VerifyBytesSha256(bytes, OfficialModelSha256))
                {
                    diagnosticSummary = "Hands: official model SHA-256 mismatch; hand feature disabled, body continues";
                    _initializing = false;
                    yield break;
                }

                try
                {
                    Directory.CreateDirectory(cachedDirectory);
                    File.WriteAllBytes(cachedPath, bytes);
                    _verifiedModelPath = cachedPath;
                }
                catch (Exception exception)
                {
                    diagnosticSummary = $"Hands: could not cache verified model ({exception.Message}); body continues";
                    _initializing = false;
                    yield break;
                }
            }

            try
            {
                CreateHandTask();
                _modelReady = true;
                _lastCoordinateConventionVersion = _bodyProvider == null ? -1 : _bodyProvider.CoordinateConventionVersion;
                diagnosticSummary = "Hands: ready";
            }
            catch (Exception exception)
            {
                _modelReady = false;
                ShutdownHandTask(clearModelPath: false);
                diagnosticSummary = $"Hands: initialization failed ({exception.Message}); body continues";
            }
            _initializing = false;
        }

        private void CreateHandTask()
        {
            ShutdownHandTask(clearModelPath: false);
            _sessionVersion++;
            var callbackSession = _sessionVersion;
            var options = new HandLandmarkerOptions(
                new BaseOptions(BaseOptions.Delegate.CPU, modelAssetPath: _verifiedModelPath),
                runningMode: RunningMode.LIVE_STREAM,
                numHands: 2,
                minHandDetectionConfidence: minimumDetectionConfidence,
                minHandPresenceConfidence: minimumPresenceConfidence,
                minTrackingConfidence: minimumTrackingConfidence,
                resultCallback: (result, image, timestampMillisec) => OnHandResult(result, timestampMillisec, callbackSession));
            _landmarker = HandLandmarker.CreateFromOptions(options);
            _scheduler.Reset();
            _completedSequence = 0;
            _activeSlot = -1;
            _pendingSlot = -1;
            _nextCaptureTimeSeconds = Time.unscaledTimeAsDouble;
        }
    }
}
