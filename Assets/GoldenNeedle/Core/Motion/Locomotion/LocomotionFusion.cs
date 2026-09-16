using System;
using GoldenNeedle.Core.Motion.Retargeting;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    [Serializable]
    public sealed class LocomotionFusionSettings
    {
        [Tooltip("Game-world scale applied to mapped physical left/right support displacement.")]
        [Min(0f)] public float lateralScale = 0.9f;

        [Tooltip("Game-world scale applied to mapped physical toward/away support displacement.")]
        [Min(0f)] public float depthScale = 1.5f;

        [Tooltip("Camera-space lateral support displacement ignored around the physical origin before physical scaling.")]
        [Min(0f)] public float lateralDeadzone = 0.012f;

        [Tooltip("Camera-space depth support displacement ignored around the physical origin before physical scaling.")]
        [Min(0f)] public float depthDeadzone = 0.012f;

        [Tooltip("Normalized lateral displacement from the last accepted physical position required to leave stationary hold.")]
        [InspectorName("Lateral Movement Enter")]
        [Min(0.001f)] public float lateralMovementEnter = 0.025f;

        [Tooltip("Normalized lateral displacement needed to keep following physical motion after movement has acquired.")]
        [InspectorName("Lateral Movement Continue")]
        [Min(0f)] public float lateralMovementRelease = 0.010f;

        [Tooltip("Normalized camera-depth displacement from the last accepted physical position required to leave stationary hold.")]
        [InspectorName("Depth Movement Enter")]
        [Min(0.001f)] public float depthMovementEnter = 0.040f;

        [Tooltip("Normalized camera-depth displacement needed to keep following physical motion after movement has acquired.")]
        [InspectorName("Depth Movement Continue")]
        [Min(0f)] public float depthMovementRelease = 0.016f;

        [Tooltip("Mapped physical speed where cadence suppression begins.")]
        [Min(0f)] public float physicalVelocityStart = 0.08f;

        [Tooltip("Mapped physical speed where cadence is fully suppressed.")]
        [Min(0f)] public float physicalVelocityFull = 0.32f;

        [Tooltip("Minimum trusted support-base confidence required for a live physical update.")]
        [Range(0f, 1f)] public float minimumRootConfidence = 0.30f;

        public void Sanitize()
        {
            lateralScale = Mathf.Max(0f, Safe(lateralScale, 0.9f));
            depthScale = Mathf.Max(0f, Safe(depthScale, 1.5f));
            lateralDeadzone = Mathf.Max(0f, Safe(lateralDeadzone, 0.012f));
            depthDeadzone = Mathf.Max(0f, Safe(depthDeadzone, 0.012f));
            lateralMovementEnter = Mathf.Max(0.001f, Safe(lateralMovementEnter, 0.025f));
            lateralMovementRelease = Mathf.Clamp(
                Safe(lateralMovementRelease, 0.010f),
                0f,
                Mathf.Max(0f, lateralMovementEnter - 0.001f));
            depthMovementEnter = Mathf.Max(0.001f, Safe(depthMovementEnter, 0.040f));
            depthMovementRelease = Mathf.Clamp(
                Safe(depthMovementRelease, 0.016f),
                0f,
                Mathf.Max(0f, depthMovementEnter - 0.001f));
            physicalVelocityStart = Mathf.Max(0f, Safe(physicalVelocityStart, 0.08f));
            physicalVelocityFull = Mathf.Max(
                physicalVelocityStart + 0.001f,
                Safe(physicalVelocityFull, 0.32f));
            minimumRootConfidence = Mathf.Clamp01(Safe(minimumRootConfidence, 0.30f));
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    public struct LocomotionFusionResult
    {
        public Vector2 physicalContribution;
        public Vector2 cadenceVelocity;
        public float physicalActivity;
        public float cadenceBlend;
        public bool physicalTranslationActive;
        public bool cadenceActive;
        public bool rootTrackingLive;
    }

    /// <summary>
    /// Fuses finite physical displacement with cadence extension. A stateful stationary gate owns
    /// the accepted physical displacement so tiny support fluctuations around any already-displaced
    /// position do not hunt the avatar root or spuriously suppress cadence. Temporary root-tracking
    /// loss holds the last accepted physical offset and continuity-rebases the gate on reacquisition.
    /// </summary>
    public sealed class LocomotionFusion
    {
        private readonly LocomotionFusionSettings _settings;
        private Vector2 _lastPhysicalContribution;
        private Vector2 _acceptedCameraDisplacement;
        private Vector2 _measurementRebaseOffset;
        private bool _hasAcceptedCameraDisplacement;
        private bool _measurementNeedsRebase;
        private bool _lateralMoving;
        private bool _depthMoving;

        public LocomotionFusion(LocomotionFusionSettings settings)
        {
            _settings = settings ?? new LocomotionFusionSettings();
            _settings.Sanitize();
        }

        public void ResetPhysicalContribution()
        {
            _lastPhysicalContribution = Vector2.zero;
            _acceptedCameraDisplacement = Vector2.zero;
            _measurementRebaseOffset = Vector2.zero;
            _hasAcceptedCameraDisplacement = false;
            _measurementNeedsRebase = false;
            _lateralMoving = false;
            _depthMoving = false;
        }

        public LocomotionFusionResult Evaluate(
            CameraSpaceRootSample root,
            CadenceSample cadence,
            Vector2 worldHeadingXZ,
            CanonicalToAvatarAxisMap physicalReferenceMap,
            float deltaTime = 1f / 60f)
        {
            var rootUsable = root.isValid &&
                root.hasOrigin &&
                root.confidence >= _settings.minimumRootConfidence &&
                physicalReferenceMap.IsValid;

            var acceptedVelocityCamera = Vector2.zero;
            if (rootUsable)
            {
                var measuredCameraDisplacement = new Vector2(
                    ApplyDeadzone(
                        root.displacementXZ.x,
                        _settings.lateralDeadzone),
                    ApplyDeadzone(
                        root.displacementXZ.y,
                        _settings.depthDeadzone));

                if (!_hasAcceptedCameraDisplacement)
                {
                    _acceptedCameraDisplacement = measuredCameraDisplacement;
                    _measurementRebaseOffset = Vector2.zero;
                    _hasAcceptedCameraDisplacement = true;
                    _measurementNeedsRebase = false;
                    _lateralMoving = false;
                    _depthMoving = false;
                    acceptedVelocityCamera = root.velocityXZ;
                }
                else
                {
                    if (_measurementNeedsRebase)
                    {
                        // The root tracker already continuity-rebases support authority. This
                        // additional offset makes the stationary gate itself continuity-safe when
                        // its input stream was temporarily unavailable: the first reacquired sample
                        // maps exactly onto the last accepted physical displacement.
                        _measurementRebaseOffset =
                            _acceptedCameraDisplacement - measuredCameraDisplacement;
                        _measurementNeedsRebase = false;
                        _lateralMoving = false;
                        _depthMoving = false;
                    }

                    var adjustedMeasurement =
                        measuredCameraDisplacement + _measurementRebaseOffset;
                    var previousAccepted = _acceptedCameraDisplacement;

                    var lateralAccepted = UpdateAcceptedAxis(
                        adjustedMeasurement.x,
                        ref _acceptedCameraDisplacement.x,
                        ref _lateralMoving,
                        _settings.lateralMovementEnter,
                        _settings.lateralMovementRelease);
                    var depthAccepted = UpdateAcceptedAxis(
                        adjustedMeasurement.y,
                        ref _acceptedCameraDisplacement.y,
                        ref _depthMoving,
                        _settings.depthMovementEnter,
                        _settings.depthMovementRelease);

                    var dt = Mathf.Max(0.0001f, deltaTime);
                    if (lateralAccepted)
                    {
                        acceptedVelocityCamera.x =
                            (_acceptedCameraDisplacement.x - previousAccepted.x) / dt;
                    }
                    if (depthAccepted)
                    {
                        acceptedVelocityCamera.y =
                            (_acceptedCameraDisplacement.y - previousAccepted.y) / dt;
                    }
                }

                var scaledAcceptedDisplacement = new Vector3(
                    _acceptedCameraDisplacement.x * _settings.lateralScale,
                    0f,
                    _acceptedCameraDisplacement.y * _settings.depthScale);
                _lastPhysicalContribution = MapCameraVectorToWorldXZ(
                    scaledAcceptedDisplacement,
                    physicalReferenceMap);
            }
            else if (_hasAcceptedCameraDisplacement)
            {
                _measurementNeedsRebase = true;
                _lateralMoving = false;
                _depthMoving = false;
            }

            var physicalVelocity = rootUsable
                ? MapCameraVectorToWorldXZ(
                    new Vector3(
                        acceptedVelocityCamera.x * _settings.lateralScale,
                        0f,
                        acceptedVelocityCamera.y * _settings.depthScale),
                    physicalReferenceMap)
                : Vector2.zero;
            var speed = physicalVelocity.magnitude;
            var activity = rootUsable
                ? Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        _settings.physicalVelocityStart,
                        _settings.physicalVelocityFull,
                        speed)) * root.confidence
                : 0f;

            var heading = worldHeadingXZ.sqrMagnitude > 0.000001f
                ? worldHeadingXZ.normalized
                : Vector2.zero;
            var cadenceBlend = cadence.active
                ? Mathf.Clamp01(cadence.confidence * (1f - activity))
                : 0f;
            var cadenceVelocity = heading * cadence.virtualSpeed * cadenceBlend;

            return new LocomotionFusionResult
            {
                physicalContribution = _lastPhysicalContribution,
                cadenceVelocity = cadenceVelocity,
                physicalActivity = activity,
                cadenceBlend = cadenceBlend,
                physicalTranslationActive = activity >= 0.35f,
                cadenceActive = cadence.active &&
                    cadenceBlend > 0.05f &&
                    heading.sqrMagnitude > 0f,
                rootTrackingLive = rootUsable,
            };
        }

        public static Vector2 MapCameraVectorToWorldXZ(
            Vector3 scaledCameraVector,
            CanonicalToAvatarAxisMap referenceMap)
        {
            if (!referenceMap.IsValid ||
                !IsFinite(scaledCameraVector))
            {
                return Vector2.zero;
            }

            var mappedWorld = referenceMap.MapVector(scaledCameraVector);
            if (!IsFinite(mappedWorld))
            {
                return Vector2.zero;
            }

            return new Vector2(mappedWorld.x, mappedWorld.z);
        }

        private static bool UpdateAcceptedAxis(
            float measured,
            ref float accepted,
            ref bool moving,
            float enterThreshold,
            float releaseThreshold)
        {
            var delta = measured - accepted;
            var magnitude = Mathf.Abs(delta);
            if (!moving)
            {
                if (magnitude < enterThreshold)
                {
                    return false;
                }

                accepted = measured;
                moving = true;
                return true;
            }

            if (magnitude >= releaseThreshold)
            {
                accepted = measured;
                return true;
            }

            moving = false;
            return false;
        }

        private static float ApplyDeadzone(float value, float deadzone)
        {
            var magnitude = Mathf.Abs(value);
            if (magnitude <= deadzone)
            {
                return 0f;
            }

            return Mathf.Sign(value) * (magnitude - deadzone);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}
