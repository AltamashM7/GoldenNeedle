using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class WebCamCpuFrameResamplerTests
    {
        [TestCase(false, false, 1, 2, 3, 4)]
        [TestCase(true, false, 2, 1, 4, 3)]
        [TestCase(false, true, 3, 4, 1, 2)]
        [TestCase(true, true, 4, 3, 2, 1)]
        public void SameSizeTransformMatchesGpuTextureCoordinateConvention(
            bool flipHorizontally,
            bool flipVertically,
            int expected0,
            int expected1,
            int expected2,
            int expected3)
        {
            var source = new[]
            {
                Pixel(1), Pixel(2),
                Pixel(3), Pixel(4),
            };
            using var destination = new NativeArray<Color32>(4, Allocator.Temp);

            WebCamCpuFrameResampler.ResampleBilinear(
                source,
                2,
                2,
                destination,
                2,
                2,
                flipHorizontally,
                flipVertically);

            Assert.That(destination[0].r, Is.EqualTo(expected0));
            Assert.That(destination[1].r, Is.EqualTo(expected1));
            Assert.That(destination[2].r, Is.EqualTo(expected2));
            Assert.That(destination[3].r, Is.EqualTo(expected3));
        }

        [Test]
        public void BilinearDownscaleUsesDestinationPixelCenters()
        {
            var source = new Color32[16];
            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 4; x++)
                {
                    source[y * 4 + x] = Pixel((byte)(x + y * 10));
                }
            }
            using var destination = new NativeArray<Color32>(4, Allocator.Temp);

            WebCamCpuFrameResampler.ResampleBilinear(
                source,
                4,
                4,
                destination,
                2,
                2,
                flipHorizontally: false,
                flipVertically: false);

            Assert.That(destination[0].r, Is.EqualTo(6));
            Assert.That(destination[1].r, Is.EqualTo(8));
            Assert.That(destination[2].r, Is.EqualTo(26));
            Assert.That(destination[3].r, Is.EqualTo(28));
        }

        [Test]
        public void EligibilityRequiresExistingDirectExperimentAndHealthySession()
        {
            Assert.That(
                WebCamCpuFrameResampler.IsEligible(
                    directReadbackExperimentEnabled: true,
                    webcamCpuCandidateEnabled: true,
                    providerReady: true,
                    sessionAvailable: true,
                    bodyResourcesCurrent: true,
                    cameraPlaying: true,
                    sourceWidth: 640,
                    sourceHeight: 480,
                    destinationWidth: 320,
                    destinationHeight: 240,
                    destinationFormat: TextureFormat.RGBA32),
                Is.True);

            Assert.That(
                WebCamCpuFrameResampler.IsEligible(
                    directReadbackExperimentEnabled: false,
                    webcamCpuCandidateEnabled: true,
                    providerReady: true,
                    sessionAvailable: true,
                    bodyResourcesCurrent: true,
                    cameraPlaying: true,
                    sourceWidth: 640,
                    sourceHeight: 480,
                    destinationWidth: 320,
                    destinationHeight: 240,
                    destinationFormat: TextureFormat.RGBA32),
                Is.False);

            Assert.That(
                WebCamCpuFrameResampler.IsEligible(
                    directReadbackExperimentEnabled: true,
                    webcamCpuCandidateEnabled: true,
                    providerReady: true,
                    sessionAvailable: false,
                    bodyResourcesCurrent: true,
                    cameraPlaying: true,
                    sourceWidth: 640,
                    sourceHeight: 480,
                    destinationWidth: 320,
                    destinationHeight: 240,
                    destinationFormat: TextureFormat.RGBA32),
                Is.False);
        }

        [Test]
        public void InvalidDestinationBufferIsRejected()
        {
            var source = new[] { Pixel(1), Pixel(2), Pixel(3), Pixel(4) };
            using var destination = new NativeArray<Color32>(3, Allocator.Temp);

            Assert.Throws<System.ArgumentException>(() =>
                WebCamCpuFrameResampler.ResampleBilinear(
                    source,
                    2,
                    2,
                    destination,
                    2,
                    2,
                    flipHorizontally: false,
                    flipVertically: false));
        }

        private static Color32 Pixel(byte value)
        {
            return new Color32(value, 0, 0, 255);
        }
    }
}
