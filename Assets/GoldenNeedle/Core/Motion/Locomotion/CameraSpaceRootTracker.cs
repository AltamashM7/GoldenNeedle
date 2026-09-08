using System;
using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    [Serializable]
    public sealed class CameraSpaceRootTrackerSettings
    {
        [Tooltip("Minimum confidence for ankle/heel/toe points used to form each support foot.")]
        [Range(0f, 1f)] public float minimumJointConfidence = 0.40f;

        [Tooltip("Smallest usable image-space measurement for body-scale normalization.")]
        [Min(0.001f)] public float minimumImageMeasurement = 0.005f;

        [Tooltip("Floor used when correcting shoulder/hip width for torso yaw.")]
        [Range(0.15f, 0.70f)] public float minimumYawCosine = 0.30f;

        [Tooltip("Maximum normalized disagreement allowed between left/right foot displacement before support relocation is considered gait/cycling instead of room translation.")]
        [Min(0.01f)] public float supportAgreementTolerance = 0.12f;

        [Tooltip("Minimum support-base depth displacement that requires torso-scale corroboration. Smaller values are treated as stationary depth.")]
        [Min(0f)] public float minimumSupportDepthDisplacement = 0.018f;

        [Tooltip("Minimum matching torso apparent-scale depth evidence required before support Y motion is accepted as room-depth translation.")]
        [Min(0f)] public float minimumDepthScaleEvidence = 0.012f;

        [Tooltip("Response speed for trusted support-base displacement.")]
        [Min(0.1f)] public float positionResponse = 10f;

        [Tooltip("Response speed for trusted support-base velocity.")]
        [Min(0.1f)] public float velocityResponse = 8f;

        [Tooltip("Safety clamp on the normalized camera-depth displacement proxy.")]
        [Min(0.1f)] public float maximumDepthProxy = 1.5f;

        public void Sanitize()
        {
            minimumJointConfidence = Mathf.Clamp01(Safe(minimumJointConfidence, 0.40f));
            minimumImageMeasurement = Mathf.Max(0.001f, Safe(minimumImageMeasurement, 0.005f));
            minimumYawCosine = Mathf.Clamp(Safe(minimumYawCosine, 0.30f), 0.15f, 0.70f);
            supportAgreementTolerance = Mathf.Max(0.01f, Safe(supportAgreementTolerance, 0.12f));
            minimumSupportDepthDisplacement = Mathf.Max(
                0f,
                Safe(minimumSupportDepthDisplacement, 0.018f));
            minimumDepthScaleEvidence = Mathf.Max(
                0f,
                Safe(minimumDepthScaleEvidence, 0.012f));
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
        public Vector2 supportAgreementXZ;
        public bool depthCorroborated;
    }

    /// <summary>
    /// Estimates relative camera-space room displacement from the user's support base, not torso
    /// motion. Each support foot is built from ankle/heel/toe image observations and compared with
    /// that same foot's recenter reference. Both feet must agree before the support base can update.
    /// Torso geometry is retained only for scale normalization/cadence support and as corroborating
    /// evidence for camera-depth relocation.
    /// </summary>
    public sealed class CameraSpaceRootTracker
    {
        private struct SupportFootMeasurement
        {
            public Vector2 imagePosition;
            public float confidence;
        }

        private struct RootMeasurement
        {
            public SupportFootMeasurement leftFoot;
            public SupportFootMeasurement rightFoot;
            public bool hasBodyScale;
            public float apparentScale;
            public float yawCosine;
            public float supportConfidence;
        }

        private readonly CameraSpaceRootTrackerSettings _settings;
        private bool _hasReference;
        private bool _pendingRecenter;
        private bool _hasFilteredDisplacement;
        private bool _hasMeasurement;
        private RootMeasurement _latestMeasurement;
        private RootMeasurement _referenceMeasurement;
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
            Publish(
                true,
                _latestMeasurement,
                Vector2.zero,
                true);
            return true;
        }

        public CameraSpaceRootSample Update(
            CanonicalPoseFrame frame,
            MotionCalibrationProfile profile,
            float deltaTime)
        {
            if (!TryMeasure(frame, profile, out var measurement))
            {
                PublishHolding();
                return LatestSample;
            }

            _latestMeasurement = measurement;
            _hasMeasurement = true;

            if (!_hasReference || _pendingRecenter)
            {
                // Initial origin needs a stable body-scale reference. Later tracking can continue
                // from feet alone even when upper-body scale is temporarily unavailable.
                if (!measurement.hasBodyScale)
                {
                    PublishHolding();
                    return LatestSample;
                }

                CaptureReference(measurement);
                Publish(
                    true,
                    measurement,
                    Vector2.zero,
                    true);
                return LatestSample;
            }

            var referenceScale = Mathf.Max(
                _settings.minimumImageMeasurement,
                _referenceMeasurement.apparentScale);
            var leftDelta = new Vector2(
                measurement.leftFoot.imagePosition.x -
                    _referenceMeasurement.leftFoot.imagePosition.x,
                measurement.leftFoot.imagePosition.y -
                    _referenceMeasurement.leftFoot.imagePosition.y) /
                referenceScale;
            var rightDelta = new Vector2(
                measurement.rightFoot.imagePosition.x -
                    _referenceMeasurement.rightFoot.imagePosition.x,
                measurement.rightFoot.imagePosition.y -
                    _referenceMeasurement.rightFoot.imagePosition.y) /
                referenceScale;

            // Canonical image X already follows the fixed camera/view frame. Canonical image Y is
            // bottom-up; a support base moving farther from the camera appears higher, so +dY is
            // the same sign as camera +Z (away).
            var agreement = new Vector2(
                Mathf.Abs(leftDelta.x - rightDelta.x),
                Mathf.Abs(leftDelta.y - rightDelta.y));
            var lateralConsensus =
                agreement.x <= _settings.supportAgreementTolerance;
            var depthConsensus =
                agreement.y <= _settings.supportAgreementTolerance;

            var targetDisplacement = _filteredDisplacement;
            if (lateralConsensus)
            {
                targetDisplacement.x = (leftDelta.x + rightDelta.x) * 0.5f;
            }

            var supportDepth = (leftDelta.y + rightDelta.y) * 0.5f;
            var depthCorroborated = false;
            if (depthConsensus)
            {
                if (Mathf.Abs(supportDepth) <=
                    _settings.minimumSupportDepthDisplacement)
                {
                    targetDisplacement.y = 0f;
                    depthCorroborated = true;
                }
                else if (measurement.hasBodyScale)
                {
                    var bodyDepthEvidence = -Mathf.Log(
                        Mathf.Max(
                            _settings.minimumImageMeasurement,
                            measurement.apparentScale) /
                        referenceScale);
                    var sameDirection =
                        Mathf.Sign(bodyDepthEvidence) ==
                        Mathf.Sign(supportDepth);
                    if (sameDirection &&
                        Mathf.Abs(bodyDepthEvidence) >=
                        _settings.minimumDepthScaleEvidence)
                    {
                        // Support movement remains the authority. Torso scale only confirms that
                        // the support Y change represents camera-depth relocation rather than lift.
                        targetDisplacement.y = supportDepth;
                        depthCorroborated = true;
                    }
                }
            }

            var supportUpdateTrusted =
                lateralConsensus &&
                depthConsensus &&
                depthCorroborated;
            if (!supportUpdateTrusted)
            {
                PublishHolding(measurement, agreement, depthCorroborated);
                return LatestSample;
            }

            targetDisplacement.y = Mathf.Clamp(
                targetDisplacement.y,
                -_settings.maximumDepthProxy,
                _settings.maximumDepthProxy);
            ApplyFilter(
                targetDisplacement,
                Mathf.Max(0.0001f, deltaTime));

            Publish(
                true,
                measurement,
                agreement,
                depthCorroborated);
            return LatestSample;
        }

        private void CaptureReference(RootMeasurement measurement)
        {
            if (!measurement.hasBodyScale && _hasReference)
            {
                measurement.hasBodyScale = true;
                measurement.apparentScale = _referenceMeasurement.apparentScale;
                measurement.yawCosine = _referenceMeasurement.yawCosine;
            }

            _referenceMeasurement = measurement;
            _filteredDisplacement = Vector2.zero;
            _filteredVelocity = Vector2.zero;
            _hasFilteredDisplacement = true;
            _hasReference = true;
            _pendingRecenter = false;
        }

        private void ApplyFilter(Vector2 targetDisplacement, float deltaTime)
        {
            if (!_hasFilteredDisplacement)
            {
                _filteredDisplacement = targetDisplacement;
                _filteredVelocity = Vector2.zero;
                _hasFilteredDisplacement = true;
                return;
            }

            var previous = _filteredDisplacement;
            var positionAlpha =
                1f - Mathf.Exp(-_settings.positionResponse * deltaTime);
            _filteredDisplacement = Vector2.Lerp(
                previous,
                targetDisplacement,
                positionAlpha);

            var rawVelocity =
                (_filteredDisplacement - previous) / deltaTime;
            var velocityAlpha =
                1f - Mathf.Exp(-_settings.velocityResponse * deltaTime);
            _filteredVelocity = Vector2.Lerp(
                _filteredVelocity,
                rawVelocity,
                velocityAlpha);
        }

        private void PublishHolding()
        {
            LatestSample = new CameraSpaceRootSample
            {
                isValid = false,
                hasOrigin = _hasReference,
                positionXZ = _filteredDisplacement,
                displacementXZ = _filteredDisplacement,
                velocityXZ = Vector2.zero,
                confidence = 0f,
                apparentScale = LatestSample.apparentScale,
                yawCosine = LatestSample.yawCosine,
                supportAgreementXZ = LatestSample.supportAgreementXZ,
                depthCorroborated = false,
            };
        }

        private void PublishHolding(
            RootMeasurement measurement,
            Vector2 agreement,
            bool depthCorroborated)
        {
            LatestSample = new CameraSpaceRootSample
            {
                isValid = false,
                hasOrigin = _hasReference,
                positionXZ = _filteredDisplacement,
                displacementXZ = _filteredDisplacement,
                velocityXZ = Vector2.zero,
                confidence = measurement.supportConfidence,
                apparentScale = measurement.hasBodyScale
                    ? measurement.apparentScale
                    : LatestSample.apparentScale,
                yawCosine = measurement.yawCosine,
                supportAgreementXZ = agreement,
                depthCorroborated = depthCorroborated,
            };
        }

        private void Publish(
            bool valid,
            RootMeasurement measurement,
            Vector2 agreement,
            bool depthCorroborated)
        {
            LatestSample = new CameraSpaceRootSample
            {
                isValid = valid,
                hasOrigin = _hasReference,
                positionXZ = _filteredDisplacement,
                displacementXZ = _filteredDisplacement,
                velocityXZ = _filteredVelocity,
                confidence = Mathf.Clamp01(measurement.supportConfidence),
                apparentScale = measurement.hasBodyScale
                    ? measurement.apparentScale
                    : _referenceMeasurement.apparentScale,
                yawCosine = measurement.yawCosine,
                supportAgreementXZ = agreement,
                depthCorroborated = depthCorroborated,
            };
        }

        private bool TryMeasure(
            CanonicalPoseFrame frame,
            MotionCalibrationProfile profile,
            out RootMeasurement measurement)
        {
            measurement = default;
            if (frame == null ||
                profile == null ||
                !profile.bodyReferenceValid ||
                !TrySupportFoot(
                    frame,
                    CanonicalJointId.LeftAnkle,
                    CanonicalJointId.LeftHeel,
                    CanonicalJointId.LeftToe,
                    out var leftFoot) ||
                !TrySupportFoot(
                    frame,
                    CanonicalJointId.RightAnkle,
                    CanonicalJointId.RightHeel,
                    CanonicalJointId.RightToe,
                    out var rightFoot))
            {
                return false;
            }

            var hasBodyScale = TryMeasureBodyScale(
                frame,
                profile,
                out var apparentScale,
                out var yawCosine);

            measurement = new RootMeasurement
            {
                leftFoot = leftFoot,
                rightFoot = rightFoot,
                hasBodyScale = hasBodyScale,
                apparentScale = apparentScale,
                yawCosine = yawCosine,
                supportConfidence = Mathf.Min(
                    leftFoot.confidence,
                    rightFoot.confidence),
            };
            return true;
        }

        private bool TrySupportFoot(
            CanonicalPoseFrame frame,
            CanonicalJointId ankleId,
            CanonicalJointId heelId,
            CanonicalJointId toeId,
            out SupportFootMeasurement foot)
        {
            foot = default;
            var weightedPosition = Vector2.zero;
            var confidenceSum = 0f;
            var weightSum = 0f;
            var validPoints = 0;

            AddSupportPoint(
                frame.GetJoint(ankleId),
                0.40f,
                ref weightedPosition,
                ref confidenceSum,
                ref weightSum,
                ref validPoints);
            AddSupportPoint(
                frame.GetJoint(heelId),
                0.30f,
                ref weightedPosition,
                ref confidenceSum,
                ref weightSum,
                ref validPoints);
            AddSupportPoint(
                frame.GetJoint(toeId),
                0.30f,
                ref weightedPosition,
                ref confidenceSum,
                ref weightSum,
                ref validPoints);

            if (validPoints < 2 || weightSum <= 0f)
            {
                return false;
            }

            foot = new SupportFootMeasurement
            {
                imagePosition = weightedPosition / weightSum,
                confidence = Mathf.Clamp01(confidenceSum / weightSum),
            };
            return IsFinite(foot.imagePosition);
        }

        private void AddSupportPoint(
            CanonicalPoseJoint joint,
            float weight,
            ref Vector2 weightedPosition,
            ref float confidenceSum,
            ref float weightSum,
            ref int validPoints)
        {
            if (!joint.IsTracked ||
                !joint.hasImagePosition ||
                joint.confidence < _settings.minimumJointConfidence ||
                !IsFinite(joint.imagePosition) ||
                !IsFinite(joint.confidence))
            {
                return;
            }

            weightedPosition += joint.imagePosition * weight;
            confidenceSum += joint.confidence * weight;
            weightSum += weight;
            validPoints++;
        }

        private bool TryMeasureBodyScale(
            CanonicalPoseFrame frame,
            MotionCalibrationProfile profile,
            out float apparentScale,
            out float yawCosine)
        {
            apparentScale = 0f;
            yawCosine = 1f;
            if (!TryImageJoint(
                    frame,
                    CanonicalJointId.LeftShoulder,
                    out var leftShoulder) ||
                !TryImageJoint(
                    frame,
                    CanonicalJointId.RightShoulder,
                    out var rightShoulder) ||
                !TryImageJoint(
                    frame,
                    CanonicalJointId.LeftHip,
                    out var leftHip) ||
                !TryImageJoint(
                    frame,
                    CanonicalJointId.RightHip,
                    out var rightHip) ||
                !TryImageJoint(
                    frame,
                    CanonicalJointId.Pelvis,
                    out var pelvis) ||
                !TryImageJoint(
                    frame,
                    CanonicalJointId.Chest,
                    out var chest))
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

            yawCosine = EstimateYawCosine(frame, profile);
            var widthDivisor =
                Mathf.Max(_settings.minimumYawCosine, yawCosine);
            var correctedShoulder = shoulderSpan / widthDivisor;
            var correctedHip = hipSpan / widthDivisor;

            var widthReliability = Mathf.InverseLerp(
                _settings.minimumYawCosine,
                0.72f,
                yawCosine);
            widthReliability *= widthReliability;
            var shoulderWeight = 0.32f * widthReliability;
            var hipWeight = 0.32f * widthReliability;
            const float torsoWeight = 0.36f;
            var weightSum =
                torsoWeight + shoulderWeight + hipWeight;
            var logScale =
                torsoWeight * Mathf.Log(
                    Mathf.Max(
                        _settings.minimumImageMeasurement,
                        torsoSpan)) +
                shoulderWeight * Mathf.Log(
                    Mathf.Max(
                        _settings.minimumImageMeasurement,
                        correctedShoulder)) +
                hipWeight * Mathf.Log(
                    Mathf.Max(
                        _settings.minimumImageMeasurement,
                        correctedHip));
            apparentScale = Mathf.Exp(
                logScale / Mathf.Max(0.0001f, weightSum));
            return IsFinite(apparentScale) &&
                apparentScale >= _settings.minimumImageMeasurement;
        }

        private float EstimateYawCosine(
            CanonicalPoseFrame frame,
            MotionCalibrationProfile profile)
        {
            if (!TryPosition(
                    frame.GetJoint(CanonicalJointId.LeftShoulder),
                    out var leftShoulder) ||
                !TryPosition(
                    frame.GetJoint(CanonicalJointId.RightShoulder),
                    out var rightShoulder) ||
                !TryPosition(
                    frame.GetJoint(CanonicalJointId.LeftHip),
                    out var leftHip) ||
                !TryPosition(
                    frame.GetJoint(CanonicalJointId.RightHip),
                    out var rightHip))
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
                Mathf.Abs(Vector3.Dot(
                    liveRight,
                    neutralRight)));
        }

        private bool TryImageJoint(
            CanonicalPoseFrame frame,
            CanonicalJointId id,
            out CanonicalPoseJoint joint)
        {
            joint = frame.GetJoint(id);
            return joint.IsTracked &&
                joint.hasImagePosition &&
                joint.confidence >= _settings.minimumJointConfidence &&
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

            if (joint.hasLocalPosition &&
                IsFinite(joint.localPosition))
            {
                position = joint.localPosition;
                return true;
            }

            if (joint.hasWorldPosition &&
                IsFinite(joint.worldPosition))
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
            if (!IsFinite(value) ||
                value.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            normalized = value.normalized;
            return IsFinite(normalized);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) &&
                IsFinite(value.y);
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
