using GoldenNeedle.Core.Motion.Retargeting;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    public struct GroundedFootConstraintSample
    {
        public bool hasStandingReference;
        public bool active;
        public bool leftSolved;
        public bool rightSolved;
        public bool leftReachClamped;
        public bool rightReachClamped;
        public float leftStandingY;
        public float rightStandingY;
        public float leftResidualY;
        public float rightResidualY;
        public Vector3 leftTarget;
        public Vector3 rightTarget;
    }

    /// <summary>
    /// Phase-5 post-root residual constraint for grounded bends. Root Y remains owned entirely by
    /// VerticalLocomotionInterpreter. This helper only preserves each captured standing foot plane
    /// after Phase-5 moves the avatar root, then re-solves the two leg chains with Golden Needle's
    /// existing analytic two-bone IK implementation.
    /// </summary>
    public sealed class GroundedFootConstraint
    {
        private const float VerticalActivationEpsilon = 0.001f;
        private const float DirectionEpsilonSquared = 0.00000001f;
        private const float ReachClampTolerance = 0.0005f;

        private readonly IChainIkSolver _ikSolver;
        private HumanoidRigBinding _binding;
        private int _referencePoseVersion = -1;
        private bool _hasStandingReference;
        private float _leftStandingY;
        private float _rightStandingY;
        private bool _active;
        private Vector3 _leftPreviousBendDirection;
        private Vector3 _rightPreviousBendDirection;
        private bool _hasLeftPreviousBendDirection;
        private bool _hasRightPreviousBendDirection;

        public GroundedFootConstraint(IChainIkSolver ikSolver = null)
        {
            _ikSolver = ikSolver ?? new AnalyticTwoBoneIkSolver();
            LatestSample = default;
        }

        public GroundedFootConstraintSample LatestSample { get; private set; }
        public bool HasStandingReference => _hasStandingReference;
        public float LeftStandingY => _leftStandingY;
        public float RightStandingY => _rightStandingY;

        public void Reset()
        {
            _binding = null;
            _referencePoseVersion = -1;
            ClearReference();
        }

        /// <summary>
        /// Evaluates lifecycle/gating after Phase-5 root placement and, when appropriate, re-solves
        /// only the two leg chains toward targets that preserve current X/Z while restoring the
        /// captured standing foot Y. No root position is read back or modified from solve residuals.
        /// </summary>
        public GroundedFootConstraintSample Update(
            HumanoidRigBinding binding,
            bool calibrationValid,
            VerticalLocomotionSample verticalSample,
            VerticalLocomotionSettings verticalSettings,
            bool applyCorrection)
        {
            verticalSettings = verticalSettings ?? new VerticalLocomotionSettings();
            verticalSettings.Sanitize();

            if (!calibrationValid)
            {
                Reset();
                return Publish(false, false, false, false, false, false,
                    Vector3.zero, Vector3.zero, 0f, 0f);
            }

            if (!EnsureBinding(binding))
            {
                return Publish(false, false, false, false, false, false,
                    Vector3.zero, Vector3.zero, 0f, 0f);
            }

            if (!_hasStandingReference && CanCaptureStandingReference(
                    binding,
                    verticalSample,
                    verticalSettings))
            {
                CaptureStandingReference(binding);
            }

            if (!_hasStandingReference ||
                !applyCorrection ||
                !ShouldConstrain(verticalSample, verticalSettings) ||
                !BothLegChainsAvailable(binding))
            {
                _active = false;
                return Publish(
                    _hasStandingReference,
                    false,
                    false,
                    false,
                    false,
                    false,
                    Vector3.zero,
                    Vector3.zero,
                    CurrentResidual(binding, CanonicalKinematicChainId.LeftLeg, _leftStandingY),
                    CurrentResidual(binding, CanonicalKinematicChainId.RightLeg, _rightStandingY));
            }

            _active = true;
            var leftSolved = TrySolveLeg(
                binding,
                CanonicalKinematicChainId.LeftLeg,
                _leftStandingY,
                ref _leftPreviousBendDirection,
                ref _hasLeftPreviousBendDirection,
                out var leftTarget,
                out var leftClamped);
            var rightSolved = TrySolveLeg(
                binding,
                CanonicalKinematicChainId.RightLeg,
                _rightStandingY,
                ref _rightPreviousBendDirection,
                ref _hasRightPreviousBendDirection,
                out var rightTarget,
                out var rightClamped);

            return Publish(
                _hasStandingReference,
                true,
                leftSolved,
                rightSolved,
                leftClamped,
                rightClamped,
                leftTarget,
                rightTarget,
                CurrentResidual(binding, CanonicalKinematicChainId.LeftLeg, _leftStandingY),
                CurrentResidual(binding, CanonicalKinematicChainId.RightLeg, _rightStandingY));
        }

        public static Vector3 BuildGroundedTarget(Vector3 currentFootPosition, float standingFootY)
        {
            return new Vector3(
                currentFootPosition.x,
                standingFootY,
                currentFootPosition.z);
        }

        public static bool IsGroundedSupportTrustworthy(
            VerticalLocomotionSample sample,
            VerticalLocomotionSettings settings)
        {
            settings = settings ?? new VerticalLocomotionSettings();
            settings.Sanitize();
            return sample.isAvailable &&
                sample.referenceReady &&
                !sample.IsJumpActive &&
                Mathf.Abs(sample.supportRise) <= settings.groundedSupportTolerance &&
                sample.footAsymmetry <= settings.maximumJumpFootAsymmetry;
        }

        private bool EnsureBinding(HumanoidRigBinding binding)
        {
            if (binding == null || !binding.IsBound)
            {
                Reset();
                return false;
            }

            if (_binding != binding ||
                _referencePoseVersion != binding.ReferencePoseVersion)
            {
                _binding = binding;
                _referencePoseVersion = binding.ReferencePoseVersion;
                ClearReference();
            }

            return true;
        }

        private void ClearReference()
        {
            _hasStandingReference = false;
            _leftStandingY = 0f;
            _rightStandingY = 0f;
            _active = false;
            _leftPreviousBendDirection = Vector3.zero;
            _rightPreviousBendDirection = Vector3.zero;
            _hasLeftPreviousBendDirection = false;
            _hasRightPreviousBendDirection = false;
            LatestSample = default;
        }

        private static bool CanCaptureStandingReference(
            HumanoidRigBinding binding,
            VerticalLocomotionSample sample,
            VerticalLocomotionSettings settings)
        {
            return BothLegChainsAvailable(binding) &&
                IsGroundedSupportTrustworthy(sample, settings) &&
                sample.state == VerticalLocomotionState.Standing &&
                !sample.groundedBendActive &&
                Mathf.Abs(sample.worldOffsetY) <= VerticalActivationEpsilon;
        }

        private void CaptureStandingReference(HumanoidRigBinding binding)
        {
            var left = binding.GetChainTip(CanonicalKinematicChainId.LeftLeg);
            var right = binding.GetChainTip(CanonicalKinematicChainId.RightLeg);
            if (left == null || right == null ||
                !IsFinite(left.position) || !IsFinite(right.position))
            {
                return;
            }

            _leftStandingY = left.position.y;
            _rightStandingY = right.position.y;
            _hasStandingReference = true;
            _active = false;
        }

        private bool ShouldConstrain(
            VerticalLocomotionSample sample,
            VerticalLocomotionSettings settings)
        {
            if (!IsGroundedSupportTrustworthy(sample, settings))
            {
                return false;
            }

            // Continue through filtered crouch recovery until the tracked root is effectively back
            // at its vertical origin. Neutral standing does not continuously re-solve the legs.
            return sample.groundedBendActive ||
                sample.worldOffsetY < -VerticalActivationEpsilon ||
                (_active && Mathf.Abs(sample.worldOffsetY) > VerticalActivationEpsilon);
        }

        private bool TrySolveLeg(
            HumanoidRigBinding binding,
            CanonicalKinematicChainId chainId,
            float standingY,
            ref Vector3 previousBendDirection,
            ref bool hasPreviousBendDirection,
            out Vector3 target,
            out bool reachClamped)
        {
            target = Vector3.zero;
            reachClamped = false;
            if (!IsLeg(chainId) ||
                binding == null ||
                !binding.IsChainAvailable(chainId))
            {
                return false;
            }

            var root = binding.GetChainRoot(chainId);
            var mid = binding.GetChainMid(chainId);
            var tip = binding.GetChainTip(chainId);
            var upperLength = binding.GetChainUpperLength(chainId);
            var lowerLength = binding.GetChainLowerLength(chainId);
            if (root == null || mid == null || tip == null ||
                upperLength <= 0.000001f || lowerLength <= 0.000001f ||
                !IsFinite(root.position) || !IsFinite(mid.position) || !IsFinite(tip.position))
            {
                return false;
            }

            target = BuildGroundedTarget(tip.position, standingY);
            if (!IsFinite(target))
            {
                return false;
            }

            var targetOffset = target - root.position;
            var fallbackDirection = tip.position - root.position;
            if (!TryNormalize(fallbackDirection, out fallbackDirection))
            {
                fallbackDirection = targetOffset;
            }
            if (!TryNormalize(fallbackDirection, out fallbackDirection))
            {
                fallbackDirection = Vector3.down;
            }

            var targetDirection = targetOffset;
            var hasTargetDirection = TryNormalize(targetDirection, out targetDirection);
            var currentBend = mid.position - root.position;
            if (hasTargetDirection)
            {
                currentBend -= targetDirection * Vector3.Dot(currentBend, targetDirection);
            }
            var hasCurrentBend = TryNormalize(currentBend, out currentBend);

            var referenceBend = ReferenceBendDirection(binding, chainId);
            var request = new TwoBoneIkRequest
            {
                rootPosition = root.position,
                targetEffectorPosition = target,
                bendHintPosition = hasCurrentBend
                    ? root.position + currentBend * upperLength
                    : Vector3.zero,
                hasBendHint = hasCurrentBend,
                previousBendDirection = previousBendDirection,
                hasPreviousBendDirection = hasPreviousBendDirection,
                referenceBendDirection = referenceBend,
                fallbackEffectorDirection = fallbackDirection,
                upperLength = upperLength,
                lowerLength = lowerLength,
            };

            if (!_ikSolver.TrySolve(request, out var result) || !result.isValid)
            {
                return false;
            }

            reachClamped = Mathf.Abs(result.targetDistance - result.clampedTargetDistance) >
                ReachClampTolerance;
            if (!ApplySolvedChain(root, mid, tip, result))
            {
                return false;
            }

            if (TryNormalize(result.bendDirection, out var reliableBend))
            {
                previousBendDirection = reliableBend;
                hasPreviousBendDirection = true;
            }

            return IsFinite(tip.position);
        }

        private static Vector3 ReferenceBendDirection(
            HumanoidRigBinding binding,
            CanonicalKinematicChainId chainId)
        {
            if (binding != null &&
                binding.TryGetChainReference(
                    chainId,
                    out var parentReferenceFrame,
                    out _,
                    out _,
                    out _,
                    out var bendDirection))
            {
                if (binding.TryGetCurrentParentFrame(chainId, out var currentParentFrame))
                {
                    var current = currentParentFrame * bendDirection;
                    if (TryNormalize(current, out current))
                    {
                        return current;
                    }
                }

                var reference = parentReferenceFrame * bendDirection;
                if (TryNormalize(reference, out reference))
                {
                    return reference;
                }
            }

            var fallback = binding == null
                ? Vector3.forward
                : binding.AvatarReferenceBodyRotation * Vector3.forward;
            return TryNormalize(fallback, out fallback) ? fallback : Vector3.forward;
        }

        private static bool ApplySolvedChain(
            Transform root,
            Transform mid,
            Transform tip,
            TwoBoneIkResult result)
        {
            if (!TryRotateToward(
                    root,
                    mid.position,
                    result.solvedMidPosition))
            {
                return false;
            }

            if (!TryRotateToward(
                    mid,
                    tip.position,
                    result.solvedEffectorPosition))
            {
                return false;
            }

            return IsFinite(root.rotation) &&
                IsFinite(mid.rotation) &&
                IsFinite(tip.position);
        }

        private static bool TryRotateToward(
            Transform joint,
            Vector3 fromPoint,
            Vector3 toPoint)
        {
            var fromDirection = fromPoint - joint.position;
            var toDirection = toPoint - joint.position;
            if (!TryNormalize(fromDirection, out fromDirection) ||
                !TryNormalize(toDirection, out toDirection))
            {
                return false;
            }

            joint.rotation = Quaternion.FromToRotation(fromDirection, toDirection) * joint.rotation;
            return IsFinite(joint.rotation);
        }

        private GroundedFootConstraintSample Publish(
            bool hasReference,
            bool active,
            bool leftSolved,
            bool rightSolved,
            bool leftClamped,
            bool rightClamped,
            Vector3 leftTarget,
            Vector3 rightTarget,
            float leftResidual,
            float rightResidual)
        {
            LatestSample = new GroundedFootConstraintSample
            {
                hasStandingReference = hasReference,
                active = active,
                leftSolved = leftSolved,
                rightSolved = rightSolved,
                leftReachClamped = leftClamped,
                rightReachClamped = rightClamped,
                leftStandingY = _leftStandingY,
                rightStandingY = _rightStandingY,
                leftResidualY = leftResidual,
                rightResidualY = rightResidual,
                leftTarget = leftTarget,
                rightTarget = rightTarget,
            };
            return LatestSample;
        }

        private static float CurrentResidual(
            HumanoidRigBinding binding,
            CanonicalKinematicChainId chainId,
            float standingY)
        {
            if (binding == null || !binding.IsChainAvailable(chainId))
            {
                return 0f;
            }

            var tip = binding.GetChainTip(chainId);
            return tip == null || !IsFinite(tip.position)
                ? 0f
                : tip.position.y - standingY;
        }

        private static bool BothLegChainsAvailable(HumanoidRigBinding binding)
        {
            return binding != null &&
                binding.IsBound &&
                binding.IsChainAvailable(CanonicalKinematicChainId.LeftLeg) &&
                binding.IsChainAvailable(CanonicalKinematicChainId.RightLeg);
        }

        private static bool IsLeg(CanonicalKinematicChainId id)
        {
            return id == CanonicalKinematicChainId.LeftLeg ||
                id == CanonicalKinematicChainId.RightLeg;
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

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
