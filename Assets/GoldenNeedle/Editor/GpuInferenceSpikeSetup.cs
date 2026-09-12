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
    public static partial class GpuInferenceSpikeSetup
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

                // Audit the exact bytes before asking Sentis to import either model. This ensures the
                // sparse/DENSIFY report survives even when an importer rejects one model.
                var detectorAudit = TfliteAudit.Read(detectorBytes);
                var landmarkAudit = TfliteAudit.Read(landmarkBytes);

                var detectorImport = ImportModelIndependently(
                    "Detector",
                    DetectorAssetPath,
                    detectorAudit.DensifyOperators.Count > 0);
                var landmarkImport = ImportModelIndependently(
                    "Landmark",
                    LandmarkAssetPath,
                    landmarkAudit.DensifyOperators.Count > 0);

                var auditText = BuildAuditText(
                    bundleBytes,
                    detectorBytes,
                    landmarkBytes,
                    detectorAudit,
                    landmarkAudit,
                    detectorImport,
                    landmarkImport);
                WriteIfChanged(ToAbsoluteAssetPath(AuditAssetPath), Encoding.UTF8.GetBytes(auditText));
                AssetDatabase.ImportAsset(
                    AuditAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                if (detectorImport.Asset == null && landmarkImport.Asset == null)
                {
                    throw new InvalidOperationException(
                        "Neither exact TFLite imported as a Sentis ModelAsset. " +
                        "Detector: " + detectorImport.Display + " Landmark: " + landmarkImport.Display +
                        " The detailed importer summaries and DENSIFY audit were still written to " + AuditAssetPath + ".");
                }

                CreateDiagnosticScene(
                    detectorImport,
                    landmarkImport,
                    detectorAudit,
                    landmarkAudit);

                AssetDatabase.Refresh();

                var suitePlan = BuildSuitePlan(detectorImport, landmarkImport);
                UnityEngine.Debug.Log(
                    "[GpuInferenceSpike] Landmark-first preparation completed without modifying the production bundle.\n" +
                    "Detector: " + detectorImport.Display + "\n" +
                    "Landmark: " + landmarkImport.Display + "\n" +
                    suitePlan + "\n\n" +
                    auditText + "\nDiagnostic scene: " + SceneAssetPath);

                EditorUtility.DisplayDialog(
                    "Golden Needle GPU Inference Spike",
                    "Exact models were extracted and audited independently.\n\n" +
                    "Detector: " + detectorImport.Display + "\n" +
                    "Landmark: " + landmarkImport.Display + "\n\n" +
                    suitePlan + "\n\n" +
                    "Diagnostic scene:\n" + SceneAssetPath + "\n\n" +
                    "Importer failures do not block a benchmark for the model that did import. " +
                    "See Console and Generated/model-audit.txt for full details.",
                    "OK");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[GpuInferenceSpike] Preparation failed.\n" + ex);
                EditorUtility.DisplayDialog(
                    "GPU Inference Spike preparation failed",
                    ex.Message + "\n\nSee Console and Generated/model-audit.txt when present. No ONNX fallback or model rewrite was attempted.",
                    "OK");
            }
        }

        static ImportAttempt ImportModelIndependently(string label, string assetPath, bool modelContainsDensify)
        {
            var capturedErrors = new List<string>();
            Exception thrown = null;
            var fileName = Path.GetFileName(assetPath);

            Application.LogCallback callback = (condition, stackTrace, type) =>
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                    return;

                if (IsRelevantImporterMessage(condition, fileName, label))
                    capturedErrors.Add(CompactImporterMessage(condition));
            };

            Application.logMessageReceived += callback;
            try
            {
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            catch (Exception ex)
            {
                thrown = ex;
                capturedErrors.Add(CompactImporterMessage(ex.GetBaseException().Message));
            }
            finally
            {
                Application.logMessageReceived -= callback;
            }

            var asset = AssetDatabase.LoadAssetAtPath<ModelAsset>(assetPath);
            if (asset != null && thrown == null)
            {
                var detail = capturedErrors.Count == 0
                    ? "Sentis produced a ModelAsset."
                    : "Sentis produced a ModelAsset; importer also logged: " + JoinDistinct(capturedErrors);
                return new ImportAttempt(label, asset, "IMPORTED", detail);
            }

            var errors = JoinDistinct(capturedErrors);
            var densifyObserved = errors.IndexOf("DENSIFY", StringComparison.OrdinalIgnoreCase) >= 0;
            var status = densifyObserved
                ? "FAILED IMPORT (DENSIFY)"
                : modelContainsDensify
                    ? "FAILED IMPORT (model contains DENSIFY; inspect importer error)"
                    : "FAILED IMPORT";

            if (string.IsNullOrWhiteSpace(errors))
            {
                errors = modelContainsDensify
                    ? "No ModelAsset was produced. The exact model audit contains DENSIFY; inspect the Unity Console for the Sentis importer error."
                    : "No ModelAsset was produced. Inspect the Unity Console for the Sentis importer error.";
            }

            return new ImportAttempt(label, null, status, errors);
        }

        static bool IsRelevantImporterMessage(string condition, string fileName, string label)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return false;

            return condition.IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   condition.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   condition.IndexOf("LiteRT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   condition.IndexOf("TFLite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   condition.IndexOf("DENSIFY", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   condition.IndexOf("unsupported operator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   condition.IndexOf("unsupported data", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string CompactImporterMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return string.Empty;

            var oneLine = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
            const int maxLength = 1200;
            return oneLine.Length <= maxLength ? oneLine : oneLine.Substring(0, maxLength) + "…";
        }

        static string JoinDistinct(List<string> messages)
        {
            var distinct = messages
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return distinct.Length == 0 ? string.Empty : string.Join(" | ", distinct);
        }

        static string BuildSuitePlan(ImportAttempt detector, ImportAttempt landmark)
        {
            if (detector.Asset != null && landmark.Asset != null)
                return "Suites: Detector + Landmark will run.";
            if (landmark.Asset != null)
                return "Suites: LANDMARK ONLY will run; detector will be SKIPPED because import failed.";
            if (detector.Asset != null)
                return "Suites: DETECTOR ONLY will run; landmark will be SKIPPED because import failed.";
            return "Suites: none; neither model imported.";
        }

        static void CreateDiagnosticScene(
            ImportAttempt detectorImport,
            ImportAttempt landmarkImport,
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
                serialized.FindProperty("detectorModelAsset").objectReferenceValue = detectorImport.Asset;
                serialized.FindProperty("landmarkModelAsset").objectReferenceValue = landmarkImport.Asset;
                serialized.FindProperty("detectorImportStatus").stringValue = detectorImport.Status;
                serialized.FindProperty("detectorImportDetails").stringValue = detectorImport.Details;
                serialized.FindProperty("landmarkImportStatus").stringValue = landmarkImport.Status;
                serialized.FindProperty("landmarkImportDetails").stringValue = landmarkImport.Details;

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
            TfliteAudit landmarkAudit,
            ImportAttempt detectorImport,
            ImportAttempt landmarkImport)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Golden Needle GPU inference spike — exact model audit");
            builder.AppendLine("Generated locally from the production MediaPipe .task/.bytes bundle.");
            builder.AppendLine("No model conversion or mutation was performed.");
            builder.AppendLine();
            builder.AppendLine(
                $"bundle: {BundleAssetPath} size={bundle.LongLength} sha256={Sha256(bundle)}");
            builder.AppendLine(
                $"pose_detector.tflite size={detector.LongLength} sha256={Sha256(detector)}");
            builder.AppendLine(
                $"pose_landmarks_detector.tflite size={landmark.LongLength} sha256={Sha256(landmark)}");
            builder.AppendLine();
            builder.AppendLine("Sentis import status:");
            builder.AppendLine("  Detector: " + detectorImport.Display);
            builder.AppendLine("    " + detectorImport.Details);
            builder.AppendLine("  Landmark: " + landmarkImport.Display);
            builder.AppendLine("    " + landmarkImport.Details);
            builder.AppendLine("  " + BuildSuitePlan(detectorImport, landmarkImport));
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

        sealed class ImportAttempt
        {
            public readonly string Label;
            public readonly ModelAsset Asset;
            public readonly string Status;
            public readonly string Details;

            public ImportAttempt(string label, ModelAsset asset, string status, string details)
            {
                Label = label;
                Asset = asset;
                Status = status;
                Details = string.IsNullOrWhiteSpace(details) ? "(no importer detail captured)" : details.Trim();
            }

            public string Display => Status;
        }

    }
}
#endif
