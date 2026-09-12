using System;
using System.Collections.Generic;
using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using UnityEditor;
using UnityEngine;

namespace GoldenNeedle.Editor
{
    [CustomEditor(typeof(MediaPipePoseProvider))]
    public sealed class MediaPipePoseProviderEditor : UnityEditor.Editor
    {
        private const int BodyInferenceInspectorMinLongEdge = 160;
        private const int BodyInferenceInspectorMaxLongEdge = 640;

        private SerializedProperty _preferredCameraName;
        private SerializedProperty _requestedCameraWidth;
        private SerializedProperty _requestedCameraHeight;
        private SerializedProperty _requestedCameraFps;
        private SerializedProperty _cameraStartupTimeoutSeconds;
        private SerializedProperty _manualRotationOverride;
        private SerializedProperty _mirrorFrontFacingDisplay;
        private SerializedProperty _targetInferenceFps;
        private SerializedProperty _readbackTimeoutSeconds;
        private SerializedProperty _trustSettings;
        private SerializedProperty _enableBodyInferenceDownscale;
        private SerializedProperty _bodyInferenceLongEdge;
        private SerializedProperty _enableImmediateInferenceLaunchAfterReadback;
        private SerializedProperty _enableDirectBodyCpuReadback;

        private void OnEnable()
        {
            _preferredCameraName =
                serializedObject.FindProperty("preferredCameraName");
            _requestedCameraWidth =
                serializedObject.FindProperty("requestedCameraWidth");
            _requestedCameraHeight =
                serializedObject.FindProperty("requestedCameraHeight");
            _requestedCameraFps =
                serializedObject.FindProperty("requestedCameraFps");
            _cameraStartupTimeoutSeconds =
                serializedObject.FindProperty("cameraStartupTimeoutSeconds");
            _manualRotationOverride =
                serializedObject.FindProperty("manualRotationOverride");
            _mirrorFrontFacingDisplay =
                serializedObject.FindProperty("mirrorFrontFacingDisplay");
            _targetInferenceFps =
                serializedObject.FindProperty("targetInferenceFps");
            _readbackTimeoutSeconds =
                serializedObject.FindProperty("readbackTimeoutSeconds");
            _trustSettings =
                serializedObject.FindProperty("trustSettings");
            _enableBodyInferenceDownscale =
                serializedObject.FindProperty("enableBodyInferenceDownscale");
            _bodyInferenceLongEdge =
                serializedObject.FindProperty("bodyInferenceLongEdge");
            _enableImmediateInferenceLaunchAfterReadback =
                serializedObject.FindProperty("enableImmediateInferenceLaunchAfterReadback");
            _enableDirectBodyCpuReadback =
                serializedObject.FindProperty("enableDirectBodyCpuReadback");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var provider = (MediaPipePoseProvider)target;
            EditorGUILayout.LabelField(
                "Camera Input",
                EditorStyles.boldLabel);

            var devices = WebCamTexture.devices;
            var labels = new List<string> { "Automatic" };
            var values = new List<string> { string.Empty };
            for (var i = 0; i < devices.Length; i++)
            {
                labels.Add(devices[i].name);
                values.Add(devices[i].name);
            }

            var currentPreferred =
                _preferredCameraName.stringValue ?? string.Empty;
            var selectedIndex = FindValueIndex(
                values,
                currentPreferred);
            if (selectedIndex < 0 &&
                !string.IsNullOrWhiteSpace(currentPreferred))
            {
                labels.Add(
                    $"{currentPreferred} (not connected)");
                values.Add(currentPreferred);
                selectedIndex = values.Count - 1;
            }

            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            EditorGUI.BeginChangeCheck();
            var nextIndex = EditorGUILayout.Popup(
                "Device",
                selectedIndex,
                labels.ToArray());
            var deviceChanged = EditorGUI.EndChangeCheck();
            if (deviceChanged)
            {
                _preferredCameraName.stringValue =
                    values[nextIndex];
            }

            EditorGUILayout.PropertyField(
                _requestedCameraWidth,
                new GUIContent(
                    "Requested Width",
                    "WebCamTexture request only. Hardware/driver may return another supported size."));
            EditorGUILayout.PropertyField(
                _requestedCameraHeight,
                new GUIContent(
                    "Requested Height",
                    "Use 480 x 640 for a lightweight portrait phone-camera request."));
            EditorGUILayout.PropertyField(
                _requestedCameraFps,
                new GUIContent(
                    "Requested FPS",
                    "Camera capture request. Actual capture FPS is reported in F7."));
            EditorGUILayout.PropertyField(
                _manualRotationOverride,
                new GUIContent(
                    "Orientation",
                    "Auto uses WebCamTexture.videoRotationAngle. Manual quarter-turns override incorrect USB/virtual-camera metadata for both inference and Lab display."));
            EditorGUILayout.PropertyField(
                _mirrorFrontFacingDisplay,
                new GUIContent(
                    "Display Mirror",
                    "Presentation-only horizontal mirror. It never changes MediaPipe inference semantics."));
            EditorGUILayout.PropertyField(
                _cameraStartupTimeoutSeconds,
                new GUIContent("Startup Timeout Seconds"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Pose Landmarker",
                EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _targetInferenceFps,
                new GUIContent(
                    "Target Inference FPS",
                    "Maximum requested cadence. Actual CPU Pose Results/s may be lower; no backlog is created."));
            EditorGUILayout.PropertyField(
                _readbackTimeoutSeconds,
                new GUIContent("Readback Timeout Seconds"));
            EditorGUILayout.PropertyField(
                _trustSettings,
                true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Body Pose Inference",
                EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _enableBodyInferenceDownscale,
                new GUIContent(
                    "Enable Body Inference Downscale",
                    "Downscale only the MediaPipe body-pose input. The full-resolution webcam texture and Motion Engine Lab preview remain unchanged."));

            _bodyInferenceLongEdge.intValue = Mathf.Clamp(
                _bodyInferenceLongEdge.intValue,
                BodyInferenceInspectorMinLongEdge,
                BodyInferenceInspectorMaxLongEdge);
            using (new EditorGUI.DisabledScope(
                       !_enableBodyInferenceDownscale.boolValue))
            {
                _bodyInferenceLongEdge.intValue = EditorGUILayout.IntSlider(
                    new GUIContent(
                        "Body Inference Long Edge",
                        "Target long edge for MediaPipe body-pose input only. The source aspect ratio is preserved; this does not lower the webcam/Lab preview resolution."),
                    _bodyInferenceLongEdge.intValue,
                    BodyInferenceInspectorMinLongEdge,
                    BodyInferenceInspectorMaxLongEdge);
            }

            EditorGUILayout.PropertyField(
                _enableImmediateInferenceLaunchAfterReadback,
                new GUIContent(
                    "Immediate Launch After Readback",
                    "Attempts body-pose inference immediately when readback finishes. If unsafe or ineligible, the frame remains prepared for the normal Update path; no extra queue or concurrent inference is created."));
            EditorGUILayout.PropertyField(
                _enableDirectBodyCpuReadback,
                new GUIContent(
                    "Direct Body CPU Readback",
                    "Experimental body-pose-only path. When eligible it writes GPU readback directly into the pooled TextureFrame CPU buffer, bypassing Homuler staging/copy/Apply. It remains CPU pose inference, automatically falls back to the existing Homuler path when ineligible, and never changes CameraTexture or Lab display."));

            serializedObject.ApplyModifiedProperties();

            if (deviceChanged &&
                Application.isPlaying)
            {
                provider.RequestCameraSwitch(
                    values[nextIndex]);
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    "Runtime Camera",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Selected",
                    string.IsNullOrWhiteSpace(
                        provider.SelectedCameraName)
                        ? "(starting)"
                        : provider.SelectedCameraName);
                EditorGUILayout.LabelField(
                    "Devices",
                    provider.CameraDeviceCount.ToString());
                EditorGUILayout.LabelField(
                    "Actual",
                    $"{provider.ActualCameraWidth}x{provider.ActualCameraHeight} @ {provider.CameraFramesPerSecond:0.0} fps");
                EditorGUILayout.LabelField(
                    "Body Input",
                    $"{provider.BodyInferenceWidth}x{provider.BodyInferenceHeight} {(provider.BodyInferenceUsesScaledTexture ? "scaled" : "native")}");
                EditorGUILayout.LabelField(
                    "Immediate Launch",
                    provider.ImmediateInferenceLaunchAfterReadbackEnabled ? "On" : "Off");
                EditorGUILayout.LabelField(
                    "Readback Path",
                    provider.ActiveBodyReadbackPathLabel);
                if (!string.IsNullOrEmpty(provider.DirectBodyCpuReadbackFallbackReason))
                {
                    EditorGUILayout.LabelField(
                        "Direct Fallback",
                        provider.DirectBodyCpuReadbackFallbackReason);
                }
                EditorGUILayout.LabelField(
                    "Rotation",
                    $"{provider.EffectiveRotationDegrees}° (reported {provider.VideoRotationAngle}°)");
                if (provider.CameraSwitchPending)
                {
                    EditorGUILayout.HelpBox(
                        $"Waiting to switch safely to: {provider.PendingCameraName}",
                        MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Any camera exposed by Windows/Unity as a WebCamDevice can be selected here. A phone needs to appear as a USB/UVC or virtual webcam; Golden Needle does not use a phone-specific network protocol.",
                    MessageType.Info);
            }
        }

        private static int FindValueIndex(
            List<string> values,
            string value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(
                        values[i],
                        value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
