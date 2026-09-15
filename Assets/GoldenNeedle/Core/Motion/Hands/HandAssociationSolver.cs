using UnityEngine;

namespace GoldenNeedle.Core.Motion.Hands
{
    public struct HandAssociationCandidate
    {
        public bool isValid;
        public Vector2 wristImagePosition;
        public CanonicalHandSide handednessEvidence;
        public float handednessScore;
    }

    public struct BodyWristEvidence
    {
        public bool hasLeft;
        public Vector2 left;
        public float leftConfidence;
        public bool hasRight;
        public Vector2 right;
        public float rightConfidence;
    }

    public struct HandAssociationResult
    {
        public CanonicalHandSide side;
        public HandAssociationMode mode;
        public float confidence;
        public bool isAssigned;
    }

    public static class HandAssociationSolver
    {
        private const float AmbiguityDistance = 0.055f;
        private const float MaximumUsefulDistance = 0.35f;

        public static void AssociatePair(
            in HandAssociationCandidate first,
            in HandAssociationCandidate second,
            in BodyWristEvidence body,
            out HandAssociationResult firstResult,
            out HandAssociationResult secondResult)
        {
            firstResult = AssociateSingle(in first, in body);
            secondResult = AssociateSingle(in second, in body);
            if (!first.isValid || !second.isValid || !body.hasLeft || !body.hasRight)
            {
                ResolveDuplicateSides(in first, in second, ref firstResult, ref secondResult);
                return;
            }

            var aLeft = Distance(first.wristImagePosition, body.left);
            var aRight = Distance(first.wristImagePosition, body.right);
            var bLeft = Distance(second.wristImagePosition, body.left);
            var bRight = Distance(second.wristImagePosition, body.right);
            var normal = aLeft + bRight;
            var swapped = aRight + bLeft;
            if (Mathf.Abs(normal - swapped) <= AmbiguityDistance)
            {
                ApplyHandednessFallback(in first, ref firstResult);
                ApplyHandednessFallback(in second, ref secondResult);
                ResolveDuplicateSides(in first, in second, ref firstResult, ref secondResult);
                return;
            }

            var useNormal = normal < swapped;
            firstResult = BuildPoseResult(useNormal ? CanonicalHandSide.Left : CanonicalHandSide.Right, useNormal ? aLeft : aRight);
            secondResult = BuildPoseResult(useNormal ? CanonicalHandSide.Right : CanonicalHandSide.Left, useNormal ? bRight : bLeft);
        }

        public static HandAssociationResult AssociateSingle(in HandAssociationCandidate candidate, in BodyWristEvidence body)
        {
            if (!candidate.isValid)
            {
                return default;
            }

            if (body.hasLeft && body.hasRight)
            {
                var leftDistance = Distance(candidate.wristImagePosition, body.left);
                var rightDistance = Distance(candidate.wristImagePosition, body.right);
                if (Mathf.Abs(leftDistance - rightDistance) > AmbiguityDistance)
                {
                    return BuildPoseResult(leftDistance < rightDistance ? CanonicalHandSide.Left : CanonicalHandSide.Right, Mathf.Min(leftDistance, rightDistance));
                }
            }
            else if (body.hasLeft)
            {
                var single = BuildSingleWristResult(CanonicalHandSide.Left, Distance(candidate.wristImagePosition, body.left));
                if (single.isAssigned) return single;
            }
            else if (body.hasRight)
            {
                var single = BuildSingleWristResult(CanonicalHandSide.Right, Distance(candidate.wristImagePosition, body.right));
                if (single.isAssigned) return single;
            }

            var fallback = default(HandAssociationResult);
            ApplyHandednessFallback(in candidate, ref fallback);
            return fallback;
        }

        private static HandAssociationResult BuildPoseResult(CanonicalHandSide side, float distance)
        {
            if (distance > MaximumUsefulDistance)
            {
                return new HandAssociationResult { side = CanonicalHandSide.Unknown, mode = HandAssociationMode.Ambiguous, isAssigned = false };
            }
            return new HandAssociationResult
            {
                side = side,
                mode = HandAssociationMode.PoseWristProximity,
                confidence = Mathf.Clamp01(1f - distance / MaximumUsefulDistance),
                isAssigned = true,
            };
        }

        private static HandAssociationResult BuildSingleWristResult(CanonicalHandSide side, float distance)
        {
            if (distance > MaximumUsefulDistance)
            {
                return new HandAssociationResult { side = CanonicalHandSide.Unknown, mode = HandAssociationMode.Ambiguous, isAssigned = false };
            }
            return new HandAssociationResult
            {
                side = side,
                mode = HandAssociationMode.PoseSingleWrist,
                confidence = Mathf.Clamp01((1f - distance / MaximumUsefulDistance) * 0.75f),
                isAssigned = true,
            };
        }

        private static void ApplyHandednessFallback(in HandAssociationCandidate candidate, ref HandAssociationResult result)
        {
            if (candidate.handednessEvidence == CanonicalHandSide.Unknown || candidate.handednessScore < 0.5f)
            {
                result = new HandAssociationResult
                {
                    side = CanonicalHandSide.Unknown,
                    mode = HandAssociationMode.Ambiguous,
                    confidence = 0f,
                    isAssigned = false,
                };
                return;
            }

            result = new HandAssociationResult
            {
                side = candidate.handednessEvidence,
                mode = HandAssociationMode.HandednessFallback,
                confidence = Mathf.Clamp01(candidate.handednessScore * 0.7f),
                isAssigned = true,
            };
        }

        private static void ResolveDuplicateSides(
            in HandAssociationCandidate first,
            in HandAssociationCandidate second,
            ref HandAssociationResult firstResult,
            ref HandAssociationResult secondResult)
        {
            if (!firstResult.isAssigned || !secondResult.isAssigned || firstResult.side != secondResult.side)
            {
                return;
            }

            if (firstResult.confidence > secondResult.confidence + 0.05f)
            {
                secondResult = Ambiguous();
                return;
            }
            if (secondResult.confidence > firstResult.confidence + 0.05f)
            {
                firstResult = Ambiguous();
                return;
            }

            firstResult = Ambiguous();
            secondResult = Ambiguous();
        }

        private static HandAssociationResult Ambiguous()
        {
            return new HandAssociationResult
            {
                side = CanonicalHandSide.Unknown,
                mode = HandAssociationMode.Ambiguous,
                confidence = 0f,
                isAssigned = false,
            };
        }

        private static float Distance(Vector2 first, Vector2 second)
        {
            var delta = first - second;
            return Mathf.Sqrt(delta.x * delta.x + delta.y * delta.y);
        }
    }
}
