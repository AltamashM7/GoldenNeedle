using GoldenNeedle.Core.Motion.Locomotion;
using GoldenNeedle.Core.Motion.Retargeting;
using UnityEngine;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    /// <summary>
    /// Minimal fixed-camera Phase-5 locomotion viewport plus compact authority/vertical diagnostics.
    /// The display makes the reconstructed split explicit: body candidate, support validation and
    /// accepted physical displacement are separate observations; crouch diagnostics report the
    /// avatar-relative root mapping while Phase 4 remains the only leg-pose authority.
    /// </summary>
    [DefaultExecutionOrder(170)]
    public sealed class LocomotionPrototypeView : MonoBehaviour
    {
        [SerializeField] private HumanoidRigBinding binding;
        [SerializeField] private EmbodiedLocomotionController locomotion;
        [SerializeField] private int renderTextureWidth = 640;
        [SerializeField] private int renderTextureHeight = 360;
        [SerializeField] private float gridHalfExtent = 6f;
        [SerializeField] private float gridSpacing = 1f;

        private Camera _camera;
        private RenderTexture _renderTexture;
        private Transform _environmentRoot;
        private Material _gridMaterial;
        private Material _majorGridMaterial;
        private bool _initialized;
        private Vector3 _worldOrigin;
        private PoseTrackingSpikePresenter _presenter;
        private ThirdPersonLabCamera _gameViewCamera;
        private GUIStyle _diagnosticStyle;

        public bool IsReady => _initialized && _camera != null && _renderTexture != null;
        public RenderTexture Texture => _renderTexture;

        private void Awake()
        {
            binding = binding == null ? GetComponent<HumanoidRigBinding>() : binding;
            locomotion = locomotion == null ? GetComponent<EmbodiedLocomotionController>() : locomotion;
            _presenter = GetComponent<PoseTrackingSpikePresenter>();
            _gameViewCamera = Object.FindAnyObjectByType<ThirdPersonLabCamera>();
        }

        private void LateUpdate()
        {
            binding = binding == null ? GetComponent<HumanoidRigBinding>() : binding;
            locomotion = locomotion == null ? GetComponent<EmbodiedLocomotionController>() : locomotion;
            _presenter = _presenter == null ? GetComponent<PoseTrackingSpikePresenter>() : _presenter;
            _gameViewCamera = _gameViewCamera == null
                ? Object.FindAnyObjectByType<ThirdPersonLabCamera>()
                : _gameViewCamera;
            if (!_initialized)
            {
                TryInitialize();
            }
        }

        private void OnGUI()
        {
            if (locomotion == null ||
                _presenter == null ||
                !_presenter.LocomotionDiagnosticsVisible ||
                _presenter.AllDebugPresentationHidden ||
                _presenter.CoordinateDiagnosticVisible ||
                _presenter.Canonical3DVisible ||
                (_gameViewCamera != null && _gameViewCamera.IsGameViewActive))
            {
                return;
            }

            EnsureDiagnosticStyle();
            var panel = GetLocomotionPanelRect(Screen.width, Screen.height);
            var height = Mathf.Min(174f, Mathf.Max(142f, panel.height * 0.46f));
            var rect = new Rect(
                panel.x + 10f,
                panel.yMax - height - 8f,
                panel.width - 20f,
                height);

            GUI.color = new Color(0.015f, 0.025f, 0.04f, 0.94f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var root = locomotion.RootSample;
            var vertical = locomotion.VerticalSample;
            var crouch = locomotion.CrouchGroundingSample;
            var availability = vertical.isAvailable
                ? "Live"
                : vertical.referenceReady ? "Grace / unavailable" : "Unavailable";
            var reference = vertical.referenceReady ? "Ready" : "Waiting";
            var origin = locomotion.HasVerticalOrigin
                ? locomotion.VerticalOriginY.ToString("0.00")
                : "waiting";
            var accepted = root.movementAccepted ? "ACCEPT" : "hold";
            var support = root.supportValidated ? "valid" : "reject";
            var bend = vertical.groundedBendActive ? "grounded bend" : "neutral";
            var avatarScale = crouch.hasAvatarLegScale
                ? crouch.avatarStandingLegScale.ToString("0.000")
                : "waiting";
            var crouchMap = crouch.active ? "ACTIVE" : "idle";
            var clamp = crouch.depthClamped ? "depth-clamped" : "within-cap";
            var appliedOffset = locomotion.HasVerticalOrigin
                ? locomotion.FinalWorldPositionY - locomotion.VerticalOriginY
                : 0f;

            var text =
                $"PHYSICAL AUTHORITY: body={Format(root.bodyCandidateXZ)}   support={root.supportMode}/{support}   frame={accepted}\n" +
                $"Support evidence={Format(root.supportEvidenceXZ)}   accepted X/Z={Format(root.displacementXZ)}   v={Format(root.velocityXZ)}\n" +
                $"VERTICAL: {vertical.state}/{vertical.jumpPhase}   {availability}   {bend}\n" +
                $"Compression={vertical.crouchCompression:0.000}   support rise={vertical.supportRise:0.000}   asym={vertical.footAsymmetry:0.000}\n" +
                $"Jump={vertical.jumpSignal:0.000}   interpreter Y={vertical.worldOffsetY:+0.00;-0.00;0.00}   applied Y={appliedOffset:+0.00;-0.00;0.00}\n" +
                $"AVATAR CROUCH: leg scale={avatarScale}   map={crouchMap}   primary={crouch.primaryRootOffsetY:+0.000;-0.000;0.000}   final={crouch.finalRootOffsetY:+0.000;-0.000;0.000}   residual=off   {clamp}\n" +
                $"Reference={reference}   root-Y origin={origin}";

            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, rect.height - 10f),
                text,
                _diagnosticStyle);
        }

        private static string Format(Vector2 value)
        {
            return $"({value.x:+0.00;-0.00;0.00}, {value.y:+0.00;-0.00;0.00})";
        }

        private void TryInitialize()
        {
            var playerRoot = locomotion != null && locomotion.PlayerRoot != null
                ? locomotion.PlayerRoot
                : binding != null && binding.IsBound ? binding.AvatarRoot : null;
            if (playerRoot == null)
            {
                return;
            }

            _worldOrigin = playerRoot.position;
            CreateEnvironment();
            CreateCamera();
            _initialized = _camera != null && _renderTexture != null;
        }

        private void CreateEnvironment()
        {
            if (_environmentRoot != null)
            {
                return;
            }

            _environmentRoot = new GameObject("Phase5A_LocomotionGrid").transform;
            _environmentRoot.SetParent(transform, true);
            _environmentRoot.position = _worldOrigin;

            _gridMaterial = CreateMaterial(
                "Phase5A_Grid",
                new Color(0.25f, 0.32f, 0.40f, 1f));
            _majorGridMaterial = CreateMaterial(
                "Phase5A_GridMajor",
                new Color(0.10f, 0.75f, 0.92f, 1f));

            var spacing = Mathf.Max(0.5f, gridSpacing);
            var extent = Mathf.Max(3f, gridHalfExtent);
            var count = Mathf.CeilToInt(extent / spacing);
            for (var i = -count; i <= count; i++)
            {
                var offset = i * spacing;
                var major = i == 0 || Mathf.Abs(i) % 2 == 0;
                CreateLine(
                    new Vector3(offset, 0.005f, 0f),
                    new Vector3(0.018f, 0.01f, extent * 2f),
                    major);
                CreateLine(
                    new Vector3(0f, 0.006f, offset),
                    new Vector3(extent * 2f, 0.012f, 0.018f),
                    major);
            }

            for (var z = -4; z <= 6; z += 2)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"Distance_{z:+0;-0;0}";
                marker.transform.SetParent(_environmentRoot, false);
                marker.transform.localPosition = new Vector3(-extent + 0.25f, 0.15f, z);
                marker.transform.localScale = new Vector3(0.10f, 0.30f, 0.10f);
                var renderer = marker.GetComponent<Renderer>();
                if (renderer != null && _majorGridMaterial != null)
                {
                    renderer.sharedMaterial = _majorGridMaterial;
                }
                RemoveCollider(marker);
            }
        }

        private void CreateLine(Vector3 localPosition, Vector3 localScale, bool major)
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = major ? "GridMajor" : "GridMinor";
            line.transform.SetParent(_environmentRoot, false);
            line.transform.localPosition = localPosition;
            line.transform.localRotation = Quaternion.identity;
            line.transform.localScale = localScale;
            var renderer = line.GetComponent<Renderer>();
            var material = major ? _majorGridMaterial : _gridMaterial;
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
            RemoveCollider(line);
        }

        private void CreateCamera()
        {
            if (_camera != null)
            {
                return;
            }

            var cameraObject = new GameObject("Phase5A_LocomotionViewCamera");
            cameraObject.transform.SetParent(transform, true);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.015f, 0.02f, 0.035f, 1f);
            _camera.fieldOfView = 48f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 40f;
            _camera.cullingMask = -1;

            var target = _worldOrigin + new Vector3(0f, 0.9f, 1.0f);
            _camera.transform.position = _worldOrigin + new Vector3(0f, 5.2f, -7.2f);
            _camera.transform.rotation = Quaternion.LookRotation(
                target - _camera.transform.position,
                Vector3.up);

            var width = Mathf.Clamp(renderTextureWidth, 320, 1024);
            var height = Mathf.Clamp(renderTextureHeight, 180, 768);
            _renderTexture = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32)
            {
                name = "Phase5A_LocomotionPrototypeView",
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
            };
            _renderTexture.Create();
            _camera.targetTexture = _renderTexture;
        }

        private void EnsureDiagnosticStyle()
        {
            if (_diagnosticStyle != null)
            {
                return;
            }

            _diagnosticStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = Color.white },
            };
        }

        private static Rect GetLocomotionPanelRect(float screenWidth, float screenHeight)
        {
            var margin = Mathf.Clamp(
                Mathf.Min(screenWidth, screenHeight) * 0.015f,
                10f,
                16f);
            var gap = Mathf.Clamp(screenWidth * 0.009f, 10f, 14f);
            var helpHeight = Mathf.Clamp(screenHeight * 0.085f, 56f, 72f);
            var helpY = Mathf.Max(margin, screenHeight - margin - helpHeight);
            var contentBottom = Mathf.Max(margin + 240f, helpY - gap);
            var contentHeight = Mathf.Max(240f, contentBottom - margin);
            var availableWidth = Mathf.Max(580f, screenWidth - margin * 2f - gap);
            var columnWidth = availableWidth * 0.5f;
            var rightX = margin + columnWidth + gap;
            var topHeight = Mathf.Clamp(contentHeight * 0.61f, 340f, 430f);
            topHeight = Mathf.Min(
                topHeight,
                Mathf.Max(180f, contentHeight - gap - 140f));
            return new Rect(rightX, margin, columnWidth, topHeight);
        }

        private static Material CreateMaterial(string materialName, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return null;
            }

            return new Material(shader)
            {
                name = materialName,
                color = color,
            };
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            var collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }
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

            if (_environmentRoot != null)
            {
                Destroy(_environmentRoot.gameObject);
                _environmentRoot = null;
            }

            if (_gridMaterial != null)
            {
                Destroy(_gridMaterial);
                _gridMaterial = null;
            }

            if (_majorGridMaterial != null)
            {
                Destroy(_majorGridMaterial);
                _majorGridMaterial = null;
            }
        }
    }
}
