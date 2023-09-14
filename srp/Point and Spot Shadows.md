# **Point and Spot Shadows**

[TOC]



## Spot Light Shadows

我们将首先支持聚光灯的实时阴影。我们将使用与定向光相同的方法，但会有一些变化。我们还将尽可能简化支持，使用均匀分块的阴影图集，并按Unity提供的顺序填充具有阴影的光源。

### Shadow Mixing

第一步是使烘焙阴影和实时阴影能够混合使用。在Shadows中调整GetOtherShadowAttenuation，使其表现类似于GetDirectionalShadowAttenuation，但它使用其他阴影数据，并依赖于一个新的GetOtherShadow函数。新函数最初返回1，因为其他光源尚未具有实时阴影。

```
float GetOtherShadow (
	OtherShadowData other, ShadowData global, Surface surfaceWS
) {
	return 1.0;
}

float GetOtherShadowAttenuation (
	OtherShadowData other, ShadowData global, Surface surfaceWS
) {
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif
	
	float shadow;
	if (other.strength * global.strength <= 0.0) {
		shadow = GetBakedShadow(
			global.shadowMask, other.shadowMaskChannel, abs(other.strength)
		);
	}
	else {
		shadow = GetOtherShadow(other, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(
			global, shadow, other.shadowMaskChannel, other.strength
		);
	}
	return shadow;
}
```


全局强度用于确定我们是否可以跳过对实时阴影的采样，要么是因为我们超出了阴影距离，要么是因为超出了最大级联球。然而，级联仅适用于定向阴影，对于其他光源来说并没有意义，因为它们具有固定的位置，因此它们的阴影图不随视图移动。尽管如此，以相同的方式淡化所有阴影是一个好主意，否则我们可能会出现屏幕上某些区域没有定向阴影但具有其他阴影的情况。因此，我们将为所有光源使用相同的全局阴影强度。

我们需要处理的一个特殊情况是当没有定向阴影存在，但我们确实有其他阴影时。在这种情况下，没有级联，因此它们不应影响全局阴影强度。而且我们仍然需要阴影距离淡化值。因此，让我们将设置级联计数和距离淡化的代码从Shadows.RenderDirectionShadows移动到Shadows.Render，并在适当的情况下将级联计数设置为零。

```
	public void Render () {
		…
		buffer.SetGlobalInt(
			cascadeCountId,
			shadowedDirLightCount > 0 ? settings.directional.cascadeCount : 0
		);
		float f = 1f - settings.directional.cascadeFade;
		buffer.SetGlobalVector(
			shadowDistanceFadeId, new Vector4(
				1f / settings.maxDistance, 1f / settings.distanceFade,
				1f / (1f - f * f)
			)
		);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}

	void RenderDirectionalShadows () {
		…

		//buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
		buffer.SetGlobalVectorArray(
			cascadeCullingSpheresId, cascadeCullingSpheres
		);
		buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
		buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
		//float f = 1f - settings.directional.cascadeFade;
		//buffer.SetGlobalVector(
		//	shadowDistanceFadeId, new Vector4(
		//		1f / settings.maxDistance, 1f / settings.distanceFade,
		//		1f / (1f - f * f)
		//	)
		//);
		…
	}
```

然后，我们必须确保在GetShadowData中级联循环之后不会错误地将全局强度设置为零。

```
	if (i == _CascadeCount && _CascadeCount > 0) {
		data.strength = 0.0;
	}
```

### Other Realtime Shadows

定向光源具有自己的阴影图集。对于所有其他具有实时阴影的光源，我们将使用一个单独的阴影图集，并对它们进行独立计数。让我们最多使用十六个具有实时阴影的其他光源。

```
	const int maxShadowedDirLightCount = 4, maxShadowedOtherLightCount = 16;
	const int maxCascades = 4;

	…

	int shadowedDirLightCount, shadowedOtherLightCount;
	
	…
	
	public void Setup (…) {
		…
		shadowedDirLightCount = shadowedOtherLightCount = 0;
		useShadowMask = false;
	}
```

这意味着我们可能会遇到启用阴影但无法适应阴影图集的光源。哪些光源将不会有阴影取决于它们在可见光源列表中的位置。我们不会为没在列表中的的光源保留阴影。但是，如果它们具有烘焙阴影，我们仍然可以允许这些光源。为了实现这一点，首先重构ReserveOtherShadows，使其在光源没有阴影时立即返回。否则，它会检查阴影遮罩通道（默认使用-1），然后始终返回阴影强度和通道。

```
	public Vector4 ReserveOtherShadows (Light light, int visibleLightIndex) {
		if (light.shadows == LightShadows.None || light.shadowStrength <= 0f) {
			return new Vector4(0f, 0f, 0f, -1f);
		}

		float maskChannel = -1f;
		//if (light.shadows != LightShadows.None && light.shadowStrength > 0f) {
		LightBakingOutput lightBaking = light.bakingOutput;
		if (
			lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
			lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
		) {
			useShadowMask = true;
			maskChannel = lightBaking.occlusionMaskChannel;
		}
		return new Vector4(
			light.shadowStrength, 0f, 0f,
			maskChannel
		);
			//}
		//}
		//return new Vector4(0f, 0f, 0f, -1f);
	}
```

然后，在返回之前，检查是否增加光源计数会超过最大值，或者是否没有阴影需要渲染。如果是这样，返回一个负的阴影强度和遮罩通道，以便在适当的情况下使用烘焙阴影。否则，继续增加光源计数并设置图块索引。

```
		if (
			shadowedOtherLightCount >= maxShadowedOtherLightCount ||
			!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
		) {
			return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
		}

		return new Vector4(
			light.shadowStrength, shadowedOtherLightCount++, 0f,
			maskChannel
		);
```

### Two Atlases

由于定向阴影和其他阴影是分开保存的，我们可以对它们进行不同的配置。在ShadowSettings中添加一个新的配置结构和字段，用于其他阴影，仅包含图集大小和滤波器，因为级联不适用于它们。

```
[System.Serializable]
	public struct Other {

		public MapSize atlasSize;

		public FilterMode filter;
	}

	public Other other = new Other {
		atlasSize = MapSize._1024,
		filter = FilterMode.PCF2x2
	};
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/spot-light-shadows/other-shadows-settings.png)

```
		#pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
```

And add a corresponding keyword array to `**Shadows**`.

```
	static string[] otherFilterKeywords = {
		"_OTHER_PCF3",
		"_OTHER_PCF5",
		"_OTHER_PCF7",
	};
```

我们还需要跟踪其他阴影图集和矩阵的着色器属性标识符，以及一个用于保存矩阵的数组。

```
	static int
		dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
		dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
		otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
		otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
		…;
		
	…
		
	static Matrix4x4[]
		dirShadowMatrices = new Matrix4x4[maxShadowedDirLightCount * maxCascades],
		otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];
```

我们已经将定向阴影图集的大小发送到GPU，使用一个向量的XY分量。现在，我们还需要发送其他阴影图集的大小，可以将其放在同一个向量的ZW分量中。将其提升为一个字段，并将设置全局向量的代码从RenderDirectionalShadows移动到Render中。然后，RenderDirectionalShadows只需要分配给字段的XY分量。

```
	Vector4 atlasSizes;
	
	…
	
	public void Render () {
		…
		buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}
	
	void RenderDirectionalShadows () {
		int atlasSize = (int)settings.directional.atlasSize;
		atlasSizes.x = atlasSize;
		atlasSizes.y = 1f / atlasSize;
		…
		//buffer.SetGlobalVector(
		//	shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize)
		//);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}
```

在完成这些步骤之后，复制RenderDirectionalShadows并将其重命名为RenderOtherShadows。修改它，以便使用正确的设置、图集、矩阵，并设置正确的大小分量。然后从中删除级联和剔除球代码。还删除对RenderDirectionalShadows的调用，但保留循环。

```
	void RenderOtherShadows () {
		int atlasSize = (int)settings.other.atlasSize;
		atlasSizes.z = atlasSize;
		atlasSizes.w = 1f / atlasSize;
		buffer.GetTemporaryRT(
			otherShadowAtlasId, atlasSize, atlasSize,
			32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
		);
		buffer.SetRenderTarget(
			otherShadowAtlasId,
			RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
		);
		buffer.ClearRenderTarget(true, false, Color.clear);
		buffer.BeginSample(bufferName);
		ExecuteBuffer();

		int tiles = shadowedOtherLightCount;
		int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
		int tileSize = atlasSize / split;

		for (int i = 0; i < shadowedOtherLightCount; i++) {
			//RenderDirectionalShadows(i, split, tileSize);
		}

		//buffer.SetGlobalVectorArray(
		//	cascadeCullingSpheresId, cascadeCullingSpheres
		//);
		//buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
		buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
		SetKeywords(
			otherFilterKeywords, (int)settings.other.filter - 1
		);
		//SetKeywords(
		//	cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1
		//);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}
```

现在，根据需要，我们可以在RenderShadows中同时渲染定向阴影和其他阴影。如果没有其他阴影，那么我们需要一个虚拟纹理，就像定向阴影一样。我们可以简单地使用定向阴影图集作为虚拟纹理。

```
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
		if (shadowedOtherLightCount > 0) {
			RenderOtherShadows();
		}
		else {
			buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
		}
		
		…
	}
```

And release the other shadow atlas in `Cleanup`, in this case only if we did get one.

```
	public void Cleanup () {
		buffer.ReleaseTemporaryRT(dirShadowAtlasId);
		if (shadowedOtherLightCount > 0) {
			buffer.ReleaseTemporaryRT(otherShadowAtlasId);
		}
		ExecuteBuffer();
	}
```

### Rendering Spot Shadows

为了渲染聚光灯的阴影，我们需要知道其可见光源索引、坡度缩放偏差和法线偏差。因此，创建一个ShadowedOtherLight结构体，其中包含这些字段，并添加一个数组字段，类似于我们跟踪定向阴影数据的方式。

```
	struct ShadowedOtherLight {
		public int visibleLightIndex;
		public float slopeScaleBias;
		public float normalBias;
	}

	ShadowedOtherLight[] shadowedOtherLights =
		new ShadowedOtherLight[maxShadowedOtherLightCount];
```

Copy the relevant data at the end of `ReserveOtherShadows`, before returning.

```
	public Vector4 ReserveOtherShadows (Light light, int visibleLightIndex) {
		…

		shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight {
			visibleLightIndex = visibleLightIndex,
			slopeScaleBias = light.shadowBias,
			normalBias = light.shadowNormalBias
		};

		return new Vector4(
			light.shadowStrength, shadowedOtherLightCount++, 0f,
			maskChannel
		);
	}
```

然而，在这一点上，我们应该意识到，我们不能保证在Lighting中将正确的光源索引发送给ReserveOtherShadows，因为它为其他光源传递了自己的索引。当存在有阴影的定向光源时，索引将是错误的。我们通过为光源设置方法添加一个参数，用于正确的可见光源索引，并在保留阴影时使用它来解决这个问题。为了保持一致性，让我们也为定向光源执行此操作。

```
	void SetupDirectionalLight (
		int index, int visibleIndex, ref VisibleLight visibleLight
	) {
		…
		dirLightShadowData[index] =
			shadows.ReserveDirectionalShadows(visibleLight.light, visibleIndex);
	}

	void SetupPointLight (
		int index, int visibleIndex, ref VisibleLight visibleLight
	) {
		…
		otherLightShadowData[index] =
			shadows.ReserveOtherShadows(light, visibleIndex);
	}

	void SetupSpotLight (
		int index, int visibleIndex, ref VisibleLight visibleLight
	) {
		…
		otherLightShadowData[index] =
			shadows.ReserveOtherShadows(light, visibleIndex);
	}
```

Adjust `SetupLights` so it passes the visible light index to the setup methods.

```
			switch (visibleLight.lightType) {
				case LightType.Directional:
					if (dirLightCount < maxDirLightCount) {
						SetupDirectionalLight(
							dirLightCount++, i, ref visibleLight
						);
					}
					break;
				case LightType.Point:
					if (otherLightCount < maxOtherLightCount) {
						newIndex = otherLightCount;
						SetupPointLight(otherLightCount++, i, ref visibleLight);
					}
					break;
				case LightType.Spot:
					if (otherLightCount < maxOtherLightCount) {
						newIndex = otherLightCount;
						SetupSpotLight(otherLightCount++, i, ref visibleLight);
					}
					break;
			}
```

回到Shadows，创建一个RenderSpotShadows方法，其与具有参数的RenderDirectionalShadows方法执行相同的操作，除了它不会循环多个图块，没有级联，也没有剔除因子。在这种情况下，我们可以使用CullingResults.ComputeSpotShadowMatricesAndCullingPrimitives，其工作方式类似于ComputeDirectionalShadowMatricesAndCullingPrimitives，但它只有可见光源索引、矩阵和拆分数据作为参数。在Unity 2022中，我们还必须使用BatchCullingProjectionType.Perspective而不是正交。

```
	void RenderSpotShadows (int index, int split, int tileSize) {
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
```

Invoke this method inside the loop of `RenderOtherShadows`.

```
	for (int i = 0; i < shadowedOtherLightCount; i++) {
			RenderSpotShadows(i, split, tileSize);
		}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/spot-light-shadows/spot-shadow-map.png)

### No Pancaking

现在，阴影会为聚光灯渲染，使用与用于定向阴影相同的ShadowCaster通道。这在大多数情况下运行良好，但阴影平面仅适用于正交阴影投影，用于假定定向光源无限远的情况。而对于聚光灯，它们具有位置，因此阴影投射器可能部分位于光源位置之后。在这种情况下，由于我们使用透视投影，将顶点限制在近平面上会严重扭曲这些阴影。因此，在不适当使用阴影平面时，我们应该关闭限制。

我们可以通过全局着色器属性告诉着色器是否启用了阴影平面，我们将其命名为_ShadowPancaking。在Shadows中跟踪其标识符。

```
	static int
		…
		shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
		shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");
```

Set it to 1 before rendering shadows `RenderDirectionalShadows`.

```
		buffer.ClearRenderTarget(true, false, Color.clear);
		buffer.SetGlobalFloat(shadowPancakingId, 1f);
		buffer.BeginSample(bufferName);
```

And to zero in `RenderOtherShadows`.

```
		buffer.ClearRenderTarget(true, false, Color.clear);
		buffer.SetGlobalFloat(shadowPancakingId, 0f);
		buffer.BeginSample(bufferName);
```

Then add it to the *ShadowCaster* pass of our *Lit* shader as a boolean, using it to only clamp when appropriate.

```
bool _ShadowPancaking;

Varyings ShadowCasterPassVertex (Attributes input) {
	…

	if (_ShadowPancaking) {
		#if UNITY_REVERSED_Z
			output.positionCS.z = min(
				output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE
			);
		#else
			output.positionCS.z = max(
				output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE
			);
		#endif
	}

	output.baseUV = TransformBaseUV(input.baseUV);
	return output;
}
```

### Sampling Spot Shadows

要对其他阴影进行采样，我们必须调整Shadows。首先，定义其他过滤器和最大阴影其他光源计数的宏。然后添加其他阴影图集和其他阴影矩阵数组。

```
#if defined(_OTHER_PCF3)
	#define OTHER_FILTER_SAMPLES 4
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
	#define OTHER_FILTER_SAMPLES 9
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
	#define OTHER_FILTER_SAMPLES 16
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	…
	float4x4 _DirectionalShadowMatrices
		[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	…
CBUFFER_END
```

复制SampleDirectionalShadowAtlas和FilterDirectionalShadow，并将它们重命名并调整它们以便为其他阴影工作。请注意，对于这个版本，我们需要使用图集大小向量的另一对分量。

```
float SampleOtherShadowAtlas (float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(
		_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}

float FilterOtherShadow (float3 positionSTS) {
	#if defined(OTHER_FILTER_SETUP)
		real weights[OTHER_FILTER_SAMPLES];
		real2 positions[OTHER_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.wwzz;
		OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for (int i = 0; i < OTHER_FILTER_SAMPLES; i++) {
			shadow += weights[i] * SampleOtherShadowAtlas(
				float3(positions[i].xy, positionSTS.z)
			);
		}
		return shadow;
	#else
		return SampleOtherShadowAtlas(positionSTS);
	#endif
}
```

The `**OtherShadowData**` struct now also needs a tile index.

```
struct OtherShadowData {
	float strength;
	int tileIndex;
	int shadowMaskChannel;
};
```

Which is set by `GetOtherShadowData` in *Light*.

```
OtherShadowData GetOtherShadowData (int lightIndex) {
	OtherShadowData data;
	data.strength = _OtherLightShadowData[lightIndex].x;
	data.tileIndex = _OtherLightShadowData[lightIndex].y;
	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
	return data;
}
```

现在，我们可以在GetOtherShadow中采样阴影图，而不是始终返回1。它的工作方式类似于GetCascadedShadow，但没有第二个级联要混合，并且它是透视投影，所以我们必须将变换后的位置的XYZ分量除以其W分量。此外，我们目前还没有功能性的法线偏差，所以我们将其乘以零。

```
float GetOtherShadow (
	OtherShadowData other, ShadowData global, Surface surfaceWS
) {
	float3 normalBias = surfaceWS.interpolatedNormal * 0.0;
	float4 positionSTS = mul(
		_OtherShadowMatrices[other.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	);
	return FilterOtherShadow(positionSTS.xyz / positionSTS.w);
}
```

![with](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/spot-light-shadows/with-shadows.png)

![without](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/spot-light-shadows/without-shadows.png)

### Normal Bias

聚光灯和定向光源一样也会受到阴影痤疮（shadow acne）的影响。但由于透视投影，纹素大小不是恒定的，因此阴影痤疮也不是恒定的。离光源越远，阴影痤疮越明显。![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/spot-light-shadows/variable-texel-size.png)

纹素大小会随着距离光平面的增加而线性增加，光平面是将世界分为在光源前面和后面的平面。因此，我们可以在距离1处计算纹素大小，从而计算法线偏差，并将其发送到着色器，其中我们将将其缩放到适当的大小。

在世界空间中，距离光平面1的位置处，阴影块的大小是聚光角的一半的正切的两倍，单位是弧度。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/spot-light-shadows/tile-size-diagram.png)

这与透视投影相匹配，因此距离1处的世界空间纹素大小等于2除以投影缩放，我们可以使用其矩阵的左上值。我们可以使用这个值来计算法线偏差，方式与我们为定向光源所做的方式相同，只是我们可以立即将光的法线偏差合并进去，因为没有多个级联。在Shadows.RenderSpotShadows中设置阴影矩阵之前执行此操作。

```
		float texelSize = 2f / (tileSize * projectionMatrix.m00);
		float filterSize = texelSize * ((float)settings.other.filter + 1f);
		float bias = light.normalBias * filterSize * 1.4142136f;
		otherShadowMatrices[index] = ConvertToAtlasMatrix(
			projectionMatrix * viewMatrix,
			SetTileViewport(index, split, tileSize), tileScale
		);
```

现在我们需要将偏差发送到着色器。稍后我们还需要为每个图块发送更多的数据，因此让我们添加一个 _OtherShadowTiles 矢量数组着色器属性。在Shadows中为其添加一个标识符和数组，并在RenderOtherShadows中与矩阵一起设置它。

```
	static int
		…
		otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
		otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles"),
		…;

	static Vector4[]
		cascadeCullingSpheres = new Vector4[maxCascades],
		cascadeData = new Vector4[maxCascades],
		otherShadowTiles = new Vector4[maxShadowedOtherLightCount];
	
	…
	
	void RenderOtherShadows () {
		…

		buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
		buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
		…
	}
```

创建一个新的SetOtherTileData方法，带有一个索引和偏差。让它将偏差放在向量的最后一个分量中，然后将其存储在图块数据数组中。

```
	void SetOtherTileData (int index, float bias) {
		Vector4 data = Vector4.zero;
		data.w = bias;
		otherShadowTiles[index] = data;
	}
```

Invoke it in `RenderSpotShadows` once we have the bias.

```
		float bias = light.normalBias * filterSize * 1.4142136f;
		SetOtherTileData(index, bias);
```

然后将其他阴影图块数组添加到阴影缓冲区，并在Shadows中使用它来缩放法线偏差。

```
CBUFFER_START(_CustomShadows)
	…
	float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _ShadowAtlasSize;
	float4 _ShadowDistanceFade;
CBUFFER_END

…

float GetOtherShadow (
	OtherShadowData other, ShadowData global, Surface surfaceWS
) {
	float4 tileData = _OtherShadowTiles[other.tileIndex];
	float3 normalBias = surfaceWS.interpolatedNormal * tileData.w;
	…
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/spot-light-shadows/normal-bias-constant.png)

在这一点上，我们有一个仅在固定距离上正确的法线偏差。要根据距离从光平面缩放它，我们需要知道世界空间中的光位置和聚光方向，因此将它们添加到OtherShadowData中。

```
struct OtherShadowData {
	float strength;
	int tileIndex;
	int shadowMaskChannel;
	float3 lightPositionWS;
	float3 spotDirectionWS;
};
```

让Light将这些值复制到OtherShadowData中。由于这些值来自光本身而不是阴影数据，在GetOtherShadowData中将它们设置为零，并在GetOtherLight中复制它们。

```
OtherShadowData GetOtherShadowData (int lightIndex) {
	…
	data.lightPositionWS = 0.0;
	data.spotDirectionWS = 0.0;
	return data;
}

Light GetOtherLight (int index, Surface surfaceWS, ShadowData shadowData) {
	Light light;
	light.color = _OtherLightColors[index].rgb;
	float3 position = _OtherLightPositions[index].xyz;
	float3 ray = position - surfaceWS.position;
	…
	float3 spotDirection = _OtherLightDirections[index].xyz;
	float spotAttenuation = Square(
		saturate(dot(spotDirection, light.direction) *
		spotAngles.x + spotAngles.y)
	);
	OtherShadowData otherShadowData = GetOtherShadowData(index);
	otherShadowData.lightPositionWS = position;
	otherShadowData.spotDirectionWS = spotDirection;
	…
}
```

在GetOtherShadow中，我们通过取表面到光线矢量与聚光方向的点积来找到到平面的距离。使用它来缩放法线偏差。

```
	float4 tileData = _OtherShadowTiles[other.tileIndex];
	float3 surfaceToLight = other.lightPositionWS - surfaceWS.position;
	float distanceToLightPlane = dot(surfaceToLight, other.spotDirectionWS);
	float3 normalBias =
		surfaceWS.interpolatedNormal * (distanceToLightPlane * tileData.w);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/spot-light-shadows/normal-bias-variable.png)

对于定向光源，我们配置了级联球，以确保我们永远不会在适当的阴影图块之外采样，但是我们不能对其他阴影使用相同的方法。对于聚光灯，它们的图块紧密贴合其锥形区域，因此法线偏差和滤波器大小将使采样推到在锥形边缘接近图块边缘的位置之外。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/spot-light-shadows/without-clamping.png)


解决这个问题的最简单方法是手动将采样限制在图块边界内，就好像每个图块都是其自己单独的纹理。这仍然会在边缘附近拉伸阴影，但不会引入无效的阴影。

调整SetOtherTileData方法，以便它还计算并存储图块边界，根据通过新参数提供的偏移和比例。图块的最小纹理坐标是缩放的偏移量，我们将其存储在数据向量的XY分量中。由于图块是正方形的，我们可以只在Z分量中存储图块的比例，将W分量保留给偏差。我们还必须在两个维度中都将边界缩小半个纹素，以确保采样不会超出边缘。

```
	void SetOtherTileData (int index, Vector2 offset, float scale, float bias) {
		float border = atlasSizes.w * 0.5f;
		Vector4 data;
		data.x = offset.x * scale + border;
		data.y = offset.y * scale + border;
		data.z = scale - border - border;
		data.w = bias;
		otherShadowTiles[index] = data;
	}
```

在RenderSpotShadows中，使用通过SetTileViewport找到的偏移量和分割的倒数作为SetOtherTileData的新参数。

```
		Vector2 offset = SetTileViewport(index, split, tileSize);
		SetOtherTileData(index, offset, 1f / split, bias);
		otherShadowMatrices[index] = ConvertToAtlasMatrix(
			projectionMatrix * viewMatrix, offset, split
		);
```

ConverToAtlasMatrix方法还使用了分割的倒数，因此我们可以计算一次，然后将其传递给两个方法。

```
		float tileScale = 1f / split;
		SetOtherTileData(index, offset, tileScale);
		otherShadowMatrices[index] = ConvertToAtlasMatrix(
			projectionMatrix * viewMatrix, offset, tileScale
		);
```

Then `ConvertToAtlasMatrix` doesn't have to perform the division itself.

```
	Matrix4x4 ConvertToAtlasMatrix (Matrix4x4 m, Vector2 offset, float scale) {
		…
		//float scale = 1f / split;
		…
	}
```

这需要RenderDirectionalShadows执行除法，而它只需要为所有级联执行一次。

```
	void RenderDirectionalShadows (int index, int split, int tileSize) {
		…
		float tileScale = 1f / split;
		
		for (int i = 0; i < cascadeCount; i++) {
			…
			dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
				projectionMatrix * viewMatrix,
				SetTileViewport(tileIndex, split, tileSize), tileScale
			);
			…
		}
	}
```

要应用边界，向SampleOtherShadowAtlas添加一个float3参数，并将其用于在阴影图块空间中限制位置。FilterOtherShadows需要相同的参数，以便它可以传递它。而GetOtherShadow则从图块数据中检索它。

```
float SampleOtherShadowAtlas (float3 positionSTS, float3 bounds) {
	positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
	return SAMPLE_TEXTURE2D_SHADOW(
		_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}

float FilterOtherShadow (float3 positionSTS, float3 bounds) {
	#if defined(OTHER_FILTER_SETUP)
		…
		for (int i = 0; i < OTHER_FILTER_SAMPLES; i++) {
			shadow += weights[i] * SampleOtherShadowAtlas(
				float3(positions[i].xy, positionSTS.z), bounds
			);
		}
		return shadow;
	#else
		return SampleOtherShadowAtlas(positionSTS, bounds);
	#endif
}

float GetOtherShadow (
	OtherShadowData other, ShadowData global, Surface surfaceWS
) {
	…
	return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/spot-light-shadows/with-clamping.png)

## Point Light Shadows

点光源的阴影工作方式与聚光灯类似。不同之处在于，点光源不限于一个锥形区域，因此我们需要将它们的阴影渲染到一个立方体贴图中。这是通过分别渲染立方体的所有六个面来完成的。因此，为了实时阴影的目的，我们将把点光源视为六个光源。它将在阴影图集中占用六个图块。这意味着我们可以同时支持多达两个点光源的实时阴影，因为它们将占用可用的十六个图块中的十二个。如果可用的图块少于六个，则点光源无法获得实时阴影。

### Six Tiles for One Light

首先，在渲染阴影时，我们需要知道我们正在处理点光源，因此向ShadowedOtherLight添加一个布尔值以指示这一点。

```
	struct ShadowedOtherLight {
		…
		public bool isPoint;
	}
```

在ReserveOtherShadows中检查是否有点光源。如果是的话，新的光源数量（包括这个点光源）将比当前数量多六个，否则只多一个。如果这会超过最大值，那么该光源最多只能有烘焙阴影。如果图集中有足够的空间，那么还将存储它是否为点光源在返回的阴影数据的第三个分量中，以便在着色器中轻松检测点光源。

```
	public Vector4 ReserveOtherShadows (Light light, int visibleLightIndex) {
		…

		bool isPoint = light.type == LightType.Point;
		int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);
		if (
			newLightCount > maxShadowedOtherLightCount ||
			!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
		) {
			return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
		}

		shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight {
			visibleLightIndex = visibleLightIndex,
			slopeScaleBias = light.shadowBias,
			normalBias = light.shadowNormalBias,
			isPoint = isPoint
		};

		Vector4 data = new Vector4(
			light.shadowStrength, shadowedOtherLightCount,
			isPoint ? 1f : 0f, maskChannel
		);
		shadowedOtherLightCount = newLightCount;
		return data;
	}
```

### Rendering Point Shadows

调整RenderOtherShadows，使其在循环中根据需要调用一个新的RenderPointShadows方法或现有的RenderSpotShadows方法。此外，由于点光源计为六个，因此根据每种光源类型正确增加迭代器的数量，而不仅仅是递增它。

```
		for (int i = 0; i < shadowedOtherLightCount;) { //i++) {
			if (shadowedOtherLights[i].isPoint) {
				RenderPointShadows(i, split, tileSize);
				i += 6;
			}
			else {
				RenderSpotShadows(i, split, tileSize);
				i += 1;
			}
		}
```

新的RenderPointShadows方法是RenderSpotShadows的复制品，有两个不同之处。首先，它必须渲染六次而不仅仅是一次，遍历其六个图块。其次，它必须使用ComputePointShadowMatricesAndCullingPrimitives而不是ComputeSpotShadowMatricesAndCullingPrimitives。此方法需要在光源索引之后添加两个额外的参数：CubemapFace索引和偏差。我们为每个面渲染一次，目前将偏差设置为零。

```
	void RenderPointShadows (int index, int split, int tileSize) {
		ShadowedOtherLight light = shadowedOtherLights[index];
		var shadowSettings = new ShadowDrawingSettings(
			cullingResults, light.visibleLightIndex,
			BatchCullingProjectionType.Perspective
		);
		for (int i = 0; i < 6; i++) {
			cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
				light.visibleLightIndex, (CubemapFace)i, 0f,
				out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
				out ShadowSplitData splitData
			);
			shadowSettings.splitData = splitData;
			int tileIndex = index + i;
			float texelSize = 2f / (tileSize * projectionMatrix.m00);
			float filterSize = texelSize * ((float)settings.other.filter + 1f);
			float bias = light.normalBias * filterSize * 1.4142136f;
			Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
			float tileScale = 1f / split;
			SetOtherTileData(tileIndex, offset, tileScale, bias);
			otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
				projectionMatrix * viewMatrix, offset, tileScale
			);

			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
			ExecuteBuffer();
			context.DrawShadows(ref shadowSettings);
			buffer.SetGlobalDepthBias(0f, 0f);
		}
	}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/point-shadow-map-back-faces.png)

立方体贴图面的视场始终为90°，因此在距离1处的世界空间图块大小始终为2。这意味着我们可以将偏差的计算移到循环外。我们还可以将图块比例提升出循环。

```
		float texelSize = 2f / tileSize;
		float filterSize = texelSize * ((float)settings.other.filter + 1f);
		float bias = light.normalBias * filterSize * 1.4142136f;
		float tileScale = 1f / split;
		
		for (int i = 0; i < 6; i++) {
			…
			//float texelSize = 2f / (tileSize * projectionMatrix.m00);
			//float filterSize = texelSize * ((float)settings.other.filter + 1f);
			//float bias = light.normalBias * filterSize * 1.4142136f;
			Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
			//float tileScale = 1f / split;
			…
		}
```

### Sampling Point Shadows

点光源阴影存储在一个立方体贴图中，我们的着色器对其进行采样。但是，我们将立方体贴图面存储为图块在一个图集中，因此我们不能使用标准的立方体贴图采样。我们必须自己确定要从中采样的适当面。为此，我们需要知道是否正在处理点光源以及从表面到光源的方向。将这两者都添加到OtherShadowData中。

```
struct OtherShadowData {
	float strength;
	int tileIndex;
	bool isPoint;
	int shadowMaskChannel;
	float3 lightPositionWS;
	float3 lightDirectionWS;
	float3 spotDirectionWS;
};
```

在Light中设置这两个值。如果其他光的阴影数据的第三个分量等于1，则它是一个点光源。

```
OtherShadowData GetOtherShadowData (int lightIndex) {
	…
	data.isPoint = _OtherLightShadowData[lightIndex].z == 1.0;
	data.lightPositionWS = 0.0;
	data.lightDirectionWS = 0.0;
	data.spotDirectionWS = 0.0;
	return data;
}

Light GetOtherLight (int index, Surface surfaceWS, ShadowData shadowData) {
	…
	otherShadowData.lightPositionWS = position;
	otherShadowData.lightDirectionWS = light.direction;
	otherShadowData.spotDirectionWS = spotDirection;
	…
}
```

接下来，我们必须在点光源的情况下调整GetOtherShadow中的图块索引和光源平面。首先将它们转换为变量，最初配置为聚光灯。将图块索引设为float，因为我们将为它添加一个也定义为float的偏移量。

```
float GetOtherShadow (
	OtherShadowData other, ShadowData global, Surface surfaceWS
) {
	float tileIndex = other.tileIndex;
	float3 lightPlane = other.spotDirectionWS;
	float4 tileData = _OtherShadowTiles[tileIndex];
	float3 surfaceToLight = other.lightPositionWS - surfaceWS.position;
	float distanceToLightPlane = dot(surfaceToLight, lightPlane);
	float3 normalBias =
		surfaceWS.interpolatedNormal * (distanceToLightPlane * tileData.w);
	float4 positionSTS = mul(
		_OtherShadowMatrices[tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	);
	return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}
```

如果我们有一个点光源，那么我们必须使用适当的轴对齐平面。我们可以使用CubeMapFaceID函数来找到面的偏移量，通过将其传递给该函数的是光线方向的否定值。这个函数要么是内置的，要么是在核心RP库中定义的，返回一个float。立方体贴图面的顺序是+X，−X，+Y，−Y，+Z，−Z，这与我们渲染它们的方式相匹配。将偏移量添加到图块索引。

```
float GetOtherShadow (
	OtherShadowData other, ShadowData global, Surface surfaceWS
) {
	float tileIndex = other.tileIndex;
	float3 lightPlane = other.spotDirectionWS;
	if (other.isPoint) {
		float faceOffset = CubeMapFaceID(-other.lightDirectionWS);
		tileIndex += faceOffset;
	}
	…
}
	if (other.isPoint) {

		plane = pointShadowPlanes[CubeMapFaceID(-other.lightDirectionWS)];
	}
```

接下来，我们需要使用与面方向相匹配的光源平面。为它们创建一个静态常量数组，并使用面偏移量对其进行索引。平面的法线必须指向与面相反的方向，就像聚光方向指向光源一样。

```
static const float3 pointShadowPlanes[6] = {
	float3(-1.0, 0.0, 0.0),
	float3(1.0, 0.0, 0.0),
	float3(0.0, -1.0, 0.0),
	float3(0.0, 1.0, 0.0),
	float3(0.0, 0.0, -1.0),
	float3(0.0, 0.0, 1.0)
};

float GetOtherShadow (
	OtherShadowData other, ShadowData global, Surface surfaceWS
) {
	float tileIndex = other.tileIndex;
	float3 plane = other.spotDirectionWS;
	if (other.isPoint) {
		float faceOffset = CubeMapFaceID(-other.lightDirectionWS);
		tileIndex += faceOffset;
		lightPlane = pointShadowPlanes[faceOffset];
	}
	…
}
```

![with](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/with-shadows.png)

![without](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/without-shadows.png)

### Drawing the Correct Faces

我们现在可以看到点光源的实时阴影。它们似乎不会出现阴影痤疮，即使没有偏差。不幸的是，光现在会透过物体漏到靠近它们的对面表面。增加阴影偏差会使情况变得更糟，并且似乎会在靠近其他表面的物体的阴影中切割出洞。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/normal-bias-3.png)

这是因为Unity渲染点光源阴影的方式。它以颠倒的方式绘制它们，从而颠倒了三角形的卷绕顺序。通常，从光的角度来看，会绘制正面，但现在会绘制背面。这可以防止大多数阴影痤疮，但会引入光漏。我们无法停止翻转，但可以通过否定从ComputePointShadowMatricesAndCullingPrimitives获取的视图矩阵的一行来撤销它。让我们否定它的第二行。这会在图块中再次将一切颠倒，将一切恢复正常。由于该行的第一个分量始终为零，因此我们只需要否定其他三个分量。

```
			cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
				light.visibleLightIndex, (CubemapFace)i, fovBias*0,
				out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
				out ShadowSplitData splitData
			);
			viewMatrix.m11 = -viewMatrix.m11;
			viewMatrix.m12 = -viewMatrix.m12;
			viewMatrix.m13 = -viewMatrix.m13;
```

![bias 0](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/normal-shadows-bias-0.png)

![bias 1](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/normal-shadow-bias-1.png)

这个改变在比较阴影贴图时最明显。

![front](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/point-shadow-map-front-faces.png)

![back](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/point-shadow-map-back-faces.png)

请注意，将MeshRenderer的Cast Shadows模式设置为Two Sided的对象不受影响，因为它们的所有面都不会被剔除。例如，我已经将所有使用剪切或透明材质的球体设置为投射双面阴影，以使它们看起来更加实心。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/two-sided-sphere-shadows.png)

### Field of View Bias

在立方体贴图的各个面之间总是存在不连续性，因为纹理平面的方向突然改变了90°。常规的立方体贴图采样可以在一定程度上隐藏这一点，因为它可以在各个面之间进行插值，但我们每个片段只从一个瓦片中采样。我们遇到了与聚光灯阴影瓦片边缘存在的相同问题，但现在它们没有被隐藏，因为没有聚光衰减。

![with claming](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/without-fov-bias-with-clamping.png)

![without claming](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/without-fov-bias-without-claming.png)

我们可以通过在渲染阴影时略微增加视野（FOV）来减少这些伪影，以便我们永远不会采样超出瓦片的边缘。这就是 ComputePointShadowMatricesAndCullingPrimitives 的 bias 参数的作用。我们通过使我们的瓦片尺寸略大于距离光源1的位置的2来实现这一点。具体来说，我们在每一侧都加上法线偏差和滤波器大小。然后，半个相应 FOV 角的正切等于1加上偏差和滤波器大小。将其加倍，转换为度数，减去90°，并将其用于 RenderPointShadows 中的 FOV 偏差。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/fov-bias-diagram.png)

```
	float fovBias =
			Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
		for (int i = 0; i < 6; i++) {
			cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
				light.visibleLightIndex, (CubemapFace)i, fovBias,
				out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
				out ShadowSplitData splitData
			);
			…
		}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/with-fov-bias.png)

请注意，这种方法并不完美，因为通过增加瓦片尺寸，像素尺寸也会增加。因此，滤波器尺寸会增加，法线偏差也应该增加，这意味着我们必须再次增加FOV。然而，通常情况下，差异小得足以让我们忽略瓦片尺寸的增加，除非在小的图集尺寸下使用大的法线偏差和滤波器。

我们可以为聚光灯使用相同的方法吗？

 可以的，这将使在稍作修改的情况下不再需要瓦片夹紧。然而，ComputeSpotShadowMatricesAndCullingPrimitives没有FOV偏差参数，因此我们将不得不创建自己的变体，这超出了本教程的范围。