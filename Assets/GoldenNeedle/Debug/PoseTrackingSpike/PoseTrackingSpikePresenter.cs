using UnityEngine;
using UnityEngine.InputSystem;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    [RequireComponent(typeof(MediaPipePoseProvider))]
    public sealed class PoseTrackingSpikePresenter : MonoBehaviour
    {
        private static readonly int[,] Connections =
        {
            { 0, 1 }, { 0, 2 }, { 1, 3 }, { 2, 4 }, { 0, 7 }, { 0, 8 },
            { 11, 12 }, { 11, 13 }, { 13, 15 }, { 15, 17 }, { 15, 19 }, { 15, 21 },
            { 12, 14 }, { 14, 16 }, { 16, 18 }, { 16, 20 }, { 16, 22 },
            { 11, 23 }, { 12, 24 }, { 23, 24 }, { 23, 25 }, { 25, 27 }, { 27, 29 }, { 27, 31 },
            { 24, 26 }, { 26, 28 }, { 28, 30 }, { 28, 32 },
        };

        [SerializeField] private MediaPipePoseProvider provider;
        [SerializeField] private bool drawUnavailableLandmarks = true;
        [SerializeField] private float previewPanelWidth = 360f;

        private readonly PoseObservation _observation = new PoseObservation();
        private float _renderFps;
        private GUIStyle _labelStyle;
        private GUIStyle _smallLabelStyle;

        private void Awake()
        {
            provider = provider == null ? GetComponent<MediaPipePoseProvider>() : provider;
        }

        private void Update()
        {
            if (provider == null)
            {
                return;
            }

            provider.CopyLatestObservation(_observation);
            var frameTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _renderFps = Mathf.Lerp(_renderFps, 1f / frameTime, 1f - Mathf.Exp(-8f * frameTime));

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                provider.Retry();
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
            var contentRect = GetContentRect(previewRect, provider.ActualCameraWidth, provider.ActualCameraHeight);
            var oldMatrix = GUI.matrix;
            var pivot = previewRect.center;

            GUIUtility.ScaleAroundPivot(new Vector2(provider.FlipInputHorizontally ? -1f : 1f, provider.FlipInputVertically ? -1f : 1f), pivot);
            GUIUtility.RotateAroundPivot(-provider.VideoRotationAngle, pivot);
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

            DrawSkeleton(contentRect);
            GUI.matrix = oldMatrix;
            DrawDiagnostics();
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

        private void DrawSkeleton(Rect contentRect)
        {
            if (!_observation.hasPose)
            {
                return;
            }

            for (var i = 0; i < Connections.GetLength(0); i++)
            {
                var from = _observation.GetLandmark(Connections[i, 0]);
                var to = _observation.GetLandmark(Connections[i, 1]);
                if (from.IsTracked && to.IsTracked)
                {
                    DrawLine(ToScreenPoint(from, contentRect), ToScreenPoint(to, contentRect), new Color(0.1f, 1f, 0.55f, 0.95f), 4f);
                }
            }

            for (var i = 0; i < PoseObservation.LandmarkCount; i++)
            {
                var landmark = _observation.GetLandmark(i);
                if (landmark.IsTracked)
                {
                    DrawPoint(ToScreenPoint(landmark, contentRect), new Color(0.2f, 1f, 0.7f, 1f), 12f);
                }
                else if (drawUnavailableLandmarks)
                {
                    DrawPoint(ToScreenPoint(landmark, contentRect), new Color(1f, 0.55f, 0.15f, 0.35f), 7f);
                }
            }
        }

        private void DrawDiagnostics()
        {
            var panelHeight = 224f;
            var panel = new Rect(Screen.width - previewPanelWidth - 16f, 16f, previewPanelWidth, panelHeight);
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.82f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var state = !_observation.hasPose
                ? provider.Status == PoseProviderStatus.Ready ? "WAITING / UNAVAILABLE" : provider.Status.ToString().ToUpperInvariant()
                : _observation.trustedCount > 0 ? "TRACKING" : "POSE / NO TRUSTED LANDMARKS";
            var cameraName = string.IsNullOrEmpty(provider.SelectedCameraName) ? "(none)" : provider.SelectedCameraName;
            var resolution = provider.ActualCameraWidth > 0 ? $"{provider.ActualCameraWidth}x{provider.ActualCameraHeight}" : "(not started)";
            var age = double.IsInfinity(provider.LatestPoseAgeMilliseconds) ? "-" : $"{provider.LatestPoseAgeMilliseconds:0} ms";
            var text =
                $"POSE TRACKING SPIKE\n" +
                $"State: {state}\n" +
                $"Camera: {cameraName}\n" +
                $"Capture: {resolution} @ {provider.CameraFramesPerSecond:0.0} fps\n" +
                $"Render: {_renderFps:0.0} fps\n" +
                $"Requests: {provider.InferenceRequestsPerSecond:0.0}/s   Results: {provider.PoseResultsPerSecond:0.0}/s\n" +
                $"Latest pose age: {age}\n" +
                $"Trusted landmarks: {_observation.trustedCount}/{PoseObservation.LandmarkCount}\n" +
                $"Inference: {provider.LastInferenceDurationMilliseconds:0.0} ms\n" +
                $"Orientation: rot {provider.VideoRotationAngle}° / mirror {provider.VideoVerticallyMirrored}\n" +
                $"Status: {provider.StatusMessage}";
            GUI.Label(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, panel.height - 20f), text, _smallLabelStyle);

            var help = new Rect(16f, Screen.height - 36f, 500f, 24f);
            GUI.Label(help, "R: retry camera/model   Green: trusted   Orange: unavailable/untrusted", _smallLabelStyle);
        }

        private static Rect GetContentRect(Rect target, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return target;
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

        private static Vector2 ToScreenPoint(PoseLandmarkObservation landmark, Rect contentRect)
        {
            return new Vector2(contentRect.x + landmark.x * contentRect.width, contentRect.y + (1f - landmark.y) * contentRect.height);
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
