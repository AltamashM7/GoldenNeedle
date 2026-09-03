using UnityEngine;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    /// <summary>
    /// Describes four separate camera concepts: sensor/storage metadata, the input preparation
    /// applied before MediaPipe inference, the resulting canonical inference frame, and the
    /// optional user-facing display mirror. The canonical data mapper does not use this state to
    /// undo inference transforms. SourceTextureHorizontallyMirrored is reserved for a physically
    /// verified source condition; front-facing status alone must not set it.
    /// </summary>
    public readonly struct CameraOrientationState
    {
        public CameraOrientationState(
            int sensorRotationDegrees,
            bool sensorVerticallyMirrored,
            bool frontFacing,
            bool sourceTextureHorizontallyMirrored,
            bool displayMirrored,
            bool inferenceFlipHorizontally,
            bool inferenceFlipVertically,
            int inferenceRotationDegrees)
        {
            SensorRotationDegrees = NormalizeRotation(sensorRotationDegrees);
            SensorVerticallyMirrored = sensorVerticallyMirrored;
            FrontFacing = frontFacing;
            SourceTextureHorizontallyMirrored = sourceTextureHorizontallyMirrored;
            DisplayMirrored = displayMirrored;
            InferenceFlipHorizontally = inferenceFlipHorizontally;
            InferenceFlipVertically = inferenceFlipVertically;
            InferenceRotationDegrees = NormalizeRotation(inferenceRotationDegrees);
        }

        /// <summary>Sensor/storage metadata, not a canonical landmark transform.</summary>
        public int SensorRotationDegrees { get; }

        /// <summary>Sensor/storage metadata, not a canonical landmark transform.</summary>
        public bool SensorVerticallyMirrored { get; }

        public bool FrontFacing { get; }

        /// <summary>
        /// Optional correction for a physically verified horizontally mirrored raw source.
        /// The current MediaPipe provider does not infer this from front-facing status.
        /// </summary>
        public bool SourceTextureHorizontallyMirrored { get; }

        /// <summary>Optional user-facing selfie mirror. Never changes canonical data.</summary>
        public bool DisplayMirrored { get; }

        /// <summary>Actual horizontal pixel preparation before MediaPipe sees the image.</summary>
        public bool InferenceFlipHorizontally { get; }

        /// <summary>Actual vertical pixel preparation before MediaPipe sees the image.</summary>
        public bool InferenceFlipVertically { get; }

        /// <summary>Actual image rotation supplied to MediaPipe after pixel preparation.</summary>
        public int InferenceRotationDegrees { get; }

        /// <summary>Display rotation derived from physical WebCamTexture sensor metadata.</summary>
        public int DisplayRotationDegrees => SensorRotationDegrees;

        /// <summary>Display vertical correction derived from physical sensor metadata.</summary>
        public bool DisplayVerticalCorrection => SensorVerticallyMirrored;

        /// <summary>Explicit user-facing horizontal mirror, independent of inference preparation.</summary>
        public bool DisplayHorizontalMirror => DisplayMirrored;

        /// <summary>
        /// Net horizontal presentation transform: correct the raw source mirror first, then apply
        /// the optional user-facing display mirror. Canonical data is not changed.
        /// </summary>
        public bool PresentationHorizontalMirror => SourceTextureHorizontallyMirrored ^ DisplayMirrored;

        /// <summary>
        /// INPUT SPACE: WebCamTexture sensor/storage normalized coordinates with top-left image
        /// semantics. OUTPUT SPACE: normalized coordinates in the canonical inference image
        /// after the same H/V pixel preparation and quarter-turn rotation used for MediaPipe.
        /// This helper is for preview geometry/tests; it is not used to remap returned landmarks.
        /// </summary>
        public Vector2 SensorTopLeftToInferenceNormalized(Vector2 sensorTopLeftNormalized)
        {
            var point = sensorTopLeftNormalized;
            if (InferenceFlipHorizontally)
            {
                point.x = 1f - point.x;
            }

            if (InferenceFlipVertically)
            {
                point.y = 1f - point.y;
            }

            return RotateTopLeft(point, InferenceRotationDegrees);
        }

        /// <summary>
        /// INPUT SPACE: MediaPipe inference normalized coordinates with top-left image semantics.
        /// OUTPUT SPACE: user-facing display normalized coordinates. The method first inverses the
        /// exact H/V + rotation inference preparation back to sensor/storage coordinates, then
        /// applies sensor display corrections and the optional explicit display mirror.
        /// Canonical/world data is not modified.
        /// </summary>
        public Vector2 InferenceTopLeftToDisplayNormalized(Vector2 inferenceTopLeftNormalized)
        {
            var sensorPoint = RotateTopLeft(inferenceTopLeftNormalized, -InferenceRotationDegrees);
            if (InferenceFlipVertically)
            {
                sensorPoint.y = 1f - sensorPoint.y;
            }

            if (InferenceFlipHorizontally)
            {
                sensorPoint.x = 1f - sensorPoint.x;
            }

            return SensorTopLeftToDisplayNormalized(sensorPoint);
        }

        /// <summary>
        /// INPUT SPACE: WebCamTexture sensor/storage normalized coordinates with top-left image
        /// semantics. OUTPUT SPACE: user-facing display normalized coordinates after sensor
        /// vertical correction, raw-source horizontal correction, sensor rotation, and the
        /// optional explicit display mirror.
        /// This display helper is independent of inference H/V flags.
        /// </summary>
        public Vector2 SensorTopLeftToDisplayNormalized(Vector2 sensorTopLeftNormalized)
        {
            var point = sensorTopLeftNormalized;
            if (SensorVerticallyMirrored)
            {
                point.y = 1f - point.y;
            }

            point = RotateTopLeft(point, SensorRotationDegrees);
            if (PresentationHorizontalMirror)
            {
                point.x = 1f - point.x;
            }

            return point;
        }

        public static int NormalizeRotation(int degrees)
        {
            var normalized = degrees % 360;
            return normalized < 0 ? normalized + 360 : normalized;
        }

        private static Vector2 RotateTopLeft(Vector2 point, int clockwiseDegrees)
        {
            switch (NormalizeRotation(clockwiseDegrees))
            {
                case 90:
                    return new Vector2(1f - point.y, point.x);
                case 180:
                    return new Vector2(1f - point.x, 1f - point.y);
                case 270:
                    return new Vector2(point.y, 1f - point.x);
                default:
                    return point;
            }
        }
    }
}
