using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Canonical
{
    /// <summary>
    /// Converts the MediaPipe provider boundary into the engine-owned canonical skeleton.
    /// This is the only place in the phase-2 runtime path that knows MediaPipe's 33-landmark
    /// indices. Downstream systems consume CanonicalPoseFrame only.
    /// </summary>
    public static class MediaPipeCanonicalPoseMapper
    {
        private static readonly CanonicalJointId[] DirectJointIds =
        {
            CanonicalJointId.Head,
            CanonicalJointId.LeftShoulder,
            CanonicalJointId.RightShoulder,
            CanonicalJointId.LeftElbow,
            CanonicalJointId.RightElbow,
            CanonicalJointId.LeftWrist,
            CanonicalJointId.RightWrist,
            CanonicalJointId.LeftHip,
            CanonicalJointId.RightHip,
            CanonicalJointId.LeftKnee,
            CanonicalJointId.RightKnee,
            CanonicalJointId.LeftAnkle,
            CanonicalJointId.RightAnkle,
            CanonicalJointId.LeftHeel,
            CanonicalJointId.RightHeel,
            CanonicalJointId.LeftToe,
            CanonicalJointId.RightToe,
        };

        private static readonly int[] DirectSourceIndices =
        {
            0,
            11, 12,
            13, 14,
            15, 16,
            23, 24,
            25, 26,
            27, 28,
            29, 30,
            31, 32,
        };

        public static void Map(PoseObservation source, CanonicalPoseFrame destination, CameraOrientationState orientation)
        {
            if (destination == null)
            {
                return;
            }

            if (source == null)
            {
                destination.Clear();
                return;
            }

            destination.Begin(source.sourceTimestampMillisec, source.receivedAtSeconds, source.hasPose);
            if (!source.hasPose)
            {
                destination.Complete();
                return;
            }

            for (var i = 0; i < DirectJointIds.Length; i++)
            {
                MapDirect(source.GetLandmark(DirectSourceIndices[i]), DirectJointIds[i], orientation, destination);
            }

            MapDerivedMidpoint(CanonicalJointId.Pelvis, CanonicalJointId.LeftHip, CanonicalJointId.RightHip, destination);
            MapDerivedMidpoint(CanonicalJointId.Chest, CanonicalJointId.LeftShoulder, CanonicalJointId.RightShoulder, destination);
            MapDerivedMidpoint(CanonicalJointId.Spine, CanonicalJointId.Pelvis, CanonicalJointId.Chest, destination);
            MakeLocalPositionsRootRelative(destination);
            destination.Complete();
        }

        private static void MapDirect(
            PoseLandmarkObservation source,
            CanonicalJointId id,
            CameraOrientationState orientation,
            CanonicalPoseFrame destination)
        {
            if (!source.IsTracked)
            {
                return;
            }

            var joint = new CanonicalPoseJoint
            {
                id = id,
                tracking = CanonicalTrackingState.Tracked,
                confidence = ConfidenceFor(source),
                imagePosition = orientation.MediaPipeImageToCameraNormalized(new Vector2(source.x, source.y)),
                hasImagePosition = true,
            };

            if (source.hasWorldCoordinates && IsFinite(source.worldX) && IsFinite(source.worldY) && IsFinite(source.worldZ))
            {
                joint.worldPosition = CanonicalCoordinateSystem.ToCanonicalWorld(source.worldX, source.worldY, source.worldZ);
                joint.localPosition = joint.worldPosition;
                joint.hasWorldPosition = true;
                joint.hasLocalPosition = true;
            }

            destination.SetJoint(in joint);
        }

        private static void MapDerivedMidpoint(
            CanonicalJointId derivedId,
            CanonicalJointId firstId,
            CanonicalJointId secondId,
            CanonicalPoseFrame destination)
        {
            var first = destination.GetJoint(firstId);
            var second = destination.GetJoint(secondId);
            if (!first.IsTracked || !second.IsTracked)
            {
                return;
            }

            var derived = new CanonicalPoseJoint
            {
                id = derivedId,
                tracking = CanonicalTrackingState.Tracked,
                confidence = Mathf.Min(first.confidence, second.confidence),
                imagePosition = (first.imagePosition + second.imagePosition) * 0.5f,
                hasImagePosition = first.hasImagePosition && second.hasImagePosition,
            };

            if (first.hasWorldPosition && second.hasWorldPosition)
            {
                derived.worldPosition = (first.worldPosition + second.worldPosition) * 0.5f;
                derived.hasWorldPosition = true;
            }

            if (first.hasLocalPosition && second.hasLocalPosition)
            {
                derived.localPosition = (first.localPosition + second.localPosition) * 0.5f;
                derived.hasLocalPosition = true;
            }

            destination.SetJoint(in derived);
        }

        private static void MakeLocalPositionsRootRelative(CanonicalPoseFrame destination)
        {
            var pelvis = destination.GetJoint(CanonicalJointId.Pelvis);
            var hasCanonicalRoot = pelvis.IsTracked && pelvis.hasWorldPosition;
            var root = hasCanonicalRoot ? pelvis.worldPosition : Vector3.zero;

            for (var i = 0; i < CanonicalPoseFrame.JointCount; i++)
            {
                var joint = destination.GetJoint((CanonicalJointId)i);
                if (!joint.IsTracked || !joint.hasWorldPosition)
                {
                    continue;
                }

                joint.localPosition = hasCanonicalRoot
                    ? CanonicalCoordinateSystem.RootRelative(joint.worldPosition, root)
                    : joint.worldPosition;
                joint.hasLocalPosition = true;
                destination.SetJoint(in joint);
            }
        }

        private static float ConfidenceFor(PoseLandmarkObservation source)
        {
            if (source.hasVisibility && source.hasPresence)
            {
                return Mathf.Clamp01(Mathf.Min(source.visibility, source.presence));
            }

            if (source.hasVisibility)
            {
                return Mathf.Clamp01(source.visibility);
            }

            if (source.hasPresence)
            {
                return Mathf.Clamp01(source.presence);
            }

            return source.IsTracked ? 1f : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
