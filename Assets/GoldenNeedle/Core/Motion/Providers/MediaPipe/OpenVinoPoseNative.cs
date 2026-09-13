using System;
using System.Runtime.InteropServices;
using System.Text;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    public enum PoseInferenceBackend
    {
        MediaPipeTfliteCpu = 0,
        OpenVinoCpuFp32 = 1,
    }

    public readonly struct OpenVinoPoseLandmark
    {
        public OpenVinoPoseLandmark(
            float x,
            float y,
            float z,
            float visibility,
            float presence,
            float worldX,
            float worldY,
            float worldZ,
            bool hasVisibility,
            bool hasPresence,
            bool hasWorld)
        {
            X = x;
            Y = y;
            Z = z;
            Visibility = visibility;
            Presence = presence;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            HasVisibility = hasVisibility;
            HasPresence = hasPresence;
            HasWorld = hasWorld;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Visibility { get; }
        public float Presence { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public bool HasVisibility { get; }
        public bool HasPresence { get; }
        public bool HasWorld { get; }
    }

    public readonly struct OpenVinoPoseFrameResult
    {
        public OpenVinoPoseFrameResult(
            long timestampMillisec,
            bool hasPose,
            bool detectorRan,
            OpenVinoPoseLandmark[] landmarks,
            double graphProcessMilliseconds,
            double detectorInferenceMilliseconds,
            double landmarkInferenceMilliseconds,
            double tensorInputCopyMilliseconds,
            double tensorOutputCopyMilliseconds,
            double frameCopyMilliseconds,
            double outputMarshalMilliseconds)
        {
            TimestampMillisec = timestampMillisec;
            HasPose = hasPose;
            DetectorRan = detectorRan;
            Landmarks = landmarks ?? Array.Empty<OpenVinoPoseLandmark>();
            GraphProcessMilliseconds = graphProcessMilliseconds;
            DetectorInferenceMilliseconds = detectorInferenceMilliseconds;
            LandmarkInferenceMilliseconds = landmarkInferenceMilliseconds;
            TensorInputCopyMilliseconds = tensorInputCopyMilliseconds;
            TensorOutputCopyMilliseconds = tensorOutputCopyMilliseconds;
            FrameCopyMilliseconds = frameCopyMilliseconds;
            OutputMarshalMilliseconds = outputMarshalMilliseconds;
        }

        public long TimestampMillisec { get; }
        public bool HasPose { get; }
        public bool DetectorRan { get; }
        public OpenVinoPoseLandmark[] Landmarks { get; }
        public double GraphProcessMilliseconds { get; }
        public double DetectorInferenceMilliseconds { get; }
        public double LandmarkInferenceMilliseconds { get; }
        public double TensorInputCopyMilliseconds { get; }
        public double TensorOutputCopyMilliseconds { get; }
        public double FrameCopyMilliseconds { get; }
        public double OutputMarshalMilliseconds { get; }
        public double BridgeCopyMilliseconds =>
            TensorInputCopyMilliseconds + TensorOutputCopyMilliseconds + FrameCopyMilliseconds + OutputMarshalMilliseconds;
    }

    public sealed class OpenVinoPoseNativeException : Exception
    {
        public OpenVinoPoseNativeException(int resultCode, string message)
            : base(message)
        {
            ResultCode = resultCode;
        }

        public int ResultCode { get; }
    }

    /// <summary>
    /// Managed owner for the experimental Golden Needle MediaPipe/OpenVINO native runtime.
    /// It contains no Unity API calls, so creation, processing and disposal may all be kept on
    /// one worker thread. The stock Homuler MediaPipe runtime is not modified by this class.
    /// </summary>
    public sealed class OpenVinoPoseRuntime : IDisposable
    {
        public const int LandmarkCount = 33;
        public const uint AbiVersion = (1u << 16) | 1u;

        private IntPtr _context;
        private IntPtr _engine;
        private bool _disposed;

        private OpenVinoPoseRuntime(IntPtr context, IntPtr engine, string runtimeInfo, string engineInfo)
        {
            _context = context;
            _engine = engine;
            RuntimeInfo = runtimeInfo ?? string.Empty;
            EngineInfo = engineInfo ?? string.Empty;
        }

        public string RuntimeInfo { get; }
        public string EngineInfo { get; }
        public string BackendLabel => "OPENVINO_CPU_FP32";

        public static OpenVinoPoseRuntime Create(string detectorModelPath, string landmarkModelPath)
        {
            if (string.IsNullOrWhiteSpace(detectorModelPath))
            {
                throw new ArgumentException("Detector model path is required.", nameof(detectorModelPath));
            }
            if (string.IsNullOrWhiteSpace(landmarkModelPath))
            {
                throw new ArgumentException("Landmark model path is required.", nameof(landmarkModelPath));
            }

            var nativeAbi = OpenVinoPoseNative.GetAbiVersion();
            if (nativeAbi != AbiVersion)
            {
                throw new OpenVinoPoseNativeException(
                    (int)OpenVinoPoseNative.Result.AbiMismatch,
                    $"OpenVINO pose ABI mismatch. Managed={AbiVersion}, native={nativeAbi}.");
            }

            var contextConfig = new OpenVinoPoseNative.ContextConfig
            {
                StructSize = (uint)Marshal.SizeOf<OpenVinoPoseNative.ContextConfig>(),
                AbiVersion = AbiVersion,
                ConfigVersion = 1,
                Device = "CPU",
            };

            IntPtr context = IntPtr.Zero;
            IntPtr engine = IntPtr.Zero;
            try
            {
                Check(OpenVinoPoseNative.Create(ref contextConfig, out context), "context creation");
                Check(OpenVinoPoseNative.SelfTest(context), "runtime self-test");

                var engineConfig = new OpenVinoPoseNative.EngineConfig
                {
                    StructSize = (uint)Marshal.SizeOf<OpenVinoPoseNative.EngineConfig>(),
                    AbiVersion = AbiVersion,
                    ConfigVersion = 1,
                    DetectorModelPath = detectorModelPath,
                    LandmarkModelPath = landmarkModelPath,
                };
                Check(OpenVinoPoseNative.PoseEngineCreate(context, ref engineConfig, out engine), "pose-engine creation");

                return new OpenVinoPoseRuntime(
                    context,
                    engine,
                    QueryText((buffer, capacity) => OpenVinoPoseNative.GetRuntimeInfo(context, buffer, capacity)),
                    QueryText((buffer, capacity) => OpenVinoPoseNative.PoseEngineGetInfo(engine, buffer, capacity)));
            }
            catch
            {
                if (engine != IntPtr.Zero)
                {
                    OpenVinoPoseNative.PoseEngineDestroy(engine);
                }
                if (context != IntPtr.Zero)
                {
                    OpenVinoPoseNative.Destroy(context);
                }
                throw;
            }
        }

        public OpenVinoPoseFrameResult ProcessRgba(
            byte[] rgba,
            int width,
            int height,
            int strideBytes,
            int rotationDegrees,
            long timestampMillisec)
        {
            ThrowIfDisposed();
            if (rgba == null)
            {
                throw new ArgumentNullException(nameof(rgba));
            }
            if (width <= 0 || height <= 0 || strideBytes < width * 4)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "RGBA dimensions/stride are invalid.");
            }
            if ((long)strideBytes * height > rgba.LongLength)
            {
                throw new ArgumentException("RGBA buffer is smaller than stride * height.", nameof(rgba));
            }
            if (rotationDegrees % 90 != 0)
            {
                throw new ArgumentException("Rotation must be a multiple of 90 degrees.", nameof(rotationDegrees));
            }

            var nativeResult = OpenVinoPoseNative.NativePoseResult.Create();
            var handle = GCHandle.Alloc(rgba, GCHandleType.Pinned);
            try
            {
                Check(
                    OpenVinoPoseNative.PoseEngineProcessRgba(
                        _engine,
                        handle.AddrOfPinnedObject(),
                        width,
                        height,
                        strideBytes,
                        rotationDegrees,
                        timestampMillisec,
                        ref nativeResult),
                    "pose inference");
            }
            finally
            {
                handle.Free();
            }

            if (nativeResult.Backend != (uint)OpenVinoPoseNative.Backend.OpenVinoCpuFp32)
            {
                throw new OpenVinoPoseNativeException(
                    (int)OpenVinoPoseNative.Result.Internal,
                    $"Unexpected native backend identity: {nativeResult.Backend}.");
            }
            if (nativeResult.TimestampMillisec != timestampMillisec)
            {
                throw new OpenVinoPoseNativeException(
                    (int)OpenVinoPoseNative.Result.Internal,
                    "Native result timestamp did not match the submitted frame.");
            }
            if (nativeResult.HasPose != 0 && nativeResult.LandmarkCount != LandmarkCount)
            {
                throw new OpenVinoPoseNativeException(
                    (int)OpenVinoPoseNative.Result.Internal,
                    $"Native pose returned {nativeResult.LandmarkCount} landmarks instead of {LandmarkCount}.");
            }

            var hasPose = nativeResult.HasPose != 0;
            var landmarks = hasPose ? new OpenVinoPoseLandmark[LandmarkCount] : Array.Empty<OpenVinoPoseLandmark>();
            if (hasPose)
            {
                for (var i = 0; i < LandmarkCount; i++)
                {
                    var source = nativeResult.Landmarks[i];
                    landmarks[i] = new OpenVinoPoseLandmark(
                        source.X,
                        source.Y,
                        source.Z,
                        source.Visibility,
                        source.Presence,
                        source.WorldX,
                        source.WorldY,
                        source.WorldZ,
                        (source.Flags & (uint)OpenVinoPoseNative.LandmarkFlags.HasVisibility) != 0,
                        (source.Flags & (uint)OpenVinoPoseNative.LandmarkFlags.HasPresence) != 0,
                        (source.Flags & (uint)OpenVinoPoseNative.LandmarkFlags.HasWorld) != 0);
                }
            }

            return new OpenVinoPoseFrameResult(
                nativeResult.TimestampMillisec,
                hasPose,
                nativeResult.DetectorRan != 0,
                landmarks,
                nativeResult.GraphProcessMilliseconds,
                nativeResult.DetectorInferenceMilliseconds,
                nativeResult.LandmarkInferenceMilliseconds,
                nativeResult.TensorInputCopyMilliseconds,
                nativeResult.TensorOutputCopyMilliseconds,
                nativeResult.FrameCopyMilliseconds,
                nativeResult.OutputMarshalMilliseconds);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_engine != IntPtr.Zero)
            {
                OpenVinoPoseNative.PoseEngineDestroy(_engine);
                _engine = IntPtr.Zero;
            }
            if (_context != IntPtr.Zero)
            {
                OpenVinoPoseNative.Destroy(_context);
                _context = IntPtr.Zero;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed || _context == IntPtr.Zero || _engine == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(OpenVinoPoseRuntime));
            }
        }

        private static void Check(int result, string operation)
        {
            if (result == (int)OpenVinoPoseNative.Result.Ok)
            {
                return;
            }
            var nativeError = QueryText(OpenVinoPoseNative.GetLastError);
            throw new OpenVinoPoseNativeException(
                result,
                string.IsNullOrWhiteSpace(nativeError)
                    ? $"OpenVINO pose {operation} failed with code {result}."
                    : $"OpenVINO pose {operation} failed with code {result}: {nativeError}");
        }

        private delegate int TextQuery(StringBuilder buffer, uint capacity);

        private static string QueryText(TextQuery query)
        {
            const int capacity = 4096;
            var buffer = new StringBuilder(capacity);
            var result = query(buffer, capacity);
            return result == (int)OpenVinoPoseNative.Result.Ok ? buffer.ToString() : string.Empty;
        }
    }

    internal static class OpenVinoPoseNative
    {
        private const string LibraryName = "golden_needle_openvino_pose";
        private const int NativeLandmarkCount = 33;

        internal enum Result
        {
            Ok = 0,
            InvalidArgument = 1,
            AbiMismatch = 2,
            UnsupportedDevice = 3,
            OpenVino = 4,
            BufferTooSmall = 5,
            Internal = 6,
            MediaPipe = 7,
            Timestamp = 8,
            Model = 9,
        }

        internal enum Backend : uint
        {
            Unknown = 0,
            OpenVinoCpuFp32 = 1,
        }

        [Flags]
        internal enum LandmarkFlags : uint
        {
            HasVisibility = 1u << 0,
            HasPresence = 1u << 1,
            HasWorld = 1u << 2,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct ContextConfig
        {
            internal uint StructSize;
            internal uint AbiVersion;
            internal uint ConfigVersion;
            [MarshalAs(UnmanagedType.LPStr)] internal string Device;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct EngineConfig
        {
            internal uint StructSize;
            internal uint AbiVersion;
            internal uint ConfigVersion;
            [MarshalAs(UnmanagedType.LPStr)] internal string DetectorModelPath;
            [MarshalAs(UnmanagedType.LPStr)] internal string LandmarkModelPath;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeLandmark
        {
            internal float X;
            internal float Y;
            internal float Z;
            internal float Visibility;
            internal float Presence;
            internal float WorldX;
            internal float WorldY;
            internal float WorldZ;
            internal uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativePoseResult
        {
            internal uint StructSize;
            internal uint Version;
            internal uint Backend;
            internal uint HasPose;
            internal long TimestampMillisec;
            internal uint LandmarkCount;
            internal uint DetectorRan;
            internal double GraphProcessMilliseconds;
            internal double DetectorInferenceMilliseconds;
            internal double LandmarkInferenceMilliseconds;
            internal double TensorInputCopyMilliseconds;
            internal double TensorOutputCopyMilliseconds;
            internal double FrameCopyMilliseconds;
            internal double OutputMarshalMilliseconds;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = NativeLandmarkCount)]
            internal NativeLandmark[] Landmarks;

            internal static NativePoseResult Create()
            {
                return new NativePoseResult
                {
                    StructSize = (uint)Marshal.SizeOf<NativePoseResult>(),
                    Version = 1,
                    Landmarks = new NativeLandmark[NativeLandmarkCount],
                };
            }
        }

        [DllImport(LibraryName, EntryPoint = "gnovpose_get_abi_version", CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint GetAbiVersion();

        [DllImport(LibraryName, EntryPoint = "gnovpose_get_last_error", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern int GetLastError([Out] StringBuilder buffer, uint capacity);

        [DllImport(LibraryName, EntryPoint = "gnovpose_create", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Create(ref ContextConfig config, out IntPtr context);

        [DllImport(LibraryName, EntryPoint = "gnovpose_destroy", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Destroy(IntPtr context);

        [DllImport(LibraryName, EntryPoint = "gnovpose_self_test", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SelfTest(IntPtr context);

        [DllImport(LibraryName, EntryPoint = "gnovpose_get_runtime_info", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern int GetRuntimeInfo(IntPtr context, [Out] StringBuilder buffer, uint capacity);

        [DllImport(LibraryName, EntryPoint = "gnovpose_pose_engine_create", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PoseEngineCreate(IntPtr context, ref EngineConfig config, out IntPtr engine);

        [DllImport(LibraryName, EntryPoint = "gnovpose_pose_engine_destroy", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PoseEngineDestroy(IntPtr engine);

        [DllImport(LibraryName, EntryPoint = "gnovpose_pose_engine_get_info", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern int PoseEngineGetInfo(IntPtr engine, [Out] StringBuilder buffer, uint capacity);

        [DllImport(LibraryName, EntryPoint = "gnovpose_pose_engine_process_rgba", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PoseEngineProcessRgba(
            IntPtr engine,
            IntPtr rgba,
            int width,
            int height,
            int strideBytes,
            int rotationDegrees,
            long timestampMillisec,
            [In, Out] ref NativePoseResult result);
    }
}
