using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true, useLightPerObject = true;

    [SerializeField]
    bool allowHDR = true;

    [SerializeField]
    ShadowSetting shadowSetting;

    [SerializeField]
    PostFXSetting postFXSetting = default;

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(allowHDR, useDynamicBatching, useGPUInstancing, useSRPBatcher, useLightPerObject, shadowSetting, postFXSetting);
    }
}
