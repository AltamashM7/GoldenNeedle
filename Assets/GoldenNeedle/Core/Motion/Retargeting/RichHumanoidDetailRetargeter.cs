using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Hands;
using GoldenNeedle.Core.Motion.Rich;
using GoldenNeedle.Core.Motion.Rotation;
using GoldenNeedle.Core.Motion.Runtime;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// Foundation E additive post-Phase-4 detail layer. Phase 4 remains the endpoint/swing owner;
    /// this component contributes only rich axial limb twist plus optional palm/finger detail.
    /// </summary>
    public sealed class RichHumanoidDetailRetargeter : MonoBehaviour, IHumanoidPostSolveDetailLayer
    {
        private const int RichLimbChannelCount = 8;
        private const float ReferenceChangeEpsilon = 0.000001f;

        private static readonly RichOrientationChannelId[] RichChannels =
        {
            RichOrientationChannelId.LeftUpperArm,
            RichOrientationChannelId.LeftLowerArm,
            RichOrientationChannelId.RightUpperArm,
            RichOrientationChannelId.RightLowerArm,
            RichOrientationChannelId.LeftUpperLeg,
            RichOrientationChannelId.LeftLowerLeg,
            RichOrientationChannelId.RightUpperLeg,
            RichOrientationChannelId.RightLowerLeg,
        };

        private static readonly CanonicalBoneId[] RichBones =
        {
            CanonicalBoneId.LeftUpperArm,
            CanonicalBoneId.LeftLowerArm,
            CanonicalBoneId.RightUpperArm,
            CanonicalBoneId.RightLowerArm,
            CanonicalBoneId.LeftUpperLeg,
            CanonicalBoneId.LeftLowerLeg,
            CanonicalBoneId.RightUpperLeg,
            CanonicalBoneId.RightLowerLeg,
        };

        private static readonly HandLandmarkId[,] FingerSegmentLandmarks =
        {
            { HandLandmarkId.ThumbCmc, HandLandmarkId.ThumbMcp },
            { HandLandmarkId.ThumbMcp, HandLandmarkId.ThumbIp },
            { HandLandmarkId.ThumbIp, HandLandmarkId.ThumbTip },
            { HandLandmarkId.IndexMcp, HandLandmarkId.IndexPip },
            { HandLandmarkId.IndexPip, HandLandmarkId.IndexDip },
            { HandLandmarkId.IndexDip, HandLandmarkId.IndexTip },
            { HandLandmarkId.MiddleMcp, HandLandmarkId.MiddlePip },
            { HandLandmarkId.MiddlePip, HandLandmarkId.MiddleDip },
            { HandLandmarkId.MiddleDip, HandLandmarkId.MiddleTip },
            { HandLandmarkId.RingMcp, HandLandmarkId.RingPip },
            { HandLandmarkId.RingPip, HandLandmarkId.RingDip },
            { HandLandmarkId.RingDip, HandLandmarkId.RingTip },
            { HandLandmarkId.PinkyMcp, HandLandmarkId.PinkyPip },
            { HandLandmarkId.PinkyPip, HandLandmarkId.PinkyDip },
            { HandLandmarkId.PinkyDip, HandLandmarkId.PinkyTip },
        };

        [SerializeField] private MotionEngineRuntime bodyRuntime;
        [SerializeField] private HandMotionRuntime handRuntime;
        [SerializeField] private bool enableFoundationE = true;
        [SerializeField] private bool enableRichLimbAxialDetail = true;
        [SerializeField] private bool enablePalmOrientation = true;
        [SerializeField] private bool enableFingerArticulation = true;
        [SerializeField, Min(0.1f)] private float detailResponse = 24f;

        private readonly HumanoidRigDetailCapabilities _capabilities = new HumanoidRigDetailCapabilities();
        private readonly CanonicalAnatomicalBasis[] _richReferenceBases = new CanonicalAnatomicalBasis[RichLimbChannelCount];
        private readonly bool[] _hasRichReference = new bool[RichLimbChannelCount];
        private readonly float[] _richTargetDegrees = new float[RichLimbChannelCount];
        private readonly float[] _richCurrentDegrees = new float[RichLimbChannelCount];
        private readonly CanonicalAnatomicalBasis[] _handReferenceBases = new CanonicalAnatomicalBasis[2];
        private readonly bool[] _handReferenceValid = new bool[2];
        private readonly bool[] _palmWasFresh = new bool[2];
        private readonly bool[] _targetPalmReferenceValid = new bool[2];
        private readonly Vector3[] _targetPalmPrimaryParentLocal = new Vector3[2];
        private readonly Vector3[] _targetPalmSecondaryParentLocal = new Vector3[2];
        private readonly Vector3[] _targetPalmThirdParentLocal = new Vector3[2];
        private readonly Quaternion[] _fingerCurrentLocalRotations = new Quaternion[HumanoidRigDetailCapabilities.FingerBoneCount];

        private int _bindingReferenceVersion = -1;
        private string _richProviderId = string.Empty;
        private long _lastRichTimestamp;
        private bool _richWasAvailable;
        private MotionCalibrationProfile _profile;
        private Vector3 _profileRight;
        private Vector3 _profileUp;
        private Vector3 _profileForward;
        private float _maxEndpointResidual;

        public bool IsPostSolveDetailEnabled => enableFoundationE;
        public bool RichLimbAxialDetailEnabled => enableRichLimbAxialDetail;
        public bool PalmOrientationEnabled => enablePalmOrientation;
        public bool FingerArticulationEnabled => enableFingerArticulation;
        public int OptionalFingerBoneCount => _capabilities.AvailableFingerBoneCount;
        public float MaxEndpointPreservationResidual => _maxEndpointResidual;

        private void Awake()
        {
            ResolveRuntimeReferences();
        }

        private void Start()
        {
            ResolveRuntimeReferences();
        }

        private void OnValidate()
        {
            detailResponse = Mathf.Max(0.1f, detailResponse);
        }

        public void ApplyPostSolveDetail(HumanoidRigBinding binding, float deltaTime)
        {
            if (!enableFoundationE || binding == null || !binding.IsBound)
            {
                ResetPostSolveDetailState(binding);
                return;
            }

            ResolveRuntimeReferences();
            EnsureCapabilityAndReferenceState(binding);
            _maxEndpointResidual = 0f;

            if (enableRichLimbAxialDetail)
            {
                ApplyRichLimbTwist(binding, Mathf.Max(0f, deltaTime));
            }
            else
            {
                ClearRichTargetsAndContributions();
            }

            ApplyHands(binding, Mathf.Max(0f, deltaTime));
        }

        public void ResetPostSolveDetailState(HumanoidRigBinding binding)
        {
            ClearRichTargetsAndContributions();
            ClearPalmSourceReferences();
            RestorePalmSideToReferenceImmediate(binding, CanonicalHandSide.Left);
            RestorePalmSideToReferenceImmediate(binding, CanonicalHandSide.Right);
            if (_capabilities.AvailableFingerBoneCount > 0)
            {
                _capabilities.ResetRotationsToReference();
                CaptureFingerCurrentRotations();
            }
        }

        private void ResolveRuntimeReferences()
        {
            bodyRuntime = bodyRuntime == null ? GetComponent<MotionEngineRuntime>() : bodyRuntime;
            handRuntime = handRuntime == null ? GetComponent<HandMotionRuntime>() : handRuntime;
        }

        private void EnsureCapabilityAndReferenceState(HumanoidRigBinding binding)
        {
            var profile = bodyRuntime == null || bodyRuntime.Calibration == null
                ? null
                : bodyRuntime.Calibration.Profile;
            var profileChanged = ProfileReferenceChanged(profile);
            if (_bindingReferenceVersion != binding.ReferencePoseVersion)
            {
                _bindingReferenceVersion = binding.ReferencePoseVersion;
                BindOptionalFingerCapabilities(binding);
                CaptureTargetPalmBindingReferences(binding);
                profileChanged = true;
            }

            if (profileChanged)
            {
                ResetSourceReferences(binding);
                CaptureProfileReference(profile);
            }
        }

        private void BindOptionalFingerCapabilities(HumanoidRigBinding binding)
        {
            _capabilities.Clear();
            var animator = binding.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid)
            {
                _capabilities.BindAnimator(animator);
            }
            else
            {
                var behaviours = binding.GetComponents<MonoBehaviour>();
                for (var i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IExplicitHumanoidDetailRigSource source)
                    {
                        _capabilities.BindExplicit(source);
                        break;
                    }
                }
            }
            CaptureFingerCurrentRotations();
        }

        private bool ProfileReferenceChanged(MotionCalibrationProfile profile)
        {
            if (profile == null || !profile.bodyReferenceValid)
            {
                return _profile != profile;
            }
            if (_profile != profile)
            {
                return true;
            }
            return (_profileRight - profile.neutralBodyRight).sqrMagnitude > ReferenceChangeEpsilon ||
                   (_profileUp - profile.neutralBodyUp).sqrMagnitude > ReferenceChangeEpsilon ||
                   (_profileForward - profile.neutralBodyForward).sqrMagnitude > ReferenceChangeEpsilon;
        }

        private void CaptureProfileReference(MotionCalibrationProfile profile)
        {
            _profile = profile;
            if (profile == null)
            {
                _profileRight = Vector3.zero;
                _profileUp = Vector3.zero;
                _profileForward = Vector3.zero;
                return;
            }
            _profileRight = profile.neutralBodyRight;
            _profileUp = profile.neutralBodyUp;
            _profileForward = profile.neutralBodyForward;
        }

        private void ApplyRichLimbTwist(HumanoidRigBinding binding, float deltaTime)
        {
            var rich = bodyRuntime == null ? null : bodyRuntime.RichMotionFrame;
            var profile = bodyRuntime == null || bodyRuntime.Calibration == null
                ? null
                : bodyRuntime.Calibration.Profile;
            if (rich == null || profile == null || !profile.bodyReferenceValid ||
                !HumanoidRetargetingMath.TryCreateCanonicalToAvatarMap(profile, binding, out var map))
            {
                NeutralizeRichContributions(binding, deltaTime);
                return;
            }

            var sourceDiscontinuity =
                !rich.sourceAvailable ||
                (_richWasAvailable && !string.Equals(_richProviderId, rich.sourceProviderId)) ||
                (_lastRichTimestamp > 0L && rich.sourceTimestampMillisec > 0L &&
                 rich.sourceTimestampMillisec < _lastRichTimestamp);
            if (sourceDiscontinuity)
            {
                ResetRichReferencesOnly();
            }

            _richWasAvailable = rich.sourceAvailable;
            _richProviderId = rich.sourceProviderId ?? string.Empty;
            _lastRichTimestamp = rich.sourceTimestampMillisec;

            for (var i = 0; i < RichLimbChannelCount; i++)
            {
                var channel = rich.GetOrientation(RichChannels[i]);
                if (channel.twistState == RichTwistObservability.Observed && channel.HasFullOrientation)
                {
                    if (!_hasRichReference[i])
                    {
                        _richReferenceBases[i] = channel.basis;
                        _hasRichReference[i] = true;
                        _richTargetDegrees[i] = 0f;
                    }
                    else if (FoundationERetargetMath.TryMeasureRelativeAxialDegrees(
                                 in _richReferenceBases[i],
                                 in channel.basis,
                                 out var sourceDegrees))
                    {
                        _richTargetDegrees[i] = FoundationERetargetMath.MapSignedAxialDegrees(sourceDegrees, map);
                    }
                }
                else if (channel.twistState == RichTwistObservability.Held && _hasRichReference[i])
                {
                    // Hold the last trusted target. Foundation C owns the short ambiguity window.
                }
                else
                {
                    // ReferenceFallback is fallback evidence, not a fresh observation. Unobservable
                    // likewise fabricates no twist; only this additive contribution returns to zero.
                    _richTargetDegrees[i] = 0f;
                }

                _richCurrentDegrees[i] = FoundationERetargetMath.SmoothContribution(
                    _richCurrentDegrees[i],
                    _richTargetDegrees[i],
                    deltaTime,
                    detailResponse);
                ApplyRichChannel(binding, i, _richCurrentDegrees[i]);
            }
        }

        private void NeutralizeRichContributions(HumanoidRigBinding binding, float deltaTime)
        {
            for (var i = 0; i < RichLimbChannelCount; i++)
            {
                _richTargetDegrees[i] = 0f;
                _richCurrentDegrees[i] = FoundationERetargetMath.SmoothContribution(
                    _richCurrentDegrees[i], 0f, deltaTime, detailResponse);
                ApplyRichChannel(binding, i, _richCurrentDegrees[i]);
            }
        }

        private void ApplyRichChannel(HumanoidRigBinding binding, int index, float degrees)
        {
            if (Mathf.Abs(degrees) <= 0.0001f)
            {
                return;
            }
            var bone = binding.GetBoneTransform(RichBones[index]);
            var child = GetRichDirectChild(binding, index);
            if (FoundationERetargetMath.TryApplyAxialTwistPreservingDownstream(
                    bone, child, degrees, out var residual))
            {
                _maxEndpointResidual = Mathf.Max(_maxEndpointResidual, residual);
            }
        }

        private static Transform GetRichDirectChild(HumanoidRigBinding binding, int index)
        {
            switch (RichChannels[index])
            {
                case RichOrientationChannelId.LeftUpperArm:
                    return binding.GetBoneTransform(CanonicalBoneId.LeftLowerArm);
                case RichOrientationChannelId.LeftLowerArm:
                    return binding.GetChainTip(CanonicalKinematicChainId.LeftArm);
                case RichOrientationChannelId.RightUpperArm:
                    return binding.GetBoneTransform(CanonicalBoneId.RightLowerArm);
                case RichOrientationChannelId.RightLowerArm:
                    return binding.GetChainTip(CanonicalKinematicChainId.RightArm);
                case RichOrientationChannelId.LeftUpperLeg:
                    return binding.GetBoneTransform(CanonicalBoneId.LeftLowerLeg);
                case RichOrientationChannelId.LeftLowerLeg:
                    return binding.GetChainTip(CanonicalKinematicChainId.LeftLeg);
                case RichOrientationChannelId.RightUpperLeg:
                    return binding.GetBoneTransform(CanonicalBoneId.RightLowerLeg);
                case RichOrientationChannelId.RightLowerLeg:
                    return binding.GetChainTip(CanonicalKinematicChainId.RightLeg);
                default:
                    return null;
            }
        }

        private void ApplyHands(HumanoidRigBinding binding, float deltaTime)
        {
            var frame = handRuntime == null ? null : handRuntime.HandFrame;
            ApplyHand(binding, frame == null ? null : frame.Left, CanonicalHandSide.Left, deltaTime);
            ApplyHand(binding, frame == null ? null : frame.Right, CanonicalHandSide.Right, deltaTime);
        }

        private void ApplyHand(
            HumanoidRigBinding binding,
            CanonicalHand hand,
            CanonicalHandSide side,
            float deltaTime)
        {
            var sideIndex = SideIndex(side);
            var fresh = hand != null && hand.isFresh && hand.isDetected && hand.palmBasis.isValid;
            if (!fresh)
            {
                InvalidatePalmSourceReference(sideIndex);
                ReturnPalmSideToReference(binding, side, deltaTime);
                ReturnFingerSideToReference(side, deltaTime);
                return;
            }

            if (!enablePalmOrientation)
            {
                // Disabling the category removes the previous E palm contribution rather than
                // freezing it. Re-enabling will acquire a new zero-delta source reference.
                InvalidatePalmSourceReference(sideIndex);
                ReturnPalmSideToReference(binding, side, deltaTime);
            }
            else
            {
                if (!_targetPalmReferenceValid[sideIndex] &&
                    !TryCaptureTargetPalmBindingReference(binding, side))
                {
                    InvalidatePalmSourceReference(sideIndex);
                    ReturnPalmSideToReference(binding, side, deltaTime);
                    ReturnFingerSideToReference(side, deltaTime);
                    return;
                }

                if (!_palmWasFresh[sideIndex] || !_handReferenceValid[sideIndex])
                {
                    // First trustworthy acquisition/reacquisition establishes a zero-delta source
                    // reference. The target zero is the binding-version parent-local palm reference.
                    _handReferenceBases[sideIndex] = hand.palmBasis;
                    _handReferenceValid[sideIndex] = true;
                    _palmWasFresh[sideIndex] = true;
                    ReturnPalmSideToReference(binding, side, deltaTime);
                    ReturnFingerSideToReference(side, deltaTime);
                    return;
                }

                if (!ApplyAbsolutePalmOrientation(
                        binding,
                        hand,
                        side,
                        sideIndex,
                        deltaTime))
                {
                    ReturnPalmSideToReference(binding, side, deltaTime);
                }
            }

            if (!TryCharacterizeTargetPalm(binding, side, out _,
                    out var targetPrimary, out var targetSecondary, out var targetThird))
            {
                ReturnFingerSideToReference(side, deltaTime);
                return;
            }

            if (enableFingerArticulation)
            {
                ApplyFingerArticulation(
                    hand,
                    side,
                    targetPrimary,
                    targetSecondary,
                    targetThird,
                    deltaTime);
            }
            else
            {
                ReturnFingerSideToReference(side, deltaTime);
            }
        }

        private bool ApplyAbsolutePalmOrientation(
            HumanoidRigBinding binding,
            CanonicalHand hand,
            CanonicalHandSide side,
            int sideIndex,
            float deltaTime)
        {
            var chainId = ArmChain(side);
            var handTransform = binding.GetChainTip(chainId);
            if (handTransform == null || !_targetPalmReferenceValid[sideIndex])
            {
                return false;
            }

            var parentWorldRotation = handTransform.parent == null
                ? Quaternion.identity
                : handTransform.parent.rotation;
            var baselineLocalRotation = binding.GetChainTipBindLocalRotation(chainId);
            if (!FoundationERetargetMath.TryBuildAbsolutePalmLocalRotation(
                    in _handReferenceBases[sideIndex],
                    in hand.palmBasis,
                    _targetPalmPrimaryParentLocal[sideIndex],
                    _targetPalmSecondaryParentLocal[sideIndex],
                    _targetPalmThirdParentLocal[sideIndex],
                    parentWorldRotation,
                    baselineLocalRotation,
                    out var targetLocalRotation,
                    out _,
                    out _,
                    out _))
            {
                return false;
            }

            handTransform.localRotation = FoundationERetargetMath.SmoothLocalRotation(
                handTransform.localRotation,
                targetLocalRotation,
                deltaTime,
                detailResponse);
            return true;
        }

        private void CaptureTargetPalmBindingReferences(HumanoidRigBinding binding)
        {
            for (var i = 0; i < 2; i++)
            {
                _targetPalmReferenceValid[i] = false;
                _targetPalmPrimaryParentLocal[i] = Vector3.zero;
                _targetPalmSecondaryParentLocal[i] = Vector3.zero;
                _targetPalmThirdParentLocal[i] = Vector3.zero;
            }

            TryCaptureTargetPalmBindingReference(binding, CanonicalHandSide.Left);
            TryCaptureTargetPalmBindingReference(binding, CanonicalHandSide.Right);
        }

        private bool TryCaptureTargetPalmBindingReference(
            HumanoidRigBinding binding,
            CanonicalHandSide side)
        {
            var sideIndex = SideIndex(side);
            if (!TryCharacterizeTargetPalm(binding, side, out var handTransform,
                    out var livePrimary, out var liveSecondary, out var liveThird) ||
                handTransform == null)
            {
                _targetPalmReferenceValid[sideIndex] = false;
                return false;
            }

            var chainId = ArmChain(side);
            var parentWorldRotation = handTransform.parent == null
                ? Quaternion.identity
                : handTransform.parent.rotation;
            var baselineLocalRotation = binding.GetChainTipBindLocalRotation(chainId);
            var baselineWorldRotation = parentWorldRotation * baselineLocalRotation;

            // Remove any current hand-local rotation delta from the geometry before storing the
            // reference. This makes the stored target basis a parent-local binding reference rather
            // than a snapshot of a previously E-modified live hand.
            var removeCurrentHandDelta = baselineWorldRotation * Quaternion.Inverse(handTransform.rotation);
            var parentWorldInverse = Quaternion.Inverse(parentWorldRotation);
            var primaryParentLocal = parentWorldInverse * (removeCurrentHandDelta * livePrimary);
            var secondaryParentLocal = parentWorldInverse * (removeCurrentHandDelta * liveSecondary);
            var thirdParentLocal = parentWorldInverse * (removeCurrentHandDelta * liveThird);
            if (!TryNormalize(primaryParentLocal, out primaryParentLocal) ||
                !TryNormalize(secondaryParentLocal, out secondaryParentLocal) ||
                !TryNormalize(thirdParentLocal, out thirdParentLocal))
            {
                _targetPalmReferenceValid[sideIndex] = false;
                return false;
            }

            _targetPalmPrimaryParentLocal[sideIndex] = primaryParentLocal;
            _targetPalmSecondaryParentLocal[sideIndex] = secondaryParentLocal;
            _targetPalmThirdParentLocal[sideIndex] = thirdParentLocal;
            _targetPalmReferenceValid[sideIndex] = true;
            return true;
        }

        private bool TryCharacterizeTargetPalm(
            HumanoidRigBinding binding,
            CanonicalHandSide side,
            out Transform hand,
            out Vector3 primary,
            out Vector3 secondary,
            out Vector3 third)
        {
            var left = side == CanonicalHandSide.Left;
            hand = binding.GetChainTip(left ? CanonicalKinematicChainId.LeftArm : CanonicalKinematicChainId.RightArm);
            var offset = left ? 0 : 15;
            var index = _capabilities.GetFingerBone((HumanoidFingerBoneId)(offset + 3));
            var middle = _capabilities.GetFingerBone((HumanoidFingerBoneId)(offset + 6));
            var little = _capabilities.GetFingerBone((HumanoidFingerBoneId)(offset + 12));
            primary = Vector3.zero;
            secondary = Vector3.zero;
            third = Vector3.zero;
            if (hand == null || index == null || middle == null || little == null)
            {
                return false;
            }

            primary = middle.position - hand.position;
            if (!TryNormalize(primary, out primary))
            {
                return false;
            }
            secondary = index.position - little.position;
            secondary -= primary * Vector3.Dot(secondary, primary);
            if (!TryNormalize(secondary, out secondary))
            {
                return false;
            }
            third = Vector3.Cross(primary, secondary);
            if (!TryNormalize(third, out third))
            {
                return false;
            }
            secondary = Vector3.Cross(third, primary).normalized;
            return true;
        }

        private void ApplyFingerArticulation(
            CanonicalHand hand,
            CanonicalHandSide side,
            Vector3 targetPrimary,
            Vector3 targetSecondary,
            Vector3 targetThird,
            float deltaTime)
        {
            var sideOffset = side == CanonicalHandSide.Left ? 0 : 15;
            for (var local = 0; local < 15; local++)
            {
                var id = (HumanoidFingerBoneId)(sideOffset + local);
                var bone = _capabilities.GetFingerBone(id);
                var tip = _capabilities.GetSegmentTip(id);
                if (bone == null || tip == null ||
                    !TryHandSegment(hand, local, out var sourceDirection) ||
                    !FoundationERetargetMath.TryMapDirectionBetweenBases(
                        sourceDirection,
                        in hand.palmBasis,
                        targetPrimary,
                        targetSecondary,
                        targetThird,
                        out var targetDirection))
                {
                    continue;
                }

                var currentDirection = tip.position - bone.position;
                if (!TryNormalize(currentDirection, out currentDirection))
                {
                    continue;
                }

                var targetWorldRotation = Quaternion.FromToRotation(currentDirection, targetDirection) * bone.rotation;
                var parent = bone.parent;
                var targetLocalRotation = parent == null
                    ? targetWorldRotation
                    : Quaternion.Inverse(parent.rotation) * targetWorldRotation;
                var index = (int)id;
                _fingerCurrentLocalRotations[index] = Quaternion.Slerp(
                    _fingerCurrentLocalRotations[index],
                    targetLocalRotation,
                    1f - Mathf.Exp(-Mathf.Max(0.1f, detailResponse) * deltaTime));
                bone.localRotation = _fingerCurrentLocalRotations[index];
            }
        }

        private static bool TryHandSegment(CanonicalHand hand, int localIndex, out Vector3 direction)
        {
            direction = Vector3.zero;
            var a = hand.GetLandmark(FingerSegmentLandmarks[localIndex, 0]);
            var b = hand.GetLandmark(FingerSegmentLandmarks[localIndex, 1]);
            if (!a.hasHandLocalPosition || !b.hasHandLocalPosition ||
                a.confidence <= 0f || b.confidence <= 0f)
            {
                return false;
            }
            return TryNormalize(b.handLocalPosition - a.handLocalPosition, out direction);
        }

        private void ReturnPalmSideToReference(
            HumanoidRigBinding binding,
            CanonicalHandSide side,
            float deltaTime)
        {
            if (binding == null)
            {
                return;
            }
            var chainId = ArmChain(side);
            var hand = binding.GetChainTip(chainId);
            if (hand == null)
            {
                return;
            }
            hand.localRotation = FoundationERetargetMath.SmoothLocalRotation(
                hand.localRotation,
                binding.GetChainTipBindLocalRotation(chainId),
                deltaTime,
                detailResponse);
        }

        private static void RestorePalmSideToReferenceImmediate(
            HumanoidRigBinding binding,
            CanonicalHandSide side)
        {
            if (binding == null)
            {
                return;
            }
            var chainId = ArmChain(side);
            var hand = binding.GetChainTip(chainId);
            if (hand != null)
            {
                hand.localRotation = binding.GetChainTipBindLocalRotation(chainId);
            }
        }

        private void ReturnFingerSideToReference(CanonicalHandSide side, float deltaTime)
        {
            var start = side == CanonicalHandSide.Left ? 0 : 15;
            var t = 1f - Mathf.Exp(-Mathf.Max(0.1f, detailResponse) * Mathf.Max(0f, deltaTime));
            for (var i = start; i < start + 15; i++)
            {
                var id = (HumanoidFingerBoneId)i;
                var bone = _capabilities.GetFingerBone(id);
                if (bone == null)
                {
                    continue;
                }
                _fingerCurrentLocalRotations[i] = Quaternion.Slerp(
                    _fingerCurrentLocalRotations[i],
                    _capabilities.GetReferenceLocalRotation(id),
                    t);
                bone.localRotation = _fingerCurrentLocalRotations[i];
            }
        }

        private void CaptureFingerCurrentRotations()
        {
            for (var i = 0; i < HumanoidRigDetailCapabilities.FingerBoneCount; i++)
            {
                var bone = _capabilities.GetFingerBone((HumanoidFingerBoneId)i);
                _fingerCurrentLocalRotations[i] = bone == null ? Quaternion.identity : bone.localRotation;
            }
        }

        private void ResetSourceReferences(HumanoidRigBinding binding)
        {
            ResetRichReferencesOnly();
            ClearPalmSourceReferences();
            RestorePalmSideToReferenceImmediate(binding, CanonicalHandSide.Left);
            RestorePalmSideToReferenceImmediate(binding, CanonicalHandSide.Right);
        }

        private void ClearPalmSourceReferences()
        {
            for (var i = 0; i < 2; i++)
            {
                _handReferenceValid[i] = false;
                _palmWasFresh[i] = false;
                _handReferenceBases[i] = default;
            }
        }

        private void InvalidatePalmSourceReference(int sideIndex)
        {
            _handReferenceValid[sideIndex] = false;
            _palmWasFresh[sideIndex] = false;
            _handReferenceBases[sideIndex] = default;
        }

        private static CanonicalKinematicChainId ArmChain(CanonicalHandSide side)
        {
            return side == CanonicalHandSide.Left
                ? CanonicalKinematicChainId.LeftArm
                : CanonicalKinematicChainId.RightArm;
        }

        private static int SideIndex(CanonicalHandSide side)
        {
            return side == CanonicalHandSide.Left ? 0 : 1;
        }

        private void ResetRichReferencesOnly()
        {
            for (var i = 0; i < RichLimbChannelCount; i++)
            {
                _hasRichReference[i] = false;
                _richReferenceBases[i] = default;
                _richTargetDegrees[i] = 0f;
            }
        }

        private void ClearRichTargetsAndContributions()
        {
            for (var i = 0; i < RichLimbChannelCount; i++)
            {
                _hasRichReference[i] = false;
                _richReferenceBases[i] = default;
                _richTargetDegrees[i] = 0f;
                _richCurrentDegrees[i] = 0f;
            }
            _richProviderId = string.Empty;
            _lastRichTimestamp = 0L;
            _richWasAvailable = false;
            _maxEndpointResidual = 0f;
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y) ||
                float.IsNaN(value.z) || float.IsInfinity(value.z) ||
                value.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }
            normalized = value.normalized;
            return true;
        }
    }
}
