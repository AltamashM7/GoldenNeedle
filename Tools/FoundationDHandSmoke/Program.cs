using GoldenNeedle.Core.Motion.Hands;
using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using UnityEngine;

static class Program
{
    static void Main()
    {
        Assert(Enum.GetValues<HandLandmarkId>().Length == 21, "21 semantics");
        Assert((int)HandLandmarkId.Wrist == 0 && (int)HandLandmarkId.PinkyTip == 20, "stable numeric meanings");
        TestSharedProviderTimeline();
        TestBasisAndFeatures();
        TestAssociation();
        TestFreshnessAndPartial();
        TestScheduler();
        Console.WriteLine("FOUNDATION_D_HAND_SMOKE=PASS");
        Console.WriteLine("SHARED_PROVIDER_TIMELINE_SURFACE=PASS");
        Console.WriteLine("SEMANTIC_CONTRACT=PASS");
        Console.WriteLine("PALM_AND_ARTICULATION=PASS");
        Console.WriteLine("ASSOCIATION_AND_FRESHNESS=PASS");
        Console.WriteLine("BOUNDED_SCHEDULER=PASS");
    }

    static void TestSharedProviderTimeline()
    {
        var provider = new MediaPipePoseProvider();
        Assert(MediaPipePoseProviderTimeline.TryGetTimelineClock(provider, out var clock), "provider clock exposed read-only");
        var firstMs = provider.CurrentTimelineMilliseconds();
        var firstSeconds = provider.CurrentTimelineSeconds();
        Thread.Sleep(5);
        var secondMs = provider.CurrentTimelineMilliseconds();
        var secondSeconds = provider.CurrentTimelineSeconds();
        Assert(secondMs >= firstMs, "provider milliseconds monotonic");
        Assert(secondSeconds >= firstSeconds, "provider seconds monotonic");
        Assert(Math.Abs(secondSeconds - clock.ElapsedTicks / (double)System.Diagnostics.Stopwatch.Frequency) < .02d, "same Stopwatch epoch");
    }

    static void TestBasisAndFeatures()
    {
        var open = BuildHand(false);
        HandFeatureSolver.PopulateDerivedFeatures(open);
        Assert(open.palmBasis.isValid, "palm basis valid");
        Assert(MathF.Abs(open.palmBasis.primaryAxis.magnitude - 1f) < .001f, "primary normalized");
        Assert(MathF.Abs(Vector3.Dot(open.palmBasis.primaryAxis, open.palmBasis.secondaryAxis)) < .001f, "basis orthogonal");
        Assert(open.palmBasis.determinant > .98f, "right handed");
        Assert(open.indexCurl.isValid && open.indexCurl.value < .2f, "extended low curl");
        Assert(open.shape == HandShapeState.Open, "open state");
        Assert(open.pointing.isKnown && !open.pointing.value, "open not pointing");

        var fist = BuildHand(true);
        HandFeatureSolver.PopulateDerivedFeatures(fist);
        Assert(fist.indexCurl.isValid && fist.indexCurl.value > .7f, "bent high curl");
        Assert(fist.shape == HandShapeState.Fist, "fist state");

        var pointing = BuildHand(true);
        SetFinger(pointing, HandLandmarkId.IndexMcp, HandLandmarkId.IndexPip, HandLandmarkId.IndexDip, HandLandmarkId.IndexTip, false, .08f);
        HandFeatureSolver.PopulateDerivedFeatures(pointing);
        Assert(pointing.pointing.isKnown && pointing.pointing.value, "pointing state");

        var missing = BuildHand(false);
        missing.SetLandmark(new CanonicalHandLandmark { id = HandLandmarkId.IndexDip });
        var invalid = HandFeatureSolver.ComputeFingerCurl(missing, HandLandmarkId.IndexMcp, HandLandmarkId.IndexPip, HandLandmarkId.IndexDip, HandLandmarkId.IndexTip);
        Assert(!invalid.isValid, "missing invalid not NaN");

        var degenerate = new CanonicalHand(CanonicalHandSide.Left);
        degenerate.Begin(CanonicalHandSide.Left, 1, 1, CanonicalHandSide.Left, 1, HandAssociationMode.HandednessFallback, 1);
        foreach (var id in new[] { HandLandmarkId.Wrist, HandLandmarkId.IndexMcp, HandLandmarkId.MiddleMcp, HandLandmarkId.PinkyMcp })
            degenerate.SetLandmark(new CanonicalHandLandmark { id = id, hasHandLocalPosition = true, handLocalPosition = Vector3.zero, confidence = 1 });
        Assert(!HandFeatureSolver.BuildPalmBasis(degenerate).isValid, "degenerate rejected");
    }

    static void TestAssociation()
    {
        var body = new BodyWristEvidence { hasLeft = true, left = new Vector2(.2f, .5f), hasRight = true, right = new Vector2(.8f, .5f), leftConfidence = 1, rightConfidence = 1 };
        var a = new HandAssociationCandidate { isValid = true, wristImagePosition = new Vector2(.21f, .5f), handednessEvidence = CanonicalHandSide.Right, handednessScore = .9f };
        var b = new HandAssociationCandidate { isValid = true, wristImagePosition = new Vector2(.79f, .5f), handednessEvidence = CanonicalHandSide.Left, handednessScore = .9f };
        HandAssociationSolver.AssociatePair(in a, in b, in body, out var ar, out var br);
        Assert(ar.side == CanonicalHandSide.Left && br.side == CanonicalHandSide.Right, "pose proximity primary");
        var leftOnlyBody = new BodyWristEvidence { hasLeft = true, left = new Vector2(.2f, .5f) };
        var one = HandAssociationSolver.AssociateSingle(in a, in leftOnlyBody);
        Assert(one.side == CanonicalHandSide.Left && one.mode == HandAssociationMode.PoseSingleWrist, "single wrist");
        var farRight = new HandAssociationCandidate { isValid = true, wristImagePosition = new Vector2(.82f, .5f), handednessEvidence = CanonicalHandSide.Right, handednessScore = .9f };
        var recovered = HandAssociationSolver.AssociateSingle(in farRight, in leftOnlyBody);
        Assert(recovered.side == CanonicalHandSide.Right && recovered.mode == HandAssociationMode.HandednessFallback, "single visible body wrist fallback");
        var noBody = default(BodyWristEvidence);
        var fallback = HandAssociationSolver.AssociateSingle(in a, in noBody);
        Assert(fallback.side == CanonicalHandSide.Right && fallback.mode == HandAssociationMode.HandednessFallback, "handedness fallback");
        var crossA = new HandAssociationCandidate { isValid = true, wristImagePosition = new Vector2(.49f, .5f), handednessEvidence = CanonicalHandSide.Left, handednessScore = .51f };
        var crossB = new HandAssociationCandidate { isValid = true, wristImagePosition = new Vector2(.51f, .5f), handednessEvidence = CanonicalHandSide.Right, handednessScore = .51f };
        HandAssociationSolver.AssociatePair(in crossA, in crossB, in body, out var ca, out var cb);
        Assert(ca.isAssigned && cb.isAssigned && ca.side != cb.side, "crossing fallback remains distinct");
    }

    static void TestFreshnessAndPartial()
    {
        var settings = HandFreshnessSettings.CreateDefault();
        var left = BuildHand(false);
        left.EvaluateFreshness(1.2, 1100, settings);
        Assert(left.isFresh, "within age/skew");
        left.EvaluateFreshness(1.6, 1100, settings);
        Assert(!left.isFresh, "stale age rejected");
        var right = BuildHand(false);
        right.Begin(CanonicalHandSide.Right, 1000, 1.0, CanonicalHandSide.Right, 1, HandAssociationMode.PoseWristProximity, 1);
        right.EvaluateFreshness(1.1, 1400, settings);
        Assert(!right.isFresh, "skew rejected");
        var frame = new CanonicalHandFrame();
        var freshRight = BuildHand(false);
        freshRight.Begin(CanonicalHandSide.Right, 1100, 1.15, CanonicalHandSide.Right, 1, HandAssociationMode.PoseWristProximity, 1);
        freshRight.EvaluateFreshness(1.2, 1100, settings);
        left.Clear(CanonicalHandSide.Left);
        frame.Begin("test", 1100, 1.2, true);
        frame.CopyHand(left);
        frame.CopyHand(freshRight);
        Assert(!frame.Left.isFresh && frame.Right.isFresh, "left/right partial independence");
        frame.Clear();
        Assert(!frame.Left.isDetected && !frame.Right.isDetected, "session clear removes samples");
    }

    static void TestScheduler()
    {
        var scheduler = new BoundedHandInferenceScheduler();
        Assert(scheduler.Offer(1, out var launch) && launch && scheduler.HasActive, "first active");
        scheduler.Offer(2, out launch);
        Assert(!launch && scheduler.HasPending && scheduler.PendingSequence == 2, "pending");
        scheduler.Offer(3, out launch);
        Assert(scheduler.PendingSequence == 3 && scheduler.ReplacedPendingCount == 1, "replace newest");
        Assert(scheduler.CompleteActive(out var next) && next == 3 && scheduler.HasActive && !scheduler.HasPending, "promote latest only");
        Assert(!scheduler.CompleteActive(out next), "no fifo remains");
    }

    static CanonicalHand BuildHand(bool bent)
    {
        var hand = new CanonicalHand(CanonicalHandSide.Left);
        hand.Begin(CanonicalHandSide.Left, 1000, 1.0, CanonicalHandSide.Left, 1, HandAssociationMode.PoseWristProximity, 1);
        Set(hand, HandLandmarkId.Wrist, Vector3.zero);
        Set(hand, HandLandmarkId.IndexMcp, new Vector3(.04f, .06f, 0));
        Set(hand, HandLandmarkId.MiddleMcp, new Vector3(0, .07f, 0));
        Set(hand, HandLandmarkId.RingMcp, new Vector3(-.025f, .06f, 0));
        Set(hand, HandLandmarkId.PinkyMcp, new Vector3(-.05f, .05f, 0));
        SetFinger(hand, HandLandmarkId.ThumbCmc, HandLandmarkId.ThumbMcp, HandLandmarkId.ThumbIp, HandLandmarkId.ThumbTip, bent, .055f);
        SetFinger(hand, HandLandmarkId.IndexMcp, HandLandmarkId.IndexPip, HandLandmarkId.IndexDip, HandLandmarkId.IndexTip, bent, .08f);
        SetFinger(hand, HandLandmarkId.MiddleMcp, HandLandmarkId.MiddlePip, HandLandmarkId.MiddleDip, HandLandmarkId.MiddleTip, bent, .085f);
        SetFinger(hand, HandLandmarkId.RingMcp, HandLandmarkId.RingPip, HandLandmarkId.RingDip, HandLandmarkId.RingTip, bent, .078f);
        SetFinger(hand, HandLandmarkId.PinkyMcp, HandLandmarkId.PinkyPip, HandLandmarkId.PinkyDip, HandLandmarkId.PinkyTip, bent, .07f);
        return hand;
    }

    static void SetFinger(CanonicalHand hand, HandLandmarkId mcp, HandLandmarkId pip, HandLandmarkId dip, HandLandmarkId tip, bool bent, float length)
    {
        var existing = hand.GetLandmark(mcp);
        var root = existing.hasHandLocalPosition ? existing.handLocalPosition : Vector3.zero;
        if (!existing.hasHandLocalPosition) Set(hand, mcp, root);
        if (!bent)
        {
            Set(hand, pip, root + new Vector3(0, length * .33f, 0));
            Set(hand, dip, root + new Vector3(0, length * .66f, 0));
            Set(hand, tip, root + new Vector3(0, length, 0));
        }
        else
        {
            Set(hand, pip, root + new Vector3(0, length * .25f, 0));
            Set(hand, dip, root + new Vector3(0, length * .15f, length * .15f));
            Set(hand, tip, root + new Vector3(0, length * .05f, 0));
        }
    }

    static void Set(CanonicalHand hand, HandLandmarkId id, Vector3 position) => hand.SetLandmark(new CanonicalHandLandmark
    {
        id = id,
        hasHandLocalPosition = true,
        handLocalPosition = position,
        hasImagePosition = true,
        imagePosition = new Vector2(.5f, .5f),
        confidence = 1,
    });

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
    }
}
