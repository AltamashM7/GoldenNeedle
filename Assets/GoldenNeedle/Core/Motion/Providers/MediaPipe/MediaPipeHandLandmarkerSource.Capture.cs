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
        private void CaptureLatestHandFrame(double nowSeconds)
        {
            var cameraTexture = _bodyProvider.CameraTexture as WebCamTexture;
            if (cameraTexture == null || !cameraTexture.isPlaying || cameraTexture.width <= 16 || cameraTexture.height <= 16)
            {
                _droppedCaptureCount++;
                return;
            }

            EnsureBuffers(cameraTexture.width, cameraTexture.height);
            var targetSlot = !_scheduler.HasActive ? 0 : (_activeSlot == 0 ? 1 : 0);
            var targetBuffer = targetSlot == 0 ? _slotA : _slotB;
            var startTicks = Stopwatch.GetTimestamp();
            cameraTexture.GetPixels32(_capturePixels);
            WebCamCpuFramePreparation.PrepareRgba(
                _capturePixels,
                cameraTexture.width,
                cameraTexture.height,
                targetBuffer,
                handInputWidth,
                handInputHeight,
                _bodyProvider.Orientation.InferenceFlipHorizontally,
                _bodyProvider.Orientation.InferenceFlipVertically);
            _lastAcquisitionMilliseconds = StopwatchElapsedMilliseconds(startTicks, Stopwatch.GetTimestamp());

            var timestampMillisec = Math.Max(
                _lastSubmittedTimestampMillisec + 1,
                (long)Math.Round(nowSeconds * 1000d));
            _lastSubmittedTimestampMillisec = timestampMillisec;
            _scheduler.Offer(timestampMillisec, out var launchNow);
            if (launchNow)
            {
                _activeSlot = targetSlot;
                SubmitActive(targetSlot, timestampMillisec);
            }
            else
            {
                _pendingSlot = targetSlot;
            }
        }

        private void SubmitActive(int slot, long timestampMillisec)
        {
            if (_landmarker == null)
            {
                return;
            }
            var buffer = slot == 0 ? _slotA : _slotB;
            _imageProcessingOptions = new ImageProcessingOptions(
                rotationDegrees: _bodyProvider.Orientation.InferenceRotationDegrees);
            using var image = new Mediapipe.Image(
                ImageFormat.Types.Format.Srgba,
                handInputWidth,
                handInputHeight,
                handInputWidth * 4,
                buffer);
            _activeSubmittedTimestampMillisec = timestampMillisec;
            _activeStartedStopwatchTicks = Stopwatch.GetTimestamp();
            _landmarker.DetectAsync(image, timestampMillisec, _imageProcessingOptions);
            _submittedCount++;
        }

        private void OnHandResult(HandLandmarkerResult result, long timestampMillisec, int callbackSession)
        {
            if (callbackSession != Volatile.Read(ref _sessionVersion))
            {
                return;
            }

            lock (_resultLock)
            {
                if (callbackSession != _sessionVersion)
                {
                    return;
                }
                _callbackSnapshot.Clear();
                _callbackSnapshot.sessionVersion = callbackSession;
                _callbackSnapshot.sourceTimestampMillisec = timestampMillisec;
                _callbackSnapshot.callbackStopwatchTicks = Stopwatch.GetTimestamp();
                CopyTaskResult(result, timestampMillisec, _callbackSnapshot);
                _hasPendingResult = true;
            }

            if (timestampMillisec == Volatile.Read(ref _activeSubmittedTimestampMillisec))
            {
                var start = Volatile.Read(ref _activeStartedStopwatchTicks);
                if (start > 0)
                {
                    _lastInferenceMilliseconds = StopwatchElapsedMilliseconds(start, Stopwatch.GetTimestamp());
                }
                Interlocked.Exchange(ref _completedSequence, timestampMillisec);
            }
            Interlocked.Increment(ref _callbackCount);
        }

        private static void CopyTaskResult(HandLandmarkerResult result, long timestampMillisec, MediaPipeHandResultSnapshot destination)
        {
            var handLandmarks = result.handLandmarks;
            var worldLandmarks = result.handWorldLandmarks;
            var handedness = result.handedness;
            var count = handLandmarks == null ? 0 : Math.Min(2, handLandmarks.Count);
            destination.handCount = count;
            for (var handIndex = 0; handIndex < count; handIndex++)
            {
                var target = destination.hands[handIndex];
                target.Clear();
                target.sourceTimestampMillisec = timestampMillisec;

                var normalized = handLandmarks[handIndex].landmarks;
                var localWorld = worldLandmarks != null && handIndex < worldLandmarks.Count
                    ? worldLandmarks[handIndex].landmarks
                    : null;
                target.landmarkCount = Math.Min(
                    CanonicalHandSchema.LandmarkCount,
                    normalized == null ? 0 : normalized.Count);
                for (var landmarkIndex = 0; landmarkIndex < target.landmarkCount; landmarkIndex++)
                {
                    var value = normalized[landmarkIndex];
                    target.normalized[landmarkIndex] = new MediaPipeHandPointSnapshot
                    {
                        valid = IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z),
                        x = value.x,
                        y = value.y,
                        z = value.z,
                    };
                    if (localWorld != null && landmarkIndex < localWorld.Count)
                    {
                        var world = localWorld[landmarkIndex];
                        target.handLocal[landmarkIndex] = new MediaPipeHandPointSnapshot
                        {
                            valid = IsFinite(world.x) && IsFinite(world.y) && IsFinite(world.z),
                            x = world.x,
                            y = world.y,
                            z = world.z,
                        };
                    }
                }

                if (handedness != null && handIndex < handedness.Count && handedness[handIndex].categories != null && handedness[handIndex].categories.Count > 0)
                {
                    var category = handedness[handIndex].categories[0];
                    target.handedness = ParseHandedness(category.categoryName);
                    target.handednessScore = Clamp01NoUnity(category.score);
                }
            }
        }

        private void ProcessCompletedActive()
        {
            var completed = Interlocked.Read(ref _completedSequence);
            if (completed <= 0 || !_scheduler.HasActive || completed != _scheduler.ActiveSequence)
            {
                return;
            }
            Interlocked.Exchange(ref _completedSequence, 0);
            if (_scheduler.CompleteActive(out var nextSequence))
            {
                _activeSlot = _pendingSlot;
                _pendingSlot = -1;
                SubmitActive(_activeSlot, nextSequence);
            }
            else
            {
                _activeSlot = -1;
                _activeStartedStopwatchTicks = 0;
            }
        }
    }
}
