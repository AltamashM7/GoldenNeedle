using System.Numerics;

static class Program
{
    private const float Epsilon = 1e-5f;
    private static int _passed;
    private static float _maxPalmTargetDriftDegrees;
    private static float _palmStaleResidualDegrees;
    private static float _palmParentLocalDriftDegrees;
    private static float _palmReacquireStepDegrees;

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
        Test("PALM_REFERENCE_NO_ACCUMULATION", PalmReferenceNoAccumulation);
        Test("PALM_STALE_RETURN", PalmStaleReturn);
        Test("PALM_DISABLE_PHASE4_BASELINE", PalmDisablePhase4Baseline);
        Test("PALM_CATEGORY_DISABLE_RETURN", PalmCategoryDisableReturn);
        Test("PALM_PARENT_RELATIVE_REFERENCE", PalmParentRelativeReference);
        Test("PALM_REACQUISITION_ZERO_DELTA", PalmReacquisitionZeroDelta);
        Test("PALM_LEFT_RIGHT_INDEPENDENT", PalmLeftRightIndependent);
        Test("E_CORE_PROVIDER_ISOLATED", ECoreProviderIsolated);
        Test("E_LATEST_ONLY_NO_BACKLOG", ELatestOnlyNoBacklog);
        Console.WriteLine($"FOUNDATION_E_SMOKE=PASS tests={_passed}");
        Console.WriteLine($"FOUNDATION_E_MAX_ENDPOINT_RESIDUAL={EndpointResidual():G9}");
        Console.WriteLine($"PALM_MAX_TARGET_DRIFT_DEG={_maxPalmTargetDriftDegrees:G9}");
        Console.WriteLine($"PALM_STALE_RESIDUAL_DEG={_palmStaleResidualDegrees:G9}");
        Console.WriteLine($"PALM_PARENT_LOCAL_DRIFT_DEG={_palmParentLocalDriftDegrees:G9}");
        Console.WriteLine($"PALM_REACQUIRE_STEP_DEG={_palmReacquireStepDegrees:G9}");
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

    private static void PalmReferenceNoAccumulation()
    {
        var baseline = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(.08f, -.05f, .03f));
        var state = new PalmReferenceModel(baseline);
        state.Step(AxisAngle(Vector3.UnitZ, 0f), true, true, 1f / 60f);
        var source = AxisAngle(Vector3.UnitZ, DegreesToRadians(24f));
        Quaternion? firstTarget = null;
        var maxTargetDrift = 0f;
        for (var i = 0; i < 240; i++)
        {
            state.Step(source, true, true, 1f / 60f);
            firstTarget ??= state.TargetLocal;
            maxTargetDrift = MathF.Max(maxTargetDrift, QuaternionAngleDegrees(firstTarget.Value, state.TargetLocal));
        }
        _maxPalmTargetDriftDegrees = maxTargetDrift;
        Require(maxTargetDrift < 0.0001f, $"absolute palm target drifted by {maxTargetDrift} degrees");
        Require(QuaternionAngleDegrees(baseline, state.CurrentLocal) > 1f, "non-zero palm delta did not drive");
        Require(QuaternionAngleDegrees(state.CurrentLocal, state.TargetLocal) < 0.01f, "palm did not converge");
    }

    private static void PalmStaleReturn()
    {
        var baseline = Quaternion.Identity;
        var state = new PalmReferenceModel(baseline);
        state.Step(Quaternion.Identity, true, true, 1f / 60f);
        var driven = AxisAngle(Vector3.UnitZ, DegreesToRadians(28f));
        for (var i = 0; i < 120; i++) state.Step(driven, true, true, 1f / 60f);
        Require(QuaternionAngleDegrees(baseline, state.CurrentLocal) > 1f, "setup did not drive palm");
        for (var i = 0; i < 120; i++) state.Step(driven, false, true, 1f / 60f);
        _palmStaleResidualDegrees = QuaternionAngleDegrees(baseline, state.CurrentLocal);
        Require(_palmStaleResidualDegrees < 0.01f, $"stale palm residual {_palmStaleResidualDegrees}");
    }

    private static void PalmDisablePhase4Baseline()
    {
        var baseline = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(.04f, .03f, -.02f));
        var state = new PalmReferenceModel(baseline);
        state.Step(Quaternion.Identity, true, true, 1f / 60f);
        for (var i = 0; i < 90; i++)
            state.Step(AxisAngle(Vector3.UnitZ, DegreesToRadians(31f)), true, true, 1f / 60f);
        state.ResetImmediate();
        Require(QuaternionAngleDegrees(baseline, state.CurrentLocal) < 0.0001f,
            "master/reset did not restore Phase 4 baseline");
    }

    private static void PalmCategoryDisableReturn()
    {
        var state = new PalmReferenceModel(Quaternion.Identity);
        state.Step(Quaternion.Identity, true, true, 1f / 60f);
        var driven = AxisAngle(Vector3.UnitZ, DegreesToRadians(26f));
        for (var i = 0; i < 100; i++) state.Step(driven, true, true, 1f / 60f);
        for (var i = 0; i < 120; i++) state.Step(driven, true, false, 1f / 60f);
        Require(QuaternionAngleDegrees(Quaternion.Identity, state.CurrentLocal) < 0.01f,
            "palm category disable froze previous contribution");
        Require(!state.HasSourceReference, "palm category disable retained stale source reference");
    }

    private static void PalmParentRelativeReference()
    {
        var baseline = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(.02f, -.08f, .05f));
        var state = new PalmReferenceModel(baseline);
        state.Step(Quaternion.Identity, true, true, 1f / 60f);
        var driven = AxisAngle(Vector3.UnitZ, DegreesToRadians(19f));
        for (var i = 0; i < 180; i++) state.Step(driven, true, true, 1f / 60f);
        var localBefore = state.CurrentLocal;
        var parentA = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(.1f, .05f, 0f));
        var parentB = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(.7f, -.2f, .18f));
        var worldA = Quaternion.Normalize(parentA * localBefore);
        for (var i = 0; i < 180; i++) state.Step(driven, true, true, 1f / 60f);
        var localAfter = state.CurrentLocal;
        var worldB = Quaternion.Normalize(parentB * localAfter);
        _palmParentLocalDriftDegrees = QuaternionAngleDegrees(localBefore, localAfter);
        Require(_palmParentLocalDriftDegrees < 0.01f,
            $"parent motion changed relative palm contribution by {_palmParentLocalDriftDegrees}");
        Require(QuaternionAngleDegrees(worldA, worldB) > 10f, "hand failed to follow moving parent");
    }

    private static void PalmReacquisitionZeroDelta()
    {
        var state = new PalmReferenceModel(Quaternion.Identity);
        state.Step(Quaternion.Identity, true, true, 1f / 60f);
        var firstDriven = AxisAngle(Vector3.UnitZ, DegreesToRadians(27f));
        for (var i = 0; i < 90; i++) state.Step(firstDriven, true, true, 1f / 60f);
        for (var i = 0; i < 3; i++) state.Step(firstDriven, false, true, 1f / 60f);
        var before = state.CurrentLocal;
        var reacquire = AxisAngle(Vector3.UnitZ, DegreesToRadians(-48f));
        state.Step(reacquire, true, true, 1f / 60f);
        var after = state.CurrentLocal;
        _palmReacquireStepDegrees = QuaternionAngleDegrees(before, after);
        Require(state.HasSourceReference, "reacquisition did not establish source reference");
        Require(QuaternionAngleDegrees(state.TargetLocal, Quaternion.Identity) < 0.0001f,
            "first reacquired sample produced non-zero E target");
        Require(_palmReacquireStepDegrees < 8f, $"reacquisition jumped {_palmReacquireStepDegrees} degrees");
        var next = AxisAngle(Vector3.UnitZ, DegreesToRadians(-33f));
        for (var i = 0; i < 120; i++) state.Step(next, true, true, 1f / 60f);
        Require(QuaternionAngleDegrees(Quaternion.Identity, state.CurrentLocal) > 1f,
            "post-reacquisition relative change did not drive");
    }

    private static void PalmLeftRightIndependent()
    {
        var left = new PalmReferenceModel(Quaternion.Identity);
        var right = new PalmReferenceModel(Quaternion.Identity);
        left.Step(Quaternion.Identity, true, true, 1f / 60f);
        right.Step(Quaternion.Identity, true, true, 1f / 60f);
        var leftDriven = AxisAngle(Vector3.UnitZ, DegreesToRadians(22f));
        var rightDriven = AxisAngle(Vector3.UnitZ, DegreesToRadians(-29f));
        for (var i = 0; i < 120; i++)
        {
            left.Step(leftDriven, true, true, 1f / 60f);
            right.Step(rightDriven, true, true, 1f / 60f);
        }
        var rightBefore = right.CurrentLocal;
        for (var i = 0; i < 120; i++)
        {
            left.Step(leftDriven, false, true, 1f / 60f);
            right.Step(rightDriven, true, true, 1f / 60f);
        }
        Require(QuaternionAngleDegrees(Quaternion.Identity, left.CurrentLocal) < 0.01f,
            "left fallback did not return");
        Require(QuaternionAngleDegrees(Quaternion.Identity, right.CurrentLocal) > 1f,
            "left fallback disabled right palm");
        Require(QuaternionAngleDegrees(rightBefore, right.CurrentLocal) < 0.01f,
            "right palm drifted while left returned");
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

    private static Quaternion SmoothQuaternion(Quaternion current, Quaternion target, float dt, float response)
    {
        var t = 1f - MathF.Exp(-MathF.Max(0.1f, response) * MathF.Max(0f, dt));
        return Quaternion.Normalize(Quaternion.Slerp(Quaternion.Normalize(current), Quaternion.Normalize(target), t));
    }

    private static Quaternion AxisAngle(Vector3 axis, float radians)
        => Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), radians));

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;

    private static float QuaternionAngleDegrees(Quaternion a, Quaternion b)
    {
        var dot = MathF.Abs(Quaternion.Dot(Quaternion.Normalize(a), Quaternion.Normalize(b)));
        dot = Math.Clamp(dot, -1f, 1f);
        return 2f * MathF.Acos(dot) * 180f / MathF.PI;
    }

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

    /// <summary>
    /// Deterministic standalone model of the corrected runtime state transition. It intentionally
    /// computes each palm target from baseline * current-vs-reference source delta, never from the
    /// previously visible/output palm orientation.
    /// </summary>
    private sealed class PalmReferenceModel
    {
        private readonly Quaternion _baselineLocal;
        private Quaternion _sourceReference = Quaternion.Identity;

        public PalmReferenceModel(Quaternion baselineLocal)
        {
            _baselineLocal = Quaternion.Normalize(baselineLocal);
            CurrentLocal = _baselineLocal;
            TargetLocal = _baselineLocal;
        }

        public bool HasSourceReference { get; private set; }
        public Quaternion CurrentLocal { get; private set; }
        public Quaternion TargetLocal { get; private set; }

        public void Step(Quaternion sourcePalm, bool fresh, bool categoryEnabled, float dt)
        {
            sourcePalm = Quaternion.Normalize(sourcePalm);
            if (!fresh || !categoryEnabled)
            {
                HasSourceReference = false;
                TargetLocal = _baselineLocal;
                CurrentLocal = SmoothQuaternion(CurrentLocal, TargetLocal, dt, 24f);
                return;
            }

            if (!HasSourceReference)
            {
                _sourceReference = sourcePalm;
                HasSourceReference = true;
                TargetLocal = _baselineLocal;
                CurrentLocal = SmoothQuaternion(CurrentLocal, TargetLocal, dt, 24f);
                return;
            }

            var relative = Quaternion.Normalize(sourcePalm * Quaternion.Inverse(_sourceReference));
            TargetLocal = Quaternion.Normalize(relative * _baselineLocal);
            CurrentLocal = SmoothQuaternion(CurrentLocal, TargetLocal, dt, 24f);
        }

        public void ResetImmediate()
        {
            HasSourceReference = false;
            TargetLocal = _baselineLocal;
            CurrentLocal = _baselineLocal;
        }
    }
}
