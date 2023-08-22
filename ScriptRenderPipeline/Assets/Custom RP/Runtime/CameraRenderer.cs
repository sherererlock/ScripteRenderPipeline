using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraRenderer
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

		if(!Cull())
			return;

		Setup();
		DrawVisibleGeometry();
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
		buffer.ClearRenderTarget(true, true, Color.clear);
		buffer.BeginSample(bufferName);
		ExecuteBuffer();
    }

	void DrawVisibleGeometry()
    {
		var sortingSetting = new SortingSettings(camera)
        {
			criteria = SortingCriteria.CommonOpaque
        };

		var drawSettings = new DrawingSettings(unlitShaderTagId, sortingSetting);

		var filteringSetting = new FilteringSettings(RenderQueueRange.all);

		context.DrawRenderers(cullingResults, ref drawSettings, ref filteringSetting);

		context.DrawSkybox(camera);
    }

	void Submit()
    {
		buffer.EndSample(bufferName);
		ExecuteBuffer();
		context.Submit();
    }

	void ExecuteBuffer()
    {
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
    }
}
