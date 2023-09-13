using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();
    bool useDynamicBatching;
    bool useGPUInstancing;
    bool useLightPerObject;
    ShadowSetting shadowSetting;
    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useScriptableRenderPipelineBatching, bool useLightPerObject, ShadowSetting shadowSetting)
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightPerObject = useLightPerObject;
        GraphicsSettings.useScriptableRenderPipelineBatching = useScriptableRenderPipelineBatching;
        GraphicsSettings.lightsUseLinearIntensity = true;
        this.shadowSetting = shadowSetting;

        InitializeForEditor();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach(Camera camera in cameras)
            renderer.Render(context, camera, useDynamicBatching, useGPUInstancing, useLightPerObject, shadowSetting);
    }
}
