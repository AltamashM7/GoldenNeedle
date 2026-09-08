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
            MediaPipeCanonicalPoseMapper.Map(observation, frame);

            var leftShoulder = frame.GetJoint(CanonicalJointId.LeftShoulder);
            var rightShoulder = frame.GetJoint(CanonicalJointId.RightShoulder);
            var chest = frame.GetJoint(CanonicalJointId.Chest);
            var pelvis = frame.GetJoint(CanonicalJointId.Pelvis);
            var spine = frame.GetJoint(CanonicalJointId.Spine);

            Assert.That(leftShoulder.IsTracked, Is.True);
            Assert.That(leftShoulder.imagePosition, Is.EqualTo(new Vector2(0.2f, 0.7f)));
            Assert.That(rightShoulder.imagePosition, Is.EqualTo(new Vector2(0.8f, 0.7f)));
            Assert.That(chest.imagePosition, Is.EqualTo(new Vector2(0.5f, 0.7f)));
            Assert.That(chest.confidence, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(pelvis.imagePosition, Is.EqualTo(new Vector2(0.5f, 0.3f)));
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
            MediaPipeCanonicalPoseMapper.Map(observation, frame);

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
            MediaPipeCanonicalPoseMapper.Map(observation, frame);
            var pelvis = frame.GetJoint(CanonicalJointId.Pelvis);

            Assert.That(pelvis.confidence, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(Vector3.Distance(pelvis.worldPosition, new Vector3(0f, 0.3f, 0.1f)), Is.LessThan(0.0001f));
            Assert.That(pelvis.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void MapsMediaPipeNormalizedCoordinatesDirectlyInCanonicalInferenceFrame()
        {
            var observation = NewObservation();
            Track(observation, 11, 0.2f, 0.3f, Vector3.zero);
            var frame = new CanonicalPoseFrame();

            MediaPipeCanonicalPoseMapper.Map(observation, frame);

            Assert.That(frame.GetJoint(CanonicalJointId.LeftShoulder).imagePosition, Is.EqualTo(new Vector2(0.2f, 0.7f)));
        }

        [Test]
        public void SensorInputPreparationSupportsAllQuarterTurns()
        {
            var input = new Vector2(0.2f, 0.3f);

            Assert.That(Orientation(inferenceRotationDegrees: 0).SensorTopLeftToInferenceNormalized(input), Is.EqualTo(new Vector2(0.2f, 0.3f)));
            Assert.That(Orientation(inferenceRotationDegrees: 90).SensorTopLeftToInferenceNormalized(input), Is.EqualTo(new Vector2(0.7f, 0.2f)));
            Assert.That(Orientation(inferenceRotationDegrees: 180).SensorTopLeftToInferenceNormalized(input), Is.EqualTo(new Vector2(0.8f, 0.7f)));
            Assert.That(Orientation(inferenceRotationDegrees: 270).SensorTopLeftToInferenceNormalized(input), Is.EqualTo(new Vector2(0.3f, 0.8f)));
        }

        [Test]
        public void SensorInputPreparationAppliesFlipsBeforeRotation()
        {
            var input = new Vector2(0.2f, 0.3f);
            var orientation = Orientation(
                inferenceFlipHorizontally: true,
                inferenceFlipVertically: true,
                inferenceRotationDegrees: 90);

            Assert.That(orientation.SensorTopLeftToInferenceNormalized(input), Is.EqualTo(new Vector2(0.3f, 0.8f)));
        }

        [Test]
        public void FrontCameraInferenceKeepsPhysicalXAndProjectsToUnmirroredDisplay()
        {
            var sensorPoint = new Vector2(0.2f, 0.3f);
            // Normal front-camera transport at rotation 0 keeps X physical/unmirrored while the
            // Unity readback still requires its vertical transport correction.
            var unmirrored = Orientation(
                frontFacing: true,
                inferenceFlipVertically: true);
            var mirrored = Orientation(
                frontFacing: true,
                displayMirrored: true,
                inferenceFlipVertically: true);

            var inferencePoint = unmirrored.SensorTopLeftToInferenceNormalized(sensorPoint);
            Assert.That(inferencePoint, Is.EqualTo(new Vector2(0.2f, 0.7f)));
            Assert.That(unmirrored.InferenceTopLeftToDisplayNormalized(inferencePoint), Is.EqualTo(sensorPoint));
            Assert.That(
                mirrored.InferenceTopLeftToDisplayNormalized(inferencePoint),
                Is.EqualTo(new Vector2(0.8f, 0.3f)));
        }

        [Test]
        public void InferenceUpperPointProjectsToUpperGuiAfterFrontCameraPreparation()
        {
            var orientation = Orientation(
                frontFacing: true,
                inferenceFlipVertically: true);
            var upperInferencePoint = new Vector2(0.2f, 0.2f);

            var guiNormalized = orientation.InferenceTopLeftToGuiNormalized(upperInferencePoint);

            Assert.That(guiNormalized.x, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(guiNormalized.y, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(guiNormalized.y, Is.LessThan(0.5f));
        }

        [Test]
        public void FrontCameraUnmirroredInferencePreservesAnatomicalLeftRightAndKnownDepthTurn()
        {
            var orientation = Orientation(
                frontFacing: true,
                inferenceFlipVertically: true);
            // Unmirrored person facing the camera: anatomical left appears viewer-right,
            // anatomical right appears viewer-left.
            var physicalLeftSensor = new Vector2(0.70f, 0.40f);
            var physicalRightSensor = new Vector2(0.30f, 0.40f);

            var leftInference = orientation.SensorTopLeftToInferenceNormalized(physicalLeftSensor);
            var rightInference = orientation.SensorTopLeftToInferenceNormalized(physicalRightSensor);
            Assert.That(leftInference.x, Is.EqualTo(0.70f).Within(0.0001f));
            Assert.That(rightInference.x, Is.EqualTo(0.30f).Within(0.0001f));

            var observation = NewObservation();
            // Controlled-turn contract: physical/anatomical right side is nearer, represented here
            // by the smaller canonical-preserved MediaPipe world Z.
            Track(observation, 11, leftInference.x, leftInference.y, new Vector3(0.20f, -0.50f, 0.20f));
            Track(observation, 12, rightInference.x, rightInference.y, new Vector3(-0.20f, -0.50f, -0.20f));
            Track(observation, 23, 0.65f, 0.70f, new Vector3(0.15f, 0.10f, 0.15f));
            Track(observation, 24, 0.35f, 0.70f, new Vector3(-0.15f, 0.10f, -0.15f));

            var frame = new CanonicalPoseFrame();
            MediaPipeCanonicalPoseMapper.Map(observation, frame);

            var leftShoulder = frame.GetJoint(CanonicalJointId.LeftShoulder);
            var rightShoulder = frame.GetJoint(CanonicalJointId.RightShoulder);
            var leftHip = frame.GetJoint(CanonicalJointId.LeftHip);
            var rightHip = frame.GetJoint(CanonicalJointId.RightHip);

            Assert.That(leftShoulder.imagePosition.x, Is.GreaterThan(rightShoulder.imagePosition.x));
            Assert.That(leftShoulder.worldPosition.x, Is.GreaterThan(rightShoulder.worldPosition.x));
            Assert.That(rightShoulder.worldPosition.z, Is.LessThan(leftShoulder.worldPosition.z));
            Assert.That(rightHip.worldPosition.z, Is.LessThan(leftHip.worldPosition.z));
        }

        [Test]
        public void ExplicitDisplayMirrorDoesNotChangeCanonicalSemanticIds()
        {
            var observation = NewObservation();
            Track(observation, 11, 0.70f, 0.40f, new Vector3(0.20f, -0.50f, 0.10f));
            Track(observation, 12, 0.30f, 0.40f, new Vector3(-0.20f, -0.50f, 0.10f));

            var canonical = new CanonicalPoseFrame();
            MediaPipeCanonicalPoseMapper.Map(observation, canonical);

            var unmirrored = Orientation(frontFacing: true, inferenceFlipVertically: true);
            var mirrored = Orientation(
                frontFacing: true,
                displayMirrored: true,
                inferenceFlipVertically: true);
            var leftInference = new Vector2(0.70f, 0.60f);

            Assert.That(
                unmirrored.InferenceTopLeftToDisplayNormalized(leftInference).x,
                Is.EqualTo(0.70f).Within(0.0001f));
            Assert.That(
                mirrored.InferenceTopLeftToDisplayNormalized(leftInference).x,
                Is.EqualTo(0.30f).Within(0.0001f));
            Assert.That(canonical.GetJoint(CanonicalJointId.LeftShoulder).worldPosition.x, Is.GreaterThan(0f));
            Assert.That(canonical.GetJoint(CanonicalJointId.RightShoulder).worldPosition.x, Is.LessThan(0f));
        }

        [Test]
        public void DisplayBaselineUsesSensorMetadataOnly()
        {
            var input = new Vector2(0.2f, 0.3f);
            var orientation = Orientation(inferenceFlipHorizontally: true, inferenceFlipVertically: true);

            Assert.That(orientation.SensorTopLeftToDisplayNormalized(input), Is.EqualTo(input));
            Assert.That(orientation.SensorTopLeftToInferenceNormalized(input), Is.EqualTo(new Vector2(0.8f, 0.7f)));
        }

        [Test]
        public void ExplicitDisplayMirrorIsTheOnlyDefaultHorizontalPresentationTransform()
        {
            var input = new Vector2(0.2f, 0.3f);
            var baseline = Orientation(frontFacing: true);
            var mirrored = Orientation(frontFacing: true, displayMirrored: true);

            Assert.That(baseline.DisplayMirrored, Is.False);
            Assert.That(baseline.SourceTextureHorizontallyMirrored, Is.False);
            Assert.That(baseline.PresentationHorizontalMirror, Is.False);
            Assert.That(baseline.SensorTopLeftToDisplayNormalized(input), Is.EqualTo(input));
            Assert.That(mirrored.DisplayMirrored, Is.True);
            Assert.That(mirrored.PresentationHorizontalMirror, Is.True);
            Assert.That(mirrored.SensorTopLeftToDisplayNormalized(input), Is.EqualTo(new Vector2(0.8f, 0.3f)));
        }

        [Test]
        public void SensorVerticalMirrorAffectsDisplayOnly()
        {
            var input = new Vector2(0.2f, 0.3f);
            var orientation = Orientation(sensorVerticallyMirrored: true);

            Assert.That(orientation.SensorTopLeftToDisplayNormalized(input), Is.EqualTo(new Vector2(0.2f, 0.7f)));
            Assert.That(orientation.SensorTopLeftToInferenceNormalized(input), Is.EqualTo(input));
        }

        [Test]
        public void SensorDisplayRotationSupportsAllQuarterTurns()
        {
            var input = new Vector2(0.2f, 0.3f);
            var expected = new[]
            {
                new Vector2(0.2f, 0.3f),
                new Vector2(0.7f, 0.2f),
                new Vector2(0.8f, 0.7f),
                new Vector2(0.3f, 0.8f),
            };
            var rotations = new[] { 0, 90, 180, 270 };

            for (var i = 0; i < rotations.Length; i++)
            {
                Assert.That(
                    Orientation(sensorRotationDegrees: rotations[i]).SensorTopLeftToDisplayNormalized(input),
                    Is.EqualTo(expected[i]));
            }
        }

        [Test]
        public void WorldConversionUsesTheCanonicalBaseAxesOnly()
        {
            var rawWorld = new Vector3(1f, 2f, 3f);
            AssertCanonicalWorld(CanonicalCoordinateSystem.MediaPipeWorldToCanonical(rawWorld), new Vector3(1f, -2f, 3f));
        }

        [Test]
        public void PixelTransportOrientationDoesNotTransformReturnedWorldCoordinates()
        {
            var orientation = Orientation(inferenceFlipVertically: true);
            Assert.That(orientation.SensorTopLeftToInferenceNormalized(new Vector2(0.5f, 0.2f)).y, Is.EqualTo(0.8f));

            var observation = NewObservation();
            Track(observation, 23, 0.5f, 0.6f, new Vector3(0f, -0.6f, 0.1f));
            Track(observation, 24, 0.5f, 0.6f, new Vector3(0f, -0.4f, 0.1f));
            var frame = new CanonicalPoseFrame();

            MediaPipeCanonicalPoseMapper.Map(observation, frame);

            AssertCanonicalWorld(frame.GetJoint(CanonicalJointId.Pelvis).worldPosition, new Vector3(0f, 0.5f, 0.1f));
        }

        [Test]
        public void DisplayMirrorDoesNotChangeCanonicalData()
        {
            var observation = NewObservation();
            Track(observation, 11, 0.2f, 0.3f, new Vector3(-0.2f, -0.5f, 0.1f));
            Track(observation, 12, 0.8f, 0.3f, new Vector3(0.2f, -0.5f, 0.1f));
            var unmirroredOrientation = Orientation(displayMirrored: false);
            var mirroredOrientation = Orientation(displayMirrored: true);
            var unmirrored = new CanonicalPoseFrame();
            var mirrored = new CanonicalPoseFrame();

            MediaPipeCanonicalPoseMapper.Map(observation, unmirrored);
            MediaPipeCanonicalPoseMapper.Map(observation, mirrored);

            Assert.That(
                unmirroredOrientation.SensorTopLeftToDisplayNormalized(new Vector2(0.2f, 0.3f)),
                Is.EqualTo(new Vector2(0.2f, 0.3f)));
            Assert.That(
                mirroredOrientation.SensorTopLeftToDisplayNormalized(new Vector2(0.2f, 0.3f)),
                Is.EqualTo(new Vector2(0.8f, 0.3f)));
            Assert.That(mirrored.GetJoint(CanonicalJointId.LeftShoulder).imagePosition, Is.EqualTo(unmirrored.GetJoint(CanonicalJointId.LeftShoulder).imagePosition));
            Assert.That(mirrored.GetJoint(CanonicalJointId.LeftShoulder).worldPosition, Is.EqualTo(unmirrored.GetJoint(CanonicalJointId.LeftShoulder).worldPosition));
        }

        [Test]
        public void DisplayMirrorAndOverlayMirrorTogether()
        {
            var sourcePoint = new Vector2(0.2f, 0.3f);
            var canonicalPoint = CanonicalCoordinateSystem.MediaPipeNormalizedToCanonicalImage(sourcePoint);
            var contentRect = new Rect(10f, 20f, 100f, 200f);
            var unmirroredPreviewPoint = Orientation().SensorTopLeftToDisplayNormalized(sourcePoint);
            var mirroredPreviewPoint = Orientation(displayMirrored: true).SensorTopLeftToDisplayNormalized(sourcePoint);
            var unmirroredOverlayPoint = CanonicalCoordinateSystem.CanonicalImageToGuiScreen(canonicalPoint, contentRect);
            var mirroredOverlayPoint = CanonicalCoordinateSystem.CanonicalImageToGuiScreen(canonicalPoint, contentRect, true);

            Assert.That(mirroredPreviewPoint.x, Is.EqualTo(1f - unmirroredPreviewPoint.x).Within(0.0001f));
            Assert.That(mirroredPreviewPoint.y, Is.EqualTo(unmirroredPreviewPoint.y).Within(0.0001f));
            Assert.That(mirroredOverlayPoint.x, Is.EqualTo(contentRect.x + (1f - unmirroredPreviewPoint.x) * contentRect.width).Within(0.0001f));
            Assert.That(mirroredOverlayPoint.y, Is.EqualTo(unmirroredOverlayPoint.y).Within(0.0001f));
            Assert.That(canonicalPoint, Is.EqualTo(new Vector2(0.2f, 0.7f)));
        }

        [Test]
        public void MapperKeepsSemanticIdsWithoutApplyingInputOrientation()
        {
            var observation = NewObservation();
            Track(observation, 15, 0.2f, 0.3f, new Vector3(1f, 2f, 3f));
            Track(observation, 16, 0.8f, 0.3f, new Vector3(4f, 5f, 6f));
            var frame = new CanonicalPoseFrame();

            MediaPipeCanonicalPoseMapper.Map(observation, frame);

            Assert.That(frame.GetJoint(CanonicalJointId.LeftWrist).imagePosition, Is.EqualTo(new Vector2(0.2f, 0.7f)));
            Assert.That(frame.GetJoint(CanonicalJointId.RightWrist).imagePosition, Is.EqualTo(new Vector2(0.8f, 0.7f)));
            AssertCanonicalWorld(frame.GetJoint(CanonicalJointId.LeftWrist).worldPosition, new Vector3(1f, -2f, 3f));
            AssertCanonicalWorld(frame.GetJoint(CanonicalJointId.RightWrist).worldPosition, new Vector3(4f, -5f, 6f));
        }

        [Test]
        public void CanonicalImageAndWorldDeltasAgreeAfterWorldConversion()
        {
            var observation = NewObservation();
            Track(observation, 11, 0.3f, 0.3f, new Vector3(-0.2f, -0.3f, 0.1f));
            Track(observation, 12, 0.7f, 0.3f, new Vector3(0.2f, -0.3f, 0.1f));
            Track(observation, 15, 0.2f, 0.2f, new Vector3(-0.3f, -0.5f, 0.1f));
            Track(observation, 16, 0.8f, 0.6f, new Vector3(0.3f, 0.1f, 0.1f));
            Track(observation, 23, 0.35f, 0.6f, new Vector3(-0.15f, 0.2f, 0.2f));
            Track(observation, 24, 0.65f, 0.6f, new Vector3(0.15f, 0.2f, 0.2f));
            Track(observation, 25, 0.35f, 0.75f, new Vector3(-0.15f, 0.5f, 0.2f));
            Track(observation, 26, 0.65f, 0.75f, new Vector3(0.15f, 0.5f, 0.2f));
            Track(observation, 27, 0.35f, 0.9f, new Vector3(-0.15f, 0.8f, 0.2f));
            Track(observation, 28, 0.65f, 0.9f, new Vector3(0.15f, 0.8f, 0.2f));
            var frame = new CanonicalPoseFrame();

            MediaPipeCanonicalPoseMapper.Map(observation, frame);

            var agreement = CanonicalCoordinateAgreementEvaluator.Evaluate(frame);
            Assert.That(agreement.xComparisons, Is.GreaterThan(0));
            Assert.That(agreement.yComparisons, Is.GreaterThan(0));
            Assert.That(agreement.xPass, Is.True);
            Assert.That(agreement.yPass, Is.True);
        }

        [Test]
        public void CanonicalWorldZRemainsTheAwayAxisAcrossOrientationTransforms()
        {
            var input = new Vector3(1f, 2f, -4f);
            Assert.That(CanonicalCoordinateSystem.MediaPipeWorldToCanonical(input).z, Is.EqualTo(-4f));
        }

        [Test]
        public void CanonicalImageYUpMapsToSmallerGuiScreenY()
        {
            var observation = NewObservation();
            Track(observation, 11, 0.5f, 0.2f, Vector3.zero);
            Track(observation, 13, 0.5f, 0.8f, Vector3.zero);

            var frame = new CanonicalPoseFrame();
            MediaPipeCanonicalPoseMapper.Map(observation, frame);

            var higher = frame.GetJoint(CanonicalJointId.LeftShoulder);
            var lower = frame.GetJoint(CanonicalJointId.LeftElbow);
            var contentRect = new Rect(0f, 0f, 100f, 100f);
            var higherScreen = CanonicalCoordinateSystem.CanonicalImageToGuiScreen(higher.imagePosition, contentRect);
            var lowerScreen = CanonicalCoordinateSystem.CanonicalImageToGuiScreen(lower.imagePosition, contentRect);

            Assert.That(higher.imagePosition.y, Is.GreaterThan(lower.imagePosition.y));
            Assert.That(higherScreen.y, Is.LessThan(lowerScreen.y));
        }

        [Test]
        public void CanonicalGuiMappingUsesOneYAxisInversion()
        {
            var contentRect = new Rect(10f, 20f, 100f, 200f);

            Assert.That(
                CanonicalCoordinateSystem.CanonicalImageToGuiScreen(Vector2.zero, contentRect),
                Is.EqualTo(new Vector2(10f, 220f)));
            Assert.That(
                CanonicalCoordinateSystem.CanonicalImageToGuiScreen(Vector2.one, contentRect),
                Is.EqualTo(new Vector2(110f, 20f)));
        }

        [Test]
        public void CanonicalCoordinateAgreementUsesStrongVerticalChains()
        {
            var observation = NewObservation();
            Track(observation, 11, 0.35f, 0.2f, new Vector3(-0.2f, -0.8f, 0.1f));
            Track(observation, 12, 0.65f, 0.2f, new Vector3(0.2f, -0.8f, 0.1f));
            Track(observation, 23, 0.35f, 0.45f, new Vector3(-0.15f, -0.55f, 0.1f));
            Track(observation, 24, 0.65f, 0.45f, new Vector3(0.15f, -0.55f, 0.1f));
            Track(observation, 25, 0.35f, 0.65f, new Vector3(-0.15f, -0.35f, 0.1f));
            Track(observation, 26, 0.65f, 0.65f, new Vector3(0.15f, -0.35f, 0.1f));
            Track(observation, 27, 0.35f, 0.85f, new Vector3(-0.15f, -0.15f, 0.1f));
            Track(observation, 28, 0.65f, 0.85f, new Vector3(0.15f, -0.15f, 0.1f));
            var frame = new CanonicalPoseFrame();

            MediaPipeCanonicalPoseMapper.Map(observation, frame);

            var agreement = CanonicalCoordinateAgreementEvaluator.Evaluate(frame);
            Assert.That(frame.GetJoint(CanonicalJointId.Chest).imagePosition.y, Is.GreaterThan(frame.GetJoint(CanonicalJointId.Pelvis).imagePosition.y));
            Assert.That(frame.GetJoint(CanonicalJointId.LeftHip).imagePosition.y, Is.GreaterThan(frame.GetJoint(CanonicalJointId.LeftKnee).imagePosition.y));
            Assert.That(frame.GetJoint(CanonicalJointId.LeftKnee).imagePosition.y, Is.GreaterThan(frame.GetJoint(CanonicalJointId.LeftAnkle).imagePosition.y));
            Assert.That(frame.GetJoint(CanonicalJointId.Chest).worldPosition.y, Is.GreaterThan(frame.GetJoint(CanonicalJointId.Pelvis).worldPosition.y));
            Assert.That(frame.GetJoint(CanonicalJointId.LeftHip).worldPosition.y, Is.GreaterThan(frame.GetJoint(CanonicalJointId.LeftKnee).worldPosition.y));
            Assert.That(frame.GetJoint(CanonicalJointId.LeftKnee).worldPosition.y, Is.GreaterThan(frame.GetJoint(CanonicalJointId.LeftAnkle).worldPosition.y));
            Assert.That(agreement.yComparisons, Is.GreaterThanOrEqualTo(7));
            Assert.That(agreement.yMismatches, Is.EqualTo(0));
            Assert.That(agreement.yPass, Is.True);
        }

        [Test]
        public void CanonicalCoordinateAgreementDoesNotFailOnTinyBilateralYNoise()
        {
            var observation = NewObservation();
            Track(observation, 11, 0.2f, 0.300f, new Vector3(-0.2f, -0.500f, 0.1f));
            Track(observation, 12, 0.8f, 0.301f, new Vector3(0.2f, -0.501f, 0.1f));
            var frame = new CanonicalPoseFrame();

            MediaPipeCanonicalPoseMapper.Map(observation, frame);

            var agreement = CanonicalCoordinateAgreementEvaluator.Evaluate(frame);
            Assert.That(agreement.yComparisons, Is.EqualTo(0));
            Assert.That(agreement.yMismatches, Is.EqualTo(0));
            Assert.That(agreement.hasYEvidence, Is.False);
            Assert.That(agreement.yInsufficientComparisons, Is.EqualTo(0));
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

        [Test]
        public void AutoRotationPreservesReportedCameraMetadata()
        {
            Assert.That(
                CameraRotationPolicy.ResolveEffectiveRotationDegrees(
                    270,
                    CameraRotationOverride.Auto),
                Is.EqualTo(270));
        }

        [Test]
        public void ManualRotationOverridesProduceExactQuarterTurns()
        {
            Assert.That(
                CameraRotationPolicy.ResolveEffectiveRotationDegrees(
                    270,
                    CameraRotationOverride.Degrees0),
                Is.EqualTo(0));
            Assert.That(
                CameraRotationPolicy.ResolveEffectiveRotationDegrees(
                    0,
                    CameraRotationOverride.Degrees90),
                Is.EqualTo(90));
            Assert.That(
                CameraRotationPolicy.ResolveEffectiveRotationDegrees(
                    90,
                    CameraRotationOverride.Degrees180),
                Is.EqualTo(180));
            Assert.That(
                CameraRotationPolicy.ResolveEffectiveRotationDegrees(
                    180,
                    CameraRotationOverride.Degrees270),
                Is.EqualTo(270));
        }

        [Test]
        public void ManualQuarterTurnCanDriveInferenceAndDisplayInSameFrame()
        {
            var effective =
                CameraRotationPolicy.ResolveEffectiveRotationDegrees(
                    0,
                    CameraRotationOverride.Degrees90);
            var orientation = Orientation(
                inferenceRotationDegrees: effective,
                sensorRotationDegrees: effective);
            var sensorPoint = new Vector2(0.2f, 0.3f);

            var inference =
                orientation.SensorTopLeftToInferenceNormalized(
                    sensorPoint);
            var display =
                orientation.SensorTopLeftToDisplayNormalized(
                    sensorPoint);

            Assert.That(
                inference,
                Is.EqualTo(new Vector2(0.7f, 0.2f)));
            Assert.That(
                display,
                Is.EqualTo(new Vector2(0.7f, 0.2f)));
        }

        private static CameraOrientationState Orientation(
            bool displayMirrored = false,
            bool inferenceFlipHorizontally = false,
            bool inferenceFlipVertically = false,
            int inferenceRotationDegrees = 0,
            int sensorRotationDegrees = 0,
            bool sensorVerticallyMirrored = false,
            bool frontFacing = false,
            bool sourceTextureHorizontallyMirrored = false)
        {
            return new CameraOrientationState(
                sensorRotationDegrees: sensorRotationDegrees,
                sensorVerticallyMirrored: sensorVerticallyMirrored,
                frontFacing: frontFacing,
                sourceTextureHorizontallyMirrored: sourceTextureHorizontallyMirrored,
                displayMirrored: displayMirrored,
                inferenceFlipHorizontally: inferenceFlipHorizontally,
                inferenceFlipVertically: inferenceFlipVertically,
                inferenceRotationDegrees: inferenceRotationDegrees);
        }

        private static void AssertCanonicalWorld(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
        }
    }
}
