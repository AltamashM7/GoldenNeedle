using System;
using System.Collections.Generic;

namespace GoldenNeedle.Core.Commands
{
    public enum SpeechProviderLifecycleState
    {
        Stopped = 0,
        Disabled = 1,
        Created = 2,
        Starting = 3,
        Running = 4,
        Failed = 5,
        Unsupported = 6,
        Disposed = 7,
    }

    public enum SpeechRecognitionOutcome
    {
        None = 0,
        Dispatched = 1,
        Unmapped = 2,
        ConfidenceRejected = 3,
        CooldownRejected = 4,
        DispatchFailed = 5,
    }

    public readonly struct SpeechProviderDiagnostics
    {
        public SpeechProviderDiagnostics(
            SpeechProviderLifecycleState lifecycleState,
            bool backendSupported,
            string phraseSystemStatus,
            bool recognizerExists,
            bool recognizerRunning,
            int registeredKeywordCount,
            int microphoneDeviceCount,
            string primaryMicrophoneDevice,
            string microphoneDiagnostic,
            string lastErrorCode,
            string lastErrorMessage,
            string lastTransition)
        {
            LifecycleState = lifecycleState;
            BackendSupported = backendSupported;
            PhraseSystemStatus = phraseSystemStatus ?? string.Empty;
            RecognizerExists = recognizerExists;
            RecognizerRunning = recognizerRunning;
            RegisteredKeywordCount = Math.Max(0, registeredKeywordCount);
            MicrophoneDeviceCount = microphoneDeviceCount;
            PrimaryMicrophoneDevice = primaryMicrophoneDevice ?? string.Empty;
            MicrophoneDiagnostic = microphoneDiagnostic ?? string.Empty;
            LastErrorCode = lastErrorCode ?? string.Empty;
            LastErrorMessage = lastErrorMessage ?? string.Empty;
            LastTransition = lastTransition ?? string.Empty;
        }

        public SpeechProviderLifecycleState LifecycleState { get; }
        public bool BackendSupported { get; }
        public string PhraseSystemStatus { get; }
        public bool RecognizerExists { get; }
        public bool RecognizerRunning { get; }
        public int RegisteredKeywordCount { get; }
        public int MicrophoneDeviceCount { get; }
        public string PrimaryMicrophoneDevice { get; }
        public string MicrophoneDiagnostic { get; }
        public string LastErrorCode { get; }
        public string LastErrorMessage { get; }
        public string LastTransition { get; }
    }

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

    public interface ISpeechInputProviderDiagnostics
    {
        SpeechProviderDiagnostics Diagnostics { get; }
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
        private bool _acceptRecognitionEvents;
        private string _backendName = "Not initialized";
        private string _backendStatus = "Stopped";
        private SpeechProviderLifecycleState _lifecycleState = SpeechProviderLifecycleState.Stopped;
        private SpeechProviderDiagnostics _lastProviderDiagnostics;
        private long _recognitionSequence;

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
            _lastProviderDiagnostics = EmptyProviderDiagnostics(SpeechProviderLifecycleState.Stopped);
        }

        public string BackendName => _backendName;
        public string BackendStatus => _provider == null ? _backendStatus : _provider.Status;
        public string LastRecognizedPhrase { get; private set; } = string.Empty;
        public string LastResolvedCommand { get; private set; } = string.Empty;
        public string LastRejectionReason { get; private set; } = string.Empty;
        public string ConfigurationError => _resolver == null ? "Resolver unavailable" : _resolver.ValidationError;
        public bool IsRunning => _provider != null && _provider.IsRunning;
        public long RecognitionEventCount { get; private set; }
        public long LastRecognitionSequence { get; private set; }
        public double LastRecognitionTimeSeconds { get; private set; } = double.NaN;
        public SpeechRecognitionConfidence LastRawConfidence { get; private set; } = SpeechRecognitionConfidence.Unknown;
        public SpeechRecognitionOutcome LastRecognitionOutcome { get; private set; } = SpeechRecognitionOutcome.None;
        public bool LastMappingFound { get; private set; }
        public bool LastConfidenceRejected { get; private set; }
        public bool LastCooldownRejected { get; private set; }
        public bool LastDispatchAttempted { get; private set; }
        public string LastDispatchStatus { get; private set; } = string.Empty;
        public string LastDispatchMessage { get; private set; } = string.Empty;

        public SpeechProviderLifecycleState LifecycleState
        {
            get
            {
                if (_provider is ISpeechInputProviderDiagnostics diagnostics)
                {
                    return diagnostics.Diagnostics.LifecycleState;
                }
                return _lifecycleState;
            }
        }

        public SpeechProviderDiagnostics ProviderDiagnostics
        {
            get
            {
                if (_provider is ISpeechInputProviderDiagnostics diagnostics)
                {
                    return diagnostics.Diagnostics;
                }
                return _lastProviderDiagnostics;
            }
        }

        public string DiagnosticSummary
        {
            get
            {
                var provider = ProviderDiagnostics;
                var phraseSystem = DisplayValue(provider.PhraseSystemStatus);
                var recognizer = provider.RecognizerExists
                    ? provider.RecognizerRunning ? "Running" : "Stopped"
                    : "none";
                var micCount = provider.MicrophoneDeviceCount < 0
                    ? "?"
                    : provider.MicrophoneDeviceCount.ToString();
                var raw = RecognitionEventCount == 0
                    ? "-"
                    : $"\"{DisplayValue(LastRecognizedPhrase)}\" {LastRawConfidence}";
                var outcome = RecognitionEventCount == 0
                    ? "-"
                    : LastRecognitionOutcome.ToString();
                if (RecognitionEventCount > 0 && !string.IsNullOrWhiteSpace(LastRejectionReason))
                {
                    outcome += $": {LastRejectionReason}";
                }
                var backendError = provider.LastErrorMessage;
                if (string.IsNullOrWhiteSpace(backendError) &&
                    (LifecycleState == SpeechProviderLifecycleState.Failed ||
                     LifecycleState == SpeechProviderLifecycleState.Unsupported))
                {
                    backendError = LastRejectionReason;
                }

                return
                    $"{LifecycleState} | PhraseSystem={phraseSystem} | Recognizer={recognizer} " +
                    $"| Keywords={provider.RegisteredKeywordCount} | MicDevices={micCount} " +
                    $"| Events={RecognitionEventCount} | Last={raw} | Outcome={outcome} " +
                    $"| Error={DisplayValue(backendError)} | Transition={DisplayValue(provider.LastTransition)}";
            }
        }

        public bool Start()
        {
            if (_disposed)
            {
                _lifecycleState = SpeechProviderLifecycleState.Disposed;
                _backendStatus = "Disposed";
                LastRejectionReason = "Speech command input is disposed";
                return false;
            }

            if (_provider != null && _acceptRecognitionEvents &&
                (LifecycleState == SpeechProviderLifecycleState.Starting ||
                 LifecycleState == SpeechProviderLifecycleState.Running ||
                 _provider.IsRunning))
            {
                return true;
            }

            StopProvider();
            ResetRecognitionDiagnostics();
            _configuration.Sanitize();
            _resolver = new SpeechCommandResolver(_configuration);
            _cooldown = new SpeechCommandCooldown(_configuration.commandCooldownSeconds);

            if (!_configuration.speechEnabled)
            {
                _lifecycleState = SpeechProviderLifecycleState.Disabled;
                _backendName = "Speech disabled";
                _backendStatus = "Disabled by configuration";
                LastRejectionReason = "Speech is disabled";
                _lastProviderDiagnostics = EmptyProviderDiagnostics(SpeechProviderLifecycleState.Disabled);
                return false;
            }

            if (!_resolver.IsValid)
            {
                SetInputFailure("Speech configuration", "Invalid", _resolver.ValidationError);
                return false;
            }

            if (_resolver.EffectivePhrases.Count == 0)
            {
                SetInputFailure(
                    "Speech configuration",
                    "No enabled phrases",
                    "No enabled speech mappings are configured");
                return false;
            }

            if (_dispatcher == null)
            {
                SetInputFailure(
                    "Speech command input",
                    "Unavailable",
                    "Golden Needle command dispatcher is unavailable");
                return false;
            }

            if (_providerFactory == null)
            {
                SetInputFailure(
                    "Speech provider",
                    "Unavailable",
                    "Speech provider factory is unavailable");
                return false;
            }

            try
            {
                _provider = _providerFactory(_resolver.EffectivePhrases);
                if (_provider == null)
                {
                    SetInputFailure(
                        "Speech provider",
                        "Unavailable",
                        "Speech provider factory returned no provider");
                    return false;
                }

                _backendName = _provider.BackendName;
                _backendStatus = _provider.Status;
                _lifecycleState = SpeechProviderLifecycleState.Created;
                CaptureProviderDiagnostics();

                if (!_provider.IsSupported)
                {
                    _lifecycleState = SpeechProviderLifecycleState.Unsupported;
                    LastRejectionReason = string.IsNullOrWhiteSpace(_provider.LastError)
                        ? "Speech recognition is unsupported on this runtime"
                        : _provider.LastError;
                    CaptureProviderDiagnostics();
                    return false;
                }

                _provider.PhraseRecognized += OnPhraseRecognized;
                _acceptRecognitionEvents = true;
                _lifecycleState = SpeechProviderLifecycleState.Starting;
                var started = _provider.Start();
                _backendStatus = _provider.Status;
                CaptureProviderDiagnostics();
                if (!started)
                {
                    _acceptRecognitionEvents = false;
                    _provider.PhraseRecognized -= OnPhraseRecognized;
                    var providerState = ProviderDiagnostics.LifecycleState;
                    _lifecycleState = providerState == SpeechProviderLifecycleState.Unsupported
                        ? SpeechProviderLifecycleState.Unsupported
                        : SpeechProviderLifecycleState.Failed;
                    LastRejectionReason = string.IsNullOrWhiteSpace(_provider.LastError)
                        ? "Speech provider failed to start"
                        : _provider.LastError;
                    return false;
                }

                _lifecycleState = _provider.IsRunning
                    ? SpeechProviderLifecycleState.Running
                    : SpeechProviderLifecycleState.Starting;
                LastRejectionReason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                _acceptRecognitionEvents = false;
                _lifecycleState = SpeechProviderLifecycleState.Failed;
                _backendStatus = "Error";
                LastRejectionReason = $"Speech initialization failed: {exception.Message}";
                if (_provider != null)
                {
                    _provider.PhraseRecognized -= OnPhraseRecognized;
                    CaptureProviderDiagnostics();
                }
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
            _lifecycleState = SpeechProviderLifecycleState.Stopped;
            _backendStatus = "Stopped";
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            StopProvider();
            _disposed = true;
            _lifecycleState = SpeechProviderLifecycleState.Disposed;
            _backendStatus = "Disposed";
        }

        private void OnPhraseRecognized(SpeechRecognitionSample sample)
        {
            if (_disposed || !_acceptRecognitionEvents)
            {
                return;
            }

            RecognitionEventCount++;
            LastRecognitionSequence = ++_recognitionSequence;
            LastRecognitionTimeSeconds = _clockSeconds();
            LastRecognizedPhrase = sample.Phrase ?? string.Empty;
            LastRawConfidence = sample.Confidence;
            LastResolvedCommand = string.Empty;
            LastRejectionReason = string.Empty;
            LastMappingFound = false;
            LastConfidenceRejected = false;
            LastCooldownRejected = false;
            LastDispatchAttempted = false;
            LastDispatchStatus = string.Empty;
            LastDispatchMessage = string.Empty;
            LastRecognitionOutcome = SpeechRecognitionOutcome.None;
            _backendStatus = _provider == null ? _backendStatus : _provider.Status;
            CaptureProviderDiagnostics();

            if (!_resolver.TryResolve(
                    sample.Phrase,
                    sample.Confidence,
                    out var request,
                    out var rejectionReason))
            {
                LastMappingFound = HasEffectiveMapping(sample.Phrase);
                LastConfidenceRejected = LastMappingFound;
                LastRecognitionOutcome = LastMappingFound
                    ? SpeechRecognitionOutcome.ConfidenceRejected
                    : SpeechRecognitionOutcome.Unmapped;
                LastRejectionReason = rejectionReason;
                return;
            }

            LastMappingFound = true;
            LastResolvedCommand = request.ToString();
            var now = _clockSeconds();
            if (!_cooldown.CanDispatch(now, out var remainingSeconds))
            {
                LastCooldownRejected = true;
                LastRecognitionOutcome = SpeechRecognitionOutcome.CooldownRejected;
                LastRejectionReason = $"Speech command cooldown active ({remainingSeconds:0.00}s remaining)";
                return;
            }

            LastDispatchAttempted = true;
            GoldenNeedleCommandResult result;
            try
            {
                result = _dispatcher.Execute(request);
            }
            catch (Exception exception)
            {
                LastRecognitionOutcome = SpeechRecognitionOutcome.DispatchFailed;
                LastDispatchStatus = "Exception";
                LastDispatchMessage = exception.Message;
                LastRejectionReason = $"Speech dispatch failed: {exception.Message}";
                return;
            }

            LastDispatchStatus = result.Status.ToString();
            LastDispatchMessage = result.Message ?? string.Empty;
            if (result.Succeeded)
            {
                _cooldown.MarkDispatched(now);
                LastRecognitionOutcome = SpeechRecognitionOutcome.Dispatched;
                LastRejectionReason = string.Empty;
                return;
            }

            LastRecognitionOutcome = SpeechRecognitionOutcome.DispatchFailed;
            LastRejectionReason = string.IsNullOrWhiteSpace(result.Message)
                ? result.Status.ToString()
                : result.Message;
        }

        private bool HasEffectiveMapping(string phrase)
        {
            var normalized = SpeechCommandResolver.NormalizePhrase(phrase);
            if (string.IsNullOrEmpty(normalized) || _resolver == null)
            {
                return false;
            }

            var phrases = _resolver.EffectivePhrases;
            for (var i = 0; i < phrases.Count; i++)
            {
                if (string.Equals(phrases[i], normalized, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private void StopProvider()
        {
            _acceptRecognitionEvents = false;
            _cooldown?.Reset();
            if (_provider == null)
            {
                return;
            }

            var provider = _provider;
            provider.PhraseRecognized -= OnPhraseRecognized;
            try
            {
                provider.Stop();
                _backendName = provider.BackendName;
                _backendStatus = provider.Status;
                CaptureProviderDiagnostics(provider);
            }
            catch (Exception exception)
            {
                _lifecycleState = SpeechProviderLifecycleState.Failed;
                LastRejectionReason = $"Speech stop failed: {exception.Message}";
            }
            finally
            {
                try
                {
                    provider.Dispose();
                }
                catch (Exception exception)
                {
                    _lifecycleState = SpeechProviderLifecycleState.Failed;
                    LastRejectionReason = $"Speech dispose failed: {exception.Message}";
                }
                _provider = null;
            }
        }

        private void CaptureProviderDiagnostics(ISpeechInputProvider provider = null)
        {
            provider = provider ?? _provider;
            if (provider is ISpeechInputProviderDiagnostics diagnostics)
            {
                _lastProviderDiagnostics = diagnostics.Diagnostics;
                return;
            }

            if (provider == null)
            {
                return;
            }

            _lastProviderDiagnostics = new SpeechProviderDiagnostics(
                _lifecycleState,
                provider.IsSupported,
                "n/a",
                false,
                provider.IsRunning,
                _resolver == null ? 0 : _resolver.EffectivePhrases.Count,
                -1,
                string.Empty,
                "Unavailable from this provider",
                string.Empty,
                provider.LastError,
                provider.Status);
        }

        private void ResetRecognitionDiagnostics()
        {
            RecognitionEventCount = 0;
            LastRecognitionSequence = 0;
            LastRecognitionTimeSeconds = double.NaN;
            LastRecognizedPhrase = string.Empty;
            LastRawConfidence = SpeechRecognitionConfidence.Unknown;
            LastResolvedCommand = string.Empty;
            LastRejectionReason = string.Empty;
            LastMappingFound = false;
            LastConfidenceRejected = false;
            LastCooldownRejected = false;
            LastDispatchAttempted = false;
            LastDispatchStatus = string.Empty;
            LastDispatchMessage = string.Empty;
            LastRecognitionOutcome = SpeechRecognitionOutcome.None;
        }

        private void SetInputFailure(string backendName, string backendStatus, string reason)
        {
            _lifecycleState = SpeechProviderLifecycleState.Failed;
            _backendName = backendName;
            _backendStatus = backendStatus;
            LastRejectionReason = reason ?? string.Empty;
            _lastProviderDiagnostics = EmptyProviderDiagnostics(SpeechProviderLifecycleState.Failed);
        }

        private static SpeechProviderDiagnostics EmptyProviderDiagnostics(
            SpeechProviderLifecycleState state)
        {
            return new SpeechProviderDiagnostics(
                state,
                false,
                "n/a",
                false,
                false,
                0,
                -1,
                string.Empty,
                "Not sampled",
                string.Empty,
                string.Empty,
                state.ToString());
        }

        private static string DisplayValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }
}
