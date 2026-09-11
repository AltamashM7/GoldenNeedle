using GoldenNeedle.Debug.PoseTrackingSpike;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class ThirdPersonLabCameraTests
    {
        [Test]
        public void LabUsesEnabledClearOnlyCameraAndGameRestoresWorldSettings()
        {
            var gameObject = new GameObject("ThirdPersonLabCameraTest");
            try
            {
                var camera = gameObject.AddComponent<Camera>();
                camera.cullingMask = 0x12345;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.2f, 0.3f, 0.4f, 1f);
                var expectedColor = camera.backgroundColor;

                var labCamera = gameObject.AddComponent<ThirdPersonLabCamera>();
                labCamera.SetGameViewActive(false);

                Assert.That(camera.enabled, Is.True);
                Assert.That(camera.cullingMask, Is.EqualTo(0));
                Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
                Assert.That(labCamera.IsLabClearOnly, Is.True);

                labCamera.SetGameViewActive(true);

                Assert.That(camera.enabled, Is.True);
                Assert.That(camera.cullingMask, Is.EqualTo(0x12345));
                Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
                Assert.That(camera.backgroundColor, Is.EqualTo(expectedColor));
                Assert.That(labCamera.IsGameViewActive, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void NormalizeQuaternionMakesSlightlyNonUnitRotationUnitWithoutChangingOrientation()
        {
            var original = new Quaternion(0.12f, -0.24f, 0.08f, 0.961f);
            var normalized = original;

            var originalMagnitudeSquared =
                original.x * original.x +
                original.y * original.y +
                original.z * original.z +
                original.w * original.w;
            Assert.That(Mathf.Abs(originalMagnitudeSquared - 1f),
                Is.GreaterThan(0.000001f));
            Assert.That(InvokeTryNormalizeQuaternion(ref normalized), Is.True);
            Assert.That(normalized.x * normalized.x +
                        normalized.y * normalized.y +
                        normalized.z * normalized.z +
                        normalized.w * normalized.w,
                Is.EqualTo(1f).Within(0.000001f));
            Assert.That(Quaternion.Angle(Quaternion.Normalize(original), normalized),
                Is.LessThan(0.001f));
        }

        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(float.NaN, 0f, 0f, 1f)]
        [TestCase(float.PositiveInfinity, 0f, 0f, 1f)]
        public void NormalizeQuaternionRejectsInvalidRotation(
            float x,
            float y,
            float z,
            float w)
        {
            var rotation = new Quaternion(x, y, z, w);

            Assert.That(InvokeTryNormalizeQuaternion(ref rotation), Is.False);
        }

        private static bool InvokeTryNormalizeQuaternion(ref Quaternion rotation)
        {
            var method = typeof(ThirdPersonLabCamera).GetMethod(
                "TryNormalizeQuaternion",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var arguments = new object[] { rotation };
            var result = (bool)method.Invoke(null, arguments);
            rotation = (Quaternion)arguments[0];
            return result;
        }
    }
}
