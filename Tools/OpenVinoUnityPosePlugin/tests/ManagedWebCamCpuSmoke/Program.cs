using System;
using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using Unity.Collections;
using UnityEngine;

internal static class Program
{
    private static int Main()
    {
        HalfScaleAndFlips();
        GeneralMapping();
        TimingWindow();
        Console.WriteLine("OPENVINO_WEBCAM_CPU_MANAGED_SMOKE=PASS");
        return 0;
    }

    private static void HalfScaleAndFlips()
    {
        var source = new[]
        {
            Pixel(10), Pixel(10), Pixel(20), Pixel(20),
            Pixel(10), Pixel(10), Pixel(20), Pixel(20),
            Pixel(30), Pixel(30), Pixel(40), Pixel(40),
            Pixel(30), Pixel(30), Pixel(40), Pixel(40),
        };
        using var output = new NativeArray<byte>(16);
        WebCamCpuFramePreparation.PrepareRgba(
            source, 4, 4, output, 2, 2, true, true);
        Expect(Read(output, 2, 0, 0) == 40, "2:1 HV pixel 00");
        Expect(Read(output, 2, 1, 0) == 30, "2:1 HV pixel 10");
        Expect(Read(output, 2, 0, 1) == 20, "2:1 HV pixel 01");
        Expect(Read(output, 2, 1, 1) == 10, "2:1 HV pixel 11");
    }

    private static void GeneralMapping()
    {
        var source = new[]
        {
            Pixel(1), Pixel(2), Pixel(3),
            Pixel(4), Pixel(5), Pixel(6),
        };
        using var output = new NativeArray<byte>(24);
        WebCamCpuFramePreparation.PrepareRgba(
            source, 3, 2, output, 3, 2, true, false);
        Expect(Read(output, 3, 0, 0) == 3, "general H pixel 00");
        Expect(Read(output, 3, 2, 1) == 4, "general H pixel 21");
    }

    private static void TimingWindow()
    {
        var window = new WebCamCpuAcquisitionTimingWindow(3);
        window.Add(new WebCamCpuAcquisitionTimingSample(1, 2, 3));
        window.Add(new WebCamCpuAcquisitionTimingSample(2, 3, 5));
        window.Add(new WebCamCpuAcquisitionTimingSample(3, 4, 7));
        window.Add(new WebCamCpuAcquisitionTimingSample(4, 5, 9));
        Expect(window.ValidSampleCount == 3, "bounded timing window");
        Expect(
            Math.Abs(window.GetMedianMilliseconds(WebCamCpuAcquisitionTimingMetric.GetPixels32) - 3d) < 1e-9,
            "timing median");
        Expect(
            Math.Abs(window.GetP95Milliseconds(WebCamCpuAcquisitionTimingMetric.Total) - 9d) < 1e-9,
            "timing p95");
    }

    private static Color32 Pixel(byte value) => new Color32(value, value, value, 255);

    private static byte Read(NativeArray<byte> output, int width, int x, int y)
    {
        return output[(y * width + x) * 4];
    }

    private static void Expect(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Managed webcam CPU smoke failed: {label}");
        }
    }
}
