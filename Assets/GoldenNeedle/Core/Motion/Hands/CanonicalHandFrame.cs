using System;
using GoldenNeedle.Core.Motion.Rich;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Hands
{
    public static class CanonicalHandSchema
    {
        public const string Identity = "GoldenNeedle.CanonicalHands";
        public const int Version = 1;
        public const int LandmarkCount = 21;
    }

    public enum CanonicalHandSide
    {
        Unknown = 0,
        Left = 1,
        Right = 2,
    }

    public enum HandLandmarkId
    {
        Wrist = 0,
        ThumbCmc = 1,
        ThumbMcp = 2,
        ThumbIp = 3,
        ThumbTip = 4,
        IndexMcp = 5,
        IndexPip = 6,
        IndexDip = 7,
        IndexTip = 8,
        MiddleMcp = 9,
        MiddlePip = 10,
        MiddleDip = 11,
        MiddleTip = 12,
        RingMcp = 13,
        RingPip = 14,
        RingDip = 15,
        RingTip = 16,
        PinkyMcp = 17,
        PinkyPip = 18,
        PinkyDip = 19,
        PinkyTip = 20,
    }

    public enum HandAssociationMode
    {
        Unavailable = 0,
        PoseWristProximity = 1,
        PoseSingleWrist = 2,
        HandednessFallback = 3,
        Ambiguous = 4,
    }

    public enum HandShapeState
    {
        Unknown = 0,
        Intermediate = 1,
        Open = 2,
        Fist = 3,
    }

    [Serializable]
    public struct CanonicalHandLandmark
    {
        public HandLandmarkId id;
        public bool hasImagePosition;
        public Vector2 imagePosition;
        public bool hasHandLocalPosition;
        public Vector3 handLocalPosition;
        [Range(0f, 1f)] public float confidence;

        public bool IsValid => hasImagePosition || hasHandLocalPosition;
    }

    [Serializable]
    public struct HandScalarFeature
    {
        public bool isValid;
        [Range(0f, 1f)] public float value;
        [Range(0f, 1f)] public float confidence;
    }

    [Serializable]
    public struct HandBooleanFeature
    {
        public bool isKnown;
        public bool value;
        [Range(0f, 1f)] public float confidence;
    }

    [Serializable]
    public sealed class CanonicalHand
    {
        private readonly CanonicalHandLandmark[] _landmarks =
            new CanonicalHandLandmark[CanonicalHandSchema.LandmarkCount];

        public CanonicalHandSide side { get; private set; }
        public bool isDetected { get; private set; }
        public bool isFresh { get; private set; }
        public HandAssociationMode associationMode { get; private set; }
        public float associationConfidence { get; private set; }
        public CanonicalHandSide handednessEvidence { get; private set; }
        public float handednessScore { get; private set; }
        public long sourceTimestampMillisec { get; private set; }
        public double receivedAtSeconds { get; private set; }
        public double ageMilliseconds { get; private set; }
        public double bodyHandSkewMilliseconds { get; private set; }
        public int trackedLandmarkCount { get; private set; }
        public CanonicalAnatomicalBasis palmBasis { get; private set; }
        public HandScalarFeature thumbCurl { get; private set; }
        public HandScalarFeature indexCurl { get; private set; }
        public HandScalarFeature middleCurl { get; private set; }
        public HandScalarFeature ringCurl { get; private set; }
        public HandScalarFeature pinkyCurl { get; private set; }
        public HandShapeState shape { get; private set; }
        public HandBooleanFeature pointing { get; private set; }

        public CanonicalHand(CanonicalHandSide side)
        {
            Clear(side);
        }

        public CanonicalHandLandmark GetLandmark(HandLandmarkId id)
        {
            return _landmarks[(int)id];
        }

        public void Begin(
            CanonicalHandSide handSide,
            long timestampMillisec,
            double receivedTimeSeconds,
            CanonicalHandSide handedness,
            float handednessConfidence,
            HandAssociationMode mode,
            float associationScore)
        {
            side = handSide;
            sourceTimestampMillisec = timestampMillisec;
            receivedAtSeconds = receivedTimeSeconds;
            handednessEvidence = handedness;
            handednessScore = Mathf.Clamp01(handednessConfidence);
            associationMode = mode;
            associationConfidence = Mathf.Clamp01(associationScore);
            isDetected = true;
            isFresh = false;
            ageMilliseconds = double.PositiveInfinity;
            bodyHandSkewMilliseconds = double.PositiveInfinity;
            trackedLandmarkCount = 0;
            palmBasis = default;
            thumbCurl = default;
            indexCurl = default;
            middleCurl = default;
            ringCurl = default;
            pinkyCurl = default;
            shape = HandShapeState.Unknown;
            pointing = default;
            ClearLandmarks();
        }

        public void SetLandmark(in CanonicalHandLandmark landmark)
        {
            var index = (int)landmark.id;
            if (index < 0 || index >= _landmarks.Length)
            {
                return;
            }

            if (_landmarks[index].IsValid)
            {
                trackedLandmarkCount--;
            }
            _landmarks[index] = landmark;
            if (landmark.IsValid)
            {
                trackedLandmarkCount++;
            }
        }

        public void SetDerived(
            in CanonicalAnatomicalBasis basis,
            in HandScalarFeature thumb,
            in HandScalarFeature index,
            in HandScalarFeature middle,
            in HandScalarFeature ring,
            in HandScalarFeature pinky,
            HandShapeState shapeState,
            in HandBooleanFeature pointingState)
        {
            palmBasis = basis;
            thumbCurl = thumb;
            indexCurl = index;
            middleCurl = middle;
            ringCurl = ring;
            pinkyCurl = pinky;
            shape = shapeState;
            pointing = pointingState;
        }

        public void EvaluateFreshness(double evaluationTimeSeconds, long bodyTimestampMillisec, HandFreshnessSettings settings)
        {
            ageMilliseconds = receivedAtSeconds <= 0d
                ? double.PositiveInfinity
                : Math.Max(0d, (evaluationTimeSeconds - receivedAtSeconds) * 1000d);
            bodyHandSkewMilliseconds = bodyTimestampMillisec <= 0 || sourceTimestampMillisec <= 0
                ? double.PositiveInfinity
                : Math.Abs(bodyTimestampMillisec - sourceTimestampMillisec);
            isFresh = isDetected &&
                ageMilliseconds <= settings.maximumHandAgeMilliseconds &&
                bodyHandSkewMilliseconds <= settings.maximumBodyHandSkewMilliseconds;
        }

        public void CopyFrom(CanonicalHand source)
        {
            side = source.side;
            isDetected = source.isDetected;
            isFresh = source.isFresh;
            associationMode = source.associationMode;
            associationConfidence = source.associationConfidence;
            handednessEvidence = source.handednessEvidence;
            handednessScore = source.handednessScore;
            sourceTimestampMillisec = source.sourceTimestampMillisec;
            receivedAtSeconds = source.receivedAtSeconds;
            ageMilliseconds = source.ageMilliseconds;
            bodyHandSkewMilliseconds = source.bodyHandSkewMilliseconds;
            trackedLandmarkCount = source.trackedLandmarkCount;
            palmBasis = source.palmBasis;
            thumbCurl = source.thumbCurl;
            indexCurl = source.indexCurl;
            middleCurl = source.middleCurl;
            ringCurl = source.ringCurl;
            pinkyCurl = source.pinkyCurl;
            shape = source.shape;
            pointing = source.pointing;
            Array.Copy(source._landmarks, _landmarks, _landmarks.Length);
        }

        public void MarkUnavailable(bool preserveSample)
        {
            isFresh = false;
            if (!preserveSample)
            {
                isDetected = false;
                associationMode = HandAssociationMode.Unavailable;
                associationConfidence = 0f;
                trackedLandmarkCount = 0;
                palmBasis = default;
                thumbCurl = default;
                indexCurl = default;
                middleCurl = default;
                ringCurl = default;
                pinkyCurl = default;
                shape = HandShapeState.Unknown;
                pointing = default;
                ClearLandmarks();
            }
        }

        public void Clear(CanonicalHandSide handSide)
        {
            side = handSide;
            isDetected = false;
            isFresh = false;
            associationMode = HandAssociationMode.Unavailable;
            associationConfidence = 0f;
            handednessEvidence = CanonicalHandSide.Unknown;
            handednessScore = 0f;
            sourceTimestampMillisec = 0;
            receivedAtSeconds = 0d;
            ageMilliseconds = double.PositiveInfinity;
            bodyHandSkewMilliseconds = double.PositiveInfinity;
            trackedLandmarkCount = 0;
            palmBasis = default;
            thumbCurl = default;
            indexCurl = default;
            middleCurl = default;
            ringCurl = default;
            pinkyCurl = default;
            shape = HandShapeState.Unknown;
            pointing = default;
            ClearLandmarks();
        }

        private void ClearLandmarks()
        {
            for (var i = 0; i < _landmarks.Length; i++)
            {
                _landmarks[i] = new CanonicalHandLandmark { id = (HandLandmarkId)i };
            }
        }
    }

    [Serializable]
    public struct HandFreshnessSettings
    {
        [Min(1f)] public float maximumHandAgeMilliseconds;
        [Min(1f)] public float maximumBodyHandSkewMilliseconds;

        public static HandFreshnessSettings CreateDefault()
        {
            return new HandFreshnessSettings
            {
                maximumHandAgeMilliseconds = 350f,
                maximumBodyHandSkewMilliseconds = 200f,
            };
        }

        public void Sanitize()
        {
            maximumHandAgeMilliseconds = Mathf.Max(1f, maximumHandAgeMilliseconds);
            maximumBodyHandSkewMilliseconds = Mathf.Max(1f, maximumBodyHandSkewMilliseconds);
        }
    }

    [Serializable]
    public sealed class CanonicalHandFrame
    {
        private readonly CanonicalHand _left = new CanonicalHand(CanonicalHandSide.Left);
        private readonly CanonicalHand _right = new CanonicalHand(CanonicalHandSide.Right);

        public string schemaIdentity => CanonicalHandSchema.Identity;
        public int schemaVersion => CanonicalHandSchema.Version;
        public string sourceProviderId { get; private set; }
        public long bodySourceTimestampMillisec { get; private set; }
        public double evaluationTimeSeconds { get; private set; }
        public bool sourceAvailable { get; private set; }
        public int freshHandCount => (_left.isFresh ? 1 : 0) + (_right.isFresh ? 1 : 0);
        public CanonicalHand Left => _left;
        public CanonicalHand Right => _right;

        public CanonicalHand GetHand(CanonicalHandSide side)
        {
            if (side == CanonicalHandSide.Left) return _left;
            if (side == CanonicalHandSide.Right) return _right;
            return null;
        }

        public void Begin(string providerId, long bodyTimestampMillisec, double evaluationSeconds, bool available)
        {
            sourceProviderId = providerId ?? string.Empty;
            bodySourceTimestampMillisec = bodyTimestampMillisec;
            evaluationTimeSeconds = evaluationSeconds;
            sourceAvailable = available;
        }

        public void CopyHand(CanonicalHand source)
        {
            if (source == null || source.side == CanonicalHandSide.Unknown)
            {
                return;
            }
            GetHand(source.side).CopyFrom(source);
        }

        public void Clear()
        {
            sourceProviderId = string.Empty;
            bodySourceTimestampMillisec = 0;
            evaluationTimeSeconds = 0d;
            sourceAvailable = false;
            _left.Clear(CanonicalHandSide.Left);
            _right.Clear(CanonicalHandSide.Right);
        }
    }
}
