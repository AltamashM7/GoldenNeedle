using UnityEngine;

namespace GoldenNeedle.Core.Motion.Rich
{
    public enum CanonicalAnatomicalHandedness { Unknown = 0, RightHanded = 1, LeftHanded = -1 }

    public struct CanonicalAnatomicalBasis
    {
        public Vector3 primaryAxis;
        public Vector3 secondaryAxis;
        public Vector3 thirdAxis;
        public float determinant;
        public CanonicalAnatomicalHandedness handedness;
        public bool isValid;
    }
}
