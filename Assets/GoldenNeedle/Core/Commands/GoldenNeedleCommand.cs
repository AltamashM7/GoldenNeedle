using System;

namespace GoldenNeedle.Core.Commands
{
    public enum GoldenNeedleCommand
    {
        BeginCalibration,
        ResetCalibration,
        Recenter,
        RetryTracking,
        CycleCaptureCameraDevice,
        SelectCaptureCameraDevice,
        ToggleLabGamePresentation,
        SetLabPresentation,
        SetGamePresentation,
        ToggleRawLandmarks,
        ToggleCanonical2D,
        ToggleCanonical3D,
        ToggleStabilized2D,
        ToggleRigDrive,
        ToggleCoordinateDiagnostic,
        ToggleMainDiagnostics,
        ToggleProceduralRigViewport,
        ToggleLocomotionDiagnostics,
        ToggleLocomotionWorldView,
        ToggleAllDebugPresentation,
        SelectCameraViewPreset,
    }

    [Serializable]
    public struct GoldenNeedleCommandRequest
    {
        public GoldenNeedleCommand command;
        public string parameter;

        public GoldenNeedleCommandRequest(
            GoldenNeedleCommand command,
            string parameter = null)
        {
            this.command = command;
            this.parameter = parameter ?? string.Empty;
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(parameter)
                ? command.ToString()
                : $"{command} ({parameter})";
        }
    }

    public enum GoldenNeedleCommandResultStatus
    {
        Executed,
        Rejected,
        MissingTarget,
        Unsupported,
    }

    public readonly struct GoldenNeedleCommandResult
    {
        public GoldenNeedleCommandResult(
            GoldenNeedleCommandRequest request,
            GoldenNeedleCommandResultStatus status,
            string message)
        {
            Request = request;
            Status = status;
            Message = message ?? string.Empty;
        }

        public GoldenNeedleCommandRequest Request { get; }
        public GoldenNeedleCommandResultStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == GoldenNeedleCommandResultStatus.Executed;

        public static GoldenNeedleCommandResult Executed(
            GoldenNeedleCommandRequest request,
            string message = "Command executed")
        {
            return new GoldenNeedleCommandResult(
                request,
                GoldenNeedleCommandResultStatus.Executed,
                message);
        }

        public static GoldenNeedleCommandResult Rejected(
            GoldenNeedleCommandRequest request,
            string message)
        {
            return new GoldenNeedleCommandResult(
                request,
                GoldenNeedleCommandResultStatus.Rejected,
                message);
        }

        public static GoldenNeedleCommandResult MissingTarget(
            GoldenNeedleCommandRequest request,
            string target)
        {
            return new GoldenNeedleCommandResult(
                request,
                GoldenNeedleCommandResultStatus.MissingTarget,
                $"Command target unavailable: {target}");
        }

        public static GoldenNeedleCommandResult Unsupported(
            GoldenNeedleCommandRequest request,
            string message)
        {
            return new GoldenNeedleCommandResult(
                request,
                GoldenNeedleCommandResultStatus.Unsupported,
                message);
        }
    }

    public interface IGoldenNeedleCommandDispatcher
    {
        GoldenNeedleCommandResult Execute(GoldenNeedleCommandRequest request);
    }

    /// <summary>
    /// Runtime service hooks for authorities that live outside the scene-resident presenter.
    /// The registry carries invocation delegates only; domain state and transform math remain
    /// owned by the registered runtime authority.
    /// </summary>
    public static class GoldenNeedleCommandRuntimeServices
    {
        private static Func<string, bool> _cameraViewPresetSelector;

        public static Func<string, bool> CameraViewPresetSelector => _cameraViewPresetSelector;

        public static void RegisterCameraViewPresetSelector(Func<string, bool> selector)
        {
            if (selector != null)
            {
                _cameraViewPresetSelector = selector;
            }
        }

        public static void UnregisterCameraViewPresetSelector(Func<string, bool> selector)
        {
            if (selector != null && _cameraViewPresetSelector == selector)
            {
                _cameraViewPresetSelector = null;
            }
        }
    }

    /// <summary>
    /// Narrow invocation surface used by the project-owned router. Runtime/domain objects keep
    /// their own state machines; these delegates only forward commands to their public APIs.
    /// </summary>
    public sealed class GoldenNeedleCommandTargets
    {
        public Action BeginCalibration { get; set; }
        public Action ResetCalibration { get; set; }
        public Action Recenter { get; set; }
        public Action RetryTracking { get; set; }
        public Func<bool> CycleCaptureCameraDevice { get; set; }
        public Func<string, bool> SelectCaptureCameraDevice { get; set; }
        public Action ToggleLabGamePresentation { get; set; }
        public Action<bool> SetGamePresentation { get; set; }
        public Action ToggleRawLandmarks { get; set; }
        public Action ToggleCanonical2D { get; set; }
        public Action ToggleCanonical3D { get; set; }
        public Action ToggleStabilized2D { get; set; }
        public Action ToggleRigDrive { get; set; }
        public Action ToggleCoordinateDiagnostic { get; set; }
        public Action ToggleMainDiagnostics { get; set; }
        public Action ToggleProceduralRigViewport { get; set; }
        public Action ToggleLocomotionDiagnostics { get; set; }
        public Action ToggleLocomotionWorldView { get; set; }
        public Action ToggleAllDebugPresentation { get; set; }
        public Func<string, bool> SelectCameraViewPreset { get; set; }
    }

    /// <summary>
    /// Single project-owned action router shared by keyboard, speech and future UI inputs.
    /// </summary>
    public sealed class GoldenNeedleCommandRouter : IGoldenNeedleCommandDispatcher
    {
        private readonly GoldenNeedleCommandTargets _targets;

        public GoldenNeedleCommandRouter(GoldenNeedleCommandTargets targets)
        {
            _targets = targets ?? new GoldenNeedleCommandTargets();
        }

        public GoldenNeedleCommandResult Execute(GoldenNeedleCommandRequest request)
        {
            switch (request.command)
            {
                case GoldenNeedleCommand.BeginCalibration:
                    return Invoke(request, _targets.BeginCalibration, "MotionEngineRuntime.BeginCalibration");
                case GoldenNeedleCommand.ResetCalibration:
                    return Invoke(request, _targets.ResetCalibration, "MotionEngineRuntime.ResetCalibration");
                case GoldenNeedleCommand.Recenter:
                    return Invoke(request, _targets.Recenter, "EmbodiedLocomotionController.Recenter");
                case GoldenNeedleCommand.RetryTracking:
                    return Invoke(request, _targets.RetryTracking, "MediaPipePoseProvider.Retry");
                case GoldenNeedleCommand.CycleCaptureCameraDevice:
                    return InvokeBool(
                        request,
                        _targets.CycleCaptureCameraDevice,
                        "MediaPipePoseProvider.RequestCycleCamera",
                        "Capture-camera cycle request was rejected");
                case GoldenNeedleCommand.SelectCaptureCameraDevice:
                    if (string.IsNullOrWhiteSpace(request.parameter))
                    {
                        return GoldenNeedleCommandResult.Rejected(
                            request,
                            "SelectCaptureCameraDevice requires a camera-device name parameter");
                    }
                    return InvokeStringBool(
                        request,
                        _targets.SelectCaptureCameraDevice,
                        request.parameter,
                        "MediaPipePoseProvider.RequestCameraSwitch",
                        "Capture-camera switch request was rejected");
                case GoldenNeedleCommand.ToggleLabGamePresentation:
                    return Invoke(request, _targets.ToggleLabGamePresentation, "ThirdPersonLabCamera.ToggleGameView");
                case GoldenNeedleCommand.SetLabPresentation:
                    return InvokeBoolSetter(request, _targets.SetGamePresentation, false, "ThirdPersonLabCamera.SetGameViewActive");
                case GoldenNeedleCommand.SetGamePresentation:
                    return InvokeBoolSetter(request, _targets.SetGamePresentation, true, "ThirdPersonLabCamera.SetGameViewActive");
                case GoldenNeedleCommand.ToggleRawLandmarks:
                    return Invoke(request, _targets.ToggleRawLandmarks, "PoseTrackingSpikePresenter.ToggleRawLandmarks");
                case GoldenNeedleCommand.ToggleCanonical2D:
                    return Invoke(request, _targets.ToggleCanonical2D, "PoseTrackingSpikePresenter.ToggleCanonical2D");
                case GoldenNeedleCommand.ToggleCanonical3D:
                    return Invoke(request, _targets.ToggleCanonical3D, "PoseTrackingSpikePresenter.ToggleCanonical3D");
                case GoldenNeedleCommand.ToggleStabilized2D:
                    return Invoke(request, _targets.ToggleStabilized2D, "PoseTrackingSpikePresenter.ToggleStabilized2D");
                case GoldenNeedleCommand.ToggleRigDrive:
                    return Invoke(request, _targets.ToggleRigDrive, "HumanoidRetargeter.DriveRig");
                case GoldenNeedleCommand.ToggleCoordinateDiagnostic:
                    return Invoke(request, _targets.ToggleCoordinateDiagnostic, "PoseTrackingSpikePresenter.ToggleCoordinateDiagnostic");
                case GoldenNeedleCommand.ToggleMainDiagnostics:
                    return Invoke(request, _targets.ToggleMainDiagnostics, "PoseTrackingSpikePresenter.ToggleMainDiagnostics");
                case GoldenNeedleCommand.ToggleProceduralRigViewport:
                    return Invoke(request, _targets.ToggleProceduralRigViewport, "PoseTrackingSpikePresenter.ToggleProceduralRigViewport");
                case GoldenNeedleCommand.ToggleLocomotionDiagnostics:
                    return Invoke(request, _targets.ToggleLocomotionDiagnostics, "PoseTrackingSpikePresenter.ToggleLocomotionDiagnostics");
                case GoldenNeedleCommand.ToggleLocomotionWorldView:
                    return Invoke(request, _targets.ToggleLocomotionWorldView, "PoseTrackingSpikePresenter.ToggleLocomotionWorldView");
                case GoldenNeedleCommand.ToggleAllDebugPresentation:
                    return Invoke(request, _targets.ToggleAllDebugPresentation, "PoseTrackingSpikePresenter.ToggleAllDebugPresentation");
                case GoldenNeedleCommand.SelectCameraViewPreset:
                    if (string.IsNullOrWhiteSpace(request.parameter))
                    {
                        return GoldenNeedleCommandResult.Rejected(
                            request,
                            "SelectCameraViewPreset requires a preset-name parameter");
                    }
                    var presetSelector =
                        _targets.SelectCameraViewPreset ??
                        GoldenNeedleCommandRuntimeServices.CameraViewPresetSelector;
                    return InvokeStringBool(
                        request,
                        presetSelector,
                        request.parameter.Trim(),
                        "ThirdPersonLabCamera.SelectViewPreset",
                        "Camera view preset selection was rejected");
                default:
                    return GoldenNeedleCommandResult.Unsupported(request, "Unknown Golden Needle command");
            }
        }

        private static GoldenNeedleCommandResult Invoke(
            GoldenNeedleCommandRequest request,
            Action action,
            string targetName)
        {
            if (action == null)
            {
                return GoldenNeedleCommandResult.MissingTarget(request, targetName);
            }

            action();
            return GoldenNeedleCommandResult.Executed(request);
        }

        private static GoldenNeedleCommandResult InvokeBool(
            GoldenNeedleCommandRequest request,
            Func<bool> action,
            string targetName,
            string rejectedMessage)
        {
            if (action == null)
            {
                return GoldenNeedleCommandResult.MissingTarget(request, targetName);
            }

            return action()
                ? GoldenNeedleCommandResult.Executed(request)
                : GoldenNeedleCommandResult.Rejected(request, rejectedMessage);
        }

        private static GoldenNeedleCommandResult InvokeStringBool(
            GoldenNeedleCommandRequest request,
            Func<string, bool> action,
            string parameter,
            string targetName,
            string rejectedMessage)
        {
            if (action == null)
            {
                return GoldenNeedleCommandResult.MissingTarget(request, targetName);
            }

            return action(parameter)
                ? GoldenNeedleCommandResult.Executed(request)
                : GoldenNeedleCommandResult.Rejected(request, rejectedMessage);
        }

        private static GoldenNeedleCommandResult InvokeBoolSetter(
            GoldenNeedleCommandRequest request,
            Action<bool> action,
            bool value,
            string targetName)
        {
            if (action == null)
            {
                return GoldenNeedleCommandResult.MissingTarget(request, targetName);
            }

            action(value);
            return GoldenNeedleCommandResult.Executed(request);
        }
    }
}
