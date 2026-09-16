using System;
using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    [Serializable]
    public sealed class CameraSpaceRootTrackerSettings
    {
        [Tooltip("Minimum confidence for torso/support joints used by physical locomotion.")]
        [Range(0f, 1f)] public float minimumJointConfidence = 0.40f;

        [Tooltip("Smallest usable image-space body measurement for normalization.")]
        [Min(0.001f)] public float minimumImageMeasurement = 0.005f;

        [Tooltip("Floor used when correcting shoulder/hip width for torso yaw.")]
        [Range(0.15f, 0.70f)] public float minimumYawCosine = 0.30f;

        [Tooltip("Normalized foot-height separation required to enter single-foot support validation.")]
        [Min(0.01f)] public float supportSingleFootEnter = 0.12f;

        [Tooltip("Normalized foot-height separation below which support validation returns to both feet.")]
        [Min(0f)] public float supportBothEnter = 0.06f;

        [Tooltip("Differential foot-Y magnitude where gait asymmetry begins reducing camera-depth trust.")]
        [Min(0f)] public float depthDifferentialStart = 0.05f;

        [Tooltip("Differential foot-Y magnitude where gait asymmetry fully suppresses new camera-depth updates.")]
        [Min(0.01f)] public float depthDifferentialFull = 0.20f;

        [Tooltip("Minimum coherent support-base depth relocation required before body-scale depth can commit.")]
        [Min(0f)] public float minimumSupportDepthDisplacement = 0.018f;

        [Tooltip("Minimum matching torso apparent-scale depth evidence required before room-depth translation commits.")]
        [Min(0f)] public float minimumDepthScaleEvidence = 0.012f;

        [Tooltip("Response speed for the one authoritative accepted physical displacement.")]
        [Min(0.1f)] public float positionResponse = 10f;

        [Tooltip("Response speed for authoritative physical velocity.")]
        [Min(0.1f)] public float velocityResponse = 8f;

        [Tooltip("Safety clamp on the normalized camera-depth displacement proxy.")]
        [Min(0.1f)] public float maximumDepthProxy = 1.5f;

        public void Sanitize()
        {
            minimumJointConfidence = Mathf.Clamp01(Safe(minimumJointConfidence, 0.40f));
            minimumImageMeasurement = Mathf.Max(0.001f, Safe(minimumImageMeasurement, 0.005f));
            minimumYawCosine = Mathf.Clamp(Safe(minimumYawCosine, 0.30f), 0.15f, 0.70f);
            supportSingleFootEnter = Mathf.Max(0.01f, Safe(supportSingleFootEnter, 0.12f));
            supportBothEnter = Mathf.Clamp(
                Safe(supportBothEnter, 0.06f),
                0f,
                Mathf.Max(0f, supportSingleFootEnter - 0.005f));
            depthDifferentialStart = Mathf.Max(0f, Safe(depthDifferentialStart, 0.05f));
            depthDifferentialFull = Mathf.Max(
                depthDifferentialStart + 0.01f,
                Safe(depthDifferentialFull, 0.20f));
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

    public enum PhysicalSupportMode
    {
        Unavailable,
        Both,
        Left,
        Right,
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
        public Vector2 supportCommonXZ;
        public Vector2 supportDifferentialXZ;
        public float depthReliability;
        public bool depthCorroborated;
        public Vector2 bodyCandidateXZ;
        public Vector2 supportEvidenceXZ;
        public PhysicalSupportMode supportMode;
        public bool supportValidated;
        public bool movementAccepted;
    }

    /// <summary>
    /// One authoritative physical-position tracker. Torso placement/apparent scale provide the
    /// continuous camera-space body candidate, while support feet only validate whether candidate
    /// motion is genuine room relocation instead of lean/sway/swing articulation. The tracker owns
    /// accepted displacement, filtering, recenter and tracking-loss continuity. Downstream fusion
    /// maps this already-authoritative result and does not maintain a second position state machine.
    /// </summary>
    public sealed class CameraSpaceRootTracker
    {
        // Reuse the long-standing pre-Foundation physical deadzone as the minimum accumulated
        // lateral evidence before a new body position can commit. It is measured from the last
        // accepted state, so slow deliberate motion accumulates instead of being permanently lost.
        private const float HistoricalLateralCommitDelta = 0.012f;

        private struct SupportFootMeasurement
        {
            public Vector2 imagePosition;
            public float confidence;
        }

        private struct RootMeasurement
        {
            public float centerX;
            public float apparentScale;
            public float yawCosine;
            public float bodyConfidence;
            public SupportFootMeasurement leftFoot;
            public SupportFootMeasurement rightFoot;
            public float supportConfidence;
        }

        private readonly CameraSpaceRootTrackerSettings _settings;
        private bool _hasReference;
        private bool _pendingRecenter;
        private bool _hasMeasurement;
        private bool _hasFilteredDisplacement;
        private bool _needsMeasurementRebase;
        private bool _hasSupportMode;
        private PhysicalSupportMode _supportMode;
        private RootMeasurement _latestMeasurement;
        private RootMeasurement _referenceMeasurement;
        private float _referenceXProxy;
        private Vector2 _bodyRebaseOffset;
        private Vector2 _supportRebaseOffset;
        private Vector2 _acceptedBodyDisplacement;
        private Vector2 _acceptedSupportDisplacement;
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
            _hasMeasurement = false;
            _hasFilteredDisplacement = false;
            _needsMeasurementRebase = false;
            _hasSupportMode = false;
            _supportMode = PhysicalSupportMode.Unavailable;
            _latestMeasurement = default;
            _referenceMeasurement = default;
            _referenceXProxy = 0f;
            _bodyRebaseOffset = Vector2.zero;
            _supportRebaseOffset = Vector2.zero;
            _acceptedBodyDisplacement = Vector2.zero;
            _acceptedSupportDisplacement = Vector2.zero;
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
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                PhysicalSupportMode.Both,
                true,
                false,
                1f,
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
                CaptureReference(measurement);
                Publish(
                    true,
                    measurement,
                    Vector2.zero,
                    Vector2.zero,
                    Vector2.zero,
                    Vector2.zero,
                    PhysicalSupportMode.Both,
                    true,
                    false,
                    1f,
                    true);
                return LatestSample;
            }

            var referenceScale = Mathf.Max(
                _settings.minimumImageMeasurement,
                _referenceMeasurement.apparentScale);
            var bodyDepth = Mathf.Clamp(
                -Mathf.Log(
                    Mathf.Max(_settings.minimumImageMeasurement, measurement.apparentScale) /
                    referenceScale),
                -_settings.maximumDepthProxy,
                _settings.maximumDepthProxy);
            var effectiveScale = Mathf.Max(
                _settings.minimumImageMeasurement,
                referenceScale * Mathf.Exp(-bodyDepth));
            var xProxy = (measurement.centerX - 0.5f) / effectiveScale;
            var rawBodyCandidate = new Vector2(
                xProxy - _referenceXProxy,
                bodyDepth);

            var rawLeftSupport = new Vector2(
                measurement.leftFoot.imagePosition.x - _referenceMeasurement.leftFoot.imagePosition.x,
                measurement.leftFoot.imagePosition.y - _referenceMeasurement.leftFoot.imagePosition.y) /
                referenceScale;
            var rawRightSupport = new Vector2(
                measurement.rightFoot.imagePosition.x - _referenceMeasurement.rightFoot.imagePosition.x,
                measurement.rightFoot.imagePosition.y - _referenceMeasurement.rightFoot.imagePosition.y) /
                referenceScale;

            if (_needsMeasurementRebase)
            {
                var rawCommon = (rawLeftSupport + rawRightSupport) * 0.5f;
                _bodyRebaseOffset = _acceptedBodyDisplacement - rawBodyCandidate;
                _supportRebaseOffset = _acceptedSupportDisplacement - rawCommon;
                _needsMeasurementRebase = false;
                _hasSupportMode = false;
            }

            var bodyCandidate = rawBodyCandidate + _bodyRebaseOffset;
            var leftSupport = rawLeftSupport + _supportRebaseOffset;
            var rightSupport = rawRightSupport + _supportRebaseOffset;
            var commonSupport = (leftSupport + rightSupport) * 0.5f;
            var differentialSupport = (leftSupport - rightSupport) * 0.5f;
            var supportMode = ResolveSupportMode(measurement, referenceScale);
            var supportValidated = supportMode == PhysicalSupportMode.Both &&
                SupportDisplacementsAgree(leftSupport, rightSupport);

            var acceptedThisFrame = false;
            var depthCorroborated = false;
            var depthReliability = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    _settings.depthDifferentialStart,
                    _settings.depthDifferentialFull,
                    Mathf.Abs(differentialSupport.y)));

            if (supportValidated)
            {
                var bodyDelta = bodyCandidate - _acceptedBodyDisplacement;
                var supportDelta = commonSupport - _acceptedSupportDisplacement;

                if (AxisMotionAgrees(
                        bodyDelta.x,
                        supportDelta.x,
                        HistoricalLateralCommitDelta,
                        HistoricalLateralCommitDelta))
                {
                    _acceptedBodyDisplacement.x = bodyCandidate.x;
                    _acceptedSupportDisplacement.x = commonSupport.x;
                    acceptedThisFrame = true;
                }

                if (depthReliability > 0.001f &&
                    AxisMotionAgrees(
                        bodyDelta.y,
                        supportDelta.y,
                        _settings.minimumDepthScaleEvidence,
                        _settings.minimumSupportDepthDisplacement))
                {
                    _acceptedBodyDisplacement.y = Mathf.Clamp(
                        bodyCandidate.y,
                        -_settings.maximumDepthProxy,
                        _settings.maximumDepthProxy);
                    _acceptedSupportDisplacement.y = commonSupport.y;
                    depthCorroborated = true;
                    acceptedThisFrame = true;
                }
            }

            ApplyFilter(
                _acceptedBodyDisplacement,
                Mathf.Max(0.0001f, deltaTime));

            Publish(
                true,
                measurement,
                commonSupport,
                differentialSupport,
                bodyCandidate,
                commonSupport,
                supportMode,
                supportValidated,
                acceptedThisFrame,
                depthReliability,
                depthCorroborated);
            return LatestSample;
        }

        private void CaptureReference(RootMeasurement measurement)
        {
            _referenceMeasurement = measurement;
            _referenceXProxy =
                (measurement.centerX - 0.5f) /
                Mathf.Max(_settings.minimumImageMeasurement, measurement.apparentScale);
            _bodyRebaseOffset = Vector2.zero;
            _supportRebaseOffset = Vector2.zero;
            _acceptedBodyDisplacement = Vector2.zero;
            _acceptedSupportDisplacement = Vector2.zero;
            _filteredDisplacement = Vector2.zero;
            _filteredVelocity = Vector2.zero;
            _hasFilteredDisplacement = true;
            _hasReference = true;
            _pendingRecenter = false;
            _needsMeasurementRebase = false;
            _supportMode = PhysicalSupportMode.Both;
            _hasSupportMode = true;
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
            var positionAlpha = 1f - Mathf.Exp(-_settings.positionResponse * deltaTime);
            _filteredDisplacement = Vector2.Lerp(previous, targetDisplacement, positionAlpha);

            var rawVelocity = (_filteredDisplacement - previous) / deltaTime;
            var velocityAlpha = 1f - Mathf.Exp(-_settings.velocityResponse * deltaTime);
            _filteredVelocity = Vector2.Lerp(_filteredVelocity, rawVelocity, velocityAlpha);
            if ((_filteredDisplacement - targetDisplacement).sqrMagnitude < 0.00000001f)
            {
                _filteredDisplacement = targetDisplacement;
            }
            if (_filteredVelocity.sqrMagnitude < 0.00000001f &&
                (_filteredDisplacement - targetDisplacement).sqrMagnitude < 0.00000001f)
            {
                _filteredVelocity = Vector2.zero;
            }
        }

        private void PublishHolding()
        {
            if (_hasReference)
            {
                _needsMeasurementRebase = true;
                _hasSupportMode = false;
            }

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
                supportCommonXZ = LatestSample.supportCommonXZ,
                supportDifferentialXZ = LatestSample.supportDifferentialXZ,
                depthReliability = 0f,
                depthCorroborated = false,
                bodyCandidateXZ = LatestSample.bodyCandidateXZ,
                supportEvidenceXZ = LatestSample.supportEvidenceXZ,
                supportMode = PhysicalSupportMode.Unavailable,
                supportValidated = false,
                movementAccepted = false,
            };
        }

        private void Publish(
            bool valid,
            RootMeasurement measurement,
            Vector2 commonSupport,
            Vector2 differentialSupport,
            Vector2 bodyCandidate,
            Vector2 supportEvidence,
            PhysicalSupportMode supportMode,
            bool supportValidated,
            bool movementAccepted,
            float depthReliability,
            bool depthCorroborated)
        {
            LatestSample = new CameraSpaceRootSample
            {
                isValid = valid,
                hasOrigin = _hasReference,
                positionXZ = _filteredDisplacement,
                displacementXZ = _filteredDisplacement,
                velocityXZ = _filteredVelocity,
                confidence = Mathf.Clamp01(Mathf.Min(
                    measurement.bodyConfidence,
                    measurement.supportConfidence)),
                apparentScale = measurement.apparentScale,
                yawCosine = measurement.yawCosine,
                supportCommonXZ = commonSupport,
                supportDifferentialXZ = differentialSupport,
                depthReliability = Mathf.Clamp01(depthReliability),
                depthCorroborated = depthCorroborated,
                bodyCandidateXZ = bodyCandidate,
                supportEvidenceXZ = supportEvidence,
                supportMode = supportMode,
                supportValidated = supportValidated,
                movementAccepted = movementAccepted,
            };
        }

        private bool TryMeasure(
            CanonicalPoseFrame frame,
            MotionCalibrationProfile profile,
            out RootMeasurement measurement)
        {
            measurement = default;
            if (frame == null || profile == null || !profile.bodyReferenceValid ||
                !TryMeasureBody(frame, profile, out var centerX, out var apparentScale,
                    out var yawCosine, out var bodyConfidence) ||
                !TrySupportFoot(frame, CanonicalJointId.LeftAnkle, CanonicalJointId.LeftHeel,
                    CanonicalJointId.LeftToe, out var leftFoot) ||
                !TrySupportFoot(frame, CanonicalJointId.RightAnkle, CanonicalJointId.RightHeel,
                    CanonicalJointId.RightToe, out var rightFoot))
            {
                return false;
            }

            measurement = new RootMeasurement
            {
                centerX = centerX,
                apparentScale = apparentScale,
                yawCosine = yawCosine,
                bodyConfidence = bodyConfidence,
                leftFoot = leftFoot,
                rightFoot = rightFoot,
                supportConfidence = Mathf.Min(leftFoot.confidence, rightFoot.confidence),
            };
            return true;
        }

        private bool TryMeasureBody(
            CanonicalPoseFrame frame,
            MotionCalibrationProfile profile,
            out float centerX,
            out float apparentScale,
            out float yawCosine,
            out float confidence)
        {
            centerX = 0f;
            apparentScale = 0f;
            yawCosine = 1f;
            confidence = 0f;
            if (!TryImageJoint(frame, CanonicalJointId.LeftShoulder, out var leftShoulder) ||
                !TryImageJoint(frame, CanonicalJointId.RightShoulder, out var rightShoulder) ||
                !TryImageJoint(frame, CanonicalJointId.LeftHip, out var leftHip) ||
                !TryImageJoint(frame, CanonicalJointId.RightHip, out var rightHip) ||
                !TryImageJoint(frame, CanonicalJointId.Pelvis, out var pelvis) ||
                !TryImageJoint(frame, CanonicalJointId.Chest, out var chest))
            {
                return false;
            }

            var shoulderSpan = Vector2.Distance(leftShoulder.imagePosition, rightShoulder.imagePosition);
            var hipSpan = Vector2.Distance(leftHip.imagePosition, rightHip.imagePosition);
            var torsoSpan = Vector2.Distance(pelvis.imagePosition, chest.imagePosition);
            if (shoulderSpan < _settings.minimumImageMeasurement ||
                hipSpan < _settings.minimumImageMeasurement ||
                torsoSpan < _settings.minimumImageMeasurement)
            {
                return false;
            }

            yawCosine = EstimateYawCosine(frame, profile);
            var widthDivisor = Mathf.Max(_settings.minimumYawCosine, yawCosine);
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
            var weightSum = torsoWeight + shoulderWeight + hipWeight;
            var logScale =
                torsoWeight * Mathf.Log(Mathf.Max(_settings.minimumImageMeasurement, torsoSpan)) +
                shoulderWeight * Mathf.Log(Mathf.Max(_settings.minimumImageMeasurement, correctedShoulder)) +
                hipWeight * Mathf.Log(Mathf.Max(_settings.minimumImageMeasurement, correctedHip));
            apparentScale = Mathf.Exp(logScale / Mathf.Max(0.0001f, weightSum));
            if (!IsFinite(apparentScale) || apparentScale < _settings.minimumImageMeasurement)
            {
                return false;
            }

            var shoulderMid = (leftShoulder.imagePosition + rightShoulder.imagePosition) * 0.5f;
            var hipMid = (leftHip.imagePosition + rightHip.imagePosition) * 0.5f;
            centerX = ((shoulderMid.x + hipMid.x) * 0.5f);
            confidence = Mathf.Min(
                Mathf.Min(leftShoulder.confidence, rightShoulder.confidence),
                Mathf.Min(
                    Mathf.Min(leftHip.confidence, rightHip.confidence),
                    Mathf.Min(pelvis.confidence, chest.confidence)));
            return IsFinite(centerX) && IsFinite(confidence);
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
            AddSupportPoint(frame.GetJoint(ankleId), 0.40f, ref weightedPosition,
                ref confidenceSum, ref weightSum, ref validPoints);
            AddSupportPoint(frame.GetJoint(heelId), 0.30f, ref weightedPosition,
                ref confidenceSum, ref weightSum, ref validPoints);
            AddSupportPoint(frame.GetJoint(toeId), 0.30f, ref weightedPosition,
                ref confidenceSum, ref weightSum, ref validPoints);

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
            if (!joint.IsTracked || !joint.hasImagePosition ||
                joint.confidence < _settings.minimumJointConfidence ||
                !IsFinite(joint.imagePosition) || !IsFinite(joint.confidence))
            {
                return;
            }

            weightedPosition += joint.imagePosition * weight;
            confidenceSum += joint.confidence * weight;
            weightSum += weight;
            validPoints++;
        }

        private PhysicalSupportMode ResolveSupportMode(
            RootMeasurement measurement,
            float referenceScale)
        {
            var signedHeightDifference =
                (measurement.leftFoot.imagePosition.y - measurement.rightFoot.imagePosition.y) /
                Mathf.Max(_settings.minimumImageMeasurement, measurement.apparentScale > 0f
                    ? measurement.apparentScale
                    : referenceScale);

            if (!_hasSupportMode)
            {
                _supportMode = InitialSupportMode(signedHeightDifference);
                _hasSupportMode = true;
                return _supportMode;
            }

            switch (_supportMode)
            {
                case PhysicalSupportMode.Left:
                    if (signedHeightDifference >= _settings.supportSingleFootEnter)
                        _supportMode = PhysicalSupportMode.Right;
                    else if (Mathf.Abs(signedHeightDifference) <= _settings.supportBothEnter)
                        _supportMode = PhysicalSupportMode.Both;
                    break;
                case PhysicalSupportMode.Right:
                    if (signedHeightDifference <= -_settings.supportSingleFootEnter)
                        _supportMode = PhysicalSupportMode.Left;
                    else if (Mathf.Abs(signedHeightDifference) <= _settings.supportBothEnter)
                        _supportMode = PhysicalSupportMode.Both;
                    break;
                default:
                    _supportMode = InitialSupportMode(signedHeightDifference);
                    break;
            }

            return _supportMode;
        }

        private PhysicalSupportMode InitialSupportMode(float signedHeightDifference)
        {
            if (signedHeightDifference >= _settings.supportSingleFootEnter)
                return PhysicalSupportMode.Right;
            if (signedHeightDifference <= -_settings.supportSingleFootEnter)
                return PhysicalSupportMode.Left;
            return PhysicalSupportMode.Both;
        }

        private bool SupportDisplacementsAgree(Vector2 left, Vector2 right)
        {
            var tolerance = Mathf.Max(0.01f, _settings.supportBothEnter);
            return Mathf.Abs(left.x - right.x) <= tolerance &&
                Mathf.Abs(left.y - right.y) <= tolerance;
        }

        private static bool AxisMotionAgrees(
            float bodyDelta,
            float supportDelta,
            float bodyThreshold,
            float supportThreshold)
        {
            return Mathf.Abs(bodyDelta) >= Mathf.Max(0f, bodyThreshold) &&
                Mathf.Abs(supportDelta) >= Mathf.Max(0f, supportThreshold) &&
                Mathf.Sign(bodyDelta) == Mathf.Sign(supportDelta);
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

            var liveRight = (rightShoulder - leftShoulder) + (rightHip - leftHip);
            var up = profile.neutralBodyUp;
            var neutralRight = profile.neutralBodyRight;
            if (!TryNormalize(up, out up) || !TryNormalize(neutralRight, out neutralRight))
            {
                return 1f;
            }

            liveRight -= up * Vector3.Dot(liveRight, up);
            neutralRight -= up * Vector3.Dot(neutralRight, up);
            if (!TryNormalize(liveRight, out liveRight) || !TryNormalize(neutralRight, out neutralRight))
            {
                return 1f;
            }

            return Mathf.Clamp01(Mathf.Abs(Vector3.Dot(liveRight, neutralRight)));
        }

        private bool TryImageJoint(
            CanonicalPoseFrame frame,
            CanonicalJointId id,
            out CanonicalPoseJoint joint)
        {
            joint = frame.GetJoint(id);
            return joint.IsTracked && joint.hasImagePosition &&
                joint.confidence >= _settings.minimumJointConfidence &&
                IsFinite(joint.imagePosition) && IsFinite(joint.confidence);
        }

        private static bool TryPosition(CanonicalPoseJoint joint, out Vector3 position)
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

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value) || value.sqrMagnitude <= 0.000001f)
                return false;
            normalized = value.normalized;
            return IsFinite(normalized);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
