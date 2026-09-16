using GoldenNeedle.Core.Commands;

namespace GoldenNeedle.Gameplay.Commands
{
    public enum GameplayCommandContext
    {
        Calibration = 0,
        Hub = 1,
        Activity = 2,
    }

    public static class GameplayCommandContextPolicy
    {
        public static bool IsProductionGameplayCommand(GoldenNeedleCommand command)
        {
            switch (command)
            {
                case GoldenNeedleCommand.BeginCalibration:
                case GoldenNeedleCommand.Recenter:
                case GoldenNeedleCommand.RetryTracking:
                case GoldenNeedleCommand.SetRawAvatarPresentation:
                case GoldenNeedleCommand.SetStabilizedAvatarPresentation:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsAllowed(
            GameplayCommandContext context,
            GoldenNeedleCommand command)
        {
            if (!IsProductionGameplayCommand(command))
            {
                return false;
            }

            switch (context)
            {
                case GameplayCommandContext.Calibration:
                    return command == GoldenNeedleCommand.BeginCalibration ||
                           command == GoldenNeedleCommand.RetryTracking ||
                           IsPresentationCommand(command);
                case GameplayCommandContext.Hub:
                case GameplayCommandContext.Activity:
                    return command == GoldenNeedleCommand.Recenter ||
                           command == GoldenNeedleCommand.RetryTracking ||
                           IsPresentationCommand(command);
                default:
                    return false;
            }
        }

        private static bool IsPresentationCommand(GoldenNeedleCommand command)
        {
            return command == GoldenNeedleCommand.SetRawAvatarPresentation ||
                   command == GoldenNeedleCommand.SetStabilizedAvatarPresentation;
        }
    }
}
