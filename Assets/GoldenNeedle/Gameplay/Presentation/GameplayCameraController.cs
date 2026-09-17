using System;
using GoldenNeedle.Core.Commands;
using GoldenNeedle.Debug.PoseTrackingSpike;
using GoldenNeedle.Gameplay.Player;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Presentation
{
    /// <summary>
    /// Scene-local production camera authority. It follows only the persistent Golden Needle
    /// player and reuses the retained Foundation B camera-preset geometry and fallback rules.
    /// Calibration intentionally does not use this component.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class GameplayCameraController : MonoBehaviour
    {
        [Header("Runtime wiring")]
        [SerializeField] private Camera controlledCamera;

        [Header("Camera view presets")]
        [SerializeField] private string initialViewPreset = "Back";
        [SerializeField] private CameraViewPreset[] viewPresets = CameraViewPreset.CreateDefaults();

        [Header("Diagnostics")]
        [SerializeField] private string selectedPresetDiagnostic = "Back";
        [SerializeField] private string focusTargetDiagnostic = "Waiting for persistent player";
        [SerializeField] private string presetSelectionDiagnostic = "Ready";

        private GoldenNeedlePlayerFacade _facade;
        private int _selectedPresetIndex = -1;
        private bool _presetConfigurationPrepared;
        private bool _hasHeading;
        private Vector2 _smoothedHeading;
        private bool _hasAppliedFirstPose;
        private Func<string, bool> _registeredPresetSelector;

        public string CurrentViewPresetName => selectedPresetDiagnostic;
        public string ResolvedFocusTarget => focusTargetDiagnostic;
        public string LastPresetSelectionError => presetSelectionDiagnostic == "Ready"
            ? string.Empty
            : presetSelectionDiagnostic;

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
            ResolveCamera();
            EnsurePresetConfiguration();
        }

        private void OnEnable()
        {
            _registeredPresetSelector = SelectViewPreset;
            GoldenNeedleCommandRuntimeServices.RegisterCameraViewPresetSelector(
                _registeredPresetSelector);
        }

        private void OnDisable()
        {
            UnregisterPresetSelector();
        }

        private void OnDestroy()
        {
            UnregisterPresetSelector();
        }

        private void OnValidate()
        {
            ResolveCamera();
            _presetConfigurationPrepared = false;
            EnsurePresetConfiguration();
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
            return true;
        }

        private void LateUpdate()
        {
            ResolveCamera();
            EnsurePresetConfiguration();
            if (controlledCamera == null)
            {
                focusTargetDiagnostic = "Unavailable: camera";
                return;
            }

            if (!TryResolvePersistentPlayer(out var facade))
            {
                focusTargetDiagnostic = "Waiting for persistent player";
                return;
            }

            var playerRoot = facade.PlayerRoot;
            if (playerRoot == null)
            {
                focusTargetDiagnostic = "Unavailable: player root";
                return;
            }

            var deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            RefreshHeading(facade, playerRoot, deltaTime);

            var preset = CurrentPreset;
            if (preset == null)
            {
                focusTargetDiagnostic = "Unavailable: no valid preset";
                return;
            }

            if (!TryResolveFocus(facade, playerRoot, preset.focusSemantic, out var focusResolution) ||
                !CameraViewPresetMath.TryCalculateView(
                    _smoothedHeading,
                    focusResolution.Position,
                    preset,
                    out var targetPosition,
                    out var lookTarget))
            {
                focusTargetDiagnostic = $"Unavailable: {preset.focusSemantic}";
                return;
            }

            if (!_hasAppliedFirstPose)
            {
                ApplyPoseImmediate(targetPosition, lookTarget, preset.fieldOfView);
                _hasAppliedFirstPose = true;
                return;
            }

            var positionAlpha = CameraViewPresetMath.ResponseAlpha(
                preset.positionResponse,
                deltaTime);
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
                    deltaTime);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    orientationAlpha);
            }

            var fieldOfViewAlpha = CameraViewPresetMath.ResponseAlpha(
                preset.fieldOfViewResponse,
                deltaTime);
            controlledCamera.fieldOfView = Mathf.Lerp(
                controlledCamera.fieldOfView,
                preset.fieldOfView,
                fieldOfViewAlpha);
        }

        private void ResolveCamera()
        {
            controlledCamera = controlledCamera == null
                ? GetComponent<Camera>()
                : controlledCamera;
        }

        private bool TryResolvePersistentPlayer(out GoldenNeedlePlayerFacade facade)
        {
            var session = GoldenNeedlePlayerSession.Instance;
            var candidate = session == null || !session.IsPersistentInstance
                ? null
                : session.GetComponent<GoldenNeedlePlayerFacade>();

            if (candidate != _facade)
            {
                _facade = candidate;
                _hasHeading = false;
                _hasAppliedFirstPose = false;
            }

            facade = _facade;
            return facade != null;
        }

        private void RefreshHeading(
            GoldenNeedlePlayerFacade facade,
            Transform playerRoot,
            float deltaTime)
        {
            if (facade.HasWorldHeading &&
                CameraViewPresetMath.IsFinite(facade.WorldHeadingXZ) &&
                facade.WorldHeadingXZ.sqrMagnitude > 0.000001f)
            {
                var targetHeading = facade.WorldHeadingXZ.normalized;
                if (!_hasHeading)
                {
                    _smoothedHeading = targetHeading;
                    _hasHeading = true;
                    return;
                }

                var alpha = CameraViewPresetMath.ResponseAlpha(8f, deltaTime);
                var blended = Vector2.Lerp(_smoothedHeading, targetHeading, alpha);
                if (blended.sqrMagnitude > 0.000001f)
                {
                    _smoothedHeading = blended.normalized;
                }
                return;
            }

            if (_hasHeading)
            {
                return;
            }

            var rootForward = playerRoot == null ? Vector3.zero : playerRoot.forward;
            var fallbackHeading = new Vector2(rootForward.x, rootForward.z);
            if (!CameraViewPresetMath.IsFinite(fallbackHeading) ||
                fallbackHeading.sqrMagnitude <= 0.000001f)
            {
                // Back presets orbit along negative forward, so +Z is the stable world-forward
                // convention when the avatar has not supplied a body heading yet.
                fallbackHeading = new Vector2(0f, 1f);
            }

            _smoothedHeading = fallbackHeading.normalized;
            _hasHeading = true;
        }

        private bool TryResolveFocus(
            GoldenNeedlePlayerFacade facade,
            Transform playerRoot,
            CameraFocusSemantic semantic,
            out CameraFocusResolution resolution)
        {
            var candidates = new CameraFocusCandidates();
            AssignCandidate(
                playerRoot,
                out candidates.hasAvatarRoot,
                out candidates.avatarRoot);
            AssignCandidate(
                facade.LeftWristAnchor,
                out candidates.hasLeftHand,
                out candidates.leftHand);
            AssignCandidate(
                facade.RightWristAnchor,
                out candidates.hasRightHand,
                out candidates.rightHand);

            if (!CameraViewPresetMath.TryResolveFocus(
                    semantic,
                    candidates,
                    out resolution))
            {
                return false;
            }

            focusTargetDiagnostic = resolution.UsedFallback
                ? $"{semantic} -> {resolution.ResolvedTarget} (fallback)"
                : $"{semantic} -> {resolution.ResolvedTarget}";
            return true;
        }

        private void ApplyPoseImmediate(
            Vector3 targetPosition,
            Vector3 lookTarget,
            float targetFieldOfView)
        {
            transform.position = targetPosition;
            var lookDirection = lookTarget - targetPosition;
            if (lookDirection.sqrMagnitude > 0.000001f)
            {
                transform.rotation = Quaternion.LookRotation(
                    lookDirection.normalized,
                    Vector3.up);
            }
            controlledCamera.fieldOfView = targetFieldOfView;
        }

        private void EnsurePresetConfiguration()
        {
            if (_presetConfigurationPrepared)
            {
                return;
            }

            if (viewPresets == null || viewPresets.Length == 0)
            {
                viewPresets = CameraViewPreset.CreateDefaults();
            }

            for (var index = 0; index < viewPresets.Length; index++)
            {
                viewPresets[index]?.Sanitize();
            }

            initialViewPreset = string.IsNullOrWhiteSpace(initialViewPreset)
                ? "Back"
                : initialViewPreset.Trim();
            _presetConfigurationPrepared = true;

            if (CameraViewPresetMath.TryFindUniquePreset(
                    viewPresets,
                    initialViewPreset,
                    out var preset,
                    out var presetIndex,
                    out _))
            {
                _selectedPresetIndex = presetIndex;
                selectedPresetDiagnostic = preset.presetName;
                presetSelectionDiagnostic = "Ready";
                return;
            }

            if (CameraViewPresetMath.TryFindUniquePreset(
                    viewPresets,
                    "Back",
                    out preset,
                    out presetIndex,
                    out var failureReason))
            {
                _selectedPresetIndex = presetIndex;
                selectedPresetDiagnostic = preset.presetName;
                presetSelectionDiagnostic = "Ready";
                return;
            }

            _selectedPresetIndex = -1;
            selectedPresetDiagnostic = "(none)";
            presetSelectionDiagnostic = failureReason;
        }

        private void UnregisterPresetSelector()
        {
            if (_registeredPresetSelector == null)
            {
                return;
            }

            GoldenNeedleCommandRuntimeServices.UnregisterCameraViewPresetSelector(
                _registeredPresetSelector);
            _registeredPresetSelector = null;
        }

        private static void AssignCandidate(
            Transform target,
            out bool available,
            out Vector3 position)
        {
            available = target != null && CameraViewPresetMath.IsFinite(target.position);
            position = available ? target.position : Vector3.zero;
        }
    }
}
