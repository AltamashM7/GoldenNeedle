using GoldenNeedle.Editor.Gameplay;
using NUnit.Framework;

namespace GoldenNeedle.Tests.Editor
{
    public sealed class GameplayPlayerPrefabBuilderTests
    {
        [Test]
        public void ExactRequiredCounts_AcceptsOneOfEveryRequiredComponent()
        {
            var counts = new GameplayPlayerRequiredComponentCounts(
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1);

            Assert.That(
                GameplayPlayerPrefabBuilder.ValidateExactRequiredComponentCounts(counts, out var error),
                Is.True,
                error);
            Assert.That(error, Is.Empty);
        }

        [Test]
        public void ExactRequiredCounts_RejectsMissingOrDuplicateComponents()
        {
            var counts = new GameplayPlayerRequiredComponentCounts(
                1, 1, 0, 1, 2, 1, 1, 1, 1, 1);

            Assert.That(
                GameplayPlayerPrefabBuilder.ValidateExactRequiredComponentCounts(counts, out var error),
                Is.False);
            StringAssert.Contains("MotionEngineRuntime", error);
            StringAssert.Contains("HumanoidRetargeter", error);
        }

        [Test]
        public void DebugClassification_IsExplicitAndRejectsUnknownPoseTrackingSpikeDebugTypes()
        {
            const string known = "GoldenNeedle.Debug.PoseTrackingSpike.PoseTrackingSpikePresenter";
            const string unknownDebug = "GoldenNeedle.Debug.PoseTrackingSpike.FutureLabOnlyView";
            const string production = "GoldenNeedle.Core.Motion.Runtime.MotionEngineRuntime";

            Assert.That(GameplayPlayerPrefabBuilder.IsKnownDebugComponentTypeName(known), Is.True);
            Assert.That(GameplayPlayerPrefabBuilder.IsKnownDebugComponentTypeName(unknownDebug), Is.False);
            Assert.That(GameplayPlayerPrefabBuilder.IsPoseTrackingSpikeDebugTypeName(unknownDebug), Is.True);
            Assert.That(GameplayPlayerPrefabBuilder.IsPoseTrackingSpikeDebugTypeName(production), Is.False);
        }
    }
}
