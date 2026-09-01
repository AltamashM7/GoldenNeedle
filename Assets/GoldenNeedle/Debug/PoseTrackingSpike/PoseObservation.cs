using System;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    public enum LandmarkTrust
    {
        Unavailable = 0,
        Tracked = 1,
    }

    [Serializable]
    public struct PoseLandmarkObservation
    {
        public int index;
        public float x;
        public float y;
        public float z;
        public float worldX;
        public float worldY;
        public float worldZ;
        public float visibility;
        public float presence;
        public bool hasVisibility;
        public bool hasPresence;
        public bool hasWorldCoordinates;
        public LandmarkTrust trust;

        public bool IsTracked => trust == LandmarkTrust.Tracked;
    }

    [Serializable]
    public sealed class PoseObservation
    {
        public const int LandmarkCount = 33;

        private readonly PoseLandmarkObservation[] _landmarks = new PoseLandmarkObservation[LandmarkCount];

        public long sourceTimestampMillisec { get; private set; }
        public double receivedAtSeconds { get; private set; }
        public bool hasPose { get; private set; }
        public int trustedCount { get; private set; }

        public PoseObservation()
        {
            Clear();
        }

        public PoseLandmarkObservation GetLandmark(int index)
        {
            return _landmarks[index];
        }

        public void Begin(long timestampMillisec, double receivedTimeSeconds, bool poseAvailable)
        {
            sourceTimestampMillisec = timestampMillisec;
            receivedAtSeconds = receivedTimeSeconds;
            hasPose = poseAvailable;
            trustedCount = 0;

            for (var i = 0; i < _landmarks.Length; i++)
            {
                _landmarks[i] = new PoseLandmarkObservation
                {
                    index = i,
                    trust = LandmarkTrust.Unavailable,
                };
            }
        }

        public void SetLandmark(in PoseLandmarkObservation observation)
        {
            _landmarks[observation.index] = observation;
            if (observation.IsTracked)
            {
                trustedCount++;
            }
        }

        public void CopyFrom(PoseObservation source)
        {
            sourceTimestampMillisec = source.sourceTimestampMillisec;
            receivedAtSeconds = source.receivedAtSeconds;
            hasPose = source.hasPose;
            trustedCount = source.trustedCount;
            Array.Copy(source._landmarks, _landmarks, _landmarks.Length);
        }

        public void MarkUnavailable()
        {
            hasPose = false;
            trustedCount = 0;
            for (var i = 0; i < _landmarks.Length; i++)
            {
                var landmark = _landmarks[i];
                landmark.trust = LandmarkTrust.Unavailable;
                _landmarks[i] = landmark;
            }
        }

        public void Clear()
        {
            sourceTimestampMillisec = 0;
            receivedAtSeconds = 0;
            hasPose = false;
            trustedCount = 0;
            for (var i = 0; i < _landmarks.Length; i++)
            {
                _landmarks[i] = new PoseLandmarkObservation
                {
                    index = i,
                    trust = LandmarkTrust.Unavailable,
                };
            }
        }
    }
}
