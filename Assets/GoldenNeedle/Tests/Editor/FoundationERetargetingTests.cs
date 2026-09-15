using System;
using System.Reflection;
using GoldenNeedle.Core.Motion.Hands;
using GoldenNeedle.Core.Motion.Rich;
using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Rotation;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    internal sealed class FoundationETestDetailSource : MonoBehaviour, IExplicitHumanoidDetailRigSource
    {
        private readonly Transform[] _bones = new Transform[HumanoidRigDetailCapabilities.FingerBoneCount];

        public void Set(HumanoidFingerBoneId id, Transform bone)
        {
            _bones[(int)id] = bone;
        }

        public void Set(
            HumanoidFingerBoneId a, Transform aTransform,
            HumanoidFingerBoneId b, Transform bTransform,
            HumanoidFingerBoneId c, Transform cTransform)
        {
            _bones[(int)a] = aTransform;
            _bones[(int)b] = bTransform;
            _bones[(int)c] = cTransform;
        }

        public bool TryGetDetailFingerBone(HumanoidFingerBoneId id, out Transform bone)
        {
            bone = _bones[(int)id];
            return bone != null;
        }
    }

    public sealed class FoundationERetargetingTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void ProperAndReflectedAxisMapsPreserveAndInvertTwistSign()
        {
            var proper = new CanonicalToAvatarAxisMap(
                new SignedAxisBasis(Vector3.right, Vector3.up, Vector3.forward, 1f),
                new SignedAxisBasis(Vector3.right, Vector3.up, Vector3.forward, 1f));
            var reflected = new CanonicalToAvatarAxisMap(
                new SignedAxisBasis(Vector3.right, Vector3.up, -Vector3.forward, -1f),
                new SignedAxisBasis(Vector3.right, Vector3.up, Vector3.forward, 1f));

            Assert.That(FoundationERetargetMath.MapSignedAxialDegrees(31f, proper), Is.EqualTo(31f).Within(0.0001f));
            Assert.That(FoundationERetargetMath.MapSignedAxialDegrees(31f, reflected), Is.EqualTo(-31f).Within(0.0001f));
        }

        [Test]
        public void RelativeAxialMeasurementUsesFirstBasisAsZeroReference()
        {
            var reference = Basis(Vector3.right, Vector3.up, Vector3.forward);
            var angle = 42f;
            var current = Basis(
                Vector3.right,
                Quaternion.AngleAxis(angle, Vector3.right) * Vector3.up,
                Quaternion.AngleAxis(angle, Vector3.right) * Vector3.forward);

            Assert.That(FoundationERetargetMath.TryMeasureRelativeAxialDegrees(in reference, in current, out var measured), Is.True);
            Assert.That(measured, Is.EqualTo(angle).Within(0.01f));
        }

        [Test]
        public void ParentAxialTwistCompensatesChildAndPreservesDownstreamEndpoints()
        {
            var root = new GameObject("E-root");
            var child = new GameObject("E-child");
            var tip = new GameObject("E-tip");
            try
            {
                child.transform.SetParent(root.transform, false);
                tip.transform.SetParent(child.transform, false);
                child.transform.localPosition = Vector3.right;
                tip.transform.localPosition = new Vector3(0.6f, 0.4f, -0.2f);
                var childBefore = child.transform.position;
                var tipBefore = tip.transform.position;

                Assert.That(
                    FoundationERetargetMath.TryApplyAxialTwistPreservingDownstream(
                        root.transform, child.transform, 67f, out var residual),
                    Is.True);

                Assert.That(residual, Is.LessThan(0.000001f));
                Assert.That(Vector3.Distance(childBefore, child.transform.position), Is.LessThan(0.000001f));
                Assert.That(Vector3.Distance(tipBefore, tip.transform.position), Is.LessThan(0.000001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RepeatedIdenticalPalmInputBuildsOneAbsoluteTargetWithoutAccumulation()
        {
            var referenceSource = Basis(Vector3.right, Vector3.up, Vector3.forward);
            var sourceDelta = Quaternion.AngleAxis(27f, Vector3.forward);
            var currentSource = Basis(
                sourceDelta * Vector3.right,
                sourceDelta * Vector3.up,
                sourceDelta * Vector3.forward);
            var parent = Quaternion.Euler(13f, -22f, 7f);
            var baseline = Quaternion.Euler(-4f, 11f, 3f);
            var firstTarget = Quaternion.identity;

            for (var i = 0; i < 100; i++)
            {
                Assert.That(FoundationERetargetMath.TryBuildAbsolutePalmLocalRotation(
                    in referenceSource,
                    in currentSource,
                    Vector3.right,
                    Vector3.up,
                    Vector3.forward,
                    parent,
                    baseline,
                    out var target,
                    out _,
                    out _,
                    out _), Is.True);
                if (i == 0) firstTarget = target;
                Assert.That(Quaternion.Angle(firstTarget, target), Is.LessThan(0.0001f));
            }

            Assert.That(Quaternion.Angle(baseline, firstTarget), Is.GreaterThan(1f));
        }

        [Test]
        public void AbsolutePalmTargetIsParentRelativeAndDoesNotCounterRotateParentMotion()
        {
            var referenceSource = Basis(Vector3.right, Vector3.up, Vector3.forward);
            var sourceDelta = Quaternion.AngleAxis(19f, Vector3.forward);
            var currentSource = Basis(
                sourceDelta * Vector3.right,
                sourceDelta * Vector3.up,
                sourceDelta * Vector3.forward);
            var baseline = Quaternion.Euler(2f, -6f, 9f);
            var parentA = Quaternion.Euler(0f, 10f, 0f);
            var parentB = Quaternion.Euler(17f, 55f, -8f);

            Assert.That(FoundationERetargetMath.TryBuildAbsolutePalmLocalRotation(
                in referenceSource, in currentSource,
                Vector3.right, Vector3.up, Vector3.forward,
                parentA, baseline,
                out var localA, out _, out _, out _), Is.True);
            Assert.That(FoundationERetargetMath.TryBuildAbsolutePalmLocalRotation(
                in referenceSource, in currentSource,
                Vector3.right, Vector3.up, Vector3.forward,
                parentB, baseline,
                out var localB, out _, out _, out _), Is.True);

            Assert.That(Quaternion.Angle(localA, localB), Is.LessThan(0.001f));
            var worldA = parentA * localA;
            var worldB = parentB * localB;
            Assert.That(Quaternion.Angle(worldA, worldB), Is.GreaterThan(10f));
        }

        [Test]
        public void PartialDetailCapabilitiesCacheOnlyAvailableSegmentsAndNeverMutateAuthoredGeometry()
        {
            var owner = new GameObject("E-detail-source");
            var a = new GameObject("finger-a");
            var b = new GameObject("finger-b");
            var c = new GameObject("finger-c");
            try
            {
                a.transform.SetParent(owner.transform, false);
                b.transform.SetParent(a.transform, false);
                c.transform.SetParent(b.transform, false);
                a.transform.localPosition = new Vector3(.1f, .2f, .3f);
                b.transform.localPosition = new Vector3(.04f, 0f, 0f);
                c.transform.localPosition = new Vector3(.03f, 0f, 0f);
                a.transform.localScale = new Vector3(1f, .9f, 1.1f);

                var source = owner.AddComponent<FoundationETestDetailSource>();
                source.Set(
                    HumanoidFingerBoneId.LeftIndexProximal, a.transform,
                    HumanoidFingerBoneId.LeftIndexIntermediate, b.transform,
                    HumanoidFingerBoneId.LeftIndexDistal, c.transform);

                var capabilities = new HumanoidRigDetailCapabilities();
                capabilities.BindExplicit(source);
                Assert.That(capabilities.AvailableFingerBoneCount, Is.EqualTo(3));

                a.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
                b.transform.localRotation = Quaternion.Euler(-15f, 5f, 9f);
                Assert.That(capabilities.AuthoredPositionsAndScalesIntact(), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void OptionalDetailCountDoesNotParticipateInLegacyBindingValidityOrCount()
        {
            using var rig = PalmRig.Create();
            Assert.That(rig.Binding.IsBound, Is.True);
            Assert.That(rig.Binding.BoundBoneCount, Is.EqualTo(CanonicalRotationFrame.BoneCount));
            Assert.That(rig.Binding.OptionalDetailBoneCount, Is.EqualTo(6));
            Assert.That(rig.Binding.GetChainTipBindLocalRotation(CanonicalKinematicChainId.LeftArm),
                Is.EqualTo(rig.LeftBaseline));
        }

        [Test]
        public void RepeatedIdenticalPalmRuntimeInputConvergesWithoutFrameByFrameDrift()
        {
            using var rig = PalmRig.Create();
            SetFreshPalm(rig.Hands, CanonicalHandSide.Left, Basis(Vector3.right, Vector3.up, Vector3.forward), 1000);
            rig.Detail.ApplyPostSolveDetail(rig.Binding, Dt);

            var delta = Quaternion.AngleAxis(24f, Vector3.forward);
            SetPalmBasis(rig.Hands, CanonicalHandSide.Left,
                Basis(delta * Vector3.right, delta * Vector3.up, delta * Vector3.forward));
            Advance(rig, 160);
            var settled = rig.LeftHand.localRotation;
            Assert.That(Quaternion.Angle(rig.LeftBaseline, settled), Is.GreaterThan(1f));

            Advance(rig, 160);
            Assert.That(Quaternion.Angle(settled, rig.LeftHand.localRotation), Is.LessThan(0.02f));
        }

        [Test]
        public void StaleHandSmoothlyReturnsPalmContributionToPhase4Baseline()
        {
            using var rig = PalmRig.Create();
            DrivePalm(rig, CanonicalHandSide.Left, 28f);
            Assert.That(Quaternion.Angle(rig.LeftBaseline, rig.LeftHand.localRotation), Is.GreaterThan(1f));

            rig.Hands.HandFrame.Left.MarkUnavailable(true);
            Advance(rig, 120);
            Assert.That(Quaternion.Angle(rig.LeftBaseline, rig.LeftHand.localRotation), Is.LessThan(0.02f));
        }

        [Test]
        public void MasterDisableImmediatelyRestoresPalmBaseline()
        {
            using var rig = PalmRig.Create();
            DrivePalm(rig, CanonicalHandSide.Left, 31f);
            Assert.That(Quaternion.Angle(rig.LeftBaseline, rig.LeftHand.localRotation), Is.GreaterThan(1f));

            SetPrivateBool(rig.Detail, "enableFoundationE", false);
            rig.Detail.ApplyPostSolveDetail(rig.Binding, Dt);
            Assert.That(Quaternion.Angle(rig.LeftBaseline, rig.LeftHand.localRotation), Is.LessThan(0.0001f));
        }

        [Test]
        public void PalmCategoryDisableReturnsPriorContributionWithoutDisablingFoundationE()
        {
            using var rig = PalmRig.Create();
            DrivePalm(rig, CanonicalHandSide.Left, 26f);
            Assert.That(Quaternion.Angle(rig.LeftBaseline, rig.LeftHand.localRotation), Is.GreaterThan(1f));

            SetPrivateBool(rig.Detail, "enablePalmOrientation", false);
            Advance(rig, 120);
            Assert.That(rig.Detail.IsPostSolveDetailEnabled, Is.True);
            Assert.That(Quaternion.Angle(rig.LeftBaseline, rig.LeftHand.localRotation), Is.LessThan(0.02f));
        }

        [Test]
        public void ParentMotionPreservesRelativePalmContributionWithoutDrift()
        {
            using var rig = PalmRig.Create();
            DrivePalm(rig, CanonicalHandSide.Left, 21f);
            var localBefore = rig.LeftHand.localRotation;
            var worldBefore = rig.LeftHand.rotation;

            rig.LeftLowerArm.localRotation = Quaternion.Euler(12f, 42f, -9f) * rig.LeftLowerArm.localRotation;
            Advance(rig, 120);

            Assert.That(Quaternion.Angle(localBefore, rig.LeftHand.localRotation), Is.LessThan(0.03f));
            Assert.That(Quaternion.Angle(worldBefore, rig.LeftHand.rotation), Is.GreaterThan(10f));
        }

        [Test]
        public void ReacquisitionEstablishesZeroDeltaBeforeNewRelativePalmMotion()
        {
            using var rig = PalmRig.Create();
            DrivePalm(rig, CanonicalHandSide.Left, 27f);
            rig.Hands.HandFrame.Left.MarkUnavailable(true);
            Advance(rig, 3);
            var beforeReacquire = rig.LeftHand.localRotation;

            var reacquireDelta = Quaternion.AngleAxis(-48f, Vector3.forward);
            SetFreshPalm(rig.Hands, CanonicalHandSide.Left,
                Basis(reacquireDelta * Vector3.right, reacquireDelta * Vector3.up, reacquireDelta * Vector3.forward),
                2000);
            rig.Detail.ApplyPostSolveDetail(rig.Binding, Dt);
            var afterReacquire = rig.LeftHand.localRotation;
            Assert.That(Quaternion.Angle(beforeReacquire, afterReacquire), Is.LessThan(8f));
            Assert.That(
                Quaternion.Angle(afterReacquire, rig.LeftBaseline),
                Is.LessThan(Quaternion.Angle(beforeReacquire, rig.LeftBaseline)));

            var nextDelta = Quaternion.AngleAxis(-33f, Vector3.forward);
            SetPalmBasis(rig.Hands, CanonicalHandSide.Left,
                Basis(nextDelta * Vector3.right, nextDelta * Vector3.up, nextDelta * Vector3.forward));
            Advance(rig, 120);
            Assert.That(Quaternion.Angle(rig.LeftBaseline, rig.LeftHand.localRotation), Is.GreaterThan(1f));
        }

        [Test]
        public void LeftAndRightPalmFallbackRemainIndependent()
        {
            using var rig = PalmRig.Create();
            SetFreshPalm(rig.Hands, CanonicalHandSide.Left, Basis(Vector3.right, Vector3.up, Vector3.forward), 1000);
            SetFreshPalm(rig.Hands, CanonicalHandSide.Right, Basis(Vector3.right, Vector3.up, Vector3.forward), 1000);
            rig.Detail.ApplyPostSolveDetail(rig.Binding, Dt);

            var leftDelta = Quaternion.AngleAxis(23f, Vector3.forward);
            var rightDelta = Quaternion.AngleAxis(-29f, Vector3.forward);
            SetPalmBasis(rig.Hands, CanonicalHandSide.Left,
                Basis(leftDelta * Vector3.right, leftDelta * Vector3.up, leftDelta * Vector3.forward));
            SetPalmBasis(rig.Hands, CanonicalHandSide.Right,
                Basis(rightDelta * Vector3.right, rightDelta * Vector3.up, rightDelta * Vector3.forward));
            Advance(rig, 160);
            var rightBefore = rig.RightHand.localRotation;

            rig.Hands.HandFrame.Left.MarkUnavailable(true);
            Advance(rig, 120);

            Assert.That(Quaternion.Angle(rig.LeftBaseline, rig.LeftHand.localRotation), Is.LessThan(0.02f));
            Assert.That(Quaternion.Angle(rig.RightBaseline, rig.RightHand.localRotation), Is.GreaterThan(1f));
            Assert.That(Quaternion.Angle(rightBefore, rig.RightHand.localRotation), Is.LessThan(0.03f));
        }

        private static void DrivePalm(PalmRig rig, CanonicalHandSide side, float degrees)
        {
            SetFreshPalm(rig.Hands, side, Basis(Vector3.right, Vector3.up, Vector3.forward), 1000);
            rig.Detail.ApplyPostSolveDetail(rig.Binding, Dt);
            var delta = Quaternion.AngleAxis(degrees, Vector3.forward);
            SetPalmBasis(rig.Hands, side,
                Basis(delta * Vector3.right, delta * Vector3.up, delta * Vector3.forward));
            Advance(rig, 160);
        }

        private static void Advance(PalmRig rig, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                rig.Detail.ApplyPostSolveDetail(rig.Binding, Dt);
            }
        }

        private static void SetFreshPalm(
            HandMotionRuntime runtime,
            CanonicalHandSide side,
            CanonicalAnatomicalBasis basis,
            long timestamp)
        {
            var hand = runtime.HandFrame.GetHand(side);
            var received = timestamp / 1000d;
            hand.Begin(side, timestamp, received, side, 1f, HandAssociationMode.HandednessFallback, 1f);
            hand.SetDerived(basis, default, default, default, default, default, HandShapeState.Intermediate, default);
            var settings = HandFreshnessSettings.CreateDefault();
            hand.EvaluateFreshness(received + 0.01d, timestamp, settings);
            Assert.That(hand.isFresh, Is.True);
        }

        private static void SetPalmBasis(
            HandMotionRuntime runtime,
            CanonicalHandSide side,
            CanonicalAnatomicalBasis basis)
        {
            var hand = runtime.HandFrame.GetHand(side);
            hand.SetDerived(basis, default, default, default, default, default, HandShapeState.Intermediate, default);
            Assert.That(hand.isFresh, Is.True);
        }

        private static void SetPrivateBool(object target, string fieldName, bool value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static CanonicalAnatomicalBasis Basis(Vector3 primary, Vector3 secondary, Vector3 third)
        {
            return new CanonicalAnatomicalBasis
            {
                primaryAxis = primary.normalized,
                secondaryAxis = secondary.normalized,
                thirdAxis = third.normalized,
                determinant = Vector3.Dot(Vector3.Cross(primary.normalized, secondary.normalized), third.normalized),
                handedness = CanonicalAnatomicalHandedness.RightHanded,
                isValid = true,
            };
        }

        private sealed class PalmRig : IDisposable
        {
            public GameObject Owner { get; private set; }
            public HumanoidRigBinding Binding { get; private set; }
            public HandMotionRuntime Hands { get; private set; }
            public RichHumanoidDetailRetargeter Detail { get; private set; }
            public Transform LeftLowerArm { get; private set; }
            public Transform LeftHand { get; private set; }
            public Transform RightHand { get; private set; }
            public Quaternion LeftBaseline { get; private set; }
            public Quaternion RightBaseline { get; private set; }

            public static PalmRig Create()
            {
                var rig = new PalmRig { Owner = new GameObject("Foundation-E-palm-rig") };
                var source = rig.Owner.AddComponent<FoundationETestDetailSource>();
                var bones = new Transform[CanonicalRotationFrame.BoneCount];

                var hips = Child(rig.Owner.transform, "hips", new Vector3(0f, 1f, 0f));
                var chest = Child(rig.Owner.transform, "chest", new Vector3(0f, 1.55f, 0f));
                var leftUpperArm = Child(chest, "left-upper-arm", new Vector3(-0.28f, 0.08f, 0f));
                var leftLowerArm = Child(leftUpperArm, "left-lower-arm", new Vector3(-0.42f, 0f, 0f));
                var leftHand = Child(leftLowerArm, "left-hand", new Vector3(-0.28f, 0f, 0f));
                var rightUpperArm = Child(chest, "right-upper-arm", new Vector3(0.28f, 0.08f, 0f));
                var rightLowerArm = Child(rightUpperArm, "right-lower-arm", new Vector3(0.42f, 0f, 0f));
                var rightHand = Child(rightLowerArm, "right-hand", new Vector3(0.28f, 0f, 0f));
                var leftUpperLeg = Child(hips, "left-upper-leg", new Vector3(-0.15f, -0.1f, 0f));
                var leftLowerLeg = Child(leftUpperLeg, "left-lower-leg", new Vector3(0f, -0.5f, 0f));
                var leftFoot = Child(leftLowerLeg, "left-foot", new Vector3(0f, -0.45f, 0.08f));
                var rightUpperLeg = Child(hips, "right-upper-leg", new Vector3(0.15f, -0.1f, 0f));
                var rightLowerLeg = Child(rightUpperLeg, "right-lower-leg", new Vector3(0f, -0.5f, 0f));
                var rightFoot = Child(rightLowerLeg, "right-foot", new Vector3(0f, -0.45f, 0.08f));

                bones[(int)CanonicalBoneId.Pelvis] = hips;
                bones[(int)CanonicalBoneId.Chest] = chest;
                bones[(int)CanonicalBoneId.LeftUpperArm] = leftUpperArm;
                bones[(int)CanonicalBoneId.LeftLowerArm] = leftLowerArm;
                bones[(int)CanonicalBoneId.RightUpperArm] = rightUpperArm;
                bones[(int)CanonicalBoneId.RightLowerArm] = rightLowerArm;
                bones[(int)CanonicalBoneId.LeftUpperLeg] = leftUpperLeg;
                bones[(int)CanonicalBoneId.LeftLowerLeg] = leftLowerLeg;
                bones[(int)CanonicalBoneId.RightUpperLeg] = rightUpperLeg;
                bones[(int)CanonicalBoneId.RightLowerLeg] = rightLowerLeg;

                var leftIndex = Child(leftHand, "left-index", new Vector3(-0.08f, 0.025f, 0.015f));
                var leftMiddle = Child(leftHand, "left-middle", new Vector3(-0.09f, 0f, 0f));
                var leftLittle = Child(leftHand, "left-little", new Vector3(-0.07f, -0.035f, -0.012f));
                var rightIndex = Child(rightHand, "right-index", new Vector3(0.08f, 0.025f, 0.015f));
                var rightMiddle = Child(rightHand, "right-middle", new Vector3(0.09f, 0f, 0f));
                var rightLittle = Child(rightHand, "right-little", new Vector3(0.07f, -0.035f, -0.012f));
                source.Set(
                    HumanoidFingerBoneId.LeftIndexProximal, leftIndex,
                    HumanoidFingerBoneId.LeftMiddleProximal, leftMiddle,
                    HumanoidFingerBoneId.LeftLittleProximal, leftLittle);
                source.Set(
                    HumanoidFingerBoneId.RightIndexProximal, rightIndex,
                    HumanoidFingerBoneId.RightMiddleProximal, rightMiddle,
                    HumanoidFingerBoneId.RightLittleProximal, rightLittle);

                rig.Binding = rig.Owner.AddComponent<HumanoidRigBinding>();
                rig.Binding.ConfigureExplicit(rig.Owner.transform, bones, new[]
                {
                    leftHand, rightHand, leftFoot, rightFoot,
                });
                rig.Hands = rig.Owner.AddComponent<HandMotionRuntime>();
                rig.Detail = rig.Owner.AddComponent<RichHumanoidDetailRetargeter>();
                rig.LeftLowerArm = leftLowerArm;
                rig.LeftHand = leftHand;
                rig.RightHand = rightHand;
                rig.LeftBaseline = rig.Binding.GetChainTipBindLocalRotation(CanonicalKinematicChainId.LeftArm);
                rig.RightBaseline = rig.Binding.GetChainTipBindLocalRotation(CanonicalKinematicChainId.RightArm);
                return rig;
            }

            public void Dispose()
            {
                if (Owner != null) UnityEngine.Object.DestroyImmediate(Owner);
            }

            private static Transform Child(Transform parent, string name, Vector3 localPosition)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPosition;
                return go.transform;
            }
        }
    }
}
