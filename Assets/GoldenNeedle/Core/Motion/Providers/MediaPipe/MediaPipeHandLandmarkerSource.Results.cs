using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Hands;
using Mediapipe;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Unity.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    public sealed partial class MediaPipeHandLandmarkerSource
    {
        private void ConsumePendingResult(CanonicalPoseFrame bodyFrame)
        {
            var consumed = false;
            lock (_resultLock)
            {
                if (_hasPendingResult && _callbackSnapshot.sessionVersion == _sessionVersion)
                {
                    _mainSnapshot.CopyFrom(_callbackSnapshot);
                    _hasPendingResult = false;
                    consumed = true;
                }
            }
            if (!consumed)
            {
                return;
            }

            // receivedAtSeconds was captured in the callback from the exact body-provider Stopwatch.
            // Do not reconstruct semantic receive time from Unity time; body/hand age and skew require one epoch.
            _lastResultReceivedAtSeconds = _mainSnapshot.receivedAtSeconds;
            _latestDetectedHandCount = _mainSnapshot.handCount;
            var bodyEvidence = BuildBodyWristEvidence(bodyFrame);
            var first = _mainSnapshot.handCount > 0
                ? MediaPipeHandLandmarkMapper.BuildAssociationCandidate(_mainSnapshot.hands[0])
                : default;
            var second = _mainSnapshot.handCount > 1
                ? MediaPipeHandLandmarkMapper.BuildAssociationCandidate(_mainSnapshot.hands[1])
                : default;

            HandAssociationResult firstResult;
            HandAssociationResult secondResult;
            if (_mainSnapshot.handCount > 1)
            {
                HandAssociationSolver.AssociatePair(in first, in second, in bodyEvidence, out firstResult, out secondResult);
            }
            else if (_mainSnapshot.handCount == 1)
            {
                firstResult = HandAssociationSolver.AssociateSingle(in first, in bodyEvidence);
                secondResult = default;
            }
            else
            {
                firstResult = default;
                secondResult = default;
            }

            if (firstResult.isAssigned)
            {
                MapIntoHeld(_mainSnapshot.hands[0], in firstResult, _lastResultReceivedAtSeconds);
            }
            if (secondResult.isAssigned)
            {
                MapIntoHeld(_mainSnapshot.hands[1], in secondResult, _lastResultReceivedAtSeconds);
            }
        }

        private void MapIntoHeld(MediaPipeHandDetectionSnapshot snapshot, in HandAssociationResult association, double receivedAtSeconds)
        {
            var destination = association.side == CanonicalHandSide.Left ? _mappedFirst : _mappedSecond;
            MediaPipeHandLandmarkMapper.MapDetection(
                snapshot,
                association.side,
                association.mode,
                association.confidence,
                receivedAtSeconds,
                destination);
            if (association.side == CanonicalHandSide.Left)
            {
                _heldLeft.CopyFrom(destination);
            }
            else if (association.side == CanonicalHandSide.Right)
            {
                _heldRight.CopyFrom(destination);
            }
        }

        private static BodyWristEvidence BuildBodyWristEvidence(CanonicalPoseFrame bodyFrame)
        {
            if (bodyFrame == null)
            {
                return default;
            }
            var left = bodyFrame.GetJoint(CanonicalJointId.LeftWrist);
            var right = bodyFrame.GetJoint(CanonicalJointId.RightWrist);
            return new BodyWristEvidence
            {
                hasLeft = left.IsTracked && left.hasImagePosition,
                left = left.imagePosition,
                leftConfidence = left.confidence,
                hasRight = right.IsTracked && right.hasImagePosition,
                right = right.imagePosition,
                rightConfidence = right.confidence,
            };
        }

        private void RestartForCoordinateConvention()
        {
            _lastCoordinateConventionVersion = _bodyProvider.CoordinateConventionVersion;
            _sessionResetCount++;
            ShutdownHandTask(clearModelPath: false);
            ClearSessionState();
            if (handTrackingEnabled && !_initializing)
            {
                StartCoroutine(EnsureInitialized());
            }
        }

        private void RestartTaskAfterTimeout()
        {
            _sessionResetCount++;
            ShutdownHandTask(clearModelPath: false);
            ClearSessionState();
            if (handTrackingEnabled && !_initializing)
            {
                StartCoroutine(EnsureInitialized());
            }
        }

        private void ClearSessionState()
        {
            _sessionVersion++;
            _scheduler.Reset();
            _activeSlot = -1;
            _pendingSlot = -1;
            _activeStartedStopwatchTicks = 0;
            _activeSubmittedTimestampMillisec = 0;
            _latestDetectedHandCount = 0;
            _lastResultReceivedAtSeconds = 0d;
            _rateWindowStartSeconds = 0d;
            _rateWindowSubmittedStart = _submittedCount;
            _rateWindowCallbackStart = _callbackCount;
            _actualSubmissionRate = 0f;
            _actualCallbackRate = 0f;
            Interlocked.Exchange(ref _completedSequence, 0);
            lock (_resultLock)
            {
                _callbackSnapshot.Clear();
                _mainSnapshot.Clear();
                _hasPendingResult = false;
            }
            _heldLeft.Clear(CanonicalHandSide.Left);
            _heldRight.Clear(CanonicalHandSide.Right);
        }

        private void ShutdownHandTask(bool clearModelPath)
        {
            _modelReady = false;
            if (_landmarker != null)
            {
                try
                {
                    ((IDisposable)_landmarker).Dispose();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogWarning($"[Foundation D] Optional hand task shutdown warning: {exception.Message}");
                }
                _landmarker = null;
            }
            if (clearModelPath)
            {
                _verifiedModelPath = null;
            }
        }

        private void EnsureBuffers(int cameraWidth, int cameraHeight)
        {
            var pixelCount = checked(cameraWidth * cameraHeight);
            if (_capturePixels == null || _capturePixels.Length != pixelCount)
            {
                _capturePixels = new Color32[pixelCount];
            }
            var byteCount = checked(checked(handInputWidth * handInputHeight) * 4);
            if (!_slotA.IsCreated || _slotA.Length != byteCount || !_slotB.IsCreated || _slotB.Length != byteCount)
            {
                DisposeBuffers();
                _slotA = new NativeArray<byte>(byteCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                _slotB = new NativeArray<byte>(byteCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
        }

        private void DisposeBuffers()
        {
            if (_slotA.IsCreated) _slotA.Dispose();
            if (_slotB.IsCreated) _slotB.Dispose();
            _capturePixels = null;
        }

        private void UpdateDiagnostics(double cadenceNowSeconds)
        {
            if (_rateWindowStartSeconds <= 0d)
            {
                _rateWindowStartSeconds = cadenceNowSeconds;
                _rateWindowSubmittedStart = _submittedCount;
                _rateWindowCallbackStart = _callbackCount;
            }
            else
            {
                var rateWindow = cadenceNowSeconds - _rateWindowStartSeconds;
                if (rateWindow >= 1d)
                {
                    _actualSubmissionRate = (float)((_submittedCount - _rateWindowSubmittedStart) / rateWindow);
                    _actualCallbackRate = (float)((_callbackCount - _rateWindowCallbackStart) / rateWindow);
                    _rateWindowStartSeconds = cadenceNowSeconds;
                    _rateWindowSubmittedStart = _submittedCount;
                    _rateWindowCallbackStart = _callbackCount;
                }
            }

            var semanticNowSeconds = BodyTimelineSeconds();
            var latestAge = _lastResultReceivedAtSeconds <= 0d || semanticNowSeconds <= 0d
                ? double.PositiveInfinity
                : Math.Max(0d, (semanticNowSeconds - _lastResultReceivedAtSeconds) * 1000d);
            var ageText = double.IsInfinity(latestAge) ? "-" : $"{latestAge:0}ms";
            diagnosticSummary =
                $"Hands: {(IsReady ? "ready" : _initializing ? "initializing" : "waiting")} target/actual={targetHandInferenceFps:0.#}/{_actualCallbackRate:0.0}/s submit={_actualSubmissionRate:0.0}/s " +
                $"det={_latestDetectedHandCount} active={_scheduler.HasActive} pending={_scheduler.HasPending} repl={_scheduler.ReplacedPendingCount} " +
                $"acq={_lastAcquisitionMilliseconds:0.0}ms infer={_lastInferenceMilliseconds:0.0}ms resultAge={ageText} drops={_droppedCaptureCount} resets={_sessionResetCount} " +
                $"L={FormatHandDiagnostic(_heldLeft)} R={FormatHandDiagnostic(_heldRight)}";
        }

        private static string FormatHandDiagnostic(CanonicalHand hand)
        {
            if (hand == null || !hand.isFresh)
            {
                return "unavailable";
            }
            return $"{hand.shape}/{hand.associationMode} age={hand.ageMilliseconds:0}ms skew={hand.bodyHandSkewMilliseconds:0}ms";
        }

        private void SanitizeSettings()
        {
            targetHandInferenceFps = Mathf.Clamp(targetHandInferenceFps, 5f, 30f);
            handInputWidth = Math.Max(64, handInputWidth);
            handInputHeight = Math.Max(64, handInputHeight);
            minimumDetectionConfidence = Mathf.Clamp01(minimumDetectionConfidence);
            minimumPresenceConfidence = Mathf.Clamp01(minimumPresenceConfidence);
            minimumTrackingConfidence = Mathf.Clamp01(minimumTrackingConfidence);
            activeRequestTimeoutMilliseconds = Mathf.Max(250f, activeRequestTimeoutMilliseconds);
            freshnessSettings.Sanitize();
        }

        private static CanonicalHandSide ParseHandedness(string categoryName)
        {
            if (string.Equals(categoryName, "Left", StringComparison.OrdinalIgnoreCase)) return CanonicalHandSide.Left;
            if (string.Equals(categoryName, "Right", StringComparison.OrdinalIgnoreCase)) return CanonicalHandSide.Right;
            return CanonicalHandSide.Unknown;
        }

        private static bool VerifyFileSha256(string path, string expected)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var sha = SHA256.Create();
                return Hex(sha.ComputeHash(stream)) == expected;
            }
            catch
            {
                return false;
            }
        }

        private static bool VerifyBytesSha256(byte[] bytes, string expected)
        {
            if (bytes == null || bytes.Length == 0) return false;
            using var sha = SHA256.Create();
            return Hex(sha.ComputeHash(bytes)) == expected;
        }

        private static string Hex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }

        private static double StopwatchElapsedMilliseconds(long start, long end)
        {
            return Math.Max(0d, (end - start) * 1000d / Stopwatch.Frequency);
        }

        private static float Clamp01NoUnity(float value)
        {
            if (float.IsNaN(value) || value <= 0f) return 0f;
            if (value >= 1f) return 1f;
            return value;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}