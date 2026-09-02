using UnityEngine;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    /// <summary>
    /// Keeps the camera sensor/display transform separate from the transform used to prepare
    /// a Unity texture for MediaPipe. Landmark coordinates returned by MediaPipe are mapped back
    /// into the unrotated camera image before they cross into the canonical layer. The returned
    /// normalized coordinates use the project convention: x left-to-right and y bottom-to-top.
    /// </summary>
    public readonly struct CameraOrientationState
    {
        public CameraOrientationState(
            int sensorRotationDegrees,
            bool sensorVerticallyMirrored,
            bool frontFacing,
            bool displayMirrored,
            bool inferenceFlipHorizontally,
            bool inferenceFlipVertically,
            int inferenceRotationDegrees)
        {
            SensorRotationDegrees = NormalizeRotation(sensorRotationDegrees);
            SensorVerticallyMirrored = sensorVerticallyMirrored;
            FrontFacing = frontFacing;
            DisplayMirrored = displayMirrored;
            InferenceFlipHorizontally = inferenceFlipHorizontally;
            InferenceFlipVertically = inferenceFlipVertically;
            InferenceRotationDegrees = NormalizeRotation(inferenceRotationDegrees);
        }

        public int SensorRotationDegrees { get; }
        public bool SensorVerticallyMirrored { get; }
        public bool FrontFacing { get; }
        public bool DisplayMirrored { get; }
        public bool InferenceFlipHorizontally { get; }
        public bool InferenceFlipVertically { get; }
        public int InferenceRotationDegrees { get; }

        // The preview uses only physical camera metadata plus the explicit display mirror choice.
        // In particular, the Unity-to-MediaPipe vertical conversion is never reused for display.
        public bool DisplayFlipHorizontally => DisplayMirrored;
        public bool DisplayFlipVertically => SensorVerticallyMirrored;
        public int DisplayRotationDegrees => SensorRotationDegrees;

        public Vector2 MediaPipeImageToCameraNormalized(Vector2 mediaPipeNormalized)
        {
            // ImageProcessingOptions rotates after the texture readback flip. Undo that order,
            // but keep the result in normalized camera coordinates. The provider boundary uses
            // the project convention of x left-to-right and y bottom-to-top; GUI conversion is
            // intentionally owned by the presentation boundary instead.
            var point = InverseRotateTopLeft(mediaPipeNormalized, InferenceRotationDegrees);
            if (InferenceFlipHorizontally)
            {
                point.x = 1f - point.x;
            }

            if (InferenceFlipVertically)
            {
                point.y = 1f - point.y;
            }

            return point;
        }

        public static int NormalizeRotation(int degrees)
        {
            var normalized = degrees % 360;
            return normalized < 0 ? normalized + 360 : normalized;
        }

        private static Vector2 InverseRotateTopLeft(Vector2 point, int clockwiseDegrees)
        {
            switch (NormalizeRotation(clockwiseDegrees))
            {
                case 90:
                    return new Vector2(point.y, 1f - point.x);
                case 180:
                    return new Vector2(1f - point.x, 1f - point.y);
                case 270:
                    return new Vector2(1f - point.y, point.x);
                default:
                    return point;
            }
        }
    }
}
