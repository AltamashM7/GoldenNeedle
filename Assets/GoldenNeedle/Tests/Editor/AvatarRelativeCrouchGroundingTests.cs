using GoldenNeedle.Core.Motion.Locomotion;
using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Rotation;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class AvatarRelativeCrouchGroundingTests
    {
        [Test]
        public void NeutralCompressionProducesZeroRootOffset()
        {
            var context = CreateRigContext(1f);
            try
            {
                var mapper = Mapper();
                var sample = mapper.Update(
                    context.binding,
                    true,
                    StandingSample(),
                    VerticalSettings(),
                    0.1f);

                Assert.That(sample.hasAvatarLegScale, Is.True);
                Assert.That(sample.active, Is.False);
                Assert.That(sample.primaryRootOffsetY, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(sample.finalRootOffsetY, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(sample.residualGroundCorrectionY, Is.EqualTo(0f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void ShallowCompressionDescendsBeforeSemanticCrouch()
        {
            var context = CreateRigContext(1f);
            try
            {
                var mapper = Mapper();
                var shallow = mapper.Update(
                    context.binding,
                    true,
                    BendSample(0.10f, VerticalLocomotionState.Standing),
                    VerticalSettings(),
                    0.1f);

                Assert.That(shallow.active, Is.True);
                Assert.That(shallow.primaryRootOffsetY, Is.LessThan(0f));
                Assert.That(shallow.finalRootOffsetY, Is.LessThan(0f));
                Assert.That(Mathf.Abs(shallow.finalRootOffsetY),
                    Is.LessThan(shallow.avatarStandingLegScale * 0.20f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void DeeperCompressionProducesLargerButLegRelativeBoundedDescent()
        {
            var context = CreateRigContext(1f);
            try
            {
                var mapper = Mapper();
                var shallow = mapper.Update(
                    context.binding,
                    true,
                    BendSample(0.10f, VerticalLocomotionState.Standing),
                    VerticalSettings(),
                    0.1f);
                var deep = mapper.Update(
                    context.binding,
                    true,
                    BendSample(0.28f, VerticalLocomotionState.Crouch),
                    VerticalSettings(),
                    0.1f);

                Assert.That(deep.finalRootOffsetY, Is.LessThan(shallow.finalRootOffsetY));
                Assert.That(Mathf.Abs(deep.finalRootOffsetY),
                    Is.LessThanOrEqualTo(deep.avatarStandingLegScale * 0.65f + 0.0001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void SameCompressionScalesWithAvatarStandingLegSize()
        {
            var shortRig = CreateRigContext(0.55f);
            var tallRig = CreateRigContext(1.25f);
            try
            {
                var shortMapper = Mapper();
                var tallMapper = Mapper();
                var bend = BendSample(0.22f, VerticalLocomotionState.Crouch);
                var shortSample = shortMapper.Update(
                    shortRig.binding, true, bend, VerticalSettings(), 0.1f);
                var tallSample = tallMapper.Update(
                    tallRig.binding, true, bend, VerticalSettings(), 0.1f);

                Assert.That(tallSample.avatarStandingLegScale,
                    Is.GreaterThan(shortSample.avatarStandingLegScale * 2f));
                var scaleRatio = tallSample.avatarStandingLegScale /
                    shortSample.avatarStandingLegScale;
                var depthRatio = Mathf.Abs(tallSample.finalRootOffsetY) /
                    Mathf.Abs(shortSample.finalRootOffsetY);
                Assert.That(depthRatio, Is.EqualTo(scaleRatio).Within(0.01f));
            }
            finally
            {
                shortRig.Dispose();
                tallRig.Dispose();
            }
        }

        [Test]
        public void ChibiRigDoesNotReceiveTallRigAbsoluteCrouchDepth()
        {
            var chibi = CreateRigContext(0.40f);
            var tall = CreateRigContext(1.35f);
            try
            {
                var bend = BendSample(0.30f, VerticalLocomotionState.Crouch);
                var chibiSample = Mapper().Update(
                    chibi.binding, true, bend, VerticalSettings(), 0.1f);
                var tallSample = Mapper().Update(
                    tall.binding, true, bend, VerticalSettings(), 0.1f);

                Assert.That(Mathf.Abs(chibiSample.finalRootOffsetY),
                    Is.LessThan(Mathf.Abs(tallSample.finalRootOffsetY) * 0.40f));
            }
            finally
            {
                chibi.Dispose();
                tall.Dispose();
            }
        }

        [Test]
        public void SemanticCrouchStateDoesNotChangeContinuousMappingAtSameCompression()
        {
            var standingRig = CreateRigContext(1f);
            var crouchRig = CreateRigContext(1f);
            try
            {
                var standing = Mapper().Update(
                    standingRig.binding,
                    true,
                    BendSample(0.15f, VerticalLocomotionState.Standing),
                    VerticalSettings(),
                    0.1f);
                var crouch = Mapper().Update(
                    crouchRig.binding,
                    true,
                    BendSample(0.15f, VerticalLocomotionState.Crouch),
                    VerticalSettings(),
                    0.1f);

                Assert.That(standing.finalRootOffsetY,
                    Is.EqualTo(crouch.finalRootOffsetY).Within(0.0001f));
            }
            finally
            {
                standingRig.Dispose();
                crouchRig.Dispose();
            }
        }

        [Test]
        public void StandingRecoveryReturnsSmoothlyTowardOrigin()
        {
            var context = CreateRigContext(1f);
            try
            {
                var mapper = new AvatarRelativeCrouchGrounding(Settings());
                var slow = VerticalSettings(8f);
                var bent = mapper.Update(
                    context.binding,
                    true,
                    BendSample(0.24f, VerticalLocomotionState.Crouch),
                    slow,
                    0.1f);
                var recovering = mapper.Update(
                    context.binding,
                    true,
                    StandingSample(),
                    slow,
                    0.1f);
                var recovered = recovering;
                for (var i = 0; i < 12; i++)
                {
                    recovered = mapper.Update(
                        context.binding, true, StandingSample(), slow, 0.1f);
                }

                Assert.That(bent.finalRootOffsetY, Is.LessThan(0f));
                Assert.That(recovering.finalRootOffsetY, Is.GreaterThan(bent.finalRootOffsetY));
                Assert.That(recovering.finalRootOffsetY, Is.LessThanOrEqualTo(0f));
                Assert.That(Mathf.Abs(recovered.finalRootOffsetY), Is.LessThan(0.001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void GroundedMappingNeverReauthorsPhase4LegRotations()
        {
            var context = CreateRigContext(1f);
            try
            {
                var leftUpper = context.bones[(int)CanonicalBoneId.LeftUpperLeg];
                var leftLower = context.bones[(int)CanonicalBoneId.LeftLowerLeg];
                var rightUpper = context.bones[(int)CanonicalBoneId.RightUpperLeg];
                var rightLower = context.bones[(int)CanonicalBoneId.RightLowerLeg];
                leftUpper.localRotation = Quaternion.Euler(18f, 4f, 7f);
                leftLower.localRotation = Quaternion.Euler(31f, -3f, 2f);
                rightUpper.localRotation = Quaternion.Euler(20f, -4f, -6f);
                rightLower.localRotation = Quaternion.Euler(29f, 3f, -2f);
                var before = new[]
                {
                    leftUpper.localRotation,
                    leftLower.localRotation,
                    rightUpper.localRotation,
                    rightLower.localRotation,
                };

                var sample = Mapper().Update(
                    context.binding,
                    true,
                    BendSample(0.28f, VerticalLocomotionState.Crouch),
                    VerticalSettings(),
                    0.1f);

                Assert.That(sample.active, Is.True);
                Assert.That(Quaternion.Angle(before[0], leftUpper.localRotation), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(before[1], leftLower.localRotation), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(before[2], rightUpper.localRotation), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(before[3], rightLower.localRotation), Is.LessThan(0.0001f));
                Assert.That(sample.residualGroundCorrectionY, Is.EqualTo(0f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void SingleLegEvidenceReleasesGroundedMapping()
        {
            var context = CreateRigContext(1f);
            try
            {
                var mapper = Mapper();
                var unilateral = BendSample(0.20f, VerticalLocomotionState.Standing);
                unilateral.groundedBendActive = false;
                unilateral.footAsymmetry = 0.20f;
                var sample = mapper.Update(
                    context.binding,
                    true,
                    unilateral,
                    VerticalSettings(),
                    0.1f);

                Assert.That(sample.primaryRootOffsetY, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(sample.active, Is.False);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void JumpTakeoffClearsGroundedOffsetImmediately()
        {
            var context = CreateRigContext(1f);
            try
            {
                var mapper = Mapper();
                var bend = mapper.Update(
                    context.binding,
                    true,
                    BendSample(0.22f, VerticalLocomotionState.Crouch),
                    VerticalSettings(),
                    0.1f);
                var jump = BendSample(0f, VerticalLocomotionState.Jump);
                jump.jumpPhase = VerticalJumpPhase.Takeoff;
                jump.worldOffsetY = 0.25f;
                jump.groundedBendActive = false;
                jump.suppressPhysicalDepth = true;
                var released = mapper.Update(
                    context.binding,
                    true,
                    jump,
                    VerticalSettings(),
                    0.1f);

                Assert.That(bend.finalRootOffsetY, Is.LessThan(0f));
                Assert.That(released.active, Is.False);
                Assert.That(released.finalRootOffsetY, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(jump.worldOffsetY, Is.GreaterThan(0f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void StandingLegScaleIsStableAcrossLiveLegPoseChanges()
        {
            var context = CreateRigContext(1f);
            try
            {
                var mapper = Mapper();
                var standing = mapper.Update(
                    context.binding, true, StandingSample(), VerticalSettings(), 0.1f);
                var capturedScale = standing.avatarStandingLegScale;

                context.bones[(int)CanonicalBoneId.LeftUpperLeg].localRotation =
                    Quaternion.Euler(45f, 0f, 15f);
                context.bones[(int)CanonicalBoneId.RightUpperLeg].localRotation =
                    Quaternion.Euler(38f, 0f, -12f);
                var crouched = mapper.Update(
                    context.binding,
                    true,
                    BendSample(0.24f, VerticalLocomotionState.Crouch),
                    VerticalSettings(),
                    0.1f);

                Assert.That(crouched.avatarStandingLegScale,
                    Is.EqualTo(capturedScale).Within(0.0001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void CalibrationAndBindingResetClearScaleSession()
        {
            var context = CreateRigContext(1f);
            try
            {
                var mapper = Mapper();
                mapper.Update(context.binding, true, StandingSample(), VerticalSettings(), 0.1f);
                Assert.That(mapper.HasAvatarLegScale, Is.True);

                mapper.Reset();
                Assert.That(mapper.HasAvatarLegScale, Is.False);

                mapper.Update(context.binding, true, StandingSample(), VerticalSettings(), 0.1f);
                Assert.That(mapper.HasAvatarLegScale, Is.True);
                var previousVersion = context.binding.ReferencePoseVersion;
                context.binding.ConfigureExplicit(context.root, context.bones, context.tips);
                Assert.That(context.binding.ReferencePoseVersion, Is.GreaterThan(previousVersion));

                var refreshed = mapper.Update(
                    context.binding, true, StandingSample(), VerticalSettings(), 0.1f);
                Assert.That(refreshed.hasAvatarLegScale, Is.True);
                Assert.That(refreshed.finalRootOffsetY, Is.EqualTo(0f).Within(0.0001f));

                var invalid = mapper.Update(
                    context.binding, false, default, VerticalSettings(), 0.1f);
                Assert.That(invalid.hasAvatarLegScale, Is.False);
                Assert.That(mapper.HasAvatarLegScale, Is.False);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void LegRelativeMaximumDepthClampScalesWithAvatar()
        {
            var shortRig = CreateRigContext(0.5f);
            var tallRig = CreateRigContext(1.5f);
            try
            {
                var extreme = BendSample(2f, VerticalLocomotionState.Crouch);
                var shortSample = Mapper().Update(
                    shortRig.binding, true, extreme, VerticalSettings(), 0.1f);
                var tallSample = Mapper().Update(
                    tallRig.binding, true, extreme, VerticalSettings(), 0.1f);

                Assert.That(shortSample.depthClamped, Is.True);
                Assert.That(tallSample.depthClamped, Is.True);
                Assert.That(Mathf.Abs(shortSample.primaryRootOffsetY),
                    Is.EqualTo(shortSample.avatarStandingLegScale * 0.65f).Within(0.0001f));
                Assert.That(Mathf.Abs(tallSample.primaryRootOffsetY),
                    Is.EqualTo(tallSample.avatarStandingLegScale * 0.65f).Within(0.0001f));
            }
            finally
            {
                shortRig.Dispose();
                tallRig.Dispose();
            }
        }

        private static AvatarRelativeCrouchGrounding Mapper()
        {
            return new AvatarRelativeCrouchGrounding(Settings());
        }

        private static AvatarRelativeCrouchGroundingSettings Settings()
        {
            return new AvatarRelativeCrouchGroundingSettings
            {
                depthMultiplier = 1.20f,
                maximumDepthFraction = 0.65f,
            };
        }

        private static VerticalLocomotionSettings VerticalSettings(float response = 1000f)
        {
            return new VerticalLocomotionSettings
            {
                crouchReleaseThreshold = 0.09f,
                verticalResponse = response,
            };
        }

        private static VerticalLocomotionSample StandingSample()
        {
            return new VerticalLocomotionSample
            {
                isAvailable = true,
                referenceReady = true,
                state = VerticalLocomotionState.Standing,
                jumpPhase = VerticalJumpPhase.Grounded,
                crouchCompression = 0f,
                supportRise = 0f,
                footAsymmetry = 0f,
                worldOffsetY = 0f,
                groundedBendActive = false,
            };
        }

        private static VerticalLocomotionSample BendSample(
            float compression,
            VerticalLocomotionState state)
        {
            return new VerticalLocomotionSample
            {
                isAvailable = true,
                referenceReady = true,
                state = state,
                jumpPhase = state == VerticalLocomotionState.Jump
                    ? VerticalJumpPhase.Takeoff
                    : VerticalJumpPhase.Grounded,
                crouchCompression = compression,
                supportRise = 0f,
                footAsymmetry = 0f,
                worldOffsetY = state == VerticalLocomotionState.Jump ? 0.25f : -compression,
                groundedBendActive = state != VerticalLocomotionState.Jump && compression > 0f,
                suppressPhysicalDepth = state == VerticalLocomotionState.Jump,
            };
        }

        private static RigContext CreateRigContext(float legScale)
        {
            var rootObject = new GameObject("AvatarRelativeCrouchRig");
            var root = rootObject.transform;
            var bones = CreateBones(root, legScale, out var tips);
            var binding = rootObject.AddComponent<HumanoidRigBinding>();
            binding.ConfigureExplicit(root, bones, tips);
            return new RigContext(rootObject, root, binding, bones, tips);
        }

        private static Transform[] CreateBones(
            Transform root,
            float legScale,
            out Transform[] tips)
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

            var hipDrop = 0.45f * legScale;
            var upperLength = 0.47f * legScale;
            var lowerVertical = 0.12f * legScale;
            var footForward = 0.10f * legScale;
            var leftUpperLeg = CreateChild("LeftUpperLeg", hips, new Vector3(-0.17f, -hipDrop, 0f));
            var leftLowerLeg = CreateChild("LeftLowerLeg", leftUpperLeg, new Vector3(0f, -upperLength, 0f));
            var leftFoot = CreateChild("LeftFoot", leftLowerLeg, new Vector3(0f, -lowerVertical, footForward));
            var rightUpperLeg = CreateChild("RightUpperLeg", hips, new Vector3(0.17f, -hipDrop, 0f));
            var rightLowerLeg = CreateChild("RightLowerLeg", rightUpperLeg, new Vector3(0f, -upperLength, 0f));
            var rightFoot = CreateChild("RightFoot", rightLowerLeg, new Vector3(0f, -lowerVertical, footForward));

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

        private static Transform CreateChild(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private sealed class RigContext
        {
            private readonly GameObject _rootObject;
            public readonly Transform root;
            public readonly HumanoidRigBinding binding;
            public readonly Transform[] bones;
            public readonly Transform[] tips;

            public RigContext(
                GameObject rootObject,
                Transform root,
                HumanoidRigBinding binding,
                Transform[] bones,
                Transform[] tips)
            {
                _rootObject = rootObject;
                this.root = root;
                this.binding = binding;
                this.bones = bones;
                this.tips = tips;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_rootObject);
            }
        }
    }
}
