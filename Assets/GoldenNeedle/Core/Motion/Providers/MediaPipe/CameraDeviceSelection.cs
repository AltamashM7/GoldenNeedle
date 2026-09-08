using System;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    /// <summary>
    /// Pure camera-device selection helpers shared by runtime selection and Editor diagnostics.
    /// Persistent identity is the WebCamDevice name rather than its array index.
    /// </summary>
    public static class CameraDeviceSelection
    {
        public static int SelectPreferredOrFallbackIndex(
            string[] deviceNames,
            string preferredCameraName)
        {
            if (deviceNames == null || deviceNames.Length == 0)
            {
                return -1;
            }

            if (!string.IsNullOrWhiteSpace(preferredCameraName))
            {
                for (var i = 0; i < deviceNames.Length; i++)
                {
                    if (string.Equals(
                            deviceNames[i],
                            preferredCameraName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            for (var i = 0; i < deviceNames.Length; i++)
            {
                if (!IsDepthLike(deviceNames[i]))
                {
                    return i;
                }
            }

            // Preserve the historical provider fallback when only depth/IR-like devices exist.
            return 0;
        }

        public static string GetNextUsableDeviceName(
            string[] deviceNames,
            string currentCameraName)
        {
            if (deviceNames == null || deviceNames.Length == 0)
            {
                return string.Empty;
            }

            var usableCount = CountUsableDevices(deviceNames);
            if (usableCount == 0)
            {
                return string.Empty;
            }

            var currentIndex = -1;
            for (var i = 0; i < deviceNames.Length; i++)
            {
                if (!IsDepthLike(deviceNames[i]) &&
                    string.Equals(
                        deviceNames[i],
                        currentCameraName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            for (var offset = 1; offset <= deviceNames.Length; offset++)
            {
                var index = currentIndex < 0
                    ? offset - 1
                    : (currentIndex + offset) % deviceNames.Length;
                if (!IsDepthLike(deviceNames[index]))
                {
                    return deviceNames[index];
                }
            }

            return string.Empty;
        }

        public static int CountUsableDevices(string[] deviceNames)
        {
            if (deviceNames == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < deviceNames.Length; i++)
            {
                if (!IsDepthLike(deviceNames[i]))
                {
                    count++;
                }
            }

            return count;
        }

        public static bool IsDepthLike(string deviceName)
        {
            var name = (deviceName ?? string.Empty).ToLowerInvariant();
            return name.Contains("depth") ||
                name.Contains("infrared") ||
                name.Contains("infra red") ||
                name.Contains(" ir ");
        }

        public static bool CanProcessPendingSwitch(
            bool pending,
            bool bootstrapActive,
            bool readbackPending,
            int inferenceOutstanding)
        {
            return pending &&
                !bootstrapActive &&
                !readbackPending &&
                inferenceOutstanding == 0;
        }
    }
}
