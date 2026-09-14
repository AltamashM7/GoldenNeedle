namespace GoldenNeedle.Core.Motion.Rich
{
    /// <summary>
    /// Optional additive source capability for provider-independent rich anatomical evidence.
    /// Legacy ICanonicalPoseSource implementations are not required to implement this interface.
    /// </summary>
    public interface IRichMotionEvidenceSource
    {
        string RichMotionSourceId { get; }

        bool TryCopyLatestRichMotionEvidence(RichMotionEvidenceFrame destination);
    }
}
