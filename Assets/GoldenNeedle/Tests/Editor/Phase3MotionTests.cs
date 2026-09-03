using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Stabilization;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class Phase3MotionTests
    {
        [Test]
        public void OneEuroInitializesAndNeverReturnsNaN()
        {
            var filter = new OneEuroFilterVector3();
            var result = filter.Filter(new Vector3(float.NaN, 1f, float.PositiveInfinity), 0f);

            Assert.That(result.x, Is.EqualTo(0f));
            Assert.That(result.y, Is.EqualTo(1f));
            Assert.That(result.z, Is.EqualTo(0f));
            Assert.That(IsFinite(result), Is.True);
        }

        [Test]
        public void OneEuroConvergesAndReducesJitter()
        {
            var filter = new OneEuroFilter1D();
            var unfilteredJitter = 0f;
            var filteredJitter = 0f;
            var previous = 0.5f;
            var filtered = 0.5f;

            for (var i = 0; i < 80; i++)
            {
                var sample = i % 2 == 0 ? 0.4f : 0.6f;
                filtered = filter.Filter(sample, 0.05f);
                unfilteredJitter += Mathf.Abs(sample - 0.5f);
                filteredJitter += Mathf.Abs(filtered - previous);
                previous = filtered;
            }

            Assert.That(filtered, Is.EqualTo(0.5f).Within(0.04f));
            Assert.That(filteredJitter, Is.LessThan(unfilteredJitter));
        }

        [Test]
        public void OneEuroFollowsStepAndUsesActualDeltaTimeAfterReset()
        {
            var shortStep = new OneEuroFilter1D();
            var longStep = new OneEuroFilter1D();
            var shortResult = shortStep.Filter(0f, 0.05f);
            shortResult = shortStep.Filter(1f, 0.01f);
            var longResult = longStep.Filter(0f, 0.05f);
            longResult = longStep.Filter(1f, 0.20f);

            Assert.That(shortResult, Is.GreaterThan(0f));
            Assert.That(longResult, Is.GreaterThan(shortResult));
            shortStep.Reset();
            Assert.That(shortStep.Filter(0.75f, 0.05f), Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        public void ConfidenceRequiresAcquisitionAndUsesSustainThreshold()
        {
            var settings = NewStabilizerSettings();
            settings.acquireSamples = 2;
            var stabilizer = new CanonicalPoseStabilizer(settings);

            var first = SingleJointFrame(1, 0.01, CanonicalJointId.LeftWrist, 0.65f, new Vector2(0.2f, 0.5f));
            var second = SingleJointFrame(2, 0.06, CanonicalJointId.LeftWrist, 0.65f, new Vector2(0.21f, 0.5f));
            var output = new CanonicalPoseFrame();
            stabilizer.Stabilize(first, output, 0.01);
            Assert.That(output.GetJoint(CanonicalJointId.LeftWrist).IsTracked, Is.False);

            stabilizer.Stabilize(second, output, 0.06);
            Assert.That(output.GetJoint(CanonicalJointId.LeftWrist).IsTracked, Is.True);

            var sustained = SingleJointFrame(3, 0.11, CanonicalJointId.LeftWrist, 0.45f, new Vector2(0.22f, 0.5f));
            stabilizer.Stabilize(sustained, output, 0.11);
            Assert.That(output.GetJoint(CanonicalJointId.LeftWrist).IsTracked, Is.True);
        }

        [Test]
        public void ConfidenceGracePreservesBriefDropoutThenLosesJoint()
        {
            var settings = NewStabilizerSettings();
            settings.acquireSamples = 1;
            var stabilizer = new CanonicalPoseStabilizer(settings);
            var output = new CanonicalPoseFrame();
            stabilizer.Stabilize(SingleJointFrame(1, 0.00, CanonicalJointId.LeftWrist, 0.9f, new Vector2(0.2f, 0.5f)), output, 0.00);
            stabilizer.Stabilize(SingleJointFrame(2, 0.05, CanonicalJointId.LeftWrist, 0f, Vector2.zero), output, 0.05);
            Assert.That(output.GetJoint(CanonicalJointId.LeftWrist).IsTracked, Is.True);

            stabilizer.Stabilize(SingleJointFrame(3, 0.16, CanonicalJointId.LeftWrist, 0f, Vector2.zero), output, 0.16);
            Assert.That(output.GetJoint(CanonicalJointId.LeftWrist).IsTracked, Is.False);
        }

        [Test]
        public void ConfidenceResetReacquiresFromNewSampleAndStatesAreIndependent()
        {
            var settings = NewStabilizerSettings();
            settings.acquireSamples = 1;
            var stabilizer = new CanonicalPoseStabilizer(settings);
            var output = new CanonicalPoseFrame();
            stabilizer.Stabilize(TwoJointFrame(1, 0.00, true, 0.1f, true, 0.3f), output, 0.00);
            stabilizer.Stabilize(TwoJointFrame(2, 0.40, false, 0.9f, true, 0.4f), output, 0.40);
            stabilizer.Stabilize(TwoJointFrame(3, 0.41, true, 0.9f, true, 0.5f), output, 0.41);

            var wrist = output.GetJoint(CanonicalJointId.LeftWrist);
            var elbow = output.GetJoint(CanonicalJointId.LeftElbow);
            Assert.That(wrist.IsTracked, Is.True);
            Assert.That(wrist.imagePosition.x, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(elbow.IsTracked, Is.True);
        }

        [Test]
        public void CalibrationMissingRequiredJointDoesNotAdvance()
        {
            var session = NewCalibrationSession();
            session.Begin(0d);
            var frame = NeutralFrame(1, 0d);
            frame.SetJoint(Untracked(CanonicalJointId.LeftAnkle));
            frame.Complete();
            session.Update(frame, 0d);

            Assert.That(session.State, Is.EqualTo(MotionCalibrationState.AwaitingNeutral));
            Assert.That(session.Progress01, Is.EqualTo(0f));
        }

        [Test]
        public void CalibrationRequiresStableNeutralHoldThenMovesToTPose()
        {
            var session = NewCalibrationSession();
            session.Begin(0d);
            session.Update(NeutralFrame(1, 0d), 0d);
            session.Update(NeutralFrame(2, 0.6d), 0.6d);
            Assert.That(session.State, Is.EqualTo(MotionCalibrationState.SamplingNeutral));
            Assert.That(session.Progress01, Is.GreaterThan(0.4f));
            session.Update(NeutralFrame(3, 1.3d), 1.3d);

            Assert.That(session.State, Is.EqualTo(MotionCalibrationState.AwaitingTPose));
            Assert.That(session.Profile.neutralSampleCount, Is.EqualTo(3));
        }

        [Test]
        public void CalibrationMovingNeutralPoseResetsSamplingProgress()
        {
            var session = NewCalibrationSession();
            session.Begin(0d);
            session.Update(NeutralFrame(1, 0d), 0d);
            var moved = NeutralFrame(2, 0.2d);
            SetTracked(moved, CanonicalJointId.LeftKnee, new Vector2(0.4f, 0.55f));
            moved.Complete();
            session.Update(moved, 0.2d);

            Assert.That(session.State, Is.EqualTo(MotionCalibrationState.AwaitingNeutral));
            Assert.That(session.Progress01, Is.EqualTo(0f));
        }

        [Test]
        public void CalibrationRejectsNonTPoseAndCompletesValidTPoseWithFiniteProfile()
        {
            var session = NewCalibrationSession();
            session.Begin(0d);
            session.Update(NeutralFrame(1, 0d), 0d);
            session.Update(NeutralFrame(2, 0.7d), 0.7d);
            session.Update(NeutralFrame(3, 1.3d), 1.3d);

            session.Update(NonTPoseFrame(4, 1.4d), 1.4d);
            Assert.That(session.State, Is.EqualTo(MotionCalibrationState.AwaitingTPose));
            session.Update(TPoseFrame(5, 1.4d), 1.4d);
            session.Update(TPoseFrame(6, 1.8d), 1.8d);
            session.Update(TPoseFrame(7, 2.2d), 2.2d);

            Assert.That(session.State, Is.EqualTo(MotionCalibrationState.Complete));
            Assert.That(session.IsValid, Is.True);
            Assert.That(session.Profile.shoulderWidth, Is.GreaterThan(0f));
            Assert.That(session.Profile.hipWidth, Is.GreaterThan(0f));
            Assert.That(session.Profile.torsoLength, Is.GreaterThan(0f));
            Assert.That(session.Profile.leftArmReach, Is.GreaterThan(0f));
            Assert.That(session.Profile.rightArmReach, Is.GreaterThan(0f));
            Assert.That(session.Profile.leftLegReach, Is.GreaterThan(0f));
            Assert.That(session.Profile.rightLegReach, Is.GreaterThan(0f));
            Assert.That(IsFinite(session.Profile.neutralBodyUp), Is.True);
            Assert.That(IsFinite(session.Profile.tPoseLeftArmDirection), Is.True);
            Assert.That(Vector3.Dot(session.Profile.neutralBodyForward, Vector3.back), Is.GreaterThan(0.99f));
            Assert.That(session.Profile.version, Is.EqualTo(MotionCalibrationProfile.CurrentVersion));
        }

        private static CanonicalStabilizerSettings NewStabilizerSettings()
        {
            return new CanonicalStabilizerSettings
            {
                acquireConfidence = 0.60f,
                sustainConfidence = 0.40f,
                lossGraceSeconds = 0.10f,
                resetAfterLossSeconds = 0.25f,
                minCutoff = 1f,
                beta = 0.05f,
                derivativeCutoff = 1f,
            };
        }

        private static MotionCalibrationSession NewCalibrationSession()
        {
            return new MotionCalibrationSession(new MotionCalibrationSettings
            {
                neutralHoldSeconds = 1.25f,
                tPoseHoldSeconds = 0.75f,
                neutralStabilityTolerance = 0.06f,
                tPoseStabilityTolerance = 0.10f,
            });
        }

        private static CanonicalPoseFrame SingleJointFrame(long timestamp, double received, CanonicalJointId id, float confidence, Vector2 image)
        {
            var frame = new CanonicalPoseFrame();
            frame.Begin(timestamp, received, confidence > 0f);
            if (confidence > 0f)
            {
                SetTracked(frame, id, image, confidence);
            }

            frame.Complete();
            return frame;
        }

        private static CanonicalPoseFrame TwoJointFrame(long timestamp, double received, bool wristTracked, float wristX, bool elbowTracked, float elbowX)
        {
            var frame = new CanonicalPoseFrame();
            frame.Begin(timestamp, received, wristTracked || elbowTracked);
            if (wristTracked)
            {
                SetTracked(frame, CanonicalJointId.LeftWrist, new Vector2(wristX, 0.5f));
            }

            if (elbowTracked)
            {
                SetTracked(frame, CanonicalJointId.LeftElbow, new Vector2(elbowX, 0.5f));
            }

            frame.Complete();
            return frame;
        }

        private static CanonicalPoseFrame NeutralFrame(long timestamp, double received)
        {
            var frame = new CanonicalPoseFrame();
            frame.Begin(timestamp, received, true);
            SetTracked(frame, CanonicalJointId.Pelvis, new Vector2(0.5f, 0.5f));
            SetTracked(frame, CanonicalJointId.Chest, new Vector2(0.5f, 0.7f));
            SetTracked(frame, CanonicalJointId.LeftShoulder, new Vector2(0.35f, 0.75f));
            SetTracked(frame, CanonicalJointId.RightShoulder, new Vector2(0.65f, 0.75f));
            SetTracked(frame, CanonicalJointId.LeftHip, new Vector2(0.4f, 0.5f));
            SetTracked(frame, CanonicalJointId.RightHip, new Vector2(0.6f, 0.5f));
            SetTracked(frame, CanonicalJointId.LeftKnee, new Vector2(0.4f, 0.3f));
            SetTracked(frame, CanonicalJointId.RightKnee, new Vector2(0.6f, 0.3f));
            SetTracked(frame, CanonicalJointId.LeftAnkle, new Vector2(0.4f, 0.1f));
            SetTracked(frame, CanonicalJointId.RightAnkle, new Vector2(0.6f, 0.1f));
            frame.Complete();
            return frame;
        }

        private static CanonicalPoseFrame NonTPoseFrame(long timestamp, double received)
        {
            var frame = NeutralFrame(timestamp, received);
            SetTracked(frame, CanonicalJointId.LeftElbow, new Vector2(0.35f, 0.60f));
            SetTracked(frame, CanonicalJointId.LeftWrist, new Vector2(0.35f, 0.45f));
            SetTracked(frame, CanonicalJointId.RightElbow, new Vector2(0.65f, 0.60f));
            SetTracked(frame, CanonicalJointId.RightWrist, new Vector2(0.65f, 0.45f));
            frame.Complete();
            return frame;
        }

        private static CanonicalPoseFrame TPoseFrame(long timestamp, double received)
        {
            var frame = NeutralFrame(timestamp, received);
            SetTracked(frame, CanonicalJointId.LeftElbow, new Vector2(0.20f, 0.75f));
            SetTracked(frame, CanonicalJointId.LeftWrist, new Vector2(0.05f, 0.75f));
            SetTracked(frame, CanonicalJointId.RightElbow, new Vector2(0.80f, 0.75f));
            SetTracked(frame, CanonicalJointId.RightWrist, new Vector2(0.95f, 0.75f));
            frame.Complete();
            return frame;
        }

        private static void SetTracked(CanonicalPoseFrame frame, CanonicalJointId id, Vector2 image, float confidence = 1f)
        {
            var position = new Vector3(image.x, image.y, 0f);
            frame.SetJoint(new CanonicalPoseJoint
            {
                id = id,
                tracking = CanonicalTrackingState.Tracked,
                confidence = confidence,
                imagePosition = image,
                hasImagePosition = true,
                worldPosition = position,
                hasWorldPosition = true,
                localPosition = position,
                hasLocalPosition = true,
            });
        }

        private static CanonicalPoseJoint Untracked(CanonicalJointId id)
        {
            return new CanonicalPoseJoint { id = id, tracking = CanonicalTrackingState.Unavailable };
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
