using GoldenNeedle.Gameplay.Calibration;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class CalibrationCameraPresentationGeometryTests
    {
        [TestCase(0, 666.6667f, 500f, 666.6667f, 500f)]
        [TestCase(90, 375f, 500f, 500f, 375f)]
        [TestCase(180, 666.6667f, 500f, 666.6667f, 500f)]
        [TestCase(270, 375f, 500f, 500f, 375f)]
        public void RotationFitsOrientedContentAndPreservesRawTextureFootprint(
            int rotation,
            float expectedContentWidth,
            float expectedContentHeight,
            float expectedRawWidth,
            float expectedRawHeight)
        {
            var geometry = CalibrationCameraPresentationGeometry.Create(
                640,
                480,
                rotation,
                1000f,
                500f,
                false,
                false);

            Assert.That(geometry.HasValidSource, Is.True);
            Assert.That(geometry.HasValidTarget, Is.True);
            Assert.That(geometry.RotationDegrees, Is.EqualTo(rotation));
            Assert.That(geometry.OrientedContentSize.x, Is.EqualTo(expectedContentWidth).Within(0.0001f));
            Assert.That(geometry.OrientedContentSize.y, Is.EqualTo(expectedContentHeight).Within(0.0001f));
            Assert.That(geometry.RawTextureSize.x, Is.EqualTo(expectedRawWidth).Within(0.0001f));
            Assert.That(geometry.RawTextureSize.y, Is.EqualTo(expectedRawHeight).Within(0.0001f));
        }

        [Test]
        public void RawTextureAspectRemainsSourceAspectAtEveryQuarterTurn()
        {
            var sourceAspect = 640f / 480f;

            foreach (var rotation in new[] { 0, 90, 180, 270 })
            {
                var geometry = CalibrationCameraPresentationGeometry.Create(
                    640,
                    480,
                    rotation,
                    1000f,
                    500f,
                    false,
                    false);

                Assert.That(geometry.RawTextureAspectRatio, Is.EqualTo(sourceAspect).Within(0.0001f));
            }
        }

        [Test]
        public void RotatedRawTextureFootprintMatchesOrientedContentSize()
        {
            foreach (var rotation in new[] { 0, 90, 180, 270 })
            {
                var geometry = CalibrationCameraPresentationGeometry.Create(
                    640,
                    480,
                    rotation,
                    1000f,
                    500f,
                    false,
                    false);
                var rotatedFootprint = rotation == 90 || rotation == 270
                    ? new Vector2(geometry.RawTextureSize.y, geometry.RawTextureSize.x)
                    : geometry.RawTextureSize;

                Assert.That(rotatedFootprint.x, Is.EqualTo(geometry.OrientedContentSize.x).Within(0.0001f));
                Assert.That(rotatedFootprint.y, Is.EqualTo(geometry.OrientedContentSize.y).Within(0.0001f));
            }
        }

        [Test]
        public void PresentationMirrorAndVerticalCorrectionRemainSeparateFromGeometry()
        {
            var geometry = CalibrationCameraPresentationGeometry.Create(
                640,
                480,
                90,
                1000f,
                500f,
                true,
                true);
            var unflippedGeometry = CalibrationCameraPresentationGeometry.Create(
                640,
                480,
                90,
                1000f,
                500f,
                false,
                false);

            Assert.That(geometry.HorizontalMirror, Is.True);
            Assert.That(geometry.VerticalCorrection, Is.True);
            Assert.That(geometry.OrientedContentSize, Is.EqualTo(unflippedGeometry.OrientedContentSize));
            Assert.That(geometry.RawTextureSize, Is.EqualTo(unflippedGeometry.RawTextureSize));
        }

        [Test]
        public void ZeroSourceDimensionsProduceInvalidGeometry()
        {
            var geometry = CalibrationCameraPresentationGeometry.Create(
                0,
                480,
                0,
                1000f,
                500f,
                false,
                false);

            Assert.That(geometry.HasValidSource, Is.False);
            Assert.That(geometry.HasValidTarget, Is.True);
            Assert.That(geometry.OrientedContentSize, Is.EqualTo(Vector2.zero));
            Assert.That(geometry.RawTextureSize, Is.EqualTo(Vector2.zero));
            Assert.That(geometry.RawTextureAspectRatio, Is.EqualTo(0f));
        }

        [Test]
        public void ZeroTargetDimensionsProduceInvalidGeometry()
        {
            var geometry = CalibrationCameraPresentationGeometry.Create(
                640,
                480,
                0,
                0f,
                500f,
                false,
                false);

            Assert.That(geometry.HasValidSource, Is.True);
            Assert.That(geometry.HasValidTarget, Is.False);
            Assert.That(geometry.OrientedContentSize, Is.EqualTo(Vector2.zero));
            Assert.That(geometry.RawTextureSize, Is.EqualTo(Vector2.zero));
            Assert.That(geometry.OrientedContentAspectRatio, Is.EqualTo(0f));
        }
    }
}
