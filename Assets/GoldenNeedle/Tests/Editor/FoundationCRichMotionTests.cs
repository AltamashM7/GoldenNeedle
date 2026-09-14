using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Rich;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class FoundationCRichMotionTests
    {
        [Test]
        public void CanonicalBodyV1RemainsExactlyTwentyJointsWithStableNumericMeanings()
        {
            Assert.That(CanonicalPoseFrame.JointCount, Is.EqualTo(20));
            Assert.That((int)CanonicalJointId.Pelvis, Is.EqualTo(0));
            Assert.That((int)CanonicalJointId.Spine, Is.EqualTo(1));
            Assert.That((int)CanonicalJointId.Chest, Is.EqualTo(2));
            Assert.That((int)CanonicalJointId.Head, Is.EqualTo(3));
            Assert.That((int)CanonicalJointId.LeftShoulder, Is.EqualTo(4));
            Assert.That((int)CanonicalJointId.LeftElbow, Is.EqualTo(5));
            Assert.That((int)CanonicalJointId.LeftWrist, Is.EqualTo(6));
            Assert.That((int)CanonicalJointId.RightShoulder, Is.EqualTo(7));
            Assert.That((int)CanonicalJointId.RightElbow, Is.EqualTo(8));
            Assert.That((int)CanonicalJointId.RightWrist, Is.EqualTo(9));
            Assert.That((int)CanonicalJointId.LeftHip, Is.EqualTo(10));
            Assert.That((int)CanonicalJointId.LeftKnee, Is.EqualTo(11));
            Assert.That((int)CanonicalJointId.LeftAnkle, Is.EqualTo(12));
            Assert.That((int)CanonicalJointId.LeftHeel, Is.EqualTo(13));
            Assert.That((int)CanonicalJointId.LeftToe, Is.EqualTo(14));
            Assert.That((int)CanonicalJointId.RightHip, Is.EqualTo(15));
            Assert.That((int)CanonicalJointId.RightKnee, Is.EqualTo(16));
            Assert.That((int)CanonicalJointId.RightAnkle, Is.EqualTo(17));
            Assert.That((int)CanonicalJointId.RightHeel, Is.EqualTo(18));
            Assert.That((int)CanonicalJointId.RightToe, Is.EqualTo(19));
        }

        [Test]
        public void AnatomicalBasisIsFiniteNormalizedOrthogonalAndRightHanded()
        {
            Assert.That(
                RichAnatomicalOrientationSolver.TryBuildBasis(
                    Vector3.up,
                    new Vector3(1f, 0.2f, 0.1f),
                    0.0001f,
                    0.0025f,
                    out var basis),
                Is.True);

            Assert.That(IsFinite(basis.primaryAxis), Is.True);
            Assert.That(IsFinite(basis.secondaryAxis), Is.True);
            Assert.That(IsFinite(basis.thirdAxis), Is.True);
            Assert.That(basis.primaryAxis.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(basis.secondaryAxis.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(basis.thirdAxis.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(Mathf.Abs(Vector3.Dot(basis.primaryAxis, basis.secondaryAxis)), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(Vector3.Dot(basis.primaryAxis, basis.thirdAxis)), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(Vector3.Dot(basis.secondaryAxis, basis.thirdAxis)), Is.LessThan(0.001f));
            Assert.That(basis.handedness, Is.EqualTo(CanonicalAnatomicalHandedness.RightHanded));
            Assert.That(basis.determinant, Is.GreaterThan(0.999f));
        }

        [Test]
        public void DegenerateSecondaryPreservesSwingWithoutClaimingTwist()
        {
            var output = Solve(new RichAnatomicalOrientationSolver(), Frame(0d,
                Point(RichMotionEvidenceId.LeftElbow, Vector3.zero),
                Point(RichMotionEvidenceId.LeftWrist, Vector3.up),
                Point(RichMotionEvidenceId.LeftPinky, Vector3.up * 0.2f),
                Point(RichMotionEvidenceId.LeftThumb, Vector3.up * 0.4f)));

            var channel = output.GetOrientation(RichOrientationChannelId.LeftLowerArm);
            Assert.That(channel.HasSwing, Is.True);
            Assert.That(channel.HasFullOrientation, Is.False);
            Assert.That(channel.twistState, Is.EqualTo(RichTwistObservability.Unobservable));
            Assert.That(IsFinite(channel.primaryAxis), Is.True);
        }

        [Test]
        public void AxialForearmEvidenceChangesRichBasisWhilePrimaryDirectionStaysFixed()
        {
            var solver = new RichAnatomicalOrientationSolver();
            var first = Solve(solver, ForearmFrame(
                0d,
                new Vector3(-0.1f, 1f, 0f),
                new Vector3(0.1f, 1f, 0f)))
                .GetOrientation(RichOrientationChannelId.LeftLowerArm);
            var second = Solve(solver, ForearmFrame(
                0.1d,
                new Vector3(0f, 1f, -0.1f),
                new Vector3(0f, 1f, 0.1f)))
                .GetOrientation(RichOrientationChannelId.LeftLowerArm);

            Assert.That(first.twistState, Is.EqualTo(RichTwistObservability.Observed));
            Assert.That(second.twistState, Is.EqualTo(RichTwistObservability.Observed));
            Assert.That(Vector3.Dot(first.primaryAxis, second.primaryAxis), Is.GreaterThan(0.9999f));
            Assert.That(Mathf.Abs(Vector3.Dot(first.basis.secondaryAxis, second.basis.secondaryAxis)), Is.LessThan(0.05f));
        }

        [Test]
        public void ShortSecondaryLossHoldsTwistAndReprojectsAgainstUpdatedSwing()
        {
            var solver = new RichAnatomicalOrientationSolver();
            Solve(solver, ForearmFrame(
                0d,
                new Vector3(-0.1f, 1f, 0f),
                new Vector3(0.1f, 1f, 0f)));

            var held = Solve(solver, Frame(0.1d,
                Point(RichMotionEvidenceId.LeftElbow, Vector3.zero),
                Point(RichMotionEvidenceId.LeftWrist, new Vector3(0.1f, 0.995f, 0f))))
                .GetOrientation(RichOrientationChannelId.LeftLowerArm);

            Assert.That(held.HasSwing, Is.True);
            Assert.That(held.HasFullOrientation, Is.True);
            Assert.That(held.twistState, Is.EqualTo(RichTwistObservability.Held));
            Assert.That(held.twistConfidence, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(Mathf.Abs(Vector3.Dot(held.primaryAxis, held.basis.secondaryAxis)), Is.LessThan(0.001f));
        }

        [Test]
        public void LongerSecondaryLossTransitionsToFiniteReferenceFallbackWithoutFlip()
        {
            var solver = new RichAnatomicalOrientationSolver();
            Solve(solver, ForearmFrame(
                0d,
                new Vector3(-0.1f, 1f, 0f),
                new Vector3(0.1f, 1f, 0f)));
            var twisted = Solve(solver, ForearmFrame(
                0.1d,
                new Vector3(0f, 1f, -0.1f),
                new Vector3(0f, 1f, 0.1f)))
                .GetOrientation(RichOrientationChannelId.LeftLowerArm);

            var fallback = Solve(solver, Frame(0.7d,
                Point(RichMotionEvidenceId.LeftElbow, Vector3.zero),
                Point(RichMotionEvidenceId.LeftWrist, Vector3.up)))
                .GetOrientation(RichOrientationChannelId.LeftLowerArm);

            Assert.That(fallback.HasSwing, Is.True);
            Assert.That(fallback.HasFullOrientation, Is.True);
            Assert.That(fallback.twistState, Is.EqualTo(RichTwistObservability.ReferenceFallback));
            Assert.That(IsFinite(fallback.basis.secondaryAxis), Is.True);
            Assert.That(Vector3.Dot(twisted.basis.secondaryAxis, fallback.basis.secondaryAxis), Is.GreaterThan(0f));
            Assert.That(fallback.twistConfidence, Is.LessThan(twisted.twistConfidence));
        }

        [Test]
        public void MissingLimbDoesNotInvalidateYawedTorsoAndMirroredArmsStayRightHanded()
        {
            var output = Solve(new RichAnatomicalOrientationSolver(), Frame(0d,
                Point(RichMotionEvidenceId.Pelvis, Vector3.zero),
                Point(RichMotionEvidenceId.Chest, Vector3.up),
                Point(RichMotionEvidenceId.LeftHip, new Vector3(0f, 0f, -0.2f)),
                Point(RichMotionEvidenceId.RightHip, new Vector3(0f, 0f, 0.2f)),
                Point(RichMotionEvidenceId.LeftShoulder, new Vector3(0f, 1f, -0.25f)),
                Point(RichMotionEvidenceId.RightShoulder, new Vector3(0f, 1f, 0.25f)),
                Point(RichMotionEvidenceId.LeftElbow, new Vector3(-0.5f, 0f, 0f)),
                Point(RichMotionEvidenceId.LeftWrist, new Vector3(-0.5f, 1f, 0f)),
                Point(RichMotionEvidenceId.LeftPinky, new Vector3(-0.6f, 1f, 0f)),
                Point(RichMotionEvidenceId.LeftThumb, new Vector3(-0.4f, 1f, 0f))));

            Assert.That(output.GetOrientation(RichOrientationChannelId.Pelvis).twistState, Is.EqualTo(RichTwistObservability.Observed));
            Assert.That(output.GetOrientation(RichOrientationChannelId.Chest).twistState, Is.EqualTo(RichTwistObservability.Observed));
            Assert.That(output.GetOrientation(RichOrientationChannelId.LeftLowerArm).basis.handedness, Is.EqualTo(CanonicalAnatomicalHandedness.RightHanded));
            Assert.That(output.GetOrientation(RichOrientationChannelId.RightLowerArm).HasSwing, Is.False);
        }

        [Test]
        public void FootUsesHeelToePrimaryAndSafelyRejectsParallelSecondary()
        {
            var observed = Solve(new RichAnatomicalOrientationSolver(), Frame(0d,
                Point(RichMotionEvidenceId.LeftHeel, Vector3.zero),
                Point(RichMotionEvidenceId.LeftToe, new Vector3(0f, 0f, 0.2f)),
                Point(RichMotionEvidenceId.LeftAnkle, new Vector3(0f, 0.1f, -0.05f))))
                .GetOrientation(RichOrientationChannelId.LeftFoot);
            Assert.That(observed.twistState, Is.EqualTo(RichTwistObservability.Observed));

            var degenerate = Solve(new RichAnatomicalOrientationSolver(), Frame(0d,
                Point(RichMotionEvidenceId.LeftHeel, Vector3.zero),
                Point(RichMotionEvidenceId.LeftToe, new Vector3(0f, 0f, 0.2f)),
                Point(RichMotionEvidenceId.LeftAnkle, new Vector3(0f, 0f, -0.1f))))
                .GetOrientation(RichOrientationChannelId.LeftFoot);
            Assert.That(degenerate.HasSwing, Is.True);
            Assert.That(degenerate.HasFullOrientation, Is.False);
            Assert.That(degenerate.twistState, Is.EqualTo(RichTwistObservability.Unobservable));
        }

        private struct EvidencePoint
        {
            public RichMotionEvidenceId id;
            public Vector3 position;
        }

        private static EvidencePoint Point(RichMotionEvidenceId id, Vector3 position)
        {
            return new EvidencePoint { id = id, position = position };
        }

        private static RichMotionEvidenceFrame ForearmFrame(
            double time,
            Vector3 pinky,
            Vector3 thumb)
        {
            return Frame(time,
                Point(RichMotionEvidenceId.LeftElbow, Vector3.zero),
                Point(RichMotionEvidenceId.LeftWrist, Vector3.up),
                Point(RichMotionEvidenceId.LeftPinky, pinky),
                Point(RichMotionEvidenceId.LeftThumb, thumb));
        }

        private static RichMotionEvidenceFrame Frame(double time, params EvidencePoint[] points)
        {
            var frame = new RichMotionEvidenceFrame();
            frame.Begin("Synthetic", (long)(time * 1000d) + 1L, time, time, true);
            for (var i = 0; i < points.Length; i++)
            {
                var evidence = new RichMotionEvidencePoint
                {
                    id = points[i].id,
                    tracking = CanonicalTrackingState.Tracked,
                    confidence = 1f,
                    position = points[i].position,
                    hasPosition = true,
                };
                frame.SetEvidence(in evidence);
            }
            frame.Complete();
            return frame;
        }

        private static RichMotionFrame Solve(
            RichAnatomicalOrientationSolver solver,
            RichMotionEvidenceFrame frame)
        {
            var output = new RichMotionFrame();
            solver.Solve(frame, output);
            return output;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
