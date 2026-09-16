using System.Collections.Generic;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    /// <summary>
    /// Lab compatibility bridge. The Windows recognizer implementation is production-owned under
    /// Core.Commands.Providers; PoseTrackingSpike keeps its existing construction site without a
    /// second recognizer implementation.
    /// </summary>
    internal sealed class WindowsKeywordSpeechProvider :
        GoldenNeedle.Core.Commands.Providers.WindowsKeywordSpeechProvider
    {
        public WindowsKeywordSpeechProvider(IReadOnlyList<string> keywords)
            : base(keywords)
        {
        }
    }
}
