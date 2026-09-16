using System;
using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Runtime;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    public struct CrouchGroundingSample
    {
        public bool referenceReady;
        public bool measurementTrusted;
        public bool ownsRootY;
        public float correctionY;
        public float bilateralDifferenceNormalized;
    }

    /// <summary>
    /// Pure post-solve grounded-bend anchoring state. Standing captures each solved foot height
    /// relative to the avatar root. While existing vertical evidence still says the support base is
    /// grounded, the same root-relative measurement yields the downward root-Y correction required
    /// to keep both feet at their standing ground height. This runs independently from semantic
    /// Crouch acquisition, while Jump remains the exclusive upward/root-Y action authority.
    /// </summary>
    public sealed class GroundedCrouchFootAnchor
    {
        private const float MinimumLegScale = 0.0001f;
        private const float MaximumBilateralDifferenceNormalized = 0.12f;
        private const float GroundingDeadbandNormalized = 0.01f;
        private const float OwnershipEpsilon = 0.001f;

        private bool _hasReference;
        private float _referenceLeftRelativeY;
        private float _referenceRightRelativeY;
        private float _targetCorrectionY;
        private float _filteredCorrectionY;

        public CrouchGroundingSample LatestSample { get; private set; }

        public void Reset()
        {
            _hasReference = false;
            _referenceLeftRelativeY = 0f;
            _referenceRightRelativeY = 0f;
            _targetCorrectionY = 0f;
            _filteredCorrectionY = 0f;
            LatestSample = default;
        }

        public CrouchGroundingSample Update(
            bool calibrationValid,
            VerticalLocomotionSample verticalSample,
            VerticalLocomotionSettings settings,
            bool feetAvailable,
            float leftFootRelativeY,
            float rightFootRelativeY,
            float legReferenceScale,
            float deltaTime)
        {
            if (!calibrationValid)
            {
                Reset();
                return LatestSample;
            }

            settings = settings ?? new VerticalLocomotionSettings();
            settings.Sanitize();

            var validFeet = feetAvailable &&
                IsFinite(leftFootRelativeY) &&
                IsFinite(rightFootRelativeY) &&
                IsFinite(legReferenceScale) &&
                legReferenceScale > MinimumLegScale;
            var groundingEvidenceSafe = IsGroundingEvidenceSafe(
                verticalSample,
                settings);

            if (!_hasReference)
            {
                if (groundingEvidenceSafe &&
                    verticalSample.state == VerticalLocomotionState.Standing &&
                    validFeet)
                {
                    _referenceLeftRelativeY = leftFootRelativeY;
                    _referenceRightRelativeY = rightFootRelativeY;
                    _hasReference = true;
                    _targetCorrectionY = 0f;
                    _filteredCorrectionY = 0f;
                }

                LatestSample = new CrouchGroundingSample
                {
                    referenceReady = _hasReference,
                    correctionY = _filteredCorrectionY,
                };
                return LatestSample;
            }

            var dt = Mathf.Max(0.0001f, deltaTime);
            if (verticalSample.state == VerticalLocomotionState.Jump ||
                !groundingEvidenceSafe)
            {
                // Jump and takeoff-like support motion must never be pinned to the floor. Clear the
                // target in the background, but relinquish ownership immediately so the existing
                // vertical interpreter remains the only semantic action/root-Y authority.
                _targetCorrectionY = 0f;
                ApplyFilter(0f, settings.verticalResponse, dt);
                LatestSample = new CrouchGroundingSample
                {
                    referenceReady = true,
                    measurementTrusted = false,
                    ownsRootY = false,
                    correctionY = _filteredCorrectionY,
                    bilateralDifferenceNormalized = 0f,
                };
                return LatestSample;
            }

            var bilateralDifferenceNormalized = 0f;
            var trusted = false;
            if (validFeet)
            {
                var leftCorrection =
                    _referenceLeftRelativeY - leftFootRelativeY;
                var rightCorrection =
                    _referenceRightRelativeY - rightFootRelativeY;
                bilateralDifferenceNormalized = Mathf.Abs(
                    leftCorrection - rightCorrection) /
                    Mathf.Max(MinimumLegScale, legReferenceScale);

                if (bilateralDifferenceNormalized <=
                    MaximumBilateralDifferenceNormalized)
                {
                    var bilateralCorrection =
                        (leftCorrection + rightCorrection) * 0.5f;
                    var normalizedDownwardCorrection =
                        Mathf.Max(0f, -bilateralCorrection) /
                        Mathf.Max(MinimumLegScale, legReferenceScale);
                    var bendEvidence =
                        verticalSample.crouchCompression > 0f ||
                        verticalSample.state == VerticalLocomotionState.Crouch;

                    if (!bendEvidence ||
                        normalizedDownwardCorrection <=
                        GroundingDeadbandNormalized)
                    {
                        _targetCorrectionY = 0f;
                    }
                    else
                    {
                        // Grounded bend anchoring may only lower the root. Upward root movement is
                        // reserved for the existing jump path. Use the existing crouch safety clamp
                        // without using crouchWorldScale as the normal grounding authority.
                        _targetCorrectionY = Mathf.Clamp(
                            Mathf.Min(0f, bilateralCorrection),
                            -Mathf.Max(0f, settings.maximumCrouchDepth),
                            0f);
                    }

                    trusted = true;
                }
            }

            // Preserve the previous bilateral-disagreement protection: an unusable one-sided solve
            // never invents a new correction. It holds the last trusted target until reliable
            // bilateral geometry returns or vertical evidence says grounding is no longer safe.
            ApplyFilter(
                _targetCorrectionY,
                settings.verticalResponse,
                dt);

            var semanticCrouch =
                verticalSample.state == VerticalLocomotionState.Crouch;
            var hasGroundingCorrection =
                Mathf.Abs(_filteredCorrectionY) > OwnershipEpsilon ||
                Mathf.Abs(_targetCorrectionY) > OwnershipEpsilon;
            LatestSample = new CrouchGroundingSample
            {
                referenceReady = true,
                measurementTrusted = trusted,
                ownsRootY = semanticCrouch || hasGroundingCorrection,
                correctionY = _filteredCorrectionY,
                bilateralDifferenceNormalized = bilateralDifferenceNormalized,
            };
            return LatestSample;
        }

        private static bool IsGroundingEvidenceSafe(
            VerticalLocomotionSample sample,
            VerticalLocomotionSettings settings)
        {
            return sample.referenceReady &&
                sample.isAvailable &&
                sample.state != VerticalLocomotionState.Jump &&
                Mathf.Abs(sample.supportRise) <=
                    settings.groundedSupportTolerance &&
                sample.footAsymmetry <=
                    settings.maximumJumpFootAsymmetry &&
                sample.apparentScaleChange <=
                    settings.maximumApparentScaleChange;
        }

        private void ApplyFilter(float target, float response, float deltaTime)
        {
            var alpha = 1f - Mathf.Exp(
                -Mathf.Max(0.1f, response) * deltaTime);
            _filteredCorrectionY = Mathf.Lerp(
                _filteredCorrectionY,
                target,
                alpha);
            if (Mathf.Abs(_filteredCorrectionY) < 0.0001f &&
                Mathf.Abs(target) < 0.0001f)
            {
                _filteredCorrectionY = 0f;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Phase 5A root-translation controller. It leaves Phase 4 pose/orientation untouched and
    /// applies locomotion only after retargeting: horizontal X/Z plus bounded tracked vertical
    /// movement. Jump uses the vertical interpreter directly; trustworthy grounded bends use
    /// post-Phase-4 solved foot geometry so feet remain anchored to their standing ground reference.
    /// Root rotation is never copied from tracking.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class EmbodiedLocomotionController : MonoBehaviour
    {
        [Header("Runtime Wiring")]
        [SerializeField] private MotionEngineRuntime runtime;
        [SerializeField] private HumanoidRigBinding binding;
        [Tooltip("Enable Phase 5A avatar-root translation. Phase 4 pose retargeting is independent.")]
        [SerializeField] private bool driveLocomotion = true;

        [Header("Root / Physical Tracking")]
        [Tooltip("Support-foot tracking, filtering, and depth-corroboration settings.")]
        [SerializeField] private CameraSpaceRootTrackerSettings rootTracking = new CameraSpaceRootTrackerSettings();

        [Header("Vertical Locomotion")]
        [Tooltip("Standing reference, physical jump/crouch detection, root-Y scaling, response and tracking-grace settings.")]
        [SerializeField] private VerticalLocomotionSettings vertical = new VerticalLocomotionSettings();

        [Header("Physical Locomotion / Fusion")]
        [Tooltip("Physical mapping scale/deadzones, stationary-motion hysteresis, and physical-vs-cadence suppression settings.")]
        [SerializeField] private LocomotionFusionSettings fusion = new LocomotionFusionSettings();

        [Header("Cadence")]
        [Tooltip("Lower-body cadence acquisition, confidence, stop, and virtual-speed settings.")]
        [SerializeField] private CadenceDetectorSettings cadence = new CadenceDetectorSettings();

        [Header("Heading")]
        [Tooltip("Response speed for smoothing cadence travel heading. Physical room displacement does not use live heading.")]
        [Min(0.1f), SerializeField] private float headingResponse = 8f;

        private CameraSpaceRootTracker _rootTracker;
        private VerticalLocomotionInterpreter _verticalInterpreter;
        private GroundedCrouchFootAnchor _crouchGrounding;
        private CadenceDetector _cadenceDetector;
        private LocomotionFusion _fusion;
        private Transform _playerRoot;
        private Vector2 _virtualOriginXZ;
        private Vector2 _smoothedHeading;
        private bool _hasHeading;
        private bool _initializedPlayerRoot;
        private bool _wasCalibrationValid;
        private bool _pendingRecenter;
        private int _recenterCount;
        private float _verticalOriginY;
        private bool _hasVerticalOrigin;
        private bool _holdingJumpDepth;
        private float _heldJumpDepthDisplacement;

        public bool DriveLocomotion
        {
            get => driveLocomotion;
            set => driveLocomotion = value;
        }

        public CameraSpaceRootSample RootSample { get; private set; }
        public VerticalLocomotionSample VerticalSample { get; private set; }
        public CrouchGroundingSample GroundingSample { get; private set; }
        public CadenceSample CadenceSample { get; private set; }
        public BodyHeadingSample HeadingSample { get; private set; }
        public LocomotionFusionResult FusionResult { get; private set; }
        public Vector2 FinalFrameMotionXZ { get; private set; }
        public Vector2 FinalWorldPositionXZ { get; private set; }
        public float FinalFrameMotionY { get; private set; }
        public float FinalWorldPositionY { get; private set; }
        public float VerticalOriginY => _verticalOriginY;
        public bool HasVerticalOrigin => _hasVerticalOrigin;
        public bool RecenterPending => _pendingRecenter || (_rootTracker != null && _rootTracker.RecenterPending);
        public int RecenterCount => _recenterCount;
        public Transform PlayerRoot => _playerRoot;
        public bool HasWorldHeading => _hasHeading;
        public Vector2 WorldHeadingXZ => _smoothedHeading;

        private void Awake()
        {
            runtime = runtime == null ? GetComponent<MotionEngineRuntime>() : runtime;
            binding = binding == null ? GetComponent<HumanoidRigBinding>() : binding;
            InitializeModules();
        }

        private void Start()
        {
            runtime = runtime == null ? GetComponent<MotionEngineRuntime>() : runtime;
            binding = binding == null ? GetComponent<HumanoidRigBinding>() : binding;
            ResolvePlayerRoot();
        }

        private void LateUpdate()
        {
            InitializeModules();
            runtime = runtime == null ? GetComponent<MotionEngineRuntime>() : runtime;
            binding = binding == null ? GetComponent<HumanoidRigBinding>() : binding;
            if (!ResolvePlayerRoot() || runtime == null)
            {
                ClearFrameDiagnostics();
                return;
            }

            var profile = runtime.Calibration == null ? null : runtime.Calibration.Profile;
            var calibrationValid = profile != null && profile.bodyReferenceValid;
            if (!calibrationValid)
            {
                if (_wasCalibrationValid)
                {
                    _rootTracker.Reset();
                    _verticalInterpreter.Reset();
                    _crouchGrounding.Reset();
                    _cadenceDetector.Reset();
                    _fusion.ResetPhysicalContribution();
                    RestoreVerticalOrigin();
                    _virtualOriginXZ = CurrentPlayerXZ();
                    _hasHeading = false;
                    _hasVerticalOrigin = false;
                    _holdingJumpDepth = false;
                }

                _wasCalibrationValid = false;
                ClearFrameDiagnostics();
                return;
            }

            if (!_wasCalibrationValid)
            {
                _rootTracker.Reset();
                _verticalInterpreter.Reset();
                _crouchGrounding.Reset();
                _cadenceDetector.Reset();
                _fusion.ResetPhysicalContribution();
                _virtualOriginXZ = CurrentPlayerXZ();
                _verticalOriginY = _playerRoot.position.y;
                _hasVerticalOrigin = true;
                _holdingJumpDepth = false;
                _hasHeading = false;
                _wasCalibrationValid = true;
            }

            var dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            var previousRootSample = RootSample;
            RootSample = _rootTracker.Update(runtime.StabilizedFrame, profile, dt);

            if (_pendingRecenter && RootSample.isValid)
            {
                PerformRecenterNow();
                RootSample = _rootTracker.LatestSample;
            }

            VerticalSample = _verticalInterpreter.Update(
                runtime.StabilizedFrame,
                profile,
                dt);
            GroundingSample = UpdateCrouchGrounding(dt);

            CadenceSample = _cadenceDetector.Update(
                runtime.StabilizedFrame,
                Mathf.Max(0.0001f, RootSample.apparentScale),
                dt);

            HeadingSample = default;
            if (BodyHeadingEstimator.TryEstimate(
                    runtime.StabilizedFrame,
                    profile,
                    binding,
                    out var headingSample))
            {
                HeadingSample = headingSample;
                var targetHeading = headingSample.worldHeadingXZ;
                if (!_hasHeading)
                {
                    _smoothedHeading = targetHeading;
                    _hasHeading = true;
                }
                else
                {
                    var alpha = 1f - Mathf.Exp(-Mathf.Max(0.1f, headingResponse) * dt);
                    var blended = Vector2.Lerp(_smoothedHeading, targetHeading, alpha);
                    if (blended.sqrMagnitude > 0.000001f)
                    {
                        _smoothedHeading = blended.normalized;
                    }
                }
            }

            var physicalReferenceMap = default(CanonicalToAvatarAxisMap);
            HumanoidRetargetingMath.TryCreateCanonicalToAvatarMap(
                profile,
                binding,
                out physicalReferenceMap);

            var rootForFusion = RootSample;
            if (VerticalSample.suppressPhysicalDepth)
            {
                if (!_holdingJumpDepth)
                {
                    _heldJumpDepthDisplacement = previousRootSample.hasOrigin
                        ? previousRootSample.displacementXZ.y
                        : RootSample.displacementXZ.y;
                    _holdingJumpDepth = true;
                }

                // Support image-Y rises during a real jump. Hold the pre-jump camera-depth
                // displacement while jump/landing is active so that vertical motion cannot leak
                // into horizontal world depth. Lateral physical displacement remains untouched.
                rootForFusion.displacementXZ.y = _heldJumpDepthDisplacement;
                rootForFusion.velocityXZ.y = 0f;
            }
            else
            {
                _holdingJumpDepth = false;
            }

            FusionResult = _fusion.Evaluate(
                rootForFusion,
                CadenceSample,
                _hasHeading ? _smoothedHeading : Vector2.zero,
                physicalReferenceMap,
                dt);

            _virtualOriginXZ += FusionResult.cadenceVelocity * dt;
            var desiredXZ = _virtualOriginXZ + FusionResult.physicalContribution;
            var verticalOffsetY = VerticalSample.worldOffsetY;
            if (!VerticalSample.IsJumpActive && GroundingSample.ownsRootY)
            {
                verticalOffsetY = GroundingSample.correctionY;
            }

            var desiredY = _hasVerticalOrigin
                ? _verticalOriginY + verticalOffsetY
                : _playerRoot.position.y;
            var currentPosition = _playerRoot.position;
            var currentXZ = new Vector2(currentPosition.x, currentPosition.z);
            FinalFrameMotionXZ = desiredXZ - currentXZ;
            FinalWorldPositionXZ = desiredXZ;
            FinalFrameMotionY = desiredY - currentPosition.y;
            FinalWorldPositionY = desiredY;

            if (driveLocomotion)
            {
                _playerRoot.position = new Vector3(
                    desiredXZ.x,
                    desiredY,
                    desiredXZ.y);
            }
        }

        /// <summary>
        /// Makes the current physical X/Z tracking position the new physical origin without moving
        /// the virtual character. Vertical standing reference, standing foot-ground reference and
        /// root-Y origin are intentionally independent and are not redefined by horizontal recenter.
        /// </summary>
        public void Recenter()
        {
            if (_rootTracker == null || _playerRoot == null || !RootSample.isValid)
            {
                _pendingRecenter = true;
                return;
            }

            PerformRecenterNow();
        }

        private void PerformRecenterNow()
        {
            if (_playerRoot == null || _rootTracker == null)
            {
                _pendingRecenter = true;
                return;
            }

            var preservedWorldXZ = CurrentPlayerXZ();
            if (!_rootTracker.Recenter())
            {
                _pendingRecenter = true;
                return;
            }

            // Physical displacement becomes zero after Recenter(), so making the virtual origin
            // equal to the current world position preserves GamePosition exactly. Resetting fusion
            // also resets/rebases the stationary physical-motion gate at the new X/Z origin.
            _virtualOriginXZ = preservedWorldXZ;
            _fusion.ResetPhysicalContribution();
            _pendingRecenter = false;
            _recenterCount++;
            FusionResult = default;
            FinalFrameMotionXZ = Vector2.zero;
            FinalWorldPositionXZ = preservedWorldXZ;
        }

        private void InitializeModules()
        {
            if (_rootTracker == null)
            {
                rootTracking = rootTracking ?? new CameraSpaceRootTrackerSettings();
                cadence = cadence ?? new CadenceDetectorSettings();
                fusion = fusion ?? new LocomotionFusionSettings();
                rootTracking.Sanitize();
                cadence.Sanitize();
                fusion.Sanitize();
                _rootTracker = new CameraSpaceRootTracker(rootTracking);
                _cadenceDetector = new CadenceDetector(cadence);
                _fusion = new LocomotionFusion(fusion);
            }

            if (_verticalInterpreter == null)
            {
                vertical = vertical ?? new VerticalLocomotionSettings();
                vertical.Sanitize();
                _verticalInterpreter = new VerticalLocomotionInterpreter(vertical);
            }

            if (_crouchGrounding == null)
            {
                _crouchGrounding = new GroundedCrouchFootAnchor();
            }
        }

        private bool ResolvePlayerRoot()
        {
            var candidate = binding != null && binding.IsBound ? binding.AvatarRoot : null;
            if (candidate == null)
            {
                _playerRoot = null;
                _initializedPlayerRoot = false;
                _hasVerticalOrigin = false;
                _crouchGrounding?.Reset();
                return false;
            }

            if (_playerRoot != candidate || !_initializedPlayerRoot)
            {
                _playerRoot = candidate;
                _virtualOriginXZ = CurrentPlayerXZ();
                _verticalOriginY = _playerRoot.position.y;
                _hasVerticalOrigin = false;
                _rootTracker?.Reset();
                _verticalInterpreter?.Reset();
                _crouchGrounding?.Reset();
                _cadenceDetector?.Reset();
                _fusion?.ResetPhysicalContribution();
                _holdingJumpDepth = false;
                _hasHeading = false;
                _wasCalibrationValid = false;
                _initializedPlayerRoot = true;
            }

            return true;
        }

        private CrouchGroundingSample UpdateCrouchGrounding(float deltaTime)
        {
            var feetAvailable = TryGetSolvedLegFootGeometry(
                out var leftFootRelativeY,
                out var rightFootRelativeY,
                out var legReferenceScale);
            return _crouchGrounding.Update(
                true,
                VerticalSample,
                vertical,
                feetAvailable,
                leftFootRelativeY,
                rightFootRelativeY,
                legReferenceScale,
                deltaTime);
        }

        private bool TryGetSolvedLegFootGeometry(
            out float leftFootRelativeY,
            out float rightFootRelativeY,
            out float legReferenceScale)
        {
            leftFootRelativeY = 0f;
            rightFootRelativeY = 0f;
            legReferenceScale = 0f;
            if (_playerRoot == null ||
                binding == null ||
                !binding.IsBound ||
                !binding.IsChainAvailable(CanonicalKinematicChainId.LeftLeg) ||
                !binding.IsChainAvailable(CanonicalKinematicChainId.RightLeg))
            {
                return false;
            }

            var leftFoot = binding.GetChainTip(CanonicalKinematicChainId.LeftLeg);
            var rightFoot = binding.GetChainTip(CanonicalKinematicChainId.RightLeg);
            if (leftFoot == null || rightFoot == null)
            {
                return false;
            }

            leftFootRelativeY = leftFoot.position.y - _playerRoot.position.y;
            rightFootRelativeY = rightFoot.position.y - _playerRoot.position.y;
            legReferenceScale =
                (binding.GetChainTotalReach(CanonicalKinematicChainId.LeftLeg) +
                 binding.GetChainTotalReach(CanonicalKinematicChainId.RightLeg)) * 0.5f;
            return IsFinite(leftFootRelativeY) &&
                IsFinite(rightFootRelativeY) &&
                IsFinite(legReferenceScale) &&
                legReferenceScale > 0.0001f;
        }

        private void RestoreVerticalOrigin()
        {
            if (_playerRoot == null || !_hasVerticalOrigin || !driveLocomotion)
            {
                return;
            }

            var position = _playerRoot.position;
            position.y = _verticalOriginY;
            _playerRoot.position = position;
        }

        private Vector2 CurrentPlayerXZ()
        {
            if (_playerRoot == null)
            {
                return Vector2.zero;
            }

            return new Vector2(_playerRoot.position.x, _playerRoot.position.z);
        }

        private void ClearFrameDiagnostics()
        {
            RootSample = default;
            VerticalSample = _verticalInterpreter == null
                ? default
                : _verticalInterpreter.LatestSample;
            GroundingSample = _crouchGrounding == null
                ? default
                : _crouchGrounding.LatestSample;
            CadenceSample = default;
            HeadingSample = default;
            FusionResult = default;
            FinalFrameMotionXZ = Vector2.zero;
            FinalWorldPositionXZ = _playerRoot == null ? Vector2.zero : CurrentPlayerXZ();
            FinalFrameMotionY = 0f;
            FinalWorldPositionY = _playerRoot == null ? 0f : _playerRoot.position.y;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
