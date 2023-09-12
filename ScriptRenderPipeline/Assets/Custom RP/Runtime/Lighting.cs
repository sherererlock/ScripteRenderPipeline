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

    static int otherLightCountID = Shader.PropertyToID("_OtherLightCount");
    static int otherLightColorID = Shader.PropertyToID("_OtherLightColors");
    static int otherLightPositionsID = Shader.PropertyToID("_OtherLightPositions");
    static int otherLightDirectionsID = Shader.PropertyToID("_OtherLightDirections");
    static int otherLightSpotsID = Shader.PropertyToID("_OtherLightSpots");

    const int maxDirLightCount = 4, maxOtherLightCount = 64;

    static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static Vector4[] dirLightDirections = new Vector4[maxDirLightCount];
    static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

    static Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightDirections = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightSpots = new Vector4[maxOtherLightCount];

    CullingResults cullingResults;

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

        int dirLightCount = 0, otherLightCount = 0;
        for(int i = 0; i < visibleLights.Length; i ++)
        {
            VisibleLight visibleLight = visibleLights[i];
            switch(visibleLight.lightType)
            {
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount)
                        SetupDirectionalLight(dirLightCount++, ref visibleLight);
            
                    break;
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount)
                        SetupPointLight(otherLightCount++, ref visibleLight);
                    break;

                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount)
                        SetupSpotLight(otherLightCount++, ref visibleLight);

                    break;
            }

        }

        buffer.SetGlobalInt(dirLightCountID, dirLightCount);
        if (dirLightCount > 0)
        {
            buffer.SetGlobalVectorArray(dirLightColorID, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionID, dirLightDirections);
            buffer.SetGlobalVectorArray(dirLightShadowDataID, dirLightShadowData);
        }

        buffer.SetGlobalInt(otherLightCountID, otherLightCount);
        if(otherLightCount > 0)
        {
            buffer.SetGlobalVectorArray(otherLightColorID, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsID, otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsID, otherLightDirections);
            buffer.SetGlobalVectorArray(otherLightSpotsID, otherLightSpots);
        }
    }

	void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirLightShadowData[index] = shadow.ReserveDirectionalShadows(visibleLight.light, index);
    }

    void SetupPointLight(int index, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;
        otherLightPositions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        otherLightPositions[index].w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightSpots[index] = new Vector4(0f, 1f);
    }

    void SetupSpotLight(int index, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;
        otherLightPositions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        otherLightPositions[index].w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);

        Light light = visibleLight.light;
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        otherLightSpots[index] = new Vector4(
            angleRangeInv, -outerCos * angleRangeInv
        );

    }

    public void Cleanup()
    {
        shadow.Cleanup();
    }
}
