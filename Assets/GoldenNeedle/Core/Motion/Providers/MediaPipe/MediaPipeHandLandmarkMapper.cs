using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Hands;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    /// <summary>
    /// The only Foundation D layer that knows MediaPipe Hand Landmarker landmark ordering.
    /// Downstream code receives HandLandmarkId semantics and hand-local geometry only.
    /// </summary>
    public static class MediaPipeHandLandmarkMapper
    {
        private static readonly HandLandmarkId[] SemanticIds =
        {
            HandLandmarkId.Wrist,
            HandLandmarkId.ThumbCmc,
            HandLandmarkId.ThumbMcp,
            HandLandmarkId.ThumbIp,
            HandLandmarkId.ThumbTip,
            HandLandmarkId.IndexMcp,
            HandLandmarkId.IndexPip,
            HandLandmarkId.IndexDip,
            HandLandmarkId.IndexTip,
            HandLandmarkId.MiddleMcp,
            HandLandmarkId.MiddlePip,
            HandLandmarkId.MiddleDip,
            HandLandmarkId.MiddleTip,
            HandLandmarkId.RingMcp,
            HandLandmarkId.RingPip,
            HandLandmarkId.RingDip,
            HandLandmarkId.RingTip,
            HandLandmarkId.PinkyMcp,
            HandLandmarkId.PinkyPip,
            HandLandmarkId.PinkyDip,
            HandLandmarkId.PinkyTip,
        };

        public static HandAssociationCandidate BuildAssociationCandidate(MediaPipeHandDetectionSnapshot source)
        {
            if (source == null || source.landmarkCount <= 0 || !source.normalized[0].valid)
            {
                return default;
            }

            return new HandAssociationCandidate
            {
                isValid = true,
                wristImagePosition = CanonicalCoordinateSystem.MediaPipeNormalizedToCanonicalImage(
                    new Vector2(source.normalized[0].x, source.normalized[0].y)),
                handednessEvidence = source.handedness,
                handednessScore = source.handednessScore,
            };
        }

        public static void MapDetection(
            MediaPipeHandDetectionSnapshot source,
            CanonicalHandSide assignedSide,
            HandAssociationMode associationMode,
            float associationConfidence,
            double receivedAtSeconds,
            CanonicalHand destination)
        {
            if (source == null || destination == null || assignedSide == CanonicalHandSide.Unknown)
            {
                return;
            }

            destination.Begin(
                assignedSide,
                source.sourceTimestampMillisec,
                receivedAtSeconds,
                source.handedness,
                source.handednessScore,
                associationMode,
                associationConfidence);

            var count = Mathf.Min(CanonicalHandSchema.LandmarkCount, source.landmarkCount);
            for (var i = 0; i < count; i++)
            {
                var normalized = source.normalized[i];
                var world = source.handLocal[i];
                if (!normalized.valid && !world.valid)
                {
                    continue;
                }

                var landmark = new CanonicalHandLandmark
                {
                    id = SemanticIds[i],
                    confidence = 1f,
                };
                if (normalized.valid && IsFinite(normalized.x) && IsFinite(normalized.y))
                {
                    landmark.imagePosition = CanonicalCoordinateSystem.MediaPipeNormalizedToCanonicalImage(
                        new Vector2(normalized.x, normalized.y));
                    landmark.hasImagePosition = true;
                }
                if (world.valid && IsFinite(world.x) && IsFinite(world.y) && IsFinite(world.z))
                {
                    // This uses canonical axis signs only. Translation remains hand-local by contract.
                    landmark.handLocalPosition = CanonicalCoordinateSystem.MediaPipeWorldToCanonical(
                        new Vector3(world.x, world.y, world.z));
                    landmark.hasHandLocalPosition = true;
                }
                destination.SetLandmark(in landmark);
            }

            HandFeatureSolver.PopulateDerivedFeatures(destination);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
