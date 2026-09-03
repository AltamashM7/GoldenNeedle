using System;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// The four positional chains consumed by the Motion Engine retargeter. This enum is
    /// deliberately provider- and avatar-independent.
    /// </summary>
    public enum CanonicalKinematicChainId
    {
        LeftArm = 0,
        RightArm = 1,
        LeftLeg = 2,
        RightLeg = 3,
    }

    /// <summary>
    /// One provider-independent positional chain target. Source vectors stay in Golden Needle
    /// canonical 3D space. The avatar adapter is responsible for the explicit canonical-to-avatar
    /// signed-axis conversion and for scaling by avatar-authored reach.
    /// </summary>
    [Serializable]
    public struct CanonicalKinematicChainTarget
    {
        public CanonicalKinematicChainId id;
        public bool isValid;
        public bool hasBendHint;
        [Range(0f, 1f)] public float confidence;
        public Vector3 sourceRootPosition;
        public Vector3 sourceMidPosition;
        public Vector3 sourceEffectorPosition;
        public float sourceReach;
        public Vector3 normalizedEffectorDisplacement;
        public Vector3 normalizedBendHintDisplacement;
        public long sourceTimestampMillisec;
        public double receivedAtSeconds;
    }

    /// <summary>
    /// Preallocated output of the canonical kinematic target builder. It contains no MediaPipe,
    /// Transform, Animator, or avatar-axis types and can therefore be consumed by any target
    /// adapter.
    /// </summary>
    [Serializable]
    public sealed class CanonicalKinematicTargets
    {
        public const int ChainCount = 4;

        private readonly CanonicalKinematicChainTarget[] _chains = new CanonicalKinematicChainTarget[ChainCount];

        public long sourceTimestampMillisec { get; private set; }
        public double receivedAtSeconds { get; private set; }
        public bool calibrationValid { get; private set; }
        public bool hasMeaningfulTargets { get; private set; }
        public int validChainCount { get; private set; }

        public CanonicalKinematicTargets()
        {
            Clear();
        }

        public CanonicalKinematicChainTarget GetTarget(CanonicalKinematicChainId id)
        {
            return _chains[(int)id];
        }

        public void Begin(long timestampMillisec, double receivedTimeSeconds, bool profileValid)
        {
            sourceTimestampMillisec = timestampMillisec;
            receivedAtSeconds = receivedTimeSeconds;
            calibrationValid = profileValid;
            hasMeaningfulTargets = false;
            validChainCount = 0;
            ClearChains();
        }

        public void SetTarget(in CanonicalKinematicChainTarget target)
        {
            var index = (int)target.id;
            var previous = _chains[index];
            if (previous.isValid)
            {
                validChainCount--;
            }

            _chains[index] = target;
            if (target.isValid)
            {
                validChainCount++;
            }
        }

        public void Complete()
        {
            hasMeaningfulTargets = validChainCount > 0;
        }

        public void Clear()
        {
            sourceTimestampMillisec = 0L;
            receivedAtSeconds = 0d;
            calibrationValid = false;
            hasMeaningfulTargets = false;
            validChainCount = 0;
            ClearChains();
        }

        private void ClearChains()
        {
            for (var i = 0; i < _chains.Length; i++)
            {
                _chains[i] = new CanonicalKinematicChainTarget
                {
                    id = (CanonicalKinematicChainId)i,
                    isValid = false,
                    hasBendHint = false,
                    confidence = 0f,
                    sourceReach = 0f,
                    normalizedEffectorDisplacement = Vector3.zero,
                    normalizedBendHintDisplacement = Vector3.zero,
                };
            }
        }
    }
}
