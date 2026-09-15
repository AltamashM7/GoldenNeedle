using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// Provider-independent optional post-solve detail boundary. HumanoidRetargeter owns the
    /// accepted Phase 4 solve and invokes these layers only after that exact solve has completed.
    /// Implementations must degrade locally: an absent or disabled layer leaves Phase 4 unchanged.
    /// </summary>
    public interface IHumanoidPostSolveDetailLayer
    {
        bool IsPostSolveDetailEnabled { get; }

        void ApplyPostSolveDetail(HumanoidRigBinding binding, float deltaTime);

        /// <summary>
        /// Resets only layer-owned additive/reference state. It must not invalidate or rebuild the
        /// legacy body binding.
        /// </summary>
        void ResetPostSolveDetailState(HumanoidRigBinding binding);
    }
}
