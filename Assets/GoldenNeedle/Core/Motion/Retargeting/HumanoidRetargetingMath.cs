using GoldenNeedle.Core.Motion.Calibration;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// Cached source/target characterization for one semantic limb chain. Chain frames map a
    /// chain-local reference pose into each side's anatomical parent space; the resulting
    /// rotation is therefore independent of authored bone-local axes.
    /// </summary>
    public struct ChainReferenceCharacterization
    {
        public bool isValid;
        public float sourceReach;
        public float targetReach;
        public Quaternion sourceParentReferenceFrame;
        public Quaternion targetParentReferenceFrame;
        public Quaternion sourceChainReferenceFrame;
        public Quaternion targetChainReferenceFrame;
        public Quaternion sourceToTargetChainRotation;
        public Vector3 sourceReferenceRootToMidParentLocal;
        public Vector3 sourceReferenceRootToTipParentLocal;
        public Vector3 targetReferenceRootToMidParentLocal;
        public Vector3 targetReferenceRootToTipParentLocal;
        public Vector3 sourceReferenceBendDirection;
        public Vector3 targetReferenceBendDirection;
    }

    /// <summary>
    /// Pure frame and characterization math shared by canonical target construction and target
    /// adapters. The forward convention intentionally matches the accepted canonical rotation
    /// solver: Up x Right selects the frontal hemisphere, with continuity supplied by the hint.
    /// </summary>
    public static class HumanoidRetargetingMath
    {
        private const float MinimumDirectionSquared = 0.000001f;

        public static Quaternion CalculateAlignment(Quaternion avatarReferenceBodyRotation, Quaternion sourceCalibrationBodyRotation)
        {
            return avatarReferenceBodyRotation * Quaternion.Inverse(sourceCalibrationBodyRotation);
        }

        public static Quaternion CalculateAvatarWorldDelta(
            Quaternion avatarReferenceBodyRotation,
            Quaternion sourceCalibrationBodyRotation,
            Quaternion sourceDeltaFromCalibration)
        {
            var alignment = CalculateAlignment(avatarReferenceBodyRotation, sourceCalibrationBodyRotation);
            return alignment * sourceDeltaFromCalibration * Quaternion.Inverse(alignment);
        }

        public static Quaternion CalculateTargetWorldRotation(
            Quaternion avatarReferenceBodyRotation,
            Quaternion sourceCalibrationBodyRotation,
            Quaternion sourceDeltaFromCalibration,
            Quaternion avatarBindWorldRotation)
        {
            return CalculateAvatarWorldDelta(
                       avatarReferenceBodyRotation,
                       sourceCalibrationBodyRotation,
                       sourceDeltaFromCalibration) *
                   avatarBindWorldRotation;
        }

        public static bool TryBuildAnatomicalFrame(
            Vector3 right,
            Vector3 up,
            Vector3 forwardHint,
            out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (!TryNormalize(right, out right) || !TryNormalize(up, out up))
            {
                return false;
            }

            up -= right * Vector3.Dot(up, right);
            if (!TryNormalize(up, out up))
            {
                return false;
            }

            var forward = Vector3.Cross(up, right);
            if (!TryNormalize(forward, out forward))
            {
                return false;
            }

            if (TryNormalize(forwardHint, out var normalizedForwardHint) &&
                Vector3.Dot(forward, normalizedForwardHint) < 0f)
            {
                forward = -forward;
            }

            up = Vector3.Cross(right, forward);
            if (!TryNormalize(up, out up))
            {
                return false;
            }

            right = Vector3.Cross(forward, up);
            if (!TryNormalize(right, out right))
            {
                return false;
            }

            rotation = Quaternion.LookRotation(forward, up);
            return IsFinite(rotation);
        }

        public static bool TryBuildChainReferenceFrame(
            Vector3 rootToTip,
            Vector3 rootToMid,
            Vector3 fallbackSecondary,
            out Quaternion chainFrame,
            out Vector3 bendDirection)
        {
            chainFrame = Quaternion.identity;
            bendDirection = Vector3.zero;
            if (!TryNormalize(rootToTip, out var primary))
            {
                return false;
            }

            var projectedMid = rootToMid - primary * Vector3.Dot(rootToMid, primary);
            if (!TryNormalize(projectedMid, out bendDirection))
            {
                projectedMid = fallbackSecondary - primary * Vector3.Dot(fallbackSecondary, primary);
                if (!TryNormalize(projectedMid, out bendDirection))
                {
                    var alternate = Mathf.Abs(primary.y) < 0.85f ? Vector3.up : Vector3.right;
                    projectedMid = alternate - primary * Vector3.Dot(alternate, primary);
                    if (!TryNormalize(projectedMid, out bendDirection))
                    {
                        projectedMid = Vector3.forward - primary * Vector3.Dot(Vector3.forward, primary);
                        if (!TryNormalize(projectedMid, out bendDirection))
                        {
                            return false;
                        }
                    }
                }
            }

            chainFrame = Quaternion.LookRotation(primary, bendDirection);
            return IsFinite(chainFrame);
        }

        public static bool TryBuildChainCharacterization(
            MotionCalibrationProfile profile,
            HumanoidRigBinding binding,
            CanonicalKinematicChainId chainId,
            out ChainReferenceCharacterization characterization)
        {
            characterization = default;
            if (profile == null || !profile.isValid || binding == null ||
                !binding.IsChainAvailable(chainId))
            {
                return false;
            }

            if (!TryBuildAnatomicalFrame(
                    profile.neutralBodyRight,
                    profile.neutralBodyUp,
                    profile.neutralBodyForward,
                    out var sourceParentReferenceFrame))
            {
                return false;
            }

            if (!TryGetSourceReferenceGeometry(
                    profile,
                    chainId,
                    out var sourceRootToMid,
                    out var sourceRootToTip,
                    out var sourceReach))
            {
                return false;
            }

            var sourceParentInverse = Quaternion.Inverse(sourceParentReferenceFrame);
            var sourceRootToMidParentLocal = sourceParentInverse * sourceRootToMid;
            var sourceRootToTipParentLocal = sourceParentInverse * sourceRootToTip;
            var sourceFallback = IsArm(chainId) ? Vector3.up : Vector3.forward;
            if (!TryBuildChainReferenceFrame(
                    sourceRootToTipParentLocal,
                    sourceRootToMidParentLocal,
                    sourceFallback,
                    out var sourceChainReferenceFrame,
                    out var sourceBendDirection))
            {
                return false;
            }

            if (!binding.TryGetChainReference(
                    chainId,
                    out var targetParentReferenceFrame,
                    out var targetChainReferenceFrame,
                    out var targetRootToMidParentLocal,
                    out var targetRootToTipParentLocal,
                    out var targetBendDirection))
            {
                return false;
            }

            var targetReach = binding.GetChainTotalReach(chainId);
            var sourceToTarget = targetChainReferenceFrame * Quaternion.Inverse(sourceChainReferenceFrame);
            if (!IsFinite(sourceToTarget) || !IsFinite(targetReach) || targetReach <= 0.000001f)
            {
                return false;
            }

            characterization = new ChainReferenceCharacterization
            {
                isValid = true,
                sourceReach = sourceReach,
                targetReach = targetReach,
                sourceParentReferenceFrame = sourceParentReferenceFrame,
                targetParentReferenceFrame = targetParentReferenceFrame,
                sourceChainReferenceFrame = sourceChainReferenceFrame,
                targetChainReferenceFrame = targetChainReferenceFrame,
                sourceToTargetChainRotation = sourceToTarget,
                sourceReferenceRootToMidParentLocal = sourceRootToMidParentLocal,
                sourceReferenceRootToTipParentLocal = sourceRootToTipParentLocal,
                targetReferenceRootToMidParentLocal = targetRootToMidParentLocal,
                targetReferenceRootToTipParentLocal = targetRootToTipParentLocal,
                sourceReferenceBendDirection = sourceBendDirection,
                targetReferenceBendDirection = targetBendDirection,
            };
            return true;
        }

        private static bool TryGetSourceReferenceGeometry(
            MotionCalibrationProfile profile,
            CanonicalKinematicChainId chainId,
            out Vector3 rootToMid,
            out Vector3 rootToTip,
            out float reach)
        {
            rootToMid = Vector3.zero;
            rootToTip = Vector3.zero;
            reach = 0f;
            switch (chainId)
            {
                case CanonicalKinematicChainId.LeftArm:
                    rootToMid = profile.tPoseLeftElbowPosition - profile.tPoseLeftShoulderPosition;
                    rootToTip = profile.tPoseLeftWristPosition - profile.tPoseLeftShoulderPosition;
                    reach = profile.leftArmReach;
                    if (rootToTip.sqrMagnitude <= MinimumDirectionSquared &&
                        TryNormalize(profile.tPoseLeftArmDirection, out var leftArmDirection))
                    {
                        rootToTip = leftArmDirection * reach;
                        rootToMid = rootToTip * 0.5f;
                    }
                    break;
                case CanonicalKinematicChainId.RightArm:
                    rootToMid = profile.tPoseRightElbowPosition - profile.tPoseRightShoulderPosition;
                    rootToTip = profile.tPoseRightWristPosition - profile.tPoseRightShoulderPosition;
                    reach = profile.rightArmReach;
                    if (rootToTip.sqrMagnitude <= MinimumDirectionSquared &&
                        TryNormalize(profile.tPoseRightArmDirection, out var rightArmDirection))
                    {
                        rootToTip = rightArmDirection * reach;
                        rootToMid = rootToTip * 0.5f;
                    }
                    break;
                case CanonicalKinematicChainId.LeftLeg:
                    rootToMid = profile.neutralLeftKneePosition - profile.neutralLeftHipPosition;
                    rootToTip = profile.neutralLeftAnklePosition - profile.neutralLeftHipPosition;
                    reach = profile.leftLegReach;
                    break;
                case CanonicalKinematicChainId.RightLeg:
                    rootToMid = profile.neutralRightKneePosition - profile.neutralRightHipPosition;
                    rootToTip = profile.neutralRightAnklePosition - profile.neutralRightHipPosition;
                    reach = profile.rightLegReach;
                    break;
                default:
                    return false;
            }

            return IsFinite(rootToMid) && IsFinite(rootToTip) && IsFinite(reach) &&
                reach > 0.0001f && rootToTip.sqrMagnitude > MinimumDirectionSquared;
        }

        private static bool IsArm(CanonicalKinematicChainId chainId)
        {
            return chainId == CanonicalKinematicChainId.LeftArm ||
                   chainId == CanonicalKinematicChainId.RightArm;
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value) || value.sqrMagnitude <= MinimumDirectionSquared)
            {
                return false;
            }

            normalized = value.normalized;
            return IsFinite(normalized);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
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
