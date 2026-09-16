using GoldenNeedle.Core.Commands;

namespace GoldenNeedle.Gameplay.Commands
{
    /// <summary>
    /// Production speech vocabulary. This intentionally does not replace the PoseTrackingSpike
    /// default/lab configuration.
    /// </summary>
    public static class GameplaySpeechCommandConfiguration
    {
        public static SpeechCommandConfiguration CreateProduction()
        {
            return new SpeechCommandConfiguration
            {
                speechEnabled = true,
                commandCooldownSeconds = 0.75f,
                wakePrefix = string.Empty,
                mappings = new[]
                {
                    new SpeechCommandMapping(
                        "begin calibration",
                        GoldenNeedleCommand.BeginCalibration,
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "recenter",
                        GoldenNeedleCommand.Recenter,
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "retry tracking",
                        GoldenNeedleCommand.RetryTracking,
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "back view",
                        GoldenNeedleCommand.SelectCameraViewPreset,
                        "Back",
                        SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "front view",
                        GoldenNeedleCommand.SelectCameraViewPreset,
                        "Front",
                        SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "left view",
                        GoldenNeedleCommand.SelectCameraViewPreset,
                        "Left",
                        SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "right view",
                        GoldenNeedleCommand.SelectCameraViewPreset,
                        "Right",
                        SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "full body view",
                        GoldenNeedleCommand.SelectCameraViewPreset,
                        "FullBody",
                        SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "hands view",
                        GoldenNeedleCommand.SelectCameraViewPreset,
                        "Hands",
                        SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "left hand view",
                        GoldenNeedleCommand.SelectCameraViewPreset,
                        "LeftHand",
                        SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "right hand view",
                        GoldenNeedleCommand.SelectCameraViewPreset,
                        "RightHand",
                        SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "reduce latency",
                        GoldenNeedleCommand.SetRawAvatarPresentation,
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "low latency mode",
                        GoldenNeedleCommand.SetRawAvatarPresentation,
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "smooth motion",
                        GoldenNeedleCommand.SetStabilizedAvatarPresentation,
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                    new SpeechCommandMapping(
                        "stabilized mode",
                        GoldenNeedleCommand.SetStabilizedAvatarPresentation,
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                },
            };
        }
    }
}
