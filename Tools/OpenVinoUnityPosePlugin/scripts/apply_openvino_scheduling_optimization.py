from pathlib import Path

PROVIDER = Path("Assets/GoldenNeedle/Core/Motion/Providers/MediaPipe/MediaPipePoseProvider.cs")
TESTS = Path("Assets/GoldenNeedle/Tests/Editor/MediaPipeInferenceSchedulingTests.cs")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


def replace_block(text: str, start: str, end: str, new_block: str, label: str) -> str:
    start_index = text.find(start)
    if start_index < 0:
        raise SystemExit(f"{label}: start marker not found")
    end_index = text.find(end, start_index)
    if end_index < 0:
        raise SystemExit(f"{label}: end marker not found")
    return text[:start_index] + new_block + text[end_index:]


provider = PROVIDER.read_text(encoding="utf-8")
tests = TESTS.read_text(encoding="utf-8")

if "public sealed class OpenVinoLatestFrameMailbox" in provider:
    required = [
        "PreparedInferenceLaunchOrigin.OpenVinoWorkerContinuation",
        "OpenVinoWorkerLoop()",
        "OpenVinoLatestFrameMailbox",
        "OVW",
        "U/RB/OVW=",
    ]
    missing = [item for item in required if item not in provider]
    if missing:
        raise SystemExit(f"existing scheduling optimization is incomplete: {missing}")
    if "OpenVinoMailboxReplacesPendingWithoutGrowing" not in tests:
        raise SystemExit("provider is optimized but deterministic mailbox tests are missing")
    print("OPENVINO_SCHEDULING_TRANSFORM=ALREADY_APPLIED")
    raise SystemExit(0)

provider = replace_once(
    provider,
    "using UnityEngine;\nusing UnityEngine.Rendering;",
    "using Unity.Collections;\nusing UnityEngine;\nusing UnityEngine.Rendering;",
    "Unity.Collections import",
)

provider = replace_once(
    provider,
    "    public enum PreparedInferenceLaunchOrigin\n    {\n        None,\n        Update,\n        ReadbackContinuation,\n    }",
    "    public enum PreparedInferenceLaunchOrigin\n    {\n        None,\n        Update,\n        ReadbackContinuation,\n        OpenVinoWorkerContinuation,\n    }",
    "launch-origin enum",
)

old_scheduler_start = "    public sealed class InferenceLaunchScheduler\n    {"
old_scheduler_end = "    /// <summary>\n    /// Pure policy surface for deterministic tests of the bounded latest-frame pipeline."
new_scheduler = '''    public sealed class InferenceLaunchScheduler
    {
        private readonly object _gate = new object();
        private bool _hasAcceptedRequest;
        private double _lastAcceptedRequestAtSeconds;

        public bool HasAcceptedRequest
        {
            get
            {
                lock (_gate)
                {
                    return _hasAcceptedRequest;
                }
            }
        }

        public double LastAcceptedRequestAtSeconds
        {
            get
            {
                lock (_gate)
                {
                    return _lastAcceptedRequestAtSeconds;
                }
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                _hasAcceptedRequest = false;
                _lastAcceptedRequestAtSeconds = 0d;
            }
        }

        public double SecondsUntilIntervalElapsed(double nowSeconds, double minimumIntervalSeconds)
        {
            lock (_gate)
            {
                if (!_hasAcceptedRequest)
                {
                    return 0d;
                }

                var remaining = Math.Max(0d, minimumIntervalSeconds) -
                    (nowSeconds - _lastAcceptedRequestAtSeconds);
                return Math.Max(0d, remaining);
            }
        }

        public bool IsIntervalElapsed(double nowSeconds, double minimumIntervalSeconds)
        {
            return SecondsUntilIntervalElapsed(nowSeconds, minimumIntervalSeconds) <= 0d;
        }

        public bool CanLaunch(
            double nowSeconds,
            double minimumIntervalSeconds,
            bool busy,
            bool preparedFrameAvailable = true)
        {
            return preparedFrameAvailable &&
                !busy &&
                IsIntervalElapsed(nowSeconds, minimumIntervalSeconds);
        }

        public void MarkAccepted(double nowSeconds)
        {
            lock (_gate)
            {
                _hasAcceptedRequest = true;
                _lastAcceptedRequestAtSeconds = nowSeconds;
            }
        }
    }

'''
provider = replace_block(
    provider,
    old_scheduler_start,
    old_scheduler_end,
    new_scheduler,
    "thread-safe inference scheduler",
)

old_window_start = "    public sealed class InferenceContinuationTimingWindow\n    {"
old_window_end = "    /// <summary>\n    /// Main-thread request cadence helper."
new_window = '''    public sealed class InferenceContinuationTimingWindow
    {
        private readonly object _gate = new object();
        private readonly InferenceContinuationTimingSample[] _samples;
        private readonly double[] _scratch;
        private int _nextIndex;
        private int _count;
        private int _missingSampleCount;

        public InferenceContinuationTimingWindow(int capacity = 64)
        {
            capacity = Math.Max(1, capacity);
            _samples = new InferenceContinuationTimingSample[capacity];
            _scratch = new double[capacity];
        }

        public int Capacity => _samples.Length;
        public int ValidSampleCount
        {
            get
            {
                lock (_gate)
                {
                    return _count;
                }
            }
        }
        public int MissingSampleCount
        {
            get
            {
                lock (_gate)
                {
                    return _missingSampleCount;
                }
            }
        }
        public int PreparedWaitingSampleCount
        {
            get
            {
                lock (_gate)
                {
                    return CountPreparedWaiting();
                }
            }
        }

        public double PreparedWaitingRate
        {
            get
            {
                lock (_gate)
                {
                    return _count == 0 ? double.NaN : CountPreparedWaiting() / (double)_count;
                }
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                _nextIndex = 0;
                _count = 0;
                _missingSampleCount = 0;
            }
        }

        public void RecordMissingSample()
        {
            lock (_gate)
            {
                _missingSampleCount++;
            }
        }

        public void Add(in InferenceContinuationTimingSample sample)
        {
            lock (_gate)
            {
                _samples[_nextIndex] = sample;
                _nextIndex = (_nextIndex + 1) % _samples.Length;
                _count = Math.Min(_count + 1, _samples.Length);
            }
        }

        public int GetOriginSampleCount(PreparedInferenceLaunchOrigin origin, bool preparedOnly = false)
        {
            lock (_gate)
            {
                var count = 0;
                for (var i = 0; i < _count; i++)
                {
                    if (_samples[i].NextLaunchOrigin == origin && (!preparedOnly || _samples[i].PreparedWaitingAtResult))
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public double GetMedianMilliseconds(InferenceContinuationTimingMetric metric, bool preparedOnly = false)
        {
            lock (_gate)
            {
                var count = CopyMetric(metric, preparedOnly);
                if (count == 0)
                {
                    return double.NaN;
                }

                Array.Sort(_scratch, 0, count);
                var middle = count / 2;
                return count % 2 == 0
                    ? (_scratch[middle - 1] + _scratch[middle]) * 0.5d
                    : _scratch[middle];
            }
        }

        public double GetP95Milliseconds(InferenceContinuationTimingMetric metric, bool preparedOnly = false)
        {
            lock (_gate)
            {
                var count = CopyMetric(metric, preparedOnly);
                if (count == 0)
                {
                    return double.NaN;
                }

                Array.Sort(_scratch, 0, count);
                var index = Math.Min(count - 1, Math.Max(0, (int)Math.Ceiling(count * 0.95d) - 1));
                return _scratch[index];
            }
        }

        private int CountPreparedWaiting()
        {
            var count = 0;
            for (var i = 0; i < _count; i++)
            {
                if (_samples[i].PreparedWaitingAtResult)
                {
                    count++;
                }
            }
            return count;
        }

        private int CopyMetric(InferenceContinuationTimingMetric metric, bool preparedOnly)
        {
            var count = 0;
            for (var i = 0; i < _count; i++)
            {
                if (preparedOnly && !_samples[i].PreparedWaitingAtResult)
                {
                    continue;
                }

                _scratch[count++] = metric switch
                {
                    InferenceContinuationTimingMetric.SubmitCall => _samples[i].SubmitCallMilliseconds,
                    InferenceContinuationTimingMetric.RequestToResult => _samples[i].RequestToResultMilliseconds,
                    InferenceContinuationTimingMetric.ResultToNextLaunch => _samples[i].ResultToNextLaunchMilliseconds,
                    _ => double.NaN,
                };
            }
            return count;
        }
    }

'''
provider = replace_block(
    provider,
    old_window_start,
    old_window_end,
    new_window,
    "thread-safe continuation timing window",
)

provider = replace_once(
    provider,
    "                (nextLaunchOrigin != PreparedInferenceLaunchOrigin.Update &&\n                 nextLaunchOrigin != PreparedInferenceLaunchOrigin.ReadbackContinuation))",
    "                (nextLaunchOrigin != PreparedInferenceLaunchOrigin.Update &&\n                 nextLaunchOrigin != PreparedInferenceLaunchOrigin.ReadbackContinuation &&\n                 nextLaunchOrigin != PreparedInferenceLaunchOrigin.OpenVinoWorkerContinuation))",
    "continuation timing origin validation",
)

mailbox_code = '''    /// <summary>
    /// Two-buffer, one-slot latest-frame mailbox used only by the experimental OpenVINO backend.
    /// One slot may be owned by native inference while the other is the single replaceable pending
    /// frame. There is deliberately no FIFO/history/catch-up queue.
    /// </summary>
    public sealed class OpenVinoLatestFrameMailbox
    {
        public sealed class Frame
        {
            internal Frame(int slotId)
            {
                SlotId = slotId;
            }

            internal byte[] Rgba;
            public int SlotId { get; }
            public int Width { get; internal set; }
            public int Height { get; internal set; }
            public int StrideBytes { get; internal set; }
            public int RotationDegrees { get; internal set; }
            public long TimestampMillisec { get; internal set; }
            public double ObservedAtSeconds { get; internal set; }
            public double PublishedAtSeconds { get; internal set; }
            public int PublishedFrameCount { get; internal set; }
            public int CoordinateConventionVersion { get; internal set; }
            public PreparedInferenceLaunchOrigin LaunchOriginHint { get; internal set; }
            public int ByteCount => Rgba == null ? 0 : Rgba.Length;
        }

        private readonly object _gate = new object();
        private readonly Frame _slotA = new Frame(0);
        private readonly Frame _slotB = new Frame(1);
        private Frame _active;
        private Frame _pending;
        private bool _accepting = true;
        private int _bufferAllocationCount;

        public int PendingCount
        {
            get
            {
                lock (_gate)
                {
                    return _pending == null ? 0 : 1;
                }
            }
        }

        public bool HasActive
        {
            get
            {
                lock (_gate)
                {
                    return _active != null;
                }
            }
        }

        public int BufferAllocationCount
        {
            get
            {
                lock (_gate)
                {
                    return _bufferAllocationCount;
                }
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                if (_active != null)
                {
                    throw new InvalidOperationException("Cannot reset OpenVINO mailbox while a frame is active.");
                }
                _pending = null;
                _accepting = true;
            }
        }

        public bool Publish(
            NativeArray<byte> rgba,
            int width,
            int height,
            int strideBytes,
            int rotationDegrees,
            long timestampMillisec,
            double observedAtSeconds,
            double publishedAtSeconds,
            int publishedFrameCount,
            int coordinateConventionVersion,
            PreparedInferenceLaunchOrigin launchOriginHint,
            out bool replacedPending)
        {
            if (!rgba.IsCreated)
            {
                throw new ArgumentException("RGBA source must be created.", nameof(rgba));
            }
            if (width <= 0 || height <= 0 || strideBytes != checked(width * 4) ||
                rgba.Length != checked(strideBytes * height))
            {
                throw new ArgumentException("OpenVINO mailbox RGBA dimensions/stride are invalid.");
            }

            lock (_gate)
            {
                if (!_accepting)
                {
                    replacedPending = false;
                    return false;
                }

                var target = _pending;
                replacedPending = target != null;
                if (target == null)
                {
                    target = ReferenceEquals(_active, _slotA) ? _slotB : _slotA;
                }
                if (ReferenceEquals(target, _active))
                {
                    throw new InvalidOperationException("OpenVINO mailbox attempted to overwrite the active inference buffer.");
                }

                if (target.Rgba == null || target.Rgba.Length != rgba.Length)
                {
                    target.Rgba = new byte[rgba.Length];
                    _bufferAllocationCount++;
                }
                rgba.CopyTo(target.Rgba);
                target.Width = width;
                target.Height = height;
                target.StrideBytes = strideBytes;
                target.RotationDegrees = rotationDegrees;
                target.TimestampMillisec = timestampMillisec;
                target.ObservedAtSeconds = observedAtSeconds;
                target.PublishedAtSeconds = publishedAtSeconds;
                target.PublishedFrameCount = publishedFrameCount;
                target.CoordinateConventionVersion = coordinateConventionVersion;
                target.LaunchOriginHint = launchOriginHint;
                _pending = target;
                return true;
            }
        }

        public bool TryTake(int currentCoordinateConventionVersion, out Frame frame)
        {
            lock (_gate)
            {
                frame = null;
                if (!_accepting || _active != null || _pending == null)
                {
                    return false;
                }
                if (_pending.CoordinateConventionVersion != currentCoordinateConventionVersion)
                {
                    _pending = null;
                    return false;
                }

                frame = _pending;
                _pending = null;
                _active = frame;
                return true;
            }
        }

        public void Complete(Frame frame)
        {
            if (frame == null)
            {
                return;
            }
            lock (_gate)
            {
                if (!ReferenceEquals(_active, frame))
                {
                    throw new InvalidOperationException("OpenVINO mailbox completion did not match the active frame.");
                }
                _active = null;
            }
        }

        public bool ClearPending()
        {
            lock (_gate)
            {
                var hadPending = _pending != null;
                _pending = null;
                return hadPending;
            }
        }

        public void StopAcceptingAndClear()
        {
            lock (_gate)
            {
                _accepting = false;
                _pending = null;
            }
        }
    }

    public static class OpenVinoSchedulingPolicy
    {
        public static long NextMonotonicTimestamp(long clockTimestampMillisec, long previousTimestampMillisec)
        {
            return Math.Max(clockTimestampMillisec, previousTimestampMillisec + 1L);
        }
    }

'''
provider = replace_once(
    provider,
    "    /// <summary>\n    /// Selects the texture dimensions used only by the body-pose inference path.",
    mailbox_code + "    /// <summary>\n    /// Selects the texture dimensions used only by the body-pose inference path.",
    "OpenVINO latest-frame mailbox insertion",
)

provider = replace_once(
    provider,
    "        private OpenVinoPoseRuntime _openVinoPoseRuntime;\n        private Task<OpenVinoPoseRuntime> _openVinoBootstrapTask;\n        private Task _openVinoInferenceTask;\n        private byte[] _openVinoRgbaBuffer;",
    "        private OpenVinoPoseRuntime _openVinoPoseRuntime;\n        private Task<OpenVinoPoseRuntime> _openVinoBootstrapTask;\n        private readonly OpenVinoLatestFrameMailbox _openVinoMailbox = new OpenVinoLatestFrameMailbox();\n        private AutoResetEvent _openVinoWorkerSignal;\n        private Task _openVinoWorkerTask;\n        private int _openVinoWorkerStopRequested;\n        private int _openVinoMailboxPaused;\n        private long _openVinoLastPublishedTimestampMillisec;\n        private int _openVinoWorkerContinuationLaunchesInWindow;\n        private int _latestMainThreadFrameCount;",
    "OpenVINO worker/mailbox fields",
)

provider = replace_once(
    provider,
    "        public float ImmediateLaunchesPerSecond { get; private set; }\n        public float DirectReadbacksPerSecond { get; private set; }",
    "        public float ImmediateLaunchesPerSecond { get; private set; }\n        public float OpenVinoWorkerContinuationsPerSecond { get; private set; }\n        public int OpenVinoMailboxPendingCount => _openVinoMailbox.PendingCount;\n        public bool OpenVinoMailboxHasActive => _openVinoMailbox.HasActive;\n        public int OpenVinoMailboxBufferAllocationCount => _openVinoMailbox.BufferAllocationCount;\n        public float DirectReadbacksPerSecond { get; private set; }",
    "OpenVINO scheduling public telemetry",
)

provider = replace_once(
    provider,
    "        public bool HasPreparedFrame => _preparedTextureFrame != null;",
    "        public bool HasPreparedFrame => _preparedTextureFrame != null || _openVinoMailbox.PendingCount != 0;",
    "prepared-frame telemetry",
)

provider = replace_once(
    provider,
    "        public string LastAcceptedLaunchOriginLabel =>\n            _lastAcceptedLaunchOrigin == PreparedInferenceLaunchOrigin.ReadbackContinuation\n                ? \"RB\"\n                : _lastAcceptedLaunchOrigin == PreparedInferenceLaunchOrigin.Update\n                    ? \"Update\"\n                    : \"-\";",
    "        public string LastAcceptedLaunchOriginLabel =>\n            _lastAcceptedLaunchOrigin == PreparedInferenceLaunchOrigin.OpenVinoWorkerContinuation\n                ? \"OVW\"\n                : _lastAcceptedLaunchOrigin == PreparedInferenceLaunchOrigin.ReadbackContinuation\n                    ? \"RB\"\n                    : _lastAcceptedLaunchOrigin == PreparedInferenceLaunchOrigin.Update\n                        ? \"Update\"\n                        : \"-\";",
    "launch origin label",
)

provider = replace_once(
    provider,
    "        private void Update()\n        {\n            if (_bootstrapCoroutine != null",
    "        private void Update()\n        {\n            Volatile.Write(ref _latestMainThreadFrameCount, Time.frameCount);\n\n            if (_bootstrapCoroutine != null",
    "main-thread frame-count publication",
)

provider = replace_once(
    provider,
    "            if (_activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32)\n            {\n                LaunchPreparedOpenVinoInference(\n                    textureFrame,\n                    frameObservedAtSeconds,\n                    framePublishedAtSeconds,\n                    framePublishedFrameCount,\n                    origin);\n                return;\n            }",
    "            if (_activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32)\n            {\n                LaunchPreparedOpenVinoInference(\n                    textureFrame,\n                    frameObservedAtSeconds,\n                    framePublishedAtSeconds,\n                    framePublishedFrameCount,\n                    origin);\n                return;\n            }",
    "OpenVINO launch branch retained",
)

old_ov_method_start = "        private void LaunchPreparedOpenVinoInference(\n"
old_ov_method_end = "        private void OnOpenVinoPoseResult(OpenVinoPoseFrameResult result)\n"
new_ov_methods = '''        private void LaunchPreparedOpenVinoInference(
            TextureFrame textureFrame,
            double frameObservedAtSeconds,
            double framePublishedAtSeconds,
            int framePublishedFrameCount,
            PreparedInferenceLaunchOrigin origin)
        {
            var frameReleased = false;
            try
            {
                if (_openVinoPoseRuntime == null || _openVinoWorkerTask == null || _openVinoWorkerSignal == null)
                {
                    throw new InvalidOperationException("OpenVINO pose worker is not ready.");
                }

                var width = textureFrame.width;
                var height = textureFrame.height;
                var strideBytes = checked(width * 4);
                var expectedByteCount = checked(strideBytes * height);
                var rawData = textureFrame.GetRawTextureData<byte>();
                if (rawData.Length != expectedByteCount)
                {
                    throw new InvalidOperationException(
                        $"OpenVINO RGBA input size mismatch. expected={expectedByteCount} actual={rawData.Length}");
                }

                var timestampMillisec = OpenVinoSchedulingPolicy.NextMonotonicTimestamp(
                    _clock.ElapsedMilliseconds,
                    _openVinoLastPublishedTimestampMillisec);
                _openVinoLastPublishedTimestampMillisec = timestampMillisec;
                var publishedAtSeconds = framePublishedAtSeconds > 0d
                    ? framePublishedAtSeconds
                    : NowSeconds();
                var publishedFrameCount = framePublishedFrameCount >= 0
                    ? framePublishedFrameCount
                    : Volatile.Read(ref _latestMainThreadFrameCount);

                var copyStartedAtSeconds = NowSeconds();
                var accepted = _openVinoMailbox.Publish(
                    rawData,
                    width,
                    height,
                    strideBytes,
                    _orientation.InferenceRotationDegrees,
                    timestampMillisec,
                    frameObservedAtSeconds,
                    publishedAtSeconds,
                    publishedFrameCount,
                    _coordinateConventionVersion,
                    origin,
                    out var replacedPending);
                var managedCopyMilliseconds = (float)((NowSeconds() - copyStartedAtSeconds) * 1000d);
                LastCpuImageBuildDurationMilliseconds = managedCopyMilliseconds;
                LastOpenVinoManagedInputCopyMilliseconds = managedCopyMilliseconds;

                textureFrame.Release();
                frameReleased = true;
                if (!accepted)
                {
                    return;
                }

                if (replacedPending)
                {
                    Interlocked.Increment(ref _preparedFrameReplacementsInWindow);
                }
                Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 1);
                _openVinoWorkerSignal.Set();
            }
            catch (Exception exception)
            {
                if (!frameReleased)
                {
                    textureFrame.Release();
                }
                Volatile.Write(ref _activeInferenceDiagnosticAccepted, 0);
                SetFailure(PoseProviderStatus.InferenceFailed, $"OpenVINO frame publication failed: {exception.Message}");
                UnityEngine.Debug.LogException(exception, this);
            }
        }

        private void StartOpenVinoWorker()
        {
            if (_openVinoWorkerTask != null)
            {
                throw new InvalidOperationException("OpenVINO pose worker is already running.");
            }

            _openVinoMailbox.Reset();
            Volatile.Write(ref _openVinoWorkerStopRequested, 0);
            Volatile.Write(ref _openVinoMailboxPaused, 0);
            _openVinoLastPublishedTimestampMillisec = 0L;
            _openVinoWorkerSignal = new AutoResetEvent(false);
            _openVinoWorkerTask = Task.Run(OpenVinoWorkerLoop);
        }

        private void OpenVinoWorkerLoop()
        {
            var continueFromPreviousResult = false;
            while (Volatile.Read(ref _openVinoWorkerStopRequested) == 0)
            {
                if (Volatile.Read(ref _openVinoMailboxPaused) != 0)
                {
                    continueFromPreviousResult = false;
                    _openVinoWorkerSignal.WaitOne();
                    continue;
                }

                if (_openVinoMailbox.PendingCount == 0)
                {
                    continueFromPreviousResult = false;
                    _openVinoWorkerSignal.WaitOne();
                    continue;
                }

                var targetFps = Math.Max(1f, Volatile.Read(ref targetInferenceFps));
                var intervalSeconds = 1d / targetFps;
                var waitSeconds = _inferenceScheduler.SecondsUntilIntervalElapsed(
                    NowSeconds(),
                    intervalSeconds);
                if (waitSeconds > 0d)
                {
                    var waitMilliseconds = Math.Max(1, (int)Math.Ceiling(waitSeconds * 1000d));
                    _openVinoWorkerSignal.WaitOne(waitMilliseconds);
                    continue;
                }

                if (!_openVinoMailbox.TryTake(
                        Volatile.Read(ref _coordinateConventionVersion),
                        out var frame))
                {
                    Volatile.Write(
                        ref _preparedTextureFrameDiagnosticOccupied,
                        _openVinoMailbox.PendingCount == 0 ? 0 : 1);
                    continue;
                }

                Volatile.Write(
                    ref _preparedTextureFrameDiagnosticOccupied,
                    _openVinoMailbox.PendingCount == 0 ? 0 : 1);
                var origin = continueFromPreviousResult
                    ? PreparedInferenceLaunchOrigin.OpenVinoWorkerContinuation
                    : frame.LaunchOriginHint;
                continueFromPreviousResult = ProcessOpenVinoMailboxFrame(frame, origin);
            }
        }

        private bool ProcessOpenVinoMailboxFrame(
            OpenVinoLatestFrameMailbox.Frame frame,
            PreparedInferenceLaunchOrigin origin)
        {
            try
            {
                if (_openVinoPoseRuntime == null)
                {
                    throw new InvalidOperationException("OpenVINO pose runtime is not ready.");
                }
                if (Volatile.Read(ref _openVinoWorkerStopRequested) != 0 ||
                    Volatile.Read(ref _openVinoMailboxPaused) != 0)
                {
                    return false;
                }

                var acceptedAtSeconds = NowSeconds();
                var priorCompletion = CapturePendingInferenceCompletion();
                var diagnosticSessionId = Volatile.Read(ref _inferenceDiagnosticSessionId);
                var diagnosticSerial = Interlocked.Increment(ref _nextInferenceDiagnosticSerial);
                Interlocked.Exchange(ref _activeInferenceDiagnosticSessionId, diagnosticSessionId);
                Interlocked.Exchange(ref _activeInferenceDiagnosticSerial, diagnosticSerial);
                Interlocked.Exchange(ref _activeInferenceDiagnosticTimestampMilliseconds, frame.TimestampMillisec);
                Volatile.Write(ref _activeInferenceDiagnosticAccepted, 0);
                var diagnosticSubmitStartTicks = Stopwatch.GetTimestamp();
                Interlocked.Exchange(ref _activeInferenceDiagnosticSubmitStartTicks, diagnosticSubmitStartTicks);

                Interlocked.Exchange(ref _inferenceOutstanding, 1);
                _inferenceStartedAtSeconds = acceptedAtSeconds;
                _activeInferenceFrameObservedAtSeconds = frame.ObservedAtSeconds;
                _inferenceScheduler.MarkAccepted(acceptedAtSeconds);

                var diagnosticSubmitReturnTicks = Stopwatch.GetTimestamp();
                Interlocked.Exchange(ref _activeInferenceDiagnosticSubmitReturnTicks, diagnosticSubmitReturnTicks);
                Volatile.Write(ref _activeInferenceDiagnosticAccepted, 1);
                RecordInferenceContinuationSample(
                    priorCompletion,
                    diagnosticSessionId,
                    diagnosticSubmitStartTicks,
                    origin);

                LastPreparedToInferenceLaunchMilliseconds = frame.PublishedAtSeconds > 0d
                    ? (float)((acceptedAtSeconds - frame.PublishedAtSeconds) * 1000d)
                    : 0f;
                var latestMainThreadFrameCount = Volatile.Read(ref _latestMainThreadFrameCount);
                LastPreparedToInferenceLaunchFrameDelta = frame.PublishedFrameCount >= 0
                    ? Math.Max(0, latestMainThreadFrameCount - frame.PublishedFrameCount)
                    : 0;
                _lastAcceptedLaunchOrigin = origin;
                if (origin == PreparedInferenceLaunchOrigin.ReadbackContinuation ||
                    origin == PreparedInferenceLaunchOrigin.OpenVinoWorkerContinuation)
                {
                    Interlocked.Increment(ref _immediateLaunchesInWindow);
                }
                if (origin == PreparedInferenceLaunchOrigin.OpenVinoWorkerContinuation)
                {
                    Interlocked.Increment(ref _openVinoWorkerContinuationLaunchesInWindow);
                }
                Interlocked.Increment(ref _totalInferenceRequests);
                Interlocked.Increment(ref _requestsInWindow);

                var result = _openVinoPoseRuntime.ProcessRgba(
                    frame.Rgba,
                    frame.Width,
                    frame.Height,
                    frame.StrideBytes,
                    frame.RotationDegrees,
                    frame.TimestampMillisec);
                OnOpenVinoPoseResult(result);
                return Volatile.Read(ref _openVinoWorkerStopRequested) == 0 &&
                    Volatile.Read(ref _openVinoMailboxPaused) == 0;
            }
            catch (Exception exception)
            {
                PublishOpenVinoWorkerFailure(exception);
                return false;
            }
            finally
            {
                _openVinoMailbox.Complete(frame);
            }
        }

        private void PauseOpenVinoMailboxAndDiscardPending()
        {
            if (_activeInferenceBackend != PoseInferenceBackend.OpenVinoCpuFp32 &&
                _openVinoWorkerTask == null)
            {
                return;
            }

            Volatile.Write(ref _openVinoMailboxPaused, 1);
            _openVinoMailbox.ClearPending();
            Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 0);
            _openVinoWorkerSignal?.Set();
        }

        private void DiscardOpenVinoPendingFrame()
        {
            if (_openVinoMailbox.ClearPending())
            {
                Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 0);
            }
        }

'''
provider = replace_block(
    provider,
    old_ov_method_start,
    old_ov_method_end,
    new_ov_methods,
    "OpenVINO bounded worker/mailbox implementation",
)

provider = replace_once(
    provider,
    "            Interlocked.Exchange(ref _inferenceOutstanding, 0);\n        }\n\n        private void ConsumeOpenVinoWorkerFailure()",
    "            Interlocked.Exchange(ref _inferenceOutstanding, 0);\n            Volatile.Write(ref _openVinoWorkerStopRequested, 1);\n            Volatile.Write(ref _openVinoMailboxPaused, 1);\n            _openVinoMailbox.StopAcceptingAndClear();\n            Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 0);\n            _openVinoWorkerSignal?.Set();\n        }\n\n        private void ConsumeOpenVinoWorkerFailure()",
    "OpenVINO worker failure stop path",
)

provider = replace_once(
    provider,
    "            if (_preparedTextureFrame != null)\n            {\n                _preparedTextureFrame.Release();\n                _preparedFrameReplacementsInWindow++;\n            }\n\n            _preparedTextureFrame = textureFrame;\n            Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 1);\n            _preparedFrameObservedAtSeconds = frameObservedAtSeconds;\n            _preparedCoordinateConventionVersion = coordinateConventionVersion;\n            _preparedFramePublishedAtSeconds = NowSeconds();\n            _preparedFramePublishedFrameCount = Time.frameCount;\n            if (directRequest)\n            {\n                RecordDirectReadbackTimingSample(\n                    directDiagnosticSerial,\n                    directSubmitStartTicks,\n                    directSubmitReturnTicks,\n                    coroutineObserveTicks,\n                    Stopwatch.GetTimestamp(),\n                    isError: false);\n            }\n            _readbackPending = false;\n\n            TryImmediateLaunchAfterReadback();",
    "            var publishedAtSeconds = NowSeconds();\n            var publishedFrameCount = Time.frameCount;\n            if (_activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32)\n            {\n                LaunchPreparedOpenVinoInference(\n                    textureFrame,\n                    frameObservedAtSeconds,\n                    publishedAtSeconds,\n                    publishedFrameCount,\n                    PreparedInferenceLaunchOrigin.ReadbackContinuation);\n                if (directRequest)\n                {\n                    RecordDirectReadbackTimingSample(\n                        directDiagnosticSerial,\n                        directSubmitStartTicks,\n                        directSubmitReturnTicks,\n                        coroutineObserveTicks,\n                        Stopwatch.GetTimestamp(),\n                        isError: false);\n                }\n                _readbackPending = false;\n                yield break;\n            }\n\n            if (_preparedTextureFrame != null)\n            {\n                _preparedTextureFrame.Release();\n                _preparedFrameReplacementsInWindow++;\n            }\n\n            _preparedTextureFrame = textureFrame;\n            Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 1);\n            _preparedFrameObservedAtSeconds = frameObservedAtSeconds;\n            _preparedCoordinateConventionVersion = coordinateConventionVersion;\n            _preparedFramePublishedAtSeconds = publishedAtSeconds;\n            _preparedFramePublishedFrameCount = publishedFrameCount;\n            if (directRequest)\n            {\n                RecordDirectReadbackTimingSample(\n                    directDiagnosticSerial,\n                    directSubmitStartTicks,\n                    directSubmitReturnTicks,\n                    coroutineObserveTicks,\n                    Stopwatch.GetTimestamp(),\n                    isError: false);\n            }\n            _readbackPending = false;\n\n            TryImmediateLaunchAfterReadback();",
    "OpenVINO readback-to-mailbox publication",
)

provider = replace_once(
    provider,
    "                    _openVinoPoseRuntime = bootstrapTask.Result;\n                    _activeInferenceBackend = PoseInferenceBackend.OpenVinoCpuFp32;\n                    OpenVinoRuntimeInfo",
    "                    _openVinoPoseRuntime = bootstrapTask.Result;\n                    _activeInferenceBackend = PoseInferenceBackend.OpenVinoCpuFp32;\n                    StartOpenVinoWorker();\n                    OpenVinoRuntimeInfo",
    "OpenVINO worker startup",
)

provider = replace_once(
    provider,
    "            if (_preparedTextureFrame != null)\n            {\n                ReleasePreparedFrame();\n            }\n\n            try\n            {\n                RebuildBodyInferenceResources();",
    "            if (_activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32)\n            {\n                DiscardOpenVinoPendingFrame();\n            }\n            else if (_preparedTextureFrame != null)\n            {\n                ReleasePreparedFrame();\n            }\n\n            try\n            {\n                RebuildBodyInferenceResources();",
    "body-resource pending-frame invalidation",
)

provider = replace_once(
    provider,
    "            _pendingCameraName = targetName;\n            _pendingPreferredCameraName = preferredIdentity;\n            _cameraSwitchPending = true;\n            StatusMessage = $\"Camera switch pending: {targetName}\";",
    "            _pendingCameraName = targetName;\n            _pendingPreferredCameraName = preferredIdentity;\n            _cameraSwitchPending = true;\n            PauseOpenVinoMailboxAndDiscardPending();\n            StatusMessage = $\"Camera switch pending: {targetName}\";",
    "camera-switch mailbox pause",
)

provider = provider.replace("_coordinateConventionVersion++;", "Interlocked.Increment(ref _coordinateConventionVersion);")
if "_coordinateConventionVersion++;" in provider:
    raise SystemExit("coordinate convention increment replacement incomplete")

provider = replace_once(
    provider,
    "            if (!_hasPublishedCoordinateConvention || !SameCoordinateConvention(_orientation, nextOrientation))\n            {\n                Interlocked.Increment(ref _coordinateConventionVersion);\n                _hasPublishedCoordinateConvention = true;\n            }",
    "            if (!_hasPublishedCoordinateConvention || !SameCoordinateConvention(_orientation, nextOrientation))\n            {\n                Interlocked.Increment(ref _coordinateConventionVersion);\n                _hasPublishedCoordinateConvention = true;\n                if (_activeInferenceBackend == PoseInferenceBackend.OpenVinoCpuFp32)\n                {\n                    DiscardOpenVinoPendingFrame();\n                }\n            }",
    "coordinate-change mailbox invalidation",
)

old_cleanup_start = "        private void CleanupOpenVinoRuntime()\n        {"
old_cleanup_end = "        private bool CleanupRuntime()\n"
new_cleanup = '''        private void CleanupOpenVinoRuntime()
        {
            Volatile.Write(ref _openVinoMailboxPaused, 1);
            Volatile.Write(ref _openVinoWorkerStopRequested, 1);
            _openVinoMailbox.StopAcceptingAndClear();
            Volatile.Write(ref _preparedTextureFrameDiagnosticOccupied, 0);
            _openVinoWorkerSignal?.Set();

            var workerTask = _openVinoWorkerTask;
            if (workerTask != null)
            {
                try
                {
                    workerTask.Wait();
                }
                catch (AggregateException)
                {
                    // Worker errors are surfaced through the provider failure channel while active.
                }
                _openVinoWorkerTask = null;
            }
            _openVinoWorkerSignal?.Dispose();
            _openVinoWorkerSignal = null;

            var bootstrapTask = _openVinoBootstrapTask;
            if (bootstrapTask != null)
            {
                try
                {
                    bootstrapTask.Wait();
                    if (bootstrapTask.Status == TaskStatus.RanToCompletion &&
                        bootstrapTask.Result != null &&
                        !ReferenceEquals(bootstrapTask.Result, _openVinoPoseRuntime))
                    {
                        bootstrapTask.Result.Dispose();
                    }
                }
                catch (AggregateException)
                {
                    // Initialization failure is already represented by bootstrap/provider status.
                }
                _openVinoBootstrapTask = null;
            }

            _openVinoPoseRuntime?.Dispose();
            _openVinoPoseRuntime = null;
            _openVinoLastPublishedTimestampMillisec = 0L;
            OpenVinoRuntimeInfo = string.Empty;
            OpenVinoEngineInfo = string.Empty;
            lock (_openVinoWorkerFailureGate)
            {
                _openVinoWorkerFailure = null;
            }
        }

'''
provider = replace_block(
    provider,
    old_cleanup_start,
    old_cleanup_end,
    new_cleanup,
    "OpenVINO deterministic worker teardown",
)

provider = provider.replace("_requestsInWindow++;", "Interlocked.Increment(ref _requestsInWindow);")
provider = provider.replace("_immediateLaunchesInWindow++;", "Interlocked.Increment(ref _immediateLaunchesInWindow);")

provider = replace_once(
    provider,
    "            var requests = _requestsInWindow;\n            var callbacks = Interlocked.Exchange(ref _callbacksInWindow, 0);",
    "            var requests = Interlocked.Exchange(ref _requestsInWindow, 0);\n            var immediateLaunches = Interlocked.Exchange(ref _immediateLaunchesInWindow, 0);\n            var openVinoWorkerContinuations = Interlocked.Exchange(ref _openVinoWorkerContinuationLaunchesInWindow, 0);\n            var callbacks = Interlocked.Exchange(ref _callbacksInWindow, 0);",
    "atomic scheduling metric snapshot",
)

provider = replace_once(
    provider,
    "            ImmediateLaunchesPerSecond = (float)(_immediateLaunchesInWindow / elapsed);\n            DirectReadbacksPerSecond",
    "            ImmediateLaunchesPerSecond = (float)(immediateLaunches / elapsed);\n            OpenVinoWorkerContinuationsPerSecond = (float)(openVinoWorkerContinuations / elapsed);\n            DirectReadbacksPerSecond",
    "worker continuation rate metric",
)

provider = replace_once(
    provider,
    "$\"U/RB={InferenceContinuationTimingUpdateLaunchCount}/{InferenceContinuationTimingReadbackLaunchCount} \" +\n                    $\"missing={InferenceContinuationTimingMissingSampleCount}\";",
    "$\"U/RB/OVW={InferenceContinuationTimingUpdateLaunchCount}/{InferenceContinuationTimingReadbackLaunchCount}/{InferenceContinuationTimingOpenVinoWorkerLaunchCount} \" +\n                    $\"missing={InferenceContinuationTimingMissingSampleCount}\";",
    "continuation origin telemetry",
)

provider = replace_once(
    provider,
    "        public int InferenceContinuationTimingReadbackLaunchCount =>\n            _inferenceContinuationTimingWindow.GetOriginSampleCount(PreparedInferenceLaunchOrigin.ReadbackContinuation);",
    "        public int InferenceContinuationTimingReadbackLaunchCount =>\n            _inferenceContinuationTimingWindow.GetOriginSampleCount(PreparedInferenceLaunchOrigin.ReadbackContinuation);\n        public int InferenceContinuationTimingOpenVinoWorkerLaunchCount =>\n            _inferenceContinuationTimingWindow.GetOriginSampleCount(PreparedInferenceLaunchOrigin.OpenVinoWorkerContinuation);",
    "worker continuation timing count",
)

provider = replace_once(
    provider,
    "$\"\\nOV ms managed/graph/det/lmk/bridge: {LastOpenVinoManagedInputCopyMilliseconds:0.0}/{LastOpenVinoGraphProcessMilliseconds:0.0}/{LastOpenVinoDetectorInferenceMilliseconds:0.0}/{LastOpenVinoLandmarkInferenceMilliseconds:0.0}/{LastOpenVinoBridgeCopyMilliseconds:0.0} detRan={(LastOpenVinoDetectorRan ? 1 : 0)}\"",
    "$\"\\nOV ms managed/graph/det/lmk/bridge: {LastOpenVinoManagedInputCopyMilliseconds:0.0}/{LastOpenVinoGraphProcessMilliseconds:0.0}/{LastOpenVinoDetectorInferenceMilliseconds:0.0}/{LastOpenVinoLandmarkInferenceMilliseconds:0.0}/{LastOpenVinoBridgeCopyMilliseconds:0.0} detRan={(LastOpenVinoDetectorRan ? 1 : 0)}\" +\n                      $\" sched pending={OpenVinoMailboxPendingCount} active={(OpenVinoMailboxHasActive ? 1 : 0)} ovw={OpenVinoWorkerContinuationsPerSecond:0.0}/s buffers={OpenVinoMailboxBufferAllocationCount}\"",
    "OpenVINO mailbox telemetry",
)

provider = provider.replace(
    "            _requestsInWindow = 0;\n            _cameraFramesInWindow = 0;",
    "            _cameraFramesInWindow = 0;",
    1,
)
provider = provider.replace(
    "            _preparedFrameReplacementsInWindow = 0;\n            _immediateLaunchesInWindow = 0;\n            _directReadbacksInWindow = 0;",
    "            _preparedFrameReplacementsInWindow = 0;\n            _directReadbacksInWindow = 0;",
    1,
)

provider = replace_once(
    provider,
    "            _requestsInWindow = 0;\n            _cameraFramesInWindow = 0;\n            _skippedInWindow = 0;",
    "            Interlocked.Exchange(ref _requestsInWindow, 0);\n            Interlocked.Exchange(ref _immediateLaunchesInWindow, 0);\n            Interlocked.Exchange(ref _openVinoWorkerContinuationLaunchesInWindow, 0);\n            _cameraFramesInWindow = 0;\n            _skippedInWindow = 0;",
    "session atomic scheduling metric reset",
)
provider = replace_once(
    provider,
    "            _preparedFrameReplacementsInWindow = 0;\n            _immediateLaunchesInWindow = 0;\n            _directReadbacksInWindow = 0;",
    "            _preparedFrameReplacementsInWindow = 0;\n            _directReadbacksInWindow = 0;",
    "session immediate reset removal",
)
provider = replace_once(
    provider,
    "            ImmediateLaunchesPerSecond = 0f;\n            DirectReadbacksPerSecond = 0f;",
    "            ImmediateLaunchesPerSecond = 0f;\n            OpenVinoWorkerContinuationsPerSecond = 0f;\n            DirectReadbacksPerSecond = 0f;",
    "session worker continuation rate reset",
)

# There are two Status/reset locations in historical versions; all remaining plain request/immediate
# resets must be absent because worker-owned increments use Interlocked snapshots.
if "_requestsInWindow++;" in provider or "_immediateLaunchesInWindow++;" in provider:
    raise SystemExit("non-atomic worker-visible scheduling counters remain")

# Add mailbox-related deterministic tests.
tests = replace_once(
    tests,
    "using GoldenNeedle.Core.Motion.Providers.MediaPipe;\nusing NUnit.Framework;",
    "using GoldenNeedle.Core.Motion.Providers.MediaPipe;\nusing NUnit.Framework;\nusing Unity.Collections;",
    "NativeArray scheduling-test import",
)

new_tests = '''
        [Test]
        public void SchedulerReportsRemainingIntervalForWorkerContinuation()
        {
            var scheduler = new InferenceLaunchScheduler();
            scheduler.MarkAccepted(1d);

            Assert.That(scheduler.SecondsUntilIntervalElapsed(1.02d, 0.05d), Is.EqualTo(0.03d).Within(1e-9));
            Assert.That(scheduler.SecondsUntilIntervalElapsed(1.05d, 0.05d), Is.EqualTo(0d));
        }

        [Test]
        public void OpenVinoMailboxReplacesPendingWithoutGrowing()
        {
            var mailbox = new OpenVinoLatestFrameMailbox();
            using var first = FilledBytes(16, 1);
            using var second = FilledBytes(16, 2);
            using var newest = FilledBytes(16, 3);

            Assert.That(Publish(mailbox, first, timestamp: 10, out var replaced), Is.True);
            Assert.That(replaced, Is.False);
            Assert.That(mailbox.PendingCount, Is.EqualTo(1));
            Assert.That(mailbox.BufferAllocationCount, Is.EqualTo(1));

            Assert.That(mailbox.TryTake(1, out var active), Is.True);
            Assert.That(mailbox.HasActive, Is.True);
            Assert.That(mailbox.PendingCount, Is.EqualTo(0));

            Assert.That(Publish(mailbox, second, timestamp: 20, out replaced), Is.True);
            Assert.That(replaced, Is.False);
            Assert.That(mailbox.PendingCount, Is.EqualTo(1));
            Assert.That(mailbox.BufferAllocationCount, Is.EqualTo(2));

            Assert.That(Publish(mailbox, newest, timestamp: 30, out replaced), Is.True);
            Assert.That(replaced, Is.True);
            Assert.That(mailbox.PendingCount, Is.EqualTo(1));
            Assert.That(mailbox.BufferAllocationCount, Is.EqualTo(2));
            Assert.That(mailbox.TryTake(1, out _), Is.False, "a second inference cannot overlap the active slot");

            mailbox.Complete(active);
            Assert.That(mailbox.TryTake(1, out var pending), Is.True);
            Assert.That(pending.TimestampMillisec, Is.EqualTo(30), "newest pending frame must win");
            Assert.That(pending.SlotId, Is.Not.EqualTo(active.SlotId), "pending writes must never overwrite the active buffer");
            mailbox.Complete(pending);
        }

        [Test]
        public void OpenVinoMailboxDropsStaleCoordinateConvention()
        {
            var mailbox = new OpenVinoLatestFrameMailbox();
            using var bytes = FilledBytes(16, 7);
            Assert.That(Publish(mailbox, bytes, timestamp: 10, out _), Is.True);

            Assert.That(mailbox.TryTake(2, out _), Is.False);
            Assert.That(mailbox.PendingCount, Is.EqualTo(0));
            Assert.That(mailbox.HasActive, Is.False);
        }

        [Test]
        public void OpenVinoMailboxStopClearsPendingAndRejectsNewFrames()
        {
            var mailbox = new OpenVinoLatestFrameMailbox();
            using var bytes = FilledBytes(16, 9);
            Assert.That(Publish(mailbox, bytes, timestamp: 10, out _), Is.True);

            mailbox.StopAcceptingAndClear();

            Assert.That(mailbox.PendingCount, Is.EqualTo(0));
            Assert.That(Publish(mailbox, bytes, timestamp: 20, out _), Is.False);
        }

        [Test]
        public void OpenVinoTimestampPolicyIsStrictlyMonotonic()
        {
            Assert.That(OpenVinoSchedulingPolicy.NextMonotonicTimestamp(100, 90), Is.EqualTo(100));
            Assert.That(OpenVinoSchedulingPolicy.NextMonotonicTimestamp(100, 100), Is.EqualTo(101));
            Assert.That(OpenVinoSchedulingPolicy.NextMonotonicTimestamp(99, 100), Is.EqualTo(101));
        }

        [Test]
        public void InferenceContinuationTimingAcceptsOpenVinoWorkerOrigin()
        {
            Assert.That(
                InferenceContinuationTimingMath.TryCreateSample(
                    expectedSessionId: 3,
                    completedSessionId: 3,
                    expectedSerial: 7,
                    completedSerial: 7,
                    submitStartTicks: 100,
                    submitReturnTicks: 105,
                    callbackEntryTicks: 125,
                    completionTicks: 130,
                    nextAcceptedLaunchTicks: 131,
                    preparedWaitingAtResult: true,
                    nextLaunchOrigin: PreparedInferenceLaunchOrigin.OpenVinoWorkerContinuation,
                    out var sample,
                    frequency: 1000),
                Is.True);
            Assert.That(sample.NextLaunchOrigin, Is.EqualTo(PreparedInferenceLaunchOrigin.OpenVinoWorkerContinuation));
            Assert.That(sample.ResultToNextLaunchMilliseconds, Is.EqualTo(1d));
        }

'''
tests = replace_once(
    tests,
    "        private static DirectReadbackTimingSample TimingSample(double callbackToPollMilliseconds)\n",
    new_tests + "        private static DirectReadbackTimingSample TimingSample(double callbackToPollMilliseconds)\n",
    "OpenVINO mailbox tests insertion",
)

helpers = '''
        private static NativeArray<byte> FilledBytes(int count, byte value)
        {
            var bytes = new NativeArray<byte>(count, Allocator.Temp);
            for (var i = 0; i < count; i++)
            {
                bytes[i] = value;
            }
            return bytes;
        }

        private static bool Publish(
            OpenVinoLatestFrameMailbox mailbox,
            NativeArray<byte> bytes,
            long timestamp,
            out bool replaced)
        {
            return mailbox.Publish(
                bytes,
                width: 2,
                height: 2,
                strideBytes: 8,
                rotationDegrees: 0,
                timestampMillisec: timestamp,
                observedAtSeconds: timestamp / 1000d,
                publishedAtSeconds: timestamp / 1000d,
                publishedFrameCount: (int)timestamp,
                coordinateConventionVersion: 1,
                launchOriginHint: PreparedInferenceLaunchOrigin.ReadbackContinuation,
                out replaced);
        }

'''
tests = replace_once(
    tests,
    "        private static bool ImmediateLaunchAllowed(\n",
    helpers + "        private static bool ImmediateLaunchAllowed(\n",
    "OpenVINO mailbox test helpers",
)

# Ensure core invariants are represented in generated source.
provider_required = [
    "OpenVinoLatestFrameMailbox",
    "Task.Run(OpenVinoWorkerLoop)",
    "PreparedInferenceLaunchOrigin.OpenVinoWorkerContinuation",
    "_openVinoMailbox.PendingCount",
    "_openVinoMailbox.TryTake",
    "_openVinoMailbox.Complete(frame)",
    "StopAcceptingAndClear",
    "runtime.ProcessRgba" if "runtime.ProcessRgba" in provider else "_openVinoPoseRuntime.ProcessRgba",
]
missing = [item for item in provider_required if item not in provider]
if missing:
    raise SystemExit(f"generated provider missing scheduling invariants: {missing}")
if "Queue<" in provider or "ConcurrentQueue<" in provider:
    raise SystemExit("queue type introduced into provider; latest-frame/no-backlog invariant violated")

PROVIDER.write_text(provider, encoding="utf-8", newline="\n")
TESTS.write_text(tests, encoding="utf-8", newline="\n")
print("OPENVINO_SCHEDULING_TRANSFORM=PASS")
