using GoldenNeedle.Core.Commands;
using GoldenNeedle.Gameplay.Commands;
using NUnit.Framework;
using System.Linq;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class GameplayCommandBridgeTests
    {
        [Test]
        public void ProductionSpeechConfigurationContainsOnlyApprovedGameplayPhrases()
        {
            var bindings = GameplaySpeechCommandConfiguration.CreateProduction();
            Assert.That(bindings.Length, Is.EqualTo(15));
            Assert.That(bindings.Select(b => b.Phrase).Distinct().Count(), Is.EqualTo(bindings.Length));
            Assert.That(bindings.Any(b => b.Phrase == "game view"), Is.False);
        }
        [TestCase("reduce latency")]
        [TestCase("low latency mode")]
        public void RawAliasesResolveToRawAvatarPresentation(string phrase)
        {
            var binding = GameplaySpeechCommandConfiguration.CreateProduction().Single(b => b.Phrase == phrase);
            Assert.That(binding.Request.Id, Is.EqualTo("game.SetRawAvatarPresentation"));
        }

        [TestCase("smooth motion")]
        [TestCase("stabilized mode")]
        public void SmoothAliasesResolveToStabilizedAvatarPresentation(string phrase)
        {
            var binding = GameplaySpeechCommandConfiguration.CreateProduction().Single(b => b.Phrase == phrase);
            Assert.That(binding.Request.Id, Is.EqualTo("game.SetStabilizedAvatarPresentation"));
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
                    GameplayCommandContext.Calibration,
                    GoldenNeedleCommand.SelectCameraViewPreset),
                Is.False);
            Assert.That(
                GameplayCommandContextPolicy.IsAllowed(
                    GameplayCommandContext.Hub,
                    GoldenNeedleCommand.SelectCameraViewPreset),
                Is.True);
            Assert.That(
                GameplayCommandContextPolicy.IsAllowed(
                    GameplayCommandContext.Activity,
                    GoldenNeedleCommand.SelectCameraViewPreset),
                Is.False);

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
        public void GameplayCommandHostRaisesCommandProcessedOnceAfterRememberingResult()
        {
            var hostObject = new GameObject("GameplayCommandHostTest");
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<GameplayCommandHost>();
            host.SetSpeechEnabled(false);

            var eventCount = 0;
            GoldenNeedleCommandResult observed = default;
            host.CommandProcessed += result =>
            {
                eventCount++;
                observed = result;
                Assert.That(host.LastResult.Request.command, Is.EqualTo(result.Request.command));
                Assert.That(host.LastResult.Status, Is.EqualTo(result.Status));
            };

            hostObject.SetActive(true);
            var result = host.Execute(GoldenNeedleCommand.BeginCalibration);

            Assert.That(result.Status, Is.EqualTo(GoldenNeedleCommandResultStatus.MissingTarget));
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(observed.Request.command, Is.EqualTo(GoldenNeedleCommand.BeginCalibration));
            Assert.That(host.LastResult.Message, Is.EqualTo(observed.Message));

            Object.DestroyImmediate(hostObject);
        }

    }
}
