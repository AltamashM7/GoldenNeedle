using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Rich;
using GoldenNeedle.Core.Motion.Runtime;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    /// <summary>
    /// MediaPipe-specific adapter. It owns one persistent 33-landmark provider observation and
    /// exposes both the accepted V1 canonical source and the optional additive rich-evidence source.
    /// Both mappings reuse the exact same latest observation; no second inference or frame queue is
    /// introduced by the rich capability.
    /// </summary>
    [RequireComponent(typeof(MediaPipePoseProvider))]
    public sealed class MediaPipeCanonicalPoseSource : MonoBehaviour, ICanonicalPoseSource, IRichMotionEvidenceSource
    {
        [SerializeField] private MediaPipePoseProvider provider;

        private readonly PoseObservation _observation = new PoseObservation();

        public MediaPipePoseProvider Provider => provider;
        public PoseObservation LatestObservation => _observation;
        public int CoordinateConventionVersion => provider == null ? 0 : provider.CoordinateConventionVersion;
        public string RichMotionSourceId => MediaPipeRichMotionEvidenceMapper.SourceProviderId;

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
    }
}
