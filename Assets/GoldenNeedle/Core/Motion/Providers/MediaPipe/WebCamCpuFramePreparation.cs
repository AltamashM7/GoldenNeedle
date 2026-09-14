using System;
using Unity.Collections;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    public enum WebCamCpuAcquisitionTimingMetric
    {
        GetPixels32,
        Preparation,
        Total,
    }

    public readonly struct WebCamCpuAcquisitionTimingSample
    {
        public WebCamCpuAcquisitionTimingSample(
            double getPixels32Milliseconds,
            double preparationMilliseconds,
            double totalMilliseconds)
        {
            GetPixels32Milliseconds = getPixels32Milliseconds;
            PreparationMilliseconds = preparationMilliseconds;
            TotalMilliseconds = totalMilliseconds;
        }

        public double GetPixels32Milliseconds { get; }
        public double PreparationMilliseconds { get; }
        public double TotalMilliseconds { get; }
    }

    public sealed class WebCamCpuAcquisitionTimingWindow
    {
        private readonly WebCamCpuAcquisitionTimingSample[] _samples;
        private readonly double[] _scratch;
        private int _nextIndex;
        private int _count;

        public WebCamCpuAcquisitionTimingWindow(int capacity = 64)
        {
            capacity = Math.Max(1, capacity);
            _samples = new WebCamCpuAcquisitionTimingSample[capacity];
            _scratch = new double[capacity];
        }

        public int ValidSampleCount => _count;

        public void Reset()
        {
            _nextIndex = 0;
            _count = 0;
        }

        public void Add(in WebCamCpuAcquisitionTimingSample sample)
        {
            _samples[_nextIndex] = sample;
            _nextIndex = (_nextIndex + 1) % _samples.Length;
            _count = Math.Min(_count + 1, _samples.Length);
        }

        public double GetMedianMilliseconds(WebCamCpuAcquisitionTimingMetric metric)
        {
            if (_count == 0)
            {
                return double.NaN;
            }

            CopyMetric(metric);
            Array.Sort(_scratch, 0, _count);
            var middle = _count / 2;
            return _count % 2 == 0
                ? (_scratch[middle - 1] + _scratch[middle]) * 0.5d
                : _scratch[middle];
        }

        public double GetP95Milliseconds(WebCamCpuAcquisitionTimingMetric metric)
        {
            if (_count == 0)
            {
                return double.NaN;
            }

            CopyMetric(metric);
            Array.Sort(_scratch, 0, _count);
            var index = Math.Min(
                _count - 1,
                Math.Max(0, (int)Math.Ceiling(_count * 0.95d) - 1));
            return _scratch[index];
        }

        private void CopyMetric(WebCamCpuAcquisitionTimingMetric metric)
        {
            for (var i = 0; i < _count; i++)
            {
                _scratch[i] = metric switch
                {
                    WebCamCpuAcquisitionTimingMetric.GetPixels32 => _samples[i].GetPixels32Milliseconds,
                    WebCamCpuAcquisitionTimingMetric.Preparation => _samples[i].PreparationMilliseconds,
                    WebCamCpuAcquisitionTimingMetric.Total => _samples[i].TotalMilliseconds,
                    _ => double.NaN,
                };
            }
        }
    }

    public static class WebCamCpuFramePreparation
    {
        public static void PrepareRgba(
            Color32[] source,
            int sourceWidth,
            int sourceHeight,
            NativeArray<byte> destination,
            int targetWidth,
            int targetHeight,
            bool flipHorizontally,
            bool flipVertically)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (sourceWidth <= 0 || sourceHeight <= 0 ||
                targetWidth <= 0 || targetHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceWidth),
                    "Source and target dimensions must be positive.");
            }

            var sourcePixelCount = checked(sourceWidth * sourceHeight);
            var targetByteCount = checked(checked(targetWidth * targetHeight) * 4);
            if (source.Length != sourcePixelCount)
            {
                throw new ArgumentException(
                    $"Source pixel count mismatch. expected={sourcePixelCount} actual={source.Length}",
                    nameof(source));
            }
            if (!destination.IsCreated || destination.Length != targetByteCount)
            {
                throw new ArgumentException(
                    $"Destination RGBA size mismatch. expected={targetByteCount} actual={(destination.IsCreated ? destination.Length : 0)}",
                    nameof(destination));
            }

            if (sourceWidth == targetWidth * 2 &&
                sourceHeight == targetHeight * 2)
            {
                PrepareHalfScale(
                    source,
                    sourceWidth,
                    sourceHeight,
                    destination,
                    targetWidth,
                    targetHeight,
                    flipHorizontally,
                    flipVertically);
                return;
            }

            PrepareBilinear(
                source,
                sourceWidth,
                sourceHeight,
                destination,
                targetWidth,
                targetHeight,
                flipHorizontally,
                flipVertically);
        }

        private static void PrepareHalfScale(
            Color32[] source,
            int sourceWidth,
            int sourceHeight,
            NativeArray<byte> destination,
            int targetWidth,
            int targetHeight,
            bool flipHorizontally,
            bool flipVertically)
        {
            for (var y = 0; y < targetHeight; y++)
            {
                var sourceY = (flipVertically ? targetHeight - 1 - y : y) * 2;
                for (var x = 0; x < targetWidth; x++)
                {
                    var sourceX = (flipHorizontally ? targetWidth - 1 - x : x) * 2;
                    var firstRow = sourceY * sourceWidth + sourceX;
                    var secondRow = firstRow + sourceWidth;
                    var c00 = source[firstRow];
                    var c10 = source[firstRow + 1];
                    var c01 = source[secondRow];
                    var c11 = source[secondRow + 1];
                    WriteRgba(
                        destination,
                        (y * targetWidth + x) * 4,
                        Average4(c00.r, c10.r, c01.r, c11.r),
                        Average4(c00.g, c10.g, c01.g, c11.g),
                        Average4(c00.b, c10.b, c01.b, c11.b),
                        Average4(c00.a, c10.a, c01.a, c11.a));
                }
            }
        }

        private static void PrepareBilinear(
            Color32[] source,
            int sourceWidth,
            int sourceHeight,
            NativeArray<byte> destination,
            int targetWidth,
            int targetHeight,
            bool flipHorizontally,
            bool flipVertically)
        {
            for (var y = 0; y < targetHeight; y++)
            {
                var sampleY = ((y + 0.5d) * sourceHeight / targetHeight) - 0.5d;
                if (flipVertically)
                {
                    sampleY = (sourceHeight - 1) - sampleY;
                }
                sampleY = Clamp(sampleY, 0d, sourceHeight - 1d);
                var y0 = (int)Math.Floor(sampleY);
                var y1 = Math.Min(sourceHeight - 1, y0 + 1);
                var fy = sampleY - y0;

                for (var x = 0; x < targetWidth; x++)
                {
                    var sampleX = ((x + 0.5d) * sourceWidth / targetWidth) - 0.5d;
                    if (flipHorizontally)
                    {
                        sampleX = (sourceWidth - 1) - sampleX;
                    }
                    sampleX = Clamp(sampleX, 0d, sourceWidth - 1d);
                    var x0 = (int)Math.Floor(sampleX);
                    var x1 = Math.Min(sourceWidth - 1, x0 + 1);
                    var fx = sampleX - x0;

                    var c00 = source[y0 * sourceWidth + x0];
                    var c10 = source[y0 * sourceWidth + x1];
                    var c01 = source[y1 * sourceWidth + x0];
                    var c11 = source[y1 * sourceWidth + x1];
                    WriteRgba(
                        destination,
                        (y * targetWidth + x) * 4,
                        BilinearChannel(c00.r, c10.r, c01.r, c11.r, fx, fy),
                        BilinearChannel(c00.g, c10.g, c01.g, c11.g, fx, fy),
                        BilinearChannel(c00.b, c10.b, c01.b, c11.b, fx, fy),
                        BilinearChannel(c00.a, c10.a, c01.a, c11.a, fx, fy));
                }
            }
        }

        private static byte Average4(byte a, byte b, byte c, byte d)
        {
            return (byte)(((int)a + b + c + d + 2) / 4);
        }

        private static byte BilinearChannel(
            byte c00,
            byte c10,
            byte c01,
            byte c11,
            double fx,
            double fy)
        {
            var top = c00 + (c10 - c00) * fx;
            var bottom = c01 + (c11 - c01) * fx;
            var value = top + (bottom - top) * fy;
            var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            return (byte)Math.Max(0, Math.Min(255, rounded));
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static void WriteRgba(
            NativeArray<byte> destination,
            int offset,
            byte r,
            byte g,
            byte b,
            byte a)
        {
            destination[offset] = r;
            destination[offset + 1] = g;
            destination[offset + 2] = b;
            destination[offset + 3] = a;
        }
    }
}
