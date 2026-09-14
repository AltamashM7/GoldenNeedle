using System;
using GoldenNeedle.Core.Motion.Canonical;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Rich
{
    public static class RichMotionSchema
    {
        public const string Identity = "GoldenNeedle.RichMotion";
        public const int Version = 1;
    }

    public enum RichMotionEvidenceId
    {
        Pelvis = 0,
        Chest = 1,
        LeftShoulder = 2,
        RightShoulder = 3,
        LeftElbow = 4,
        RightElbow = 5,
        LeftWrist = 6,
        RightWrist = 7,
        LeftHip = 8,
        RightHip = 9,
        LeftKnee = 10,
        RightKnee = 11,
        LeftAnkle = 12,
        RightAnkle = 13,
        LeftHeel = 14,
        RightHeel = 15,
        LeftToe = 16,
        RightToe = 17,
        LeftThumb = 18,
        LeftPinky = 19,
        RightThumb = 20,
        RightPinky = 21,
    }

    public enum RichOrientationChannelId
    {
        Pelvis = 0,
        Chest = 1,
        LeftUpperArm = 2,
        LeftLowerArm = 3,
        RightUpperArm = 4,
        RightLowerArm = 5,
        LeftUpperLeg = 6,
        LeftLowerLeg = 7,
        RightUpperLeg = 8,
        RightLowerLeg = 9,
        LeftFoot = 10,
        RightFoot = 11,
    }

    public enum CanonicalAnatomicalHandedness
    {
        Unknown = 0,
        RightHanded = 1,
        LeftHanded = -1,
    }

    public enum RichTwistObservability
    {
        Unobservable = 0,
        Observed = 1,
        Held = 2,
        ReferenceFallback = 3,
    }

    [Serializable]
    public struct RichMotionEvidencePoint
    {
        public RichMotionEvidenceId id;
        public CanonicalTrackingState tracking;
        [Range(0f, 1f)] public float confidence;
        public Vector3 position;
        public bool hasPosition;

        public bool IsTracked =>
            tracking == CanonicalTrackingState.Tracked && hasPosition;
    }

    /// <summary>
    /// Provider-independent anatomical basis. These three explicit axes are the rich canonical
    /// orientation authority; quaternions are intentionally absent from this contract.
    /// </summary>
    [Serializable]
    public struct CanonicalAnatomicalBasis
    {
        public Vector3 primaryAxis;
        public Vector3 secondaryAxis;
        public Vector3 thirdAxis;
        public float determinant;
        public CanonicalAnatomicalHandedness handedness;
        public bool isValid;
    }

    [Serializable]
    public struct RichOrientationChannel
    {
        public RichOrientationChannelId id;
        public CanonicalTrackingState tracking;
        public bool hasPrimaryAxis;
        public Vector3 primaryAxis;
        [Range(0f, 1f)] public float swingConfidence;
        public CanonicalAnatomicalBasis basis;
        [Range(0f, 1f)] public float orientationConfidence;
        [Range(0f, 1f)] public float twistConfidence;
        public RichTwistObservability twistState;

        public bool HasSwing =>
            tracking == CanonicalTrackingState.Tracked && hasPrimaryAxis;
        public bool HasFullOrientation => HasSwing && basis.isValid;
    }

    /// <summary>
    /// Optional provider-to-core rich evidence frame. It contains only semantic Golden Needle
    /// evidence points; provider landmark indices and provider-native types never cross this boundary.
    /// </summary>
    [Serializable]
    public sealed class RichMotionEvidenceFrame
    {
        public const int EvidenceCount = 22;

        private readonly RichMotionEvidencePoint[] _evidence =
            new RichMotionEvidencePoint[EvidenceCount];

        public string schemaIdentity => RichMotionSchema.Identity;
        public int schemaVersion => RichMotionSchema.Version;
        public string sourceProviderId { get; private set; }
        public long sourceTimestampMillisec { get; private set; }
        public double receivedAtSeconds { get; private set; }
        public double evaluationTimeSeconds { get; private set; }
        public bool sourceAvailable { get; private set; }
        public bool hasEvidence { get; private set; }
        public int trackedEvidenceCount { get; private set; }

        public RichMotionEvidenceFrame()
        {
            Clear();
        }

        public RichMotionEvidencePoint GetEvidence(RichMotionEvidenceId id)
        {
            return _evidence[(int)id];
        }

        public void Begin(
            string providerId,
            long timestampMillisec,
            double receivedTimeSeconds,
            double evaluationSeconds,
            bool isSourceAvailable)
        {
            sourceProviderId = providerId ?? string.Empty;
            sourceTimestampMillisec = timestampMillisec;
            receivedAtSeconds = receivedTimeSeconds;
            evaluationTimeSeconds = evaluationSeconds;
            sourceAvailable = isSourceAvailable;
            hasEvidence = false;
            trackedEvidenceCount = 0;
            ClearEvidence();
        }

        public void SetEvidence(in RichMotionEvidencePoint evidence)
        {
            var index = (int)evidence.id;
            if (index < 0 || index >= _evidence.Length)
            {
                return;
            }

            if (_evidence[index].IsTracked)
            {
                trackedEvidenceCount--;
            }

            _evidence[index] = evidence;
            if (evidence.IsTracked)
            {
                trackedEvidenceCount++;
            }
        }

        public void Complete()
        {
            hasEvidence = trackedEvidenceCount > 0;
        }

        public void Clear()
        {
            sourceProviderId = string.Empty;
            sourceTimestampMillisec = 0L;
            receivedAtSeconds = 0d;
            evaluationTimeSeconds = 0d;
            sourceAvailable = false;
            hasEvidence = false;
            trackedEvidenceCount = 0;
            ClearEvidence();
        }

        private void ClearEvidence()
        {
            for (var i = 0; i < _evidence.Length; i++)
            {
                _evidence[i] = new RichMotionEvidencePoint
                {
                    id = (RichMotionEvidenceId)i,
                    tracking = CanonicalTrackingState.Unavailable,
                    confidence = 0f,
                    position = Vector3.zero,
                    hasPosition = false,
                };
            }
        }
    }

    /// <summary>
    /// Rich canonical motion output. It carries semantic position evidence and anatomical basis
    /// channels while remaining additive to CanonicalBodyV1 and the legacy swing-only rotation path.
    /// </summary>
    [Serializable]
    public sealed class RichMotionFrame
    {
        public const int OrientationChannelCount = 12;

        private readonly RichMotionEvidencePoint[] _evidence =
            new RichMotionEvidencePoint[RichMotionEvidenceFrame.EvidenceCount];
        private readonly RichOrientationChannel[] _orientations =
            new RichOrientationChannel[OrientationChannelCount];

        public string schemaIdentity => RichMotionSchema.Identity;
        public int schemaVersion => RichMotionSchema.Version;
        public string sourceProviderId { get; private set; }
        public long sourceTimestampMillisec { get; private set; }
        public double receivedAtSeconds { get; private set; }
        public double evaluationTimeSeconds { get; private set; }
        public bool sourceAvailable { get; private set; }
        public bool hasEvidence { get; private set; }
        public int trackedEvidenceCount { get; private set; }
        public int swingObservableCount { get; private set; }
        public int validOrientationCount { get; private set; }
        public int observedTwistCount { get; private set; }
        public int heldTwistCount { get; private set; }
        public int referenceFallbackTwistCount { get; private set; }
        public int unobservableTwistCount { get; private set; }

        public RichMotionFrame()
        {
            Clear();
        }

        public RichMotionEvidencePoint GetEvidence(RichMotionEvidenceId id)
        {
            return _evidence[(int)id];
        }

        public RichOrientationChannel GetOrientation(RichOrientationChannelId id)
        {
            return _orientations[(int)id];
        }

        public void BeginFromEvidence(RichMotionEvidenceFrame source)
        {
            if (source == null)
            {
                Clear();
                return;
            }

            sourceProviderId = source.sourceProviderId;
            sourceTimestampMillisec = source.sourceTimestampMillisec;
            receivedAtSeconds = source.receivedAtSeconds;
            evaluationTimeSeconds = source.evaluationTimeSeconds;
            sourceAvailable = source.sourceAvailable;
            hasEvidence = source.hasEvidence;
            trackedEvidenceCount = source.trackedEvidenceCount;
            for (var i = 0; i < _evidence.Length; i++)
            {
                _evidence[i] = source.GetEvidence((RichMotionEvidenceId)i);
            }

            ClearOrientations();
        }

        public void SetOrientation(in RichOrientationChannel orientation)
        {
            var index = (int)orientation.id;
            if (index < 0 || index >= _orientations.Length)
            {
                return;
            }

            _orientations[index] = orientation;
        }

        public void Complete()
        {
            swingObservableCount = 0;
            validOrientationCount = 0;
            observedTwistCount = 0;
            heldTwistCount = 0;
            referenceFallbackTwistCount = 0;
            unobservableTwistCount = 0;

            for (var i = 0; i < _orientations.Length; i++)
            {
                var orientation = _orientations[i];
                if (orientation.HasSwing)
                {
                    swingObservableCount++;
                }
                if (orientation.HasFullOrientation)
                {
                    validOrientationCount++;
                }

                switch (orientation.twistState)
                {
                    case RichTwistObservability.Observed:
                        observedTwistCount++;
                        break;
                    case RichTwistObservability.Held:
                        heldTwistCount++;
                        break;
                    case RichTwistObservability.ReferenceFallback:
                        referenceFallbackTwistCount++;
                        break;
                    default:
                        unobservableTwistCount++;
                        break;
                }
            }
        }

        public void Clear()
        {
            sourceProviderId = string.Empty;
            sourceTimestampMillisec = 0L;
            receivedAtSeconds = 0d;
            evaluationTimeSeconds = 0d;
            sourceAvailable = false;
            hasEvidence = false;
            trackedEvidenceCount = 0;
            for (var i = 0; i < _evidence.Length; i++)
            {
                _evidence[i] = new RichMotionEvidencePoint
                {
                    id = (RichMotionEvidenceId)i,
                    tracking = CanonicalTrackingState.Unavailable,
                };
            }
            ClearOrientations();
            Complete();
            unobservableTwistCount = 0;
        }

        private void ClearOrientations()
        {
            swingObservableCount = 0;
            validOrientationCount = 0;
            observedTwistCount = 0;
            heldTwistCount = 0;
            referenceFallbackTwistCount = 0;
            unobservableTwistCount = 0;
            for (var i = 0; i < _orientations.Length; i++)
            {
                _orientations[i] = new RichOrientationChannel
                {
                    id = (RichOrientationChannelId)i,
                    tracking = CanonicalTrackingState.Unavailable,
                    twistState = RichTwistObservability.Unobservable,
                };
            }
        }
    }
}
