using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{
    enum Pass
    {
        BloomPrefilterFireFlies,
        BloomPrefilter,
        BloomHorizontal,
        BloomVertical,
        BloomAdd,
        BloomScatter,
        BloomScatterFinal,
        Copy
    }

	const string bufferName = "Post FX";
	CommandBuffer buffer = new CommandBuffer()
    {
		name = bufferName
    };

    ScriptableRenderContext context;
    Camera camera;
    PostFXSetting postFXSetting;

    public bool IsActive => postFXSetting != null;

    int fxSourceId = Shader.PropertyToID("_PostFXSource");
    int fxSource2Id = Shader.PropertyToID("_PostFXSource2");
    int bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling");
    int bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter");
    int bloomThresholdId = Shader.PropertyToID("_BloomThreshold");
    int bloomIntensityId = Shader.PropertyToID("_BloomIntensity");

    const int maxBloomPyramidLevels = 16;

    int bloomPyramidId;

    bool useHDR;

    public PostFXStack()
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSetting postFXSetting, bool useHDR)
    {
        this.context = context;
        this.camera = camera;
        this.postFXSetting = camera.cameraType <= CameraType.SceneView ? postFXSetting : null;
        this.useHDR = useHDR;

        ApplySceneViewState();
    }

    public void Render(int sourceId)
    {
        //buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
        //Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
        DoBloom(sourceId);

        ExecuteBuffer();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, postFXSetting.Material, (int)pass, MeshTopology.Triangles, 3);
    }

    void DoBloom(int sourceId)
    {
        buffer.BeginSample("Bloom");

        PostFXSetting.BloomSettings bloom = postFXSetting.Bloom;
        int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
        if ( bloom.maxIterations == 0 || bloom.intensity <= 0f || height < bloom.downScaleLimit * 2 || width < bloom.downScaleLimit * 2)
        {
            Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
            buffer.EndSample("Bloom");
            return;
        }

        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId, threshold);

        RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        buffer.GetTemporaryRT(bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        Draw(sourceId, bloomPrefilterId, bloom.fadeFireFlies ? Pass.BloomPrefilterFireFlies : Pass.BloomPrefilter);

        width /= 2;
        height /= 2;

        int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;

        int i;
        for (i = 0; i < bloom.maxIterations; i++)
        {
            if (height < bloom.downScaleLimit || width < bloom.downScaleLimit)
                break;

            int midId = toId - 1;
            buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
            buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
            Draw(fromId, midId, Pass.BloomHorizontal);
            Draw(midId, toId, Pass.BloomVertical);
            fromId = toId;
            toId += 2;

            width /= 2;
            height /= 2;
        }

        //Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
        buffer.ReleaseTemporaryRT(fromId - 1);
        toId -= 5;

        buffer.SetGlobalFloat(bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);

        Pass combinePass, finalPass;
        float finalIntensity;
        if (bloom.mode == PostFXSetting.BloomSettings.Mode.Additive)
        {
            combinePass = finalPass = Pass.BloomAdd;
            buffer.SetGlobalFloat(bloomIntensityId, 1f);
            finalIntensity = bloom.intensity;
        }
        else
        {
            combinePass = Pass.BloomScatter;
            finalPass = Pass.BloomScatterFinal;
            buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
            finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
        }

        if (i > 1)
        {
            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, combinePass);

                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);

                fromId = toId;
                toId -= 2;
            }
        }
        else
        {
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }

        buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);

        buffer.SetGlobalTexture(fxSource2Id, sourceId);
        Draw(fromId, BuiltinRenderTextureType.CameraTarget, finalPass);
        buffer.ReleaseTemporaryRT(fromId);

        buffer.EndSample("Bloom");
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

}
