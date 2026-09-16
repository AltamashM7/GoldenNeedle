using GoldenNeedle.Core.Commands;
using GoldenNeedle.Gameplay.Commands;
using NUnit.Framework;

namespace GoldenNeedle.Tests
{
    public sealed class GameplayCommandBridgeTests
    {
        [Test]
        public void ProductionSpeechConfigurationContainsOnlyApprovedGameplayPhrases()
        {
            var resolver = new SpeechCommandResolver(
                GameplaySpeechCommandConfiguration.CreateProduction());

            Assert.That(resolver.IsValid, Is.True, resolver.ValidationError);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "begin calibration",
                    "recenter",
                    "retry tracking",
                    "reduce latency",
                    "low latency mode",
                    "smooth motion",
                    "stabilized mode",
                },
                resolver.EffectivePhrases);
            Assert.That(resolver.EffectivePhrases, Has.Count.EqualTo(7));
            CollectionAssert.DoesNotContain(resolver.EffectivePhrases, "game view");
            CollectionAssert.DoesNotContain(resolver.EffectivePhrases, "raw landmarks");
            CollectionAssert.DoesNotContain(resolver.EffectivePhrases, "hands view");
        }

        [TestCase("reduce latency")]
        [TestCase("low latency mode")]
        public void LowLatencyAliasesResolveToRawAvatarPresentation(string phrase)
        {
            var resolver = new SpeechCommandResolver(
                GameplaySpeechCommandConfiguration.CreateProduction());

            Assert.That(
                resolver.TryResolve(
                    phrase,
                    SpeechRecognitionConfidence.Low,
                    out var request,
                    out var rejection),
                Is.True,
                rejection);
            Assert.That(request.command, Is.EqualTo(GoldenNeedleCommand.SetRawAvatarPresentation));
        }

        [TestCase("smooth motion")]
        [TestCase("stabilized mode")]
        public void SmoothAliasesResolveToStabilizedAvatarPresentation(string phrase)
        {
            var resolver = new SpeechCommandResolver(
                GameplaySpeechCommandConfiguration.CreateProduction());

            Assert.That(
                resolver.TryResolve(
                    phrase,
                    SpeechRecognitionConfidence.Low,
                    out var request,
                    out var rejection),
                Is.True,
                rejection);
            Assert.That(request.command, Is.EqualTo(GoldenNeedleCommand.SetStabilizedAvatarPresentation));
        }

        [Test]
        public void ContextPolicyAllowsOnlyTheIntendedProductionCommands()
        {
            Assert.That(
                GameplayCommandContextPolicy.IsAllowed(
                    GameplayCommandContext.Calibration,
                    GoldenNeedleCommand.BeginCalibration),
                Is.True);
            Assert.That(
                GameplayCommandContextPolicy.IsAllowed(
                    GameplayCommandContext.Calibration,
                    GoldenNeedleCommand.Recenter),
                Is.False);

            Assert.That(
                GameplayCommandContextPolicy.IsAllowed(
                    GameplayCommandContext.Hub,
                    GoldenNeedleCommand.BeginCalibration),
                Is.False);
            Assert.That(
                GameplayCommandContextPolicy.IsAllowed(
                    GameplayCommandContext.Hub,
                    GoldenNeedleCommand.Recenter),
                Is.True);

            Assert.That(
                GameplayCommandContextPolicy.IsAllowed(
                    GameplayCommandContext.Activity,
                    GoldenNeedleCommand.Recenter),
                Is.True);

            foreach (var context in new[]
                     {
                         GameplayCommandContext.Calibration,
                         GameplayCommandContext.Hub,
                         GameplayCommandContext.Activity,
                     })
            {
                Assert.That(
                    GameplayCommandContextPolicy.IsAllowed(
                        context,
                        GoldenNeedleCommand.RetryTracking),
                    Is.True);
                Assert.That(
                    GameplayCommandContextPolicy.IsAllowed(
                        context,
                        GoldenNeedleCommand.SetRawAvatarPresentation),
                    Is.True);
                Assert.That(
                    GameplayCommandContextPolicy.IsAllowed(
                        context,
                        GoldenNeedleCommand.SetStabilizedAvatarPresentation),
                    Is.True);
                Assert.That(
                    GameplayCommandContextPolicy.IsAllowed(
                        context,
                        GoldenNeedleCommand.ToggleRawLandmarks),
                    Is.False);
            }
        }

        [Test]
        public void PresentationCommandsRouteToTheCorrectNarrowTarget()
        {
            var rawCalls = 0;
            var stabilizedCalls = 0;
            var router = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
            {
                SetRawAvatarPresentation = () =>
                {
                    rawCalls++;
                    return true;
                },
                SetStabilizedAvatarPresentation = () =>
                {
                    stabilizedCalls++;
                    return true;
                },
            });

            var raw = router.Execute(new GoldenNeedleCommandRequest(
                GoldenNeedleCommand.SetRawAvatarPresentation));
            var stabilized = router.Execute(new GoldenNeedleCommandRequest(
                GoldenNeedleCommand.SetStabilizedAvatarPresentation));

            Assert.That(raw.Succeeded, Is.True);
            Assert.That(stabilized.Succeeded, Is.True);
            Assert.That(rawCalls, Is.EqualTo(1));
            Assert.That(stabilizedCalls, Is.EqualTo(1));
        }

        [Test]
        public void MissingPresentationTargetsFailSafely()
        {
            var router = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets());

            var raw = router.Execute(new GoldenNeedleCommandRequest(
                GoldenNeedleCommand.SetRawAvatarPresentation));
            var stabilized = router.Execute(new GoldenNeedleCommandRequest(
                GoldenNeedleCommand.SetStabilizedAvatarPresentation));

            Assert.That(raw.Status, Is.EqualTo(GoldenNeedleCommandResultStatus.MissingTarget));
            Assert.That(stabilized.Status, Is.EqualTo(GoldenNeedleCommandResultStatus.MissingTarget));
        }

        [Test]
        public void ExistingDefaultLabSpeechConfigurationRemainsIntact()
        {
            var resolver = new SpeechCommandResolver(SpeechCommandConfiguration.CreateDefault());

            Assert.That(resolver.IsValid, Is.True, resolver.ValidationError);
            Assert.That(
                resolver.TryResolve(
                    "game view",
                    SpeechRecognitionConfidence.Low,
                    out var gameView,
                    out _),
                Is.True);
            Assert.That(gameView.command, Is.EqualTo(GoldenNeedleCommand.SetGamePresentation));
            Assert.That(
                resolver.TryResolve(
                    "hands view",
                    SpeechRecognitionConfidence.Low,
                    out var handsView,
                    out _),
                Is.True);
            Assert.That(handsView.command, Is.EqualTo(GoldenNeedleCommand.SelectCameraViewPreset));
            Assert.That(handsView.parameter, Is.EqualTo("Hands"));
            Assert.That(
                resolver.TryResolve(
                    "reduce latency",
                    SpeechRecognitionConfidence.Low,
                    out _,
                    out _),
                Is.False);
        }
    }
}
