using System;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Stabilization
{
    [Serializable]
    public sealed class CanonicalStabilizerSettings
    {
        [Range(0f, 1f)] public float acquireConfidence = 0.60f;
        [Range(0f, 1f)] public float sustainConfidence = 0.40f;
        [Min(1)] public int acquireSamples = 2;
        [Min(0f)] public float lossGraceSeconds = 0.10f;
        [Min(0f)] public float resetAfterLossSeconds = 0.25f;

        [Header("One Euro positional filter")]
        [Min(0.0001f)] public float minCutoff = 1f;
        [Min(0f)] public float beta = 0.05f;
        [Min(0.0001f)] public float derivativeCutoff = 1f;
        [Min(0.0001f)] public float defaultDeltaTimeSeconds = 0.05f;
        [Min(0.0001f)] public float maximumDeltaTimeSeconds = 0.25f;

        public void Sanitize()
        {
            acquireConfidence = Mathf.Clamp01(Safe(acquireConfidence, 0.60f));
            sustainConfidence = Mathf.Clamp01(Safe(sustainConfidence, 0.40f));
            acquireSamples = Mathf.Max(1, acquireSamples);
            lossGraceSeconds = Mathf.Max(0f, Safe(lossGraceSeconds, 0.10f));
            resetAfterLossSeconds = Mathf.Max(lossGraceSeconds, Safe(resetAfterLossSeconds, 0.25f));
            minCutoff = Mathf.Max(0.0001f, Safe(minCutoff, 1f));
            beta = Mathf.Max(0f, Safe(beta, 0.05f));
            derivativeCutoff = Mathf.Max(0.0001f, Safe(derivativeCutoff, 1f));
            defaultDeltaTimeSeconds = Mathf.Max(0.0001f, Safe(defaultDeltaTimeSeconds, 0.05f));
            maximumDeltaTimeSeconds = Mathf.Max(defaultDeltaTimeSeconds, Safe(maximumDeltaTimeSeconds, 0.25f));
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
