using GoldenNeedle.Core.Motion.Calibration;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// An orthonormal semantic basis. handednessSign is +1 for a proper right-handed basis and -1
    /// for a reflected basis. Golden Needle canonical anatomy is allowed to use the latter; a
    /// quaternion is deliberately not used to represent this type.
    /// </summary>
    public readonly struct SignedAxisBasis
    {
        public SignedAxisBasis(Vector3 right, Vector3 up, Vector3 forward, float handednessSign)
        {
            Right = right;
            Up = up;
            Forward = forward;
            HandednessSign = handednessSign;
        }

        public Vector3 Right { get; }
        public Vector3 Up { get; }
        public Vector3 Forward { get; }
        public float HandednessSign { get; }
        public bool IsValid => HumanoidRetargetingMath.IsFiniteBasis(this);
    }

    /// <summary>
    /// Explicit linear map between a canonical semantic basis and an avatar semantic basis.
    /// Unlike Quaternion, this map can represent a reflection.
    /// </summary>
    public readonly struct CanonicalToAvatarAxisMap
    {
        public CanonicalToAvatarAxisMap(SignedAxisBasis source, SignedAxisBasis target)
        {
            Source = source;
            Target = target;
        }

        public SignedAxisBasis Source { get; }
        public SignedAxisBasis Target { get; }
        public bool IsValid => Source.IsValid && Target.IsValid;
        public float DeterminantSign => Source.HandednessSign * Target.HandednessSign;

        public Vector3 MapVector(Vector3 sourceVector)
        {
            return Target.Right * Vector3.Dot(sourceVector, Source.Right) +
                   Target.Up * Vector3.Dot(sourceVector, Source.Up) +
                   Target.Forward * Vector3.Dot(sourceVector, Source.Forward);
        }

        public Vector3 UnmapVector(Vector3 targetVector)
        {
            var right = Vector3.Dot(targetVector, Target.Right);
            var up = Vector3.Dot(targetVector, Target.Up);
            var forward = Vector3.Dot(targetVector, Target.Forward);
            return Source.Right * right + Source.Up * up + Source.Forward * forward;
        }
    }

    /// <summary>
    /// Cached source/target characterization retained for compatibility with older Phase 4
    /// diagnostics. Production limb retargeting no longer uses this quaternion-only mapping.
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

    public static class HumanoidRetargetingMath
    {
        private const float MinimumDirectionSquared = 0.000001f;

        /// <summary>
        /// Builds the source semantic basis without changing its handedness. Forward is
        /// orthogonalized but never flipped merely to satisfy Quaternion handedness.
        /// </summary>
        public static bool TryBuildSignedBasis(
            Vector3 right,
            Vector3 up,
            Vector3 forward,
            out SignedAxisBasis basis)
        {
            basis = default;
            if (!TryNormalize(right, out right) || !TryNormalize(up, out up))
            {
                return false;
            }

            up -= right * Vector3.Dot(up, right);
            if (!TryNormalize(up, out up))
            {
                return false;
            }

            forward -= right * Vector3.Dot(forward, right);
            forward -= up * Vector3.Dot(forward, up);
            if (!TryNormalize(forward, out forward))
            {
                return false;
            }

            var handedness = Vector3.Dot(Vector3.Cross(right, up), forward);
            if (!IsFinite(handedness) || Mathf.Abs(handedness) <= 0.5f)
            {
                return false;
            }

            basis = new SignedAxisBasis(right, up, forward, Mathf.Sign(handedness));
            return basis.IsValid;
        }

        /// <summary>
        /// Builds a target semantic reference basis while deliberately preserving the supplied
        /// Forward sign. Animator Humanoid avatars can therefore disambiguate body-facing
        /// orientation without flipping anatomical Right or Up.
        /// </summary>
        public static bool TryBuildTargetReferenceBasis(
            Vector3 right,
            Vector3 up,
            Vector3 semanticForward,
            out SignedAxisBasis basis)
        {
            return TryBuildSignedBasis(right, up, semanticForward, out basis);
        }

        /// <summary>
        /// Builds a proper Unity-compatible anatomical basis from target Right and Up. Explicit
        /// procedural/debug binding retains this deterministic geometry-only contract.
        /// </summary>
        public static bool TryBuildRightHandedBasis(
            Vector3 right,
            Vector3 up,
            out SignedAxisBasis basis)
        {
            basis = default;
            if (!TryNormalize(right, out right) || !TryNormalize(up, out up))
            {
                return false;
            }

            up -= right * Vector3.Dot(up, right);
            if (!TryNormalize(up, out up))
            {
                return false;
            }

            var forward = Vector3.Cross(right, up);
            if (!TryNormalize(forward, out forward))
            {
                return false;
            }

            basis = new SignedAxisBasis(right, up, forward, 1f);
            return basis.IsValid;
        }

        public static bool TryCreateCanonicalToAvatarMap(
            MotionCalibrationProfile profile,
            HumanoidRigBinding binding,
            out CanonicalToAvatarAxisMap map)
        {
            map = default;
            if (profile == null || !profile.bodyReferenceValid || binding == null || !binding.IsBound ||
                !TryBuildSignedBasis(
                    profile.neutralBodyRight,
                    profile.neutralBodyUp,
                    profile.neutralBodyForward,
                    out var sourceBasis) ||
                !binding.TryGetReferenceBodyBasis(out var targetBasis))
            {
                return false;
            }

            map = new CanonicalToAvatarAxisMap(sourceBasis, targetBasis);
            return map.IsValid;
        }

        /// <summary>
        /// Maps a live source Right/Up body frame through the signed canonical-to-avatar map.
        /// The source forward is derived using the calibration basis handedness, so large yaw and
        /// side views do not need a previous-frame or reference-forward hemisphere constraint.
        /// The mapped result is proper and can therefore be represented by a Unity quaternion.
        /// </summary>
        public static bool TryBuildMappedBodyRotation(
            CanonicalToAvatarAxisMap map,
            Vector3 currentSourceRight,
            Vector3 currentSourceUp,
            out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (!map.IsValid ||
                !TryNormalize(currentSourceRight, out var right) ||
                !TryNormalize(currentSourceUp, out var up))
            {
                return false;
            }

            up -= right * Vector3.Dot(up, right);
            if (!TryNormalize(up, out up))
            {
                return false;
            }

            var properForward = Vector3.Cross(right, up);
            if (!TryNormalize(properForward, out properForward))
            {
                return false;
            }

            var sourceForward = map.Source.HandednessSign < 0f ? -properForward : properForward;
            var mappedRight = map.MapVector(right);
            var mappedUp = map.MapVector(up);
            var mappedForward = map.MapVector(sourceForward);
            if (!TryNormalize(mappedRight, out mappedRight) ||
                !TryNormalize(mappedUp, out mappedUp) ||
                !TryNormalize(mappedForward, out mappedForward))
            {
                return false;
            }

            rotation = Quaternion.LookRotation(mappedForward, mappedUp);
            if (!IsFinite(rotation))
            {
                return false;
            }

            // A valid map of a basis with the source handedness must become a proper target frame.
            return Vector3.Dot(rotation * Vector3.right, mappedRight) > 0.999f &&
                   Vector3.Dot(rotation * Vector3.up, mappedUp) > 0.999f &&
                   Vector3.Dot(rotation * Vector3.forward, mappedForward) > 0.999f;
        }

        /// <summary>
        /// Builds the proper world-space rotation delta that moves the cached target reference
        /// semantic basis to the mapped live source basis. This remains representable by a
        /// Quaternion even when the target semantic basis itself is reflected, because reference
        /// and live target bases have the same handedness.
        /// </summary>
        public static bool TryBuildMappedBodyDelta(
            CanonicalToAvatarAxisMap map,
            Vector3 currentSourceRight,
            Vector3 currentSourceUp,
            out Quaternion delta)
        {
            delta = Quaternion.identity;
            if (!map.IsValid ||
                !TryNormalize(currentSourceRight, out var right) ||
                !TryNormalize(currentSourceUp, out var up))
            {
                return false;
            }

            up -= right * Vector3.Dot(up, right);
            if (!TryNormalize(up, out up))
            {
                return false;
            }

            var properForward = Vector3.Cross(right, up);
            if (!TryNormalize(properForward, out properForward))
            {
                return false;
            }

            var sourceForward = map.Source.HandednessSign < 0f ? -properForward : properForward;
            var mappedRight = map.MapVector(right);
            var mappedUp = map.MapVector(up);
            var mappedForward = map.MapVector(sourceForward);
            if (!TryNormalize(mappedRight, out mappedRight) ||
                !TryNormalize(mappedUp, out mappedUp) ||
                !TryNormalize(mappedForward, out mappedForward) ||
                !TryBuildRightHandedBasis(map.Target.Right, map.Target.Up, out var referenceProper) ||
                !TryBuildRightHandedBasis(mappedRight, mappedUp, out var currentProper))
            {
                return false;
            }

            var mappedHandedness = Vector3.Dot(Vector3.Cross(mappedRight, mappedUp), mappedForward);
            if (!IsFinite(mappedHandedness) ||
                Mathf.Sign(mappedHandedness) != Mathf.Sign(map.Target.HandednessSign))
            {
                return false;
            }

            var referenceRotation = Quaternion.LookRotation(referenceProper.Forward, referenceProper.Up);
            var currentRotation = Quaternion.LookRotation(currentProper.Forward, currentProper.Up);
            delta = currentRotation * Quaternion.Inverse(referenceRotation);
            if (!IsFinite(delta))
            {
                return false;
            }

            return Vector3.Dot(delta * map.Target.Right, mappedRight) > 0.999f &&
                   Vector3.Dot(delta * map.Target.Up, mappedUp) > 0.999f &&
                   Vector3.Dot(delta * map.Target.Forward, mappedForward) > 0.999f;
        }

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

        /// <summary>
        /// Legacy proper-frame helper. It intentionally preserves Right and Up and therefore
        /// cannot encode a reflected forward hint. Production handedness conversion uses
        /// SignedAxisBasis instead.
        /// </summary>
        public static bool TryBuildAnatomicalFrame(
            Vector3 right,
            Vector3 up,
            Vector3 forwardHint,
            out Quaternion rotation)
        {
            _ = forwardHint;
            rotation = Quaternion.identity;
            if (!TryBuildRightHandedBasis(right, up, out var basis))
            {
                return false;
            }

            rotation = Quaternion.LookRotation(basis.Forward, basis.Up);
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

        /// <summary>
        /// Compatibility-only Phase 4 characterization. Production retargeting uses one explicit
        /// signed canonical-to-avatar map instead.
        /// </summary>
        public static bool TryBuildChainCharacterization(
            MotionCalibrationProfile profile,
            HumanoidRigBinding binding,
            CanonicalKinematicChainId chainId,
            out ChainReferenceCharacterization characterization)
        {
            characterization = default;
            if (profile == null || !profile.bodyReferenceValid || binding == null ||
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

        internal static bool IsFiniteBasis(SignedAxisBasis basis)
        {
            return IsFinite(basis.Right) && IsFinite(basis.Up) && IsFinite(basis.Forward) &&
                   IsFinite(basis.HandednessSign) && Mathf.Abs(Mathf.Abs(basis.HandednessSign) - 1f) < 0.001f &&
                   Mathf.Abs(Vector3.Dot(basis.Right, basis.Up)) < 0.001f &&
                   Mathf.Abs(Vector3.Dot(basis.Right, basis.Forward)) < 0.001f &&
                   Mathf.Abs(Vector3.Dot(basis.Up, basis.Forward)) < 0.001f;
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
            var geometry = GetCalibrationGeometry(profile, chainId);
            if (!geometry.isValid ||
                !TryNormalize(geometry.referenceUpperDirection, out var upperDirection) ||
                !TryNormalize(geometry.referenceLowerDirection, out var lowerDirection) ||
                !IsFinite(geometry.upperLength) ||
                !IsFinite(geometry.lowerLength) ||
                !IsFinite(geometry.reach) ||
                geometry.upperLength <= 0f ||
                geometry.lowerLength <= 0f ||
                geometry.reach <= 0f)
            {
                return false;
            }

            rootToMid = upperDirection * geometry.upperLength;
            rootToTip = rootToMid + lowerDirection * geometry.lowerLength;
            reach = geometry.reach;
            return IsFinite(rootToMid) &&
                   IsFinite(rootToTip) &&
                   rootToTip.sqrMagnitude > MinimumDirectionSquared;
        }

        private static MotionCalibrationChainGeometry GetCalibrationGeometry(
            MotionCalibrationProfile profile,
            CanonicalKinematicChainId chainId)
        {
            switch (chainId)
            {
                case CanonicalKinematicChainId.LeftArm:
                    return profile.leftArmGeometry;
                case CanonicalKinematicChainId.RightArm:
                    return profile.rightArmGeometry;
                case CanonicalKinematicChainId.LeftLeg:
                    return profile.leftLegGeometry;
                case CanonicalKinematicChainId.RightLeg:
                    return profile.rightLegGeometry;
                default:
                    return default;
            }
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
