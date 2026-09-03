using System;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Canonical
{
    public enum CanonicalJointId
    {
        Pelvis = 0,
        Spine = 1,
        Chest = 2,
        Head = 3,
        LeftShoulder = 4,
        LeftElbow = 5,
        LeftWrist = 6,
        RightShoulder = 7,
        RightElbow = 8,
        RightWrist = 9,
        LeftHip = 10,
        LeftKnee = 11,
        LeftAnkle = 12,
        LeftHeel = 13,
        LeftToe = 14,
        RightHip = 15,
        RightKnee = 16,
        RightAnkle = 17,
        RightHeel = 18,
        RightToe = 19,
    }

    public enum CanonicalTrackingState
    {
        Unavailable = 0,
        Tracked = 1,
    }

    [Serializable]
    public struct CanonicalPoseJoint
    {
        public CanonicalJointId id;
        public CanonicalTrackingState tracking;
        [Range(0f, 1f)] public float confidence;

        /// <summary>Golden Needle canonical image position: normalized X right, Y up.</summary>
        public Vector2 imagePosition;
        public bool hasImagePosition;

        /// <summary>Golden Needle canonical 3D position: X right, Y up, Z away.</summary>
        public Vector3 worldPosition;
        public bool hasWorldPosition;

        /// <summary>Canonical world position relative to the trusted canonical pelvis when available.</summary>
        public Vector3 localPosition;
        public bool hasLocalPosition;

        public bool IsTracked => tracking == CanonicalTrackingState.Tracked;
    }

    /// <summary>
    /// Preallocated, provider-independent canonical pose frame. A frame can contain a partial
    /// body; trackedJointCount and per-joint state are authoritative instead of an all-or-nothing
    /// pose flag.
    /// </summary>
    [Serializable]
    public sealed class CanonicalPoseFrame
    {
        public const int JointCount = 20;

        private readonly CanonicalPoseJoint[] _joints = new CanonicalPoseJoint[JointCount];

        public long sourceTimestampMillisec { get; private set; }
        public double receivedAtSeconds { get; private set; }
        public bool hasPose { get; private set; }
        public bool hasMeaningfulPose { get; private set; }
        public int trackedJointCount { get; private set; }
        public bool hasCanonicalPelvis { get; private set; }
        public bool hasCanonical3D { get; private set; }

        public CanonicalPoseFrame()
        {
            Clear();
        }

        public CanonicalPoseJoint GetJoint(CanonicalJointId id)
        {
            return _joints[(int)id];
        }

        public void Begin(long timestampMillisec, double receivedTimeSeconds, bool poseAvailable)
        {
            sourceTimestampMillisec = timestampMillisec;
            receivedAtSeconds = receivedTimeSeconds;
            hasPose = poseAvailable;
            hasMeaningfulPose = false;
            trackedJointCount = 0;
            hasCanonicalPelvis = false;
            hasCanonical3D = false;

            ClearJoints();
        }

        public void SetJoint(in CanonicalPoseJoint joint)
        {
            var index = (int)joint.id;
            var previous = _joints[index];
            if (previous.IsTracked)
            {
                trackedJointCount--;
            }

            _joints[index] = joint;
            if (joint.IsTracked)
            {
                trackedJointCount++;
            }
        }

        public void Complete()
        {
            hasMeaningfulPose = trackedJointCount > 0;
            hasCanonicalPelvis = GetJoint(CanonicalJointId.Pelvis).IsTracked;
            hasCanonical3D = false;
            for (var i = 0; i < _joints.Length; i++)
            {
                if (_joints[i].IsTracked && (_joints[i].hasLocalPosition || _joints[i].hasWorldPosition))
                {
                    hasCanonical3D = true;
                    break;
                }
            }
        }

        public void MarkUnavailable()
        {
            hasPose = false;
            hasMeaningfulPose = false;
            trackedJointCount = 0;
            hasCanonicalPelvis = false;
            hasCanonical3D = false;
            ClearJoints();
        }

        public void Clear()
        {
            sourceTimestampMillisec = 0;
            receivedAtSeconds = 0;
            hasPose = false;
            hasMeaningfulPose = false;
            trackedJointCount = 0;
            hasCanonicalPelvis = false;
            hasCanonical3D = false;
            ClearJoints();
        }

        private void ClearJoints()
        {
            for (var i = 0; i < _joints.Length; i++)
            {
                _joints[i] = new CanonicalPoseJoint
                {
                    id = (CanonicalJointId)i,
                    tracking = CanonicalTrackingState.Unavailable,
                    confidence = 0f,
                };
            }
        }
    }
}
