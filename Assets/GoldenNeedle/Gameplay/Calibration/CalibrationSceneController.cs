using System;
using GoldenNeedle.Core.Commands;
using GoldenNeedle.Gameplay.Commands;
using GoldenNeedle.Gameplay.Flow;
using GoldenNeedle.Gameplay.Player;
using UnityEngine;

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
    /// Scene-local flow authority for the production calibration entry path. Presentation is
    /// notified of state changes but owns no calibration polling or scene-transition timing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CalibrationSceneController : MonoBehaviour
    {
        public const string HubSceneName = "GoldenNeedle_Hub";
        public const string HubSpawnId = "HubEntry";

        private GoldenNeedlePlayerFacade _playerFacade;
        private GameplayCommandHost _gameplayCommandHost;
        private GameFlowManager _gameFlowManager;
        private bool _calibrationRequested;
        private bool _transitionIssued;
        private CalibrationSceneState _state = CalibrationSceneState.Initializing;

        public CalibrationSceneState State => _state;
        public bool CalibrationRequested => _calibrationRequested;
        public GoldenNeedlePlayerFacade PlayerFacade => _playerFacade;
        public event Action<CalibrationSceneState> StateChanged;

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
                    if (_playerFacade.IsCalibrationComplete)
                    {
                        SetState(CalibrationSceneState.CalibrationUsable);
                    }
                    break;
            }
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

            _gameplayCommandHost.Execute(GoldenNeedleCommand.BeginCalibration);
        }

        public void OnRetryTrackingPressed()
        {
            TryResolveServices();
            _gameplayCommandHost?.Execute(GoldenNeedleCommand.RetryTracking);
        }

        /// <summary>
        /// Requests the accepted Hub transition after the scene presentation has finished its
        /// Inspector-authored success preview. This is the only public transition entry point
        /// used by CalibrationPresentationController.
        /// </summary>
        public bool RequestHubTransitionAfterPresentation()
        {
            TryResolveServices();
            if (_state != CalibrationSceneState.CalibrationUsable ||
                _transitionIssued ||
                _gameFlowManager == null ||
                !_calibrationRequested ||
                _playerFacade == null ||
                !_playerFacade.IsCalibrationComplete)
            {
                return false;
            }

            _transitionIssued = true;
            SetState(CalibrationSceneState.Transitioning);
            if (_gameFlowManager.TryTransitionTo(HubSceneName, HubSpawnId))
            {
                return true;
            }

            // GameFlowManager normally notifies OnTransitionFailed synchronously for rejected
            // requests. Keep this fallback for a manager implementation that only returns false.
            if (_state == CalibrationSceneState.Transitioning)
            {
                _transitionIssued = false;
                SetState(CalibrationSceneState.TransitionFailed);
            }

            return false;
        }

        public void RetryHubTransition()
        {
            TryResolveServices();
            if (_state != CalibrationSceneState.TransitionFailed ||
                _transitionIssued ||
                _gameFlowManager == null ||
                !_calibrationRequested ||
                _playerFacade == null ||
                !_playerFacade.IsCalibrationComplete)
            {
                return;
            }

            // A retry does not replay calibration or the success hold. It only re-enters the
            // usable state and makes one guarded transition request.
            SetState(CalibrationSceneState.CalibrationUsable);
            RequestHubTransitionAfterPresentation();
        }

        private void TryResolveServices()
        {
            if (_playerFacade == null)
            {
                var session = GoldenNeedlePlayerSession.Instance;
                _playerFacade = session == null
                    ? null
                    : session.GetComponent<GoldenNeedlePlayerFacade>();
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

        private void OnTransitionFailed(GameFlowFailureCode _, string __)
        {
            if (_state != CalibrationSceneState.Transitioning)
            {
                return;
            }

            _transitionIssued = false;
            SetState(CalibrationSceneState.TransitionFailed);
        }

        private void SetState(CalibrationSceneState state)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
            StateChanged?.Invoke(state);
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
        }
    }
}
