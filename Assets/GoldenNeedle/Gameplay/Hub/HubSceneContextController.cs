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

        [Header("Automatic tracking recovery")]
        [Tooltip("Time that completed-calibration Hub entry may observe unavailable body tracking before requesting the existing provider restart path.")]
        [Min(0.1f), SerializeField] private float trackingRecoveryDelaySeconds = 0.75f;
        [Tooltip("Bounded time during which a busy provider may accept the one recovery request once it becomes safe.")]
        [Min(0.1f), SerializeField] private float trackingRecoveryRequestWindowSeconds = 1f;
        [Tooltip("Additional readiness time after an accepted provider restart, added to the provider camera startup timeout.")]
        [Min(0.1f), SerializeField] private float postRecoveryReadinessMarginSeconds = 1f;

        private bool _applied;
        private bool _diagnosticEmitted;
        private bool _bindingRecoveryAttempted;
        private bool _trackingRecoveryWindowPrepared;
        private bool _trackingRecoveryRequested;
        private bool _trackingRecoveryAccepted;
        private Coroutine _applyCoroutine;
        private GameplayCommandHost _gameplayCommandHost;
        private GoldenNeedlePlayerFacade _playerFacade;

        public bool IsApplied => _applied;

        private void OnEnable()
        {
            _applied = false;
            _diagnosticEmitted = false;
            _bindingRecoveryAttempted = false;
            _trackingRecoveryWindowPrepared = false;
            _trackingRecoveryRequested = false;
            _trackingRecoveryAccepted = false;
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
            var readinessElapsed = 0f;
            var trackingUnavailableElapsed = 0f;
            var recoveryRequestElapsed = 0f;
            var readinessBudgetSeconds = Mathf.Max(0.1f, readinessGraceSeconds);
            while (isActiveAndEnabled)
            {
                ResolveReferences();
                if (_playerFacade == null)
                {
                    yield return null;
                    continue;
                }

                if (!_trackingRecoveryWindowPrepared)
                {
                    _playerFacade.BeginTrackingRecoveryWindow();
                    _trackingRecoveryWindowPrepared = true;
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

                var deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
                if (TryRecoverTrackingIfNeeded(
                        _playerFacade,
                        deltaTime,
                        ref trackingUnavailableElapsed,
                        ref recoveryRequestElapsed))
                {
                    _trackingRecoveryAccepted = true;
                    readinessElapsed = 0f;
                    readinessBudgetSeconds = GetPostRecoveryReadinessBudget(_playerFacade);
                }

                if (readinessElapsed >= readinessBudgetSeconds)
                {
                    EmitReadinessDiagnostic(_playerFacade, readinessElapsed);
                    _applyCoroutine = null;
                    yield break;
                }

                readinessElapsed += deltaTime;
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

        private bool TryRecoverTrackingIfNeeded(
            GoldenNeedlePlayerFacade facade,
            float deltaTime,
            ref float trackingUnavailableElapsed,
            ref float recoveryRequestElapsed)
        {
            if (_trackingRecoveryAccepted ||
                facade == null ||
                !HasStructuralMotionControl(facade) ||
                !facade.IsCalibrationComplete)
            {
                trackingUnavailableElapsed = 0f;
                recoveryRequestElapsed = 0f;
                return false;
            }

            if (facade.IsBodyTrackingAvailable)
            {
                trackingUnavailableElapsed = 0f;
                recoveryRequestElapsed = 0f;
                return false;
            }

            trackingUnavailableElapsed += deltaTime;
            if (trackingUnavailableElapsed < Mathf.Max(0.1f, trackingRecoveryDelaySeconds))
            {
                return false;
            }

            recoveryRequestElapsed += deltaTime;
            if (recoveryRequestElapsed > Mathf.Max(0.1f, trackingRecoveryRequestWindowSeconds))
            {
                return false;
            }

            _trackingRecoveryRequested = true;
            return facade.TryRecoverTracking();
        }

        private float GetPostRecoveryReadinessBudget(GoldenNeedlePlayerFacade facade)
        {
            var providerStartupTimeout = facade == null
                ? 0f
                : facade.PoseProviderCameraStartupTimeoutSeconds;
            return Mathf.Max(
                Mathf.Max(0.1f, readinessGraceSeconds),
                providerStartupTimeout + Mathf.Max(0.1f, postRecoveryReadinessMarginSeconds));
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
                $"providerStatus={facade.ProviderStatus}, " +
                $"providerStatusMessage={facade.ProviderStatusMessage}, " +
                $"selectedCamera={facade.SelectedCameraName}, " +
                $"cameraTexture={facade.PoseProviderHasCameraTexture}, " +
                $"webcamPlaying={facade.PoseProviderCameraPlaying}, " +
                $"providerBootstrapping={facade.PoseProviderIsBootstrapping}, " +
                $"poseResultReceived={facade.PoseProviderHasReceivedResult}, " +
                $"latestPoseAgeMs={facade.LatestPoseAgeMilliseconds:0.0}, " +
                $"activeInferenceBackend={facade.ActiveInferenceBackendLabel}, " +
                $"rigBound={facade.IsRigBound}, " +
                $"bindingMode={facade.RigBindingModeName}, " +
                $"externalAnimationAuthority={facade.IsAvatarAnimationAuthorityEnabled}, " +
                $"poseDrive={facade.IsAvatarPoseDriveEnabled}, " +
                $"locomotionDrive={facade.IsLocomotionEnabled}, " +
                $"kinematicTargetsLive={facade.AreRetargetTargetsLive}, " +
                $"sourceChainsValid={facade.RetargetSourceChainsValid}, " +
                $"targetsGenerated={facade.RetargetTargetsGenerated}, " +
                $"ikChainsSolved={facade.RetargetIkChainsSolved}, " +
                $"trackingRecoveryRequested={_trackingRecoveryRequested && facade.TrackingRecoveryRequested}, " +
                $"trackingRecoveryAccepted={_trackingRecoveryAccepted && facade.TrackingRecoveryAccepted}, " +
                $"trackingRecoveryPerformed={facade.TrackingRecoveryPerformed}, " +
                $"trackingRecoveryRequests={facade.TrackingRecoveryRequestCount}, " +
                $"trackingRecoveryRestarts={facade.TrackingRecoveryRestartCount}.",
                this);
        }
    }
}
