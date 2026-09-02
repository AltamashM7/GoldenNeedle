using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using GoldenNeedle.Core.Motion.Stabilization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    [RequireComponent(typeof(MediaPipePoseProvider))]
    public sealed class PoseTrackingSpikePresenter : MonoBehaviour
    {
        private static readonly int[,] RawConnections =
        {
            { 0, 1 }, { 0, 2 }, { 1, 3 }, { 2, 4 }, { 0, 7 }, { 0, 8 },
            { 11, 12 }, { 11, 13 }, { 13, 15 }, { 15, 17 }, { 15, 19 }, { 15, 21 },
            { 12, 14 }, { 14, 16 }, { 16, 18 }, { 16, 20 }, { 16, 22 },
            { 11, 23 }, { 12, 24 }, { 23, 24 }, { 23, 25 }, { 25, 27 }, { 27, 29 }, { 27, 31 },
            { 24, 26 }, { 26, 28 }, { 28, 30 }, { 30, 32 },
        };

        private static readonly CanonicalJointId[,] CanonicalConnections =
        {
            { CanonicalJointId.Pelvis, CanonicalJointId.Spine },
            { CanonicalJointId.Spine, CanonicalJointId.Chest },
            { CanonicalJointId.Chest, CanonicalJointId.Head },
            { CanonicalJointId.Chest, CanonicalJointId.LeftShoulder },
            { CanonicalJointId.LeftShoulder, CanonicalJointId.LeftElbow },
            { CanonicalJointId.LeftElbow, CanonicalJointId.LeftWrist },
            { CanonicalJointId.Chest, CanonicalJointId.RightShoulder },
            { CanonicalJointId.RightShoulder, CanonicalJointId.RightElbow },
            { CanonicalJointId.RightElbow, CanonicalJointId.RightWrist },
            { CanonicalJointId.Pelvis, CanonicalJointId.LeftHip },
            { CanonicalJointId.LeftHip, CanonicalJointId.LeftKnee },
            { CanonicalJointId.LeftKnee, CanonicalJointId.LeftAnkle },
            { CanonicalJointId.LeftAnkle, CanonicalJointId.LeftHeel },
            { CanonicalJointId.LeftAnkle, CanonicalJointId.LeftToe },
            { CanonicalJointId.Pelvis, CanonicalJointId.RightHip },
            { CanonicalJointId.RightHip, CanonicalJointId.RightKnee },
            { CanonicalJointId.RightKnee, CanonicalJointId.RightAnkle },
            { CanonicalJointId.RightAnkle, CanonicalJointId.RightHeel },
            { CanonicalJointId.RightAnkle, CanonicalJointId.RightToe },
        };

        [SerializeField] private MediaPipePoseProvider provider;
        [SerializeField] private bool drawRawLandmarks = true;
        [SerializeField] private bool drawCanonical2D = true;
        [SerializeField] private bool drawCanonical3D = true;
        [SerializeField] private bool drawStabilized2D = true;
        [SerializeField] private bool drawUnavailableLandmarks = true;
        [SerializeField] private float previewPanelWidth = 360f;
        [SerializeField] private CanonicalStabilizerSettings stabilizerSettings = new CanonicalStabilizerSettings();
        [SerializeField] private MotionCalibrationSettings calibrationSettings = new MotionCalibrationSettings();

        private readonly PoseObservation _observation = new PoseObservation();
        private readonly CanonicalPoseFrame _canonicalFrame = new CanonicalPoseFrame();
        private readonly CanonicalPoseFrame _stabilizedFrame = new CanonicalPoseFrame();
        private CanonicalPoseStabilizer _stabilizer;
        private MotionCalibrationSession _calibration;
        private float _renderFps;
        private GUIStyle _labelStyle;
        private GUIStyle _smallLabelStyle;

        private void Awake()
        {
            provider = provider == null ? GetComponent<MediaPipePoseProvider>() : provider;
            _stabilizer = new CanonicalPoseStabilizer(stabilizerSettings);
            _calibration = new MotionCalibrationSession(calibrationSettings);
        }

        private void Update()
        {
            if (provider == null)
            {
                return;
            }

            provider.CopyLatestObservation(_observation);
            MediaPipeCanonicalPoseMapper.Map(_observation, _canonicalFrame, provider.Orientation);
            var evaluationTime = GetProviderEvaluationTime();
            _stabilizer.Stabilize(_canonicalFrame, _stabilizedFrame, evaluationTime);
            _calibration.Update(_stabilizedFrame, evaluationTime);

            var frameTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _renderFps = Mathf.Lerp(_renderFps, 1f / frameTime, 1f - Mathf.Exp(-8f * frameTime));

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                provider.Retry();
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                drawRawLandmarks = !drawRawLandmarks;
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                drawCanonical2D = !drawCanonical2D;
            }

            if (keyboard.f3Key.wasPressedThisFrame)
            {
                drawCanonical3D = !drawCanonical3D;
            }

            if (keyboard.f4Key.wasPressedThisFrame)
            {
                drawStabilized2D = !drawStabilized2D;
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                _calibration.Begin(evaluationTime);
            }

            if (keyboard.xKey.wasPressedThisFrame)
            {
                _calibration.Reset();
            }
        }

        private void OnGUI()
        {
            if (provider == null)
            {
                return;
            }

            EnsureStyles();

            var previewRect = new Rect(0f, 0f, Screen.width, Screen.height);
            var orientation = provider.Orientation;
            var contentRect = GetContentRect(
                previewRect,
                provider.ActualCameraWidth,
                provider.ActualCameraHeight,
                orientation.DisplayRotationDegrees);
            var oldMatrix = GUI.matrix;
            var pivot = previewRect.center;

            // This transform is display-only. It intentionally does not use the texture flips
            // required to convert Unity pixel data into MediaPipe image coordinates.
            GUIUtility.ScaleAroundPivot(
                new Vector2(
                    orientation.DisplayFlipHorizontally ? -1f : 1f,
                    orientation.DisplayFlipVertically ? -1f : 1f),
                pivot);
            GUIUtility.RotateAroundPivot(-orientation.DisplayRotationDegrees, pivot);
            GUI.color = Color.white;
            if (provider.CameraTexture != null)
            {
                GUI.DrawTexture(previewRect, provider.CameraTexture, ScaleMode.ScaleAndCrop, false);
            }
            else
            {
                GUI.color = new Color(0.04f, 0.05f, 0.07f, 1f);
                GUI.DrawTexture(previewRect, Texture2D.whiteTexture);
            }

            if (drawRawLandmarks)
            {
                DrawRawSkeleton(contentRect, orientation);
            }

            if (drawCanonical2D)
            {
                DrawCanonical2DSkeleton(_canonicalFrame, contentRect, false);
            }

            if (drawStabilized2D)
            {
                DrawCanonical2DSkeleton(_stabilizedFrame, contentRect, true);
            }

            GUI.matrix = oldMatrix;
            DrawDiagnostics();
            if (drawCanonical3D)
            {
                DrawCanonical3DView();
            }
        }

        private void EnsureStyles()
        {
            if (_smallLabelStyle != null)
            {
                return;
            }

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = Color.white },
            };
            _smallLabelStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 12,
            };
        }

        private void DrawRawSkeleton(Rect contentRect, CameraOrientationState orientation)
        {
            if (!_observation.hasPose)
            {
                return;
            }

            for (var i = 0; i < RawConnections.GetLength(0); i++)
            {
                var from = _observation.GetLandmark(RawConnections[i, 0]);
                var to = _observation.GetLandmark(RawConnections[i, 1]);
                if (from.IsTracked && to.IsTracked)
                {
                    DrawLine(
                        ToRawScreenPoint(orientation.MediaPipeImageToCameraNormalized(new Vector2(from.x, from.y)), contentRect),
                        ToRawScreenPoint(orientation.MediaPipeImageToCameraNormalized(new Vector2(to.x, to.y)), contentRect),
                        new Color(0.1f, 1f, 0.55f, 0.9f),
                        3f);
                }
            }

            for (var i = 0; i < PoseObservation.LandmarkCount; i++)
            {
                var landmark = _observation.GetLandmark(i);
                if (landmark.IsTracked)
                {
                    DrawPoint(
                        ToRawScreenPoint(orientation.MediaPipeImageToCameraNormalized(new Vector2(landmark.x, landmark.y)), contentRect),
                        new Color(0.2f, 1f, 0.7f, 1f),
                        10f);
                }
                else if (drawUnavailableLandmarks && IsFinite(landmark.x) && IsFinite(landmark.y))
                {
                    DrawPoint(
                        ToRawScreenPoint(orientation.MediaPipeImageToCameraNormalized(new Vector2(landmark.x, landmark.y)), contentRect),
                        new Color(1f, 0.55f, 0.15f, 0.35f),
                        6f);
                }
            }
        }

        private void DrawCanonical2DSkeleton(CanonicalPoseFrame frame, Rect contentRect, bool stabilized)
        {
            if (!frame.hasMeaningfulPose)
            {
                return;
            }

            for (var i = 0; i < CanonicalConnections.GetLength(0); i++)
            {
                var from = frame.GetJoint(CanonicalConnections[i, 0]);
                var to = frame.GetJoint(CanonicalConnections[i, 1]);
                if (from.IsTracked && to.IsTracked && from.hasImagePosition && to.hasImagePosition)
                {
                    var lineColor = stabilized
                        ? new Color(0.15f, 1f, 1f, 0.92f)
                        : new Color(1f, 0.85f, 0.1f, 0.95f);
                    DrawLine(
                        CanonicalCoordinateSystem.CanonicalImageToGuiScreen(from.imagePosition, contentRect),
                        CanonicalCoordinateSystem.CanonicalImageToGuiScreen(to.imagePosition, contentRect),
                        lineColor,
                        stabilized ? 4f : 5f);
                }
            }

            for (var i = 0; i < CanonicalPoseFrame.JointCount; i++)
            {
                var joint = frame.GetJoint((CanonicalJointId)i);
                if (!joint.IsTracked || !joint.hasImagePosition)
                {
                    continue;
                }

                var color = stabilized
                    ? new Color(0.25f, 1f, 1f, 1f)
                    : IsDerivedJoint(joint.id)
                        ? new Color(1f, 0.3f, 0.95f, 1f)
                        : new Color(1f, 0.9f, 0.15f, 1f);
                DrawPoint(CanonicalCoordinateSystem.CanonicalImageToGuiScreen(joint.imagePosition, contentRect), color, stabilized ? 11f : 14f);
            }
        }

        private void DrawDiagnostics()
        {
            var panelHeight = 370f;
            var panel = new Rect(16f, 16f, previewPanelWidth, panelHeight);
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.84f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var state = !_observation.hasPose
                ? provider.Status == PoseProviderStatus.Ready ? "WAITING / UNAVAILABLE" : provider.Status.ToString().ToUpperInvariant()
                : _observation.trustedCount > 0 ? "TRACKING" : "POSE / NO TRUSTED LANDMARKS";
            var cameraName = string.IsNullOrEmpty(provider.SelectedCameraName) ? "(none)" : provider.SelectedCameraName;
            var resolution = provider.ActualCameraWidth > 0 ? $"{provider.ActualCameraWidth}x{provider.ActualCameraHeight}" : "(not started)";
            var age = double.IsInfinity(provider.LatestPoseAgeMilliseconds) ? "-" : $"{provider.LatestPoseAgeMilliseconds:0} ms";
            var text =
                $"POSE TRACKING / CANONICAL SPIKE\n" +
                $"State: {state}\n" +
                $"Camera: {cameraName}\n" +
                $"Capture: {resolution} @ {provider.CameraFramesPerSecond:0.0} fps\n" +
                $"Render: {_renderFps:0.0} fps\n" +
                $"Requests: {provider.InferenceRequestsPerSecond:0.0}/s   Results: {provider.PoseResultsPerSecond:0.0}/s\n" +
                $"Latest pose age: {age}\n" +
                $"Raw trusted: {_observation.trustedCount}/{PoseObservation.LandmarkCount}\n" +
                $"Canonical tracked: {_canonicalFrame.trackedJointCount}/{CanonicalPoseFrame.JointCount}\n" +
                $"Stabilized tracked: {_stabilizedFrame.trackedJointCount}/{CanonicalPoseFrame.JointCount}\n" +
                $"Pelvis: {_canonicalFrame.hasCanonicalPelvis}   3D: {_canonicalFrame.hasCanonical3D}\n" +
                $"Calibration: {_calibration.State}   {_calibration.Progress01 * 100f:0}%   valid={_calibration.IsValid}\n" +
                $"Calib dimensions: shoulder {_calibration.Profile.shoulderWidth:0.00}   hip {_calibration.Profile.hipWidth:0.00}   torso {_calibration.Profile.torsoLength:0.00}\n" +
                $"Inference: {provider.LastInferenceDurationMilliseconds:0.0} ms\n" +
                $"Orientation: sensor rot {provider.Orientation.SensorRotationDegrees}° / sensor V {provider.Orientation.SensorVerticallyMirrored}\n" +
                $"Inference flips H/V {provider.Orientation.InferenceFlipHorizontally}/{provider.Orientation.InferenceFlipVertically}, rot {provider.Orientation.InferenceRotationDegrees}°\n" +
                $"Display mirror: {provider.Orientation.DisplayMirrored}\n" +
                $"Status: {provider.StatusMessage}";
            GUI.Label(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, panel.height - 20f), text, _smallLabelStyle);

            var help = new Rect(16f, Screen.height - 36f, 640f, 24f);
            GUI.Label(help, "R retry   C calibrate   X cancel/reset   F1 raw   F2 canonical 2D   F3 3D   F4 stabilized 2D   Cyan stabilized / yellow canonical / magenta derived", _smallLabelStyle);
        }

        private void DrawCanonical3DView()
        {
            var width = Mathf.Clamp(previewPanelWidth, 280f, 420f);
            var panel = new Rect(Screen.width - width - 16f, 16f, width, Screen.height - 64f);
            GUI.color = new Color(0.015f, 0.02f, 0.035f, 0.92f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 22f), "CANONICAL 3D / LOCAL SPACE", _smallLabelStyle);

            var view = new Rect(panel.x + 16f, panel.y + 38f, panel.width - 32f, panel.height - 86f);
            var center = view.center;
            DrawLine(center + new Vector2(-view.width * 0.35f, 0f), center + new Vector2(view.width * 0.35f, 0f), new Color(0.95f, 0.25f, 0.25f, 0.55f), 1f);
            DrawLine(center + new Vector2(0f, view.height * 0.35f), center + new Vector2(0f, -view.height * 0.35f), new Color(0.25f, 1f, 0.35f, 0.55f), 1f);
            DrawLine(center + new Vector2(-view.width * 0.18f, view.height * 0.18f), center + new Vector2(view.width * 0.18f, -view.height * 0.18f), new Color(0.25f, 0.55f, 1f, 0.55f), 1f);

            for (var i = 0; i < CanonicalConnections.GetLength(0); i++)
            {
                var from = _canonicalFrame.GetJoint(CanonicalConnections[i, 0]);
                var to = _canonicalFrame.GetJoint(CanonicalConnections[i, 1]);
                if (from.IsTracked && to.IsTracked && from.hasLocalPosition && to.hasLocalPosition)
                {
                    DrawLine(Project3D(from.localPosition, view), Project3D(to.localPosition, view), new Color(1f, 0.72f, 0.12f, 0.9f), 3f);
                }
            }

            for (var i = 0; i < CanonicalConnections.GetLength(0); i++)
            {
                var from = _stabilizedFrame.GetJoint(CanonicalConnections[i, 0]);
                var to = _stabilizedFrame.GetJoint(CanonicalConnections[i, 1]);
                if (from.IsTracked && to.IsTracked && from.hasLocalPosition && to.hasLocalPosition)
                {
                    DrawLine(Project3D(from.localPosition, view), Project3D(to.localPosition, view), new Color(0.15f, 1f, 1f, 0.95f), 3f);
                }
            }

            for (var i = 0; i < CanonicalPoseFrame.JointCount; i++)
            {
                var joint = _canonicalFrame.GetJoint((CanonicalJointId)i);
                if (!joint.IsTracked || !joint.hasLocalPosition)
                {
                    continue;
                }

                DrawPoint(Project3D(joint.localPosition, view), IsDerivedJoint(joint.id) ? new Color(1f, 0.3f, 0.95f) : new Color(1f, 0.9f, 0.15f), 9f);
            }

            for (var i = 0; i < CanonicalPoseFrame.JointCount; i++)
            {
                var joint = _stabilizedFrame.GetJoint((CanonicalJointId)i);
                if (!joint.IsTracked || !joint.hasLocalPosition)
                {
                    continue;
                }

                DrawPoint(Project3D(joint.localPosition, view), new Color(0.25f, 1f, 1f), 7f);
            }

            GUI.Label(new Rect(panel.x + 12f, panel.yMax - 42f, panel.width - 24f, 34f), "+X right/red   +Y up/green   +Z away/blue\nYellow canonical   Cyan stabilized   Pelvis-relative when available", _smallLabelStyle);
        }

        private double GetProviderEvaluationTime()
        {
            if (_canonicalFrame.receivedAtSeconds > 0d && !double.IsInfinity(provider.LatestPoseAgeMilliseconds))
            {
                var age = provider.LatestPoseAgeMilliseconds > 0d ? provider.LatestPoseAgeMilliseconds : 0d;
                return _canonicalFrame.receivedAtSeconds + age * 0.001d;
            }

            return Time.unscaledTimeAsDouble;
        }

        private static Vector2 Project3D(Vector3 position, Rect view)
        {
            var perspective = 1f / Mathf.Max(0.45f, 1f + position.z * 0.45f);
            var scale = Mathf.Min(view.width, view.height) * 0.42f;
            return view.center + new Vector2(position.x * scale * perspective, -position.y * scale * perspective);
        }

        private static Rect GetContentRect(Rect target, int width, int height, int displayRotationDegrees)
        {
            if (width <= 0 || height <= 0)
            {
                return target;
            }

            if (displayRotationDegrees == 90 || displayRotationDegrees == 270)
            {
                var swapped = width;
                width = height;
                height = swapped;
            }

            var sourceAspect = width / (float)height;
            var targetAspect = target.width / target.height;
            if (sourceAspect > targetAspect)
            {
                var drawnWidth = target.height * sourceAspect;
                return new Rect(target.center.x - drawnWidth * 0.5f, target.y, drawnWidth, target.height);
            }

            var drawnHeight = target.width / sourceAspect;
            return new Rect(target.x, target.center.y - drawnHeight * 0.5f, target.width, drawnHeight);
        }

        private static Vector2 ToRawScreenPoint(Vector2 normalizedImagePosition, Rect contentRect)
        {
            return new Vector2(
                contentRect.x + normalizedImagePosition.x * contentRect.width,
                contentRect.y + (1f - normalizedImagePosition.y) * contentRect.height);
        }

        private static bool IsDerivedJoint(CanonicalJointId id)
        {
            return id == CanonicalJointId.Pelvis || id == CanonicalJointId.Spine || id == CanonicalJointId.Chest;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void DrawPoint(Vector2 position, Color color, float size)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(position.x - size * 0.5f, position.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private static void DrawLine(Vector2 from, Vector2 to, Color color, float width)
        {
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;
            var delta = to - from;
            var length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            var midpoint = (from + to) * 0.5f;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, midpoint);
            GUI.color = color;
            GUI.DrawTexture(new Rect(midpoint.x - length * 0.5f, midpoint.y - width * 0.5f, length, width), Texture2D.whiteTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }
    }
}
