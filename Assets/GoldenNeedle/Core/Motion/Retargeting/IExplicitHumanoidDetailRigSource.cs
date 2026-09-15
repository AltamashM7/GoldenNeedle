using UnityEngine;

namespace GoldenNeedle.Core.Motion.Retargeting
{
    /// <summary>
    /// Optional extension for procedural/debug rigs that expose Foundation E finger detail.
    /// The legacy explicit body-source contract deliberately remains unchanged so existing rigs stay valid.
    /// </summary>
    public interface IExplicitHumanoidDetailRigSource
    {
        bool TryGetDetailFingerBone(HumanoidFingerBoneId id, out Transform bone);
    }
}
