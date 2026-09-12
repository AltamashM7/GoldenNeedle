using GoldenNeedle.Debugging.GpuInferenceSpike;
using NUnit.Framework;

namespace GoldenNeedle.Tests
{
    public sealed class GpuInferenceSpikeMathTests
    {
        [Test]
        public void StatisticsComputesPercentilesAndRate()
        {
            var stats = GpuInferenceSpikeMath.ComputeStatistics(
                new[] { 10.0, 20.0, 30.0, 40.0, 50.0 });

            Assert.That(stats.Count, Is.EqualTo(5));
            Assert.That(stats.MeanMilliseconds, Is.EqualTo(30.0).Within(1e-9));
            Assert.That(stats.P50Milliseconds, Is.EqualTo(30.0).Within(1e-9));
            Assert.That(stats.P95Milliseconds, Is.EqualTo(48.0).Within(1e-9));
            Assert.That(stats.P99Milliseconds, Is.EqualTo(49.6).Within(1e-9));
            Assert.That(stats.InferencesPerSecond, Is.EqualTo(1000.0 / 30.0).Within(1e-9));
        }

        [Test]
        public void ComparisonReportsExpectedErrors()
        {
            var reference = new[] { 0f, 1f, 2f, -4f };
            var candidate = new[] { 0f, 1.5f, 1f, -2f };

            var comparison = GpuInferenceSpikeMath.Compare(reference, candidate);

            Assert.That(comparison.Count, Is.EqualTo(4));
            Assert.That(comparison.NonFiniteCount, Is.Zero);
            Assert.That(comparison.MaxAbsoluteError, Is.EqualTo(2.0).Within(1e-9));
            Assert.That(comparison.MeanAbsoluteError, Is.EqualTo(0.875).Within(1e-9));
            Assert.That(
                comparison.RootMeanSquareError,
                Is.EqualTo(System.Math.Sqrt(5.25 / 4.0)).Within(1e-9));
        }

        [Test]
        public void ComparisonCountsNonFiniteValues()
        {
            var reference = new[] { 1f, float.NaN, 3f, float.PositiveInfinity };
            var candidate = new[] { 1f, 2f, float.NegativeInfinity, 4f };

            var comparison = GpuInferenceSpikeMath.Compare(reference, candidate);

            Assert.That(comparison.NonFiniteCount, Is.EqualTo(3));
            Assert.That(comparison.IsFinite, Is.False);
            Assert.That(GpuInferenceSpikeMath.ContainsNonFinite(candidate), Is.True);
        }

        [Test]
        public void DeterministicInputsAreStable()
        {
            var first = GpuInferenceSpikeMath.CreateDeterministicInput(32, 2, 1234);
            var second = GpuInferenceSpikeMath.CreateDeterministicInput(32, 2, 1234);
            var gradient = GpuInferenceSpikeMath.CreateDeterministicInput(5, 1);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(gradient, Is.EqualTo(new[] { 0f, 0.25f, 0.5f, 0.75f, 1f }));
        }

        [TestCase(true, true, 25.0, 10.0, true, true, GpuSpikeVerdict.StrongPass)]
        [TestCase(true, true, 17.0, 10.0, true, true, GpuSpikeVerdict.ConditionalPass)]
        [TestCase(true, true, 12.0, 10.0, true, true, GpuSpikeVerdict.StopReject)]
        [TestCase(true, true, 25.0, 30.0, true, true, GpuSpikeVerdict.StopReject)]
        [TestCase(false, true, 25.0, 10.0, true, true, GpuSpikeVerdict.StopReject)]
        public void VerdictClassifierMatchesSpikeThresholds(
            bool tfliteRuns,
            bool equivalent,
            double gpuRate,
            double cpuRate,
            bool renderOkay,
            bool resident,
            GpuSpikeVerdict expected)
        {
            var verdict = GpuInferenceSpikeMath.Classify(
                tfliteRuns,
                equivalent,
                gpuRate,
                cpuRate,
                renderOkay,
                resident);

            Assert.That(verdict, Is.EqualTo(expected));
        }
    }
}