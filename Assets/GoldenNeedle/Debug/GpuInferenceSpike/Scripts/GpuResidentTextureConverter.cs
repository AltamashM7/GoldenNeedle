using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoldenNeedle.Debugging.GpuInferenceSpike
{
    internal static class TextureConverter
    {
        public static void ToTensor(
            RenderTexture source,
            Tensor<float> tensor,
            TextureTransform transform)
        {
            var commandBuffer =
                CommandBufferPool.Get("GoldenNeedle GPU Spike Texture->Tensor");
            try
            {
                commandBuffer.Clear();
                Unity.InferenceEngine.TextureConverter.ToTensor(
                    commandBuffer,
                    new RenderTargetIdentifier(source),
                    tensor,
                    transform);
                Graphics.ExecuteCommandBuffer(commandBuffer);
            }
            finally
            {
                CommandBufferPool.Release(commandBuffer);
            }
        }
    }
}
