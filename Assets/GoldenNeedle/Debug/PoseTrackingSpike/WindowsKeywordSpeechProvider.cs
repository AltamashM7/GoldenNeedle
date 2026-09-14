using System;
using System.Collections.Generic;
using GoldenNeedle.Core.Commands;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
using UnityEngine.Windows.Speech;
#endif

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    /// <summary>
    /// Windows-first speech backend. It emits only recognized phrase/confidence samples and has
    /// no knowledge of Golden Needle calibration, locomotion, cameras or presentation actions.
    /// </summary>
    public sealed class WindowsKeywordSpeechProvider : ISpeechInputProvider
    {
        private readonly string[] _keywords;
        private bool _disposed;
        private string _status;
        private string _lastError;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
        private KeywordRecognizer _recognizer;
        private bool _errorSubscribed;
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
            IsSupported = PhraseRecognitionSystem.isSupported;
            _status = IsSupported ? "Ready" : "Unsupported on this Windows runtime";
            if (!IsSupported)
            {
                _lastError = "Unity PhraseRecognitionSystem reports speech recognition is unsupported";
            }
#else
            IsSupported = false;
            _status = "Unsupported on this platform";
            _lastError = "Unity Windows speech recognition is available only on supported Windows runtimes";
#endif
        }

        public event Action<SpeechRecognitionSample> PhraseRecognized;

        public string BackendName => "Unity Windows KeywordRecognizer";
        public string Status => _status ?? string.Empty;
        public string LastError => _lastError ?? string.Empty;
        public bool IsSupported { get; }

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

        public bool Start()
        {
            if (_disposed)
            {
                _status = "Disposed";
                _lastError = "Speech provider has already been disposed";
                return false;
            }
            if (!IsSupported)
            {
                return false;
            }
            if (_keywords.Length == 0)
            {
                _status = "No keywords";
                _lastError = "Cannot start KeywordRecognizer without effective phrases";
                return false;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
            if (_recognizer != null && _recognizer.IsRunning)
            {
                _status = "Listening";
                return true;
            }

            try
            {
                DisposeRecognizer();
                // Low is intentionally the recognizer-wide threshold. The project-owned resolver
                // applies each mapping's stricter minimum confidence after recognition.
                _recognizer = new KeywordRecognizer(_keywords, ConfidenceLevel.Low);
                _recognizer.OnPhraseRecognized += HandlePhraseRecognized;
                PhraseRecognitionSystem.OnError += HandlePhraseSystemError;
                _errorSubscribed = true;
                _recognizer.Start();
                _lastError = string.Empty;
                _status = _recognizer.IsRunning ? "Listening" : "Start requested";
                return _recognizer.IsRunning;
            }
            catch (Exception exception)
            {
                _status = "Start error";
                _lastError = exception.Message;
                DisposeRecognizer();
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
            try
            {
                if (_recognizer != null && _recognizer.IsRunning)
                {
                    _recognizer.Stop();
                }
                _status = IsSupported ? "Stopped" : _status;
            }
            catch (Exception exception)
            {
                _status = "Stop error";
                _lastError = exception.Message;
            }
#endif
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
            DisposeRecognizer();
#endif
            _disposed = true;
            _status = "Disposed";
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
        private void HandlePhraseRecognized(PhraseRecognizedEventArgs args)
        {
            try
            {
                PhraseRecognized?.Invoke(new SpeechRecognitionSample(
                    args.text,
                    MapConfidence(args.confidence)));
            }
            catch (Exception exception)
            {
                _status = "Recognition callback error";
                _lastError = exception.Message;
            }
        }

        private void HandlePhraseSystemError(SpeechError errorCode)
        {
            if (errorCode == SpeechError.NoError)
            {
                return;
            }
            _status = $"Speech error: {errorCode}";
            _lastError = errorCode.ToString();
        }

        private void DisposeRecognizer()
        {
            if (_errorSubscribed)
            {
                PhraseRecognitionSystem.OnError -= HandlePhraseSystemError;
                _errorSubscribed = false;
            }

            if (_recognizer == null)
            {
                return;
            }

            try
            {
                if (_recognizer.IsRunning)
                {
                    _recognizer.Stop();
                }
            }
            catch (Exception exception)
            {
                _lastError = exception.Message;
            }
            finally
            {
                _recognizer.OnPhraseRecognized -= HandlePhraseRecognized;
                _recognizer.Dispose();
                _recognizer = null;
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
