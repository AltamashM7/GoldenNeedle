using System;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Calibration
{
    public enum MotionCalibrationChainWaitReason
    {
        None = 0,
        MissingJoint = 1,
        LowConfidence = 2,
        InvalidGeometry = 3,
    }

    public readonly struct MotionCalibrationChainProgress
    {
        public MotionCalibrationChainProgress(
            bool isReady,
            int sampleCount,
            int requiredSamples,
            MotionCalibrationChainWaitReason waitReason,
            CanonicalJointId waitingForJoint)
        {
            IsReady = isReady;
            SampleCount = sampleCount;
            RequiredSamples = requiredSamples;
            WaitReason = waitReason;
            WaitingForJoint = waitingForJoint;
        }

        public bool IsReady { get; }
        public int SampleCount { get; }
        public int RequiredSamples { get; }
        public MotionCalibrationChainWaitReason WaitReason { get; }
        public CanonicalJointId WaitingForJoint { get; }
    }

    /// <summary>
    /// Modular in-memory calibration. Body reference is sampled from a comfortable stable torso
    /// pose. Each limb chain independently accumulates three-joint segment measurements whenever
    /// that chain is trustworthy; no bilateral T-pose or all-body gate exists.
    /// </summary>
    public sealed class MotionCalibrationSession
    {
        private const int BodyJointCount = 6;
        private const int ChainCount = 4;

        private static readonly CanonicalJointId[] BodyReferenceJoints =
        {
            CanonicalJointId.Pelvis,
            CanonicalJointId.Chest,
            CanonicalJointId.LeftShoulder,
            CanonicalJointId.RightShoulder,
            CanonicalJointId.LeftHip,
            CanonicalJointId.RightHip,
        };

        private struct ChainDefinition
        {
            public MotionCalibrationChainId id;
            public CanonicalJointId root;
            public CanonicalJointId mid;
            public CanonicalJointId tip;
        }

        private struct ChainAccumulator
        {
            public int sampleCount;
            public float totalWeight;
            public float upperLengthWeightedSum;
            public float lowerLengthWeightedSum;
            public Vector3 upperDirectionWeightedSum;
            public Vector3 lowerDirectionWeightedSum;
        }

        private static readonly ChainDefinition[] ChainDefinitions =
        {
            new ChainDefinition
            {
                id = MotionCalibrationChainId.LeftArm,
                root = CanonicalJointId.LeftShoulder,
                mid = CanonicalJointId.LeftElbow,
                tip = CanonicalJointId.LeftWrist,
            },
            new ChainDefinition
            {
                id = MotionCalibrationChainId.RightArm,
                root = CanonicalJointId.RightShoulder,
                mid = CanonicalJointId.RightElbow,
                tip = CanonicalJointId.RightWrist,
            },
            new ChainDefinition
            {
                id = MotionCalibrationChainId.LeftLeg,
                root = CanonicalJointId.LeftHip,
                mid = CanonicalJointId.LeftKnee,
                tip = CanonicalJointId.LeftAnkle,
            },
            new ChainDefinition
            {
                id = MotionCalibrationChainId.RightLeg,
                root = CanonicalJointId.RightHip,
                mid = CanonicalJointId.RightKnee,
                tip = CanonicalJointId.RightAnkle,
            },
        };

        private readonly MotionCalibrationSettings _settings;
        private readonly Vector3[] _bodySums = new Vector3[BodyJointCount];
        private readonly float[] _bodyWeights = new float[BodyJointCount];
        private readonly Vector2[] _previousBodyImage = new Vector2[BodyJointCount];
        private readonly bool[] _hasPreviousBodyImage = new bool[BodyJointCount];
        private readonly ChainAccumulator[] _chainAccumulators = new ChainAccumulator[ChainCount];
        private readonly MotionCalibrationChainWaitReason[] _chainWaitReasons = new MotionCalibrationChainWaitReason[ChainCount];
        private readonly CanonicalJointId[] _chainWaitingJoints = new CanonicalJointId[ChainCount];
        private readonly MotionCalibrationProfile _profile = new MotionCalibrationProfile();

        private bool _hasLastFrame;
        private long _lastSourceTimestamp;
        private double _lastReceivedAt;
        private double _stageStartedAt;
        private float _progress01;
        private int _bodyAccumulatedSamples;

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

        /// <summary>
        /// Calibration is usable as soon as the body reference is valid. Individual chains remain
        /// independently optional until their own geometry is ready.
        /// </summary>
        public bool IsValid => _profile.bodyReferenceValid;
        public bool IsComplete => State == MotionCalibrationState.Ready;

        public void Begin(double nowSeconds = 0d)
        {
            _profile.Clear();
            State = MotionCalibrationState.AwaitingBodyReference;
            _profile.state = State;
            _progress01 = 0f;
            _stageStartedAt = nowSeconds;
            _hasLastFrame = false;
            ClearBodySampling();
            ClearChainSampling();
        }

        public void Reset()
        {
            _profile.Clear();
            State = MotionCalibrationState.Idle;
            _profile.state = State;
            _progress01 = 0f;
            _stageStartedAt = 0d;
            _hasLastFrame = false;
            ClearBodySampling();
            ClearChainSampling();
        }

        public MotionCalibrationChainProgress GetChainProgress(MotionCalibrationChainId id)
        {
            var index = (int)id;
            var geometry = _profile.GetChainGeometry(id);
            var samples = geometry.isValid ? geometry.sampleCount : _chainAccumulators[index].sampleCount;
            return new MotionCalibrationChainProgress(
                geometry.isValid,
                samples,
                _settings.geometrySamplesRequired,
                geometry.isValid ? MotionCalibrationChainWaitReason.None : _chainWaitReasons[index],
                _chainWaitingJoints[index]);
        }

        public void Update(CanonicalPoseFrame frame, double nowSeconds)
        {
            if (State == MotionCalibrationState.Idle || frame == null || !IsNewFrame(frame))
            {
                return;
            }

            var now = SanitizeTime(nowSeconds, frame.receivedAtSeconds);

            // Geometry is deliberately independent from body-reference stability. A body-reference
            // reset must never erase valid samples already collected for an unrelated chain.
            if (State != MotionCalibrationState.Ready)
            {
                AccumulateAvailableGeometry(frame, now);
            }

            switch (State)
            {
                case MotionCalibrationState.AwaitingBodyReference:
                    UpdateAwaitingBodyReference(frame, now);
                    break;
                case MotionCalibrationState.SamplingBodyReference:
                    UpdateSamplingBodyReference(frame, now);
                    break;
                case MotionCalibrationState.AcquiringGeometry:
                    RefreshGeometryState(now);
                    break;
                case MotionCalibrationState.Ready:
                    _progress01 = 1f;
                    break;
            }
        }

        private void UpdateAwaitingBodyReference(CanonicalPoseFrame frame, double now)
        {
            if (!IsBodyReferenceFrameValid(frame))
            {
                _progress01 = 0f;
                return;
            }

            State = MotionCalibrationState.SamplingBodyReference;
            _profile.state = State;
            _stageStartedAt = now;
            ClearBodySampling();
            AccumulateBodyReference(frame);
            _progress01 = 0f;
        }

        private void UpdateSamplingBodyReference(CanonicalPoseFrame frame, double now)
        {
            if (!IsBodyReferenceFrameValid(frame) || !IsBodyReferenceStable(frame))
            {
                State = MotionCalibrationState.AwaitingBodyReference;
                _profile.state = State;
                _progress01 = 0f;
                ClearBodySampling();
                return;
            }

            AccumulateBodyReference(frame);
            _progress01 = Mathf.Clamp01(
                (float)((now - _stageStartedAt) / _settings.bodyReferenceHoldSeconds));
            if (_progress01 < 1f)
            {
                return;
            }

            FinalizeBodyReference(now);
        }

        private bool IsBodyReferenceFrameValid(CanonicalPoseFrame frame)
        {
            for (var i = 0; i < BodyReferenceJoints.Length; i++)
            {
                var joint = frame.GetJoint(BodyReferenceJoints[i]);
                if (!ValidImage(joint) ||
                    Confidence(joint) < _settings.minimumMeasurementConfidence ||
                    !TryGetMeasurementPosition(joint, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsBodyReferenceStable(CanonicalPoseFrame frame)
        {
            for (var i = 0; i < BodyReferenceJoints.Length; i++)
            {
                var joint = frame.GetJoint(BodyReferenceJoints[i]);
                if (_hasPreviousBodyImage[i] &&
                    Vector2.Distance(_previousBodyImage[i], joint.imagePosition) >
                    _settings.bodyReferenceStabilityTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private void AccumulateBodyReference(CanonicalPoseFrame frame)
        {
            _bodyAccumulatedSamples++;
            for (var i = 0; i < BodyReferenceJoints.Length; i++)
            {
                var joint = frame.GetJoint(BodyReferenceJoints[i]);
                if (!TryGetMeasurementPosition(joint, out var position))
                {
                    continue;
                }

                var weight = Mathf.Max(0.001f, Confidence(joint));
                _bodySums[i] += position * weight;
                _bodyWeights[i] += weight;
                _previousBodyImage[i] = joint.imagePosition;
                _hasPreviousBodyImage[i] = true;
            }
        }

        private void FinalizeBodyReference(double now)
        {
            var body = new Vector3[BodyJointCount];
            for (var i = 0; i < body.Length; i++)
            {
                body[i] = _bodyWeights[i] > 0f ? _bodySums[i] / _bodyWeights[i] : Vector3.zero;
            }

            _profile.neutralPelvisPosition = body[0];
            _profile.neutralChestPosition = body[1];
            _profile.neutralLeftShoulderPosition = body[2];
            _profile.neutralRightShoulderPosition = body[3];
            _profile.neutralLeftHipPosition = body[4];
            _profile.neutralRightHipPosition = body[5];
            _profile.shoulderWidth = Vector3.Distance(body[2], body[3]);
            _profile.hipWidth = Vector3.Distance(body[4], body[5]);
            _profile.torsoLength = Vector3.Distance(body[0], body[1]);
            _profile.neutralBodyRight = SafeNormalize(body[3] - body[2]);
            _profile.neutralBodyUp = SafeNormalize(body[1] - body[0]);
            _profile.neutralBodyForward = SafeNormalize(
                Vector3.Cross(_profile.neutralBodyRight, _profile.neutralBodyUp));
            _profile.bodyReferenceSampleCount = _bodyAccumulatedSamples;
            _profile.bodyReferenceCapturedAtSeconds = now;
            _profile.version = MotionCalibrationProfile.CurrentVersion;

            _profile.bodyReferenceValid =
                IsFinite(_profile.neutralPelvisPosition) &&
                IsFinite(_profile.neutralChestPosition) &&
                IsFinite(_profile.neutralLeftShoulderPosition) &&
                IsFinite(_profile.neutralRightShoulderPosition) &&
                IsFinite(_profile.neutralLeftHipPosition) &&
                IsFinite(_profile.neutralRightHipPosition) &&
                IsFinite(_profile.neutralBodyRight) &&
                IsFinite(_profile.neutralBodyUp) &&
                IsFinite(_profile.neutralBodyForward) &&
                _profile.shoulderWidth > 0.0001f &&
                _profile.hipWidth > 0.0001f &&
                _profile.torsoLength > 0.0001f &&
                _profile.neutralBodyRight.sqrMagnitude > 0.5f &&
                _profile.neutralBodyUp.sqrMagnitude > 0.5f &&
                _profile.neutralBodyForward.sqrMagnitude > 0.5f;

            // Compatibility/global validity now means body-reference usability only.
            _profile.isValid = _profile.bodyReferenceValid;

            if (!_profile.bodyReferenceValid)
            {
                State = MotionCalibrationState.AwaitingBodyReference;
                _profile.state = State;
                _progress01 = 0f;
                ClearBodySampling();
                return;
            }

            RefreshGeometryState(now);
        }

        private void AccumulateAvailableGeometry(CanonicalPoseFrame frame, double now)
        {
            for (var i = 0; i < ChainDefinitions.Length; i++)
            {
                var definition = ChainDefinitions[i];
                if (_profile.GetChainGeometry(definition.id).isValid)
                {
                    _chainWaitReasons[i] = MotionCalibrationChainWaitReason.None;
                    continue;
                }

                if (!TryMeasureChain(
                        frame,
                        definition,
                        out var upperVector,
                        out var lowerVector,
                        out var weight,
                        out var waitReason,
                        out var waitingFor))
                {
                    _chainWaitReasons[i] = waitReason;
                    _chainWaitingJoints[i] = waitingFor;
                    continue;
                }

                _chainWaitReasons[i] = MotionCalibrationChainWaitReason.None;
                var accumulator = _chainAccumulators[i];
                var upperLength = upperVector.magnitude;
                var lowerLength = lowerVector.magnitude;
                accumulator.sampleCount++;
                accumulator.totalWeight += weight;
                accumulator.upperLengthWeightedSum += upperLength * weight;
                accumulator.lowerLengthWeightedSum += lowerLength * weight;
                accumulator.upperDirectionWeightedSum += upperVector.normalized * weight;
                accumulator.lowerDirectionWeightedSum += lowerVector.normalized * weight;
                _chainAccumulators[i] = accumulator;

                if (accumulator.sampleCount >= _settings.geometrySamplesRequired &&
                    accumulator.totalWeight > 0f)
                {
                    FinalizeChain(i, definition.id, accumulator);
                }
            }

            if (_profile.bodyReferenceValid)
            {
                RefreshGeometryState(now);
            }
        }

        private bool TryMeasureChain(
            CanonicalPoseFrame frame,
            ChainDefinition definition,
            out Vector3 upperVector,
            out Vector3 lowerVector,
            out float weight,
            out MotionCalibrationChainWaitReason waitReason,
            out CanonicalJointId waitingFor)
        {
            upperVector = Vector3.zero;
            lowerVector = Vector3.zero;
            weight = 0f;
            waitReason = MotionCalibrationChainWaitReason.None;
            waitingFor = definition.root;

            var root = frame.GetJoint(definition.root);
            var mid = frame.GetJoint(definition.mid);
            var tip = frame.GetJoint(definition.tip);

            if (!TryGetMeasurementPosition(root, out var rootPosition))
            {
                waitReason = MotionCalibrationChainWaitReason.MissingJoint;
                waitingFor = definition.root;
                return false;
            }

            if (!TryGetMeasurementPosition(mid, out var midPosition))
            {
                waitReason = MotionCalibrationChainWaitReason.MissingJoint;
                waitingFor = definition.mid;
                return false;
            }

            if (!TryGetMeasurementPosition(tip, out var tipPosition))
            {
                waitReason = MotionCalibrationChainWaitReason.MissingJoint;
                waitingFor = definition.tip;
                return false;
            }

            weight = Mathf.Min(Confidence(root), Mathf.Min(Confidence(mid), Confidence(tip)));
            if (weight < _settings.minimumMeasurementConfidence)
            {
                waitReason = MotionCalibrationChainWaitReason.LowConfidence;
                return false;
            }

            upperVector = midPosition - rootPosition;
            lowerVector = tipPosition - midPosition;
            if (!IsFinite(upperVector) || !IsFinite(lowerVector) ||
                upperVector.magnitude < _settings.minimumSegmentLength ||
                lowerVector.magnitude < _settings.minimumSegmentLength)
            {
                waitReason = MotionCalibrationChainWaitReason.InvalidGeometry;
                return false;
            }

            return true;
        }

        private void FinalizeChain(
            int index,
            MotionCalibrationChainId id,
            ChainAccumulator accumulator)
        {
            var upperLength = accumulator.upperLengthWeightedSum / accumulator.totalWeight;
            var lowerLength = accumulator.lowerLengthWeightedSum / accumulator.totalWeight;
            var geometry = new MotionCalibrationChainGeometry
            {
                isValid = IsFinite(upperLength) && IsFinite(lowerLength) &&
                          upperLength >= _settings.minimumSegmentLength &&
                          lowerLength >= _settings.minimumSegmentLength,
                sampleCount = accumulator.sampleCount,
                upperLength = upperLength,
                lowerLength = lowerLength,
                reach = upperLength + lowerLength,
                referenceUpperDirection = SafeNormalize(accumulator.upperDirectionWeightedSum),
                referenceLowerDirection = SafeNormalize(accumulator.lowerDirectionWeightedSum),
            };

            if (!geometry.isValid || !IsFinite(geometry.reach) || geometry.reach <= 0f)
            {
                _chainWaitReasons[index] = MotionCalibrationChainWaitReason.InvalidGeometry;
                return;
            }

            _profile.SetChainGeometry(id, geometry);
            _chainWaitReasons[index] = MotionCalibrationChainWaitReason.None;
        }

        private void RefreshGeometryState(double now)
        {
            if (!_profile.bodyReferenceValid)
            {
                return;
            }

            if (_profile.AllChainGeometryValid)
            {
                State = MotionCalibrationState.Ready;
                _profile.state = State;
                if (_profile.completedAtSeconds <= 0d)
                {
                    _profile.completedAtSeconds = now;
                }

                _progress01 = 1f;
                return;
            }

            State = MotionCalibrationState.AcquiringGeometry;
            _profile.state = State;
            var totalProgress = 0f;
            for (var i = 0; i < ChainCount; i++)
            {
                var geometry = _profile.GetChainGeometry((MotionCalibrationChainId)i);
                var samples = geometry.isValid
                    ? _settings.geometrySamplesRequired
                    : _chainAccumulators[i].sampleCount;
                totalProgress += Mathf.Clamp01(samples / (float)_settings.geometrySamplesRequired);
            }

            _progress01 = totalProgress / ChainCount;
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

        private static bool ValidImage(CanonicalPoseJoint joint)
        {
            return joint.IsTracked &&
                joint.hasImagePosition &&
                IsFinite(joint.imagePosition);
        }

        private static bool TryGetMeasurementPosition(CanonicalPoseJoint joint, out Vector3 position)
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

        private static float Confidence(CanonicalPoseJoint joint)
        {
            return IsFinite(joint.confidence) ? Mathf.Clamp01(joint.confidence) : 0f;
        }

        private void ClearBodySampling()
        {
            _bodyAccumulatedSamples = 0;
            Array.Clear(_bodySums, 0, _bodySums.Length);
            Array.Clear(_bodyWeights, 0, _bodyWeights.Length);
            Array.Clear(_hasPreviousBodyImage, 0, _hasPreviousBodyImage.Length);
        }

        private void ClearChainSampling()
        {
            Array.Clear(_chainAccumulators, 0, _chainAccumulators.Length);
            Array.Clear(_chainWaitReasons, 0, _chainWaitReasons.Length);
            Array.Clear(_chainWaitingJoints, 0, _chainWaitingJoints.Length);
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
            return IsFinite(value) && value.sqrMagnitude > 0.000001f
                ? value.normalized
                : Vector3.zero;
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
