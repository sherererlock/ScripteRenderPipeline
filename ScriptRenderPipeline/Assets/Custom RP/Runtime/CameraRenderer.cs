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

    public void Render (ScriptableRenderContext context, Camera camera) {
		this.context = context;
		this.camera = camera;

		PrepareBuffer();
		PrepareForSceneWindow();
		if (!Cull())
			return;

		Setup();
		DrawVisibleGeometry();
		DrawUnSupportedShaders();
		DrawGizmos();
		Submit();
	} 

	bool Cull () {
		if (camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
			cullingResults = context.Cull(ref p);
			return true;
		}
		return false;
	}

	void Setup()
    {
		context.SetupCameraProperties(camera);
		CameraClearFlags flag = camera.clearFlags;

		buffer.ClearRenderTarget(
			flag <= CameraClearFlags.Depth,
			flag == CameraClearFlags.Color, 
			flag == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);

		buffer.BeginSample(SampleName);
		ExecuteBuffer();
    }

	void DrawVisibleGeometry()
    {
		var sortingSetting = new SortingSettings(camera)
        {
			criteria = SortingCriteria.CommonOpaque
        };

		var drawSettings = new DrawingSettings(unlitShaderTagId, sortingSetting);
		var filteringSetting = new FilteringSettings(RenderQueueRange.opaque);
		context.DrawRenderers(cullingResults, ref drawSettings, ref filteringSetting);

		context.DrawSkybox(camera);

		sortingSetting.criteria = SortingCriteria.CommonTransparent;
		drawSettings.sortingSettings = sortingSetting;
		filteringSetting.renderQueueRange = RenderQueueRange.transparent;

		context.DrawRenderers(cullingResults, ref drawSettings, ref filteringSetting);
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
}
