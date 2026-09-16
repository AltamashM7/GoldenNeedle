using GoldenNeedle.Core.Motion.Locomotion;
using GoldenNeedle.Core.Motion.Retargeting;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class LocomotionGroundingStabilityTests
    {
        [Test]
        public void StationaryGateHoldsLateralJitterAtNonzeroOffset()
        {
            var fusion = StableFusion();
            var map = IdentityMap();

            fusion.Evaluate(Root(0f, 0f), default, Vector2.up, map, 0.1f);
            var moved = fusion.Evaluate(Root(0.06f, 0f), default, Vector2.up, map, 0.1f);
            var jitterA = fusion.Evaluate(Root(0.068f, 0f, 0.20f, 0f), default, Vector2.up, map, 0.1f);
            var jitterB = fusion.Evaluate(Root(0.052f, 0f, -0.20f, 0f), default, Vector2.up, map, 0.1f);

            Assert.That(moved.physicalContribution.x, Is.EqualTo(0.06f).Within(0.0001f));
            Assert.That(jitterA.physicalContribution.x, Is.EqualTo(moved.physicalContribution.x).Within(0.0001f));
            Assert.That(jitterB.physicalContribution.x, Is.EqualTo(moved.physicalContribution.x).Within(0.0001f));
            Assert.That(jitterA.physicalActivity, Is.LessThan(0.001f));
            Assert.That(jitterB.physicalActivity, Is.LessThan(0.001f));
        }

        [Test]
        public void StationaryGateHoldsDepthJitterAtNonzeroOffset()
        {
            var fusion = StableFusion();
            var map = IdentityMap();

            fusion.Evaluate(Root(0f, 0f), default, Vector2.up, map, 0.1f);
            var moved = fusion.Evaluate(Root(0f, 0.08f), default, Vector2.up, map, 0.1f);
            var jitterA = fusion.Evaluate(Root(0f, 0.094f, 0f, 0.30f), default, Vector2.up, map, 0.1f);
            var jitterB = fusion.Evaluate(Root(0f, 0.066f, 0f, -0.30f), default, Vector2.up, map, 0.1f);

            Assert.That(moved.physicalContribution.y, Is.EqualTo(0.08f).Within(0.0001f));
            Assert.That(jitterA.physicalContribution.y, Is.EqualTo(moved.physicalContribution.y).Within(0.0001f));
            Assert.That(jitterB.physicalContribution.y, Is.EqualTo(moved.physicalContribution.y).Within(0.0001f));
            Assert.That(jitterA.physicalActivity, Is.LessThan(0.001f));
            Assert.That(jitterB.physicalActivity, Is.LessThan(0.001f));
        }

        [Test]
        public void SlowDeliberateMotionAccumulatesUntilMovementEntry()
        {
            var fusion = StableFusion();
            var map = IdentityMap();

            fusion.Evaluate(Root(0f, 0f), default, Vector2.up, map, 0.1f);
            var first = fusion.Evaluate(Root(0.010f, 0f), default, Vector2.up, map, 0.1f);
            var second = fusion.Evaluate(Root(0.020f, 0f), default, Vector2.up, map, 0.1f);
            var acquired = fusion.Evaluate(Root(0.030f, 0f), default, Vector2.up, map, 0.1f);

            Assert.That(Mathf.Abs(first.physicalContribution.x), Is.LessThan(0.0001f));
            Assert.That(Mathf.Abs(second.physicalContribution.x), Is.LessThan(0.0001f));
            Assert.That(acquired.physicalContribution.x, Is.EqualTo(0.030f).Within(0.0001f));
        }

        [Test]
        public void AcquiredMotionUsesLowerContinuationThresholdWithoutChatter()
        {
            var fusion = StableFusion();
            var map = IdentityMap();

            fusion.Evaluate(Root(0f, 0f), default, Vector2.up, map, 0.1f);
            var acquired = fusion.Evaluate(Root(0.030f, 0f), default, Vector2.up, map, 0.1f);
            var continued = fusion.Evaluate(Root(0.042f, 0f), default, Vector2.up, map, 0.1f);

            Assert.That(acquired.physicalContribution.x, Is.EqualTo(0.030f).Within(0.0001f));
            Assert.That(continued.physicalContribution.x, Is.EqualTo(0.042f).Within(0.0001f));
        }

        [Test]
        public void SettledMotionReleasesPhysicalActivity()
        {
            var fusion = StableFusion();
            var map = IdentityMap();

            fusion.Evaluate(Root(0f, 0f), default, Vector2.up, map, 0.1f);
            fusion.Evaluate(Root(0.030f, 0f), default, Vector2.up, map, 0.1f);
            fusion.Evaluate(Root(0.042f, 0f), default, Vector2.up, map, 0.1f);
            var settled = fusion.Evaluate(Root(0.046f, 0f, 0.30f, 0f), default, Vector2.up, map, 0.1f);

            Assert.That(settled.physicalContribution.x, Is.EqualTo(0.042f).Within(0.0001f));
            Assert.That(settled.physicalActivity, Is.LessThan(0.001f));
            Assert.That(settled.physicalTranslationActive, Is.False);
        }

        [Test]
        public void GatedNoiseDoesNotSuppressCadence()
        {
            var fusion = StableFusion();
            var map = IdentityMap();
            var cadence = new CadenceSample
            {
                active = true,
                confidence = 1f,
                virtualSpeed = 1f,
            };

            fusion.Evaluate(Root(0.06f, 0f), default, Vector2.up, map, 0.1f);
            var jitter = fusion.Evaluate(
                Root(0.067f, 0f, 0.40f, 0f),
                cadence,
                Vector2.up,
                map,
                0.1f);

            Assert.That(jitter.physicalActivity, Is.LessThan(0.001f));
            Assert.That(jitter.cadenceActive, Is.True);
            Assert.That(jitter.cadenceVelocity.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void PhysicalGateResetSupportsRecenterWithoutTeleport()
        {
            var fusion = StableFusion();
            var map = IdentityMap();

            fusion.Evaluate(Root(0f, 0f), default, Vector2.up, map, 0.1f);
            var moved = fusion.Evaluate(Root(0.06f, 0f), default, Vector2.up, map, 0.1f);
            Assert.That(moved.physicalContribution.x, Is.EqualTo(0.06f).Within(0.0001f));

            fusion.ResetPhysicalContribution();
            var recentered = fusion.Evaluate(Root(0f, 0f), default, Vector2.up, map, 0.1f);
            var smallNoise = fusion.Evaluate(Root(0.01f, 0f), default, Vector2.up, map, 0.1f);

            Assert.That(recentered.physicalContribution.magnitude, Is.LessThan(0.0001f));
            Assert.That(smallNoise.physicalContribution.magnitude, Is.LessThan(0.0001f));
        }

        [Test]
        public void TrackingReacquisitionRebasesGateWithoutTeleport()
        {
            var fusion = StableFusion();
            var map = IdentityMap();

            fusion.Evaluate(Root(0f, 0f), default, Vector2.up, map, 0.1f);
            var moved = fusion.Evaluate(Root(0.06f, 0f), default, Vector2.up, map, 0.1f);
            var lost = fusion.Evaluate(Root(0.06f, 0f, valid: false), default, Vector2.up, map, 0.1f);
            var reacquired = fusion.Evaluate(Root(0.20f, 0f), default, Vector2.up, map, 0.1f);
            var continued = fusion.Evaluate(Root(0.24f, 0f), default, Vector2.up, map, 0.1f);

            Assert.That(lost.physicalContribution.x, Is.EqualTo(moved.physicalContribution.x).Within(0.0001f));
            Assert.That(reacquired.physicalContribution.x, Is.EqualTo(moved.physicalContribution.x).Within(0.0001f));
            Assert.That(continued.physicalContribution.x, Is.GreaterThan(reacquired.physicalContribution.x + 0.02f));
        }

        [Test]
        public void StandingFootAnchorCapturesZeroCorrectionReference()
        {
            var settings = GroundingSettings();
            var anchor = new GroundedCrouchFootAnchor();
            var sample = anchor.Update(
                true,
                GroundedVertical(),
                settings,
                true,
                -1f,
                -1f,
                1f,
                0.1f);

            Assert.That(sample.referenceReady, Is.True);
            Assert.That(sample.ownsRootY, Is.False);
            Assert.That(Mathf.Abs(sample.correctionY), Is.LessThan(0.0001f));
        }

        [Test]
        public void ShallowGroundedBendAnchorsFeetBeforeSemanticCrouch()
        {
            var settings = GroundingSettings();
            var anchor = ReadyAnchor(settings);
            var shallow = anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Standing,
                    crouchCompression: 0.08f),
                settings,
                true,
                -0.92f,
                -0.92f,
                1f,
                0.1f);

            Assert.That(shallow.measurementTrusted, Is.True);
            Assert.That(shallow.ownsRootY, Is.True);
            Assert.That(shallow.correctionY, Is.EqualTo(-0.08f).Within(0.001f));
        }

        [Test]
        public void GroundingCorrectionIncreasesSmoothlyFromShallowBendIntoSemanticCrouch()
        {
            var settings = GroundingSettings(response: 8f);
            var anchor = ReadyAnchor(settings);
            var shallow = anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Standing,
                    crouchCompression: 0.08f),
                settings,
                true,
                -0.92f,
                -0.92f,
                1f,
                0.1f);
            var crouch = anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Crouch,
                    crouchCompression: 0.25f),
                settings,
                true,
                -0.75f,
                -0.75f,
                1f,
                0.1f);

            Assert.That(shallow.correctionY, Is.LessThan(0f));
            Assert.That(crouch.ownsRootY, Is.True);
            Assert.That(crouch.correctionY, Is.LessThan(shallow.correctionY));
            Assert.That(crouch.correctionY, Is.GreaterThanOrEqualTo(-0.25f));
        }

        [Test]
        public void ReturningUprightReleasesGroundingTowardZeroWithoutPop()
        {
            var settings = GroundingSettings(response: 8f);
            var anchor = ReadyAnchor(settings);
            var bent = anchor.Update(
                true,
                GroundedVertical(crouchCompression: 0.12f),
                settings,
                true,
                -0.82f,
                -0.82f,
                1f,
                0.1f);
            var releasing = anchor.Update(
                true,
                GroundedVertical(),
                settings,
                true,
                -1f,
                -1f,
                1f,
                0.1f);

            Assert.That(releasing.correctionY, Is.GreaterThan(bent.correctionY));
            Assert.That(releasing.correctionY, Is.LessThanOrEqualTo(0f));
            Assert.That(releasing.ownsRootY, Is.True);

            settings.verticalResponse = 1000f;
            var upright = anchor.Update(
                true,
                GroundedVertical(),
                settings,
                true,
                -1f,
                -1f,
                1f,
                0.1f);
            Assert.That(Mathf.Abs(upright.correctionY), Is.LessThan(0.0001f));
            Assert.That(upright.ownsRootY, Is.False);
        }

        [Test]
        public void TinyStandingFootNoiseRemainsInsideGroundingDeadband()
        {
            var settings = GroundingSettings();
            var anchor = ReadyAnchor(settings);
            var noise = anchor.Update(
                true,
                GroundedVertical(crouchCompression: 0.03f),
                settings,
                true,
                -0.994f,
                -0.994f,
                1f,
                0.1f);

            Assert.That(noise.measurementTrusted, Is.True);
            Assert.That(noise.ownsRootY, Is.False);
            Assert.That(Mathf.Abs(noise.correctionY), Is.LessThan(0.0001f));
        }

        [Test]
        public void UnilateralSolvedFootMovementDoesNotCreateLargeGroundingCorrection()
        {
            var settings = GroundingSettings();
            var anchor = ReadyAnchor(settings);
            var unilateral = anchor.Update(
                true,
                GroundedVertical(crouchCompression: 0.10f),
                settings,
                true,
                -0.72f,
                -1.00f,
                1f,
                0.1f);

            Assert.That(unilateral.measurementTrusted, Is.False);
            Assert.That(unilateral.bilateralDifferenceNormalized, Is.GreaterThan(0.12f));
            Assert.That(Mathf.Abs(unilateral.correctionY), Is.LessThan(0.0001f));
            Assert.That(unilateral.ownsRootY, Is.False);
        }

        [Test]
        public void SupportRiseTakeoffEvidenceDisablesGroundingBeforeSemanticJump()
        {
            var settings = GroundingSettings();
            var anchor = ReadyAnchor(settings);
            var grounded = anchor.Update(
                true,
                GroundedVertical(crouchCompression: 0.08f),
                settings,
                true,
                -0.92f,
                -0.92f,
                1f,
                0.1f);
            var takeoffEvidence = anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Standing,
                    crouchCompression: 0.05f,
                    supportRise: 0.08f),
                settings,
                true,
                -0.88f,
                -0.88f,
                1f,
                0.1f);

            Assert.That(grounded.ownsRootY, Is.True);
            Assert.That(takeoffEvidence.ownsRootY, Is.False);
            Assert.That(takeoffEvidence.measurementTrusted, Is.False);
        }

        [Test]
        public void SemanticJumpAlwaysDisablesGrounding()
        {
            var settings = GroundingSettings();
            var anchor = ReadyAnchor(settings);
            anchor.Update(
                true,
                GroundedVertical(crouchCompression: 0.08f),
                settings,
                true,
                -0.92f,
                -0.92f,
                1f,
                0.1f);
            var jump = anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Jump,
                    supportRise: 0.03f),
                settings,
                true,
                -0.85f,
                -0.85f,
                1f,
                0.1f);

            Assert.That(jump.ownsRootY, Is.False);
            Assert.That(jump.measurementTrusted, Is.False);
        }

        [Test]
        public void DeepSemanticCrouchStillUsesBilateralGroundAnchoring()
        {
            var settings = GroundingSettings();
            var anchor = ReadyAnchor(settings);
            var crouch = anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Crouch,
                    crouchCompression: 0.25f),
                settings,
                true,
                -0.75f,
                -0.75f,
                1f,
                0.1f);

            Assert.That(crouch.measurementTrusted, Is.True);
            Assert.That(crouch.ownsRootY, Is.True);
            Assert.That(crouch.correctionY, Is.EqualTo(-0.25f).Within(0.001f));
        }

        [Test]
        public void CrouchGroundingCorrectionIsBounded()
        {
            var settings = GroundingSettings(maximumDepth: 0.30f);
            var anchor = ReadyAnchor(settings);
            var crouch = anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Crouch,
                    crouchCompression: 0.60f),
                settings,
                true,
                0f,
                0f,
                1f,
                0.1f);

            Assert.That(crouch.correctionY, Is.EqualTo(-0.30f).Within(0.001f));
        }

        [Test]
        public void BilateralFootNoiseDoesNotCreateLargeGroundingJitter()
        {
            var settings = GroundingSettings();
            var anchor = ReadyAnchor(settings);
            var first = anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Crouch,
                    crouchCompression: 0.25f),
                settings,
                true,
                -0.75f,
                -0.77f,
                1f,
                0.1f);
            var second = anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Crouch,
                    crouchCompression: 0.25f),
                settings,
                true,
                -0.76f,
                -0.74f,
                1f,
                0.1f);

            Assert.That(first.measurementTrusted, Is.True);
            Assert.That(second.measurementTrusted, Is.True);
            Assert.That(Mathf.Abs(second.correctionY - first.correctionY), Is.LessThan(0.02f));
        }

        [Test]
        public void UnreliableBilateralCrouchMeasurementHoldsLastTrustedCorrection()
        {
            var settings = GroundingSettings();
            var anchor = ReadyAnchor(settings);
            var trusted = anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Crouch,
                    crouchCompression: 0.25f),
                settings,
                true,
                -0.75f,
                -0.75f,
                1f,
                0.1f);
            var unreliable = anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Crouch,
                    crouchCompression: 0.25f),
                settings,
                true,
                -0.60f,
                -0.90f,
                1f,
                0.1f);

            Assert.That(unreliable.measurementTrusted, Is.False);
            Assert.That(unreliable.ownsRootY, Is.True);
            Assert.That(unreliable.correctionY, Is.EqualTo(trusted.correctionY).Within(0.001f));
        }

        [Test]
        public void GroundingResetClearsStaleCrouchState()
        {
            var settings = GroundingSettings();
            var anchor = ReadyAnchor(settings);
            anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Crouch,
                    crouchCompression: 0.25f),
                settings,
                true,
                -0.75f,
                -0.75f,
                1f,
                0.1f);

            anchor.Reset();
            var afterReset = anchor.Update(
                true,
                GroundedVertical(
                    state: VerticalLocomotionState.Crouch,
                    crouchCompression: 0.25f),
                settings,
                true,
                -0.75f,
                -0.75f,
                1f,
                0.1f);

            Assert.That(afterReset.referenceReady, Is.False);
            Assert.That(afterReset.ownsRootY, Is.False);
            Assert.That(Mathf.Abs(afterReset.correctionY), Is.LessThan(0.0001f));
        }

        private static LocomotionFusion StableFusion()
        {
            return new LocomotionFusion(new LocomotionFusionSettings
            {
                lateralScale = 1f,
                depthScale = 1f,
                lateralDeadzone = 0f,
                depthDeadzone = 0f,
                lateralMovementEnter = 0.025f,
                lateralMovementRelease = 0.010f,
                depthMovementEnter = 0.040f,
                depthMovementRelease = 0.016f,
                physicalVelocityStart = 0.05f,
                physicalVelocityFull = 0.20f,
                minimumRootConfidence = 0.2f,
            });
        }

        private static CameraSpaceRootSample Root(
            float x,
            float depth,
            float velocityX = 0f,
            float velocityDepth = 0f,
            bool valid = true)
        {
            return new CameraSpaceRootSample
            {
                isValid = valid,
                hasOrigin = true,
                displacementXZ = new Vector2(x, depth),
                velocityXZ = new Vector2(velocityX, velocityDepth),
                confidence = valid ? 1f : 0f,
            };
        }

        private static VerticalLocomotionSettings GroundingSettings(
            float response = 1000f,
            float maximumDepth = 0.60f)
        {
            return new VerticalLocomotionSettings
            {
                maximumCrouchDepth = maximumDepth,
                verticalResponse = response,
                groundedSupportTolerance = 0.06f,
                maximumJumpFootAsymmetry = 0.08f,
                maximumApparentScaleChange = 0.12f,
            };
        }

        private static VerticalLocomotionSample GroundedVertical(
            VerticalLocomotionState state = VerticalLocomotionState.Standing,
            float crouchCompression = 0f,
            float supportRise = 0f,
            float footAsymmetry = 0f,
            float apparentScaleChange = 0f,
            bool available = true)
        {
            return new VerticalLocomotionSample
            {
                isAvailable = available,
                referenceReady = true,
                state = state,
                jumpPhase = state == VerticalLocomotionState.Jump
                    ? VerticalJumpPhase.Takeoff
                    : VerticalJumpPhase.Grounded,
                crouchCompression = crouchCompression,
                supportRise = supportRise,
                footAsymmetry = footAsymmetry,
                apparentScaleChange = apparentScaleChange,
                suppressPhysicalDepth = state == VerticalLocomotionState.Jump,
            };
        }

        private static GroundedCrouchFootAnchor ReadyAnchor(
            VerticalLocomotionSettings settings)
        {
            var anchor = new GroundedCrouchFootAnchor();
            anchor.Update(
                true,
                GroundedVertical(),
                settings,
                true,
                -1f,
                -1f,
                1f,
                0.1f);
            return anchor;
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
    }
}
