using System.Diagnostics;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Hands;
using GoldenNeedle.Core.Motion.Rich;
using GoldenNeedle.Core.Motion.Runtime;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    /// <summary>
    /// MediaPipe-specific adapter. It owns one persistent 33-landmark body observation and
    /// exposes the accepted V1 canonical source plus optional additive rich and hand capabilities.
    /// Detailed Foundation D hands are an explicit experimental opt-in; body inference/scheduling
    /// remains owned entirely by MediaPipePoseProvider.
    /// </summary>
    [RequireComponent(typeof(MediaPipePoseProvider))]
    public sealed class MediaPipeCanonicalPoseSource : MonoBehaviour, ICanonicalPoseSource, IRichMotionEvidenceSource, ICanonicalHandSource
    {
        [SerializeField] private MediaPipePoseProvider provider;
        [Header("Foundation D")]
        [Tooltip("Experimental high-detail Hand Landmarker stream. Disabled by default for the low-end baseline.")]
        [SerializeField] private bool enableDetailedHands = false;

        private readonly PoseObservation _observation = new PoseObservation();
        private MediaPipeHandLandmarkerSource _handSource;
        private HandMotionRuntime _handRuntime;
        private Stopwatch _providerTimelineClock;

        public MediaPipePoseProvider Provider => provider;
        public PoseObservation LatestObservation => _observation;
        public int CoordinateConventionVersion => provider == null ? 0 : provider.CoordinateConventionVersion;
        public string RichMotionSourceId => MediaPipeRichMotionEvidenceMapper.SourceProviderId;
        public string HandSourceId => MediaPipeHandLandmarkerSource.SourceProviderId;
        public bool DetailedHandsEnabled => enableDetailedHands;
        public string HandDiagnosticSummary => !enableDetailedHands
            ? "Hands: detailed tracking disabled (experimental)"
            : _handSource == null
                ? "Hands: source not initialized"
                : _handSource.DiagnosticSummary;

        // Rich/body evaluation and Foundation D hand freshness use the same Stopwatch epoch that
        // already timestamps MediaPipePoseProvider inference. There is no Unity-time fallback here.
        public double EvaluationTimeSeconds => _providerTimelineClock == null
            ? 0d
            : _providerTimelineClock.ElapsedTicks / (double)Stopwatch.Frequency;

        private void Awake()
        {
            provider = provider == null ? GetComponent<MediaPipePoseProvider>() : provider;
            MediaPipePoseProviderTimeline.TryGetTimelineClock(provider, out _providerTimelineClock);

            // Keep the lightweight consumer present so Foundation E resolves it once, but do not
            // create the expensive Hand Landmarker source for the normal low-end baseline.
            _handRuntime = GetComponent<HandMotionRuntime>();
            if (_handRuntime == null)
            {
                _handRuntime = gameObject.AddComponent<HandMotionRuntime>();
            }
            _handSource = GetComponent<MediaPipeHandLandmarkerSource>();
            ApplyDetailedHandTrackingState();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && enableDetailedHands)
            {
                ApplyDetailedHandTrackingState();
            }
        }

        private void OnDisable()
        {
            if (_handSource != null)
            {
                _handSource.SetTrackingEnabled(false);
                _handSource.enabled = false;
            }
            if (_handRuntime != null)
            {
                _handRuntime.enabled = false;
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                ApplyDetailedHandTrackingState();
            }
        }

        /// <summary>
        /// Explicit experimental opt-in/out for the detailed MediaPipe Hand Landmarker path.
        /// The normal runtime leaves this disabled.
        /// </summary>
        public void SetDetailedHandsEnabled(bool enabled)
        {
            enableDetailedHands = enabled;
            ApplyDetailedHandTrackingState();
        }

        private void ApplyDetailedHandTrackingState()
        {
            if (!enableDetailedHands)
            {
                if (_handSource != null)
                {
                    _handSource.SetTrackingEnabled(false);
                    _handSource.enabled = false;
                }
                if (_handRuntime != null)
                {
                    _handRuntime.enabled = false;
                }
                return;
            }

            EnsureDetailedHandComponents();
            _handSource.enabled = true;
            _handRuntime.enabled = true;
            _handSource.SetTrackingEnabled(true);
        }

        private void EnsureDetailedHandComponents()
        {
            _handSource = _handSource == null ? GetComponent<MediaPipeHandLandmarkerSource>() : _handSource;
            if (_handSource == null)
            {
                _handSource = gameObject.AddComponent<MediaPipeHandLandmarkerSource>();
            }

            _handRuntime = _handRuntime == null ? GetComponent<HandMotionRuntime>() : _handRuntime;
            if (_handRuntime == null)
            {
                _handRuntime = gameObject.AddComponent<HandMotionRuntime>();
            }
        }

        public bool TryCopyLatestCanonicalPose(CanonicalPoseFrame destination)
        {
            if (destination == null)
            {
                return false;
            }

            if (provider == null)
            {
                destination.Clear();
                return false;
            }

            provider.CopyLatestObservation(_observation);
            MediaPipeCanonicalPoseMapper.Map(_observation, destination);
            return destination.hasMeaningfulPose;
        }

        public bool TryCopyLatestRichMotionEvidence(RichMotionEvidenceFrame destination)
        {
            if (destination == null)
            {
                return false;
            }

            if (provider == null)
            {
                destination.Clear();
                return false;
            }

            // MotionEngineRuntime calls the accepted canonical copy first each frame. That call
            // refreshes _observation once from the provider; rich mapping reuses it verbatim.
            MediaPipeRichMotionEvidenceMapper.Map(
                _observation,
                EvaluationTimeSeconds,
                destination);
            return destination.hasEvidence;
        }

        public bool TryCopyLatestCanonicalHands(
            CanonicalPoseFrame latestBodyPose,
            double evaluationTimeSeconds,
            CanonicalHandFrame destination)
        {
            if (destination == null)
            {
                return false;
            }
            if (!enableDetailedHands || _handSource == null || !_handSource.HandTrackingEnabled)
            {
                destination.Clear();
                return false;
            }
            return _handSource.TryCopyLatestCanonicalHands(
                latestBodyPose,
                evaluationTimeSeconds,
                destination);
        }
    }
}
