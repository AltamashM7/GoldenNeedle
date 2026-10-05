using System.IO;
using HDMotionEngine.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GoldenNeedle.Editor.Gameplay
{
    // Product setup only: the compiled SDK owns payload verification and installation.
    public sealed class MotionSdkRuntimeAssetsBootstrap : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        [InitializeOnLoadMethod]
        private static void Initialize() => EditorApplication.delayCall += EnsureModels;
        private static void EnsureModels()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var relative in new[] { "GoldenNeedle/OpenVinoPoseModels/pose_detector.tflite", "GoldenNeedle/OpenVinoPoseModels/pose_landmarks_detector.tflite", "GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes", "GoldenNeedle/PoseTrackingSpike/Models/hand_landmarker.task" })
                if (!File.Exists(Path.Combine(Application.streamingAssetsPath, relative))) { SdkRuntimeAssetsInstaller.Install(); return; }
        }
        public void OnPreprocessBuild(BuildReport report) => SdkRuntimeAssetsInstaller.Install();
    }
}
