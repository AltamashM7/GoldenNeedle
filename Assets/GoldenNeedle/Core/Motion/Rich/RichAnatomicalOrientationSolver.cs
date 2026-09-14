using System;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Rich
{
    [Serializable]
    public sealed class RichOrientationSolverSettings
    {
        [Range(0f, 1f)] public float minimumEvidenceConfidence = 0.35f;
        public float minimumVectorMagnitude = 0.0001f;
        public float minimumProjectedSecondaryMagnitude = 0.0025f;
        public double secondaryHoldSeconds = 0.20d;
        public double referenceFallbackAfterSeconds = 0.55d;
        public float referenceFallbackResponse = 5.0f;
    }

    /// <summary>
    /// Reconstructs provider-independent anatomical bases from semantic evidence. The common
    /// descriptor path is shared by every supported channel; no bone-specific twist correction is
    /// hidden in the solver. Swing remains available independently when secondary evidence is not.
    /// </summary>
    public sealed class RichAnatomicalOrientationSolver
    {
        private readonly struct ChannelDescriptor
        {
            public ChannelDescriptor(
                RichOrientationChannelId channel,
                RichMotionEvidenceId primaryFrom,
                RichMotionEvidenceId primaryTo,
                RichMotionEvidenceId secondaryFrom,
                RichMotionEvidenceId secondaryTo)
            {
                this.channel = channel;
                this.primaryFrom = primaryFrom;
                this.primaryTo = primaryTo;
                this.secondaryFrom = secondaryFrom;
                this.secondaryTo = secondaryTo;
            }

            public readonly RichOrientationChannelId channel;
            public readonly RichMotionEvidenceId primaryFrom;
            public readonly RichMotionEvidenceId primaryTo;
            public readonly RichMotionEvidenceId secondaryFrom;
            public readonly RichMotionEvidenceId secondaryTo;
        }

        private struct TemporalState
        {
            public bool hasObservedSecondary;
            public Vector3 lastObservedSecondary;
            public double lastObservedTimeSeconds;
            public float lastObservedConfidence;
            public bool hasReferenceSecondary;
            public Vector3 referenceSecondary;
            public float referenceConfidence;
            public bool hasLastOutputSecondary;
            public Vector3 lastOutputSecondary;
            public double lastOutputTimeSeconds;
        }

        private static readonly ChannelDescriptor[] Descriptors =
        {
            new ChannelDescriptor(
                RichOrientationChannelId.Pelvis,
                RichMotionEvidenceId.Pelvis,
                RichMotionEvidenceId.Chest,
                RichMotionEvidenceId.LeftHip,
                RichMotionEvidenceId.RightHip),
            new ChannelDescriptor(
                RichOrientationChannelId.Chest,
                RichMotionEvidenceId.Pelvis,
                RichMotionEvidenceId.Chest,
                RichMotionEvidenceId.LeftShoulder,
                RichMotionEvidenceId.RightShoulder),
            new ChannelDescriptor(
                RichOrientationChannelId.LeftUpperArm,
                RichMotionEvidenceId.LeftShoulder,
                RichMotionEvidenceId.LeftElbow,
                RichMotionEvidenceId.LeftElbow,
                RichMotionEvidenceId.LeftWrist),
            new ChannelDescriptor(
                RichOrientationChannelId.LeftLowerArm,
                RichMotionEvidenceId.LeftElbow,
                RichMotionEvidenceId.LeftWrist,
                RichMotionEvidenceId.LeftPinky,
                RichMotionEvidenceId.LeftThumb),
            new ChannelDescriptor(
                RichOrientationChannelId.RightUpperArm,
                RichMotionEvidenceId.RightShoulder,
                RichMotionEvidenceId.RightElbow,
                RichMotionEvidenceId.RightElbow,
                RichMotionEvidenceId.RightWrist),
            new ChannelDescriptor(
                RichOrientationChannelId.RightLowerArm,
                RichMotionEvidenceId.RightElbow,
                RichMotionEvidenceId.RightWrist,
                RichMotionEvidenceId.RightPinky,
                RichMotionEvidenceId.RightThumb),
            new ChannelDescriptor(
                RichOrientationChannelId.LeftUpperLeg,
                RichMotionEvidenceId.LeftHip,
                RichMotionEvidenceId.LeftKnee,
                RichMotionEvidenceId.LeftKnee,
                RichMotionEvidenceId.LeftAnkle),
            new ChannelDescriptor(
                RichOrientationChannelId.LeftLowerLeg,
                RichMotionEvidenceId.LeftKnee,
                RichMotionEvidenceId.LeftAnkle,
                RichMotionEvidenceId.LeftHeel,
                RichMotionEvidenceId.LeftToe),
            new ChannelDescriptor(
                RichOrientationChannelId.RightUpperLeg,
                RichMotionEvidenceId.RightHip,
                RichMotionEvidenceId.RightKnee,
                RichMotionEvidenceId.RightKnee,
                RichMotionEvidenceId.RightAnkle),
            new ChannelDescriptor(
                RichOrientationChannelId.RightLowerLeg,
                RichMotionEvidenceId.RightKnee,
                RichMotionEvidenceId.RightAnkle,
                RichMotionEvidenceId.RightHeel,
                RichMotionEvidenceId.RightToe),
            new ChannelDescriptor(
                RichOrientationChannelId.LeftFoot,
                RichMotionEvidenceId.LeftHeel,
                RichMotionEvidenceId.LeftToe,
                RichMotionEvidenceId.LeftAnkle,
                RichMotionEvidenceId.LeftHeel),
            new ChannelDescriptor(
                RichOrientationChannelId.RightFoot,
                RichMotionEvidenceId.RightHeel,
                RichMotionEvidenceId.RightToe,
                RichMotionEvidenceId.RightAnkle,
                RichMotionEvidenceId.RightHeel),
        };

        private readonly RichOrientationSolverSettings _settings;
        private readonly TemporalState[] _temporal =
            new TemporalState[RichMotionFrame.OrientationChannelCount];
        private string _lastSourceProviderId = string.Empty;
        private long _lastSourceTimestampMillisec;

        public RichAnatomicalOrientationSolver(RichOrientationSolverSettings settings = null)
        {
            _settings = settings ?? new RichOrientationSolverSettings();
        }

        public RichOrientationSolverSettings Settings => _settings;

        public void Solve(RichMotionEvidenceFrame source, RichMotionFrame destination)
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

            if (HasSourceSessionChanged(source))
            {
                ResetTemporalState();
            }

            _lastSourceProviderId = source.sourceProviderId ?? string.Empty;
            _lastSourceTimestampMillisec = source.sourceTimestampMillisec;
            destination.BeginFromEvidence(source);

            if (!source.sourceAvailable)
            {
                destination.Complete();
                return;
            }

            var evaluationTime = SanitizeTime(source.evaluationTimeSeconds, source.receivedAtSeconds);
            for (var i = 0; i < Descriptors.Length; i++)
            {
                SolveChannel(source, Descriptors[i], evaluationTime, destination);
            }
            destination.Complete();
        }

        public void Reset()
        {
            ResetTemporalState();
            _lastSourceProviderId = string.Empty;
            _lastSourceTimestampMillisec = 0L;
        }

        public void ResetReferenceState()
        {
            Reset();
        }

        public static bool TryBuildBasis(
            Vector3 primaryEvidence,
            Vector3 secondaryEvidence,
            float minimumVectorMagnitude,
            float minimumProjectedSecondaryMagnitude,
            out CanonicalAnatomicalBasis basis)
        {
            basis = default;
            if (!TryNormalize(primaryEvidence, minimumVectorMagnitude, out var primary))
            {
                return false;
            }

            var projectedSecondary =
                secondaryEvidence - primary * Vector3.Dot(secondaryEvidence, primary);
            if (!TryNormalize(
                    projectedSecondary,
                    minimumProjectedSecondaryMagnitude,
                    out var secondary))
            {
                return false;
            }

            var third = Vector3.Cross(primary, secondary);
            if (!TryNormalize(third, minimumVectorMagnitude, out third))
            {
                return false;
            }

            secondary = Vector3.Cross(third, primary);
            if (!TryNormalize(secondary, minimumVectorMagnitude, out secondary))
            {
                return false;
            }

            var determinant = Vector3.Dot(Vector3.Cross(primary, secondary), third);
            if (!IsFinite(determinant) || determinant < 0.999f)
            {
                return false;
            }

            basis = new CanonicalAnatomicalBasis
            {
                primaryAxis = primary,
                secondaryAxis = secondary,
                thirdAxis = third,
                determinant = determinant,
                handedness = CanonicalAnatomicalHandedness.RightHanded,
                isValid = true,
            };
            return true;
        }

        private void SolveChannel(
            RichMotionEvidenceFrame source,
            ChannelDescriptor descriptor,
            double evaluationTime,
            RichMotionFrame destination)
        {
            var channel = new RichOrientationChannel
            {
                id = descriptor.channel,
                tracking = CanonicalTrackingState.Unavailable,
                twistState = RichTwistObservability.Unobservable,
            };

            if (!TryGetDirection(
                    source,
                    descriptor.primaryFrom,
                    descriptor.primaryTo,
                    out var primaryEvidence,
                    out var primaryConfidence) ||
                !TryNormalize(
                    primaryEvidence,
                    SafeMinimumVectorMagnitude,
                    out var primary))
            {
                destination.SetOrientation(in channel);
                return;
            }

            channel.tracking = CanonicalTrackingState.Tracked;
            channel.hasPrimaryAxis = true;
            channel.primaryAxis = primary;
            channel.swingConfidence = primaryConfidence;

            var temporalIndex = (int)descriptor.channel;
            var temporal = _temporal[temporalIndex];
            if (TryGetDirection(
                    source,
                    descriptor.secondaryFrom,
                    descriptor.secondaryTo,
                    out var secondaryEvidence,
                    out var secondaryConfidence) &&
                TryBuildBasis(
                    primary,
                    secondaryEvidence,
                    SafeMinimumVectorMagnitude,
                    SafeMinimumProjectedSecondaryMagnitude,
                    out var observedBasis))
            {
                var orientationConfidence = Mathf.Min(primaryConfidence, secondaryConfidence);
                channel.basis = observedBasis;
                channel.orientationConfidence = orientationConfidence;
                channel.twistConfidence = orientationConfidence;
                channel.twistState = RichTwistObservability.Observed;

                temporal.hasObservedSecondary = true;
                temporal.lastObservedSecondary = observedBasis.secondaryAxis;
                temporal.lastObservedTimeSeconds = evaluationTime;
                temporal.lastObservedConfidence = secondaryConfidence;
                temporal.hasLastOutputSecondary = true;
                temporal.lastOutputSecondary = observedBasis.secondaryAxis;
                temporal.lastOutputTimeSeconds = evaluationTime;
                if (!temporal.hasReferenceSecondary)
                {
                    temporal.hasReferenceSecondary = true;
                    temporal.referenceSecondary = observedBasis.secondaryAxis;
                    temporal.referenceConfidence = orientationConfidence;
                }

                _temporal[temporalIndex] = temporal;
                destination.SetOrientation(in channel);
                return;
            }

            if (!temporal.hasObservedSecondary)
            {
                destination.SetOrientation(in channel);
                return;
            }

            var secondsSinceObserved = Math.Max(0d, evaluationTime - temporal.lastObservedTimeSeconds);
            if (secondsSinceObserved < SafeReferenceFallbackAfterSeconds)
            {
                if (TryBuildBasis(
                        primary,
                        temporal.lastObservedSecondary,
                        SafeMinimumVectorMagnitude,
                        SafeMinimumProjectedSecondaryMagnitude,
                        out var heldBasis))
                {
                    channel.basis = heldBasis;
                    channel.orientationConfidence = Mathf.Min(
                        primaryConfidence,
                        HeldConfidence(temporal.lastObservedConfidence, secondsSinceObserved));
                    channel.twistConfidence = channel.orientationConfidence;
                    channel.twistState = RichTwistObservability.Held;
                    temporal.hasLastOutputSecondary = true;
                    temporal.lastOutputSecondary = heldBasis.secondaryAxis;
                    temporal.lastOutputTimeSeconds = evaluationTime;
                    _temporal[temporalIndex] = temporal;
                }

                destination.SetOrientation(in channel);
                return;
            }

            if (temporal.hasReferenceSecondary &&
                TryBuildBasis(
                    primary,
                    temporal.referenceSecondary,
                    SafeMinimumVectorMagnitude,
                    SafeMinimumProjectedSecondaryMagnitude,
                    out var referenceBasis))
            {
                var currentSecondary = temporal.hasLastOutputSecondary
                    ? ProjectSecondary(primary, temporal.lastOutputSecondary, referenceBasis.secondaryAxis)
                    : referenceBasis.secondaryAxis;
                var deltaTime = temporal.hasLastOutputSecondary
                    ? Math.Max(0d, evaluationTime - temporal.lastOutputTimeSeconds)
                    : 0d;
                var alpha = 1f - Mathf.Exp(
                    -SafeReferenceFallbackResponse * Mathf.Min((float)deltaTime, 0.25f));
                var blendedSecondary = BlendAroundPrimary(
                    currentSecondary,
                    referenceBasis.secondaryAxis,
                    primary,
                    alpha);

                if (TryBuildBasis(
                        primary,
                        blendedSecondary,
                        SafeMinimumVectorMagnitude,
                        SafeMinimumProjectedSecondaryMagnitude,
                        out var fallbackBasis))
                {
                    channel.basis = fallbackBasis;
                    channel.orientationConfidence = Mathf.Min(
                        primaryConfidence,
                        Mathf.Clamp01(temporal.referenceConfidence * 0.25f));
                    channel.twistConfidence = channel.orientationConfidence;
                    channel.twistState = RichTwistObservability.ReferenceFallback;
                    temporal.hasLastOutputSecondary = true;
                    temporal.lastOutputSecondary = fallbackBasis.secondaryAxis;
                    temporal.lastOutputTimeSeconds = evaluationTime;
                    _temporal[temporalIndex] = temporal;
                }
            }

            destination.SetOrientation(in channel);
        }

        private bool TryGetDirection(
            RichMotionEvidenceFrame source,
            RichMotionEvidenceId fromId,
            RichMotionEvidenceId toId,
            out Vector3 direction,
            out float confidence)
        {
            direction = Vector3.zero;
            confidence = 0f;
            var from = source.GetEvidence(fromId);
            var to = source.GetEvidence(toId);
            if (!from.IsTracked || !to.IsTracked ||
                !IsFinite(from.position) || !IsFinite(to.position))
            {
                return false;
            }

            var fromConfidence = SafeConfidence(from.confidence);
            var toConfidence = SafeConfidence(to.confidence);
            confidence = Mathf.Min(fromConfidence, toConfidence);
            if (confidence < SafeMinimumEvidenceConfidence)
            {
                return false;
            }

            direction = to.position - from.position;
            return IsFinite(direction) &&
                direction.sqrMagnitude >= SafeMinimumVectorMagnitude * SafeMinimumVectorMagnitude;
        }

        private float HeldConfidence(float observedConfidence, double secondsSinceObserved)
        {
            var safeObserved = SafeConfidence(observedConfidence);
            var holdSeconds = SafeSecondaryHoldSeconds;
            var fallbackSeconds = SafeReferenceFallbackAfterSeconds;
            if (secondsSinceObserved <= holdSeconds)
            {
                var normalized = holdSeconds <= 0d ? 1f : (float)(secondsSinceObserved / holdSeconds);
                return safeObserved * Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(normalized));
            }

            var tailDuration = Math.Max(0.0001d, fallbackSeconds - holdSeconds);
            var tail = Mathf.Clamp01((float)((secondsSinceObserved - holdSeconds) / tailDuration));
            return safeObserved * Mathf.Lerp(0.5f, 0.1f, tail);
        }

        private bool HasSourceSessionChanged(RichMotionEvidenceFrame source)
        {
            if (!string.IsNullOrEmpty(_lastSourceProviderId) &&
                !string.Equals(
                    _lastSourceProviderId,
                    source.sourceProviderId ?? string.Empty,
                    StringComparison.Ordinal))
            {
                return true;
            }

            return _lastSourceTimestampMillisec > 0L &&
                source.sourceTimestampMillisec > 0L &&
                source.sourceTimestampMillisec < _lastSourceTimestampMillisec;
        }

        private void ResetTemporalState()
        {
            for (var i = 0; i < _temporal.Length; i++)
            {
                _temporal[i] = default;
            }
        }

        private static Vector3 ProjectSecondary(
            Vector3 primary,
            Vector3 candidate,
            Vector3 fallback)
        {
            var projected = candidate - primary * Vector3.Dot(candidate, primary);
            return TryNormalize(projected, 0.0001f, out var normalized)
                ? normalized
                : fallback;
        }

        private static Vector3 BlendAroundPrimary(
            Vector3 current,
            Vector3 target,
            Vector3 primary,
            float alpha)
        {
            if (alpha <= 0f)
            {
                return current;
            }
            if (alpha >= 1f)
            {
                return target;
            }

            var sine = Vector3.Dot(primary, Vector3.Cross(current, target));
            var cosine = Mathf.Clamp(Vector3.Dot(current, target), -1f, 1f);
            var angle = Mathf.Atan2(sine, cosine) * alpha;
            var cosStep = Mathf.Cos(angle);
            var sinStep = Mathf.Sin(angle);
            var rotated = current * cosStep + Vector3.Cross(primary, current) * sinStep;
            return TryNormalize(rotated, 0.0001f, out var normalized)
                ? normalized
                : target;
        }

        private static bool TryNormalize(
            Vector3 value,
            float minimumMagnitude,
            out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value))
            {
                return false;
            }

            var minimum = Mathf.Max(0.000001f, minimumMagnitude);
            if (value.sqrMagnitude < minimum * minimum)
            {
                return false;
            }

            normalized = value.normalized;
            return IsFinite(normalized);
        }

        private static double SanitizeTime(double value, double fallback)
        {
            if (!double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d)
            {
                return value;
            }
            return !double.IsNaN(fallback) && !double.IsInfinity(fallback) && fallback >= 0d
                ? fallback
                : 0d;
        }

        private static float SafeConfidence(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private float SafeMinimumEvidenceConfidence =>
            Mathf.Clamp01(_settings.minimumEvidenceConfidence);
        private float SafeMinimumVectorMagnitude =>
            Mathf.Max(0.000001f, _settings.minimumVectorMagnitude);
        private float SafeMinimumProjectedSecondaryMagnitude =>
            Mathf.Max(0.000001f, _settings.minimumProjectedSecondaryMagnitude);
        private double SafeSecondaryHoldSeconds =>
            Math.Max(0d, _settings.secondaryHoldSeconds);
        private double SafeReferenceFallbackAfterSeconds =>
            Math.Max(SafeSecondaryHoldSeconds, _settings.referenceFallbackAfterSeconds);
        private float SafeReferenceFallbackResponse =>
            Mathf.Max(0.01f, _settings.referenceFallbackResponse);

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
