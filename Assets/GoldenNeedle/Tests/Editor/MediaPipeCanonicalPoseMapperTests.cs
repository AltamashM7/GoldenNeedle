using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class MediaPipeCanonicalPoseMapperTests
    {
        [Test]
        public void MapsDirectLeftRightJointsAndDerivedMidpoints()
        {
            var observation = NewObservation();
            Track(observation, 11, 0.2f, 0.3f, new Vector3(-0.2f, -0.5f, 0.1f), 0.9f);
            Track(observation, 12, 0.8f, 0.3f, new Vector3(0.2f, -0.5f, 0.1f), 0.8f);
            Track(observation, 13, 0.1f, 0.4f, new Vector3(-0.4f, -0.2f, 0.1f), 0.7f);
            Track(observation, 14, 0.9f, 0.4f, new Vector3(0.4f, -0.2f, 0.1f), 0.6f);
            Track(observation, 23, 0.35f, 0.7f, new Vector3(-0.15f, 0.1f, 0.2f), 0.75f);
            Track(observation, 24, 0.65f, 0.7f, new Vector3(0.15f, 0.1f, 0.2f), 0.55f);

            var frame = new CanonicalPoseFrame();
            MediaPipeCanonicalPoseMapper.Map(observation, frame, DefaultOrientation());

            var leftShoulder = frame.GetJoint(CanonicalJointId.LeftShoulder);
            var rightShoulder = frame.GetJoint(CanonicalJointId.RightShoulder);
            var chest = frame.GetJoint(CanonicalJointId.Chest);
            var pelvis = frame.GetJoint(CanonicalJointId.Pelvis);
            var spine = frame.GetJoint(CanonicalJointId.Spine);

            Assert.That(leftShoulder.IsTracked, Is.True);
            Assert.That(leftShoulder.imagePosition, Is.EqualTo(new Vector2(0.2f, 0.3f)));
            Assert.That(rightShoulder.imagePosition, Is.EqualTo(new Vector2(0.8f, 0.3f)));
            Assert.That(chest.imagePosition, Is.EqualTo(new Vector2(0.5f, 0.3f)));
            Assert.That(chest.confidence, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(pelvis.imagePosition, Is.EqualTo(new Vector2(0.5f, 0.7f)));
            Assert.That(spine.imagePosition, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(pelvis.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(frame.hasCanonicalPelvis, Is.True);
            Assert.That(frame.hasCanonical3D, Is.True);
        }

        [Test]
        public void PreservesPartialUpperBodyWhenHipsAreUnavailable()
        {
            var observation = NewObservation();
            Track(observation, 11, 0.2f, 0.3f, Vector3.zero);
            Track(observation, 13, 0.1f, 0.4f, Vector3.zero);
            Track(observation, 15, 0.05f, 0.5f, Vector3.zero);

            var frame = new CanonicalPoseFrame();
            MediaPipeCanonicalPoseMapper.Map(observation, frame, DefaultOrientation());

            Assert.That(frame.GetJoint(CanonicalJointId.LeftShoulder).IsTracked, Is.True);
            Assert.That(frame.GetJoint(CanonicalJointId.LeftElbow).IsTracked, Is.True);
            Assert.That(frame.GetJoint(CanonicalJointId.LeftWrist).IsTracked, Is.True);
            Assert.That(frame.GetJoint(CanonicalJointId.Pelvis).IsTracked, Is.False);
            Assert.That(frame.GetJoint(CanonicalJointId.Chest).IsTracked, Is.False);
            Assert.That(frame.hasMeaningfulPose, Is.True);
            Assert.That(frame.hasCanonicalPelvis, Is.False);
        }

        [Test]
        public void ConvertsWorldAxesAndUsesMinimumDerivedConfidence()
        {
            var observation = NewObservation();
            Track(observation, 23, 0.35f, 0.7f, new Vector3(-0.2f, -0.4f, -0.1f), 0.85f);
            Track(observation, 24, 0.65f, 0.7f, new Vector3(0.2f, -0.2f, 0.3f), 0.55f);

            var frame = new CanonicalPoseFrame();
            MediaPipeCanonicalPoseMapper.Map(observation, frame, DefaultOrientation());
            var pelvis = frame.GetJoint(CanonicalJointId.Pelvis);

            Assert.That(pelvis.confidence, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(Vector3.Distance(pelvis.worldPosition, new Vector3(0f, 0.3f, 0.1f)), Is.LessThan(0.0001f));
            Assert.That(pelvis.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void InvertsInferenceTransformBeforeCanonicalImageConversion()
        {
            var orientation = new CameraOrientationState(
                sensorRotationDegrees: 0,
                sensorVerticallyMirrored: false,
                frontFacing: true,
                displayMirrored: false,
                inferenceFlipHorizontally: true,
                inferenceFlipVertically: true,
                inferenceRotationDegrees: 0);

            var cameraImage = orientation.MediaPipeImageToCameraNormalized(new Vector2(0.2f, 0.3f));

            Assert.That(cameraImage, Is.EqualTo(new Vector2(0.8f, 0.7f)));
        }

        [Test]
        public void CanonicalImageYUpMapsToSmallerGuiScreenY()
        {
            var observation = NewObservation();
            Track(observation, 11, 0.5f, 0.8f, Vector3.zero);
            Track(observation, 13, 0.5f, 0.2f, Vector3.zero);

            var frame = new CanonicalPoseFrame();
            MediaPipeCanonicalPoseMapper.Map(observation, frame, DefaultOrientation());

            var higher = frame.GetJoint(CanonicalJointId.LeftShoulder);
            var lower = frame.GetJoint(CanonicalJointId.LeftElbow);
            var contentRect = new Rect(0f, 0f, 100f, 100f);
            var higherScreen = CanonicalCoordinateSystem.CanonicalImageToGuiScreen(higher.imagePosition, contentRect);
            var lowerScreen = CanonicalCoordinateSystem.CanonicalImageToGuiScreen(lower.imagePosition, contentRect);

            Assert.That(higher.imagePosition.y, Is.GreaterThan(lower.imagePosition.y));
            Assert.That(higherScreen.y, Is.LessThan(lowerScreen.y));
        }

        private static PoseObservation NewObservation()
        {
            var observation = new PoseObservation();
            observation.Begin(100, 2.0, true);
            return observation;
        }

        private static void Track(PoseObservation observation, int index, float x, float y, Vector3 world, float confidence = 1f)
        {
            observation.SetLandmark(new PoseLandmarkObservation
            {
                index = index,
                x = x,
                y = y,
                z = 0f,
                worldX = world.x,
                worldY = world.y,
                worldZ = world.z,
                visibility = confidence,
                presence = confidence,
                hasVisibility = true,
                hasPresence = true,
                hasWorldCoordinates = world != Vector3.zero,
                trust = LandmarkTrust.Tracked,
            });
        }

        private static CameraOrientationState DefaultOrientation()
        {
            return new CameraOrientationState(0, false, false, false, false, false, 0);
        }
    }
}
