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

动态物体不会影响烘培全局光照，但可以通过Light Probes受到其影响。Light Probes是场景中的一个点，通过三阶多项式（具体而言是L2球谐函数）来近似所有传入光线的烘培结果。Light Probes分布在场景周围，Unity会在物体之间进行插值，以得出它们位置的最终光照近似值。

### Light Probe Group

通过在场景中创建Light Probes组（GameObject / Light / Light Probe Group），可以向场景添加Light Probes。这将创建一个带有LightProbeGroup组件的游戏对象，默认情况下它包含六个呈立方体形状排列的探头。当启用“Edit Light Probes”时，您可以像操作游戏对象一样移动、复制和删除单个探头。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/light-probes/light-probes-editing-inspector.png)

一个场景中可以有多个探头组。Unity将它们所有的探头组合在一起，然后创建一个连接它们的四面体体积网格。每个动态物体最终位于一个四面体内部。其顶点处的四个探头会进行插值，得出应用于该物体的最终光照。如果一个物体位于探头覆盖范围之外，会使用最近三角形的光照信息代替，因此光照可能看起来有些奇怪。

默认情况下，选中动态物体时会使用图标来显示影响该物体的探头，以及其位置处的插值结果。您可以通过在光照窗口的调试设置中调整“Light Probe Visualization”来更改这一行为。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/light-probes/light-probes-selected-inspector.png)

![scene](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/light-probes/light-probes-selected-scene.png)

放置Light Probes的位置取决于场景。首先，**只有在动态物体将出现的地方才需要它们**。其次**，在光照发生变化的地方放置它们**。每个探头都是插值的端点，所以将它们放在光照过渡的周围。第三，**不要将它们放在烘培几何体内**，因为它们会变成黑色。最后，插值会穿过物体，因此如果墙的两侧光照不同，请将探头靠近墙的两侧。这样，没有物体会在两侧之间插值。除此之外，您需要进行实验来找到最佳位置。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/light-probes/light-probes-all.png)

### Sampling Probes

插值的Light Probes数据必须针对每个物体传递到GPU。我们需要告诉Unity这样做，这次是通过PerObjectData.LightProbe而不是PerObjectData.Lightmaps来实现。我们需要同时启用这两个特性标志，因此可以使用布尔的“或”运算符将它们组合起来。

```
perObjectData = PerObjectData.Lightmaps | PerObjectData.LightProbe
```

所需的UnityPerDraw数据由七个float4向量组成，分别表示红色、绿色和蓝色光的多项式分量。它们的名称为unity_SH*，其中*可以是A、B或C。前两个有带有r、g和b后缀的三个版本。

```
CBUFFER_START(UnityPerDraw)
	…

	float4 unity_SHAr;
	float4 unity_SHAg;
	float4 unity_SHAb;
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;
CBUFFER_END
```

在全局光照（GI）中，我们通过一个新的SampleLightProbe函数对Light Probes进行采样。我们需要一个方向来进行采样，所以将世界空间的表面参数传递给它。

如果此物体正在使用光照贴图，则返回零。否则返回零和SampleSH9中的最大值。该函数需要探头数据和法线向量作为参数。探头数据必须以系数数组的形式提供。

```
float3 SampleLightProbe (Surface surfaceWS) {
	#if defined(LIGHTMAP_ON)
		return 0.0;
	#else
		float4 coefficients[7];
		coefficients[0] = unity_SHAr;
		coefficients[1] = unity_SHAg;
		coefficients[2] = unity_SHAb;
		coefficients[3] = unity_SHBr;
		coefficients[4] = unity_SHBg;
		coefficients[5] = unity_SHBb;
		coefficients[6] = unity_SHC;
		return max(0.0, SampleSH9(coefficients, surfaceWS.normal));
	#endif
}
```

向GetGI添加一个表面参数，并使其将Light Probes样本添加到漫反射光中。

```
GI GetGI (float2 lightMapUV, Surface surfaceWS) {
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
	return gi;
}
```

Finally, pass the surface to it in `LitPassFragment`.

```
	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/light-probes/sampling-probes.png)

### Light Probe Proxy Volumes

Light Probes适用于相对较小的动态物体，但由于光照是基于单个点的，所以对于较大的物体效果不佳。例如，我在场景中添加了两个拉伸的立方体。由于它们的位置位于暗区域内，这些立方体是均匀暗淡的，尽管显然这与实际光照不符合。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/light-probes/large-objects-single-probe.png)


我们可以通过使用Light Probes代理体积（Light Probe Proxy Volume，简称LPPV）来解决这个限制。最简单的方法是为每个立方体添加一个LightProbeProxyVolume组件，然后将它们的Light Probes模式设置为使用代理体积。

这些体积可以以多种方式进行配置。在这种情况下，我使用了自定义分辨率模式，在立方体的边缘放置了子探头，使它们可见。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/light-probes/lppv-inspector.png)

![scene](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/light-probes/lppv-scene.png)

### Sampling LPPVs

LPPV也需要将数据针对每个物体传递到GPU。在这种情况下，我们需要启用PerObjectData.LightProbeProxyVolume。

```
	perObjectData =
				PerObjectData.Lightmaps | PerObjectData.LightProbe |
				PerObjectData.LightProbeProxyVolume
```

还需要向UnityPerDraw添加四个额外的值：unity_ProbeVolumeParams、unity_ProbeVolumeWorldToObject、unity_ProbeVolumeSizeInv和unity_ProbeVolumeMin。其中第二个是一个矩阵，而其他三个是4D向量。

```
CBUFFER_START(UnityPerDraw)
	…

	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;
CBUFFER_END
```

体积数据存储在一个3D浮点纹理中，称为unity_ProbeVolumeSH。通过TEXTURE3D_FLOAT宏将其与采样器状态一起添加到GI中。

```
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);
```

是使用LPPV还是插值的Light Probes是通过unity_ProbeVolumeParams的第一个分量进行通信的。如果它被设置了，我们就必须通过SampleProbeVolumeSH4函数对体积进行采样。我们需要将纹理和采样器传递给它，然后是世界位置和法线。之后是矩阵，随后是unity_ProbeVolumeParams的Y和Z分量，然后是min和size-inv数据的XYZ部分。

```
		if (unity_ProbeVolumeParams.x) {
			return SampleProbeVolumeSH4(
				TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
				surfaceWS.position, surfaceWS.normal,
				unity_ProbeVolumeWorldToObject,
				unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
				unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
			);
		}
		else {
			float4 coefficients[7];
			coefficients[0] = unity_SHAr;
			coefficients[1] = unity_SHAg;
			coefficients[2] = unity_SHAb;
			coefficients[3] = unity_SHBr;
			coefficients[4] = unity_SHBg;
			coefficients[5] = unity_SHBb;
			coefficients[6] = unity_SHC;
			return max(0.0, SampleSH9(coefficients, surfaceWS.normal));
		}
```

对LPPV进行采样需要将其转换到体积空间，以及一些其他的计算，包括体积纹理采样和球谐函数的应用。在这种情况下，只应用L1球谐函数，因此结果不太精确，但可以在单个物体的表面上变化。

## Meta Pass

因为间接漫反射光会从表面反射，所以它会受到这些表面漫反射率的影响。目前这一点并未实现。Unity将我们的表面视为均匀白色。Unity在**烘焙时**使用特殊的Meta Pass来确定反射光。由于我们尚未定义这样的通道，Unity使用默认通道，结果为白色。

### Unified Input

添加另一个通道意味着我们需要重新定义着色器属性。让我们从LitPass中提取基础纹理和UnityPerMaterial缓冲，并将它们放入一个新的Shaders/LitInput.hlsl文件中。我们还可以通过引入TransformBaseUV、GetBase、GetCutoff、GetMetallic和GetSmoothness函数来隐藏实例化代码。为所有这些函数都提供一个基础UV参数，即使它未被使用。这样隐藏了值是从贴图中获取还是其他方式获取的。

```
#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float2 TransformBaseUV (float2 baseUV) {
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase (float2 baseUV) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
	float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	return map * color;
}

float GetCutoff (float2 baseUV) {
	return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff);
}

float GetMetallic (float2 baseUV) {
	return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
}

float GetSmoothness (float2 baseUV) {
	return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
}

#endif
```

为了在Lit的所有通道中包含这个文件，在其SubShader块的顶部添加一个HLSLINCLUDE块，位于通道之前。在其中包含Common，然后是LitInput。这段代码将会插入到所有通道的开头。

```
	SubShader {
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "LitInput.hlsl"
		ENDHLSL
		
		…
	}
```

Remove the now duplicate include statement and declarations from *LitPass*.

Use `TransformBaseUV` in `LitPassVertex`.

```
output.baseUV = TransformBaseUV(input.baseUV);
```

And the relevant functions to retrieve shader properties in `LitPassFragment`.

```
	//float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	//float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	float4 base = GetBase(input.baseUV);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(input.baseUV));
	#endif
	
	…
	surface.metallic = GetMetallic(input.baseUV);
	surface.smoothness = GetSmoothness(input.baseUV);
```

### Unlit

让我们也对Unlit着色器进行类似的操作。复制LitInput.hlsl并将其重命名为UnlitInput.hlsl。然后从UnityPerMaterial版本中删除_Metallic和_Smoothness。保留GetMetallic和GetSmoothness函数，并使它们返回0.0，表示非常暗淡的漫反射表面。之后，也为着色器添加一个HLSLINCLUDE块。

```
	HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "UnlitInput.hlsl"
		ENDHLSL
```

像我们为LitPass做的那样，将UnlitPass进行转换。请注意，ShadowCasterPass适用于两种着色器，尽管它最终具有不同的输入定义。

### Meta Light Mode

在Lit和Unlit着色器中都添加一个新的通道，将LightMode设置为Meta。该通道需要**关闭剔除**，可以通过添加Cull Off选项来配置。它将使用在一个新的MetaPass.hlsl文件中定义的MetaPassVertex和MetaPassFragment函数。它不需要多重编译指令。

```
		Pass {
			Tags {
				"LightMode" = "Meta"
			}

			Cull Off

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex MetaPassVertex
			#pragma fragment MetaPassFragment
			#include "MetaPass.hlsl"
			ENDHLSL
		}
```

我们需要获取表面的漫反射率，所以我们必须在MetaPassFragment中获取它的BRDF数据。因此，我们必须包括BRDF，以及Surface、Shadows和Light，因为它依赖于它们。我们只需要知道物体空间的位置和基础UV，最初将裁剪空间位置设置为零。可以通过ZERO_INITIALIZE(Surface, surface)将表面初始化为零，之后我们只需要设置其颜色、金属度和光滑度值。这足以获取BRDF数据，但我们将从返回零开始。

```c
#ifndef CUSTOM_META_PASS_INCLUDED
#define CUSTOM_META_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"

struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV;
};

Varyings MetaPassVertex (Attributes input) {
	Varyings output;
	output.positionCS = 0.0;
	output.baseUV = TransformBaseUV(input.baseUV);
	return output;
}

float4 MetaPassFragment (Varyings input) : SV_TARGET {
	float4 base = GetBase(input.baseUV);
	Surface surface;
	ZERO_INITIALIZE(Surface, surface);
	surface.color = base.rgb;
	surface.metallic = GetMetallic(input.baseUV);
	surface.smoothness = GetSmoothness(input.baseUV);
	BRDF brdf = GetBRDF(surface);
	float4 meta = 0.0;
	return meta;
}

#endif
```

一旦Unity使用我们自己的Meta Pass重新烘焙场景，所有的间接光照都会消失，因为黑色的表面不会反射任何光线。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/meta-pass/no-indirect-light.png)

### Light Map Coordinates

就像采样光照贴图时一样，我们需要使用光照贴图的UV坐标。不同的是，这一次我们要沿相反的方向前进，将它们用于XY物体空间位置。之后，我们必须将它传递给TransformWorldToHClip函数，尽管在这种情况下，该函数执行的是与其名称所示不同类型的转换。

```
struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	float2 lightMapUV : TEXCOORD1;
};

…

Varyings MetaPassVertex (Attributes input) {
	Varyings output;
	input.positionOS.xy =
		input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
	output.positionCS = TransformWorldToHClip(input.positionOS);
	output.baseUV = TransformBaseUV(input.baseUV);
	return output;
}
```

我们仍然需要物体空间顶点属性作为输入，因为着色器希望它存在。实际上，似乎除非明确使用Z坐标，否则OpenGL无法工作。我们将使用与Unity自己的Meta Pass相同的虚拟赋值，即input.positionOS.z > 0.0 ? FLT_MIN : 0.0。

```
	input.positionOS.xy =
		input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
	input.positionOS.z = input.positionOS.z > 0.0 ? FLT_MIN : 0.0;
```

### Diffuse Reflectivity

Meta Pass可以用于生成不同的数据。所请求的内容是通过一个bool4 unity_MetaFragmentControl标志向量进行通信的。

```
bool4 unity_MetaFragmentControl;
```

如果设置了X标志，那么请求的是漫反射率，因此将其设置为RGB结果。A分量应设置为1。

```
	float4 meta = 0.0;
	if (unity_MetaFragmentControl.x) {
		meta = float4(brdf.diffuse, 1.0);
        meta.rgb += brdf.specular * brdf.roughness * 0.5;
        meta.rgb = min(
			PositivePow(meta.rgb, unity_OneOverOutputBoost), unity_MaxOutputValue
		); 
	}
	return meta;
```

这足以为反射光着色，但Unity的Meta Pass还会稍微增强结果，方法是加上一半的高光反射率，乘以粗糙度。背后的想法是高度高光但粗糙的材质也会传递一些间接光。

之后，结果会通过使用提供的unity_OneOverOutputBoost和PositivePow方法将其提升为一个幂，并将其限制在unity_MaxOutputValue内进行修改。

These values are provided as floats.

```
float unity_OneOverOutputBoost;
float unity_MaxOutputValue;
```

现在我们已经正确地获得了彩色的间接光照，还要在GetLighting中将接收表面的漫反射率应用于它。

```
float3 color = gi.diffuse * brdf.diffuse;
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/meta-pass/proper-lighting.png)

同时，通过将环境光照的强度设置回1，让环境光照再次生效。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/meta-pass/environment-lighting.png)

最后，将光源的模式设置回Mixed。这将使它再次成为实时光源，并且所有的间接漫反射光照都已烘焙完成。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/meta-pass/mixed-lighting.png)

## Emissive Surfaces

一些表面会自发地发出光，因此即使没有其他照明，它们也是可见的。这可以通过在LitPassFragment的末尾添加一些颜色来实现。这不是真正的光源，因此不会影响其他表面。然而，这种效果可以对烘焙的照明产生影响。

### Emitted Light

在Lit着色器中添加两个新属性：一个emission 贴图和颜色，就像基础贴图和颜色一样。然而，我们将为两者使用相同的坐标转换，所以我们不需要为emission 贴图显示单独的控制选项。它们可以通过给它添加NoScaleOffset属性来隐藏。为了支持非常明亮的emission 光，为颜色添加HDR属性。这使得可以通过检查器配置具有大于一的亮度的颜色，显示HRD颜色弹出窗口而不是常规颜色弹出窗口。

作为示例，我创建了一个不透明的emission 材质，使用了Default-Particle纹理，其中包含一个圆形渐变，从而产生一个明亮的点。

```
		[NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
		[HDR] _EmissionColor("Emission", Color) = (0.0, 0.0, 0.0, 0.0)
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/emissive-surfaces/emissive-material.png)

将贴图添加到LitInput并将发射颜色添加到UnityPerMaterial。然后添加一个名为GetEmission的函数，它的工作方式与GetBase类似，只是它使用另一个贴图和颜色。

```
TEXTURE2D(_BaseMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
	…
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

…

float3 GetEmission (float2 baseUV) {
	float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, baseUV);
	float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor);
	return map.rgb * color.rgb;
}
```

在LitPassFragment的末尾将发射光添加到最终颜色中。

```
	float3 color = GetLighting(surface, brdf, gi);
	color += GetEmission(input.baseUV);
	return float4(color, surface.alpha);
```

还要在UnlitInput中添加一个GetEmission函数。在这种情况下，我们只是将它设置为GetBase的代理。因此，如果烘焙一个不受照明影响的物体，它会发出其全部颜色。

```
float3 GetEmission (float2 baseUV) {
	return GetBase(baseUV).rgb;
}
```

为了使不受照明影响的材质能够发出非常明亮的光，我们可以将HDR属性添加到Unlit的基础颜色属性中。

```
		[HDR] _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
```

最后，让我们将发射颜色添加到PerObjectMaterialProperties中。在这种情况下，我们可以通过给配置字段添加ColorUsage属性来允许HDR输入。我们必须传递给它两个布尔值。第一个指示是否需要显示alpha通道，而这对我们来说是不需要的。第二个指示是否允许HDR值。

```
	static int
		baseColorId = Shader.PropertyToID("_BaseColor"),
		cutoffId = Shader.PropertyToID("_Cutoff"),
		metallicId = Shader.PropertyToID("_Metallic"),
		smoothnessId = Shader.PropertyToID("_Smoothness"),
		emissionColorId = Shader.PropertyToID("_EmissionColor");

	…

	[SerializeField, ColorUsage(false, true)]
	Color emissionColor = Color.black;

	…

	void OnValidate () {
		…
		block.SetColor(emissionColorId, emissionColor);
		GetComponent<Renderer>().SetPropertyBlock(block);
	}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/emissive-surfaces/per-object-emission.png)

我在场景中添加了一些小的发光立方体。我让它们对全局光照产生影响，并且在光照图中将它们的比例加倍，以避免关于重叠UV坐标的警告。当顶点在光照贴图中过于接近，因此它们必须共享相同的纹理单元时，就会出现这种情况。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/emissive-surfaces/emissive-objects.png)

### Baked Emission

发光光线是通过单独的通道进行烘焙的。当unity_MetaFragmentControl的Y标志被设置时，MetaPassFragment应该返回发出的光线，同样将A分量设置为1。

```
	if (unity_MetaFragmentControl.x) {
		…
	}
	else if (unity_MetaFragmentControl.y) {
		meta = float4(GetEmission(input.baseUV), 1.0);
	}
```

但这不会自动发生。我们必须启用每个材质的发光烘焙。我们可以通过在PerObjectMaterialProperties.OnGUI中在编辑器中调用LightmapEmissionProperty来显示此配置选项。

```
	public override void OnGUI (
		MaterialEditor materialEditor, MaterialProperty[] properties
	) {
		EditorGUI.BeginChangeCheck();
		base.OnGUI(materialEditor, properties);
		editor = materialEditor;
		materials = materialEditor.targets;
		this.properties = properties;

		BakedEmission();

		…
	}

	void BakedEmission () {
		editor.LightmapEmissionProperty();
	}
```

这会显示一个全局光照的下拉菜单，初始设置为"None"。尽管它的名称如此，但它只影响发光烘焙。将其更改为"Baked"会告诉光照映射器为发出的光运行一个单独的通道。还有一个"Realtime"选项，但它已被弃用。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/emissive-surfaces/emission-set-to-baked.png)

这仍然不起作用，因为Unity在烘焙时会极力避免使用单独的发射通道。如果材质的发射为零，则会被忽略。然而，这并没有考虑到每个物体的材质属性。我们可以通过在发射模式更改时，将所有选定材质的globalIlluminationFlags属性的默认MaterialGlobalIlluminationFlags.EmissiveIsBlack标志禁用来覆盖此行为。这意味着您只有在需要时才应启用"Baked"选项。

```
	void BakedEmission () {
		EditorGUI.BeginChangeCheck();
		editor.LightmapEmissionProperty();
		if (EditorGUI.EndChangeCheck()) {
			foreach (Material m in editor.targets) {
				m.globalIlluminationFlags &=
					~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
			}
		}
	}
```

![with](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/emissive-surfaces/baked-emission-with-light.png)

![without](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/emissive-surfaces/baked-emission-without-light.png)

## Baked Transparency

也可以烘焙透明物体，但需要额外的努力

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/baked-transparency/semitransparent-ceiling.png)

### Hard-Coded Properties

很遗憾，Unity的光照映射器在处理透明度时有一种固定的方法。它根据材质的队列来确定它是不透明的、裁剪的还是透明的。然后通过将_MainTex和_Color属性的alpha分量相乘来确定透明度，使用_Cutoff属性进行alpha裁剪。我们的着色器具有第三个属性，但缺少前两个属性。目前唯一使此功能正常工作的方法是将所需的属性添加到我们的着色器中，并给予它们HideInInspector属性，这样它们就不会显示在检查器中。Unity的SRP着色器也必须解决相同的问题。

### Copying Properties

我们必须确保_MainTex属性指向与_BaseMap相同的纹理，并使用相同的UV变换。两个颜色属性也必须相同。我们可以在CustomShaderGUI.OnGUI的末尾调用一个新的CopyLightMappingProperties方法，如果有更改发生的话。如果相关属性存在，则复制它们的值。

```
	public override void OnGUI (
		MaterialEditor materialEditor, MaterialProperty[] properties
	) {
		…

		if (EditorGUI.EndChangeCheck()) {
			SetShadowCasterPass();
			CopyLightMappingProperties();
		}
	}

	void CopyLightMappingProperties () {
		MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
		MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
		if (mainTex != null && baseMap != null) {
			mainTex.textureValue = baseMap.textureValue;
			mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
		}
		MaterialProperty color = FindProperty("_Color", properties, false);
		MaterialProperty baseColor =
			FindProperty("_BaseColor", properties, false);
		if (color != null && baseColor != null) {
			color.colorValue = baseColor.colorValue;
		}
	}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/baked-transparency/transparent-baked.png)

这对于裁剪材质也适用。虽然是可能的，但在MetaPassFragment中裁剪片段并不是必需的，因为透明度是单独处理的。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/baked-transparency/cutout-baked.png)

不幸的是，这意味着烘焙的透明度只能依赖于单一的纹理、颜色和cutoff属性。此外，光照映射器只考虑材质的属性，不会考虑每个实例的属性。

## Mesh Ball

我们结束前，为MeshBall生成的实例添加全局光照支持。由于它的实例是在播放模式下生成的，无法进行烘焙，但通过一些努力，它们可以通过Light Probes接收到烘焙的光照。![img](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/mesh-ball/unlit.png)

### Light Probes

我们通过调用一个需要五个额外参数的变体DrawMeshInstanced方法来指示使用Light Probes。首先是阴影投射模式，我们希望它处于开启状态。接下来是实例是否应该投射阴影，我们希望是。接下来是层级，我们只使用默认值零。然后，我们必须提供一个摄像机，实例应该对其可见。传递null意味着它们应该对所有摄像机进行渲染。最后，我们可以设置Light Probes模式。我们必须使用LightProbeUsage.CustomProvided，因为没有单一的位置可以用于混合探头。

```
using UnityEngine;
using UnityEngine.Rendering;

public class MeshBall : MonoBehaviour {
	
	…
	
	void Update () {
		if (block == null) {
			block = new MaterialPropertyBlock();
			block.SetVectorArray(baseColorId, baseColors);
			block.SetFloatArray(metallicId, metallic);
			block.SetFloatArray(smoothnessId, smoothness);
		}
		Graphics.DrawMeshInstanced(
			mesh, 0, material, matrices, 1023, block,
			ShadowCastingMode.On, true, 0, null, LightProbeUsage.CustomProvided
		);
	}
```

我们必须手动为所有实例生成插值的Light Probes，并将它们添加到材质属性块中。这意味着在配置块时需要访问实例位置。我们可以通过获取其转换矩阵的最后一列来检索它们，并将它们存储在一个临时数组中。

```
		if (block == null) {
			block = new MaterialPropertyBlock();
			block.SetVectorArray(baseColorId, baseColors);
			block.SetFloatArray(metallicId, metallic);
			block.SetFloatArray(smoothnessId, smoothness);

			var positions = new Vector3[1023];
			for (int i = 0; i < matrices.Length; i++) {
				positions[i] = matrices[i].GetColumn(3);
			}
		}
```

Light Probes必须通过一个SphericalHarmonicsL2数组提供。通过使用位置和Light Probes数组作为参数，调用LightProbes.CalculateInterpolatedLightAndOcclusionProbes来填充它。还有一个用于遮挡的第三个参数，我们将使用null。

```
			for (int i = 0; i < matrices.Length; i++) {
				positions[i] = matrices[i].GetColumn(3);
			}
			var lightProbes = new SphericalHarmonicsL2[1023];
			LightProbes.CalculateInterpolatedLightAndOcclusionProbes(
				positions, lightProbes, null
			);
```

之后，我们可以通过CopySHCoefficientArraysFrom将Light Probes复制到块中。

```
			LightProbes.CalculateInterpolatedLightAndOcclusionProbes(
				positions, lightProbes, null
			);
			block.CopySHCoefficientArraysFrom(lightProbes);
```

### LPPV


另一种方法是使用LPPV（Light Probe Proxy Volume）。考虑到实例都存在于一个紧密的空间中，这是有道理的。这使我们不必计算和存储插值的Light Probes。此外，它使得能够在不必每帧都提供新的Light Probes数据的情况下，对实例位置进行动画处理，只要它们保持在卷内。

添加一个LightProbeProxyVolume配置字段。如果正在使用它，就不要将Light Probes数据添加到块中。然后将LightProbeUsage.UseProxyVolume传递给DrawMeshInstanced，而不是LightProbeUsage.CustomProvided。我们始终可以将卷作为附加参数提供，即使它为null并且没有使用。

```
	[SerializeField]
	LightProbeProxyVolume lightProbeVolume = null;
	
	…

	void Update () {
		if (block == null) {
			…

			if (!lightProbeVolume) {
				var positions = new Vector3[1023];
				…
				block.CopySHCoefficientArraysFrom(lightProbes);
			}
		}
		Graphics.DrawMeshInstanced(
			mesh, 0, material, matrices, 1023, block,
			ShadowCastingMode.On, true, 0, null,
			lightProbeVolume ?
				LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided,
			lightProbeVolume
		);
	}
```

您可以将LPPV组件添加到网格球上，或将其放在其他位置。可以使用自定义边界模式来定义卷所占据的世界空间区域。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/mesh-ball/lppv-inspector.png)

![scene](https://catlikecoding.com/unity/tutorials/custom-srp/baked-light/mesh-ball/lppv-scene.png)