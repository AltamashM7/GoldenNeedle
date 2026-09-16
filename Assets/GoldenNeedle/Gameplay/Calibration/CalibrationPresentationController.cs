using System.Collections;
using GoldenNeedle.Gameplay.Player;
using UnityEngine;
using UnityEngine.UI;
using GoldenNeedle.Gameplay.Presentation;

namespace GoldenNeedle.Gameplay.Calibration
{
    /// <summary>
    /// Scene-local presentation authority for Calibration. All composition, styling, and timing
    /// values remain Inspector-authored; CalibrationSceneController remains the flow authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CalibrationPresentationController : MonoBehaviour
    {
        [Header("Flow and staging")]
        [SerializeField] private CalibrationSceneController flowController;
        [SerializeField] private GoldenNeedlePlayerFacade playerFacade;
        [SerializeField] private Transform characterStageAnchor;
        [SerializeField] private bool placeCharacterAtStageOnStart = true;
        [SerializeField] private bool suppressAvatarPoseDriveOnIntro = true;
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private string idleStateName;
        [Min(0f), SerializeField] private float idleCrossFadeSeconds = 0.15f;
        [SerializeField] private bool playIdleOnIntro = true;
        [SerializeField] private PresentationCameraRig cameraRig;

        [Header("World-space presentation groups")]
        [SerializeField] private WorldSpacePresentationGroup introPromptGroup;
        [SerializeField] private WorldSpacePresentationGroup webcamGroup;
        [SerializeField] private WorldSpacePresentationGroup successGroup;
        [SerializeField] private WorldSpacePresentationGroup failureGroup;

        [Header("World-space input")]
        [SerializeField] private Button beginCalibrationButton;
        [SerializeField] private bool showClickFallback = true;
        [SerializeField] private Button retryTransitionButton;
        [SerializeField] private bool showRetryButton = true;

        [Header("Success preview")]
        [Min(0f), SerializeField] private float successPreviewSeconds = 2.5f;
        [SerializeField] private bool hideWebcamOnSuccess = true;
        [SerializeField] private bool keepSuccessOnTransitionFailure = true;

        private Coroutine _successPreviewCoroutine;
        private CalibrationSceneState _lastAppliedState;
        private CalibrationSceneState _lastControlState;
        private bool _hasAppliedState;
        private bool _hasAppliedControlState;
        private bool _hasStagedCharacter;
        private bool _hasPlayedIdle;
        private bool _flowSubscribed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            TryBindFlowController();
        }

        private void Start()
        {
            ResolveReferences();
            TryBindFlowController();
            ApplyCameraPose();
            if (flowController != null)
            {
                ApplyState(flowController.State, true);
            }
            else
            {
                ApplyState(CalibrationSceneState.Initializing, true);
            }

            EnsurePlayerSetup();
        }

        private void Update()
        {
            TryBindFlowController();
            EnsurePlayerSetup();
        }

        private void OnDisable()
        {
            UnbindFlowController();
            StopSuccessPreview();
        }

        private void OnDestroy()
        {
            UnbindFlowController();
            StopSuccessPreview();
        }

        private void OnFlowStateChanged(CalibrationSceneState state)
        {
            ApplyState(state, false);
        }

        private void ApplyState(CalibrationSceneState state, bool immediate)
        {
            var stateChanged = !_hasAppliedState || _lastAppliedState != state;
            _lastAppliedState = state;
            _hasAppliedState = true;

            if (stateChanged)
            {
                ApplyPlayerControlState(state);
            }

            switch (state)
            {
                case CalibrationSceneState.Initializing:
                case CalibrationSceneState.WaitingForTracking:
                case CalibrationSceneState.Ready:
                    StopSuccessPreview();
                    ShowGroup(introPromptGroup, true, immediate);
                    ShowGroup(webcamGroup, false, immediate);
                    ShowGroup(successGroup, false, immediate);
                    ShowGroup(failureGroup, false, immediate);
                    PlayIntroIdleIfConfigured();
                    break;
                case CalibrationSceneState.Calibrating:
                    StopSuccessPreview();
                    ShowGroup(introPromptGroup, false, immediate);
                    ShowGroup(webcamGroup, true, immediate);
                    ShowGroup(successGroup, false, immediate);
                    ShowGroup(failureGroup, false, immediate);
                    break;
                case CalibrationSceneState.CalibrationUsable:
                    ShowGroup(introPromptGroup, false, immediate);
                    ShowGroup(webcamGroup, !hideWebcamOnSuccess, immediate);
                    ShowGroup(successGroup, true, immediate);
                    ShowGroup(failureGroup, false, immediate);
                    if (stateChanged)
                    {
                        BeginSuccessPreview();
                    }
                    break;
                case CalibrationSceneState.Transitioning:
                    StopSuccessPreview();
                    ShowGroup(introPromptGroup, false, immediate);
                    ShowGroup(webcamGroup, false, immediate);
                    ShowGroup(successGroup, true, immediate);
                    ShowGroup(failureGroup, false, immediate);
                    break;
                case CalibrationSceneState.TransitionFailed:
                    StopSuccessPreview();
                    ShowGroup(introPromptGroup, false, immediate);
                    ShowGroup(webcamGroup, false, immediate);
                    ShowGroup(successGroup, keepSuccessOnTransitionFailure, immediate);
                    ShowGroup(failureGroup, true, immediate);
                    break;
            }

            ApplyInputState(state);
        }

        private void BeginSuccessPreview()
        {
            StopSuccessPreview();
            _successPreviewCoroutine = StartCoroutine(SuccessPreviewRoutine());
        }

        private IEnumerator SuccessPreviewRoutine()
        {
            var elapsed = 0f;
            while (elapsed < Mathf.Max(0f, successPreviewSeconds))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _successPreviewCoroutine = null;
            if (flowController != null &&
                flowController.State == CalibrationSceneState.CalibrationUsable)
            {
                flowController.RequestHubTransitionAfterPresentation();
            }
        }

        private void StopSuccessPreview()
        {
            if (_successPreviewCoroutine == null)
            {
                return;
            }

            StopCoroutine(_successPreviewCoroutine);
            _successPreviewCoroutine = null;
        }

        private void ApplyPlayerControlState(CalibrationSceneState state)
        {
            EnsurePlayerReference();
            if (playerFacade == null)
            {
                return;
            }

            switch (state)
            {
                case CalibrationSceneState.Initializing:
                case CalibrationSceneState.WaitingForTracking:
                case CalibrationSceneState.Ready:
                    playerFacade.SetAvatarAnimationAuthorityEnabled(true);
                    playerFacade.SetAvatarPoseDriveEnabled(false);
                    playerFacade.SetLocomotionEnabled(false);
                    break;
                case CalibrationSceneState.Calibrating:
                    playerFacade.SetAvatarAnimationAuthorityEnabled(true);
                    playerFacade.SetAvatarPoseDriveEnabled(false);
                    playerFacade.SetLocomotionEnabled(false);
                    break;
                case CalibrationSceneState.CalibrationUsable:
                case CalibrationSceneState.Transitioning:
                case CalibrationSceneState.TransitionFailed:
                    playerFacade.SetAvatarAnimationAuthorityEnabled(false);
                    playerFacade.SetAvatarPoseDriveEnabled(true);
                    playerFacade.SetLocomotionEnabled(false);
                    break;
            }

            _lastControlState = state;
            _hasAppliedControlState = true;
        }

        private void EnsurePlayerSetup()
        {
            EnsurePlayerReference();
            if (playerFacade == null)
            {
                return;
            }

            var state = flowController == null
                ? CalibrationSceneState.Initializing
                : flowController.State;
            if (!_hasAppliedControlState || _lastControlState != state)
            {
                ApplyPlayerControlState(state);
            }

            if (_hasStagedCharacter)
            {
                return;
            }

            if (!placeCharacterAtStageOnStart || characterStageAnchor == null)
            {
                _hasStagedCharacter = true;
                return;
            }

            playerFacade.TryPlacePlayerAt(characterStageAnchor.position);
            _hasStagedCharacter = true;
        }

        private void PlayIntroIdleIfConfigured()
        {
            if (_hasPlayedIdle || !playIdleOnIntro || characterAnimator == null ||
                string.IsNullOrWhiteSpace(idleStateName))
            {
                return;
            }

            var stateHash = Animator.StringToHash(idleStateName);
            if (!characterAnimator.HasState(0, stateHash))
            {
                return;
            }

            var crossFadeSeconds = Mathf.Max(0f, idleCrossFadeSeconds);
            if (crossFadeSeconds <= 0f)
            {
                characterAnimator.Play(stateHash, 0, 0f);
            }
            else
            {
                characterAnimator.CrossFadeInFixedTime(idleStateName, crossFadeSeconds, 0, 0f);
            }

            _hasPlayedIdle = true;
        }

        private void ApplyInputState(CalibrationSceneState state)
        {
            if (beginCalibrationButton != null)
            {
                beginCalibrationButton.gameObject.SetActive(showClickFallback);
                beginCalibrationButton.interactable = state == CalibrationSceneState.Ready;
            }

            if (retryTransitionButton != null)
            {
                retryTransitionButton.gameObject.SetActive(showRetryButton);
                retryTransitionButton.interactable = state == CalibrationSceneState.TransitionFailed;
            }
        }

        private void ShowGroup(
            WorldSpacePresentationGroup group,
            bool visible,
            bool immediate)
        {
            if (group == null)
            {
                return;
            }

            if (visible)
            {
                if (immediate)
                {
                    group.ShowImmediate();
                }
                else
                {
                    group.Show();
                }
            }
            else if (immediate)
            {
                group.HideImmediate();
            }
            else
            {
                group.Hide();
            }
        }

        private void ApplyCameraPose()
        {
            cameraRig?.ApplyPose();
        }

        private void TryBindFlowController()
        {
            if (flowController == null)
            {
                flowController = GetComponent<CalibrationSceneController>();
            }

            if (flowController == null || _flowSubscribed)
            {
                return;
            }

            flowController.StateChanged += OnFlowStateChanged;
            _flowSubscribed = true;
        }

        private void UnbindFlowController()
        {
            if (!_flowSubscribed || flowController == null)
            {
                return;
            }

            flowController.StateChanged -= OnFlowStateChanged;
            _flowSubscribed = false;
        }

        private void EnsurePlayerReference()
        {
            if (playerFacade != null)
            {
                return;
            }

            var session = GoldenNeedlePlayerSession.Instance;
            playerFacade = session == null
                ? null
                : session.GetComponent<GoldenNeedlePlayerFacade>();
        }

        private void ResolveReferences()
        {
            flowController = flowController == null
                ? GetComponent<CalibrationSceneController>()
                : flowController;
            EnsurePlayerReference();
        }
    }
}
