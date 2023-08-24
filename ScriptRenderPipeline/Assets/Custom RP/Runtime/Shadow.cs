using UnityEngine;
using UnityEngine.Rendering;

public class Shadow
{
	const string bufferName = "Shadow";
	CommandBuffer buffer = new CommandBuffer()
    {
		name = bufferName
    };

    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
    static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
    static int cullingSpheresId = Shader.PropertyToID("_CullingSpheres");
    static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    static Vector4[] cullingSpheres = new Vector4[maxCascades];

    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSetting shadowSetting;

    const int maxShadowDirectionalLightCount = 4;
    const int maxCascades = 4;

    struct ShadowDirectionalLight
    {
        public int visibleLightIndex;
    }

    ShadowDirectionalLight[] shadowDirectionalLights = new ShadowDirectionalLight[maxShadowDirectionalLightCount];
    int ShadowedDirectionalLightCount = 0;

    static Matrix4x4[] directionalShadowMatrices = new Matrix4x4[maxShadowDirectionalLightCount * maxCascades];

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSetting shadowSetting)
    {
        this.cullingResults = cullingResults;
        this.shadowSetting = shadowSetting;
        this.context = context;

        ShadowedDirectionalLightCount = 0;

        buffer.BeginSample(bufferName);

        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Render()
    {
        if(ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
    }

    void RenderDirectionalShadows()
    {
        int size = (int)shadowSetting.directional.atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, size, size, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = ShadowedDirectionalLightCount * shadowSetting.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = size / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i ++)
        {
            RenderDirectionalShadow(i, split, tileSize);
        }

        float f = 1f - shadowSetting.directional.cascadeFade;

        buffer.SetGlobalMatrixArray(dirShadowMatricesId, directionalShadowMatrices);
        buffer.SetGlobalInt(cascadeCountId, shadowSetting.directional.cascadeCount);
        buffer.SetGlobalVectorArray(cullingSpheresId, cullingSpheres);
        buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4( 1.0f / shadowSetting.maxDistance, 1.0f / shadowSetting.distanceFade,
            1f / (1f - f * f)));

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadow(int index, int split, int tileSize)
    {
        ShadowDirectionalLight light = shadowDirectionalLights[index];
        var shadowDrawSetting = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Orthographic);

        int cascadeCount = shadowSetting.directional.cascadeCount;
        int tileOffset = cascadeCount * index;

        for (int i = 0; i < cascadeCount; i ++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i, cascadeCount,
                shadowSetting.directional.CascadeRatios, tileSize, 0f, out Matrix4x4 view, out Matrix4x4 proj, out ShadowSplitData splitData);

            if (index == 0)
            {
                Vector4 cullingSphere = splitData.cullingSphere;
                cullingSphere.w *= cullingSphere.w;
                cullingSpheres[i] = cullingSphere;
            }

            shadowDrawSetting.splitData = splitData;
            int tileIndex = tileOffset + i;
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            directionalShadowMatrices[tileIndex] = ConvertToAtlasMatrix(proj * view, offset, split);

            buffer.SetViewProjectionMatrices(view, proj);

            ExecuteBuffer();

            context.DrawShadows(ref shadowDrawSetting);
        }

    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }

        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;

        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);

        return m;
    }

    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(
            offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
        ));

        return offset;
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public Vector2 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (ShadowedDirectionalLightCount >= maxShadowDirectionalLightCount 
            || light.shadows == LightShadows.None 
            || light.shadowStrength <= 0.0f
            || !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            return Vector2.zero;

        shadowDirectionalLights[ShadowedDirectionalLightCount] = new ShadowDirectionalLight
        {
            visibleLightIndex = visibleLightIndex
        };

        return new Vector2(light.shadowStrength, shadowSetting.directional.cascadeCount * ShadowedDirectionalLightCount++);
    }

}
