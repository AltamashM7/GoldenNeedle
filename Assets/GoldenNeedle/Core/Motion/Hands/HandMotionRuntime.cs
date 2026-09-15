using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Runtime;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Hands
{
    /// <summary>
    /// Additive Foundation D runtime. It reads the already-produced body frame after
    /// MotionEngineRuntime and exposes optional synchronized hands without making body pose depend on hands.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class HandMotionRuntime : MonoBehaviour
    {
        [SerializeField] private MotionEngineRuntime bodyRuntime;
        [SerializeField] private MonoBehaviour handSourceBehaviour;

        private ICanonicalHandSource _handSource;
        private readonly CanonicalHandFrame _handFrame = new CanonicalHandFrame();

        public CanonicalHandFrame HandFrame => _handFrame;
        public ICanonicalHandSource HandSource => _handSource;
        public bool IsHandSourceAvailable => _handSource != null;
        public string HandSummary => _handSource == null
            ? "Hands: source unavailable"
            : _handSource.HandDiagnosticSummary;

        private void Awake()
        {
            bodyRuntime = bodyRuntime == null ? GetComponent<MotionEngineRuntime>() : bodyRuntime;
            ResolveSource();
        }

        private void Update()
        {
            if (bodyRuntime == null)
            {
                bodyRuntime = GetComponent<MotionEngineRuntime>();
            }
            if (_handSource == null && !ResolveSource())
            {
                _handFrame.Clear();
                return;
            }

            var bodyFrame = bodyRuntime == null ? null : bodyRuntime.RawCanonicalFrame;
            var evaluation = bodyRuntime == null ? Time.unscaledTimeAsDouble : bodyRuntime.EvaluationTimeSeconds;
            try
            {
                _handSource.TryCopyLatestCanonicalHands(bodyFrame, evaluation, _handFrame);
            }
            catch (System.Exception exception)
            {
                // Hands are optional. Never allow a hand subsystem fault to break the accepted body runtime.
                Debug.LogWarning($"[Foundation D] Hand source degraded without affecting body tracking: {exception.Message}");
                _handFrame.Clear();
                _handSource = null;
            }
        }

        private bool ResolveSource()
        {
            if (handSourceBehaviour is ICanonicalHandSource explicitSource)
            {
                _handSource = explicitSource;
                return true;
            }

            var components = GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is ICanonicalHandSource source)
                {
                    _handSource = source;
                    return true;
                }
            }
            return false;
        }
    }
}
