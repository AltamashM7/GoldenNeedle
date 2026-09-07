using System;
using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Runtime;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    /// <summary>
    /// Phase 5A root-translation controller. It leaves Phase 4 pose/orientation untouched and only
    /// writes the bound avatar root X/Z world position after retargeting. Root Y and root rotation
    /// are never copied from tracking.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class EmbodiedLocomotionController : MonoBehaviour
    {
        [SerializeField] private MotionEngineRuntime runtime;
        [SerializeField] private HumanoidRigBinding binding;
        [SerializeField] private bool driveLocomotion = true;
        [SerializeField] private CameraSpaceRootTrackerSettings rootTracking = new CameraSpaceRootTrackerSettings();
        [SerializeField] private CadenceDetectorSettings cadence = new CadenceDetectorSettings();
        [SerializeField] private LocomotionFusionSettings fusion = new LocomotionFusionSettings();
        [Min(0.1f), SerializeField] private float headingResponse = 8f;

        private CameraSpaceRootTracker _rootTracker;
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

        public bool DriveLocomotion
        {
            get => driveLocomotion;
            set => driveLocomotion = value;
        }

        public CameraSpaceRootSample RootSample { get; private set; }
        public CadenceSample CadenceSample { get; private set; }
        public BodyHeadingSample HeadingSample { get; private set; }
        public LocomotionFusionResult FusionResult { get; private set; }
        public Vector2 FinalFrameMotionXZ { get; private set; }
        public Vector2 FinalWorldPositionXZ { get; private set; }
        public bool RecenterPending => _pendingRecenter || (_rootTracker != null && _rootTracker.RecenterPending);
        public int RecenterCount => _recenterCount;
        public Transform PlayerRoot => _playerRoot;

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
                    _cadenceDetector.Reset();
                    _fusion.ResetPhysicalContribution();
                    _virtualOriginXZ = CurrentPlayerXZ();
                    _hasHeading = false;
                }

                _wasCalibrationValid = false;
                ClearFrameDiagnostics();
                return;
            }

            if (!_wasCalibrationValid)
            {
                _rootTracker.Reset();
                _cadenceDetector.Reset();
                _fusion.ResetPhysicalContribution();
                _virtualOriginXZ = CurrentPlayerXZ();
                _hasHeading = false;
                _wasCalibrationValid = true;
            }

            var dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            RootSample = _rootTracker.Update(runtime.StabilizedFrame, profile, dt);

            if (_pendingRecenter && RootSample.isValid)
            {
                PerformRecenterNow();
                RootSample = _rootTracker.LatestSample;
            }

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

            FusionResult = _fusion.Evaluate(
                RootSample,
                CadenceSample,
                _hasHeading ? _smoothedHeading : Vector2.zero,
                physicalReferenceMap);

            _virtualOriginXZ += FusionResult.cadenceVelocity * dt;
            var desired = _virtualOriginXZ + FusionResult.physicalContribution;
            var current = CurrentPlayerXZ();
            FinalFrameMotionXZ = desired - current;
            FinalWorldPositionXZ = desired;

            if (driveLocomotion)
            {
                var currentPosition = _playerRoot.position;
                _playerRoot.position = new Vector3(
                    desired.x,
                    currentPosition.y,
                    desired.y);
            }
        }

        /// <summary>
        /// Makes the current physical tracking position the new physical origin without moving the
        /// virtual character. The same public API can later be invoked by a discrete voice command.
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
            // equal to the current world position preserves GamePosition exactly.
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
        }

        private bool ResolvePlayerRoot()
        {
            var candidate = binding != null && binding.IsBound ? binding.AvatarRoot : null;
            if (candidate == null)
            {
                _playerRoot = null;
                _initializedPlayerRoot = false;
                return false;
            }

            if (_playerRoot != candidate || !_initializedPlayerRoot)
            {
                _playerRoot = candidate;
                _virtualOriginXZ = CurrentPlayerXZ();
                _rootTracker?.Reset();
                _cadenceDetector?.Reset();
                _fusion?.ResetPhysicalContribution();
                _hasHeading = false;
                _wasCalibrationValid = false;
                _initializedPlayerRoot = true;
            }

            return true;
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
            CadenceSample = default;
            HeadingSample = default;
            FusionResult = default;
            FinalFrameMotionXZ = Vector2.zero;
            FinalWorldPositionXZ = _playerRoot == null ? Vector2.zero : CurrentPlayerXZ();
        }
    }
}
