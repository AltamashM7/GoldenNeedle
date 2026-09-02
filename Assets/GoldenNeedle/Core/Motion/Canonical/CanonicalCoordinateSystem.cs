using UnityEngine;

namespace GoldenNeedle.Core.Motion.Canonical
{
    /// <summary>
    /// Project-owned coordinate convention used by canonical motion data.
    /// Image positions are camera-image coordinates before the display-only sensor transform:
    /// x=0 is left, x=1 is right, y=0 is bottom, and y=1 is top.
    /// 3D positions use +X camera/view right, +Y up, and +Z away from the camera.
    /// </summary>
    public static class CanonicalCoordinateSystem
    {
        public static Vector2 CanonicalImageToGuiScreen(Vector2 canonicalImagePosition, Rect contentRect)
        {
            // Canonical image Y increases upward; Unity IMGUI screen Y increases downward.
            return new Vector2(
                contentRect.x + canonicalImagePosition.x * contentRect.width,
                contentRect.y + (1f - canonicalImagePosition.y) * contentRect.height);
        }

        public static Vector3 ToCanonicalWorld(float mediaPipeX, float mediaPipeY, float mediaPipeZ)
        {
            // MediaPipe pose world landmarks use an image-like vertical axis (smaller Y is
            // higher) and report smaller Z values for points closer to the camera. The project
            // therefore inverts Y and keeps Z so +Z means farther/away from the camera.
            return new Vector3(mediaPipeX, -mediaPipeY, mediaPipeZ);
        }

        public static Vector3 RootRelative(Vector3 worldPosition, Vector3 rootPosition)
        {
            return worldPosition - rootPosition;
        }
    }
}
