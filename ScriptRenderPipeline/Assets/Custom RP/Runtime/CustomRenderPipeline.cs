using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();
    bool allowHDR;
    bool useDynamicBatching;
    bool useGPUInstancing;
    bool useLightPerObject;
    ShadowSetting shadowSetting;
    PostFXSetting postFXSetting;
    public CustomRenderPipeline(bool allowHDR, bool useDynamicBatching, bool useGPUInstancing, bool useScriptableRenderPipelineBatching, bool useLightPerObject, ShadowSetting shadowSetting, PostFXSetting postFXSetting)
    {
        this.allowHDR = allowHDR;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightPerObject = useLightPerObject;
        GraphicsSettings.useScriptableRenderPipelineBatching = useScriptableRenderPipelineBatching;
        GraphicsSettings.lightsUseLinearIntensity = true;
        this.shadowSetting = shadowSetting;
        this.postFXSetting = postFXSetting;

        InitializeForEditor();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach(Camera camera in cameras)
            renderer.Render(context, camera, allowHDR, useDynamicBatching, useGPUInstancing, useLightPerObject, shadowSetting, postFXSetting);
    }
}
