## DrawCalls

- White a HLSL shader.
- Support the SRP batcher, GPU instancing, and dynamic batching.
- Configure material properties per object and draw many at random.
- Create transparent and cutout materials.

### Shaders

#### Unlit Shader

```c#
Shader "Custom RP/Unlit" { // 下拉框中的选项
	
	Properties {
        _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
    } // 材质属性
	
	SubShader {
		
		Pass {} // 定义渲染物体的方式
	}
}
```

#### HLSL Programs

```c#
Pass {
    	HLSLPROGRAM
        #pragma vertex UnlitPassVertex
        #pragma fragment UnlitPassFragment
        #include "UnlitPass.hlsl"
        ENDHLSL
}
```

#### Include Guard

防止文件被include多次

#### Shader Functions

hlsl语法

`SV_TARGET`  Fragment输出

`SV_POSITION` vertext输出

`POSITION` Frament函数输入

#### Space Transformation

空间转换：obj->world->view->projection

1. 创建unityInput.hlsl,声明uniform等属性
2. 创建common.hlsl，声明负责转换空间的函数

#### Core Library

使用unity库的方法来转换空间

#### Color

1. 用`uniform`变量定义逐材质的属性color
2. 在`properties`块中定义相同名称的属性，并提供类型和值的信息
3. 在材质中可以修改刚才的属性，起到逐材质定义的作用

------

### Batching

减少DrawCall，减少GPU、CPU通信的次数。

#### SRP Batcher

与其说SRP批次减少了绘制调用的数量，不如说是使其更加精简。它把**材料属性缓存在GPU**上，所以它们不必在每次绘制调用时都被发送。这既减少了必须传达的数据量，也减少了CPU在每次绘制调用时必须做的工作。但是，这只有在着色器严格遵守统一数据结构的情况下才有效。每个绘制调用只需要包含一个偏移到正确的内存位置,Unity不会比较材质的确切内存布局，它只是将使用完全相同的着色器变体的绘制调用进行分组。

要求：

- 材质的属性必须声明在具体的内存Buffer中而不是全局
- `GraphicsSettings.useScriptableRenderPipelineBatching = true;`

#### Many Colors

实现PerObjectMaterialProperties

1. 获取`_BaseColor`的shaderID
2. 利用`MaterialPropertyBlock `设置具体的颜色
3. 在Renderer上调用`SetPropertyBlock(block)`设置到shader中

#### GPU Instancing

一次渲染许多个具有相同mesh的物体。GPU通过将这些物体的transform信息和材质属性组织到一个数组中，遍历数组进行渲染。

1. 在“Shader中”添加指令`#pragma multi_compile_instancing`，通过材质决定是否启用gpu instancing
2. 包含`UnityInstancing.hlsl `
3. 将顶点输入定义为struct
4. 用一系列宏来访问属性

#### Drawing Many Instanced Meshes

创建cs文件来提供transform数组和材质数组给gpu以便其可以进行GPU Instancing Draw

#### Dynamic Batching

将一些共享相同材质的小模型合并成一个大的模型去渲染。

`DrawingSetting` 中设置`enableDynamicBatching `是否开启动态合并

#### Configuring Batching

配置合并功能：`SRPBatch`，`GPUInstancing`,`Dynamic Batching`

1. 修改`DrawVisibleGeometry `

   ```c#
   	void DrawVisibleGeometry (bool useDynamicBatching, bool useGPUInstancing) {
   		var sortingSettings = new SortingSettings(camera) {
   			criteria = SortingCriteria.CommonOpaque
   		};
   		var drawingSettings = new DrawingSettings(
   			unlitShaderTagId, sortingSettings
   		) {
   			enableDynamicBatching = useDynamicBatching,
   			enableInstancing = useGPUInstancing
   		};
   ```

2. 修改`Render`函数

   ```c#
   	public void Render (
   		ScriptableRenderContext context, Camera camera,
   		bool useDynamicBatching, bool useGPUInstancing
   	) {
   		…
   		DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
   		…
   	}
   ```

3. 给`CustomRenderPipeline`添加成员变量控制合批方法

   ```c#
   	bool useDynamicBatching, useGPUInstancing;
   
   	public CustomRenderPipeline (
   		bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher
   	) {
   		this.useDynamicBatching = useDynamicBatching;
   		this.useGPUInstancing = useGPUInstancing;
   		GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
   	}
   
   	protected override void Render (
   		ScriptableRenderContext context, Camera[] cameras
   	) {
   		foreach (Camera camera in cameras) {
   			renderer.Render(
   				context, camera, useDynamicBatching, useGPUInstancing
   			);
   		}
   	}
   ```

4. 给`CustomRenderPipelineAsset`添加选项以配置合批方法

   ```c#
   	[SerializeField]
   	bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true;
   
   	protected override RenderPipeline CreatePipeline () {
   		return new CustomRenderPipeline(
   			useDynamicBatching, useGPUInstancing, useSRPBatcher
   		);
   	}
   ```

------

### Transparency

#### Blend Modes

不透明和透明渲染的主要区别在于，我们是替换之前绘制的任何东西，还是与之前的结果结合起来产生透视效果。我们可以通过设置源和目标混合模式来控制这一点。这里的源指的是现在被绘制的东西，而目的指的是之前被绘制的东西，以及结果最终会在哪里出现。为此添加两个着色器属性： _SrcBlend和_DstBlend。它们是混合模式的枚举，但我们可以使用的最佳类型是Float，默认情况下，源点设置为1，终点设置为0。

1. `Properties`中添加blend参数
2. Pass中添加blend指令
3. 深度不做混合

#### Not Writing Depth

#### Texturing

1. 在`Properties`块中声明`Texture`
2. 在shader中声明变量以访问Texture
3. tiling和offset的声明
4. 在顶点属性中uv的声明
5. 在Varyings 结构体中uv的声明
6. 用scale和offset来计算uv
7. 采样贴图

### Alpha Clipping

一个材质通常使用透明度混合或阿尔法剪裁，而不是同时使用两者。一个典型的剪辑材质是完全不透明的，除了被丢弃的片段之外，它的确会向深度缓冲区写入数据。它使用AlphaTest渲染队列，这意味着它在所有完全不透明的对象之后被渲染。之所以这样做，是因为丢弃碎片会使一些GPU优化变得不可能，因为不能再假设三角形完全覆盖它们后面的东西。通过先绘制完全不透明的物体，它们最终可能会覆盖部分alpha-clipped物体，这样就不需要处理它们的隐藏片段了。

### Shader Features



------

```c#
Shader "Custom RP/Unlit" { // 下拉框中的选项
	
	Properties {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0    
         [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
    } // 材质属性
	
	SubShader {
		
		Pass {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment
            #include "UnlitPass.hlsl"
            ENDHLSL
        } // 定义渲染物体的方式
	}
}
```

UnityInput.hlsl

```c#
#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

/*
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
real4 unity_WorldTransformParams;
*/
CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;
CBUFFER_END    

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;

#endif
```

Common.hlsl

```c#
#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED
    
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "UnityInput.hlsl"

/*
float3 TransformObjectToWorld (float3 positionOS) {
	return mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz;
}

float4 TransformWorldToHClip (float3 positionWS) {
	return mul(unity_MatrixVP, float4(positionWS, 1.0));
}
*/

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection
#define UNITY_MATRIX_P glstate_matrix_projection
    
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

#endif
```

UnlitPass.hlsl

```c
#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

/*
CBUFFER_START(UnityPerMaterial)
	float4 _BaseColor;
CBUFFER_END


float4 UnlitPassFragment () : SV_TARGET {
	return _BaseColor;
}

float4 UnlitPassVertex (float3 positionOS : POSITION) : SV_POSITION {
	float3 positionWS = TransformObjectToWorld(positionOS.xyz);
	return TransformWorldToHClip(positionWS);
}
*/

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
    
struct Attributes {
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID// 物体index
};

struct Varyings {
	float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID // 物体index
};

Varyings UnlitPassVertex (Attributes input) { //: SV_POSITION {
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);// 从输入中取出index放在全局变量中
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(positionWS);
    
   	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw;
    
	return output;
}

float4 UnlitPassFragment (Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    
	return baseMap * baseColor;
}

#endif
```

PerObjectMaterialProperties.cs

```c#
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour {
	
	static int baseColorId = Shader.PropertyToID("_BaseColor");
	static MaterialPropertyBlock block;
    
	[SerializeField]
	Color baseColor = Color.white;
    
	void OnValidate () {
		if (block == null) {
			block = new MaterialPropertyBlock();
		}
		block.SetColor(baseColorId, baseColor);
		GetComponent<Renderer>().SetPropertyBlock(block);
	}    
    
    void Awake () {
		OnValidate();
	}
}
```

MeshBall.cs

```c#
using UnityEngine;

public class MeshBall : MonoBehaviour {

	static int baseColorId = Shader.PropertyToID("_BaseColor");

	[SerializeField]
	Mesh mesh = default;

	[SerializeField]
	Material material = default;
    
   	Matrix4x4[] matrices = new Matrix4x4[1023];
	Vector4[] baseColors = new Vector4[1023];

	MaterialPropertyBlock block;
    void Awake () {
		for (int i = 0; i < matrices.Length; i++) {
			matrices[i] = Matrix4x4.TRS(
				Random.insideUnitSphere * 10f, Quaternion.identity, Vector3.one
			);
			baseColors[i] =
				new Vector4(Random.value, Random.value, Random.value, 1f);
		}
	}
	void Update () {
		if (block == null) {
			block = new MaterialPropertyBlock();
			block.SetVectorArray(baseColorId, baseColors);
		}
		Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block);
	}    
}

```

