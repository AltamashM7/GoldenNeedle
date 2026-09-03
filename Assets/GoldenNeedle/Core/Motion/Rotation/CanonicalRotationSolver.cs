using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Rotation
{
    /// <summary>
    /// Reconstructs constrained swing-only bone deltas from stabilized canonical positions.
    /// Monocular axial twist is intentionally not treated as observable.
    /// </summary>
    public sealed class CanonicalRotationSolver
    {
        private const float MinimumVectorMagnitudeSquared = 0.000001f;

        public Quaternion ReferenceBodyRotation { get; private set; } = Quaternion.identity;
        public bool HasReferenceBodyRotation { get; private set; }

        public void Solve(CanonicalPoseFrame source, MotionCalibrationProfile profile, CanonicalRotationFrame destination)
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

            var profileValid = profile != null && profile.isValid;
            destination.Begin(source.sourceTimestampMillisec, source.receivedAtSeconds, profileValid);
            var referenceBodyRotation = Quaternion.identity;
            HasReferenceBodyRotation = profileValid && TryBuildBodyRotation(
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

            TrySolveDirectionalBone(
                CanonicalBoneId.LeftUpperArm,
                source,
                profile.tPoseLeftArmDirection,
                CanonicalJointId.LeftShoulder,
                CanonicalJointId.LeftElbow,
                destination);
            TrySolveDirectionalBone(
                CanonicalBoneId.LeftLowerArm,
                source,
                profile.tPoseLeftArmDirection,
                CanonicalJointId.LeftElbow,
                CanonicalJointId.LeftWrist,
                destination);
            TrySolveDirectionalBone(
                CanonicalBoneId.RightUpperArm,
                source,
                profile.tPoseRightArmDirection,
                CanonicalJointId.RightShoulder,
                CanonicalJointId.RightElbow,
                destination);
            TrySolveDirectionalBone(
                CanonicalBoneId.RightLowerArm,
                source,
                profile.tPoseRightArmDirection,
                CanonicalJointId.RightElbow,
                CanonicalJointId.RightWrist,
                destination);

            TrySolveDirectionalBone(
                CanonicalBoneId.LeftUpperLeg,
                source,
                profile.neutralLeftKneePosition - profile.neutralLeftHipPosition,
                CanonicalJointId.LeftHip,
                CanonicalJointId.LeftKnee,
                destination);
            TrySolveDirectionalBone(
                CanonicalBoneId.LeftLowerLeg,
                source,
                profile.neutralLeftAnklePosition - profile.neutralLeftKneePosition,
                CanonicalJointId.LeftKnee,
                CanonicalJointId.LeftAnkle,
                destination);
            TrySolveDirectionalBone(
                CanonicalBoneId.RightUpperLeg,
                source,
                profile.neutralRightKneePosition - profile.neutralRightHipPosition,
                CanonicalJointId.RightHip,
                CanonicalJointId.RightKnee,
                destination);
            TrySolveDirectionalBone(
                CanonicalBoneId.RightLowerLeg,
                source,
                profile.neutralRightAnklePosition - profile.neutralRightKneePosition,
                CanonicalJointId.RightKnee,
                CanonicalJointId.RightAnkle,
                destination);

            destination.Complete();
        }

        public static bool TryBuildBodyRotation(Vector3 right, Vector3 up, Vector3 forwardHint, out Quaternion rotation)
        {
            // A Quaternion can only represent a proper rotation. The calibration profile keeps its
            // semantic forward separately (and may therefore describe a reflected anatomical
            // basis); rotation output derives a proper frame from Right + Up only.
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
            if (!TryBuildBodyRotation(currentRight, currentUp, profile.neutralBodyForward, out var currentRotation))
            {
                return;
            }

            var delta = currentRotation * Quaternion.Inverse(QuaternionFromProfile(profile));
            if (!IsFinite(delta))
            {
                return;
            }

            AddTrackedBone(id, ConfidenceMin(left, right, pelvisJoint, chestJoint), delta, Vector3.zero, currentUp, destination);
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
            if (!TryGetPosition(from, out var fromPosition) || !TryGetPosition(to, out var toPosition))
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

            AddTrackedBone(id, Mathf.Min(Confidence(from), Confidence(to)), delta, normalizedReference, normalizedCurrent, destination);
        }

        private static Quaternion QuaternionFromProfile(MotionCalibrationProfile profile)
        {
            return TryBuildBodyRotation(profile.neutralBodyRight, profile.neutralBodyUp, profile.neutralBodyForward, out var rotation)
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
            return IsFinite(joint.confidence) ? Mathf.Clamp01(joint.confidence) : 0f;
        }

        private static float ConfidenceMin(CanonicalPoseJoint first, CanonicalPoseJoint second, CanonicalPoseJoint third, CanonicalPoseJoint fourth)
        {
            return Mathf.Min(Confidence(first), Confidence(second), Confidence(third), Confidence(fourth));
        }

        private static bool TryGetPosition(CanonicalPoseJoint joint, out Vector3 position)
        {
            if (!joint.IsTracked)
            {
                position = Vector3.zero;
                return false;
            }

            if (joint.hasLocalPosition && TryNormalizePosition(joint.localPosition, out position))
            {
                return true;
            }

            if (joint.hasWorldPosition && TryNormalizePosition(joint.worldPosition, out position))
            {
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private static bool TryNormalizePosition(Vector3 value, out Vector3 position)
        {
            position = value;
            return IsFinite(value);
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value) || value.sqrMagnitude <= MinimumVectorMagnitudeSquared)
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
