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

        [Header("Camera continuity")]
        [Tooltip("Grace period for a completed-calibration Hub entry to observe a genuinely stale camera frame before attempting same-texture soft recovery.")]
        [Min(0.1f), SerializeField] private float trackingRecoveryDelaySeconds = 0.75f;
        [Tooltip("Bounded time to wait for a fresh frame after soft recovery of the existing WebCamTexture.")]
        [Min(0.1f), SerializeField] private float softCameraRecoveryWaitSeconds = 1f;
        [Tooltip("Bounded time during which the one hard fallback may be accepted once the provider becomes idle.")]
        [Min(0.1f), SerializeField] private float hardCameraRecoveryRequestWindowSeconds = 1f;
        [Tooltip("Fresh-camera and full-motion-readiness stabilization window after Hub entry or recovery.")]
        [Min(0.1f), SerializeField] private float stableReadinessWindowSeconds = 1f;
        [Tooltip("Additional readiness time after an accepted provider restart, added to the provider camera startup timeout.")]
        [Min(0.1f), SerializeField] private float postRecoveryReadinessMarginSeconds = 1f;

        private bool _applied;
        private bool _diagnosticEmitted;
        private bool _bindingRecoveryAttempted;
        private bool _continuityWindowPrepared;
        private bool _softCameraRecoveryRequested;
        private bool _hardCameraRecoveryRequested;
        private bool _hardCameraRecoveryAccepted;
        private Coroutine _applyCoroutine;
        private GameplayCommandHost _gameplayCommandHost;
        private GoldenNeedlePlayerFacade _playerFacade;

        public bool IsApplied => _applied;

        private void OnEnable()
        {
            _applied = false;
            _diagnosticEmitted = false;
            _bindingRecoveryAttempted = false;
            _continuityWindowPrepared = false;
            _softCameraRecoveryRequested = false;
            _hardCameraRecoveryRequested = false;
            _hardCameraRecoveryAccepted = false;
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
            var cameraUnavailableElapsed = 0f;
            var softRecoveryElapsed = 0f;
            var hardRecoveryElapsed = 0f;
            var stableReadinessElapsed = 0f;
            var readinessBudgetSeconds = Mathf.Max(0.1f, readinessGraceSeconds);
            var softRecoveryAttempted = false;
            var hardRecoveryAttempted = false;

            while (isActiveAndEnabled)
            {
                ResolveReferences();
                if (_playerFacade == null)
                {
                    yield return null;
                    continue;
                }

                if (!_continuityWindowPrepared)
                {
                    _playerFacade.BeginHubContinuityWindow();
                    _continuityWindowPrepared = true;
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

                var deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
                if (TryMaintainCameraContinuity(
                        _playerFacade,
                        deltaTime,
                        ref cameraUnavailableElapsed,
                        ref softRecoveryElapsed,
                        ref hardRecoveryElapsed,
                        ref softRecoveryAttempted,
                        ref hardRecoveryAttempted))
                {
                    readinessElapsed = 0f;
                    stableReadinessElapsed = 0f;
                    readinessBudgetSeconds = _hardCameraRecoveryAccepted
                        ? GetPostRecoveryReadinessBudget(_playerFacade)
                        : Mathf.Max(0.1f, readinessGraceSeconds);
                }

                var runtimeReady = HasRuntimeMotionReadiness(_playerFacade);
                var cameraAndProviderReady =
                    _playerFacade.PoseProviderCameraFrameFresh &&
                    _playerFacade.PoseProviderIsUsable;
                if (runtimeReady && cameraAndProviderReady)
                {
                    stableReadinessElapsed += deltaTime;
                    if (stableReadinessElapsed >= Mathf.Max(0.1f, stableReadinessWindowSeconds))
                    {
                        _applyCoroutine = null;
                        yield break;
                    }
                }
                else
                {
                    stableReadinessElapsed = 0f;
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

        private bool TryMaintainCameraContinuity(
            GoldenNeedlePlayerFacade facade,
            float deltaTime,
            ref float cameraUnavailableElapsed,
            ref float softRecoveryElapsed,
            ref float hardRecoveryElapsed,
            ref bool softRecoveryAttempted,
            ref bool hardRecoveryAttempted)
        {
            if (facade == null ||
                !HasStructuralMotionControl(facade) ||
                !facade.IsCalibrationComplete)
            {
                cameraUnavailableElapsed = 0f;
                softRecoveryElapsed = 0f;
                hardRecoveryElapsed = 0f;
                return false;
            }

            // A body/result gap is not a camera failure. If the provider is still seeing fresh
            // frames, leave its camera, OpenVINO runtime, worker, convention, and calibration alone.
            if (facade.PoseProviderCameraFrameFresh)
            {
                cameraUnavailableElapsed = 0f;
                softRecoveryElapsed = 0f;
                hardRecoveryElapsed = 0f;
                return false;
            }

            cameraUnavailableElapsed += deltaTime;
            if (_hardCameraRecoveryAccepted || hardRecoveryAttempted)
            {
                return false;
            }

            var continuityGrace = Mathf.Max(0.1f, trackingRecoveryDelaySeconds);
            if (!softRecoveryAttempted)
            {
                if (cameraUnavailableElapsed < continuityGrace)
                {
                    return false;
                }

                softRecoveryAttempted = true;
                _softCameraRecoveryRequested = true;
                softRecoveryElapsed = 0f;
                facade.TryBeginSoftCameraRecovery();
                return true;
            }

            if (!facade.SoftCameraRecoverySucceeded)
            {
                softRecoveryElapsed += deltaTime;
                if (softRecoveryElapsed < Mathf.Max(0.1f, softCameraRecoveryWaitSeconds))
                {
                    return false;
                }
            }
            else if (cameraUnavailableElapsed < continuityGrace)
            {
                // The one soft attempt succeeded earlier, but a later bounded stabilization
                // loss must still be observed for the same grace period before hard fallback.
                return false;
            }

            hardRecoveryElapsed += deltaTime;
            if (hardRecoveryElapsed > Mathf.Max(0.1f, hardCameraRecoveryRequestWindowSeconds))
            {
                return false;
            }

            _hardCameraRecoveryRequested = true;
            if (!facade.TryHardRecoverTracking())
            {
                return false;
            }

            hardRecoveryAttempted = true;
            _hardCameraRecoveryAccepted = facade.HardCameraRecoveryAccepted;
            return _hardCameraRecoveryAccepted;
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
                $"[GoldenNeedle Hub] Motion/camera continuity not confirmed after {elapsed:0.00}s: " +
                $"calibrationUsable={facade.IsCalibrationUsable}, " +
                $"calibrationComplete={facade.IsCalibrationComplete}, " +
                $"poseDrive={facade.IsAvatarPoseDriveEnabled}, " +
                $"locomotionDrive={facade.IsLocomotionEnabled}, " +
                $"bodyTrackingAvailable={facade.IsBodyTrackingAvailable}, " +
                $"providerStatus={facade.ProviderStatus}, " +
                $"providerStatusMessage={facade.ProviderStatusMessage}, " +
                $"selectedCamera={facade.SelectedCameraName}, " +
                $"cameraTexture={facade.PoseProviderHasCameraTexture}, " +
                $"webcamPlaying={facade.PoseProviderCameraPlaying}, " +
                $"cameraFramesPerSecond={facade.PoseProviderCameraFramesPerSecond:0.0}, " +
                $"hasSeenFreshCameraFrame={facade.PoseProviderHasSeenFreshCameraFrame}, " +
                $"latestCameraFrameAgeMs={facade.LatestCameraFrameAgeMilliseconds:0.0}, " +
                $"cameraFrameFresh={facade.PoseProviderCameraFrameFresh}, " +
                $"providerUsable={facade.PoseProviderIsUsable}, " +
                $"providerBootstrapping={facade.PoseProviderIsBootstrapping}, " +
                $"poseResultReceived={facade.PoseProviderHasReceivedResult}, " +
                $"latestPoseAgeMs={facade.LatestPoseAgeMilliseconds:0.0}, " +
                $"activeInferenceBackend={facade.ActiveInferenceBackendLabel}, " +
                $"activeBodyAcquisition={facade.ActiveBodyFrameAcquisitionModeLabel}, " +
                $"webCamCpuFallbackReason={facade.WebCamCpuAcquisitionFallbackReason}, " +
                $"openVinoRuntimeAvailable={facade.OpenVinoRuntimeAvailable}, " +
                $"openVinoWorkerTaskAvailable={facade.OpenVinoWorkerTaskAvailable}, " +
                $"openVinoWorkerSignalAvailable={facade.OpenVinoWorkerSignalAvailable}, " +
                $"rigBound={facade.IsRigBound}, " +
                $"bindingMode={facade.RigBindingModeName}, " +
                $"externalAnimationAuthority={facade.IsAvatarAnimationAuthorityEnabled}, " +
                $"kinematicTargetsLive={facade.AreRetargetTargetsLive}, " +
                $"sourceChainsValid={facade.RetargetSourceChainsValid}, " +
                $"targetsGenerated={facade.RetargetTargetsGenerated}, " +
                $"ikChainsSolved={facade.RetargetIkChainsSolved}, " +
                $"softCameraRecoveryRequested={_softCameraRecoveryRequested && facade.SoftCameraRecoveryRequested}, " +
                $"softCameraRecoveryPerformed={facade.SoftCameraRecoveryPerformed}, " +
                $"softCameraRecoverySucceeded={facade.SoftCameraRecoverySucceeded}, " +
                $"hardCameraRecoveryRequested={_hardCameraRecoveryRequested && facade.HardCameraRecoveryRequested}, " +
                $"hardCameraRecoveryAccepted={_hardCameraRecoveryAccepted && facade.HardCameraRecoveryAccepted}, " +
                $"hardCameraRecoveryPerformed={facade.HardCameraRecoveryPerformed}, " +
                $"hardRecoveryRequests={facade.TrackingRecoveryRequestCount}, " +
                $"hardRecoveryRestarts={facade.TrackingRecoveryRestartCount}.",
                this);
        }
    }
}
