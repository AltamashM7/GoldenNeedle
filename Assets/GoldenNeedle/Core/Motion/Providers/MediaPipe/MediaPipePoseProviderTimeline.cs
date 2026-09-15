using System.Diagnostics;
using System.Reflection;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    /// <summary>
    /// Read-only access to the exact Stopwatch timeline already owned by MediaPipePoseProvider.
    /// Foundation D uses this observation surface so body and hand semantic timestamps share one epoch.
    /// No scheduling, lifecycle, cadence, mailbox, or body timestamp behavior is changed here.
    /// </summary>
    public static class MediaPipePoseProviderTimeline
    {
        private static readonly FieldInfo ClockField = typeof(MediaPipePoseProvider).GetField(
            "_clock",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool TryGetTimelineClock(MediaPipePoseProvider provider, out Stopwatch clock)
        {
            clock = null;
            if (provider == null || ClockField == null)
            {
                return false;
            }

            clock = ClockField.GetValue(provider) as Stopwatch;
            return clock != null;
        }

        public static long CurrentTimelineMilliseconds(this MediaPipePoseProvider provider)
        {
            return TryGetTimelineClock(provider, out var clock) ? clock.ElapsedMilliseconds : 0L;
        }

        public static double CurrentTimelineSeconds(this MediaPipePoseProvider provider)
        {
            return TryGetTimelineClock(provider, out var clock)
                ? clock.ElapsedTicks / (double)Stopwatch.Frequency
                : 0d;
        }
    }
}
