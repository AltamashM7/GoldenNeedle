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
            var anchor = new GroundedCrouchFootAnchor();
            var sample = anchor.Update(
                true,
                true,
                VerticalLocomotionState.Standing,
                true,
                true,
                -1f,
                -1f,
                1f,
                0.6f,
                1000f,
                0.1f);

            Assert.That(sample.referenceReady, Is.True);
            Assert.That(sample.ownsRootY, Is.False);
            Assert.That(Mathf.Abs(sample.correctionY), Is.LessThan(0.0001f));
        }

        [Test]
        public void CrouchSolvedFootRiseProducesMatchingDownwardRootCorrection()
        {
            var anchor = ReadyAnchor();
            var crouch = anchor.Update(
                true,
                true,
                VerticalLocomotionState.Crouch,
                true,
                true,
                -0.75f,
                -0.75f,
                1f,
                0.6f,
                1000f,
                0.1f);

            Assert.That(crouch.measurementTrusted, Is.True);
            Assert.That(crouch.ownsRootY, Is.True);
            Assert.That(crouch.correctionY, Is.EqualTo(-0.25f).Within(0.001f));
        }

        [Test]
        public void CrouchGroundingCorrectionIsBounded()
        {
            var anchor = ReadyAnchor();
            var crouch = anchor.Update(
                true,
                true,
                VerticalLocomotionState.Crouch,
                true,
                true,
                0f,
                0f,
                1f,
                0.30f,
                1000f,
                0.1f);

            Assert.That(crouch.correctionY, Is.EqualTo(-0.30f).Within(0.001f));
        }

        [Test]
        public void BilateralFootNoiseDoesNotCreateLargeGroundingJitter()
        {
            var anchor = ReadyAnchor();
            var first = anchor.Update(
                true,
                true,
                VerticalLocomotionState.Crouch,
                true,
                true,
                -0.75f,
                -0.77f,
                1f,
                0.6f,
                1000f,
                0.1f);
            var second = anchor.Update(
                true,
                true,
                VerticalLocomotionState.Crouch,
                true,
                true,
                -0.76f,
                -0.74f,
                1f,
                0.6f,
                1000f,
                0.1f);

            Assert.That(first.measurementTrusted, Is.True);
            Assert.That(second.measurementTrusted, Is.True);
            Assert.That(Mathf.Abs(second.correctionY - first.correctionY), Is.LessThan(0.02f));
        }

        [Test]
        public void UnreliableBilateralCrouchMeasurementHoldsLastTrustedCorrection()
        {
            var anchor = ReadyAnchor();
            var trusted = anchor.Update(
                true,
                true,
                VerticalLocomotionState.Crouch,
                true,
                true,
                -0.75f,
                -0.75f,
                1f,
                0.6f,
                1000f,
                0.1f);
            var unreliable = anchor.Update(
                true,
                true,
                VerticalLocomotionState.Crouch,
                true,
                true,
                -0.60f,
                -0.90f,
                1f,
                0.6f,
                1000f,
                0.1f);

            Assert.That(unreliable.measurementTrusted, Is.False);
            Assert.That(unreliable.ownsRootY, Is.True);
            Assert.That(unreliable.correctionY, Is.EqualTo(trusted.correctionY).Within(0.001f));
        }

        [Test]
        public void CrouchReleaseReturnsTowardStandingWithoutOvershoot()
        {
            var anchor = ReadyAnchor();
            var crouch = anchor.Update(
                true,
                true,
                VerticalLocomotionState.Crouch,
                true,
                true,
                -0.75f,
                -0.75f,
                1f,
                0.6f,
                1000f,
                0.1f);
            var released = anchor.Update(
                true,
                true,
                VerticalLocomotionState.Standing,
                true,
                true,
                -1f,
                -1f,
                1f,
                0.6f,
                8f,
                0.1f);

            Assert.That(released.correctionY, Is.GreaterThan(crouch.correctionY));
            Assert.That(released.correctionY, Is.LessThanOrEqualTo(0f));
            Assert.That(released.ownsRootY, Is.True);
        }

        [Test]
        public void JumpDisablesGroundedCrouchCompensation()
        {
            var anchor = ReadyAnchor();
            anchor.Update(
                true,
                true,
                VerticalLocomotionState.Crouch,
                true,
                true,
                -0.75f,
                -0.75f,
                1f,
                0.6f,
                1000f,
                0.1f);
            var jump = anchor.Update(
                true,
                true,
                VerticalLocomotionState.Jump,
                true,
                true,
                -0.80f,
                -0.80f,
                1f,
                0.6f,
                8f,
                0.1f);

            Assert.That(jump.ownsRootY, Is.False);
        }

        [Test]
        public void GroundingResetClearsStaleCrouchState()
        {
            var anchor = ReadyAnchor();
            anchor.Update(
                true,
                true,
                VerticalLocomotionState.Crouch,
                true,
                true,
                -0.75f,
                -0.75f,
                1f,
                0.6f,
                1000f,
                0.1f);

            anchor.Reset();
            var afterReset = anchor.Update(
                true,
                true,
                VerticalLocomotionState.Crouch,
                true,
                true,
                -0.75f,
                -0.75f,
                1f,
                0.6f,
                1000f,
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

        private static GroundedCrouchFootAnchor ReadyAnchor()
        {
            var anchor = new GroundedCrouchFootAnchor();
            anchor.Update(
                true,
                true,
                VerticalLocomotionState.Standing,
                true,
                true,
                -1f,
                -1f,
                1f,
                0.6f,
                1000f,
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
