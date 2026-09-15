namespace GoldenNeedle.Core.Motion.Hands
{
    /// <summary>
    /// Provider-independent latest-only scheduler state. Exactly one active request is admitted;
    /// while active, the newest pending sequence replaces the older pending sequence.
    /// </summary>
    public sealed class BoundedHandInferenceScheduler
    {
        public bool HasActive { get; private set; }
        public bool HasPending { get; private set; }
        public long ActiveSequence { get; private set; }
        public long PendingSequence { get; private set; }
        public int ReplacedPendingCount { get; private set; }
        public int CompletedCount { get; private set; }
        public int TimedOutCount { get; private set; }

        public bool Offer(long sequence, out bool launchNow)
        {
            launchNow = false;
            if (!HasActive)
            {
                HasActive = true;
                ActiveSequence = sequence;
                launchNow = true;
                return true;
            }

            if (HasPending)
            {
                ReplacedPendingCount++;
            }
            PendingSequence = sequence;
            HasPending = true;
            return true;
        }

        public bool CompleteActive(out long nextSequence)
        {
            nextSequence = 0;
            if (!HasActive)
            {
                return false;
            }

            CompletedCount++;
            HasActive = false;
            ActiveSequence = 0;
            return PromotePending(out nextSequence);
        }

        public bool TimeoutActive(out long nextSequence)
        {
            nextSequence = 0;
            if (!HasActive)
            {
                return false;
            }

            TimedOutCount++;
            HasActive = false;
            ActiveSequence = 0;
            return PromotePending(out nextSequence);
        }

        public void Reset()
        {
            HasActive = false;
            HasPending = false;
            ActiveSequence = 0;
            PendingSequence = 0;
            ReplacedPendingCount = 0;
            CompletedCount = 0;
            TimedOutCount = 0;
        }

        private bool PromotePending(out long nextSequence)
        {
            nextSequence = 0;
            if (!HasPending)
            {
                return false;
            }

            nextSequence = PendingSequence;
            PendingSequence = 0;
            HasPending = false;
            HasActive = true;
            ActiveSequence = nextSequence;
            return true;
        }
    }
}
