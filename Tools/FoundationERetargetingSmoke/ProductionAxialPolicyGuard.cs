using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ProductionAxialPolicyGuard
{
    [ModuleInitializer]
    internal static void VerifyProductionAxialPolicy()
    {
        var detailPath = Path.Combine(
            "Assets", "GoldenNeedle", "Core", "Motion", "Retargeting",
            "RichHumanoidDetailRetargeter.cs");
        var presenterPath = Path.Combine(
            "Assets", "GoldenNeedle", "Debug", "PoseTrackingSpike",
            "PoseTrackingSpikePresenter.cs");
        var scenePath = Path.Combine(
            "Assets", "GoldenNeedle", "Debug", "PoseTrackingSpike",
            "PoseTrackingSpike.unity");
        var retargeterPath = Path.Combine(
            "Assets", "GoldenNeedle", "Core", "Motion", "Retargeting",
            "HumanoidRetargeter.cs");
        var canonicalSourcePath = Path.Combine(
            "Assets", "GoldenNeedle", "Core", "Motion", "Providers", "MediaPipe",
            "MediaPipeCanonicalPoseSource.cs");

        var detail = File.ReadAllText(detailPath);
        RequireContains(
            detail,
            "[SerializeField] private bool enableRichLimbAxialDetail = false;",
            "production rich limb axial detail default is not OFF");
        RequireContains(
            detail,
            "if (enableRichLimbAxialDetail)",
            "explicit experimental axial opt-in branch is missing");
        RequireContains(
            detail,
            "ApplyRichLimbTwist(binding, Mathf.Max(0f, deltaTime));",
            "experimental axial application path is missing");
        RequireContains(
            detail,
            "ClearRichTargetsAndContributions();",
            "disabled axial path no longer clears E-owned axial state");

        var presenter = File.ReadAllText(presenterPath);
        RequireContains(
            presenter,
            "gameObject.AddComponent<RichHumanoidDetailRetargeter>();",
            "live code-owned Foundation E composition is missing");

        var scene = File.ReadAllText(scenePath);
        if (scene.Contains(
                "Assembly-CSharp::GoldenNeedle.Core.Motion.Retargeting.RichHumanoidDetailRetargeter",
                StringComparison.Ordinal) ||
            scene.Contains("enableRichLimbAxialDetail:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "live scene unexpectedly serializes Foundation E axial state; code default is no longer authoritative");
        }

        var retargeter = File.ReadAllText(retargeterPath);
        var phase4Index = retargeter.IndexOf(
            "ApplyMotionFrame(\n                sourceFrame,",
            StringComparison.Ordinal);
        var detailIndex = retargeter.IndexOf(
            "ApplyPostSolveDetailLayers(deltaTime);",
            phase4Index < 0 ? 0 : phase4Index,
            StringComparison.Ordinal);
        if (phase4Index < 0 || detailIndex < 0 || phase4Index > detailIndex)
        {
            throw new InvalidOperationException(
                "Phase 4 no longer rebuilds the body solve before optional Foundation E detail");
        }

        var canonicalSource = File.ReadAllText(canonicalSourcePath);
        RequireContains(
            canonicalSource,
            "[SerializeField] private bool enableDetailedHands = false;",
            "Batch 1 detailed-hand production default regressed");

        Console.WriteLine("FOUNDATION_E_PRODUCTION_AXIAL_DEFAULT_OFF=PASS");
        Console.WriteLine("FOUNDATION_E_EXPERIMENTAL_AXIAL_OPT_IN_AVAILABLE=PASS");
        Console.WriteLine("FOUNDATION_E_PHASE4_REBASE_BEFORE_DETAIL=PASS");
        Console.WriteLine("FOUNDATION_D_DETAILED_HAND_DEFAULT_OFF_PRESERVED=PASS");
    }

    private static void RequireContains(string text, string marker, string message)
    {
        if (!text.Contains(marker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message);
        }
    }
}
