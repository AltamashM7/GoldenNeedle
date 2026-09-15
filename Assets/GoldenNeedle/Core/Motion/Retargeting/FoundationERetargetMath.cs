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

        /// <summary>
        /// Builds one absolute world-space delta that maps a stable target reference palm basis to
        /// the source palm's orientation relative to its stable source reference. The returned delta
        /// depends only on the two references plus the current source basis; it never depends on a
        /// previously E-modified hand Transform and therefore cannot accumulate frame-over-frame.
        /// </summary>
        public static bool TryBuildRelativeBasisDelta(
            in CanonicalAnatomicalBasis referenceSource,
            in CanonicalAnatomicalBasis currentSource,
            Vector3 targetReferencePrimary,
            Vector3 targetReferenceSecondary,
            Vector3 targetReferenceThird,
            out Quaternion delta,
            out Vector3 desiredPrimary,
            out Vector3 desiredSecondary,
            out Vector3 desiredThird)
        {
            delta = Quaternion.identity;
            desiredPrimary = Vector3.zero;
            desiredSecondary = Vector3.zero;
            desiredThird = Vector3.zero;

            if (!referenceSource.isValid || !currentSource.isValid ||
                !TryNormalize(referenceSource.primaryAxis, out var referencePrimary) ||
                !TryNormalize(referenceSource.secondaryAxis, out var referenceSecondary) ||
                !TryNormalize(referenceSource.thirdAxis, out var referenceThird) ||
                !TryNormalize(currentSource.primaryAxis, out var currentPrimary) ||
                !TryNormalize(currentSource.secondaryAxis, out var currentSecondary) ||
                !TryNormalize(targetReferencePrimary, out targetReferencePrimary) ||
                !TryNormalize(targetReferenceSecondary, out targetReferenceSecondary) ||
                !TryNormalize(targetReferenceThird, out targetReferenceThird))
            {
                return false;
            }

            desiredPrimary =
                targetReferencePrimary * Vector3.Dot(currentPrimary, referencePrimary) +
                targetReferenceSecondary * Vector3.Dot(currentPrimary, referenceSecondary) +
                targetReferenceThird * Vector3.Dot(currentPrimary, referenceThird);
            if (!TryNormalize(desiredPrimary, out desiredPrimary))
            {
                return false;
            }

            desiredSecondary =
                targetReferencePrimary * Vector3.Dot(currentSecondary, referencePrimary) +
                targetReferenceSecondary * Vector3.Dot(currentSecondary, referenceSecondary) +
                targetReferenceThird * Vector3.Dot(currentSecondary, referenceThird);
            desiredSecondary -= desiredPrimary * Vector3.Dot(desiredSecondary, desiredPrimary);
            if (!TryNormalize(desiredSecondary, out desiredSecondary))
            {
                return false;
            }

            desiredThird = Vector3.Cross(desiredPrimary, desiredSecondary);
            if (!TryNormalize(desiredThird, out desiredThird))
            {
                return false;
            }
            desiredSecondary = Vector3.Cross(desiredThird, desiredPrimary).normalized;

            var swing = Quaternion.FromToRotation(targetReferencePrimary, desiredPrimary);
            if (!IsFinite(swing))
            {
                return false;
            }

            var rotatedSecondary = swing * targetReferenceSecondary;
            rotatedSecondary -= desiredPrimary * Vector3.Dot(rotatedSecondary, desiredPrimary);
            if (!TryNormalize(rotatedSecondary, out rotatedSecondary))
            {
                return false;
            }

            var sin = Vector3.Dot(desiredPrimary, Vector3.Cross(rotatedSecondary, desiredSecondary));
            var cos = Mathf.Clamp(Vector3.Dot(rotatedSecondary, desiredSecondary), -1f, 1f);
            var twistDegrees = Mathf.Atan2(sin, cos) * Mathf.Rad2Deg;
            if (!IsFinite(twistDegrees))
            {
                return false;
            }

            delta = Quaternion.AngleAxis(twistDegrees, desiredPrimary) * swing;
            return TryNormalize(delta, out delta);
        }

        /// <summary>
        /// Reconstructs an absolute hand local-rotation target from a stable source reference,
        /// a target palm basis stored in the moving hand-parent's local frame, and the hand's bind
        /// local rotation. Parent motion therefore moves the whole reference frame without changing
        /// the relative E palm contribution.
        /// </summary>
        public static bool TryBuildAbsolutePalmLocalRotation(
            in CanonicalAnatomicalBasis referenceSource,
            in CanonicalAnatomicalBasis currentSource,
            Vector3 targetReferencePrimaryParentLocal,
            Vector3 targetReferenceSecondaryParentLocal,
            Vector3 targetReferenceThirdParentLocal,
            Quaternion parentWorldRotation,
            Quaternion baselineLocalRotation,
            out Quaternion targetLocalRotation,
            out Vector3 desiredPrimary,
            out Vector3 desiredSecondary,
            out Vector3 desiredThird)
        {
            targetLocalRotation = baselineLocalRotation;
            desiredPrimary = Vector3.zero;
            desiredSecondary = Vector3.zero;
            desiredThird = Vector3.zero;
            if (!TryNormalize(parentWorldRotation, out parentWorldRotation) ||
                !TryNormalize(baselineLocalRotation, out baselineLocalRotation))
            {
                return false;
            }

            var targetReferencePrimary = parentWorldRotation * targetReferencePrimaryParentLocal;
            var targetReferenceSecondary = parentWorldRotation * targetReferenceSecondaryParentLocal;
            var targetReferenceThird = parentWorldRotation * targetReferenceThirdParentLocal;
            if (!TryBuildRelativeBasisDelta(
                    in referenceSource,
                    in currentSource,
                    targetReferencePrimary,
                    targetReferenceSecondary,
                    targetReferenceThird,
                    out var delta,
                    out desiredPrimary,
                    out desiredSecondary,
                    out desiredThird))
            {
                return false;
            }

            var baselineWorldRotation = parentWorldRotation * baselineLocalRotation;
            var targetWorldRotation = delta * baselineWorldRotation;
            targetLocalRotation = Quaternion.Inverse(parentWorldRotation) * targetWorldRotation;
            return TryNormalize(targetLocalRotation, out targetLocalRotation);
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

        public static Quaternion SmoothLocalRotation(
            Quaternion current,
            Quaternion target,
            float deltaTime,
            float response)
        {
            if (!TryNormalize(current, out current)) current = Quaternion.identity;
            if (!TryNormalize(target, out target)) return current;
            var dt = Mathf.Max(0f, deltaTime);
            var t = 1f - Mathf.Exp(-Mathf.Max(0.1f, response) * dt);
            var result = Quaternion.Slerp(current, target, Mathf.Clamp01(t));
            return TryNormalize(result, out result) ? result : target;
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

        private static bool TryNormalize(Quaternion value, out Quaternion normalized)
        {
            normalized = Quaternion.identity;
            if (!IsFinite(value))
            {
                return false;
            }
            var magnitudeSquared = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
            if (!IsFinite(magnitudeSquared) || magnitudeSquared <= 0.00000001f)
            {
                return false;
            }
            var inverseMagnitude = 1f / Mathf.Sqrt(magnitudeSquared);
            normalized = new Quaternion(
                value.x * inverseMagnitude,
                value.y * inverseMagnitude,
                value.z * inverseMagnitude,
                value.w * inverseMagnitude);
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
