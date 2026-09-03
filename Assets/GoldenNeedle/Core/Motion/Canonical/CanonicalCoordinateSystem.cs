using System.Text;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Canonical
{
    public readonly struct CanonicalCoordinateAgreement
    {
        public CanonicalCoordinateAgreement(
            int xComparisons,
            int xMismatches,
            int yComparisons,
            int yMismatches,
            int yInsufficientComparisons = 0)
        {
            this.xComparisons = xComparisons;
            this.xMismatches = xMismatches;
            this.yComparisons = yComparisons;
            this.yMismatches = yMismatches;
            this.yInsufficientComparisons = yInsufficientComparisons;
        }

        public readonly int xComparisons;
        public readonly int xMismatches;
        public readonly int yComparisons;
        public readonly int yMismatches;
        public readonly int yInsufficientComparisons;
        public bool hasXEvidence => xComparisons > 0;
        public bool hasYEvidence => yComparisons > 0;
        public bool xPass => hasXEvidence && xMismatches == 0;
        public bool yPass => hasYEvidence && yMismatches == 0;
    }

    public static class CanonicalCoordinateAgreementEvaluator
    {
        public const float DefaultMinimumImageSeparation = 0.025f;
        public const float DefaultMinimumWorldSeparation = 0.025f;

        private struct ComparisonPair
        {
            public ComparisonPair(CanonicalJointId first, CanonicalJointId second)
            {
                this.first = first;
                this.second = second;
            }

            public CanonicalJointId first;
            public CanonicalJointId second;
        }

        private static readonly ComparisonPair[] XComparisonPairs =
        {
            new ComparisonPair(CanonicalJointId.LeftShoulder, CanonicalJointId.RightShoulder),
            new ComparisonPair(CanonicalJointId.LeftHip, CanonicalJointId.RightHip),
            new ComparisonPair(CanonicalJointId.LeftWrist, CanonicalJointId.RightWrist),
            new ComparisonPair(CanonicalJointId.LeftKnee, CanonicalJointId.RightKnee),
            new ComparisonPair(CanonicalJointId.LeftAnkle, CanonicalJointId.RightAnkle),
        };

        // Y evidence uses vertical anatomical chains. Bilateral pairs are intentionally not used
        // for Y because shoulders/hips/knees/ankles may legitimately be nearly level.
        private static readonly ComparisonPair[] YComparisonPairs =
        {
            new ComparisonPair(CanonicalJointId.Chest, CanonicalJointId.Pelvis),
            new ComparisonPair(CanonicalJointId.LeftShoulder, CanonicalJointId.LeftHip),
            new ComparisonPair(CanonicalJointId.RightShoulder, CanonicalJointId.RightHip),
            new ComparisonPair(CanonicalJointId.LeftHip, CanonicalJointId.LeftKnee),
            new ComparisonPair(CanonicalJointId.RightHip, CanonicalJointId.RightKnee),
            new ComparisonPair(CanonicalJointId.LeftKnee, CanonicalJointId.LeftAnkle),
            new ComparisonPair(CanonicalJointId.RightKnee, CanonicalJointId.RightAnkle),
            new ComparisonPair(CanonicalJointId.LeftShoulder, CanonicalJointId.LeftWrist),
            new ComparisonPair(CanonicalJointId.RightShoulder, CanonicalJointId.RightWrist),
        };

        public static CanonicalCoordinateAgreement Evaluate(
            CanonicalPoseFrame frame,
            float minimumImageSeparation = DefaultMinimumImageSeparation,
            float minimumWorldSeparation = DefaultMinimumWorldSeparation)
        {
            var xComparisons = 0;
            var xMismatches = 0;
            var yComparisons = 0;
            var yMismatches = 0;
            var yInsufficientComparisons = 0;
            if (frame == null)
            {
                return new CanonicalCoordinateAgreement(0, 0, 0, 0);
            }

            for (var i = 0; i < XComparisonPairs.Length; i++)
            {
                if (!TryGetComparison(frame, XComparisonPairs[i], out var imageDelta, out var worldDelta))
                {
                    continue;
                }

                if (Mathf.Abs(imageDelta.x) >= minimumImageSeparation && Mathf.Abs(worldDelta.x) >= minimumWorldSeparation)
                {
                    xComparisons++;
                    if (Mathf.Sign(imageDelta.x) != Mathf.Sign(worldDelta.x))
                    {
                        xMismatches++;
                    }
                }

            }

            for (var i = 0; i < YComparisonPairs.Length; i++)
            {
                if (!TryGetComparison(frame, YComparisonPairs[i], out var imageDelta, out var worldDelta))
                {
                    continue;
                }

                var imageSeparated = Mathf.Abs(imageDelta.y) >= minimumImageSeparation;
                var worldSeparated = Mathf.Abs(worldDelta.y) >= minimumWorldSeparation;
                if (imageSeparated && worldSeparated)
                {
                    yComparisons++;
                    if (Mathf.Sign(imageDelta.y) != Mathf.Sign(worldDelta.y))
                    {
                        yMismatches++;
                    }
                }
                else
                {
                    yInsufficientComparisons++;
                }
            }

            return new CanonicalCoordinateAgreement(xComparisons, xMismatches, yComparisons, yMismatches, yInsufficientComparisons);
        }

        public static string DescribeYComparisons(
            CanonicalPoseFrame frame,
            float minimumImageSeparation = DefaultMinimumImageSeparation,
            float minimumWorldSeparation = DefaultMinimumWorldSeparation)
        {
            if (frame == null)
            {
                return "Y audit: no canonical frame\n";
            }

            var builder = new StringBuilder("Y audit: anatomical pair / imageΔY (abs) / worldΔY (abs) / sign\n");
            for (var i = 0; i < YComparisonPairs.Length; i++)
            {
                var pair = YComparisonPairs[i];
                AppendYComparison(builder, frame, pair, minimumImageSeparation, minimumWorldSeparation);
            }

            builder.Append("Legacy bilateral Y pairs (diagnostic only)\n");
            for (var i = 0; i < XComparisonPairs.Length; i++)
            {
                AppendYComparison(builder, frame, XComparisonPairs[i], minimumImageSeparation, minimumWorldSeparation);
            }

            return builder.ToString();
        }

        private static void AppendYComparison(
            StringBuilder builder,
            CanonicalPoseFrame frame,
            ComparisonPair pair,
            float minimumImageSeparation,
            float minimumWorldSeparation)
        {
            if (!TryGetComparison(frame, pair, out var imageDelta, out var worldDelta))
            {
                builder.Append(pair.first).Append(" -> ").Append(pair.second).Append(": unavailable\n");
                return;
            }

            var imageSeparated = Mathf.Abs(imageDelta.y) >= minimumImageSeparation;
            var worldSeparated = Mathf.Abs(worldDelta.y) >= minimumWorldSeparation;
            var sign = imageSeparated && worldSeparated
                ? Mathf.Sign(imageDelta.y) == Mathf.Sign(worldDelta.y) ? "agree" : "MISMATCH"
                : "N/A-small";
            builder.Append(pair.first).Append(" -> ").Append(pair.second)
                .Append(": ")
                .Append(imageDelta.y.ToString("+0.000;-0.000;0.000"))
                .Append(" (").Append(Mathf.Abs(imageDelta.y).ToString("0.000")).Append(") / ")
                .Append(worldDelta.y.ToString("+0.000;-0.000;0.000"))
                .Append(" (").Append(Mathf.Abs(worldDelta.y).ToString("0.000")).Append(") / ")
                .Append(sign).Append('\n');
        }

        private static bool TryGetComparison(
            CanonicalPoseFrame frame,
            ComparisonPair pair,
            out Vector2 imageDelta,
            out Vector3 worldDelta)
        {
            var first = frame.GetJoint(pair.first);
            var second = frame.GetJoint(pair.second);
            imageDelta = default;
            worldDelta = default;
            if (!first.IsTracked || !second.IsTracked ||
                !first.hasImagePosition || !second.hasImagePosition ||
                !first.hasWorldPosition || !second.hasWorldPosition)
            {
                return false;
            }

            imageDelta = second.imagePosition - first.imagePosition;
            worldDelta = second.worldPosition - first.worldPosition;
            return true;
        }
    }

    /// <summary>
    /// Project-owned coordinate convention used by canonical motion data.
    /// Canonical image and world coordinates are both expressed in the correctly oriented
    /// MediaPipe inference frame. Image positions use x=0 left, x=1 right, y=0 bottom, and
    /// y=1 top. 3D positions use +X camera/view right, +Y up, and +Z away from the camera.
    /// </summary>
    public static class CanonicalCoordinateSystem
    {
        /// <summary>
        /// INPUT SPACE: MediaPipe normalized landmark coordinates in the canonical inference
        /// image frame, where normalized Y increases downward.
        /// OUTPUT SPACE: Golden Needle canonical image space, where Y increases upward.
        /// </summary>
        public static Vector2 MediaPipeNormalizedToCanonicalImage(Vector2 mediaPipeNormalized)
        {
            return new Vector2(mediaPipeNormalized.x, 1f - mediaPipeNormalized.y);
        }

        /// <summary>
        /// INPUT SPACE: MediaPipe pose-world coordinates returned for the canonical inference
        /// frame. MediaPipe world Y is negative above the hip-centered origin.
        /// OUTPUT SPACE: Golden Needle canonical 3D space: X right, Y up, Z away.
        /// No sensor, inference, or display transform belongs in this conversion.
        /// </summary>
        public static Vector3 MediaPipeWorldToCanonical(Vector3 mediaPipeWorld)
        {
            return new Vector3(mediaPipeWorld.x, -mediaPipeWorld.y, mediaPipeWorld.z);
        }

        /// <summary>
        /// INPUT SPACE: Golden Needle canonical image space.
        /// OUTPUT SPACE: Unity IMGUI screen space within the supplied content rectangle.
        /// The optional display mirror is a presentation-only X reflection.
        /// </summary>
        public static Vector2 CanonicalImageToGuiScreen(
            Vector2 canonicalImagePosition,
            Rect contentRect,
            bool displayMirrored = false)
        {
            // Canonical image Y increases upward; Unity IMGUI screen Y increases downward.
            var displayX = displayMirrored ? 1f - canonicalImagePosition.x : canonicalImagePosition.x;
            return new Vector2(
                contentRect.x + displayX * contentRect.width,
                contentRect.y + (1f - canonicalImagePosition.y) * contentRect.height);
        }

        /// <summary>
        /// INPUT SPACE: Golden Needle canonical world positions in camera coordinates.
        /// OUTPUT SPACE: The same positions relative to the supplied canonical root.
        /// </summary>
        public static Vector3 RootRelative(Vector3 worldPosition, Vector3 rootPosition)
        {
            return worldPosition - rootPosition;
        }
    }
}
