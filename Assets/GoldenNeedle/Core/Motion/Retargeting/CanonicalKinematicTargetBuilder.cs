using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// Converts stabilized canonical positions into normalized chain targets expressed in the
    /// current source anatomical parent space. Avatar alignment and avatar-proportion mapping
    /// remain in the target retargeter.
    /// </summary>
    public sealed class CanonicalKinematicTargetBuilder
    {
        private struct ChainDefinition
        {
            public CanonicalKinematicChainId id;
            public CanonicalJointId root;
            public CanonicalJointId mid;
            public CanonicalJointId effector;
            public bool usesChestFrame;
        }

        private static readonly ChainDefinition[] Definitions =
        {
            new ChainDefinition
            {
                id = CanonicalKinematicChainId.LeftArm,
                root = CanonicalJointId.LeftShoulder,
                mid = CanonicalJointId.LeftElbow,
                effector = CanonicalJointId.LeftWrist,
                usesChestFrame = true,
            },
            new ChainDefinition
            {
                id = CanonicalKinematicChainId.RightArm,
                root = CanonicalJointId.RightShoulder,
                mid = CanonicalJointId.RightElbow,
                effector = CanonicalJointId.RightWrist,
                usesChestFrame = true,
            },
            new ChainDefinition
            {
                id = CanonicalKinematicChainId.LeftLeg,
                root = CanonicalJointId.LeftHip,
                mid = CanonicalJointId.LeftKnee,
                effector = CanonicalJointId.LeftAnkle,
                usesChestFrame = false,
            },
            new ChainDefinition
            {
                id = CanonicalKinematicChainId.RightLeg,
                root = CanonicalJointId.RightHip,
                mid = CanonicalJointId.RightKnee,
                effector = CanonicalJointId.RightAnkle,
                usesChestFrame = false,
            },
        };

        private Quaternion _previousChestFrame = Quaternion.identity;
        private Quaternion _previousPelvisFrame = Quaternion.identity;
        private bool _hasPreviousChestFrame;
        private bool _hasPreviousPelvisFrame;

        public void Reset()
        {
            _previousChestFrame = Quaternion.identity;
            _previousPelvisFrame = Quaternion.identity;
            _hasPreviousChestFrame = false;
            _hasPreviousPelvisFrame = false;
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
                    !TryGetPosition(effectorJoint, out var effectorPosition) ||
                    !TryBuildCurrentParentFrame(source, profile, definition.usesChestFrame, out var parentFrame))
                {
                    continue;
                }

                var sourceReach = GetSourceReach(profile, definition.id);
                if (!IsFinite(sourceReach) || sourceReach <= 0.0001f)
                {
                    continue;
                }

                var sourceEffectorDisplacement = effectorPosition - rootPosition;
                if (!IsFinite(sourceEffectorDisplacement))
                {
                    continue;
                }

                var parentInverse = Quaternion.Inverse(parentFrame);
                var parentLocalEffector = parentInverse * sourceEffectorDisplacement;
                var hasBendHint = TryGetPosition(midJoint, out var midPosition);
                var parentLocalMid = hasBendHint ? parentInverse * (midPosition - rootPosition) : Vector3.zero;
                if (hasBendHint && !IsFinite(parentLocalMid))
                {
                    hasBendHint = false;
                    midPosition = Vector3.zero;
                    parentLocalMid = Vector3.zero;
                }

                var normalizedEffector = parentLocalEffector / sourceReach;
                var normalizedMid = hasBendHint ? parentLocalMid / sourceReach : Vector3.zero;
                if (!IsFinite(normalizedEffector) || !IsFinite(normalizedMid))
                {
                    continue;
                }

                destination.SetTarget(new CanonicalKinematicChainTarget
                {
                    id = definition.id,
                    isValid = true,
                    hasBendHint = hasBendHint && parentLocalMid.sqrMagnitude > 0.00000001f,
                    confidence = Mathf.Clamp01(Mathf.Min(Confidence(rootJoint), Confidence(effectorJoint))),
                    sourceRootPosition = rootPosition,
                    sourceMidPosition = midPosition,
                    sourceEffectorPosition = effectorPosition,
                    sourceReach = sourceReach,
                    hasSourceParentFrame = true,
                    sourceParentFrameRotation = parentFrame,
                    sourceParentLocalMidDisplacement = parentLocalMid,
                    sourceParentLocalEffectorDisplacement = parentLocalEffector,
                    normalizedEffectorDisplacement = normalizedEffector,
                    normalizedBendHintDisplacement = normalizedMid,
                    sourceTimestampMillisec = timestamp,
                    receivedAtSeconds = receivedAt,
                });
            }

            destination.Complete();
        }

        private bool TryBuildCurrentParentFrame(
            CanonicalPoseFrame source,
            MotionCalibrationProfile profile,
            bool usesChestFrame,
            out Quaternion frame)
        {
            frame = Quaternion.identity;
            var leftLateralJoint = Vector3.zero;
            var rightLateralJoint = Vector3.zero;
            var pelvis = Vector3.zero;
            var chest = Vector3.zero;
            var hasLateral = TryGetPosition(
                    source.GetJoint(usesChestFrame ? CanonicalJointId.LeftShoulder : CanonicalJointId.LeftHip),
                    out leftLateralJoint) &&
                TryGetPosition(
                    source.GetJoint(usesChestFrame ? CanonicalJointId.RightShoulder : CanonicalJointId.RightHip),
                    out rightLateralJoint);
            if (!hasLateral)
            {
                return false;
            }

            var lateral = rightLateralJoint - leftLateralJoint;
            var hasFullTorso = TryGetPosition(source.GetJoint(CanonicalJointId.Pelvis), out pelvis) &&
                TryGetPosition(source.GetJoint(CanonicalJointId.Chest), out chest);
            var hasFullFrame = hasFullTorso &&
                TryBuildContinuityFrame(
                    lateral,
                    chest - pelvis,
                    profile.neutralBodyForward,
                    usesChestFrame ? _previousChestFrame : _previousPelvisFrame,
                    usesChestFrame ? _hasPreviousChestFrame : _hasPreviousPelvisFrame,
                    out frame);

            if (!hasFullFrame)
            {
                var referenceFrame = usesChestFrame ? _previousChestFrame : _previousPelvisFrame;
                var hasReferenceFrame = usesChestFrame ? _hasPreviousChestFrame : _hasPreviousPelvisFrame;
                if (!hasReferenceFrame && HumanoidRetargetingMath.TryBuildAnatomicalFrame(
                        profile.neutralBodyRight,
                        profile.neutralBodyUp,
                        profile.neutralBodyForward,
                        out referenceFrame))
                {
                    hasReferenceFrame = true;
                }

                if (!hasReferenceFrame || !TryBuildFrameFromLateral(
                        lateral,
                        referenceFrame,
                        out frame))
                {
                    return false;
                }
            }

            if (usesChestFrame)
            {
                _previousChestFrame = frame;
                _hasPreviousChestFrame = true;
            }
            else
            {
                _previousPelvisFrame = frame;
                _hasPreviousPelvisFrame = true;
            }

            return true;
        }

        private static bool TryBuildContinuityFrame(
            Vector3 right,
            Vector3 up,
            Vector3 forwardHint,
            Quaternion previous,
            bool hasPrevious,
            out Quaternion frame)
        {
            var continuityHint = hasPrevious ? previous * Vector3.forward : forwardHint;
            return HumanoidRetargetingMath.TryBuildAnatomicalFrame(right, up, continuityHint, out frame);
        }

        private static bool TryBuildFrameFromLateral(
            Vector3 right,
            Quaternion referenceFrame,
            out Quaternion frame)
        {
            var referenceUp = referenceFrame * Vector3.up;
            var projectedUp = referenceUp - right.normalized * Vector3.Dot(referenceUp, right.normalized);
            if (projectedUp.sqrMagnitude <= 0.000001f)
            {
                var referenceForward = referenceFrame * Vector3.forward;
                projectedUp = referenceForward - right.normalized * Vector3.Dot(referenceForward, right.normalized);
            }

            return HumanoidRetargetingMath.TryBuildAnatomicalFrame(
                right,
                projectedUp,
                referenceFrame * Vector3.forward,
                out frame);
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
