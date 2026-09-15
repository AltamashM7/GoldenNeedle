using System.Diagnostics;

namespace GoldenNeedle.Core.Motion.Providers.MediaPipe
{
    public sealed class MediaPipePoseProvider
    {
        // Mirrors only the production provider's read-only timeline field for the pure smoke.
        private readonly Stopwatch _clock = Stopwatch.StartNew();
    }
}
