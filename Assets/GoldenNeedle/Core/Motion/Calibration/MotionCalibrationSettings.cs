using System;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Calibration
{
    [Serializable]
    public sealed class MotionCalibrationSettings
    {
        [Min(0.1f)] public float neutralHoldSeconds = 1.25f;
        [Min(0.1f)] public float tPoseHoldSeconds = 0.75f;
        [Range(0.01f, 0.5f)] public float neutralStabilityTolerance = 0.06f;
        [Range(0.01f, 0.5f)] public float tPoseStabilityTolerance = 0.10f;
        [Range(5f, 45f)] public float tPoseAngularToleranceDegrees = 25f;
        [Range(0.1f, 1f)] public float minimumArmExtensionRatio = 0.65f;
        [Range(90f, 179f)] public float minimumElbowExtensionDegrees = 135f;
        [Range(0.01f, 0.5f)] public float wristHeightTolerance = 0.16f;

        public void Sanitize()
        {
            neutralHoldSeconds = Mathf.Max(0.1f, Safe(neutralHoldSeconds, 1.25f));
            tPoseHoldSeconds = Mathf.Max(0.1f, Safe(tPoseHoldSeconds, 0.75f));
            neutralStabilityTolerance = Mathf.Clamp(Safe(neutralStabilityTolerance, 0.06f), 0.01f, 0.5f);
            tPoseStabilityTolerance = Mathf.Clamp(Safe(tPoseStabilityTolerance, 0.10f), 0.01f, 0.5f);
            tPoseAngularToleranceDegrees = Mathf.Clamp(Safe(tPoseAngularToleranceDegrees, 25f), 5f, 45f);
            minimumArmExtensionRatio = Mathf.Clamp(Safe(minimumArmExtensionRatio, 0.65f), 0.1f, 1f);
            minimumElbowExtensionDegrees = Mathf.Clamp(Safe(minimumElbowExtensionDegrees, 135f), 90f, 179f);
            wristHeightTolerance = Mathf.Clamp(Safe(wristHeightTolerance, 0.16f), 0.01f, 0.5f);
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
