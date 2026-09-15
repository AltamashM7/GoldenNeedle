using GoldenNeedle.Core.Motion.Rich;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// Stateless Foundation E vector/basis math. Rich canonical bases remain the authority;
    /// Unity Quaternions are created only when an avatar Transform is finally rotated.
    /// </summary>
    public static class FoundationERetargetMath
    {
        private const float MinimumDirectionSquared = 0.00000001f;

        public static bool TryGetSignedBasis(in CanonicalAnatomicalBasis source, out SignedAxisBasis basis)
        {
            basis = default;
            if (!source.isValid ||
                !HumanoidRetargetingMath.TryBuildSignedBasis(
                    source.primaryAxis,
                    source.secondaryAxis,
                    source.thirdAxis,
                    out basis))
            {
                return false;
            }
            return true;
        }

        public static float MapSignedAxialDegrees(float sourceDegrees, CanonicalToAvatarAxisMap map)
        {
            return map.IsValid ? sourceDegrees * map.DeterminantSign : 0f;
        }

        public static bool TryMeasureRelativeAxialDegrees(
            in CanonicalAnatomicalBasis reference,
            in CanonicalAnatomicalBasis current,
            out float degrees)
        {
            degrees = 0f;
            if (!TryNormalize(reference.primaryAxis, out var referencePrimary) ||
                !TryNormalize(reference.secondaryAxis, out var referenceSecondary) ||
                !TryNormalize(current.primaryAxis, out var currentPrimary) ||
                !TryNormalize(current.secondaryAxis, out var currentSecondary))
            {
                return false;
            }

            if (!TryTransportVectorMinimal(
                    referenceSecondary,
                    referencePrimary,
                    currentPrimary,
                    out var transportedSecondary))
            {
                return false;
            }

            transportedSecondary -= currentPrimary * Vector3.Dot(transportedSecondary, currentPrimary);
            currentSecondary -= currentPrimary * Vector3.Dot(currentSecondary, currentPrimary);
            if (!TryNormalize(transportedSecondary, out transportedSecondary) ||
                !TryNormalize(currentSecondary, out currentSecondary))
            {
                return false;
            }

            var sin = Vector3.Dot(
                currentPrimary,
                Vector3.Cross(transportedSecondary, currentSecondary));
            var cos = Mathf.Clamp(
                Vector3.Dot(transportedSecondary, currentSecondary),
                -1f,
                1f);
            degrees = Mathf.Atan2(sin, cos) * Mathf.Rad2Deg;
            return IsFinite(degrees);
        }

        public static bool TryMapDirectionBetweenBases(
            Vector3 sourceDirection,
            in CanonicalAnatomicalBasis sourceBasis,
            Vector3 targetPrimary,
            Vector3 targetSecondary,
            Vector3 targetThird,
            out Vector3 targetDirection)
        {
            targetDirection = Vector3.zero;
            if (!TryNormalize(sourceBasis.primaryAxis, out var sourcePrimary) ||
                !TryNormalize(sourceBasis.secondaryAxis, out var sourceSecondary) ||
                !TryNormalize(sourceBasis.thirdAxis, out var sourceThird) ||
                !TryNormalize(targetPrimary, out targetPrimary) ||
                !TryNormalize(targetSecondary, out targetSecondary) ||
                !TryNormalize(targetThird, out targetThird) ||
                !IsFinite(sourceDirection))
            {
                return false;
            }

            targetDirection =
                targetPrimary * Vector3.Dot(sourceDirection, sourcePrimary) +
                targetSecondary * Vector3.Dot(sourceDirection, sourceSecondary) +
                targetThird * Vector3.Dot(sourceDirection, sourceThird);
            return TryNormalize(targetDirection, out targetDirection);
        }

        public static bool TryApplyAxialTwistPreservingDownstream(
            Transform bone,
            Transform directChild,
            float degrees,
            out float childPositionResidual)
        {
            childPositionResidual = 0f;
            if (bone == null || directChild == null || !IsFinite(degrees))
            {
                return false;
            }

            var axis = directChild.position - bone.position;
            if (!TryNormalize(axis, out axis))
            {
                return false;
            }

            var childPosition = directChild.position;
            var childRotation = directChild.rotation;
            var delta = Quaternion.AngleAxis(degrees, axis);
            if (!IsFinite(delta))
            {
                return false;
            }

            bone.rotation = delta * bone.rotation;
            directChild.rotation = childRotation;
            childPositionResidual = Vector3.Distance(childPosition, directChild.position);
            return IsFinite(bone.rotation) &&
                   IsFinite(directChild.rotation) &&
                   IsFinite(childPositionResidual);
        }

        public static float SmoothContribution(float current, float target, float deltaTime, float response)
        {
            if (!IsFinite(current)) current = 0f;
            if (!IsFinite(target)) target = 0f;
            var dt = Mathf.Max(0f, deltaTime);
            var t = 1f - Mathf.Exp(-Mathf.Max(0.1f, response) * dt);
            return Mathf.Lerp(current, target, Mathf.Clamp01(t));
        }

        private static bool TryTransportVectorMinimal(
            Vector3 value,
            Vector3 fromAxis,
            Vector3 toAxis,
            out Vector3 transported)
        {
            transported = value;
            var cross = Vector3.Cross(fromAxis, toAxis);
            var sin = cross.magnitude;
            var cos = Mathf.Clamp(Vector3.Dot(fromAxis, toAxis), -1f, 1f);
            if (!IsFinite(sin) || !IsFinite(cos))
            {
                return false;
            }

            if (sin <= 0.000001f)
            {
                // Parallel is identity. Antiparallel has no unique shortest transport axis.
                return cos > 0f && IsFinite(transported);
            }

            var axis = cross / sin;
            transported = value * cos +
                          Vector3.Cross(axis, value) * sin +
                          axis * Vector3.Dot(axis, value) * (1f - cos);
            return IsFinite(transported);
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
