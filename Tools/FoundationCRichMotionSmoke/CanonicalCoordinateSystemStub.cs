using UnityEngine;

namespace GoldenNeedle.Core.Motion.Canonical
{
    public static class CanonicalCoordinateSystem
    {
        public static Vector3 MediaPipeWorldToCanonical(Vector3 mediaPipeWorld)
        {
            return new Vector3(mediaPipeWorld.x, -mediaPipeWorld.y, mediaPipeWorld.z);
        }
    }
}
