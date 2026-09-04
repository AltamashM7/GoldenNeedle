using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Rotation;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class HumanoidRetargetingTests
    {
        [Test]
        public void AnalyticIkSolvesReachableBentChain()
        {
            var solver = new AnalyticTwoBoneIkSolver();
            var request = new TwoBoneIkRequest
            {
                rootPosition = Vector3.zero,
                targetEffectorPosition = new Vector3(0.35f, 0.35f, 0f),
                bendHintPosition = new Vector3(0f, 0f, 1f),
                hasBendHint = true,
                referenceBendDirection = Vector3.forward,
                fallbackEffectorDirection = Vector3.right,
                upperLength = 0.6f,
                lowerLength = 0.5f,
            };

            Assert.That(solver.TrySolve(request, out var result), Is.True);
            Assert.That(Vector3.Distance(result.solvedRootPosition, result.solvedMidPosition), Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(Vector3.Distance(result.solvedMidPosition, result.solvedEffectorPosition), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(Vector3.Distance(result.solvedEffectorPosition, request.targetEffectorPosition), Is.LessThan(0.0001f));
            Assert.That(result.usedBendHint, Is.True);
        }

        [Test]
        public void AnalyticIkClampsNearExtendedAndOverCompressedTargets()
        {
            var solver = new AnalyticTwoBoneIkSolver();
            var nearExtended = new TwoBoneIkRequest
            {
                rootPosition = Vector3.zero,
                targetEffectorPosition = new Vector3(1.5f, 0f, 0f),
                referenceBendDirection = Vector3.up,
                fallbackEffectorDirection = Vector3.right,
                upperLength = 0.75f,
                lowerLength = 0.65f,
            };
            var overCompressed = nearExtended;
            overCompressed.targetEffectorPosition = Vector3.zero;

            Assert.That(solver.TrySolve(nearExtended, out var extendedResult), Is.True);
            Assert.That(extendedResult.clampedTargetDistance, Is.LessThan(nearExtended.targetEffectorPosition.magnitude));
            Assert.That(Vector3.Distance(extendedResult.solvedRootPosition, extendedResult.solvedMidPosition), Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(solver.TrySolve(overCompressed, out var compressedResult), Is.True);
            Assert.That(compressedResult.clampedTargetDistance, Is.GreaterThan(0f));
            Assert.That(IsFinite(compressedResult.solvedMidPosition), Is.True);
        }

        [Test]
        public void AnalyticIkSupportsArbitraryWorldOrientation()
        {
            var solver = new AnalyticTwoBoneIkSolver();
            var request = new TwoBoneIkRequest
            {
                rootPosition = new Vector3(1.2f, -0.4f, 2.1f),
                targetEffectorPosition = new Vector3(1.35f, -0.05f, 2.38f),
                bendHintPosition = new Vector3(1.8f, -0.25f, 2.0f),
                hasBendHint = true,
                referenceBendDirection = Vector3.up,
                fallbackEffectorDirection = Vector3.forward,
                upperLength = 0.45f,
                lowerLength = 0.4f,
            };

            Assert.That(solver.TrySolve(request, out var result), Is.True);
            Assert.That(Vector3.Distance(result.solvedRootPosition, result.solvedMidPosition), Is.EqualTo(request.upperLength).Within(0.0001f));
            Assert.That(Vector3.Distance(result.solvedMidPosition, result.solvedEffectorPosition), Is.EqualTo(request.lowerLength).Within(0.0001f));
            Assert.That(IsFinite(result.solvedEffectorPosition), Is.True);
        }

        [Test]
        public void AnalyticIkRejectsInvalidLengthsAndCoordinates()
        {
            var solver = new AnalyticTwoBoneIkSolver();
            var request = new TwoBoneIkRequest
            {
                rootPosition = Vector3.zero,
                targetEffectorPosition = Vector3.right,
                referenceBendDirection = Vector3.up,
                fallbackEffectorDirection = Vector3.right,
                upperLength = float.NaN,
                lowerLength = 0.5f,
            };
            Assert.That(solver.TrySolve(request, out _), Is.False);

            request.upperLength = 0.5f;
            request.targetEffectorPosition = new Vector3(float.PositiveInfinity, 0f, 0f);
            Assert.That(solver.TrySolve(request, out _), Is.False);
        }

        [Test]
        public void AnalyticIkUsesPreviousPlaneWhenHintIsDegenerateAndKeepsItsSign()
        {
            var solver = new AnalyticTwoBoneIkSolver();
            var first = new TwoBoneIkRequest
            {
                rootPosition = Vector3.zero,
                targetEffectorPosition = new Vector3(0f, -0.5f, 0f),
                bendHintPosition = Vector3.forward,
                hasBendHint = true,
                referenceBendDirection = Vector3.back,
                fallbackEffectorDirection = Vector3.down,
                upperLength = 0.4f,
                lowerLength = 0.35f,
            };
            Assert.That(solver.TrySolve(first, out var firstResult), Is.True);

            var second = first;
            second.bendHintPosition = Vector3.down * 0.01f;
            second.hasBendHint = true;
            second.previousBendDirection = firstResult.bendDirection;
            second.hasPreviousBendDirection = true;
            Assert.That(solver.TrySolve(second, out var secondResult), Is.True);
            Assert.That(secondResult.usedPreviousBendDirection, Is.True);
            Assert.That(Vector3.Dot(firstResult.bendDirection, secondResult.bendDirection), Is.GreaterThan(0.99f));
        }

        [Test]
        public void KinematicTargetBuilderNormalizesEachSideWithItsOwnReach()
        {
            var profile = ValidProfile();
            profile.leftArmGeometry = Geometry(0.25f, 0.25f, Vector3.down, Vector3.down);
            profile.rightArmGeometry = Geometry(0.5f, 0.5f, Vector3.down, Vector3.down);
            var frame = CreateCanonicalFrame(
                new Vector3(-0.4f, 1.2f, 0f),
                new Vector3(-0.4f, 0.9f, 0f),
                new Vector3(-0.4f, 0.7f, 0f),
                new Vector3(0.4f, 1.2f, 0f),
                new Vector3(0.4f, 0.9f, 0f),
                new Vector3(0.4f, 0.2f, 0f));
            var targets = new CanonicalKinematicTargets();

            new CanonicalKinematicTargetBuilder().Build(frame, profile, targets);

            var left = targets.GetTarget(CanonicalKinematicChainId.LeftArm);
            var right = targets.GetTarget(CanonicalKinematicChainId.RightArm);
            Assert.That(left.isValid, Is.True);
            Assert.That(right.isValid, Is.True);
            Assert.That(left.sourceReach, Is.EqualTo(0.5f));
            Assert.That(right.sourceReach, Is.EqualTo(1f));
            Assert.That(left.normalizedEffectorDisplacement.y, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(right.normalizedEffectorDisplacement.y, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(Vector3.Distance(left.sourceRootPosition, left.sourceEffectorPosition), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(Vector3.Distance(right.sourceRootPosition, right.sourceEffectorPosition), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void KinematicTargetBuilderAllowsRootAndEffectorWithoutMidHint()
        {
            var profile = ValidProfile();
            var frame = CreateCanonicalFrame(
                new Vector3(-0.4f, 1.2f, 0f),
                Vector3.zero,
                new Vector3(-0.4f, 0.7f, 0f),
                new Vector3(0.4f, 1.2f, 0f),
                Vector3.zero,
                new Vector3(0.4f, 0.7f, 0f));
            frame.SetJoint(Untracked(CanonicalJointId.LeftElbow));
            frame.SetJoint(Untracked(CanonicalJointId.RightElbow));
            frame.Complete();
            var targets = new CanonicalKinematicTargets();

            new CanonicalKinematicTargetBuilder().Build(frame, profile, targets);

            Assert.That(targets.GetTarget(CanonicalKinematicChainId.LeftArm).isValid, Is.True);
            Assert.That(targets.GetTarget(CanonicalKinematicChainId.LeftArm).hasBendHint, Is.False);
            Assert.That(targets.GetTarget(CanonicalKinematicChainId.RightArm).isValid, Is.True);
            Assert.That(targets.GetTarget(CanonicalKinematicChainId.RightArm).hasBendHint, Is.False);
        }

        [Test]
        public void AsymmetricArmTargetsDriveLeftDownAndRightUp()
        {
            var context = CreateRigContext();
            try
            {
                var leftDisplacement = Vector3.down * 0.65992f;
                var rightDisplacement = Vector3.up * 0.65992f;
                var targets = NewTargets();
                AddTarget(targets, CanonicalKinematicChainId.LeftArm, context.binding, leftDisplacement, Vector3.forward);
                AddTarget(targets, CanonicalKinematicChainId.RightArm, context.binding, rightDisplacement, Vector3.forward);
                targets.Complete();

                Apply(context, targets, ReferenceFrame());

                AssertTipAtDisplacement(context, CanonicalKinematicChainId.LeftArm, leftDisplacement);
                AssertTipAtDisplacement(context, CanonicalKinematicChainId.RightArm, rightDisplacement);
                AssertChainSegmentDirections(context, CanonicalKinematicChainId.LeftArm, Vector3.down);
                AssertChainSegmentDirections(context, CanonicalKinematicChainId.RightArm, Vector3.up);
                Assert.That(context.retargeter.IkChainsSolved, Is.EqualTo(2));
                Assert.That(context.retargeter.LimbBonesDriven, Is.EqualTo(4));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void ComplementaryArmTargetsDriveLeftUpAndRightDownWithoutSwappingSides()
        {
            var context = CreateRigContext();
            try
            {
                var leftDisplacement = Vector3.up * 0.65992f;
                var rightDisplacement = Vector3.down * 0.65992f;
                var targets = NewTargets();
                AddTarget(targets, CanonicalKinematicChainId.LeftArm, context.binding, leftDisplacement, Vector3.forward);
                AddTarget(targets, CanonicalKinematicChainId.RightArm, context.binding, rightDisplacement, Vector3.forward);
                targets.Complete();

                Apply(context, targets, ReferenceFrame());

                AssertTipAtDisplacement(context, CanonicalKinematicChainId.LeftArm, leftDisplacement);
                AssertTipAtDisplacement(context, CanonicalKinematicChainId.RightArm, rightDisplacement);
                AssertChainSegmentDirections(context, CanonicalKinematicChainId.LeftArm, Vector3.up);
                AssertChainSegmentDirections(context, CanonicalKinematicChainId.RightArm, Vector3.down);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void BentBothArmsUseTheirActualWristTargetsAndHints()
        {
            var context = CreateRigContext();
            try
            {
                var leftDisplacement = new Vector3(-0.12f, 0.42f, 0.18f);
                var rightDisplacement = new Vector3(0.12f, 0.42f, -0.18f);
                var targets = NewTargets();
                AddTarget(targets, CanonicalKinematicChainId.LeftArm, context.binding, leftDisplacement, Vector3.forward);
                AddTarget(targets, CanonicalKinematicChainId.RightArm, context.binding, rightDisplacement, Vector3.back);
                targets.Complete();

                Apply(context, targets, ReferenceFrame());

                AssertTipAtDisplacement(context, CanonicalKinematicChainId.LeftArm, leftDisplacement);
                AssertTipAtDisplacement(context, CanonicalKinematicChainId.RightArm, rightDisplacement);
                Assert.That(context.retargeter.GetChainDebugState(CanonicalKinematicChainId.LeftArm).hasBendHint, Is.True);
                Assert.That(context.retargeter.GetChainDebugState(CanonicalKinematicChainId.RightArm).hasBendHint, Is.True);
                AssertBendPlane(context, CanonicalKinematicChainId.LeftArm);
                AssertBendPlane(context, CanonicalKinematicChainId.RightArm);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void LateralRaisedLegTargetUsesTheLeftLegChainOnly()
        {
            var context = CreateRigContext();
            try
            {
                var leftDisplacement = new Vector3(-0.12f, 0.24f, 0.22f);
                var targets = NewTargets();
                AddTarget(targets, CanonicalKinematicChainId.LeftLeg, context.binding, leftDisplacement, Vector3.forward);
                targets.Complete();
                var rightBindTip = context.binding.GetChainTip(CanonicalKinematicChainId.RightLeg).position;

                Apply(context, targets, ReferenceFrame());

                AssertTipAtDisplacement(context, CanonicalKinematicChainId.LeftLeg, leftDisplacement);
                Assert.That(context.retargeter.IkChainsSolved, Is.EqualTo(1));
                Assert.That(Vector3.Distance(context.binding.GetChainTip(CanonicalKinematicChainId.RightLeg).position, rightBindTip), Is.LessThan(0.0001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void RaisedBentKneeTargetReachesTheActualFootTransform()
        {
            var context = CreateRigContext();
            try
            {
                var displacement = new Vector3(0.18f, 0.26f, 0.18f);
                var targets = NewTargets();
                AddTarget(targets, CanonicalKinematicChainId.RightLeg, context.binding, displacement, Vector3.forward);
                targets.Complete();

                Apply(context, targets, ReferenceFrame());

                AssertTipAtDisplacement(context, CanonicalKinematicChainId.RightLeg, displacement);
                var state = context.retargeter.GetChainDebugState(CanonicalKinematicChainId.RightLeg);
                Assert.That(state.endpointError, Is.LessThan(0.0001f));
                Assert.That(state.normalizedEndpointError, Is.LessThan(0.0001f));
                AssertBendPlane(context, CanonicalKinematicChainId.RightLeg);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void PartialChainStillSolvesWithRootAndEffectorWhenMidIsUnavailable()
        {
            var context = CreateRigContext();
            try
            {
                var displacement = new Vector3(0.05f, -0.42f, 0.12f);
                var targets = NewTargets();
                AddTarget(targets, CanonicalKinematicChainId.LeftArm, context.binding, displacement, Vector3.zero, false);
                targets.Complete();

                Apply(context, targets, ReferenceFrame());

                AssertTipAtDisplacement(context, CanonicalKinematicChainId.LeftArm, displacement);
                Assert.That(context.retargeter.IkChainsSolved, Is.EqualTo(1));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void ActualEndpointAndBindLocalPositionsAndScalesArePreserved()
        {
            var context = CreateRigContext();
            try
            {
                var positions = new Vector3[CanonicalRotationFrame.BoneCount];
                var scales = new Vector3[CanonicalRotationFrame.BoneCount];
                for (var i = 0; i < positions.Length; i++)
                {
                    var id = (CanonicalBoneId)i;
                    positions[i] = context.binding.GetBoneTransform(id).localPosition;
                    scales[i] = context.binding.GetBoneTransform(id).localScale;
                }
                var rootPosition = context.binding.AvatarRoot.position;
                var tipPosition = context.binding.GetChainTip(CanonicalKinematicChainId.LeftArm).localPosition;
                var tipScale = context.binding.GetChainTip(CanonicalKinematicChainId.LeftArm).localScale;
                var targets = NewTargets();
                AddTarget(targets, CanonicalKinematicChainId.LeftArm, context.binding, new Vector3(0.05f, 0.4f, 0.15f), Vector3.forward);
                targets.Complete();

                Apply(context, targets, ReferenceFrame());

                Assert.That(context.binding.AvatarRoot.position, Is.EqualTo(rootPosition));
                for (var i = 0; i < positions.Length; i++)
                {
                    var id = (CanonicalBoneId)i;
                    Assert.That(context.binding.GetBoneTransform(id).localPosition, Is.EqualTo(positions[i]));
                    Assert.That(context.binding.GetBoneTransform(id).localScale, Is.EqualTo(scales[i]));
                }
                Assert.That(context.binding.GetChainTip(CanonicalKinematicChainId.LeftArm).localPosition, Is.EqualTo(tipPosition));
                Assert.That(context.binding.GetChainTip(CanonicalKinematicChainId.LeftArm).localScale, Is.EqualTo(tipScale));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void TorsoRotationIsAppliedOnceBeforeLimbSolve()
        {
            var context = CreateRigContext();
            try
            {
                var displacement = new Vector3(0f, -0.42f, 0.16f);
                var targets = NewTargets();
                AddTarget(targets, CanonicalKinematicChainId.LeftArm, context.binding, displacement, Vector3.forward);
                targets.Complete();
                var frame = ReferenceFrame();
                frame.SetBone(new CanonicalBoneRotation
                {
                    id = CanonicalBoneId.Chest,
                    tracking = CanonicalTrackingState.Tracked,
                    confidence = 1f,
                    rotationDeltaFromCalibration = Quaternion.Euler(0f, 28f, 0f),
                });
                frame.Complete();

                Apply(context, targets, frame);

                AssertTipAtDisplacement(context, CanonicalKinematicChainId.LeftArm, displacement);
                Assert.That(Quaternion.Angle(
                    context.binding.GetBoneTransform(CanonicalBoneId.Chest).rotation,
                    Quaternion.Euler(0f, 28f, 0f) * context.binding.GetBindWorldRotation(CanonicalBoneId.Chest)), Is.LessThan(0.001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void DriveOffRestoresTheSameRigToReferencePose()
        {
            var context = CreateRigContext();
            try
            {
                var bindUpperDirection = (context.binding.GetChainMid(CanonicalKinematicChainId.LeftArm).position -
                                          context.binding.GetChainRoot(CanonicalKinematicChainId.LeftArm).position).normalized;
                var bindLowerDirection = (context.binding.GetChainTip(CanonicalKinematicChainId.LeftArm).position -
                                          context.binding.GetChainMid(CanonicalKinematicChainId.LeftArm).position).normalized;
                var targets = NewTargets();
                AddTarget(targets, CanonicalKinematicChainId.LeftArm, context.binding, new Vector3(0f, 0.4f, 0.15f), Vector3.forward);
                targets.Complete();
                Apply(context, targets, ReferenceFrame());
                context.retargeter.DriveRig = false;

                Assert.That(context.retargeter.DrivenBoneCount, Is.EqualTo(0));
                Assert.That(context.binding.GetBoneTransform(CanonicalBoneId.LeftUpperArm).localRotation,
                    Is.EqualTo(context.binding.GetBindLocalRotation(CanonicalBoneId.LeftUpperArm)));
                Assert.That(context.binding.GetBoneTransform(CanonicalBoneId.LeftLowerArm).localRotation,
                    Is.EqualTo(context.binding.GetBindLocalRotation(CanonicalBoneId.LeftLowerArm)));
                AssertSegmentDirection(
                    context.binding.GetChainRoot(CanonicalKinematicChainId.LeftArm),
                    context.binding.GetChainMid(CanonicalKinematicChainId.LeftArm),
                    bindUpperDirection);
                AssertSegmentDirection(
                    context.binding.GetChainMid(CanonicalKinematicChainId.LeftArm),
                    context.binding.GetChainTip(CanonicalKinematicChainId.LeftArm),
                    bindLowerDirection);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void SignedAxisMapRepresentsCanonicalReflectionAgainstYaw180Rig()
        {
            var context = CreateRigContext(Quaternion.Euler(0f, 180f, 0f));
            try
            {
                var profile = RetargetProfile();
                Assert.That(
                    HumanoidRetargetingMath.TryCreateCanonicalToAvatarMap(profile, context.binding, out var map),
                    Is.True);

                Assert.That(map.Source.HandednessSign, Is.EqualTo(-1f));
                Assert.That(map.Target.HandednessSign, Is.EqualTo(1f));
                Assert.That(map.DeterminantSign, Is.EqualTo(-1f));
                Assert.That(Vector3.Angle(map.MapVector(Vector3.right), Vector3.left), Is.LessThan(0.01f));
                Assert.That(Vector3.Angle(map.MapVector(Vector3.up), Vector3.up), Is.LessThan(0.01f));
                Assert.That(Vector3.Angle(map.MapVector(Vector3.back), Vector3.back), Is.LessThan(0.01f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void SignedAxisBodyRotationAllowsLargeYawWithoutHemisphereForcing()
        {
            var context = CreateRigContext(Quaternion.Euler(0f, 180f, 0f));
            try
            {
                var profile = RetargetProfile();
                Assert.That(
                    HumanoidRetargetingMath.TryCreateCanonicalToAvatarMap(profile, context.binding, out var map),
                    Is.True);

                var sourceYaw = Quaternion.Euler(0f, 135f, 0f);
                var currentRight = sourceYaw * profile.neutralBodyRight;
                var currentUp = sourceYaw * profile.neutralBodyUp;
                var currentForward = sourceYaw * profile.neutralBodyForward;
                Assert.That(
                    HumanoidRetargetingMath.TryBuildMappedBodyRotation(
                        map,
                        currentRight,
                        currentUp,
                        out var mappedRotation),
                    Is.True);

                Assert.That(
                    Vector3.Angle(mappedRotation * Vector3.right, map.MapVector(currentRight)),
                    Is.LessThan(0.01f));
                Assert.That(
                    Vector3.Angle(mappedRotation * Vector3.up, map.MapVector(currentUp)),
                    Is.LessThan(0.01f));
                Assert.That(
                    Vector3.Angle(mappedRotation * Vector3.forward, map.MapVector(currentForward)),
                    Is.LessThan(0.01f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void ProductionSignedAxisPathIsStableAcrossRepeatedYawFrames()
        {
            var context = CreateRigContext(Quaternion.Euler(0f, 180f, 0f));
            try
            {
                var profile = RetargetProfile();
                Assert.That(context.binding.TryGetReferenceBodyBasis(out var referenceBefore), Is.True);

                var sourceFrame = CreateYawedReferenceCanonicalFrame(
                    profile,
                    Quaternion.Euler(0f, 70f, 0f));
                var targets = new CanonicalKinematicTargets();
                new CanonicalKinematicTargetBuilder().Build(sourceFrame, profile, targets);
                var frame = ReferenceFrame();

                context.retargeter.ApplyMotionFrame(sourceFrame, frame, targets, profile, 1f / 60f);
                var firstState = context.retargeter.GetChainDebugState(CanonicalKinematicChainId.LeftArm);
                var firstTip = context.binding.GetChainTip(CanonicalKinematicChainId.LeftArm).position;
                Assert.That(firstState.solved, Is.True);

                Assert.That(context.binding.TryGetReferenceBodyBasis(out var referenceAfterFirst), Is.True);
                Assert.That(Vector3.Angle(referenceBefore.Right, referenceAfterFirst.Right), Is.LessThan(0.001f));
                Assert.That(Vector3.Angle(referenceBefore.Up, referenceAfterFirst.Up), Is.LessThan(0.001f));
                Assert.That(Vector3.Angle(referenceBefore.Forward, referenceAfterFirst.Forward), Is.LessThan(0.001f));

                context.retargeter.ApplyMotionFrame(sourceFrame, frame, targets, profile, 1f / 60f);
                var secondState = context.retargeter.GetChainDebugState(CanonicalKinematicChainId.LeftArm);
                var secondTip = context.binding.GetChainTip(CanonicalKinematicChainId.LeftArm).position;

                Assert.That(secondState.solved, Is.True);
                Assert.That(Vector3.Distance(firstState.desiredEffectorPosition, secondState.desiredEffectorPosition), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(firstTip, secondTip), Is.LessThan(0.0001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void ProductionSignedAxisPathDrivesAsymmetricArmsOnYaw180Rig()
        {
            var context = CreateRigContext(Quaternion.Euler(0f, 180f, 0f));
            try
            {
                var profile = RetargetProfile();
                var sourceFrame = CreateReferenceCanonicalFrame(profile);
                var targets = NewTargets();
                targets.SetTarget(new CanonicalKinematicChainTarget
                {
                    id = CanonicalKinematicChainId.LeftArm,
                    isValid = true,
                    hasBendHint = true,
                    confidence = 1f,
                    sourceReach = profile.leftArmGeometry.reach,
                    normalizedEffectorDisplacement = new Vector3(-0.15f, -0.70f, 0.20f),
                    normalizedBendHintDisplacement = new Vector3(-0.08f, -0.35f, 0.24f),
                });
                targets.SetTarget(new CanonicalKinematicChainTarget
                {
                    id = CanonicalKinematicChainId.RightArm,
                    isValid = true,
                    hasBendHint = true,
                    confidence = 1f,
                    sourceReach = profile.rightArmGeometry.reach,
                    normalizedEffectorDisplacement = new Vector3(0.20f, 0.62f, -0.18f),
                    normalizedBendHintDisplacement = new Vector3(0.11f, 0.31f, -0.22f),
                });
                targets.Complete();

                Assert.That(
                    HumanoidRetargetingMath.TryCreateCanonicalToAvatarMap(profile, context.binding, out var map),
                    Is.True);

                var frame = ReferenceFrame();
                context.retargeter.ApplyMotionFrame(sourceFrame, frame, targets, profile, 1f / 60f);

                foreach (var chainId in new[]
                {
                    CanonicalKinematicChainId.LeftArm,
                    CanonicalKinematicChainId.RightArm,
                })
                {
                    var target = targets.GetTarget(chainId);
                    var root = context.binding.GetChainRoot(chainId);
                    var tip = context.binding.GetChainTip(chainId);
                    var reach = context.binding.GetChainTotalReach(chainId);
                    var expected = root.position + map.MapVector(target.normalizedEffectorDisplacement) * reach;
                    Assert.That(Vector3.Distance(tip.position, expected), Is.LessThan(0.001f), chainId.ToString());
                    Assert.That(context.retargeter.GetChainDebugState(chainId).solved, Is.True);
                }
            }
            finally
            {
                context.Dispose();
            }
        }

        private static void Apply(RigContext context, CanonicalKinematicTargets targets, CanonicalRotationFrame frame)
        {
            context.retargeter.ApplyRotationFrame(frame, targets, Quaternion.identity, 1f / 60f);
        }

        private static CanonicalRotationFrame ReferenceFrame()
        {
            var frame = new CanonicalRotationFrame();
            frame.Begin(1L, 0d, true);
            frame.Complete();
            return frame;
        }

        private static CanonicalKinematicTargets NewTargets()
        {
            var targets = new CanonicalKinematicTargets();
            targets.Begin(1L, 0d, true);
            return targets;
        }

        private static void AddTarget(
            CanonicalKinematicTargets targets,
            CanonicalKinematicChainId id,
            HumanoidRigBinding binding,
            Vector3 displacement,
            Vector3 hint,
            bool hasHint = true)
        {
            var reach = binding.GetChainTotalReach(id);
            targets.SetTarget(new CanonicalKinematicChainTarget
            {
                id = id,
                isValid = true,
                hasBendHint = hasHint,
                confidence = 1f,
                sourceRootPosition = Vector3.zero,
                sourceMidPosition = hint,
                sourceEffectorPosition = displacement,
                sourceReach = 1f,
                normalizedEffectorDisplacement = displacement / reach,
                normalizedBendHintDisplacement = hasHint ? hint / reach : Vector3.zero,
                sourceTimestampMillisec = 1L,
                receivedAtSeconds = 0d,
            });
        }

        private static void AssertTipAtDisplacement(RigContext context, CanonicalKinematicChainId id, Vector3 displacement)
        {
            var root = context.binding.GetChainRoot(id);
            var tip = context.binding.GetChainTip(id);
            Assert.That(Vector3.Distance(tip.position, root.position + displacement), Is.LessThan(0.0001f));
            var state = context.retargeter.GetChainDebugState(id);
            Assert.That(Vector3.Distance(tip.position, state.desiredEffectorPosition), Is.LessThan(0.0001f));
        }

        private static void AssertSegmentDirection(Transform from, Transform to, Vector3 expected)
        {
            var actual = (to.position - from.position).normalized;
            Assert.That(Vector3.Angle(actual, expected), Is.LessThan(2f));
        }

        private static void AssertChainSegmentDirections(
            RigContext context,
            CanonicalKinematicChainId id,
            Vector3 expected)
        {
            AssertSegmentDirection(context.binding.GetChainRoot(id), context.binding.GetChainMid(id), expected);
            AssertSegmentDirection(context.binding.GetChainMid(id), context.binding.GetChainTip(id), expected);
        }

        private static void AssertBendPlane(RigContext context, CanonicalKinematicChainId id)
        {
            var state = context.retargeter.GetChainDebugState(id);
            var root = context.binding.GetChainRoot(id).position;
            var targetDirection = (state.desiredEffectorPosition - root).normalized;
            var expectedBend = state.bendHintPosition - root;
            expectedBend -= targetDirection * Vector3.Dot(expectedBend, targetDirection);
            expectedBend.Normalize();

            var actualMid = context.binding.GetChainMid(id).position - root;
            actualMid -= targetDirection * Vector3.Dot(actualMid, targetDirection);
            actualMid.Normalize();

            Assert.That(Vector3.Dot(actualMid, expectedBend), Is.GreaterThan(0.98f));
        }

        private static RigContext CreateRigContext()
        {
            return CreateRigContext(Quaternion.identity);
        }

        private static RigContext CreateRigContext(Quaternion rootRotation)
        {
            var rootObject = new GameObject("AnalyticIkRigRoot");
            var root = rootObject.transform;
            root.rotation = rootRotation;
            var bones = CreateBones(root, out var tips);
            var binding = rootObject.AddComponent<HumanoidRigBinding>();
            binding.ConfigureExplicit(root, bones, tips);
            var retargeter = rootObject.AddComponent<HumanoidRetargeter>();
            retargeter.DriveRig = true;
            return new RigContext(rootObject, binding, retargeter);
        }

        private static Transform[] CreateBones(Transform root, out Transform[] tips)
        {
            var hips = CreateChild("Hips", root, new Vector3(0f, 1f, 0f));
            var spine = CreateChild("Spine", hips, new Vector3(0f, 0.2f, 0f));
            var chest = CreateChild("Chest", spine, new Vector3(0f, 0.2f, 0f));
            var leftUpperArm = CreateChild("LeftUpperArm", chest, new Vector3(-0.31f, 0.02f, 0f));
            var leftLowerArm = CreateChild("LeftLowerArm", leftUpperArm, new Vector3(-0.36f, 0f, 0f));
            var leftHand = CreateChild("LeftHand", leftLowerArm, new Vector3(-0.30f, 0f, 0f));
            var rightUpperArm = CreateChild("RightUpperArm", chest, new Vector3(0.31f, 0.02f, 0f));
            var rightLowerArm = CreateChild("RightLowerArm", rightUpperArm, new Vector3(0.36f, 0f, 0f));
            var rightHand = CreateChild("RightHand", rightLowerArm, new Vector3(0.30f, 0f, 0f));
            var leftUpperLeg = CreateChild("LeftUpperLeg", hips, new Vector3(-0.17f, -0.45f, 0f));
            var leftLowerLeg = CreateChild("LeftLowerLeg", leftUpperLeg, new Vector3(0f, -0.47f, 0f));
            var leftFoot = CreateChild("LeftFoot", leftLowerLeg, new Vector3(0f, -0.12f, 0.10f));
            var rightUpperLeg = CreateChild("RightUpperLeg", hips, new Vector3(0.17f, -0.45f, 0f));
            var rightLowerLeg = CreateChild("RightLowerLeg", rightUpperLeg, new Vector3(0f, -0.47f, 0f));
            var rightFoot = CreateChild("RightFoot", rightLowerLeg, new Vector3(0f, -0.12f, 0.10f));

            tips = new[] { leftHand, rightHand, leftFoot, rightFoot };
            return new[]
            {
                hips,
                chest,
                leftUpperArm,
                leftLowerArm,
                rightUpperArm,
                rightLowerArm,
                leftUpperLeg,
                leftLowerLeg,
                rightUpperLeg,
                rightLowerLeg,
            };
        }

        private static CanonicalPoseFrame CreateCanonicalFrame(
            Vector3 leftShoulder,
            Vector3 leftElbow,
            Vector3 leftWrist,
            Vector3 rightShoulder,
            Vector3 rightElbow,
            Vector3 rightWrist)
        {
            var frame = new CanonicalPoseFrame();
            frame.Begin(1L, 0d, true);
            SetTracked(frame, CanonicalJointId.LeftShoulder, leftShoulder);
            SetTracked(frame, CanonicalJointId.LeftElbow, leftElbow);
            SetTracked(frame, CanonicalJointId.LeftWrist, leftWrist);
            SetTracked(frame, CanonicalJointId.RightShoulder, rightShoulder);
            SetTracked(frame, CanonicalJointId.RightElbow, rightElbow);
            SetTracked(frame, CanonicalJointId.RightWrist, rightWrist);
            frame.Complete();
            return frame;
        }

        private static CanonicalPoseFrame CreateReferenceCanonicalFrame(MotionCalibrationProfile profile)
        {
            var frame = new CanonicalPoseFrame();
            frame.Begin(1L, 0d, true);
            SetTracked(frame, CanonicalJointId.Pelvis, profile.neutralPelvisPosition);
            SetTracked(frame, CanonicalJointId.Chest, profile.neutralChestPosition);
            AddReferenceChain(
                frame,
                CanonicalJointId.LeftShoulder,
                CanonicalJointId.LeftElbow,
                CanonicalJointId.LeftWrist,
                profile.neutralLeftShoulderPosition,
                profile.leftArmGeometry);
            AddReferenceChain(
                frame,
                CanonicalJointId.RightShoulder,
                CanonicalJointId.RightElbow,
                CanonicalJointId.RightWrist,
                profile.neutralRightShoulderPosition,
                profile.rightArmGeometry);
            AddReferenceChain(
                frame,
                CanonicalJointId.LeftHip,
                CanonicalJointId.LeftKnee,
                CanonicalJointId.LeftAnkle,
                profile.neutralLeftHipPosition,
                profile.leftLegGeometry);
            AddReferenceChain(
                frame,
                CanonicalJointId.RightHip,
                CanonicalJointId.RightKnee,
                CanonicalJointId.RightAnkle,
                profile.neutralRightHipPosition,
                profile.rightLegGeometry);
            frame.Complete();
            return frame;
        }

        private static void AddReferenceChain(
            CanonicalPoseFrame frame,
            CanonicalJointId rootId,
            CanonicalJointId midId,
            CanonicalJointId tipId,
            Vector3 rootPosition,
            MotionCalibrationChainGeometry geometry)
        {
            var midPosition = rootPosition +
                geometry.referenceUpperDirection * geometry.upperLength;
            var tipPosition = midPosition +
                geometry.referenceLowerDirection * geometry.lowerLength;
            SetTracked(frame, rootId, rootPosition);
            SetTracked(frame, midId, midPosition);
            SetTracked(frame, tipId, tipPosition);
        }

        private static CanonicalPoseFrame CreateYawedReferenceCanonicalFrame(
            MotionCalibrationProfile profile,
            Quaternion yaw)
        {
            var frame = CreateReferenceCanonicalFrame(profile);
            for (var i = 0; i < CanonicalPoseFrame.JointCount; i++)
            {
                var joint = frame.GetJoint((CanonicalJointId)i);
                if (!joint.IsTracked || !joint.hasLocalPosition)
                {
                    continue;
                }

                joint.localPosition = yaw * joint.localPosition;
                frame.SetJoint(joint);
            }

            frame.Complete();
            return frame;
        }

        private static void SetTracked(CanonicalPoseFrame frame, CanonicalJointId id, Vector3 position)
        {
            frame.SetJoint(new CanonicalPoseJoint
            {
                id = id,
                tracking = CanonicalTrackingState.Tracked,
                confidence = 1f,
                localPosition = position,
                hasLocalPosition = true,
            });
        }

        private static CanonicalPoseJoint Untracked(CanonicalJointId id)
        {
            return new CanonicalPoseJoint
            {
                id = id,
                tracking = CanonicalTrackingState.Unavailable,
                confidence = 0f,
            };
        }

        private static MotionCalibrationProfile ValidProfile()
        {
            return new MotionCalibrationProfile
            {
                isValid = true,
                bodyReferenceValid = true,
                version = MotionCalibrationProfile.CurrentVersion,
                state = MotionCalibrationState.Ready,
                leftArmGeometry = Geometry(0.25f, 0.25f, Vector3.left, Vector3.left),
                rightArmGeometry = Geometry(0.5f, 0.5f, Vector3.right, Vector3.right),
                leftLegGeometry = Geometry(0.5f, 0.5f, Vector3.down, Vector3.down),
                rightLegGeometry = Geometry(0.5f, 0.5f, Vector3.down, Vector3.down),
                neutralBodyRight = Vector3.right,
                neutralBodyUp = Vector3.up,
                neutralBodyForward = Vector3.back,
            };
        }

        private static MotionCalibrationProfile RetargetProfile()
        {
            var profile = ValidProfile();
            profile.neutralPelvisPosition = Vector3.zero;
            profile.neutralChestPosition = new Vector3(0f, 0.40f, 0f);
            profile.neutralLeftShoulderPosition = new Vector3(-0.31f, 0.42f, 0f);
            profile.neutralRightShoulderPosition = new Vector3(0.31f, 0.42f, 0f);
            profile.neutralLeftHipPosition = new Vector3(-0.17f, -0.45f, 0f);
            profile.neutralRightHipPosition = new Vector3(0.17f, -0.45f, 0f);
            profile.leftArmGeometry = Geometry(
                0.36f,
                0.30f,
                Vector3.left,
                Vector3.left);
            profile.rightArmGeometry = Geometry(
                0.36f,
                0.30f,
                Vector3.right,
                Vector3.right);
            var lowerLegVector = new Vector3(0f, -0.12f, 0.10f);
            profile.leftLegGeometry = Geometry(
                0.45f,
                lowerLegVector.magnitude,
                Vector3.down,
                lowerLegVector.normalized);
            profile.rightLegGeometry = Geometry(
                0.45f,
                lowerLegVector.magnitude,
                Vector3.down,
                lowerLegVector.normalized);
            return profile;
        }

        private static MotionCalibrationChainGeometry Geometry(
            float upperLength,
            float lowerLength,
            Vector3 upperDirection,
            Vector3 lowerDirection)
        {
            return new MotionCalibrationChainGeometry
            {
                isValid = true,
                sampleCount = 3,
                upperLength = upperLength,
                lowerLength = lowerLength,
                reach = upperLength + lowerLength,
                referenceUpperDirection = upperDirection.normalized,
                referenceLowerDirection = lowerDirection.normalized,
            };
        }

        private static Transform CreateChild(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class RigContext
        {
            private readonly GameObject _rootObject;
            public readonly HumanoidRigBinding binding;
            public readonly HumanoidRetargeter retargeter;

            public RigContext(GameObject rootObject, HumanoidRigBinding binding, HumanoidRetargeter retargeter)
            {
                _rootObject = rootObject;
                this.binding = binding;
                this.retargeter = retargeter;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_rootObject);
            }
        }
    }
}
