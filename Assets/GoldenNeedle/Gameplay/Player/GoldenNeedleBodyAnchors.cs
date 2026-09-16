using GoldenNeedle.Core.Motion.Retargeting;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Player
{
    [DisallowMultipleComponent]
    public sealed class GoldenNeedleBodyAnchors : MonoBehaviour
    {
        [SerializeField] private HumanoidRigBinding rigBinding;

        public Transform LeftWrist => Resolve(CanonicalKinematicChainId.LeftArm);
        public Transform RightWrist => Resolve(CanonicalKinematicChainId.RightArm);
        public Transform LeftFoot => Resolve(CanonicalKinematicChainId.LeftLeg);
        public Transform RightFoot => Resolve(CanonicalKinematicChainId.RightLeg);

        private void Awake()
        {
            ResolveBinding();
        }

        private Transform Resolve(CanonicalKinematicChainId id)
        {
            ResolveBinding();
            return rigBinding != null && rigBinding.IsBound && rigBinding.IsChainAvailable(id)
                ? rigBinding.GetChainTip(id)
                : null;
        }

        private void ResolveBinding()
        {
            rigBinding = rigBinding == null ? GetComponent<HumanoidRigBinding>() : rigBinding;
        }
    }
}
