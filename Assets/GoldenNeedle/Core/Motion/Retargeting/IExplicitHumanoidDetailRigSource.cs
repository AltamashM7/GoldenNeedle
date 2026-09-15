using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// Optional extension for procedural/debug rigs that expose Foundation E finger detail.
    /// IExplicitHumanoidRigSource deliberately remains unchanged so existing explicit rigs stay valid.
    /// </summary>
    public interface IExplicitHumanoidDetailRigSource
    {
        bool TryGetDetailFingerBone(HumanoidFingerBoneId id, out Transform bone);
    }
}
