using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Runtime;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    /// <summary>
    /// Phase-5 root-translation owner. Phase 4 continues to own normal pose/IK. Physical X/Z comes
    /// from one authoritative CameraSpaceRootTracker. VerticalLocomotionInterpreter owns semantic
    /// Jump/Crouch and normalized body-compression evidence; AvatarRelativeCrouchGrounding maps
    /// grounded compression onto stable avatar reference leg geometry without re-solving the legs.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class EmbodiedLocomotionController : MonoBehaviour
    {
        [Header("Runtime Wiring")]
        [SerializeField] private MotionEngineRuntime runtime;
        [SerializeField] private HumanoidRigBinding binding;
        [Tooltip("Enable Phase-5 avatar-root translation. Phase 4 pose retargeting is independent.")]
        [SerializeField] private bool driveLocomotion = true;

        [Header("Root / Physical Tracking")]
        [Tooltip("Body-candidate tracking, support validation, filtering and depth-corroboration settings.")]
        [SerializeField] private CameraSpaceRootTrackerSettings rootTracking = new CameraSpaceRootTrackerSettings();

        [Header("Vertical Locomotion")]
        [Tooltip("Standing reference, jump/crouch semantics, continuous grounded compression, response and grace settings.")]
        [SerializeField] private VerticalLocomotionSettings vertical = new VerticalLocomotionSettings();

        [Header("Avatar-relative Crouch")]
        [Tooltip("Maps normalized grounded body compression onto stable avatar standing-leg geometry. This does not rotate/re-solve the legs.")]
        [SerializeField] private AvatarRelativeCrouchGroundingSettings crouchGrounding = new AvatarRelativeCrouchGroundingSettings();

        [Header("Physical Locomotion / Fusion")]
        [Tooltip("Physical mapping scale/deadzones and physical-vs-cadence suppression settings.")]
        [SerializeField] private LocomotionFusionSettings fusion = new LocomotionFusionSettings();

        [Header("Cadence")]
        [Tooltip("Lower-body cadence acquisition, confidence, stop, and virtual-speed settings.")]
        [SerializeField] private CadenceDetectorSettings cadence = new CadenceDetectorSettings();

        [Header("Heading")]
        [Tooltip("Response speed for smoothing cadence travel heading. Physical room displacement does not use live heading.")]
        [Min(0.1f), SerializeField] private float headingResponse = 8f;

        private CameraSpaceRootTracker _rootTracker;
        private VerticalLocomotionInterpreter _verticalInterpreter;
        private AvatarRelativeCrouchGrounding _avatarCrouchGrounding;
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
        public AvatarRelativeCrouchGroundingSample CrouchGroundingSample { get; private set; }
        public CadenceSample CadenceSample { get; private set; }
        public BodyHeadingSample HeadingSample { get; private set; }
        public LocomotionFusionResult FusionResult { get; private set; }
        public Vector2 FinalFrameMotionXZ { get; private set; }
        public Vector2 FinalWorldPositionXZ { get; private set; }
        public float FinalFrameMotionY { get; private set; }
        public float FinalWorldPositionY { get; private set; }
        public float VerticalOriginY => _verticalOriginY;
        public bool HasVerticalOrigin => _hasVerticalOrigin;
        public bool RecenterPending => _pendingRecenter ||
            (_rootTracker != null && _rootTracker.RecenterPending);
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
                    _avatarCrouchGrounding.Reset();
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
                _avatarCrouchGrounding.Reset();
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
            CrouchGroundingSample = _avatarCrouchGrounding.Update(
                binding,
                calibrationValid,
                VerticalSample,
                vertical,
                dt);

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

                // Genuine vertical takeoff can move support landmarks in image Y. Hold the
                // pre-jump accepted depth only at the fusion boundary so jump cannot become Z.
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
                physicalReferenceMap);

            _virtualOriginXZ += FusionResult.cadenceVelocity * dt;
            var desiredXZ = _virtualOriginXZ + FusionResult.physicalContribution;
            var verticalOffsetY = VerticalSample.IsJumpActive
                ? Mathf.Max(0f, VerticalSample.worldOffsetY)
                : CrouchGroundingSample.hasAvatarLegScale
                    ? CrouchGroundingSample.finalRootOffsetY
                    : 0f;
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
                // Phase 4 has already produced the visible/smoothed leg pose at execution order
                // 100. Phase 5 now translates the avatar root only; no crouch-specific leg IK runs
                // afterward, so Phase 4 remains the knee/leg pose authority.
                _playerRoot.position = new Vector3(
                    desiredXZ.x,
                    desiredY,
                    desiredXZ.y);
            }
        }

        /// <summary>
        /// Makes the current physical X/Z tracking position the new origin without moving the
        /// virtual character. Vertical standing reference/root origin remain independent.
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

            if (_avatarCrouchGrounding == null)
            {
                crouchGrounding = crouchGrounding ?? new AvatarRelativeCrouchGroundingSettings();
                crouchGrounding.Sanitize();
                _avatarCrouchGrounding = new AvatarRelativeCrouchGrounding(crouchGrounding);
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
                _avatarCrouchGrounding?.Reset();
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
                _avatarCrouchGrounding?.Reset();
                _cadenceDetector?.Reset();
                _fusion?.ResetPhysicalContribution();
                _holdingJumpDepth = false;
                _hasHeading = false;
                _wasCalibrationValid = false;
                _initializedPlayerRoot = true;
            }

            return true;
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
            RootSample = _rootTracker == null ? default : _rootTracker.LatestSample;
            VerticalSample = _verticalInterpreter == null
                ? default
                : _verticalInterpreter.LatestSample;
            CrouchGroundingSample = _avatarCrouchGrounding == null
                ? default
                : _avatarCrouchGrounding.LatestSample;
            CadenceSample = default;
            HeadingSample = default;
            FusionResult = default;
            FinalFrameMotionXZ = Vector2.zero;
            FinalWorldPositionXZ = _playerRoot == null ? Vector2.zero : CurrentPlayerXZ();
            FinalFrameMotionY = 0f;
            FinalWorldPositionY = _playerRoot == null ? 0f : _playerRoot.position.y;
        }
    }
}
