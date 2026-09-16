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
        private bool _hasAppliedState;

        public bool HasValidPreview { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            if (aspectRatioFitter != null)
            {
                aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            }
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

            if (_hasAppliedState &&
                _lastTexture == texture &&
                _lastSourceWidth == sourceWidth &&
                _lastSourceHeight == sourceHeight &&
                _lastRotationDegrees == rotationDegrees &&
                _lastHorizontalMirror == horizontalMirror &&
                _lastVerticalCorrection == verticalCorrection)
            {
                return;
            }

            var geometry = CalibrationCameraPresentationGeometry.Create(
                sourceWidth,
                sourceHeight,
                rotationDegrees,
                horizontalMirror,
                verticalCorrection);

            rawImage.texture = texture;
            rawImage.color = texture == null
                ? new Color(0.08f, 0.09f, 0.12f, 1f)
                : Color.white;

            var contentTransform = rawImage.rectTransform;
            contentTransform.localRotation = Quaternion.Euler(0f, 0f, geometry.RotationDegrees);
            contentTransform.localScale = new Vector3(
                geometry.HorizontalMirror ? -1f : 1f,
                geometry.VerticalCorrection ? -1f : 1f,
                1f);

            if (aspectRatioFitter != null)
            {
                aspectRatioFitter.enabled = geometry.HasValidSource;
                if (geometry.HasValidSource)
                {
                    aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    aspectRatioFitter.aspectRatio = geometry.AspectRatio;
                }
            }

            HasValidPreview = geometry.HasValidSource && texture != null;
            _lastTexture = texture;
            _lastSourceWidth = sourceWidth;
            _lastSourceHeight = sourceHeight;
            _lastRotationDegrees = rotationDegrees;
            _lastHorizontalMirror = horizontalMirror;
            _lastVerticalCorrection = verticalCorrection;
            _hasAppliedState = true;
        }

        private void ResolveReferences()
        {
            rawImage = rawImage == null ? GetComponent<RawImage>() : rawImage;
            aspectRatioFitter = aspectRatioFitter == null ? GetComponent<AspectRatioFitter>() : aspectRatioFitter;
        }
    }
}
