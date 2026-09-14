using System;
using System.Collections.Generic;
using System.Text;

namespace GoldenNeedle.Core.Commands
{
    public enum SpeechRecognitionConfidence
    {
        Unknown = 0,
        Low = 1,
        Medium = 2,
        High = 3,
    }

    [Serializable]
    public sealed class SpeechCommandMapping
    {
        public bool enabled = true;
        public string phrase = string.Empty;
        public GoldenNeedleCommand command;
        public string parameter = string.Empty;
        public SpeechRecognitionConfidence minimumConfidence = SpeechRecognitionConfidence.Medium;

        public SpeechCommandMapping()
        {
        }

        public SpeechCommandMapping(
            string phrase,
            GoldenNeedleCommand command,
            string parameter = "",
            SpeechRecognitionConfidence minimumConfidence = SpeechRecognitionConfidence.Medium)
        {
            enabled = true;
            this.phrase = phrase ?? string.Empty;
            this.command = command;
            this.parameter = parameter ?? string.Empty;
            this.minimumConfidence = minimumConfidence;
        }
    }

    [Serializable]
    public sealed class SpeechCommandConfiguration
    {
        public bool speechEnabled = true;
        public float commandCooldownSeconds = 0.75f;
        public string wakePrefix = string.Empty;
        public SpeechCommandMapping[] mappings = Array.Empty<SpeechCommandMapping>();

        public void Sanitize()
        {
            if (float.IsNaN(commandCooldownSeconds) || float.IsInfinity(commandCooldownSeconds))
            {
                commandCooldownSeconds = 0.75f;
            }
            commandCooldownSeconds = Math.Max(0f, commandCooldownSeconds);
            wakePrefix = wakePrefix ?? string.Empty;
            mappings = mappings ?? Array.Empty<SpeechCommandMapping>();
        }

        public static SpeechCommandConfiguration CreateDefault()
        {
            return new SpeechCommandConfiguration
            {
                speechEnabled = true,
                commandCooldownSeconds = 0.75f,
                wakePrefix = string.Empty,
                mappings = new[]
                {
                    new SpeechCommandMapping("begin calibration", GoldenNeedleCommand.BeginCalibration),
                    new SpeechCommandMapping("reset calibration", GoldenNeedleCommand.ResetCalibration),
                    new SpeechCommandMapping("recenter", GoldenNeedleCommand.Recenter),
                    new SpeechCommandMapping("retry tracking", GoldenNeedleCommand.RetryTracking),
                    new SpeechCommandMapping("game view", GoldenNeedleCommand.SetGamePresentation),
                    new SpeechCommandMapping("lab view", GoldenNeedleCommand.SetLabPresentation),
                    new SpeechCommandMapping("back view", GoldenNeedleCommand.SelectCameraViewPreset, "Back"),
                    new SpeechCommandMapping("front view", GoldenNeedleCommand.SelectCameraViewPreset, "Front"),
                    new SpeechCommandMapping("left view", GoldenNeedleCommand.SelectCameraViewPreset, "Left"),
                    new SpeechCommandMapping("right view", GoldenNeedleCommand.SelectCameraViewPreset, "Right"),
                    new SpeechCommandMapping("full body view", GoldenNeedleCommand.SelectCameraViewPreset, "FullBody"),
                    new SpeechCommandMapping("hands view", GoldenNeedleCommand.SelectCameraViewPreset, "Hands"),
                    new SpeechCommandMapping("left hand view", GoldenNeedleCommand.SelectCameraViewPreset, "LeftHand"),
                    new SpeechCommandMapping("right hand view", GoldenNeedleCommand.SelectCameraViewPreset, "RightHand"),
                },
            };
        }
    }

    public sealed class SpeechCommandResolver
    {
        private readonly Dictionary<string, SpeechCommandMapping> _mappings =
            new Dictionary<string, SpeechCommandMapping>(StringComparer.Ordinal);
        private readonly string[] _effectivePhrases;

        public SpeechCommandResolver(SpeechCommandConfiguration configuration)
        {
            configuration = configuration ?? new SpeechCommandConfiguration();
            configuration.Sanitize();
            var phrases = new List<string>();
            var errors = new List<string>();

            for (var i = 0; i < configuration.mappings.Length; i++)
            {
                var mapping = configuration.mappings[i];
                if (mapping == null || !mapping.enabled)
                {
                    continue;
                }

                var effectivePhrase = BuildEffectivePhrase(configuration.wakePrefix, mapping.phrase);
                if (string.IsNullOrEmpty(effectivePhrase))
                {
                    errors.Add($"Enabled mapping #{i + 1} has an empty effective phrase");
                    continue;
                }

                if (_mappings.ContainsKey(effectivePhrase))
                {
                    errors.Add($"Ambiguous speech phrase: '{effectivePhrase}'");
                    continue;
                }

                _mappings.Add(effectivePhrase, mapping);
                phrases.Add(effectivePhrase);
            }

            _effectivePhrases = phrases.ToArray();
            ValidationError = string.Join("; ", errors);
        }

        public bool IsValid => string.IsNullOrEmpty(ValidationError);
        public string ValidationError { get; }
        public IReadOnlyList<string> EffectivePhrases => _effectivePhrases;

        public bool TryResolve(
            string recognizedPhrase,
            SpeechRecognitionConfidence confidence,
            out GoldenNeedleCommandRequest request,
            out string rejectionReason)
        {
            request = default;
            if (!IsValid)
            {
                rejectionReason = ValidationError;
                return false;
            }

            var normalized = NormalizePhrase(recognizedPhrase);
            if (string.IsNullOrEmpty(normalized) || !_mappings.TryGetValue(normalized, out var mapping))
            {
                rejectionReason = string.IsNullOrEmpty(normalized)
                    ? "Recognized phrase was empty after normalization"
                    : $"No enabled speech mapping for '{normalized}'";
                return false;
            }

            if (confidence != SpeechRecognitionConfidence.Unknown &&
                confidence < mapping.minimumConfidence)
            {
                rejectionReason =
                    $"Recognition confidence {confidence} is below mapping minimum {mapping.minimumConfidence}";
                return false;
            }

            request = new GoldenNeedleCommandRequest(mapping.command, mapping.parameter);
            rejectionReason = string.Empty;
            return true;
        }

        public static string BuildEffectivePhrase(string wakePrefix, string phrase)
        {
            var normalizedPrefix = NormalizePhrase(wakePrefix);
            var normalizedPhrase = NormalizePhrase(phrase);
            if (string.IsNullOrEmpty(normalizedPrefix))
            {
                return normalizedPhrase;
            }
            if (string.IsNullOrEmpty(normalizedPhrase))
            {
                return normalizedPrefix;
            }
            return normalizedPrefix + " " + normalizedPhrase;
        }

        public static string NormalizePhrase(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(phrase.Length);
            var pendingSpace = false;
            for (var i = 0; i < phrase.Length; i++)
            {
                var character = phrase[i];
                if (char.IsWhiteSpace(character))
                {
                    if (builder.Length > 0)
                    {
                        pendingSpace = true;
                    }
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }
                builder.Append(char.ToLowerInvariant(character));
            }
            return builder.ToString();
        }
    }

    public sealed class SpeechCommandCooldown
    {
        private double _lastDispatchSeconds = double.NegativeInfinity;

        public SpeechCommandCooldown(double cooldownSeconds)
        {
            CooldownSeconds = Math.Max(0d, cooldownSeconds);
        }

        public double CooldownSeconds { get; }

        public bool CanDispatch(double nowSeconds, out double remainingSeconds)
        {
            var elapsed = nowSeconds - _lastDispatchSeconds;
            remainingSeconds = Math.Max(0d, CooldownSeconds - elapsed);
            return remainingSeconds <= 0d;
        }

        public void MarkDispatched(double nowSeconds)
        {
            _lastDispatchSeconds = nowSeconds;
        }

        public void Reset()
        {
            _lastDispatchSeconds = double.NegativeInfinity;
        }
    }
}
