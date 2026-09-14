using System;
using GoldenNeedle.Core.Commands;
using NUnit.Framework;

namespace GoldenNeedle.Tests
{
    public sealed class FoundationACommandSystemTests
    {
        [Test]
        public void PhraseNormalizationTrimsCollapsesWhitespaceAndIgnoresCase()
        {
            Assert.That(
                SpeechCommandResolver.NormalizePhrase("  BeGiN\t  CALIBRATION \n"),
                Is.EqualTo("begin calibration"));
        }

        [Test]
        public void WakePrefixBuildsDeterministicEffectivePhrase()
        {
            Assert.That(
                SpeechCommandResolver.BuildEffectivePhrase("  Golden Needle ", " Game   View "),
                Is.EqualTo("golden needle game view"));
        }

        [Test]
        public void WakePrefixIsRequiredDuringResolutionWhenConfigured()
        {
            var configuration = Config(
                new SpeechCommandMapping("game view", GoldenNeedleCommand.SetGamePresentation));
            configuration.wakePrefix = "Golden Needle";
            var resolver = new SpeechCommandResolver(configuration);

            Assert.That(
                resolver.TryResolve(
                    "game view",
                    SpeechRecognitionConfidence.Medium,
                    out _,
                    out _),
                Is.False);
            Assert.That(
                resolver.TryResolve(
                    "  GOLDEN   NEEDLE  game view ",
                    SpeechRecognitionConfidence.Medium,
                    out var request,
                    out _),
                Is.True);
            Assert.That(request.command, Is.EqualTo(GoldenNeedleCommand.SetGamePresentation));
        }

        [Test]
        public void DuplicateEffectivePhraseMakesConfigurationInvalid()
        {
            var configuration = Config(
                new SpeechCommandMapping("game view", GoldenNeedleCommand.SetGamePresentation),
                new SpeechCommandMapping(" GAME   VIEW ", GoldenNeedleCommand.SetLabPresentation));

            var resolver = new SpeechCommandResolver(configuration);

            Assert.That(resolver.IsValid, Is.False);
            Assert.That(resolver.ValidationError, Does.Contain("Ambiguous speech phrase"));
        }

        [Test]
        public void PerMappingConfidenceRejectsBelowThreshold()
        {
            var configuration = Config(
                new SpeechCommandMapping(
                    "recenter",
                    GoldenNeedleCommand.Recenter,
                    minimumConfidence: SpeechRecognitionConfidence.High));
            var resolver = new SpeechCommandResolver(configuration);

            Assert.That(
                resolver.TryResolve(
                    "recenter",
                    SpeechRecognitionConfidence.Medium,
                    out _,
                    out var rejection),
                Is.False);
            Assert.That(rejection, Does.Contain("below mapping minimum"));
        }

        [Test]
        public void CommandParameterIsPreservedThroughResolutionAndRouting()
        {
            var configuration = Config(
                new SpeechCommandMapping(
                    "use usb camera",
                    GoldenNeedleCommand.SelectCaptureCameraDevice,
                    "USB Camera"));
            var resolver = new SpeechCommandResolver(configuration);
            string selected = null;
            var router = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
            {
                SelectCaptureCameraDevice = value =>
                {
                    selected = value;
                    return true;
                },
            });

            Assert.That(
                resolver.TryResolve(
                    "use usb camera",
                    SpeechRecognitionConfidence.Medium,
                    out var request,
                    out _),
                Is.True);
            var result = router.Execute(request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(request.parameter, Is.EqualTo("USB Camera"));
            Assert.That(selected, Is.EqualTo("USB Camera"));
        }

        [Test]
        public void CameraPresetCommandUsesSharedParameterizedTarget()
        {
            string selected = null;
            var router = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
            {
                SelectCameraViewPreset = value =>
                {
                    selected = value;
                    return true;
                },
            });

            var result = router.Execute(new GoldenNeedleCommandRequest(
                GoldenNeedleCommand.SelectCameraViewPreset,
                "  Hands  "));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(selected, Is.EqualTo("Hands"));
        }

        [Test]
        public void CameraPresetCommandRejectsBlankRejectedAndMissingTargetsSafely()
        {
            var rejectedRouter = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
            {
                SelectCameraViewPreset = _ => false,
            });
            var blank = rejectedRouter.Execute(new GoldenNeedleCommandRequest(
                GoldenNeedleCommand.SelectCameraViewPreset,
                "   "));
            var unknown = rejectedRouter.Execute(new GoldenNeedleCommandRequest(
                GoldenNeedleCommand.SelectCameraViewPreset,
                "Unknown"));

            Func<string, bool> temporarySelector = _ => true;
            GoldenNeedleCommandRuntimeServices.RegisterCameraViewPresetSelector(temporarySelector);
            GoldenNeedleCommandRuntimeServices.UnregisterCameraViewPresetSelector(temporarySelector);
            var missingRouter = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets());
            var missing = missingRouter.Execute(new GoldenNeedleCommandRequest(
                GoldenNeedleCommand.SelectCameraViewPreset,
                "Hands"));

            Assert.That(blank.Status, Is.EqualTo(GoldenNeedleCommandResultStatus.Rejected));
            Assert.That(unknown.Status, Is.EqualTo(GoldenNeedleCommandResultStatus.Rejected));
            Assert.That(missing.Status, Is.EqualTo(GoldenNeedleCommandResultStatus.MissingTarget));
        }

        [Test]
        public void CameraPresetRuntimeServiceCanSupplyTheExistingPrimaryCameraAuthority()
        {
            string selected = null;
            Func<string, bool> selector = value =>
            {
                selected = value;
                return true;
            };
            GoldenNeedleCommandRuntimeServices.RegisterCameraViewPresetSelector(selector);
            try
            {
                var router = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets());
                var result = router.Execute(new GoldenNeedleCommandRequest(
                    GoldenNeedleCommand.SelectCameraViewPreset,
                    "LeftHand"));

                Assert.That(result.Succeeded, Is.True);
                Assert.That(selected, Is.EqualTo("LeftHand"));
            }
            finally
            {
                GoldenNeedleCommandRuntimeServices.UnregisterCameraViewPresetSelector(selector);
            }
        }

        [Test]
        public void DefaultSpeechConfigurationIncludesFoundationBCameraPresets()
        {
            var resolver = new SpeechCommandResolver(SpeechCommandConfiguration.CreateDefault());

            Assert.That(
                resolver.TryResolve(
                    "hands view",
                    SpeechRecognitionConfidence.Medium,
                    out var request,
                    out _),
                Is.True);
            Assert.That(request.command, Is.EqualTo(GoldenNeedleCommand.SelectCameraViewPreset));
            Assert.That(request.parameter, Is.EqualTo("Hands"));
        }

        [Test]
        public void RouterUsesSharedTargetsAndReportsMissingTargetsSafely()
        {
            var calibrationCalls = 0;
            var router = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
            {
                BeginCalibration = () => calibrationCalls++,
            });

            var executed = router.Execute(
                new GoldenNeedleCommandRequest(GoldenNeedleCommand.BeginCalibration));
            var missing = router.Execute(
                new GoldenNeedleCommandRequest(GoldenNeedleCommand.Recenter));

            Assert.That(executed.Succeeded, Is.True);
            Assert.That(calibrationCalls, Is.EqualTo(1));
            Assert.That(missing.Status, Is.EqualTo(GoldenNeedleCommandResultStatus.MissingTarget));
        }

        [Test]
        public void SpeechCooldownBlocksDuplicateSpeechButDoesNotBelongToRouter()
        {
            var now = 10d;
            var calls = 0;
            var configuration = Config(
                new SpeechCommandMapping("recenter", GoldenNeedleCommand.Recenter));
            configuration.commandCooldownSeconds = 1f;
            var provider = new FakeSpeechProvider();
            var router = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
            {
                Recenter = () => calls++,
            });
            using var input = new SpeechCommandInput(
                configuration,
                router,
                _ => provider,
                () => now);

            Assert.That(input.Start(), Is.True);
            provider.Emit("recenter", SpeechRecognitionConfidence.Medium);
            provider.Emit("recenter", SpeechRecognitionConfidence.Medium);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(input.LastRejectionReason, Does.Contain("cooldown"));

            var keyboardResult = router.Execute(
                new GoldenNeedleCommandRequest(GoldenNeedleCommand.Recenter));
            Assert.That(keyboardResult.Succeeded, Is.True);
            Assert.That(calls, Is.EqualTo(2));

            now = 11.1d;
            provider.Emit("recenter", SpeechRecognitionConfidence.Medium);
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void UnsupportedProviderDisablesGracefullyWithoutDispatch()
        {
            var calls = 0;
            var provider = new FakeSpeechProvider { Supported = false };
            var input = new SpeechCommandInput(
                Config(new SpeechCommandMapping("recenter", GoldenNeedleCommand.Recenter)),
                new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
                {
                    Recenter = () => calls++,
                }),
                _ => provider,
                () => 0d);

            Assert.That(input.Start(), Is.False);
            Assert.That(calls, Is.Zero);
            Assert.That(input.LastRejectionReason, Is.Not.Empty);
            input.Dispose();
        }

        private static SpeechCommandConfiguration Config(params SpeechCommandMapping[] mappings)
        {
            return new SpeechCommandConfiguration
            {
                speechEnabled = true,
                commandCooldownSeconds = 0.75f,
                wakePrefix = string.Empty,
                mappings = mappings,
            };
        }

        private sealed class FakeSpeechProvider : ISpeechInputProvider
        {
            public event Action<SpeechRecognitionSample> PhraseRecognized;
            public string BackendName => "Fake";
            public string Status => IsRunning ? "Listening" : Supported ? "Ready" : "Unsupported";
            public string LastError => Supported ? string.Empty : "Unsupported fake backend";
            public bool Supported { get; set; } = true;
            public bool IsSupported => Supported;
            public bool IsRunning { get; private set; }

            public bool Start()
            {
                IsRunning = Supported;
                return IsRunning;
            }

            public void Stop()
            {
                IsRunning = false;
            }

            public void Dispose()
            {
                IsRunning = false;
            }

            public void Emit(string phrase, SpeechRecognitionConfidence confidence)
            {
                PhraseRecognized?.Invoke(new SpeechRecognitionSample(phrase, confidence));
            }
        }
    }
}
