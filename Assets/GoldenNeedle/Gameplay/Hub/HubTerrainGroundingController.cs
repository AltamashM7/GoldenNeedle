using System;
using System.Collections;
using GoldenNeedle.Gameplay.Flow;
using GoldenNeedle.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldenNeedle.Gameplay.Hub
{
    /// <summary>
    /// Scene-local terrain conformance for Hub. EmbodiedLocomotionController remains the sole
    /// owner of X/Z translation and semantic Jump/Crouch motion; this component adjusts only the
    /// final player-root world Y against the assigned Hub TerrainCollider.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class HubTerrainGroundingController : MonoBehaviour
    {
        [Header("Assigned Hub ground")]
        [Tooltip("The Hub TerrainCollider used for all ground samples. Other scene colliders are ignored.")]
        [SerializeField] private TerrainCollider groundCollider;

        [Tooltip("Height above the player root from which the downward terrain ray starts.")]
        [Min(0.1f), SerializeField] private float rayStartHeight = 4f;

        [Tooltip("Maximum downward distance used when sampling the assigned terrain.")]
        [Min(0.1f), SerializeField] private float raycastDistance = 50f;

        [Tooltip("Delay used only when Hub is opened directly outside the normal GameFlow transition.")]
        [Min(0f), SerializeField] private float directSceneFallbackDelaySeconds = 0.5f;

        [Tooltip("Horizontal movement larger than this is treated as an explicit scene/spawn relocation and recaptures the neutral terrain offset.")]
        [Min(0.1f), SerializeField] private float relocationTeleportThreshold = 4f;

        [Tooltip("How long a missing terrain hit may persist before one bounded diagnostic warning is emitted.")]
        [Min(0f), SerializeField] private float missingGroundGraceSeconds = 0.25f;

        private GameFlowManager _gameFlowManager;
        private GoldenNeedlePlayerFacade _playerFacade;
        private Transform _playerRoot;
        private Coroutine _initializeCoroutine;
        private bool _flowSubscribed;
        private bool _observedTransition;
        private bool _groundingReady;
        private bool _hasGroundReference;
        private float _rootToGroundOffset;
        private Vector3 _previousRootPosition;
        private bool _hasPreviousRootPosition;
        private float _missingGroundSeconds;
        private bool _missingGroundDiagnosticEmitted;
        private float _initialGroundWaitSeconds;
        private bool _initialGroundDiagnosticEmitted;

        public bool HasGroundReference => _hasGroundReference;
        public float RootToGroundOffset => _rootToGroundOffset;
        public float LastGroundY { get; private set; }
        public float LastTargetY { get; private set; }

        private void OnEnable()
        {
            _groundingReady = false;
            _hasGroundReference = false;
            _hasPreviousRootPosition = false;
            _missingGroundSeconds = 0f;
            _missingGroundDiagnosticEmitted = false;
            _initialGroundWaitSeconds = 0f;
            _initialGroundDiagnosticEmitted = false;
            _observedTransition = false;
            ResolveReferences();
            _initializeCoroutine = StartCoroutine(InitializeWhenPlaced());
        }

        private void OnDisable()
        {
            if (_initializeCoroutine != null)
            {
                StopCoroutine(_initializeCoroutine);
                _initializeCoroutine = null;
            }

            UnsubscribeFromFlow();
            _groundingReady = false;
            _hasGroundReference = false;
            _hasPreviousRootPosition = false;
        }

        private void LateUpdate()
        {
            if (!_groundingReady || groundCollider == null)
            {
                return;
            }

            ResolveReferences();
            if (_playerFacade == null || !TryResolvePlayerRoot())
            {
                return;
            }

            var currentPosition = _playerRoot.position;
            if (!_hasGroundReference)
            {
                if (!TryCaptureGroundReference())
                {
                    RecordPreviousRootPosition(currentPosition);
                    return;
                }

                currentPosition = _playerRoot.position;
            }

            if (_hasPreviousRootPosition && HasRelocated(currentPosition, _previousRootPosition))
            {
                // GameFlow placement and other explicit scene relocations happen outside the
                // locomotion loop. Capture the authored root/ground relationship at the new spot.
                _hasGroundReference = false;
                if (!TryCaptureGroundReference())
                {
                    RecordPreviousRootPosition(currentPosition);
                    return;
                }

                currentPosition = _playerRoot.position;
            }

            if (!_playerFacade.IsLocomotionEnabled)
            {
                // Hub authority may take one frame to enable after scene activation. Do not
                // fight the accepted controller while it is intentionally disabled.
                RecordPreviousRootPosition(currentPosition);
                return;
            }

            if (!TrySampleGround(currentPosition, out var groundY))
            {
                HandleMissingGroundSample(currentPosition);
                return;
            }

            _missingGroundSeconds = 0f;
            var semanticVerticalOffset = GetSemanticVerticalOffset();
            var targetY = groundY + _rootToGroundOffset + semanticVerticalOffset;
            if (!IsFinite(targetY))
            {
                RecordPreviousRootPosition(currentPosition);
                return;
            }

            LastGroundY = groundY;
            LastTargetY = targetY;
            currentPosition.y = targetY;
            _playerRoot.position = currentPosition;
            RecordPreviousRootPosition(currentPosition);
        }

        private IEnumerator InitializeWhenPlaced()
        {
            var directFallbackElapsed = 0f;
            while (isActiveAndEnabled && !_groundingReady)
            {
                ResolveReferences();
                if (_playerFacade == null || !TryResolvePlayerRoot())
                {
                    yield return null;
                    continue;
                }

                if (_gameFlowManager != null && _gameFlowManager.IsTransitioning)
                {
                    _observedTransition = true;
                    directFallbackElapsed = 0f;
                    yield return null;
                    continue;
                }

                if (_observedTransition && !HasCompletedHubTransition())
                {
                    yield return null;
                    continue;
                }

                var completedHubTransition = HasCompletedHubTransition();
                if (!completedHubTransition)
                {
                    directFallbackElapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                    if (directFallbackElapsed < Mathf.Max(0f, directSceneFallbackDelaySeconds))
                    {
                        yield return null;
                        continue;
                    }
                }

                if (TryCaptureGroundReference())
                {
                    _groundingReady = true;
                    _initializeCoroutine = null;
                    yield break;
                }

                _initialGroundWaitSeconds += Mathf.Max(0f, Time.unscaledDeltaTime);
                if (!_initialGroundDiagnosticEmitted &&
                    _initialGroundWaitSeconds >= Mathf.Max(0f, missingGroundGraceSeconds))
                {
                    _initialGroundDiagnosticEmitted = true;
                    UnityEngine.Debug.LogWarning(
                        groundCollider == null
                            ? "[GoldenNeedle Hub] Terrain grounding is waiting for its assigned TerrainCollider; no fallback ground source will be used."
                            : "[GoldenNeedle Hub] Terrain grounding could not establish a baseline on the assigned TerrainCollider; placement Y remains unchanged until a terrain hit is available.",
                        this);
                }

                yield return null;
            }

            _initializeCoroutine = null;
        }

        private void ResolveReferences()
        {
            if (_gameFlowManager == null)
            {
                _gameFlowManager = GameFlowManager.Instance;
                if (_gameFlowManager != null && !_flowSubscribed)
                {
                    _gameFlowManager.TransitionCompleted += OnTransitionCompleted;
                    _flowSubscribed = true;
                }
            }

            if (_playerFacade == null)
            {
                var session = GoldenNeedlePlayerSession.Instance;
                _playerFacade = session == null
                    ? null
                    : session.GetComponent<GoldenNeedlePlayerFacade>();
            }
        }

        private bool TryResolvePlayerRoot()
        {
            if (_playerFacade == null)
            {
                return false;
            }

            var candidate = _playerFacade.PlayerRoot;
            if (candidate == null)
            {
                return false;
            }

            if (_playerRoot != candidate)
            {
                _playerRoot = candidate;
                _groundingReady = false;
                _hasGroundReference = false;
                _hasPreviousRootPosition = false;
            }

            return true;
        }

        private void OnTransitionCompleted(string destinationSceneName, string _)
        {
            if (!isActiveAndEnabled ||
                !string.Equals(
                    SceneManager.GetActiveScene().name,
                    destinationSceneName,
                    StringComparison.Ordinal))
            {
                return;
            }

            _observedTransition = true;
            ResolveReferences();
            if (TryResolvePlayerRoot() && TryCaptureGroundReference())
            {
                _groundingReady = true;
            }
        }

        private bool HasCompletedHubTransition()
        {
            return _gameFlowManager != null &&
                   _gameFlowManager.State == GameFlowTransitionState.Completed &&
                   string.Equals(
                       _gameFlowManager.LastCompletedSceneName,
                       SceneManager.GetActiveScene().name,
                       StringComparison.Ordinal);
        }

        private bool TryCaptureGroundReference()
        {
            if (_playerRoot == null || !TrySampleGround(_playerRoot.position, out var groundY))
            {
                return false;
            }

            _rootToGroundOffset = _playerRoot.position.y - groundY;
            _hasGroundReference = true;
            _missingGroundSeconds = 0f;
            LastGroundY = groundY;
            LastTargetY = _playerRoot.position.y;
            RecordPreviousRootPosition(_playerRoot.position);
            return true;
        }

        private bool TrySampleGround(Vector3 rootPosition, out float groundY)
        {
            groundY = 0f;
            if (groundCollider == null ||
                !groundCollider.enabled ||
                !groundCollider.gameObject.activeInHierarchy)
            {
                return false;
            }

            var rayOrigin = rootPosition + Vector3.up * Mathf.Max(0.1f, rayStartHeight);
            if (!groundCollider.Raycast(
                    new Ray(rayOrigin, Vector3.down),
                    out var hit,
                    Mathf.Max(0.1f, raycastDistance)))
            {
                return false;
            }

            groundY = hit.point.y;
            return IsFinite(groundY);
        }

        private float GetSemanticVerticalOffset()
        {
            if (_playerFacade == null || !_playerFacade.HasLocomotionVerticalOrigin)
            {
                return 0f;
            }

            var finalY = _playerFacade.LocomotionFinalWorldPositionY;
            var originY = _playerFacade.LocomotionVerticalOriginY;
            return IsFinite(finalY) && IsFinite(originY) ? finalY - originY : 0f;
        }

        private void HandleMissingGroundSample(Vector3 currentPosition)
        {
            _missingGroundSeconds += Mathf.Max(0f, Time.unscaledDeltaTime);
            if (!_missingGroundDiagnosticEmitted &&
                _missingGroundSeconds >= Mathf.Max(0f, missingGroundGraceSeconds))
            {
                _missingGroundDiagnosticEmitted = true;
                UnityEngine.Debug.LogWarning(
                    "[GoldenNeedle Hub] Terrain grounding could not sample the assigned " +
                    "TerrainCollider; accepted locomotion Y was preserved for this frame.",
                    this);
            }

            // Deliberately leave the accepted locomotion Y untouched on a missing-ground frame.
            RecordPreviousRootPosition(currentPosition);
        }

        private bool HasRelocated(Vector3 currentPosition, Vector3 previousPosition)
        {
            var currentXZ = new Vector2(currentPosition.x, currentPosition.z);
            var previousXZ = new Vector2(previousPosition.x, previousPosition.z);
            var threshold = Mathf.Max(0.1f, relocationTeleportThreshold);
            return (currentXZ - previousXZ).sqrMagnitude > threshold * threshold;
        }

        private void RecordPreviousRootPosition(Vector3 position)
        {
            _previousRootPosition = position;
            _hasPreviousRootPosition = true;
        }

        private void UnsubscribeFromFlow()
        {
            if (!_flowSubscribed || _gameFlowManager == null)
            {
                return;
            }

            _gameFlowManager.TransitionCompleted -= OnTransitionCompleted;
            _flowSubscribed = false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
