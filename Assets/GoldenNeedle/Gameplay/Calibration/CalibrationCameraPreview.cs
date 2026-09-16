using GoldenNeedle.Gameplay.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenNeedle.Gameplay.Calibration
{
    /// <summary>
    /// Production-only RawImage presentation for the persistent player's existing camera texture.
    /// It never owns a camera, copies a frame, or changes inference coordinates.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class CalibrationCameraPreview : MonoBehaviour
    {
        [SerializeField] private RawImage rawImage;
        [SerializeField] private AspectRatioFitter aspectRatioFitter;

        private GoldenNeedlePlayerFacade _playerFacade;
        private Texture _lastTexture;
        private int _lastSourceWidth;
        private int _lastSourceHeight;
        private int _lastRotationDegrees;
        private bool _lastHorizontalMirror;
        private bool _lastVerticalCorrection;
        private RectTransform _lastTargetTransform;
        private float _lastTargetWidth;
        private float _lastTargetHeight;
        private bool _hasAppliedState;

        public bool HasValidPreview { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            DisableAspectRatioFitter();
        }

        public void SetPlayerFacade(GoldenNeedlePlayerFacade playerFacade)
        {
            if (_playerFacade == playerFacade)
            {
                return;
            }

            _playerFacade = playerFacade;
            _hasAppliedState = false;
        }

        private void LateUpdate()
        {
            if (_playerFacade == null)
            {
                var session = GoldenNeedlePlayerSession.Instance;
                _playerFacade = session == null
                    ? null
                    : session.GetComponent<GoldenNeedlePlayerFacade>();
            }

            ApplyCurrentState();
        }

        private void ApplyCurrentState()
        {
            ResolveReferences();
            DisableAspectRatioFitter();
            if (rawImage == null || _playerFacade == null)
            {
                HasValidPreview = false;
                return;
            }

            var texture = _playerFacade.CameraPreviewTexture;
            var sourceWidth = _playerFacade.CameraPreviewSourceWidth;
            var sourceHeight = _playerFacade.CameraPreviewSourceHeight;
            var rotationDegrees = _playerFacade.CameraPreviewRotationDegrees;
            var horizontalMirror = _playerFacade.CameraPreviewPresentationHorizontalMirror;
            var verticalCorrection = _playerFacade.CameraPreviewDisplayVerticalCorrection;
            var contentTransform = rawImage.rectTransform;
            var targetTransform = contentTransform.parent as RectTransform;
            var targetWidth = targetTransform == null ? 0f : targetTransform.rect.width;
            var targetHeight = targetTransform == null ? 0f : targetTransform.rect.height;

            if (_hasAppliedState &&
                _lastTexture == texture &&
                _lastSourceWidth == sourceWidth &&
                _lastSourceHeight == sourceHeight &&
                _lastRotationDegrees == rotationDegrees &&
                _lastHorizontalMirror == horizontalMirror &&
                _lastVerticalCorrection == verticalCorrection &&
                _lastTargetTransform == targetTransform &&
                Mathf.Approximately(_lastTargetWidth, targetWidth) &&
                Mathf.Approximately(_lastTargetHeight, targetHeight))
            {
                return;
            }

            var geometry = CalibrationCameraPresentationGeometry.Create(
                sourceWidth,
                sourceHeight,
                rotationDegrees,
                targetWidth,
                targetHeight,
                horizontalMirror,
                verticalCorrection);

            rawImage.texture = texture;
            rawImage.color = texture == null
                ? new Color(0.08f, 0.09f, 0.12f, 1f)
                : Color.white;

            contentTransform.anchorMin = new Vector2(0.5f, 0.5f);
            contentTransform.anchorMax = new Vector2(0.5f, 0.5f);
            contentTransform.pivot = new Vector2(0.5f, 0.5f);
            contentTransform.anchoredPosition = Vector2.zero;
            contentTransform.localRotation = Quaternion.Euler(0f, 0f, geometry.RotationDegrees);
            contentTransform.localScale = new Vector3(
                geometry.HorizontalMirror ? -1f : 1f,
                geometry.VerticalCorrection ? -1f : 1f,
                1f);

            if (geometry.HasValidSource && geometry.HasValidTarget)
            {
                contentTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    geometry.RawTextureSize.x);
                contentTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    geometry.RawTextureSize.y);
            }
            else
            {
                contentTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
                contentTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
            }

            HasValidPreview = geometry.HasValidSource && geometry.HasValidTarget && texture != null;
            _lastTexture = texture;
            _lastSourceWidth = sourceWidth;
            _lastSourceHeight = sourceHeight;
            _lastRotationDegrees = rotationDegrees;
            _lastHorizontalMirror = horizontalMirror;
            _lastVerticalCorrection = verticalCorrection;
            _lastTargetTransform = targetTransform;
            _lastTargetWidth = targetWidth;
            _lastTargetHeight = targetHeight;
            _hasAppliedState = true;
        }

        private void ResolveReferences()
        {
            rawImage = rawImage == null ? GetComponent<RawImage>() : rawImage;
            aspectRatioFitter = aspectRatioFitter == null ? GetComponent<AspectRatioFitter>() : aspectRatioFitter;
        }

        private void DisableAspectRatioFitter()
        {
            if (aspectRatioFitter != null)
            {
                aspectRatioFitter.enabled = false;
            }
        }
    }
}
