using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class VerticalLocomotionTests
    {
        [Test]
        public void NeutralStandingCapturesReferenceAndProducesZeroOffset()
        {
            var detector = FastVerticalInterpreter();
            var sample = detector.Update(VerticalFrame(), Profile(), 0.1f);

            Assert.That(sample.isAvailable, Is.True);
            Assert.That(sample.referenceReady, Is.True);
            Assert.That(sample.state, Is.EqualTo(VerticalLocomotionState.Standing));
            Assert.That(sample.jumpPhase, Is.EqualTo(VerticalJumpPhase.Grounded));
            Assert.That(sample.groundedBendActive, Is.False);
            Assert.That(Mathf.Abs(sample.worldOffsetY), Is.LessThan(0.0001f));
        }

        [Test]
        public void ShallowGroundedCompressionLowersRootBeforeSemanticCrouch()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            var shallow = detector.Update(
                VerticalFrame(crouchDrop: 0.04f),
                Profile(),
                0.1f);

            Assert.That(shallow.crouchCompression, Is.EqualTo(0.10f).Within(0.01f));
            Assert.That(shallow.state, Is.EqualTo(VerticalLocomotionState.Standing));
            Assert.That(shallow.IsCrouchActive, Is.False);
            Assert.That(shallow.groundedBendActive, Is.True);
            Assert.That(shallow.worldOffsetY, Is.LessThan(-0.05f));
        }

        [Test]
        public void DeeperCompressionProducesLargerNegativeOffsetAndSemanticCrouch()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            var shallow = detector.Update(
                VerticalFrame(crouchDrop: 0.04f), Profile(), 0.1f);
            var deep = detector.Update(
                VerticalFrame(crouchDrop: 0.10f), Profile(), 0.1f);

            Assert.That(shallow.state, Is.EqualTo(VerticalLocomotionState.Standing));
            Assert.That(deep.crouchCompression, Is.GreaterThan(0.20f));
            Assert.That(deep.state, Is.EqualTo(VerticalLocomotionState.Crouch));
            Assert.That(deep.groundedBendActive, Is.True);
            Assert.That(deep.worldOffsetY, Is.LessThan(shallow.worldOffsetY - 0.10f));
        }

        [Test]
        public void SemanticCrouchThresholdIsIndependentFromContinuousBodyDescent()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);

            var belowThreshold = detector.Update(
                VerticalFrame(crouchDrop: 0.05f), Profile(), 0.1f);
            var aboveThreshold = detector.Update(
                VerticalFrame(crouchDrop: 0.08f), Profile(), 0.1f);

            Assert.That(belowThreshold.crouchCompression, Is.GreaterThan(0.09f));
            Assert.That(belowThreshold.crouchCompression, Is.LessThan(0.18f));
            Assert.That(belowThreshold.state, Is.EqualTo(VerticalLocomotionState.Standing));
            Assert.That(belowThreshold.worldOffsetY, Is.LessThan(0f));
            Assert.That(aboveThreshold.crouchCompression, Is.GreaterThanOrEqualTo(0.18f));
            Assert.That(aboveThreshold.state, Is.EqualTo(VerticalLocomotionState.Crouch));
            Assert.That(aboveThreshold.worldOffsetY, Is.LessThan(belowThreshold.worldOffsetY));
        }

        [Test]
        public void PlantedCrouchDoesNotCreatePhysicalXZOrDepthTravel()
        {
            var profile = Profile();
            var tracker = FastRootTracker();
            tracker.Update(VerticalFrame(), profile, 0.1f);
            var crouch = tracker.Update(
                VerticalFrame(crouchDrop: 0.10f), profile, 0.1f);

            Assert.That(crouch.isValid, Is.True);
            Assert.That(crouch.movementAccepted, Is.False);
            Assert.That(crouch.displacementXZ.magnitude, Is.LessThan(0.005f));
            Assert.That(crouch.depthCorroborated, Is.False);
        }

        [Test]
        public void StandingRecoveryReturnsTowardZeroSmoothly()
        {
            var detector = VerticalInterpreter(response: 8f);
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            var bent = detector.Update(
                VerticalFrame(crouchDrop: 0.10f), Profile(), 0.1f);
            var recovering = detector.Update(
                VerticalFrame(), Profile(), 0.1f);
            var recovered = recovering;
            for (var i = 0; i < 12; i++)
            {
                recovered = detector.Update(VerticalFrame(), Profile(), 0.1f);
            }

            Assert.That(bent.worldOffsetY, Is.LessThan(0f));
            Assert.That(recovering.worldOffsetY, Is.GreaterThan(bent.worldOffsetY));
            Assert.That(recovering.worldOffsetY, Is.LessThanOrEqualTo(0f));
            Assert.That(Mathf.Abs(recovered.worldOffsetY), Is.LessThan(0.001f));
            Assert.That(recovered.state, Is.EqualTo(VerticalLocomotionState.Standing));
        }

        [Test]
        public void CoherentWholeBodyRiseAcquiresJumpAndOwnsPositiveRootY()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            var jump = detector.Update(
                VerticalFrame(wholeBodyRise: 0.08f), Profile(), 0.1f);

            Assert.That(jump.state, Is.EqualTo(VerticalLocomotionState.Jump));
            Assert.That(jump.jumpPhase, Is.EqualTo(VerticalJumpPhase.Takeoff));
            Assert.That(jump.worldOffsetY, Is.GreaterThan(0.20f));
            Assert.That(jump.groundedBendActive, Is.False);
            Assert.That(jump.suppressPhysicalDepth, Is.True);
        }

        [Test]
        public void JumpOverridesPriorGroundedBendInsteadOfBeingPinned()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            var bend = detector.Update(
                VerticalFrame(crouchDrop: 0.04f), Profile(), 0.1f);
            var standing = detector.Update(VerticalFrame(), Profile(), 0.1f);
            var jump = detector.Update(
                VerticalFrame(wholeBodyRise: 0.08f), Profile(), 0.1f);

            Assert.That(bend.worldOffsetY, Is.LessThan(0f));
            Assert.That(standing.state, Is.EqualTo(VerticalLocomotionState.Standing));
            Assert.That(jump.IsJumpActive, Is.True);
            Assert.That(jump.groundedBendActive, Is.False);
            Assert.That(jump.worldOffsetY, Is.GreaterThan(0f));
        }

        [Test]
        public void SingleLegLiftDoesNotCreateJumpOrCrouchDescent()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            var lift = detector.Update(
                VerticalFrame(leftFootExtraY: 0.08f), Profile(), 0.1f);

            Assert.That(lift.state, Is.EqualTo(VerticalLocomotionState.Standing));
            Assert.That(lift.IsJumpActive, Is.False);
            Assert.That(lift.IsCrouchActive, Is.False);
            Assert.That(lift.footAsymmetry, Is.GreaterThan(0.10f));
            Assert.That(lift.groundedBendActive, Is.False);
            Assert.That(Mathf.Abs(lift.worldOffsetY), Is.LessThan(0.0001f));
        }

        [Test]
        public void ApparentScaleChangeAloneDoesNotCreateVerticalAction()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            var sample = detector.Update(
                VerticalFrame(anatomicalScale: 1.35f), Profile(), 0.1f);

            Assert.That(sample.apparentScaleChange, Is.GreaterThan(0.12f));
            Assert.That(sample.IsJumpActive, Is.False);
            Assert.That(sample.IsCrouchActive, Is.False);
            Assert.That(sample.groundedBendActive, Is.False);
            Assert.That(Mathf.Abs(sample.worldOffsetY), Is.LessThan(0.0001f));
        }

        [Test]
        public void JumpLandingReturnsToStandingWithoutSticking()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            detector.Update(VerticalFrame(wholeBodyRise: 0.08f), Profile(), 0.1f);
            var landing = detector.Update(VerticalFrame(), Profile(), 0.1f);
            var standing = detector.Update(VerticalFrame(), Profile(), 0.1f);

            Assert.That(landing.state, Is.EqualTo(VerticalLocomotionState.Jump));
            Assert.That(landing.jumpPhase, Is.EqualTo(VerticalJumpPhase.Landing));
            Assert.That(Mathf.Abs(landing.worldOffsetY), Is.LessThan(0.0001f));
            Assert.That(standing.state, Is.EqualTo(VerticalLocomotionState.Standing));
            Assert.That(standing.jumpPhase, Is.EqualTo(VerticalJumpPhase.Grounded));
        }

        [Test]
        public void TrackingAndCalibrationResetClearStaleVerticalAction()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            detector.Update(VerticalFrame(crouchDrop: 0.10f), Profile(), 0.1f);
            detector.Update(EmptyFrame(), Profile(), 0.05f);
            var expired = detector.Update(EmptyFrame(), Profile(), 0.20f);

            Assert.That(expired.state, Is.EqualTo(VerticalLocomotionState.Unavailable));
            Assert.That(Mathf.Abs(expired.worldOffsetY), Is.LessThan(0.0001f));

            detector.Update(VerticalFrame(), Profile(), 0.1f);
            detector.Update(VerticalFrame(wholeBodyRise: 0.08f), Profile(), 0.1f);
            var invalidProfile = Profile();
            invalidProfile.bodyReferenceValid = false;
            var reset = detector.Update(VerticalFrame(), invalidProfile, 0.1f);

            Assert.That(reset.state, Is.EqualTo(VerticalLocomotionState.Unavailable));
            Assert.That(reset.referenceReady, Is.False);
            Assert.That(Mathf.Abs(reset.worldOffsetY), Is.LessThan(0.0001f));
        }

        [Test]
        public void InPlaceJumpDoesNotCreateMeaningfulPhysicalXZ()
        {
            var tracker = FastRootTracker();
            var profile = Profile();
            tracker.Update(VerticalFrame(), profile, 0.1f);
            var jump = tracker.Update(
                VerticalFrame(wholeBodyRise: 0.08f), profile, 0.1f);

            Assert.That(jump.displacementXZ.magnitude, Is.LessThan(0.005f));
            Assert.That(jump.movementAccepted, Is.False);
        }

        private static VerticalLocomotionInterpreter FastVerticalInterpreter()
        {
            return VerticalInterpreter(1000f);
        }

        private static VerticalLocomotionInterpreter VerticalInterpreter(float response)
        {
            return new VerticalLocomotionInterpreter(new VerticalLocomotionSettings
            {
                minimumJointConfidence = 0.20f,
                jumpEnterThreshold = 0.10f,
                jumpReleaseThreshold = 0.04f,
                jumpWorldScale = 1.60f,
                maximumJumpHeight = 0.90f,
                maximumJumpFootAsymmetry = 0.08f,
                crouchEnterThreshold = 0.18f,
                crouchReleaseThreshold = 0.09f,
                crouchWorldScale = 1.20f,
                maximumCrouchDepth = 0.65f,
                verticalResponse = response,
                trackingGraceSeconds = 0.10f,
                maximumApparentScaleChange = 0.12f,
                maximumJumpCoherenceSpread = 0.10f,
                groundedSupportTolerance = 0.06f,
            });
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

        private static MotionCalibrationProfile Profile()
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

        private static CanonicalPoseFrame VerticalFrame(
            float wholeBodyRise = 0f,
            float crouchDrop = 0f,
            float leftFootExtraX = 0f,
            float rightFootExtraX = 0f,
            float leftFootExtraY = 0f,
            float rightFootExtraY = 0f,
            float torsoCenterX = 0.50f,
            float anatomicalScale = 1.00f)
        {
            var frame = new CanonicalPoseFrame();
            frame.Begin(1L, 0d, true);

            const float supportY = 0.10f;
            var legHeight = 0.40f * anatomicalScale;
            var torsoHeight = 0.24f * anatomicalScale;
            var kneeHeight = 0.22f * anatomicalScale;
            var shoulderHalf = 0.15f * anatomicalScale;
            var hipHalf = 0.10f * anatomicalScale;

            AddSupportFoot(frame, true, new Vector2(
                0.58f + leftFootExtraX,
                supportY + wholeBodyRise + leftFootExtraY));
            AddSupportFoot(frame, false, new Vector2(
                0.42f + rightFootExtraX,
                supportY + wholeBodyRise + rightFootExtraY));

            var pelvisY = supportY + wholeBodyRise + legHeight - crouchDrop;
            var chestY = pelvisY + torsoHeight;
            var kneeY = supportY + wholeBodyRise + kneeHeight - crouchDrop * 0.50f;

            SetImageOnly(frame, CanonicalJointId.LeftKnee,
                new Vector2(torsoCenterX + 0.08f * anatomicalScale, kneeY));
            SetImageOnly(frame, CanonicalJointId.RightKnee,
                new Vector2(torsoCenterX - 0.08f * anatomicalScale, kneeY));
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
            SetTracked(frame, CanonicalJointId.LeftShoulder,
                new Vector2(torsoCenterX + shoulderHalf, chestY),
                new Vector3(0.20f, 0.50f, 0f));
            SetTracked(frame, CanonicalJointId.RightShoulder,
                new Vector2(torsoCenterX - shoulderHalf, chestY),
                new Vector3(-0.20f, 0.50f, 0f));
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
