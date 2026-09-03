using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// Converts stabilized canonical positions into normalized root-to-effector and root-to-mid
    /// chain vectors. Body-reference readiness is global; each chain is emitted only when that
    /// chain's independent calibration geometry is valid.
    /// </summary>
    public sealed class CanonicalKinematicTargetBuilder
    {
        private struct ChainDefinition
        {
            public CanonicalKinematicChainId id;
            public MotionCalibrationChainId calibrationId;
            public CanonicalJointId root;
            public CanonicalJointId mid;
            public CanonicalJointId effector;
        }

        private static readonly ChainDefinition[] Definitions =
        {
            new ChainDefinition
            {
                id = CanonicalKinematicChainId.LeftArm,
                calibrationId = MotionCalibrationChainId.LeftArm,
                root = CanonicalJointId.LeftShoulder,
                mid = CanonicalJointId.LeftElbow,
                effector = CanonicalJointId.LeftWrist,
            },
            new ChainDefinition
            {
                id = CanonicalKinematicChainId.RightArm,
                calibrationId = MotionCalibrationChainId.RightArm,
                root = CanonicalJointId.RightShoulder,
                mid = CanonicalJointId.RightElbow,
                effector = CanonicalJointId.RightWrist,
            },
            new ChainDefinition
            {
                id = CanonicalKinematicChainId.LeftLeg,
                calibrationId = MotionCalibrationChainId.LeftLeg,
                root = CanonicalJointId.LeftHip,
                mid = CanonicalJointId.LeftKnee,
                effector = CanonicalJointId.LeftAnkle,
            },
            new ChainDefinition
            {
                id = CanonicalKinematicChainId.RightLeg,
                calibrationId = MotionCalibrationChainId.RightLeg,
                root = CanonicalJointId.RightHip,
                mid = CanonicalJointId.RightKnee,
                effector = CanonicalJointId.RightAnkle,
            },
        };

        public void Reset()
        {
            // No temporal orientation state is required. Stabilization/calibration own sampling.
        }

        public void Build(
            CanonicalPoseFrame source,
            MotionCalibrationProfile profile,
            CanonicalKinematicTargets destination)
        {
            if (destination == null)
            {
                return;
            }

            var timestamp = source == null ? 0L : source.sourceTimestampMillisec;
            var receivedAt = source == null ? 0d : source.receivedAtSeconds;
            var bodyReferenceValid = profile != null && profile.bodyReferenceValid;
            destination.Begin(timestamp, receivedAt, bodyReferenceValid);
            if (source == null || !bodyReferenceValid)
            {
                destination.Complete();
                return;
            }

            for (var i = 0; i < Definitions.Length; i++)
            {
                var definition = Definitions[i];
                var geometry = profile.GetChainGeometry(definition.calibrationId);
                if (!geometry.isValid ||
                    !IsFinite(geometry.reach) ||
                    geometry.reach <= 0.0001f)
                {
                    continue;
                }

                var rootJoint = source.GetJoint(definition.root);
                var midJoint = source.GetJoint(definition.mid);
                var effectorJoint = source.GetJoint(definition.effector);
                if (!TryGetPosition(rootJoint, out var rootPosition) ||
                    !TryGetPosition(effectorJoint, out var effectorPosition))
                {
                    continue;
                }

                var effectorDisplacement = effectorPosition - rootPosition;
                if (!IsFinite(effectorDisplacement))
                {
                    continue;
                }

                var hasBendHint = TryGetPosition(midJoint, out var midPosition);
                var midDisplacement = hasBendHint ? midPosition - rootPosition : Vector3.zero;
                if (hasBendHint && !IsFinite(midDisplacement))
                {
                    hasBendHint = false;
                    midPosition = Vector3.zero;
                    midDisplacement = Vector3.zero;
                }

                var normalizedEffector = effectorDisplacement / geometry.reach;
                var normalizedMid = hasBendHint
                    ? midDisplacement / geometry.reach
                    : Vector3.zero;
                if (!IsFinite(normalizedEffector) || !IsFinite(normalizedMid))
                {
                    continue;
                }

                destination.SetTarget(new CanonicalKinematicChainTarget
                {
                    id = definition.id,
                    isValid = true,
                    hasBendHint = hasBendHint && midDisplacement.sqrMagnitude > 0.00000001f,
                    confidence = Mathf.Clamp01(
                        Mathf.Min(Confidence(rootJoint), Confidence(effectorJoint))),
                    sourceRootPosition = rootPosition,
                    sourceMidPosition = midPosition,
                    sourceEffectorPosition = effectorPosition,
                    sourceReach = geometry.reach,
                    normalizedEffectorDisplacement = normalizedEffector,
                    normalizedBendHintDisplacement = normalizedMid,
                    sourceTimestampMillisec = timestamp,
                    receivedAtSeconds = receivedAt,
                });
            }

            destination.Complete();
        }

        private static float Confidence(CanonicalPoseJoint joint)
        {
            return IsFinite(joint.confidence)
                ? Mathf.Clamp01(joint.confidence)
                : 0f;
        }

        private static bool TryGetPosition(CanonicalPoseJoint joint, out Vector3 position)
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
