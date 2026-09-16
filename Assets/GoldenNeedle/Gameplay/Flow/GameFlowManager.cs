using System;
using System.Collections;
using System.IO;
using GoldenNeedle.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldenNeedle.Gameplay.Flow
{
    public enum GameFlowTransitionState
    {
        Idle = 0,
        FadingOut = 1,
        LoadingScene = 2,
        ResolvingSpawn = 3,
        PlacingPlayer = 4,
        FadingIn = 5,
        Completed = 6,
        Failed = 7,
    }

    public enum GameFlowFailureCode
    {
        None = 0,
        InvalidRequest = 1,
        TransitionAlreadyInProgress = 2,
        SceneLoadFailed = 3,
        SpawnResolutionFailed = 4,
        PlayerUnavailable = 5,
        PlayerPlacementFailed = 6,
    }

    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class GameFlowManager : MonoBehaviour
    {
        [Header("Persistent flow")]
        [SerializeField] private GameFlowFade fade;
        [SerializeField] private string hubSceneName = "GoldenNeedle_Hub";

        private bool _transitionInProgress;

        public static GameFlowManager Instance { get; private set; }

        public bool IsPersistentInstance => Instance == this;
        public bool IsTransitioning => _transitionInProgress;
        public GameFlowTransitionState State { get; private set; } = GameFlowTransitionState.Idle;
        public GameFlowFailureCode LastFailureCode { get; private set; } = GameFlowFailureCode.None;
        public string LastFailureMessage { get; private set; } = string.Empty;
        public string CurrentDestinationSceneName { get; private set; } = string.Empty;
        public string CurrentRequestedSpawnId { get; private set; } = string.Empty;
        public string LastCompletedSceneName { get; private set; } = string.Empty;
        public string LastCompletedSpawnId { get; private set; } = string.Empty;

        public event Action<GameFlowTransitionState> StateChanged;
        public event Action<string, string> TransitionCompleted;
        public event Action<GameFlowFailureCode, string> TransitionFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool TryTransitionTo(string destinationSceneName, string requestedSpawnId = null)
        {
            if (_transitionInProgress)
            {
                RejectRequest(
                    GameFlowFailureCode.TransitionAlreadyInProgress,
                    $"A transition to '{CurrentDestinationSceneName}' is already in progress.",
                    false);
                return false;
            }

            if (!isActiveAndEnabled)
            {
                RejectRequest(
                    GameFlowFailureCode.InvalidRequest,
                    "GameFlowManager must be active and enabled to begin a transition.",
                    true);
                return false;
            }

            var normalizedSceneName = destinationSceneName == null ? string.Empty : destinationSceneName.Trim();
            if (string.IsNullOrEmpty(normalizedSceneName))
            {
                RejectRequest(
                    GameFlowFailureCode.InvalidRequest,
                    "Destination scene identity/name cannot be empty.",
                    true);
                return false;
            }

            var normalizedSpawnId = requestedSpawnId == null ? string.Empty : requestedSpawnId.Trim();
            _transitionInProgress = true;
            CurrentDestinationSceneName = normalizedSceneName;
            CurrentRequestedSpawnId = normalizedSpawnId;
            LastFailureCode = GameFlowFailureCode.None;
            LastFailureMessage = string.Empty;
            StartCoroutine(TransitionRoutine(normalizedSceneName, normalizedSpawnId));
            return true;
        }

        public bool ReturnToHub(string requestedSpawnId = null)
        {
            return TryTransitionTo(hubSceneName, requestedSpawnId);
        }

        private IEnumerator TransitionRoutine(string destinationSceneName, string requestedSpawnId)
        {
            SetState(GameFlowTransitionState.FadingOut);
            if (fade != null)
            {
                yield return fade.FadeOut();
            }

            SetState(GameFlowTransitionState.LoadingScene);
            AsyncOperation loadOperation = null;
            Exception loadException = null;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(destinationSceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                loadException = exception;
            }

            if (loadException != null)
            {
                RecordTransitionFailure(
                    GameFlowFailureCode.SceneLoadFailed,
                    $"Failed to start loading scene '{destinationSceneName}': {loadException.Message}");
                yield return RestoreVisibilityAfterFailure();
                EndFailedTransition();
                yield break;
            }

            if (loadOperation == null)
            {
                RecordTransitionFailure(
                    GameFlowFailureCode.SceneLoadFailed,
                    $"Unity returned no asynchronous load operation for scene '{destinationSceneName}'.");
                yield return RestoreVisibilityAfterFailure();
                EndFailedTransition();
                yield break;
            }

            while (!loadOperation.isDone)
            {
                yield return null;
            }

            var destinationScene = ResolveLoadedDestinationScene(destinationSceneName);
            if (!destinationScene.IsValid() || !destinationScene.isLoaded)
            {
                RecordTransitionFailure(
                    GameFlowFailureCode.SceneLoadFailed,
                    $"Scene '{destinationSceneName}' finished loading but could not be resolved as the loaded destination scene.");
                yield return RestoreVisibilityAfterFailure();
                EndFailedTransition();
                yield break;
            }

            if (!string.IsNullOrEmpty(requestedSpawnId))
            {
                SetState(GameFlowTransitionState.ResolvingSpawn);
                if (!PlayerSpawnPointResolver.TryResolve(
                        destinationScene,
                        requestedSpawnId,
                        out var spawnPoint,
                        out var spawnError))
                {
                    RecordTransitionFailure(GameFlowFailureCode.SpawnResolutionFailed, spawnError);
                    yield return RestoreVisibilityAfterFailure();
                    EndFailedTransition();
                    yield break;
                }

                var session = GoldenNeedlePlayerSession.Instance;
                var facade = session == null ? null : session.GetComponent<GoldenNeedlePlayerFacade>();
                if (session == null || facade == null)
                {
                    RecordTransitionFailure(
                        GameFlowFailureCode.PlayerUnavailable,
                        "The persistent GoldenNeedlePlayerSession or its GoldenNeedlePlayerFacade is unavailable.");
                    yield return RestoreVisibilityAfterFailure();
                    EndFailedTransition();
                    yield break;
                }

                SetState(GameFlowTransitionState.PlacingPlayer);
                if (!facade.TryPlacePlayerAt(spawnPoint.PlacementPose.position))
                {
                    RecordTransitionFailure(
                        GameFlowFailureCode.PlayerPlacementFailed,
                        $"The persistent player could not be placed at spawn '{requestedSpawnId}' in scene '{destinationScene.name}'.");
                    yield return RestoreVisibilityAfterFailure();
                    EndFailedTransition();
                    yield break;
                }
            }

            SetState(GameFlowTransitionState.FadingIn);
            if (fade != null)
            {
                yield return fade.FadeIn();
            }

            LastCompletedSceneName = destinationScene.name;
            LastCompletedSpawnId = requestedSpawnId;
            LastFailureCode = GameFlowFailureCode.None;
            LastFailureMessage = string.Empty;
            _transitionInProgress = false;
            SetState(GameFlowTransitionState.Completed);
            TransitionCompleted?.Invoke(LastCompletedSceneName, LastCompletedSpawnId);
        }

        private IEnumerator RestoreVisibilityAfterFailure()
        {
            if (fade != null)
            {
                yield return fade.FadeIn();
            }
        }

        private void EndFailedTransition()
        {
            _transitionInProgress = false;
            CurrentDestinationSceneName = string.Empty;
            CurrentRequestedSpawnId = string.Empty;
        }

        private void RecordTransitionFailure(GameFlowFailureCode code, string message)
        {
            LastFailureCode = code;
            LastFailureMessage = message ?? string.Empty;
            SetState(GameFlowTransitionState.Failed);
            TransitionFailed?.Invoke(code, LastFailureMessage);
        }

        private void RejectRequest(GameFlowFailureCode code, string message, bool changeState)
        {
            LastFailureCode = code;
            LastFailureMessage = message ?? string.Empty;
            if (changeState)
            {
                SetState(GameFlowTransitionState.Failed);
            }
            TransitionFailed?.Invoke(code, LastFailureMessage);
        }

        private void SetState(GameFlowTransitionState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(state);
        }

        private static Scene ResolveLoadedDestinationScene(string destinationSceneIdentity)
        {
            var byName = SceneManager.GetSceneByName(destinationSceneIdentity);
            if (byName.IsValid() && byName.isLoaded)
            {
                return byName;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                return default;
            }

            var requestedFileName = Path.GetFileNameWithoutExtension(destinationSceneIdentity);
            if (string.Equals(activeScene.path, destinationSceneIdentity, StringComparison.Ordinal) ||
                string.Equals(activeScene.name, destinationSceneIdentity, StringComparison.Ordinal) ||
                string.Equals(activeScene.name, requestedFileName, StringComparison.Ordinal))
            {
                return activeScene;
            }

            return default;
        }
    }
}
