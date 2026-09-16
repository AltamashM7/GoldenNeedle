using System;
using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    [Serializable]
    public sealed class VerticalLocomotionSettings
    {
        [Tooltip("Minimum confidence required for the feet, knees, hips, pelvis and chest used by vertical locomotion.")]
        [Range(0f, 1f)] public float minimumJointConfidence = 0.40f;

        [Tooltip("Normalized coherent whole-body rise required to acquire a jump.")]
        [InspectorName("Jump Detection Threshold")]
        [Min(0.01f)] public float jumpEnterThreshold = 0.12f;

        [Tooltip("Normalized whole-body rise below which an acquired jump begins landing.")]
        [InspectorName("Jump Release Threshold")]
        [Min(0f)] public float jumpReleaseThreshold = 0.045f;

        [Tooltip("World-space meters applied per normalized measured jump amount.")]
        [InspectorName("Jump Height / World Scale")]
        [Min(0f)] public float jumpWorldScale = 1.60f;

        [Tooltip("Maximum upward avatar-root offset produced by physical jumping.")]
        [InspectorName("Maximum Jump Height")]
        [Min(0f)] public float maximumJumpHeight = 0.90f;

        [Tooltip("Maximum normalized left/right support-height mismatch allowed for coherent jump acquisition.")]
        [InspectorName("Maximum Jump Foot Asymmetry")]
        [Min(0.01f)] public float maximumJumpFootAsymmetry = 0.08f;

        [Tooltip("Normalized pelvis-to-support compression required to acquire semantic crouch.")]
        [InspectorName("Crouch Enter Threshold")]
        [Min(0.01f)] public float crouchEnterThreshold = 0.18f;

        [Tooltip("Normalized pelvis-to-support compression below which semantic crouch releases.")]
        [InspectorName("Crouch Release Threshold")]
        [Min(0f)] public float crouchReleaseThreshold = 0.09f;

        [Tooltip("World-space meters applied per normalized grounded body-compression amount.")]
        [InspectorName("Crouch Depth / World Scale")]
        [Min(0f)] public float crouchWorldScale = 1.20f;

        [Tooltip("Maximum downward avatar-root offset produced by grounded body compression.")]
        [InspectorName("Maximum Crouch Depth")]
        [Min(0f)] public float maximumCrouchDepth = 0.65f;

        [Tooltip("Response speed for the tracked avatar-root Y offset. Higher values react faster.")]
        [InspectorName("Vertical Response")]
        [Min(0.1f)] public float verticalResponse = 18f;

        [Tooltip("How long an acquired vertical state may hold through missing required landmarks before returning safely toward neutral.")]
        [InspectorName("Vertical Tracking Grace")]
        [Min(0f)] public float trackingGraceSeconds = 0.16f;

        [Tooltip("Maximum logarithmic apparent-body-scale change accepted while acquiring a vertical action.")]
        [Min(0.01f)] public float maximumApparentScaleChange = 0.12f;

        [Tooltip("Maximum normalized spread between left foot, right foot, pelvis and chest rise during jump acquisition.")]
        [Min(0.01f)] public float maximumJumpCoherenceSpread = 0.10f;

        [Tooltip("Maximum normalized support-base movement considered grounded for bend/crouch and jump landing.")]
        [Min(0.01f)] public float groundedSupportTolerance = 0.06f;

        public void Sanitize()
        {
            minimumJointConfidence = Mathf.Clamp01(Safe(minimumJointConfidence, 0.40f));
            jumpEnterThreshold = Mathf.Max(0.01f, Safe(jumpEnterThreshold, 0.12f));
            jumpReleaseThreshold = Mathf.Clamp(
                Safe(jumpReleaseThreshold, 0.045f),
                0f,
                Mathf.Max(0f, jumpEnterThreshold - 0.005f));
            jumpWorldScale = Mathf.Max(0f, Safe(jumpWorldScale, 1.60f));
            maximumJumpHeight = Mathf.Max(0f, Safe(maximumJumpHeight, 0.90f));
            maximumJumpFootAsymmetry = Mathf.Max(
                0.01f,
                Safe(maximumJumpFootAsymmetry, 0.08f));
            crouchEnterThreshold = Mathf.Max(0.01f, Safe(crouchEnterThreshold, 0.18f));
            crouchReleaseThreshold = Mathf.Clamp(
                Safe(crouchReleaseThreshold, 0.09f),
                0f,
                Mathf.Max(0f, crouchEnterThreshold - 0.005f));
            crouchWorldScale = Mathf.Max(0f, Safe(crouchWorldScale, 1.20f));
            maximumCrouchDepth = Mathf.Max(0f, Safe(maximumCrouchDepth, 0.65f));
            verticalResponse = Mathf.Max(0.1f, Safe(verticalResponse, 18f));
            trackingGraceSeconds = Mathf.Max(0f, Safe(trackingGraceSeconds, 0.16f));
            maximumApparentScaleChange = Mathf.Max(
                0.01f,
                Safe(maximumApparentScaleChange, 0.12f));
            maximumJumpCoherenceSpread = Mathf.Max(
                0.01f,
                Safe(maximumJumpCoherenceSpread, 0.10f));
            groundedSupportTolerance = Mathf.Max(
                0.01f,
                Safe(groundedSupportTolerance, 0.06f));
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    public enum VerticalLocomotionState
    {
        Unavailable,
        Standing,
        Jump,
        Crouch,
    }

    public enum VerticalJumpPhase
    {
        Grounded,
        Takeoff,
        Airborne,
        Landing,
    }

    public struct VerticalLocomotionSample
    {
        public bool isAvailable;
        public bool referenceReady;
        public VerticalLocomotionState state;
        public VerticalJumpPhase jumpPhase;
        public float jumpSignal;
        public float crouchCompression;
        public float supportRise;
        public float footAsymmetry;
        public float apparentScaleChange;
        public float worldOffsetY;
        public bool groundedBendActive;
        public bool suppressPhysicalDepth;

        public bool IsJumpActive => state == VerticalLocomotionState.Jump;
        public bool IsCrouchActive => state == VerticalLocomotionState.Crouch;
    }

    /// <summary>
    /// Interprets stabilized canonical image geometry into vertical locomotion only. Semantic Jump
    /// and Crouch remain one explicit state machine. Independently, trustworthy grounded
    /// pelvis-to-support compression continuously drives negative root Y, so a shallow bend lowers
    /// the body before semantic Crouch acquisition. Jump remains the exclusive positive-Y owner.
    /// </summary>
    public sealed class VerticalLocomotionInterpreter
    {
        private struct SupportFootMeasurement
        {
            public float y;
            public float confidence;
        }

        private struct Measurement
        {
            public SupportFootMeasurement leftFoot;
            public SupportFootMeasurement rightFoot;
            public float pelvisY;
            public float chestY;
            public float leftKneeY;
            public float rightKneeY;
            public float apparentScale;
            public float confidence;

            public float SupportY => (leftFoot.y + rightFoot.y) * 0.5f;
            public float PelvisSupportHeight => pelvisY - SupportY;
            public float TorsoHeight => chestY - pelvisY;
        }

        private readonly VerticalLocomotionSettings _settings;
        private bool _hasReference;
        private Measurement _reference;
        private VerticalLocomotionState _state;
        private VerticalJumpPhase _jumpPhase;
        private float _missingSeconds;
        private float _filteredOffsetY;
        private bool _hasFilteredOffset;

        public VerticalLocomotionInterpreter(VerticalLocomotionSettings settings)
        {
            _settings = settings ?? new VerticalLocomotionSettings();
            _settings.Sanitize();
            Reset();
        }

        public VerticalLocomotionSample LatestSample { get; private set; }
        public bool HasReference => _hasReference;

        public void Reset()
        {
            _hasReference = false;
            _reference = default;
            _state = VerticalLocomotionState.Unavailable;
            _jumpPhase = VerticalJumpPhase.Grounded;
            _missingSeconds = 0f;
            _filteredOffsetY = 0f;
            _hasFilteredOffset = true;
            LatestSample = new VerticalLocomotionSample
            {
                state = VerticalLocomotionState.Unavailable,
                jumpPhase = VerticalJumpPhase.Grounded,
            };
        }

        public VerticalLocomotionSample Update(
            CanonicalPoseFrame frame,
            MotionCalibrationProfile profile,
            float deltaTime)
        {
            var dt = Mathf.Max(0.0001f, deltaTime);
            if (profile == null || !profile.bodyReferenceValid)
            {
                Reset();
                return LatestSample;
            }

            if (!TryMeasure(frame, out var measurement))
            {
                return HandleUnavailable(dt);
            }

            _missingSeconds = 0f;
            if (!_hasReference)
            {
                if (!CanCaptureStandingReference(measurement))
                {
                    ApplyOffsetFilter(0f, dt);
                    return Publish(false, false, VerticalLocomotionState.Unavailable,
                        VerticalJumpPhase.Grounded, 0f, 0f, 0f, 0f, 0f, false);
                }

                CaptureReference(measurement);
                return Publish(true, true, VerticalLocomotionState.Standing,
                    VerticalJumpPhase.Grounded, 0f, 0f, 0f, 0f, 0f, false);
            }

            var referenceHeight = Mathf.Max(0.0001f, _reference.PelvisSupportHeight);
            var leftRise = (measurement.leftFoot.y - _reference.leftFoot.y) / referenceHeight;
            var rightRise = (measurement.rightFoot.y - _reference.rightFoot.y) / referenceHeight;
            var supportRise = (measurement.SupportY - _reference.SupportY) / referenceHeight;
            var pelvisRise = (measurement.pelvisY - _reference.pelvisY) / referenceHeight;
            var chestRise = (measurement.chestY - _reference.chestY) / referenceHeight;
            var footAsymmetry = Mathf.Abs(leftRise - rightRise);
            var jumpSignal = Min4(leftRise, rightRise, pelvisRise, chestRise);
            var jumpSpread = Max4(leftRise, rightRise, pelvisRise, chestRise) - jumpSignal;
            var crouchCompression = 1f - measurement.PelvisSupportHeight / referenceHeight;
            var scaleChange = Mathf.Abs(Mathf.Log(
                Mathf.Max(0.0001f, measurement.apparentScale) /
                Mathf.Max(0.0001f, _reference.apparentScale)));

            var scaleStable = scaleChange <= _settings.maximumApparentScaleChange;
            var feetCoherent = footAsymmetry <= _settings.maximumJumpFootAsymmetry;
            var supportGrounded = Mathf.Abs(supportRise) <= _settings.groundedSupportTolerance;
            var jumpCandidate = scaleStable && feetCoherent &&
                jumpSignal >= _settings.jumpEnterThreshold &&
                jumpSpread <= _settings.maximumJumpCoherenceSpread;
            var crouchCandidate = scaleStable && feetCoherent && supportGrounded &&
                crouchCompression >= _settings.crouchEnterThreshold &&
                pelvisRise <= -_settings.crouchEnterThreshold * 0.50f &&
                chestRise <= -_settings.crouchEnterThreshold * 0.25f;

            if (_state == VerticalLocomotionState.Unavailable)
            {
                _state = VerticalLocomotionState.Standing;
                _jumpPhase = VerticalJumpPhase.Grounded;
            }

            var jumpTarget = 0f;
            switch (_state)
            {
                case VerticalLocomotionState.Jump:
                    if (_jumpPhase == VerticalJumpPhase.Landing)
                    {
                        if (jumpCandidate)
                        {
                            _jumpPhase = VerticalJumpPhase.Takeoff;
                            jumpTarget = JumpOffset(jumpSignal);
                        }
                        else
                        {
                            _state = VerticalLocomotionState.Standing;
                            _jumpPhase = VerticalJumpPhase.Grounded;
                        }
                    }
                    else if (scaleStable &&
                        footAsymmetry <= _settings.maximumJumpFootAsymmetry * 1.50f &&
                        jumpSignal >= _settings.jumpReleaseThreshold)
                    {
                        _jumpPhase = VerticalJumpPhase.Airborne;
                        jumpTarget = JumpOffset(jumpSignal);
                    }
                    else
                    {
                        _jumpPhase = VerticalJumpPhase.Landing;
                    }
                    break;

                case VerticalLocomotionState.Crouch:
                    if (!(scaleStable && feetCoherent && supportGrounded &&
                        crouchCompression >= _settings.crouchReleaseThreshold))
                    {
                        _state = VerticalLocomotionState.Standing;
                        _jumpPhase = VerticalJumpPhase.Grounded;
                    }
                    break;

                default:
                    if (jumpCandidate)
                    {
                        _state = VerticalLocomotionState.Jump;
                        _jumpPhase = VerticalJumpPhase.Takeoff;
                        jumpTarget = JumpOffset(jumpSignal);
                    }
                    else if (crouchCandidate)
                    {
                        _state = VerticalLocomotionState.Crouch;
                        _jumpPhase = VerticalJumpPhase.Grounded;
                    }
                    else
                    {
                        _state = VerticalLocomotionState.Standing;
                        _jumpPhase = VerticalJumpPhase.Grounded;
                    }
                    break;
            }

            var groundedBendSafe = _state != VerticalLocomotionState.Jump &&
                scaleStable && feetCoherent && supportGrounded;
            var bendTarget = groundedBendSafe
                ? GroundedCompressionOffset(crouchCompression)
                : 0f;
            var groundedBendActive = bendTarget < -0.00001f;
            var targetOffsetY = _state == VerticalLocomotionState.Jump
                ? jumpTarget
                : bendTarget;

            ApplyOffsetFilter(targetOffsetY, dt);
            return Publish(true, true, _state, _jumpPhase, jumpSignal,
                crouchCompression, supportRise, footAsymmetry, scaleChange,
                groundedBendActive);
        }

        private VerticalLocomotionSample HandleUnavailable(float deltaTime)
        {
            _missingSeconds += deltaTime;
            if (_hasReference && _missingSeconds <= _settings.trackingGraceSeconds)
            {
                var held = LatestSample;
                held.isAvailable = false;
                held.referenceReady = true;
                held.worldOffsetY = _filteredOffsetY;
                held.suppressPhysicalDepth = held.state == VerticalLocomotionState.Jump;
                LatestSample = held;
                return LatestSample;
            }

            _state = VerticalLocomotionState.Unavailable;
            _jumpPhase = VerticalJumpPhase.Grounded;
            ApplyOffsetFilter(0f, deltaTime);
            return Publish(false, _hasReference, VerticalLocomotionState.Unavailable,
                VerticalJumpPhase.Grounded, LatestSample.jumpSignal,
                LatestSample.crouchCompression, LatestSample.supportRise,
                LatestSample.footAsymmetry, LatestSample.apparentScaleChange, false);
        }

        private void CaptureReference(Measurement measurement)
        {
            _reference = measurement;
            _hasReference = true;
            _state = VerticalLocomotionState.Standing;
            _jumpPhase = VerticalJumpPhase.Grounded;
            _missingSeconds = 0f;
            _filteredOffsetY = 0f;
            _hasFilteredOffset = true;
        }

        private bool CanCaptureStandingReference(Measurement measurement)
        {
            var legHeight = measurement.PelvisSupportHeight;
            var torsoHeight = measurement.TorsoHeight;
            if (legHeight <= 0.05f || torsoHeight <= 0.04f)
            {
                return false;
            }

            var footAsymmetry = Mathf.Abs(
                measurement.leftFoot.y - measurement.rightFoot.y) / legHeight;
            var kneeSupportHeight =
                ((measurement.leftKneeY + measurement.rightKneeY) * 0.5f -
                    measurement.SupportY);
            var legToTorsoRatio = legHeight / torsoHeight;
            return footAsymmetry <= _settings.maximumJumpFootAsymmetry &&
                legToTorsoRatio >= 0.75f &&
                kneeSupportHeight >= legHeight * 0.25f &&
                kneeSupportHeight <= legHeight * 0.90f;
        }

        private float JumpOffset(float jumpSignal)
        {
            return Mathf.Clamp(
                Mathf.Max(0f, jumpSignal) * _settings.jumpWorldScale,
                0f,
                _settings.maximumJumpHeight);
        }

        private float GroundedCompressionOffset(float compression)
        {
            // Reuse the semantic release tuning to derive a much smaller motion deadband. This
            // removes neutral measurement noise without making semantic Crouch the point where the
            // body suddenly starts descending.
            var motionDeadband = Mathf.Clamp(
                _settings.crouchReleaseThreshold * 0.25f,
                0.01f,
                0.05f);
            var effectiveCompression = Mathf.Max(0f, compression - motionDeadband);
            return -Mathf.Clamp(
                effectiveCompression * _settings.crouchWorldScale,
                0f,
                _settings.maximumCrouchDepth);
        }

        private void ApplyOffsetFilter(float target, float deltaTime)
        {
            if (!_hasFilteredOffset)
            {
                _filteredOffsetY = target;
                _hasFilteredOffset = true;
                return;
            }

            var alpha = 1f - Mathf.Exp(-_settings.verticalResponse * deltaTime);
            _filteredOffsetY = Mathf.Lerp(_filteredOffsetY, target, alpha);
            if (Mathf.Abs(_filteredOffsetY) < 0.00001f && Mathf.Abs(target) < 0.00001f)
            {
                _filteredOffsetY = 0f;
            }
        }

        private VerticalLocomotionSample Publish(
            bool available,
            bool referenceReady,
            VerticalLocomotionState state,
            VerticalJumpPhase jumpPhase,
            float jumpSignal,
            float crouchCompression,
            float supportRise,
            float footAsymmetry,
            float scaleChange,
            bool groundedBendActive)
        {
            LatestSample = new VerticalLocomotionSample
            {
                isAvailable = available,
                referenceReady = referenceReady,
                state = state,
                jumpPhase = jumpPhase,
                jumpSignal = jumpSignal,
                crouchCompression = crouchCompression,
                supportRise = supportRise,
                footAsymmetry = footAsymmetry,
                apparentScaleChange = scaleChange,
                worldOffsetY = _filteredOffsetY,
                groundedBendActive = groundedBendActive,
                suppressPhysicalDepth = state == VerticalLocomotionState.Jump,
            };
            return LatestSample;
        }

        private bool TryMeasure(CanonicalPoseFrame frame, out Measurement measurement)
        {
            measurement = default;
            if (frame == null ||
                !TrySupportFoot(frame, CanonicalJointId.LeftAnkle, CanonicalJointId.LeftHeel,
                    CanonicalJointId.LeftToe, out var leftFoot) ||
                !TrySupportFoot(frame, CanonicalJointId.RightAnkle, CanonicalJointId.RightHeel,
                    CanonicalJointId.RightToe, out var rightFoot) ||
                !TryImageJoint(frame, CanonicalJointId.Pelvis, out var pelvis) ||
                !TryImageJoint(frame, CanonicalJointId.Chest, out var chest) ||
                !TryImageJoint(frame, CanonicalJointId.LeftHip, out var leftHip) ||
                !TryImageJoint(frame, CanonicalJointId.RightHip, out var rightHip) ||
                !TryImageJoint(frame, CanonicalJointId.LeftKnee, out var leftKnee) ||
                !TryImageJoint(frame, CanonicalJointId.RightKnee, out var rightKnee))
            {
                return false;
            }

            var torsoHeight = chest.imagePosition.y - pelvis.imagePosition.y;
            var hipSpan = Vector2.Distance(leftHip.imagePosition, rightHip.imagePosition);
            if (torsoHeight <= 0.005f || hipSpan <= 0.005f)
            {
                return false;
            }

            var logScale = Mathf.Log(torsoHeight) + Mathf.Log(hipSpan);
            var scaleCount = 2f;
            if (TryImageJoint(frame, CanonicalJointId.LeftShoulder, out var leftShoulder) &&
                TryImageJoint(frame, CanonicalJointId.RightShoulder, out var rightShoulder))
            {
                var shoulderSpan = Vector2.Distance(
                    leftShoulder.imagePosition,
                    rightShoulder.imagePosition);
                if (shoulderSpan > 0.005f)
                {
                    logScale += Mathf.Log(shoulderSpan);
                    scaleCount += 1f;
                }
            }

            var apparentScale = Mathf.Exp(logScale / scaleCount);
            if (!IsFinite(apparentScale) || apparentScale <= 0f)
            {
                return false;
            }

            measurement = new Measurement
            {
                leftFoot = leftFoot,
                rightFoot = rightFoot,
                pelvisY = pelvis.imagePosition.y,
                chestY = chest.imagePosition.y,
                leftKneeY = leftKnee.imagePosition.y,
                rightKneeY = rightKnee.imagePosition.y,
                apparentScale = apparentScale,
                confidence = Mathf.Min(
                    Mathf.Min(leftFoot.confidence, rightFoot.confidence),
                    Mathf.Min(
                        Mathf.Min(pelvis.confidence, chest.confidence),
                        Mathf.Min(leftKnee.confidence, rightKnee.confidence))),
            };

            return measurement.PelvisSupportHeight > 0.01f &&
                measurement.TorsoHeight > 0.005f;
        }

        private bool TrySupportFoot(
            CanonicalPoseFrame frame,
            CanonicalJointId ankleId,
            CanonicalJointId heelId,
            CanonicalJointId toeId,
            out SupportFootMeasurement foot)
        {
            foot = default;
            var weightedY = 0f;
            var confidenceSum = 0f;
            var weightSum = 0f;
            var validPoints = 0;
            AddSupportPoint(frame.GetJoint(ankleId), 0.40f, ref weightedY,
                ref confidenceSum, ref weightSum, ref validPoints);
            AddSupportPoint(frame.GetJoint(heelId), 0.30f, ref weightedY,
                ref confidenceSum, ref weightSum, ref validPoints);
            AddSupportPoint(frame.GetJoint(toeId), 0.30f, ref weightedY,
                ref confidenceSum, ref weightSum, ref validPoints);

            if (validPoints < 2 || weightSum <= 0f)
            {
                return false;
            }

            foot = new SupportFootMeasurement
            {
                y = weightedY / weightSum,
                confidence = Mathf.Clamp01(confidenceSum / weightSum),
            };
            return IsFinite(foot.y);
        }

        private void AddSupportPoint(
            CanonicalPoseJoint joint,
            float weight,
            ref float weightedY,
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

            weightedY += joint.imagePosition.y * weight;
            confidenceSum += joint.confidence * weight;
            weightSum += weight;
            validPoints++;
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

        private static float Min4(float a, float b, float c, float d)
        {
            return Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));
        }

        private static float Max4(float a, float b, float c, float d)
        {
            return Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d));
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
