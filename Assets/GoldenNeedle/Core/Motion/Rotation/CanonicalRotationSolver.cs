using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Rotation
{
    /// <summary>
    /// Reconstructs constrained swing-only diagnostic bone deltas from stabilized canonical
    /// positions. Torso requires only the body reference. Each limb segment is optional and uses
    /// that chain's sampled reference segment direction when available.
    /// </summary>
    public sealed class CanonicalRotationSolver
    {
        private const float MinimumVectorMagnitudeSquared = 0.000001f;

        public Quaternion ReferenceBodyRotation { get; private set; } = Quaternion.identity;
        public bool HasReferenceBodyRotation { get; private set; }

        public void Solve(
            CanonicalPoseFrame source,
            MotionCalibrationProfile profile,
            CanonicalRotationFrame destination)
        {
            if (destination == null)
            {
                return;
            }

            if (source == null)
            {
                destination.Begin(0L, 0d, false);
                destination.Complete();
                HasReferenceBodyRotation = false;
                ReferenceBodyRotation = Quaternion.identity;
                return;
            }

            var bodyReferenceValid = profile != null && profile.bodyReferenceValid;
            destination.Begin(
                source.sourceTimestampMillisec,
                source.receivedAtSeconds,
                bodyReferenceValid);

            var referenceBodyRotation = Quaternion.identity;
            HasReferenceBodyRotation = bodyReferenceValid &&
                TryBuildBodyRotation(
                    profile.neutralBodyRight,
                    profile.neutralBodyUp,
                    profile.neutralBodyForward,
                    out referenceBodyRotation);

            if (!HasReferenceBodyRotation)
            {
                ReferenceBodyRotation = Quaternion.identity;
                destination.Complete();
                return;
            }

            ReferenceBodyRotation = referenceBodyRotation;

            TrySolveTorsoBone(
                CanonicalBoneId.Pelvis,
                source,
                profile,
                CanonicalJointId.LeftHip,
                CanonicalJointId.RightHip,
                CanonicalJointId.Pelvis,
                CanonicalJointId.Chest,
                destination);
            TrySolveTorsoBone(
                CanonicalBoneId.Chest,
                source,
                profile,
                CanonicalJointId.LeftShoulder,
                CanonicalJointId.RightShoulder,
                CanonicalJointId.Pelvis,
                CanonicalJointId.Chest,
                destination);

            TrySolveChain(
                source,
                profile.leftArmGeometry,
                CanonicalBoneId.LeftUpperArm,
                CanonicalBoneId.LeftLowerArm,
                CanonicalJointId.LeftShoulder,
                CanonicalJointId.LeftElbow,
                CanonicalJointId.LeftWrist,
                destination);
            TrySolveChain(
                source,
                profile.rightArmGeometry,
                CanonicalBoneId.RightUpperArm,
                CanonicalBoneId.RightLowerArm,
                CanonicalJointId.RightShoulder,
                CanonicalJointId.RightElbow,
                CanonicalJointId.RightWrist,
                destination);
            TrySolveChain(
                source,
                profile.leftLegGeometry,
                CanonicalBoneId.LeftUpperLeg,
                CanonicalBoneId.LeftLowerLeg,
                CanonicalJointId.LeftHip,
                CanonicalJointId.LeftKnee,
                CanonicalJointId.LeftAnkle,
                destination);
            TrySolveChain(
                source,
                profile.rightLegGeometry,
                CanonicalBoneId.RightUpperLeg,
                CanonicalBoneId.RightLowerLeg,
                CanonicalJointId.RightHip,
                CanonicalJointId.RightKnee,
                CanonicalJointId.RightAnkle,
                destination);

            destination.Complete();
        }

        public static bool TryBuildBodyRotation(
            Vector3 right,
            Vector3 up,
            Vector3 forwardHint,
            out Quaternion rotation)
        {
            _ = forwardHint;
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

            var forward = Vector3.Cross(right, up);
            if (!TryNormalize(forward, out forward))
            {
                return false;
            }

            rotation = Quaternion.LookRotation(forward, up);
            return IsFinite(rotation);
        }

        private static void TrySolveChain(
            CanonicalPoseFrame source,
            MotionCalibrationChainGeometry geometry,
            CanonicalBoneId upperBone,
            CanonicalBoneId lowerBone,
            CanonicalJointId root,
            CanonicalJointId mid,
            CanonicalJointId tip,
            CanonicalRotationFrame destination)
        {
            if (!geometry.isValid)
            {
                return;
            }

            TrySolveDirectionalBone(
                upperBone,
                source,
                geometry.referenceUpperDirection,
                root,
                mid,
                destination);
            TrySolveDirectionalBone(
                lowerBone,
                source,
                geometry.referenceLowerDirection,
                mid,
                tip,
                destination);
        }

        private static void TrySolveTorsoBone(
            CanonicalBoneId id,
            CanonicalPoseFrame source,
            MotionCalibrationProfile profile,
            CanonicalJointId leftSide,
            CanonicalJointId rightSide,
            CanonicalJointId pelvis,
            CanonicalJointId chest,
            CanonicalRotationFrame destination)
        {
            var left = source.GetJoint(leftSide);
            var right = source.GetJoint(rightSide);
            var pelvisJoint = source.GetJoint(pelvis);
            var chestJoint = source.GetJoint(chest);
            if (!TryGetPosition(left, out var leftPosition) ||
                !TryGetPosition(right, out var rightPosition) ||
                !TryGetPosition(pelvisJoint, out var pelvisPosition) ||
                !TryGetPosition(chestJoint, out var chestPosition))
            {
                return;
            }

            var currentRight = rightPosition - leftPosition;
            var currentUp = chestPosition - pelvisPosition;
            if (!TryBuildBodyRotation(
                    currentRight,
                    currentUp,
                    profile.neutralBodyForward,
                    out var currentRotation))
            {
                return;
            }

            var delta = currentRotation * Quaternion.Inverse(QuaternionFromProfile(profile));
            if (!IsFinite(delta))
            {
                return;
            }

            AddTrackedBone(
                id,
                ConfidenceMin(left, right, pelvisJoint, chestJoint),
                delta,
                Vector3.zero,
                currentUp,
                destination);
        }

        private static void TrySolveDirectionalBone(
            CanonicalBoneId id,
            CanonicalPoseFrame source,
            Vector3 referenceDirection,
            CanonicalJointId fromId,
            CanonicalJointId toId,
            CanonicalRotationFrame destination)
        {
            var from = source.GetJoint(fromId);
            var to = source.GetJoint(toId);
            if (!TryGetPosition(from, out var fromPosition) ||
                !TryGetPosition(to, out var toPosition))
            {
                return;
            }

            var currentDirection = toPosition - fromPosition;
            if (!TryNormalize(referenceDirection, out var normalizedReference) ||
                !TryNormalize(currentDirection, out var normalizedCurrent))
            {
                return;
            }

            var delta = Quaternion.FromToRotation(normalizedReference, normalizedCurrent);
            if (!IsFinite(delta))
            {
                return;
            }

            AddTrackedBone(
                id,
                Mathf.Min(Confidence(from), Confidence(to)),
                delta,
                normalizedReference,
                normalizedCurrent,
                destination);
        }

        private static Quaternion QuaternionFromProfile(MotionCalibrationProfile profile)
        {
            return TryBuildBodyRotation(
                profile.neutralBodyRight,
                profile.neutralBodyUp,
                profile.neutralBodyForward,
                out var rotation)
                ? rotation
                : Quaternion.identity;
        }

        private static void AddTrackedBone(
            CanonicalBoneId id,
            float confidence,
            Quaternion delta,
            Vector3 referenceDirection,
            Vector3 currentDirection,
            CanonicalRotationFrame destination)
        {
            destination.SetBone(new CanonicalBoneRotation
            {
                id = id,
                tracking = CanonicalTrackingState.Tracked,
                confidence = Mathf.Clamp01(IsFinite(confidence) ? confidence : 0f),
                rotationDeltaFromCalibration = delta,
                referenceDirection = referenceDirection,
                currentDirection = currentDirection,
            });
        }

        private static float Confidence(CanonicalPoseJoint joint)
        {
            return IsFinite(joint.confidence)
                ? Mathf.Clamp01(joint.confidence)
                : 0f;
        }

        private static float ConfidenceMin(
            CanonicalPoseJoint first,
            CanonicalPoseJoint second,
            CanonicalPoseJoint third,
            CanonicalPoseJoint fourth)
        {
            return Mathf.Min(
                Confidence(first),
                Confidence(second),
                Confidence(third),
                Confidence(fourth));
        }

        private static bool TryGetPosition(
            CanonicalPoseJoint joint,
            out Vector3 position)
        {
            if (!joint.IsTracked)
            {
                position = Vector3.zero;
                return false;
            }

            if (joint.hasLocalPosition && IsFinite(joint.localPosition))
            {
                position = joint.localPosition;
                return true;
            }

            if (joint.hasWorldPosition && IsFinite(joint.worldPosition))
            {
                position = joint.worldPosition;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value) ||
                value.sqrMagnitude <= MinimumVectorMagnitudeSquared)
            {
                return false;
            }

            normalized = value.normalized;
            return IsFinite(normalized);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z) &&
                IsFinite(value.w);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
