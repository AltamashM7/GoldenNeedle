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
    /// Foundation D hands run through a separate independently throttled Hand Landmarker task;
    /// body inference/scheduling remains owned entirely by MediaPipePoseProvider.
    /// </summary>
    [RequireComponent(typeof(MediaPipePoseProvider))]
    public sealed class MediaPipeCanonicalPoseSource : MonoBehaviour, ICanonicalPoseSource, IRichMotionEvidenceSource, ICanonicalHandSource
    {
        [SerializeField] private MediaPipePoseProvider provider;
        [Header("Foundation D")]
        [SerializeField] private bool enableDetailedHands = true;

        private readonly PoseObservation _observation = new PoseObservation();
        private MediaPipeHandLandmarkerSource _handSource;

        public MediaPipePoseProvider Provider => provider;
        public PoseObservation LatestObservation => _observation;
        public int CoordinateConventionVersion => provider == null ? 0 : provider.CoordinateConventionVersion;
        public string RichMotionSourceId => MediaPipeRichMotionEvidenceMapper.SourceProviderId;
        public string HandSourceId => MediaPipeHandLandmarkerSource.SourceProviderId;
        public string HandDiagnosticSummary => _handSource == null
            ? "Hands: source not initialized"
            : _handSource.DiagnosticSummary;

        public double EvaluationTimeSeconds
        {
            get
            {
                if (_observation.receivedAtSeconds > 0d && provider != null && !double.IsInfinity(provider.LatestPoseAgeMilliseconds))
                {
                    var age = provider.LatestPoseAgeMilliseconds > 0d ? provider.LatestPoseAgeMilliseconds : 0d;
                    return _observation.receivedAtSeconds + age * 0.001d;
                }

                return Time.unscaledTimeAsDouble;
            }
        }

        private void Awake()
        {
            provider = provider == null ? GetComponent<MediaPipePoseProvider>() : provider;
            _handSource = GetComponent<MediaPipeHandLandmarkerSource>();
            if (_handSource == null)
            {
                _handSource = gameObject.AddComponent<MediaPipeHandLandmarkerSource>();
            }
            _handSource.SetTrackingEnabled(enableDetailedHands);

            if (GetComponent<HandMotionRuntime>() == null)
            {
                gameObject.AddComponent<HandMotionRuntime>();
            }
        }

        private void OnValidate()
        {
            if (_handSource != null)
            {
                _handSource.SetTrackingEnabled(enableDetailedHands);
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
            if (!enableDetailedHands || _handSource == null)
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
