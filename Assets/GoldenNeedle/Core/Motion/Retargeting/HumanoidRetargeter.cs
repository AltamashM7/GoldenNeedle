using System;
using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Rotation;
using GoldenNeedle.Core.Motion.Runtime;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    public struct HumanoidChainDebugState
    {
        public bool sourceValid;
        public bool targetGenerated;
        public bool solved;
        public bool hasCharacterization;
        public bool hasBendHint;
        public bool targetWasClamped;
        public bool fidelityComparable;
        public Vector3 desiredEffectorPosition;
        public Vector3 bendHintPosition;
        public Vector3 actualTipPosition;
        public Vector3 mappedEffectorParentLocal;
        public Vector3 mappedHintParentLocal;
        public float endpointError;
        public float normalizedEndpointError;
        public float ikEndpointResidual;
        public float normalizedIkEndpointResidual;
        public float retargetFidelityError;
        public float normalizedRetargetFidelityError;
        public float bendPlaneErrorDegrees;
    }

    /// <summary>
    /// LateUpdate consumer of MotionEngineRuntime. Pelvis/chest retain their calibrated
    /// rotation-frame path; the eight limb bones are posed by four positional analytic two-bone
    /// IK chains. Live limb targets are mapped through current anatomical parent frames and a
    /// cached per-chain reference characterization.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class HumanoidRetargeter : MonoBehaviour
    {
        private const float MinimumDirectionSquared = 0.00000001f;

        [SerializeField] private MotionEngineRuntime runtime;
        [SerializeField] private HumanoidRigBinding binding;
        [Tooltip("Centralized return-to-reference time after a chain becomes unavailable.")]
        [SerializeField, Range(0.15f, 0.25f)] private float unavailableReturnSeconds = 0.20f;
        [SerializeField] private bool driveRig;

        private readonly IChainIkSolver _ikSolver = new AnalyticTwoBoneIkSolver();
        private readonly Vector3[] _previousBendDirections = new Vector3[CanonicalKinematicTargets.ChainCount];
        private readonly bool[] _hasPreviousBendDirections = new bool[CanonicalKinematicTargets.ChainCount];
        private readonly float[] _missingHintSeconds = new float[CanonicalKinematicTargets.ChainCount];
        private readonly float[] _missingChainSeconds = new float[CanonicalKinematicTargets.ChainCount];
        private readonly HumanoidChainDebugState[] _chainDebugStates = new HumanoidChainDebugState[CanonicalKinematicTargets.ChainCount];
        private readonly ChainReferenceCharacterization[] _characterizations = new ChainReferenceCharacterization[CanonicalKinematicTargets.ChainCount];
        private readonly bool[] _hasCharacterizations = new bool[CanonicalKinematicTargets.ChainCount];

        private MotionCalibrationProfile _characterizationProfile;
        private int _characterizationBindingVersion = -1;
        private int _drivenLimbBoneCount;

        public MotionEngineRuntime Runtime => runtime;
        public HumanoidRigBinding Binding => binding;
        public bool IsBound => binding != null && binding.IsBound;
        public string BindingModeName => binding == null ? "Unbound" : binding.BindingModeName;
        public int DrivenBoneCount => _drivenLimbBoneCount;
        public bool KinematicTargetsLive { get; private set; }
        public int SourceChainsValid { get; private set; }
        public int TargetsGenerated { get; private set; }
        public int IkChainsSolved { get; private set; }
        public int LimbBonesDriven => _drivenLimbBoneCount;
        public float MaxEndpointError { get; private set; }
        public float MaxNormalizedEndpointError { get; private set; }
        public float MaxRetargetFidelityError { get; private set; }
        public float MaxNormalizedRetargetFidelityError { get; private set; }
        public float MaxIkEndpointResidual { get; private set; }
        public float MaxNormalizedIkEndpointResidual { get; private set; }
        public float MaxBendPlaneErrorDegrees { get; private set; }

        public bool DriveRig
        {
            get => driveRig;
            set
            {
                if (driveRig == value)
                {
                    return;
                }

                driveRig = value;
                if (!driveRig)
                {
                    ClearDiagnostics();
                    binding?.ResetToReferencePose();
                }
            }
        }

        private void Awake()
        {
            runtime = runtime == null ? GetComponent<MotionEngineRuntime>() : runtime;
            binding = binding == null ? GetComponent<HumanoidRigBinding>() : binding;
        }

        private void Start()
        {
            runtime = runtime == null ? GetComponent<MotionEngineRuntime>() : runtime;
            binding = binding == null ? GetComponent<HumanoidRigBinding>() : binding;
        }

        private void LateUpdate()
        {
            if (!driveRig || runtime == null || binding == null || !binding.IsBound)
            {
                ClearDiagnostics();
                binding?.ResetToReferencePose();
                return;
            }

            var frame = runtime.RotationFrame;
            var targets = runtime.KinematicTargets;
            var profile = runtime.Calibration == null ? null : runtime.Calibration.Profile;
            if (frame == null || targets == null || profile == null || !frame.calibrationValid ||
                !targets.calibrationValid || !profile.isValid || runtime.RotationSolver == null ||
                !runtime.RotationSolver.HasReferenceBodyRotation)
            {
                ClearDiagnostics();
                binding.ResetToReferencePose();
                return;
            }

            ApplyRotationFrame(
                frame,
                targets,
                profile,
                runtime.RotationSolver.ReferenceBodyRotation,
                Mathf.Max(Time.unscaledDeltaTime, 0f));
        }

        /// <summary>
        /// Compatibility entry point for callers that only have a rotation frame. The frame still
        /// drives torso orientation, but limb targets use the legacy direct world mapping because
        /// no source calibration geometry was supplied.
        /// </summary>
        public void ApplyRotationFrame(CanonicalRotationFrame frame, Quaternion sourceCalibrationBodyRotation, float deltaTime)
        {
            ApplyRotationFrameInternal(frame, null, null, sourceCalibrationBodyRotation, deltaTime, true);
        }

        public void ApplyRotationFrame(
            CanonicalRotationFrame frame,
            CanonicalKinematicTargets targets,
            Quaternion sourceCalibrationBodyRotation,
            float deltaTime)
        {
            ApplyRotationFrameInternal(frame, targets, null, sourceCalibrationBodyRotation, deltaTime, true);
        }

        public void ApplyRotationFrame(
            CanonicalRotationFrame frame,
            CanonicalKinematicTargets targets,
            MotionCalibrationProfile profile,
            Quaternion sourceCalibrationBodyRotation,
            float deltaTime)
        {
            ApplyRotationFrameInternal(frame, targets, profile, sourceCalibrationBodyRotation, deltaTime, false);
        }

        public HumanoidChainDebugState GetChainDebugState(CanonicalKinematicChainId id)
        {
            return _chainDebugStates[(int)id];
        }

        private void ApplyRotationFrameInternal(
            CanonicalRotationFrame frame,
            CanonicalKinematicTargets targets,
            MotionCalibrationProfile profile,
            Quaternion sourceCalibrationBodyRotation,
            float deltaTime,
            bool useLegacyWorldMapping)
        {
            runtime = runtime == null ? GetComponent<MotionEngineRuntime>() : runtime;
            binding = binding == null ? GetComponent<HumanoidRigBinding>() : binding;
            if (binding == null || !binding.IsBound || frame == null || !frame.calibrationValid)
            {
                ClearDiagnostics();
                binding?.ResetToReferencePose();
                return;
            }

            if (!driveRig)
            {
                ClearDiagnostics();
                binding.ResetToReferencePose();
                return;
            }

            var returnStep = Mathf.Clamp01(Mathf.Max(0f, deltaTime) / Mathf.Max(0.15f, unavailableReturnSeconds));
            var avatarReferenceBodyRotation = binding.AvatarReferenceBodyRotation;
            ApplyTorsoBone(CanonicalBoneId.Pelvis, frame, sourceCalibrationBodyRotation, avatarReferenceBodyRotation, returnStep);
            ApplyTorsoBone(CanonicalBoneId.Chest, frame, sourceCalibrationBodyRotation, avatarReferenceBodyRotation, returnStep);

            ClearKinematicDiagnostics(targets);
            if (targets == null || !targets.calibrationValid)
            {
                for (var i = 0; i < CanonicalKinematicTargets.ChainCount; i++)
                {
                    ReturnChainToReference((CanonicalKinematicChainId)i, returnStep);
                }

                return;
            }

            if (!useLegacyWorldMapping)
            {
                EnsureCharacterizations(profile);
            }

            KinematicTargetsLive = targets.hasMeaningfulTargets;
            SourceChainsValid = targets.validChainCount;
            TargetsGenerated = targets.validChainCount;
            for (var i = 0; i < CanonicalKinematicTargets.ChainCount; i++)
            {
                ApplyChain(
                    (CanonicalKinematicChainId)i,
                    targets.GetTarget((CanonicalKinematicChainId)i),
                    useLegacyWorldMapping,
                    returnStep,
                    Mathf.Max(0f, deltaTime));
            }
        }

        private void EnsureCharacterizations(MotionCalibrationProfile profile)
        {
            if (profile == _characterizationProfile &&
                binding != null &&
                _characterizationBindingVersion == binding.ReferencePoseVersion)
            {
                return;
            }

            _characterizationProfile = profile;
            _characterizationBindingVersion = binding == null ? -1 : binding.ReferencePoseVersion;
            for (var i = 0; i < CanonicalKinematicTargets.ChainCount; i++)
            {
                _hasCharacterizations[i] = HumanoidRetargetingMath.TryBuildChainCharacterization(
                    profile,
                    binding,
                    (CanonicalKinematicChainId)i,
                    out _characterizations[i]);
            }
        }

        private void ApplyTorsoBone(
            CanonicalBoneId id,
            CanonicalRotationFrame frame,
            Quaternion sourceCalibrationBodyRotation,
            Quaternion avatarReferenceBodyRotation,
            float returnStep)
        {
            var target = binding.GetBoneTransform(id);
            if (target == null)
            {
                return;
            }

            var bone = frame.GetBone(id);
            if (bone.IsTracked && IsFinite(bone.rotationDeltaFromCalibration))
            {
                target.rotation = HumanoidRetargetingMath.CalculateTargetWorldRotation(
                    avatarReferenceBodyRotation,
                    sourceCalibrationBodyRotation,
                    bone.rotationDeltaFromCalibration,
                    binding.GetBindWorldRotation(id));
            }
            else if (returnStep > 0f)
            {
                target.localRotation = Quaternion.Slerp(
                    target.localRotation,
                    binding.GetBindLocalRotation(id),
                    returnStep);
            }
        }

        private void ApplyChain(
            CanonicalKinematicChainId chainId,
            CanonicalKinematicChainTarget sourceTarget,
            bool useLegacyWorldMapping,
            float returnStep,
            float deltaTime)
        {
            var index = (int)chainId;
            var debug = new HumanoidChainDebugState
            {
                sourceValid = sourceTarget.isValid,
                targetGenerated = sourceTarget.isValid,
                hasCharacterization = useLegacyWorldMapping || _hasCharacterizations[index],
            };
            _chainDebugStates[index] = debug;

            if (!sourceTarget.isValid || !binding.IsChainAvailable(chainId) ||
                (!useLegacyWorldMapping && !_hasCharacterizations[index]))
            {
                _missingChainSeconds[index] += deltaTime;
                if (_missingChainSeconds[index] > Mathf.Max(0.15f, unavailableReturnSeconds))
                {
                    _hasPreviousBendDirections[index] = false;
                }

                ReturnChainToReference(chainId, returnStep);
                return;
            }

            _missingChainSeconds[index] = 0f;
            binding.RestoreChainReferencePose(chainId);
            var root = binding.GetChainRoot(chainId);
            var mid = binding.GetChainMid(chainId);
            var tip = binding.GetChainTip(chainId);
            var targetReach = binding.GetChainTotalReach(chainId);
            if (root == null || mid == null || tip == null || !IsFinite(targetReach) || targetReach <= 0.000001f)
            {
                ReturnChainToReference(chainId, returnStep);
                return;
            }

            Vector3 mappedEffector;
            Vector3 mappedHint;
            Quaternion targetParentFrame;
            if (useLegacyWorldMapping)
            {
                mappedEffector = sourceTarget.normalizedEffectorDisplacement;
                mappedHint = sourceTarget.normalizedBendHintDisplacement;
                targetParentFrame = Quaternion.identity;
            }
            else
            {
                var characterization = _characterizations[index];
                mappedEffector = characterization.sourceToTargetChainRotation * sourceTarget.normalizedEffectorDisplacement;
                mappedHint = characterization.sourceToTargetChainRotation * sourceTarget.normalizedBendHintDisplacement;
                if (!binding.TryGetCurrentParentFrame(chainId, out targetParentFrame))
                {
                    ReturnChainToReference(chainId, returnStep);
                    return;
                }
            }

            if (!IsFinite(mappedEffector) || !IsFinite(mappedHint) || !IsFinite(targetParentFrame))
            {
                ReturnChainToReference(chainId, returnStep);
                return;
            }

            var rootPosition = root.position;
            var desiredDisplacement = useLegacyWorldMapping
                ? mappedEffector * targetReach
                : targetParentFrame * (mappedEffector * targetReach);
            var desiredEffector = rootPosition + desiredDisplacement;
            var bendDisplacement = useLegacyWorldMapping
                ? mappedHint * targetReach
                : targetParentFrame * (mappedHint * targetReach);
            var bendHint = rootPosition + bendDisplacement;
            debug.desiredEffectorPosition = desiredEffector;
            debug.bendHintPosition = bendHint;
            debug.mappedEffectorParentLocal = mappedEffector;
            debug.mappedHintParentLocal = mappedHint;
            debug.hasBendHint = sourceTarget.hasBendHint && mappedHint.sqrMagnitude > MinimumDirectionSquared;
            _chainDebugStates[index] = debug;

            if (debug.hasBendHint)
            {
                _missingHintSeconds[index] = 0f;
            }
            else
            {
                _missingHintSeconds[index] += deltaTime;
                if (_missingHintSeconds[index] > Mathf.Max(0.15f, unavailableReturnSeconds))
                {
                    _hasPreviousBendDirections[index] = false;
                }
            }

            var referenceBend = useLegacyWorldMapping
                ? binding.AvatarReferenceBodyRotation * Vector3.forward
                : targetParentFrame * (_characterizations[index].targetChainReferenceFrame * Vector3.up);
            var fallbackEffector = mappedEffector.sqrMagnitude > MinimumDirectionSquared
                ? useLegacyWorldMapping ? mappedEffector : targetParentFrame * mappedEffector
                : referenceBend;
            var request = new TwoBoneIkRequest
            {
                rootPosition = rootPosition,
                targetEffectorPosition = desiredEffector,
                bendHintPosition = bendHint,
                hasBendHint = debug.hasBendHint,
                previousBendDirection = _previousBendDirections[index],
                hasPreviousBendDirection = _hasPreviousBendDirections[index],
                referenceBendDirection = referenceBend,
                fallbackEffectorDirection = fallbackEffector,
                upperLength = binding.GetChainUpperLength(chainId),
                lowerLength = binding.GetChainLowerLength(chainId),
            };

            if (!_ikSolver.TrySolve(request, out var result) || !ApplySolvedChain(root, mid, tip, result))
            {
                ReturnChainToReference(chainId, returnStep);
                return;
            }

            _previousBendDirections[index] = result.bendDirection;
            _hasPreviousBendDirections[index] = true;
            IkChainsSolved++;
            _drivenLimbBoneCount += 2;

            var clamped = Mathf.Abs(result.clampedTargetDistance - result.targetDistance) > 0.0001f;
            var endpointResidual = Vector3.Distance(tip.position, result.solvedEffectorPosition);
            var normalizedEndpointResidual = endpointResidual / Mathf.Max(targetReach, 0.000001f);
            debug = _chainDebugStates[index];
            debug.solved = true;
            debug.targetWasClamped = clamped;
            debug.actualTipPosition = tip.position;
            debug.endpointError = IsFinite(endpointResidual) ? endpointResidual : 0f;
            debug.normalizedEndpointError = IsFinite(normalizedEndpointResidual) ? normalizedEndpointResidual : 0f;
            debug.ikEndpointResidual = debug.endpointError;
            debug.normalizedIkEndpointResidual = debug.normalizedEndpointError;
            MaxEndpointError = Mathf.Max(MaxEndpointError, debug.endpointError);
            MaxNormalizedEndpointError = Mathf.Max(MaxNormalizedEndpointError, debug.normalizedEndpointError);
            MaxIkEndpointResidual = Mathf.Max(MaxIkEndpointResidual, debug.endpointError);
            MaxNormalizedIkEndpointResidual = Mathf.Max(MaxNormalizedIkEndpointResidual, debug.normalizedEndpointError);

            if (!useLegacyWorldMapping && !clamped)
            {
                var actualTargetParentLocal = Quaternion.Inverse(targetParentFrame) *
                    ((tip.position - rootPosition) / Mathf.Max(targetReach, 0.000001f));
                var actualSourceNormalized = Quaternion.Inverse(_characterizations[index].sourceToTargetChainRotation) *
                    actualTargetParentLocal;
                var fidelityError = Vector3.Distance(actualSourceNormalized, sourceTarget.normalizedEffectorDisplacement);
                debug.fidelityComparable = IsFinite(fidelityError);
                debug.retargetFidelityError = debug.fidelityComparable ? fidelityError : 0f;
                debug.normalizedRetargetFidelityError = debug.retargetFidelityError;
                MaxRetargetFidelityError = Mathf.Max(MaxRetargetFidelityError, debug.retargetFidelityError);
                MaxNormalizedRetargetFidelityError = Mathf.Max(MaxNormalizedRetargetFidelityError, debug.normalizedRetargetFidelityError);
            }

            if (debug.hasBendHint && TryCalculateBendPlaneError(
                    rootPosition,
                    result.solvedEffectorPosition,
                    bendHint,
                    mid.position,
                    out var bendError))
            {
                debug.bendPlaneErrorDegrees = bendError;
                MaxBendPlaneErrorDegrees = Mathf.Max(MaxBendPlaneErrorDegrees, bendError);
            }

            _chainDebugStates[index] = debug;
        }

        private void ReturnChainToReference(CanonicalKinematicChainId chainId, float returnStep)
        {
            if (binding == null || !binding.IsChainAvailable(chainId))
            {
                return;
            }

            var root = binding.GetChainRoot(chainId);
            var mid = binding.GetChainMid(chainId);
            if (root != null && returnStep > 0f)
            {
                root.localRotation = Quaternion.Slerp(
                    root.localRotation,
                    binding.GetChainRootBindLocalRotation(chainId),
                    returnStep);
            }

            if (mid != null && returnStep > 0f)
            {
                mid.localRotation = Quaternion.Slerp(
                    mid.localRotation,
                    binding.GetChainMidBindLocalRotation(chainId),
                    returnStep);
            }
        }

        private static bool ApplySolvedChain(Transform root, Transform mid, Transform tip, TwoBoneIkResult result)
        {
            if (!result.isValid || root == null || mid == null || tip == null)
            {
                return false;
            }

            var rootToMid = mid.position - root.position;
            var solvedRootToMid = result.solvedMidPosition - root.position;
            if (!TryRotateToward(root, rootToMid, solvedRootToMid))
            {
                return false;
            }

            var midToTip = tip.position - mid.position;
            var solvedMidToTip = result.solvedEffectorPosition - mid.position;
            if (!TryRotateToward(mid, midToTip, solvedMidToTip))
            {
                return false;
            }

            return IsFinite(root.position) && IsFinite(mid.position) && IsFinite(tip.position);
        }

        private static bool TryRotateToward(Transform target, Vector3 from, Vector3 to)
        {
            if (target == null || !TryNormalize(from, out from) || !TryNormalize(to, out to))
            {
                return false;
            }

            var delta = Quaternion.FromToRotation(from, to);
            if (!IsFinite(delta))
            {
                return false;
            }

            target.rotation = delta * target.rotation;
            return IsFinite(target.rotation);
        }

        private static bool TryCalculateBendPlaneError(
            Vector3 root,
            Vector3 solvedEffector,
            Vector3 expectedHint,
            Vector3 actualMid,
            out float angle)
        {
            angle = 0f;
            if (!TryNormalize(solvedEffector - root, out var targetDirection))
            {
                return false;
            }

            var expected = expectedHint - root;
            expected -= targetDirection * Vector3.Dot(expected, targetDirection);
            var actual = actualMid - root;
            actual -= targetDirection * Vector3.Dot(actual, targetDirection);
            if (!TryNormalize(expected, out expected) || !TryNormalize(actual, out actual))
            {
                return false;
            }

            angle = Vector3.Angle(actual, expected);
            return IsFinite(angle);
        }

        private void ClearKinematicDiagnostics(CanonicalKinematicTargets targets)
        {
            _drivenLimbBoneCount = 0;
            IkChainsSolved = 0;
            MaxEndpointError = 0f;
            MaxNormalizedEndpointError = 0f;
            MaxRetargetFidelityError = 0f;
            MaxNormalizedRetargetFidelityError = 0f;
            MaxIkEndpointResidual = 0f;
            MaxNormalizedIkEndpointResidual = 0f;
            MaxBendPlaneErrorDegrees = 0f;
            SourceChainsValid = targets == null ? 0 : targets.validChainCount;
            TargetsGenerated = targets == null ? 0 : targets.validChainCount;
            KinematicTargetsLive = targets != null && targets.calibrationValid && targets.hasMeaningfulTargets;
            Array.Clear(_chainDebugStates, 0, _chainDebugStates.Length);
        }

        private void ClearDiagnostics()
        {
            _drivenLimbBoneCount = 0;
            KinematicTargetsLive = false;
            SourceChainsValid = 0;
            TargetsGenerated = 0;
            IkChainsSolved = 0;
            MaxEndpointError = 0f;
            MaxNormalizedEndpointError = 0f;
            MaxRetargetFidelityError = 0f;
            MaxNormalizedRetargetFidelityError = 0f;
            MaxIkEndpointResidual = 0f;
            MaxNormalizedIkEndpointResidual = 0f;
            MaxBendPlaneErrorDegrees = 0f;
            Array.Clear(_chainDebugStates, 0, _chainDebugStates.Length);
        }

        private void OnDisable()
        {
            ClearDiagnostics();
            binding?.ResetToReferencePose();
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value) || value.sqrMagnitude <= MinimumDirectionSquared)
            {
                return false;
            }

            normalized = value.normalized;
            return IsFinite(normalized);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
