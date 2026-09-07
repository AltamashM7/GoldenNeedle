using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Locomotion;
using GoldenNeedle.Core.Motion.Retargeting;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class Phase5LocomotionTests
    {
        [Test]
        public void CameraSpaceRootTrackerUsesImagePlacementScaleAndRecenters()
        {
            var settings = new CameraSpaceRootTrackerSettings
            {
                minimumJointConfidence = 0.2f,
                positionResponse = 1000f,
                velocityResponse = 1000f,
                maximumDepthProxy = 5f,
            };
            var tracker = new CameraSpaceRootTracker(settings);
            var profile = FrontCameraProfile();

            var baseline = RootFrame(0.50f, 1.00f);
            var baselineSample = tracker.Update(baseline, profile, 0.1f);
            Assert.That(baselineSample.isValid, Is.True);
            Assert.That(baselineSample.hasOrigin, Is.True);
            Assert.That(baselineSample.displacementXZ.magnitude, Is.LessThan(0.0001f));

            var shifted = tracker.Update(RootFrame(0.56f, 1.00f), profile, 0.1f);
            Assert.That(shifted.displacementXZ.x, Is.GreaterThan(0.05f));
            Assert.That(Mathf.Abs(shifted.displacementXZ.y), Is.LessThan(0.01f));

            Assert.That(tracker.Recenter(), Is.True);
            Assert.That(tracker.LatestSample.displacementXZ.magnitude, Is.LessThan(0.0001f));

            var yawOnly = tracker.Update(RootFrame(0.56f, 1.00f, 60f), profile, 0.1f);
            Assert.That(Mathf.Abs(yawOnly.displacementXZ.y), Is.LessThan(0.03f));

            var farther = tracker.Update(RootFrame(0.56f, 0.80f, 60f), profile, 0.1f);
            Assert.That(farther.displacementXZ.y, Is.GreaterThan(0.10f));
        }

        [Test]
        public void CadenceDetectorAcquiresAlternatingRhythmAndStopsQuickly()
        {
            var settings = new CadenceDetectorSettings
            {
                minimumJointConfidence = 0.2f,
                signalResponse = 1000f,
                eventThreshold = 0.05f,
                minimumStepRate = 0.5f,
                maximumStepRate = 5f,
                acquisitionEvents = 3,
                acquireConfidence = 0.40f,
                sustainConfidence = 0.20f,
                stopTimeoutSeconds = 0.45f,
                virtualStridePerStep = 0.4f,
            };
            var detector = new CadenceDetector(settings);

            detector.Update(CadenceFrame(true), 0.30f, 0.10f);
            detector.Update(CadenceFrame(false), 0.30f, 0.25f);
            var active = detector.Update(CadenceFrame(true), 0.30f, 0.25f);

            Assert.That(active.active, Is.True);
            Assert.That(active.rateStepsPerSecond, Is.GreaterThan(3f));
            Assert.That(active.virtualSpeed, Is.GreaterThan(1f));

            detector.Update(EmptyFrame(), 0.30f, 0.25f);
            var stopped = detector.Update(EmptyFrame(), 0.30f, 0.30f);
            Assert.That(stopped.active, Is.False);
        }

        [Test]
        public void FusionSuppressesCadenceDuringMeaningfulPhysicalTranslation()
        {
            var fusion = new LocomotionFusion(new LocomotionFusionSettings
            {
                lateralScale = 2f,
                depthScale = 4f,
                lateralDeadzone = 0f,
                depthDeadzone = 0f,
                physicalVelocityStart = 0.10f,
                physicalVelocityFull = 0.20f,
                minimumRootConfidence = 0.2f,
            });
            var cadence = new CadenceSample
            {
                active = true,
                confidence = 1f,
                rateStepsPerSecond = 2f,
                virtualSpeed = 1f,
            };
            var movingRoot = new CameraSpaceRootSample
            {
                isValid = true,
                hasOrigin = true,
                displacementXZ = new Vector2(0.10f, 0.10f),
                velocityXZ = new Vector2(0.20f, 0f),
                confidence = 1f,
            };

            var moving = fusion.Evaluate(movingRoot, cadence, Vector2.up);
            Assert.That(moving.physicalContribution.x, Is.EqualTo(0.20f).Within(0.0001f));
            Assert.That(moving.physicalContribution.y, Is.EqualTo(0.40f).Within(0.0001f));
            Assert.That(moving.physicalTranslationActive, Is.True);
            Assert.That(moving.cadenceVelocity.magnitude, Is.LessThan(0.01f));

            var lostRoot = movingRoot;
            lostRoot.isValid = false;
            lostRoot.confidence = 0f;
            var held = fusion.Evaluate(lostRoot, default, Vector2.up);
            Assert.That(held.physicalContribution.x, Is.EqualTo(0.20f).Within(0.0001f));
            Assert.That(held.physicalContribution.y, Is.EqualTo(0.40f).Within(0.0001f));
            Assert.That(held.rootTrackingLive, Is.False);

            movingRoot.velocityXZ = Vector2.zero;
            var inPlace = fusion.Evaluate(movingRoot, cadence, Vector2.up);
            Assert.That(inPlace.physicalTranslationActive, Is.False);
            Assert.That(inPlace.cadenceVelocity.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CorrectedCanonicalHeadingMapsToProperTargetForward()
        {
            Assert.That(
                HumanoidRetargetingMath.TryBuildSignedBasis(
                    Vector3.left,
                    Vector3.up,
                    Vector3.back,
                    out var source),
                Is.True);
            Assert.That(
                HumanoidRetargetingMath.TryBuildRightHandedBasis(
                    Vector3.right,
                    Vector3.up,
                    out var target),
                Is.True);

            var map = new CanonicalToAvatarAxisMap(source, target);
            Assert.That(
                BodyHeadingEstimator.TryMapHeading(
                    map,
                    Vector3.back,
                    out var heading),
                Is.True);

            Assert.That(heading.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(heading.y, Is.EqualTo(1f).Within(0.0001f));
        }

        private static MotionCalibrationProfile FrontCameraProfile()
        {
            return new MotionCalibrationProfile
            {
                isValid = true,
                bodyReferenceValid = true,
                version = MotionCalibrationProfile.CurrentVersion,
                state = MotionCalibrationState.Ready,
                neutralBodyRight = Vector3.left,
                neutralBodyUp = Vector3.up,
                neutralBodyForward = Vector3.back,
            };
        }

        private static CanonicalPoseFrame RootFrame(
            float centerX,
            float scale,
            float yawDegrees = 0f)
        {
            var frame = new CanonicalPoseFrame();
            frame.Begin(1L, 0d, true);
            var yawCosine = Mathf.Abs(Mathf.Cos(yawDegrees * Mathf.Deg2Rad));
            var shoulderHalf = 0.15f * scale * yawCosine;
            var hipHalf = 0.10f * scale * yawCosine;
            var pelvisY = 0.42f;
            var chestY = pelvisY + 0.30f * scale;
            var yaw = Quaternion.Euler(0f, yawDegrees, 0f);

            SetTracked(
                frame,
                CanonicalJointId.LeftShoulder,
                new Vector2(centerX + shoulderHalf, chestY),
                yaw * new Vector3(0.20f, 0f, 0f) + new Vector3(0f, 0.50f, 0f));
            SetTracked(
                frame,
                CanonicalJointId.RightShoulder,
                new Vector2(centerX - shoulderHalf, chestY),
                yaw * new Vector3(-0.20f, 0f, 0f) + new Vector3(0f, 0.50f, 0f));
            SetTracked(
                frame,
                CanonicalJointId.LeftHip,
                new Vector2(centerX + hipHalf, pelvisY),
                yaw * new Vector3(0.15f, 0f, 0f));
            SetTracked(
                frame,
                CanonicalJointId.RightHip,
                new Vector2(centerX - hipHalf, pelvisY),
                yaw * new Vector3(-0.15f, 0f, 0f));
            SetTracked(
                frame,
                CanonicalJointId.Pelvis,
                new Vector2(centerX, pelvisY),
                Vector3.zero);
            SetTracked(
                frame,
                CanonicalJointId.Chest,
                new Vector2(centerX, chestY),
                new Vector3(0f, 0.50f, 0f));
            frame.Complete();
            return frame;
        }

        private static CanonicalPoseFrame CadenceFrame(bool leftHigh)
        {
            var frame = new CanonicalPoseFrame();
            frame.Begin(1L, 0d, true);
            var leftAnkleY = leftHigh ? 0.26f : 0.14f;
            var rightAnkleY = leftHigh ? 0.14f : 0.26f;
            var leftKneeY = leftHigh ? 0.50f : 0.44f;
            var rightKneeY = leftHigh ? 0.44f : 0.50f;
            SetImageOnly(frame, CanonicalJointId.LeftAnkle, new Vector2(0.58f, leftAnkleY));
            SetImageOnly(frame, CanonicalJointId.RightAnkle, new Vector2(0.42f, rightAnkleY));
            SetImageOnly(frame, CanonicalJointId.LeftKnee, new Vector2(0.56f, leftKneeY));
            SetImageOnly(frame, CanonicalJointId.RightKnee, new Vector2(0.44f, rightKneeY));
            frame.Complete();
            return frame;
        }

        private static CanonicalPoseFrame EmptyFrame()
        {
            var frame = new CanonicalPoseFrame();
            frame.Begin(1L, 0d, false);
            frame.Complete();
            return frame;
        }

        private static void SetTracked(
            CanonicalPoseFrame frame,
            CanonicalJointId id,
            Vector2 image,
            Vector3 world)
        {
            frame.SetJoint(new CanonicalPoseJoint
            {
                id = id,
                tracking = CanonicalTrackingState.Tracked,
                confidence = 1f,
                imagePosition = image,
                hasImagePosition = true,
                worldPosition = world,
                hasWorldPosition = true,
                localPosition = world,
                hasLocalPosition = true,
            });
        }

        private static void SetImageOnly(
            CanonicalPoseFrame frame,
            CanonicalJointId id,
            Vector2 image)
        {
            frame.SetJoint(new CanonicalPoseJoint
            {
                id = id,
                tracking = CanonicalTrackingState.Tracked,
                confidence = 1f,
                imagePosition = image,
                hasImagePosition = true,
            });
        }
    }
}
