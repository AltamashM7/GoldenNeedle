using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class MediaPipeInferenceSchedulingTests
    {
        [Test]
        public void IntervalMustElapseBeforeAnotherLaunch()
        {
            var scheduler = new InferenceLaunchScheduler();

            Assert.That(scheduler.CanLaunch(0d, 0.05d, false), Is.True);
            scheduler.MarkAccepted(0d);

            Assert.That(scheduler.CanLaunch(0.02d, 0.05d, false), Is.False);
            Assert.That(scheduler.CanLaunch(0.05d, 0.05d, false), Is.True);
        }

        [Test]
        public void BusySkipDoesNotPostponeNextFreeLaunch()
        {
            var scheduler = new InferenceLaunchScheduler();
            scheduler.MarkAccepted(0d);

            Assert.That(scheduler.IsIntervalElapsed(0.05d, 0.05d), Is.True);
            Assert.That(scheduler.CanLaunch(0.05d, 0.05d, true), Is.False);
            Assert.That(scheduler.LastAcceptedRequestAtSeconds, Is.EqualTo(0d));
            Assert.That(scheduler.CanLaunch(0.06d, 0.05d, false), Is.True);
        }

        [Test]
        public void BusyStateNeverCreatesAQueuedLaunch()
        {
            var scheduler = new InferenceLaunchScheduler();
            scheduler.MarkAccepted(1d);

            Assert.That(scheduler.CanLaunch(1.10d, 0.05d, true), Is.False);
            Assert.That(scheduler.CanLaunch(1.20d, 0.05d, true), Is.False);
            Assert.That(scheduler.LastAcceptedRequestAtSeconds, Is.EqualTo(1d));

            Assert.That(scheduler.CanLaunch(1.20d, 0.05d, false), Is.True);
            scheduler.MarkAccepted(1.20d);
            Assert.That(scheduler.CanLaunch(1.20d, 0.05d, false), Is.False);
        }

        [Test]
        public void PreparedFrameIsRequiredForInferenceLaunch()
        {
            var scheduler = new InferenceLaunchScheduler();
            scheduler.MarkAccepted(0d);

            Assert.That(
                scheduler.CanLaunch(0.04d, 1d / 30d, false, false),
                Is.False);
            Assert.That(
                scheduler.CanLaunch(0.04d, 1d / 30d, false, true),
                Is.True);
        }

        [Test]
        public void ReadbackPreparationCanOverlapActiveInference()
        {
            // Readback admission intentionally depends only on one-readback + fresh-frame bounds.
            // Inference occupancy is not an input, which removes the old serialized dead time.
            Assert.That(
                LatestFramePipelinePolicy.CanStartReadback(
                    readbackPending: false,
                    freshFramePending: true),
                Is.True);
            Assert.That(
                LatestFramePipelinePolicy.CanStartReadback(
                    readbackPending: true,
                    freshFramePending: true),
                Is.False);
            Assert.That(
                LatestFramePipelinePolicy.CanStartReadback(
                    readbackPending: false,
                    freshFramePending: false),
                Is.False);
        }

        [Test]
        public void PreparedFrameCannotCreateConcurrentInference()
        {
            Assert.That(
                LatestFramePipelinePolicy.CanLaunchInference(
                    preparedFrameAvailable: true,
                    inferenceOutstanding: true,
                    intervalElapsed: true),
                Is.False);
            Assert.That(
                LatestFramePipelinePolicy.CanLaunchInference(
                    preparedFrameAvailable: true,
                    inferenceOutstanding: false,
                    intervalElapsed: true),
                Is.True);
        }

        [Test]
        public void ProviderDefaultInferenceTargetIsThirtyFps()
        {
            var gameObject = new GameObject("MediaPipeProviderDefaultTest");
            try
            {
                var provider = gameObject.AddComponent<MediaPipePoseProvider>();
                Assert.That(provider.TargetInferenceFps, Is.EqualTo(30f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ResetAllowsImmediateFirstLaunch()
        {
            var scheduler = new InferenceLaunchScheduler();
            scheduler.MarkAccepted(10d);
            scheduler.Reset();

            Assert.That(scheduler.HasAcceptedRequest, Is.False);
            Assert.That(scheduler.CanLaunch(10d, 0.05d, false), Is.True);
        }
    }
}
