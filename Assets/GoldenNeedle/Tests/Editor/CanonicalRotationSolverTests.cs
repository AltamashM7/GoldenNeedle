using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Rotation;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class CanonicalRotationSolverTests
    {
        [Test]
        public void CalibrationPoseProducesIdentityDeltasForAllDrivenBones()
        {
            var profile = ReferenceProfile();
            var frame = ReferenceFrame();
            var output = new CanonicalRotationFrame();

            new CanonicalRotationSolver().Solve(frame, profile, output);

            Assert.That(output.validBoneCount, Is.EqualTo(CanonicalRotationFrame.BoneCount));
            for (var i = 0; i < CanonicalRotationFrame.BoneCount; i++)
            {
                var bone = output.GetBone((CanonicalBoneId)i);
                Assert.That(bone.IsTracked, Is.True);
                Assert.That(Quaternion.Angle(bone.rotationDeltaFromCalibration, Quaternion.identity), Is.LessThan(0.01f));
            }
        }

        [Test]
        public void RaisedLeftArmProducesLeftOnlyNonIdentitySwing()
        {
            var frame = ReferenceFrame();
            Set3D(frame, CanonicalJointId.LeftElbow, new Vector3(-0.4f, 2.25f, 0f));
            Set3D(frame, CanonicalJointId.LeftWrist, new Vector3(-0.4f, 2.55f, 0f));
            var output = new CanonicalRotationFrame();

            new CanonicalRotationSolver().Solve(frame, ReferenceProfile(), output);

            Assert.That(Quaternion.Angle(output.GetBone(CanonicalBoneId.LeftUpperArm).rotationDeltaFromCalibration, Quaternion.identity), Is.GreaterThan(10f));
            Assert.That(Quaternion.Angle(output.GetBone(CanonicalBoneId.RightUpperArm).rotationDeltaFromCalibration, Quaternion.identity), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(output.GetBone(CanonicalBoneId.RightLowerArm).rotationDeltaFromCalibration, Quaternion.identity), Is.LessThan(0.01f));
        }

        [Test]
        public void ElbowBendProducesFiniteLowerArmRotation()
        {
            var frame = ReferenceFrame();
            Set3D(frame, CanonicalJointId.LeftElbow, new Vector3(-0.4f, 2.25f, 0f));
            Set3D(frame, CanonicalJointId.LeftWrist, new Vector3(-0.7f, 2.25f, 0f));
            var output = new CanonicalRotationFrame();

            new CanonicalRotationSolver().Solve(frame, ReferenceProfile(), output);

            var lowerArm = output.GetBone(CanonicalBoneId.LeftLowerArm);
            Assert.That(lowerArm.IsTracked, Is.True);
            Assert.That(IsFinite(lowerArm.rotationDeltaFromCalibration), Is.True);
        }

        [Test]
        public void LegLiftAndBendProduceFiniteLegRotations()
        {
            var frame = ReferenceFrame();
            Set3D(frame, CanonicalJointId.LeftKnee, new Vector3(-0.35f, 0.85f, 0.05f));
            Set3D(frame, CanonicalJointId.LeftAnkle, new Vector3(-0.05f, 0.50f, 0.20f));
            var output = new CanonicalRotationFrame();

            new CanonicalRotationSolver().Solve(frame, ReferenceProfile(), output);

            Assert.That(output.GetBone(CanonicalBoneId.LeftUpperLeg).IsTracked, Is.True);
            Assert.That(output.GetBone(CanonicalBoneId.LeftLowerLeg).IsTracked, Is.True);
            Assert.That(IsFinite(output.GetBone(CanonicalBoneId.LeftUpperLeg).rotationDeltaFromCalibration), Is.True);
            Assert.That(IsFinite(output.GetBone(CanonicalBoneId.LeftLowerLeg).rotationDeltaFromCalibration), Is.True);
        }

        [Test]
        public void MissingLegsDoNotInvalidateTrackedArms()
        {
            var frame = new CanonicalPoseFrame();
            frame.Begin(1L, 0.1d, true);
            Set3D(frame, CanonicalJointId.LeftShoulder, new Vector3(-0.4f, 1.95f, 0f));
            Set3D(frame, CanonicalJointId.LeftElbow, new Vector3(-0.7f, 1.95f, 0f));
            Set3D(frame, CanonicalJointId.LeftWrist, new Vector3(-1.0f, 1.95f, 0f));
            Set3D(frame, CanonicalJointId.RightShoulder, new Vector3(0.4f, 1.95f, 0f));
            Set3D(frame, CanonicalJointId.RightElbow, new Vector3(0.7f, 1.95f, 0f));
            Set3D(frame, CanonicalJointId.RightWrist, new Vector3(1.0f, 1.95f, 0f));
            frame.Complete();
            var output = new CanonicalRotationFrame();

            new CanonicalRotationSolver().Solve(frame, ReferenceProfile(), output);

            Assert.That(output.GetBone(CanonicalBoneId.LeftUpperArm).IsTracked, Is.True);
            Assert.That(output.GetBone(CanonicalBoneId.LeftLowerArm).IsTracked, Is.True);
            Assert.That(output.GetBone(CanonicalBoneId.RightUpperArm).IsTracked, Is.True);
            Assert.That(output.GetBone(CanonicalBoneId.LeftUpperLeg).IsTracked, Is.False);
            Assert.That(output.GetBone(CanonicalBoneId.RightLowerLeg).IsTracked, Is.False);
        }

        [Test]
        public void MissingLeftWristAffectsOnlyLeftLowerArm()
        {
            var frame = ReferenceFrame();
            frame.SetJoint(new CanonicalPoseJoint { id = CanonicalJointId.LeftWrist, tracking = CanonicalTrackingState.Unavailable });
            frame.Complete();
            var output = new CanonicalRotationFrame();

            new CanonicalRotationSolver().Solve(frame, ReferenceProfile(), output);

            Assert.That(output.GetBone(CanonicalBoneId.LeftUpperArm).IsTracked, Is.True);
            Assert.That(output.GetBone(CanonicalBoneId.LeftLowerArm).IsTracked, Is.False);
            Assert.That(output.GetBone(CanonicalBoneId.RightUpperArm).IsTracked, Is.True);
            Assert.That(output.GetBone(CanonicalBoneId.RightLowerArm).IsTracked, Is.True);
            Assert.That(output.GetBone(CanonicalBoneId.LeftUpperLeg).IsTracked, Is.True);
        }

        [Test]
        public void InvalidDirectionBecomesUnavailableWithoutNaN()
        {
            var profile = ReferenceProfile();
            var leftArm = profile.leftArmGeometry;
            leftArm.referenceUpperDirection = Vector3.zero;
            leftArm.referenceLowerDirection = Vector3.zero;
            profile.leftArmGeometry = leftArm;
            var output = new CanonicalRotationFrame();

            new CanonicalRotationSolver().Solve(ReferenceFrame(), profile, output);

            Assert.That(output.GetBone(CanonicalBoneId.LeftUpperArm).IsTracked, Is.False);
            for (var i = 0; i < CanonicalRotationFrame.BoneCount; i++)
            {
                Assert.That(IsFinite(output.GetBone((CanonicalBoneId)i).rotationDeltaFromCalibration), Is.True);
            }
        }

        [Test]
        public void PelvisAndChestBasisAreFinite()
        {
            var output = new CanonicalRotationFrame();
            new CanonicalRotationSolver().Solve(ReferenceFrame(), ReferenceProfile(), output);

            Assert.That(output.GetBone(CanonicalBoneId.Pelvis).IsTracked, Is.True);
            Assert.That(output.GetBone(CanonicalBoneId.Chest).IsTracked, Is.True);
            Assert.That(IsFinite(output.GetBone(CanonicalBoneId.Pelvis).rotationDeltaFromCalibration), Is.True);
            Assert.That(IsFinite(output.GetBone(CanonicalBoneId.Chest).rotationDeltaFromCalibration), Is.True);
        }

        [Test]
        public void BodyQuaternionPreservesRightAndUpWhenSemanticForwardIsReflected()
        {
            Assert.That(CanonicalRotationSolver.TryBuildBodyRotation(
                Vector3.right,
                Vector3.up,
                Vector3.back,
                out var rotation), Is.True);

            Assert.That(Vector3.Dot(rotation * Vector3.right, Vector3.right), Is.GreaterThan(0.99f));
            Assert.That(Vector3.Dot(rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.99f));
            Assert.That(Vector3.Dot(rotation * Vector3.forward, Vector3.forward), Is.GreaterThan(0.99f));
        }

        private static MotionCalibrationProfile ReferenceProfile()
        {
            return new MotionCalibrationProfile
            {
                version = MotionCalibrationProfile.CurrentVersion,
                isValid = true,
                bodyReferenceValid = true,
                state = MotionCalibrationState.Ready,
                neutralPelvisPosition = new Vector3(0f, 1.05f, 0f),
                neutralChestPosition = new Vector3(0f, 1.95f, 0f),
                neutralLeftShoulderPosition = new Vector3(-0.4f, 1.95f, 0f),
                neutralRightShoulderPosition = new Vector3(0.4f, 1.95f, 0f),
                neutralLeftHipPosition = new Vector3(-0.2f, 1.05f, 0f),
                neutralRightHipPosition = new Vector3(0.2f, 1.05f, 0f),
                neutralBodyRight = Vector3.right,
                neutralBodyUp = Vector3.up,
                neutralBodyForward = Vector3.back,
                leftArmGeometry = Geometry(0.3f, 0.3f, Vector3.left, Vector3.left),
                rightArmGeometry = Geometry(0.3f, 0.3f, Vector3.right, Vector3.right),
                leftLegGeometry = Geometry(0.5f, 0.5f, Vector3.down, Vector3.down),
                rightLegGeometry = Geometry(0.5f, 0.5f, Vector3.down, Vector3.down),
            };
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

        private static CanonicalPoseFrame ReferenceFrame()
        {
            var frame = new CanonicalPoseFrame();
            frame.Begin(1L, 0.1d, true);
            Set3D(frame, CanonicalJointId.Pelvis, new Vector3(0f, 1.05f, 0f));
            Set3D(frame, CanonicalJointId.Chest, new Vector3(0f, 1.95f, 0f));
            Set3D(frame, CanonicalJointId.LeftShoulder, new Vector3(-0.4f, 1.95f, 0f));
            Set3D(frame, CanonicalJointId.LeftElbow, new Vector3(-0.7f, 1.95f, 0f));
            Set3D(frame, CanonicalJointId.LeftWrist, new Vector3(-1.0f, 1.95f, 0f));
            Set3D(frame, CanonicalJointId.RightShoulder, new Vector3(0.4f, 1.95f, 0f));
            Set3D(frame, CanonicalJointId.RightElbow, new Vector3(0.7f, 1.95f, 0f));
            Set3D(frame, CanonicalJointId.RightWrist, new Vector3(1.0f, 1.95f, 0f));
            Set3D(frame, CanonicalJointId.LeftHip, new Vector3(-0.2f, 1.05f, 0f));
            Set3D(frame, CanonicalJointId.LeftKnee, new Vector3(-0.2f, 0.55f, 0f));
            Set3D(frame, CanonicalJointId.LeftAnkle, new Vector3(-0.2f, 0.05f, 0f));
            Set3D(frame, CanonicalJointId.RightHip, new Vector3(0.2f, 1.05f, 0f));
            Set3D(frame, CanonicalJointId.RightKnee, new Vector3(0.2f, 0.55f, 0f));
            Set3D(frame, CanonicalJointId.RightAnkle, new Vector3(0.2f, 0.05f, 0f));
            frame.Complete();
            return frame;
        }

        private static void Set3D(CanonicalPoseFrame frame, CanonicalJointId id, Vector3 position)
        {
            frame.SetJoint(new CanonicalPoseJoint
            {
                id = id,
                tracking = CanonicalTrackingState.Tracked,
                confidence = 1f,
                worldPosition = position,
                hasWorldPosition = true,
                localPosition = position,
                hasLocalPosition = true,
            });
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
