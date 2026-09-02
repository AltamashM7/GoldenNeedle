using System;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Stabilization
{
    /// <summary>
    /// Small project-owned One Euro filters for position signals. The filter is deliberately
    /// independent of providers, avatars, animation, and locomotion.
    /// </summary>
    public sealed class OneEuroFilter1D
    {
        private const float TwoPi = 6.28318530718f;
        private const float MinimumDt = 0.0001f;
        private const float MaximumDt = 1f;

        private readonly float _minCutoff;
        private readonly float _beta;
        private readonly float _derivativeCutoff;
        private bool _initialized;
        private float _value;
        private float _derivative;

        public OneEuroFilter1D(float minCutoff = 1f, float beta = 0.05f, float derivativeCutoff = 1f)
        {
            _minCutoff = Mathf.Max(0.0001f, Sanitize(minCutoff, 1f));
            _beta = Mathf.Max(0f, Sanitize(beta, 0.05f));
            _derivativeCutoff = Mathf.Max(0.0001f, Sanitize(derivativeCutoff, 1f));
        }

        public bool IsInitialized => _initialized;

        public float Filter(float value, float deltaTimeSeconds)
        {
            if (!IsFinite(value))
            {
                return _initialized ? _value : 0f;
            }

            if (!_initialized)
            {
                _initialized = true;
                _value = value;
                _derivative = 0f;
                return value;
            }

            var dt = SanitizeDeltaTime(deltaTimeSeconds);
            var rawDerivative = (value - _value) / dt;
            var derivativeAlpha = Alpha(_derivativeCutoff, dt);
            _derivative = Mathf.Lerp(_derivative, rawDerivative, derivativeAlpha);

            var cutoff = _minCutoff + _beta * Mathf.Abs(_derivative);
            _value = Mathf.Lerp(_value, value, Alpha(cutoff, dt));
            return _value;
        }

        public void Reset()
        {
            _initialized = false;
            _value = 0f;
            _derivative = 0f;
        }

        private static float Alpha(float cutoff, float deltaTimeSeconds)
        {
            var safeCutoff = Mathf.Max(0.0001f, cutoff);
            var tau = 1f / (TwoPi * safeCutoff);
            return 1f / (1f + tau / deltaTimeSeconds);
        }

        private static float SanitizeDeltaTime(float value)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                return 0.05f;
            }

            return Mathf.Clamp(value, MinimumDt, MaximumDt);
        }

        private static float Sanitize(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class OneEuroFilterVector2
    {
        private readonly OneEuroFilter1D _x;
        private readonly OneEuroFilter1D _y;

        public OneEuroFilterVector2(float minCutoff = 1f, float beta = 0.05f, float derivativeCutoff = 1f)
        {
            _x = new OneEuroFilter1D(minCutoff, beta, derivativeCutoff);
            _y = new OneEuroFilter1D(minCutoff, beta, derivativeCutoff);
        }

        public bool IsInitialized => _x.IsInitialized && _y.IsInitialized;

        public Vector2 Filter(Vector2 value, float deltaTimeSeconds)
        {
            return new Vector2(_x.Filter(value.x, deltaTimeSeconds), _y.Filter(value.y, deltaTimeSeconds));
        }

        public void Reset()
        {
            _x.Reset();
            _y.Reset();
        }
    }

    public sealed class OneEuroFilterVector3
    {
        private readonly OneEuroFilter1D _x;
        private readonly OneEuroFilter1D _y;
        private readonly OneEuroFilter1D _z;

        public OneEuroFilterVector3(float minCutoff = 1f, float beta = 0.05f, float derivativeCutoff = 1f)
        {
            _x = new OneEuroFilter1D(minCutoff, beta, derivativeCutoff);
            _y = new OneEuroFilter1D(minCutoff, beta, derivativeCutoff);
            _z = new OneEuroFilter1D(minCutoff, beta, derivativeCutoff);
        }

        public bool IsInitialized => _x.IsInitialized && _y.IsInitialized && _z.IsInitialized;

        public Vector3 Filter(Vector3 value, float deltaTimeSeconds)
        {
            return new Vector3(
                _x.Filter(value.x, deltaTimeSeconds),
                _y.Filter(value.y, deltaTimeSeconds),
                _z.Filter(value.z, deltaTimeSeconds));
        }

        public void Reset()
        {
            _x.Reset();
            _y.Reset();
            _z.Reset();
        }
    }
}
