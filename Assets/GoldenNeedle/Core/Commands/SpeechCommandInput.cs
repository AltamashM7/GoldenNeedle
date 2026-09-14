using System;
using System.Collections.Generic;

namespace GoldenNeedle.Core.Commands
{
    public readonly struct SpeechRecognitionSample
    {
        public SpeechRecognitionSample(
            string phrase,
            SpeechRecognitionConfidence confidence)
        {
            Phrase = phrase ?? string.Empty;
            Confidence = confidence;
        }

        public string Phrase { get; }
        public SpeechRecognitionConfidence Confidence { get; }
    }

    public interface ISpeechInputProvider : IDisposable
    {
        event Action<SpeechRecognitionSample> PhraseRecognized;

        string BackendName { get; }
        string Status { get; }
        string LastError { get; }
        bool IsSupported { get; }
        bool IsRunning { get; }
        bool Start();
        void Stop();
    }

    /// <summary>
    /// Speech-only input adapter. Recognition policy, confidence and cooldown are resolved here;
    /// accepted requests are forwarded to the same dispatcher used by keyboard/future UI input.
    /// </summary>
    public sealed class SpeechCommandInput : IDisposable
    {
        private readonly SpeechCommandConfiguration _configuration;
        private readonly IGoldenNeedleCommandDispatcher _dispatcher;
        private readonly Func<IReadOnlyList<string>, ISpeechInputProvider> _providerFactory;
        private readonly Func<double> _clockSeconds;
        private SpeechCommandResolver _resolver;
        private SpeechCommandCooldown _cooldown;
        private ISpeechInputProvider _provider;
        private bool _disposed;
        private string _backendName = "Not initialized";
        private string _backendStatus = "Stopped";

        public SpeechCommandInput(
            SpeechCommandConfiguration configuration,
            IGoldenNeedleCommandDispatcher dispatcher,
            Func<IReadOnlyList<string>, ISpeechInputProvider> providerFactory,
            Func<double> clockSeconds)
        {
            _configuration = configuration ?? SpeechCommandConfiguration.CreateDefault();
            _configuration.Sanitize();
            _dispatcher = dispatcher;
            _providerFactory = providerFactory;
            _clockSeconds = clockSeconds ?? (() => 0d);
            _resolver = new SpeechCommandResolver(_configuration);
            _cooldown = new SpeechCommandCooldown(_configuration.commandCooldownSeconds);
        }

        public string BackendName => _backendName;
        public string BackendStatus => _provider == null ? _backendStatus : _provider.Status;
        public string LastRecognizedPhrase { get; private set; } = string.Empty;
        public string LastResolvedCommand { get; private set; } = string.Empty;
        public string LastRejectionReason { get; private set; } = string.Empty;
        public string ConfigurationError => _resolver == null ? "Resolver unavailable" : _resolver.ValidationError;
        public bool IsRunning => _provider != null && _provider.IsRunning;

        public string DiagnosticSummary
        {
            get
            {
                var providerError = _provider == null ? string.Empty : _provider.LastError;
                var failure = string.IsNullOrWhiteSpace(providerError)
                    ? LastRejectionReason
                    : providerError;
                return
                    $"{BackendName}: {BackendStatus} | phrase='{DisplayValue(LastRecognizedPhrase)}' " +
                    $"cmd={DisplayValue(LastResolvedCommand)} reject={DisplayValue(failure)}";
            }
        }

        public bool Start()
        {
            if (_disposed)
            {
                _backendStatus = "Disposed";
                LastRejectionReason = "Speech command input is disposed";
                return false;
            }

            StopProvider();
            _configuration.Sanitize();
            _resolver = new SpeechCommandResolver(_configuration);
            _cooldown = new SpeechCommandCooldown(_configuration.commandCooldownSeconds);

            if (!_configuration.speechEnabled)
            {
                _backendName = "Speech disabled";
                _backendStatus = "Disabled by configuration";
                LastRejectionReason = "Speech is disabled";
                return false;
            }

            if (!_resolver.IsValid)
            {
                _backendName = "Speech configuration";
                _backendStatus = "Invalid";
                LastRejectionReason = _resolver.ValidationError;
                return false;
            }

            if (_resolver.EffectivePhrases.Count == 0)
            {
                _backendName = "Speech configuration";
                _backendStatus = "No enabled phrases";
                LastRejectionReason = "No enabled speech mappings are configured";
                return false;
            }

            if (_dispatcher == null)
            {
                _backendName = "Speech command input";
                _backendStatus = "Unavailable";
                LastRejectionReason = "Golden Needle command dispatcher is unavailable";
                return false;
            }

            if (_providerFactory == null)
            {
                _backendName = "Speech provider";
                _backendStatus = "Unavailable";
                LastRejectionReason = "Speech provider factory is unavailable";
                return false;
            }

            try
            {
                _provider = _providerFactory(_resolver.EffectivePhrases);
                if (_provider == null)
                {
                    _backendName = "Speech provider";
                    _backendStatus = "Unavailable";
                    LastRejectionReason = "Speech provider factory returned no provider";
                    return false;
                }

                _backendName = _provider.BackendName;
                _backendStatus = _provider.Status;
                if (!_provider.IsSupported)
                {
                    LastRejectionReason = string.IsNullOrWhiteSpace(_provider.LastError)
                        ? "Speech recognition is unsupported on this runtime"
                        : _provider.LastError;
                    _backendStatus = _provider.Status;
                    StopProvider(disposeOnly: true);
                    return false;
                }

                _provider.PhraseRecognized += OnPhraseRecognized;
                var started = _provider.Start();
                _backendStatus = _provider.Status;
                if (!started)
                {
                    LastRejectionReason = string.IsNullOrWhiteSpace(_provider.LastError)
                        ? "Speech provider failed to start"
                        : _provider.LastError;
                    StopProvider(disposeOnly: true);
                    return false;
                }

                LastRejectionReason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                _backendStatus = "Error";
                LastRejectionReason = $"Speech initialization failed: {exception.Message}";
                StopProvider(disposeOnly: true);
                return false;
            }
        }

        public void Stop()
        {
            if (_disposed)
            {
                return;
            }
            StopProvider();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            StopProvider();
            _disposed = true;
            _backendStatus = "Disposed";
        }

        private void OnPhraseRecognized(SpeechRecognitionSample sample)
        {
            LastRecognizedPhrase = sample.Phrase ?? string.Empty;
            _backendStatus = _provider == null ? _backendStatus : _provider.Status;

            if (!_resolver.TryResolve(
                    sample.Phrase,
                    sample.Confidence,
                    out var request,
                    out var rejectionReason))
            {
                LastResolvedCommand = string.Empty;
                LastRejectionReason = rejectionReason;
                return;
            }

            LastResolvedCommand = request.ToString();
            var now = _clockSeconds();
            if (!_cooldown.CanDispatch(now, out var remainingSeconds))
            {
                LastRejectionReason = $"Speech command cooldown active ({remainingSeconds:0.00}s remaining)";
                return;
            }

            var result = _dispatcher.Execute(request);
            if (result.Succeeded)
            {
                _cooldown.MarkDispatched(now);
                LastRejectionReason = string.Empty;
                return;
            }

            LastRejectionReason = string.IsNullOrWhiteSpace(result.Message)
                ? result.Status.ToString()
                : result.Message;
        }

        private void StopProvider(bool disposeOnly = false)
        {
            if (_provider == null)
            {
                if (!disposeOnly && !_disposed)
                {
                    _backendStatus = "Stopped";
                }
                return;
            }

            var provider = _provider;
            _provider = null;
            provider.PhraseRecognized -= OnPhraseRecognized;
            try
            {
                if (!disposeOnly)
                {
                    provider.Stop();
                }
            }
            catch (Exception exception)
            {
                LastRejectionReason = $"Speech stop failed: {exception.Message}";
            }
            finally
            {
                _backendName = provider.BackendName;
                _backendStatus = disposeOnly ? provider.Status : "Stopped";
                try
                {
                    provider.Dispose();
                }
                catch (Exception exception)
                {
                    LastRejectionReason = $"Speech dispose failed: {exception.Message}";
                }
            }
        }

        private static string DisplayValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }
}
