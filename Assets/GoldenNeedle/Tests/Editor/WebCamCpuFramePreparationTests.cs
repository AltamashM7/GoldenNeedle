using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class WebCamCpuFramePreparationTests
    {
        [TestCase(false, false, 10, 20, 30, 40)]
        [TestCase(true, false, 20, 10, 40, 30)]
        [TestCase(false, true, 30, 40, 10, 20)]
        [TestCase(true, true, 40, 30, 20, 10)]
        public void HalfScaleAveragesTwoByTwoAndPreservesFlipSemantics(
            bool flipHorizontally,
            bool flipVertically,
            byte expected00,
            byte expected10,
            byte expected01,
            byte expected11)
        {
            var source = BuildFourByFourBlocks();
            using var destination = new NativeArray<byte>(
                2 * 2 * 4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);

            WebCamCpuFramePreparation.PrepareRgba(
                source,
                4,
                4,
                destination,
                2,
                2,
                flipHorizontally,
                flipVertically);

            AssertPixel(destination, 2, 0, 0, expected00);
            AssertPixel(destination, 2, 1, 0, expected10);
            AssertPixel(destination, 2, 0, 1, expected01);
            AssertPixel(destination, 2, 1, 1, expected11);
        }

        [Test]
        public void GeneralPathPreservesPixelsAtEqualResolution()
        {
            var source = new[]
            {
                Pixel(1), Pixel(2), Pixel(3),
                Pixel(4), Pixel(5), Pixel(6),
            };
            using var destination = new NativeArray<byte>(
                3 * 2 * 4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);

            WebCamCpuFramePreparation.PrepareRgba(
                source, 3, 2, destination, 3, 2, false, false);

            for (var y = 0; y < 2; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    AssertPixel(destination, 3, x, y, source[y * 3 + x].r);
                }
            }
        }

        [Test]
        public void GeneralPathFlipsAtEqualResolution()
        {
            var source = new[]
            {
                Pixel(1), Pixel(2), Pixel(3),
                Pixel(4), Pixel(5), Pixel(6),
            };
            using var destination = new NativeArray<byte>(
                3 * 2 * 4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);

            WebCamCpuFramePreparation.PrepareRgba(
                source, 3, 2, destination, 3, 2, true, true);

            AssertPixel(destination, 3, 0, 0, 6);
            AssertPixel(destination, 3, 1, 0, 5);
            AssertPixel(destination, 3, 2, 0, 4);
            AssertPixel(destination, 3, 0, 1, 3);
            AssertPixel(destination, 3, 1, 1, 2);
            AssertPixel(destination, 3, 2, 1, 1);
        }

        [Test]
        public void InvalidDestinationSizeFailsClosed()
        {
            var source = new[] { Pixel(1), Pixel(2), Pixel(3), Pixel(4) };
            using var destination = new NativeArray<byte>(
                15,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);

            Assert.Throws<System.ArgumentException>(() =>
                WebCamCpuFramePreparation.PrepareRgba(
                    source, 2, 2, destination, 2, 2, false, false));
        }

        [Test]
        public void TimingWindowReportsMedianAndP95WithoutGrowing()
        {
            var window = new WebCamCpuAcquisitionTimingWindow(3);
            window.Add(new WebCamCpuAcquisitionTimingSample(1d, 10d, 11d));
            window.Add(new WebCamCpuAcquisitionTimingSample(2d, 20d, 22d));
            window.Add(new WebCamCpuAcquisitionTimingSample(3d, 30d, 33d));
            window.Add(new WebCamCpuAcquisitionTimingSample(4d, 40d, 44d));

            Assert.That(window.ValidSampleCount, Is.EqualTo(3));
            Assert.That(
                window.GetMedianMilliseconds(WebCamCpuAcquisitionTimingMetric.GetPixels32),
                Is.EqualTo(3d));
            Assert.That(
                window.GetP95Milliseconds(WebCamCpuAcquisitionTimingMetric.Total),
                Is.EqualTo(44d));
        }

        private static Color32[] BuildFourByFourBlocks()
        {
            return new[]
            {
                Pixel(10), Pixel(10), Pixel(20), Pixel(20),
                Pixel(10), Pixel(10), Pixel(20), Pixel(20),
                Pixel(30), Pixel(30), Pixel(40), Pixel(40),
                Pixel(30), Pixel(30), Pixel(40), Pixel(40),
            };
        }

        private static Color32 Pixel(byte value)
        {
            return new Color32(value, value, value, 255);
        }

        private static void AssertPixel(
            NativeArray<byte> rgba,
            int width,
            int x,
            int y,
            byte expected)
        {
            var offset = (y * width + x) * 4;
            Assert.That(rgba[offset], Is.EqualTo(expected));
            Assert.That(rgba[offset + 1], Is.EqualTo(expected));
            Assert.That(rgba[offset + 2], Is.EqualTo(expected));
            Assert.That(rgba[offset + 3], Is.EqualTo(255));
        }
    }
}
