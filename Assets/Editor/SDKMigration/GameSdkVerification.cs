using System;
using System.IO;
using System.Linq;
using HDMotionEngine;
using GoldenNeedle.Gameplay.Player;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameSdkVerification
{
    public static void Verify()
    {
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.hd.motion-engine");
        if (package == null || package.version != "0.1.0-preview.2") throw new Exception("Wrong SDK version");
        if (Directory.GetFiles(package.resolvedPath, "*.cs", SearchOption.AllDirectories).Length != 0) throw new Exception("SDK contains implementation source");
        if (Directory.Exists("Assets/GoldenNeedle/Core/Motion") || Directory.Exists("Packages/com.github.homuler.mediapipe") || Directory.Exists("Tools/OpenVinoUnityPosePlugin")) throw new Exception("Private implementation remains in current product tree");
        var root = PrefabUtility.LoadPrefabContents("Assets/GoldenNeedle/Gameplay/Player/GoldenNeedlePlayer.prefab");
        var facade = root.GetComponent<GoldenNeedlePlayerFacade>();
        var engine = root.GetComponent<MotionEngineController>();
        if (facade == null || engine == null || facade.MotionEngine != engine) throw new Exception("Product facade is not bound to SDK");
        if (root.GetComponentsInChildren<MotionEngineController>(true).Length != 1) throw new Exception("Duplicate engine host");
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) throw new Exception("Player prefab has a missing script");
            if (component.GetType().FullName.StartsWith("GoldenNeedle.Core.Motion.") && component.GetType().Assembly != typeof(MotionEngineController).Assembly) throw new Exception("Player uses source component: " + component.GetType().FullName);
        }
        foreach (var name in new[] { "poseProvider", "motionRuntime", "rigBinding", "retargeter", "locomotion" })
            if (new SerializedObject(engine).FindProperty(name).objectReferenceValue == null) throw new Exception("Engine stack reference missing: " + name);
        PrefabUtility.UnloadPrefabContents(root);
        HDMotionEngine.Editor.SdkRuntimeAssetsInstaller.Install();
        HDMotionEngine.Editor.SdkRuntimeAssetsInstaller.Install();
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            if (scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) != 0) throw new Exception("Enabled scene has missing scripts: " + entry.path);
        }
        using (var native = GoldenNeedle.Core.Motion.Providers.MediaPipe.OpenVinoPoseRuntime.Create(Path.Combine(Application.streamingAssetsPath, "GoldenNeedle/OpenVinoPoseModels/pose_detector.tflite"), Path.Combine(Application.streamingAssetsPath, "GoldenNeedle/OpenVinoPoseModels/pose_landmarks_detector.tflite")))
        {
            var frame = native.ProcessRgba(new byte[320 * 240 * 4], 320, 240, 320 * 4, 0, 1);
            if (frame.TimestampMillisec != 1 || native.BackendLabel != "OPENVINO_CPU_FP32") throw new Exception("OpenVINO identity/inference failed");
            File.WriteAllText("Docs/motion-sdk-native-result.json", JsonUtility.ToJson(new NativeResult { backend = native.BackendLabel, timestamp = frame.TimestampMillisec, hasPose = frame.HasPose, passed = true }, true));
        }
        Debug.Log("HD_MOTION_GAME_SDK_VERIFY=PASS");
    }
    public static void BuildWindows()
    {
        Verify();
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64)) throw new Exception("Windows x64 build support unavailable in this Editor installation");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = "Builds/SDKIntegration/GoldenNeedle.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
        File.WriteAllText("Docs/motion-sdk-windows-build-result.json", JsonUtility.ToJson(new PlayerResult { result = report.summary.result.ToString(), errors = report.summary.totalErrors, warnings = report.summary.totalWarnings, bytes = report.summary.totalSize }, true));
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows Player build failed");
        var managed = Directory.GetFiles("Builds/SDKIntegration", "*.dll", SearchOption.AllDirectories);
        if (managed.Any(p => p.EndsWith("HDMotionEngine.Editor.dll") || GoldenNeedle.Editor.Gameplay.ExcludeDevelopmentToolingFromPlayer.IsDevelopmentTool(p) || p.EndsWith("HDMotionLab.dll") || p.Contains("TestRunner"))) throw new Exception("Development-only assembly in Player");
        foreach (var name in new[] { "HDMotionEngine.Runtime.dll", "Mediapipe.Runtime.dll", "golden_needle_openvino_pose.dll", "openvino.dll", "openvino_intel_cpu_plugin.dll", "mediapipe_c.dll" })
            if (!managed.Any(p => Path.GetFileName(p).Equals(name, StringComparison.OrdinalIgnoreCase))) throw new Exception("Required SDK DLL missing in Player: " + name);
        Debug.Log("HD_MOTION_GAME_WINDOWS_BUILD=PASS");
    }
    [Serializable] class NativeResult { public bool passed; public string backend; public long timestamp; public bool hasPose; }
    [Serializable] class PlayerResult { public string result; public int errors; public int warnings; public ulong bytes; }
}
