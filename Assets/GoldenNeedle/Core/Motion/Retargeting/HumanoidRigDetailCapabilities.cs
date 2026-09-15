using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    public enum HumanoidFingerBoneId
    {
        LeftThumbProximal = 0,
        LeftThumbIntermediate = 1,
        LeftThumbDistal = 2,
        LeftIndexProximal = 3,
        LeftIndexIntermediate = 4,
        LeftIndexDistal = 5,
        LeftMiddleProximal = 6,
        LeftMiddleIntermediate = 7,
        LeftMiddleDistal = 8,
        LeftRingProximal = 9,
        LeftRingIntermediate = 10,
        LeftRingDistal = 11,
        LeftLittleProximal = 12,
        LeftLittleIntermediate = 13,
        LeftLittleDistal = 14,
        RightThumbProximal = 15,
        RightThumbIntermediate = 16,
        RightThumbDistal = 17,
        RightIndexProximal = 18,
        RightIndexIntermediate = 19,
        RightIndexDistal = 20,
        RightMiddleProximal = 21,
        RightMiddleIntermediate = 22,
        RightMiddleDistal = 23,
        RightRingProximal = 24,
        RightRingIntermediate = 25,
        RightRingDistal = 26,
        RightLittleProximal = 27,
        RightLittleIntermediate = 28,
        RightLittleDistal = 29,
    }

    /// <summary>
    /// Cached optional detail capability set. None of these transforms participate in legacy
    /// HumanoidRigBinding.IsBound/BoundBoneCount validity. Zero and partial capability sets are valid.
    /// </summary>
    public sealed class HumanoidRigDetailCapabilities
    {
        public const int FingerBoneCount = 30;

        private static readonly HumanBodyBones[] AnimatorFingerBones =
        {
            HumanBodyBones.LeftThumbProximal,
            HumanBodyBones.LeftThumbIntermediate,
            HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal,
            HumanBodyBones.LeftIndexIntermediate,
            HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal,
            HumanBodyBones.LeftMiddleIntermediate,
            HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal,
            HumanBodyBones.LeftRingIntermediate,
            HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal,
            HumanBodyBones.LeftLittleIntermediate,
            HumanBodyBones.LeftLittleDistal,
            HumanBodyBones.RightThumbProximal,
            HumanBodyBones.RightThumbIntermediate,
            HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal,
            HumanBodyBones.RightIndexIntermediate,
            HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal,
            HumanBodyBones.RightMiddleIntermediate,
            HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal,
            HumanBodyBones.RightRingIntermediate,
            HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal,
            HumanBodyBones.RightLittleIntermediate,
            HumanBodyBones.RightLittleDistal,
        };

        private readonly Transform[] _fingerBones = new Transform[FingerBoneCount];
        private readonly Transform[] _segmentTips = new Transform[FingerBoneCount];
        private readonly Quaternion[] _referenceLocalRotations = new Quaternion[FingerBoneCount];
        private readonly Vector3[] _referenceLocalPositions = new Vector3[FingerBoneCount];
        private readonly Vector3[] _referenceLocalScales = new Vector3[FingerBoneCount];
        private readonly Vector3[] _referenceWorldSegmentDirections = new Vector3[FingerBoneCount];

        public int AvailableFingerBoneCount { get; private set; }
        public int ReferenceVersion { get; private set; }

        public Transform GetFingerBone(HumanoidFingerBoneId id) => _fingerBones[(int)id];
        public Transform GetSegmentTip(HumanoidFingerBoneId id) => _segmentTips[(int)id];
        public Quaternion GetReferenceLocalRotation(HumanoidFingerBoneId id) => _referenceLocalRotations[(int)id];
        public Vector3 GetReferenceLocalPosition(HumanoidFingerBoneId id) => _referenceLocalPositions[(int)id];
        public Vector3 GetReferenceLocalScale(HumanoidFingerBoneId id) => _referenceLocalScales[(int)id];
        public Vector3 GetReferenceWorldSegmentDirection(HumanoidFingerBoneId id) => _referenceWorldSegmentDirections[(int)id];
        public bool HasFingerBone(HumanoidFingerBoneId id) => _fingerBones[(int)id] != null;

        public void BindAnimator(Animator animator)
        {
            Clear();
            if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
            {
                return;
            }

            for (var i = 0; i < FingerBoneCount; i++)
            {
                _fingerBones[i] = animator.GetBoneTransform(AnimatorFingerBones[i]);
            }
            CompleteBinding();
        }

        public void BindExplicit(IExplicitHumanoidDetailRigSource source)
        {
            Clear();
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < FingerBoneCount; i++)
            {
                source.TryGetDetailFingerBone((HumanoidFingerBoneId)i, out _fingerBones[i]);
            }
            CompleteBinding();
        }

        public void Clear()
        {
            AvailableFingerBoneCount = 0;
            for (var i = 0; i < FingerBoneCount; i++)
            {
                _fingerBones[i] = null;
                _segmentTips[i] = null;
                _referenceLocalRotations[i] = Quaternion.identity;
                _referenceLocalPositions[i] = Vector3.zero;
                _referenceLocalScales[i] = Vector3.one;
                _referenceWorldSegmentDirections[i] = Vector3.zero;
            }
        }

        public void CaptureReference()
        {
            ReferenceVersion++;
            for (var i = 0; i < FingerBoneCount; i++)
            {
                var bone = _fingerBones[i];
                if (bone == null)
                {
                    continue;
                }

                _referenceLocalRotations[i] = bone.localRotation;
                _referenceLocalPositions[i] = bone.localPosition;
                _referenceLocalScales[i] = bone.localScale;
                var tip = ResolveSegmentTip(i, bone);
                _segmentTips[i] = tip;
                var direction = tip == null ? Vector3.zero : tip.position - bone.position;
                _referenceWorldSegmentDirections[i] =
                    IsFinite(direction) && direction.sqrMagnitude > 0.00000001f
                        ? direction.normalized
                        : Vector3.zero;
            }
        }

        public void ResetRotationsToReference()
        {
            for (var i = 0; i < FingerBoneCount; i++)
            {
                var bone = _fingerBones[i];
                if (bone != null)
                {
                    bone.localRotation = _referenceLocalRotations[i];
                }
            }
        }

        public bool AuthoredPositionsAndScalesIntact(float tolerance = 0.000001f)
        {
            for (var i = 0; i < FingerBoneCount; i++)
            {
                var bone = _fingerBones[i];
                if (bone == null)
                {
                    continue;
                }

                if ((bone.localPosition - _referenceLocalPositions[i]).sqrMagnitude > tolerance * tolerance ||
                    (bone.localScale - _referenceLocalScales[i]).sqrMagnitude > tolerance * tolerance)
                {
                    return false;
                }
            }
            return true;
        }

        private void CompleteBinding()
        {
            AvailableFingerBoneCount = 0;
            for (var i = 0; i < FingerBoneCount; i++)
            {
                if (_fingerBones[i] != null)
                {
                    AvailableFingerBoneCount++;
                }
            }
            CaptureReference();
        }

        private Transform ResolveSegmentTip(int index, Transform bone)
        {
            var segment = index % 15;
            var withinFinger = segment % 3;
            if (withinFinger < 2 && index + 1 < FingerBoneCount)
            {
                var next = _fingerBones[index + 1];
                if (next != null && next.parent == bone)
                {
                    return next;
                }
            }

            for (var i = 0; i < bone.childCount; i++)
            {
                var child = bone.GetChild(i);
                if (child == null)
                {
                    continue;
                }
                var delta = child.position - bone.position;
                if (IsFinite(delta) && delta.sqrMagnitude > 0.00000001f)
                {
                    return child;
                }
            }
            return null;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
