using UnityEngine;
using GoldenNeedle.Core.Motion.Retargeting;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    /// <summary>
    /// Presents the actual procedural retarget target through a dedicated camera. The rig remains
    /// the same Transform hierarchy consumed by HumanoidRetargeter; this component only renders it
    /// into a small Motion Engine Lab panel so the fullscreen webcam IMGUI cannot obscure it.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class ProceduralDebugRigView : MonoBehaviour
    {
        [SerializeField] private ProceduralDebugHumanoidRig rig;
        [SerializeField] private HumanoidRetargeter retargeter;
        [SerializeField] private int renderTextureSize = 512;

        private Camera _camera;
        private RenderTexture _renderTexture;

        public ProceduralDebugHumanoidRig Rig => rig;
        public bool IsReady => rig != null && rig.IsBuilt && _camera != null && _renderTexture != null;
        public RenderTexture Texture => _renderTexture;

        private void Awake()
        {
            rig = rig == null ? GetComponent<ProceduralDebugHumanoidRig>() : rig;
            retargeter = retargeter == null ? GetComponent<HumanoidRetargeter>() : retargeter;
            CreateCamera();
            AimCamera();
        }

        private void Start()
        {
            rig = rig == null ? GetComponent<ProceduralDebugHumanoidRig>() : rig;
            retargeter = retargeter == null ? GetComponent<HumanoidRetargeter>() : retargeter;
            AimCamera();
        }

        private void LateUpdate()
        {
            retargeter = retargeter == null ? GetComponent<HumanoidRetargeter>() : retargeter;
            UpdateKinematicMarkers();
            AimCamera();
        }

        private void UpdateKinematicMarkers()
        {
            if (rig == null)
            {
                return;
            }

            var show = retargeter != null && retargeter.DriveRig;
            for (var i = 0; i < CanonicalKinematicTargets.ChainCount; i++)
            {
                var state = retargeter == null
                    ? default
                    : retargeter.GetChainDebugState((CanonicalKinematicChainId)i);
                rig.SetKinematicMarkers(
                    (CanonicalKinematicChainId)i,
                    show && state.targetGenerated,
                    state.desiredEffectorPosition,
                    show && state.targetGenerated && state.hasBendHint,
                    state.bendHintPosition);
            }
        }

        private void CreateCamera()
        {
            if (_camera != null)
            {
                return;
            }

            var cameraObject = new GameObject("DebugRigViewCamera");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.015f, 0.02f, 0.035f, 1f);
            _camera.orthographic = true;
            _camera.orthographicSize = 1.35f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 20f;
            _camera.cullingMask = -1;

            var size = Mathf.Clamp(renderTextureSize, 256, 1024);
            _renderTexture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
            {
                name = "MotionEngineDebugRigView",
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            _renderTexture.Create();
            _camera.targetTexture = _renderTexture;
        }

        private void AimCamera()
        {
            if (_camera == null || rig == null || rig.AvatarRoot == null)
            {
                return;
            }

            var target = rig.AvatarRoot.position + Vector3.up * 1.0f;
            _camera.transform.position = target + Vector3.back * 3.2f;
            _camera.transform.rotation = Quaternion.LookRotation(target - _camera.transform.position, Vector3.up);
        }

        private void OnDestroy()
        {
            if (_camera != null)
            {
                _camera.targetTexture = null;
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }
        }
    }
}
