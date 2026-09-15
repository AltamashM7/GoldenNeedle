using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Hands;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    /// <summary>
    /// Provider boundary for the lightweight coarse-hand capability. Numeric pose-landmark
    /// indices remain confined here; downstream estimation consumes semantic hand evidence only.
    /// </summary>
    public static class MediaPipeCoarseHandEvidenceMapper
    {
        public const string SourceProviderId = "MediaPipePose33.CoarseHands";

        private const int LeftElbow = 13;
        private const int RightElbow = 14;
        private const int LeftWrist = 15;
        private const int RightWrist = 16;
        private const int LeftPinky = 17;
        private const int RightPinky = 18;
        private const int LeftIndex = 19;
        private const int RightIndex = 20;
        private const int LeftThumb = 21;
        private const int RightThumb = 22;

        public static void Map(
            PoseObservation source,
            out CoarseHandPoseEvidence left,
            out CoarseHandPoseEvidence right)
        {
            left = CreateEvidence(source, CanonicalHandSide.Left);
            right = CreateEvidence(source, CanonicalHandSide.Right);
            if (source == null)
            {
                return;
            }

            left.elbow = MapPoint(source.GetLandmark(LeftElbow));
            left.wrist = MapPoint(source.GetLandmark(LeftWrist));
            left.pinky = MapPoint(source.GetLandmark(LeftPinky));
            left.index = MapPoint(source.GetLandmark(LeftIndex));
            left.thumb = MapPoint(source.GetLandmark(LeftThumb));

            right.elbow = MapPoint(source.GetLandmark(RightElbow));
            right.wrist = MapPoint(source.GetLandmark(RightWrist));
            right.pinky = MapPoint(source.GetLandmark(RightPinky));
            right.index = MapPoint(source.GetLandmark(RightIndex));
            right.thumb = MapPoint(source.GetLandmark(RightThumb));
        }

        private static CoarseHandPoseEvidence CreateEvidence(
            PoseObservation source,
            CanonicalHandSide side)
        {
            return new CoarseHandPoseEvidence
            {
                side = side,
                sourceTimestampMillisec = source == null ? 0L : source.sourceTimestampMillisec,
                receivedAtSeconds = source == null ? 0d : source.receivedAtSeconds,
                poseAvailable = source != null && source.hasPose,
            };
        }

        private static CoarseHandEvidencePoint MapPoint(PoseLandmarkObservation source)
        {
            return new CoarseHandEvidencePoint
            {
                isTracked = source.IsTracked,
                hasPosition = source.hasWorldCoordinates,
                position = CanonicalCoordinateSystem.MediaPipeWorldToCanonical(
                    new Vector3(source.worldX, source.worldY, source.worldZ)),
                confidence = ConfidenceFor(source),
            };
        }

        private static float ConfidenceFor(PoseLandmarkObservation source)
        {
            if (source.hasVisibility && source.hasPresence)
            {
                return Mathf.Clamp01(Mathf.Min(source.visibility, source.presence));
            }
            if (source.hasVisibility)
            {
                return Mathf.Clamp01(source.visibility);
            }
            if (source.hasPresence)
            {
                return Mathf.Clamp01(source.presence);
            }
            return source.IsTracked ? 1f : 0f;
        }
    }
}
