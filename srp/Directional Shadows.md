# Directional Shadows

## Rendering Shadows

### Shadow Settings

- max Distance
- Texture Size

1. 创建Shadow Settings类

   ```c#
   using UnityEngine;
   
   [System.Serializable]
   public class ShadowSettings {
   
   	[Min(0f)]
   	public float maxDistance = 100f;
      	public enum TextureSize {
   		_256 = 256, _512 = 512, _1024 = 1024,
   		_2048 = 2048, _4096 = 4096, _8192 = 8192
   	}
       
   	[System.Serializable]
   	public struct Directional {
   		public TextureSize atlasSize;
   	}
       
   	public Directional directional = new Directional {
   		atlasSize = TextureSize._1024
   	};    
   }
   ```

2. 为`CustomRenderPipelineAsset`添加`ShadowSettings`成员变量

   ```c#
   	[SerializeField]
   	ShadowSettings shadows = default;
   
   	protected override RenderPipeline CreatePipeline () {
   		return new CustomRenderPipeline(
   			useDynamicBatching, useGPUInstancing, useSRPBatcher, shadows
   		);
   	}
   ```

3. `CustomRenderPipeline`

   ```c#
   	ShadowSettings shadowSettings;
   
   	public CustomRenderPipeline (
   		bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,
   		ShadowSettings shadowSettings
   	) {
   		this.shadowSettings = shadowSettings;
   		…
   	}
   
   	protected override void Render (
   		ScriptableRenderContext context, Camera[] cameras
   	) {
   		foreach (Camera camera in cameras) {
   			renderer.Render(
   				context, camera, useDynamicBatching, useGPUInstancing,
   				shadowSettings
   			);
   		}
   	}
   ```

4. `CameraRenderer`

   ```c#
   	public void Render (
   		ScriptableRenderContext context, Camera camera,
   		bool useDynamicBatching, bool useGPUInstancing,
   		ShadowSettings shadowSettings
   	) {
   		…
   		if (!Cull(shadowSettings.maxDistance)) {
   			return;
   		}
   
   		Setup();
   		lighting.Setup(context, cullingResults, shadowSettings);
   		…
   	}
   
   	bool Cull (float maxShadowDistance) {
   		if (camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
   			p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
   			cullingResults = context.Cull(ref p);
   			return true;
   		}
   		return false;
       }
   
   
   ```

5. `Lighting`

   ```c#
   	public void Setup (
   		ScriptableRenderContext context, CullingResults cullingResults,
   		ShadowSettings shadowSettings
   	) { … }
   ```

### Shadows Class

1. 创建单独的Shadow.cs来处理阴影

   ```c#
   using UnityEngine;
   using UnityEngine.Rendering;
   
   public class Shadows {
   
   	const string bufferName = "Shadows";
   
   	CommandBuffer buffer = new CommandBuffer {
   		name = bufferName
   	};
   
   	ScriptableRenderContext context;
   
   	CullingResults cullingResults;
   
   	ShadowSettings settings;
   
   	public void Setup (
   		ScriptableRenderContext context, CullingResults cullingResults,
   		ShadowSettings settings
   	) {
   		this.context = context;
   		this.cullingResults = cullingResults;
   		this.settings = settings;
   	}
   
   	void ExecuteBuffer () {
   		context.ExecuteCommandBuffer(buffer);
   		buffer.Clear();
   	}
   }
   ```

2. 在Lighting中创建成员变量，并且初始化shadow

   ```c#
   	Shadows shadows = new Shadows();
   
   	public void Setup (…) {
   		this.cullingResults = cullingResults;
   		buffer.BeginSample(bufferName);
   		shadows.Setup(context, cullingResults, shadowSettings);
   		SetupLights();
   		…
   	}
   ```

### Lights with Shadows

可支持投影的最大光源数

```c#
	const int maxShadowedDirectionalLightCount = 1;
```

投射阴影的光源信息

```c#
	struct ShadowedDirectionalLight {
		public int visibleLightIndex;
	}

	ShadowedDirectionalLight[] ShadowedDirectionalLights =
		new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
```

获取光源信息并保存

```c#
	int ShadowedDirectionalLightCount;

	…
	
	public void Setup (…) {
		…
		ShadowedDirectionalLightCount = 0;
	}
	
	public void ReserveDirectionalShadows (Light light, int visibleLightIndex) {
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount && //支持数量限制
			light.shadows != LightShadows.None && light.shadowStrength > 0f && //光源条件
           	cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b) // 光源的距离
           ) {
			ShadowedDirectionalLights[ShadowedDirectionalLightCount++] =
				new ShadowedDirectionalLight {
					visibleLightIndex = visibleLightIndex
				};
		}
	}
```

在Lighting中调用

```c#
	void SetupDirectionalLight (int index, ref VisibleLight visibleLight) {
		dirLightColors[index] = visibleLight.finalColor;
		dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
		shadows.ReserveDirectionalShadows(visibleLight.light, index);
	}
```

### Creating the Shadow Atlas

在Lighting中调用shadows.Render

```c#
		shadows.Setup(context, cullingResults, shadowSettings);
		SetupLights();
		shadows.Render();
```

Shadow中创建Render方法

```c#
	public void Render () {
		if (ShadowedDirectionalLightCount > 0) {
			RenderDirectionalShadows();
		}
	}

	void RenderDirectionalShadows () {}
```

贴图的创建

```c#
static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");

void RenderDirectionalShadows () {
    int atlasSize = (int)settings.directional.atlasSize;
    buffer.GetTemporaryRT(
        dirShadowAtlasId, atlasSize, atlasSize, // id和尺寸
        32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap // 像素大小，采样方式，格式
    );
}
```

贴图的清理

```c#
// shadow中	
public void Cleanup () {
		buffer.ReleaseTemporaryRT(dirShadowAtlasId);
		ExecuteBuffer();
	}

// lighting中
public void Cleanup () {
    shadows.Cleanup();
}

// cameraRender中，渲染完场景后
public void Render (…) {
    …
    lighting.Cleanup();
    Submit();
}
```

我们只有在先认领纹理的情况下才能释放它，目前我们只在有方向性阴影需要渲染时才会这样做。显而易见的解决方案是，只有在有阴影时才释放纹理。然而，不声称一个纹理将导致WebGL 2.0的问题，因为它将纹理和采样器绑定在一起。当一个带有我们的着色器的材质在纹理缺失的情况下被加载时，它将会失败，因为它将会得到一个默认的纹理，而这个纹理将不会与阴影采样器兼容。我们可以通过引入shader关键字来避免这种情况，生成省略阴影采样代码的shader变体。另一种方法是在不需要阴影的时候得到一个1×1的假纹理，避免额外的着色器变体。让我们这样做吧。

```c#
	public void Render () {
		if (shadowedDirLightCount > 0) {
			RenderDirectionalShadows();
		}
		else {
			buffer.GetTemporaryRT(
				dirShadowAtlasId, 1, 1,
				32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
			);
		}
	}
```

设置渲染目标

```c#
		buffer.GetTemporaryRT(…);
		buffer.SetRenderTarget(
			dirShadowAtlasId,
			RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store //如何loaded，如何stored
		);

		buffer.ClearRenderTarget(true, false, Color.clear); //清除渲染目标
		ExecuteBuffer();
```

### Shadows First

在设置相机之前就渲染阴影，并且将阴影的流程嵌套在MainCamera中

```c#
// CameraRender.cs
buffer.BeginSample(SampleName);
ExecuteBuffer();
lighting.Setup(context, cullingResults, shadowSettings);
buffer.EndSample(SampleName);
Setup();
```

### Rendering

`RenderDirectionalShadows`辅助函数来渲染每一个光源产生的阴影

```c#
	void RenderDirectionalShadows () {
		…
		buffer.ClearRenderTarget(true, false, Color.clear);
		buffer.BeginSample(bufferName);
		ExecuteBuffer();

		for (int i = 0; i < ShadowedDirectionalLightCount; i++) {
			RenderDirectionalShadows(i, atlasSize);
		}
		
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}	

	void RenderDirectionalShadows (int index, int tileSize) {}
```

`ShadowDrawingSettings`, 保存了哪个光源和split设置

```c#
	void RenderDirectionalShadows (int index, int tileSize) {
		ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
		var shadowSettings =
			new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
	}
```

阴影图的概念是，我们从光线的角度渲染场景，只存储深度信息。其结果是告诉我们，光线在击中某物之前走了多远。

然而，定向光被认为是无限远的，因此没有一个真实的位置。所以我们要做的是找出与灯光方向相匹配的视图和投影矩阵，并给我们一个剪辑空间立方体，该立方体与摄像机可见的区域相重叠，可以包含灯光的阴影。与其自己想办法，我们不如使用`CullingResults`的`ComputeDirectionalShadowMatricesAndCullingPrimitives`方法来帮我们做这件事，给它传递九个参数。

`ShadowSplitData`:包含关于如何剔除投射阴影的对象的信息

```c#
		var shadowSettings =
			new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
		cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
			light.visibleLightIndex, 0, 1, Vector3.zero, tileSize, 0f,
			out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
			out ShadowSplitData splitData
		);

		shadowSettings.splitData = splitData;
		buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
		ExecuteBuffer();
		context.DrawShadows(ref shadowSettings);
```

### Shadow Caster Pass

材质shader中必须要有`ShadowCaster pass`才能渲染阴影

```c#
	SubShader {
		Pass {
			Tags {
				"LightMode" = "CustomLit"
			}

			…
		}

		Pass {
			Tags {
				"LightMode" = "ShadowCaster"
			}

			ColorMask 0 // 不写color buffer

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _CLIPPING
			#pragma multi_compile_instancing
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#include "ShadowCasterPass.hlsl"
			ENDHLSL
		}
	}
```

`ShadowCasterPass.hlsl`

```c
#ifndef CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#define CUSTOM_SHADOW_CASTER_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings ShadowCasterPassVertex (Attributes input) {
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(positionWS);

	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw;
	return output;
}

void ShadowCasterPassFragment (Varyings input) { // 不写颜色所以没有返回值
	UNITY_SETUP_INSTANCE_ID(input);
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	float4 base = baseMap * baseColor;
	#if defined(_CLIPPING)
		clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
	#endif
}

#endif
```

在frame debugger中检查shader caster pass的生成的shadow map，并尝试改动maxdistance观察变化

正交投影

### Multiple Lights

支持最大四个光源同时生成阴影

```c#
const int maxShadowedDirectionalLightCount = 4;
```

分割计算每个tile的size

```c#
	void RenderDirectionalShadows () {
		…
		
		int split = ShadowedDirectionalLightCount <= 1 ? 1 : 2;
		int tileSize = atlasSize / split;

		for (int i = 0; i < ShadowedDirectionalLightCount; i++) {
			RenderDirectionalShadows(i, split, tileSize);
		}
	}
	
	void RenderDirectionalShadows (int index, int split, int tileSize) { … }
```

计算每个viewport

```c#
	void SetTileViewport (int index, int split) {
		Vector2 offset = new Vector2(index % split, index / split);
		buffer.SetViewport(new Rect(
			offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
		));
	}


SetTileViewport(index, split, tileSize);
buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
```

## Sampling Shadows

### Shadow Matrices

将shader point转换到Light空间下，根据正确的uv要在shader中采样深度贴图。

计算各个tile下的uv值

```c#
	static int
		dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
		dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
		
	static Matrix4x4[]
		dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount];

	void RenderDirectionalShadows () {
		…

		buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}

	void RenderDirectionalShadows (int index, int split, int tileSize) {
		…
		SetTileViewport(index, split, tileSize);
		dirShadowMatrices[index] = projectionMatrix * viewMatrix;
		buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
		…
	}
```

根据offset转换VP

```c#
	Vector2 SetTileViewport (int index, int split, float tileSize) {
		…
		return offset;
	}

dirShadowMatrices[index] = ConvertToAtlasMatrix(
    projectionMatrix * viewMatrix,
    SetTileViewport(index, split, tileSize), split
);

/*
第二，剪辑空间被定义在一个立方体内，其坐标从-1到1，中心是0。但纹理坐标和深度则从0到1。我们可以通过缩放和偏移XYZ维度的一半来将这种转换烘烤到矩阵中。我们可以用矩阵乘法来做，但这将导致大量的零乘法和无谓的加法。所以让我们直接调整矩阵。
*/

	Matrix4x4 ConvertToAtlasMatrix (Matrix4x4 m, Vector2 offset, int split) 
    {
		if (SystemInfo.usesReversedZBuffer) {
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
        
		return m;
	}

/*
m00, m01, m02, m03
m10, m11, m12, m13
m20, m21, m22, m23
m30, m31, m32, m33
*/
```

### Storing Shadow Data Per Light

shader中计算时需要shadow Strength和光的索引(用来计算采样的offset)

```c#
	public Vector2 ReserveDirectionalShadows (…) {
		if (…) {
			ShadowedDirectionalLights[ShadowedDirectionalLightCount] =
				new ShadowedDirectionalLight {
					visibleLightIndex = visibleLightIndex
				};
			return new Vector2(
				light.shadowStrength, ShadowedDirectionalLightCount++
			);
		}
		return Vector2.zero;
	}
```



```c#
	static int
		dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
		dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
		dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
		dirLightShadowDataId =
			Shader.PropertyToID("_DirectionalLightShadowData");

	static Vector4[]
		dirLightColors = new Vector4[maxDirLightCount],
		dirLightDirections = new Vector4[maxDirLightCount],
		dirLightShadowData = new Vector4[maxDirLightCount];

	…

	void SetupLights () {
		…
		buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
	}

	void SetupDirectionalLight (int index, ref VisibleLight visibleLight) {
		dirLightColors[index] = visibleLight.finalColor;
		dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirLightShadowData[index] =
			shadows.ReserveDirectionalShadows(visibleLight.light, index);
	}
```

```c
CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END
```

### Shadows HLSL File

定义专门的hlsl文件来处理阴影，并将其包含在`LitPass`中

```c
#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare // 定义采样方式
SAMPLER_CMP(SHADOW_SAMPLER);  

CBUFFER_START(_CustomShadows)
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

#endif
```

### Sampling Shadows

定义结构体

```c
struct DirectionalShadowData {
	float strength;
	int tileIndex;
};
```

需要将shading point的位置从世界空间转换到tile空间

```c
struct Surface {
	float3 position;
	…
};

Surface surface;
surface.position = input.positionWS;
surface.normal = normalize(input.normalWS);
```

采样贴图

```c
float SampleDirectionalShadowAtlas (float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(
		_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}
```

计算参数并且采样

```c
float GetDirectionalShadowAttenuation (DirectionalShadowData data, Surface surfaceWS) {
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[data.tileIndex],
		float4(surfaceWS.position, 1.0)
	).xyz;
	float shadow = SampleDirectionalShadowAtlas(positionSTS);
	return shadow;
}
```

对阴影图集进行采样的结果是一个系数，它决定了有多少光线到达表面，只考虑到阴影。这是一个在0-1范围内的值，被称为衰减因子。如果片段完全被阴影覆盖，那么我们得到的是0，当它完全没有阴影时，我们得到的是1。介于两者之间的数值表示该片段是部分阴影的。

除此之外，灯光的阴影强度可以被降低，无论是出于艺术原因还是为了表现半透明表面的阴影。当强度降低到零时，衰减就完全不受阴影的影响，应该是1。所以最终的衰减是通过1和采样衰减之间的线性插值找到的，以强度为基础。

```c
float GetDirectionalShadowAttenuation (DirectionalShadowData data, Surface surfaceWS) {
	if (data.strength <= 0.0) {
		return 1.0;
	}
	…

return lerp(1.0, shadow, data.strength);
```

### Attenuating Light

将阴影看成是光的衰减因子

```c
struct Light {
	float3 color;
	float3 direction;
	float attenuation;
};

DirectionalShadowData GetDirectionalShadowData (int lightIndex) {
	DirectionalShadowData data;
	data.strength = _DirectionalLightShadowData[lightIndex].x;
	data.tileIndex = _DirectionalLightShadowData[lightIndex].y;
	return data;
}

Light GetDirectionalLight (int index, Surface surfaceWS) {
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirections[index].xyz;
	DirectionalShadowData shadowData = GetDirectionalShadowData(index);
	light.attenuation = GetDirectionalShadowAttenuation(shadowData, surfaceWS);
	return light;
}
```

计算光照时乘以这个衰减因子

```c
float3 GetLighting (Surface surfaceWS, BRDF brdf) {
	float3 color = 0.0;
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		color += GetLighting(surfaceWS, brdf, GetDirectionalLight(i, surfaceWS));
	}
	return color;
}

float3 IncomingLight (Surface surface, Light light) {
	return
		saturate(dot(surface.normal, light.direction) * light.attenuation) *
		light.color;
}
```

我们终于有了阴影，但它们看起来很糟糕。不应该有阴影的表面最终被形成像素带的阴影假象所覆盖。这些都是由自我阴影造成的，是由于阴影图的分辨率有限造成的。使用不同的分辨率可以改变伪影模式，但不能消除它们。这些表面最终会部分形成阴影，但我们以后会处理这个问题。artifact 使我们很容易看到阴影贴图所覆盖的区域，所以我们现在要保留它们。

例如，我们可以看到阴影贴图只覆盖了可见区域的一部分，由最大阴影距离控制。改变最大距离可以增长或缩小该区域。阴影贴图是与光线方向一致的，而不是与摄像机一致。一些阴影在最大距离之外是可见的，但也有一些是缺失的，当阴影被采样到地图的边缘之外时，就会变得很奇怪。如果只有一个有阴影的光是活跃的，那么结果就会被钳制，否则采样就会跨越瓦片边界，一个光最终会使用另一个光的阴影。

## Cascaded Shadow Maps

因为定向光会影响到最大阴影距离以内的所有东西，他们的阴影图最终会覆盖很大的区域。由于阴影贴图使用正投影，阴影贴图中的每个texel有一个固定的世界空间大小。如果这个尺寸太大，那么个别的阴影texel就会清晰可见，导致阴影边缘参差不齐，小的阴影也会消失。这种情况可以通过增加图集的大小来缓解，但只能达到一定程度。

当使用透视摄影机时，更远的东西看起来更小。在一定的视觉距离上，一个阴影图texel将映射到一个显示像素，这意味着阴影的分辨率在理论上是最佳的。在离摄像机较近的地方，我们需要较高的阴影分辨率，而较远的地方，较低的分辨率就足够了。这表明，在理想情况下，我们会根据阴影接收器的视距，使用一个可变的阴影地图分辨率。

级联阴影图是这个问题的一个解决方案。这个想法是，影子投射者被渲染了不止一次，所以每个光在图集中得到了多个瓦片，被称为级联。第一个级联只覆盖靠近摄像机的一个小区域，而连续的级联则以相同数量的 texels 覆盖一个越来越大的区域。然后，着色器对每个片断的最佳级联进行采样。

### Settings

添加级联贴图个数和级联贴图覆盖大小配置

```c#
	public struct Directional {

		public MapSize atlasSize;

		[Range(1, 4)]
		public int cascadeCount;

		[Range(0f, 1f)]
		public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
	}

	public Directional directional = new Directional {
		atlasSize = MapSize._1024,
		cascadeCount = 4,
		cascadeRatio1 = 0.1f,
		cascadeRatio2 = 0.25f,
		cascadeRatio3 = 0.5f
	};

public Vector3 CascadeRatios =>
    new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
```

### Rendering Cascades

矩阵

```c#
	const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;

	…

	static Matrix4x4[]
		dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];
```

```c
#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

…

CBUFFER_START(_CustomShadows)
	float4x4 _DirectionalShadowMatrices
		[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
CBUFFER_END
```

`Shadows.ReserveDirectionalShadows `

```c#
			return new Vector2(
				light.shadowStrength,
				settings.directional.cascadeCount * ShadowedDirectionalLightCount++
			);

		int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
		int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
		int tileSize = atlasSize / split;
```

```c#
	void RenderDirectionalShadows (int index, int split, int tileSize) {
		ShadowedDirectionalLight light = shadowedDirectionalLights[index];
		var shadowSettings =
			new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
		int cascadeCount = settings.directional.cascadeCount;
		int tileOffset = index * cascadeCount;
		Vector3 ratios = settings.directional.CascadeRatios;
		
		for (int i = 0; i < cascadeCount; i++) {
			cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
				light.visibleLightIndex, i, cascadeCount, ratios, tileSize, 0f,
				out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
				out ShadowSplitData splitData
			);
			shadowSettings.splitData = splitData;
			int tileIndex = tileOffset + i;
			dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
				projectionMatrix * viewMatrix,
				SetTileViewport(tileIndex, split, tileSize), split
			);
			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			ExecuteBuffer();
			context.DrawShadows(ref shadowSettings);
		}
	}
```

### Culling Spheres

Unity通过为每个级联创建一个剔除球来确定其覆盖的区域。由于阴影的投影是正交的和方形的，它们最终与它们的剔除球体紧密结合，但也覆盖了它们周围的一些空间。这就是为什么有些影子可以在剔除区域之外看到。另外，光的方向对球体来说并不重要，所以所有方向的光最终都使用相同的剔除球体。

### Max Distance
