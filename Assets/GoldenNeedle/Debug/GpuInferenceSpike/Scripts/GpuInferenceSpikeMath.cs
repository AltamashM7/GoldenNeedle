using System;

namespace GoldenNeedle.Debugging.GpuInferenceSpike
{
    public readonly struct GpuSpikeStatistics
    {
        public readonly int Count;
        public readonly double MeanMilliseconds;
        public readonly double P50Milliseconds;
        public readonly double P95Milliseconds;
        public readonly double P99Milliseconds;
        public readonly double InferencesPerSecond;

        public GpuSpikeStatistics(
            int count,
            double meanMilliseconds,
            double p50Milliseconds,
            double p95Milliseconds,
            double p99Milliseconds)
        {
            Count = count;
            MeanMilliseconds = meanMilliseconds;
            P50Milliseconds = p50Milliseconds;
            P95Milliseconds = p95Milliseconds;
            P99Milliseconds = p99Milliseconds;
            InferencesPerSecond = meanMilliseconds > 0.0 ? 1000.0 / meanMilliseconds : 0.0;
        }

        public override string ToString()
        {
            return $"n={Count} mean={MeanMilliseconds:F2} ms p50={P50Milliseconds:F2} ms " +
                   $"p95={P95Milliseconds:F2} ms p99={P99Milliseconds:F2} ms " +
                   $"rate={InferencesPerSecond:F2}/s";
        }
    }

    public readonly struct GpuSpikeComparison
    {
        public readonly int Count;
        public readonly int NonFiniteCount;
        public readonly double MaxAbsoluteError;
        public readonly double MeanAbsoluteError;
        public readonly double RootMeanSquareError;
        public readonly double MeanRelativeError;

        public GpuSpikeComparison(
            int count,
            int nonFiniteCount,
            double maxAbsoluteError,
            double meanAbsoluteError,
            double rootMeanSquareError,
            double meanRelativeError)
        {
            Count = count;
            NonFiniteCount = nonFiniteCount;
            MaxAbsoluteError = maxAbsoluteError;
            MeanAbsoluteError = meanAbsoluteError;
            RootMeanSquareError = rootMeanSquareError;
            MeanRelativeError = meanRelativeError;
        }

        public bool IsFinite => NonFiniteCount == 0;

        public override string ToString()
        {
            return $"n={Count} nonfinite={NonFiniteCount} maxAbs={MaxAbsoluteError:E3} " +
                   $"meanAbs={MeanAbsoluteError:E3} rms={RootMeanSquareError:E3} " +
                   $"meanRel={MeanRelativeError:E3}";
        }
    }

    public enum GpuSpikeVerdict
    {
        Unmeasured = 0,
        StrongPass = 1,
        ConditionalPass = 2,
        StopReject = 3,
    }

    public static class GpuInferenceSpikeMath
    {
        public static float[] CreateDeterministicInput(int elementCount, int mode, int seed = 1337)
        {
            if (elementCount < 0)
                throw new ArgumentOutOfRangeException(nameof(elementCount));

            var values = new float[elementCount];
            switch (mode)
            {
                case 0:
                    return values;

                case 1:
                    var denominator = Math.Max(1, elementCount - 1);
                    for (var i = 0; i < elementCount; i++)
                        values[i] = (float)i / denominator;
                    return values;

                case 2:
                    var random = new Random(seed);
                    for (var i = 0; i < elementCount; i++)
                        values[i] = (float)random.NextDouble();
                    return values;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), "Expected 0=zeros, 1=gradient, or 2=seeded random.");
            }
        }

        public static GpuSpikeStatistics ComputeStatistics(double[] milliseconds)
        {
            if (milliseconds == null)
                throw new ArgumentNullException(nameof(milliseconds));
            if (milliseconds.Length == 0)
                return new GpuSpikeStatistics(0, 0.0, 0.0, 0.0, 0.0);

            var sorted = (double[])milliseconds.Clone();
            Array.Sort(sorted);

            double sum = 0.0;
            for (var i = 0; i < sorted.Length; i++)
                sum += sorted[i];

            return new GpuSpikeStatistics(
                sorted.Length,
                sum / sorted.Length,
                PercentileSorted(sorted, 0.50),
                PercentileSorted(sorted, 0.95),
                PercentileSorted(sorted, 0.99));
        }

        public static GpuSpikeComparison Compare(float[] reference, float[] candidate)
        {
            if (reference == null)
                throw new ArgumentNullException(nameof(reference));
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            if (reference.Length != candidate.Length)
                throw new ArgumentException("Reference and candidate arrays must have the same length.");

            if (reference.Length == 0)
                return new GpuSpikeComparison(0, 0, 0.0, 0.0, 0.0, 0.0);

            var nonFinite = 0;
            double maxAbs = 0.0;
            double sumAbs = 0.0;
            double sumSquared = 0.0;
            double sumRelative = 0.0;
            var finiteCount = 0;

            for (var i = 0; i < reference.Length; i++)
            {
                var a = reference[i];
                var b = candidate[i];
                if (!IsFinite(a) || !IsFinite(b))
                {
                    nonFinite++;
                    continue;
                }

                var abs = Math.Abs((double)a - b);
                var denom = Math.Max(Math.Max(Math.Abs((double)a), Math.Abs((double)b)), 1e-6);
                maxAbs = Math.Max(maxAbs, abs);
                sumAbs += abs;
                sumSquared += abs * abs;
                sumRelative += abs / denom;
                finiteCount++;
            }

            if (finiteCount == 0)
                return new GpuSpikeComparison(reference.Length, nonFinite, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);

            return new GpuSpikeComparison(
                reference.Length,
                nonFinite,
                maxAbs,
                sumAbs / finiteCount,
                Math.Sqrt(sumSquared / finiteCount),
                sumRelative / finiteCount);
        }

        public static bool ContainsNonFinite(float[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            for (var i = 0; i < values.Length; i++)
            {
                if (!IsFinite(values[i]))
                    return true;
            }

            return false;
        }

        public static GpuSpikeVerdict Classify(
            bool exactTfliteRunsOnGpu,
            bool outputsFiniteAndEquivalent,
            double landmarkGpuRate,
            double landmarkCpuRate,
            bool renderImpactAcceptable,
            bool gpuResidentInputProven)
        {
            if (!exactTfliteRunsOnGpu ||
                !outputsFiniteAndEquivalent ||
                !renderImpactAcceptable ||
                !gpuResidentInputProven ||
                landmarkGpuRate <= landmarkCpuRate)
                return GpuSpikeVerdict.StopReject;

            if (landmarkGpuRate >= 20.0)
                return GpuSpikeVerdict.StrongPass;

            if (landmarkGpuRate >= 15.0)
                return GpuSpikeVerdict.ConditionalPass;

            return GpuSpikeVerdict.StopReject;
        }

        static double PercentileSorted(double[] sorted, double percentile)
        {
            if (sorted.Length == 1)
                return sorted[0];

            var position = (sorted.Length - 1) * percentile;
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);
            if (lower == upper)
                return sorted[lower];

            var fraction = position - lower;
            return sorted[lower] + ((sorted[upper] - sorted[lower]) * fraction);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}