# LOD and Reflections

许多小物体可以为场景增添细节，使其更加有趣。然而，那些过于小以至于无法覆盖多个像素的细节会退化成模糊的噪音。在这些视觉尺度下，最好不要渲染它们，这还可以释放 CPU 和 GPU 以渲染更重要的物体。我们还可以决定在它们仍然可以被区分的时候更早地剔除这些物体。这可以进一步提高性能，但会导致物体根据它们的视觉大小突然出现和消失。我们还可以添加中间步骤，在最终完全剔除物体之前逐渐切换到越来越不详细的可视化。Unity通过使用LOD组，可以实现所有这些操作。

### LOD Group Component

您可以通过创建一个空的游戏对象并向其添加一个LODGroup组件来将LOD（级别细节）组添加到场景中。默认组定义了四个级别：LOD 0，LOD 1，LOD 2，最后是剔除，这意味着不会渲染任何内容。这些百分比表示估计的可视大小阈值，相对于显示窗口的尺寸而言。因此，LOD 0 通常用于覆盖窗口大于60%的对象，通常考虑垂直尺寸，因为这是最小的尺寸。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/group-component.png)


然而，*Quality* 项目设置部分包含一个LODBias（LOD Bias）选项，它会调整这些阈值。默认情况下，它设置为2，这意味着它会将此评估的估计可视大小加倍。因此，LOD 0 最终用于大于30%而不是60%的一切。当Bias设置为除1以外的值时，组件的检查器会显示警告。此外，还有一个“最大LOD级别”选项，可以用来限制最高的LOD级别。例如，如果将其设置为1，那么LOD 1也会被用于替代LOD 0。

想法是将可视化LOD级别的所有游戏对象都作为组对象的子对象。例如，您可以使用相同大小的三个彩色球体来表示三个LOD级别。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/lod-group-sphere.png)

每个对象都必须分配到适当的LOD级别。您可以通过在组件中选择一个级别块，然后将对象拖放到其“渲染器列表”上，或者直接将其拖放到一个LOD级别块上来完成此操作。这样可以确保每个对象在适当的LOD级别下进行渲染。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/lod-renderers.png)

Unity会自动渲染适当的对象。但是，如果在编辑器中选择特定对象，它将覆盖这种行为，这样您可以在场景中看到所选对象。如果选择了LOD组本身，编辑器还会指示当前可见的LOD级别是哪个。这有助于您在编辑器中了解当前的LOD级别显示情况。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/rendering-lod-spheres.png)

通过移动摄像机，您可以改变每个组使用的LOD级别。或者，您还可以调整LOD bias以在保持其他所有内容不变的情况下查看可视化效果的变化。这些方法允许您根据观察视角和距离来自动切换不同的LOD级别，以提高性能并确保场景中的对象呈现适当的细节。

### Additive LOD Groups

对象可以添加到多个LOD级别中。您可以使用这一特性，在较高的级别添加较小的细节，同时在多个级别中使用相同的较大对象。例如，您可以使用叠放的扁平立方体制作一个三层金字塔。基础立方体是所有三个级别的一部分。中间的立方体是LOD 0和LOD 1的一部分，而最小的顶部立方体仅属于LOD 0。因此，可以根据可视大小来添加和移除细节，而不是替换整个对象。这允许您更精细地控制渲染细节，以优化性能。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/stacked-cubes-lod.png)

可以对LOD组进行光照贴图吗？ 可以。当您使LOD组对全局光照贡献时，它会包含在光照贴图中。LOD 0如预期一样用于光照贴图。其他LOD级别也会获得烘焙光照，但场景的其余部分只考虑LOD 0。您还可以选择仅对一些级别进行烘焙，让其他级别依赖于光探头（light probes）。这使得您可以更灵活地控制场景的光照效果。

### LOD Transitions

LOD级别的突然切换可能在视觉上显得突兀，特别是如果一个对象由于自身或摄像机的轻微移动而频繁地快速切换。通过将LOD组的Fade Mode设置为Cross Fade，可以使这种过渡变得渐进。这样，旧级别在新级别淡入的同时淡出，从而使过渡更加平滑。这对于减少视觉不连续性非常有用，让对象的LOD级别变化更加流畅。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/cross-fade-mode.png)

关于SpeedTree的渐变模式选项如何？ 该模式专门用于SpeedTree树木，它使用自己的LOD系统来折叠树木并在3D模型和广告牌表示之间进行过渡。我们不会使用它。

您可以在每个LOD级别中控制切换到下一个级别的交叉淡入何时开始。当启用交叉淡入时，此选项将变为可见。Fade Transition Width为零表示此级别与下一个较低级别之间没有淡入淡出，而值为1表示它立即开始淡入淡出。在默认设置下，当设置为0.5时，LOD 0会在80%的时候开始与LOD 1进行交叉淡入淡出。这个参数允许您精确地控制LOD级别之间的淡入淡出过渡，以满足特定的视觉需求。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/fade-transition-width.png)

在交叉淡入激活时，两个LOD级别会同时进行渲染。如何混合它们取决于着色器。Unity会为LOD_FADE_CROSSFADE关键字选择一个着色器变体，因此为我们的Lit着色器添加一个多编译指令来支持它。在CustomLit和ShadowCaster通道都需要添加这个指令。这将确保着色器能够正确地处理LOD级别之间的交叉淡入淡出效果。

```
			#pragma multi_compile _ LOD_FADE_CROSSFADE
```

如果使用了淡入淡出效果，对象的淡入程度通过UnityPerDraw缓冲区的unity_LODFade向量进行传递，我们已经定义了这个向量。它的X分量包含淡入因子。它的Y分量包含相同的因子，但以十六个步骤进行量化，我们不会使用它。让我们可视化淡入因子，如果正在使用它，可以在LitPassFragment的开头返回它。

```
float4 LitPassFragment (Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
	#if defined(LOD_FADE_CROSSFADE)
		return unity_LODFade.x;
	#endif
	
	…
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/fade-factor.png)

正在淡出的对象从1开始逐渐减小到零，这是预期的行为。但我们还看到了表示较高LOD级别的实心黑色对象。这是因为正在淡入的对象其淡入因子被取反。我们可以通过返回取反的淡入因子来观察到这一点。

```
		return -unity_LODFade.x;
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/fade-factor-negated.png)

请注意，同时存在于两个LOD级别的对象不会与自身进行交叉淡入淡出。这是因为它们已经是最高LOD级别的一部分，不需要淡入淡出效果。

### Dithering

要混合两个LOD级别，我们可以使用剪裁（clipping），采用类似于近似半透明阴影的方法。因为我们需要同时处理表面和它们的阴影，让我们在Common中添加一个名为ClipLOD的函数来实现这个功能。给它传递剪裁空间（clip-space）的XY坐标以及淡入淡出因子作为参数。然后，如果启用了交叉淡入淡出，可以根据淡入淡出因子减去一个抖动模式来进行剪裁。这样可以实现不同LOD级别的混合效果。

```
void ClipLOD (float2 positionCS, float fade) {
	#if defined(LOD_FADE_CROSSFADE)
		float dither = 0;
		clip(fade - dither);
	#endif
}
```

为了检查剪裁是否按预期工作，我们可以从一个垂直渐变开始，每32个像素重复一次。这应该会创建交替的水平条纹。通过观察这些效果，您可以验证剪裁是否按照所需的方式工作。

```
		float dither = (positionCS.y % 32) / 32;
```

在LitPassFragment中调用ClipLOD函数，而不是直接返回淡入淡出因子。这将让我们使用剪裁来处理不同LOD级别之间的混合效果，以取代直接的渲染结果。

```
	//#if defined(LOD_FADE_CROSSFADE)
	//	return unity_LODFade.x;
	//#endif
	ClipLOD(input.positionCS.xy, unity_LODFade.x);
```

同时在ShadowCasterPassFragment的开头调用ClipLOD函数，以实现阴影的交叉淡入淡出效果。这将确保在阴影通道中也使用剪裁来处理不同LOD级别之间的混合

```
void ShadowCasterPassFragment (Varyings input) {
	UNITY_SETUP_INSTANCE_ID(input);
	ClipLOD(input.positionCS.xy, unity_LODFade.x);

	…
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/striped-lod-half.png)

我们得到了条纹渲染，但在交叉淡入淡出时只有两个LOD级别中的一个显示出来。这是因为其中一个具有负的淡入淡出因子。为解决这个问题，当出现这种情况时，我们应该将抖动模式加上去，而不是减去它。这将确保在淡入淡出期间正确混合两个LOD级别。

```
	clip(fade + (fade < 0.0 ? dither : -dither));
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/striped-lod-complete.png)

既然它正常工作了，我们可以切换到一个适当的抖动模式。让我们选择与我们用于半透明阴影相同的抖动模式。这将提高渲染的质量和准确性。

```
		float dither = InterleavedGradientNoise(positionCS.xy, 0);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/dithered-lod.png)

### Animated Cross-Fading

尽管抖动可以创建相对平滑的过渡，但抖动模式仍然会显而易见。与半透明阴影一样，淡出的效果可能会不稳定和令人分心。理想情况下，交叉淡入淡出只是一个临时的效果，即使在这种情况下也不会有其他任何变化。我们可以通过启用LOD组的“Animate Cross-fading”选项来实现这一点。这将忽略淡入淡出过渡宽度，而是一旦组通过了LOD阈值，就会快速进行交叉淡入淡出。这样可以确保过渡是临时的，不会引起不稳定的效果。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/animated-cross-fading.png)

默认的动画持续时间是半秒，可以通过设置静态属性`LODGroup.crossFadeAnimationDuration`来更改所有组的动画持续时间。然而，在不处于播放模式时，Unity 2022中的过渡速度会更快。这是因为在编辑模式下，Unity通常会采用更快的速度以提高开发效率，而在播放模式下才会采用实际的持续时间来模拟淡入淡出效果。这一行为确保了在编辑时可以更快地预览效果，而在播放时才会以实际的速度呈现。

## Reflections

环境的镜面反射是增加场景细节和逼真度的另一现象，镜子是最明显的例子，但我们目前还不支持。这对于金属表面尤为重要，目前它们大多是黑色的。为了使这一点更加明显，您可以向“Baked Light”场景中添加更多的金属球体，它们具有不同的颜色和光滑度属性。这将帮助您在场景中突出金属材质的效果。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/reflections/without-reflections.png)

### Indirect BRDF

我们已经支持漫反射全局光照，这取决于BRDF的漫反射颜色。现在我们还要添加镜面全局光照，这也依赖于BRDF。所以让我们在BRDF中添加一个IndirectBRDF函数，它需要表面和BRDF参数，以及从全局光照中获取的漫反射和镜面颜色。最初，让它只返回反射的漫反射光。这是一个良好的起点，可以在此基础上继续添加镜面反射的支持。

```
float3 IndirectBRDF (
	Surface surface, BRDF brdf, float3 diffuse, float3 specular
) {
    return diffuse * brdf.diffuse;
}
```

添加镜面反射开始时类似：只需包括乘以BRDF的镜面颜色的镜面GI即可。这将帮助您逐步实现镜面反射的支持，使场景更加逼真。

```
	float3 reflection = specular * brdf.specular;

    return diffuse * brdf.diffuse + reflection;
```

正确，粗糙度确实会散射反射光线，因此它应该减小我们最终看到的镜面反射。为实现这一效果，我们可以通过将镜面GI除以粗糙度的平方加一来实现。因此，低粗糙度值不会产生太大影响，而最大粗糙度将反射减半。这将考虑到材质的粗糙度，使镜面反射看起来更加自然。

```
	float3 reflection = specular * brdf.specular;
	reflection /= brdf.roughness * brdf.roughness + 1.0;
```

在GetLighting中调用IndirectBRDF来获取间接的BRDF光照，而不是直接计算漫反射间接光照。一开始，可以使用白色作为镜面GI颜色，以确保镜面反射的效果。这将有助于实现镜面全局光照。

```
float3 GetLighting (Surface surfaceWS, BRDF brdf, GI gi) {
	ShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;
	
	float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, 1.0);
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
	}
	return color;
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/reflections/reflecting-white-environment.png)

一切都变得至少亮了一点，因为我们添加了之前缺少的光照。对于金属表面的变化是显著的：它们的颜色现在明亮而明显。

### Sampling the Environment

镜面反射会镜像环境，通常默认为天空盒（skybox）。它作为一个立方体贴图纹理可通过unity_SpecCube0获得。在GI（全局光照）中与其采样状态一起声明，这次使用TEXTURECUBE宏。

```
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);
```

然后，添加一个SampleEnvironment函数，该函数带有世界坐标表面参数，采样纹理并返回其RGB分量。我们通过SAMPLE_TEXTURECUBE_LOD宏来采样立方体贴图，该宏接受地图、采样器状态、UVW坐标和Mip级别作为参数。由于它是一个立方体贴图，我们需要使用3D纹理坐标，因此使用UVW。我们始终从使用最高Mip级别开始，因此我们采样全分辨率纹理

```
float3 SampleEnvironment (Surface surfaceWS) {
	float3 uvw = 0.0;
	float4 environment = SAMPLE_TEXTURECUBE_LOD(
		unity_SpecCube0, samplerunity_SpecCube0, uvw, 0.0
	);
	return environment.rgb;
}
```

采样立方体贴图是根据一个方向进行的，而在这种情况下，该方向是从相机到表面反射的视线方向。我们通过使用负视线方向和表面法线作为参数来调用reflect函数来获得它。

```
	float3 uvw = reflect(-surfaceWS.viewDirection, surfaceWS.normal);
```

接下来，在GI中添加一个镜面颜色，并将采样到的环境存储在GetGI函数中

```
struct GI {
	float3 diffuse;
	float3 specular;
	ShadowMask shadowMask;
};

…

GI GetGI (float2 lightMapUV, Surface surfaceWS) {
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
	gi.specular = SampleEnvironment(surfaceWS);
	…
}
```

现在我们可以在GetLighting函数中将正确的颜色传递给IndirectBRDF

```
	float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
```

最后，要使其工作，我们必须在CameraRenderer.DrawVisibleGeometry中设置每个对象的数据时，告诉Unity包括反射探针。

```
			perObjectData =
				PerObjectData.ReflectionProbes |
				PerObjectData.Lightmaps | PerObjectData.ShadowMask |
				PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
				PerObjectData.LightProbeProxyVolume |
				PerObjectData.OcclusionProbeProxyVolume
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/reflections/reflecting-environment.png)

表面现在反射环境。对于金属表面来说这是显而易见的，但其他表面也反射它。由于它只是天空盒，没有其他东西被反射，但我们稍后会研究这个问题。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/reflections/environment-probe.png)

### Rough Reflections

由于粗糙度会散射镜面反射，它不仅会减弱其强度，还会使其模糊，就好像它失焦了。Unity通过在较低的Mip级别中存储环境贴图的模糊版本来近似这种效果。要访问正确的Mip级别，我们需要知道感知粗糙度，所以让我们将其添加到BRDF结构中。

```
struct BRDF {
	…
	float perceptualRoughness;
};

…

BRDF GetBRDF (Surface surface, bool applyAlphaToDiffuse = false) {
	…

	brdf.perceptualRoughness =
		PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
	brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
	return brdf;
}
```

我们可以依靠PerceptualRoughnessToMipmapLevel函数来计算给定感知粗糙度的正确Mip级别。它在Core RP Library的ImageBasedLighting文件中定义。这需要我们向SampleEnvironment添加一个BRDF参数。

```
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

…

float3 SampleEnvironment (Surface surfaceWS, BRDF brdf) {
	float3 uvw = reflect(-surfaceWS.viewDirection, surfaceWS.normal);
	float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
	float4 environment = SAMPLE_TEXTURECUBE_LOD(
		unity_SpecCube0, samplerunity_SpecCube0, uvw, mip
	);
	return environment.rgb;
}
```

在GetGI函数中也添加所需的参数，并将其传递。

```
GI GetGI (float2 lightMapUV, Surface surfaceWS, BRDF brdf) {
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
	gi.specular = SampleEnvironment(surfaceWS, brdf);
	…
}
```

Finally, supply it in `LitPassFragment`.

```
	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/reflections/rough-reflections.png)

### Fresnel Reflection


所有表面的一个特性是，当以极端角度观察时，它们开始变得像完美的镜子，因为光线在它们上面反射时几乎没有受到影响。这现象被称为菲涅尔反射。实际上，它比这更复杂，因为它涉及到不同介质边界处的光波传输和反射，但我们只是使用Universal RP使用的同样近似，即假设为空气-固体边界。

我们使用Schlick的近似变种来处理菲涅尔反射。在理想情况下，它将镜面BRDF颜色替换为纯白色，但粗糙度可以阻止反射出现。我们通过将表面的光滑度和反射性相加来得到最终颜色，最大值为1。由于它是灰度的，我们可以只向BRDF添加一个值来表示它。

```
struct BRDF {
	…
	float fresnel;
};

…

BRDF GetBRDF (Surface surface, bool applyAlphaToDiffuse = false) {
	…
	
	brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);
	return brdf;
}
```

在IndirectBRDF函数中，我们通过将表面法线和视线方向的点积取1并将结果的四次方来确定菲涅尔效应的强度。在这里，我们可以使用Core RP Library中方便的Pow4函数。

```
	float fresnelStrength =
		Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));
	float3 reflection = specular * brdf.specular;
```

然后，根据强度在BRDF的镜面和菲涅尔颜色之间进行插值，然后使用结果来着色环境反射。

```
	float3 reflection =
		specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/reflections/fresnel-reflections.png)

### Fresnel Slider

菲涅尔反射主要在几何体的边缘添加反射效果。当环境贴图与物体背后的颜色匹配时，效果是微妙的，但如果情况不是这样，反射可能会显得奇怪和分散注意力。结构内部球体边缘处的明亮反射就是一个很好的例子。

降低光滑度可以去除菲涅尔反射，但也会使整个表面变得暗淡。此外，在某些情况下，菲涅尔近似不适用，例如在水下。因此，让我们添加一个滑块以在Lit着色器中进行缩放。

```
		_Metallic ("Metallic", Range(0, 1)) = 0
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
		_Fresnel ("Fresnel", Range(0, 1)) = 1
```

将其添加到LitInput的UnityPerMaterial缓冲区中，并创建一个用于获取菲涅尔效应的GetFresnel函数。

```
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	…
	UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

…

float GetFresnel (float2 baseUV) {
	return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Fresnel);
}
```

此外，为了保持它们同步，还需要为UnlitInput添加一个虚拟函数。

```
float GetFresnel (float2 baseUV) {
	return 0.0;
}
```

表面现在有一个用于其菲涅尔强度的字段。

```
struct Surface {
	…
	float smoothness;
	float fresnelStrength;
	float dither;
};
```

我们在LitPassFragment中将其设置为滑块属性的值。

```
	surface.smoothness = GetSmoothness(input.baseUV);
	surface.fresnelStrength = GetFresnel(input.baseUV);
```

最后，使用它来缩放我们在IndirectBRDF中使用的菲涅尔强度。

```
	float fresnelStrength = surface.fresnelStrength *
		Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));
```

https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/reflections/adjusting-fresnel-strength.mp4

### Reflection Probes

默认的环境立方体贴图只包含天空盒。要在场景中反射其他内容，我们必须通过GameObject / Light / Reflection Probe向其中添加一个反射探针。这些探针将场景渲染到一个立方体贴图中，从它们的位置开始。因此，只有靠近探针的表面的反射才会看起来更或多或少正确。因此，通常需要在场景中放置多个探针。它们具有Importance和Box Size属性，可用于控制每个探针影响的区域。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/reflections/reflection-probe-inspector.png)

![cube map](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/reflections/reflection-probe-map.png)

默认情况下，探针的类型设置为烘焙（Baked），这意味着它会在构建时渲染一次，并且立方体贴图会在构建中保存。您还可以将其设置为实时（Realtime），以保持立方体贴图与动态场景保持同步。它会像任何其他相机一样渲染，使用我们的渲染管线（RP），为立方体贴图的每个面渲染一次。因此，实时反射探针的性能开销较大。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/reflections/reflection-probes.png)

每个对象只使用一个环境探针，但场景中可以有多个探针。因此，您可能需要分割对象以获得可接受的反射效果。例如，用于构建结构的立方体理想情况下应该分割为内部和外部两部分，这样每个部分可以使用不同的反射探针。此外，**这意味着反射探针会破坏GPU批处理**。不幸的是，Mesh Ball根本无法使用反射探针，始终只使用天空盒。

MeshRenderer组件具有Anchor Override选项，可用于微调它们使用的探针，而无需担心包围盒的大小和位置。还有一个Reflection Probes选项，默认设置为Blend Probes。这个想法是Unity允许在两个最佳反射探针之间进行混合。然而，这种模式与SRP批处理器不兼容，因此Unity的其他渲染管线不支持它，我们也不支持。如果您感兴趣，如何混合探针可以在我的2018 SRP教程的Reflections教程中找到解释，但我预计一旦传统渲染管线被移除，这个功能将不复存在。我们将在未来研究其他反射技术。因此，唯一两种可用的模式是Off，它始终使用天空盒，和Simple，它选择最重要的探针。其他模式与Simple完全相同。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/reflections/simple-probes.png)

此外，反射探针还有一个选项，可以启用盒子投影模式。这应该改变反射的确定方式，以更好地匹配它们的有限影响区域，但这也不受SRP批处理器支持，因此我们也不支持它。

### Decoding Probes

最后，我们必须确保正确解释立方体贴图中的数据。它可以是HDR或LDR，并且其强度也可以调整。这些设置通过unity_SpecCube0_HDR向量提供，该向量位于UnityPerDraw缓冲区中的unity_ProbesOcclusion之后。

```
CBUFFER_START(UnityPerDraw)
	…

	float4 unity_ProbesOcclusion;
	
	float4 unity_SpecCube0_HDR;
	
	…
CBUFFER_END
```

我们通过在SampleEnvironment的末尾使用原始环境数据和设置作为参数调用DecodeHDREnvironment来获取正确的颜色。

```
float3 SampleEnvironment (Surface surfaceWS, BRDF brdf) {
	…
	return DecodeHDREnvironment(environment, unity_SpecCube0_HDR);
}
```