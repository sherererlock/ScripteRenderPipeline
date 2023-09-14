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
    static int cascadeDataId = Shader.PropertyToID("_CascadeData");
    static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");

    static int otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas");
    static int otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices");
    static int otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles");

    static int shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSetting shadowSetting;

    const int maxShadowDirectionalLightCount = 4, maxShadowedOtherLightCount = 16;
    const int maxCascades = 4;

    struct ShadowDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    ShadowDirectionalLight[] shadowDirectionalLights = new ShadowDirectionalLight[maxShadowDirectionalLightCount];
    int ShadowedDirectionalLightCount = 0, ShadowedOtherLightCount = 0;

    static Vector4[] cullingSpheres = new Vector4[maxCascades];
    static Vector4[] cascadeData = new Vector4[maxCascades];
    static Vector4[] otherShadowTiles = new Vector4[maxShadowedOtherLightCount];

    static Matrix4x4[] directionalShadowMatrices = new Matrix4x4[maxShadowDirectionalLightCount * maxCascades];
    static Matrix4x4[] otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

    static string[] directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7"
    };

    static string[] otherFilterKeywords = {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7",
    };

    static string[] cascadeBlendKeyWords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    bool useShadowMask;
    static string[] shadowMaskKeywords = {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };

    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public bool isPoint;
    }

    ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[maxShadowedOtherLightCount];

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSetting shadowSetting)
    {
        this.cullingResults = cullingResults;
        this.shadowSetting = shadowSetting;
        this.context = context;

        ShadowedDirectionalLightCount = ShadowedOtherLightCount = 0;

        buffer.BeginSample(bufferName);

        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();

        useShadowMask = false;
    }

    Vector4 atlasSizes;
    public void Render()
    {
        if(ShadowedDirectionalLightCount > 0)
            RenderDirectionalShadows();
        else
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);

        if (ShadowedOtherLightCount > 0)
            RenderOtherShadows();
        else
            buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);

        buffer.BeginSample(bufferName);

        buffer.SetGlobalInt(cascadeCountId, ShadowedDirectionalLightCount > 0 ? shadowSetting.directional.cascadeCount : 0);
        float f = 1f - shadowSetting.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1.0f / shadowSetting.maxDistance, 1.0f / shadowSetting.distanceFade,
            1f / (1f - f * f)));

        SetKeywords(shadowMaskKeywords, useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 :1 : -1);

        buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows()
    {
        int size = (int)shadowSetting.directional.atlasSize;
        atlasSizes.x = size;
        atlasSizes.y = 1f / size;

        buffer.GetTemporaryRT(dirShadowAtlasId, size, size, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 1f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = ShadowedDirectionalLightCount * shadowSetting.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = size / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i ++)
            RenderDirectionalShadow(i, split, tileSize);

        buffer.SetGlobalMatrixArray(dirShadowMatricesId, directionalShadowMatrices);
        buffer.SetGlobalVectorArray(cullingSpheresId, cullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);

        SetKeywords(cascadeBlendKeyWords, (int)shadowSetting.directional.cadcadeBlend - 1);
        SetKeywords(directionalFilterKeywords, (int)shadowSetting.directional.filterMode - 1);

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderOtherShadows()
    {
        int size = (int)shadowSetting.other.atlasSize;
        atlasSizes.z = size;
        atlasSizes.w = 1f / size;

        buffer.GetTemporaryRT(otherShadowAtlasId, size, size, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.SetGlobalFloat(shadowPancakingId, 0f);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = ShadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = size / split;

        for (int i = 0; i < ShadowedOtherLightCount;)
        {
            if(shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6;
            }
            else
            {
                RenderSpotShadows(i, split, tileSize);
                i += 1;
            }
        }

        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
        buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);

        SetKeywords(otherFilterKeywords, (int)shadowSetting.other.filter - 1);

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void SetKeywords(string[] keywords, int index)
    {
        for(int i = 0; i < keywords.Length; i ++)
        {
            if (i == index)
                buffer.EnableShaderKeyword(keywords[i]);
            else
                buffer.DisableShaderKeyword(keywords[i]);
        }
    }

    void RenderDirectionalShadow(int index, int split, int tileSize)
    {
        ShadowDirectionalLight light = shadowDirectionalLights[index];
        var shadowDrawSetting = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Orthographic);

        int cascadeCount = shadowSetting.directional.cascadeCount;
        int tileOffset = cascadeCount * index;

        float cullingFactor =
            Mathf.Max(0f, 0.8f - shadowSetting.directional.cascadeFade);

        for (int i = 0; i < cascadeCount; i ++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i, cascadeCount,
                shadowSetting.directional.CascadeRatios, tileSize, light.nearPlaneOffset, out Matrix4x4 view, out Matrix4x4 proj, out ShadowSplitData splitData);

            if (index == 0)
                SetCascadeData(i, splitData.cullingSphere, tileSize);

            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowDrawSetting.splitData = splitData;
            int tileIndex = tileOffset + i;
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            directionalShadowMatrices[tileIndex] = ConvertToAtlasMatrix(proj * view, offset, split);

            buffer.SetViewProjectionMatrices(view, proj);
            //buffer.SetGlobalDepthBias(0f, 0f);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();

            context.DrawShadows(ref shadowDrawSetting);
            buffer.SetGlobalDepthBias(0f, 0f);
        }

    }

    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(
            cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Perspective
        );

        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
        );

        shadowSettings.splitData = splitData;

        float texelSize = 2f / (tileSize * projectionMatrix.m00);
        float filterSize = texelSize * ((float)shadowSetting.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        SetOtherTileData(index, bias);

        otherShadowMatrices[index] = ConvertToAtlasMatrix(
            projectionMatrix * viewMatrix,
            SetTileViewport(index, split, tileSize), split
        );
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);
    }

    void RenderPointShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(
            cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Perspective
        );

        float texelSize = 2f / tileSize;
        float filterSize = texelSize * ((float)shadowSetting.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;

        float fovBias =
            Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;

        for (int i = 0; i < 6; i ++)
        {
            cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, (CubemapFace)i, fovBias, out Matrix4x4 viewMatrix,
                out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
            );

            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;

            shadowSettings.splitData = splitData;

            int tileIndex = index + i;

            SetOtherTileData(tileIndex, bias);
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);

            otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix,
                offset, split
            );

            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    void SetCascadeData(int index, Vector4 cullingSphere, int tileSize)
    {
        float texelSize = 2.0f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)shadowSetting.directional.filterMode + 1);
        cascadeData[index] = new Vector4(1.0f / cullingSphere.w, filterSize * 1.4142136f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cullingSpheres[index] = cullingSphere;
    }

    void SetOtherTileData(int index, float bias)
    {
        Vector4 data = Vector4.zero;
        data.w = bias;
        otherShadowTiles[index] = data;
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
        if (ShadowedOtherLightCount > 0)
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);

        ExecuteBuffer();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (ShadowedDirectionalLightCount >= maxShadowDirectionalLightCount
            || light.shadows == LightShadows.None 
            || light.shadowStrength <= 0.0f)
            return new Vector4(0f, 0f, 0f, -1f);

        float maskChannel = -1f;
        shadowDirectionalLights[ShadowedDirectionalLightCount] = new ShadowDirectionalLight
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            nearPlaneOffset = light.shadowNearPlane
        };

        LightBakingOutput lightBaking = light.bakingOutput;
        if (
            lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
        )
        {
            useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }

        if (!cullingResults.GetShadowCasterBounds(
            visibleLightIndex, out Bounds b
        ))
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        return new Vector4(light.shadowStrength, shadowSetting.directional.cascadeCount * ShadowedDirectionalLightCount++, light.shadowNormalBias, maskChannel) ;
    }

    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        if (ShadowedOtherLightCount >= maxShadowedOtherLightCount ||light.shadows == LightShadows.None || light.shadowStrength <= 0f)
            return new Vector4(0f, 0f, 0f, -1f);

        int maskChannel = -1;

        LightBakingOutput lightBaking = light.bakingOutput;
        if (
            lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
        )
        {
            maskChannel = lightBaking.occlusionMaskChannel;
            useShadowMask = true;
        }

        bool isPoint = light.type == LightType.Point;
        int newLightCount = ShadowedOtherLightCount + (isPoint ? 6 : 1);
        if(newLightCount > maxShadowedOtherLightCount || !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            return new Vector4(-light.shadowStrength, 0f, 0f, -1f);

        shadowedOtherLights[ShadowedOtherLightCount] = new ShadowedOtherLight()
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias,
            isPoint = isPoint
        };

        Vector4 data = new Vector4(light.shadowStrength, ShadowedOtherLightCount++, isPoint ? 1f : 0f, maskChannel);
        ShadowedOtherLightCount = newLightCount;

        return data;
    }

}
