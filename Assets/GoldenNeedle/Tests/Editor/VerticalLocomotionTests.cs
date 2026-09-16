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
            Assert.That(Mathf.Abs(sample.worldOffsetY), Is.LessThan(0.0001f));
        }

        [Test]
        public void SingleLegLiftDoesNotTriggerJump()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);

            var sample = detector.Update(
                VerticalFrame(leftFootExtraY: 0.08f),
                Profile(),
                0.1f);

            Assert.That(sample.state, Is.EqualTo(VerticalLocomotionState.Standing));
            Assert.That(sample.IsJumpActive, Is.False);
            Assert.That(sample.IsCrouchActive, Is.False);
            Assert.That(sample.footAsymmetry, Is.GreaterThan(0.10f));
            Assert.That(Mathf.Abs(sample.worldOffsetY), Is.LessThan(0.0001f));
        }

        [Test]
        public void SingleLegLateralAndVerticalMotionDoesNotTriggerVerticalAction()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);

            var sample = detector.Update(
                VerticalFrame(
                    leftFootExtraX: 0.10f,
                    leftFootExtraY: 0.09f),
                Profile(),
                0.1f);

            Assert.That(sample.state, Is.EqualTo(VerticalLocomotionState.Standing));
            Assert.That(sample.IsJumpActive, Is.False);
            Assert.That(sample.IsCrouchActive, Is.False);
            Assert.That(Mathf.Abs(sample.worldOffsetY), Is.LessThan(0.0001f));
        }

        [Test]
        public void CoherentWholeBodyRiseAcquiresJumpAndPositiveOffset()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);

            var sample = detector.Update(
                VerticalFrame(wholeBodyRise: 0.08f),
                Profile(),
                0.1f);

            Assert.That(sample.state, Is.EqualTo(VerticalLocomotionState.Jump));
            Assert.That(sample.jumpPhase, Is.EqualTo(VerticalJumpPhase.Takeoff));
            Assert.That(sample.jumpSignal, Is.GreaterThan(0.15f));
            Assert.That(sample.footAsymmetry, Is.LessThan(0.001f));
            Assert.That(sample.worldOffsetY, Is.GreaterThan(0.20f));
            Assert.That(sample.suppressPhysicalDepth, Is.True);
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
            Assert.That(Mathf.Abs(standing.worldOffsetY), Is.LessThan(0.0001f));
        }

        [Test]
        public void SecondJumpCanAcquireAfterLanding()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            detector.Update(VerticalFrame(wholeBodyRise: 0.08f), Profile(), 0.1f);
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            detector.Update(VerticalFrame(), Profile(), 0.1f);

            var secondJump = detector.Update(
                VerticalFrame(wholeBodyRise: 0.09f),
                Profile(),
                0.1f);

            Assert.That(secondJump.state, Is.EqualTo(VerticalLocomotionState.Jump));
            Assert.That(secondJump.jumpPhase, Is.EqualTo(VerticalJumpPhase.Takeoff));
            Assert.That(secondJump.worldOffsetY, Is.GreaterThan(0f));
        }

        [Test]
        public void ApparentScaleChangeAloneDoesNotTriggerVerticalAction()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);

            var sample = detector.Update(
                VerticalFrame(anatomicalScale: 1.35f),
                Profile(),
                0.1f);

            Assert.That(sample.apparentScaleChange, Is.GreaterThan(0.12f));
            Assert.That(sample.state, Is.EqualTo(VerticalLocomotionState.Standing));
            Assert.That(sample.IsJumpActive, Is.False);
            Assert.That(sample.IsCrouchActive, Is.False);
        }

        [Test]
        public void PlantedTorsoLeanDoesNotTriggerJumpOrCrouch()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);

            var sample = detector.Update(
                VerticalFrame(torsoCenterX: 0.62f),
                Profile(),
                0.1f);

            Assert.That(sample.state, Is.EqualTo(VerticalLocomotionState.Standing));
            Assert.That(Mathf.Abs(sample.jumpSignal), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(sample.crouchCompression), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(sample.worldOffsetY), Is.LessThan(0.0001f));
        }

        [Test]
        public void GroundedPelvisCompressionAcquiresCrouch()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);

            var sample = detector.Update(
                VerticalFrame(crouchDrop: 0.10f),
                Profile(),
                0.1f);

            Assert.That(sample.state, Is.EqualTo(VerticalLocomotionState.Crouch));
            Assert.That(sample.crouchCompression, Is.GreaterThan(0.20f));
            Assert.That(Mathf.Abs(sample.supportRise), Is.LessThan(0.001f));
            Assert.That(sample.worldOffsetY, Is.LessThan(-0.20f));
            Assert.That(sample.suppressPhysicalDepth, Is.False);
        }

        [Test]
        public void HeldCrouchRemainsCrouchedWithStableNegativeOffset()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            var entered = detector.Update(
                VerticalFrame(crouchDrop: 0.10f),
                Profile(),
                0.1f);
            var held = detector.Update(
                VerticalFrame(crouchDrop: 0.10f),
                Profile(),
                0.1f);

            Assert.That(entered.state, Is.EqualTo(VerticalLocomotionState.Crouch));
            Assert.That(held.state, Is.EqualTo(VerticalLocomotionState.Crouch));
            Assert.That(held.worldOffsetY, Is.EqualTo(entered.worldOffsetY).Within(0.001f));
            Assert.That(held.worldOffsetY, Is.LessThan(0f));
        }

        [Test]
        public void CrouchReleaseUsesHysteresisAndReturnsToZero()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);
            detector.Update(VerticalFrame(crouchDrop: 0.10f), Profile(), 0.1f);

            var stillCrouched = detector.Update(
                VerticalFrame(crouchDrop: 0.05f),
                Profile(),
                0.1f);
            var released = detector.Update(VerticalFrame(), Profile(), 0.1f);

            Assert.That(stillCrouched.crouchCompression, Is.GreaterThan(0.09f));
            Assert.That(stillCrouched.state, Is.EqualTo(VerticalLocomotionState.Crouch));
            Assert.That(stillCrouched.worldOffsetY, Is.LessThan(0f));
            Assert.That(released.state, Is.EqualTo(VerticalLocomotionState.Standing));
            Assert.That(Mathf.Abs(released.worldOffsetY), Is.LessThan(0.0001f));
        }

        [Test]
        public void JumpAndCrouchRemainMutuallyExclusive()
        {
            var detector = FastVerticalInterpreter();
            detector.Update(VerticalFrame(), Profile(), 0.1f);

            var jump = detector.Update(
                VerticalFrame(wholeBodyRise: 0.08f),
                Profile(),
                0.1f);

            Assert.That(jump.IsJumpActive, Is.True);
            Assert.That(jump.IsCrouchActive, Is.False);
            Assert.That(jump.state, Is.Not.EqualTo(VerticalLocomotionState.Crouch));
        }

        [Test]
        public void TrackingLossDoesNotLeaveVerticalActionStuck()
        {
            var jumpDetector = FastVerticalInterpreter();
            jumpDetector.Update(VerticalFrame(), Profile(), 0.1f);
            jumpDetector.Update(VerticalFrame(wholeBodyRise: 0.08f), Profile(), 0.1f);
            var jumpGrace = jumpDetector.Update(EmptyFrame(), Profile(), 0.05f);
            var jumpExpired = jumpDetector.Update(EmptyFrame(), Profile(), 0.20f);

            Assert.That(jumpGrace.isAvailable, Is.False);
            Assert.That(jumpGrace.state, Is.EqualTo(VerticalLocomotionState.Jump));
            Assert.That(jumpExpired.state, Is.EqualTo(VerticalLocomotionState.Unavailable));
            Assert.That(Mathf.Abs(jumpExpired.worldOffsetY), Is.LessThan(0.0001f));

            var crouchDetector = FastVerticalInterpreter();
            crouchDetector.Update(VerticalFrame(), Profile(), 0.1f);
            crouchDetector.Update(VerticalFrame(crouchDrop: 0.10f), Profile(), 0.1f);
            crouchDetector.Update(EmptyFrame(), Profile(), 0.05f);
            var crouchExpired = crouchDetector.Update(EmptyFrame(), Profile(), 0.20f);

            Assert.That(crouchExpired.state, Is.EqualTo(VerticalLocomotionState.Unavailable));
            Assert.That(Mathf.Abs(crouchExpired.worldOffsetY), Is.LessThan(0.0001f));
        }

        [Test]
        public void InPlaceJumpDoesNotCreateMeaningfulRootTrackerXZ()
        {
            var tracker = FastRootTracker();
            var profile = Profile();
            tracker.Update(VerticalFrame(), profile, 0.1f);

            var jump = tracker.Update(
                VerticalFrame(wholeBodyRise: 0.08f),
                profile,
                0.1f);

            Assert.That(jump.isValid, Is.True);
            Assert.That(Mathf.Abs(jump.displacementXZ.x), Is.LessThan(0.005f));
            Assert.That(Mathf.Abs(jump.displacementXZ.y), Is.LessThan(0.005f));
        }

        [Test]
        public void InPlaceCrouchDoesNotCreateMeaningfulRootTrackerXZ()
        {
            var tracker = FastRootTracker();
            var profile = Profile();
            tracker.Update(VerticalFrame(), profile, 0.1f);

            var crouch = tracker.Update(
                VerticalFrame(crouchDrop: 0.10f),
                profile,
                0.1f);

            Assert.That(crouch.isValid, Is.True);
            Assert.That(crouch.displacementXZ.magnitude, Is.LessThan(0.005f));
        }

        private static VerticalLocomotionInterpreter FastVerticalInterpreter()
        {
            return new VerticalLocomotionInterpreter(
                new VerticalLocomotionSettings
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
                    verticalResponse = 1000f,
                    trackingGraceSeconds = 0.10f,
                    maximumApparentScaleChange = 0.12f,
                    maximumJumpCoherenceSpread = 0.10f,
                    groundedSupportTolerance = 0.06f,
                });
        }

        private static CameraSpaceRootTracker FastRootTracker()
        {
            return new CameraSpaceRootTracker(
                new CameraSpaceRootTrackerSettings
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
            var scaledLegHeight = 0.40f * anatomicalScale;
            var scaledTorsoHeight = 0.24f * anatomicalScale;
            var scaledKneeHeight = 0.22f * anatomicalScale;
            var shoulderHalf = 0.15f * anatomicalScale;
            var hipHalf = 0.10f * anatomicalScale;

            var leftFootCenter = new Vector2(
                0.58f + leftFootExtraX,
                supportY + wholeBodyRise + leftFootExtraY);
            var rightFootCenter = new Vector2(
                0.42f + rightFootExtraX,
                supportY + wholeBodyRise + rightFootExtraY);
            AddSupportFoot(frame, true, leftFootCenter);
            AddSupportFoot(frame, false, rightFootCenter);

            var pelvisY = supportY + wholeBodyRise + scaledLegHeight - crouchDrop;
            var chestY = pelvisY + scaledTorsoHeight;
            var kneeY = supportY + wholeBodyRise + scaledKneeHeight - crouchDrop * 0.50f;

            SetImageOnly(
                frame,
                CanonicalJointId.LeftKnee,
                new Vector2(torsoCenterX + 0.08f * anatomicalScale, kneeY));
            SetImageOnly(
                frame,
                CanonicalJointId.RightKnee,
                new Vector2(torsoCenterX - 0.08f * anatomicalScale, kneeY));
            SetImageOnly(
                frame,
                CanonicalJointId.LeftHip,
                new Vector2(torsoCenterX + hipHalf, pelvisY));
            SetImageOnly(
                frame,
                CanonicalJointId.RightHip,
                new Vector2(torsoCenterX - hipHalf, pelvisY));
            SetImageOnly(
                frame,
                CanonicalJointId.Pelvis,
                new Vector2(torsoCenterX, pelvisY));
            SetImageOnly(
                frame,
                CanonicalJointId.Chest,
                new Vector2(torsoCenterX, chestY));
            SetImageOnly(
                frame,
                CanonicalJointId.LeftShoulder,
                new Vector2(torsoCenterX + shoulderHalf, chestY));
            SetImageOnly(
                frame,
                CanonicalJointId.RightShoulder,
                new Vector2(torsoCenterX - shoulderHalf, chestY));

            frame.Complete();
            return frame;
        }

        private static void AddSupportFoot(
            CanonicalPoseFrame frame,
            bool left,
            Vector2 center)
        {
            var ankle = left
                ? CanonicalJointId.LeftAnkle
                : CanonicalJointId.RightAnkle;
            var heel = left
                ? CanonicalJointId.LeftHeel
                : CanonicalJointId.RightHeel;
            var toe = left
                ? CanonicalJointId.LeftToe
                : CanonicalJointId.RightToe;

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
