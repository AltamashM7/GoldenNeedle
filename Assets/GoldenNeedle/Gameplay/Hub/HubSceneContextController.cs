using System.Collections;
using GoldenNeedle.Gameplay.Commands;
using GoldenNeedle.Gameplay.Player;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Hub
{
    /// <summary>
    /// Scene-local Hub entry hook for the persistent production command host.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubSceneContextController : MonoBehaviour
    {
        [Header("Motion readiness")]
        [Tooltip("Bounded time to allow tracking and retarget runtime state to become live before one diagnostic warning is emitted.")]
        [Min(0.1f), SerializeField] private float readinessGraceSeconds = 2f;

        private bool _applied;
        private bool _diagnosticEmitted;
        private bool _bindingRecoveryAttempted;
        private Coroutine _applyCoroutine;
        private GameplayCommandHost _gameplayCommandHost;
        private GoldenNeedlePlayerFacade _playerFacade;

        public bool IsApplied => _applied;

        private void OnEnable()
        {
            _applied = false;
            _diagnosticEmitted = false;
            _bindingRecoveryAttempted = false;
            _applyCoroutine = StartCoroutine(ApplyWhenAvailable());
        }

        private void OnDisable()
        {
            if (_applyCoroutine == null)
            {
                return;
            }

            StopCoroutine(_applyCoroutine);
            _applyCoroutine = null;
        }

        private IEnumerator ApplyWhenAvailable()
        {
            var elapsed = 0f;
            while (isActiveAndEnabled)
            {
                ResolveReferences();
                if (_playerFacade == null)
                {
                    yield return null;
                    continue;
                }

                _gameplayCommandHost?.SetContext(GameplayCommandContext.Hub);
                if (!_bindingRecoveryAttempted)
                {
                    _bindingRecoveryAttempted = true;
                    _playerFacade.TryEnsureRigBinding();
                }

                // These are scene authority requests, not a replacement motion owner. Setters
                // are idempotent and remain explicit while the bounded readiness window runs.
                _playerFacade.SetAvatarAnimationAuthorityEnabled(false);
                _playerFacade.SetAvatarPoseDriveEnabled(true);
                _playerFacade.SetLocomotionEnabled(true);

                if (HasStructuralMotionControl(_playerFacade))
                {
                    _applied = true;
                }

                if (HasRuntimeMotionReadiness(_playerFacade))
                {
                    _applyCoroutine = null;
                    yield break;
                }

                if (elapsed >= Mathf.Max(0.1f, readinessGraceSeconds))
                {
                    EmitReadinessDiagnostic(_playerFacade, elapsed);
                    _applyCoroutine = null;
                    yield break;
                }

                elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                yield return null;
            }

            _applyCoroutine = null;
        }

        private void ResolveReferences()
        {
            if (_gameplayCommandHost == null)
            {
                _gameplayCommandHost = FindAnyObjectByType<GameplayCommandHost>();
            }

            if (_playerFacade != null)
            {
                return;
            }

            var session = GoldenNeedlePlayerSession.Instance;
            _playerFacade = session == null
                ? null
                : session.GetComponent<GoldenNeedlePlayerFacade>();
        }

        private static bool HasStructuralMotionControl(GoldenNeedlePlayerFacade facade)
        {
            return facade != null &&
                   !facade.IsAvatarAnimationAuthorityEnabled &&
                   facade.IsAvatarPoseDriveEnabled &&
                   facade.IsLocomotionEnabled &&
                   facade.IsRigBound;
        }

        private static bool HasRuntimeMotionReadiness(GoldenNeedlePlayerFacade facade)
        {
            return HasStructuralMotionControl(facade) &&
                   facade.IsCalibrationComplete &&
                   facade.IsBodyTrackingAvailable &&
                   facade.AreRetargetTargetsLive &&
                   facade.RetargetIkChainsSolved > 0;
        }

        private void EmitReadinessDiagnostic(GoldenNeedlePlayerFacade facade, float elapsed)
        {
            if (_diagnosticEmitted || facade == null)
            {
                return;
            }

            _diagnosticEmitted = true;
            UnityEngine.Debug.LogWarning(
                $"[GoldenNeedle Hub] Motion readiness not confirmed after {elapsed:0.00}s: " +
                $"calibrationUsable={facade.IsCalibrationUsable}, " +
                $"calibrationComplete={facade.IsCalibrationComplete}, " +
                $"bodyTrackingAvailable={facade.IsBodyTrackingAvailable}, " +
                $"rigBound={facade.IsRigBound}, " +
                $"bindingMode={facade.RigBindingModeName}, " +
                $"externalAnimationAuthority={facade.IsAvatarAnimationAuthorityEnabled}, " +
                $"poseDrive={facade.IsAvatarPoseDriveEnabled}, " +
                $"locomotionDrive={facade.IsLocomotionEnabled}, " +
                $"kinematicTargetsLive={facade.AreRetargetTargetsLive}, " +
                $"sourceChainsValid={facade.RetargetSourceChainsValid}, " +
                $"targetsGenerated={facade.RetargetTargetsGenerated}, " +
                $"ikChainsSolved={facade.RetargetIkChainsSolved}.",
                this);
        }
    }
}
