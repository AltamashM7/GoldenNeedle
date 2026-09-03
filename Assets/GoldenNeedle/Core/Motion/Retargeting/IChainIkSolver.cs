using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// Value-only request for one two-bone chain. The interface keeps the analytic implementation
    /// replaceable without coupling it to Unity Transform state.
    /// </summary>
    public struct TwoBoneIkRequest
    {
        public Vector3 rootPosition;
        public Vector3 targetEffectorPosition;
        public Vector3 bendHintPosition;
        public bool hasBendHint;
        public Vector3 previousBendDirection;
        public bool hasPreviousBendDirection;
        public Vector3 referenceBendDirection;
        public Vector3 fallbackEffectorDirection;
        public float upperLength;
        public float lowerLength;
    }

    public struct TwoBoneIkResult
    {
        public bool isValid;
        public bool usedBendHint;
        public bool usedPreviousBendDirection;
        public Vector3 solvedRootPosition;
        public Vector3 solvedMidPosition;
        public Vector3 solvedEffectorPosition;
        public Vector3 bendDirection;
        public float targetDistance;
        public float clampedTargetDistance;
        public float endpointError;
    }

    public interface IChainIkSolver
    {
        bool TrySolve(TwoBoneIkRequest request, out TwoBoneIkResult result);
    }

    /// <summary>
    /// Small allocation-free analytic two-bone IK solver. It clamps unreachable targets and uses a
    /// deterministic bend-plane hierarchy so positional webcam input cannot produce invalid
    /// rotations or random elbow/knee flips.
    /// </summary>
    public sealed class AnalyticTwoBoneIkSolver : IChainIkSolver
    {
        private const float DirectionEpsilonSquared = 0.00000001f;
        private const float SignContinuityThreshold = -0.25f;

        public bool TrySolve(TwoBoneIkRequest request, out TwoBoneIkResult result)
        {
            result = default;
            if (!IsFinite(request.rootPosition) || !IsFinite(request.targetEffectorPosition) ||
                !IsFinite(request.upperLength) || !IsFinite(request.lowerLength) ||
                request.upperLength <= 0.000001f || request.lowerLength <= 0.000001f)
            {
                return false;
            }

            var upperLength = request.upperLength;
            var lowerLength = request.lowerLength;
            var chainLength = upperLength + lowerLength;
            var epsilon = Mathf.Max(0.00001f, chainLength * 0.0001f);
            var minimumReach = Mathf.Abs(upperLength - lowerLength) + epsilon;
            var maximumReach = chainLength - epsilon;
            if (!IsFinite(minimumReach) || !IsFinite(maximumReach) || maximumReach < minimumReach)
            {
                return false;
            }

            var targetOffset = request.targetEffectorPosition - request.rootPosition;
            var targetDistance = targetOffset.magnitude;
            if (!IsFinite(targetDistance))
            {
                return false;
            }

            var targetDirection = targetDistance * targetDistance > DirectionEpsilonSquared
                ? targetOffset / targetDistance
                : request.fallbackEffectorDirection;
            if (!TryNormalize(targetDirection, out targetDirection))
            {
                targetDirection = Vector3.forward;
            }

            var clampedDistance = Mathf.Clamp(targetDistance, minimumReach, maximumReach);
            var solvedEffector = request.rootPosition + targetDirection * clampedDistance;
            if (!TrySelectBendDirection(request, targetDirection, out var bendDirection, out var usedHint, out var usedPrevious))
            {
                return false;
            }

            var cSquared = clampedDistance * clampedDistance;
            var x = (upperLength * upperLength - lowerLength * lowerLength + cSquared) /
                    (2f * Mathf.Max(clampedDistance, epsilon));
            x = Mathf.Clamp(x, -upperLength, upperLength);
            var ySquared = Mathf.Max(0f, upperLength * upperLength - x * x);
            var y = Mathf.Sqrt(ySquared);
            var solvedMid = request.rootPosition + targetDirection * x + bendDirection * y;
            if (!IsFinite(solvedMid) || !IsFinite(solvedEffector))
            {
                return false;
            }

            result = new TwoBoneIkResult
            {
                isValid = true,
                usedBendHint = usedHint,
                usedPreviousBendDirection = usedPrevious,
                solvedRootPosition = request.rootPosition,
                solvedMidPosition = solvedMid,
                solvedEffectorPosition = solvedEffector,
                bendDirection = bendDirection,
                targetDistance = targetDistance,
                clampedTargetDistance = clampedDistance,
                endpointError = Vector3.Distance(solvedEffector, request.targetEffectorPosition),
            };
            return IsFinite(result.endpointError);
        }

        private static bool TrySelectBendDirection(
            TwoBoneIkRequest request,
            Vector3 targetDirection,
            out Vector3 bendDirection,
            out bool usedHint,
            out bool usedPrevious)
        {
            bendDirection = Vector3.zero;
            usedHint = false;
            usedPrevious = false;

            var previous = Vector3.zero;
            var currentHint = Vector3.zero;
            var hasPrevious = request.hasPreviousBendDirection &&
                              TryNormalize(request.previousBendDirection, out previous);
            var hasCurrentHint = request.hasBendHint &&
                                 TryProjectDirection(request.bendHintPosition - request.rootPosition, targetDirection, out currentHint);
            if (hasCurrentHint)
            {
                bendDirection = currentHint;
                usedHint = true;

                // A clearly opposite hint is an intentional bend-side change. Near the plane
                // boundary, retain the previous sign so small landmark noise cannot flip a limb.
                if (hasPrevious)
                {
                    var sign = Vector3.Dot(bendDirection, previous);
                    if (sign < 0f && sign > SignContinuityThreshold)
                    {
                        bendDirection = -bendDirection;
                    }
                }
            }
            else if (hasPrevious && TryProjectDirection(previous, targetDirection, out var previousPlane))
            {
                bendDirection = previousPlane;
                usedPrevious = true;
            }
            else if (TryProjectDirection(request.referenceBendDirection, targetDirection, out var referencePlane))
            {
                bendDirection = referencePlane;
            }
            else
            {
                var fallbackAxis = Mathf.Abs(targetDirection.y) < 0.85f ? Vector3.up : Vector3.right;
                if (!TryProjectDirection(fallbackAxis, targetDirection, out bendDirection) &&
                    !TryProjectDirection(Vector3.forward, targetDirection, out bendDirection))
                {
                    return false;
                }
            }

            if (hasPrevious && !usedHint && Vector3.Dot(bendDirection, previous) < 0f)
            {
                bendDirection = -bendDirection;
            }

            return TryNormalize(bendDirection, out bendDirection);
        }

        private static bool TryProjectDirection(Vector3 value, Vector3 normal, out Vector3 projected)
        {
            projected = value - normal * Vector3.Dot(value, normal);
            return TryNormalize(projected, out projected);
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value) || value.sqrMagnitude <= DirectionEpsilonSquared)
            {
                return false;
            }

            normalized = value.normalized;
            return IsFinite(normalized);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
