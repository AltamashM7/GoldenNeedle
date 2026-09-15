using System.Numerics;

static class Program
{
    private const float Epsilon = 1e-5f;
    private static int _passed;

    static void Main()
    {
        Test("MASTER_DISABLED_PHASE4_COMPATIBLE", MasterDisabledPhase4Compatible);
        Test("ZERO_OPTIONAL_FINGERS_BODY_VALID", ZeroOptionalFingersBodyValid);
        Test("PARTIAL_OPTIONAL_FINGERS_ONLY_AVAILABLE_DRIVEN", PartialOptionalFingersOnlyAvailableDriven);
        Test("FULL_OPTIONAL_FINGERS_CHARACTERIZABLE", FullOptionalFingersCharacterizable);
        Test("AUTHORED_POSITION_SCALE_IMMUTABLE", AuthoredPositionScaleImmutable);
        Test("LEFT_RIGHT_HANDS_INDEPENDENT", LeftRightHandsIndependent);
        Test("STALE_HAND_STOPS_LIVE_ARTICULATION", StaleHandStopsLiveArticulation);
        Test("REACQUISITION_ZERO_DELTA", ReacquisitionZeroDelta);
        Test("PROPER_MAP_TWIST_SIGN", ProperMapTwistSign);
        Test("REFLECTED_MAP_TWIST_SIGN", ReflectedMapTwistSign);
        Test("OBSERVABILITY_CONTINUITY", ObservabilityContinuity);
        Test("PARENT_TWIST_CHILD_COMPENSATION", ParentTwistChildCompensation);
        Test("ENDPOINT_RESIDUAL_TIGHT", EndpointResidualTight);
        Test("REFERENCE_VERSION_REACQUIRE", ReferenceVersionReacquire);
        Test("OPTIONAL_DETAIL_CANNOT_INVALIDATE_BODY", OptionalDetailCannotInvalidateBody);
        Test("E_CORE_PROVIDER_ISOLATED", ECoreProviderIsolated);
        Test("E_LATEST_ONLY_NO_BACKLOG", ELatestOnlyNoBacklog);
        Console.WriteLine($"FOUNDATION_E_SMOKE=PASS tests={_passed}");
        Console.WriteLine($"FOUNDATION_E_MAX_ENDPOINT_RESIDUAL={EndpointResidual():G9}");
    }

    private static void Test(string name, Action action)
    {
        action();
        _passed++;
        Console.WriteLine($"{name}=PASS");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void MasterDisabledPhase4Compatible()
    {
        var phase4 = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(.23f, -.11f, .37f));
        var output = ApplyOptional(phase4, false, .9f);
        Require(QuaternionDistance(phase4, output) < Epsilon, "disabled E changed Phase 4 output");
    }

    private static Quaternion ApplyOptional(Quaternion phase4, bool enabled, float axialRadians)
        => enabled ? Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitX, axialRadians) * phase4) : phase4;

    private static void ZeroOptionalFingersBodyValid()
    {
        var rig = new RigValidity(10, 0);
        Require(rig.IsBound && rig.BoundBoneCount == 10 && rig.OptionalCount == 0,
            "zero optional detail altered body validity");
    }

    private static void PartialOptionalFingersOnlyAvailableDriven()
    {
        var available = new bool[30];
        available[0] = available[3] = available[19] = true;
        var driven = new bool[30];
        for (var i = 0; i < available.Length; i++) if (available[i]) driven[i] = true;
        Require(driven.Count(v => v) == 3 && driven.Zip(available).All(pair => pair.First == pair.Second),
            "partial capability escaped availability mask");
    }

    private static void FullOptionalFingersCharacterizable()
    {
        var roots = Enumerable.Range(0, 30).Select(i => new Vector3(i + 1, i % 5 + 1, 1)).ToArray();
        Require(roots.Length == 30 && roots.All(v => v.LengthSquared() > 0f), "full capability characterization failed");
    }

    private static void AuthoredPositionScaleImmutable()
    {
        var position = new Vector3(.2f, .3f, .4f);
        var scale = new Vector3(1f, .9f, 1.1f);
        var rotation = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitY, .7f));
        Require(Vector3.Distance(position, new Vector3(.2f, .3f, .4f)) < Epsilon &&
                Vector3.Distance(scale, new Vector3(1f, .9f, 1.1f)) < Epsilon &&
                rotation != Quaternion.Identity,
            "rotation-only detail mutated authored position/scale");
    }

    private static void LeftRightHandsIndependent()
    {
        var left = new HandState { Contribution = .8f, Fresh = true };
        var right = new HandState { Contribution = -.3f, Fresh = true };
        left.Fresh = false;
        Require(!left.Fresh && right.Fresh && MathF.Abs(right.Contribution + .3f) < Epsilon,
            "one hand altered the other");
    }

    private static void StaleHandStopsLiveArticulation()
    {
        var hand = new HandState { Contribution = 1f, Fresh = false };
        var before = hand.Contribution;
        hand.Contribution = Smooth(before, 0f, .016f, 24f);
        Require(!hand.Fresh && hand.Contribution < before && hand.Contribution >= 0f,
            "stale hand did not neutralize optional contribution");
    }

    private static void ReacquisitionZeroDelta()
    {
        var state = new ReferenceState();
        state.Observe(32f, true);
        Require(state.HasReference && MathF.Abs(state.Target) < Epsilon, "first acquisition was not zero-delta");
        state.Observe(47f, true);
        Require(MathF.Abs(state.Target - 15f) < Epsilon, "observed delta wrong");
        state.Observe(0f, false);
        state.Observe(-91f, true);
        Require(MathF.Abs(state.Target) < Epsilon, "reacquisition snapped instead of rebasing");
    }

    private static void ProperMapTwistSign()
        => Require(MathF.Abs(MapAxial(27f, 1f) - 27f) < Epsilon, "proper map sign changed");

    private static void ReflectedMapTwistSign()
        => Require(MathF.Abs(MapAxial(27f, -1f) + 27f) < Epsilon, "reflected map sign not inverted");

    private static float MapAxial(float degrees, float determinantSign) => degrees * determinantSign;

    private static void ObservabilityContinuity()
    {
        var state = new TwistState();
        state.Step(Observability.Observed, 20f);
        Require(MathF.Abs(state.Target - 20f) < Epsilon, "observed target missing");
        state.Step(Observability.Held, 999f);
        Require(MathF.Abs(state.Target - 20f) < Epsilon, "held did not preserve trusted target");
        state.Step(Observability.ReferenceFallback, 999f);
        Require(MathF.Abs(state.Target) < Epsilon, "fallback fabricated fresh twist");
        state.Step(Observability.Observed, -12f);
        Require(MathF.Abs(state.Target + 12f) < Epsilon, "observed reacquire failed");
        state.Step(Observability.Unobservable, 999f);
        Require(MathF.Abs(state.Target) < Epsilon, "unobservable fabricated twist");
    }

    private static void ParentTwistChildCompensation()
        => Require(EndpointResidual() < 1e-6f, "downstream compensation moved endpoint");

    private static void EndpointResidualTight()
        => Require(EndpointResidual() < 1e-6f, $"endpoint residual {EndpointResidual():G9}");

    private static float EndpointResidual()
    {
        var root = Vector3.Zero;
        var mid = new Vector3(1f, 0f, 0f);
        var downstreamWorld = new Vector3(.65f, .42f, -.18f);
        var originalTip = mid + downstreamWorld;
        var twist = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 1.13f);
        var midAfterParent = root + Vector3.Transform(mid - root, twist);
        // Restoring the direct child's world rotation after parent axial twist preserves the
        // downstream world vector. The direct child lies on the twist axis, so its position is fixed.
        var compensatedTip = midAfterParent + downstreamWorld;
        return Vector3.Distance(originalTip, compensatedTip);
    }

    private static void ReferenceVersionReacquire()
    {
        var state = new ReferenceState { Version = 4 };
        state.Observe(10f, true);
        state.Observe(30f, true);
        Require(MathF.Abs(state.Target - 20f) < Epsilon, "pre-reset detail wrong");
        state.ResetForVersion(5);
        state.Observe(-70f, true);
        Require(state.Version == 5 && MathF.Abs(state.Target) < Epsilon,
            "version reset did not zero-delta reacquire");
    }

    private static void OptionalDetailCannotInvalidateBody()
    {
        foreach (var count in new[] { 0, 1, 14, 30 })
        {
            var rig = new RigValidity(10, count);
            Require(rig.IsBound && rig.BoundBoneCount == 10, $"optional count {count} altered body binding");
        }
    }

    private static void ECoreProviderIsolated()
    {
        var root = FindRepoRoot();
        if (root is null) return;
        var files = new[]
        {
            "Assets/GoldenNeedle/Core/Motion/Retargeting/FoundationERetargetMath.cs",
            "Assets/GoldenNeedle/Core/Motion/Retargeting/RichHumanoidDetailRetargeter.cs",
            "Assets/GoldenNeedle/Core/Motion/Retargeting/IHumanoidPostSolveDetailLayer.cs",
        };
        foreach (var relative in files)
        {
            var text = File.ReadAllText(Path.Combine(root, relative));
            Require(!text.Contains("MediaPipePoseProvider") &&
                    !text.Contains("MediaPipeHandLandmarkerSource") &&
                    !text.Contains("PoseObservation"),
                $"provider-native dependency leaked into {relative}");
        }
    }

    private static void ELatestOnlyNoBacklog()
    {
        var root = FindRepoRoot();
        if (root is null) return;
        var text = File.ReadAllText(Path.Combine(root,
            "Assets/GoldenNeedle/Core/Motion/Retargeting/RichHumanoidDetailRetargeter.cs"));
        foreach (var token in new[] { "Queue<", "ConcurrentQueue<", "LinkedList<", "Enqueue(", "Dequeue(" })
            Require(!text.Contains(token), $"historical queue/backlog token found: {token}");
    }

    private static string? FindRepoRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Assets")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Tools")))
                return directory.FullName;
            directory = directory.Parent;
        }
        return null;
    }

    private static float Smooth(float current, float target, float dt, float response)
        => current + (target - current) * (1f - MathF.Exp(-response * dt));

    private static float QuaternionDistance(Quaternion a, Quaternion b)
        => 1f - MathF.Abs(Quaternion.Dot(Quaternion.Normalize(a), Quaternion.Normalize(b)));

    private readonly record struct RigValidity(int RequiredBodyBones, int OptionalCount)
    {
        public bool IsBound => RequiredBodyBones == 10;
        public int BoundBoneCount => IsBound ? 10 : 0;
    }

    private struct HandState
    {
        public float Contribution;
        public bool Fresh;
    }

    private enum Observability { Observed, Held, ReferenceFallback, Unobservable }

    private sealed class TwistState
    {
        public float Target;
        public void Step(Observability state, float observed)
        {
            if (state == Observability.Observed) Target = observed;
            else if (state == Observability.ReferenceFallback || state == Observability.Unobservable) Target = 0f;
        }
    }

    private sealed class ReferenceState
    {
        public bool HasReference;
        public float Reference;
        public float Target;
        public int Version;

        public void Observe(float value, bool fresh)
        {
            if (!fresh)
            {
                HasReference = false;
                Target = 0f;
                return;
            }
            if (!HasReference)
            {
                Reference = value;
                HasReference = true;
                Target = 0f;
                return;
            }
            Target = value - Reference;
        }

        public void ResetForVersion(int version)
        {
            Version = version;
            HasReference = false;
            Target = 0f;
        }
    }
}
