using System;
using GoldenNeedle.Core.Commands;
using GoldenNeedle.Core.Motion.Locomotion;
using GoldenNeedle.Core.Motion.Retargeting;
using UnityEngine;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    /// <summary>
    /// Presentation-only third-person camera for the Motion Engine Lab. In Lab View the Camera
    /// remains enabled as a clear-only, culling-mask-zero camera so Unity has a valid render target
    /// behind the IMGUI webcam without rendering the world. F12 restores the original world camera
    /// settings. Foundation B extends the same single Camera authority with data-driven view presets.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class ThirdPersonLabCamera : MonoBehaviour
    {
        [Header("Runtime Wiring")]
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private HumanoidRigBinding binding;
        [SerializeField] private EmbodiedLocomotionController locomotion;

        [Header("Third-Person Compatibility Baseline")]
        [Tooltip("Legacy Back-view distance used to seed Foundation B defaults for existing scenes.")]
        [Min(0.5f), SerializeField] private float followDistance = 4.0f;

        [Tooltip("Legacy Back-view height used to seed Foundation B defaults for existing scenes.")]
        [Min(0.2f), SerializeField] private float cameraHeight = 2.2f;

        [Tooltip("Legacy Back-view look height used to seed Foundation B defaults for existing scenes.")]
        [Min(0f), SerializeField] private float lookHeight = 1.15f;

        [Tooltip("Legacy Back-view response used to seed Foundation B defaults for existing scenes.")]
        [Min(0.1f), SerializeField] private float positionResponse = 7.0f;

        [Tooltip("Response speed for the retained body-heading direction. This is separate from preset camera orientation response.")]
        [Min(0.1f), SerializeField] private float headingResponse = 8.0f;

        [Tooltip("Legacy Back-view field of view used to seed Foundation B defaults for existing scenes.")]
        [Range(30f, 90f), SerializeField] private float fieldOfView = 55f;

        [Header("Camera View Presets")]
        [Tooltip("Initial selected gameplay camera preset. Matching is trimmed and case-insensitive.")]
        [SerializeField] private string initialViewPreset = "Back";

        [Tooltip("Named data-driven gameplay camera presets. Existing scenes are seeded from the legacy Back values without a YAML migration.")]
        [SerializeField] private CameraViewPreset[] viewPresets = CameraViewPreset.CreateDefaults();

        [HideInInspector, SerializeField] private bool cameraPresetDefaultsInitialized;

        [Header("Camera Preset Diagnostics")]
        [SerializeField] private string selectedPresetDiagnostic = "Back";
        [SerializeField] private string focusTargetDiagnostic = "Unresolved";
        [SerializeField] private string presetSelectionDiagnostic = "Ready";

        private bool _gameViewActive;
        private bool _hasHeading;
        private Vector2 _smoothedHeading;
        private Camera _capturedCamera;
        private int _gameCullingMask;
        private CameraClearFlags _gameClearFlags;
        private Color _gameBackgroundColor;
        private int _selectedPresetIndex = -1;
        private bool _presetConfigurationPrepared;
        private CameraFocusResolution _lastFocusResolution;
        private bool _hasFocusResolution;
        private Func<string, bool> _registeredPresetSelector;

        public bool IsGameViewActive => _gameViewActive;
        public bool HasRetainedHeading => _hasHeading;
        public Vector2 RetainedHeadingXZ => _smoothedHeading;
        public string CurrentViewPresetName => selectedPresetDiagnostic;
        public CameraFocusSemantic CurrentRequestedFocusSemantic =>
            CurrentPreset == null ? CameraFocusSemantic.AvatarBody : CurrentPreset.focusSemantic;
        public bool HasResolvedFocus => _hasFocusResolution;
        public bool IsFocusFallbackActive => _hasFocusResolution && _lastFocusResolution.UsedFallback;
        public string ResolvedFocusTarget => _hasFocusResolution
            ? _lastFocusResolution.ResolvedTarget
            : "Unresolved";
        public string LastPresetSelectionError => presetSelectionDiagnostic == "Ready"
            ? string.Empty
            : presetSelectionDiagnostic;
        public bool IsLabClearOnly =>
            controlledCamera != null &&
            controlledCamera.enabled &&
            controlledCamera.cullingMask == 0 &&
            controlledCamera.clearFlags == CameraClearFlags.SolidColor &&
            !_gameViewActive;

        private CameraViewPreset CurrentPreset
        {
            get
            {
                EnsurePresetConfiguration();
                return _selectedPresetIndex >= 0 &&
                       viewPresets != null &&
                       _selectedPresetIndex < viewPresets.Length
                    ? viewPresets[_selectedPresetIndex]
                    : null;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            EnsurePresetConfiguration();
            CaptureGameCameraSettings();
            ApplyCameraSettingsImmediate();
            ApplyCameraRenderMode();
        }

        private void OnEnable()
        {
            _registeredPresetSelector = SelectViewPreset;
            GoldenNeedleCommandRuntimeServices.RegisterCameraViewPresetSelector(
                _registeredPresetSelector);
        }

        private void OnDisable()
        {
            if (_registeredPresetSelector != null)
            {
                GoldenNeedleCommandRuntimeServices.UnregisterCameraViewPresetSelector(
                    _registeredPresetSelector);
            }
        }

        private void OnDestroy()
        {
            if (_registeredPresetSelector != null)
            {
                GoldenNeedleCommandRuntimeServices.UnregisterCameraViewPresetSelector(
                    _registeredPresetSelector);
                _registeredPresetSelector = null;
            }
        }

        private void OnValidate()
        {
            followDistance = Mathf.Max(0.5f, followDistance);
            cameraHeight = Mathf.Max(0.2f, cameraHeight);
            lookHeight = Mathf.Max(0f, lookHeight);
            positionResponse = Mathf.Max(0.1f, positionResponse);
            headingResponse = Mathf.Max(0.1f, headingResponse);
            fieldOfView = Mathf.Clamp(fieldOfView, 30f, 90f);
            _presetConfigurationPrepared = false;
            EnsurePresetConfiguration();
            ApplyCameraSettingsImmediate();
        }

        public void ToggleGameView()
        {
            SetGameViewActive(!_gameViewActive);
        }

        public void SetGameViewActive(bool active)
        {
            _gameViewActive = active;
            ResolveReferences();
            EnsurePresetConfiguration();
            CaptureGameCameraSettings();
            ApplyCameraSettingsImmediate();
            ApplyCameraRenderMode();

            if (active)
            {
                TryRefreshHeading(1f);
                SnapToCurrentPresetIfPossible();
            }
        }

        public bool SelectViewPreset(string presetName)
        {
            EnsurePresetConfiguration();
            if (!CameraViewPresetMath.TryFindUniquePreset(
                    viewPresets,
                    presetName,
                    out var preset,
                    out var index,
                    out var failureReason))
            {
                presetSelectionDiagnostic = failureReason;
                return false;
            }

            _selectedPresetIndex = index;
            selectedPresetDiagnostic = preset.presetName.Trim();
            presetSelectionDiagnostic = "Ready";
            focusTargetDiagnostic = "Unresolved";
            _hasFocusResolution = false;
            return true;
        }

        private void LateUpdate()
        {
            if (!_gameViewActive)
            {
                return;
            }

            ResolveReferences();
            EnsurePresetConfiguration();
            var playerRoot = ResolvePlayerRoot();
            if (playerRoot == null)
            {
                focusTargetDiagnostic = "Unavailable: avatar root";
                _hasFocusResolution = false;
                return;
            }

            var dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            TryRefreshHeading(dt);

            if (!_hasHeading)
            {
                // No mapped heading has ever been available. Preserve the current camera transform
                // rather than snapping to a hard-coded global direction.
                focusTargetDiagnostic = "Waiting for retained body heading";
                return;
            }

            var preset = CurrentPreset;
            if (preset == null)
            {
                focusTargetDiagnostic = "Unavailable: no valid preset";
                return;
            }

            if (!TryResolveFocus(playerRoot, preset.focusSemantic, out var focusResolution))
            {
                focusTargetDiagnostic = $"Unavailable: {preset.focusSemantic}";
                _hasFocusResolution = false;
                return;
            }

            if (!CameraViewPresetMath.TryCalculateView(
                    _smoothedHeading,
                    focusResolution.Position,
                    preset,
                    out var targetPosition,
                    out var lookTarget))
            {
                focusTargetDiagnostic = "Unavailable: invalid preset geometry";
                return;
            }

            var positionAlpha = CameraViewPresetMath.ResponseAlpha(
                preset.positionResponse,
                dt);
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
                var orientationAlpha = CameraViewPresetMath.ResponseAlpha(
                    preset.orientationResponse,
                    dt);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    orientationAlpha);
            }

            if (controlledCamera != null)
            {
                var fovAlpha = CameraViewPresetMath.ResponseAlpha(
                    preset.fieldOfViewResponse,
                    dt);
                controlledCamera.fieldOfView = Mathf.Lerp(
                    controlledCamera.fieldOfView,
                    preset.fieldOfView,
                    fovAlpha);
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

        private void EnsurePresetConfiguration()
        {
            if (_presetConfigurationPrepared)
            {
                return;
            }

            if (!cameraPresetDefaultsInitialized ||
                viewPresets == null ||
                viewPresets.Length == 0)
            {
                viewPresets = CameraViewPreset.CreateDefaults(
                    followDistance,
                    cameraHeight,
                    lookHeight,
                    positionResponse,
                    fieldOfView);
                cameraPresetDefaultsInitialized = true;
            }

            for (var i = 0; i < viewPresets.Length; i++)
            {
                viewPresets[i]?.Sanitize();
            }

            initialViewPreset = string.IsNullOrWhiteSpace(initialViewPreset)
                ? "Back"
                : initialViewPreset.Trim();
            _presetConfigurationPrepared = true;
            ResolveInitialPreset();
        }

        private void ResolveInitialPreset()
        {
            if (_selectedPresetIndex >= 0 &&
                viewPresets != null &&
                _selectedPresetIndex < viewPresets.Length &&
                viewPresets[_selectedPresetIndex] != null)
            {
                selectedPresetDiagnostic = viewPresets[_selectedPresetIndex].presetName;
                return;
            }

            if (CameraViewPresetMath.TryFindUniquePreset(
                    viewPresets,
                    initialViewPreset,
                    out var preset,
                    out var index,
                    out _))
            {
                _selectedPresetIndex = index;
                selectedPresetDiagnostic = preset.presetName;
                presetSelectionDiagnostic = "Ready";
                return;
            }

            if (CameraViewPresetMath.TryFindUniquePreset(
                    viewPresets,
                    "Back",
                    out preset,
                    out index,
                    out _))
            {
                _selectedPresetIndex = index;
                selectedPresetDiagnostic = preset.presetName;
                presetSelectionDiagnostic = "Ready";
                return;
            }

            _selectedPresetIndex = FindFirstUniquelyNamedPreset();
            if (_selectedPresetIndex >= 0)
            {
                selectedPresetDiagnostic = viewPresets[_selectedPresetIndex].presetName;
                presetSelectionDiagnostic = "Ready";
            }
            else
            {
                selectedPresetDiagnostic = "(none)";
                presetSelectionDiagnostic = "No valid uniquely named camera preset is configured";
            }
        }

        private int FindFirstUniquelyNamedPreset()
        {
            if (viewPresets == null)
            {
                return -1;
            }

            for (var i = 0; i < viewPresets.Length; i++)
            {
                var candidate = viewPresets[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.presetName))
                {
                    continue;
                }
                if (CameraViewPresetMath.TryFindUniquePreset(
                        viewPresets,
                        candidate.presetName,
                        out _,
                        out var index,
                        out _))
                {
                    return index;
                }
            }
            return -1;
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

        private bool TryResolveFocus(
            Transform playerRoot,
            CameraFocusSemantic semantic,
            out CameraFocusResolution resolution)
        {
            var candidates = new CameraFocusCandidates();
            AssignCandidate(playerRoot, out candidates.hasAvatarRoot, out candidates.avatarRoot);

            if (binding != null)
            {
                AssignCandidate(
                    binding.GetBoneTransform(CanonicalBoneId.Chest),
                    out candidates.hasChest,
                    out candidates.chest);
                AssignCandidate(
                    binding.GetBoneTransform(CanonicalBoneId.Pelvis),
                    out candidates.hasPelvis,
                    out candidates.pelvis);
                AssignCandidate(
                    binding.GetBoneTransform(CanonicalBoneId.LeftLowerArm),
                    out candidates.hasLeftLowerArm,
                    out candidates.leftLowerArm);
                AssignCandidate(
                    binding.GetBoneTransform(CanonicalBoneId.RightLowerArm),
                    out candidates.hasRightLowerArm,
                    out candidates.rightLowerArm);
                AssignCandidate(
                    binding.GetChainTip(CanonicalKinematicChainId.LeftArm),
                    out candidates.hasLeftHand,
                    out candidates.leftHand);
                AssignCandidate(
                    binding.GetChainTip(CanonicalKinematicChainId.RightArm),
                    out candidates.hasRightHand,
                    out candidates.rightHand);
            }

            if (!CameraViewPresetMath.TryResolveFocus(
                    semantic,
                    candidates,
                    out resolution))
            {
                return false;
            }

            _lastFocusResolution = resolution;
            _hasFocusResolution = true;
            focusTargetDiagnostic = resolution.UsedFallback
                ? $"{semantic} -> {resolution.ResolvedTarget} (fallback)"
                : $"{semantic} -> {resolution.ResolvedTarget}";
            return true;
        }

        private static void AssignCandidate(
            Transform target,
            out bool available,
            out Vector3 position)
        {
            available = target != null && CameraViewPresetMath.IsFinite(target.position);
            position = available ? target.position : Vector3.zero;
        }

        private void SnapToCurrentPresetIfPossible()
        {
            var playerRoot = ResolvePlayerRoot();
            var preset = CurrentPreset;
            if (playerRoot == null || !_hasHeading || preset == null)
            {
                return;
            }

            if (!TryResolveFocus(playerRoot, preset.focusSemantic, out var focusResolution) ||
                !CameraViewPresetMath.TryCalculateView(
                    _smoothedHeading,
                    focusResolution.Position,
                    preset,
                    out var targetPosition,
                    out var lookTarget))
            {
                return;
            }

            transform.position = targetPosition;
            var lookDirection = lookTarget - transform.position;
            if (lookDirection.sqrMagnitude > 0.000001f)
            {
                transform.rotation = Quaternion.LookRotation(
                    lookDirection.normalized,
                    Vector3.up);
            }

            if (controlledCamera != null)
            {
                controlledCamera.fieldOfView = preset.fieldOfView;
            }
        }

        private void ApplyCameraSettingsImmediate()
        {
            if (controlledCamera == null)
            {
                return;
            }

            var preset = CurrentPreset;
            controlledCamera.fieldOfView = preset == null
                ? fieldOfView
                : preset.fieldOfView;
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
            var cameraRotation = controlledCamera.transform.rotation;
            if (!TryNormalizeQuaternion(ref cameraRotation))
            {
                // An invalid camera rotation cannot preserve a meaningful orientation. Use a
                // known-valid fallback before allowing the Camera back into the render path.
                cameraRotation = Quaternion.identity;
            }

            controlledCamera.transform.rotation = cameraRotation;
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

        private static bool TryNormalizeQuaternion(ref Quaternion rotation)
        {
            if (!IsFinite(rotation.x) ||
                !IsFinite(rotation.y) ||
                !IsFinite(rotation.z) ||
                !IsFinite(rotation.w))
            {
                return false;
            }

            var magnitudeSquared =
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w;
            if (!IsFinite(magnitudeSquared) || magnitudeSquared <= 0.000000000001f)
            {
                return false;
            }

            var magnitude = Mathf.Sqrt(magnitudeSquared);
            if (!IsFinite(magnitude) || magnitude <= 0.000001f)
            {
                return false;
            }

            rotation = new Quaternion(
                rotation.x / magnitude,
                rotation.y / magnitude,
                rotation.z / magnitude,
                rotation.w / magnitude);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
