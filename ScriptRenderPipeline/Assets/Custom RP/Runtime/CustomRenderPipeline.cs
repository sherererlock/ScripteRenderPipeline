using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();
    bool useDynamicBatching;
    bool useGPUInstancing;
    ShadowSetting shadowSetting;
    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useScriptableRenderPipelineBatching, ShadowSetting shadowSetting)
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        GraphicsSettings.useScriptableRenderPipelineBatching = useScriptableRenderPipelineBatching;
        GraphicsSettings.lightsUseLinearIntensity = true;
        this.shadowSetting = shadowSetting;
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach(Camera camera in cameras)
            renderer.Render(context, camera, useDynamicBatching, useGPUInstancing, shadowSetting);
    }
}
