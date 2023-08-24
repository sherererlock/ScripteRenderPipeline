# ScripteRenderPipeline

```mermaid
classDiagram
class RenderPipelineAsset{
	+ RenderPipeline renderPipeline
	+ RenderPipeline CreateRenderPipeline();
}

class RenderPipeline{
	+ CameraRenderer renderer;
	+ void Render (ScriptableRenderContext context, Camera camera)
}

class CameraRenderer{
	+ScriptableRenderContext contexy;
	+Camera camera;
    +CullingResults cullingResults;
    + void Render (ScriptableRenderContext context, Camera camera)
}

class Lighting{
	+ void Setup(ScriptableRenderContext context, CullingResults cullingResults)；
}

RenderPipeline *-- CameraRenderer
CameraRenderer o-- ScriptableRenderContext
CameraRenderer o-- camera
CameraRenderer *-- Lighting
```

流程

```c++
void CustomRenderPipeline::Render(ScriptableRenderContext context, Camera[] cameras)
{
    void CameraRenderer::Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing) 
    {
    	Setup(); // 相机属性， clear,
        void Lighting::Setup(ScriptableRenderContext context, CullingResults cullingResults)
        {
            // 将Light数据传输到GPU
        }
        
        void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
        {
            // 不透明物体渲染
            // skybox渲染
            // 半透明物体渲染
        }
        
        void Submit()
        {
            // 提交命令
        }
    }
}
```

shader

```mermaid
classDiagram
class Surface{
    float3 position;
    float3 color;
    float3 normal;
    float3 viewDirection;
    float depth;
    float alpha;
    float metallic;
    float smoothness;	
}

class Light{
    float3 color;
    float3 direction;
    float attenuation;
}

class ShadowData{
	int cascadeIndex;
	float strength;
}

class DirectionalShadowData {
	float shadowStrength;
	int tileIndex;
	float normalBias;
}

class BRDF {
    float3 diffuse;
    float3 specular;
    float roughness;
}


```

流程

```c#
float4 LitPassFragment(Varings input)
{
    // 构造Surface
    
    // 根据Surface计算BRDF
    
    // 计算光照
    float3 GetLighting(Surface surface, BRDF brdf)
    {
        
    }
}
```

