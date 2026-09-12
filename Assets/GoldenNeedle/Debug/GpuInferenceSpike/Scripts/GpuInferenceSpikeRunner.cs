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
        enum Stage { Idle, Warmup, Equivalence, CpuBenchmark, GpuBenchmark, GpuResidentBenchmark, Complete, Failed }

        [Header("Exact extracted LiteRT assets (independently optional)")]
        [SerializeField] ModelAsset detectorModelAsset;
        [SerializeField] ModelAsset landmarkModelAsset;

        [Header("Preparation/import status")]
        [SerializeField] string detectorImportStatus = "UNKNOWN";
        [SerializeField, TextArea] string detectorImportDetails = "Scene predates landmark-first preparation.";
        [SerializeField] string landmarkImportStatus = "UNKNOWN";
        [SerializeField, TextArea] string landmarkImportDetails = "Scene predates landmark-first preparation.";

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
        string _detectorLoadError;
        string _landmarkLoadError;
        readonly Queue<float> _recentFrameMilliseconds = new Queue<float>();
        const int FrameWindow = 240;

        void Awake()
        {
            _hardwareSummary = BuildHardwareSummary();
            _detectorModel = LoadOptional("Detector", detectorModelAsset, out _detectorLoadError);
            _landmarkModel = LoadOptional("Landmark", landmarkModelAsset, out _landmarkLoadError);
            var b = new StringBuilder();
            AppendPlan(b, "Detector", detectorImportStatus, detectorImportDetails, _detectorModel, _detectorLoadError);
            AppendPlan(b, "Landmark", landmarkImportStatus, landmarkImportDetails, _landmarkModel, _landmarkLoadError);
            _modelSummary = b.ToString().TrimEnd();
            if (_detectorModel == null && _landmarkModel == null) _status = "No imported model is available.";
            else if (_landmarkModel != null && _detectorModel == null) _status = "Landmark-only benchmark ready. Detector will be skipped.";
            else if (_detectorModel != null && _landmarkModel == null) _status = "Detector-only benchmark ready. Landmark will be skipped.";
            else _status = "Detector + landmark benchmarks ready.";
        }

        void Update()
        {
            _recentFrameMilliseconds.Enqueue(Time.unscaledDeltaTime * 1000f);
            while (_recentFrameMilliseconds.Count > FrameWindow) _recentFrameMilliseconds.Dequeue();
        }

        void OnGUI()
        {
            var width = Mathf.Min(Screen.width - 24f, 1120f);
            GUILayout.BeginArea(new Rect(12f, 12f, width, Screen.height - 24f), GUI.skin.box);
            GUILayout.Label("<b>Golden Needle — Sentis GPU Inference Spike</b>", RichLabel());
            GUILayout.Label("Stage: " + _stage);
            GUILayout.Label(_hardwareSummary);
            GUILayout.Space(6f);
            GUILayout.Label(_modelSummary);
            GUILayout.Space(6f);
            GUILayout.Label(_status);
            GUI.enabled = !_running && (_detectorModel != null || _landmarkModel != null);
            if (GUILayout.Button(_running ? "Benchmark running…" : "Run benchmark", GUILayout.Height(32f))) RunBenchmark();
            GUI.enabled = true;
            GUILayout.Space(6f);
            GUILayout.TextArea(_resultSummary, GUILayout.ExpandHeight(true));
            GUILayout.EndArea();
        }

        static GUIStyle RichLabel()
        {
            var style = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            return style;
        }

        static Model LoadOptional(string label, ModelAsset asset, out string error)
        {
            error = string.Empty;
            if (asset == null) return null;
            try { return ModelLoader.Load(asset); }
            catch (Exception ex)
            {
                error = label + " ModelAsset load failed: " + ex.GetBaseException().Message;
                UnityEngine.Debug.LogError("[GpuInferenceSpike] " + error);
                return null;
            }
        }

        static void AppendPlan(StringBuilder b, string label, string status, string details, Model model, string loadError)
        {
            b.Append(label).Append(": import=").Append(string.IsNullOrWhiteSpace(status) ? "UNKNOWN" : status.Trim());
            if (!string.IsNullOrWhiteSpace(details)) b.Append(" — ").Append(OneLine(details));
            if (model != null) b.Append("; suite=WILL RUN");
            else if (!string.IsNullOrWhiteSpace(loadError)) b.Append("; suite=SKIPPED (MODEL LOAD FAILED: ").Append(OneLine(loadError)).Append(')');
            else b.Append("; suite=SKIPPED (FAILED IMPORT / no ModelAsset)");
            b.AppendLine();
        }

        async void RunBenchmark()
        {
            if (_running || (_detectorModel == null && _landmarkModel == null)) return;
            _running = true;
            _resultSummary = string.Empty;
            try
            {
                var report = new StringBuilder();
                report.AppendLine("=== HARDWARE ===").AppendLine(_hardwareSummary).AppendLine();
                report.AppendLine("=== IMPORT / SUITE PLAN ===");
                AppendPlan(report, "Detector", detectorImportStatus, detectorImportDetails, _detectorModel, _detectorLoadError);
                AppendPlan(report, "Landmark", landmarkImportStatus, landmarkImportDetails, _landmarkModel, _landmarkLoadError);
                report.AppendLine();
                var detector = await RunAvailable("Detector", _detectorModel, detectorWidth, detectorHeight, report,
                    detectorImportStatus, detectorImportDetails, _detectorLoadError);
                var landmark = await RunAvailable("Landmark", _landmarkModel, landmarkWidth, landmarkHeight, report,
                    landmarkImportStatus, landmarkImportDetails, _landmarkLoadError);
                report.AppendLine("=== SUITE DISPOSITION ===");
                report.AppendLine("Detector: " + detector);
                report.AppendLine("Landmark: " + landmark).AppendLine();
                AppendCaveats(report);
                var failed = detector.StartsWith("FAILED RUNTIME", StringComparison.Ordinal) || landmark.StartsWith("FAILED RUNTIME", StringComparison.Ordinal);
                _stage = failed ? Stage.Failed : Stage.Complete;
                _status = failed ? "Completed with a runtime failure; see report." : "Complete. Architecture benchmark only; production avatar remains untouched.";
                _resultSummary = report.ToString();
                UnityEngine.Debug.Log("[GpuInferenceSpike]\n" + _resultSummary);
            }
            catch (Exception ex) { Fail("Benchmark orchestration failed: " + ex); }
            finally { _running = false; }
        }

        async Task<string> RunAvailable(string label, Model model, int width, int height, StringBuilder report,
            string importStatus, string importDetails, string loadError)
        {
            if (model == null)
            {
                var reason = !string.IsNullOrWhiteSpace(loadError)
                    ? "MODEL LOAD FAILED: " + OneLine(loadError)
                    : "FAILED IMPORT: " + OneLine(importStatus + " " + importDetails);
                report.AppendLine("=== " + label.ToUpperInvariant() + " ===");
                report.AppendLine("SKIPPED — " + reason).AppendLine();
                return "SKIPPED (" + reason + ")";
            }
            try { await RunSuite(label, model, width, height, report); return "RAN"; }
            catch (Exception ex)
            {
                var message = ex.GetBaseException().Message;
                report.AppendLine(label + " runtime suite FAILED: " + message).AppendLine();
                UnityEngine.Debug.LogError("[GpuInferenceSpike] " + label + " suite failed.\n" + ex);
                return "FAILED RUNTIME: " + OneLine(message);
            }
        }

        async Task RunSuite(string label, Model model, int width, int height, StringBuilder report)
        {
            ValidateModel(label, model);
            report.AppendLine("=== " + label.ToUpperInvariant() + " ===");
            report.AppendLine($"Input tensor: [1,{height},{width},{inputChannels}] float NHWC; outputs={model.outputs.Count}; layers={model.layers.Count}");
            _stage = Stage.Equivalence;
            _status = label + ": CPU vs GPUCompute equivalence…";
            report.Append(await RunEquivalence(model, width, height)).AppendLine();

            var values = GpuInferenceSpikeMath.CreateDeterministicInput(width * height * inputChannels, 2, 20260912);
            _stage = Stage.Warmup;
            _status = label + ": CPU warmup…";
            using (var input = new Tensor<float>(new TensorShape(1, height, width, inputChannels), values))
            using (var worker = new Worker(model, BackendType.CPU))
            {
                await WarmUp(worker, input);
                _stage = Stage.CpuBenchmark;
                _status = label + ": CPU benchmark…";
                var m = await MeasureWorker(worker, input, model.outputs.Count);
                report.AppendLine("CPU: " + m.Statistics);
                report.AppendLine($"CPU completion output[{m.CompletionOutputIndex}] bytes/readback={m.OutputBytes}");
            }

            _stage = Stage.Warmup;
            _status = label + ": GPUCompute warmup…";
            using (var input = new Tensor<float>(new TensorShape(1, height, width, inputChannels), values))
            using (var worker = new Worker(model, BackendType.GPUCompute))
            {
                await WarmUp(worker, input);
                _stage = Stage.GpuBenchmark;
                _status = label + ": GPUCompute tensor benchmark…";
                var m = await MeasureWorker(worker, input, model.outputs.Count);
                report.AppendLine("GPUCompute fixed tensor: " + m.Statistics);
                report.AppendLine($"GPU completion output[{m.CompletionOutputIndex}] bytes/readback={m.OutputBytes}");
            }

            _stage = Stage.GpuResidentBenchmark;
            _status = label + ": GPU-resident RenderTexture → tensor → GPUCompute → readback…";
            var resident = await MeasureGpuResident(model, width, height);
            report.AppendLine("GPU-resident path: " + resident.Statistics);
            report.AppendLine($"GPU-resident selected-output bytes/readback={resident.OutputBytes}; TextureConverter submission mean={resident.PreprocessSubmissionMeanMilliseconds:F3} ms");
            report.AppendLine($"Render frame sample during suite: mean={FrameMean():F2} ms p95={FrameP95():F2} ms.").AppendLine();
        }

        static void ValidateModel(string label, Model model)
        {
            if (model.inputs.Count != 1) throw new InvalidOperationException(label + " spike expects exactly one model input.");
            if (model.inputs[0].dataType != DataType.Float) throw new InvalidOperationException(label + " input is not Float.");
            if (model.outputs.Count == 0) throw new InvalidOperationException(label + " model exposes no outputs.");
        }

        async Task<string> RunEquivalence(Model model, int width, int height)
        {
            var b = new StringBuilder("CPU vs GPUCompute equivalence:\n");
            for (var mode = 0; mode < 3; mode++)
            {
                var label = mode == 0 ? "zeros" : mode == 1 ? "gradient" : "seeded-random";
                var values = GpuInferenceSpikeMath.CreateDeterministicInput(width * height * inputChannels, mode, 424242);
                var cpu = await RunAllOutputs(model, BackendType.CPU, width, height, values);
                var gpu = await RunAllOutputs(model, BackendType.GPUCompute, width, height, values);
                if (cpu.Count != gpu.Count) throw new InvalidOperationException("CPU/GPU output count mismatch.");
                b.AppendLine("  " + label + ":");
                for (var i = 0; i < cpu.Count; i++)
                {
                    if (cpu[i].Shape != gpu[i].Shape) throw new InvalidOperationException($"Output {i} shape mismatch CPU={cpu[i].Shape} GPU={gpu[i].Shape}.");
                    var comparison = GpuInferenceSpikeMath.Compare(cpu[i].Values, gpu[i].Values);
                    b.AppendLine($"    output[{i}] shape={cpu[i].Shape} dtype=Float bytes={cpu[i].Values.Length * sizeof(float)} {comparison}");
                }
            }
            return b.ToString();
        }

        static async Task<List<OutputSnapshot>> RunAllOutputs(Model model, BackendType backend, int width, int height, float[] values)
        {
            using var input = new Tensor<float>(new TensorShape(1, height, width, 3), values);
            using var worker = new Worker(model, backend);
            worker.Schedule(input);
            var snapshots = new List<OutputSnapshot>(model.outputs.Count);
            for (var i = 0; i < model.outputs.Count; i++)
            {
                var output = worker.PeekOutput(i);
                if (output.dataType != DataType.Float || !(output is Tensor<float> f))
                    throw new InvalidOperationException($"Output {i} is {output.dataType}; float-only comparator cannot read it.");
                using var clone = await f.ReadbackAndCloneAsync();
                snapshots.Add(new OutputSnapshot(output.shape.ToString(), clone.DownloadToArray()));
            }
            return snapshots;
        }

        async Task WarmUp(Worker worker, Tensor<float> input)
        {
            for (var i = 0; i < warmupIterations; i++)
            {
                worker.Schedule(input);
                if (!(worker.PeekOutput(0) is Tensor<float> output)) throw new InvalidOperationException("Warmup output[0] is not float.");
                using var clone = await output.ReadbackAndCloneAsync();
            }
        }

        async Task<Measurement> MeasureWorker(Worker worker, Tensor<float> input, int outputCount)
        {
            var times = new double[measuredIterations];
            var outputIndex = FindSmallFloatOutput(worker, outputCount);
            var bytes = 0;
            for (var i = 0; i < times.Length; i++)
            {
                var sw = Stopwatch.StartNew();
                worker.Schedule(input);
                if (!(worker.PeekOutput(outputIndex) is Tensor<float> output)) throw new InvalidOperationException("Completion output is not float.");
                bytes = output.shape.length * sizeof(float);
                using var clone = await output.ReadbackAndCloneAsync();
                sw.Stop();
                times[i] = sw.Elapsed.TotalMilliseconds;
                if ((i & 15) == 15) await Task.Yield();
            }
            return new Measurement(GpuInferenceSpikeMath.ComputeStatistics(times), outputIndex, bytes, 0.0);
        }

        async Task<Measurement> MeasureGpuResident(Model model, int width, int height)
        {
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                { name = "GpuInferenceSpike_Input", useMipMap = false, autoGenerateMips = false };
            rt.Create();
            var seed = CreateSeedTexture(width, height);
            Graphics.Blit(seed, rt);
            Destroy(seed);
            var tensor = new Tensor<float>(new TensorShape(1, height, width, inputChannels));
            var worker = new Worker(model, BackendType.GPUCompute);
            var transform = new TextureTransform().SetTensorLayout(TensorLayout.NHWC).SetCoordOrigin(CoordOrigin.TopLeft);
            try
            {
                for (var i = 0; i < warmupIterations; i++)
                {
                    TextureConverter.ToTensor(rt, tensor, transform);
                    worker.Schedule(tensor);
                    await ReadSmallOutputs(worker, model.outputs.Count);
                }
                var total = new double[measuredIterations];
                var preprocess = new double[measuredIterations];
                var bytes = 0;
                for (var i = 0; i < measuredIterations; i++)
                {
                    var all = Stopwatch.StartNew();
                    var pre = Stopwatch.StartNew();
                    TextureConverter.ToTensor(rt, tensor, transform);
                    pre.Stop();
                    preprocess[i] = pre.Elapsed.TotalMilliseconds;
                    worker.Schedule(tensor);
                    bytes = await ReadSmallOutputs(worker, model.outputs.Count);
                    all.Stop();
                    total[i] = all.Elapsed.TotalMilliseconds;
                    if ((i & 15) == 15) await Task.Yield();
                }
                var preStats = GpuInferenceSpikeMath.ComputeStatistics(preprocess);
                return new Measurement(GpuInferenceSpikeMath.ComputeStatistics(total), -1, bytes, preStats.MeanMilliseconds);
            }
            finally
            {
                worker.Dispose(); tensor.Dispose(); rt.Release(); Destroy(rt);
            }
        }

        async Task<int> ReadSmallOutputs(Worker worker, int outputCount)
        {
            var bytes = 0;
            var selected = 0;
            for (var i = 0; i < outputCount; i++)
            {
                var output = worker.PeekOutput(i);
                if (output.dataType != DataType.Float || !(output is Tensor<float> f)) continue;
                var elements = output.shape.length;
                if (elements <= 0 || elements > maxPerformanceOutputElements) continue;
                using var clone = await f.ReadbackAndCloneAsync();
                bytes += elements * sizeof(float);
                selected++;
            }
            if (selected == 0) throw new InvalidOperationException("No small float outputs were eligible for GPU readback.");
            return bytes;
        }

        int FindSmallFloatOutput(Worker worker, int outputCount)
        {
            var best = -1;
            var length = int.MaxValue;
            for (var i = 0; i < outputCount; i++)
            {
                var output = worker.PeekOutput(i);
                if (output.dataType != DataType.Float) continue;
                var n = output.shape.length;
                if (n > 0 && n <= maxPerformanceOutputElements && n < length) { best = i; length = n; }
            }
            if (best < 0) throw new InvalidOperationException("No small float output found for completion/readback.");
            return best;
        }

        static Texture2D CreateSeedTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                { name = "GpuInferenceSpike_Seed", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    pixels[y * width + x] = new Color32(
                        (byte)(x * 255 / Math.Max(1, width - 1)),
                        (byte)(y * 255 / Math.Max(1, height - 1)),
                        (byte)((x + y) * 255 / Math.Max(1, width + height - 2)), 255);
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        double FrameMean()
        {
            if (_recentFrameMilliseconds.Count == 0) return 0;
            double sum = 0; foreach (var value in _recentFrameMilliseconds) sum += value;
            return sum / _recentFrameMilliseconds.Count;
        }

        double FrameP95()
        {
            if (_recentFrameMilliseconds.Count == 0) return 0;
            var values = new double[_recentFrameMilliseconds.Count];
            var i = 0; foreach (var value in _recentFrameMilliseconds) values[i++] = value;
            return GpuInferenceSpikeMath.ComputeStatistics(values).P95Milliseconds;
        }

        static void AppendCaveats(StringBuilder report)
        {
            report.AppendLine("=== INTERPRETATION CAVEATS ===");
            report.AppendLine("1. Sentis CPU vs GPUCompute checks Sentis backend consistency only; it is not independent Google LiteRT equivalence.");
            report.AppendLine("2. GPU-resident timing reads every float output under the element cap and may overestimate final output-transfer cost.");
            report.AppendLine("3. The diagnostic scene has much lighter render load than final gameplay.");
            report.AppendLine("4. Full MediaPipe ROI/tracking/detector cadence/postprocessing is not reconstructed.");
            report.AppendLine("5. Intel HD 620 shares memory/bandwidth/compute resources with Unity rendering.");
            report.AppendLine("6. Raw landmark-network throughput is not full production pose throughput; landmark is the current architecture gate.");
        }

        static string BuildHardwareSummary()
        {
            return $"Unity: {Application.unityVersion}\nOS: {SystemInfo.operatingSystem}\nCPU: {SystemInfo.processorType} ({SystemInfo.processorCount} logical processors)\n" +
                   $"GPU: {SystemInfo.graphicsDeviceName}\nVendor: {SystemInfo.graphicsDeviceVendor}\nGraphics API: {SystemInfo.graphicsDeviceType}\n" +
                   $"Graphics memory: {SystemInfo.graphicsMemorySize} MB\nCompute shaders: {SystemInfo.supportsComputeShaders}\n" +
                   $"Sentis/InferenceEngine assembly: {typeof(Model).Assembly.GetName().Version}\nGPU utilization: use Task Manager externally during GPU pass.";
        }

        static string OneLine(string value) => string.IsNullOrWhiteSpace(value) ? "(no additional detail)" : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        void Fail(string message) { _stage = Stage.Failed; _status = message; _resultSummary = message; UnityEngine.Debug.LogError("[GpuInferenceSpike] " + message); }

        readonly struct OutputSnapshot
        {
            public readonly string Shape; public readonly float[] Values;
            public OutputSnapshot(string shape, float[] values) { Shape = shape; Values = values; }
        }

        readonly struct Measurement
        {
            public readonly GpuSpikeStatistics Statistics;
            public readonly int CompletionOutputIndex;
            public readonly int OutputBytes;
            public readonly double PreprocessSubmissionMeanMilliseconds;
            public Measurement(GpuSpikeStatistics statistics, int completionOutputIndex, int outputBytes, double preprocessSubmissionMeanMilliseconds)
            { Statistics = statistics; CompletionOutputIndex = completionOutputIndex; OutputBytes = outputBytes; PreprocessSubmissionMeanMilliseconds = preprocessSubmissionMeanMilliseconds; }
        }
    }
}
