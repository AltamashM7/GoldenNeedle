using System;
using UnityEngine;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    [Serializable]
    public sealed class PoseTrustSettings
    {
        [Range(0f, 1f)]
        public float visibilityThreshold = 0.5f;

        [Range(0f, 1f)]
        public float presenceThreshold = 0.5f;

        [Min(0f)]
        public float normalizedBoundsPadding = 0.08f;

        [Min(0f)]
        public float trackingGraceSeconds = 0.12f;

        [Min(0.05f)]
        public float staleResultSeconds = 0.5f;
    }

    public static class PoseTrustClassifier
    {
        public static bool IsCandidate(
            float x,
            float y,
            float z,
            float visibility,
            bool hasVisibility,
            float presence,
            bool hasPresence,
            PoseTrustSettings settings)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
            {
                return false;
            }

            if (hasVisibility && visibility < settings.visibilityThreshold)
            {
                return false;
            }

            if (hasPresence && presence < settings.presenceThreshold)
            {
                return false;
            }

            var padding = settings.normalizedBoundsPadding < 0f ? 0f : settings.normalizedBoundsPadding;
            return x >= -padding && x <= 1f + padding && y >= -padding && y <= 1f + padding;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
