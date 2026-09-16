using System;
using GoldenNeedle.Core.Commands;
using GoldenNeedle.Core.Commands.Providers;
using GoldenNeedle.Gameplay.Player;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Commands
{
    /// <summary>
    /// Scene-independent production command bridge. UI and speech both enter through Execute,
    /// context policy is applied once, and accepted actions are forwarded only through the
    /// persistent player session/facade gameplay boundary.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayCommandHost : MonoBehaviour, IGoldenNeedleCommandDispatcher
    {
        [SerializeField] private GameplayCommandContext commandContext = GameplayCommandContext.Calibration;
        [SerializeField] private bool speechEnabled = true;

        private GoldenNeedlePlayerFacade _boundFacade;
        private GoldenNeedleCommandRouter _boundRouter;
        private SpeechCommandInput _speechInput;

        public GameplayCommandContext CommandContext => commandContext;
        public bool SpeechEnabled => speechEnabled;
        public SpeechCommandInput SpeechInput => _speechInput;
        public GoldenNeedleCommandResult LastResult { get; private set; }
        public event Action<GoldenNeedleCommandResult> CommandProcessed;

        private void Awake()
        {
            InitializeSpeechInput();
        }

        private void OnEnable()
        {
            if (speechEnabled)
            {
                EnsureSpeechInput();
                _speechInput?.Start();
            }
        }

        private void OnDisable()
        {
            _speechInput?.Stop();
        }

        private void OnDestroy()
        {
            _speechInput?.Dispose();
            _speechInput = null;
            _boundFacade = null;
            _boundRouter = null;
        }

        public void SetContext(GameplayCommandContext context)
        {
            commandContext = context;
        }

        public void SetSpeechEnabled(bool enabled)
        {
            if (speechEnabled == enabled)
            {
                return;
            }

            speechEnabled = enabled;
            _speechInput?.Dispose();
            _speechInput = null;
            EnsureSpeechInput();
            if (speechEnabled && isActiveAndEnabled)
            {
                _speechInput?.Start();
            }
        }

        public GoldenNeedleCommandResult Execute(GoldenNeedleCommand command)
        {
            return Execute(new GoldenNeedleCommandRequest(command));
        }

        public GoldenNeedleCommandResult Execute(GoldenNeedleCommandRequest request)
        {
            if (!GameplayCommandContextPolicy.IsProductionGameplayCommand(request.command))
            {
                return Remember(GoldenNeedleCommandResult.Unsupported(
                    request,
                    $"{request.command} is not exposed by the production gameplay command host"));
            }

            if (!GameplayCommandContextPolicy.IsAllowed(commandContext, request.command))
            {
                return Remember(GoldenNeedleCommandResult.Rejected(
                    request,
                    $"{request.command} is not available in {commandContext} context"));
            }

            if (!TryResolvePlayerRouter(out var router))
            {
                return Remember(GoldenNeedleCommandResult.MissingTarget(
                    request,
                    "GoldenNeedlePlayerSession.Instance -> GoldenNeedlePlayerFacade"));
            }

            return Remember(router.Execute(request));
        }

        private GoldenNeedleCommandResult Remember(GoldenNeedleCommandResult result)
        {
            LastResult = result;
            CommandProcessed?.Invoke(result);
            return result;
        }

        private bool TryResolvePlayerRouter(out GoldenNeedleCommandRouter router)
        {
            router = null;
            var session = GoldenNeedlePlayerSession.Instance;
            if (session == null)
            {
                ClearPlayerBinding();
                return false;
            }

            var facade = session.GetComponent<GoldenNeedlePlayerFacade>();
            if (facade == null)
            {
                ClearPlayerBinding();
                return false;
            }

            if (_boundFacade != facade || _boundRouter == null)
            {
                _boundFacade = facade;
                _boundRouter = CreatePlayerRouter(facade);
            }

            router = _boundRouter;
            return router != null;
        }

        private static GoldenNeedleCommandRouter CreatePlayerRouter(GoldenNeedlePlayerFacade facade)
        {
            return new GoldenNeedleCommandRouter(new GoldenNeedleCommandTargets
            {
                BeginCalibration = facade.BeginCalibration,
                Recenter = facade.Recenter,
                RetryTracking = facade.RetryTracking,
                SetRawAvatarPresentation = () =>
                    facade.SetAvatarDriveMode(GoldenNeedleAvatarDriveMode.RawCanonical),
                SetStabilizedAvatarPresentation = () =>
                    facade.SetAvatarDriveMode(GoldenNeedleAvatarDriveMode.StabilizedCanonical),
            });
        }

        private void ClearPlayerBinding()
        {
            _boundFacade = null;
            _boundRouter = null;
        }

        private void EnsureSpeechInput()
        {
            if (_speechInput == null)
            {
                InitializeSpeechInput();
            }
        }

        private void InitializeSpeechInput()
        {
            _speechInput?.Dispose();
            var configuration = GameplaySpeechCommandConfiguration.CreateProduction();
            configuration.speechEnabled = speechEnabled;
            _speechInput = new SpeechCommandInput(
                configuration,
                this,
                phrases => new WindowsKeywordSpeechProvider(phrases),
                () => Time.unscaledTimeAsDouble);
        }
    }
}
