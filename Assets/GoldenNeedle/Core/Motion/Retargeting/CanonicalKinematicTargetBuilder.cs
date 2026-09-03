using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// Converts stabilized canonical positions into normalized root-to-effector and root-to-mid
    /// chain vectors. It deliberately does not construct a body quaternion: Golden Needle's
    /// canonical anatomical basis may be reflected relative to an avatar basis, and that
    /// handedness conversion belongs explicitly in the target adapter.
    /// </summary>
    public sealed class CanonicalKinematicTargetBuilder
    {
        private struct ChainDefinition
        {
            public CanonicalKinematicChainId id;
            public CanonicalJointId root;
            public CanonicalJointId mid;
            public CanonicalJointId effector;
        }

        private static readonly ChainDefinition[] Definitions =
        {
            new ChainDefinition
            {
                id = CanonicalKinematicChainId.LeftArm,
                root = CanonicalJointId.LeftShoulder,
                mid = CanonicalJointId.LeftElbow,
                effector = CanonicalJointId.LeftWrist,
            },
            new ChainDefinition
            {
                id = CanonicalKinematicChainId.RightArm,
                root = CanonicalJointId.RightShoulder,
                mid = CanonicalJointId.RightElbow,
                effector = CanonicalJointId.RightWrist,
            },
            new ChainDefinition
            {
                id = CanonicalKinematicChainId.LeftLeg,
                root = CanonicalJointId.LeftHip,
                mid = CanonicalJointId.LeftKnee,
                effector = CanonicalJointId.LeftAnkle,
            },
            new ChainDefinition
            {
                id = CanonicalKinematicChainId.RightLeg,
                root = CanonicalJointId.RightHip,
                mid = CanonicalJointId.RightKnee,
                effector = CanonicalJointId.RightAnkle,
            },
        };

        public void Reset()
        {
            // No temporal orientation state is required. Stabilization is owned upstream.
        }

        public void Build(CanonicalPoseFrame source, MotionCalibrationProfile profile, CanonicalKinematicTargets destination)
        {
            if (destination == null)
            {
                return;
            }

            var timestamp = source == null ? 0L : source.sourceTimestampMillisec;
            var receivedAt = source == null ? 0d : source.receivedAtSeconds;
            var profileValid = profile != null && profile.isValid;
            destination.Begin(timestamp, receivedAt, profileValid);
            if (source == null || !profileValid)
            {
                destination.Complete();
                return;
            }

            for (var i = 0; i < Definitions.Length; i++)
            {
                var definition = Definitions[i];
                var rootJoint = source.GetJoint(definition.root);
                var midJoint = source.GetJoint(definition.mid);
                var effectorJoint = source.GetJoint(definition.effector);
                if (!TryGetPosition(rootJoint, out var rootPosition) ||
                    !TryGetPosition(effectorJoint, out var effectorPosition))
                {
                    continue;
                }

                var sourceReach = GetSourceReach(profile, definition.id);
                if (!IsFinite(sourceReach) || sourceReach <= 0.0001f)
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

                var normalizedEffector = effectorDisplacement / sourceReach;
                var normalizedMid = hasBendHint ? midDisplacement / sourceReach : Vector3.zero;
                if (!IsFinite(normalizedEffector) || !IsFinite(normalizedMid))
                {
                    continue;
                }

                destination.SetTarget(new CanonicalKinematicChainTarget
                {
                    id = definition.id,
                    isValid = true,
                    hasBendHint = hasBendHint && midDisplacement.sqrMagnitude > 0.00000001f,
                    confidence = Mathf.Clamp01(Mathf.Min(Confidence(rootJoint), Confidence(effectorJoint))),
                    sourceRootPosition = rootPosition,
                    sourceMidPosition = midPosition,
                    sourceEffectorPosition = effectorPosition,
                    sourceReach = sourceReach,
                    normalizedEffectorDisplacement = normalizedEffector,
                    normalizedBendHintDisplacement = normalizedMid,
                    sourceTimestampMillisec = timestamp,
                    receivedAtSeconds = receivedAt,
                });
            }

            destination.Complete();
        }

        private static float GetSourceReach(MotionCalibrationProfile profile, CanonicalKinematicChainId id)
        {
            switch (id)
            {
                case CanonicalKinematicChainId.LeftArm:
                    return profile.leftArmReach;
                case CanonicalKinematicChainId.RightArm:
                    return profile.rightArmReach;
                case CanonicalKinematicChainId.LeftLeg:
                    return profile.leftLegReach;
                case CanonicalKinematicChainId.RightLeg:
                    return profile.rightLegReach;
                default:
                    return 0f;
            }
        }

        private static float Confidence(CanonicalPoseJoint joint)
        {
            return IsFinite(joint.confidence) ? Mathf.Clamp01(joint.confidence) : 0f;
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
