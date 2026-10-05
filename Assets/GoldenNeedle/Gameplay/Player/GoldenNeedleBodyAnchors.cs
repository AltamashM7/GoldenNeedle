using HDMotionEngine;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Player
{
    [DisallowMultipleComponent]
    public sealed class GoldenNeedleBodyAnchors : MonoBehaviour
    {
        [SerializeField] private MotionEngineController motionEngine;

        public Transform LeftWrist => Resolve(BodyAnchor.LeftWrist);
        public Transform RightWrist => Resolve(BodyAnchor.RightWrist);
        public Transform LeftFoot => Resolve(BodyAnchor.LeftFoot);
        public Transform RightFoot => Resolve(BodyAnchor.RightFoot);

        private void Awake()
        {
            ResolveBinding();
        }

        private Transform Resolve(BodyAnchor id)
        {
            ResolveBinding();
            return motionEngine == null ? null : motionEngine.GetBodyAnchor(id);
        }

        private void ResolveBinding()
        {
            if (motionEngine == null) motionEngine = GetComponent<MotionEngineController>();
        }
    }
}
