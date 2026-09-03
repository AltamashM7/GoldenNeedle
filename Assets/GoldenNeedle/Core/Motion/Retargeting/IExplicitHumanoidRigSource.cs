using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// Optional project-owned source for procedural/debug hierarchies. The binding consumes the
    /// same cached Transform array used by the Animator Humanoid path.
    /// </summary>
    public interface IExplicitHumanoidRigSource
    {
        bool TryGetBinding(Transform[] destinationBones, out Transform avatarRoot);
    }

    /// <summary>
    /// Optional endpoint extension for explicit rigs. Endpoints are the actual Hand/Foot
    /// transforms, not a renderer or a synthetic point.
    /// </summary>
    public interface IExplicitHumanoidChainSource
    {
        bool TryGetChainTip(CanonicalKinematicChainId chainId, out Transform tip);
    }
}
