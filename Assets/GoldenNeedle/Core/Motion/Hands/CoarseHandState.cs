using System;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Hands
{
    public enum CoarseHandState
    {
        Unknown = 0,
        Open = 1,
        Closed = 2,
    }

    public enum CoarseHandUnknownReason
    {
        None = 0,
        NoPose = 1,
        MissingLandmark = 2,
        MissingWorldPosition = 3,
        LowConfidence = 4,
        NonFiniteGeometry = 5,
        DegenerateScale = 6,
        AmbiguousGeometry = 7,
        Stale = 8,
    }

    [Serializable]
    public struct CoarseHandEvidencePoint
    {
        public bool isTracked;
        public bool hasPosition;
        public Vector3 position;
        [Range(0f, 1f)] public float confidence;
    }

    [Serializable]
    public struct CoarseHandPoseEvidence
    {
        public CanonicalHandSide side;
        public long sourceTimestampMillisec;
        public double receivedAtSeconds;
        public bool poseAvailable;
        public CoarseHandEvidencePoint elbow;
        public CoarseHandEvidencePoint wrist;
        public CoarseHandEvidencePoint pinky;
        public CoarseHandEvidencePoint index;
        public CoarseHandEvidencePoint thumb;
    }

    [Serializable]
    public struct CoarseHandMetrics
    {
        public float forearmLengthMeters;
        public float indexExtension;
        public float pinkyExtension;
        public float indexPinkySpread;
        public float thumbExtension;
        public float minimumLandmarkConfidence;
    }

    [Serializable]
    public struct CoarseHandEstimate
    {
        public CoarseHandState state;
        public CoarseHandUnknownReason unknownReason;
        [Range(0f, 1f)] public float evidenceStrength;
        public CoarseHandMetrics metrics;
    }

    [Serializable]
    public struct CoarseHandStateSample
    {
        public CanonicalHandSide side;
        public CoarseHandState state;
        public CoarseHandState instantaneousState;
        public CoarseHandUnknownReason unknownReason;
        [Range(0f, 1f)] public float evidenceStrength;
        public long sourceTimestampMillisec;
        public double receivedAtSeconds;
        public double ageMilliseconds;
        public CoarseHandMetrics metrics;
    }

    [Serializable]
    public struct CoarseHandEstimatorSettings
    {
        [Range(0f, 1f)] public float minimumLandmarkConfidence;
        [Range(0f, 1f)] public float minimumClosedConfidence;
        [Min(0.001f)] public float minimumForearmLengthMeters;
        [Min(0f)] public float openFingerExtensionMinimum;
        [Min(0f)] public float openSpreadMinimum;
        [Min(0f)] public float closedFingerExtensionMaximum;
        [Min(0f)] public float closedSpreadMaximum;
        [Min(0f)] public float closedThumbExtensionMaximum;
        [Min(1)] public int confirmationSamples;
        [Min(1)] public int unknownSamplesToClear;
        [Min(1f)] public float staleAfterMilliseconds;

        public static CoarseHandEstimatorSettings CreateDefault()
        {
            return new CoarseHandEstimatorSettings
            {
                minimumLandmarkConfidence = 0.55f,
                minimumClosedConfidence = 0.70f,
                minimumForearmLengthMeters = 0.04f,
                openFingerExtensionMinimum = 0.55f,
                openSpreadMinimum = 0.30f,
                closedFingerExtensionMaximum = 0.38f,
                closedSpreadMaximum = 0.22f,
                closedThumbExtensionMaximum = 0.45f,
                confirmationSamples = 2,
                unknownSamplesToClear = 2,
                staleAfterMilliseconds = 350f,
            };
        }

        public void Sanitize()
        {
            minimumLandmarkConfidence = Mathf.Clamp01(minimumLandmarkConfidence);
            minimumClosedConfidence = Mathf.Clamp(
                minimumClosedConfidence,
                minimumLandmarkConfidence,
                1f);
            minimumForearmLengthMeters = Mathf.Max(0.001f, minimumForearmLengthMeters);
            closedFingerExtensionMaximum = Mathf.Max(0f, closedFingerExtensionMaximum);
            closedSpreadMaximum = Mathf.Max(0f, closedSpreadMaximum);
            closedThumbExtensionMaximum = Mathf.Max(0f, closedThumbExtensionMaximum);
            openFingerExtensionMinimum = Mathf.Max(
                closedFingerExtensionMaximum + 0.01f,
                openFingerExtensionMinimum);
            openSpreadMinimum = Mathf.Max(
                closedSpreadMaximum + 0.01f,
                openSpreadMinimum);
            confirmationSamples = Math.Max(1, confirmationSamples);
            unknownSamplesToClear = Math.Max(1, unknownSamplesToClear);
            staleAfterMilliseconds = Mathf.Max(1f, staleAfterMilliseconds);
        }
    }

    [Serializable]
    public sealed class CoarseHandFrame
    {
        public string sourceProviderId { get; private set; } = string.Empty;
        public long sourceTimestampMillisec { get; private set; }
        public double evaluationTimeSeconds { get; private set; }
        public bool sourceAvailable { get; private set; }
        public CoarseHandStateSample Left { get; private set; }
        public CoarseHandStateSample Right { get; private set; }

        public void Set(
            string providerId,
            long timestampMillisec,
            double evaluationSeconds,
            bool available,
            in CoarseHandStateSample left,
            in CoarseHandStateSample right)
        {
            sourceProviderId = providerId ?? string.Empty;
            sourceTimestampMillisec = timestampMillisec;
            evaluationTimeSeconds = evaluationSeconds;
            sourceAvailable = available;
            Left = left;
            Right = right;
        }

        public void CopyFrom(CoarseHandFrame source)
        {
            if (source == null)
            {
                Clear();
                return;
            }
            Set(
                source.sourceProviderId,
                source.sourceTimestampMillisec,
                source.evaluationTimeSeconds,
                source.sourceAvailable,
                source.Left,
                source.Right);
        }

        public void Clear()
        {
            sourceProviderId = string.Empty;
            sourceTimestampMillisec = 0L;
            evaluationTimeSeconds = 0d;
            sourceAvailable = false;
            Left = new CoarseHandStateSample
            {
                side = CanonicalHandSide.Left,
                state = CoarseHandState.Unknown,
                instantaneousState = CoarseHandState.Unknown,
                unknownReason = CoarseHandUnknownReason.NoPose,
                ageMilliseconds = double.PositiveInfinity,
            };
            Right = new CoarseHandStateSample
            {
                side = CanonicalHandSide.Right,
                state = CoarseHandState.Unknown,
                instantaneousState = CoarseHandState.Unknown,
                unknownReason = CoarseHandUnknownReason.NoPose,
                ageMilliseconds = double.PositiveInfinity,
            };
        }
    }

    public interface ICoarseHandStateSource
    {
        string CoarseHandSourceId { get; }
        string CoarseHandDiagnosticSummary { get; }
        bool TryCopyLatestCoarseHands(
            double evaluationTimeSeconds,
            CoarseHandFrame destination);
    }
}
