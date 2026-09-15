using GoldenNeedle.Core.Motion.Rotation;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    public enum HumanoidBindingMode
    {
        Unbound = 0,
        ExplicitDebug = 1,
        AnimatorHumanoid = 2,
    }

    /// <summary>
    /// Caches the driven humanoid transforms, four kinematic chain endpoints, and their
    /// bind/reference measurements. Live retargeting may change rotations only; authored local
    /// positions and local scales remain unchanged. Foundation E detail capability is additive and
    /// deliberately does not participate in legacy IsBound/BoundBoneCount semantics.
    /// </summary>
    public sealed class HumanoidRigBinding : MonoBehaviour
    {
        public const int ChainCount = CanonicalKinematicTargets.ChainCount;

        private static readonly CanonicalBoneId[] ChainRootBones =
        {
            CanonicalBoneId.LeftUpperArm,
            CanonicalBoneId.RightUpperArm,
            CanonicalBoneId.LeftUpperLeg,
            CanonicalBoneId.RightUpperLeg,
        };

        private static readonly CanonicalBoneId[] ChainMidBones =
        {
            CanonicalBoneId.LeftLowerArm,
            CanonicalBoneId.RightLowerArm,
            CanonicalBoneId.LeftLowerLeg,
            CanonicalBoneId.RightLowerLeg,
        };

        [Header("Binding selection")]
        [SerializeField] private Animator animator;
        [SerializeField] private bool preferAnimatorHumanoid;
        [SerializeField] private Transform avatarRoot;

        [Header("Explicit debug/reference transforms")]
        [SerializeField] private Transform hips;
        [SerializeField] private Transform chest;
        [SerializeField] private Transform leftUpperArm;
        [SerializeField] private Transform leftLowerArm;
        [SerializeField] private Transform rightUpperArm;
        [SerializeField] private Transform rightLowerArm;
        [SerializeField] private Transform leftUpperLeg;
        [SerializeField] private Transform leftLowerLeg;
        [SerializeField] private Transform rightUpperLeg;
        [SerializeField] private Transform rightLowerLeg;

        [Header("Explicit chain endpoints")]
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;

        private readonly Transform[] _bones = new Transform[CanonicalRotationFrame.BoneCount];
        private readonly Quaternion[] _bindWorldRotations = new Quaternion[CanonicalRotationFrame.BoneCount];
        private readonly Quaternion[] _bindLocalRotations = new Quaternion[CanonicalRotationFrame.BoneCount];
        private readonly Vector3[] _bindWorldSegmentDirections = new Vector3[CanonicalRotationFrame.BoneCount];
        private readonly Vector3[] _bindLocalPositions = new Vector3[CanonicalRotationFrame.BoneCount];
        private readonly Vector3[] _bindLocalScales = new Vector3[CanonicalRotationFrame.BoneCount];
        private readonly Transform[] _chainRoots = new Transform[ChainCount];
        private readonly Transform[] _chainMids = new Transform[ChainCount];
        private readonly Transform[] _chainTips = new Transform[ChainCount];
        private readonly Quaternion[] _chainRootBindLocalRotations = new Quaternion[ChainCount];
        private readonly Quaternion[] _chainMidBindLocalRotations = new Quaternion[ChainCount];
        private readonly float[] _chainUpperLengths = new float[ChainCount];
        private readonly float[] _chainLowerLengths = new float[ChainCount];
        private readonly float[] _chainTotalReaches = new float[ChainCount];
        private readonly Vector3[] _chainTipBindLocalPositions = new Vector3[ChainCount];
        private readonly Vector3[] _chainTipBindLocalScales = new Vector3[ChainCount];
        private readonly Quaternion[] _chainParentReferenceFrames = new Quaternion[ChainCount];
        private readonly Quaternion[] _chainReferenceFrames = new Quaternion[ChainCount];
        private readonly Vector3[] _chainReferenceRootToMidParentLocal = new Vector3[ChainCount];
        private readonly Vector3[] _chainReferenceRootToTipParentLocal = new Vector3[ChainCount];
        private readonly Vector3[] _chainReferenceBendDirections = new Vector3[ChainCount];
        private readonly bool[] _chainAvailable = new bool[ChainCount];
        private readonly HumanoidRigDetailCapabilities _detailCapabilities = new HumanoidRigDetailCapabilities();
        private Transform _boundAvatarRoot;
        private HumanoidBindingMode _bindingMode;
        private SignedAxisBasis _referenceBodyBasis;
        private bool _hasReferenceBodyBasis;
        private bool _isBound;
        public int ReferencePoseVersion { get; private set; }

        public bool IsBound => _isBound;
        public HumanoidBindingMode BindingMode => _bindingMode;
        public string BindingModeName => _bindingMode == HumanoidBindingMode.ExplicitDebug
            ? "Explicit Debug"
            : _bindingMode == HumanoidBindingMode.AnimatorHumanoid ? "Animator Humanoid" : "Unbound";
        public int BoundBoneCount { get; private set; }
        public HumanoidRigDetailCapabilities DetailCapabilities => _detailCapabilities;
        public int OptionalDetailBoneCount => _detailCapabilities.AvailableFingerBoneCount;
        public Transform AvatarRoot => _boundAvatarRoot;
        public Quaternion AvatarReferenceBodyRotation => _boundAvatarRoot == null ? Quaternion.identity : _boundAvatarRoot.rotation;

        /// <summary>
        /// Returns the avatar's bind/reference anatomical basis using actual bound joint positions.
        /// Unlike the canonical source basis, this target basis is a proper right-handed Unity
        /// basis and can safely be converted to a Quaternion after signed-axis mapping.
        /// </summary>
        public bool TryGetReferenceBodyBasis(out SignedAxisBasis basis)
        {
            basis = _referenceBodyBasis;
            return _isBound && _hasReferenceBodyBasis && basis.IsValid;
        }

        /// <summary>
        /// Diagnostic-only Animator Humanoid foot/toe forward evidence. Reads current Humanoid
        /// bone positions, projects toe-mid minus foot-mid onto the cached target reference ground
        /// plane, and never affects binding validity or production retarget behavior.
        /// </summary>
        public bool TryGetAnimatorFootForwardForDiagnostics(out Vector3 footForward)
        {
            footForward = Vector3.zero;
            if (!_isBound ||
                _bindingMode != HumanoidBindingMode.AnimatorHumanoid ||
                animator == null ||
                !_hasReferenceBodyBasis)
            {
                return false;
            }

            var leftFootTransform = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightFootTransform = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var leftToesTransform = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            var rightToesTransform = animator.GetBoneTransform(HumanBodyBones.RightToes);
            if (leftFootTransform == null ||
                rightFootTransform == null ||
                leftToesTransform == null ||
                rightToesTransform == null)
            {
                return false;
            }

            var footMid = (leftFootTransform.position + rightFootTransform.position) * 0.5f;
            var toeMid = (leftToesTransform.position + rightToesTransform.position) * 0.5f;
            var direction = toeMid - footMid;
            direction -= _referenceBodyBasis.Up *
                Vector3.Dot(direction, _referenceBodyBasis.Up);
            if (!IsFinite(direction) || direction.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            footForward = direction.normalized;
            return IsFinite(footForward);
        }

        private void Awake()
        {
            animator = animator == null ? GetComponentInChildren<Animator>() : animator;
            RebuildBinding();
        }

        private void Start()
        {
            // A procedural debug source builds its hierarchy during Awake, while Animator
            // references are also safe to resolve again after all sibling Start calls.
            RebuildBinding();
        }

        public void RebuildBinding()
        {
            ClearBinding();

            if (preferAnimatorHumanoid && TryBindAnimatorHumanoid())
            {
                _bindingMode = HumanoidBindingMode.AnimatorHumanoid;
            }
            else if (TryBindExplicitFields())
            {
                _bindingMode = HumanoidBindingMode.ExplicitDebug;
            }
            else if (TryBindExplicitSource())
            {
                _bindingMode = HumanoidBindingMode.ExplicitDebug;
            }

            if (AllBonesAvailable())
            {
                _isBound = true;
                BoundBoneCount = CanonicalRotationFrame.BoneCount;
                BindOptionalDetailCapabilities();
                CaptureReferencePose();
            }
            else
            {
                ClearBinding();
            }
        }

        public Transform GetBoneTransform(CanonicalBoneId id)
        {
            return _bones[(int)id];
        }

        public Quaternion GetBindWorldRotation(CanonicalBoneId id)
        {
            return _bindWorldRotations[(int)id];
        }

        public Quaternion GetBindLocalRotation(CanonicalBoneId id)
        {
            return _bindLocalRotations[(int)id];
        }

        public Vector3 GetBindLocalPosition(CanonicalBoneId id)
        {
            return _bindLocalPositions[(int)id];
        }

        public Vector3 GetBindLocalScale(CanonicalBoneId id)
        {
            return _bindLocalScales[(int)id];
        }

        public bool TryGetBindWorldSegmentDirection(CanonicalBoneId id, out Vector3 direction)
        {
            direction = _bindWorldSegmentDirections[(int)id];
            return direction.sqrMagnitude > 0.000001f;
        }

        public bool IsChainAvailable(CanonicalKinematicChainId id)
        {
            return _chainAvailable[(int)id];
        }

        public Transform GetChainRoot(CanonicalKinematicChainId id)
        {
            return _chainRoots[(int)id];
        }

        public Transform GetChainMid(CanonicalKinematicChainId id)
        {
            return _chainMids[(int)id];
        }

        public Transform GetChainTip(CanonicalKinematicChainId id)
        {
            return _chainTips[(int)id];
        }

        public float GetChainUpperLength(CanonicalKinematicChainId id)
        {
            return _chainUpperLengths[(int)id];
        }

        public float GetChainLowerLength(CanonicalKinematicChainId id)
        {
            return _chainLowerLengths[(int)id];
        }

        public float GetChainTotalReach(CanonicalKinematicChainId id)
        {
            return _chainTotalReaches[(int)id];
        }

        public bool TryGetChainReference(
            CanonicalKinematicChainId id,
            out Quaternion parentReferenceFrame,
            out Quaternion chainReferenceFrame,
            out Vector3 rootToMidParentLocal,
            out Vector3 rootToTipParentLocal,
            out Vector3 bendDirection)
        {
            var index = (int)id;
            parentReferenceFrame = _chainParentReferenceFrames[index];
            chainReferenceFrame = _chainReferenceFrames[index];
            rootToMidParentLocal = _chainReferenceRootToMidParentLocal[index];
            rootToTipParentLocal = _chainReferenceRootToTipParentLocal[index];
            bendDirection = _chainReferenceBendDirections[index];
            return _chainAvailable[index] &&
                IsFinite(parentReferenceFrame) && IsFinite(chainReferenceFrame) &&
                IsFinite(rootToMidParentLocal) && IsFinite(rootToTipParentLocal) &&
                IsFinite(bendDirection);
        }

        public bool TryGetCurrentParentFrame(CanonicalKinematicChainId id, out Quaternion frame)
        {
            frame = Quaternion.identity;
            var index = (int)id;
            var left = _bones[(int)(IsArm(id) ? CanonicalBoneId.LeftUpperArm : CanonicalBoneId.LeftUpperLeg)];
            var right = _bones[(int)(IsArm(id) ? CanonicalBoneId.RightUpperArm : CanonicalBoneId.RightUpperLeg)];
            var pelvis = _bones[(int)CanonicalBoneId.Pelvis];
            var chest = _bones[(int)CanonicalBoneId.Chest];
            if (left != null && right != null && pelvis != null && chest != null)
            {
                var lateral = right.position - left.position;
                var up = chest.position - pelvis.position;
                var reference = _chainParentReferenceFrames[index];
                if (HumanoidRetargetingMath.TryBuildAnatomicalFrame(
                        lateral,
                        up,
                        reference * Vector3.forward,
                        out frame))
                {
                    return true;
                }
            }

            frame = _chainParentReferenceFrames[index];
            return _chainAvailable[index] && IsFinite(frame);
        }

        public Quaternion GetChainRootBindLocalRotation(CanonicalKinematicChainId id)
        {
            return _chainRootBindLocalRotations[(int)id];
        }

        public Quaternion GetChainMidBindLocalRotation(CanonicalKinematicChainId id)
        {
            return _chainMidBindLocalRotations[(int)id];
        }

        public Vector3 GetChainTipBindLocalPosition(CanonicalKinematicChainId id)
        {
            return _chainTipBindLocalPositions[(int)id];
        }

        public Vector3 GetChainTipBindLocalScale(CanonicalKinematicChainId id)
        {
            return _chainTipBindLocalScales[(int)id];
        }

        public void RestoreChainReferencePose(CanonicalKinematicChainId id)
        {
            var index = (int)id;
            var root = _chainRoots[index];
            var mid = _chainMids[index];
            if (root != null)
            {
                root.localRotation = _chainRootBindLocalRotations[index];
            }

            if (mid != null)
            {
                mid.localRotation = _chainMidBindLocalRotations[index];
            }
        }

        public void ResetToReferencePose()
        {
            if (!_isBound)
            {
                return;
            }

            for (var i = 0; i < _bones.Length; i++)
            {
                _bones[i].localRotation = _bindLocalRotations[i];
            }
        }

        public void ConfigureExplicit(Transform explicitAvatarRoot, Transform[] explicitBones)
        {
            ConfigureExplicit(explicitAvatarRoot, explicitBones, null);
        }

        public void ConfigureExplicit(Transform explicitAvatarRoot, Transform[] explicitBones, Transform[] explicitChainTips)
        {
            ClearBinding();
            if (explicitBones == null || explicitBones.Length < CanonicalRotationFrame.BoneCount)
            {
                return;
            }

            avatarRoot = explicitAvatarRoot;
            for (var i = 0; i < _bones.Length; i++)
            {
                _bones[i] = explicitBones[i];
            }

            if (explicitChainTips != null)
            {
                for (var i = 0; i < ChainCount && i < explicitChainTips.Length; i++)
                {
                    _chainTips[i] = explicitChainTips[i];
                }
            }

            _boundAvatarRoot = avatarRoot;
            _bindingMode = HumanoidBindingMode.ExplicitDebug;
            _isBound = AllBonesAvailable();
            BoundBoneCount = _isBound ? CanonicalRotationFrame.BoneCount : 0;
            if (_isBound)
            {
                BindOptionalDetailCapabilities();
                CaptureReferencePose();
            }
        }

        private bool TryBindAnimatorHumanoid()
        {
            if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
            {
                return false;
            }

            _boundAvatarRoot = avatarRoot == null ? animator.transform : avatarRoot;
            _bones[(int)CanonicalBoneId.Pelvis] = animator.GetBoneTransform(HumanBodyBones.Hips);
            _bones[(int)CanonicalBoneId.Chest] = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (_bones[(int)CanonicalBoneId.Chest] == null)
            {
                _bones[(int)CanonicalBoneId.Chest] = animator.GetBoneTransform(HumanBodyBones.Spine);
            }

            _bones[(int)CanonicalBoneId.LeftUpperArm] = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _bones[(int)CanonicalBoneId.LeftLowerArm] = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _bones[(int)CanonicalBoneId.RightUpperArm] = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _bones[(int)CanonicalBoneId.RightLowerArm] = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _bones[(int)CanonicalBoneId.LeftUpperLeg] = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _bones[(int)CanonicalBoneId.LeftLowerLeg] = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _bones[(int)CanonicalBoneId.RightUpperLeg] = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _bones[(int)CanonicalBoneId.RightLowerLeg] = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

            _chainTips[(int)CanonicalKinematicChainId.LeftArm] = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _chainTips[(int)CanonicalKinematicChainId.RightArm] = animator.GetBoneTransform(HumanBodyBones.RightHand);
            _chainTips[(int)CanonicalKinematicChainId.LeftLeg] = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _chainTips[(int)CanonicalKinematicChainId.RightLeg] = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            return true;
        }

        private bool TryBindExplicitFields()
        {
            _boundAvatarRoot = avatarRoot;
            _bones[(int)CanonicalBoneId.Pelvis] = hips;
            _bones[(int)CanonicalBoneId.Chest] = chest;
            _bones[(int)CanonicalBoneId.LeftUpperArm] = leftUpperArm;
            _bones[(int)CanonicalBoneId.LeftLowerArm] = leftLowerArm;
            _bones[(int)CanonicalBoneId.RightUpperArm] = rightUpperArm;
            _bones[(int)CanonicalBoneId.RightLowerArm] = rightLowerArm;
            _bones[(int)CanonicalBoneId.LeftUpperLeg] = leftUpperLeg;
            _bones[(int)CanonicalBoneId.LeftLowerLeg] = leftLowerLeg;
            _bones[(int)CanonicalBoneId.RightUpperLeg] = rightUpperLeg;
            _bones[(int)CanonicalBoneId.RightLowerLeg] = rightLowerLeg;
            _chainTips[(int)CanonicalKinematicChainId.LeftArm] = leftHand;
            _chainTips[(int)CanonicalKinematicChainId.RightArm] = rightHand;
            _chainTips[(int)CanonicalKinematicChainId.LeftLeg] = leftFoot;
            _chainTips[(int)CanonicalKinematicChainId.RightLeg] = rightFoot;
            return AllBonesAvailable() && _boundAvatarRoot != null;
        }

        private bool TryBindExplicitSource()
        {
            var components = GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (!(components[i] is IExplicitHumanoidRigSource source))
                {
                    continue;
                }

                ClearChainTips();
                if (!source.TryGetBinding(_bones, out _boundAvatarRoot))
                {
                    continue;
                }

                if (components[i] is IExplicitHumanoidChainSource chainSource)
                {
                    for (var chain = 0; chain < ChainCount; chain++)
                    {
                        chainSource.TryGetChainTip((CanonicalKinematicChainId)chain, out _chainTips[chain]);
                    }
                }

                return AllBonesAvailable() && _boundAvatarRoot != null;
            }

            return false;
        }

        private void BindOptionalDetailCapabilities()
        {
            _detailCapabilities.Clear();
            if (_bindingMode == HumanoidBindingMode.AnimatorHumanoid)
            {
                _detailCapabilities.BindAnimator(animator);
                return;
            }

            if (_bindingMode != HumanoidBindingMode.ExplicitDebug)
            {
                return;
            }

            var components = GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is IExplicitHumanoidDetailRigSource detailSource)
                {
                    _detailCapabilities.BindExplicit(detailSource);
                    return;
                }
            }
        }

        private void CaptureReferencePose()
        {
            ReferencePoseVersion++;
            for (var i = 0; i < _bones.Length; i++)
            {
                _bindWorldRotations[i] = _bones[i].rotation;
                _bindLocalRotations[i] = _bones[i].localRotation;
                _bindWorldSegmentDirections[i] = FindSegmentDirection(_bones[i]);
                _bindLocalPositions[i] = _bones[i].localPosition;
                _bindLocalScales[i] = _bones[i].localScale;
            }

            CaptureReferenceBodyBasis();
            CaptureParentReferenceFrames();
            CaptureChainReferences();
        }

        private void CaptureReferenceBodyBasis()
        {
            _referenceBodyBasis = default;
            _hasReferenceBodyBasis = false;
            var left = _bones[(int)CanonicalBoneId.LeftUpperArm];
            var right = _bones[(int)CanonicalBoneId.RightUpperArm];
            var pelvis = _bones[(int)CanonicalBoneId.Pelvis];
            var chest = _bones[(int)CanonicalBoneId.Chest];
            if (left != null && right != null && pelvis != null && chest != null &&
                HumanoidRetargetingMath.TryBuildRightHandedBasis(
                    right.position - left.position,
                    chest.position - pelvis.position,
                    out var basis))
            {
                _referenceBodyBasis = basis;
                _hasReferenceBodyBasis = true;
            }
        }

        private void CaptureParentReferenceFrames()
        {
            var referenceForward = _boundAvatarRoot == null
                ? Vector3.forward
                : _boundAvatarRoot.rotation * Vector3.forward;
            if (!TryBuildParentReferenceFrame(
                    CanonicalBoneId.LeftUpperArm,
                    CanonicalBoneId.RightUpperArm,
                    referenceForward,
                    out var chestFrame))
            {
                chestFrame = _boundAvatarRoot == null ? Quaternion.identity : _boundAvatarRoot.rotation;
            }

            if (!TryBuildParentReferenceFrame(
                    CanonicalBoneId.LeftUpperLeg,
                    CanonicalBoneId.RightUpperLeg,
                    referenceForward,
                    out var pelvisFrame))
            {
                pelvisFrame = _boundAvatarRoot == null ? Quaternion.identity : _boundAvatarRoot.rotation;
            }

            for (var i = 0; i < ChainCount; i++)
            {
                _chainParentReferenceFrames[i] = IsArm((CanonicalKinematicChainId)i)
                    ? chestFrame
                    : pelvisFrame;
            }
        }

        private bool TryBuildParentReferenceFrame(
            CanonicalBoneId leftBone,
            CanonicalBoneId rightBone,
            Vector3 referenceForward,
            out Quaternion frame)
        {
            frame = Quaternion.identity;
            var left = _bones[(int)leftBone];
            var right = _bones[(int)rightBone];
            var pelvis = _bones[(int)CanonicalBoneId.Pelvis];
            var chest = _bones[(int)CanonicalBoneId.Chest];
            return left != null && right != null && pelvis != null && chest != null &&
                HumanoidRetargetingMath.TryBuildAnatomicalFrame(
                    right.position - left.position,
                    chest.position - pelvis.position,
                    referenceForward,
                    out frame);
        }

        private void CaptureChainReferences()
        {
            for (var i = 0; i < ChainCount; i++)
            {
                var root = _bones[(int)ChainRootBones[i]];
                var mid = _bones[(int)ChainMidBones[i]];
                var tip = _chainTips[i] == null ? FindFirstNonRendererChild(mid) : _chainTips[i];
                _chainRoots[i] = root;
                _chainMids[i] = mid;
                _chainTips[i] = tip;
                _chainAvailable[i] = root != null && mid != null && tip != null;
                _chainRootBindLocalRotations[i] = root == null ? Quaternion.identity : root.localRotation;
                _chainMidBindLocalRotations[i] = mid == null ? Quaternion.identity : mid.localRotation;
                _chainTipBindLocalPositions[i] = tip == null ? Vector3.zero : tip.localPosition;
                _chainTipBindLocalScales[i] = tip == null ? Vector3.zero : tip.localScale;

                var upperLength = root == null || mid == null ? 0f : Vector3.Distance(root.position, mid.position);
                var lowerLength = mid == null || tip == null ? 0f : Vector3.Distance(mid.position, tip.position);
                _chainUpperLengths[i] = IsFinite(upperLength) ? upperLength : 0f;
                _chainLowerLengths[i] = IsFinite(lowerLength) ? lowerLength : 0f;
                _chainTotalReaches[i] = _chainUpperLengths[i] + _chainLowerLengths[i];
                _chainAvailable[i] = _chainAvailable[i] &&
                                     _chainUpperLengths[i] > 0.000001f &&
                                     _chainLowerLengths[i] > 0.000001f &&
                                     IsFinite(_chainTotalReaches[i]);

                if (_chainAvailable[i])
                {
                    var parentFrame = _chainParentReferenceFrames[i];
                    var parentInverse = Quaternion.Inverse(parentFrame);
                    var rootToMid = parentInverse * (mid.position - root.position);
                    var rootToTip = parentInverse * (tip.position - root.position);
                    var fallbackSecondary = IsArm((CanonicalKinematicChainId)i)
                        ? Vector3.up
                        : Vector3.forward;
                    _chainReferenceRootToMidParentLocal[i] = rootToMid;
                    _chainReferenceRootToTipParentLocal[i] = rootToTip;
                    _chainAvailable[i] = HumanoidRetargetingMath.TryBuildChainReferenceFrame(
                        rootToTip,
                        rootToMid,
                        fallbackSecondary,
                        out _chainReferenceFrames[i],
                        out _chainReferenceBendDirections[i]);
                }
            }
        }

        private static Transform FindFirstNonRendererChild(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null && child.GetComponent<Renderer>() == null)
                {
                    var direction = child.position - parent.position;
                    if (direction.sqrMagnitude > 0.000001f)
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        private static Vector3 FindSegmentDirection(Transform bone)
        {
            if (bone == null)
            {
                return Vector3.zero;
            }

            var child = FindFirstNonRendererChild(bone);
            return child == null ? Vector3.zero : (child.position - bone.position).normalized;
        }

        private bool AllBonesAvailable()
        {
            if (_boundAvatarRoot == null)
            {
                return false;
            }

            for (var i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private void ClearBinding()
        {
            for (var i = 0; i < _bones.Length; i++)
            {
                _bones[i] = null;
            }

            for (var i = 0; i < ChainCount; i++)
            {
                _chainRoots[i] = null;
                _chainMids[i] = null;
                _chainTips[i] = null;
                _chainAvailable[i] = false;
                _chainUpperLengths[i] = 0f;
                _chainLowerLengths[i] = 0f;
                _chainTotalReaches[i] = 0f;
                _chainTipBindLocalPositions[i] = Vector3.zero;
                _chainTipBindLocalScales[i] = Vector3.zero;
                _chainParentReferenceFrames[i] = Quaternion.identity;
                _chainReferenceFrames[i] = Quaternion.identity;
                _chainReferenceRootToMidParentLocal[i] = Vector3.zero;
                _chainReferenceRootToTipParentLocal[i] = Vector3.zero;
                _chainReferenceBendDirections[i] = Vector3.zero;
            }

            _detailCapabilities.Clear();
            _referenceBodyBasis = default;
            _hasReferenceBodyBasis = false;
            _boundAvatarRoot = null;
            _bindingMode = HumanoidBindingMode.Unbound;
            _isBound = false;
            BoundBoneCount = 0;
        }

        private void ClearChainTips()
        {
            for (var i = 0; i < ChainCount; i++)
            {
                _chainTips[i] = null;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsArm(CanonicalKinematicChainId id)
        {
            return id == CanonicalKinematicChainId.LeftArm || id == CanonicalKinematicChainId.RightArm;
        }
    }
}
