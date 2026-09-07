using System;
using GoldenNeedle.Core.Motion.Retargeting;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    [Serializable]
    public sealed class LocomotionFusionSettings
    {
        [Min(0f)] public float lateralScale = 1.8f;
        [Min(0f)] public float depthScale = 3.0f;
        [Min(0f)] public float lateralDeadzone = 0.012f;
        [Min(0f)] public float depthDeadzone = 0.012f;
        [Min(0f)] public float physicalVelocityStart = 0.08f;
        [Min(0f)] public float physicalVelocityFull = 0.32f;
        [Range(0f, 1f)] public float minimumRootConfidence = 0.30f;

        public void Sanitize()
        {
            lateralScale = Mathf.Max(0f, Safe(lateralScale, 1.8f));
            depthScale = Mathf.Max(0f, Safe(depthScale, 3.0f));
            lateralDeadzone = Mathf.Max(0f, Safe(lateralDeadzone, 0.012f));
            depthDeadzone = Mathf.Max(0f, Safe(depthDeadzone, 0.012f));
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
    /// Fuses finite physical displacement with cadence extension. Cadence is suppressed from
    /// current physical root velocity so actual walking is not obviously counted twice. Temporary
    /// root-tracking loss holds the last trusted physical offset instead of snapping to origin.
    /// </summary>
    public sealed class LocomotionFusion
    {
        private readonly LocomotionFusionSettings _settings;
        private Vector2 _lastPhysicalContribution;

        public LocomotionFusion(LocomotionFusionSettings settings)
        {
            _settings = settings ?? new LocomotionFusionSettings();
            _settings.Sanitize();
        }

        public void ResetPhysicalContribution()
        {
            _lastPhysicalContribution = Vector2.zero;
        }

        public LocomotionFusionResult Evaluate(
            CameraSpaceRootSample root,
            CadenceSample cadence,
            Vector2 worldHeadingXZ,
            CanonicalToAvatarAxisMap physicalReferenceMap)
        {
            var rootUsable = root.isValid &&
                root.hasOrigin &&
                root.confidence >= _settings.minimumRootConfidence &&
                physicalReferenceMap.IsValid;

            if (rootUsable)
            {
                var scaledCameraDisplacement = new Vector3(
                    ApplyDeadzone(
                        root.displacementXZ.x,
                        _settings.lateralDeadzone) * _settings.lateralScale,
                    0f,
                    ApplyDeadzone(
                        root.displacementXZ.y,
                        _settings.depthDeadzone) * _settings.depthScale);
                _lastPhysicalContribution = MapCameraVectorToWorldXZ(
                    scaledCameraDisplacement,
                    physicalReferenceMap);
            }

            var physicalVelocity = rootUsable
                ? MapCameraVectorToWorldXZ(
                    new Vector3(
                        root.velocityXZ.x * _settings.lateralScale,
                        0f,
                        root.velocityXZ.y * _settings.depthScale),
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
