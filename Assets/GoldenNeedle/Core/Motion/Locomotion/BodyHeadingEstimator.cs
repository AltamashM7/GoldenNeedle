using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Retargeting;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    public struct BodyHeadingSample
    {
        public bool isValid;
        public Vector2 worldHeadingXZ;
        public Vector3 sourceForward;
        public Vector3 mappedForward;
        public float confidence;
    }

    /// <summary>
    /// Reads torso orientation from the accepted canonical pose and maps anatomical Forward through
    /// the accepted Phase 4 signed source-to-avatar basis so cadence travel matches avatar heading.
    /// </summary>
    public static class BodyHeadingEstimator
    {
        public static bool TryEstimate(
            CanonicalPoseFrame frame,
            MotionCalibrationProfile profile,
            HumanoidRigBinding binding,
            out BodyHeadingSample sample)
        {
            sample = default;
            if (frame == null || profile == null || !profile.bodyReferenceValid ||
                binding == null || !binding.IsBound ||
                !HumanoidRetargetingMath.TryCreateCanonicalToAvatarMap(profile, binding, out var map))
            {
                return false;
            }

            if (!TryPosition(frame.GetJoint(CanonicalJointId.LeftShoulder), out var leftShoulder) ||
                !TryPosition(frame.GetJoint(CanonicalJointId.RightShoulder), out var rightShoulder) ||
                !TryPosition(frame.GetJoint(CanonicalJointId.LeftHip), out var leftHip) ||
                !TryPosition(frame.GetJoint(CanonicalJointId.RightHip), out var rightHip) ||
                !TryPosition(frame.GetJoint(CanonicalJointId.Pelvis), out var pelvis) ||
                !TryPosition(frame.GetJoint(CanonicalJointId.Chest), out var chest))
            {
                return false;
            }

            var right = (rightShoulder - leftShoulder) + (rightHip - leftHip);
            var up = chest - pelvis;
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

            if (map.Source.HandednessSign < 0f)
            {
                forward = -forward;
            }

            var mappedForward = map.MapVector(forward);
            var horizontal = new Vector2(mappedForward.x, mappedForward.z);
            if (!IsFinite(horizontal) || horizontal.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            var confidence = Mathf.Min(
                Mathf.Min(
                    frame.GetJoint(CanonicalJointId.LeftShoulder).confidence,
                    frame.GetJoint(CanonicalJointId.RightShoulder).confidence),
                Mathf.Min(
                    frame.GetJoint(CanonicalJointId.LeftHip).confidence,
                    frame.GetJoint(CanonicalJointId.RightHip).confidence));

            sample = new BodyHeadingSample
            {
                isValid = true,
                worldHeadingXZ = horizontal.normalized,
                sourceForward = forward,
                mappedForward = mappedForward,
                confidence = Mathf.Clamp01(confidence),
            };
            return true;
        }

        public static bool TryMapHeading(
            CanonicalToAvatarAxisMap map,
            Vector3 sourceForward,
            out Vector2 worldHeadingXZ)
        {
            worldHeadingXZ = Vector2.zero;
            if (!map.IsValid || !IsFinite(sourceForward))
            {
                return false;
            }

            var mapped = map.MapVector(sourceForward);
            var horizontal = new Vector2(mapped.x, mapped.z);
            if (!IsFinite(horizontal) || horizontal.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            worldHeadingXZ = horizontal.normalized;
            return true;
        }

        private static bool TryPosition(CanonicalPoseJoint joint, out Vector3 position)
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
            if (!IsFinite(value) || value.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            normalized = value.normalized;
            return IsFinite(normalized);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
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
