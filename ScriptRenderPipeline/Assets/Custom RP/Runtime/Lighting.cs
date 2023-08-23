using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
	const string bufferName = "Lighting";
	CommandBuffer buffer = new CommandBuffer()
    {
		name = bufferName
    };

    static int dirLightCountID = Shader.PropertyToID("_DirectionalLightCount");
    static int dirLightColorID = Shader.PropertyToID("_DirectionalLightColors");
    static int dirLightDirectionID = Shader.PropertyToID("_DirectionalLightDirections");
    static int dirLightShadowDataID = Shader.PropertyToID("_DirectionalLightShadowData");

    static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static Vector4[] dirLightDirections = new Vector4[maxDirLightCount];
    static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

    CullingResults cullingResults;

    const int maxDirLightCount = 4;

    Shadow shadow = new Shadow();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSetting shadowSetting)
    {
        this.cullingResults = cullingResults;

		buffer.BeginSample(bufferName);

        shadow.Setup(context, cullingResults, shadowSetting);
        SetupLights();

        shadow.Render();

        buffer.EndSample(bufferName);

        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupLights()
    {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

        int dirLightCount = 0;
        for(int i = 0; i < visibleLights.Length; i ++)
        {
            VisibleLight visibleLight = visibleLights[i];
            SetupDirectionalLight(dirLightCount ++, ref visibleLight);
            if(dirLightCount >= maxDirLightCount)
                break;
        }

        buffer.SetGlobalInt(dirLightCountID, dirLightCount);
        buffer.SetGlobalVectorArray(dirLightColorID, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionID, dirLightDirections);
        buffer.SetGlobalVectorArray(dirLightShadowDataID, dirLightShadowData);
    }

	void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirLightShadowData[index] = shadow.ReserveDirectionalShadows(visibleLight.light, index);
    }

    public void Cleanup()
    {
        shadow.Cleanup();
    }
}
