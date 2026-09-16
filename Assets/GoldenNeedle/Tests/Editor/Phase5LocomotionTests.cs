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
        public void NeutralStandingIgnoresRepeatedSmallBodyAndSupportNoise()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);

            var a = tracker.Update(SupportFrame(0.502f, 1f, 0.002f), profile, 0.1f);
            var b = tracker.Update(SupportFrame(0.499f, 1f, -0.001f), profile, 0.1f);
            var c = tracker.Update(SupportFrame(0.503f, 1f, 0.003f), profile, 0.1f);

            Assert.That(a.displacementXZ.magnitude, Is.LessThan(0.001f));
            Assert.That(b.displacementXZ.magnitude, Is.LessThan(0.001f));
            Assert.That(c.displacementXZ.magnitude, Is.LessThan(0.001f));
            Assert.That(c.movementAccepted, Is.False);
        }

        [Test]
        public void NonzeroRelocationSettlesWithoutHuntingFromIdleNoise()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);
            var moved = tracker.Update(
                SupportFrame(0.54f, 1f, 0.04f), profile, 0.1f);
            var settled = tracker.Update(
                SupportFrame(0.54f, 1f, 0.04f), profile, 0.1f);
            var jitter = tracker.Update(
                SupportFrame(0.542f, 1f, 0.042f), profile, 0.1f);

            Assert.That(moved.movementAccepted, Is.True);
            Assert.That(settled.displacementXZ.x, Is.GreaterThan(0.05f));
            Assert.That(jitter.displacementXZ.x,
                Is.EqualTo(settled.displacementXZ.x).Within(0.001f));
            Assert.That(jitter.movementAccepted, Is.False);
        }

        [Test]
        public void PlantedFeetIgnoreTorsoLateralLean()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);
            var leaned = tracker.Update(
                SupportFrame(torsoCenterX: 0.62f), profile, 0.1f);

            Assert.That(leaned.supportValidated, Is.True);
            Assert.That(leaned.bodyCandidateXZ.x, Is.Not.EqualTo(0f));
            Assert.That(leaned.movementAccepted, Is.False);
            Assert.That(leaned.displacementXZ.magnitude, Is.LessThan(0.005f));
        }

        [Test]
        public void PlantedFeetIgnoreTorsoForwardBackScaleChange()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);
            var leaned = tracker.Update(
                SupportFrame(torsoScale: 1.18f), profile, 0.1f);

            Assert.That(Mathf.Abs(leaned.bodyCandidateXZ.y), Is.GreaterThan(0.05f));
            Assert.That(leaned.depthCorroborated, Is.False);
            Assert.That(leaned.movementAccepted, Is.False);
            Assert.That(Mathf.Abs(leaned.displacementXZ.y), Is.LessThan(0.005f));
        }

        [Test]
        public void RaisedSwingFootDoesNotTranslatePhysicalRoot()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);
            var swing = tracker.Update(
                SupportFrame(
                    torsoCenterX: 0.53f,
                    leftExtraX: 0.08f,
                    leftExtraY: 0.08f),
                profile,
                0.1f);

            Assert.That(swing.supportMode, Is.Not.EqualTo(PhysicalSupportMode.Both));
            Assert.That(swing.movementAccepted, Is.False);
            Assert.That(swing.displacementXZ.magnitude, Is.LessThan(0.005f));
        }

        [Test]
        public void CoherentBodyAndBilateralSupportRelocationProducesMovement()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);
            var moved = tracker.Update(
                SupportFrame(0.54f, 1f, 0.04f), profile, 0.1f);

            Assert.That(moved.supportMode, Is.EqualTo(PhysicalSupportMode.Both));
            Assert.That(moved.supportValidated, Is.True);
            Assert.That(moved.movementAccepted, Is.True);
            Assert.That(moved.displacementXZ.x, Is.GreaterThan(0.05f));
        }

        [Test]
        public void AlternatingPhysicalStepEventuallyCommitsNetRelocation()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);

            var leftSwing = tracker.Update(
                SupportFrame(0.52f, 1f, leftExtraX: 0.08f, leftExtraY: 0.08f),
                profile, 0.1f);
            var leftLanded = tracker.Update(
                SupportFrame(0.52f, 1f, leftExtraX: 0.08f),
                profile, 0.1f);
            var rightSwing = tracker.Update(
                SupportFrame(0.56f, 1f,
                    leftExtraX: 0.08f,
                    rightExtraX: 0.08f,
                    rightExtraY: 0.08f),
                profile, 0.1f);
            var rightLanded = tracker.Update(
                SupportFrame(0.58f, 1f,
                    leftExtraX: 0.08f,
                    rightExtraX: 0.08f),
                profile, 0.1f);

            Assert.That(leftSwing.displacementXZ.magnitude, Is.LessThan(0.005f));
            Assert.That(leftLanded.displacementXZ.magnitude, Is.LessThan(0.005f));
            Assert.That(rightSwing.displacementXZ.magnitude, Is.LessThan(0.005f));
            Assert.That(rightLanded.movementAccepted, Is.True);
            Assert.That(rightLanded.displacementXZ.x, Is.GreaterThan(0.10f));
        }

        [Test]
        public void LandingSupportTransitionsRemainContinuousUntilCoherentRelocation()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);
            var swing = tracker.Update(
                SupportFrame(0.52f, 1f, leftExtraX: 0.08f, leftExtraY: 0.08f),
                profile, 0.1f);
            var ambiguous = tracker.Update(
                SupportFrame(0.52f, 1f, leftExtraX: 0.08f, leftExtraY: 0.02f),
                profile, 0.1f);
            var landed = tracker.Update(
                SupportFrame(0.52f, 1f, leftExtraX: 0.08f),
                profile, 0.1f);

            Assert.That(Vector2.Distance(swing.displacementXZ, ambiguous.displacementXZ),
                Is.LessThan(0.005f));
            Assert.That(Vector2.Distance(ambiguous.displacementXZ, landed.displacementXZ),
                Is.LessThan(0.005f));
        }

        [Test]
        public void SlowDeliberateMovementAccumulatesUntilItCommits()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);

            var first = tracker.Update(SupportFrame(0.502f, 1f, 0.002f), profile, 0.1f);
            var second = tracker.Update(SupportFrame(0.503f, 1f, 0.003f), profile, 0.1f);
            var third = tracker.Update(SupportFrame(0.5045f, 1f, 0.0045f), profile, 0.1f);

            Assert.That(first.movementAccepted, Is.False);
            Assert.That(second.movementAccepted, Is.False);
            Assert.That(third.movementAccepted, Is.True);
            Assert.That(third.displacementXZ.x, Is.GreaterThan(0.01f));
        }

        [Test]
        public void SupportLossAndReacquisitionRebaseWithoutTeleport()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);
            var moved = tracker.Update(
                SupportFrame(0.54f, 1f, 0.04f), profile, 0.1f);

            var lostFrame = TorsoOnlyFrame(0.70f, 1.20f);
            lostFrame.Complete();
            var lost = tracker.Update(lostFrame, profile, 0.1f);
            var reacquired = tracker.Update(
                SupportFrame(0.60f, 1f, 0.10f), profile, 0.1f);
            var continued = tracker.Update(
                SupportFrame(0.62f, 1f, 0.12f), profile, 0.1f);

            Assert.That(lost.isValid, Is.False);
            Assert.That(lost.displacementXZ.x,
                Is.EqualTo(moved.displacementXZ.x).Within(0.001f));
            Assert.That(reacquired.displacementXZ.x,
                Is.EqualTo(moved.displacementXZ.x).Within(0.001f));
            Assert.That(continued.displacementXZ.x,
                Is.GreaterThan(reacquired.displacementXZ.x + 0.02f));
        }

        [Test]
        public void RecenterZerosPhysicalDisplacementForWorldPositionPreservation()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);
            tracker.Update(SupportFrame(0.54f, 1f, 0.04f), profile, 0.1f);

            Assert.That(tracker.Recenter(), Is.True);
            Assert.That(tracker.LatestSample.displacementXZ.magnitude,
                Is.LessThan(0.0001f));

            var fusion = StableFusion();
            fusion.ResetPhysicalContribution();
            var result = fusion.Evaluate(
                tracker.LatestSample, default, Vector2.up, IdentityMap());
            Assert.That(result.physicalContribution.magnitude, Is.LessThan(0.0001f));
        }

        [Test]
        public void PhysicalMotionSuppressesCadenceButIdleNoiseDoesNot()
        {
            var fusion = new LocomotionFusion(new LocomotionFusionSettings
            {
                lateralScale = 1f,
                depthScale = 1f,
                lateralDeadzone = 0f,
                depthDeadzone = 0f,
                physicalVelocityStart = 0.05f,
                physicalVelocityFull = 0.20f,
                minimumRootConfidence = 0.2f,
            });
            var cadence = new CadenceSample
            {
                active = true,
                confidence = 1f,
                virtualSpeed = 1f,
            };
            var moving = fusion.Evaluate(
                RootSample(new Vector2(0.1f, 0f), new Vector2(0.25f, 0f)),
                cadence, Vector2.up, IdentityMap());
            var idle = fusion.Evaluate(
                RootSample(new Vector2(0.1f, 0f), Vector2.zero),
                cadence, Vector2.up, IdentityMap());

            Assert.That(moving.physicalTranslationActive, Is.True);
            Assert.That(moving.cadenceVelocity.magnitude, Is.LessThan(0.05f));
            Assert.That(idle.physicalTranslationActive, Is.False);
            Assert.That(idle.cadenceVelocity.magnitude, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SupportBaseRelocationAndScaleEvidenceProduceDepthMovement()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);
            var farther = tracker.Update(
                SupportFrame(0.50f, 0.84f, supportOffsetY: 0.035f),
                profile, 0.1f);

            Assert.That(farther.depthCorroborated, Is.True);
            Assert.That(farther.movementAccepted, Is.True);
            Assert.That(farther.displacementXZ.y, Is.GreaterThan(0.10f));
        }

        [Test]
        public void CameraAxisAndHeadingSemanticsRemainAccepted()
        {
            var map = FrontCameraToProperTargetMap();
            AssertVector2(
                LocomotionFusion.MapCameraVectorToWorldXZ(Vector3.left, map),
                Vector2.right);
            AssertVector2(
                LocomotionFusion.MapCameraVectorToWorldXZ(Vector3.back, map),
                Vector2.up);
            Assert.That(BodyHeadingEstimator.TryMapHeading(
                map, Vector3.back, out var heading), Is.True);
            AssertVector2(heading, Vector2.up);
        }

        [Test]
        public void JoggingInPlaceRemainsPhysicalZeroWhileCadenceAcquires()
        {
            var tracker = FastRootTracker();
            var cadence = new CadenceDetector(new CadenceDetectorSettings());
            var profile = FrontCameraProfile();
            tracker.Update(SupportFrame(), profile, 0.1f);

            var firstFrame = JogInPlaceFrame(true);
            var firstRoot = tracker.Update(firstFrame, profile, 0.10f);
            var firstCadence = cadence.Update(firstFrame, 0.30f, 0.10f);
            var secondFrame = JogInPlaceFrame(false);
            var secondRoot = tracker.Update(secondFrame, profile, 0.25f);
            var secondCadence = cadence.Update(secondFrame, 0.30f, 0.25f);

            Assert.That(firstRoot.displacementXZ.magnitude, Is.LessThan(0.01f));
            Assert.That(secondRoot.displacementXZ.magnitude, Is.LessThan(0.01f));
            Assert.That(firstCadence.active, Is.False);
            Assert.That(secondCadence.active, Is.True);
        }

        private static CameraSpaceRootTracker FastRootTracker()
        {
            return new CameraSpaceRootTracker(new CameraSpaceRootTrackerSettings
            {
                minimumJointConfidence = 0.2f,
                supportSingleFootEnter = 0.12f,
                supportBothEnter = 0.06f,
                depthDifferentialStart = 0.05f,
                depthDifferentialFull = 0.20f,
                minimumSupportDepthDisplacement = 0.01f,
                minimumDepthScaleEvidence = 0.01f,
                positionResponse = 1000f,
                velocityResponse = 1000f,
                maximumDepthProxy = 5f,
            });
        }

        private static LocomotionFusion StableFusion()
        {
            return new LocomotionFusion(new LocomotionFusionSettings
            {
                lateralScale = 1f,
                depthScale = 1f,
                lateralDeadzone = 0f,
                depthDeadzone = 0f,
                physicalVelocityStart = 0.05f,
                physicalVelocityFull = 0.20f,
                minimumRootConfidence = 0.2f,
            });
        }

        private static CameraSpaceRootSample RootSample(Vector2 displacement, Vector2 velocity)
        {
            return new CameraSpaceRootSample
            {
                isValid = true,
                hasOrigin = true,
                displacementXZ = displacement,
                velocityXZ = velocity,
                confidence = 1f,
            };
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

        private static CanonicalPoseFrame SupportFrame(
            float torsoCenterX = 0.50f,
            float torsoScale = 1.00f,
            float supportOffsetX = 0f,
            float supportOffsetY = 0f,
            float leftExtraX = 0f,
            float rightExtraX = 0f,
            float leftExtraY = 0f,
            float rightExtraY = 0f)
        {
            var frame = TorsoOnlyFrame(torsoCenterX, torsoScale);
            AddSupportFoot(frame, true, new Vector2(
                0.58f + supportOffsetX + leftExtraX,
                0.12f + supportOffsetY + leftExtraY));
            AddSupportFoot(frame, false, new Vector2(
                0.42f + supportOffsetX + rightExtraX,
                0.12f + supportOffsetY + rightExtraY));
            frame.Complete();
            return frame;
        }

        private static CanonicalPoseFrame TorsoOnlyFrame(
            float torsoCenterX = 0.50f,
            float torsoScale = 1.00f)
        {
            var frame = new CanonicalPoseFrame();
            frame.Begin(1L, 0d, true);
            var shoulderHalf = 0.15f * torsoScale;
            var hipHalf = 0.10f * torsoScale;
            var pelvisY = 0.42f;
            var chestY = pelvisY + 0.30f * torsoScale;

            SetTracked(frame, CanonicalJointId.LeftShoulder,
                new Vector2(torsoCenterX + shoulderHalf, chestY),
                new Vector3(0.20f, 0.50f, 0f));
            SetTracked(frame, CanonicalJointId.RightShoulder,
                new Vector2(torsoCenterX - shoulderHalf, chestY),
                new Vector3(-0.20f, 0.50f, 0f));
            SetTracked(frame, CanonicalJointId.LeftHip,
                new Vector2(torsoCenterX + hipHalf, pelvisY),
                new Vector3(0.15f, 0f, 0f));
            SetTracked(frame, CanonicalJointId.RightHip,
                new Vector2(torsoCenterX - hipHalf, pelvisY),
                new Vector3(-0.15f, 0f, 0f));
            SetTracked(frame, CanonicalJointId.Pelvis,
                new Vector2(torsoCenterX, pelvisY), Vector3.zero);
            SetTracked(frame, CanonicalJointId.Chest,
                new Vector2(torsoCenterX, chestY), new Vector3(0f, 0.50f, 0f));
            return frame;
        }

        private static CanonicalPoseFrame JogInPlaceFrame(bool leftHigh)
        {
            var frame = TorsoOnlyFrame();
            var leftFootY = leftHigh ? 0.17f : 0.07f;
            var rightFootY = leftHigh ? 0.07f : 0.17f;
            AddSupportFoot(frame, true, new Vector2(0.58f, leftFootY));
            AddSupportFoot(frame, false, new Vector2(0.42f, rightFootY));
            SetImageOnly(frame, CanonicalJointId.LeftKnee,
                new Vector2(0.56f, leftHigh ? 0.50f : 0.44f));
            SetImageOnly(frame, CanonicalJointId.RightKnee,
                new Vector2(0.44f, leftHigh ? 0.44f : 0.50f));
            frame.Complete();
            return frame;
        }

        private static void AddSupportFoot(
            CanonicalPoseFrame frame,
            bool left,
            Vector2 center)
        {
            var ankle = left ? CanonicalJointId.LeftAnkle : CanonicalJointId.RightAnkle;
            var heel = left ? CanonicalJointId.LeftHeel : CanonicalJointId.RightHeel;
            var toe = left ? CanonicalJointId.LeftToe : CanonicalJointId.RightToe;
            SetImageOnly(frame, ankle, center + new Vector2(0f, 0.010f));
            SetImageOnly(frame, heel, center + new Vector2(-0.012f, -0.006f));
            SetImageOnly(frame, toe, center + new Vector2(0.018f, -0.004f));
        }

        private static CanonicalToAvatarAxisMap FrontCameraToProperTargetMap()
        {
            Assert.That(HumanoidRetargetingMath.TryBuildSignedBasis(
                Vector3.left, Vector3.up, Vector3.back, out var source), Is.True);
            Assert.That(HumanoidRetargetingMath.TryBuildRightHandedBasis(
                Vector3.right, Vector3.up, out var target), Is.True);
            return new CanonicalToAvatarAxisMap(source, target);
        }

        private static CanonicalToAvatarAxisMap IdentityMap()
        {
            Assert.That(HumanoidRetargetingMath.TryBuildRightHandedBasis(
                Vector3.right, Vector3.up, out var basis), Is.True);
            return new CanonicalToAvatarAxisMap(basis, basis);
        }

        private static void AssertVector2(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
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
