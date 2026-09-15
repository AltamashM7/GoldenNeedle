using System;
using System.Collections.Generic;
using GoldenNeedle.Core.Commands;

internal static class Program
{
    private static int Main()
    {
        try
        {
            Equal("begin calibration", SpeechCommandResolver.NormalizePhrase("  BeGiN\t CALIBRATION  "), "normalization");
            Equal("golden needle game view", SpeechCommandResolver.BuildEffectivePhrase(" Golden Needle ", "GAME  VIEW"), "wake prefix");

            var prefixedConfiguration = Config(
                new SpeechCommandMapping("game view", GoldenNeedleCommand.SetGamePresentation));
            prefixedConfiguration.wakePrefix = "Golden Needle";
            var prefixedResolver = new SpeechCommandResolver(prefixedConfiguration);
            True(!prefixedResolver.TryResolve("game view", SpeechRecognitionConfidence.Medium, out _, out _), "wake prefix required");
            True(prefixedResolver.TryResolve(" GOLDEN  NEEDLE game view ", SpeechRecognitionConfidence.Medium, out _, out _), "wake prefix resolves");

            var duplicate = Config(
                new SpeechCommandMapping("game view", GoldenNeedleCommand.SetGamePresentation),
                new SpeechCommandMapping(" GAME  VIEW ", GoldenNeedleCommand.SetLabPresentation));
            True(!new SpeechCommandResolver(duplicate).IsValid, "duplicate phrase rejected");

            var highOnly = new SpeechCommandResolver(Config(
                new SpeechCommandMapping("recenter", GoldenNeedleCommand.Recenter, minimumConfidence: SpeechRecognitionConfidence.High)));
            True(!highOnly.TryResolve("recenter", SpeechRecognitionConfidence.Medium, out _, out _), "confidence filtering");

            string selected = null;
            var router = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
            {
                SelectCaptureCameraDevice = value => { selected = value; return true; },
            });
            var parameterResolver = new SpeechCommandResolver(Config(
                new SpeechCommandMapping("use usb camera", GoldenNeedleCommand.SelectCaptureCameraDevice, "USB Camera")));
            True(parameterResolver.TryResolve("use usb camera", SpeechRecognitionConfidence.Medium, out var cameraRequest, out _), "parameter resolve");
            True(router.Execute(cameraRequest).Succeeded, "parameter command execution");
            Equal("USB Camera", selected, "parameter preservation");

            string presetSelected = null;
            var presetRouter = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
            {
                SelectCameraViewPreset = value => { presetSelected = value; return true; },
            });
            var presetResult = presetRouter.Execute(new GoldenNeedleCommandRequest(
                GoldenNeedleCommand.SelectCameraViewPreset,
                "  Hands  "));
            True(presetResult.Succeeded, "Foundation B preset command executes through shared target");
            Equal("Hands", presetSelected, "Foundation B preset parameter trimmed");
            True(
                presetRouter.Execute(new GoldenNeedleCommandRequest(
                    GoldenNeedleCommand.SelectCameraViewPreset,
                    "   ")).Status == GoldenNeedleCommandResultStatus.Rejected,
                "blank preset rejected");

            var defaults = SpeechCommandConfiguration.CreateDefault();
            var defaultResolver = new SpeechCommandResolver(defaults);
            True(string.IsNullOrEmpty(defaults.wakePrefix), "default speech has no wake phrase");
            True(defaultResolver.TryResolve("hands view", SpeechRecognitionConfidence.Medium, out var handsRequest, out _), "default hands speech mapping resolves");
            True(handsRequest.command == GoldenNeedleCommand.SelectCameraViewPreset, "hands speech maps to preset command");
            Equal("Hands", handsRequest.parameter, "hands speech parameter");

            RawRecognitionAndPolicyDiagnostics();
            LifecycleIsIdempotentAndRestartable();
            UnsupportedProviderIsNonfatal();

            Console.WriteLine("FOUNDATION_A_COMMAND_SMOKE=PASS");
            Console.WriteLine("NORMALIZATION_WAKE_DUPLICATES=PASS");
            Console.WriteLine("CONFIDENCE_COOLDOWN=PASS");
            Console.WriteLine("PARAMETER_AND_CAMERA_PRESET_COMMAND=PASS");
            Console.WriteLine("DEFAULT_CAMERA_SPEECH_MAPPINGS=PASS");
            Console.WriteLine("RAW_RECOGNITION_DIAGNOSTICS=PASS");
            Console.WriteLine("SPEECH_LIFECYCLE_IDEMPOTENT=PASS");
            Console.WriteLine("UNSUPPORTED_SPEECH_FALLBACK=PASS");
            Console.WriteLine("SHARED_COMMAND_ROUTING=PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void RawRecognitionAndPolicyDiagnostics()
    {
        var now = 5d;
        var calls = 0;
        var fake = new FakeProvider();
        var router = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
        {
            Recenter = () => calls++,
        });
        var config = Config(new SpeechCommandMapping("recenter", GoldenNeedleCommand.Recenter));
        config.commandCooldownSeconds = 1f;
        using (var input = new SpeechCommandInput(config, router, _ => fake, () => now))
        {
            True(input.Start(), "fake provider starts");
            fake.Emit("recenter", SpeechRecognitionConfidence.Medium);
            True(calls == 1, "recognized command dispatched");
            True(input.RecognitionEventCount == 1, "raw event counted before policy");
            True(input.LastRawConfidence == SpeechRecognitionConfidence.Medium, "raw confidence retained");
            True(input.LastMappingFound, "mapping diagnostic true");
            True(input.LastDispatchAttempted, "dispatch diagnostic true");
            True(input.LastRecognitionOutcome == SpeechRecognitionOutcome.Dispatched, "dispatch outcome recorded");

            fake.Emit("recenter", SpeechRecognitionConfidence.Medium);
            True(calls == 1, "speech cooldown blocks duplicate");
            True(input.RecognitionEventCount == 2, "cooldown still counts raw recognition");
            True(input.LastCooldownRejected, "cooldown rejection classified");
            True(input.LastRecognitionOutcome == SpeechRecognitionOutcome.CooldownRejected, "cooldown outcome recorded");
            True(!input.LastDispatchAttempted, "cooldown does not attempt dispatch");

            True(router.Execute(new GoldenNeedleCommandRequest(GoldenNeedleCommand.Recenter)).Succeeded, "direct shared router remains responsive");
            True(calls == 2, "keyboard/shared route bypasses speech cooldown");

            now = 6.1d;
            fake.Emit("recenter", SpeechRecognitionConfidence.Medium);
            True(calls == 3, "speech resumes after cooldown");
        }

        var lowCalls = 0;
        var lowFake = new FakeProvider();
        using (var input = new SpeechCommandInput(
            Config(new SpeechCommandMapping(
                "recenter",
                GoldenNeedleCommand.Recenter,
                minimumConfidence: SpeechRecognitionConfidence.High)),
            new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets { Recenter = () => lowCalls++ }),
            _ => lowFake,
            () => 10d))
        {
            True(input.Start(), "confidence fake starts");
            lowFake.Emit("recenter", SpeechRecognitionConfidence.Low);
            True(input.RecognitionEventCount == 1, "low-confidence raw event visible");
            True(input.LastMappingFound, "low-confidence phrase mapped");
            True(input.LastConfidenceRejected, "low-confidence rejection classified");
            True(input.LastRecognitionOutcome == SpeechRecognitionOutcome.ConfidenceRejected, "confidence outcome recorded");
            True(!input.LastDispatchAttempted && lowCalls == 0, "confidence rejection does not dispatch");
            True(input.LastRejectionReason.Contains("minimum", StringComparison.OrdinalIgnoreCase), "minimum confidence shown diagnostically");
        }

        var unmappedFake = new FakeProvider();
        using (var input = new SpeechCommandInput(
            Config(new SpeechCommandMapping("recenter", GoldenNeedleCommand.Recenter)),
            router,
            _ => unmappedFake,
            () => 12d))
        {
            True(input.Start(), "unmapped fake starts");
            unmappedFake.Emit("not a configured phrase", SpeechRecognitionConfidence.High);
            True(input.RecognitionEventCount == 1, "unmapped raw event visible");
            True(!input.LastMappingFound, "unmapped classification recorded");
            True(input.LastRecognitionOutcome == SpeechRecognitionOutcome.Unmapped, "unmapped outcome recorded");
            True(!input.LastDispatchAttempted, "unmapped phrase does not dispatch");
        }
    }

    private static void LifecycleIsIdempotentAndRestartable()
    {
        var providers = new List<FakeProvider>();
        var calls = 0;
        var factoryCalls = 0;
        var router = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
        {
            Recenter = () => calls++,
        });
        var input = new SpeechCommandInput(
            Config(new SpeechCommandMapping("recenter", GoldenNeedleCommand.Recenter)),
            router,
            _ =>
            {
                factoryCalls++;
                var provider = new FakeProvider();
                providers.Add(provider);
                return provider;
            },
            () => 20d);

        True(input.Start(), "first lifecycle start");
        True(input.Start(), "repeated start is idempotent");
        True(factoryCalls == 1, "repeated start did not create duplicate provider");
        True(providers[0].StartCalls == 1, "repeated start did not restart provider");
        True(providers[0].SubscriberCount == 1, "repeated start did not duplicate phrase subscription");

        input.Stop();
        True(providers[0].StopCalls == 1, "stop reached provider once");
        True(providers[0].DisposeCalls == 1, "stop disposed provider resources once");
        True(providers[0].SubscriberCount == 0, "stop removed phrase callback");
        providers[0].Emit("recenter", SpeechRecognitionConfidence.High);
        True(calls == 0, "stopped input cannot dispatch stale provider event");

        True(input.Start(), "start after stop succeeds");
        True(factoryCalls == 2, "restart creates exactly one fresh provider");
        providers[1].Emit("recenter", SpeechRecognitionConfidence.High);
        True(calls == 1, "restarted provider dispatches once");

        input.Dispose();
        input.Dispose();
        True(providers[1].StopCalls == 1, "dispose stops active provider once");
        True(providers[1].DisposeCalls == 1, "dispose is idempotent");
        True(providers[1].SubscriberCount == 0, "dispose removes phrase callback");
    }

    private static void UnsupportedProviderIsNonfatal()
    {
        var calls = 0;
        var provider = new FakeProvider { Supported = false };
        var router = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
        {
            Recenter = () => calls++,
        });
        using (var input = new SpeechCommandInput(
            Config(new SpeechCommandMapping("recenter", GoldenNeedleCommand.Recenter)),
            router,
            _ => provider,
            () => 30d))
        {
            True(!input.Start(), "unsupported provider fails closed");
            True(input.LifecycleState == SpeechProviderLifecycleState.Unsupported, "unsupported lifecycle exposed");
            True(!string.IsNullOrEmpty(input.LastRejectionReason), "unsupported diagnostic available");
            True(calls == 0, "unsupported speech did not dispatch");
            True(router.Execute(new GoldenNeedleCommandRequest(GoldenNeedleCommand.Recenter)).Succeeded, "shared router survives speech failure");
            True(calls == 1, "shared route remains functional after speech failure");
        }
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

    private static void True(bool value, string label)
    {
        if (!value) throw new InvalidOperationException($"FAILED: {label}");
    }

    private static void Equal(string expected, string actual, string label)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"FAILED: {label}; expected='{expected}' actual='{actual}'");
        }
    }

    private sealed class FakeProvider : ISpeechInputProvider
    {
        private Action<SpeechRecognitionSample> _phraseRecognized;

        public event Action<SpeechRecognitionSample> PhraseRecognized
        {
            add
            {
                _phraseRecognized += value;
                SubscriberCount++;
            }
            remove
            {
                _phraseRecognized -= value;
                SubscriberCount = Math.Max(0, SubscriberCount - 1);
            }
        }

        public bool Supported { get; set; } = true;
        public string BackendName => "Fake";
        public string Status => IsRunning ? "Listening" : Supported ? "Ready" : "Unsupported";
        public string LastError => Supported ? string.Empty : "Unsupported fake backend";
        public bool IsSupported => Supported;
        public bool IsRunning { get; private set; }
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public int SubscriberCount { get; private set; }

        public bool Start()
        {
            StartCalls++;
            IsRunning = Supported;
            return IsRunning;
        }

        public void Stop()
        {
            StopCalls++;
            IsRunning = false;
        }

        public void Dispose()
        {
            DisposeCalls++;
            IsRunning = false;
        }

        public void Emit(string phrase, SpeechRecognitionConfidence confidence)
        {
            _phraseRecognized?.Invoke(new SpeechRecognitionSample(phrase, confidence));
        }
    }
}
