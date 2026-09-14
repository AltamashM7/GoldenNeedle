using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Rich;
using UnityEngine;

static class Smoke
{
    private const float Epsilon = 0.001f;

    public static void Main()
    {
        V1Compatibility();
        BasisValidityAndDegeneracy();
        AxialForearmRotationProof();
        ShortAmbiguityHoldsTwistWhileSwingMoves();
        LongLossFallsBackTowardReference();
        PartialBodyAndTorsoYawRemainIndependent();
        LeftRightHandednessRemainsRightHanded();
        FeetReconstructAndDegenerateSafely();

        Console.WriteLine("FOUNDATION_C_RICH_MOTION_SMOKE=PASS");
        Console.WriteLine("CANONICAL_BODY_V1_20_JOINTS=PASS");
        Console.WriteLine("ANATOMICAL_BASIS_ORTHONORMAL_RIGHT_HANDED=PASS");
        Console.WriteLine("AXIAL_FOREARM_ROTATION_PROOF=PASS");
        Console.WriteLine("SHORT_AMBIGUITY_HELD_REPROJECTED=PASS");
        Console.WriteLine("LONG_LOSS_REFERENCE_FALLBACK=PASS");
        Console.WriteLine("PARTIAL_BODY_INDEPENDENCE=PASS");
        Console.WriteLine("LEFT_RIGHT_HANDEDNESS=PASS");
        Console.WriteLine("TORSO_YAW_BASIS=PASS");
        Console.WriteLine("FOOT_ORIENTATION_AND_DEGENERACY=PASS");
    }

    private static void V1Compatibility()
    {
        Assert(CanonicalPoseFrame.JointCount == 20, "CanonicalPoseFrame.JointCount changed");
        var expected = new[]
        {
            CanonicalJointId.Pelvis,
            CanonicalJointId.Spine,
            CanonicalJointId.Chest,
            CanonicalJointId.Head,
            CanonicalJointId.LeftShoulder,
            CanonicalJointId.LeftElbow,
            CanonicalJointId.LeftWrist,
            CanonicalJointId.RightShoulder,
            CanonicalJointId.RightElbow,
            CanonicalJointId.RightWrist,
            CanonicalJointId.LeftHip,
            CanonicalJointId.LeftKnee,
            CanonicalJointId.LeftAnkle,
            CanonicalJointId.LeftHeel,
            CanonicalJointId.LeftToe,
            CanonicalJointId.RightHip,
            CanonicalJointId.RightKnee,
            CanonicalJointId.RightAnkle,
            CanonicalJointId.RightHeel,
            CanonicalJointId.RightToe,
        };
        Assert(expected.Length == 20, "V1 expected joint list is wrong");
        for (var i = 0; i < expected.Length; i++)
        {
            Assert((int)expected[i] == i, $"CanonicalJointId numeric meaning changed at {expected[i]}");
        }
    }

    private static void BasisValidityAndDegeneracy()
    {
        Assert(
            RichAnatomicalOrientationSolver.TryBuildBasis(
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 0.2f, 0f),
                0.0001f,
                0.0025f,
                out var basis),
            "valid basis rejected");
        Assert(Finite(basis.primaryAxis) && Finite(basis.secondaryAxis) && Finite(basis.thirdAxis), "basis not finite");
        Assert(Near(basis.primaryAxis.magnitude, 1f), "primary not normalized");
        Assert(Near(basis.secondaryAxis.magnitude, 1f), "secondary not normalized");
        Assert(Near(basis.thirdAxis.magnitude, 1f), "third not normalized");
        Assert(MathF.Abs(Vector3.Dot(basis.primaryAxis, basis.secondaryAxis)) < Epsilon, "primary/secondary not orthogonal");
        Assert(MathF.Abs(Vector3.Dot(basis.primaryAxis, basis.thirdAxis)) < Epsilon, "primary/third not orthogonal");
        Assert(MathF.Abs(Vector3.Dot(basis.secondaryAxis, basis.thirdAxis)) < Epsilon, "secondary/third not orthogonal");
        Assert(basis.handedness == CanonicalAnatomicalHandedness.RightHanded && basis.determinant > 0.999f, "basis handedness invalid");

        var solver = new RichAnatomicalOrientationSolver();
        var output = new RichMotionFrame();
        solver.Solve(Frame(0d,
            P(RichMotionEvidenceId.LeftElbow, 0f, 0f, 0f),
            P(RichMotionEvidenceId.LeftWrist, 0f, 1f, 0f),
            P(RichMotionEvidenceId.LeftPinky, 0f, 0.2f, 0f),
            P(RichMotionEvidenceId.LeftThumb, 0f, 0.4f, 0f)), output);
        var forearm = output.GetOrientation(RichOrientationChannelId.LeftLowerArm);
        Assert(forearm.HasSwing, "degenerate secondary discarded valid swing");
        Assert(!forearm.HasFullOrientation, "degenerate secondary produced a full basis");
        Assert(forearm.twistState == RichTwistObservability.Unobservable, "degenerate secondary claimed twist");
        Assert(Finite(forearm.primaryAxis), "degenerate path produced NaN");
    }

    private static void AxialForearmRotationProof()
    {
        var solver = new RichAnatomicalOrientationSolver();
        var firstOutput = new RichMotionFrame();
        solver.Solve(ForearmFrame(0d, new Vector3(-0.1f, 1f, 0f), new Vector3(0.1f, 1f, 0f)), firstOutput);
        var first = firstOutput.GetOrientation(RichOrientationChannelId.LeftLowerArm);
        Assert(first.twistState == RichTwistObservability.Observed, "initial forearm twist not observed");

        var secondOutput = new RichMotionFrame();
        solver.Solve(ForearmFrame(0.1d, new Vector3(0f, 1f, -0.1f), new Vector3(0f, 1f, 0.1f)), secondOutput);
        var second = secondOutput.GetOrientation(RichOrientationChannelId.LeftLowerArm);
        Assert(second.twistState == RichTwistObservability.Observed, "rotated palm twist not observed");
        Assert(Vector3.Dot(first.primaryAxis, second.primaryAxis) > 0.9999f, "axial proof changed swing direction");
        Assert(MathF.Abs(Vector3.Dot(first.basis.secondaryAxis, second.basis.secondaryAxis)) < 0.05f, "rich basis failed to distinguish axial rotation");
    }

    private static void ShortAmbiguityHoldsTwistWhileSwingMoves()
    {
        var solver = new RichAnatomicalOrientationSolver();
        var firstOutput = new RichMotionFrame();
        solver.Solve(ForearmFrame(0d, new Vector3(-0.1f, 1f, 0f), new Vector3(0.1f, 1f, 0f)), firstOutput);

        var ambiguous = Frame(0.1d,
            P(RichMotionEvidenceId.LeftElbow, 0f, 0f, 0f),
            P(RichMotionEvidenceId.LeftWrist, 0.1f, 0.995f, 0f));
        var output = new RichMotionFrame();
        solver.Solve(ambiguous, output);
        var held = output.GetOrientation(RichOrientationChannelId.LeftLowerArm);
        Assert(held.HasSwing && held.HasFullOrientation, "short ambiguity lost swing or held basis");
        Assert(held.twistState == RichTwistObservability.Held, "short ambiguity not marked Held");
        Assert(held.twistConfidence > 0f && held.twistConfidence < 1f, "held twist confidence not reduced");
        Assert(Vector3.Dot(held.primaryAxis, new Vector3(0f, 1f, 0f)) < 0.9999f, "primary did not continue updating during held twist");
        Assert(MathF.Abs(Vector3.Dot(held.primaryAxis, held.basis.secondaryAxis)) < Epsilon, "held secondary not reprojected");
    }

    private static void LongLossFallsBackTowardReference()
    {
        var solver = new RichAnatomicalOrientationSolver();
        var referenceOutput = new RichMotionFrame();
        solver.Solve(ForearmFrame(0d, new Vector3(-0.1f, 1f, 0f), new Vector3(0.1f, 1f, 0f)), referenceOutput);

        var twistedOutput = new RichMotionFrame();
        solver.Solve(ForearmFrame(0.1d, new Vector3(0f, 1f, -0.1f), new Vector3(0f, 1f, 0.1f)), twistedOutput);
        var twisted = twistedOutput.GetOrientation(RichOrientationChannelId.LeftLowerArm);

        var output = new RichMotionFrame();
        solver.Solve(Frame(0.7d,
            P(RichMotionEvidenceId.LeftElbow, 0f, 0f, 0f),
            P(RichMotionEvidenceId.LeftWrist, 0f, 1f, 0f)), output);
        var fallback = output.GetOrientation(RichOrientationChannelId.LeftLowerArm);
        Assert(fallback.HasSwing && fallback.HasFullOrientation, "reference fallback lost valid orientation");
        Assert(fallback.twistState == RichTwistObservability.ReferenceFallback, "long loss did not enter reference fallback");
        Assert(Finite(fallback.basis.secondaryAxis), "fallback produced non-finite basis");
        Assert(Vector3.Dot(twisted.basis.secondaryAxis, fallback.basis.secondaryAxis) > 0f, "fallback made a discontinuous flip");
        Assert(fallback.twistConfidence < twisted.twistConfidence, "fallback incorrectly reports fully observed confidence");
    }

    private static void PartialBodyAndTorsoYawRemainIndependent()
    {
        var frame = Frame(0d,
            P(RichMotionEvidenceId.Pelvis, 0f, 0f, 0f),
            P(RichMotionEvidenceId.Chest, 0f, 1f, 0f),
            P(RichMotionEvidenceId.LeftHip, 0f, 0f, -0.2f),
            P(RichMotionEvidenceId.RightHip, 0f, 0f, 0.2f),
            P(RichMotionEvidenceId.LeftShoulder, 0f, 1f, -0.25f),
            P(RichMotionEvidenceId.RightShoulder, 0f, 1f, 0.25f));
        var output = new RichMotionFrame();
        new RichAnatomicalOrientationSolver().Solve(frame, output);
        Assert(output.GetOrientation(RichOrientationChannelId.Pelvis).twistState == RichTwistObservability.Observed, "yawed pelvis basis collapsed");
        Assert(output.GetOrientation(RichOrientationChannelId.Chest).twistState == RichTwistObservability.Observed, "yawed chest basis collapsed");
        Assert(!output.GetOrientation(RichOrientationChannelId.RightLowerArm).HasSwing, "missing limb became valid");
        Assert(output.validOrientationCount >= 2, "missing limb invalidated unrelated torso channels");
    }

    private static void LeftRightHandednessRemainsRightHanded()
    {
        var frame = Frame(0d,
            P(RichMotionEvidenceId.LeftElbow, -0.5f, 0f, 0f),
            P(RichMotionEvidenceId.LeftWrist, -0.5f, 1f, 0f),
            P(RichMotionEvidenceId.LeftPinky, -0.6f, 1f, 0f),
            P(RichMotionEvidenceId.LeftThumb, -0.4f, 1f, 0f),
            P(RichMotionEvidenceId.RightElbow, 0.5f, 0f, 0f),
            P(RichMotionEvidenceId.RightWrist, 0.5f, 1f, 0f),
            P(RichMotionEvidenceId.RightPinky, 0.6f, 1f, 0f),
            P(RichMotionEvidenceId.RightThumb, 0.4f, 1f, 0f));
        var output = new RichMotionFrame();
        new RichAnatomicalOrientationSolver().Solve(frame, output);
        var left = output.GetOrientation(RichOrientationChannelId.LeftLowerArm);
        var right = output.GetOrientation(RichOrientationChannelId.RightLowerArm);
        Assert(left.HasFullOrientation && right.HasFullOrientation, "mirrored forearms not reconstructed");
        Assert(left.basis.handedness == CanonicalAnatomicalHandedness.RightHanded, "left basis handedness reversed");
        Assert(right.basis.handedness == CanonicalAnatomicalHandedness.RightHanded, "right basis handedness reversed");
        Assert(left.basis.determinant > 0.999f && right.basis.determinant > 0.999f, "mirrored determinant invalid");
    }

    private static void FeetReconstructAndDegenerateSafely()
    {
        var observed = Frame(0d,
            P(RichMotionEvidenceId.LeftHeel, 0f, 0f, 0f),
            P(RichMotionEvidenceId.LeftToe, 0f, 0f, 0.2f),
            P(RichMotionEvidenceId.LeftAnkle, 0f, 0.1f, -0.05f));
        var output = new RichMotionFrame();
        new RichAnatomicalOrientationSolver().Solve(observed, output);
        var foot = output.GetOrientation(RichOrientationChannelId.LeftFoot);
        Assert(foot.twistState == RichTwistObservability.Observed && foot.HasFullOrientation, "foot orientation not reconstructed");

        var degenerate = Frame(0d,
            P(RichMotionEvidenceId.LeftHeel, 0f, 0f, 0f),
            P(RichMotionEvidenceId.LeftToe, 0f, 0f, 0.2f),
            P(RichMotionEvidenceId.LeftAnkle, 0f, 0f, -0.1f));
        output = new RichMotionFrame();
        new RichAnatomicalOrientationSolver().Solve(degenerate, output);
        foot = output.GetOrientation(RichOrientationChannelId.LeftFoot);
        Assert(foot.HasSwing && !foot.HasFullOrientation, "degenerate foot secondary did not preserve swing-only state");
        Assert(foot.twistState == RichTwistObservability.Unobservable, "degenerate foot claimed twist");
        Assert(Finite(foot.primaryAxis), "degenerate foot produced non-finite swing");
    }

    private static RichMotionEvidenceFrame ForearmFrame(double time, Vector3 pinky, Vector3 thumb)
    {
        return Frame(time,
            P(RichMotionEvidenceId.LeftElbow, 0f, 0f, 0f),
            P(RichMotionEvidenceId.LeftWrist, 0f, 1f, 0f),
            new Point(RichMotionEvidenceId.LeftPinky, pinky),
            new Point(RichMotionEvidenceId.LeftThumb, thumb));
    }

    private readonly record struct Point(RichMotionEvidenceId Id, Vector3 Position, float Confidence = 1f);

    private static Point P(RichMotionEvidenceId id, float x, float y, float z) =>
        new Point(id, new Vector3(x, y, z));

    private static RichMotionEvidenceFrame Frame(double time, params Point[] points)
    {
        var frame = new RichMotionEvidenceFrame();
        frame.Begin("Synthetic", (long)(time * 1000d) + 1L, time, time, true);
        foreach (var point in points)
        {
            var evidence = new RichMotionEvidencePoint
            {
                id = point.Id,
                tracking = CanonicalTrackingState.Tracked,
                confidence = point.Confidence,
                position = point.Position,
                hasPosition = true,
            };
            frame.SetEvidence(in evidence);
        }
        frame.Complete();
        return frame;
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    private static bool Near(float actual, float expected) => MathF.Abs(actual - expected) < Epsilon;

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
