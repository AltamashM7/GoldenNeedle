using System;
using System.Collections;
using System.Diagnostics;
using Mediapipe.Unity.Experimental;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    public sealed partial class MediaPipePoseProvider
    {
        [Header("Camera/Readback Optimization")]
        [Tooltip("When the existing DirectCPU experiment is enabled, first try to read the WebCamTexture's reusable CPU pixels and resize/orient directly into the pooled TextureFrame. On any per-session failure the proven AsyncGPUReadback DirectCPU path remains the automatic fallback.")]
        [SerializeField] private bool enableWebCamCpuBodyReadback = true;

        private const string WebCamCpuStatusMarker = "\nCPUcam: ";

        private WebCamTexture _webCamCpuSessionCamera;
        private Color32[] _webCamCpuPixels;
        private bool _webCamCpuBodyReadbackSessionAvailable = true;
        private bool _webCamCpuPreparedLastFrame;
        private string _webCamCpuBodyReadbackFallbackReason = string.Empty;

        public bool WebCamCpuBodyReadbackEnabled => enableWebCamCpuBodyReadback;
        public bool WebCamCpuBodyReadbackSessionAvailable => _webCamCpuBodyReadbackSessionAvailable;
        public bool WebCamCpuPreparedLastFrame => _webCamCpuPreparedLastFrame;
        public string WebCamCpuBodyReadbackFallbackReason => _webCamCpuBodyReadbackFallbackReason;
        public float LastWebCamCpuAcquireMilliseconds { get; private set; }
        public float LastWebCamCpuResampleMilliseconds { get; private set; }
        public float LastWebCamCpuPreparationMilliseconds { get; private set; }
        public string ActiveBodyFrameTransferLabel => _webCamCpuPreparedLastFrame
            ? "WebCamCPU"
            : ActiveBodyReadbackPathLabel;

        private void EnsureWebCamCpuReadbackSession()
        {
            if (ReferenceEquals(_webCamCpuSessionCamera, _webCamTexture))
            {
                return;
            }

            _webCamCpuSessionCamera = _webCamTexture;
            _webCamCpuPixels = null;
            _webCamCpuBodyReadbackSessionAvailable = true;
            _webCamCpuPreparedLastFrame = false;
            _webCamCpuBodyReadbackFallbackReason = string.Empty;
            LastWebCamCpuAcquireMilliseconds = 0f;
            LastWebCamCpuResampleMilliseconds = 0f;
            LastWebCamCpuPreparationMilliseconds = 0f;
        }

        private bool CanUseWebCamCpuBodyReadback(TextureFrame textureFrame)
        {
            EnsureWebCamCpuReadbackSession();
            return WebCamCpuFrameResampler.IsEligible(
                enableDirectBodyCpuReadback,
                enableWebCamCpuBodyReadback,
                Status == PoseProviderStatus.Ready && !_shuttingDown && _textureFramePool != null,
                _webCamCpuBodyReadbackSessionAvailable,
                BodyInferenceResourcesMatchCurrentIntent(),
                _webCamTexture != null && _webCamTexture.isPlaying,
                ActualCameraWidth,
                ActualCameraHeight,
                textureFrame.width,
                textureFrame.height,
                textureFrame.format);
        }

        private void PrepareWebCamCpuBodyFrame(
            TextureFrame textureFrame,
            bool flipHorizontally,
            bool flipVertically)
        {
            var preparationStartTicks = Stopwatch.GetTimestamp();
            var sourceWidth = ActualCameraWidth;
            var sourceHeight = ActualCameraHeight;
            var requiredSourcePixels = checked(sourceWidth * sourceHeight);
            if (_webCamCpuPixels == null || _webCamCpuPixels.Length != requiredSourcePixels)
            {
                _webCamCpuPixels = new Color32[requiredSourcePixels];
            }

            var acquireStartTicks = Stopwatch.GetTimestamp();
            var pixels = _webCamTexture.GetPixels32(_webCamCpuPixels);
            var acquireEndTicks = Stopwatch.GetTimestamp();
            if (pixels == null || pixels.Length < requiredSourcePixels)
            {
                throw new InvalidOperationException(
                    $"WebCamTexture.GetPixels32 returned an invalid buffer. expected>={requiredSourcePixels} actual={(pixels == null ? 0 : pixels.Length)}");
            }
            _webCamCpuPixels = pixels;

            var destination = textureFrame.GetRawTextureData<Color32>();
            var resampleStartTicks = Stopwatch.GetTimestamp();
            WebCamCpuFrameResampler.ResampleBilinear(
                _webCamCpuPixels,
                sourceWidth,
                sourceHeight,
                destination,
                textureFrame.width,
                textureFrame.height,
                flipHorizontally,
                flipVertically);
            var resampleEndTicks = Stopwatch.GetTimestamp();

            LastWebCamCpuAcquireMilliseconds = (float)DirectReadbackTimingMath.TicksToMilliseconds(
                acquireEndTicks - acquireStartTicks,
                Stopwatch.Frequency);
            LastWebCamCpuResampleMilliseconds = (float)DirectReadbackTimingMath.TicksToMilliseconds(
                resampleEndTicks - resampleStartTicks,
                Stopwatch.Frequency);
            LastWebCamCpuPreparationMilliseconds = (float)DirectReadbackTimingMath.TicksToMilliseconds(
                resampleEndTicks - preparationStartTicks,
                Stopwatch.Frequency);
            LastGpuReadbackDurationMilliseconds = LastWebCamCpuPreparationMilliseconds;
            _activeBodyReadbackPath = BodyReadbackPath.DirectCPU;
            _activeDirectReadbackStage = LatestFramePipelinePolicy.GetDirectReadbackStage(
                flipHorizontally,
                flipVertically);
            _webCamCpuPreparedLastFrame = true;
            _webCamCpuBodyReadbackFallbackReason = string.Empty;
            _directBodyCpuReadbackFallbackReason = string.Empty;
            _directReadbacksInWindow++;
        }

        private void MarkWebCamCpuBodyReadbackUnavailable(Exception exception)
        {
            _webCamCpuBodyReadbackSessionAvailable = false;
            _webCamCpuPreparedLastFrame = false;
            _webCamCpuBodyReadbackFallbackReason =
                $"{exception.GetType().Name}: {exception.Message}";
            StatusMessage =
                $"WebCam CPU body-frame path unavailable; retaining DirectCPU GPU fallback ({exception.GetType().Name})";
        }

        private void RefreshWebCamCpuStatusSuffix()
        {
            var markerIndex = StatusMessage.IndexOf(WebCamCpuStatusMarker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                StatusMessage = StatusMessage.Substring(0, markerIndex);
            }

            if (!enableDirectBodyCpuReadback || !enableWebCamCpuBodyReadback)
            {
                return;
            }

            if (_webCamCpuPreparedLastFrame)
            {
                StatusMessage +=
                    $"{WebCamCpuStatusMarker}active=1 acq/resize/total={LastWebCamCpuAcquireMilliseconds:0.0}/{LastWebCamCpuResampleMilliseconds:0.0}/{LastWebCamCpuPreparationMilliseconds:0.0} ms stage={ActiveDirectReadbackStageLabel}";
            }
            else if (!_webCamCpuBodyReadbackSessionAvailable)
            {
                StatusMessage +=
                    $"{WebCamCpuStatusMarker}active=0 GPU fallback | {_webCamCpuBodyReadbackFallbackReason}";
            }
        }

        /// <summary>
        /// Readback preparation with a synchronous, reusable WebCamTexture CPU candidate ahead of
        /// the accepted DirectCPU AsyncGPUReadback path. This method deliberately retains the same
        /// one-readback lease, TextureFrame ownership, stale-convention rejection, OpenVINO mailbox
        /// publication and TFLite prepared-frame semantics as CapturePreparedFrameAsync.
        /// </summary>
        private IEnumerator CapturePreparedFrameOptimizedAsync(
            TextureFrame textureFrame,
            double frameObservedAtSeconds,
            int coordinateConventionVersion,
            bool flipHorizontally,
            bool flipVertically)
        {
            AsyncGPUReadbackRequest request = default;
            var readbackStartedAtSeconds = NowSeconds();
            var directRequest = false;
            var directTimedOut = false;
            var synchronousCpuFramePrepared = false;
            var directDiagnosticSerial = 0L;
            var directSubmitStartTicks = 0L;
            var directSubmitReturnTicks = 0L;

            try
            {
                _webCamCpuPreparedLastFrame = false;
                if (CanUseWebCamCpuBodyReadback(textureFrame))
                {
                    try
                    {
                        PrepareWebCamCpuBodyFrame(
                            textureFrame,
                            flipHorizontally,
                            flipVertically);
                        synchronousCpuFramePrepared = true;
                    }
                    catch (Exception exception)
                    {
                        MarkWebCamCpuBodyReadbackUnavailable(exception);
                    }
                }

                if (!synchronousCpuFramePrepared)
                {
                    Texture readbackSource = _webCamTexture;
                    if (_bodyInferenceRenderTexture != null)
                    {
                        Graphics.Blit(_webCamTexture, _bodyInferenceRenderTexture);
                        readbackSource = _bodyInferenceRenderTexture;
                    }

                    var selectedReadbackPath = SelectBodyReadbackPath(
                        textureFrame,
                        flipHorizontally,
                        flipVertically,
                        out var fallbackReason);
                    _activeBodyReadbackPath = selectedReadbackPath;
                    _activeDirectReadbackStage = DirectReadbackStage.None;
                    _directBodyCpuReadbackFallbackReason =
                        selectedReadbackPath == BodyReadbackPath.DirectFallback
                            ? fallbackReason
                            : string.Empty;

                    Texture directReadbackSource = readbackSource;
                    if (selectedReadbackPath == BodyReadbackPath.DirectCPU)
                    {
                        var stage = LatestFramePipelinePolicy.GetDirectReadbackStage(
                            flipHorizontally,
                            flipVertically);
                        if (stage != DirectReadbackStage.None)
                        {
                            if (!DirectReadbackStagingMatchesBodyInput())
                            {
                                _activeBodyReadbackPath = BodyReadbackPath.DirectFallback;
                                _directBodyCpuReadbackFallbackReason = "direct flip staging unavailable";
                            }
                            else
                            {
                                LatestFramePipelinePolicy.GetDirectReadbackFlipTransform(
                                    flipHorizontally,
                                    flipVertically,
                                    out var scale,
                                    out var offset);
                                Graphics.Blit(
                                    _bodyInferenceRenderTexture,
                                    _directBodyReadbackStagingRenderTexture,
                                    scale,
                                    offset);
                                directReadbackSource = _directBodyReadbackStagingRenderTexture;
                                _activeDirectReadbackStage = stage;
                            }
                        }
                    }

                    if (_activeBodyReadbackPath == BodyReadbackPath.DirectCPU)
                    {
                        var rawData = textureFrame.GetRawTextureData<byte>();
                        var expectedByteCount = (long)textureFrame.width * textureFrame.height * 4L;
                        if (rawData.Length != expectedByteCount)
                        {
                            _activeBodyReadbackPath = BodyReadbackPath.DirectFallback;
                            _activeDirectReadbackStage = DirectReadbackStage.None;
                            _directBodyCpuReadbackFallbackReason = "RGBA32 raw buffer size mismatch";
                            request = textureFrame.ReadTextureAsync(
                                readbackSource,
                                flipHorizontally,
                                flipVertically);
                        }
                        else
                        {
                            directDiagnosticSerial = BeginDirectReadbackDiagnostic();
                            directSubmitStartTicks = Stopwatch.GetTimestamp();
                            request = AsyncGPUReadback.RequestIntoNativeArray(
                                ref rawData,
                                directReadbackSource,
                                0,
                                TextureFormat.RGBA32,
                                completedRequest => RecordDirectReadbackCallback(
                                    directDiagnosticSerial,
                                    completedRequest));
                            directSubmitReturnTicks = Stopwatch.GetTimestamp();
                            TrackActiveDirectReadbackRequest(request);
                            directRequest = true;
                            _directReadbacksInWindow++;
                        }
                    }
                    else
                    {
                        request = textureFrame.ReadTextureAsync(
                            readbackSource,
                            flipHorizontally,
                            flipVertically);
                    }
                }
            }
            catch (Exception exception)
            {
                textureFrame.Release();
                _readbackPending = false;
                _readbackFailuresInWindow++;
                _skippedInWindow++;
                if (directRequest || _activeBodyReadbackPath == BodyReadbackPath.DirectCPU)
                {
                    _directReadbackFailuresInWindow++;
                    MarkDirectBodyCpuReadbackUnavailable(
                        $"direct request exception: {exception.GetType().Name}");
                    StatusMessage = $"Direct CPU readback unavailable; using Homuler next frame ({exception.GetType().Name})";
                }
                else
                {
                    SetFailure(PoseProviderStatus.InferenceFailed, $"Camera readback setup failed: {exception.Message}");
                    UnityEngine.Debug.LogException(exception, this);
                }
                RefreshWebCamCpuStatusSuffix();
                yield break;
            }

            if (!synchronousCpuFramePrepared)
            {
                var deadline = Time.realtimeSinceStartup + Mathf.Max(0.25f, readbackTimeoutSeconds);
                if (directRequest)
                {
                    while (!request.done)
                    {
                        if (!directTimedOut && Time.realtimeSinceStartup >= deadline)
                        {
                            directTimedOut = true;
                            _readbackTimeoutsInWindow++;
                            _directReadbackFailuresInWindow++;
                            MarkDirectBodyCpuReadbackUnavailable("direct request timed out");
                            StatusMessage = "Direct CPU readback timed out; holding buffer until completion, then using Homuler";
                        }
                        yield return null;
                    }
                }
                else
                {
                    while (!request.done && Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                    }
                }

                var requestDoneAtCoroutineObservation = request.done;
                var coroutineObserveTicks = Stopwatch.GetTimestamp();
                if (directRequest)
                {
                    ClearActiveDirectReadbackRequestIfTerminal(requestDoneAtCoroutineObservation);
                }

                LastGpuReadbackDurationMilliseconds =
                    (float)((NowSeconds() - readbackStartedAtSeconds) * 1000d);

                if (directRequest && directTimedOut)
                {
                    RecordDirectReadbackTerminalWithoutPublish(directDiagnosticSerial);
                    textureFrame.Release();
                    _readbackPending = false;
                    _skippedInWindow++;
                    RefreshWebCamCpuStatusSuffix();
                    yield break;
                }

                if (!requestDoneAtCoroutineObservation || request.hasError)
                {
                    if (directRequest)
                    {
                        RecordDirectReadbackTerminalWithoutPublish(directDiagnosticSerial);
                    }
                    textureFrame.Release();
                    _readbackPending = false;
                    _skippedInWindow++;
                    if (request.hasError)
                    {
                        _readbackFailuresInWindow++;
                        if (directRequest)
                        {
                            _directReadbackFailuresInWindow++;
                            MarkDirectBodyCpuReadbackUnavailable("direct request returned error");
                            StatusMessage = "Direct CPU readback failed; using Homuler on subsequent frames";
                        }
                        else
                        {
                            StatusMessage = "Camera frame readback failed; retrying";
                        }
                    }
                    else
                    {
                        _readbackTimeoutsInWindow++;
                        StatusMessage = "Camera frame readback timed out; retrying";
                    }
                    RefreshWebCamCpuStatusSuffix();
                    yield break;
                }

                if (_shuttingDown ||
                    coordinateConventionVersion != _coordinateConventionVersion ||
                    !IsActiveBackendReady())
                {
                    if (directRequest)
                    {
                        RecordDirectReadbackTerminalWithoutPublish(directDiagnosticSerial);
                    }
                    textureFrame.Release();
                    _readbackPending = false;
                    RefreshWebCamCpuStatusSuffix();
                    yield break;
                }

                var publishedAtSeconds = NowSeconds();
                var publishedFrameCount = Time.frameCount;
                if (_activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32)
                {
                    LaunchPreparedOpenVinoInference(
                        textureFrame,
                        frameObservedAtSeconds,
                        publishedAtSeconds,
                        publishedFrameCount,
                        PreparedInferenceLaunchOrigin.ReadbackContinuation);
                    if (directRequest)
                    {
                        RecordDirectReadbackTimingSample(
                            directDiagnosticSerial,
                            directSubmitStartTicks,
                            directSubmitReturnTicks,
                            coroutineObserveTicks,
                            Stopwatch.GetTimestamp(),
                            isError: false);
                    }
                    _readbackPending = false;
                    RefreshWebCamCpuStatusSuffix();
                    yield break;
                }

                if (_preparedTextureFrame != null)
                {
                    _preparedTextureFrame.Release();
                    _preparedFrameReplacementsInWindow++;
                }

                _preparedTextureFrame = textureFrame;
                System.Threading.Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 1);
                _preparedFrameObservedAtSeconds = frameObservedAtSeconds;
                _preparedCoordinateConventionVersion = coordinateConventionVersion;
                _preparedFramePublishedAtSeconds = publishedAtSeconds;
                _preparedFramePublishedFrameCount = publishedFrameCount;
                if (directRequest)
                {
                    RecordDirectReadbackTimingSample(
                        directDiagnosticSerial,
                        directSubmitStartTicks,
                        directSubmitReturnTicks,
                        coroutineObserveTicks,
                        Stopwatch.GetTimestamp(),
                        isError: false);
                }
                _readbackPending = false;
                RefreshWebCamCpuStatusSuffix();
                TryImmediateLaunchAfterReadback();
                yield break;
            }

            // Synchronous webcam CPU preparation completed in the current Update call. Re-apply
            // the same stale-frame/backend gate before handing the pooled TextureFrame downstream.
            if (_shuttingDown ||
                coordinateConventionVersion != _coordinateConventionVersion ||
                !IsActiveBackendReady())
            {
                textureFrame.Release();
                _readbackPending = false;
                RefreshWebCamCpuStatusSuffix();
                yield break;
            }

            var cpuPublishedAtSeconds = NowSeconds();
            var cpuPublishedFrameCount = Time.frameCount;
            if (_activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32)
            {
                LaunchPreparedOpenVinoInference(
                    textureFrame,
                    frameObservedAtSeconds,
                    cpuPublishedAtSeconds,
                    cpuPublishedFrameCount,
                    PreparedInferenceLaunchOrigin.ReadbackContinuation);
                _readbackPending = false;
                RefreshWebCamCpuStatusSuffix();
                yield break;
            }

            if (_preparedTextureFrame != null)
            {
                _preparedTextureFrame.Release();
                _preparedFrameReplacementsInWindow++;
            }

            _preparedTextureFrame = textureFrame;
            System.Threading.Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 1);
            _preparedFrameObservedAtSeconds = frameObservedAtSeconds;
            _preparedCoordinateConventionVersion = coordinateConventionVersion;
            _preparedFramePublishedAtSeconds = cpuPublishedAtSeconds;
            _preparedFramePublishedFrameCount = cpuPublishedFrameCount;
            _readbackPending = false;
            RefreshWebCamCpuStatusSuffix();
            TryImmediateLaunchAfterReadback();
        }
    }
}
