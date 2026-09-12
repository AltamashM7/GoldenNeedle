#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GoldenNeedle.Debugging.GpuInferenceSpike;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldenNeedle.EditorTools
{
    public static class GpuInferenceSpikeSetup
    {
        const string BundleAssetPath =
            "Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes";
        const string GeneratedRoot =
            "Assets/GoldenNeedle/Debug/GpuInferenceSpike/Generated";
        const string DetectorAssetPath = GeneratedRoot + "/pose_detector.tflite";
        const string LandmarkAssetPath = GeneratedRoot + "/pose_landmarks_detector.tflite";
        const string AuditAssetPath = GeneratedRoot + "/model-audit.txt";
        const string SceneAssetPath = GeneratedRoot + "/GpuInferenceSpike.unity";

        [MenuItem("Golden Needle/GPU Inference Spike/Prepare Exact Models + Scene")]
        public static void PrepareExactModelsAndScene()
        {
            try
            {
                EnsureGeneratedDirectory();

                var bundleAbsolute = ToAbsoluteAssetPath(BundleAssetPath);
                if (!File.Exists(bundleAbsolute))
                    throw new FileNotFoundException("Production pose landmarker bundle was not found.", bundleAbsolute);

                var bundleBytes = File.ReadAllBytes(bundleAbsolute);
                var detectorBytes = ReadZipEntry(bundleBytes, "pose_detector.tflite");
                var landmarkBytes = ReadZipEntry(bundleBytes, "pose_landmarks_detector.tflite");

                WriteIfChanged(ToAbsoluteAssetPath(DetectorAssetPath), detectorBytes);
                WriteIfChanged(ToAbsoluteAssetPath(LandmarkAssetPath), landmarkBytes);

                var detectorAudit = TfliteAudit.Read(detectorBytes);
                var landmarkAudit = TfliteAudit.Read(landmarkBytes);
                var auditText = BuildAuditText(
                    bundleBytes,
                    detectorBytes,
                    landmarkBytes,
                    detectorAudit,
                    landmarkAudit);
                WriteIfChanged(ToAbsoluteAssetPath(AuditAssetPath), Encoding.UTF8.GetBytes(auditText));

                AssetDatabase.ImportAsset(
                    DetectorAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(
                    LandmarkAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(
                    AuditAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var detectorModel = AssetDatabase.LoadAssetAtPath<ModelAsset>(DetectorAssetPath);
                var landmarkModel = AssetDatabase.LoadAssetAtPath<ModelAsset>(LandmarkAssetPath);
                if (detectorModel == null || landmarkModel == null)
                {
                    throw new InvalidOperationException(
                        "Sentis did not import one or both exact .tflite files as ModelAsset. " +
                        "Check the Console/importer errors. Do not convert to ONNX for this spike.");
                }

                CreateDiagnosticScene(
                    detectorModel,
                    landmarkModel,
                    detectorAudit,
                    landmarkAudit);

                AssetDatabase.Refresh();
                Debug.Log(
                    "[GpuInferenceSpike] Exact TFLite models extracted/imported without modifying the production bundle.\n" +
                    auditText + "\nDiagnostic scene: " + SceneAssetPath);
                EditorUtility.DisplayDialog(
                    "Golden Needle GPU Inference Spike",
                    "Exact detector and landmark TFLite models were extracted and imported.\n\n" +
                    "A separate diagnostic scene was generated at:\n" + SceneAssetPath + "\n\n" +
                    "Double-click that scene when you are ready to benchmark. The current scene was not replaced.",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[GpuInferenceSpike] Preparation failed.\n" + ex);
                EditorUtility.DisplayDialog(
                    "GPU Inference Spike preparation failed",
                    ex.Message + "\n\nSee Console for details. No ONNX fallback was attempted.",
                    "OK");
            }
        }

        static void CreateDiagnosticScene(
            ModelAsset detectorModel,
            ModelAsset landmarkModel,
            TfliteAudit detectorAudit,
            TfliteAudit landmarkAudit)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            try
            {
                var runnerObject = new GameObject("GpuInferenceSpikeRunner");
                SceneManager.MoveGameObjectToScene(runnerObject, scene);
                var runner = runnerObject.AddComponent<GpuInferenceSpikeRunner>();

                var serialized = new SerializedObject(runner);
                serialized.FindProperty("detectorModelAsset").objectReferenceValue = detectorModel;
                serialized.FindProperty("landmarkModelAsset").objectReferenceValue = landmarkModel;

                ApplyInputShape(serialized, "detector", detectorAudit);
                ApplyInputShape(serialized, "landmark", landmarkAudit);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var cameraObject = new GameObject("DiagnosticCamera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.04f, 0.05f, 1f);
                camera.cullingMask = 0;

                if (!EditorSceneManager.SaveScene(scene, SceneAssetPath))
                    throw new IOException("Unity failed to save the isolated GPU spike scene.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void ApplyInputShape(
            SerializedObject serialized,
            string role,
            TfliteAudit audit)
        {
            if (audit.Inputs.Count != 1)
                throw new InvalidDataException(role + " model must have exactly one TFLite input for this spike.");

            var input = audit.Inputs[0];
            if (input.Shape.Length != 4 ||
                input.Shape[0] != 1 ||
                input.Shape[3] != 3)
            {
                throw new InvalidDataException(
                    role + " input is not expected NHWC [1,H,W,3]: [" +
                    string.Join(",", input.Shape) + "].");
            }

            serialized.FindProperty(role + "Width").intValue = input.Shape[2];
            serialized.FindProperty(role + "Height").intValue = input.Shape[1];
            serialized.FindProperty("inputChannels").intValue = input.Shape[3];
        }

        static string BuildAuditText(
            byte[] bundle,
            byte[] detector,
            byte[] landmark,
            TfliteAudit detectorAudit,
            TfliteAudit landmarkAudit)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Golden Needle GPU inference spike — exact model audit");
            builder.AppendLine("Generated locally from the production MediaPipe .task/.bytes bundle.");
            builder.AppendLine("No model conversion was performed.");
            builder.AppendLine();
            builder.AppendLine(
                $"bundle: {BundleAssetPath} size={bundle.LongLength} sha256={Sha256(bundle)}");
            builder.AppendLine(
                $"pose_detector.tflite size={detector.LongLength} sha256={Sha256(detector)}");
            builder.AppendLine(
                $"pose_landmarks_detector.tflite size={landmark.LongLength} sha256={Sha256(landmark)}");
            builder.AppendLine();
            builder.Append(detectorAudit.ToReport("pose_detector.tflite"));
            builder.AppendLine();
            builder.Append(landmarkAudit.ToReport("pose_landmarks_detector.tflite"));
            return builder.ToString();
        }

        static byte[] ReadZipEntry(byte[] bundleBytes, string expectedFileName)
        {
            using var stream = new MemoryStream(bundleBytes, false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, false);
            var matches = archive.Entries
                .Where(entry =>
                    string.Equals(
                        Path.GetFileName(entry.FullName),
                        expectedFileName,
                        StringComparison.Ordinal))
                .ToArray();

            if (matches.Length != 1)
                throw new InvalidDataException(
                    $"Expected exactly one {expectedFileName} entry in bundle; found {matches.Length}.");

            using var entryStream = matches[0].Open();
            using var output = new MemoryStream();
            entryStream.CopyTo(output);
            return output.ToArray();
        }

        static void EnsureGeneratedDirectory()
        {
            var absolute = ToAbsoluteAssetPath(GeneratedRoot);
            Directory.CreateDirectory(absolute);
        }

        static string ToAbsoluteAssetPath(string assetPath)
        {
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Expected Assets-relative path.", nameof(assetPath));

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Could not resolve Unity project root.");

            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static void WriteIfChanged(string path, byte[] bytes)
        {
            if (File.Exists(path))
            {
                var existing = File.ReadAllBytes(path);
                if (existing.SequenceEqual(bytes))
                    return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());
            File.WriteAllBytes(path, bytes);
        }

        static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        sealed class TfliteAudit
        {
            public readonly List<TensorInfo> Inputs = new List<TensorInfo>();
            public readonly List<TensorInfo> Outputs = new List<TensorInfo>();
            public readonly SortedSet<string> OperatorSet = new SortedSet<string>(StringComparer.Ordinal);
            public int TensorCount;
            public int QuantizedTensorCount;

            public static TfliteAudit Read(byte[] data)
            {
                var reader = new FlatBufferReader(data);
                var model = reader.RootTable();
                var operatorCodesVector = reader.Vector(model, 1);
                var subgraphsVector = reader.Vector(model, 2);
                if (subgraphsVector.Count == 0)
                    throw new InvalidDataException("TFLite model contains no subgraphs.");

                var audit = new TfliteAudit();
                var subgraph = reader.VectorTable(subgraphsVector, 0);
                var tensors = reader.Vector(subgraph, 0);
                var inputs = reader.Vector(subgraph, 1);
                var outputs = reader.Vector(subgraph, 2);
                var operators = reader.Vector(subgraph, 3);

                audit.TensorCount = tensors.Count;
                for (var i = 0; i < tensors.Count; i++)
                {
                    var tensor = reader.VectorTable(tensors, i);
                    if (reader.TableOffset(tensor, 4) != 0)
                    {
                        var quant = reader.Table(tensor, 4);
                        var scales = reader.Vector(quant, 2);
                        if (scales.Count > 0)
                            audit.QuantizedTensorCount++;
                    }
                }

                for (var i = 0; i < inputs.Count; i++)
                    audit.Inputs.Add(ReadTensorInfo(reader, tensors, reader.VectorInt(inputs, i)));
                for (var i = 0; i < outputs.Count; i++)
                    audit.Outputs.Add(ReadTensorInfo(reader, tensors, reader.VectorInt(outputs, i)));

                for (var i = 0; i < operators.Count; i++)
                {
                    var op = reader.VectorTable(operators, i);
                    var opcodeIndex = (int)reader.UInt(op, 0, 0);
                    if (opcodeIndex < 0 || opcodeIndex >= operatorCodesVector.Count)
                        throw new InvalidDataException("TFLite operator references invalid opcode index.");

                    var opcode = reader.VectorTable(operatorCodesVector, opcodeIndex);
                    var deprecated = reader.Byte(opcode, 0, 0);
                    var builtin = reader.Int(opcode, 3, deprecated);
                    var custom = reader.String(opcode, 1);
                    var version = reader.Int(opcode, 2, 1);
                    var name = builtin == 32 && !string.IsNullOrEmpty(custom)
                        ? "CUSTOM:" + custom
                        : BuiltinOperatorName(builtin);
                    audit.OperatorSet.Add(name + "@v" + version);
                }

                return audit;
            }

            static TensorInfo ReadTensorInfo(
                FlatBufferReader reader,
                FlatBufferReader.VectorRef tensors,
                int tensorIndex)
            {
                if (tensorIndex < 0 || tensorIndex >= tensors.Count)
                    throw new InvalidDataException("TFLite input/output tensor index is invalid.");

                var tensor = reader.VectorTable(tensors, tensorIndex);
                var shapeVector = reader.Vector(tensor, 0);
                var shape = new int[shapeVector.Count];
                for (var i = 0; i < shape.Length; i++)
                    shape[i] = reader.VectorInt(shapeVector, i);

                return new TensorInfo(
                    tensorIndex,
                    reader.String(tensor, 3) ?? string.Empty,
                    TensorTypeName(reader.Byte(tensor, 1, 0)),
                    shape);
            }

            public string ToReport(string label)
            {
                var builder = new StringBuilder();
                builder.AppendLine(label + ":");
                builder.AppendLine(
                    $"  tensors={TensorCount} quantizedTensors={QuantizedTensorCount} " +
                    $"quantization={(QuantizedTensorCount == 0 ? "none detected" : "present")}");

                for (var i = 0; i < Inputs.Count; i++)
                    builder.AppendLine("  input[" + i + "] " + Inputs[i]);
                for (var i = 0; i < Outputs.Count; i++)
                    builder.AppendLine("  output[" + i + "] " + Outputs[i]);

                builder.AppendLine("  TFLite operator set:");
                foreach (var op in OperatorSet)
                    builder.AppendLine("    " + op);
                return builder.ToString();
            }

            static string TensorTypeName(int value)
            {
                switch (value)
                {
                    case 0: return "FLOAT32";
                    case 1: return "FLOAT16";
                    case 2: return "INT32";
                    case 3: return "UINT8";
                    case 4: return "INT64";
                    case 5: return "STRING";
                    case 6: return "BOOL";
                    case 7: return "INT16";
                    case 8: return "COMPLEX64";
                    case 9: return "INT8";
                    case 10: return "FLOAT64";
                    case 11: return "COMPLEX128";
                    case 12: return "UINT64";
                    case 13: return "RESOURCE";
                    case 14: return "VARIANT";
                    case 15: return "UINT32";
                    case 16: return "UINT16";
                    case 17: return "INT4";
                    case 18: return "BFLOAT16";
                    default: return "TENSOR_TYPE_" + value;
                }
            }

            static string BuiltinOperatorName(int value)
            {
                switch (value)
                {
                    case 0: return "ADD";
                    case 1: return "AVERAGE_POOL_2D";
                    case 2: return "CONCATENATION";
                    case 3: return "CONV_2D";
                    case 4: return "DEPTHWISE_CONV_2D";
                    case 6: return "DEQUANTIZE";
                    case 9: return "FULLY_CONNECTED";
                    case 14: return "LOGISTIC";
                    case 17: return "MAX_POOL_2D";
                    case 18: return "MUL";
                    case 19: return "RELU";
                    case 21: return "RELU6";
                    case 22: return "RESHAPE";
                    case 23: return "RESIZE_BILINEAR";
                    case 25: return "SOFTMAX";
                    case 28: return "TANH";
                    case 32: return "CUSTOM";
                    case 34: return "PAD";
                    case 36: return "GATHER";
                    case 39: return "TRANSPOSE";
                    case 40: return "MEAN";
                    case 41: return "SUB";
                    case 42: return "DIV";
                    case 43: return "SQUEEZE";
                    case 45: return "STRIDED_SLICE";
                    case 47: return "EXP";
                    case 49: return "SPLIT";
                    case 53: return "CAST";
                    case 54: return "PRELU";
                    case 55: return "MAXIMUM";
                    case 57: return "MINIMUM";
                    case 59: return "NEG";
                    case 60: return "PADV2";
                    case 65: return "SLICE";
                    case 67: return "TRANSPOSE_CONV";
                    case 69: return "TILE";
                    case 70: return "EXPAND_DIMS";
                    case 74: return "SUM";
                    case 75: return "SQRT";
                    case 76: return "RSQRT";
                    case 77: return "SHAPE";
                    case 78: return "POW";
                    case 83: return "PACK";
                    case 88: return "UNPACK";
                    case 92: return "SQUARE";
                    case 94: return "FILL";
                    case 97: return "RESIZE_NEAREST_NEIGHBOR";
                    case 98: return "LEAKY_RELU";
                    case 101: return "ABS";
                    case 106: return "ADD_N";
                    case 114: return "QUANTIZE";
                    case 117: return "HARD_SWISH";
                    case 126: return "BATCH_MATMUL";
                    default: return "BUILTIN_" + value;
                }
            }
        }

        readonly struct TensorInfo
        {
            public readonly int Index;
            public readonly string Name;
            public readonly string Type;
            public readonly int[] Shape;

            public TensorInfo(int index, string name, string type, int[] shape)
            {
                Index = index;
                Name = name;
                Type = type;
                Shape = shape;
            }

            public override string ToString()
            {
                return $"tensor={Index} name=\"{Name}\" type={Type} shape=[{string.Join(",", Shape)}]";
            }
        }

        sealed class FlatBufferReader
        {
            readonly byte[] _data;

            public FlatBufferReader(byte[] data)
            {
                _data = data ?? throw new ArgumentNullException(nameof(data));
                if (_data.Length < 8)
                    throw new InvalidDataException("TFLite flatbuffer is too small.");
            }

            public int RootTable()
            {
                var root = checked((int)ReadUInt32(0));
                Require(root, 4);
                return root;
            }

            public int TableOffset(int table, int fieldIndex)
            {
                Require(table, 4);
                var vtableDistance = ReadInt32(table);
                var vtable = table - vtableDistance;
                Require(vtable, 4);
                var vtableLength = ReadUInt16(vtable);
                var entry = vtable + 4 + (fieldIndex * 2);
                if (entry + 2 > vtable + vtableLength)
                    return 0;
                return ReadUInt16(entry);
            }

            public int Table(int table, int fieldIndex)
            {
                var offset = TableOffset(table, fieldIndex);
                if (offset == 0)
                    return 0;
                var location = table + offset;
                return location + checked((int)ReadUInt32(location));
            }

            public VectorRef Vector(int table, int fieldIndex)
            {
                var offset = TableOffset(table, fieldIndex);
                if (offset == 0)
                    return default;

                var location = table + offset;
                var vector = location + checked((int)ReadUInt32(location));
                var count = ReadInt32(vector);
                if (count < 0)
                    throw new InvalidDataException("Negative FlatBuffer vector length.");
                Require(vector + 4, count == 0 ? 0 : 1);
                return new VectorRef(vector + 4, count);
            }

            public int VectorTable(VectorRef vector, int index)
            {
                CheckVectorIndex(vector, index);
                var element = vector.Data + (index * 4);
                return element + checked((int)ReadUInt32(element));
            }

            public int VectorInt(VectorRef vector, int index)
            {
                CheckVectorIndex(vector, index);
                return ReadInt32(vector.Data + (index * 4));
            }

            public byte Byte(int table, int fieldIndex, byte defaultValue)
            {
                var offset = TableOffset(table, fieldIndex);
                return offset == 0 ? defaultValue : ReadByte(table + offset);
            }

            public int Int(int table, int fieldIndex, int defaultValue)
            {
                var offset = TableOffset(table, fieldIndex);
                return offset == 0 ? defaultValue : ReadInt32(table + offset);
            }

            public uint UInt(int table, int fieldIndex, uint defaultValue)
            {
                var offset = TableOffset(table, fieldIndex);
                return offset == 0 ? defaultValue : ReadUInt32(table + offset);
            }

            public string String(int table, int fieldIndex)
            {
                var offset = TableOffset(table, fieldIndex);
                if (offset == 0)
                    return null;

                var location = table + offset;
                var target = location + checked((int)ReadUInt32(location));
                var length = ReadInt32(target);
                if (length < 0)
                    throw new InvalidDataException("Negative FlatBuffer string length.");
                Require(target + 4, length);
                return Encoding.UTF8.GetString(_data, target + 4, length);
            }

            byte ReadByte(int offset)
            {
                Require(offset, 1);
                return _data[offset];
            }

            ushort ReadUInt16(int offset)
            {
                Require(offset, 2);
                return BitConverter.ToUInt16(_data, offset);
            }

            int ReadInt32(int offset)
            {
                Require(offset, 4);
                return BitConverter.ToInt32(_data, offset);
            }

            uint ReadUInt32(int offset)
            {
                Require(offset, 4);
                return BitConverter.ToUInt32(_data, offset);
            }

            void CheckVectorIndex(VectorRef vector, int index)
            {
                if (index < 0 || index >= vector.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
            }

            void Require(int offset, int count)
            {
                if (offset < 0 || count < 0 || offset > _data.Length - count)
                    throw new InvalidDataException("FlatBuffer offset is outside the model bytes.");
            }

            public readonly struct VectorRef
            {
                public readonly int Data;
                public readonly int Count;

                public VectorRef(int data, int count)
                {
                    Data = data;
                    Count = count;
                }
            }
        }
    }
}
#endif