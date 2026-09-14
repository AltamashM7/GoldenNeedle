from pathlib import Path

path = Path('Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpikePresenter.cs')
text = path.read_text(encoding='utf-8')

required_applied = [
    'using GoldenNeedle.Core.Commands;',
    'private SpeechCommandConfiguration speechCommands = SpeechCommandConfiguration.CreateDefault();',
    'private GoldenNeedleCommandRouter _commandRouter;',
    'private SpeechCommandInput _speechCommandInput;',
    'private void InitializeCommandSystem()',
    'SubmitKeyboardCommand(GoldenNeedleCommand.ToggleLocomotionWorldView);',
    'SubmitKeyboardCommand(GoldenNeedleCommand.ToggleAllDebugPresentation);',
    'SubmitKeyboardCommand(GoldenNeedleCommand.ToggleLabGamePresentation);',
    'Speech: {(_speechCommandInput == null ? "not initialized" : _speechCommandInput.DiagnosticSummary)}',
]
if all(marker in text for marker in required_applied):
    print('FOUNDATION_A_PRESENTER_MIGRATION=ALREADY_APPLIED')
    raise SystemExit(0)

using_anchor = 'using GoldenNeedle.Core.Motion.Calibration;\n'
if using_anchor not in text:
    raise SystemExit('presenter using anchor missing')
text = text.replace(
    using_anchor,
    'using GoldenNeedle.Core.Commands;\n' + using_anchor,
    1)

field_anchor = '''        [SerializeField] private LocomotionPrototypeView locomotionView;\n        [SerializeField] private ThirdPersonLabCamera gameViewCamera;\n\n        private HumanoidRigBinding _rigBinding;\n'''
field_replacement = '''        [SerializeField] private LocomotionPrototypeView locomotionView;\n        [SerializeField] private ThirdPersonLabCamera gameViewCamera;\n\n        [Header("Speech Commands")]\n        [SerializeField] private SpeechCommandConfiguration speechCommands = SpeechCommandConfiguration.CreateDefault();\n\n        private HumanoidRigBinding _rigBinding;\n        private GoldenNeedleCommandRouter _commandRouter;\n        private SpeechCommandInput _speechCommandInput;\n'''
if field_anchor not in text:
    raise SystemExit('presenter field anchor missing')
text = text.replace(field_anchor, field_replacement, 1)

awake_tail = '''            if (gameViewCamera == null)\n            {\n                gameViewCamera = Object.FindAnyObjectByType<ThirdPersonLabCamera>();\n            }\n        }\n\n'''
awake_replacement = '''            if (gameViewCamera == null)\n            {\n                gameViewCamera = Object.FindAnyObjectByType<ThirdPersonLabCamera>();\n            }\n\n            speechCommands = speechCommands ?? SpeechCommandConfiguration.CreateDefault();\n            speechCommands.Sanitize();\n            InitializeCommandSystem();\n        }\n\n'''
if awake_tail not in text:
    raise SystemExit('presenter Awake tail anchor missing')
text = text.replace(awake_tail, awake_replacement, 1)

update_start = text.find('        private void Update()\n')
on_gui_start = text.find('        private void OnGUI()\n', update_start)
if update_start < 0 or on_gui_start < 0:
    raise SystemExit('presenter Update/OnGUI boundaries missing')

command_block = r'''        private void OnEnable()
        {
            _speechCommandInput?.Start();
        }

        private void OnDisable()
        {
            _speechCommandInput?.Stop();
        }

        private void OnDestroy()
        {
            _speechCommandInput?.Dispose();
            _speechCommandInput = null;
        }

        private void OnValidate()
        {
            speechCommands = speechCommands ?? SpeechCommandConfiguration.CreateDefault();
            speechCommands.Sanitize();
        }

        public GoldenNeedleCommandResult SubmitCommand(GoldenNeedleCommandRequest request)
        {
            return _commandRouter == null
                ? GoldenNeedleCommandResult.MissingTarget(request, "GoldenNeedleCommandRouter")
                : _commandRouter.Execute(request);
        }

        public bool RawLandmarksVisible => drawRawLandmarks;
        public bool Canonical2DVisible => drawCanonical2D;
        public bool Canonical3DVisible => drawCanonical3D;
        public bool Stabilized2DVisible => drawStabilized2D;
        public bool CoordinateDiagnosticVisible => drawCoordinateDiagnostic;
        public bool MainDiagnosticsVisible => drawMainDiagnostics;
        public bool ProceduralRigViewportVisible => drawProceduralRigViewport;
        public bool LocomotionDiagnosticsVisible => drawLocomotionDiagnostics;
        public bool LocomotionWorldViewVisible => drawLocomotionWorldView;
        public bool AllDebugPresentationHidden => _hideAllDebugPresentation;

        public void ToggleRawLandmarks()
        {
            drawRawLandmarks = !drawRawLandmarks;
        }

        public void ToggleCanonical2D()
        {
            drawCanonical2D = !drawCanonical2D;
        }

        public void ToggleCanonical3D()
        {
            drawCanonical3D = !drawCanonical3D;
        }

        public void ToggleStabilized2D()
        {
            drawStabilized2D = !drawStabilized2D;
        }

        public void ToggleCoordinateDiagnostic()
        {
            drawCoordinateDiagnostic = !drawCoordinateDiagnostic;
        }

        public void ToggleMainDiagnostics()
        {
            drawMainDiagnostics = !drawMainDiagnostics;
        }

        public void ToggleProceduralRigViewport()
        {
            drawProceduralRigViewport = !drawProceduralRigViewport;
        }

        public void ToggleLocomotionDiagnostics()
        {
            drawLocomotionDiagnostics = !drawLocomotionDiagnostics;
        }

        public void ToggleLocomotionWorldView()
        {
            drawLocomotionWorldView = !drawLocomotionWorldView;
        }

        public void ToggleAllDebugPresentation()
        {
            _hideAllDebugPresentation = !_hideAllDebugPresentation;
        }

        private void InitializeCommandSystem()
        {
            var targets = new GoldenNeedleCommandTargets
            {
                BeginCalibration = runtime == null ? null : runtime.BeginCalibration,
                ResetCalibration = runtime == null ? null : runtime.ResetCalibration,
                Recenter = locomotion == null ? null : locomotion.Recenter,
                RetryTracking = provider == null ? null : provider.Retry,
                CycleCaptureCameraDevice = provider == null ? null : provider.RequestCycleCamera,
                SelectCaptureCameraDevice = provider == null ? null : provider.RequestCameraSwitch,
                ToggleLabGamePresentation = gameViewCamera == null ? null : gameViewCamera.ToggleGameView,
                SetGamePresentation = gameViewCamera == null ? null : gameViewCamera.SetGameViewActive,
                ToggleRawLandmarks = ToggleRawLandmarks,
                ToggleCanonical2D = ToggleCanonical2D,
                ToggleCanonical3D = ToggleCanonical3D,
                ToggleStabilized2D = ToggleStabilized2D,
                ToggleRigDrive = retargeter == null ? null : () => retargeter.DriveRig = !retargeter.DriveRig,
                ToggleCoordinateDiagnostic = ToggleCoordinateDiagnostic,
                ToggleMainDiagnostics = ToggleMainDiagnostics,
                ToggleProceduralRigViewport = ToggleProceduralRigViewport,
                ToggleLocomotionDiagnostics = ToggleLocomotionDiagnostics,
                ToggleLocomotionWorldView = ToggleLocomotionWorldView,
                ToggleAllDebugPresentation = ToggleAllDebugPresentation,
            };

            _commandRouter = new GoldenNeedleCommandRouter(targets);
            _speechCommandInput?.Dispose();
            _speechCommandInput = new SpeechCommandInput(
                speechCommands,
                _commandRouter,
                phrases => new WindowsKeywordSpeechProvider(phrases),
                () => Time.unscaledTimeAsDouble);
        }

        private void SubmitKeyboardCommand(GoldenNeedleCommand command)
        {
            SubmitCommand(new GoldenNeedleCommandRequest(command));
        }

        private void Update()
        {
            var frameTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _renderFps = Mathf.Lerp(_renderFps, 1f / frameTime, 1f - Mathf.Exp(-8f * frameTime));

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.RetryTracking);
            }

            if (keyboard.vKey.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.CycleCaptureCameraDevice);
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ToggleRawLandmarks);
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ToggleCanonical2D);
            }

            if (keyboard.f3Key.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ToggleCanonical3D);
            }

            if (keyboard.f4Key.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ToggleStabilized2D);
            }

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ToggleRigDrive);
            }

            if (keyboard.f6Key.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ToggleCoordinateDiagnostic);
            }

            if (keyboard.f7Key.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ToggleMainDiagnostics);
            }

            if (keyboard.f8Key.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ToggleProceduralRigViewport);
            }

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ToggleLocomotionDiagnostics);
            }

            if (keyboard.f10Key.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ToggleLocomotionWorldView);
            }

            if (keyboard.f11Key.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ToggleAllDebugPresentation);
            }

            if (keyboard.f12Key.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ToggleLabGamePresentation);
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.BeginCalibration);
            }

            if (keyboard.xKey.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.ResetCalibration);
            }

            if (keyboard.kKey.wasPressedThisFrame)
            {
                SubmitKeyboardCommand(GoldenNeedleCommand.Recenter);
            }
        }

'''
text = text[:update_start] + command_block + text[on_gui_start:]

diagnostic_anchor = '''                $"Display rot/V/mirror: {(provider == null ? 0 : provider.Orientation.DisplayRotationDegrees)}°/{(provider != null && provider.Orientation.DisplayVerticalCorrection)}/{(provider != null && provider.Orientation.DisplayMirrored)}   switch={(provider != null && provider.CameraSwitchPending ? "pending" : "ready")}\\n" +
                $"Status: {(provider == null ? "No MediaPipe provider" : provider.StatusMessage)}";'''
diagnostic_replacement = '''                $"Display rot/V/mirror: {(provider == null ? 0 : provider.Orientation.DisplayRotationDegrees)}°/{(provider != null && provider.Orientation.DisplayVerticalCorrection)}/{(provider != null && provider.Orientation.DisplayMirrored)}   switch={(provider != null && provider.CameraSwitchPending ? "pending" : "ready")}\\n" +
                $"Speech: {(_speechCommandInput == null ? "not initialized" : _speechCommandInput.DiagnosticSummary)}\\n" +
                $"Status: {(provider == null ? "No MediaPipe provider" : provider.StatusMessage)}";'''
if diagnostic_anchor not in text:
    raise SystemExit('presenter diagnostic status anchor missing')
text = text.replace(diagnostic_anchor, diagnostic_replacement, 1)

for marker in required_applied:
    if marker not in text:
        raise SystemExit(f'presenter migration marker missing after transform: {marker}')

path.write_text(text, encoding='utf-8')
print('FOUNDATION_A_PRESENTER_MIGRATION=APPLIED')
