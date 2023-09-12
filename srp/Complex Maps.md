# **Complex Maps**

## Circuitry Material

到目前为止，我们一直使用非常简单的材质来测试我们的渲染管线。但它还应该支持复杂的材质，以便我们可以表示更有趣的表面。在本教程中，我们将创建一种类似电路的艺术材质，借助一些纹理来实现。

### Albedo

我们材质的基础是它的反照率贴图。它由几层不同深浅的绿色与顶部的金色组成。每个颜色区域都是均匀的，除了一些棕色污渍，这样可以更容易区分后面我们将添加的细节。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/circuitry-material/circuitry-albedo.png)

使用这个反照率贴图，您可以创建一个新的材质，使用Lit着色器。将其平铺设置为2乘以1，这样正方形纹理可以在球体周围包裹，而不会被拉伸得太多。默认球体的极点始终会有很大的变形，这是无法避免的。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/circuitry-material/albedo-inspector.png)

![scene](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/circuitry-material/albedo-scene.png)

### Emission

我们已经支持发射贴图，因此让我们使用一个发射贴图，在金色电路上方添加一个浅蓝色的发光图案。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/circuitry-material/circuitry-emission.png)

将发射贴图分配给材质，并将发射颜色设置为白色，以使其可见。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/circuitry-material/emission-inspector.png)

![scene dark](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/circuitry-material/emission-scene-dark.png)

## Mask Map

目前，我们无法采取太多措施使我们的材质更有趣。金色电路应该是金属的，而绿色电路板则不是，但我们目前只能配置均匀的金属度和光滑度值。我们需要额外的贴图来支持在表面上变化它们。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/mask-map/metallic-smooth.png)

### MODS


我们可以添加一个独立的金属度贴图和一个光滑度贴图，但两者都只需要一个单一通道，所以我们可以将它们合并到一个单一的贴图中。这个贴图被称为遮罩贴图，它的各个通道用于遮罩不同的着色器属性。我们将使用与Unity的HDRP相同的格式，即MODS贴图，其中MODS代表金属度、遮挡度、细节和光滑度，按照这个顺序存储在RGBA通道中。

这是一个用于我们电路的这种贴图。它在所有通道中都包含数据，但目前我们只会使用其R和A通道。由于这个纹理包含的是遮罩数据而不是颜色，请确保禁用其sRGB（颜色纹理）纹理导入属性。如果不这样做，GPU在采样纹理时会错误地应用伽马到线性的转换。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/mask-map/circuitry-mask-mods.png)

为Lit添加一个掩码映射的属性。因为这是一个掩码，我们将使用白色作为默认值，这不会改变任何东西。

```
		[NoScaleOffset] _MaskMap("Mask (MODS)", 2D) = "white" {}
		_Metallic ("Metallic", Range(0, 1)) = 0
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/mask-map/mask-inspector.png)

### Mask Input

在LitInput中添加一个名为GetMask的函数，它简单地对掩码纹理进行采样并返回它。

```
TEXTURE2D(_BaseMap);
TEXTURE2D(_MaskMap);
…

float4 GetMask (float2 baseUV) {
	return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, baseUV);
}
```

在继续之前，让我们对LitInput的代码稍作整理。定义一个名为INPUT_PROP的宏，它带有一个名字参数，以提供一种简化使用UNITY_ACCESS_INSTANCED_PROP宏的方式。

```
#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)
```

现在我们可以简化所有获取函数的代码。我只展示了在GetBase中获取_BaseMap_ST的更改。

```
	float4 baseST = INPUT_PROP(_BaseMap_ST);
```

### Metallic

LitPass不应该知道某些属性是否依赖于掩码图。各个函数可以在需要时获取掩码。在GetMetallic中这样做，通过乘法将其结果与掩码的R通道进行蒙版处理。

```
float GetMetallic (float2 baseUV) {
	float metallic = INPUT_PROP(_Metallic);
	metallic *= GetMask(baseUV).r;
	return metallic;
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/mask-map/metallic-scene.png)

金属度贴图通常是基本二进制的。在我们的情况下，金色电路是完全金属的，而绿色电路板则不是。金色污渍区域是个例外，它们的金属度略低一些。

### Smoothness

在GetSmoothness中执行相同的操作，这次依赖于掩码的A通道。金色电路非常光滑，而绿色电路板则不是。

```
float GetSmoothness (float2 baseUV) {
	float smoothness = INPUT_PROP(_Smoothness);
	smoothness *= GetMask(baseUV).a;
	return smoothness;
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/mask-map/smoothness-scene.png)

### Occlusion

掩码的G通道包含遮挡数据。思路是，像缝隙和孔洞这样的小凹陷区域大部分被对象的其余部分遮挡，但如果这些特征仅由纹理表示，那么光照就会忽略它们。遮挡数据的缺失由掩码提供。添加一个新的GetOcclusion函数来获取它，最初总是返回零以展示其最大效果。

```
float GetOcclusion (float2 baseUV) {
	return 0.0;
}
```

Add the occlusion data to the `**Surface**` struct.

```
struct Surface {
	…
	float occlusion;
	float smoothness;
	float fresnelStrength;
	float dither;
};
```

And initialize it in `LitPassFragment`.

```
	surface.metallic = GetMetallic(input.baseUV);
	surface.occlusion = GetOcclusion(input.baseUV);
	surface.smoothness = GetSmoothness(input.baseUV);
```

思路是遮挡仅适用于间接环境光照。直接光照不受影响，因此当光源直接指向缝隙时，它们不会保持暗。因此，我们只用遮挡来调制IndirectBRDF的结果。

```
float3 IndirectBRDF (
	Surface surface, BRDF brdf, float3 diffuse, float3 specular
) {
	…
	
    return (diffuse * brdf.diffuse + reflection) * surface.occlusion;
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/mask-map/fully-occluded.png)

After having verified that it works have `GetOcclusion` return the G channel of the mask.

```
float GetOcclusion (float2 baseUV) {
	return GetMask(baseUV).g;
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/mask-map/occlusion-scene.png)

绿色电路板的某些部分比其他部分低，因此它们应该有一定程度的遮挡效果。这些区域很大，遮挡图的强度设置为最大以使效果清晰可见，但结果过于强烈，不太合理。与其创建另一个具有更好遮挡数据的掩码图，不如在我们的着色器中添加一个遮挡强度滑块属性。

```
		[NoScaleOffset] _MaskMap("Mask (MODS)", 2D) = "white" {}
		_Metallic ("Metallic", Range(0, 1)) = 0
		_Occlusion ("Occlusion", Range(0, 1)) = 1
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/mask-map/occlusion-inspector.png)

Add it to the `UnityPerMaterial` buffer.

```
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	…
	UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
	UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
```

然后，调整GetOcclusion函数，使其根据该属性调制掩码。在这种情况下，滑块控制掩码的强度，因此如果设置为零，则应完全忽略掩码。我们可以通过在强度基础上在掩码和1之间进行插值来实现这一点。

```
float GetOcclusion (float2 baseUV) {
	float strength = INPUT_PROP(_Occlusion);
	float occlusion = GetMask(baseUV).g;
	occlusion = lerp(occlusion, 1.0, strength);
	return occlusion;
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/mask-map/occlusion-half-strength.png)

## Detail Map

接下来的步骤是向我们的材质添加一些细节。我们通过采样具有比基础贴图更高平铺度的细节纹理，并将其与基础和掩码数据结合在一起来实现这一点。这使得表面更有趣，同时在从近距离观看表面时提供更高分辨率的信息，这种情况下，仅使用基础贴图会显得像素化。

细节应该只稍微修改表面属性，因此我们再次将数据组合在一个单一的非颜色贴图中。HDRP使用ANySNx格式，这意味着它在R中存储反照率调制，在B中存储光滑度调制，并在AG中存储细节法线向量的XY分量。但我们的贴图不包含法线向量，所以我们只使用RB通道。因此，它是一个RGB贴图，而不是RGBA。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/circuitry-detail.png)

### Detail UV coordinates

由于细节贴图应该使用比基础贴图更高的平铺度，因此它需要自己的平铺度和偏移值。添加一个材质属性用于细节贴图，这次不需要使用NoScaleOffset属性。它的默认值应该不会引起任何改变，我们可以通过使用linearGrey来实现，因为值为0.5将被视为中性。

```
		[NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
		[HDR] _EmissionColor("Emission", Color) = (0.0, 0.0, 0.0, 0.0)

		_DetailMap("Details", 2D) = "linearGrey" {}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/detail-map-inspector.png)

我们可以简单地缩放基础UV吗？ 是的，这也是可能的，而且甚至可能很方便。但是，为细节使用完全独立的UV坐标提供了最大的灵活性。这还使得可以在不依赖基础贴图的情况下使用细节贴图，尽管这种情况很少见。

将所需的纹理、采样器状态以及平铺度和偏移属性添加到LitInput中，还添加一个TransformDetailUV函数来转换细节纹理坐标。

```
TEXTURE2D(_DetailMap);
SAMPLER(sampler_DetailMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
	…
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

…

float2 TransformDetailUV (float2 detailUV) {
	float4 detailST = INPUT_PROP(_DetailMap_ST);
	return detailUV * detailST.xy + detailST.zw;
}
```

然后添加一个GetDetail函数，以给定的细节UV获取所有细节数据。

```
float4 GetDetail (float2 detailUV) {
	float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUV);
	return map;
}
```

Transform the coordinates in `LitPassVertex` and pass them along via `**Varyings**`.

```
struct Varyings {
	…
	float2 baseUV : VAR_BASE_UV;
	float2 detailUV : VAR_DETAIL_UV;
	…
};

Varyings LitPassVertex (Attributes input) {
	…
	output.baseUV = TransformBaseUV(input.baseUV);
	output.detailUV = TransformDetailUV(input.baseUV);
	return output;
}
```

### Detailed Albedo

要将细节添加到反照率贴图中，我们必须向GetBase添加一个细节UV的参数，默认设置为零，以防止现有代码出现问题。首先，简单地将所有细节直接添加到基础贴图中，然后再考虑颜色色调。

```
float4 GetBase (float2 baseUV, float2 detailUV = 0.0) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
	float4 color = INPUT_PROP(_BaseColor);
	
	float4 detail = GetDetail(detailUV);
	map += detail;
	
	return map * color;
}
```

Then pass the detail UV to it in `LitPassFragment`.

```
	float4 base = GetBase(input.baseUV, input.detailUV);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/albedo-added.png)

这确认了细节数据被正确采样，但我们还没有正确解释它。首先，值为0.5表示中性。较高的值应该增加或变亮，而较低的值应该减少或变暗。使其工作的第一步是在GetDetail中将细节值范围从0-1转换为-1-1。

```
float4 GetDetail (float2 detailUV) {
	float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUV);
	return map * 2.0 - 1.0;
}
```

其次，只有R通道会影响反照率，将其推向黑色或白色。这可以通过根据细节的符号插值颜色为0或1来实现。插值器然后是细节值的绝对值。这应该只影响反照率，而不影响基础贴图的α通道。

```
	float detail = GetDetail(detailUV).r;
	//map += detail;
	map.rgb = lerp(map.rgb, detail < 0.0 ? 0.0 : 1.0, abs(detail));
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/albedo-interpolated.png)

这个方法有效，并且非常明显，因为我们的细节贴图非常强烈。但是，增亮效果似乎比变暗效果更强。这是因为我们在线性空间中应用了修改。在伽马空间中进行操作将更好地匹配视觉上的均等分布。我们可以通过插值反照率的平方根，然后再平方来近似实现这一点。

```
	map.rgb = lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail));
	map.rgb *= map.rgb;
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/albedo-perceptual.png)

目前，细节被应用于整个表面，但想法是大部分金色电路不受影响。这就是细节掩码的作用，存储在掩码图的B通道中。我们可以通过将其纳入插值器来应用它。

```
	float mask = GetMask(baseUV).b;
	map.rgb = lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/albedo-detail-masked.png)

我们的细节目前处于最大可能的强度，这太强了。让我们引入一个细节反照率强度滑块属性，以将它们缩小。

```
		_DetailMap("Details", 2D) = "linearGrey" {}
		_DetailAlbedo("Detail Albedo", Range(0, 1)) = 1
```

Add it to `UnityPerMaterial` and multiply it with the detail in `GetBase`.

```
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	…
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

…

float4 GetBase (float2 baseUV, float2 detailUV = 0.0) {
	…
	float detail = GetDetail(detailUV).r * INPUT_PROP(_DetailAlbedo);
	…
}
```

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/detail-albedo-inspector.png)

![scene](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/detail-albedo-scene.png)

### Detailed Smoothness

将细节添加到光滑度上的方法相同。首先，也为光滑度添加一个强度滑块属性。

```
		_DetailAlbedo("Detail Albedo", Range(0, 1)) = 1
		_DetailSmoothness("Detail Smoothness", Range(0, 1)) = 1
```

然后将该属性添加到UnityPerMaterial中，在GetSmoothness中获取已缩放的细节，并以相同的方式插值。这次我们需要使用细节贴图的B通道。

```
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	…
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

…

float GetSmoothness (float2 baseUV, float2 detailUV = 0.0) {
	float smoothness = INPUT_PROP(_Smoothness);
	smoothness *= GetMask(baseUV).a;

	float detail = GetDetail(detailUV).b * INPUT_PROP(_DetailSmoothness);
	float mask = GetMask(baseUV).b;
	smoothness = lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
	
	return smoothness;
}
```

Have `LitPassFragment` pass the detail UV to `GetSmoothness` as well.

```
	surface.smoothness = GetSmoothness(input.baseUV, input.detailUV);
```

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/detail-smoothness-inspector.png)

![scene 0.2](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/detail-smoothness-scene-02.png)

### Fading Details

细节只在它们在视觉上足够大的情况下才重要。当细节太小时，不应该应用它们，因为这可能会产生嘈杂的结果。Mip映射通常会模糊数据，但对于细节，我们希望进一步淡化它们。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/not-faded.png)

Unity可以自动为我们淡化细节，如果我们启用了细节纹理的Fadeout Mip Maps导入选项。将出现一个范围滑块，用于控制淡化何时开始和结束的Mip级别。Unity简单地将Mip贴图插值为灰色，这意味着该贴图变得中性。为使此功能起作用，纹理的过滤模式必须设置为Trilinear，这应该会自动完成。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/faded-inspector.png)

![scene](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/detail-map/faded-scene.png)

## Normal Maps


尽管我们使表面变得更加复杂，但它仍然看起来平坦，因为它确实是平面的。光照与表面法线交互，这会在每个三角形上平滑地插值。如果光照也与其较小的特征交互，那么我们的表面将更具可信度。我们可以通过添加对法线贴图的支持来实现这一点。

通常，法线贴图是从具有高多边形密度的3D模型生成的，然后将其烘焙到较低多边形模型以供实时使用。高多边形几何体的法线向量会在法线贴图中进行烘焙。或者，法线贴图可以进行程序生成。对于我们的电路来说，这就是这样的贴图。在导入后，将其Texture Type设置为Normal map。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/normal-maps/circuitry-normal.png)

这个法线贴图遵循标准的切线空间法线贴图约定，将上轴（在这种情况下指的是Z轴）存储在B通道中，而右侧和前进的XY轴存储在RG中。与细节贴图一样，法线分量的-1到1范围被转换，使0.5成为中点。因此，平坦区域会呈现蓝色。

### Sampling Normals

为了采样法线，我们必须向着色器添加一个法线贴图纹理属性，以bump作为默认值，表示一个平坦的贴图。还要添加一个法线缩放属性，以便我们可以控制贴图的强度。

```
		[NoScaleOffset] _NormalMap("Normals", 2D) = "bump" {}
		_NormalScale("Normal Scale", Range(0, 1)) = 1
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/normal-maps/normals-inspector.png)


以RGB通道存储法线信息的方式如上所述是最直接的方式，但这不是最高效的方式。如果我们假设法线向量总是指向上而不是向下，我们可以省略向上的分量，并从其他两个分量中派生出它。这两个通道可以以一种压缩的纹理格式存储，以最小化精度损失。XY通常存储在RG或AG中，具体取决于纹理格式。这将改变纹理的外观，但Unity编辑器只显示原始贴图的预览和缩略图。

是否更改法线贴图取决于目标平台。如果不更改贴图，则会定义UNITY_NO_DXT5nm。如果是这样，我们可以使用UnpackNormalRGB函数来转换采样的法线数据，否则我们可以使用UnpackNormalmapRGorAG。这两个函数都有采样和缩放参数，并在Core RP Library的Packing文件中定义。添加一个函数到Common，使用这些函数来解码法线数据。

```
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

…

float3 DecodeNormal (float4 sample, float scale) {
	#if defined(UNITY_NO_DXT5nm)
	    return UnpackNormalRGB(sample, scale);
	#else
	    return UnpackNormalmapRGorAG(sample, scale);
	#endif
}
```

这假设那些Unity函数返回了归一化的向量，但在Unity 2022中已不再是这样，所以我们需要自己归一化它们。

```
float3 DecodeNormal (float4 sample, float scale) {
	#if defined(UNITY_NO_DXT5nm)
	    return normalize(UnpackNormalRGB(sample, scale));
	#else
	    return normalize(UnpackNormalmapRGorAG(sample, scale));
	#endif
}
```


DXT5代表什么？ DXT5，也称为BC3（块压缩3），是一种纹理压缩格式，将纹理分成4x4像素的块。每个块使用两种颜色进行逐像素插值的近似。每个颜色通道使用的位数不同。在DXT5中，R和B通道各使用五位，G通道使用六位，A通道使用八位。这就是为什么法线向量的X坐标移动到A通道的原因。此外，RGB通道共享一个查找表，而A通道有自己的查找表，这使法线向量的X和Y分量得以隔离。

当DXT5用于存储法线向量时，通常称为DXT5nm。然而，当需要更高的压缩质量时，Unity更倾向于使用BC7压缩。BC7压缩的工作原理类似，但它允许每个通道的位数不同。在这种情况下，X通道无需移动，最终纹理大小更大，因为在两个通道上使用更多位数，从而提高了纹理质量。

UnpackNormalmapRGorAG函数可以处理这两种方法，通过将R和A通道相乘来实现。这要求未使用的通道被设置为1，Unity会自动处理这一步。

现在将法线贴图、法线缩放和一个GetNormalTS函数添加到LitInput中，并获取并解码法线向量。

```
TEXTURE2D(_NormalMap);
…

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	…
	UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

…

float3 GetNormalTS (float2 baseUV) {
	float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, baseUV);
	float scale = INPUT_PROP(_NormalScale);
	float3 normal = DecodeNormal(map, scale);
	return normal;
}
```

### Tangent Space

因为纹理围绕几何体，所以它们在对象空间和世界空间中的方向不是均匀的。因此，法线存储的空间弯曲以匹配几何体的表面。唯一的常数是这个空间与表面切线。这就是为什么它被称为切线空间的原因。这个空间的Y轴与表面法线匹配。此外，它必须有一个X轴，与表面相切。如果我们有这两个轴，我们可以从中生成Z forward轴。

由于切线空间的X轴不是恒定的，它必须作为网格顶点数据的一部分进行定义。它被存储为一个四分量切线向量。**它的XYZ分量定义了对象空间中的轴。它的W分量要么是−1，要么是1，用于控制Z轴指向的方向**。这用于翻转具有双边对称性的网格的法线贴图，大多数动物都具有这种对称性，因此可以在网格的两侧使用相同的贴图，减半所需的纹理大小。

因此，如果我们**有世界空间的法线和切线向量，我们可以构建从切线到世界空间的转换矩阵**。我们可以使用现有的CreateTangentToWorld函数来完成这个任务，将法线、切线XYZ和切线W作为参数传递给它。然后，我们可以使用TransformTangentToWorld函数，将切线空间法线和转换矩阵作为参数传递。添加一个函数到Common，以执行所有这些操作。

```
float3 NormalTangentToWorld (float3 normalTS, float3 normalWS, float4 tangentWS) {
	float3x3 tangentToWorld =
		CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
	return TransformTangentToWorld(normalTS, tangentToWorld);
}
```

接下来，在Attributes中添加具有TANGENT语义的对象空间切线向量，并在LitPass中将世界空间切线向量添加到Varyings中。

```
struct Attributes {
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	float4 tangentOS : TANGENT;
	…
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	float3 positionWS : VAR_POSITION;
	float3 normalWS : VAR_NORMAL;
	float4 tangentWS : VAR_TANGENT;
	…
};
```

切线向量的XYZ部分可以在LitPassVertex中通过调用TransformObjectToWorldDir转换为世界空间。

```
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.tangentWS =
		float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
```

最后，在LitPassFragment中通过调用NormalTangentToWorld获取最终映射的法线。

```
	surface.normal = NormalTangentToWorld(
		GetNormalTS(input.baseUV), input.normalWS, input.tangentWS
	);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/normal-maps/normal-mapped.png)

### Interpolated Normal for Shadow Bias

扰动法线向量适用于照明表面，但我们还使用片段法线来偏移阴影采样。因此，我们应该使用原始表面法线。因此，将一个字段添加到Surface中以存储原始表面法线是合适的。

```
struct Surface {
	float3 position;
	float3 normal;
	float3 interpolatedNormal;
	…
};
```

在LitPassFragment中分配法线向量。在这种情况下，通常我们可以跳过对向量进行归一化，因为大多数网格的顶点法线不会在每个三角形上弯曲得很多，以至于会对阴影偏移产生负面影响。

```
	surface.normal = NormalTangentToWorld(
		GetNormalTS(input.baseUV), input.normalWS, input.tangentWS
	);
	surface.interpolatedNormal = input.normalWS;
```

Then use this vector in `GetCascadedShadow`.

```
float GetCascadedShadow (
	DirectionalShadowData directional, ShadowData global, Surface surfaceWS
) {
	float3 normalBias = surfaceWS.interpolatedNormal *
		(directional.normalBias * _CascadeData[global.cascadeIndex].y);
	…
	if (global.cascadeBlend < 1.0) {
		normalBias = surfaceWS.interpolatedNormal *
			(directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		…
	}
	return shadow;
}
```

### Detailed Normals

我们也可以为细节包括一个法线贴图。尽管HDRP将细节法线与反照率和光滑度合并到一个单独的贴图中，但我们将使用一个单独的纹理。将导入的纹理转换为法线贴图，并启用Fadeout Mip Maps，以使其像其他细节一样淡化。

为什么不将这两个贴图合并？ 虽然这样更高效，但生成这样的贴图更加困难。法线向量在生成Mip贴图时应该与其他数据通道进行不同处理，而Unity的纹理导入器无法做到这一点。此外，Unity在淡化Mip贴图时会忽略Alpha通道，因此该通道中的数据不会得到正确的淡化。因此，我们需要自己生成Mip贴图，无论是在Unity之外还是使用脚本。即便如此，我们仍然需要手动解码法线数据，而不能依赖于UnpackNormalmapRGorAG。我在本教程中不会涵盖所有这些内容。

Add shader properties for the map and again for the normal scale.

```
		_DetailMap("Details", 2D) = "linearGrey" {}
		[NoScaleOffset] _DetailNormalMap("Detail Normals", 2D) = "bump" {}
		_DetailAlbedo("Detail Albedo", Range(0, 1)) = 1
		_DetailSmoothness("Detail Smoothness", Range(0, 1)) = 1
		_DetailNormalScale("Detail Normal Scale", Range(0, 1)) = 1
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/normal-maps/detail-normal-inspector.png)

通过添加一个细节UV参数并采样细节贴图，调整GetNormalTS。在这种情况下，我们可以通过将它纳入细节法线的强度来应用掩码。之后，我们必须组合这两个法线，可以通过调用BlendNormalRNM与原始法线和细节法线来实现。这个函数会围绕基础法线旋转细节法线。

```
float3 GetNormalTS (float2 baseUV, float2 detailUV = 0.0) {
	float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, baseUV);
	float scale = INPUT_PROP(_NormalScale);
	float3 normal = DecodeNormal(map, scale);

	map = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailMap, detailUV);
	scale = INPUT_PROP(_DetailNormalScale) * GetMask(baseUV).b;
	float3 detail = DecodeNormal(map, scale);
	normal = BlendNormalRNM(normal, detail);

	return normal;
}
```

Finally, pass the detail UV to `GetNormalTS`.

```
	surface.normal = NormalTangentToWorld(
		GetNormalTS(input.baseUV, input.detailUV), input.normalWS, input.tangentWS
	);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/normal-maps/detail-normal-mapped.png)

## Optional Maps

并不是每种材质都需要我们目前支持的所有贴图。如果没有分配贴图，意味着结果不会被修改，但着色器仍然会执行所有工作，使用默认纹理。我们可以通过添加一些着色器功能来控制着色器使用哪些贴图，以避免不必要的工作。Unity的着色器会根据编辑器中分配的贴图自动执行此操作，但我们将使用明确的开关来控制此操作

### Normal Maps

我们首先考虑法线贴图，这是最昂贵的功能之一。添加一个与适当关键字相关联的切换着色器属性。

```
		[Toggle(_NORMAL_MAP)] _NormalMapToggle ("Normal Map", Float) = 0
		[NoScaleOffset] _NormalMap("Normals", 2D) = "bump" {}
		_NormalScale("Normal Scale", Range(0, 1)) = 1
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/optional-maps/optional-normal-maps.png)

仅在CustomLit通道中添加一个匹配的着色器功能预处理指令。其他通道都不需要映射法线，因此不应该使用这个功能。

```
			#pragma shader_feature _NORMAL_MAP
```

在LitPassFragment中，根据关键字，可以使用切线空间法线或对插值法线进行归一化。在后一种情况下，我们可以使用归一化版本进行插值法线。

```
	#if defined(_NORMAL_MAP)
		surface.normal = NormalTangentToWorld(
			GetNormalTS(input.baseUV, input.detailUV),
			input.normalWS, input.tangentWS
		);
		surface.interpolatedNormal = input.normalWS;
	#else
		surface.normal = normalize(input.normalWS);
		surface.interpolatedNormal = surface.normal;
	#endif
```

此外，如果可能的话，可以省略Varyings中的切线向量。不需要从Attributes中省略它，因为如果不使用它，它会在那里自动被忽略。

```
struct Varyings {
	…
	#if defined(_NORMAL_MAP)
		float4 tangentWS : VAR_TANGENT;
	#endif
	…
};

Varyings LitPassVertex (Attributes input) {
	…
	#if defined(_NORMAL_MAP)
		output.tangentWS = float4(
			TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w
		);
	#endif
	…
}
```

### Input Config

在这一点上，我们应该重新考虑如何将数据传递给LitInput的getter函数。我们最终可能会使用或不使用多种数据的任何组合，我们需要以某种方式进行通信。我们可以通过引入一个InputConfig结构来实现这一点，最初捆绑基本UV和细节UV坐标。同时创建一个方便的GetInputConfig函数，根据基本UV和可选的细节UV返回一个配置。

```
struct InputConfig {
	float2 baseUV;
	float2 detailUV;
};

InputConfig GetInputConfig (float2 baseUV, float2 detailUV = 0.0) {
	InputConfig c;
	c.baseUV = baseUV;
	c.detailUV = detailUV;
	return c;
}
```

现在调整所有LitInput函数，除了TransformBaseUV和TransformDetailUV之外，使它们具有一个单一的config参数。我只展示对GetBase的更改。

```
float4 GetBase (InputConfig c) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	float4 color = INPUT_PROP(_BaseColor);
	
	float detail = GetDetail(c).r * INPUT_PROP(_DetailAlbedo);
	float mask = GetMask(c).b;
	…
}
```

Then adjust `LitPassFragment` so it uses the new config approach.

```
	InputConfig config = GetInputConfig(input.baseUV, input.detailUV);
	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif
	
	Surface surface;
	surface.position = input.positionWS;
	#if defined(_NORMAL_MAP)
		surface.normal = NormalTangentToWorld(
			GetNormalTS(config), input.normalWS, input.tangentWS
		);
	#else
		surface.normal = normalize(input.normalWS);
	#endif
	…
	surface.metallic = GetMetallic(config);
	surface.occlusion = GetOcclusion(config);
	surface.smoothness = GetSmoothness(config);
	surface.fresnelStrength = GetFresnel(config);
	…
	color += GetEmission(config);
```

调整其他通道，包括MetaPass、ShadowCasterPass和UnlitPass，以使用新的方法。这意味着我们还必须使UnlitPass使用新的方法。

### Optional Mask Map

接下来，通过向InputConfig添加一个布尔值来使掩码贴图成为可选项，默认设置为false。

```
struct InputConfig {
	float2 baseUV;
	float2 detailUV;
	bool useMask;
};

InputConfig GetInputConfig (float2 baseUV, float2 detailUV = 0.0) {
	InputConfig c;
	c.baseUV = baseUV;
	c.detailUV = detailUV;
	c.useMask = false;
	return c;
}
```

我们可以通过在GetMask中简单地返回1来避免采样掩码。这假设掩码开关是恒定的，因此不会在着色器中引发分支。

```
float4 GetMask (InputConfig c) {
	if (c.useMask) {
		return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, c.baseUV);
	}
	return 1.0;
}
```

Add a toggle for it to our shader

```
[Toggle(_MASK_MAP)] _MaskMapToggle ("Mask Map", Float) = 0
		[NoScaleOffset] _MaskMap("Mask (MODS)", 2D) = "white" {}
```

Along with the relevant pragma in the *CustomLit* pass.

```
			#pragma shader_feature _MASK_MAP
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/optional-maps/optional-mask-map.png)

Now turn on the mask in `LitPassFragment` only when needed.

```
	InputConfig config = GetInputConfig(input.baseUV, input.detailUV);
	#if defined(_MASK_MAP)
		config.useMask = true;
	#endif
```

### Optional Detail

使用相同的方法，向InputConfig添加一个用于细节的切换选项，默认情况下为禁用。

```
struct InputConfig {
	…
	bool useDetail;
};

InputConfig GetInputConfig (float2 baseUV, float2 detailUV = 0.0) {
	…
	c.useDetail = false;
	return c;
}
```

只有在需要的时候才在GetDetail中采样细节贴图，否则返回零。

```
float4 GetDetail (InputConfig c) {
	if (c.useDetail) {
		float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, c.detailUV);
		return map * 2.0 - 1.0;
	}
	return 0.0;
}
```

这避免了采样细节贴图，但仍然会发生细节的整合。为了停止这个过程，也跳过GetBase中的相关代码。

```
	if (c.useDetail) {
		float detail = GetDetail(c).r * INPUT_PROP(_DetailAlbedo);
		float mask = GetMask(c).b;
		map.rgb =
			lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
		map.rgb *= map.rgb;
	}
```

And in `GetSmoothness`.

```
	if (c.useDetail) {
		float detail = GetDetail(c).b * INPUT_PROP(_DetailSmoothness);
		float mask = GetMask(c).b;
		smoothness =
			lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
	}
```

And in `GetNormalTS`.

```
	if (c.useDetail) {
		map = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailMap, c.detailUV);
		scale = INPUT_PROP(_DetailNormalScale) * GetMask(c).b;
		float3 detail = DecodeNormal(map, scale);
		normal = BlendNormalRNM(normal, detail);
	}
```

Then add a toggle property for the details to the shader.

```
		[Toggle(_DETAIL_MAP)] _DetailMapToggle ("Detail Maps", Float) = 0
		_DetailMap("Details", 2D) = "linearGrey" {}
```

Once again with an accompanying shader feature in *CustomLit*.

```
	#pragma shader_feature _DETAIL_MAP
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/optional-maps/optional-detail-maps.png)

现在只有在定义相关关键字时，才需要在Varyings中包括细节UV。

```
struct Varyings {
	…
	#if defined(_DETAIL_MAP)
		float2 detailUV : VAR_DETAIL_UV;
	#endif
	…
};

Varyings LitPassVertex (Attributes input) {
	…
	#if defined(_DETAIL_MAP)
		output.detailUV = TransformDetailUV(input.baseUV);
	#endif
	return output;
}
```

Finally, include details in `LitPassFragment` only when needed.

```
	InputConfig config = GetInputConfig(input.baseUV);
	#if defined(_MASK_MAP)
		config.useMask = true;
	#endif
	#if defined(_DETAIL_MAP)
		config.detailUV = input.detailUV;
		config.useDetail = true;
	#endif
```