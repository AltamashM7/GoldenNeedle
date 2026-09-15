using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using UnityEngine;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    /// <summary>
    /// F7-only clarification of the body-frame acquisition layer versus the ExistingReadback
    /// implementation. This is presentation-only and never participates in pose processing.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class PoseAcquisitionLabDiagnostics : MonoBehaviour
    {
        private PoseTrackingSpikePresenter _presenter;
        private MediaPipePoseProvider _provider;
        private ThirdPersonLabCamera _gameViewCamera;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToLivePresenter()
        {
            var presenter = Object.FindAnyObjectByType<PoseTrackingSpikePresenter>();
            if (presenter == null || presenter.GetComponent<PoseAcquisitionLabDiagnostics>() != null)
            {
                return;
            }

            presenter.gameObject.AddComponent<PoseAcquisitionLabDiagnostics>();
        }

        private void Awake()
        {
            _presenter = GetComponent<PoseTrackingSpikePresenter>();
            _provider = GetComponent<MediaPipePoseProvider>();
            _gameViewCamera = Object.FindAnyObjectByType<ThirdPersonLabCamera>();
        }

        private void OnGUI()
        {
            if (_presenter == null ||
                _provider == null ||
                !_presenter.MainDiagnosticsVisible ||
                _presenter.AllDebugPresentationHidden ||
                (_gameViewCamera != null && _gameViewCamera.IsGameViewActive))
            {
                return;
            }

            EnsureStyles();

            var screenWidth = Mathf.Max(1f, Screen.width);
            var screenHeight = Mathf.Max(1f, Screen.height);
            var margin = Mathf.Clamp(Mathf.Min(screenWidth, screenHeight) * 0.015f, 10f, 16f);
            var gap = Mathf.Clamp(screenWidth * 0.009f, 10f, 14f);
            var helpHeight = Mathf.Clamp(screenHeight * 0.085f, 56f, 72f);
            var helpY = Mathf.Max(margin, screenHeight - margin - helpHeight);
            var contentBottom = Mathf.Max(margin + 240f, helpY - gap);
            var contentHeight = Mathf.Max(240f, contentBottom - margin);
            var availableWidth = Mathf.Max(580f, screenWidth - margin * 2f - gap);
            var columnWidth = availableWidth * 0.5f;
            var topHeight = Mathf.Clamp(contentHeight * 0.61f, 340f, 430f);
            topHeight = Mathf.Min(topHeight, Mathf.Max(180f, contentHeight - gap - 140f));

            var panelHeight = Mathf.Min(126f, Mathf.Max(96f, topHeight * 0.34f));
            var panel = new Rect(
                margin + 7f,
                margin + topHeight - panelHeight - 7f,
                Mathf.Max(260f, columnWidth - 14f),
                panelHeight);

            var oldColor = GUI.color;
            GUI.color = new Color(0.015f, 0.02f, 0.03f, 0.98f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = oldColor;

            var requested = _provider.RequestedBodyFrameAcquisitionModeLabel;
            var webCamCpuActive =
                _provider.ActiveBodyFrameAcquisitionMode == BodyFrameAcquisitionMode.WebCamCpuPixels;
            var active = webCamCpuActive
                ? "WebCamCPU/GetPixels32"
                : _provider.RequestedBodyFrameAcquisitionMode == BodyFrameAcquisitionMode.WebCamCpuPixels
                    ? "ExistingReadback (WebCamCPU fallback)"
                    : "ExistingReadback";
            var readback = webCamCpuActive
                ? "N/A (WebCamCPU active)"
                : _provider.ActiveBodyReadbackPath == BodyReadbackPath.DirectCPU
                    ? $"DirectCPU stage={_provider.ActiveDirectReadbackStageLabel}"
                    : _provider.ActiveBodyReadbackPathLabel;
            var fallback =
                _provider.RequestedBodyFrameAcquisitionMode == BodyFrameAcquisitionMode.WebCamCpuPixels &&
                !webCamCpuActive &&
                !string.IsNullOrEmpty(_provider.WebCamCpuAcquisitionFallbackReason)
                    ? _provider.WebCamCpuAcquisitionFallbackReason
                    : "none";

            GUI.Label(
                new Rect(panel.x + 9f, panel.y + 6f, panel.width - 18f, 20f),
                "BODY PATH (F7 authoritative)",
                _headerStyle);
            GUI.Label(
                new Rect(panel.x + 9f, panel.y + 27f, panel.width - 18f, panel.height - 31f),
                $"Requested: {requested}\n" +
                $"Acquisition: {active}\n" +
                $"Readback: {readback}\n" +
                $"Fallback: {fallback}\n" +
                $"Backend: {_provider.ActiveInferenceBackendLabel}   Body input: {_provider.BodyInferenceWidth}x{_provider.BodyInferenceHeight}",
                _labelStyle);
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null)
            {
                return;
            }

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = false,
                richText = false,
            };
            _labelStyle.normal.textColor = Color.white;

            _headerStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
            };
        }
    }
}
