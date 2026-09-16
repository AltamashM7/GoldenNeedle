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
            Vector2 orientedSize,
            bool horizontalMirror,
            bool verticalCorrection,
            bool hasValidSource)
        {
            RotationDegrees = rotationDegrees;
            OrientedSize = orientedSize;
            HorizontalMirror = horizontalMirror;
            VerticalCorrection = verticalCorrection;
            HasValidSource = hasValidSource;
        }

        public int RotationDegrees { get; }
        public Vector2 OrientedSize { get; }
        public float AspectRatio => OrientedSize.y <= 0f ? 0f : OrientedSize.x / OrientedSize.y;
        public bool HorizontalMirror { get; }
        public bool VerticalCorrection { get; }
        public bool HasValidSource { get; }

        public static CalibrationCameraPresentationGeometry Create(
            int sourceWidth,
            int sourceHeight,
            int displayRotationDegrees,
            bool presentationHorizontalMirror,
            bool displayVerticalCorrection)
        {
            var rotation = CameraOrientationState.NormalizeRotation(displayRotationDegrees);
            var valid = sourceWidth > 0 && sourceHeight > 0;
            var quarterTurn = rotation == 90 || rotation == 270;
            var orientedSize = valid
                ? new Vector2(
                    quarterTurn ? sourceHeight : sourceWidth,
                    quarterTurn ? sourceWidth : sourceHeight)
                : Vector2.zero;

            return new CalibrationCameraPresentationGeometry(
                rotation,
                orientedSize,
                presentationHorizontalMirror,
                displayVerticalCorrection,
                valid);
        }
    }
}
