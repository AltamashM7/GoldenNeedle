using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Rich;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    /// <summary>
    /// MediaPipe-specific provider boundary for Foundation C rich evidence. Numeric MediaPipe pose
    /// landmark indices are intentionally confined to this mapper; downstream rich motion code
    /// consumes semantic RichMotionEvidenceId values only.
    /// </summary>
    public static class MediaPipeRichMotionEvidenceMapper
    {
        public const string SourceProviderId = "MediaPipePose33";

        private static readonly RichMotionEvidenceId[] DirectEvidenceIds =
        {
            RichMotionEvidenceId.LeftShoulder,
            RichMotionEvidenceId.RightShoulder,
            RichMotionEvidenceId.LeftElbow,
            RichMotionEvidenceId.RightElbow,
            RichMotionEvidenceId.LeftWrist,
            RichMotionEvidenceId.RightWrist,
            RichMotionEvidenceId.LeftPinky,
            RichMotionEvidenceId.RightPinky,
            RichMotionEvidenceId.LeftThumb,
            RichMotionEvidenceId.RightThumb,
            RichMotionEvidenceId.LeftHip,
            RichMotionEvidenceId.RightHip,
            RichMotionEvidenceId.LeftKnee,
            RichMotionEvidenceId.RightKnee,
            RichMotionEvidenceId.LeftAnkle,
            RichMotionEvidenceId.RightAnkle,
            RichMotionEvidenceId.LeftHeel,
            RichMotionEvidenceId.RightHeel,
            RichMotionEvidenceId.LeftToe,
            RichMotionEvidenceId.RightToe,
        };

        // MediaPipe pose-landmark indices belong only at this provider mapping boundary.
        private static readonly int[] DirectSourceIndices =
        {
            11, 12,
            13, 14,
            15, 16,
            17, 18,
            21, 22,
            23, 24,
            25, 26,
            27, 28,
            29, 30,
            31, 32,
        };

        public static void Map(
            PoseObservation source,
            double evaluationTimeSeconds,
            RichMotionEvidenceFrame destination)
        {
            if (destination == null)
            {
                return;
            }

            if (source == null)
            {
                destination.Clear();
                return;
            }

            destination.Begin(
                SourceProviderId,
                source.sourceTimestampMillisec,
                source.receivedAtSeconds,
                evaluationTimeSeconds,
                true);
            if (!source.hasPose)
            {
                destination.Complete();
                return;
            }

            for (var i = 0; i < DirectEvidenceIds.Length; i++)
            {
                MapDirect(
                    source.GetLandmark(DirectSourceIndices[i]),
                    DirectEvidenceIds[i],
                    destination);
            }

            MapDerivedMidpoint(
                RichMotionEvidenceId.Pelvis,
                RichMotionEvidenceId.LeftHip,
                RichMotionEvidenceId.RightHip,
                destination);
            MapDerivedMidpoint(
                RichMotionEvidenceId.Chest,
                RichMotionEvidenceId.LeftShoulder,
                RichMotionEvidenceId.RightShoulder,
                destination);
            destination.Complete();
        }

        private static void MapDirect(
            PoseLandmarkObservation source,
            RichMotionEvidenceId id,
            RichMotionEvidenceFrame destination)
        {
            if (!source.IsTracked ||
                !source.hasWorldCoordinates ||
                !IsFinite(source.worldX) ||
                !IsFinite(source.worldY) ||
                !IsFinite(source.worldZ))
            {
                return;
            }

            var evidence = new RichMotionEvidencePoint
            {
                id = id,
                tracking = CanonicalTrackingState.Tracked,
                confidence = ConfidenceFor(source),
                position = CanonicalCoordinateSystem.MediaPipeWorldToCanonical(
                    new Vector3(source.worldX, source.worldY, source.worldZ)),
                hasPosition = true,
            };
            destination.SetEvidence(in evidence);
        }

        private static void MapDerivedMidpoint(
            RichMotionEvidenceId derivedId,
            RichMotionEvidenceId firstId,
            RichMotionEvidenceId secondId,
            RichMotionEvidenceFrame destination)
        {
            var first = destination.GetEvidence(firstId);
            var second = destination.GetEvidence(secondId);
            if (!first.IsTracked || !second.IsTracked)
            {
                return;
            }

            var derived = new RichMotionEvidencePoint
            {
                id = derivedId,
                tracking = CanonicalTrackingState.Tracked,
                confidence = Mathf.Min(first.confidence, second.confidence),
                position = (first.position + second.position) * 0.5f,
                hasPosition = true,
            };
            destination.SetEvidence(in derived);
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

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
