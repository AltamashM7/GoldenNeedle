using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEngine;

namespace GoldenNeedle.Debugging.GpuInferenceSpike
{
    public sealed class GpuInferenceSpikeRunner : MonoBehaviour
    {
        enum Stage
        {
            Idle,
            Warmup,
            Equivalence,
            CpuBenchmark,
            GpuBenchmark,
            GpuResidentBenchmark,
            Complete,
            Failed,
        }

        [Header("Exact extracted LiteRT assets")]
        [SerializeField] ModelAsset detectorModelAsset;
        [SerializeField] ModelAsset landmarkModelAsset;

        [Header("Verified TFLite input dimensions")]
        [SerializeField] int detectorWidth = 224;
        [SerializeField] int detectorHeight = 224;
        [SerializeField] int landmarkWidth = 256;
        [SerializeField] int landmarkHeight = 256;
        [SerializeField] int inputChannels = 3;

        [Header("Benchmark")]
        [SerializeField, Min(30)] int warmupIterations = 30;
        [SerializeField, Min(100)] int measuredIterations = 300;
        [SerializeField, Min(1)] int maxPerformanceOutputElements = 50000;

        Stage _stage = Stage.Idle;
        string _status = "Idle. Click Run benchmark.";
        string _hardwareSummary;
        string _modelSummary = "Models not loaded.";
        string _resultSummary = "No benchmark has run.";
        bool _running;

        Model _detectorModel;
        Model _landmarkModel;

        readonly Queue<float> _recentFrameMilliseconds = new Queue<float>();
        const int FrameWindow = 240;

        void Awake()
        {
            _hardwareSummary = BuildHardwareSummary();
            TryLoadModels();
        }

        void Update()
        {
            var ms = Time.unscaledDeltaTime * 1000f;
            _recentFrameMilliseconds.Enqueue(ms);
            while (_recentFrameMilliseconds.Count > FrameWindow)
                _recentFrameMilliseconds.Dequeue();
        }

        void OnGUI()
        {
            var width = Mathf.Min(Screen.width - 24f, 1120f);
            GUILayout.BeginArea(new Rect(12f, 12f, width, Screen.height - 24f), GUI.skin.box);
            GUILayout.Label("<b>Golden Needle — Sentis GPU Inference Spike</b>", RichLabel());
            GUILayout.Label($"Stage: {_stage}");
            GUILayout.Space(4f);
            GUILayout.Label(_hardwareSummary);
            GUILayout.Space(8f);
            GUILayout.Label(_modelSummary);
            GUILayout.Space(8f);
            GUILayout.Label(_status);
            GUILayout.Space(8f);

            GUI.enabled = !_running && _detectorModel != null && _landmarkModel != null;
            if (GUILayout.Button(_running ? "Benchmark running…" : "Run benchmark", GUILayout.Height(32f)))
                RunBenchmark();
            GUI.enabled = true;

            GUILayout.Space(8f);
            GUILayout.TextArea(_resultSummary, GUILayout.ExpandHeight(true));
            GUILayout.EndArea();
        }

        static GUIStyle RichLabel()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.richText = true;
            style.wordWrap = true;
            return style;
        }

        void TryLoadModels()
        {
            try
            {
                if (detectorModelAsset == null || landmarkModelAsset == null)
                {
                    _modelSummary =
                        "Model assets are not assigned. Run Golden Needle > GPU Inference Spike > Prepare Exact Models + Scene.";
                    return;
                }

                _detectorModel = ModelLoader.Load(detectorModelAsset);
                _landmarkModel = ModelLoader.Load(landmarkModelAsset);
                _modelSummary =
                    "Sentis assembly: " + typeof(Model).Assembly.GetName().Version + "\n" +
                    DescribeModel("Detector", _detectorModel, detectorWidth, detectorHeight) + "\n\n" +
                    DescribeModel("Landmark", _landmarkModel, landmarkWidth, landmarkHeight);
            }
            catch (Exception ex)
            {
                Fail("Model import/load failed: " + ex);
            }
        }

        static string DescribeModel(string label, Model model, int expectedWidth, int expectedHeight)
        {
            var builder = new StringBuilder();
            builder.Append(label)
                .Append(": inputs=").Append(model.inputs.Count)
                .Append(" outputs=").Append(model.outputs.Count)
                .Append(" layers=").Append(model.layers.Count)
                .Append(" expectedInput=[1,")
                .Append(expectedHeight).Append(',')
                .Append(expectedWidth).Append(",3] NHWC");

            for (var i = 0; i < model.inputs.Count; i++)
            {
                var input = model.inputs[i];
                builder.Append("\n  input[").Append(i).Append("] ")
                    .Append(input.name)
                    .Append(" dtype=").Append(input.dataType)
                    .Append(" shape=").Append(input.shape);
            }

            var layerCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < model.layers.Count; i++)
            {
                var name = model.layers[i].GetType().Name;
                layerCounts.TryGetValue(name, out var count);
                layerCounts[name] = count + 1;
            }

            builder.Append("\n  Sentis layer set: ");
            var first = true;
            foreach (var pair in layerCounts)
            {
                if (!first)
                    builder.Append(", ");
                first = false;
                builder.Append(pair.Key).Append('×').Append(pair.Value);
            }

            return builder.ToString();
        }

        async void RunBenchmark()
        {
            if (_running || _detectorModel == null || _landmarkModel == null)
                return;

            _running = true;
            _resultSummary = "";
            try
            {
                var report = new StringBuilder();
                report.AppendLine("=== HARDWARE ===");
                report.AppendLine(_hardwareSummary);
                report.AppendLine();

                await RunModelSuite(
                    "Detector",
                    _detectorModel,
                    detectorWidth,
                    detectorHeight,
                    report);

                await RunModelSuite(
                    "Landmark",
                    _landmarkModel,
                    landmarkWidth,
                    landmarkHeight,
                    report);

                _stage = Stage.Complete;
                _status =
                    "Complete. This is an architecture benchmark only; it does not drive the production avatar.";
                _resultSummary = report.ToString();
                UnityEngine.Debug.Log("[GpuInferenceSpike]\n" + _resultSummary);
            }
            catch (Exception ex)
            {
                Fail("Benchmark failed: " + ex);
            }
            finally
            {
                _running = false;
            }
        }

        async Task RunModelSuite(
            string label,
            Model model,
            int width,
            int height,
            StringBuilder report)
        {
            ValidateModelForSpike(label, model);

            report.AppendLine("=== " + label.ToUpperInvariant() + " ===");
            report.AppendLine($"Input tensor: [1,{height},{width},{inputChannels}] float NHWC");
            report.AppendLine($"Imported outputs: {model.outputs.Count}; imported Sentis layers: {model.layers.Count}");

            _stage = Stage.Equivalence;
            _status = label + ": CPU vs GPUCompute equivalence (zeros, gradient, seeded random)…";
            var equivalence = await RunEquivalence(model, width, height);
            report.Append(equivalence);
            report.AppendLine();

            var benchmarkValues = GpuInferenceSpikeMath.CreateDeterministicInput(
                width * height * inputChannels,
                2,
                20260912);

            _stage = Stage.Warmup;
            _status = label + ": CPU warmup…";
            using (var cpuInput = new Tensor<float>(
                       new TensorShape(1, height, width, inputChannels),
                       benchmarkValues))
            using (var cpuWorker = new Worker(model, BackendType.CPU))
            {
                await WarmUp(cpuWorker, cpuInput, warmupIterations);

                _stage = Stage.CpuBenchmark;
                _status = label + ": CPU benchmark…";
                var cpu = await MeasureWorker(
                    cpuWorker,
                    cpuInput,
                    model.outputs.Count,
                    measuredIterations);
                report.AppendLine("CPU: " + cpu.Statistics);
                report.AppendLine(
                    $"CPU completion output[{cpu.CompletionOutputIndex}] bytes/readback={cpu.OutputBytes}");
            }

            _stage = Stage.Warmup;
            _status = label + ": GPUCompute warmup…";
            using (var gpuInput = new Tensor<float>(
                       new TensorShape(1, height, width, inputChannels),
                       benchmarkValues))
            using (var gpuWorker = new Worker(model, BackendType.GPUCompute))
            {
                await WarmUp(gpuWorker, gpuInput, warmupIterations);

                _stage = Stage.GpuBenchmark;
                _status = label + ": GPUCompute tensor benchmark…";
                var gpu = await MeasureWorker(
                    gpuWorker,
                    gpuInput,
                    model.outputs.Count,
                    measuredIterations);
                report.AppendLine("GPUCompute fixed tensor: " + gpu.Statistics);
                report.AppendLine(
                    $"GPU completion output[{gpu.CompletionOutputIndex}] bytes/readback={gpu.OutputBytes}");
            }

            _stage = Stage.GpuResidentBenchmark;
            _status = label + ": GPU-resident RenderTexture → TextureConverter → GPUCompute → small output readback…";
            var resident = await MeasureGpuResident(model, width, height, measuredIterations);
            report.AppendLine("GPU-resident path: " + resident.Statistics);
            report.AppendLine(
                $"GPU-resident selected-output bytes/readback={resident.OutputBytes}; " +
                $"TextureConverter submission mean={resident.PreprocessSubmissionMeanMilliseconds:F3} ms");
            report.AppendLine(
                "Texture path uses a model-sized RenderTexture and TextureTransform(NHWC, TopLeft). " +
                "TextureConverter performs texture-to-tensor channel/layout/origin conversion and resampling. " +
                "Arbitrary input value normalization is not folded into TextureTransform; exact MediaPipe " +
                "detector/ROI preprocessing remains future work.");
            report.AppendLine(
                "Performance readback currently selects every float output up to the configured element cap; " +
                "the exact minimum detector/landmark postprocess output set will be narrowed only when the " +
                "MediaPipe decode/ROI/postprocess reconstruction phase begins.");
            report.AppendLine(
                $"Render frame sample during suite: mean={CurrentFrameMean():F2} ms p95={CurrentFrameP95():F2} ms.");
            report.AppendLine();
        }

        static void ValidateModelForSpike(string label, Model model)
        {
            if (model.inputs.Count != 1)
                throw new InvalidOperationException(label + " spike expects exactly one model input.");
            if (model.inputs[0].dataType != DataType.Float)
                throw new InvalidOperationException(
                    label + " input is " + model.inputs[0].dataType + "; this spike currently supports Float input only.");
            if (model.outputs.Count == 0)
                throw new InvalidOperationException(label + " model exposes no outputs.");
        }

        async Task<string> RunEquivalence(Model model, int width, int height)
        {
            var report = new StringBuilder();
            report.AppendLine("CPU vs GPUCompute equivalence:");

            for (var mode = 0; mode < 3; mode++)
            {
                var label = mode == 0 ? "zeros" : mode == 1 ? "gradient" : "seeded-random";
                var values = GpuInferenceSpikeMath.CreateDeterministicInput(
                    width * height * inputChannels,
                    mode,
                    424242);

                var cpu = await RunAllOutputs(model, BackendType.CPU, width, height, values);
                var gpu = await RunAllOutputs(model, BackendType.GPUCompute, width, height, values);

                if (cpu.Count != gpu.Count)
                    throw new InvalidOperationException("CPU/GPU output count mismatch.");

                report.AppendLine("  " + label + ":");
                for (var outputIndex = 0; outputIndex < cpu.Count; outputIndex++)
                {
                    var a = cpu[outputIndex];
                    var b = gpu[outputIndex];
                    if (!string.Equals(a.Shape, b.Shape, StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            $"Output {outputIndex} shape mismatch CPU={a.Shape} GPU={b.Shape}.");

                    var comparison = GpuInferenceSpikeMath.Compare(a.Values, b.Values);
                    report.AppendLine(
                        $"    output[{outputIndex}] shape={a.Shape} dtype=Float bytes={a.Values.Length * sizeof(float)} {comparison}");
                }
            }

            return report.ToString();
        }

        static async Task<List<OutputSnapshot>> RunAllOutputs(
            Model model,
            BackendType backend,
            int width,
            int height,
            float[] values)
        {
            using var input = new Tensor<float>(
                new TensorShape(1, height, width, 3),
                values);
            using var worker = new Worker(model, backend);

            worker.Schedule(input);

            var snapshots = new List<OutputSnapshot>(model.outputs.Count);
            for (var i = 0; i < model.outputs.Count; i++)
            {
                var output = worker.PeekOutput(i);
                if (output.dataType != DataType.Float || !(output is Tensor<float> floatOutput))
                    throw new InvalidOperationException(
                        $"Output {i} is {output.dataType}; float-only equivalence reader cannot compare it.");

                using var clone = await floatOutput.ReadbackAndCloneAsync();
                var array = clone.DownloadToArray();
                snapshots.Add(new OutputSnapshot(output.shape.ToString(), array));
            }

            return snapshots;
        }

        static async Task WarmUp(Worker worker, Tensor<float> input, int iterations)
        {
            for (var i = 0; i < iterations; i++)
            {
                worker.Schedule(input);
                var output = worker.PeekOutput(0) as Tensor<float>;
                if (output == null)
                    throw new InvalidOperationException("Warmup output[0] is not float.");
                using var clone = await output.ReadbackAndCloneAsync();
            }
        }

        async Task<Measurement> MeasureWorker(
            Worker worker,
            Tensor<float> input,
            int outputCount,
            int iterations)
        {
            var durations = new double[iterations];
            var completionIndex = FindSmallFloatOutput(worker, outputCount);
            var outputBytes = 0;

            for (var i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                worker.Schedule(input);
                var output = worker.PeekOutput(completionIndex) as Tensor<float>;
                if (output == null)
                    throw new InvalidOperationException(
                        $"Completion output[{completionIndex}] is not float.");

                outputBytes = output.shape.length * sizeof(float);
                using var clone = await output.ReadbackAndCloneAsync();
                stopwatch.Stop();
                durations[i] = stopwatch.Elapsed.TotalMilliseconds;

                if ((i & 15) == 15)
                    await Task.Yield();
            }

            return new Measurement(
                GpuInferenceSpikeMath.ComputeStatistics(durations),
                completionIndex,
                outputBytes,
                0.0);
        }

        async Task<Measurement> MeasureGpuResident(
            Model model,
            int width,
            int height,
            int iterations)
        {
            var renderTexture = new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear)
            {
                name = "GpuInferenceSpike_Input",
                useMipMap = false,
                autoGenerateMips = false,
            };
            renderTexture.Create();

            var seedTexture = CreateSeedTexture(width, height);
            Graphics.Blit(seedTexture, renderTexture);
            Destroy(seedTexture);

            var tensor = new Tensor<float>(new TensorShape(1, height, width, inputChannels));
            var worker = new Worker(model, BackendType.GPUCompute);
            var transform = new TextureTransform()
                .SetTensorLayout(TensorLayout.NHWC)
                .SetCoordOrigin(CoordOrigin.TopLeft);

            try
            {
                for (var i = 0; i < warmupIterations; i++)
                {
                    TextureConverter.ToTensor(renderTexture, tensor, transform);
                    worker.Schedule(tensor);
                    await ReadRequiredSmallOutputs(worker, model.outputs.Count);
                }

                var durations = new double[iterations];
                var preprocessSubmission = new double[iterations];
                var outputBytes = 0;

                for (var i = 0; i < iterations; i++)
                {
                    var total = Stopwatch.StartNew();
                    var preprocess = Stopwatch.StartNew();
                    TextureConverter.ToTensor(renderTexture, tensor, transform);
                    preprocess.Stop();
                    preprocessSubmission[i] = preprocess.Elapsed.TotalMilliseconds;

                    worker.Schedule(tensor);
                    outputBytes = await ReadRequiredSmallOutputs(worker, model.outputs.Count);
                    total.Stop();
                    durations[i] = total.Elapsed.TotalMilliseconds;

                    if ((i & 15) == 15)
                        await Task.Yield();
                }

                var preprocessStats = GpuInferenceSpikeMath.ComputeStatistics(preprocessSubmission);
                return new Measurement(
                    GpuInferenceSpikeMath.ComputeStatistics(durations),
                    -1,
                    outputBytes,
                    preprocessStats.MeanMilliseconds);
            }
            finally
            {
                worker.Dispose();
                tensor.Dispose();
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }

        async Task<int> ReadRequiredSmallOutputs(Worker worker, int outputCount)
        {
            var totalBytes = 0;
            var selected = 0;

            for (var i = 0; i < outputCount; i++)
            {
                var output = worker.PeekOutput(i);
                if (output.dataType != DataType.Float || !(output is Tensor<float> floatOutput))
                    continue;

                var elements = output.shape.length;
                if (elements <= 0 || elements > maxPerformanceOutputElements)
                    continue;

                using var clone = await floatOutput.ReadbackAndCloneAsync();
                totalBytes += elements * sizeof(float);
                selected++;
            }

            if (selected == 0)
                throw new InvalidOperationException(
                    "No small float outputs were eligible for the realistic GPU readback path.");

            return totalBytes;
        }

        int FindSmallFloatOutput(Worker worker, int outputCount)
        {
            var bestIndex = -1;
            var bestLength = int.MaxValue;

            for (var i = 0; i < outputCount; i++)
            {
                var output = worker.PeekOutput(i);
                if (output.dataType != DataType.Float)
                    continue;

                var length = output.shape.length;
                if (length > 0 && length <= maxPerformanceOutputElements && length < bestLength)
                {
                    bestIndex = i;
                    bestLength = length;
                }
            }

            if (bestIndex < 0)
                throw new InvalidOperationException(
                    "No small float output was found for asynchronous completion/readback.");

            return bestIndex;
        }

        static Texture2D CreateSeedTexture(int width, int height)
        {
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "GpuInferenceSpike_Seed",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = (y * width) + x;
                    pixels[i] = new Color32(
                        (byte)(x * 255 / Math.Max(1, width - 1)),
                        (byte)(y * 255 / Math.Max(1, height - 1)),
                        (byte)((x + y) * 255 / Math.Max(1, width + height - 2)),
                        255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        double CurrentFrameMean()
        {
            if (_recentFrameMilliseconds.Count == 0)
                return 0.0;

            double sum = 0.0;
            foreach (var value in _recentFrameMilliseconds)
                sum += value;
            return sum / _recentFrameMilliseconds.Count;
        }

        double CurrentFrameP95()
        {
            if (_recentFrameMilliseconds.Count == 0)
                return 0.0;

            var values = new double[_recentFrameMilliseconds.Count];
            var index = 0;
            foreach (var value in _recentFrameMilliseconds)
                values[index++] = value;
            return GpuInferenceSpikeMath.ComputeStatistics(values).P95Milliseconds;
        }

        static string BuildHardwareSummary()
        {
            return
                $"Unity: {Application.unityVersion}\n" +
                $"OS: {SystemInfo.operatingSystem}\n" +
                $"CPU: {SystemInfo.processorType} ({SystemInfo.processorCount} logical processors)\n" +
                $"GPU: {SystemInfo.graphicsDeviceName}\n" +
                $"Vendor: {SystemInfo.graphicsDeviceVendor}\n" +
                $"Graphics API: {SystemInfo.graphicsDeviceType}\n" +
                $"Graphics memory: {SystemInfo.graphicsMemorySize} MB\n" +
                $"Compute shaders: {SystemInfo.supportsComputeShaders}\n" +
                $"Sentis/InferenceEngine assembly: {typeof(Model).Assembly.GetName().Version}\n" +
                "GPU utilization: use Task Manager > Performance externally during GPU pass.";
        }

        void Fail(string message)
        {
            _stage = Stage.Failed;
            _status = message;
            _resultSummary = message;
            UnityEngine.Debug.LogError("[GpuInferenceSpike] " + message);
        }

        readonly struct OutputSnapshot
        {
            public readonly string Shape;
            public readonly float[] Values;

            public OutputSnapshot(string shape, float[] values)
            {
                Shape = shape;
                Values = values;
            }
        }

        readonly struct Measurement
        {
            public readonly GpuSpikeStatistics Statistics;
            public readonly int CompletionOutputIndex;
            public readonly int OutputBytes;
            public readonly double PreprocessSubmissionMeanMilliseconds;

            public Measurement(
                GpuSpikeStatistics statistics,
                int completionOutputIndex,
                int outputBytes,
                double preprocessSubmissionMeanMilliseconds)
            {
                Statistics = statistics;
                CompletionOutputIndex = completionOutputIndex;
                OutputBytes = outputBytes;
                PreprocessSubmissionMeanMilliseconds = preprocessSubmissionMeanMilliseconds;
            }
        }
    }
}