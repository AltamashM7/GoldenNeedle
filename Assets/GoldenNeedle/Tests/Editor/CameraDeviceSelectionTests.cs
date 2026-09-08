using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using NUnit.Framework;

namespace GoldenNeedle.Tests
{
    public sealed class CameraDeviceSelectionTests
    {
        [Test]
        public void ExactPreferredNameSelectsThatDevice()
        {
            var devices = new[]
            {
                "HP TrueVision HD",
                "Android Webcam",
            };

            var index =
                CameraDeviceSelection.SelectPreferredOrFallbackIndex(
                    devices,
                    "Android Webcam");

            Assert.That(index, Is.EqualTo(1));
        }

        [Test]
        public void MissingPreferredNameFallsBackToFirstUsableColorDevice()
        {
            var devices = new[]
            {
                "Intel Depth Camera",
                "HP TrueVision HD",
                "Android Webcam",
            };

            var index =
                CameraDeviceSelection.SelectPreferredOrFallbackIndex(
                    devices,
                    "Missing Camera");

            Assert.That(index, Is.EqualTo(1));
        }

        [Test]
        public void CameraCyclingWrapsAcrossUsableDevices()
        {
            var devices = new[]
            {
                "HP TrueVision HD",
                "Android Webcam",
            };

            Assert.That(
                CameraDeviceSelection.GetNextUsableDeviceName(
                    devices,
                    "HP TrueVision HD"),
                Is.EqualTo("Android Webcam"));
            Assert.That(
                CameraDeviceSelection.GetNextUsableDeviceName(
                    devices,
                    "Android Webcam"),
                Is.EqualTo("HP TrueVision HD"));
        }

        [Test]
        public void CameraCyclingSkipsDepthAndInfraredDevices()
        {
            var devices = new[]
            {
                "HP TrueVision HD",
                "Intel Depth Camera",
                "Windows Hello Infrared Camera",
                "Android Webcam",
            };

            Assert.That(
                CameraDeviceSelection.CountUsableDevices(devices),
                Is.EqualTo(2));
            Assert.That(
                CameraDeviceSelection.GetNextUsableDeviceName(
                    devices,
                    "HP TrueVision HD"),
                Is.EqualTo("Android Webcam"));
            Assert.That(
                CameraDeviceSelection.GetNextUsableDeviceName(
                    devices,
                    "Android Webcam"),
                Is.EqualTo("HP TrueVision HD"));
        }

        [Test]
        public void PendingSwitchWaitsForReadbackAndInferenceToFinish()
        {
            Assert.That(
                CameraDeviceSelection.CanProcessPendingSwitch(
                    true,
                    false,
                    true,
                    0),
                Is.False);
            Assert.That(
                CameraDeviceSelection.CanProcessPendingSwitch(
                    true,
                    false,
                    false,
                    1),
                Is.False);
            Assert.That(
                CameraDeviceSelection.CanProcessPendingSwitch(
                    true,
                    true,
                    false,
                    0),
                Is.False);
            Assert.That(
                CameraDeviceSelection.CanProcessPendingSwitch(
                    true,
                    false,
                    false,
                    0),
                Is.True);
        }
    }
}
