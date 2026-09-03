using System;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Calibration
{
    [Serializable]
    public sealed class MotionCalibrationSettings
    {
        [Min(0.1f)] public float bodyReferenceHoldSeconds = 0.75f;
        [Range(0.01f, 0.5f)] public float bodyReferenceStabilityTolerance = 0.06f;
        [Range(0f, 1f)] public float minimumMeasurementConfidence = 0.40f;
        [Range(3, 24)] public int geometrySamplesRequired = 8;
        [Min(0.001f)] public float minimumSegmentLength = 0.01f;

        public void Sanitize()
        {
            bodyReferenceHoldSeconds = Mathf.Max(0.1f, Safe(bodyReferenceHoldSeconds, 0.75f));
            bodyReferenceStabilityTolerance = Mathf.Clamp(
                Safe(bodyReferenceStabilityTolerance, 0.06f),
                0.01f,
                0.5f);
            minimumMeasurementConfidence = Mathf.Clamp01(
                Safe(minimumMeasurementConfidence, 0.40f));
            geometrySamplesRequired = Mathf.Clamp(geometrySamplesRequired, 3, 24);
            minimumSegmentLength = Mathf.Max(
                0.001f,
                Safe(minimumSegmentLength, 0.01f));
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
