using GoldenNeedle.Core.Motion.Locomotion;
using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Rotation;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class GroundedFootConstraintTests
    {
        [Test]
        public void NeutralStandingCapturesStablePerFootPlane()
        {
            var context = CreateRigContext();
            try
            {
                var constraint = new GroundedFootConstraint();
                var standing = StandingSample();
                var sample = constraint.Update(
                    context.binding,
                    true,
                    standing,
                    Settings(),
                    true);

                Assert.That(sample.hasStandingReference, Is.True);
                Assert.That(sample.active, Is.False);
                Assert.That(sample.leftStandingY,
                    Is.EqualTo(context.leftFoot.position.y).Within(0.0001f));
                Assert.That(sample.rightStandingY,
                    Is.EqualTo(context.rightFoot.position.y).Within(0.0001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void ShallowRootDescentResolvesBothFeetBackToStandingPlane()
        {
            var context = CreateRigContext();
            try
            {
                var constraint = ReadyConstraint(context, out var leftY, out var rightY);
                context.root.position += Vector3.down * 0.08f;

                var sample = constraint.Update(
                    context.binding,
                    true,
                    BendSample(-0.08f, true),
                    Settings(),
                    true);

                Assert.That(sample.active, Is.True);
                Assert.That(sample.leftSolved, Is.True);
                Assert.That(sample.rightSolved, Is.True);
                Assert.That(context.leftFoot.position.y,
                    Is.EqualTo(leftY).Within(0.001f));
                Assert.That(context.rightFoot.position.y,
                    Is.EqualTo(rightY).Within(0.001f));
                Assert.That(context.root.position.y, Is.EqualTo(-0.08f).Within(0.0001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void DeepCrouchKeepsFeetOnPlaneWhileRootRemainsLower()
        {
            var context = CreateRigContext();
            try
            {
                var constraint = ReadyConstraint(context, out var leftY, out var rightY);
                context.root.position += Vector3.down * 0.24f;

                var sample = constraint.Update(
                    context.binding,
                    true,
                    BendSample(-0.24f, true, VerticalLocomotionState.Crouch),
                    Settings(),
                    true);

                Assert.That(sample.active, Is.True);
                Assert.That(context.root.position.y, Is.EqualTo(-0.24f).Within(0.0001f));
                Assert.That(context.leftFoot.position.y,
                    Is.EqualTo(leftY).Within(0.001f));
                Assert.That(context.rightFoot.position.y,
                    Is.EqualTo(rightY).Within(0.001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void RecoverySequenceKeepsPlaneAndBendDirectionStable()
        {
            var context = CreateRigContext();
            try
            {
                var constraint = ReadyConstraint(context, out var leftY, out var rightY);

                context.root.position = new Vector3(0f, -0.08f, 0f);
                constraint.Update(context.binding, true,
                    BendSample(-0.08f, true), Settings(), true);
                var shallowBend = ProjectedBend(
                    context.binding,
                    CanonicalKinematicChainId.LeftLeg,
                    leftY);

                context.root.position = new Vector3(0f, -0.22f, 0f);
                constraint.Update(context.binding, true,
                    BendSample(-0.22f, true, VerticalLocomotionState.Crouch),
                    Settings(), true);
                var deepBend = ProjectedBend(
                    context.binding,
                    CanonicalKinematicChainId.LeftLeg,
                    leftY);

                context.root.position = new Vector3(0f, -0.04f, 0f);
                var recovery = constraint.Update(context.binding, true,
                    BendSample(-0.04f, false), Settings(), true);
                var recoveryBend = ProjectedBend(
                    context.binding,
                    CanonicalKinematicChainId.LeftLeg,
                    leftY);

                Assert.That(recovery.active, Is.True);
                Assert.That(context.leftFoot.position.y,
                    Is.EqualTo(leftY).Within(0.001f));
                Assert.That(context.rightFoot.position.y,
                    Is.EqualTo(rightY).Within(0.001f));
                Assert.That(IsFinite(shallowBend), Is.True);
                Assert.That(IsFinite(deepBend), Is.True);
                Assert.That(IsFinite(recoveryBend), Is.True);
                Assert.That(Vector3.Dot(shallowBend, deepBend), Is.GreaterThan(0f));
                Assert.That(Vector3.Dot(deepBend, recoveryBend), Is.GreaterThan(0f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void GroundedTargetPreservesCurrentXZAndNeverDropsBelowPlane()
        {
            var current = new Vector3(1.25f, -0.40f, -2.5f);
            var target = GroundedFootConstraint.BuildGroundedTarget(current, 0.15f);

            Assert.That(target.x, Is.EqualTo(current.x));
            Assert.That(target.z, Is.EqualTo(current.z));
            Assert.That(target.y, Is.EqualTo(0.15f));
            Assert.That(target.y, Is.GreaterThan(current.y));
        }

        [Test]
        public void JumpAndTakeoffEvidenceReleaseConstraintImmediately()
        {
            var context = CreateRigContext();
            try
            {
                var constraint = ReadyConstraint(context, out _, out _);
                context.root.position += Vector3.down * 0.10f;
                var preJumpLeft = context.leftFoot.position;

                var jump = BendSample(0.12f, false, VerticalLocomotionState.Jump);
                jump.jumpPhase = VerticalJumpPhase.Takeoff;
                var jumpSample = constraint.Update(
                    context.binding, true, jump, Settings(), true);

                Assert.That(jumpSample.active, Is.False);
                Assert.That(Vector3.Distance(context.leftFoot.position, preJumpLeft),
                    Is.LessThan(0.0001f));

                var takeoff = BendSample(-0.04f, false);
                takeoff.supportRise = 0.20f;
                var beforeTakeoffGate = context.leftFoot.position;
                var takeoffSample = constraint.Update(
                    context.binding, true, takeoff, Settings(), true);

                Assert.That(takeoffSample.active, Is.False);
                Assert.That(Vector3.Distance(context.leftFoot.position, beforeTakeoffGate),
                    Is.LessThan(0.0001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void UnilateralSwingEvidenceDoesNotForceRaisedFootToPlane()
        {
            var context = CreateRigContext();
            try
            {
                var constraint = ReadyConstraint(context, out _, out _);
                var leftUpperLeg = context.bones[(int)CanonicalBoneId.LeftUpperLeg];
                leftUpperLeg.rotation =
                    Quaternion.AngleAxis(18f, Vector3.forward) * leftUpperLeg.rotation;
                var raisedBefore = context.leftFoot.position;

                var unilateral = BendSample(-0.08f, true);
                unilateral.footAsymmetry = 0.20f;
                var sample = constraint.Update(
                    context.binding, true, unilateral, Settings(), true);

                Assert.That(sample.active, Is.False);
                Assert.That(Vector3.Distance(context.leftFoot.position, raisedBefore),
                    Is.LessThan(0.0001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void MissingLegChainFailsSafelyWithoutMovingRoot()
        {
            var context = CreateRigContext(false);
            try
            {
                var constraint = new GroundedFootConstraint();
                var rootBefore = context.root.position;
                var sample = constraint.Update(
                    context.binding,
                    true,
                    StandingSample(),
                    Settings(),
                    true);

                Assert.That(context.binding.IsBound, Is.True);
                Assert.That(context.binding.IsChainAvailable(
                    CanonicalKinematicChainId.RightLeg), Is.False);
                Assert.That(sample.hasStandingReference, Is.False);
                Assert.That(sample.active, Is.False);
                Assert.That(context.root.position, Is.EqualTo(rootBefore));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void ResetAndBindingVersionChangeClearStaleStandingReference()
        {
            var context = CreateRigContext();
            try
            {
                var constraint = ReadyConstraint(context, out _, out _);
                Assert.That(constraint.HasStandingReference, Is.True);

                constraint.Reset();
                Assert.That(constraint.HasStandingReference, Is.False);

                constraint.Update(context.binding, true,
                    StandingSample(), Settings(), true);
                Assert.That(constraint.HasStandingReference, Is.True);

                var previousVersion = context.binding.ReferencePoseVersion;
                context.binding.ConfigureExplicit(context.root, context.bones, context.tips);
                Assert.That(context.binding.ReferencePoseVersion,
                    Is.GreaterThan(previousVersion));

                var crouched = BendSample(-0.10f, true);
                var sample = constraint.Update(
                    context.binding, true, crouched, Settings(), true);
                Assert.That(sample.hasStandingReference, Is.False);
                Assert.That(sample.active, Is.False);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void CalibrationLossClearsReferenceWithoutChangingRoot()
        {
            var context = CreateRigContext();
            try
            {
                var constraint = ReadyConstraint(context, out _, out _);
                context.root.position = new Vector3(0f, -0.12f, 0f);
                var rootBefore = context.root.position;

                var sample = constraint.Update(
                    context.binding,
                    false,
                    default,
                    Settings(),
                    true);

                Assert.That(sample.hasStandingReference, Is.False);
                Assert.That(sample.active, Is.False);
                Assert.That(context.root.position, Is.EqualTo(rootBefore));
            }
            finally
            {
                context.Dispose();
            }
        }

        private static GroundedFootConstraint ReadyConstraint(
            RigContext context,
            out float leftStandingY,
            out float rightStandingY)
        {
            var constraint = new GroundedFootConstraint();
            var sample = constraint.Update(
                context.binding,
                true,
                StandingSample(),
                Settings(),
                true);
            Assert.That(sample.hasStandingReference, Is.True);
            leftStandingY = sample.leftStandingY;
            rightStandingY = sample.rightStandingY;
            return constraint;
        }

        private static VerticalLocomotionSettings Settings()
        {
            return new VerticalLocomotionSettings
            {
                minimumJointConfidence = 0.20f,
                maximumJumpFootAsymmetry = 0.08f,
                groundedSupportTolerance = 0.06f,
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
                supportRise = 0f,
                footAsymmetry = 0f,
                worldOffsetY = 0f,
                groundedBendActive = false,
            };
        }

        private static VerticalLocomotionSample BendSample(
            float worldOffsetY,
            bool groundedBendActive,
            VerticalLocomotionState state = VerticalLocomotionState.Standing)
        {
            return new VerticalLocomotionSample
            {
                isAvailable = true,
                referenceReady = true,
                state = state,
                jumpPhase = state == VerticalLocomotionState.Jump
                    ? VerticalJumpPhase.Takeoff
                    : VerticalJumpPhase.Grounded,
                crouchCompression = Mathf.Max(0f, -worldOffsetY),
                supportRise = 0f,
                footAsymmetry = 0f,
                worldOffsetY = worldOffsetY,
                groundedBendActive = groundedBendActive,
                suppressPhysicalDepth = state == VerticalLocomotionState.Jump,
            };
        }

        private static Vector3 ProjectedBend(
            HumanoidRigBinding binding,
            CanonicalKinematicChainId chain,
            float targetY)
        {
            var root = binding.GetChainRoot(chain);
            var mid = binding.GetChainMid(chain);
            var tip = binding.GetChainTip(chain);
            var target = GroundedFootConstraint.BuildGroundedTarget(tip.position, targetY);
            var primary = (target - root.position).normalized;
            var bend = mid.position - root.position;
            bend -= primary * Vector3.Dot(bend, primary);
            return bend.sqrMagnitude > 0.00000001f ? bend.normalized : Vector3.zero;
        }

        private static RigContext CreateRigContext(bool includeRightFoot = true)
        {
            var rootObject = new GameObject("GroundedFootConstraintRig");
            var root = rootObject.transform;
            var bones = CreateBones(root, includeRightFoot, out var tips,
                out var leftFoot, out var rightFoot);
            var binding = rootObject.AddComponent<HumanoidRigBinding>();
            binding.ConfigureExplicit(root, bones, tips);
            return new RigContext(
                rootObject,
                root,
                binding,
                bones,
                tips,
                leftFoot,
                rightFoot);
        }

        private static Transform[] CreateBones(
            Transform root,
            bool includeRightFoot,
            out Transform[] tips,
            out Transform leftFoot,
            out Transform rightFoot)
        {
            var hips = CreateChild("Hips", root, new Vector3(0f, 1f, 0f));
            var spine = CreateChild("Spine", hips, new Vector3(0f, 0.2f, 0f));
            var chest = CreateChild("Chest", spine, new Vector3(0f, 0.2f, 0f));
            var leftUpperArm = CreateChild("LeftUpperArm", chest,
                new Vector3(-0.31f, 0.02f, 0f));
            var leftLowerArm = CreateChild("LeftLowerArm", leftUpperArm,
                new Vector3(-0.36f, 0f, 0f));
            var leftHand = CreateChild("LeftHand", leftLowerArm,
                new Vector3(-0.30f, 0f, 0f));
            var rightUpperArm = CreateChild("RightUpperArm", chest,
                new Vector3(0.31f, 0.02f, 0f));
            var rightLowerArm = CreateChild("RightLowerArm", rightUpperArm,
                new Vector3(0.36f, 0f, 0f));
            var rightHand = CreateChild("RightHand", rightLowerArm,
                new Vector3(0.30f, 0f, 0f));
            var leftUpperLeg = CreateChild("LeftUpperLeg", hips,
                new Vector3(-0.17f, -0.45f, 0f));
            var leftLowerLeg = CreateChild("LeftLowerLeg", leftUpperLeg,
                new Vector3(0f, -0.47f, 0f));
            leftFoot = CreateChild("LeftFoot", leftLowerLeg,
                new Vector3(0f, -0.12f, 0.10f));
            var rightUpperLeg = CreateChild("RightUpperLeg", hips,
                new Vector3(0.17f, -0.45f, 0f));
            var rightLowerLeg = CreateChild("RightLowerLeg", rightUpperLeg,
                new Vector3(0f, -0.47f, 0f));
            rightFoot = includeRightFoot
                ? CreateChild("RightFoot", rightLowerLeg,
                    new Vector3(0f, -0.12f, 0.10f))
                : null;

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

        private static Transform CreateChild(
            string name,
            Transform parent,
            Vector3 localPosition)
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
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private sealed class RigContext
        {
            private readonly GameObject _rootObject;
            public readonly Transform root;
            public readonly HumanoidRigBinding binding;
            public readonly Transform[] bones;
            public readonly Transform[] tips;
            public readonly Transform leftFoot;
            public readonly Transform rightFoot;

            public RigContext(
                GameObject rootObject,
                Transform root,
                HumanoidRigBinding binding,
                Transform[] bones,
                Transform[] tips,
                Transform leftFoot,
                Transform rightFoot)
            {
                _rootObject = rootObject;
                this.root = root;
                this.binding = binding;
                this.bones = bones;
                this.tips = tips;
                this.leftFoot = leftFoot;
                this.rightFoot = rightFoot;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_rootObject);
            }
        }
    }
}