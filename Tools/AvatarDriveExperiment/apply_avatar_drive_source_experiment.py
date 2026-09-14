#!/usr/bin/env python3
from pathlib import Path

# One-shot fail-closed transformer for the authorized avatar-drive source experiment.
ROOT = Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "Assets/GoldenNeedle/Core/Motion/Runtime/MotionEngineRuntime.cs"
RETARGETER = ROOT / "Assets/GoldenNeedle/Core/Motion/Retargeting/HumanoidRetargeter.cs"
PRESENTER = ROOT / "Assets/GoldenNeedle/Debug/PoseTrackingSpike/PoseTrackingSpikePresenter.cs"


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one marker, found {count}")
    return text.replace(old, new, 1)


runtime = RUNTIME.read_text(encoding="utf-8")
retargeter = RETARGETER.read_text(encoding="utf-8")
presenter = PRESENTER.read_text(encoding="utf-8")

runtime = replace_once(
    runtime,
    """namespace GoldenNeedle.Core.Motion.Runtime
{
    /// <summary>
""",
    """namespace GoldenNeedle.Core.Motion.Runtime
{
    public enum AvatarDrivePoseSource
    {
        StabilizedCanonical,
        RawCanonical,
    }

    /// <summary>
""",
    "avatar drive source enum",
)

runtime = replace_once(
    runtime,
    """        [SerializeField] private CanonicalStabilizerSettings stabilizerSettings = new CanonicalStabilizerSettings();
        [SerializeField] private MotionCalibrationSettings calibrationSettings = new MotionCalibrationSettings();
""",
    """        [SerializeField] private CanonicalStabilizerSettings stabilizerSettings = new CanonicalStabilizerSettings();
        [SerializeField] private MotionCalibrationSettings calibrationSettings = new MotionCalibrationSettings();

        [Header("Avatar Retargeting")]
        [Tooltip("Select which canonical pose drives only avatar pose solving. Calibration and locomotion remain stabilized in both modes.")]
        [SerializeField] private AvatarDrivePoseSource avatarDrivePoseSource = AvatarDrivePoseSource.StabilizedCanonical;
""",
    "serialized avatar drive selector",
)

runtime = replace_once(
    runtime,
    """        public CanonicalPoseFrame RawCanonicalFrame => _rawCanonicalFrame;
        public CanonicalPoseFrame StabilizedFrame => _stabilizedFrame;
        public CanonicalRotationFrame RotationFrame => _rotationFrame;
""",
    """        public CanonicalPoseFrame RawCanonicalFrame => _rawCanonicalFrame;
        public CanonicalPoseFrame StabilizedFrame => _stabilizedFrame;
        public AvatarDrivePoseSource AvatarDriveSource => avatarDrivePoseSource;
        public CanonicalPoseFrame AvatarDriveFrame =>
            avatarDrivePoseSource == AvatarDrivePoseSource.RawCanonical
                ? _rawCanonicalFrame
                : _stabilizedFrame;
        public string AvatarDriveSourceLabel =>
            avatarDrivePoseSource == AvatarDrivePoseSource.RawCanonical
                ? "RawCanonical"
                : "StabilizedCanonical";
        public CanonicalRotationFrame RotationFrame => _rotationFrame;
""",
    "avatar drive runtime properties",
)

runtime = replace_once(
    runtime,
    """            _lastEvaluationTimeSeconds = SanitizeTime(_source.EvaluationTimeSeconds);
            _stabilizer.Stabilize(_rawCanonicalFrame, _stabilizedFrame, _lastEvaluationTimeSeconds);
            _calibration.Update(_stabilizedFrame, _lastEvaluationTimeSeconds);
            _rotationSolver.Solve(_stabilizedFrame, _calibration.Profile, _rotationFrame);
            _kinematicTargetBuilder.Build(_stabilizedFrame, _calibration.Profile, _kinematicTargets);
""",
    """            _lastEvaluationTimeSeconds = SanitizeTime(_source.EvaluationTimeSeconds);
            _stabilizer.Stabilize(_rawCanonicalFrame, _stabilizedFrame, _lastEvaluationTimeSeconds);
            _calibration.Update(_stabilizedFrame, _lastEvaluationTimeSeconds);

            var avatarDriveFrame = AvatarDriveFrame;
            _rotationSolver.Solve(avatarDriveFrame, _calibration.Profile, _rotationFrame);
            _kinematicTargetBuilder.Build(avatarDriveFrame, _calibration.Profile, _kinematicTargets);
""",
    "selected avatar drive solve",
)

retargeter = replace_once(
    retargeter,
    """            var sourceFrame = runtime.StabilizedFrame;
""",
    """            var sourceFrame = runtime.AvatarDriveFrame;
""",
    "retarget torso source mapping",
)

presenter = replace_once(
    presenter,
    """                $"Drive: {(retargeter != null && retargeter.DriveRig ? \"On\" : \"Off\")}   Present: {FormatPresentationSmoothing(retargeter)}   Targets: {(retargeter != null && retargeter.KinematicTargetsLive && kinematicTargets != null ? \"Live\" : \"Waiting\")}\\n" +
""",
    """                $"Drive: {(retargeter != null && retargeter.DriveRig ? \"On\" : \"Off\")}   Avatar source: {(runtime == null ? \"Unavailable\" : runtime.AvatarDriveSourceLabel)}   Present: {FormatPresentationSmoothing(retargeter)}   Targets: {(retargeter != null && retargeter.KinematicTargetsLive && kinematicTargets != null ? \"Live\" : \"Waiting\")}\\n" +
""",
    "F7 avatar drive source diagnostic",
)

RUNTIME.write_text(runtime, encoding="utf-8", newline="\n")
RETARGETER.write_text(retargeter, encoding="utf-8", newline="\n")
PRESENTER.write_text(presenter, encoding="utf-8", newline="\n")

print("AVATAR_DRIVE_SOURCE_TRANSFORM=APPLIED")
