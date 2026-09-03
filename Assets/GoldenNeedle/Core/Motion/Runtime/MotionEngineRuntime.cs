using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Rotation;
using GoldenNeedle.Core.Motion.Stabilization;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Runtime
{
    /// <summary>
    /// Single owner of the reusable Motion Engine pipeline:
    /// canonical source -> raw canonical -> stabilization -> calibration -> rotation output.
    /// Consumers read its preallocated frames and never rebuild provider-specific stages.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class MotionEngineRuntime : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour canonicalSourceBehaviour;
        [SerializeField] private CanonicalStabilizerSettings stabilizerSettings = new CanonicalStabilizerSettings();
        [SerializeField] private MotionCalibrationSettings calibrationSettings = new MotionCalibrationSettings();

        private ICanonicalPoseSource _source;
        private CanonicalPoseStabilizer _stabilizer;
        private MotionCalibrationSession _calibration;
        private CanonicalRotationSolver _rotationSolver;
        private CanonicalKinematicTargetBuilder _kinematicTargetBuilder;
        private readonly CanonicalPoseFrame _rawCanonicalFrame = new CanonicalPoseFrame();
        private readonly CanonicalPoseFrame _stabilizedFrame = new CanonicalPoseFrame();
        private readonly CanonicalRotationFrame _rotationFrame = new CanonicalRotationFrame();
        private readonly CanonicalKinematicTargets _kinematicTargets = new CanonicalKinematicTargets();
        private double _lastEvaluationTimeSeconds;
        private int _lastCoordinateConventionVersion = -1;

        public ICanonicalPoseSource Source => _source;
        public CanonicalPoseFrame RawCanonicalFrame => _rawCanonicalFrame;
        public CanonicalPoseFrame StabilizedFrame => _stabilizedFrame;
        public CanonicalRotationFrame RotationFrame => _rotationFrame;
        public CanonicalKinematicTargets KinematicTargets => _kinematicTargets;
        public CanonicalRotationSolver RotationSolver => _rotationSolver;
        public MotionCalibrationSession Calibration => _calibration;
        public double EvaluationTimeSeconds => _lastEvaluationTimeSeconds;
        public bool IsSourceReady => _source != null;

        private void Awake()
        {
            _stabilizer = new CanonicalPoseStabilizer(stabilizerSettings);
            _calibration = new MotionCalibrationSession(calibrationSettings);
            _rotationSolver = new CanonicalRotationSolver();
            _kinematicTargetBuilder = new CanonicalKinematicTargetBuilder();
            ResolveSource();
        }

        private void Update()
        {
            if (_source == null && !ResolveSource())
            {
                _rawCanonicalFrame.Clear();
                _stabilizedFrame.Clear();
                _rotationFrame.Clear();
                _kinematicTargets.Clear();
                return;
            }

            if (_source.CoordinateConventionVersion != _lastCoordinateConventionVersion)
            {
                _lastCoordinateConventionVersion = _source.CoordinateConventionVersion;
                _stabilizer.Reset();
                _calibration.Reset();
                _kinematicTargetBuilder.Reset();
                _rotationFrame.Clear();
                _kinematicTargets.Clear();
            }

            if (!_source.TryCopyLatestCanonicalPose(_rawCanonicalFrame))
            {
                _rawCanonicalFrame.MarkUnavailable();
            }

            _lastEvaluationTimeSeconds = SanitizeTime(_source.EvaluationTimeSeconds);
            _stabilizer.Stabilize(_rawCanonicalFrame, _stabilizedFrame, _lastEvaluationTimeSeconds);
            _calibration.Update(_stabilizedFrame, _lastEvaluationTimeSeconds);
            _rotationSolver.Solve(_stabilizedFrame, _calibration.Profile, _rotationFrame);
            _kinematicTargetBuilder.Build(_stabilizedFrame, _calibration.Profile, _kinematicTargets);
        }

        public void BeginCalibration()
        {
            _kinematicTargetBuilder?.Reset();
            _calibration?.Begin(_lastEvaluationTimeSeconds);
        }

        public void ResetCalibration()
        {
            _calibration?.Reset();
            _stabilizer?.Reset();
            _kinematicTargetBuilder?.Reset();
            _rotationFrame.Clear();
            _kinematicTargets.Clear();
        }

        private bool ResolveSource()
        {
            if (canonicalSourceBehaviour != null)
            {
                _source = canonicalSourceBehaviour as ICanonicalPoseSource;
                if (_source != null)
                {
                    return true;
                }
            }

            var components = GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is ICanonicalPoseSource source)
                {
                    _source = source;
                    return true;
                }
            }

            return false;
        }

        private static double SanitizeTime(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? Time.unscaledTimeAsDouble : value;
        }
    }
}
