using System;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    [Serializable]
    public sealed class CadenceDetectorSettings
    {
        [Range(0f, 1f)] public float minimumJointConfidence = 0.40f;
        [Min(1f)] public float signalResponse = 14f;
        [Min(0.01f)] public float eventThreshold = 0.07f;
        [Min(0.1f)] public float minimumStepRate = 0.8f;
        [Min(0.5f)] public float maximumStepRate = 4.5f;
        [Range(2, 6)] public int acquisitionEvents = 3;
        [Range(0f, 1f)] public float acquireConfidence = 0.50f;
        [Range(0f, 1f)] public float sustainConfidence = 0.25f;
        [Range(0.2f, 1.0f)] public float stopTimeoutSeconds = 0.50f;
        [Min(0.05f)] public float virtualStridePerStep = 0.42f;
        [Min(0.1f)] public float maximumVirtualSpeed = 2.5f;

        public void Sanitize()
        {
            minimumJointConfidence = Mathf.Clamp01(Safe(minimumJointConfidence, 0.40f));
            signalResponse = Mathf.Max(1f, Safe(signalResponse, 14f));
            eventThreshold = Mathf.Max(0.01f, Safe(eventThreshold, 0.07f));
            minimumStepRate = Mathf.Max(0.1f, Safe(minimumStepRate, 0.8f));
            maximumStepRate = Mathf.Max(minimumStepRate + 0.1f, Safe(maximumStepRate, 4.5f));
            acquisitionEvents = Mathf.Clamp(acquisitionEvents, 2, 6);
            acquireConfidence = Mathf.Clamp01(Safe(acquireConfidence, 0.50f));
            sustainConfidence = Mathf.Clamp01(Safe(sustainConfidence, 0.25f));
            stopTimeoutSeconds = Mathf.Clamp(Safe(stopTimeoutSeconds, 0.50f), 0.2f, 1.0f);
            virtualStridePerStep = Mathf.Max(0.05f, Safe(virtualStridePerStep, 0.42f));
            maximumVirtualSpeed = Mathf.Max(0.1f, Safe(maximumVirtualSpeed, 2.5f));
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    public struct CadenceSample
    {
        public bool active;
        public float confidence;
        public float rateStepsPerSecond;
        public float virtualSpeed;
        public float phaseSignal;
        public float secondsSinceEvent;
    }

    /// <summary>
    /// First-pass cadence detector based on alternating left/right lower-body rhythm. Ankle
    /// vertical separation is primary, knee separation is supporting evidence. It intentionally
    /// contains no arm fallback yet.
    /// </summary>
    public sealed class CadenceDetector
    {
        private readonly CadenceDetectorSettings _settings;
        private float _time;
        private float _filteredSignal;
        private bool _hasSignal;
        private int _lastEventSign;
        private float _lastEventTime = float.NegativeInfinity;
        private float _smoothedInterval;
        private int _validEvents;
        private float _confidence;
        private bool _active;

        public CadenceDetector(CadenceDetectorSettings settings)
        {
            _settings = settings ?? new CadenceDetectorSettings();
            _settings.Sanitize();
        }

        public CadenceSample LatestSample { get; private set; }

        public void Reset()
        {
            _time = 0f;
            _filteredSignal = 0f;
            _hasSignal = false;
            _lastEventSign = 0;
            _lastEventTime = float.NegativeInfinity;
            _smoothedInterval = 0f;
            _validEvents = 0;
            _confidence = 0f;
            _active = false;
            LatestSample = default;
        }

        public CadenceSample Update(
            CanonicalPoseFrame frame,
            float apparentBodyScale,
            float deltaTime)
        {
            var dt = Mathf.Max(0.0001f, deltaTime);
            _time += dt;

            if (!TryBuildSignal(frame, apparentBodyScale, out var rawSignal, out var evidenceConfidence))
            {
                Decay(dt);
                Publish(0f);
                return LatestSample;
            }

            if (!_hasSignal)
            {
                _filteredSignal = rawSignal;
                _hasSignal = true;
            }
            else
            {
                var alpha = 1f - Mathf.Exp(-_settings.signalResponse * dt);
                _filteredSignal = Mathf.Lerp(_filteredSignal, rawSignal, alpha);
            }

            var eventSign = _filteredSignal >= _settings.eventThreshold
                ? 1
                : _filteredSignal <= -_settings.eventThreshold ? -1 : 0;
            if (eventSign != 0 && eventSign != _lastEventSign)
            {
                RegisterAlternatingEvent(eventSign, evidenceConfidence);
            }

            var eventAge = SecondsSinceEvent();
            if (eventAge > _settings.stopTimeoutSeconds)
            {
                _active = false;
                _validEvents = 0;
                _confidence = Mathf.MoveTowards(_confidence, 0f, dt * 2.5f);
            }
            else
            {
                _confidence = Mathf.Clamp01(_confidence * Mathf.Lerp(0.85f, 1f, evidenceConfidence));
                if (!_active &&
                    _validEvents >= _settings.acquisitionEvents &&
                    _confidence >= _settings.acquireConfidence)
                {
                    _active = true;
                }
                else if (_active && _confidence < _settings.sustainConfidence)
                {
                    _active = false;
                }
            }

            Publish(_filteredSignal);
            return LatestSample;
        }

        private void RegisterAlternatingEvent(int eventSign, float evidenceConfidence)
        {
            if (_lastEventSign == 0 || float.IsNegativeInfinity(_lastEventTime))
            {
                _lastEventSign = eventSign;
                _lastEventTime = _time;
                _validEvents = 1;
                _confidence = Mathf.Max(_confidence, 0.20f * evidenceConfidence);
                return;
            }

            var interval = _time - _lastEventTime;
            var minimumInterval = 1f / _settings.maximumStepRate;
            var maximumInterval = 1f / _settings.minimumStepRate;
            if (interval >= minimumInterval && interval <= maximumInterval)
            {
                _smoothedInterval = _smoothedInterval <= 0f
                    ? interval
                    : Mathf.Lerp(_smoothedInterval, interval, 0.35f);
                _validEvents++;
                var consistency = Mathf.Clamp01(
                    1f - Mathf.Abs(interval - _smoothedInterval) /
                    Mathf.Max(0.05f, _smoothedInterval));
                _confidence = Mathf.Clamp01(
                    _confidence +
                    Mathf.Lerp(0.18f, 0.34f, consistency) *
                    Mathf.Clamp01(evidenceConfidence));
            }
            else
            {
                _validEvents = 1;
                _smoothedInterval = 0f;
                _confidence *= 0.45f;
                _active = false;
            }

            _lastEventSign = eventSign;
            _lastEventTime = _time;
        }

        private void Decay(float deltaTime)
        {
            if (SecondsSinceEvent() > _settings.stopTimeoutSeconds)
            {
                _active = false;
                _validEvents = 0;
            }

            _confidence = Mathf.MoveTowards(_confidence, 0f, deltaTime * 3f);
            _filteredSignal = Mathf.Lerp(
                _filteredSignal,
                0f,
                1f - Mathf.Exp(-_settings.signalResponse * deltaTime));
        }

        private void Publish(float phaseSignal)
        {
            var rate = _smoothedInterval > 0.0001f
                ? Mathf.Clamp(
                    1f / _smoothedInterval,
                    _settings.minimumStepRate,
                    _settings.maximumStepRate)
                : 0f;
            var speed = _active
                ? Mathf.Min(_settings.maximumVirtualSpeed, rate * _settings.virtualStridePerStep)
                : 0f;
            LatestSample = new CadenceSample
            {
                active = _active,
                confidence = Mathf.Clamp01(_confidence),
                rateStepsPerSecond = rate,
                virtualSpeed = speed,
                phaseSignal = phaseSignal,
                secondsSinceEvent = SecondsSinceEvent(),
            };
        }

        private float SecondsSinceEvent()
        {
            return float.IsNegativeInfinity(_lastEventTime)
                ? float.PositiveInfinity
                : Mathf.Max(0f, _time - _lastEventTime);
        }

        private bool TryBuildSignal(
            CanonicalPoseFrame frame,
            float apparentBodyScale,
            out float signal,
            out float evidenceConfidence)
        {
            signal = 0f;
            evidenceConfidence = 0f;
            if (frame == null || apparentBodyScale <= 0.0001f)
            {
                return false;
            }

            var weightedSignal = 0f;
            var weightSum = 0f;
            var confidenceSum = 0f;
            if (TryImagePair(
                    frame,
                    CanonicalJointId.LeftAnkle,
                    CanonicalJointId.RightAnkle,
                    out var ankleDifference,
                    out var ankleConfidence))
            {
                const float ankleWeight = 0.65f;
                weightedSignal += ankleDifference * ankleWeight;
                weightSum += ankleWeight;
                confidenceSum += ankleConfidence * ankleWeight;
            }

            if (TryImagePair(
                    frame,
                    CanonicalJointId.LeftKnee,
                    CanonicalJointId.RightKnee,
                    out var kneeDifference,
                    out var kneeConfidence))
            {
                const float kneeWeight = 0.35f;
                weightedSignal += kneeDifference * kneeWeight;
                weightSum += kneeWeight;
                confidenceSum += kneeConfidence * kneeWeight;
            }

            if (weightSum <= 0f)
            {
                return false;
            }

            signal = (weightedSignal / weightSum) / apparentBodyScale;
            evidenceConfidence = Mathf.Clamp01(confidenceSum / weightSum);
            return IsFinite(signal);
        }

        private bool TryImagePair(
            CanonicalPoseFrame frame,
            CanonicalJointId leftId,
            CanonicalJointId rightId,
            out float yDifference,
            out float confidence)
        {
            yDifference = 0f;
            confidence = 0f;
            var left = frame.GetJoint(leftId);
            var right = frame.GetJoint(rightId);
            if (!left.IsTracked || !right.IsTracked ||
                !left.hasImagePosition || !right.hasImagePosition ||
                left.confidence < _settings.minimumJointConfidence ||
                right.confidence < _settings.minimumJointConfidence ||
                !IsFinite(left.imagePosition) || !IsFinite(right.imagePosition))
            {
                return false;
            }

            yDifference = left.imagePosition.y - right.imagePosition.y;
            confidence = Mathf.Min(left.confidence, right.confidence);
            return IsFinite(yDifference);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
