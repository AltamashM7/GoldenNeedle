using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Calibration
{
    /// <summary>
    /// Presentation-only dimensions and flips for the production calibration preview.
    /// The rotation sign is consumed by CalibrationCameraPreview in Unity's y-up UI space;
    /// it is the coordinate-space equivalent of the accepted lab GUI -rotation convention.
    /// </summary>
    public readonly struct CalibrationCameraPresentationGeometry
    {
        private CalibrationCameraPresentationGeometry(
            int rotationDegrees,
            Vector2 targetSize,
            Vector2 orientedContentSize,
            Vector2 rawTextureSize,
            bool horizontalMirror,
            bool verticalCorrection,
            bool hasValidSource,
            bool hasValidTarget)
        {
            RotationDegrees = rotationDegrees;
            TargetSize = targetSize;
            OrientedContentSize = orientedContentSize;
            RawTextureSize = rawTextureSize;
            HorizontalMirror = horizontalMirror;
            VerticalCorrection = verticalCorrection;
            HasValidSource = hasValidSource;
            HasValidTarget = hasValidTarget;
        }

        public int RotationDegrees { get; }
        public Vector2 TargetSize { get; }
        public Vector2 OrientedContentSize { get; }
        public Vector2 RawTextureSize { get; }
        public float OrientedContentAspectRatio => OrientedContentSize.y <= 0f
            ? 0f
            : OrientedContentSize.x / OrientedContentSize.y;
        public float RawTextureAspectRatio => RawTextureSize.y <= 0f
            ? 0f
            : RawTextureSize.x / RawTextureSize.y;
        public bool HorizontalMirror { get; }
        public bool VerticalCorrection { get; }
        public bool HasValidSource { get; }
        public bool HasValidTarget { get; }

        public static CalibrationCameraPresentationGeometry Create(
            int sourceWidth,
            int sourceHeight,
            int displayRotationDegrees,
            float targetWidth,
            float targetHeight,
            bool presentationHorizontalMirror,
            bool displayVerticalCorrection)
        {
            var rotation = CameraOrientationState.NormalizeRotation(displayRotationDegrees);
            var hasValidSource = sourceWidth > 0 && sourceHeight > 0;
            var hasValidTarget = targetWidth > 0f && targetHeight > 0f;
            var quarterTurn = rotation == 90 || rotation == 270;
            var targetSize = hasValidTarget
                ? new Vector2(targetWidth, targetHeight)
                : Vector2.zero;
            var orientedSourceSize = hasValidSource
                ? new Vector2(
                    quarterTurn ? sourceHeight : sourceWidth,
                    quarterTurn ? sourceWidth : sourceHeight)
                : Vector2.zero;
            var orientedContentSize = Vector2.zero;
            var rawTextureSize = Vector2.zero;

            if (hasValidSource && hasValidTarget)
            {
                var scale = Mathf.Min(
                    targetSize.x / orientedSourceSize.x,
                    targetSize.y / orientedSourceSize.y);
                orientedContentSize = orientedSourceSize * scale;
                rawTextureSize = quarterTurn
                    ? new Vector2(orientedContentSize.y, orientedContentSize.x)
                    : orientedContentSize;
            }

            return new CalibrationCameraPresentationGeometry(
                rotation,
                targetSize,
                orientedContentSize,
                rawTextureSize,
                presentationHorizontalMirror,
                displayVerticalCorrection,
                hasValidSource,
                hasValidTarget);
        }
    }
}
