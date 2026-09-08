using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using NUnit.Framework;

namespace GoldenNeedle.Tests
{
    public sealed class MediaPipeInferenceSchedulingTests
    {
        [Test]
        public void IntervalMustElapseBeforeAnotherLaunch()
        {
            var scheduler = new InferenceLaunchScheduler();

            Assert.That(
                scheduler.CanLaunch(0d, 0.05d, false),
                Is.True);

            scheduler.MarkAccepted(0d);

            Assert.That(
                scheduler.CanLaunch(0.02d, 0.05d, false),
                Is.False);
            Assert.That(
                scheduler.CanLaunch(0.05d, 0.05d, false),
                Is.True);
        }

        [Test]
        public void BusySkipDoesNotPostponeNextFreeLaunch()
        {
            var scheduler = new InferenceLaunchScheduler();
            scheduler.MarkAccepted(0d);

            Assert.That(
                scheduler.IsIntervalElapsed(0.05d, 0.05d),
                Is.True);
            Assert.That(
                scheduler.CanLaunch(0.05d, 0.05d, true),
                Is.False);

            // No scheduler state changed while busy. Once the outstanding work finishes, the very
            // next Update may launch because the original minimum interval already elapsed.
            Assert.That(
                scheduler.LastAcceptedRequestAtSeconds,
                Is.EqualTo(0d));
            Assert.That(
                scheduler.CanLaunch(0.06d, 0.05d, false),
                Is.True);
        }

        [Test]
        public void BusyStateNeverCreatesAQueuedLaunch()
        {
            var scheduler = new InferenceLaunchScheduler();
            scheduler.MarkAccepted(1d);

            Assert.That(
                scheduler.CanLaunch(1.10d, 0.05d, true),
                Is.False);
            Assert.That(
                scheduler.CanLaunch(1.20d, 0.05d, true),
                Is.False);
            Assert.That(
                scheduler.LastAcceptedRequestAtSeconds,
                Is.EqualTo(1d));

            Assert.That(
                scheduler.CanLaunch(1.20d, 0.05d, false),
                Is.True);
            scheduler.MarkAccepted(1.20d);

            Assert.That(
                scheduler.CanLaunch(1.20d, 0.05d, false),
                Is.False);
        }

        [Test]
        public void ResetAllowsImmediateFirstLaunch()
        {
            var scheduler = new InferenceLaunchScheduler();
            scheduler.MarkAccepted(10d);
            scheduler.Reset();

            Assert.That(scheduler.HasAcceptedRequest, Is.False);
            Assert.That(
                scheduler.CanLaunch(10d, 0.05d, false),
                Is.True);
        }
    }
}
