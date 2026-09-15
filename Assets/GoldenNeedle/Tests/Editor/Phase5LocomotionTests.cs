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
        public void PlantedFeetIgnoreTorsoLateralLean()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();

            var baseline = tracker.Update(
                SupportFrame(),
                profile,
                0.1f);
            Assert.That(baseline.isValid, Is.True);

            var leaned = tracker.Update(
                SupportFrame(torsoCenterX: 0.62f),
                profile,
                0.1f);

            Assert.That(leaned.isValid, Is.True);
            Assert.That(
                leaned.displacementXZ.magnitude,
                Is.LessThan(0.005f));
        }

        [Test]
        public void PlantedFeetIgnoreTorsoForwardBackScaleChange()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();

            tracker.Update(SupportFrame(), profile, 0.1f);
            var leanedTowardCamera = tracker.Update(
                SupportFrame(torsoScale: 1.18f),
                profile,
                0.1f);

            Assert.That(leanedTowardCamera.isValid, Is.True);
            Assert.That(
                Mathf.Abs(leanedTowardCamera.displacementXZ.y),
                Is.LessThan(0.005f));
        }

        [Test]
        public void RaisedSwingFootDoesNotTranslatePlantedSupportRoot()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();

            tracker.Update(SupportFrame(), profile, 0.1f);
            var swing = tracker.Update(
                SupportFrame(
                    leftExtraX: 0.08f,
                    leftExtraY: 0.06f),
                profile,
                0.1f);

            Assert.That(swing.isValid, Is.True);
            Assert.That(swing.supportCommonXZ.x, Is.GreaterThan(0.10f));
            Assert.That(swing.supportDifferentialXZ.x, Is.GreaterThan(0.10f));
            Assert.That(
                swing.displacementXZ.magnitude,
                Is.LessThan(0.005f));
        }

        [Test]
        public void RaisedSwingFootMotionAcrossSamplesDoesNotAccumulateTranslation()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();

            tracker.Update(SupportFrame(), profile, 0.1f);
            var first = tracker.Update(
                SupportFrame(
                    leftExtraX: 0.03f,
                    leftExtraY: 0.06f),
                profile,
                0.1f);
            var second = tracker.Update(
                SupportFrame(
                    leftExtraX: 0.07f,
                    leftExtraY: 0.09f),
                profile,
                0.1f);
            var third = tracker.Update(
                SupportFrame(
                    leftExtraX: 0.10f,
                    leftExtraY: 0.07f),
                profile,
                0.1f);

            Assert.That(first.isValid, Is.True);
            Assert.That(second.isValid, Is.True);
            Assert.That(third.isValid, Is.True);
            Assert.That(first.displacementXZ.magnitude, Is.LessThan(0.005f));
            Assert.That(second.displacementXZ.magnitude, Is.LessThan(0.005f));
            Assert.That(third.displacementXZ.magnitude, Is.LessThan(0.005f));
        }

        [Test]
        public void BothFeetRelocatedReachFullPhysicalLateralDisplacementAndRecenter()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();

            tracker.Update(SupportFrame(), profile, 0.1f);
            var moved = tracker.Update(
                SupportFrame(supportOffsetX: 0.04f),
                profile,
                0.1f);

            Assert.That(moved.isValid, Is.True);
            Assert.That(moved.displacementXZ.x, Is.GreaterThan(0.08f));

            Assert.That(tracker.Recenter(), Is.True);
            Assert.That(
                tracker.LatestSample.displacementXZ.magnitude,
                Is.LessThan(0.0001f));
        }

        [Test]
        public void SupportAuthorityHysteresisAndLandingRebaseAvoidRootJump()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();

            tracker.Update(SupportFrame(), profile, 0.1f);
            var raised = tracker.Update(
                SupportFrame(
                    leftExtraX: 0.06f,
                    leftExtraY: 0.08f),
                profile,
                0.1f);
            var ambiguous = tracker.Update(
                SupportFrame(
                    leftExtraX: 0.08f,
                    leftExtraY: 0.02f),
                profile,
                0.1f);
            var landed = tracker.Update(
                SupportFrame(leftExtraX: 0.08f),
                profile,
                0.1f);
            var relocated = tracker.Update(
                SupportFrame(supportOffsetX: 0.08f),
                profile,
                0.1f);

            Assert.That(raised.displacementXZ.magnitude, Is.LessThan(0.005f));
            Assert.That(ambiguous.displacementXZ.magnitude, Is.LessThan(0.005f));
            Assert.That(
                Vector2.Distance(
                    landed.displacementXZ,
                    ambiguous.displacementXZ),
                Is.LessThan(0.005f));
            Assert.That(relocated.displacementXZ.x, Is.GreaterThan(0.10f));
        }

        [Test]
        public void SupportBaseRelocationPlusScaleEvidenceProducesDepthDisplacement()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();

            tracker.Update(SupportFrame(), profile, 0.1f);
            var farther = tracker.Update(
                SupportFrame(
                    torsoScale: 0.84f,
                    supportOffsetY: 0.035f),
                profile,
                0.1f);

            Assert.That(farther.isValid, Is.True);
            Assert.That(farther.depthCorroborated, Is.True);
            Assert.That(farther.depthReliability, Is.GreaterThan(0.99f));
            Assert.That(farther.displacementXZ.y, Is.GreaterThan(0.08f));

            var nearTracker = FastRootTracker();
            nearTracker.Update(SupportFrame(), profile, 0.1f);
            var nearer = nearTracker.Update(
                SupportFrame(
                    torsoScale: 1.18f,
                    supportOffsetY: -0.035f),
                profile,
                0.1f);

            Assert.That(nearer.isValid, Is.True);
            Assert.That(nearer.depthCorroborated, Is.True);
            Assert.That(nearer.displacementXZ.y, Is.LessThan(-0.08f));
        }

        [Test]
        public void JoggingInPlaceHoldsPhysicalSupportWhileDefaultCadenceCanActivate()
        {
            var tracker = FastRootTracker();
            var cadenceDetector = new CadenceDetector(
                new CadenceDetectorSettings());
            var profile = FrontCameraProfile();

            tracker.Update(SupportFrame(), profile, 0.1f);

            var first = JogInPlaceFrame(true);
            var rootFirst = tracker.Update(first, profile, 0.10f);
            var cadenceFirst = cadenceDetector.Update(
                first,
                0.30f,
                0.10f);

            var second = JogInPlaceFrame(false);
            var rootSecond = tracker.Update(second, profile, 0.25f);
            var cadenceSecond = cadenceDetector.Update(
                second,
                0.30f,
                0.25f);

            Assert.That(rootFirst.isValid, Is.True);
            Assert.That(rootSecond.isValid, Is.True);
            Assert.That(cadenceFirst.active, Is.False);
            Assert.That(cadenceSecond.active, Is.True);
            Assert.That(
                rootSecond.supportCommonXZ.magnitude,
                Is.LessThan(0.01f));
            Assert.That(
                rootSecond.supportDifferentialXZ.magnitude,
                Is.GreaterThan(0.10f));
            Assert.That(
                rootSecond.displacementXZ.magnitude,
                Is.LessThan(0.01f));
        }

        [Test]
        public void TemporarySupportLossHoldsLastTrustedPhysicalDisplacement()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();

            tracker.Update(SupportFrame(), profile, 0.1f);
            var moved = tracker.Update(
                SupportFrame(supportOffsetX: 0.04f),
                profile,
                0.1f);
            Assert.That(moved.isValid, Is.True);

            var noSupport = TorsoOnlyFrame(
                torsoCenterX: 0.70f,
                torsoScale: 1.20f);
            noSupport.Complete();
            var lost = tracker.Update(
                noSupport,
                profile,
                0.1f);

            Assert.That(lost.isValid, Is.False);
            Assert.That(lost.hasOrigin, Is.True);
            Assert.That(
                lost.displacementXZ.x,
                Is.EqualTo(moved.displacementXZ.x).Within(0.0001f));
            Assert.That(
                lost.displacementXZ.y,
                Is.EqualTo(moved.displacementXZ.y).Within(0.0001f));
            Assert.That(
                lost.velocityXZ.magnitude,
                Is.LessThan(0.0001f));
        }

        [Test]
        public void SupportReacquisitionRebasesWithoutTeleporting()
        {
            var tracker = FastRootTracker();
            var profile = FrontCameraProfile();

            tracker.Update(SupportFrame(), profile, 0.1f);
            var moved = tracker.Update(
                SupportFrame(supportOffsetX: 0.04f),
                profile,
                0.1f);

            var noSupport = TorsoOnlyFrame();
            noSupport.Complete();
            var lost = tracker.Update(noSupport, profile, 0.1f);
            var reacquired = tracker.Update(
                SupportFrame(supportOffsetX: 0.10f),
                profile,
                0.1f);
            var continued = tracker.Update(
                SupportFrame(supportOffsetX: 0.12f),
                profile,
                0.1f);

            Assert.That(lost.isValid, Is.False);
            Assert.That(reacquired.isValid, Is.True);
            Assert.That(
                Vector2.Distance(
                    reacquired.displacementXZ,
                    moved.displacementXZ),
                Is.LessThan(0.005f));
            Assert.That(
                continued.displacementXZ.x,
                Is.GreaterThan(reacquired.displacementXZ.x + 0.05f));
        }

        [Test]
        public void CadenceDefaultAcquiresAfterTwoCleanAlternatingEventsAndStopsQuickly()
        {
            var detector = new CadenceDetector(
                new CadenceDetectorSettings());

            var first = detector.Update(
                CadenceFrame(true),
                0.30f,
                0.10f);
            var active = detector.Update(
                CadenceFrame(false),
                0.30f,
                0.25f);

            Assert.That(first.active, Is.False);
            Assert.That(active.active, Is.True);
            Assert.That(active.rateStepsPerSecond, Is.GreaterThan(3f));
            Assert.That(active.virtualSpeed, Is.GreaterThan(2f));

            detector.Update(EmptyFrame(), 0.30f, 0.25f);
            var stopped = detector.Update(EmptyFrame(), 0.30f, 0.30f);
            Assert.That(stopped.active, Is.False);
        }

        [Test]
        public void CadenceSingleIsolatedEventDoesNotAcquire()
        {
            var detector = new CadenceDetector(
                new CadenceDetectorSettings());

            var first = detector.Update(
                CadenceFrame(true),
                0.30f,
                0.10f);
            var expired = detector.Update(
                EmptyFrame(),
                0.30f,
                0.60f);

            Assert.That(first.active, Is.False);
            Assert.That(expired.active, Is.False);
        }

        [Test]
        public void CadenceDistancePerStepChangesSpeedUntilMaximumClamp()
        {
            var shortStride = new CadenceDetector(
                new CadenceDetectorSettings
                {
                    virtualStridePerStep = 0.20f,
                    maximumVirtualSpeed = 3f,
                });
            shortStride.Update(CadenceFrame(true), 0.30f, 0.10f);
            var shortResult = shortStride.Update(
                CadenceFrame(false),
                0.30f,
                0.25f);

            var longStride = new CadenceDetector(
                new CadenceDetectorSettings
                {
                    virtualStridePerStep = 0.90f,
                    maximumVirtualSpeed = 2f,
                });
            longStride.Update(CadenceFrame(true), 0.30f, 0.10f);
            var longResult = longStride.Update(
                CadenceFrame(false),
                0.30f,
                0.25f);

            Assert.That(shortResult.active, Is.True);
            Assert.That(longResult.active, Is.True);
            Assert.That(longResult.virtualSpeed, Is.GreaterThan(shortResult.virtualSpeed));
            Assert.That(longResult.virtualSpeed, Is.EqualTo(2f).Within(0.0001f));
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

            var moving = fusion.Evaluate(
                movingRoot,
                cadence,
                Vector2.up,
                IdentityMap());
            Assert.That(moving.physicalContribution.x, Is.EqualTo(0.20f).Within(0.0001f));
            Assert.That(moving.physicalContribution.y, Is.EqualTo(0.40f).Within(0.0001f));
            Assert.That(moving.physicalTranslationActive, Is.True);
            Assert.That(moving.cadenceVelocity.magnitude, Is.LessThan(0.01f));

            var lostRoot = movingRoot;
            lostRoot.isValid = false;
            lostRoot.confidence = 0f;
            var held = fusion.Evaluate(
                lostRoot,
                default,
                Vector2.up,
                IdentityMap());
            Assert.That(held.physicalContribution.x, Is.EqualTo(0.20f).Within(0.0001f));
            Assert.That(held.physicalContribution.y, Is.EqualTo(0.40f).Within(0.0001f));
            Assert.That(held.rootTrackingLive, Is.False);

            movingRoot.velocityXZ = Vector2.zero;
            var inPlace = fusion.Evaluate(
                movingRoot,
                cadence,
                Vector2.up,
                IdentityMap());
            Assert.That(inPlace.physicalTranslationActive, Is.False);
            Assert.That(inPlace.cadenceVelocity.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void PhysicalCameraAxesMapThroughAcceptedFrontCameraReferenceBasis()
        {
            var map = FrontCameraToProperTargetMap();

            AssertVector2(
                LocomotionFusion.MapCameraVectorToWorldXZ(Vector3.left, map),
                Vector2.right);
            AssertVector2(
                LocomotionFusion.MapCameraVectorToWorldXZ(Vector3.right, map),
                Vector2.left);
            AssertVector2(
                LocomotionFusion.MapCameraVectorToWorldXZ(Vector3.back, map),
                Vector2.up);
            AssertVector2(
                LocomotionFusion.MapCameraVectorToWorldXZ(Vector3.forward, map),
                Vector2.down);
        }

        [Test]
        public void PhysicalForwardAndFrontalCadenceResolveToSameWorldForward()
        {
            var map = FrontCameraToProperTargetMap();
            Assert.That(
                BodyHeadingEstimator.TryMapHeading(
                    map,
                    Vector3.back,
                    out var cadenceHeading),
                Is.True);

            var fusion = new LocomotionFusion(new LocomotionFusionSettings
            {
                lateralScale = 1f,
                depthScale = 1f,
                lateralDeadzone = 0f,
                depthDeadzone = 0f,
                physicalVelocityStart = 0.1f,
                physicalVelocityFull = 0.2f,
                minimumRootConfidence = 0.2f,
            });
            var root = new CameraSpaceRootSample
            {
                isValid = true,
                hasOrigin = true,
                displacementXZ = new Vector2(0f, -1f),
                velocityXZ = Vector2.zero,
                confidence = 1f,
            };
            var cadence = new CadenceSample
            {
                active = true,
                confidence = 1f,
                virtualSpeed = 1f,
            };

            var result = fusion.Evaluate(
                root,
                cadence,
                cadenceHeading,
                map);

            Assert.That(result.physicalContribution.y, Is.GreaterThan(0.99f));
            Assert.That(result.cadenceVelocity.y, Is.GreaterThan(0.99f));
            Assert.That(
                Vector2.Dot(
                    result.physicalContribution.normalized,
                    result.cadenceVelocity.normalized),
                Is.GreaterThan(0.999f));
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

        private static CanonicalToAvatarAxisMap FrontCameraToProperTargetMap()
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
            return new CanonicalToAvatarAxisMap(source, target);
        }

        private static CanonicalToAvatarAxisMap IdentityMap()
        {
            Assert.That(
                HumanoidRetargetingMath.TryBuildRightHandedBasis(
                    Vector3.right,
                    Vector3.up,
                    out var basis),
                Is.True);
            return new CanonicalToAvatarAxisMap(basis, basis);
        }

        private static void AssertVector2(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
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
            AddSupportFoot(
                frame,
                true,
                new Vector2(
                    0.58f + supportOffsetX + leftExtraX,
                    0.12f + supportOffsetY + leftExtraY));
            AddSupportFoot(
                frame,
                false,
                new Vector2(
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

            SetTracked(
                frame,
                CanonicalJointId.LeftShoulder,
                new Vector2(torsoCenterX + shoulderHalf, chestY),
                new Vector3(0.20f, 0.50f, 0f));
            SetTracked(
                frame,
                CanonicalJointId.RightShoulder,
                new Vector2(torsoCenterX - shoulderHalf, chestY),
                new Vector3(-0.20f, 0.50f, 0f));
            SetTracked(
                frame,
                CanonicalJointId.LeftHip,
                new Vector2(torsoCenterX + hipHalf, pelvisY),
                new Vector3(0.15f, 0f, 0f));
            SetTracked(
                frame,
                CanonicalJointId.RightHip,
                new Vector2(torsoCenterX - hipHalf, pelvisY),
                new Vector3(-0.15f, 0f, 0f));
            SetTracked(
                frame,
                CanonicalJointId.Pelvis,
                new Vector2(torsoCenterX, pelvisY),
                Vector3.zero);
            SetTracked(
                frame,
                CanonicalJointId.Chest,
                new Vector2(torsoCenterX, chestY),
                new Vector3(0f, 0.50f, 0f));
            return frame;
        }

        private static CanonicalPoseFrame JogInPlaceFrame(bool leftHigh)
        {
            var frame = TorsoOnlyFrame();
            var leftFootY = leftHigh ? 0.17f : 0.07f;
            var rightFootY = leftHigh ? 0.07f : 0.17f;

            AddSupportFoot(
                frame,
                true,
                new Vector2(0.58f, leftFootY));
            AddSupportFoot(
                frame,
                false,
                new Vector2(0.42f, rightFootY));
            SetImageOnly(
                frame,
                CanonicalJointId.LeftKnee,
                new Vector2(
                    0.56f,
                    leftHigh ? 0.50f : 0.44f));
            SetImageOnly(
                frame,
                CanonicalJointId.RightKnee,
                new Vector2(
                    0.44f,
                    leftHigh ? 0.44f : 0.50f));
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

            SetImageOnly(
                frame,
                ankle,
                center + new Vector2(0f, 0.010f));
            SetImageOnly(
                frame,
                heel,
                center + new Vector2(-0.012f, -0.006f));
            SetImageOnly(
                frame,
                toe,
                center + new Vector2(0.018f, -0.004f));
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
