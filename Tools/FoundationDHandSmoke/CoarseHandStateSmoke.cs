using System;
using System.Runtime.CompilerServices;
using GoldenNeedle.Core.Motion.Hands;
using UnityEngine;

internal static class CoarseHandStateSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var settings = CoarseHandEstimatorSettings.CreateDefault();
        TestGeometry(settings);
        TestInvalidEvidence(settings);
        TestScaleAndSides(settings);
        TestTemporal(settings);
        Console.WriteLine("COARSE_HAND_GEOMETRY=PASS");
        Console.WriteLine("COARSE_HAND_CONFIDENCE_AMBIGUITY=PASS");
        Console.WriteLine("COARSE_HAND_SCALE_INVARIANT=PASS");
        Console.WriteLine("COARSE_HAND_LEFT_RIGHT_INDEPENDENT=PASS");
        Console.WriteLine("COARSE_HAND_TEMPORAL_STABILITY=PASS");
        Console.WriteLine("COARSE_HAND_DUPLICATE_TIMESTAMP=PASS");
        Console.WriteLine("COARSE_HAND_STALE_TO_UNKNOWN=PASS");
    }

    private static void TestGeometry(CoarseHandEstimatorSettings settings)
    {
        var open = Evidence(CanonicalHandSide.Left, 100, 1.0, Geometry.Open, 1f, 1f);
        var closed = Evidence(CanonicalHandSide.Left, 101, 1.1, Geometry.Closed, 1f, 1f);
        var ambiguous = Evidence(CanonicalHandSide.Left, 102, 1.2, Geometry.Ambiguous, 1f, 1f);

        Equal(CoarseHandState.Open, CoarseHandStateEstimator.Estimate(in open, settings).state, "clear open geometry");
        Equal(CoarseHandState.Closed, CoarseHandStateEstimator.Estimate(in closed, settings).state, "clear closed geometry");
        var unknown = CoarseHandStateEstimator.Estimate(in ambiguous, settings);
        Equal(CoarseHandState.Unknown, unknown.state, "between thresholds unknown");
        Equal(CoarseHandUnknownReason.AmbiguousGeometry, unknown.unknownReason, "ambiguity reason");
    }

    private static void TestInvalidEvidence(CoarseHandEstimatorSettings settings)
    {
        var low = Evidence(CanonicalHandSide.Left, 110, 2.0, Geometry.Open, 1f, 0.4f);
        var lowResult = CoarseHandStateEstimator.Estimate(in low, settings);
        Equal(CoarseHandState.Unknown, lowResult.state, "low confidence unknown");
        Equal(CoarseHandUnknownReason.LowConfidence, lowResult.unknownReason, "low confidence reason");

        var missing = Evidence(CanonicalHandSide.Left, 111, 2.1, Geometry.Open, 1f, 1f);
        missing.pinky.isTracked = false;
        Equal(CoarseHandUnknownReason.MissingLandmark, CoarseHandStateEstimator.Estimate(in missing, settings).unknownReason, "missing landmark");

        var missingWorld = Evidence(CanonicalHandSide.Left, 112, 2.2, Geometry.Open, 1f, 1f);
        missingWorld.index.hasPosition = false;
        Equal(CoarseHandUnknownReason.MissingWorldPosition, CoarseHandStateEstimator.Estimate(in missingWorld, settings).unknownReason, "missing world position");

        var degenerate = Evidence(CanonicalHandSide.Left, 113, 2.3, Geometry.Open, 1f, 1f);
        degenerate.elbow.position = degenerate.wrist.position;
        Equal(CoarseHandUnknownReason.DegenerateScale, CoarseHandStateEstimator.Estimate(in degenerate, settings).unknownReason, "degenerate forearm scale");

        var nonFinite = Evidence(CanonicalHandSide.Left, 114, 2.4, Geometry.Open, 1f, 1f);
        nonFinite.index.position.x = float.NaN;
        Equal(CoarseHandUnknownReason.NonFiniteGeometry, CoarseHandStateEstimator.Estimate(in nonFinite, settings).unknownReason, "non-finite rejected");
    }

    private static void TestScaleAndSides(CoarseHandEstimatorSettings settings)
    {
        var smallOpen = Evidence(CanonicalHandSide.Left, 120, 3.0, Geometry.Open, 0.6f, 1f);
        var largeOpen = Evidence(CanonicalHandSide.Left, 121, 3.1, Geometry.Open, 1.8f, 1f);
        Equal(CoarseHandState.Open, CoarseHandStateEstimator.Estimate(in smallOpen, settings).state, "small scale open");
        Equal(CoarseHandState.Open, CoarseHandStateEstimator.Estimate(in largeOpen, settings).state, "large scale open");

        var tracker = new CoarseHandStateTracker();
        var frame = new CoarseHandFrame();
        var left1 = Evidence(CanonicalHandSide.Left, 130, 4.00, Geometry.Open, 1f, 1f);
        var right1 = Evidence(CanonicalHandSide.Right, 130, 4.00, Geometry.Closed, 1f, 1f);
        tracker.Update("test", in left1, in right1, 4.00, settings, frame);
        var left2 = Evidence(CanonicalHandSide.Left, 131, 4.03, Geometry.Open, 1f, 1f);
        var right2 = Evidence(CanonicalHandSide.Right, 131, 4.03, Geometry.Closed, 1f, 1f);
        tracker.Update("test", in left2, in right2, 4.03, settings, frame);
        Equal(CoarseHandState.Open, frame.Left.state, "left independent open");
        Equal(CoarseHandState.Closed, frame.Right.state, "right independent closed");
    }

    private static void TestTemporal(CoarseHandEstimatorSettings settings)
    {
        var tracker = new CoarseHandStateTracker();
        var frame = new CoarseHandFrame();

        var open1 = Evidence(CanonicalHandSide.Left, 200, 5.00, Geometry.Open, 1f, 1f);
        var rightUnknown1 = Missing(CanonicalHandSide.Right, 200, 5.00);
        tracker.Update("test", in open1, in rightUnknown1, 5.00, settings, frame);
        Equal(CoarseHandState.Unknown, frame.Left.state, "single sample does not acquire");

        var open2 = Evidence(CanonicalHandSide.Left, 201, 5.03, Geometry.Open, 1f, 1f);
        var rightUnknown2 = Missing(CanonicalHandSide.Right, 201, 5.03);
        tracker.Update("test", in open2, in rightUnknown2, 5.03, settings, frame);
        Equal(CoarseHandState.Open, frame.Left.state, "consistent samples acquire open");

        var closed1 = Evidence(CanonicalHandSide.Left, 202, 5.06, Geometry.Closed, 1f, 1f);
        var rightUnknown3 = Missing(CanonicalHandSide.Right, 202, 5.06);
        tracker.Update("test", in closed1, in rightUnknown3, 5.06, settings, frame);
        Equal(CoarseHandState.Open, frame.Left.state, "one contradictory sample holds stable state");

        tracker.Update("test", in closed1, in rightUnknown3, 5.07, settings, frame);
        Equal(CoarseHandState.Open, frame.Left.state, "duplicate timestamp does not confirm change");

        var closed2 = Evidence(CanonicalHandSide.Left, 203, 5.09, Geometry.Closed, 1f, 1f);
        var rightUnknown4 = Missing(CanonicalHandSide.Right, 203, 5.09);
        tracker.Update("test", in closed2, in rightUnknown4, 5.09, settings, frame);
        Equal(CoarseHandState.Closed, frame.Left.state, "fresh repeated evidence changes state");

        var lost1 = Missing(CanonicalHandSide.Left, 204, 5.12);
        var lostRight1 = Missing(CanonicalHandSide.Right, 204, 5.12);
        tracker.Update("test", in lost1, in lostRight1, 5.12, settings, frame);
        Equal(CoarseHandState.Closed, frame.Left.state, "single lost frame uses grace");

        var lost2 = Missing(CanonicalHandSide.Left, 205, 5.15);
        var lostRight2 = Missing(CanonicalHandSide.Right, 205, 5.15);
        tracker.Update("test", in lost2, in lostRight2, 5.15, settings, frame);
        Equal(CoarseHandState.Unknown, frame.Left.state, "repeated lost evidence clears state");

        var reacquire1 = Evidence(CanonicalHandSide.Left, 206, 5.18, Geometry.Open, 1f, 1f);
        var reacquireRight1 = Missing(CanonicalHandSide.Right, 206, 5.18);
        tracker.Update("test", in reacquire1, in reacquireRight1, 5.18, settings, frame);
        var reacquire2 = Evidence(CanonicalHandSide.Left, 207, 5.21, Geometry.Open, 1f, 1f);
        var reacquireRight2 = Missing(CanonicalHandSide.Right, 207, 5.21);
        tracker.Update("test", in reacquire2, in reacquireRight2, 5.21, settings, frame);
        Equal(CoarseHandState.Open, frame.Left.state, "reacquired open");

        tracker.Refresh("test", 5.60, settings, frame);
        Equal(CoarseHandState.Unknown, frame.Left.state, "stale sample clears state");
        Equal(CoarseHandUnknownReason.Stale, frame.Left.unknownReason, "stale reason");
    }

    private enum Geometry
    {
        Open,
        Closed,
        Ambiguous,
    }

    private static CoarseHandPoseEvidence Evidence(
        CanonicalHandSide side,
        long timestamp,
        double received,
        Geometry geometry,
        float scale,
        float confidence)
    {
        var forearm = 0.25f * scale;
        var wrist = Vector3.zero;
        Vector3 index;
        Vector3 pinky;
        Vector3 thumb;
        switch (geometry)
        {
            case Geometry.Closed:
                index = new Vector3(0.060f, 0.020f, 0f) * scale;
                pinky = new Vector3(0.055f, -0.015f, 0f) * scale;
                thumb = new Vector3(0.070f, 0.000f, 0f) * scale;
                break;
            case Geometry.Ambiguous:
                index = new Vector3(0.120f, 0.030f, 0f) * scale;
                pinky = new Vector3(0.105f, -0.025f, 0f) * scale;
                thumb = new Vector3(0.090f, 0.010f, 0f) * scale;
                break;
            default:
                index = new Vector3(0.160f, 0.080f, 0f) * scale;
                pinky = new Vector3(0.140f, -0.070f, 0f) * scale;
                thumb = new Vector3(0.100f, 0.010f, 0f) * scale;
                break;
        }

        return new CoarseHandPoseEvidence
        {
            side = side,
            sourceTimestampMillisec = timestamp,
            receivedAtSeconds = received,
            poseAvailable = true,
            elbow = Point(new Vector3(-forearm, 0f, 0f), confidence),
            wrist = Point(wrist, confidence),
            pinky = Point(pinky, confidence),
            index = Point(index, confidence),
            thumb = Point(thumb, confidence),
        };
    }

    private static CoarseHandPoseEvidence Missing(
        CanonicalHandSide side,
        long timestamp,
        double received)
    {
        var evidence = Evidence(side, timestamp, received, Geometry.Open, 1f, 1f);
        evidence.pinky.isTracked = false;
        return evidence;
    }

    private static CoarseHandEvidencePoint Point(Vector3 position, float confidence)
    {
        return new CoarseHandEvidencePoint
        {
            isTracked = true,
            hasPosition = true,
            position = position,
            confidence = confidence,
        };
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"FAILED: {label}; expected={expected} actual={actual}");
        }
    }
}
