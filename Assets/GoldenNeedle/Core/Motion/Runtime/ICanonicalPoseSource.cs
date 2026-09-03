using GoldenNeedle.Core.Motion.Canonical;

namespace GoldenNeedle.Core.Motion.Runtime
{
    /// <summary>
    /// Narrow source contract consumed by the Motion Engine runtime. Providers adapt into this
    /// contract before the runtime sees a canonical frame.
    /// </summary>
    public interface ICanonicalPoseSource
    {
        double EvaluationTimeSeconds { get; }

        int CoordinateConventionVersion { get; }

        bool TryCopyLatestCanonicalPose(CanonicalPoseFrame destination);
    }
}
