using System.Collections;
using GoldenNeedle.Core.Commands;
using GoldenNeedle.Gameplay.Commands;
using GoldenNeedle.Gameplay.Flow;
using GoldenNeedle.Gameplay.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenNeedle.Gameplay.Calibration
{
    public enum CalibrationSceneState
    {
        Initializing = 0,
        WaitingForTracking = 1,
        Ready = 2,
        Calibrating = 3,
        CalibrationUsable = 4,
        Transitioning = 5,
        TransitionFailed = 6,
    }

    /// <summary>
    /// Scene-local coordination for the production calibration entry flow. Motion and command
    /// ownership remain with the persistent player and GameplayCommandHost.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CalibrationSceneController : MonoBehaviour
    {
        public const string HubSceneName = "GoldenNeedle_Hub";
        public const string HubSpawnId = "HubEntry";

        [Header("Calibration UI")]
        [SerializeField] private Text instructionText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button beginCalibrationButton;
        [SerializeField] private Button retryTransitionButton;
        [SerializeField] private CalibrationCameraPreview cameraPreview;

        [Header("Timing")]
        [Min(0.75f), SerializeField] private float successHoldSeconds = 1f;

        private GoldenNeedlePlayerFacade _playerFacade;
        private GameplayCommandHost _gameplayCommandHost;
        private GameFlowManager _gameFlowManager;
        private Coroutine _successHoldCoroutine;
        private bool _calibrationRequested;
        private bool _transitionIssued;
        private CalibrationSceneState _state = CalibrationSceneState.Initializing;

        public CalibrationSceneState State => _state;
        public bool CalibrationRequested => _calibrationRequested;
        public GoldenNeedlePlayerFacade PlayerFacade => _playerFacade;

        private void Awake()
        {
            successHoldSeconds = Mathf.Clamp(successHoldSeconds, 0.75f, 1.25f);
            SetInstruction("Stand comfortably where your upper body is visible, then begin calibration.");
            SetState(CalibrationSceneState.Initializing);
        }

        private void OnDestroy()
        {
            UnsubscribeFromServices();
        }

        private void Update()
        {
            if (_state == CalibrationSceneState.Initializing)
            {
                TryResolveServices();
            }

            if (_playerFacade == null || _gameplayCommandHost == null || _gameFlowManager == null)
            {
                return;
            }

            switch (_state)
            {
                case CalibrationSceneState.WaitingForTracking:
                    if (_playerFacade.IsBodyTrackingAvailable)
                    {
                        SetState(CalibrationSceneState.Ready);
                    }
                    break;
                case CalibrationSceneState.Ready:
                    if (!_playerFacade.IsBodyTrackingAvailable)
                    {
                        SetState(CalibrationSceneState.WaitingForTracking);
                    }
                    break;
                case CalibrationSceneState.Calibrating:
                    if (_playerFacade.IsCalibrationUsable)
                    {
                        EnterCalibrationUsable();
                    }
                    break;
            }

            UpdateButtonState();
        }

        public void OnBeginCalibrationPressed()
        {
            TryResolveServices();
            if (_gameplayCommandHost == null || _calibrationRequested ||
                _state == CalibrationSceneState.Calibrating ||
                _state == CalibrationSceneState.CalibrationUsable ||
                _state == CalibrationSceneState.Transitioning)
            {
                return;
            }

            var result = _gameplayCommandHost.Execute(GoldenNeedleCommand.BeginCalibration);
            if (!result.Succeeded)
            {
                SetStatus(result.Message);
            }
        }

        public void OnRetryTrackingPressed()
        {
            TryResolveServices();
            if (_gameplayCommandHost != null)
            {
                var result = _gameplayCommandHost.Execute(GoldenNeedleCommand.RetryTracking);
                if (!result.Succeeded)
                {
                    SetStatus(result.Message);
                }
            }
        }

        public void RetryHubTransition()
        {
            if (_state != CalibrationSceneState.TransitionFailed ||
                _transitionIssued ||
                _gameFlowManager == null ||
                !_calibrationRequested ||
                _playerFacade == null ||
                !_playerFacade.IsCalibrationUsable)
            {
                return;
            }

            _transitionIssued = true;
            SetState(CalibrationSceneState.CalibrationUsable);
            _successHoldCoroutine = StartCoroutine(HoldThenTransition());
        }

        private void TryResolveServices()
        {
            if (_playerFacade == null)
            {
                var session = GoldenNeedlePlayerSession.Instance;
                _playerFacade = session == null
                    ? null
                    : session.GetComponent<GoldenNeedlePlayerFacade>();
                if (_playerFacade != null)
                {
                    cameraPreview?.SetPlayerFacade(_playerFacade);
                }
            }

            if (_gameplayCommandHost == null)
            {
                _gameplayCommandHost = FindAnyObjectByType<GameplayCommandHost>();
                if (_gameplayCommandHost != null)
                {
                    _gameplayCommandHost.SetContext(GameplayCommandContext.Calibration);
                    _gameplayCommandHost.CommandProcessed += OnCommandProcessed;
                }
            }

            if (_gameFlowManager == null)
            {
                _gameFlowManager = GameFlowManager.Instance ?? FindAnyObjectByType<GameFlowManager>();
                if (_gameFlowManager != null)
                {
                    _gameFlowManager.TransitionFailed += OnTransitionFailed;
                }
            }

            if (_state == CalibrationSceneState.Initializing &&
                _playerFacade != null &&
                _gameplayCommandHost != null &&
                _gameFlowManager != null)
            {
                SetState(_playerFacade.IsBodyTrackingAvailable
                    ? CalibrationSceneState.Ready
                    : CalibrationSceneState.WaitingForTracking);
            }
        }

        private void OnCommandProcessed(GoldenNeedleCommandResult result)
        {
            if (!result.Succeeded ||
                result.Request.command != GoldenNeedleCommand.BeginCalibration ||
                _calibrationRequested)
            {
                return;
            }

            _calibrationRequested = true;
            SetState(CalibrationSceneState.Calibrating);
        }

        private void EnterCalibrationUsable()
        {
            if (_transitionIssued || _state != CalibrationSceneState.Calibrating)
            {
                return;
            }

            _transitionIssued = true;
            SetState(CalibrationSceneState.CalibrationUsable);
            _successHoldCoroutine = StartCoroutine(HoldThenTransition());
        }

        private IEnumerator HoldThenTransition()
        {
            var elapsed = 0f;
            while (elapsed < successHoldSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _successHoldCoroutine = null;
            if (_state != CalibrationSceneState.CalibrationUsable || !_transitionIssued)
            {
                yield break;
            }

            SetState(CalibrationSceneState.Transitioning);
            if (!_gameFlowManager.TryTransitionTo(HubSceneName, HubSpawnId))
            {
                _transitionIssued = false;
                SetState(CalibrationSceneState.TransitionFailed);
                SetStatus(_gameFlowManager.LastFailureMessage);
            }
        }

        private void OnTransitionFailed(GameFlowFailureCode _, string message)
        {
            if (_state != CalibrationSceneState.Transitioning)
            {
                return;
            }

            _transitionIssued = false;
            SetState(CalibrationSceneState.TransitionFailed);
            SetStatus(message);
        }

        private void SetState(CalibrationSceneState state)
        {
            _state = state;
            switch (state)
            {
                case CalibrationSceneState.Initializing:
                    SetStatus("Starting motion tracking…");
                    break;
                case CalibrationSceneState.WaitingForTracking:
                    SetStatus("Waiting for body tracking. Stay visible to the camera.");
                    break;
                case CalibrationSceneState.Ready:
                    SetStatus("Tracking ready. Begin calibration when comfortable.");
                    break;
                case CalibrationSceneState.Calibrating:
                    SetStatus("Calibrating… remain visible.");
                    break;
                case CalibrationSceneState.CalibrationUsable:
                    SetStatus("Calibration ready.");
                    break;
                case CalibrationSceneState.Transitioning:
                    SetStatus("Entering Hub…");
                    break;
                case CalibrationSceneState.TransitionFailed:
                    SetStatus(_gameFlowManager == null
                        ? "Unable to enter Hub. Try again."
                        : _gameFlowManager.LastFailureMessage);
                    break;
            }

            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            if (beginCalibrationButton != null)
            {
                beginCalibrationButton.interactable =
                    _state == CalibrationSceneState.Ready &&
                    _playerFacade != null &&
                    _playerFacade.IsBodyTrackingAvailable &&
                    !_calibrationRequested;
            }

            if (retryTransitionButton != null)
            {
                retryTransitionButton.gameObject.SetActive(_state == CalibrationSceneState.TransitionFailed);
            }
        }

        private void SetInstruction(string value)
        {
            if (instructionText != null)
            {
                instructionText.text = value;
            }
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = string.IsNullOrEmpty(value) ? "" : value;
            }
        }

        private void UnsubscribeFromServices()
        {
            if (_gameplayCommandHost != null)
            {
                _gameplayCommandHost.CommandProcessed -= OnCommandProcessed;
            }

            if (_gameFlowManager != null)
            {
                _gameFlowManager.TransitionFailed -= OnTransitionFailed;
            }

            if (_successHoldCoroutine != null)
            {
                StopCoroutine(_successHoldCoroutine);
                _successHoldCoroutine = null;
            }
        }
    }
}
