# **Shadow Masks** 

## Baking Shadows

使用光照贴图的优势在于我们不受最大阴影距离的限制。烘焙阴影不会被剔除，但也无法改变。理想情况下，我们可以在最大阴影距离内使用实时阴影，超出该距离则使用烘焙阴影。Unity的阴影蒙版混合光照模式使这成为可能。

### Distance Shadow Mask

让我们考虑前面教程中相同的场景，但将最大阴影距离减小，以便结构物内的一部分不会被投射阴影。这样可以清楚地看出实时阴影的范围。我们从仅有一个光源开始。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/baking-shadows/max-distance-11.png)

将混合光照模式切换为阴影蒙版（Shadowmask）模式。这将使光照数据失效，因此需要重新烘焙。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/baking-shadows/shadow-mask-lighting-mode.png)

有两种使用阴影蒙版混合光照的方法，可以通过“Quality”项目设置进行配置。我们将使用“Distance Shadowmask”模式。另一种模式称为“Shadowmask”，我们稍后会介绍。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/baking-shadows/shadow-mask-mode.png)


这两种阴影蒙版模式使用相同的烘焙光照数据。在两种情况下，光照贴图最终包含了间接光照，与“Baked Indirect”混合光照模式完全相同。不同之处在于现在还有一个烘焙的阴影蒙版贴图，您可以通过烘焙光照贴图预览窗口进行查看。

在Unity 2022中，您可以通过禁用自动生成光照，手动生成光照，并检查生成的纹理资源，来查看阴影蒙版贴图。

![shadow mask](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/baking-shadows/baked-shadow-mask.png)


阴影蒙版贴图包含了单个混合方向光的阴影衰减情况，代表了所有对全局光照产生贡献的静态物体投射的阴影。数据存储在红色通道中，因此该贴图为黑色和红色。

与烘焙的间接光照一样，烘焙的阴影在运行时无法更改。然而，这些阴影将始终保持有效，无论光的强度或颜色如何。但是光源不应旋转，否则其投射的阴影将变得不合理。此外，如果光源的间接光照已经烘焙，您不应该过多地改变光源的属性。例如，如果在关闭灯光后间接光照仍然存在，那将显然是错误的。如果光源发生了很大的变化，您可以将其间接光照乘数设置为零，这样它就不会被烘焙的间接光所影响。

### Detecting a Shadow Mask

要使用阴影蒙版，我们的渲染流程首先必须知道它的存在。由于这与阴影有关，这就是我们的“Shadows”类的工作。我们将使用着色器关键字来控制是否使用阴影蒙版。由于有两种模式，我们将引入另一个静态的关键字数组，即使目前只包含一个关键字：_SHADOW_MASK_DISTANCE。0

```
	static string[] shadowMaskKeywords = {
		"_SHADOW_MASK_DISTANCE"
	};
```

在代码中添加一个布尔字段来跟踪是否使用阴影蒙版。我们每帧重新评估这个值，所以在“Setup”中将其初始化为false。

```
	bool useShadowMask;

	public void Setup (…) {
		…
		useShadowMask = false;
	}
```

在“Render”的结尾处启用或禁用这个关键字。我们必须这样做，即使最终没有渲染任何实时阴影，因为阴影蒙版并不是实时的。

```
	public void Render () {
		…
		buffer.BeginSample(bufferName);
		SetKeywords(shadowMaskKeywords, useShadowMask ? 0 : -1);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}
```

要确定是否需要阴影蒙版，我们必须检查是否有使用它的光源。我们将在“ReserveDirectionalShadows”中进行此操作，当我们获得一个有效的投射阴影的光源时。

每个光源都包含关于其烘焙数据的信息。这些信息存储在一个“LightBakingOutput”结构中，可以通过“Light.bakingOutput”属性获取。如果我们遇到一个将其光照贴图烘焙类型设置为“Mixed”且混合光照模式设置为“Shadow Mask”的光源，那么我们正在使用阴影蒙版。

```
	public Vector3 ReserveDirectionalShadows (
		Light light, int visibleLightIndex
	) {
		if (…) {
			LightBakingOutput lightBaking = light.bakingOutput;
			if (
				lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
				lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
			) {
				useShadowMask = true;
			}

			…
		}
		return Vector3.zero;
	}
```

这将在需要时启用着色器关键字。在“Lit”着色器的“CustomLit”通道中，为它添加一个相应的多编译指令。

```
			#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
			#pragma multi_compile _ _SHADOW_MASK_DISTANCE
			#pragma multi_compile _ LIGHTMAP_ON
```

### Shadow Mask Data

在着色器方面，我们必须知道是否正在使用阴影蒙版，如果是的话，烘焙的阴影是什么样的。让我们在“Shadows”中添加一个“ShadowMask”结构体来跟踪这两者，其中包括一个布尔字段和一个浮点向量字段。将布尔字段命名为“distance”，以指示是否启用了距离阴影蒙版模式。然后将此结构体添加到全局的“ShadowData”结构体作为一个字段。

```
struct ShadowMask {
	bool distance;
	float4 shadows;
};

struct ShadowData {
	int cascadeIndex;
	float cascadeBlend;
	float strength;
	ShadowMask shadowMask;
};
```

在“GetShadowData”中，默认情况下将阴影蒙版初始化为未使用。

```
ShadowData GetShadowData (Surface surfaceWS) {
	ShadowData data;
	data.shadowMask.distance = false;
	data.shadowMask.shadows = 1.0;
	…
}
```

尽管阴影蒙版用于投射阴影，但它是场景的烘焙光照数据的一部分。因此，检索阴影蒙版是全局光照（GI）的责任。因此，也将阴影蒙版字段添加到全局光照（GI）的结构体中，并在“GetGI”中将其初始化为未使用状态。

```
struct GI {
	float3 diffuse;
	ShadowMask shadowMask;
};

…

GI GetGI (float2 lightMapUV, Surface surfaceWS) {
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
	gi.shadowMask.distance = false;
	gi.shadowMask.shadows = 1.0;
	return gi;
}
```

Unity通过“unity_ShadowMask”纹理和相应的采样器状态将阴影蒙版贴图提供给着色器使用。在全局光照（GI）中定义这些内容，以及其他光照贴图纹理和采样器状态。

```
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);
```

接着添加一个“SampleBakedShadows”函数，该函数使用光照贴图的UV坐标来采样阴影蒙版贴图。与常规光照贴图类似，这仅在定义了“LIGHTMAP_ON”时对光照贴图的几何体有意义。否则，没有烘焙的阴影，衰减始终为1。

```
float4 SampleBakedShadows (float2 lightMapUV) {
	#if defined(LIGHTMAP_ON)
		return SAMPLE_TEXTURE2D(
			unity_ShadowMask, samplerunity_ShadowMask, lightMapUV
		);
	#else
		return 1.0;
	#endif
}
```

现在，我们可以调整“GetGI”函数，以便在定义了“_SHADOW_MASK_DISTANCE”时启用距离阴影蒙版模式并采样烘焙的阴影。请注意，这会使“distance”布尔值成为编译时常量，因此它的使用不会导致动态分支。

```
GI GetGI (float2 lightMapUV, Surface surfaceWS) {
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
	gi.shadowMask.distance = false;
	gi.shadowMask.shadows = 1.0;

	#if defined(_SHADOW_MASK_DISTANCE)
		gi.shadowMask.distance = true;
		gi.shadowMask.shadows = SampleBakedShadows(lightMapUV);
	#endif
	return gi;
}
```

在循环遍历光源之前，在“GetLighting”中，由光照负责将阴影蒙版数据从全局光照（GI）复制到“ShadowData”中。在这一步，我们还可以通过将阴影蒙版数据直接返回为最终的光照颜来调试它。

```
float3 GetLighting (Surface surfaceWS, BRDF brdf, GI gi) {
	ShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;
	return gi.shadowMask.shadows.rgb;
	
	…
}
```

起初似乎没有起作用，因为一切都变成了白色。我们必须告诉Unity将相关数据发送到GPU，就像我们在前面的教程中为相机渲染器（CameraRenderer.DrawVisibleGeometry）中的光照贴图和探头所做的那样。在这种情况下，我们必须将“PerObjectData.ShadowMask”添加到每个对象的数据中。

```
			perObjectData =
				PerObjectData.Lightmaps | PerObjectData.ShadowMask |
				PerObjectData.LightProbe |
				PerObjectData.LightProbeProxyVolume
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/baking-shadows/sampled-shadow-mask.png)

### Occlusion Probes

我们可以看到阴影蒙版被正确地应用于光照贴图的物体上。我们还可以看到动态物体没有阴影蒙版数据，正如预期的那样。它们使用光照探头而不是光照贴图。然而，Unity还将阴影蒙版数据烘焙到光照探头中，将其称为遮挡探头。我们可以通过在“UnityInput”中的“UnityPerDraw”缓冲区中添加一个“unity_ProbesOcclusion”向量来访问这些数据。将它放置在世界变换参数和光照贴图UV变换向量之间。

```
	real4 unity_WorldTransformParams;

	float4 unity_ProbesOcclusion;

	float4 unity_LightmapST;
```

```
float4 SampleBakedShadows (float2 lightMapUV) {
	#if defined(LIGHTMAP_ON)
		…
	#else
		return unity_ProbesOcclusion;
	#endif
}
```

同样地，我们必须通过启用“PerObjectData.OcclusionProbe”标志，告诉Unity将这些数据发送到GPU。

```
	perObjectData =
				PerObjectData.Lightmaps | PerObjectData.ShadowMask |
				PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
				PerObjectData.LightProbeProxyVolume
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/baking-shadows/sampled-occlusion-probes.png)


光照探头的阴影蒙版未使用的通道被设置为白色，因此动态物体在完全受到光照时会变为白色，完全投射阴影时会变为青色，而不是红色和黑色。

虽然这足以让阴影蒙版通过探头工作，但它会破坏GPU实例化。遮挡数据可以自动实例化，但只有在定义了“SHADOWS_SHADOWMASK”时，UnityInstancing才会执行此操作。因此，在需要时在“Common”中包含UnityInstancing之前定义它。这是我们唯一需要明确检查“_SHADOW_MASK_DISTANCE”是否被定义的另一个地方。

```
#if defined(_SHADOW_MASK_DISTANCE)
	#define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
```

### LPPVs

光探头代理体积也可以使用阴影蒙版。同样，我们必须通过设置一个标志来启用它，这次是“PerObjectData.OcclusionProbeProxyVolume”。

```
			perObjectData =
				PerObjectData.Lightmaps | PerObjectData.ShadowMask |
				PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
				PerObjectData.LightProbeProxyVolume |
				PerObjectData.OcclusionProbeProxyVolume
```

检索光探头代理体积（LPPV）的遮挡数据的方法与检索其光照数据相同，只是我们必须调用“SampleProbeOcclusion”而不是“SampleProbeVolumeSH4”。它存储在相同的纹理中，并且需要相同的参数，唯一的例外是不再需要法线向量。在“SampleBakedShadows”中添加一个分支，用于处理这一点，同时添加一个表面参数来获取现在所需的世界位置。

```
float4 SampleBakedShadows (float2 lightMapUV, Surface surfaceWS) {
	#if defined(LIGHTMAP_ON)
		…
	#else
		if (unity_ProbeVolumeParams.x) {
			return SampleProbeOcclusion(
				TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
				surfaceWS.position, unity_ProbeVolumeWorldToObject,
				unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
				unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
			);
		}
		else {
			return unity_ProbesOcclusion;
		}
	#endif
}
```

在“GetGI”中调用函数时，添加新的表面参数。

```
	gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/baking-shadows/sampled-lppv-occlusion.png)

### Mesh Ball

如果我们的网格球使用了LPPV，则已经支持阴影蒙版。但是，当它自己插值光探头时，我们必须在“MeshBall.Update”中添加遮挡探头数据。这可以通过使用一个临时的Vector4数组作为“CalculateInterpolatedLightAndOcclusionProbes”的最后一个参数来实现，并通过“CopyProbeOcclusionArrayFrom”方法将其传递给属性块。

```
				var lightProbes = new SphericalHarmonicsL2[1023];
				var occlusionProbes = new Vector4[1023];
				LightProbes.CalculateInterpolatedLightAndOcclusionProbes(
					positions, lightProbes, occlusionProbes
				);
				block.CopySHCoefficientArraysFrom(lightProbes);
				block.CopyProbeOcclusionArrayFrom(occlusionProbes);
```

在验证阴影蒙版数据已正确发送到着色器后，我们可以从“GetLighting”中删除其调试可视化部分。

## Mixing Shadows

既然我们已经有了阴影蒙版，下一步就是在实时阴影不可用的情况下使用它，这种情况发生在一个片段超出了最大阴影距离。

### Use Baked when Available

混合烘焙和实时阴影会使得“GetDirectionalShadowAttenuation”的工作变得更加复杂。让我们首先通过将所有实时阴影采样代码隔离出来，在“Shadows”中创建一个名为“GetCascadedShadow”的新函数。

```
float GetCascadedShadow (
	DirectionalShadowData directional, ShadowData global, Surface surfaceWS
) {
	float3 normalBias = surfaceWS.normal *
		(directional.normalBias * _CascadeData[global.cascadeIndex].y);
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[directional.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	).xyz;
	float shadow = FilterDirectionalShadow(positionSTS);
	if (global.cascadeBlend < 1.0) {
		normalBias = surfaceWS.normal *
			(directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(
			_DirectionalShadowMatrices[directional.tileIndex + 1],
			float4(surfaceWS.position + normalBias, 1.0)
		).xyz;
		shadow = lerp(
			FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend
		);
	}
	return shadow;
}

float GetDirectionalShadowAttenuation (
	DirectionalShadowData directional, ShadowData global, Surface surfaceWS
) {
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif
	
	float shadow;
	if (directional.strength <= 0.0) {
		shadow = 1.0;
	}
	else {
		shadow = GetCascadedShadow(directional, global, surfaceWS);
		shadow = lerp(1.0, shadow, directional.strength);
	}
	return shadow;
}
```

接着，添加一个名为“GetBakedShadow”的新函数，该函数根据给定的阴影蒙版返回烘焙的阴影衰减值。如果蒙版的距离模式已启用，则我们需要其阴影向量的第一个分量；否则，没有可用的衰减值，结果为1。

```
float GetBakedShadow (ShadowMask mask) {
	float shadow = 1.0;
	if (mask.distance) {
		shadow = mask.shadows.r;
	}
	return shadow;
}
```

接下来，创建一个名为“MixBakedAndRealtimeShadows”的函数，它带有“ShadowData”、实时阴影和阴影强度参数。它将阴影简单地应用到强度上，但当存在距离阴影蒙版时则不同。在这种情况下，用烘焙阴影替换实时阴影。

```
float MixBakedAndRealtimeShadows (
	ShadowData global, float shadow, float strength
) {
	float baked = GetBakedShadow(global.shadowMask);
	if (global.shadowMask.distance) {
		shadow = baked;
	}
	return lerp(1.0, shadow, strength);
}
```

让“GetDirectionalShadowAttenuation”使用该函数，而不是自行应用强度。

```
		shadow = GetCascadedShadow(directional, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(global, shadow, directional.strength);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/mixing-shadows/faded-baked-shadows.png)

结果是我们现在始终使用阴影蒙版，所以我们可以看到它的工作原理。然而，烘焙阴影随距离而褪色，就像实时阴影一样。

### Transitioning to Baked

要根据深度从实时阴影过渡到烘焙阴影，我们必须基于全局阴影强度在它们之间进行插值。然而，我们还必须应用光的阴影强度，这是我们必须在插值之后完成的。因此，在“GetDirectionalShadowData”中，我们不能再立即将这两种强度结合起来。

```
	data.strength =
		_DirectionalLightShadowData[lightIndex].x; // * shadowData.strength;
```

在“MixBakedAndRealtimeShadows”中，根据全局强度在烘焙阴影和实时阴影之间进行插值，并在之后应用光的阴影强度。但是，当没有阴影蒙版时，将这两种强度仅应用于实时阴影，就像我们之前所做的一样。

```
float MixBakedAndRealtimeShadows (
	ShadowData global, float shadow, float strength
) {
	float baked = GetBakedShadow(global.shadowMask);
	if (global.shadowMask.distance) {
		shadow = lerp(baked, shadow, global.strength);
		return lerp(1.0, shadow, strength);
	}
	return lerp(1.0, shadow, strength * global.strength);
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/mixing-shadows/mixed-shadows.png)

结果是由动态物体投射的阴影如常褪色，而由静态物体投射的阴影则过渡到阴影蒙版。

### Only Baked Shadows

目前，我们的方法仅在存在实时阴影需要渲染时才有效。如果没有实时阴影，阴影蒙版也会消失。可以通过将场景视图缩小，直到所有内容都在最大阴影距离之外，来验证这一点。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/mixing-shadows/neither-realtime-nor-baked.png)

我们必须支持存在阴影蒙版但没有实时阴影的情况。我们可以首先创建一个具有强度参数的“GetBakedShadow”函数变体，这样我们就可以方便地获取一个经过强度调制的烘焙阴影。

```
float GetBakedShadow (ShadowMask mask, float strength) {
	if (mask.distance) {
		return lerp(1.0, GetBakedShadow(mask), strength);
	}
	return 1.0;
}
```

接下来，在“GetDirectionalShadowAttenuation”中检查组合后的强度是否为零或小于零。如果是这样，不要始终返回1，而是仅返回调制后的烘焙阴影，仍然跳过实时阴影采样。

```
	if (directional.strength * global.strength <= 0.0) {
		shadow = GetBakedShadow(global.shadowMask, directional.strength);
	}
```

此外，我们还必须更改“Shadows.ReserveDirectionalShadows”，以便它不会立即跳过那些最终没有实时阴影投射者的光源。相反，首先确定光源是否使用阴影蒙版。然后检查是否没有实时阴影投射者，在这种情况下，只有阴影强度是相关的。

```
		if (
			shadowedDirLightCount < maxShadowedDirLightCount &&
			light.shadows != LightShadows.None && light.shadowStrength > 0f //&&
			//cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
		) {
			LightBakingOutput lightBaking = light.bakingOutput;
			if (
				lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
				lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
			) {
				useShadowMask = true;
			}

			if (!cullingResults.GetShadowCasterBounds(
				visibleLightIndex, out Bounds b
			)) {
				return new Vector3(light.shadowStrength, 0f, 0f);
			}

			…
		}
```

但是，当阴影强度大于零时，着色器将会采样阴影贴图，即使这是不正确的。在这种情况下，我们可以通过取反阴影强度来使其工作。

```
				return new Vector3(-light.shadowStrength, 0f, 0f);
```

在“GetDirectionalShadowAttenuation”中，在跳过实时阴影时，将绝对强度传递给“GetBakedShadow”。这样，在没有实时阴影投射者或超出最大阴影距离时都可以正常工作。

```
shadow = GetBakedShadow(global.shadowMask, abs(directional.strength));
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/mixing-shadows/baked-only.png)

### Always use the Shadow Mask

还有另一种阴影蒙版模式，称为“Shadowmask”。它与距离模式完全相同，只是Unity会忽略使用阴影蒙版的光源的静态投射阴影者。

![project settings](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/mixing-shadows/shadow-mask-mode-always.png)

![scene](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/mixing-shadows/no-static-shadows.png)


这个想法是，由于阴影蒙版在各个地方都可用，我们也可以在所有地方使用它进行静态阴影。这意味着较少的实时阴影，从而加快渲染速度，但代价是近距离处的静态阴影质量较低。

为了支持这种模式，在“Shadows”中的阴影蒙版关键字数组中添加一个名为“_SHADOW_MASK_ALWAYS”的关键字作为第一个元素。我们可以通过检查“QualitySettings.shadowmaskMode”属性来确定在渲染时应该启用哪个关键字。

```
	static string[] shadowMaskKeywords = {
		"_SHADOW_MASK_ALWAYS",
		"_SHADOW_MASK_DISTANCE"
	};
	
	…
	
	public void Render () {
		…
		buffer.BeginSample(bufferName);
		SetKeywords(shadowMaskKeywords, useShadowMask ?
			QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 :
			-1
		);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}
```

将这个关键字添加到我们着色器中的多编译指令中。

```
	#pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
```

在“Common”中，在决定是否定义“SHADOWS_SHADOWMASK”时，也要检查这个关键字。

```
#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
	#define SHADOWS_SHADOWMASK
#endif
```

为“ShadowMask”结构体添加一个单独的布尔字段，用于指示是否始终使用阴影蒙版。

```
struct ShadowMask {
	bool always;
	bool distance;
	float4 shadows;
};

…

ShadowData GetShadowData (Surface surfaceWS) {
	ShadowData data;
	data.shadowMask.always = false;
	…
}
```

然后在“GetGI”中，在适当的情况下设置它，以及它的阴影数据。

```
GI GetGI (float2 lightMapUV, Surface surfaceWS) {
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
	gi.shadowMask.always = false;
	gi.shadowMask.distance = false;
	gi.shadowMask.shadows = 1.0;

	#if defined(_SHADOW_MASK_ALWAYS)
		gi.shadowMask.always = true;
		gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
	#elif defined(_SHADOW_MASK_DISTANCE)
		gi.shadowMask.distance = true;
		gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
	#endif
	return gi;
}
```

在两种模式中的任何一种使用时，都应该在“GetBakedShadow”的两个版本中选择阴影蒙版。

```
float GetBakedShadow (ShadowMask mask) {
	float shadow = 1.0;
	if (mask.always || mask.distance) {
		shadow = mask.shadows.r;
	}
	return shadow;
}

float GetBakedShadow (ShadowMask mask, float strength) {
	if (mask.always || mask.distance) {
		return lerp(1.0, GetBakedShadow(mask), strength);
	}
	return 1.0;
}
```

最后，当阴影蒙版始终处于活动状态时，“MixBakedAndRealtimeShadows”必须采用不同的方法。首先，实时阴影必须通过全局强度进行调制，以便根据深度进行褪色。然后，通过取烘焙阴影和实时阴影的最小值，将它们合并起来。之后，光的阴影强度被应用于合并的阴影。

```
float MixBakedAndRealtimeShadows (
	ShadowData global, float shadow, float strength
) {
	float baked = GetBakedShadow(global.shadowMask);
	if (global.shadowMask.always) {
		shadow = lerp(1.0, shadow, global.strength);
		shadow = min(baked, shadow);
		return lerp(1.0, shadow, strength);
	}
	if (global.shadowMask.distance) {
		shadow = lerp(baked, shadow, global.strength);
		return lerp(1.0, shadow, strength);
	}
	return lerp(1.0, shadow, strength * global.strength);
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/mixing-shadows/baked-static-shadows.png)

## Multiple Lights

由于阴影蒙版贴图有四个通道，因此最多可以支持四个混合光源。在烘焙过程中，最重要的光源将使用红色通道，第二个光源将使用绿色通道，依此类推。让我们通过复制我们的单一定向光源，将其旋转一点，并降低其强度，以便新的光源使用绿色通道来尝试一下这个方法。

Unity将把除前四个之外的所有混合模式光源转换为完全烘焙的光源。这是基于所有光源都是定向光源的假设，这是我们目前支持的唯一光源类型。其他光源类型有有限的影响区域，这可能使得可以在一个通道上使用多个光源。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/multiple-lights/lights-sharing-baked-shadows.png)

第二个光源的实时阴影按预期工作，但它在烘焙阴影时使用了第一个光源的阴影蒙版，这显然是错误的。在使用始终阴影蒙版模式时，这是最容易看到的。

### Shadow Mask Channels

检查烘焙的阴影蒙版贴图会显示阴影被正确烘焙。只受第一个光源照亮的区域为红色，只受第二个光源照亮的区域为绿色，受两个光源照亮的区域为黄色。这在最多四个光源的情况下有效，尽管第四个光源在预览中不会显示，因为不显示阿尔法通道。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/multiple-lights/baked-shadows-two-lights.png)

两个光源都使用相同的烘焙阴影，因为我们始终使用红色通道。要使其工作，我们必须将光源的通道索引发送到GPU。我们不能依赖光源的顺序，因为它在运行时可能会变化，光源可能会被更改甚至禁用。

我们可以通过“Shadows.ReserveDirectionalShadows”中的“LightBakingOutput.occlusionMaskChannel”字段来检索光源的掩码通道索引。由于我们要向GPU发送4D向量，我们可以将它存储在我们返回的向量的第四个通道中，将返回类型更改为Vector4。当光源不使用阴影蒙版时，我们通过将其索引设置为-1来指示。

```
	public Vector4 ReserveDirectionalShadows (
		Light light, int visibleLightIndex
	) {
		if (
			shadowedDirLightCount < maxShadowedDirLightCount &&
			light.shadows != LightShadows.None && light.shadowStrength > 0f
		) {
			float maskChannel = -1;
			LightBakingOutput lightBaking = light.bakingOutput;
			if (
				lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
				lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
			) {
				useShadowMask = true;
				maskChannel = lightBaking.occlusionMaskChannel;
			}

			if (!cullingResults.GetShadowCasterBounds(
				visibleLightIndex, out Bounds b
			)) {
				return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
			}

			shadowedDirectionalLights[shadowedDirLightCount] =
				new ShadowedDirectionalLight {
					visibleLightIndex = visibleLightIndex,
					slopeScaleBias = light.shadowBias,
					nearPlaneOffset = light.shadowNearPlane
				};
			return new Vector4(
				light.shadowStrength,
				settings.directional.cascadeCount * shadowedDirLightCount++,
				light.shadowNormalBias, maskChannel
			);
		}
		return new Vector4(0f, 0f, 0f, -1f);
	}
```

### Selecting the Appropriate Channel

在着色器代码中，在“Shadows”中定义的“DirectionalShadowData”结构体中添加阴影蒙版通道作为附加的整数字段。

```
struct DirectionalShadowData {
	float strength;
	int tileIndex;
	float normalBias;
	int shadowMaskChannel;
};
```

在“GetDirectionalShadowData”中，全局光照（GI）必须设置阴影蒙版通道。

```
DirectionalShadowData GetDirectionalShadowData (
	int lightIndex, ShadowData shadowData
) {
	…
	data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;
	return data;
}
```

在“GetBakedShadow”的两个版本中都添加一个通道参数，并使用它来返回适当的阴影蒙版数据。但只有在光源使用阴影蒙版时，也就是通道至少为零时，才执行此操作。

```
float GetBakedShadow (ShadowMask mask, int channel) {
	float shadow = 1.0;
	if (mask.always || mask.distance) {
		if (channel >= 0) {
			shadow = mask.shadows[channel];
		}
	}
	return shadow;
}

float GetBakedShadow (ShadowMask mask, int channel, float strength) {
	if (mask.always || mask.distance) {
		return lerp(1.0, GetBakedShadow(mask, channel), strength);
	}
	return 1.0;
}
```

是的，但是着色器编译器会为我们处理这个。它将使用通道来索引一个静态向量缓冲区，其中适当的分量设置为1，然后将使用这些分量与阴影蒙版进行点乘来过滤它。我们也可以将点乘结果发送到GPU，以跳过查找步骤，但这需要发送一个额外的向量数组，无论如何都需要进行索引。

调整“MixBakedAndRealtimeShadows”以便它传递所需的阴影蒙版通道。

```
float MixBakedAndRealtimeShadows (
	ShadowData global, float shadow, int shadowMaskChannel, float strength
) {
	float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);
	…
}
```

最后，在“GetDirectionalShadowAttenuation”中添加所需的通道参数。

```
float GetDirectionalShadowAttenuation (
	DirectionalShadowData directional, ShadowData global, Surface surfaceWS
) {
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif
	
	float shadow;
	if (directional.strength * global.strength <= 0.0) {
		shadow = GetBakedShadow(
			global.shadowMask, directional.shadowMaskChannel,
			abs(directional.strength)
		);
	}
	else {
		shadow = GetCascadedShadow(directional, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(
			global, shadow, directional.shadowMaskChannel, directional.strength
		);
	}
	return shadow;
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/shadow-masks/multiple-lights/two-lights-two-channels.png)