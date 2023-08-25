**Baked Light**

## Baking Static Light

到目前为止，在渲染过程中我们已经计算了所有的光照，但这并不是唯一的选择。光照也可以预先计算并存储在光照贴图和探针中。这样做的两个主要原因是：减少实时计算的量以及添加无法在运行时计算的间接光照。后者是全局光照的一部分：光线并非直接来自光源，而是通过反射间接地来自于环境或发光表面。

烘焙光照的缺点是它是静态的，因此无法在运行时更改。它还需要被存储，这会增加构建大小和内存使用量。

Unity使用Enlighten系统进行实时全局光照，但该系统已被弃用，因此我们将不会使用它。此外，反射探针可以在运行时渲染，用于创建镜面环境反射，但在本教程中我们不会涉及这部分内容。

### Scene Lighting Settings

全局光照是通过“光照”窗口中的“场景”选项卡来进行配置的。烘焙光照是通过“混合光照”下的“烘焙全局光照”切换进行启用的。还有一个“光照模式”选项，我们将设置为“烘焙间接”，这意味着我们会烘焙所有静态间接光照。

如果您的项目是在Unity 2019.2或更早版本中创建的，那么您还将看到一个启用实时光照的选项，您应该将其禁用。如果您的项目是在Unity 2019.3或更高版本中创建的，则不会显示该选项。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/baking-light/baked-indirect.png)

在稍后的位置有一个“光照图设置”部分，可以用于控制光照图生成过程，这是由Unity编辑器完成的。我将使用默认设置，但将“光照图分辨率”减少到20，禁用“压缩光照图”，并将“方向模式”设置为“非定向”。我还将使用渐进式GPU光映射器。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/baking-light/lightmapping-settings.png)

它还会烘焙方向性数据，这使得法线贴图能够影响到进入烘焙光照。由于在此阶段我们不支持法线贴图，所以没有理由启用它。(**DirectionalMode**)

### Static Objects

为了演示烘焙光照，我创建了一个场景，场景中有一个绿色的平面作为地面，还有一些盒子和球体，以及一个位于中心的结构，只有一侧是开放的，因此其内部完全被阴影遮挡。

这个场景中有一个单独的定向光，其**模式设置为混合。这告诉Unity应该为这个光源烘焙间接光照**。除此之外，这个光源仍然像普通的实时光一样工作。

我还将地面平面和所有的立方体（包括构成结构的立方体）都包括在烘焙过程中。它们将成为光线反射的对象，从而产生间接光照。这是通过启用它们的MeshRenderer组件的“**贡献全局光照”**切换来完成的。启用此选项还会自动将它们的“接收全局光照”模式切换到“光照图”，这意味着到达它们表面的间接光会被烘焙到光照图中。您还可以通过在对象的“静态”下拉列表中启用“贡献GI”，或使其完全静态来启用此模式。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/baking-light/contribute-gi.png)

一旦启用，假设在光照窗口中启用了“自动生成”，场景的光照将会再次进行烘焙，否则您需要按下“生成光照”按钮。光照图设置也会显示在MeshRenderer组件中，包括包含对象的光照图的视图。

这些球体不会显示在光照图中，因为它们不会贡献到全局光照，因此被视为动态的。它们将需要依靠光照探针，我们稍后会进行介绍。静态物体也可以通过将它们的“接收全局光照”模式切换回“光照探针”来从光照图中排除。它们仍然会影响烘焙结果，但不会在光照图中占用空间。

### Fully-Baked Light

烘焙光照主要呈现蓝色，因为它受到天空盒的主导，天空盒代表了环境天空的间接照明。中心建筑周围的亮区域是由光源的间接光照在地面和墙壁上反射产生的。

我们还可以将所有的光照都烘焙到光照图中，包括直接光和间接光。这是通过将光源的模式设置为“烘焙”来实现的。然后，它将不再提供实时光照。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/baking-light/baked-mode-inspector.png)

![scene](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/baking-light/baked-mode-scene.png)

实际上，烘焙光源的直接光也被视为间接光，因此也会出现在光照图中，使得它变得更加明亮。

## Sampling Baked Lighta

目前所有物体都呈现为纯黑色，因为没有实时光照，而且我们的着色器还不了解全局光照。我们需要对光照图进行采样才能使其正常工作。

### Global Illumination

创建一个新的ShaderLibrary/GI.hlsl文件，用于包含与全局光照相关的所有代码。在其中，定义一个GI结构体和一个GetGI函数，以根据一些光照图的UV坐标来检索它。间接光来自所有方向，因此只能用于漫反射光照，而不能用于镜面反射。所以给GI结构体添加一个漫反射颜色字段。最初，将其填充为光照图的UV坐标，以供调试目的。

```c
#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

struct GI {
	float3 diffuse;
};

GI GetGI (float2 lightMapUV) {
	GI gi;
	gi.diffuse = float3(lightMapUV, 0.0);
	return gi;
}

#endif
```

镜面环境反射通常通过反射探针提供，我们将在未来的教程中进行介绍。屏幕空间反射是另一个选项。

在GetLighting函数中添加一个GI参数，并在累加实时光照之前使用它来初始化颜色值。在这一阶段，我们不会将它与表面的漫反射反射率相乘，这样我们就可以看到未经修改的接收光。

```c
float3 GetLighting (Surface surfaceWS, BRDF brdf, GI gi) {
	ShadowData shadowData = GetShadowData(surfaceWS);
	float3 color = gi.diffuse;
	…
	return color;
}
```

Include *GI* before *Lighting* in *LitPass*.

```
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

GI gi = GetGI(0.0);
float3 color = GetLighting(surface, brdf, gi);
```

### Light Map Coordinates

为了获取光照图的UV坐标，Unity必须将它们发送到着色器中。我们需要指示管道对每个使用了光照贴图的对象执行此操作。这可以通过在CameraRenderer.DrawVisibleGeometry中将绘制设置的每个对象数据属性设置为PerObjectData.Lightmaps来完成。

```c#
var drawingSettings = new DrawingSettings(
    unlitShaderTagId, sortingSettings
) {
    enableDynamicBatching = useDynamicBatching,
    enableInstancing = useGPUInstancing,
    perObjectData = PerObjectData.Lightmaps
};
```

现在，Unity将使用具有LIGHTMAP_ON关键字的着色器变体来渲染使用了光照贴图的对象。在我们的Lit着色器的CustomLit通道中添加一个多编译指令来处理这个情况。

```
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile_instancing
```

光照贴图的UV坐标是Attributes顶点数据的一部分。我们需要将它们传输到Varyings中，以便在LitPassFragment中使用它们。但我们应该仅在需要时执行此操作。我们可以使用类似于传输实例标识符的方法，并依赖于GI_ATTRIBUTE_DATA、GI_VARYINGS_DATA和TRANSFER_GI_DATA宏。

```c
struct Attributes {
	…
	GI_ATTRIBUTE_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	…
	GI_VARYINGS_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex (Attributes input) {
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	TRANSFER_GI_DATA(input, output);
	…
}
```

还需要添加另一个GI_FRAGMENT_DATA宏，以检索GetGI所需的参数。

```
	GI gi = GetGI(GI_FRAGMENT_DATA(input));
```

我们必须自己定义这些宏，在GI文件中。最初将它们定义为空，除了GI_FRAGMENT_DATA宏，它将简单地定义为零。宏的参数列表的工作方式类似于函数的参数列表，除了没有类型，并且在宏名称和参数列表之间不允许有空格，否则列表将被解释为宏定义的内容。

```
#if defined(LIGHTMAP_ON)
	#define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;
	#define GI_VARYINGS_DATA float2 lightMapUV : VAR_LIGHT_MAP_UV;
	#define TRANSFER_GI_DATA(input, output) output.lightMapUV = input.lightMapUV;
	#define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
	#define GI_ATTRIBUTE_DATA
	#define GI_VARYINGS_DATA
	#define TRANSFER_GI_DATA(input, output)
	#define GI_FRAGMENT_DATA(input) 0.0
#endif
```

当定义了LIGHTMAP_ON时，这些宏应该定义代码，以添加另一个UV集到结构体中，复制它，并检索它。光照贴图的UV是通过第二个纹理坐标通道提供的，因此我们需要在Attributes中使用TEXCOORD1语义。

现在，所有静态烘焙对象都显示出它们的UV，而所有动态对象仍然保持黑色。

### Transformed Light Map Coordinates

光照贴图坐标通常由Unity自动为每个网格生成，或者是导入的网格数据的一部分。它们定义了一个纹理展开，将网格展平，以映射到纹理坐标。展开在光照贴图中按对象进行缩放和定位，因此每个实例都有自己的空间。这与应用于基础UV的缩放和平移操作的原理相同。我们也必须将其应用于光照贴图的UV。

光照贴图的UV变换作为UnityPerDraw缓冲的一部分传递给GPU，因此在那里添加它。它被称为unity_LightmapST。尽管它已被弃用，但还是在其后添加unityDynamicLightmapST，以确保SRP批处理兼容性不会破坏。

```
CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;

	float4 unity_LightmapST;
	float4 unity_DynamicLightmapST;
CBUFFER_END
```

然后调整TRANSFER_GI_DATA宏，使其应用变换。宏定义可以分成多行，如果每行的末尾除了最后一行都标有反斜杠。

```c
	#define TRANSFER_GI_DATA(input, output) \
		output.lightMapUV = input.lightMapUV * \
		unity_LightmapST.xy + unity_LightmapST.zw;
```

### Sampling the Light Map

采样光照贴图是GI的责任。光照贴图纹理被称为unity_Lightmap，附带着采样器状态。还包括Core RP Library中的EntityLighting.hlsl文件，因为我们将使用它来检索光照数据。

```c
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);
```

创建一个SampleLightMap函数，当存在光照贴图时调用SampleSingleLightmap函数，否则返回零。在GetGI函数中使用它来设置漫反射光照。

```
float3 SampleLightMap (float2 lightMapUV) {
	#if defined(LIGHTMAP_ON)
		return SampleSingleLightmap(lightMapUV);
	#else
		return 0.0;
	#endif
}

GI GetGI (float2 lightMapUV) {
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV);
	return gi;
}
```

SampleSingleLightmap函数需要更多的参数。首先，我们必须将纹理和采样器状态作为前两个参数传递给它，可以使用TEXTURE2D_ARGS宏来处理。

```
		return SampleSingleLightmap(
			TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightMapUV,
			float4(1.0, 1.0, 0.0, 0.0),
			#if defined(UNITY_LIGHTMAP_FULL_HDR)
				false,
			#else
				true,
			#endif
			float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0)
		);
```

之后是要应用的缩放和平移。因为我们之前已经完成了这个步骤，所以我们将在这里使用一个恒等变换。

接下来是一个布尔值，用于指示光照贴图是否已压缩，当UNITY_LIGHTMAP_FULL_HDR未定义时为这种情况。最后一个参数是一个包含解码指令的float4。使用LIGHTMAP_HDR_MULTIPLIER作为其第一个分量，使用LIGHTMAP_HDR_EXPONENT作为其第二个分量。其它分量不会被使用。

### Disabling Environment Lighting

烘焙光照非常明亮，因为它还包括来自天空的间接光照。我们可以通过将其强度乘数减少到零来禁用它。这样可以让我们专注于单个定向光源。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/sampling-baked-light/environment-intensity-inspector.png)

## Light Probes