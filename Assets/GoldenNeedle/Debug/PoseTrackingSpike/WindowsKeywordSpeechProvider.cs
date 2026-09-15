using System;
using System.Collections.Generic;
using GoldenNeedle.Core.Commands;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
using UnityEngine.Windows.Speech;
#endif

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    /// <summary>
    /// Windows-first speech backend. It emits only recognized phrase/confidence samples and has
    /// no knowledge of Golden Needle calibration, locomotion, cameras or presentation actions.
    /// </summary>
    public sealed class WindowsKeywordSpeechProvider : ISpeechInputProvider, ISpeechInputProviderDiagnostics
    {
        private readonly string[] _keywords;
        private bool _disposed;
        private string _status;
        private string _lastError;
        private string _lastErrorCode;
        private string _lastTransition;
        private string _phraseSystemStatus = "n/a";
        private int _microphoneDeviceCount = -1;
        private string _primaryMicrophoneDevice;
        private string _microphoneDiagnostic = "Not sampled";
        private SpeechProviderLifecycleState _lifecycleState;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
        private KeywordRecognizer _recognizer;
        private bool _phraseSystemSubscribed;
#endif

        public WindowsKeywordSpeechProvider(IReadOnlyList<string> keywords)
        {
            if (keywords == null)
            {
                _keywords = Array.Empty<string>();
            }
            else
            {
                _keywords = new string[keywords.Count];
                for (var i = 0; i < keywords.Count; i++)
                {
                    _keywords[i] = keywords[i] ?? string.Empty;
                }
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
            try
            {
                IsSupported = PhraseRecognitionSystem.isSupported;
                _phraseSystemStatus = IsSupported
                    ? PhraseRecognitionSystem.Status.ToString()
                    : "Unsupported";
                if (IsSupported)
                {
                    SetLifecycle(
                        SpeechProviderLifecycleState.Created,
                        "Created",
                        $"Provider created; PhraseSystem={_phraseSystemStatus}");
                }
                else
                {
                    _lastErrorCode = "Unsupported";
                    _lastError = "Unity PhraseRecognitionSystem reports speech recognition is unsupported";
                    SetLifecycle(
                        SpeechProviderLifecycleState.Unsupported,
                        "Unsupported on this Windows runtime",
                        "PhraseRecognitionSystem.isSupported=false");
                }
            }
            catch (Exception exception)
            {
                IsSupported = false;
                _lastErrorCode = "SupportQueryException";
                _lastError = exception.Message;
                SetLifecycle(
                    SpeechProviderLifecycleState.Failed,
                    "Support query failed",
                    "PhraseRecognitionSystem support query threw an exception");
            }
#else
            IsSupported = false;
            _lastErrorCode = "UnsupportedPlatform";
            _lastError = "Unity Windows speech recognition is available only on supported Windows runtimes";
            SetLifecycle(
                SpeechProviderLifecycleState.Unsupported,
                "Unsupported on this platform",
                "Non-Windows runtime");
#endif
        }

        public event Action<SpeechRecognitionSample> PhraseRecognized;

        public string BackendName => "Unity Windows KeywordRecognizer";
        public string Status => _status ?? string.Empty;
        public string LastError => _lastError ?? string.Empty;
        public bool IsSupported { get; private set; }

        public bool IsRunning
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
                return _recognizer != null && _recognizer.IsRunning;
#else
                return false;
#endif
            }
        }

        public SpeechProviderDiagnostics Diagnostics =>
            new SpeechProviderDiagnostics(
                _lifecycleState,
                IsSupported,
                _phraseSystemStatus,
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
                _recognizer != null,
#else
                false,
#endif
                IsRunning,
                _keywords.Length,
                _microphoneDeviceCount,
                _primaryMicrophoneDevice,
                _microphoneDiagnostic,
                _lastErrorCode,
                _lastError,
                _lastTransition);

        public bool Start()
        {
            if (_disposed)
            {
                _lastErrorCode = "Disposed";
                _lastError = "Speech provider has already been disposed";
                SetLifecycle(
                    SpeechProviderLifecycleState.Disposed,
                    "Disposed",
                    "Start rejected because provider is disposed");
                return false;
            }
            if (!IsSupported)
            {
                if (_lifecycleState != SpeechProviderLifecycleState.Failed)
                {
                    SetLifecycle(
                        SpeechProviderLifecycleState.Unsupported,
                        "Unsupported",
                        "Start rejected because PhraseRecognitionSystem is unsupported");
                }
                return false;
            }
            if (!ValidateKeywords(out var keywordError))
            {
                _lastErrorCode = "InvalidKeywords";
                _lastError = keywordError;
                SetLifecycle(
                    SpeechProviderLifecycleState.Failed,
                    "Invalid keywords",
                    "Keyword validation failed before recognizer creation");
                return false;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
            if (_recognizer != null && _recognizer.IsRunning)
            {
                _phraseSystemStatus = ReadPhraseSystemStatus();
                SetLifecycle(
                    SpeechProviderLifecycleState.Running,
                    "Listening",
                    "Repeated Start reused the existing running recognizer");
                return true;
            }

            ReleaseBackendResources();
            _lastError = string.Empty;
            _lastErrorCode = string.Empty;
            CaptureMicrophoneDiagnostics();
            SubscribePhraseSystemEvents();
            _phraseSystemStatus = ReadPhraseSystemStatus();
            SetLifecycle(
                SpeechProviderLifecycleState.Starting,
                "Starting",
                $"Recognizer start requested; PhraseSystem={_phraseSystemStatus}");

            try
            {
                // Low is intentionally the recognizer-wide threshold. The project-owned resolver
                // applies each mapping's stricter minimum confidence after recognition.
                _recognizer = new KeywordRecognizer(_keywords, ConfidenceLevel.Low);
                _recognizer.OnPhraseRecognized += HandlePhraseRecognized;
                _recognizer.Start();
                _phraseSystemStatus = ReadPhraseSystemStatus();
                if (_recognizer.IsRunning)
                {
                    SetLifecycle(
                        SpeechProviderLifecycleState.Running,
                        "Listening",
                        $"Recognizer reports running; PhraseSystem={_phraseSystemStatus}");
                }
                else
                {
                    // PhraseRecognizer.Start is a request into the shared Windows phrase system.
                    // Keep this session alive and observable while status callbacks tell us whether
                    // the system reaches Running or fails; do not erase the evidence immediately.
                    SetLifecycle(
                        SpeechProviderLifecycleState.Starting,
                        "Start requested",
                        $"Recognizer does not yet report running; PhraseSystem={_phraseSystemStatus}");
                }
                return true;
            }
            catch (Exception exception)
            {
                ReleaseBackendResources();
                _lastErrorCode = "StartException";
                _lastError = exception.Message;
                SetLifecycle(
                    SpeechProviderLifecycleState.Failed,
                    "Start error",
                    "KeywordRecognizer creation/start threw an exception");
                return false;
            }
#else
            return false;
#endif
        }

        public void Stop()
        {
            if (_disposed)
            {
                return;
            }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
            var stopError = ReleaseBackendResources();
            _phraseSystemStatus = IsSupported ? ReadPhraseSystemStatus() : _phraseSystemStatus;
            if (!string.IsNullOrEmpty(stopError))
            {
                _lastErrorCode = "StopException";
                _lastError = stopError;
                SetLifecycle(
                    SpeechProviderLifecycleState.Failed,
                    "Stop error",
                    "Recognizer teardown reported an exception");
                return;
            }
#endif
            SetLifecycle(
                SpeechProviderLifecycleState.Stopped,
                "Stopped",
                "Provider stopped and phrase-system subscriptions removed");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
            var disposeError = ReleaseBackendResources();
            if (!string.IsNullOrEmpty(disposeError))
            {
                _lastErrorCode = "DisposeException";
                _lastError = disposeError;
            }
#endif
            _disposed = true;
            SetLifecycle(
                SpeechProviderLifecycleState.Disposed,
                "Disposed",
                "Provider disposed; native recognizer and static subscriptions released");
        }

        private bool ValidateKeywords(out string error)
        {
            if (_keywords.Length == 0)
            {
                error = "Cannot start KeywordRecognizer without effective phrases";
                return false;
            }

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < _keywords.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(_keywords[i]))
                {
                    error = $"Keyword #{i + 1} is empty";
                    return false;
                }
                if (!unique.Add(_keywords[i]))
                {
                    error = $"Duplicate KeywordRecognizer phrase '{_keywords[i]}'";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void SetLifecycle(
            SpeechProviderLifecycleState state,
            string status,
            string transition)
        {
            _lifecycleState = state;
            _status = status ?? state.ToString();
            _lastTransition = transition ?? string.Empty;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
        private void HandlePhraseRecognized(PhraseRecognizedEventArgs args)
        {
            SetLifecycle(
                SpeechProviderLifecycleState.Running,
                "Listening",
                "Phrase recognition event received");
            try
            {
                PhraseRecognized?.Invoke(new SpeechRecognitionSample(
                    args.text,
                    MapConfidence(args.confidence)));
            }
            catch (Exception exception)
            {
                _lastErrorCode = "RecognitionCallbackException";
                _lastError = exception.Message;
                SetLifecycle(
                    SpeechProviderLifecycleState.Failed,
                    "Recognition callback error",
                    "A phrase subscriber threw an exception");
            }
        }

        private void HandlePhraseSystemStatusChanged(SpeechSystemStatus status)
        {
            _phraseSystemStatus = status.ToString();
            if (_disposed)
            {
                return;
            }

            var statusName = _phraseSystemStatus;
            if (string.Equals(statusName, "Running", StringComparison.OrdinalIgnoreCase))
            {
                SetLifecycle(
                    IsRunning
                        ? SpeechProviderLifecycleState.Running
                        : SpeechProviderLifecycleState.Starting,
                    IsRunning ? "Listening" : "Phrase system running",
                    $"PhraseRecognitionSystem status changed to {statusName}");
                return;
            }

            if (string.Equals(statusName, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(_lastErrorCode))
                {
                    _lastErrorCode = "PhraseSystemFailed";
                    _lastError = "PhraseRecognitionSystem entered Failed state";
                }
                SetLifecycle(
                    SpeechProviderLifecycleState.Failed,
                    "Phrase system failed",
                    "PhraseRecognitionSystem status changed to Failed");
                return;
            }

            if (string.Equals(statusName, "Stopped", StringComparison.OrdinalIgnoreCase) &&
                (_lifecycleState == SpeechProviderLifecycleState.Starting ||
                 _lifecycleState == SpeechProviderLifecycleState.Running))
            {
                if (string.IsNullOrEmpty(_lastErrorCode))
                {
                    _lastErrorCode = "PhraseSystemStopped";
                    _lastError = "PhraseRecognitionSystem stopped while speech input was active";
                }
                SetLifecycle(
                    SpeechProviderLifecycleState.Failed,
                    "Phrase system stopped",
                    "PhraseRecognitionSystem unexpectedly stopped while provider was active");
                return;
            }

            _lastTransition = $"PhraseRecognitionSystem status changed to {statusName}";
        }

        private void HandlePhraseSystemError(SpeechError errorCode)
        {
            if (errorCode == SpeechError.NoError)
            {
                return;
            }

            _lastErrorCode = errorCode.ToString();
            _lastError = errorCode == SpeechError.MicrophoneUnavailable
                ? "PhraseRecognitionSystem reported MicrophoneUnavailable"
                : $"PhraseRecognitionSystem reported {errorCode}";
            _phraseSystemStatus = ReadPhraseSystemStatus();
            SetLifecycle(
                SpeechProviderLifecycleState.Failed,
                $"Speech error: {errorCode}",
                $"PhraseRecognitionSystem.OnError: {errorCode}");
        }

        private void SubscribePhraseSystemEvents()
        {
            if (_phraseSystemSubscribed)
            {
                return;
            }
            PhraseRecognitionSystem.OnStatusChanged += HandlePhraseSystemStatusChanged;
            PhraseRecognitionSystem.OnError += HandlePhraseSystemError;
            _phraseSystemSubscribed = true;
        }

        private void UnsubscribePhraseSystemEvents()
        {
            if (!_phraseSystemSubscribed)
            {
                return;
            }
            PhraseRecognitionSystem.OnStatusChanged -= HandlePhraseSystemStatusChanged;
            PhraseRecognitionSystem.OnError -= HandlePhraseSystemError;
            _phraseSystemSubscribed = false;
        }

        private string ReleaseBackendResources()
        {
            UnsubscribePhraseSystemEvents();
            if (_recognizer == null)
            {
                return string.Empty;
            }

            string error = string.Empty;
            try
            {
                if (_recognizer.IsRunning)
                {
                    _recognizer.Stop();
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }
            finally
            {
                _recognizer.OnPhraseRecognized -= HandlePhraseRecognized;
                try
                {
                    _recognizer.Dispose();
                }
                catch (Exception exception)
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = exception.Message;
                    }
                }
                _recognizer = null;
            }
            return error;
        }

        private void CaptureMicrophoneDiagnostics()
        {
            try
            {
                var devices = Microphone.devices;
                _microphoneDeviceCount = devices == null ? 0 : devices.Length;
                _primaryMicrophoneDevice =
                    devices != null && devices.Length > 0
                        ? devices[0] ?? string.Empty
                        : string.Empty;
                _microphoneDiagnostic = _microphoneDeviceCount == 0
                    ? "Unity reports no microphone devices"
                    : "Device enumeration only; this does not prove Windows Speech access";
            }
            catch (Exception exception)
            {
                _microphoneDeviceCount = -1;
                _primaryMicrophoneDevice = string.Empty;
                _microphoneDiagnostic = $"Microphone.devices failed: {exception.Message}";
            }
        }

        private static string ReadPhraseSystemStatus()
        {
            try
            {
                return PhraseRecognitionSystem.Status.ToString();
            }
            catch (Exception exception)
            {
                return $"Status query failed: {exception.Message}";
            }
        }

        private static SpeechRecognitionConfidence MapConfidence(ConfidenceLevel confidence)
        {
            switch (confidence)
            {
                case ConfidenceLevel.High:
                    return SpeechRecognitionConfidence.High;
                case ConfidenceLevel.Medium:
                    return SpeechRecognitionConfidence.Medium;
                case ConfidenceLevel.Low:
                    return SpeechRecognitionConfidence.Low;
                default:
                    return SpeechRecognitionConfidence.Unknown;
            }
        }
#endif
    }
}
