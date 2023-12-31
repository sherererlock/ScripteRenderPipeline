using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
	ScriptableRenderContext context;
	Camera camera;

	const string bufferName = "Render Camera";
	CommandBuffer buffer = new CommandBuffer()
	{
		name = bufferName
	};

	CullingResults cullingResults;

	static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
	static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");
	static ShaderTagId shadowShaderTagId = new ShaderTagId("ShadowCaster");


	Lighting lighting = new Lighting();

	PostFXStack postFXStack = new PostFXStack();
	static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");

	bool useHDR;

	public void Render(ScriptableRenderContext context, Camera camera, bool allowHDR, bool useDynamicBatching, bool useGPUInstancing, bool useLightPerObject, ShadowSetting shadowSetting, PostFXSetting postFXSetting) {
		this.context = context;
		this.camera = camera;

		PrepareBuffer();
		PrepareForSceneWindow();
		if (!Cull(shadowSetting.maxDistance))
			return;

		useHDR = allowHDR && camera.allowHDR;

		buffer.BeginSample(SampleName);
		ExecuteBuffer();

		lighting.Setup(context, cullingResults, shadowSetting, useLightPerObject);

		postFXStack.Setup(context, camera, postFXSetting, useHDR);

		buffer.EndSample(SampleName);

		Setup();

		DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightPerObject);
		DrawUnSupportedShaders();

		DrawGizmosBeforeFX();

        if (postFXStack.IsActive)
            postFXStack.Render(frameBufferId);

		DrawGizmosAfterFX();

		Cleanup();

		Submit();
	}

	bool Cull(float maxDistance) {
		if (camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
			p.shadowDistance = Mathf.Min(maxDistance, camera.farClipPlane);
			cullingResults = context.Cull(ref p);
			return true;
		}
		return false;
	}

	void Setup()
	{
		context.SetupCameraProperties(camera);
		CameraClearFlags flags = camera.clearFlags;

		if (postFXStack.IsActive)
		{
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }

            buffer.GetTemporaryRT(
				frameBufferId, camera.pixelWidth, camera.pixelHeight,
				32, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
			);
			buffer.SetRenderTarget(
				frameBufferId,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
			);
		}

		buffer.ClearRenderTarget(
			flags <= CameraClearFlags.Depth,
			flags == CameraClearFlags.Color,
			flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);

		buffer.BeginSample(SampleName);
		ExecuteBuffer();
	}

    void Cleanup()
    {
        lighting.Cleanup();
        if (postFXStack.IsActive)
        {
            buffer.ReleaseTemporaryRT(frameBufferId);
        }
    }

    void Submit()
	{
		buffer.EndSample(SampleName);
		ExecuteBuffer();
		context.Submit();
	}

	void ExecuteBuffer()
	{
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightPerObject)
    {
        PerObjectData lightPerObjectFlags = useLightPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;

        var sortingSetting = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };

        var drawSettings = new DrawingSettings(unlitShaderTagId, sortingSetting)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.Lightmaps | PerObjectData.ShadowMask | PerObjectData.LightProbe | PerObjectData.OcclusionProbe | PerObjectData.LightProbeProxyVolume | PerObjectData.ReflectionProbes | PerObjectData.OcclusionProbeProxyVolume | lightPerObjectFlags,
        };

        drawSettings.SetShaderPassName(1, litShaderTagId);

        var filteringSetting = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref drawSettings, ref filteringSetting);

        context.DrawSkybox(camera);

        sortingSetting.criteria = SortingCriteria.CommonTransparent;
        drawSettings.sortingSettings = sortingSetting;
        filteringSetting.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers(cullingResults, ref drawSettings, ref filteringSetting);
    }
}
