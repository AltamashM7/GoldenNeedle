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
                Object.DestroyImmediate(root);
            }
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
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void OptionalDetailCountDoesNotParticipateInLegacyBindingValidityOrCount()
        {
            var owner = new GameObject("E-binding-owner");
            var bones = new Transform[CanonicalRotationFrame.BoneCount];
            try
            {
                var detailSource = owner.AddComponent<FoundationETestDetailSource>();
                var finger = new GameObject("optional-finger");
                finger.transform.SetParent(owner.transform, false);
                detailSource.Set(HumanoidFingerBoneId.LeftThumbProximal, finger.transform);

                for (var i = 0; i < bones.Length; i++)
                {
                    var bone = new GameObject($"body-{i}");
                    bone.transform.SetParent(owner.transform, false);
                    bone.transform.localPosition = new Vector3(i * .1f, 1f + i * .02f, 0f);
                    bones[i] = bone.transform;
                }

                var binding = owner.AddComponent<HumanoidRigBinding>();
                binding.ConfigureExplicit(owner.transform, bones);

                Assert.That(binding.IsBound, Is.True);
                Assert.That(binding.BoundBoneCount, Is.EqualTo(CanonicalRotationFrame.BoneCount));
                Assert.That(binding.OptionalDetailBoneCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
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
    }
}
