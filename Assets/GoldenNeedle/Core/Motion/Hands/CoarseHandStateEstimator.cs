using System;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Hands
{
    public static class CoarseHandStateEstimator
    {
        public static CoarseHandEstimate Estimate(
            in CoarseHandPoseEvidence evidence,
            CoarseHandEstimatorSettings settings)
        {
            settings.Sanitize();
            if (!evidence.poseAvailable)
            {
                return Unknown(CoarseHandUnknownReason.NoPose);
            }

            if (!AllTracked(in evidence))
            {
                return Unknown(CoarseHandUnknownReason.MissingLandmark);
            }

            if (!AllHavePositions(in evidence))
            {
                return Unknown(CoarseHandUnknownReason.MissingWorldPosition);
            }

            if (!AllFinite(in evidence))
            {
                return Unknown(CoarseHandUnknownReason.NonFiniteGeometry);
            }

            var minimumConfidence = MinimumConfidence(in evidence);
            if (!IsFinite(minimumConfidence) || minimumConfidence < settings.minimumLandmarkConfidence)
            {
                return Unknown(
                    CoarseHandUnknownReason.LowConfidence,
                    minimumConfidence: IsFinite(minimumConfidence) ? minimumConfidence : 0f);
            }

            var forearmLength = (evidence.wrist.position - evidence.elbow.position).magnitude;
            if (!IsFinite(forearmLength) || forearmLength < settings.minimumForearmLengthMeters)
            {
                return Unknown(
                    CoarseHandUnknownReason.DegenerateScale,
                    minimumConfidence: minimumConfidence,
                    forearmLength: IsFinite(forearmLength) ? forearmLength : 0f);
            }

            var inverseScale = 1f / forearmLength;
            var metrics = new CoarseHandMetrics
            {
                forearmLengthMeters = forearmLength,
                indexExtension =
                    (evidence.index.position - evidence.wrist.position).magnitude * inverseScale,
                pinkyExtension =
                    (evidence.pinky.position - evidence.wrist.position).magnitude * inverseScale,
                indexPinkySpread =
                    (evidence.index.position - evidence.pinky.position).magnitude * inverseScale,
                thumbExtension =
                    (evidence.thumb.position - evidence.wrist.position).magnitude * inverseScale,
                minimumLandmarkConfidence = minimumConfidence,
            };

            if (!MetricsFinite(in metrics))
            {
                return new CoarseHandEstimate
                {
                    state = CoarseHandState.Unknown,
                    unknownReason = CoarseHandUnknownReason.NonFiniteGeometry,
                    evidenceStrength = 0f,
                    metrics = metrics,
                };
            }

            var minimumFingerExtension = Mathf.Min(metrics.indexExtension, metrics.pinkyExtension);
            if (minimumFingerExtension >= settings.openFingerExtensionMinimum &&
                metrics.indexPinkySpread >= settings.openSpreadMinimum)
            {
                var extensionMargin = PositiveMargin(
                    minimumFingerExtension,
                    settings.openFingerExtensionMinimum);
                var spreadMargin = PositiveMargin(
                    metrics.indexPinkySpread,
                    settings.openSpreadMinimum);
                return new CoarseHandEstimate
                {
                    state = CoarseHandState.Open,
                    unknownReason = CoarseHandUnknownReason.None,
                    evidenceStrength = Mathf.Clamp01(
                        minimumConfidence * Mathf.Min(extensionMargin, spreadMargin)),
                    metrics = metrics,
                };
            }

            var maximumFingerExtension = Mathf.Max(metrics.indexExtension, metrics.pinkyExtension);
            if (minimumConfidence >= settings.minimumClosedConfidence &&
                maximumFingerExtension <= settings.closedFingerExtensionMaximum &&
                metrics.indexPinkySpread <= settings.closedSpreadMaximum &&
                metrics.thumbExtension <= settings.closedThumbExtensionMaximum)
            {
                var extensionMargin = NegativeMargin(
                    maximumFingerExtension,
                    settings.closedFingerExtensionMaximum);
                var spreadMargin = NegativeMargin(
                    metrics.indexPinkySpread,
                    settings.closedSpreadMaximum);
                var thumbMargin = NegativeMargin(
                    metrics.thumbExtension,
                    settings.closedThumbExtensionMaximum);
                return new CoarseHandEstimate
                {
                    state = CoarseHandState.Closed,
                    unknownReason = CoarseHandUnknownReason.None,
                    evidenceStrength = Mathf.Clamp01(
                        minimumConfidence * Mathf.Min(
                            extensionMargin,
                            Mathf.Min(spreadMargin, thumbMargin))),
                    metrics = metrics,
                };
            }

            return new CoarseHandEstimate
            {
                state = CoarseHandState.Unknown,
                unknownReason = CoarseHandUnknownReason.AmbiguousGeometry,
                evidenceStrength = 0f,
                metrics = metrics,
            };
        }

        private static CoarseHandEstimate Unknown(
            CoarseHandUnknownReason reason,
            float minimumConfidence = 0f,
            float forearmLength = 0f)
        {
            return new CoarseHandEstimate
            {
                state = CoarseHandState.Unknown,
                unknownReason = reason,
                evidenceStrength = 0f,
                metrics = new CoarseHandMetrics
                {
                    minimumLandmarkConfidence = minimumConfidence,
                    forearmLengthMeters = forearmLength,
                },
            };
        }

        private static bool AllTracked(in CoarseHandPoseEvidence evidence)
        {
            return evidence.elbow.isTracked &&
                evidence.wrist.isTracked &&
                evidence.pinky.isTracked &&
                evidence.index.isTracked &&
                evidence.thumb.isTracked;
        }

        private static bool AllHavePositions(in CoarseHandPoseEvidence evidence)
        {
            return evidence.elbow.hasPosition &&
                evidence.wrist.hasPosition &&
                evidence.pinky.hasPosition &&
                evidence.index.hasPosition &&
                evidence.thumb.hasPosition;
        }

        private static bool AllFinite(in CoarseHandPoseEvidence evidence)
        {
            return IsFinite(evidence.elbow.position) &&
                IsFinite(evidence.wrist.position) &&
                IsFinite(evidence.pinky.position) &&
                IsFinite(evidence.index.position) &&
                IsFinite(evidence.thumb.position) &&
                IsFinite(evidence.elbow.confidence) &&
                IsFinite(evidence.wrist.confidence) &&
                IsFinite(evidence.pinky.confidence) &&
                IsFinite(evidence.index.confidence) &&
                IsFinite(evidence.thumb.confidence);
        }

        private static float MinimumConfidence(in CoarseHandPoseEvidence evidence)
        {
            return Mathf.Min(
                Mathf.Min(evidence.elbow.confidence, evidence.wrist.confidence),
                Mathf.Min(
                    evidence.pinky.confidence,
                    Mathf.Min(evidence.index.confidence, evidence.thumb.confidence)));
        }

        private static bool MetricsFinite(in CoarseHandMetrics metrics)
        {
            return IsFinite(metrics.forearmLengthMeters) &&
                IsFinite(metrics.indexExtension) &&
                IsFinite(metrics.pinkyExtension) &&
                IsFinite(metrics.indexPinkySpread) &&
                IsFinite(metrics.thumbExtension) &&
                IsFinite(metrics.minimumLandmarkConfidence);
        }

        private static float PositiveMargin(float value, float threshold)
        {
            var denominator = Mathf.Max(0.05f, threshold * 0.5f);
            return Mathf.Clamp01((value - threshold) / denominator + 0.5f);
        }

        private static float NegativeMargin(float value, float threshold)
        {
            var denominator = Mathf.Max(0.05f, threshold * 0.5f);
            return Mathf.Clamp01((threshold - value) / denominator + 0.5f);
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

    public sealed class CoarseHandStateTracker
    {
        private readonly SideTracker _left = new SideTracker(CanonicalHandSide.Left);
        private readonly SideTracker _right = new SideTracker(CanonicalHandSide.Right);

        public void Update(
            string providerId,
            in CoarseHandPoseEvidence leftEvidence,
            in CoarseHandPoseEvidence rightEvidence,
            double evaluationTimeSeconds,
            CoarseHandEstimatorSettings settings,
            CoarseHandFrame destination)
        {
            if (destination == null)
            {
                return;
            }

            settings.Sanitize();
            _left.Process(in leftEvidence, evaluationTimeSeconds, settings);
            _right.Process(in rightEvidence, evaluationTimeSeconds, settings);
            WriteFrame(providerId, evaluationTimeSeconds, destination);
        }

        public void Refresh(
            string providerId,
            double evaluationTimeSeconds,
            CoarseHandEstimatorSettings settings,
            CoarseHandFrame destination)
        {
            if (destination == null)
            {
                return;
            }

            settings.Sanitize();
            _left.RefreshAge(evaluationTimeSeconds, settings);
            _right.RefreshAge(evaluationTimeSeconds, settings);
            WriteFrame(providerId, evaluationTimeSeconds, destination);
        }

        public void Reset(CoarseHandFrame destination = null)
        {
            _left.Reset();
            _right.Reset();
            destination?.Clear();
        }

        private void WriteFrame(
            string providerId,
            double evaluationTimeSeconds,
            CoarseHandFrame destination)
        {
            var left = _left.Sample;
            var right = _right.Sample;
            var timestamp = Math.Max(left.sourceTimestampMillisec, right.sourceTimestampMillisec);
            destination.Set(
                providerId,
                timestamp,
                evaluationTimeSeconds,
                timestamp > 0L,
                left,
                right);
        }

        private sealed class SideTracker
        {
            private readonly CanonicalHandSide _side;
            private CoarseHandState _stableState;
            private CoarseHandState _candidateState;
            private int _candidateCount;
            private int _unknownCount;
            private long _lastTimestamp;
            private CoarseHandStateSample _sample;

            public SideTracker(CanonicalHandSide side)
            {
                _side = side;
                Reset();
            }

            public CoarseHandStateSample Sample => _sample;

            public void Process(
                in CoarseHandPoseEvidence evidence,
                double evaluationTimeSeconds,
                CoarseHandEstimatorSettings settings)
            {
                if (evidence.sourceTimestampMillisec <= 0L)
                {
                    ApplyUnavailable(
                        CoarseHandUnknownReason.NoPose,
                        evidence.receivedAtSeconds,
                        evaluationTimeSeconds,
                        settings,
                        false);
                    return;
                }

                if (_lastTimestamp > 0L && evidence.sourceTimestampMillisec < _lastTimestamp)
                {
                    ResetTemporalState();
                }

                if (evidence.sourceTimestampMillisec == _lastTimestamp)
                {
                    RefreshAge(evaluationTimeSeconds, settings);
                    return;
                }

                _lastTimestamp = evidence.sourceTimestampMillisec;
                var estimate = CoarseHandStateEstimator.Estimate(in evidence, settings);
                ApplyEstimate(
                    in evidence,
                    in estimate,
                    evaluationTimeSeconds,
                    settings);
            }

            public void RefreshAge(
                double evaluationTimeSeconds,
                CoarseHandEstimatorSettings settings)
            {
                if (_sample.sourceTimestampMillisec <= 0L || _sample.receivedAtSeconds <= 0d)
                {
                    return;
                }

                var ageMilliseconds = Math.Max(
                    0d,
                    (evaluationTimeSeconds - _sample.receivedAtSeconds) * 1000d);
                _sample.ageMilliseconds = ageMilliseconds;
                if (ageMilliseconds > settings.staleAfterMilliseconds)
                {
                    _stableState = CoarseHandState.Unknown;
                    _candidateState = CoarseHandState.Unknown;
                    _candidateCount = 0;
                    _unknownCount = 0;
                    _sample.state = CoarseHandState.Unknown;
                    _sample.instantaneousState = CoarseHandState.Unknown;
                    _sample.unknownReason = CoarseHandUnknownReason.Stale;
                    _sample.evidenceStrength = 0f;
                }
            }

            public void Reset()
            {
                _stableState = CoarseHandState.Unknown;
                _candidateState = CoarseHandState.Unknown;
                _candidateCount = 0;
                _unknownCount = 0;
                _lastTimestamp = 0L;
                _sample = new CoarseHandStateSample
                {
                    side = _side,
                    state = CoarseHandState.Unknown,
                    instantaneousState = CoarseHandState.Unknown,
                    unknownReason = CoarseHandUnknownReason.NoPose,
                    ageMilliseconds = double.PositiveInfinity,
                };
            }

            private void ApplyEstimate(
                in CoarseHandPoseEvidence evidence,
                in CoarseHandEstimate estimate,
                double evaluationTimeSeconds,
                CoarseHandEstimatorSettings settings)
            {
                _sample.side = _side;
                _sample.sourceTimestampMillisec = evidence.sourceTimestampMillisec;
                _sample.receivedAtSeconds = evidence.receivedAtSeconds;
                _sample.instantaneousState = estimate.state;
                _sample.unknownReason = estimate.unknownReason;
                _sample.evidenceStrength = estimate.evidenceStrength;
                _sample.metrics = estimate.metrics;
                _sample.ageMilliseconds = evidence.receivedAtSeconds <= 0d
                    ? double.PositiveInfinity
                    : Math.Max(0d, (evaluationTimeSeconds - evidence.receivedAtSeconds) * 1000d);

                if (_sample.ageMilliseconds > settings.staleAfterMilliseconds)
                {
                    _stableState = CoarseHandState.Unknown;
                    _candidateState = CoarseHandState.Unknown;
                    _candidateCount = 0;
                    _unknownCount = 0;
                    _sample.state = CoarseHandState.Unknown;
                    _sample.instantaneousState = CoarseHandState.Unknown;
                    _sample.unknownReason = CoarseHandUnknownReason.Stale;
                    _sample.evidenceStrength = 0f;
                    return;
                }

                if (estimate.state == CoarseHandState.Unknown)
                {
                    _candidateState = CoarseHandState.Unknown;
                    _candidateCount = 0;
                    _unknownCount++;
                    if (_unknownCount >= settings.unknownSamplesToClear)
                    {
                        _stableState = CoarseHandState.Unknown;
                    }
                    _sample.state = _stableState;
                    return;
                }

                _unknownCount = 0;
                if (_stableState == estimate.state)
                {
                    _candidateState = CoarseHandState.Unknown;
                    _candidateCount = 0;
                    _sample.state = _stableState;
                    return;
                }

                if (_candidateState == estimate.state)
                {
                    _candidateCount++;
                }
                else
                {
                    _candidateState = estimate.state;
                    _candidateCount = 1;
                }

                if (_candidateCount >= settings.confirmationSamples)
                {
                    _stableState = estimate.state;
                    _candidateState = CoarseHandState.Unknown;
                    _candidateCount = 0;
                }
                _sample.state = _stableState;
            }

            private void ApplyUnavailable(
                CoarseHandUnknownReason reason,
                double receivedAtSeconds,
                double evaluationTimeSeconds,
                CoarseHandEstimatorSettings settings,
                bool countAsFreshSample)
            {
                var evidence = new CoarseHandPoseEvidence
                {
                    side = _side,
                    sourceTimestampMillisec = _lastTimestamp,
                    receivedAtSeconds = receivedAtSeconds,
                    poseAvailable = false,
                };
                var estimate = new CoarseHandEstimate
                {
                    state = CoarseHandState.Unknown,
                    unknownReason = reason,
                };
                if (countAsFreshSample)
                {
                    ApplyEstimate(in evidence, in estimate, evaluationTimeSeconds, settings);
                }
                else
                {
                    _sample.instantaneousState = CoarseHandState.Unknown;
                    _sample.unknownReason = reason;
                    _sample.evidenceStrength = 0f;
                    RefreshAge(evaluationTimeSeconds, settings);
                }
            }

            private void ResetTemporalState()
            {
                _stableState = CoarseHandState.Unknown;
                _candidateState = CoarseHandState.Unknown;
                _candidateCount = 0;
                _unknownCount = 0;
                _lastTimestamp = 0L;
            }
        }
    }
}
