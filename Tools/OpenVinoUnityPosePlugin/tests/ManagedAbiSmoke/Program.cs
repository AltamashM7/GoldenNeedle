using System;
using System.Reflection;
using System.Runtime.InteropServices;
using GoldenNeedle.Core.Motion.Providers.MediaPipe;

internal static class Program
{
    private static int Main()
    {
        AssertEqual(0, (int)PoseInferenceBackend.MediaPipeTfliteCpu, "stock backend enum/default value");
        AssertEqual(1, (int)PoseInferenceBackend.OpenVinoCpuFp32, "OpenVINO backend enum value");
        AssertEqual(0x00010001u, OpenVinoPoseRuntime.AbiVersion, "managed ABI version");
        AssertEqual(33, OpenVinoPoseRuntime.LandmarkCount, "managed landmark count");

        AssertSize<OpenVinoPoseNative.ContextConfig>(24, "ContextConfig");
        AssertSize<OpenVinoPoseNative.EngineConfig>(32, "EngineConfig");
        AssertSize<OpenVinoPoseNative.NativeLandmark>(36, "NativeLandmark");
        AssertSize<OpenVinoPoseNative.NativePoseResult>(1280, "NativePoseResult");

        AssertOffset<OpenVinoPoseNative.ContextConfig>(nameof(OpenVinoPoseNative.ContextConfig.Device), 16);
        AssertOffset<OpenVinoPoseNative.EngineConfig>(nameof(OpenVinoPoseNative.EngineConfig.DetectorModelPath), 16);
        AssertOffset<OpenVinoPoseNative.EngineConfig>(nameof(OpenVinoPoseNative.EngineConfig.LandmarkModelPath), 24);

        AssertOffset<OpenVinoPoseNative.NativeLandmark>(nameof(OpenVinoPoseNative.NativeLandmark.Flags), 32);

        AssertOffset<OpenVinoPoseNative.NativePoseResult>(nameof(OpenVinoPoseNative.NativePoseResult.TimestampMillisec), 16);
        AssertOffset<OpenVinoPoseNative.NativePoseResult>(nameof(OpenVinoPoseNative.NativePoseResult.LandmarkCount), 24);
        AssertOffset<OpenVinoPoseNative.NativePoseResult>(nameof(OpenVinoPoseNative.NativePoseResult.GraphProcessMilliseconds), 32);
        AssertOffset<OpenVinoPoseNative.NativePoseResult>(nameof(OpenVinoPoseNative.NativePoseResult.OutputMarshalMilliseconds), 80);
        AssertOffset<OpenVinoPoseNative.NativePoseResult>(nameof(OpenVinoPoseNative.NativePoseResult.Landmarks), 88);

        var result = OpenVinoPoseNative.NativePoseResult.Create();
        AssertEqual(1280u, result.StructSize, "pose-result struct_size initializer");
        AssertEqual(1u, result.Version, "pose-result version initializer");
        AssertEqual(33, result.Landmarks?.Length ?? -1, "pose-result inline landmark array initializer");

        AssertDllImport(nameof(OpenVinoPoseNative.GetAbiVersion), "gnovpose_get_abi_version");
        AssertDllImport(nameof(OpenVinoPoseNative.PoseEngineProcessRgba), "gnovpose_pose_engine_process_rgba");

        Console.WriteLine("GNOVPOSE_MANAGED_ABI_SMOKE=PASS");
        Console.WriteLine($"ABI={OpenVinoPoseRuntime.AbiVersion}; landmarks={OpenVinoPoseRuntime.LandmarkCount}; result_size={Marshal.SizeOf<OpenVinoPoseNative.NativePoseResult>()}");
        return 0;
    }

    private static void AssertSize<T>(int expected, string label)
    {
        AssertEqual(expected, Marshal.SizeOf<T>(), $"sizeof({label})");
    }

    private static void AssertOffset<T>(string field, int expected)
    {
        AssertEqual(expected, checked((int)Marshal.OffsetOf<T>(field)), $"offsetof({typeof(T).Name}.{field})");
    }

    private static void AssertDllImport(string methodName, string expectedEntryPoint)
    {
        var method = typeof(OpenVinoPoseNative).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException($"Missing native method {methodName}.");
        }
        var attribute = method.GetCustomAttribute<DllImportAttribute>();
        if (attribute == null)
        {
            throw new InvalidOperationException($"{methodName} is missing DllImportAttribute.");
        }
        AssertEqual("golden_needle_openvino_pose", attribute.Value, $"{methodName} library");
        AssertEqual(expectedEntryPoint, attribute.EntryPoint, $"{methodName} entry point");
        AssertEqual(CallingConvention.Cdecl, attribute.CallingConvention, $"{methodName} calling convention");
    }

    private static void AssertEqual<T>(T expected, T actual, string label) where T : IEquatable<T>
    {
        if (!expected.Equals(actual))
        {
            throw new InvalidOperationException($"{label}: expected={expected} actual={actual}");
        }
    }
}
