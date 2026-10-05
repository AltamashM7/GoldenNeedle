using GoldenNeedle.Core.Commands;
using HDMotionEngine;

namespace GoldenNeedle.Gameplay.Commands
{
    /// <summary>
    /// Production speech vocabulary. This intentionally does not replace the PoseTrackingSpike
    /// default/lab configuration.
    /// </summary>
    public static class GameplaySpeechCommandConfiguration
    {
        public static SpeechBinding[] CreateProduction()
        {
            return new[]
                {
                    new SpeechBinding(
                        "begin calibration",
                        "game.BeginCalibration",
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "recenter",
                        "game.Recenter",
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "retry tracking",
                        "game.RetryTracking",
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "back view",
                        "game.SelectCameraViewPreset",
                        "Back",
                        SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "front view",
                        "game.SelectCameraViewPreset",
                        "Front",
                        SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "left view",
                        "game.SelectCameraViewPreset",
                        "Left",
                        SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "right view",
                        "game.SelectCameraViewPreset",
                        "Right",
                        SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "full body view",
                        "game.SelectCameraViewPreset",
                        "FullBody",
                        SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "hands view",
                        "game.SelectCameraViewPreset",
                        "Hands",
                        SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "left hand view",
                        "game.SelectCameraViewPreset",
                        "LeftHand",
                        SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "right hand view",
                        "game.SelectCameraViewPreset",
                        "RightHand",
                        SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "reduce latency",
                        "game.SetRawAvatarPresentation",
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "low latency mode",
                        "game.SetRawAvatarPresentation",
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "smooth motion",
                        "game.SetStabilizedAvatarPresentation",
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                    new SpeechBinding(
                        "stabilized mode",
                        "game.SetStabilizedAvatarPresentation",
                        minimumConfidence: SpeechRecognitionConfidence.Low),
                };
        }
    }
}
