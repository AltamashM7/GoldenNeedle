using System;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Calibration
{
    /// <summary>
    /// Small in-memory neutral/T-pose calibration state machine. It consumes canonical data only,
    /// so provider replacement does not change calibration behavior.
    /// </summary>
    public sealed class MotionCalibrationSession
    {
        private const int NeutralJointCount = 10;
        private static readonly CanonicalJointId[] NeutralJoints =
        {
            CanonicalJointId.Pelvis,
            CanonicalJointId.Chest,
            CanonicalJointId.LeftShoulder,
            CanonicalJointId.RightShoulder,
            CanonicalJointId.LeftHip,
            CanonicalJointId.RightHip,
            CanonicalJointId.LeftKnee,
            CanonicalJointId.RightKnee,
            CanonicalJointId.LeftAnkle,
            CanonicalJointId.RightAnkle,
        };

        private static readonly CanonicalJointId[] NeutralRequiredJoints =
        {
            CanonicalJointId.LeftShoulder,
            CanonicalJointId.RightShoulder,
            CanonicalJointId.LeftHip,
            CanonicalJointId.RightHip,
            CanonicalJointId.LeftKnee,
            CanonicalJointId.RightKnee,
            CanonicalJointId.LeftAnkle,
            CanonicalJointId.RightAnkle,
        };

        private static readonly CanonicalJointId[] TPoseJoints =
        {
            CanonicalJointId.LeftShoulder,
            CanonicalJointId.LeftWrist,
            CanonicalJointId.RightShoulder,
            CanonicalJointId.RightWrist,
        };

        private readonly MotionCalibrationSettings _settings;
        private readonly Vector3[] _neutralSums = new Vector3[NeutralJointCount];
        private readonly float[] _neutralWeights = new float[NeutralJointCount];
        private readonly Vector2[] _previousNeutralImage = new Vector2[NeutralJointCount];
        private readonly bool[] _hasPreviousNeutralImage = new bool[NeutralJointCount];
        private readonly Vector2[] _previousTPoseImage = new Vector2[4];
        private readonly bool[] _hasPreviousTPoseImage = new bool[4];
        private readonly Vector3[] _tPoseSums = new Vector3[4];
        private readonly float[] _tPoseWeights = new float[4];
        private readonly MotionCalibrationProfile _profile = new MotionCalibrationProfile();

        private bool _hasLastFrame;
        private long _lastSourceTimestamp;
        private double _lastReceivedAt;
        private double _stageStartedAt;
        private float _progress01;
        private int _neutralAccumulatedSamples;
        private int _tPoseAccumulatedSamples;

        public MotionCalibrationSession(MotionCalibrationSettings settings = null)
        {
            _settings = settings ?? new MotionCalibrationSettings();
            _settings.Sanitize();
            Reset();
        }

        public MotionCalibrationSettings Settings => _settings;
        public MotionCalibrationProfile Profile => _profile;
        public MotionCalibrationState State { get; private set; }
        public float Progress01 => _progress01;
        public bool IsValid => State == MotionCalibrationState.Complete && _profile.isValid;

        public void Begin(double nowSeconds = 0d)
        {
            _profile.Clear();
            State = MotionCalibrationState.AwaitingNeutral;
            _profile.state = State;
            _progress01 = 0f;
            _stageStartedAt = nowSeconds;
            _hasLastFrame = false;
            _neutralAccumulatedSamples = 0;
            _tPoseAccumulatedSamples = 0;
            ClearNeutralSampling();
            ClearTPoseSampling();
        }

        public void Reset()
        {
            _profile.Clear();
            State = MotionCalibrationState.Idle;
            _progress01 = 0f;
            _stageStartedAt = 0d;
            _hasLastFrame = false;
            _neutralAccumulatedSamples = 0;
            _tPoseAccumulatedSamples = 0;
            ClearNeutralSampling();
            ClearTPoseSampling();
        }

        public void Update(CanonicalPoseFrame frame, double nowSeconds)
        {
            if (State == MotionCalibrationState.Idle || State == MotionCalibrationState.Complete || frame == null)
            {
                return;
            }

            if (!IsNewFrame(frame))
            {
                return;
            }

            var now = SanitizeTime(nowSeconds, frame.receivedAtSeconds);
            switch (State)
            {
                case MotionCalibrationState.AwaitingNeutral:
                    UpdateAwaitingNeutral(frame, now);
                    break;
                case MotionCalibrationState.SamplingNeutral:
                    UpdateSamplingNeutral(frame, now);
                    break;
                case MotionCalibrationState.AwaitingTPose:
                    UpdateAwaitingTPose(frame, now);
                    break;
                case MotionCalibrationState.SamplingTPose:
                    UpdateSamplingTPose(frame, now);
                    break;
            }
        }

        private void UpdateAwaitingNeutral(CanonicalPoseFrame frame, double now)
        {
            if (!IsNeutralPoseValid(frame))
            {
                _progress01 = 0f;
                return;
            }

            State = MotionCalibrationState.SamplingNeutral;
            _profile.state = State;
            _stageStartedAt = now;
            ClearNeutralSampling();
            AccumulateNeutral(frame);
            _progress01 = 0f;
        }

        private void UpdateSamplingNeutral(CanonicalPoseFrame frame, double now)
        {
            if (!IsNeutralPoseValid(frame) || !IsNeutralStable(frame))
            {
                State = MotionCalibrationState.AwaitingNeutral;
                _profile.state = State;
                _progress01 = 0f;
                ClearNeutralSampling();
                return;
            }

            AccumulateNeutral(frame);
            _progress01 = Mathf.Clamp01((float)((now - _stageStartedAt) / _settings.neutralHoldSeconds));
            if (_progress01 < 1f)
            {
                return;
            }

            _profile.neutralSampleCount = _neutralAccumulatedSamples;
            State = MotionCalibrationState.AwaitingTPose;
            _profile.state = State;
            _progress01 = 0f;
            ClearTPoseSampling();
        }

        private void UpdateAwaitingTPose(CanonicalPoseFrame frame, double now)
        {
            if (!IsTPoseValid(frame))
            {
                _progress01 = 0f;
                return;
            }

            State = MotionCalibrationState.SamplingTPose;
            _profile.state = State;
            _stageStartedAt = now;
            ClearTPoseSampling();
            AccumulateTPose(frame);
            _progress01 = 0f;
        }

        private void UpdateSamplingTPose(CanonicalPoseFrame frame, double now)
        {
            if (!IsTPoseValid(frame) || !IsTPoseStable(frame))
            {
                State = MotionCalibrationState.AwaitingTPose;
                _profile.state = State;
                _progress01 = 0f;
                ClearTPoseSampling();
                return;
            }

            AccumulateTPose(frame);
            _progress01 = Mathf.Clamp01((float)((now - _stageStartedAt) / _settings.tPoseHoldSeconds));
            if (_progress01 < 1f)
            {
                return;
            }

            _profile.tPoseSampleCount = _tPoseAccumulatedSamples;
            FinalizeProfile(now);
        }

        private bool IsNeutralPoseValid(CanonicalPoseFrame frame)
        {
            for (var i = 0; i < NeutralRequiredJoints.Length; i++)
            {
                var joint = frame.GetJoint(NeutralRequiredJoints[i]);
                if (!joint.IsTracked || !joint.hasImagePosition || !IsFinite(joint.imagePosition))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsNeutralStable(CanonicalPoseFrame frame)
        {
            for (var i = 0; i < NeutralJoints.Length; i++)
            {
                var joint = frame.GetJoint(NeutralJoints[i]);
                if (!joint.IsTracked || !joint.hasImagePosition || !IsFinite(joint.imagePosition))
                {
                    continue;
                }

                if (_hasPreviousNeutralImage[i] &&
                    Vector2.Distance(_previousNeutralImage[i], joint.imagePosition) > _settings.neutralStabilityTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsTPoseValid(CanonicalPoseFrame frame)
        {
            var leftShoulder = frame.GetJoint(CanonicalJointId.LeftShoulder);
            var leftElbow = frame.GetJoint(CanonicalJointId.LeftElbow);
            var leftWrist = frame.GetJoint(CanonicalJointId.LeftWrist);
            var rightShoulder = frame.GetJoint(CanonicalJointId.RightShoulder);
            var rightElbow = frame.GetJoint(CanonicalJointId.RightElbow);
            var rightWrist = frame.GetJoint(CanonicalJointId.RightWrist);
            var leftHip = frame.GetJoint(CanonicalJointId.LeftHip);
            var rightHip = frame.GetJoint(CanonicalJointId.RightHip);
            if (!ValidImage(leftShoulder) || !ValidImage(leftElbow) || !ValidImage(leftWrist) ||
                !ValidImage(rightShoulder) || !ValidImage(rightElbow) || !ValidImage(rightWrist) ||
                !ValidImage(leftHip) || !ValidImage(rightHip))
            {
                return false;
            }

            var shoulderWidth = Vector2.Distance(leftShoulder.imagePosition, rightShoulder.imagePosition);
            var leftArm = leftWrist.imagePosition - leftShoulder.imagePosition;
            var rightArm = rightWrist.imagePosition - rightShoulder.imagePosition;
            var minimumArmLength = Mathf.Max(0.08f, shoulderWidth * _settings.minimumArmExtensionRatio);
            var angleTolerance = _settings.tPoseAngularToleranceDegrees;
            var leftDirection = leftArm.sqrMagnitude > 0.000001f ? leftArm.normalized : Vector2.zero;
            var rightDirection = rightArm.sqrMagnitude > 0.000001f ? rightArm.normalized : Vector2.zero;
            var leftElbowAngle = Vector2.Angle(leftShoulder.imagePosition - leftElbow.imagePosition, leftWrist.imagePosition - leftElbow.imagePosition);
            var rightElbowAngle = Vector2.Angle(rightShoulder.imagePosition - rightElbow.imagePosition, rightWrist.imagePosition - rightElbow.imagePosition);

            return leftArm.magnitude >= minimumArmLength &&
                rightArm.magnitude >= minimumArmLength &&
                Vector2.Angle(leftDirection, Vector2.left) <= angleTolerance &&
                Vector2.Angle(rightDirection, Vector2.right) <= angleTolerance &&
                Mathf.Abs(leftWrist.imagePosition.y - leftShoulder.imagePosition.y) <= _settings.wristHeightTolerance &&
                Mathf.Abs(rightWrist.imagePosition.y - rightShoulder.imagePosition.y) <= _settings.wristHeightTolerance &&
                leftElbowAngle >= _settings.minimumElbowExtensionDegrees &&
                rightElbowAngle >= _settings.minimumElbowExtensionDegrees;
        }

        private bool IsTPoseStable(CanonicalPoseFrame frame)
        {
            for (var i = 0; i < TPoseJoints.Length; i++)
            {
                var joint = frame.GetJoint(TPoseJoints[i]);
                if (_hasPreviousTPoseImage[i] &&
                    Vector2.Distance(_previousTPoseImage[i], joint.imagePosition) > _settings.tPoseStabilityTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private void AccumulateNeutral(CanonicalPoseFrame frame)
        {
            _neutralAccumulatedSamples++;
            for (var i = 0; i < NeutralJoints.Length; i++)
            {
                var joint = frame.GetJoint(NeutralJoints[i]);
                if (!ValidImage(joint))
                {
                    continue;
                }

                var weight = Mathf.Max(0.001f, Mathf.Clamp01(joint.confidence));
                var position = ReferencePosition(joint);
                _neutralSums[i] += position * weight;
                _neutralWeights[i] += weight;
                _previousNeutralImage[i] = joint.imagePosition;
                _hasPreviousNeutralImage[i] = true;
            }
        }

        private void AccumulateTPose(CanonicalPoseFrame frame)
        {
            _tPoseAccumulatedSamples++;
            for (var i = 0; i < TPoseJoints.Length; i++)
            {
                var joint = frame.GetJoint(TPoseJoints[i]);
                var weight = Mathf.Max(0.001f, Mathf.Clamp01(joint.confidence));
                _tPoseSums[i] += ReferencePosition(joint) * weight;
                _tPoseWeights[i] += weight;
                _previousTPoseImage[i] = joint.imagePosition;
                _hasPreviousTPoseImage[i] = true;
            }
        }

        private void FinalizeProfile(double now)
        {
            var neutral = new Vector3[NeutralJointCount];
            for (var i = 0; i < neutral.Length; i++)
            {
                neutral[i] = _neutralWeights[i] > 0f ? _neutralSums[i] / _neutralWeights[i] : Vector3.zero;
            }

            _profile.neutralPelvisPosition = neutral[0];
            _profile.neutralChestPosition = neutral[1];
            _profile.neutralLeftShoulderPosition = neutral[2];
            _profile.neutralRightShoulderPosition = neutral[3];
            _profile.neutralLeftHipPosition = neutral[4];
            _profile.neutralRightHipPosition = neutral[5];
            _profile.neutralLeftKneePosition = neutral[6];
            _profile.neutralRightKneePosition = neutral[7];
            _profile.neutralLeftAnklePosition = neutral[8];
            _profile.neutralRightAnklePosition = neutral[9];
            _profile.shoulderWidth = Vector3.Distance(neutral[2], neutral[3]);
            _profile.hipWidth = Vector3.Distance(neutral[4], neutral[5]);
            _profile.torsoLength = Vector3.Distance(neutral[0], neutral[1]);
            _profile.neutralBodyRight = SafeNormalize(neutral[3] - neutral[2]);
            _profile.neutralBodyUp = SafeNormalize(neutral[1] - neutral[0]);
            _profile.neutralBodyForward = SafeNormalize(Vector3.Cross(_profile.neutralBodyRight, _profile.neutralBodyUp));

            var tPose = new Vector3[4];
            for (var i = 0; i < tPose.Length; i++)
            {
                tPose[i] = _tPoseWeights[i] > 0f ? _tPoseSums[i] / _tPoseWeights[i] : Vector3.zero;
            }

            _profile.tPoseLeftArmDirection = SafeNormalize(tPose[1] - tPose[0]);
            _profile.tPoseRightArmDirection = SafeNormalize(tPose[3] - tPose[2]);
            _profile.tPoseArmSpan = Vector3.Distance(tPose[1], tPose[3]);
            _profile.completedAtSeconds = now;
            _profile.version = MotionCalibrationProfile.CurrentVersion;
            _profile.isValid = AllFinite(_profile) &&
                _profile.shoulderWidth > 0.0001f &&
                _profile.hipWidth > 0.0001f &&
                _profile.torsoLength > 0.0001f &&
                _profile.tPoseArmSpan > 0.0001f;
            State = MotionCalibrationState.Complete;
            _profile.state = State;
            _progress01 = 1f;
        }

        private bool IsNewFrame(CanonicalPoseFrame frame)
        {
            var isNew = !_hasLastFrame ||
                frame.sourceTimestampMillisec != _lastSourceTimestamp ||
                Math.Abs(frame.receivedAtSeconds - _lastReceivedAt) > 0.000001d;
            if (isNew)
            {
                _hasLastFrame = true;
                _lastSourceTimestamp = frame.sourceTimestampMillisec;
                _lastReceivedAt = frame.receivedAtSeconds;
            }

            return isNew;
        }

        private static Vector3 ReferencePosition(CanonicalPoseJoint joint)
        {
            if (joint.hasLocalPosition && IsFinite(joint.localPosition))
            {
                return joint.localPosition;
            }

            if (joint.hasWorldPosition && IsFinite(joint.worldPosition))
            {
                return joint.worldPosition;
            }

            return new Vector3(joint.imagePosition.x, joint.imagePosition.y, 0f);
        }

        private static bool ValidImage(CanonicalPoseJoint joint)
        {
            return joint.IsTracked && joint.hasImagePosition && IsFinite(joint.imagePosition);
        }

        private void ClearNeutralSampling()
        {
            _neutralAccumulatedSamples = 0;
            Array.Clear(_neutralSums, 0, _neutralSums.Length);
            Array.Clear(_neutralWeights, 0, _neutralWeights.Length);
            Array.Clear(_hasPreviousNeutralImage, 0, _hasPreviousNeutralImage.Length);
        }

        private void ClearTPoseSampling()
        {
            _tPoseAccumulatedSamples = 0;
            Array.Clear(_tPoseSums, 0, _tPoseSums.Length);
            Array.Clear(_tPoseWeights, 0, _tPoseWeights.Length);
            Array.Clear(_hasPreviousTPoseImage, 0, _hasPreviousTPoseImage.Length);
        }

        private static double SanitizeTime(double value, double fallback)
        {
            if (!double.IsNaN(value) && !double.IsInfinity(value))
            {
                return value;
            }

            return !double.IsNaN(fallback) && !double.IsInfinity(fallback) ? fallback : 0d;
        }

        private static Vector3 SafeNormalize(Vector3 value)
        {
            return IsFinite(value) && value.sqrMagnitude > 0.000001f ? value.normalized : Vector3.zero;
        }

        private static bool AllFinite(MotionCalibrationProfile profile)
        {
            return IsFinite(profile.neutralPelvisPosition) && IsFinite(profile.neutralChestPosition) &&
                IsFinite(profile.neutralLeftShoulderPosition) && IsFinite(profile.neutralRightShoulderPosition) &&
                IsFinite(profile.neutralLeftHipPosition) && IsFinite(profile.neutralRightHipPosition) &&
                IsFinite(profile.neutralLeftKneePosition) && IsFinite(profile.neutralRightKneePosition) &&
                IsFinite(profile.neutralLeftAnklePosition) && IsFinite(profile.neutralRightAnklePosition) &&
                IsFinite(profile.neutralBodyUp) && IsFinite(profile.neutralBodyRight) && IsFinite(profile.neutralBodyForward) &&
                IsFinite(profile.tPoseLeftArmDirection) && IsFinite(profile.tPoseRightArmDirection) &&
                IsFinite(profile.shoulderWidth) && IsFinite(profile.hipWidth) && IsFinite(profile.torsoLength) &&
                IsFinite(profile.tPoseArmSpan);
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
