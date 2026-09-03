using System;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Rotation
{
    public enum CanonicalBoneId
    {
        Pelvis = 0,
        Chest = 1,
        LeftUpperArm = 2,
        LeftLowerArm = 3,
        RightUpperArm = 4,
        RightLowerArm = 5,
        LeftUpperLeg = 6,
        LeftLowerLeg = 7,
        RightUpperLeg = 8,
        RightLowerLeg = 9,
    }

    [Serializable]
    public struct CanonicalBoneRotation
    {
        public CanonicalBoneId id;
        public CanonicalTrackingState tracking;
        [Range(0f, 1f)] public float confidence;
        public Quaternion rotationDeltaFromCalibration;
        public Vector3 referenceDirection;
        public Vector3 currentDirection;

        public bool IsTracked => tracking == CanonicalTrackingState.Tracked;
    }

    /// <summary>
    /// Preallocated provider-independent rotation output. Each driven bone is independently
    /// available so partial-body motion remains useful.
    /// </summary>
    [Serializable]
    public sealed class CanonicalRotationFrame
    {
        public const int BoneCount = 10;

        private readonly CanonicalBoneRotation[] _bones = new CanonicalBoneRotation[BoneCount];

        public long sourceTimestampMillisec { get; private set; }
        public double receivedAtSeconds { get; private set; }
        public bool calibrationValid { get; private set; }
        public bool hasMeaningfulRotation { get; private set; }
        public int validBoneCount { get; private set; }

        public CanonicalRotationFrame()
        {
            Clear();
        }

        public CanonicalBoneRotation GetBone(CanonicalBoneId id)
        {
            return _bones[(int)id];
        }

        public void Begin(long timestampMillisec, double receivedTimeSeconds, bool profileValid)
        {
            sourceTimestampMillisec = timestampMillisec;
            receivedAtSeconds = receivedTimeSeconds;
            calibrationValid = profileValid;
            hasMeaningfulRotation = false;
            validBoneCount = 0;
            ClearBones();
        }

        public void SetBone(in CanonicalBoneRotation bone)
        {
            var index = (int)bone.id;
            var previous = _bones[index];
            if (previous.IsTracked)
            {
                validBoneCount--;
            }

            _bones[index] = bone;
            if (bone.IsTracked)
            {
                validBoneCount++;
            }
        }

        public void Complete()
        {
            hasMeaningfulRotation = validBoneCount > 0;
        }

        public void Clear()
        {
            sourceTimestampMillisec = 0;
            receivedAtSeconds = 0d;
            calibrationValid = false;
            hasMeaningfulRotation = false;
            validBoneCount = 0;
            ClearBones();
        }

        private void ClearBones()
        {
            for (var i = 0; i < _bones.Length; i++)
            {
                _bones[i] = new CanonicalBoneRotation
                {
                    id = (CanonicalBoneId)i,
                    tracking = CanonicalTrackingState.Unavailable,
                    confidence = 0f,
                    rotationDeltaFromCalibration = Quaternion.identity,
                    referenceDirection = Vector3.zero,
                    currentDirection = Vector3.zero,
                };
            }
        }
    }
}
