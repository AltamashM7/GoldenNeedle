using GoldenNeedle.Core.Motion.Canonical;

namespace GoldenNeedle.Core.Motion.Hands
{
    public interface ICanonicalHandSource
    {
        string HandSourceId { get; }
        string HandDiagnosticSummary { get; }
        bool TryCopyLatestCanonicalHands(
            CanonicalPoseFrame latestBodyPose,
            double evaluationTimeSeconds,
            CanonicalHandFrame destination);
    }
}
