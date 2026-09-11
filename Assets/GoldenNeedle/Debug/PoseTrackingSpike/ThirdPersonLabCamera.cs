using GoldenNeedle.Core.Motion.Locomotion;
using GoldenNeedle.Core.Motion.Retargeting;
using UnityEngine;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    /// <summary>
    /// Presentation-only third-person camera for the Motion Engine Lab. In Lab View the Camera
    /// remains enabled as a clear-only, culling-mask-zero camera so Unity has a valid render target
    /// behind the IMGUI webcam without rendering the world. F12 restores the original world camera
    /// settings and enables the existing third-person follow behavior.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class ThirdPersonLabCamera : MonoBehaviour
    {
        [Header("Runtime Wiring")]
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private HumanoidRigBinding binding;
        [SerializeField] private EmbodiedLocomotionController locomotion;

        [Header("Third-Person Camera")]
        [Tooltip("Distance behind the avatar along the last valid mapped body heading.")]
        [Min(0.5f), SerializeField] private float followDistance = 4.0f;

        [Tooltip("Camera height above the avatar root.")]
        [Min(0.2f), SerializeField] private float cameraHeight = 2.2f;

        [Tooltip("Height above the avatar root used as the camera look target.")]
        [Min(0f), SerializeField] private float lookHeight = 1.15f;

        [Tooltip("Response speed for camera position and look rotation.")]
        [Min(0.1f), SerializeField] private float positionResponse = 7.0f;

        [Tooltip("Response speed for the retained body-heading direction.")]
        [Min(0.1f), SerializeField] private float headingResponse = 8.0f;

        [Tooltip("Prototype third-person field of view.")]
        [Range(30f, 90f), SerializeField] private float fieldOfView = 55f;

        private bool _gameViewActive;
        private bool _hasHeading;
        private Vector2 _smoothedHeading;
        private Camera _capturedCamera;
        private int _gameCullingMask;
        private CameraClearFlags _gameClearFlags;
        private Color _gameBackgroundColor;

        public bool IsGameViewActive => _gameViewActive;
        public bool HasRetainedHeading => _hasHeading;
        public Vector2 RetainedHeadingXZ => _smoothedHeading;
        public bool IsLabClearOnly =>
            controlledCamera != null &&
            controlledCamera.enabled &&
            controlledCamera.cullingMask == 0 &&
            controlledCamera.clearFlags == CameraClearFlags.SolidColor &&
            !_gameViewActive;

        private void Awake()
        {
            ResolveReferences();
            CaptureGameCameraSettings();
            ApplyCameraSettings();
            ApplyCameraRenderMode();
        }

        private void OnValidate()
        {
            followDistance = Mathf.Max(0.5f, followDistance);
            cameraHeight = Mathf.Max(0.2f, cameraHeight);
            lookHeight = Mathf.Max(0f, lookHeight);
            positionResponse = Mathf.Max(0.1f, positionResponse);
            headingResponse = Mathf.Max(0.1f, headingResponse);
            fieldOfView = Mathf.Clamp(fieldOfView, 30f, 90f);
            ApplyCameraSettings();
        }

        public void ToggleGameView()
        {
            SetGameViewActive(!_gameViewActive);
        }

        public void SetGameViewActive(bool active)
        {
            _gameViewActive = active;
            ResolveReferences();
            CaptureGameCameraSettings();
            ApplyCameraSettings();
            ApplyCameraRenderMode();

            if (active)
            {
                TryRefreshHeading(1f);
                SnapIfUninitialized();
            }
        }

        private void LateUpdate()
        {
            if (!_gameViewActive)
            {
                return;
            }

            ResolveReferences();
            var playerRoot = ResolvePlayerRoot();
            if (playerRoot == null)
            {
                return;
            }

            var dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            TryRefreshHeading(dt);

            if (!_hasHeading)
            {
                // No mapped heading has ever been available. Preserve the current camera transform
                // rather than snapping to a hard-coded global direction.
                return;
            }

            var heading3 = new Vector3(
                _smoothedHeading.x,
                0f,
                _smoothedHeading.y);
            var targetPosition =
                playerRoot.position -
                heading3 * followDistance +
                Vector3.up * cameraHeight;
            var lookTarget =
                playerRoot.position +
                Vector3.up * lookHeight;

            var positionAlpha =
                1f - Mathf.Exp(-positionResponse * dt);
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                positionAlpha);

            var lookDirection = lookTarget - transform.position;
            if (lookDirection.sqrMagnitude > 0.000001f)
            {
                var targetRotation = Quaternion.LookRotation(
                    lookDirection.normalized,
                    Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    positionAlpha);
            }
        }

        private void ResolveReferences()
        {
            controlledCamera = controlledCamera == null
                ? GetComponent<Camera>()
                : controlledCamera;

            if (locomotion == null)
            {
                locomotion =
                    Object.FindAnyObjectByType<EmbodiedLocomotionController>();
            }

            if (binding == null)
            {
                binding =
                    Object.FindAnyObjectByType<HumanoidRigBinding>();
            }
        }

        private void CaptureGameCameraSettings()
        {
            if (controlledCamera == null || controlledCamera == _capturedCamera)
            {
                return;
            }

            _capturedCamera = controlledCamera;
            _gameCullingMask = controlledCamera.cullingMask;
            _gameClearFlags = controlledCamera.clearFlags;
            _gameBackgroundColor = controlledCamera.backgroundColor;
        }

        private Transform ResolvePlayerRoot()
        {
            if (locomotion != null && locomotion.PlayerRoot != null)
            {
                return locomotion.PlayerRoot;
            }

            return binding != null && binding.IsBound
                ? binding.AvatarRoot
                : null;
        }

        private void TryRefreshHeading(float deltaTime)
        {
            if (locomotion == null ||
                !locomotion.HasWorldHeading ||
                locomotion.WorldHeadingXZ.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            var targetHeading = locomotion.WorldHeadingXZ.normalized;
            if (!_hasHeading)
            {
                _smoothedHeading = targetHeading;
                _hasHeading = true;
                return;
            }

            var alpha =
                1f - Mathf.Exp(-headingResponse * Mathf.Max(0.0001f, deltaTime));
            var blended = Vector2.Lerp(
                _smoothedHeading,
                targetHeading,
                alpha);
            if (blended.sqrMagnitude > 0.000001f)
            {
                _smoothedHeading = blended.normalized;
            }
        }

        private void SnapIfUninitialized()
        {
            var playerRoot = ResolvePlayerRoot();
            if (playerRoot == null || !_hasHeading)
            {
                return;
            }

            var heading3 = new Vector3(
                _smoothedHeading.x,
                0f,
                _smoothedHeading.y);
            transform.position =
                playerRoot.position -
                heading3 * followDistance +
                Vector3.up * cameraHeight;

            var lookDirection =
                playerRoot.position +
                Vector3.up * lookHeight -
                transform.position;
            if (lookDirection.sqrMagnitude > 0.000001f)
            {
                transform.rotation = Quaternion.LookRotation(
                    lookDirection.normalized,
                    Vector3.up);
            }
        }

        private void ApplyCameraSettings()
        {
            if (controlledCamera != null)
            {
                controlledCamera.fieldOfView = fieldOfView;
            }
        }

        private void ApplyCameraRenderMode()
        {
            if (controlledCamera == null)
            {
                return;
            }

            // Keep one lightweight Camera active in Lab so Unity does not display the
            // "No cameras rendering" placeholder. The webcam remains an IMGUI presentation
            // drawn later and retains its fitted whole-frame geometry.
            controlledCamera.enabled = true;
            if (_gameViewActive)
            {
                controlledCamera.cullingMask = _gameCullingMask;
                controlledCamera.clearFlags = _gameClearFlags;
                controlledCamera.backgroundColor = _gameBackgroundColor;
                return;
            }

            controlledCamera.cullingMask = 0;
            controlledCamera.clearFlags = CameraClearFlags.SolidColor;
            controlledCamera.backgroundColor = Color.black;
        }
    }
}
