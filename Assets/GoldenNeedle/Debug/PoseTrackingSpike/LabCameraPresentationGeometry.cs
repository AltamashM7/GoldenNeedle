using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using UnityEngine;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    /// <summary>
    /// Pure Lab-presentation geometry for one camera frame. OrientedContentRect is the final
    /// visible image bounds used by all 2D overlays. RawTextureDrawRect is the pre-rotation
    /// rectangle that lands on exactly those bounds after the existing GUI quarter-turn.
    /// </summary>
    public readonly struct LabCameraPresentationGeometry
    {
        private LabCameraPresentationGeometry(
            Rect targetRect,
            Rect orientedContentRect,
            Rect rawTextureDrawRect,
            int displayRotationDegrees,
            bool hasValidSource)
        {
            TargetRect = targetRect;
            OrientedContentRect = orientedContentRect;
            RawTextureDrawRect = rawTextureDrawRect;
            DisplayRotationDegrees = displayRotationDegrees;
            HasValidSource = hasValidSource;
        }

        public Rect TargetRect { get; }
        public Rect OrientedContentRect { get; }
        public Rect RawTextureDrawRect { get; }
        public int DisplayRotationDegrees { get; }
        public bool HasValidSource { get; }
        public Vector2 TransformPivot => OrientedContentRect.center;

        public static LabCameraPresentationGeometry Create(
            Rect targetRect,
            int sourceWidth,
            int sourceHeight,
            int displayRotationDegrees)
        {
            var rotation =
                CameraOrientationState.NormalizeRotation(
                    displayRotationDegrees);
            if (targetRect.width <= 0f ||
                targetRect.height <= 0f ||
                sourceWidth <= 0 ||
                sourceHeight <= 0)
            {
                return new LabCameraPresentationGeometry(
                    targetRect,
                    targetRect,
                    targetRect,
                    rotation,
                    false);
            }

            var quarterTurn = rotation == 90 || rotation == 270;
            var orientedWidth =
                quarterTurn ? sourceHeight : sourceWidth;
            var orientedHeight =
                quarterTurn ? sourceWidth : sourceHeight;
            var scale = Mathf.Min(
                targetRect.width / orientedWidth,
                targetRect.height / orientedHeight);
            var contentSize = new Vector2(
                orientedWidth * scale,
                orientedHeight * scale);
            var contentRect = CenteredRect(
                targetRect.center,
                contentSize);

            // The GUI matrix rotates the raw WebCamTexture. Before a 90/270 degree turn its
            // footprint therefore needs the fitted dimensions swapped. Both rectangles share
            // one center/pivot, so the rotation maps this raw footprint exactly to contentRect.
            var rawSize = quarterTurn
                ? new Vector2(contentRect.height, contentRect.width)
                : contentRect.size;
            var rawTextureDrawRect =
                CenteredRect(contentRect.center, rawSize);

            return new LabCameraPresentationGeometry(
                targetRect,
                contentRect,
                rawTextureDrawRect,
                rotation,
                true);
        }

        public Vector2 MapNormalizedToScreen(
            Vector2 displayNormalized)
        {
            return new Vector2(
                OrientedContentRect.x +
                    displayNormalized.x * OrientedContentRect.width,
                OrientedContentRect.y +
                    displayNormalized.y * OrientedContentRect.height);
        }

        private static Rect CenteredRect(
            Vector2 center,
            Vector2 size)
        {
            return new Rect(
                center.x - size.x * 0.5f,
                center.y - size.y * 0.5f,
                size.x,
                size.y);
        }
    }
}
