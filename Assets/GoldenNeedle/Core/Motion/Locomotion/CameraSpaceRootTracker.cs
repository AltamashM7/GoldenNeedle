using System;
using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    [Serializable]
    public sealed class CameraSpaceRootTrackerSettings
    {
        [Range(0f, 1f)] public float minimumJointConfidence = 0.40f;
        [Min(0.001f)] public float minimumImageMeasurement = 0.005f;
        [Range(0.15f, 0.70f)] public float minimumYawCosine = 0.30f;
        [Min(0.1f)] public float positionResponse = 10f;
        [Min(0.1f)] public float velocityResponse = 8f;
        [Min(0.1f)] public float maximumDepthProxy = 1.5f;

        public void Sanitize()
        {
            minimumJointConfidence = Mathf.Clamp01(Safe(minimumJointConfidence, 0.40f));
            minimumImageMeasurement = Mathf.Max(0.001f, Safe(minimumImageMeasurement, 0.005f));
            minimumYawCosine = Mathf.Clamp(Safe(minimumYawCosine, 0.30f), 0.15f, 0.70f);
            positionResponse = Mathf.Max(0.1f, Safe(positionResponse, 10f));
            velocityResponse = Mathf.Max(0.1f, Safe(velocityResponse, 8f));
            maximumDepthProxy = Mathf.Max(0.1f, Safe(maximumDepthProxy, 1.5f));
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    public struct CameraSpaceRootSample
    {
        public bool isValid;
        public bool hasOrigin;
        public Vector2 positionXZ;
        public Vector2 displacementXZ;
        public Vector2 velocityXZ;
        public float confidence;
        public float apparentScale;
        public float yawCosine;
    }

    /// <summary>
    /// Estimates the person's relative camera-space root position independently from MediaPipe's
    /// pelvis-relative pose-world coordinates. Lateral position comes from absolute torso image
    /// placement; depth comes from recenter-relative apparent scale with torso-yaw compensation.
    /// </summary>
    public sealed class CameraSpaceRootTracker
    {
        private struct RootMeasurement
        {
            public float centerX;
            public float correctedShoulder;
            public float correctedHip;
            public float torso;
            public float apparentScale;
            public float widthReliability;
            public float confidence;
            public float yawCosine;
        }

        private readonly CameraSpaceRootTrackerSettings _settings;
        private bool _hasReference;
        private bool _pendingRecenter;
        private bool _hasFilteredDisplacement;
        private bool _hasMeasurement;
        private RootMeasurement _latestMeasurement;
        private RootMeasurement _referenceMeasurement;
        private float _referenceXProxy;
        private Vector2 _filteredDisplacement;
        private Vector2 _filteredVelocity;

        public CameraSpaceRootTracker(CameraSpaceRootTrackerSettings settings)
        {
            _settings = settings ?? new CameraSpaceRootTrackerSettings();
            _settings.Sanitize();
        }

        public CameraSpaceRootSample LatestSample { get; private set; }
        public bool HasOrigin => _hasReference;
        public bool RecenterPending => _pendingRecenter;

        public void Reset()
        {
            _hasReference = false;
            _pendingRecenter = false;
            _hasFilteredDisplacement = false;
            _hasMeasurement = false;
            _latestMeasurement = default;
            _referenceMeasurement = default;
            _referenceXProxy = 0f;
            _filteredDisplacement = Vector2.zero;
            _filteredVelocity = Vector2.zero;
            LatestSample = default;
        }

        public bool Recenter()
        {
            if (!_hasMeasurement || !LatestSample.isValid)
            {
                _pendingRecenter = true;
                return false;
            }

            CaptureReference(_latestMeasurement);
            Publish(true, _latestMeasurement, _referenceMeasurement.apparentScale);
            return true;
        }

        public CameraSpaceRootSample Update(
            CanonicalPoseFrame frame,
            MotionCalibrationProfile profile,
            float deltaTime)
        {
            if (!TryMeasure(frame, profile, out var measurement))
            {
                LatestSample = new CameraSpaceRootSample
                {
                    isValid = false,
                    hasOrigin = _hasReference,
                    positionXZ = LatestSample.positionXZ,
                    displacementXZ = _filteredDisplacement,
                    velocityXZ = Vector2.zero,
                    confidence = 0f,
                    apparentScale = LatestSample.apparentScale,
                    yawCosine = LatestSample.yawCosine,
                };
                return LatestSample;
            }

            _latestMeasurement = measurement;
            _hasMeasurement = true;
            if (!_hasReference || _pendingRecenter)
            {
                CaptureReference(measurement);
                Publish(true, measurement, measurement.apparentScale);
                return LatestSample;
            }

            var depthDisplacement = ComputeRelativeDepth(measurement);
            var effectiveScale = _referenceMeasurement.apparentScale *
                Mathf.Exp(-depthDisplacement);
            effectiveScale = Mathf.Max(_settings.minimumImageMeasurement, effectiveScale);
            var xProxy = (measurement.centerX - 0.5f) / effectiveScale;
            var rawDisplacement = new Vector2(
                xProxy - _referenceXProxy,
                depthDisplacement);

            var dt = Mathf.Max(0.0001f, deltaTime);
            if (!_hasFilteredDisplacement)
            {
                _filteredDisplacement = rawDisplacement;
                _filteredVelocity = Vector2.zero;
                _hasFilteredDisplacement = true;
            }
            else
            {
                var previous = _filteredDisplacement;
                var positionAlpha = 1f - Mathf.Exp(-_settings.positionResponse * dt);
                _filteredDisplacement = Vector2.Lerp(previous, rawDisplacement, positionAlpha);
                var rawVelocity = (_filteredDisplacement - previous) / dt;
                var velocityAlpha = 1f - Mathf.Exp(-_settings.velocityResponse * dt);
                _filteredVelocity = Vector2.Lerp(_filteredVelocity, rawVelocity, velocityAlpha);
            }

            _filteredDisplacement.y = Mathf.Clamp(
                _filteredDisplacement.y,
                -_settings.maximumDepthProxy,
                _settings.maximumDepthProxy);
            Publish(true, measurement, effectiveScale);
            return LatestSample;
        }

        private void CaptureReference(RootMeasurement measurement)
        {
            _referenceMeasurement = measurement;
            _referenceXProxy =
                (measurement.centerX - 0.5f) /
                Mathf.Max(_settings.minimumImageMeasurement, measurement.apparentScale);
            _filteredDisplacement = Vector2.zero;
            _filteredVelocity = Vector2.zero;
            _hasFilteredDisplacement = true;
            _hasReference = true;
            _pendingRecenter = false;
        }

        private void Publish(
            bool valid,
            RootMeasurement measurement,
            float effectiveScale)
        {
            LatestSample = new CameraSpaceRootSample
            {
                isValid = valid,
                hasOrigin = _hasReference,
                positionXZ = new Vector2(
                    _referenceXProxy + _filteredDisplacement.x,
                    _filteredDisplacement.y),
                displacementXZ = _filteredDisplacement,
                velocityXZ = _filteredVelocity,
                confidence = Mathf.Clamp01(measurement.confidence),
                apparentScale = effectiveScale,
                yawCosine = measurement.yawCosine,
            };
        }

        private float ComputeRelativeDepth(RootMeasurement current)
        {
            var sharedWidthReliability = Mathf.Min(
                current.widthReliability,
                _referenceMeasurement.widthReliability);
            var shoulderWeight = 0.32f * sharedWidthReliability;
            var hipWeight = 0.32f * sharedWidthReliability;
            const float torsoWeight = 0.36f;
            var weightSum = torsoWeight + shoulderWeight + hipWeight;

            var logRatio =
                torsoWeight * SafeLogRatio(current.torso, _referenceMeasurement.torso) +
                shoulderWeight * SafeLogRatio(
                    current.correctedShoulder,
                    _referenceMeasurement.correctedShoulder) +
                hipWeight * SafeLogRatio(
                    current.correctedHip,
                    _referenceMeasurement.correctedHip);
            return Mathf.Clamp(
                -logRatio / Mathf.Max(0.0001f, weightSum),
                -_settings.maximumDepthProxy,
                _settings.maximumDepthProxy);
        }

        private float SafeLogRatio(float current, float reference)
        {
            var minimum = _settings.minimumImageMeasurement;
            return Mathf.Log(
                Mathf.Max(minimum, current) /
                Mathf.Max(minimum, reference));
        }

        private bool TryMeasure(
            CanonicalPoseFrame frame,
            MotionCalibrationProfile profile,
            out RootMeasurement measurement)
        {
            measurement = default;
            if (frame == null || profile == null || !profile.bodyReferenceValid)
            {
                return false;
            }

            if (!TryImageJoint(frame, CanonicalJointId.LeftShoulder, out var leftShoulder) ||
                !TryImageJoint(frame, CanonicalJointId.RightShoulder, out var rightShoulder) ||
                !TryImageJoint(frame, CanonicalJointId.LeftHip, out var leftHip) ||
                !TryImageJoint(frame, CanonicalJointId.RightHip, out var rightHip) ||
                !TryImageJoint(frame, CanonicalJointId.Pelvis, out var pelvis) ||
                !TryImageJoint(frame, CanonicalJointId.Chest, out var chest))
            {
                return false;
            }

            var jointsConfidence = Mathf.Min(
                Mathf.Min(leftShoulder.confidence, rightShoulder.confidence),
                Mathf.Min(
                    Mathf.Min(leftHip.confidence, rightHip.confidence),
                    Mathf.Min(pelvis.confidence, chest.confidence)));
            if (jointsConfidence < _settings.minimumJointConfidence)
            {
                return false;
            }

            var shoulderSpan = Vector2.Distance(
                leftShoulder.imagePosition,
                rightShoulder.imagePosition);
            var hipSpan = Vector2.Distance(
                leftHip.imagePosition,
                rightHip.imagePosition);
            var torsoSpan = Vector2.Distance(
                pelvis.imagePosition,
                chest.imagePosition);
            if (shoulderSpan < _settings.minimumImageMeasurement ||
                hipSpan < _settings.minimumImageMeasurement ||
                torsoSpan < _settings.minimumImageMeasurement)
            {
                return false;
            }

            var yawCosine = EstimateYawCosine(frame, profile);
            var widthDivisor = Mathf.Max(_settings.minimumYawCosine, yawCosine);
            var correctedShoulder = shoulderSpan / widthDivisor;
            var correctedHip = hipSpan / widthDivisor;

            // Near side-on poses make width correction noisy. Reliability changes only the weight
            // of the width ratios against their recenter references; it cannot move the zero point.
            var widthReliability = Mathf.InverseLerp(
                _settings.minimumYawCosine,
                0.72f,
                yawCosine);
            widthReliability *= widthReliability;
            var shoulderWeight = 0.32f * widthReliability;
            var hipWeight = 0.32f * widthReliability;
            const float torsoWeight = 0.36f;
            var weightSum = torsoWeight + shoulderWeight + hipWeight;
            var logScale =
                torsoWeight * Mathf.Log(Mathf.Max(_settings.minimumImageMeasurement, torsoSpan)) +
                shoulderWeight * Mathf.Log(Mathf.Max(_settings.minimumImageMeasurement, correctedShoulder)) +
                hipWeight * Mathf.Log(Mathf.Max(_settings.minimumImageMeasurement, correctedHip));
            var apparentScale = Mathf.Exp(logScale / Mathf.Max(0.0001f, weightSum));
            if (!IsFinite(apparentScale) || apparentScale < _settings.minimumImageMeasurement)
            {
                return false;
            }

            var shoulderMid =
                (leftShoulder.imagePosition + rightShoulder.imagePosition) * 0.5f;
            var hipMid =
                (leftHip.imagePosition + rightHip.imagePosition) * 0.5f;
            var centerX = (shoulderMid.x + hipMid.x) * 0.5f;

            measurement = new RootMeasurement
            {
                centerX = centerX,
                correctedShoulder = correctedShoulder,
                correctedHip = correctedHip,
                torso = torsoSpan,
                apparentScale = apparentScale,
                widthReliability = widthReliability,
                confidence = jointsConfidence * Mathf.Lerp(0.65f, 1f, widthReliability),
                yawCosine = yawCosine,
            };
            return IsFinite(centerX);
        }

        private float EstimateYawCosine(
            CanonicalPoseFrame frame,
            MotionCalibrationProfile profile)
        {
            if (!TryPosition(frame.GetJoint(CanonicalJointId.LeftShoulder), out var leftShoulder) ||
                !TryPosition(frame.GetJoint(CanonicalJointId.RightShoulder), out var rightShoulder) ||
                !TryPosition(frame.GetJoint(CanonicalJointId.LeftHip), out var leftHip) ||
                !TryPosition(frame.GetJoint(CanonicalJointId.RightHip), out var rightHip))
            {
                return 1f;
            }

            var liveRight =
                (rightShoulder - leftShoulder) +
                (rightHip - leftHip);
            var up = profile.neutralBodyUp;
            var neutralRight = profile.neutralBodyRight;
            if (!TryNormalize(up, out up) ||
                !TryNormalize(neutralRight, out neutralRight))
            {
                return 1f;
            }

            liveRight -= up * Vector3.Dot(liveRight, up);
            neutralRight -= up * Vector3.Dot(neutralRight, up);
            if (!TryNormalize(liveRight, out liveRight) ||
                !TryNormalize(neutralRight, out neutralRight))
            {
                return 1f;
            }

            return Mathf.Clamp01(
                Mathf.Abs(Vector3.Dot(liveRight, neutralRight)));
        }

        private bool TryImageJoint(
            CanonicalPoseFrame frame,
            CanonicalJointId id,
            out CanonicalPoseJoint joint)
        {
            joint = frame.GetJoint(id);
            return joint.IsTracked &&
                   joint.hasImagePosition &&
                   IsFinite(joint.imagePosition) &&
                   IsFinite(joint.confidence);
        }

        private static bool TryPosition(
            CanonicalPoseJoint joint,
            out Vector3 position)
        {
            if (!joint.IsTracked)
            {
                position = Vector3.zero;
                return false;
            }

            if (joint.hasLocalPosition && IsFinite(joint.localPosition))
            {
                position = joint.localPosition;
                return true;
            }

            if (joint.hasWorldPosition && IsFinite(joint.worldPosition))
            {
                position = joint.worldPosition;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private static bool TryNormalize(
            Vector3 value,
            out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value) || value.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            normalized = value.normalized;
            return IsFinite(normalized);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
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
