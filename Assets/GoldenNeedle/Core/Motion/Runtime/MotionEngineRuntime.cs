using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Rich;
using GoldenNeedle.Core.Motion.Rotation;
using GoldenNeedle.Core.Motion.Stabilization;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Runtime
{
    public enum AvatarDrivePoseSource
    {
        StabilizedCanonical = 0,
        RawCanonical = 1,
        ResponsiveCanonicalA = 2,
        ResponsiveCanonicalB = 3,
        ResponsiveCanonicalC = 4,
        ResponsiveCanonicalD = 5,
    }

    /// <summary>
    /// Single owner of the reusable Motion Engine pipeline:
    /// canonical source -> raw canonical -> stabilization -> calibration -> rotation output.
    /// Consumers read its preallocated frames and never rebuild provider-specific stages.
    /// Foundation C adds an optional rich-evidence/orientation side path without replacing any
    /// accepted V1, calibration, rotation, kinematic-target, locomotion, or retargeting behavior.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class MotionEngineRuntime : MonoBehaviour
    {
        private const float ResponsiveAMinCutoff = 1.5f;
        private const float ResponsiveABeta = 0.25f;
        private const float ResponsiveBMinCutoff = 2.0f;
        private const float ResponsiveBBeta = 0.50f;
        private const float ResponsiveCMinCutoff = 1.0f;
        private const float ResponsiveCBeta = 0.25f;
        private const float ResponsiveDMinCutoff = 1.0f;
        private const float ResponsiveDBeta = 0.50f;
        private const float ResponsiveDerivativeCutoff = 1.0f;

        private const string StabilizedAvatarDriveLabel = "StabilizedCanonical";
        private const string RawAvatarDriveLabel = "RawCanonical";
        private const string ResponsiveAAvatarDriveLabel = "ResponsiveCanonicalA (1.5/0.25/1.0)";
        private const string ResponsiveBAvatarDriveLabel = "ResponsiveCanonicalB (2.0/0.50/1.0)";
        private const string ResponsiveCAvatarDriveLabel = "ResponsiveCanonicalC (1.0/0.25/1.0)";
        private const string ResponsiveDAvatarDriveLabel = "ResponsiveCanonicalD (1.0/0.50/1.0)";

        [SerializeField] private MonoBehaviour canonicalSourceBehaviour;
        [SerializeField] private CanonicalStabilizerSettings stabilizerSettings = new CanonicalStabilizerSettings();
        [SerializeField] private MotionCalibrationSettings calibrationSettings = new MotionCalibrationSettings();

        [Header("Avatar Retargeting")]
        [Tooltip("Select which canonical pose drives only avatar pose solving. Calibration and locomotion remain stabilized in every mode.")]
        [SerializeField] private AvatarDrivePoseSource avatarDrivePoseSource = AvatarDrivePoseSource.StabilizedCanonical;

        [Header("Foundation C Rich Orientation")]
        [Tooltip("Optional rich anatomical-orientation ambiguity settings. This side path does not drive the production avatar in Foundation C.")]
        [SerializeField] private RichOrientationSolverSettings richOrientationSettings = new RichOrientationSolverSettings();

        private ICanonicalPoseSource _source;
        private IRichMotionEvidenceSource _richSource;
        private CanonicalPoseStabilizer _stabilizer;
        private CanonicalPoseStabilizer _responsiveStabilizerA;
        private CanonicalPoseStabilizer _responsiveStabilizerB;
        private CanonicalPoseStabilizer _responsiveStabilizerC;
        private CanonicalPoseStabilizer _responsiveStabilizerD;
        private MotionCalibrationSession _calibration;
        private CanonicalRotationSolver _rotationSolver;
        private CanonicalKinematicTargetBuilder _kinematicTargetBuilder;
        private RichAnatomicalOrientationSolver _richOrientationSolver;
        private readonly CanonicalPoseFrame _rawCanonicalFrame = new CanonicalPoseFrame();
        private readonly CanonicalPoseFrame _stabilizedFrame = new CanonicalPoseFrame();
        private readonly CanonicalPoseFrame _responsiveCanonicalFrameA = new CanonicalPoseFrame();
        private readonly CanonicalPoseFrame _responsiveCanonicalFrameB = new CanonicalPoseFrame();
        private readonly CanonicalPoseFrame _responsiveCanonicalFrameC = new CanonicalPoseFrame();
        private readonly CanonicalPoseFrame _responsiveCanonicalFrameD = new CanonicalPoseFrame();
        private readonly CanonicalRotationFrame _rotationFrame = new CanonicalRotationFrame();
        private readonly CanonicalKinematicTargets _kinematicTargets = new CanonicalKinematicTargets();
        private readonly RichMotionEvidenceFrame _richEvidenceFrame = new RichMotionEvidenceFrame();
        private readonly RichMotionFrame _richMotionFrame = new RichMotionFrame();
        private double _lastEvaluationTimeSeconds;
        private int _lastCoordinateConventionVersion = -1;

        public ICanonicalPoseSource Source => _source;
        public IRichMotionEvidenceSource RichSource => _richSource;
        public CanonicalPoseFrame RawCanonicalFrame => _rawCanonicalFrame;
        public CanonicalPoseFrame StabilizedFrame => _stabilizedFrame;
        public CanonicalPoseFrame ResponsiveCanonicalAFrame => _responsiveCanonicalFrameA;
        public CanonicalPoseFrame ResponsiveCanonicalBFrame => _responsiveCanonicalFrameB;
        public CanonicalPoseFrame ResponsiveCanonicalCFrame => _responsiveCanonicalFrameC;
        public CanonicalPoseFrame ResponsiveCanonicalDFrame => _responsiveCanonicalFrameD;
        public AvatarDrivePoseSource AvatarDriveSource => avatarDrivePoseSource;
        public CanonicalPoseFrame AvatarDriveFrame
        {
            get
            {
                switch (avatarDrivePoseSource)
                {
                    case AvatarDrivePoseSource.RawCanonical:
                        return _rawCanonicalFrame;
                    case AvatarDrivePoseSource.ResponsiveCanonicalA:
                        return _responsiveCanonicalFrameA;
                    case AvatarDrivePoseSource.ResponsiveCanonicalB:
                        return _responsiveCanonicalFrameB;
                    case AvatarDrivePoseSource.ResponsiveCanonicalC:
                        return _responsiveCanonicalFrameC;
                    case AvatarDrivePoseSource.ResponsiveCanonicalD:
                        return _responsiveCanonicalFrameD;
                    default:
                        return _stabilizedFrame;
                }
            }
        }

        public string AvatarDriveSourceLabel
        {
            get
            {
                switch (avatarDrivePoseSource)
                {
                    case AvatarDrivePoseSource.RawCanonical:
                        return RawAvatarDriveLabel;
                    case AvatarDrivePoseSource.ResponsiveCanonicalA:
                        return ResponsiveAAvatarDriveLabel;
                    case AvatarDrivePoseSource.ResponsiveCanonicalB:
                        return ResponsiveBAvatarDriveLabel;
                    case AvatarDrivePoseSource.ResponsiveCanonicalC:
                        return ResponsiveCAvatarDriveLabel;
                    case AvatarDrivePoseSource.ResponsiveCanonicalD:
                        return ResponsiveDAvatarDriveLabel;
                    default:
                        return StabilizedAvatarDriveLabel;
                }
            }
        }

        public CanonicalStabilizerSettings ResponsiveCanonicalASettings => _responsiveStabilizerA?.Settings;
        public CanonicalStabilizerSettings ResponsiveCanonicalBSettings => _responsiveStabilizerB?.Settings;
        public CanonicalStabilizerSettings ResponsiveCanonicalCSettings => _responsiveStabilizerC?.Settings;
        public CanonicalStabilizerSettings ResponsiveCanonicalDSettings => _responsiveStabilizerD?.Settings;
        public CanonicalRotationFrame RotationFrame => _rotationFrame;
        public CanonicalKinematicTargets KinematicTargets => _kinematicTargets;
        public CanonicalRotationSolver RotationSolver => _rotationSolver;
        public MotionCalibrationSession Calibration => _calibration;
        public RichMotionEvidenceFrame RichEvidenceFrame => _richEvidenceFrame;
        public RichMotionFrame RichMotionFrame => _richMotionFrame;
        public RichAnatomicalOrientationSolver RichOrientationSolver => _richOrientationSolver;
        public double EvaluationTimeSeconds => _lastEvaluationTimeSeconds;
        public bool IsSourceReady => _source != null;
        public bool IsRichSourceAvailable => _richSource != null;
        public string RichMotionSummary => _richSource == null
            ? "Rich: source unavailable"
            : $"Rich v{_richMotionFrame.schemaVersion} basis={_richMotionFrame.validOrientationCount}/{GoldenNeedle.Core.Motion.Rich.RichMotionFrame.OrientationChannelCount} twist O/H/R/U={_richMotionFrame.observedTwistCount}/{_richMotionFrame.heldTwistCount}/{_richMotionFrame.referenceFallbackTwistCount}/{_richMotionFrame.unobservableTwistCount}";

        private void Awake()
        {
            _stabilizer = new CanonicalPoseStabilizer(stabilizerSettings);
            _responsiveStabilizerA = new CanonicalPoseStabilizer(
                CreateResponsiveAvatarSettings(ResponsiveAMinCutoff, ResponsiveABeta));
            _responsiveStabilizerB = new CanonicalPoseStabilizer(
                CreateResponsiveAvatarSettings(ResponsiveBMinCutoff, ResponsiveBBeta));
            _responsiveStabilizerC = new CanonicalPoseStabilizer(
                CreateResponsiveAvatarSettings(ResponsiveCMinCutoff, ResponsiveCBeta));
            _responsiveStabilizerD = new CanonicalPoseStabilizer(
                CreateResponsiveAvatarSettings(ResponsiveDMinCutoff, ResponsiveDBeta));
            _calibration = new MotionCalibrationSession(calibrationSettings);
            _rotationSolver = new CanonicalRotationSolver();
            _kinematicTargetBuilder = new CanonicalKinematicTargetBuilder();
            _richOrientationSolver = new RichAnatomicalOrientationSolver(richOrientationSettings);
            ResolveSource();
        }

        private void Update()
        {
            if (_source == null && !ResolveSource())
            {
                _rawCanonicalFrame.Clear();
                _stabilizedFrame.Clear();
                _responsiveCanonicalFrameA.Clear();
                _responsiveCanonicalFrameB.Clear();
                _responsiveCanonicalFrameC.Clear();
                _responsiveCanonicalFrameD.Clear();
                _rotationFrame.Clear();
                _kinematicTargets.Clear();
                ClearRichMotionState(true);
                return;
            }

            if (_source.CoordinateConventionVersion != _lastCoordinateConventionVersion)
            {
                _lastCoordinateConventionVersion = _source.CoordinateConventionVersion;
                _stabilizer.Reset();
                ResetResponsiveAvatarStabilizers();
                _calibration.Reset();
                _kinematicTargetBuilder.Reset();
                _rotationFrame.Clear();
                _kinematicTargets.Clear();
                ClearRichMotionState(true);
            }

            if (!_source.TryCopyLatestCanonicalPose(_rawCanonicalFrame))
            {
                _rawCanonicalFrame.MarkUnavailable();
            }

            _lastEvaluationTimeSeconds = SanitizeTime(_source.EvaluationTimeSeconds);
            _stabilizer.Stabilize(_rawCanonicalFrame, _stabilizedFrame, _lastEvaluationTimeSeconds);
            _calibration.Update(_stabilizedFrame, _lastEvaluationTimeSeconds);

            _responsiveStabilizerA.Stabilize(
                _rawCanonicalFrame,
                _responsiveCanonicalFrameA,
                _lastEvaluationTimeSeconds);
            _responsiveStabilizerB.Stabilize(
                _rawCanonicalFrame,
                _responsiveCanonicalFrameB,
                _lastEvaluationTimeSeconds);
            _responsiveStabilizerC.Stabilize(
                _rawCanonicalFrame,
                _responsiveCanonicalFrameC,
                _lastEvaluationTimeSeconds);
            _responsiveStabilizerD.Stabilize(
                _rawCanonicalFrame,
                _responsiveCanonicalFrameD,
                _lastEvaluationTimeSeconds);

            var avatarDriveFrame = AvatarDriveFrame;
            _rotationSolver.Solve(avatarDriveFrame, _calibration.Profile, _rotationFrame);
            _kinematicTargetBuilder.Build(avatarDriveFrame, _calibration.Profile, _kinematicTargets);
            UpdateRichMotion();
        }

        public void BeginCalibration()
        {
            _kinematicTargetBuilder?.Reset();
            _richOrientationSolver?.ResetReferenceState();
            _richMotionFrame.Clear();
            _calibration?.Begin(_lastEvaluationTimeSeconds);
        }

        public void ResetCalibration()
        {
            _calibration?.Reset();
            _stabilizer?.Reset();
            ResetResponsiveAvatarStabilizers();
            _kinematicTargetBuilder?.Reset();
            _rotationFrame.Clear();
            _kinematicTargets.Clear();
            ClearRichMotionState(true);
        }

        private void UpdateRichMotion()
        {
            if (_richSource == null)
            {
                _richEvidenceFrame.Clear();
                _richMotionFrame.Clear();
                return;
            }

            _richSource.TryCopyLatestRichMotionEvidence(_richEvidenceFrame);
            _richOrientationSolver.Solve(_richEvidenceFrame, _richMotionFrame);
        }

        private void ClearRichMotionState(bool resetSolver)
        {
            _richEvidenceFrame.Clear();
            _richMotionFrame.Clear();
            if (resetSolver)
            {
                _richOrientationSolver?.Reset();
            }
        }

        private void ResetResponsiveAvatarStabilizers()
        {
            _responsiveStabilizerA?.Reset();
            _responsiveStabilizerB?.Reset();
            _responsiveStabilizerC?.Reset();
            _responsiveStabilizerD?.Reset();
            _responsiveCanonicalFrameA.Clear();
            _responsiveCanonicalFrameB.Clear();
            _responsiveCanonicalFrameC.Clear();
            _responsiveCanonicalFrameD.Clear();
        }

        private bool ResolveSource()
        {
            if (canonicalSourceBehaviour != null)
            {
                _source = canonicalSourceBehaviour as ICanonicalPoseSource;
                if (_source != null)
                {
                    _richSource = _source as IRichMotionEvidenceSource;
                    _richOrientationSolver?.Reset();
                    return true;
                }
            }

            var components = GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is ICanonicalPoseSource source)
                {
                    _source = source;
                    _richSource = source as IRichMotionEvidenceSource;
                    _richOrientationSolver?.Reset();
                    return true;
                }
            }

            _richSource = null;
            return false;
        }

        private static CanonicalStabilizerSettings CreateResponsiveAvatarSettings(
            float minCutoff,
            float beta)
        {
            return new CanonicalStabilizerSettings
            {
                acquireConfidence = 0.60f,
                sustainConfidence = 0.40f,
                acquireSamples = 2,
                lossGraceSeconds = 0.10f,
                resetAfterLossSeconds = 0.25f,
                minCutoff = minCutoff,
                beta = beta,
                derivativeCutoff = ResponsiveDerivativeCutoff,
                defaultDeltaTimeSeconds = 0.05f,
                maximumDeltaTimeSeconds = 0.25f,
            };
        }

        private static double SanitizeTime(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? Time.unscaledTimeAsDouble : value;
        }
    }
}
