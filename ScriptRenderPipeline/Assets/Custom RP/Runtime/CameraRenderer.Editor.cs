using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
	partial void DrawUnSupportedShaders();
	partial void DrawGizmosBeforeFX();
	partial void DrawGizmosAfterFX();
	partial void PrepareForSceneWindow();
	partial void PrepareBuffer();

#if UNITY_EDITOR

	string SampleName { get; set; }

	static ShaderTagId[] legacyShaderTagIds = {
		new ShaderTagId("Always"),
		new ShaderTagId("ForwardBase"),
		new ShaderTagId("PrepassBase"),
		new ShaderTagId("Vertex"),
		new ShaderTagId("VertexLMRGBM"),
		new ShaderTagId("VertexLM")
	};

	static Material errorMaterial;

	partial void DrawUnSupportedShaders()
	{
		if (errorMaterial == null)
			errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));

		var drawSetting = new DrawingSettings(
			legacyShaderTagIds[0], new SortingSettings(camera)
		)
		{
			overrideMaterial = errorMaterial
		};

		for (int i = 0; i < legacyShaderTagIds.Length; i++)
			drawSetting.SetShaderPassName(i, legacyShaderTagIds[i]);

		var filteringSetting = FilteringSettings.defaultValue;

		context.DrawRenderers(cullingResults, ref drawSetting, ref filteringSetting);

	}

    partial void DrawGizmosBeforeFX()
    {
		if(Handles.ShouldRenderGizmos())
			context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
    }

    partial void DrawGizmosAfterFX()
    {
        if (Handles.ShouldRenderGizmos())
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
    }

    partial void PrepareForSceneWindow()
    {
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }

	partial void PrepareBuffer()
    {
		buffer.name = SampleName = camera.name;
    }

#else
	const string SampleName = bufferName;
#endif

}
