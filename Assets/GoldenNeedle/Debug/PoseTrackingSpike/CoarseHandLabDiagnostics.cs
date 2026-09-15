using GoldenNeedle.Core.Motion.Hands;
using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using UnityEngine;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    /// <summary>
    /// Compact read-only Lab strip for Batch 4A signal QA. It never applies hand state to the rig.
    /// </summary>
    public sealed class CoarseHandLabDiagnostics : MonoBehaviour
    {
        private readonly CoarseHandFrame _frame = new CoarseHandFrame();
        private MediaPipeCanonicalPoseSource _source;
        private PoseTrackingSpikePresenter _presenter;
        private ThirdPersonLabCamera _gameViewCamera;
        private GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToLivePoseSource()
        {
            var source = Object.FindAnyObjectByType<MediaPipeCanonicalPoseSource>();
            if (source != null && source.GetComponent<CoarseHandLabDiagnostics>() == null)
            {
                source.gameObject.AddComponent<CoarseHandLabDiagnostics>();
            }
        }

        private void Awake()
        {
            _source = GetComponent<MediaPipeCanonicalPoseSource>();
            _presenter = GetComponent<PoseTrackingSpikePresenter>();
            _gameViewCamera = Object.FindAnyObjectByType<ThirdPersonLabCamera>();
        }

        private void OnGUI()
        {
            if (_source == null ||
                (_presenter != null &&
                 (!_presenter.MainDiagnosticsVisible || _presenter.AllDebugPresentationHidden)) ||
                (_gameViewCamera != null && _gameViewCamera.IsGameViewActive))
            {
                return;
            }

            _source.TryCopyLatestCoarseHands(_source.EvaluationTimeSeconds, _frame);
            EnsureStyle();

            var width = Mathf.Min(920f, Mathf.Max(360f, Screen.width - 24f));
            var height = 24f;
            var rect = new Rect(
                12f,
                Mathf.Max(12f, Screen.height - 106f),
                width,
                height);
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.88f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 2f, rect.width - 16f, rect.height - 4f),
                _source.CoarseHandDiagnosticSummary,
                _style);
        }

        private void EnsureStyle()
        {
            if (_style != null)
            {
                return;
            }
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = Color.white },
                clipping = TextClipping.Clip,
            };
        }
    }
}
