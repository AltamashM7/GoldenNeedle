using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using NUnit.Framework;
using UnityEditor;
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
        public void ImmediateLaunchAllowedWhenAllConditionsAreValid()
        {
            Assert.That(
                ImmediateLaunchAllowed(),
                Is.True);
        }

        [Test]
        public void ImmediateLaunchIsBlockedByCameraSwitchPending()
        {
            Assert.That(
                ImmediateLaunchAllowed(cameraSwitchPending: true),
                Is.False);
        }

        [Test]
        public void ImmediateLaunchIsBlockedByOutstandingInference()
        {
            Assert.That(
                ImmediateLaunchAllowed(inferenceOutstanding: true),
                Is.False);
        }

        [Test]
        public void ImmediateLaunchIsBlockedUntilTargetIntervalElapses()
        {
            Assert.That(
                ImmediateLaunchAllowed(intervalElapsed: false),
                Is.False);
        }

        [Test]
        public void ImmediateLaunchIsBlockedByBodyResourceConfigurationMismatch()
        {
            Assert.That(
                ImmediateLaunchAllowed(bodyResourcesCurrent: false),
                Is.False);
        }

        [Test]
        public void ImmediateLaunchIsBlockedByCoordinateConventionMismatch()
        {
            Assert.That(
                ImmediateLaunchAllowed(coordinateConventionCurrent: false),
                Is.False);
        }

        [Test]
        public void ExperimentOffLeavesBaselineUpdateLaunchPolicyAvailable()
        {
            Assert.That(
                ImmediateLaunchAllowed(experimentEnabled: false),
                Is.False);
            Assert.That(
                LatestFramePipelinePolicy.CanLaunchInference(
                    preparedFrameAvailable: true,
                    inferenceOutstanding: false,
                    intervalElapsed: true),
                Is.True);
        }

        [Test]
        public void DirectReadbackExperimentOffUsesHomuler()
        {
            Assert.That(
                SelectReadbackPath(experimentEnabled: false),
                Is.EqualTo(BodyReadbackPath.Homuler));
        }

        [Test]
        public void DirectReadbackWithoutDownscaledBodyTextureUsesFallback()
        {
            Assert.That(
                SelectReadbackPath(hasDownscaledBodyRenderTexture: false),
                Is.EqualTo(BodyReadbackPath.DirectFallback));
        }

        [Test]
        public void DirectReadbackNoFlipUsesDirectCpuWithoutStaging()
        {
            Assert.That(
                SelectReadbackPath(
                    flipHorizontally: false,
                    flipVertically: false,
                    flipStagingAvailable: false),
                Is.EqualTo(BodyReadbackPath.DirectCPU));
        }

        [Test]
        public void DirectReadbackHorizontalFlipUsesDirectCpuWhenStagingAvailable()
        {
            Assert.That(
                SelectReadbackPath(
                    flipHorizontally: true,
                    flipVertically: false,
                    flipStagingAvailable: true),
                Is.EqualTo(BodyReadbackPath.DirectCPU));
        }

        [Test]
        public void DirectReadbackVerticalFlipUsesDirectCpuWhenStagingAvailable()
        {
            Assert.That(
                SelectReadbackPath(
                    flipHorizontally: false,
                    flipVertically: true,
                    flipStagingAvailable: true),
                Is.EqualTo(BodyReadbackPath.DirectCPU));
        }

        [Test]
        public void DirectReadbackHorizontalAndVerticalFlipUsesDirectCpuWhenStagingAvailable()
        {
            Assert.That(
                SelectReadbackPath(
                    flipHorizontally: true,
                    flipVertically: true,
                    flipStagingAvailable: true),
                Is.EqualTo(BodyReadbackPath.DirectCPU));
        }

        [Test]
        public void DirectReadbackFlipRequiredWithoutStagingUsesFallback()
        {
            Assert.That(
                SelectReadbackPath(
                    flipHorizontally: false,
                    flipVertically: true,
                    flipStagingAvailable: false),
                Is.EqualTo(BodyReadbackPath.DirectFallback));
        }

        [Test]
        public void DirectReadbackDimensionMismatchUsesFallback()
        {
            Assert.That(
                SelectReadbackPath(sourceDimensionsMatchTextureFrame: false),
                Is.EqualTo(BodyReadbackPath.DirectFallback));
        }

        [Test]
        public void DirectReadbackBodyResourceMismatchUsesFallback()
        {
            Assert.That(
                SelectReadbackPath(bodyResourcesCurrent: false),
                Is.EqualTo(BodyReadbackPath.DirectFallback));
        }

        [Test]
        public void DirectReadbackUnsupportedFormatUsesFallback()
        {
            Assert.That(
                SelectReadbackPath(formatSupported: false),
                Is.EqualTo(BodyReadbackPath.DirectFallback));
        }

        [Test]
        public void DirectReadbackAllConditionsValidUsesDirectCpu()
        {
            Assert.That(
                SelectReadbackPath(),
                Is.EqualTo(BodyReadbackPath.DirectCPU));
        }

        [Test]
        public void DirectReadbackSessionUnavailableUsesFallback()
        {
            Assert.That(
                SelectReadbackPath(sessionAvailable: false),
                Is.EqualTo(BodyReadbackPath.DirectFallback));
        }

        [TestCase(false, false, 1f, 1f, 0f, 0f, DirectReadbackStage.None)]
        [TestCase(true, false, -1f, 1f, 1f, 0f, DirectReadbackStage.H)]
        [TestCase(false, true, 1f, -1f, 0f, 1f, DirectReadbackStage.V)]
        [TestCase(true, true, -1f, -1f, 1f, 1f, DirectReadbackStage.HV)]
        public void DirectReadbackFlipTransformMatchesHomulerConvention(
            bool flipHorizontally,
            bool flipVertically,
            float scaleX,
            float scaleY,
            float offsetX,
            float offsetY,
            DirectReadbackStage expectedStage)
        {
            LatestFramePipelinePolicy.GetDirectReadbackFlipTransform(
                flipHorizontally,
                flipVertically,
                out var scale,
                out var offset);

            Assert.That(scale, Is.EqualTo(new Vector2(scaleX, scaleY)));
            Assert.That(offset, Is.EqualTo(new Vector2(offsetX, offsetY)));
            Assert.That(
                LatestFramePipelinePolicy.GetDirectReadbackStage(
                    flipHorizontally,
                    flipVertically),
                Is.EqualTo(expectedStage));
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
        public void ProviderDefaultsBodyInferenceDownscaleToEnabledAt320()
        {
            var gameObject = new GameObject("MediaPipeProviderBodyInputDefaultTest");
            try
            {
                var provider = gameObject.AddComponent<MediaPipePoseProvider>();
                var serializedProvider = new SerializedObject(provider);

                Assert.That(
                    serializedProvider.FindProperty("enableBodyInferenceDownscale").boolValue,
                    Is.True);
                Assert.That(
                    serializedProvider.FindProperty("bodyInferenceLongEdge").intValue,
                    Is.EqualTo(320));
                Assert.That(
                    serializedProvider.FindProperty("enableImmediateInferenceLaunchAfterReadback").boolValue,
                    Is.True);
                Assert.That(
                    serializedProvider.FindProperty("enableDirectBodyCpuReadback").boolValue,
                    Is.False);
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

        [Test]
        public void BodyInferenceResolutionPreservesLandscapeAspectRatio()
        {
            var dimensions = BodyInferenceResolution.Calculate(640, 480, true, 320);

            Assert.That(dimensions.x, Is.EqualTo(320));
            Assert.That(dimensions.y, Is.EqualTo(240));
        }

        [Test]
        public void BodyInferenceResolutionPreservesPortraitAspectRatio()
        {
            var dimensions = BodyInferenceResolution.Calculate(480, 640, true, 320);

            Assert.That(dimensions.x, Is.EqualTo(240));
            Assert.That(dimensions.y, Is.EqualTo(320));
        }

        [Test]
        public void BodyInferenceResolutionRetainsAspectRatioWithinIntegerRounding()
        {
            var dimensions = BodyInferenceResolution.Calculate(1280, 720, true, 320);

            Assert.That(dimensions.x, Is.EqualTo(320));
            Assert.That(dimensions.y, Is.EqualTo(180));
            Assert.That(
                dimensions.x / (double)dimensions.y,
                Is.EqualTo(1280d / 720d).Within(0.01d));
        }

        [Test]
        public void DisabledBodyInferenceDownscaleUsesSourceDimensionsExactly()
        {
            var dimensions = BodyInferenceResolution.Calculate(641, 479, false, 1);

            Assert.That(dimensions.x, Is.EqualTo(641));
            Assert.That(dimensions.y, Is.EqualTo(479));
        }

        [Test]
        public void BodyInferenceTargetAtOrAboveSourceNeverUpscales()
        {
            var equalTarget = BodyInferenceResolution.Calculate(640, 480, true, 640);
            var largerTarget = BodyInferenceResolution.Calculate(640, 480, true, 1000);

            Assert.That(equalTarget, Is.EqualTo(new Vector2Int(640, 480)));
            Assert.That(largerTarget, Is.EqualTo(new Vector2Int(640, 480)));
        }

        [Test]
        public void BodyInferenceResolutionNeverProducesZeroDimensions()
        {
            var dimensions = BodyInferenceResolution.Calculate(0, 0, true, 0);

            Assert.That(dimensions.x, Is.GreaterThanOrEqualTo(1));
            Assert.That(dimensions.y, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void BodyInferenceLongEdgeIsBoundedToSafeEvenRange()
        {
            Assert.That(
                BodyInferenceResolution.ClampLongEdge(1),
                Is.EqualTo(BodyInferenceResolution.MinimumLongEdge));
            Assert.That(
                BodyInferenceResolution.ClampLongEdge(321),
                Is.EqualTo(320));
            Assert.That(
                BodyInferenceResolution.ClampLongEdge(int.MaxValue),
                Is.EqualTo(BodyInferenceResolution.MaximumLongEdge));
        }

        private static bool ImmediateLaunchAllowed(
            bool experimentEnabled = true,
            bool providerReady = true,
            bool shuttingDown = false,
            bool cameraSwitchPending = false,
            bool poseLandmarkerAvailable = true,
            bool preparedFrameAvailable = true,
            bool coordinateConventionCurrent = true,
            bool bodyResourcesCurrent = true,
            bool inferenceOutstanding = false,
            bool intervalElapsed = true)
        {
            return LatestFramePipelinePolicy.CanImmediateLaunchAfterReadback(
                experimentEnabled,
                providerReady,
                shuttingDown,
                cameraSwitchPending,
                poseLandmarkerAvailable,
                preparedFrameAvailable,
                coordinateConventionCurrent,
                bodyResourcesCurrent,
                inferenceOutstanding,
                intervalElapsed);
        }

        private static BodyReadbackPath SelectReadbackPath(
            bool experimentEnabled = true,
            bool providerReady = true,
            bool hasDownscaledBodyRenderTexture = true,
            bool sourceDimensionsMatchTextureFrame = true,
            bool bodyResourcesCurrent = true,
            bool flipHorizontally = false,
            bool flipVertically = false,
            bool flipStagingAvailable = true,
            bool formatSupported = true,
            bool sessionAvailable = true)
        {
            return LatestFramePipelinePolicy.SelectBodyReadbackPath(
                experimentEnabled,
                providerReady,
                hasDownscaledBodyRenderTexture,
                sourceDimensionsMatchTextureFrame,
                bodyResourcesCurrent,
                flipHorizontally,
                flipVertically,
                flipStagingAvailable,
                formatSupported,
                sessionAvailable);
        }
    }
}
