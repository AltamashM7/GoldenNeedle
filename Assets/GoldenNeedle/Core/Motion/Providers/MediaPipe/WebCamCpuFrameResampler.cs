using System;
using Unity.Collections;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    /// <summary>
    /// Allocation-free destination resampling for the experimental webcam CPU frame path.
    /// Source pixels use Unity's texture-coordinate ordering (left-to-right, bottom-to-top).
    /// H/V transforms intentionally match the scale/offset convention used by the existing
    /// DirectCPU GPU staging blit. Rotation remains a separate inference option.
    /// </summary>
    public static class WebCamCpuFrameResampler
    {
        public static bool IsEligible(
            bool directReadbackExperimentEnabled,
            bool webcamCpuCandidateEnabled,
            bool providerReady,
            bool sessionAvailable,
            bool bodyResourcesCurrent,
            bool cameraPlaying,
            int sourceWidth,
            int sourceHeight,
            int destinationWidth,
            int destinationHeight,
            TextureFormat destinationFormat)
        {
            return directReadbackExperimentEnabled &&
                webcamCpuCandidateEnabled &&
                providerReady &&
                sessionAvailable &&
                bodyResourcesCurrent &&
                cameraPlaying &&
                sourceWidth > 0 &&
                sourceHeight > 0 &&
                destinationWidth > 0 &&
                destinationHeight > 0 &&
                destinationFormat == TextureFormat.RGBA32;
        }

        public static void ResampleBilinear(
            Color32[] source,
            int sourceWidth,
            int sourceHeight,
            NativeArray<Color32> destination,
            int destinationWidth,
            int destinationHeight,
            bool flipHorizontally,
            bool flipVertically)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (sourceWidth <= 0 || sourceHeight <= 0 ||
                destinationWidth <= 0 || destinationHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Frame dimensions must be positive.");
            }

            var expectedSourceLength = checked(sourceWidth * sourceHeight);
            var expectedDestinationLength = checked(destinationWidth * destinationHeight);
            if (source.Length < expectedSourceLength)
            {
                throw new ArgumentException(
                    $"Source pixel buffer is too small. expected>={expectedSourceLength} actual={source.Length}",
                    nameof(source));
            }
            if (!destination.IsCreated || destination.Length != expectedDestinationLength)
            {
                throw new ArgumentException(
                    $"Destination pixel buffer size mismatch. expected={expectedDestinationLength} actual={(destination.IsCreated ? destination.Length : 0)}",
                    nameof(destination));
            }

            if (sourceWidth == destinationWidth &&
                sourceHeight == destinationHeight &&
                !flipHorizontally &&
                !flipVertically)
            {
                for (var i = 0; i < expectedDestinationLength; i++)
                {
                    destination[i] = source[i];
                }
                return;
            }

            for (var destinationY = 0; destinationY < destinationHeight; destinationY++)
            {
                var v = (destinationY + 0.5f) / destinationHeight;
                if (flipVertically)
                {
                    v = 1f - v;
                }

                var sourceY = v * sourceHeight - 0.5f;
                var sourceY0 = Mathf.FloorToInt(sourceY);
                var yFraction = sourceY - sourceY0;
                if (sourceY0 < 0)
                {
                    sourceY0 = 0;
                    yFraction = 0f;
                }
                else if (sourceY0 >= sourceHeight - 1)
                {
                    sourceY0 = sourceHeight - 1;
                    yFraction = 0f;
                }
                var sourceY1 = Math.Min(sourceY0 + 1, sourceHeight - 1);
                var sourceRow0 = sourceY0 * sourceWidth;
                var sourceRow1 = sourceY1 * sourceWidth;
                var destinationRow = destinationY * destinationWidth;

                for (var destinationX = 0; destinationX < destinationWidth; destinationX++)
                {
                    var u = (destinationX + 0.5f) / destinationWidth;
                    if (flipHorizontally)
                    {
                        u = 1f - u;
                    }

                    var sourceX = u * sourceWidth - 0.5f;
                    var sourceX0 = Mathf.FloorToInt(sourceX);
                    var xFraction = sourceX - sourceX0;
                    if (sourceX0 < 0)
                    {
                        sourceX0 = 0;
                        xFraction = 0f;
                    }
                    else if (sourceX0 >= sourceWidth - 1)
                    {
                        sourceX0 = sourceWidth - 1;
                        xFraction = 0f;
                    }
                    var sourceX1 = Math.Min(sourceX0 + 1, sourceWidth - 1);

                    destination[destinationRow + destinationX] = Bilinear(
                        source[sourceRow0 + sourceX0],
                        source[sourceRow0 + sourceX1],
                        source[sourceRow1 + sourceX0],
                        source[sourceRow1 + sourceX1],
                        xFraction,
                        yFraction);
                }
            }
        }

        private static Color32 Bilinear(
            Color32 bottomLeft,
            Color32 bottomRight,
            Color32 topLeft,
            Color32 topRight,
            float xFraction,
            float yFraction)
        {
            return new Color32(
                InterpolateChannel(
                    bottomLeft.r,
                    bottomRight.r,
                    topLeft.r,
                    topRight.r,
                    xFraction,
                    yFraction),
                InterpolateChannel(
                    bottomLeft.g,
                    bottomRight.g,
                    topLeft.g,
                    topRight.g,
                    xFraction,
                    yFraction),
                InterpolateChannel(
                    bottomLeft.b,
                    bottomRight.b,
                    topLeft.b,
                    topRight.b,
                    xFraction,
                    yFraction),
                InterpolateChannel(
                    bottomLeft.a,
                    bottomRight.a,
                    topLeft.a,
                    topRight.a,
                    xFraction,
                    yFraction));
        }

        private static byte InterpolateChannel(
            byte bottomLeft,
            byte bottomRight,
            byte topLeft,
            byte topRight,
            float xFraction,
            float yFraction)
        {
            var bottom = bottomLeft + (bottomRight - bottomLeft) * xFraction;
            var top = topLeft + (topRight - topLeft) * xFraction;
            return (byte)Mathf.Clamp(
                Mathf.RoundToInt(bottom + (top - bottom) * yFraction),
                byte.MinValue,
                byte.MaxValue);
        }
    }
}
