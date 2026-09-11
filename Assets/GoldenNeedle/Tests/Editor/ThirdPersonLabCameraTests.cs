using GoldenNeedle.Debug.PoseTrackingSpike;
using NUnit.Framework;
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
    }
}
