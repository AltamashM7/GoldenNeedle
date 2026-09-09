using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using GoldenNeedle.Debug.PoseTrackingSpike;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class LabCameraPresentationGeometryTests
    {
        private static readonly Rect WideScreen =
            new Rect(0f, 0f, 1600f, 900f);

        [TestCase(640, 480, 0, 200f, 0f, 1200f, 900f, 200f, 0f, 1200f, 900f)]
        [TestCase(480, 640, 0, 462.5f, 0f, 675f, 900f, 462.5f, 0f, 675f, 900f)]
        [TestCase(480, 640, 90, 200f, 0f, 1200f, 900f, 350f, -150f, 900f, 1200f)]
        [TestCase(480, 640, 180, 462.5f, 0f, 675f, 900f, 462.5f, 0f, 675f, 900f)]
        [TestCase(480, 640, 270, 200f, 0f, 1200f, 900f, 350f, -150f, 900f, 1200f)]
        public void WholeFrameFitMatchesExpectedOrientedAndRawRects(
            int sourceWidth,
            int sourceHeight,
            int rotation,
            float contentX,
            float contentY,
            float contentWidth,
            float contentHeight,
            float rawX,
            float rawY,
            float rawWidth,
            float rawHeight)
        {
            var geometry =
                LabCameraPresentationGeometry.Create(
                    WideScreen,
                    sourceWidth,
                    sourceHeight,
                    rotation);

            AssertRect(
                geometry.OrientedContentRect,
                new Rect(
                    contentX,
                    contentY,
                    contentWidth,
                    contentHeight));
            AssertRect(
                geometry.RawTextureDrawRect,
                new Rect(
                    rawX,
                    rawY,
                    rawWidth,
                    rawHeight));

            Assert.That(
                geometry.OrientedContentRect.xMin,
                Is.GreaterThanOrEqualTo(WideScreen.xMin));
            Assert.That(
                geometry.OrientedContentRect.xMax,
                Is.LessThanOrEqualTo(WideScreen.xMax));
            Assert.That(
                geometry.OrientedContentRect.yMin,
                Is.GreaterThanOrEqualTo(WideScreen.yMin));
            Assert.That(
                geometry.OrientedContentRect.yMax,
                Is.LessThanOrEqualTo(WideScreen.yMax));
        }

        [TestCase(0)]
        [TestCase(90)]
        [TestCase(180)]
        [TestCase(270)]
        public void RepresentativeOverlayPointsUseTheSameFittedContentRect(
            int rotation)
        {
            var geometry =
                LabCameraPresentationGeometry.Create(
                    WideScreen,
                    480,
                    640,
                    rotation);
            var orientation = Orientation(rotation);
            var points = new[]
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(0.15f, 0.20f),
                new Vector2(0.85f, 0.80f),
            };

            for (var i = 0; i < points.Length; i++)
            {
                var guiNormalized =
                    orientation.InferenceTopLeftToGuiNormalized(
                        points[i]);
                var screen =
                    geometry.MapNormalizedToScreen(
                        guiNormalized);

                Assert.That(
                    screen.x,
                    Is.InRange(
                        geometry.OrientedContentRect.xMin,
                        geometry.OrientedContentRect.xMax));
                Assert.That(
                    screen.y,
                    Is.InRange(
                        geometry.OrientedContentRect.yMin,
                        geometry.OrientedContentRect.yMax));
            }

            Assert.That(
                geometry.MapNormalizedToScreen(Vector2.zero),
                Is.EqualTo(
                    new Vector2(
                        geometry.OrientedContentRect.xMin,
                        geometry.OrientedContentRect.yMin)));
            Assert.That(
                geometry.MapNormalizedToScreen(Vector2.one),
                Is.EqualTo(
                    new Vector2(
                        geometry.OrientedContentRect.xMax,
                        geometry.OrientedContentRect.yMax)));
        }

        [Test]
        public void DisplayMirrorReflectsOverlayXOnly()
        {
            var geometry =
                LabCameraPresentationGeometry.Create(
                    WideScreen,
                    640,
                    480,
                    0);
            var input = new Vector2(0.20f, 0.35f);
            var a =
                geometry.MapNormalizedToScreen(
                    Orientation(0)
                        .InferenceTopLeftToGuiNormalized(input));
            var b =
                geometry.MapNormalizedToScreen(
                    Orientation(0, displayMirrored: true)
                        .InferenceTopLeftToGuiNormalized(input));

            Assert.That(
                a.x + b.x,
                Is.EqualTo(
                    geometry.OrientedContentRect.center.x * 2f)
                    .Within(0.0001f));
            Assert.That(
                b.y,
                Is.EqualTo(a.y).Within(0.0001f));
        }

        [Test]
        public void GeometryDoesNotIntroduceSecondYInversion()
        {
            var geometry =
                LabCameraPresentationGeometry.Create(
                    WideScreen,
                    640,
                    480,
                    0);
            var guiNormalized =
                Orientation(0)
                    .InferenceTopLeftToGuiNormalized(
                        new Vector2(0.25f, 0.20f));
            var screen =
                geometry.MapNormalizedToScreen(
                    guiNormalized);

            Assert.That(
                guiNormalized.y,
                Is.EqualTo(0.80f).Within(0.0001f));
            Assert.That(
                screen.y,
                Is.EqualTo(
                    geometry.OrientedContentRect.y +
                    0.80f *
                    geometry.OrientedContentRect.height)
                    .Within(0.0001f));
        }

        private static CameraOrientationState Orientation(
            int rotation,
            bool displayMirrored = false)
        {
            return new CameraOrientationState(
                sensorRotationDegrees: rotation,
                sensorVerticallyMirrored: false,
                frontFacing: false,
                sourceTextureHorizontallyMirrored: false,
                displayMirrored: displayMirrored,
                inferenceFlipHorizontally: false,
                inferenceFlipVertically: false,
                inferenceRotationDegrees: rotation);
        }

        private static void AssertRect(
            Rect actual,
            Rect expected)
        {
            Assert.That(
                actual.x,
                Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(
                actual.y,
                Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(
                actual.width,
                Is.EqualTo(expected.width).Within(0.0001f));
            Assert.That(
                actual.height,
                Is.EqualTo(expected.height).Within(0.0001f));
        }
    }
}
