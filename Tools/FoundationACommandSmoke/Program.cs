using System;
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

            var reserved = router.Execute(new GoldenNeedleCommandRequest(GoldenNeedleCommand.SelectCameraViewPreset, "Hands"));
            True(reserved.Status == GoldenNeedleCommandResultStatus.Unsupported, "Foundation B reserved command unsupported");
            Equal("Hands", reserved.Request.parameter, "reserved parameter preserved");

            var now = 5d;
            var recenterCalls = 0;
            var fake = new FakeProvider();
            var recenterRouter = new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets { Recenter = () => recenterCalls++ });
            var cooldownConfig = Config(new SpeechCommandMapping("recenter", GoldenNeedleCommand.Recenter));
            cooldownConfig.commandCooldownSeconds = 1f;
            using (var input = new SpeechCommandInput(cooldownConfig, recenterRouter, _ => fake, () => now))
            {
                True(input.Start(), "fake provider starts");
                fake.Emit("recenter", SpeechRecognitionConfidence.Medium);
                fake.Emit("recenter", SpeechRecognitionConfidence.Medium);
                True(recenterCalls == 1, "speech cooldown blocks duplicate");
                True(input.LastRejectionReason.Contains("cooldown", StringComparison.OrdinalIgnoreCase), "cooldown diagnostic");

                True(recenterRouter.Execute(new GoldenNeedleCommandRequest(GoldenNeedleCommand.Recenter)).Succeeded, "direct shared router remains responsive");
                True(recenterCalls == 2, "keyboard path bypasses speech cooldown");

                now = 6.1d;
                fake.Emit("recenter", SpeechRecognitionConfidence.Medium);
                True(recenterCalls == 3, "speech resumes after cooldown");
            }

            var unsupported = new FakeProvider { Supported = false };
            using (var input = new SpeechCommandInput(
                Config(new SpeechCommandMapping("recenter", GoldenNeedleCommand.Recenter)),
                recenterRouter,
                _ => unsupported,
                () => now))
            {
                True(!input.Start(), "unsupported provider fails closed");
                True(!string.IsNullOrEmpty(input.LastRejectionReason), "unsupported diagnostic");
            }

            Console.WriteLine("FOUNDATION_A_COMMAND_SMOKE=PASS");
            Console.WriteLine("NORMALIZATION_WAKE_DUPLICATES=PASS");
            Console.WriteLine("CONFIDENCE_COOLDOWN=PASS");
            Console.WriteLine("PARAMETER_AND_RESERVED_COMMAND=PASS");
            Console.WriteLine("UNSUPPORTED_SPEECH_FALLBACK=PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
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
        public event Action<SpeechRecognitionSample> PhraseRecognized;
        public bool Supported { get; set; } = true;
        public string BackendName => "Fake";
        public string Status => IsRunning ? "Listening" : Supported ? "Ready" : "Unsupported";
        public string LastError => Supported ? string.Empty : "Unsupported fake backend";
        public bool IsSupported => Supported;
        public bool IsRunning { get; private set; }
        public bool Start() { IsRunning = Supported; return IsRunning; }
        public void Stop() { IsRunning = false; }
        public void Dispose() { IsRunning = false; }
        public void Emit(string phrase, SpeechRecognitionConfidence confidence) =>
            PhraseRecognized?.Invoke(new SpeechRecognitionSample(phrase, confidence));
    }
}
