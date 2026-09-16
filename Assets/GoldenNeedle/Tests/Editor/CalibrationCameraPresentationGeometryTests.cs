using GoldenNeedle.Gameplay.Calibration;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class CalibrationCameraPresentationGeometryTests
    {
        [TestCase(0, 640, 480, 640, 480)]
        [TestCase(90, 640, 480, 480, 640)]
        [TestCase(180, 640, 480, 640, 480)]
        [TestCase(270, 640, 480, 480, 640)]
        [TestCase(450, 640, 480, 480, 640)]
        public void RotationProducesTheOrientedSourceSize(
            int rotation,
            int sourceWidth,
            int sourceHeight,
            int expectedWidth,
            int expectedHeight)
        {
            var geometry = CalibrationCameraPresentationGeometry.Create(
                sourceWidth,
                sourceHeight,
                rotation,
                false,
                false);

            Assert.That(geometry.HasValidSource, Is.True);
            Assert.That(geometry.RotationDegrees, Is.EqualTo(rotation % 360));
            Assert.That(geometry.OrientedSize, Is.EqualTo(new Vector2(expectedWidth, expectedHeight)));
            Assert.That(geometry.AspectRatio, Is.EqualTo((float)expectedWidth / expectedHeight).Within(0.0001f));
        }

        [Test]
        public void PresentationMirrorAndVerticalCorrectionRemainSeparateFromGeometry()
        {
            var geometry = CalibrationCameraPresentationGeometry.Create(
                640,
                480,
                90,
                true,
                true);

            Assert.That(geometry.HorizontalMirror, Is.True);
            Assert.That(geometry.VerticalCorrection, Is.True);
            Assert.That(geometry.RotationDegrees, Is.EqualTo(90));
            Assert.That(geometry.OrientedSize, Is.EqualTo(new Vector2(480, 640)));
        }

        [Test]
        public void InvalidSourceDoesNotProduceAnAspectRatio()
        {
            var geometry = CalibrationCameraPresentationGeometry.Create(
                0,
                480,
                0,
                false,
                false);

            Assert.That(geometry.HasValidSource, Is.False);
            Assert.That(geometry.AspectRatio, Is.EqualTo(0f));
        }
    }
}
