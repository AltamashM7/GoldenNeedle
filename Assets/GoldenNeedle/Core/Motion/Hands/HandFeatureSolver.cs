using GoldenNeedle.Core.Motion.Rich;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Hands
{
    public static class HandFeatureSolver
    {
        private const float MinimumVectorMagnitude = 1e-5f;

        public static CanonicalAnatomicalBasis BuildPalmBasis(CanonicalHand hand)
        {
            if (hand == null)
            {
                return default;
            }

            if (!TryPoint(hand, HandLandmarkId.Wrist, out var wrist, out var wristConfidence) ||
                !TryPoint(hand, HandLandmarkId.IndexMcp, out var index, out var indexConfidence) ||
                !TryPoint(hand, HandLandmarkId.MiddleMcp, out var middle, out var middleConfidence) ||
                !TryPoint(hand, HandLandmarkId.PinkyMcp, out var pinky, out var pinkyConfidence))
            {
                return default;
            }

            var primaryRaw = middle - wrist;
            var lateralRaw = index - pinky;
            if (!TryNormalize(primaryRaw, out var primary))
            {
                return default;
            }

            var secondaryProjected = lateralRaw - primary * Vector3.Dot(lateralRaw, primary);
            if (!TryNormalize(secondaryProjected, out var secondary))
            {
                return default;
            }

            var third = Vector3.Cross(primary, secondary);
            if (!TryNormalize(third, out third))
            {
                return default;
            }
            secondary = Vector3.Cross(third, primary).normalized;
            var determinant = Vector3.Dot(Vector3.Cross(primary, secondary), third);
            if (!IsFinite(determinant) || determinant < 0.98f)
            {
                return default;
            }

            return new CanonicalAnatomicalBasis
            {
                primaryAxis = primary,
                secondaryAxis = secondary,
                thirdAxis = third,
                determinant = determinant,
                handedness = CanonicalAnatomicalHandedness.RightHanded,
                isValid = true,
            };
        }

        public static HandScalarFeature ComputeFingerCurl(
            CanonicalHand hand,
            HandLandmarkId mcp,
            HandLandmarkId pip,
            HandLandmarkId dip,
            HandLandmarkId tip)
        {
            if (hand == null ||
                !TryPoint(hand, mcp, out var p0, out var c0) ||
                !TryPoint(hand, pip, out var p1, out var c1) ||
                !TryPoint(hand, dip, out var p2, out var c2) ||
                !TryPoint(hand, tip, out var p3, out var c3))
            {
                return default;
            }

            if (!TryAngle(p0 - p1, p2 - p1, out var pipAngle) ||
                !TryAngle(p1 - p2, p3 - p2, out var dipAngle))
            {
                return default;
            }

            var extension = Mathf.Clamp01(((pipAngle + dipAngle) * 0.5f - 45f) / 135f);
            return new HandScalarFeature
            {
                isValid = true,
                value = 1f - extension,
                confidence = Mathf.Clamp01(Mathf.Min(Mathf.Min(c0, c1), Mathf.Min(c2, c3))),
            };
        }

        public static HandScalarFeature ComputeThumbCurl(CanonicalHand hand)
        {
            return ComputeFingerCurl(
                hand,
                HandLandmarkId.ThumbCmc,
                HandLandmarkId.ThumbMcp,
                HandLandmarkId.ThumbIp,
                HandLandmarkId.ThumbTip);
        }

        public static void PopulateDerivedFeatures(CanonicalHand hand)
        {
            if (hand == null)
            {
                return;
            }

            var thumb = ComputeThumbCurl(hand);
            var index = ComputeFingerCurl(hand, HandLandmarkId.IndexMcp, HandLandmarkId.IndexPip, HandLandmarkId.IndexDip, HandLandmarkId.IndexTip);
            var middle = ComputeFingerCurl(hand, HandLandmarkId.MiddleMcp, HandLandmarkId.MiddlePip, HandLandmarkId.MiddleDip, HandLandmarkId.MiddleTip);
            var ring = ComputeFingerCurl(hand, HandLandmarkId.RingMcp, HandLandmarkId.RingPip, HandLandmarkId.RingDip, HandLandmarkId.RingTip);
            var pinky = ComputeFingerCurl(hand, HandLandmarkId.PinkyMcp, HandLandmarkId.PinkyPip, HandLandmarkId.PinkyDip, HandLandmarkId.PinkyTip);
            var basis = BuildPalmBasis(hand);

            var majorValid = index.isValid && middle.isValid && ring.isValid && pinky.isValid;
            var majorConfidence = majorValid
                ? Mathf.Min(Mathf.Min(index.confidence, middle.confidence), Mathf.Min(ring.confidence, pinky.confidence))
                : 0f;
            var averageMajorCurl = majorValid
                ? (index.value + middle.value + ring.value + pinky.value) * 0.25f
                : 0f;

            var shape = HandShapeState.Unknown;
            if (majorValid && majorConfidence >= 0.35f)
            {
                if (averageMajorCurl <= 0.22f)
                {
                    shape = HandShapeState.Open;
                }
                else if (averageMajorCurl >= 0.72f)
                {
                    shape = HandShapeState.Fist;
                }
                else
                {
                    shape = HandShapeState.Intermediate;
                }
            }

            var pointing = default(HandBooleanFeature);
            if (majorValid && majorConfidence >= 0.4f)
            {
                var indexExtended = index.value <= 0.25f;
                var othersFlexed = middle.value >= 0.55f && ring.value >= 0.55f && pinky.value >= 0.55f;
                pointing = new HandBooleanFeature
                {
                    isKnown = true,
                    value = indexExtended && othersFlexed,
                    confidence = majorConfidence,
                };
            }

            hand.SetDerived(in basis, in thumb, in index, in middle, in ring, in pinky, shape, in pointing);
        }

        private static bool TryPoint(CanonicalHand hand, HandLandmarkId id, out Vector3 point, out float confidence)
        {
            var landmark = hand.GetLandmark(id);
            point = landmark.handLocalPosition;
            confidence = landmark.confidence;
            return landmark.hasHandLocalPosition && IsFinite(point) && confidence > 0f;
        }

        private static bool TryNormalize(Vector3 vector, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(vector) || vector.sqrMagnitude <= MinimumVectorMagnitude * MinimumVectorMagnitude)
            {
                return false;
            }
            normalized = vector.normalized;
            return IsFinite(normalized);
        }

        private static bool TryAngle(Vector3 a, Vector3 b, out float angleDegrees)
        {
            angleDegrees = 0f;
            if (!TryNormalize(a, out var na) || !TryNormalize(b, out var nb))
            {
                return false;
            }
            angleDegrees = Mathf.Acos(Mathf.Clamp(Vector3.Dot(na, nb), -1f, 1f)) * Mathf.Rad2Deg;
            return IsFinite(angleDegrees);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
