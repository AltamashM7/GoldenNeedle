using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Runtime;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    /// <summary>
    /// MediaPipe-specific adapter. It is the only runtime source that knows both the provider and
    /// the MediaPipe-to-canonical mapper; MotionEngineRuntime sees only ICanonicalPoseSource.
    /// </summary>
    [RequireComponent(typeof(MediaPipePoseProvider))]
    public sealed class MediaPipeCanonicalPoseSource : MonoBehaviour, ICanonicalPoseSource
    {
        [SerializeField] private MediaPipePoseProvider provider;

        private readonly PoseObservation _observation = new PoseObservation();

        public MediaPipePoseProvider Provider => provider;
        public PoseObservation LatestObservation => _observation;
        public int CoordinateConventionVersion => provider == null ? 0 : provider.CoordinateConventionVersion;

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
    }
}
