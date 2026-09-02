using System;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Stabilization
{
    /// <summary>
    /// Applies provider-independent confidence hysteresis and per-joint positional smoothing.
    /// It intentionally contains no rotations, avatar concepts, or locomotion behavior.
    /// </summary>
    public sealed class CanonicalPoseStabilizer
    {
        private readonly CanonicalStabilizerSettings _settings;
        private readonly JointState[] _states = new JointState[CanonicalPoseFrame.JointCount];

        public CanonicalPoseStabilizer(CanonicalStabilizerSettings settings = null)
        {
            _settings = settings ?? new CanonicalStabilizerSettings();
            _settings.Sanitize();
            for (var i = 0; i < _states.Length; i++)
            {
                _states[i] = new JointState(_settings);
            }
        }

        public CanonicalStabilizerSettings Settings => _settings;

        public void Reset()
        {
            for (var i = 0; i < _states.Length; i++)
            {
                _states[i].Reset();
            }
        }

        public void Stabilize(CanonicalPoseFrame source, CanonicalPoseFrame destination, double evaluationTimeSeconds)
        {
            if (destination == null)
            {
                return;
            }

            if (source == null)
            {
                destination.Clear();
                return;
            }

            var evaluationTime = ResolveTime(source, evaluationTimeSeconds);
            destination.Begin(source.sourceTimestampMillisec, source.receivedAtSeconds, source.hasPose);
            for (var i = 0; i < _states.Length; i++)
            {
                var sourceJoint = source.GetJoint((CanonicalJointId)i);
                var outputJoint = _states[i].Evaluate(sourceJoint, source, evaluationTime, _settings);
                if (outputJoint.IsTracked)
                {
                    destination.SetJoint(in outputJoint);
                }
            }

            RebuildLocalPositions(destination);
            destination.Complete();
        }

        private static double ResolveTime(CanonicalPoseFrame source, double evaluationTimeSeconds)
        {
            if (!double.IsNaN(evaluationTimeSeconds) && !double.IsInfinity(evaluationTimeSeconds))
            {
                return evaluationTimeSeconds;
            }

            return source.receivedAtSeconds;
        }

        private static void RebuildLocalPositions(CanonicalPoseFrame frame)
        {
            var pelvis = frame.GetJoint(CanonicalJointId.Pelvis);
            var hasRoot = pelvis.IsTracked && pelvis.hasWorldPosition && IsFinite(pelvis.worldPosition);
            var root = hasRoot ? pelvis.worldPosition : Vector3.zero;

            for (var i = 0; i < CanonicalPoseFrame.JointCount; i++)
            {
                var joint = frame.GetJoint((CanonicalJointId)i);
                if (!joint.IsTracked)
                {
                    continue;
                }

                if (joint.hasWorldPosition && IsFinite(joint.worldPosition))
                {
                    joint.localPosition = hasRoot ? joint.worldPosition - root : joint.worldPosition;
                    joint.hasLocalPosition = true;
                    frame.SetJoint(in joint);
                }
            }
        }

        private static CanonicalPoseJoint Unavailable(CanonicalJointId id)
        {
            return new CanonicalPoseJoint
            {
                id = id,
                tracking = CanonicalTrackingState.Unavailable,
                confidence = 0f,
            };
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

        private sealed class JointState
        {
            private readonly OneEuroFilterVector2 _imageFilter;
            private readonly OneEuroFilterVector3 _worldFilter;
            private bool _active;
            private bool _hasUnavailableSince;
            private bool _hasLastSample;
            private int _acquisitionSamples;
            private double _unavailableSince;
            private double _lastSeenTime;
            private double _lastFilterTime;
            private long _lastSourceTimestamp;
            private double _lastReceivedAt;
            private CanonicalPoseJoint _lastOutput;

            public JointState(CanonicalStabilizerSettings settings)
            {
                _imageFilter = new OneEuroFilterVector2(settings.minCutoff, settings.beta, settings.derivativeCutoff);
                _worldFilter = new OneEuroFilterVector3(settings.minCutoff, settings.beta, settings.derivativeCutoff);
                Reset();
            }

            public void Reset()
            {
                _active = false;
                _hasUnavailableSince = false;
                _hasLastSample = false;
                _acquisitionSamples = 0;
                _unavailableSince = 0d;
                _lastSeenTime = 0d;
                _lastFilterTime = 0d;
                _lastSourceTimestamp = 0L;
                _lastReceivedAt = 0d;
                _lastOutput = default;
                _imageFilter.Reset();
                _worldFilter.Reset();
            }

            public CanonicalPoseJoint Evaluate(
                CanonicalPoseJoint sourceJoint,
                CanonicalPoseFrame sourceFrame,
                double evaluationTime,
                CanonicalStabilizerSettings settings)
            {
                var isNewSample = IsNewSample(sourceFrame);
                if (isNewSample)
                {
                    _lastSourceTimestamp = sourceFrame.sourceTimestampMillisec;
                    _lastReceivedAt = sourceFrame.receivedAtSeconds;
                    _hasLastSample = true;
                }

                var validMeasurement = sourceJoint.IsTracked &&
                    IsFinite(sourceJoint.confidence) &&
                    IsFinite(sourceJoint.imagePosition) &&
                    sourceJoint.hasImagePosition;
                var confidence = Mathf.Clamp01(IsFinite(sourceJoint.confidence) ? sourceJoint.confidence : 0f);

                if (!_active)
                {
                    if (!isNewSample)
                    {
                        return Unavailable(sourceJoint.id);
                    }

                    if (!validMeasurement || confidence < settings.acquireConfidence)
                    {
                        _acquisitionSamples = 0;
                        return Unavailable(sourceJoint.id);
                    }

                    _acquisitionSamples++;
                    if (_acquisitionSamples < settings.acquireSamples)
                    {
                        return Unavailable(sourceJoint.id);
                    }

                    _active = true;
                    _acquisitionSamples = 0;
                    ResetFilters();
                    _lastSeenTime = evaluationTime;
                    _hasUnavailableSince = false;
                    return FilterAvailable(sourceJoint, confidence, evaluationTime, settings);
                }

                if (!validMeasurement || confidence < settings.sustainConfidence)
                {
                    return HandleUnavailable(sourceJoint.id, evaluationTime, settings);
                }

                if (!isNewSample)
                {
                    return _lastOutput;
                }

                if (evaluationTime - _lastSeenTime >= settings.resetAfterLossSeconds)
                {
                    ResetFilters();
                }

                _hasUnavailableSince = false;
                _lastSeenTime = evaluationTime;
                return FilterAvailable(sourceJoint, confidence, evaluationTime, settings);
            }

            private bool IsNewSample(CanonicalPoseFrame sourceFrame)
            {
                return !_hasLastSample ||
                    sourceFrame.sourceTimestampMillisec != _lastSourceTimestamp ||
                    Math.Abs(sourceFrame.receivedAtSeconds - _lastReceivedAt) > 0.000001d;
            }

            private CanonicalPoseJoint HandleUnavailable(
                CanonicalJointId id,
                double evaluationTime,
                CanonicalStabilizerSettings settings)
            {
                if (!_hasUnavailableSince)
                {
                    _hasUnavailableSince = true;
                    _unavailableSince = evaluationTime;
                }

                var unavailableDuration = Math.Max(0d, evaluationTime - _unavailableSince);
                if (unavailableDuration <= settings.lossGraceSeconds)
                {
                    return _lastOutput;
                }

                if (unavailableDuration >= settings.resetAfterLossSeconds)
                {
                    _active = false;
                    _acquisitionSamples = 0;
                    ResetFilters();
                }

                return Unavailable(id);
            }

            private CanonicalPoseJoint FilterAvailable(
                CanonicalPoseJoint sourceJoint,
                float confidence,
                double evaluationTime,
                CanonicalStabilizerSettings settings)
            {
                var deltaTime = _imageFilter.IsInitialized
                    ? (float)(evaluationTime - _lastFilterTime)
                    : settings.defaultDeltaTimeSeconds;
                if (!IsFinite(deltaTime) || deltaTime <= 0f)
                {
                    deltaTime = settings.defaultDeltaTimeSeconds;
                }

                deltaTime = Mathf.Clamp(deltaTime, 0.0001f, settings.maximumDeltaTimeSeconds);
                var output = sourceJoint;
                output.tracking = CanonicalTrackingState.Tracked;
                output.confidence = confidence;
                output.imagePosition = _imageFilter.Filter(sourceJoint.imagePosition, deltaTime);
                output.hasImagePosition = true;

                if (sourceJoint.hasWorldPosition && IsFinite(sourceJoint.worldPosition))
                {
                    output.worldPosition = _worldFilter.Filter(sourceJoint.worldPosition, deltaTime);
                    output.hasWorldPosition = true;
                }
                else
                {
                    output.hasWorldPosition = false;
                }

                _lastFilterTime = evaluationTime;
                _lastOutput = output;
                return output;
            }

            private void ResetFilters()
            {
                _imageFilter.Reset();
                _worldFilter.Reset();
                _lastFilterTime = 0d;
            }
        }
    }
}
