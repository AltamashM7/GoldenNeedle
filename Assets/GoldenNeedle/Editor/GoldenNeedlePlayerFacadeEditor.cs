using GoldenNeedle.Gameplay.Player;
using UnityEditor;
using UnityEngine;

namespace GoldenNeedle.Editor.Gameplay
{
    [CustomEditor(typeof(GoldenNeedlePlayerFacade))]
    public sealed class GoldenNeedlePlayerFacadeEditor : UnityEditor.Editor
    {
        private const float TestDamageAmount = 10f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gameplay Foundation Manual QA", EditorStyles.boldLabel);

            var facade = (GoldenNeedlePlayerFacade)target;
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Runtime controls and diagnostics are available in Play Mode. The inspector does not change production configuration in Edit Mode.",
                    MessageType.Info);
                return;
            }

            DrawRuntimeControls(facade);
            EditorGUILayout.Space();
            DrawRuntimeDiagnostics(facade);
            Repaint();
        }

        private static void DrawRuntimeControls(GoldenNeedlePlayerFacade facade)
        {
            EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);

            if (GUILayout.Button("Begin Calibration"))
            {
                facade.BeginCalibration();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Recenter"))
                {
                    facade.Recenter();
                }

                if (GUILayout.Button("Retry Tracking"))
                {
                    facade.RetryTracking();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Enable Motion Control"))
                {
                    facade.SetMotionControlEnabled(true);
                }

                if (GUILayout.Button("Disable Motion Control"))
                {
                    facade.SetMotionControlEnabled(false);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Raw Presentation"))
                {
                    facade.SetAvatarDriveMode(GoldenNeedleAvatarDriveMode.RawCanonical);
                }

                if (GUILayout.Button("Stabilized Presentation"))
                {
                    facade.SetAvatarDriveMode(GoldenNeedleAvatarDriveMode.StabilizedCanonical);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(facade.Health == null))
                {
                    if (GUILayout.Button("Restore Health"))
                    {
                        facade.Health.RestoreHealth();
                    }

                    if (GUILayout.Button($"Test Damage (-{TestDamageAmount:0})"))
                    {
                        facade.Health.ApplyDamage(TestDamageAmount);
                    }
                }
            }
        }

        private static void DrawRuntimeDiagnostics(GoldenNeedlePlayerFacade facade)
        {
            EditorGUILayout.LabelField("Read-only Runtime Diagnostics", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Calibration Usable", facade.IsCalibrationUsable);
                EditorGUILayout.Toggle("Calibration Complete", facade.IsCalibrationComplete);
                EditorGUILayout.Toggle("Tracking Available", facade.IsBodyTrackingAvailable);
                EditorGUILayout.Toggle("Motion Control Enabled", facade.IsMotionControlEnabled);
                EditorGUILayout.EnumPopup("Vertical State", facade.VerticalState);
                EditorGUILayout.EnumPopup("Presentation Mode", facade.AvatarDriveMode);
                EditorGUILayout.ObjectField("Player Root", facade.PlayerRoot, typeof(Transform), true);

                var health = facade.Health;
                EditorGUILayout.FloatField("Current Health", health == null ? 0f : health.CurrentHealth);
                EditorGUILayout.FloatField("Maximum Health", health == null ? 0f : health.MaximumHealth);

                EditorGUILayout.ObjectField("Left Wrist Anchor", facade.LeftWristAnchor, typeof(Transform), true);
                EditorGUILayout.ObjectField("Right Wrist Anchor", facade.RightWristAnchor, typeof(Transform), true);
                EditorGUILayout.ObjectField("Left Foot Anchor", facade.LeftFootAnchor, typeof(Transform), true);
                EditorGUILayout.ObjectField("Right Foot Anchor", facade.RightFootAnchor, typeof(Transform), true);
            }
        }
    }
}
