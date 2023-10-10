### Spot Angle

聚光灯具有一个角度来控制其光锥的宽度。这个角度是从其中心测量的，因此90°的角度看起来就像我们现在所拥有的。除此之外，还有一个单独的内角度，用于控制光何时开始衰减。通用渲染管线和光照映射器通过在饱和之前对点积进行缩放和加法运算，然后对结果进行平方来实现这一点。具体来说，公式为
$$
saturate(d * a + b)
$$

$$
a = \frac{1} {cos(r_i)/2 - cos(r_o/2)}
$$

$$
b = -cos(r_o/2)*a
$$

$$
d = OtherLightDirection \cdot light.direction
$$

$$r_i$$是inner angles,$$r_o$$是outer angles![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-lights/spot-lights/spot-angle-attenuation-graph.png)

## Baked Light and Shadows

### Fully Baked

完全烘焙点光和聚光灯只需将它们的模式设置为"Baked"。请注意，默认情况下它们的阴影类型设置为"None"，如果您希望它们烘焙带有阴影的话，需要将其更改为其他选项。

![realtime](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-lights/baked-light-and-shadows/realtime.png)

![baked](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-lights/baked-light-and-shadows/baked-too-bright.png)

尽管这足以烘焙这些灯光，但结果表明，当它们被烘焙后，它们会变得过于明亮。这是因为Unity默认使用了不正确的光衰减，与旧版渲染管线（Legacy RP）的结果相匹配。

### Lights Delegate

我们可以通过为Unity提供一个委托方法，在Unity在编辑器中执行光照映射之前调用该方法来告诉Unity使用不同的衰减方式。为此，将CustomRenderPipeline转化为一个部分类（partial class），并在其构造函数的末尾调用一个当前不存在的InitializeForEditor方法。

```
public partial class CustomRenderPipeline : RenderPipeline {

	…

	public CustomRenderPipeline (
		bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,
		ShadowSettings shadowSettings
	) {
		…
		InitializeForEditor();
	}

	…
}
```

然后，创建另一个特定于编辑器的部分类，就像对CameraRenderer那样，为新方法定义一个虚拟方法。除了UnityEngine命名空间，我们还需要使用Unity.Collections和UnityEngine.Experimental.GlobalIllumination。这将导致LightType出现类型冲突，因此请显式使用UnityEngine.LightType来解决它。

```
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;

public partial class CustomRenderPipeline {

	partial void InitializeForEditor ();
}
```

仅限于编辑器，我们必须重写光照映射器设置其光数据的方式。这是通过为其提供一个将数据从输入的Light数组传输到NativeArray<LightDataGI>输出的方法委托来完成的。委托的类型是Lightmapping.RequestLightsDelegate，我们将使用lambda表达式定义该方法，因为我们不会在其他地方使用它。

```
partial void InitializeForEditor ();
	
#if UNITY_EDITOR

	static Lightmapping.RequestLightsDelegate lightsDelegate =
		(Light[] lights, NativeArray<LightDataGI> output) => {};

#endif
```

我们必须为每个光源配置一个LightDataGI结构，并将其添加到输出。对于每种光源类型，我们将不得不使用特殊的代码，因此在循环中使用switch语句来处理这一点。默认情况下，我们会在光数据上使用光源的实例ID调用InitNoBake，这会告诉Unity不进行光照烘焙。

```
	static Lightmapping.RequestLightsDelegate lightsDelegate =
		(Light[] lights, NativeArray<LightDataGI> output) => {
			var lightData = new LightDataGI();
			for (int i = 0; i < lights.Length; i++) {
				Light light = lights[i];
				switch (light.type) {
					default:
						lightData.InitNoBake(light.GetInstanceID());
						break;
				}
				output[i] = lightData;
			}
		};
```

接下来，对于每种支持的光源类型，我们需要构建一个专用的 light struct,，使用 light和 struct的引用作为参数调用LightmapperUtils.Extract，然后在光数据上通过引用传递结构体来调用Init。这对于定向光、点光、聚光灯和区域光都需要执行。

```
				switch (light.type) {
					case LightType.Directional:
						var directionalLight = new DirectionalLight();
						LightmapperUtils.Extract(light, ref directionalLight);
						lightData.Init(ref directionalLight);
						break;
					case LightType.Point:
						var pointLight = new PointLight();
						LightmapperUtils.Extract(light, ref pointLight);
						lightData.Init(ref pointLight);
						break;
					case LightType.Spot:
						var spotLight = new SpotLight();
						LightmapperUtils.Extract(light, ref spotLight);
						lightData.Init(ref spotLight);
						break;
					case LightType.Area:
						var rectangleLight = new RectangleLight();
						LightmapperUtils.Extract(light, ref rectangleLight);
						lightData.Init(ref rectangleLight);
						break;
					default:
						lightData.InitNoBake(light.GetInstanceID());
						break;
				}
```

在Unity 2022中，我们还可以设置聚光灯的内角度和衰减。

```
					case LightType.Spot:
						var spotLight = new SpotLight();
						LightmapperUtils.Extract(light, ref spotLight);
						spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
						spotLight.angularFalloff =
							AngularFalloffType.AnalyticAndInnerAngle;
						lightData.Init(ref spotLight);
					break;
```

如果我们不支持实时区域光，那么如果它们存在，让我们将它们的光模式强制设置为烘焙（Baked）。

```
					case LightType.Area:
						var rectangleLight = new RectangleLight();
						LightmapperUtils.Extract(light, ref rectangleLight);
						rectangleLight.mode = LightMode.Baked;
						lightData.Init(ref rectangleLight);
						break;
```

这些都是必须包括的样板代码。这一切的目的是，现在我们可以将所有光源的光数据的衰减类型设置为FalloffType.InverseSquared。

```
				lightData.falloff = FalloffType.InverseSquared;
				output[i] = lightData;
```

为了让Unity调用我们的代码，创建一个InitializeForEditor的编辑器版本，其中调用Lightmapping.SetDelegate并将我们的委托作为参数传递。

```
	partial void InitializeForEditor ();
	
#if UNITY_EDITOR

	partial void InitializeForEditor () {
		Lightmapping.SetDelegate(lightsDelegate);
	}
```

我们还必须在我们的渲染管线被释放时清理和重置委托。这是通过重写Dispose方法来实现的，让它首先调用其基本实现，然后调用Lightmapping.ResetDelegate。

```
	partial void InitializeForEditor () {
		Lightmapping.SetDelegate(lightsDelegate);
	}

	protected override void Dispose (bool disposing) {
		base.Dispose(disposing);
		Lightmapping.ResetDelegate();
	}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-lights/baked-light-and-shadows/baked-correct-falloff.png)

### Shadow Mask

点光和聚光灯的阴影也可以烘焙到阴影遮罩中，方法是将它们的模式设置为Mixed。每个光源都会获得一个通道，就像定向光一样。但由于它们的范围有限，多个光源可以使用相同的通道，只要它们不重叠。因此，阴影遮罩可以支持任意数量的光源，但每个像素最多只能支持四个光源。如果多个光源试图在争夺相同的通道时重叠，那么优先级较低的光源将被强制设置为烘焙模式，直到冲突消失为止。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-lights/baked-light-and-shadows/shadow-mask.png)

要在点光和聚光灯中使用阴影遮罩，可以向Shadows类添加一个ReserveOtherShadows方法。它的工作方式类似于ReserveDirectionalShadows，但我们只关心阴影遮罩模式，并且只需要配置阴影强度和遮罩通道。

```
	public Vector4 ReserveOtherShadows (Light light, int visibleLightIndex) {
		if (light.shadows != LightShadows.None && light.shadowStrength > 0f) {
			LightBakingOutput lightBaking = light.bakingOutput;
			if (
				lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
				lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
			) {
				useShadowMask = true;
				return new Vector4(
					light.shadowStrength, 0f, 0f,
					lightBaking.occlusionMaskChannel
				);
			}
		}
		return new Vector4(0f, 0f, 0f, -1f);
	}
```

Add a shader property name and array for the shadow data to `**Lighting**`.

```
	static int
		otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
		otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
		otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
		otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections"),
		otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
		otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

	static Vector4[]
		otherLightColors = new Vector4[maxOtherLightCount],
		otherLightPositions = new Vector4[maxOtherLightCount],
		otherLightDirections = new Vector4[maxOtherLightCount],
		otherLightSpotAngles = new Vector4[maxOtherLightCount],
		otherLightShadowData = new Vector4[maxOtherLightCount];
```

Send it to the GPU in `SetupLights`.

```
			buffer.SetGlobalVectorArray(
				otherLightSpotAnglesId, otherLightSpotAngles
			);
			buffer.SetGlobalVectorArray(
				otherLightShadowDataId, otherLightShadowData
			);
```

And configure the data in `SetupPointLight` and `SetupSpotLight`.

```
	void SetupPointLight (int index, ref VisibleLight visibleLight) {
		…
		Light light = visibleLight.light;
		otherLightShadowData[index] = shadows.ReserveOtherShadows(light, index);
	}

	void SetupSpotLight (int index, ref VisibleLight visibleLight) {
		…
		otherLightShadowData[index] = shadows.ReserveOtherShadows(light, index);
	}
```

在着色器方面，可以在Shadows中添加一个OtherShadowData结构和GetOtherShadowAttenuation函数。再次，我们采用与定向阴影相同的方法，只不过我们只有强度和遮罩通道。如果强度为正数，那么我们总是调用GetBakedShadow，否则就没有阴影。

```
struct OtherShadowData {
	float strength;
	int shadowMaskChannel;
};

float GetOtherShadowAttenuation (
	OtherShadowData other, ShadowData global, Surface surfaceWS
) {
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif
	
	float shadow;
	if (other.strength > 0.0) {
		shadow = GetBakedShadow(
			global.shadowMask, other.shadowMaskChannel, other.strength
		);
	}
	else {
		shadow = 1.0;
	}
	return shadow;
}
```

In *Light*, add the shadow data and factor it into the attenuation in `GetOtherLight`.

```
CBUFFER_START(_CustomLight)
	…
	float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

…			

OtherShadowData GetOtherShadowData (int lightIndex) {
	OtherShadowData data;
	data.strength = _OtherLightShadowData[lightIndex].x;
	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
	return data;
}

Light GetOtherLight (int index, Surface surfaceWS, ShadowData shadowData) {
	…
	
	OtherShadowData otherShadowData = GetOtherShadowData(index);
	light.attenuation =
		GetOtherShadowAttenuation(otherShadowData, shadowData, surfaceWS) *
		spotAttenuation * rangeAttenuation / distanceSqr;
	return light;
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-lights/baked-light-and-shadows/mixed-mode.png)

## Lights Per Object

目前，对于每个渲染的片段，都会评估所有可见光源。这对于定向光来说没问题，但对于超出片段范围的其他光源来说，这是不必要的工作。通常，每个点光或聚光灯只影响所有片段的一小部分，因此会有大量无用的工作，这可能会显著影响性能。为了以良好的性能支持许多光源，我们必须想办法减少每个片段的光源评估数量。有多种方法可以实现这一目标，其中最简单的方法是使用Unity的per object index。

这个想法是，Unity确定了哪些光源影响每个对象，并将这些信息发送到GPU。然后，在渲染每个对象时，我们可以只评估相关的光源，忽略其余的光源。因此，光源是基于每个对象确定的，而不是基于每个片段。这通常适用于小型对象，但对于大型对象来说并不理想，因为如果光源仅影响对象的一小部分，它将被评估为整个表面。此外，每个对象可以受到的光源数量是有限的，因此大型对象更容易缺少一些光照。

因为每个对象的光源索引并不理想，可能会丢失一些光照，所以我们将其设置为可选项。这样，我们也可以轻松比较视觉效果和性能。

### Per-Object Light Data

为CameraRenderer.DrawVisibleGeometry添加一个布尔参数，用于指示是否应该使用每个对象的光源模式。如果是这样，为绘制设置的每个对象数据启用PerObjectData.LightData和PerObjectData.LightIndices标志。这将允许您在渲染过程中选择是否使用每个对象的光源数据。

```
	void DrawVisibleGeometry (
		bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject
	) {
		PerObjectData lightsPerObjectFlags = useLightsPerObject ?
			PerObjectData.LightData | PerObjectData.LightIndices :
			PerObjectData.None;
		var sortingSettings = new SortingSettings(camera) {
			criteria = SortingCriteria.CommonOpaque
		};
		var drawingSettings = new DrawingSettings(
			unlitShaderTagId, sortingSettings
		) {
			enableDynamicBatching = useDynamicBatching,
			enableInstancing = useGPUInstancing,
			perObjectData =
				PerObjectData.ReflectionProbes |
				PerObjectData.Lightmaps | PerObjectData.ShadowMask |
				PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
				PerObjectData.LightProbeProxyVolume |
				PerObjectData.OcclusionProbeProxyVolume |
				lightsPerObjectFlags
		};
		…
	}
```

The same parameter must be added to `Render`, so it can be passed to `DrawVisibleGeometry`.

```
	public void Render (
		ScriptableRenderContext context, Camera camera,
		bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
		ShadowSettings shadowSettings
	) {
		…
		DrawVisibleGeometry(
			useDynamicBatching, useGPUInstancing, useLightsPerObject
		);
		…
	}
```

And we must also keep track of and pass along the mode in `**CustomRenderPipeline**`, like the other boolean options.

```
	bool useDynamicBatching, useGPUInstancing, useLightsPerObject;

	ShadowSettings shadowSettings;

	public CustomRenderPipeline (
		bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,
		bool useLightsPerObject, ShadowSettings shadowSettings
	) {
		this.shadowSettings = shadowSettings;
		this.useDynamicBatching = useDynamicBatching;
		this.useGPUInstancing = useGPUInstancing;
		this.useLightsPerObject = useLightsPerObject;
		…
	}
	
	…
	
	protected override void Render (
		ScriptableRenderContext context, List<Camera> cameras
	) {
		for (int i = 0; i < cameras.Count; i++)) {
			renderer.Render(
				context, cameras[i],
				useDynamicBatching, useGPUInstancing, useLightsPerObject,
				shadowSettings
			);
		}
	}
```

Finally, add the toggle option to `**CustomRenderPipelineAsset**`.

```
	[SerializeField]
	bool
		useDynamicBatching = true,
		useGPUInstancing = true,
		useSRPBatcher = true,
		useLightsPerObject = true;

	[SerializeField]
	ShadowSettings shadows = default;

	protected override RenderPipeline CreatePipeline () {
		return new CustomRenderPipeline(
			useDynamicBatching, useGPUInstancing, useSRPBatcher,
			useLightsPerObject, shadows
		);
	}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-lights/lights-per-object/lights-per-object-toggle.png)

### Sanitizing Light Indices

在Unity中，简单地为每个对象创建了一个包含所有活动光源的列表，按照它们的重要性大致排序。这个列表包括所有光源，无论它们是否可见，还包括定向光源。我们需要对这些列表进行清理，只保留可见的非定向光的索引。我们在Lighting.SetupLights方法中执行这个操作，因此在该方法中添加一个lights-per-object参数，并在Lighting.Setup中传递它。这将允许我们在设置光照时选择是否启用每个对象的光源模式。

```
	public void Setup (
		ScriptableRenderContext context, CullingResults cullingResults,
		ShadowSettings shadowSettings, bool useLightsPerObject
	) {
		…
		SetupLights(useLightsPerObject);
		…
	}

	…

	void SetupLights (bool useLightsPerObject) { … }
```

Then add the mode as an argument for `Setup` in `**CameraRenderer**.Render`.

```
		lighting.Setup(
			context, cullingResults, shadowSettings, useLightsPerObject
		);
```

在Lighting.SetupLights中，在我们循环处理可见光之前，从剔除结果中检索光源索引映射。这可以通过使用Allocator.Temp作为参数调用GetLightIndexMap来完成，这将为我们提供一个临时的NativeArray<int>，其中包含光源索引，匹配可见光索引以及场景中所有其他活动光源的索引。

```
		NativeArray<int> indexMap =
			cullingResults.GetLightIndexMap(Allocator.Temp);
		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
```

我们只在使用每个对象的光源时才需要检索这些数据。由于native array i是一个结构体，否则我们将其初始化为其默认值，不会分配任何内容。这可以确保在不需要光源索引映射时不会分配额外的内存。

```
		NativeArray<int> indexMap = useLightsPerObject ?
			cullingResults.GetLightIndexMap(Allocator.Temp) : default;
```

我们只需要包括的点光和聚光灯的索引，所有其他光源都应该跳过。我们可以通过将所有其他光源的索引设置为-1来向Unity传达这一信息。此外，我们还需要更改剩余光源的索引以匹配我们的索引。只有在检索到映射时才设置新的索引。这样可以确保只有在需要时才进行索引的更改

```
	for (int i = 0; i < visibleLights.Length; i++) {
			int newIndex = -1;
			VisibleLight visibleLight = visibleLights[i];
			switch (visibleLight.lightType) {
				…
				case LightType.Point:
					if (otherLightCount < maxOtherLightCount) {
						newIndex = otherLightCount;
						SetupPointLight(otherLightCount++, ref visibleLight);
					}
					break;
				case LightType.Spot:
					if (otherLightCount < maxOtherLightCount) {
						newIndex = otherLightCount;
						SetupSpotLight(otherLightCount++, ref visibleLight);
					}
					break;
			}
			if (useLightsPerObject) {
				indexMap[i] = newIndex;
			}
		}
```

我们还必须清除所有不可见光源的索引。如果我们使用每个对象的光源，可以在第一个循环之后添加一个第二个循环来执行这个操作。这将确保只有可见的光源索引被保留，而不可见的光源索引被清除。

```
		int i;
		for (i = 0; i < visibleLights.Length; i++) {
			…
		}

		if (useLightsPerObject) {
			for (; i < indexMap.Length; i++) {
				indexMap[i] = -1;
			}
		}
```

当完成后，我们必须通过在剔除结果上调用SetLightIndexMap来将调整后的索引映射发送回Unity。之后，索引映射不再需要，因此我们应该通过在其上调用Dispose来释放它。这可以确保不会发生内存泄漏。

```
		if (useLightsPerObject) {
			for (; i < indexMap.Length; i++) {
				indexMap[i] = -1;
			}
			cullingResults.SetLightIndexMap(indexMap);
			indexMap.Dispose();
		}
```

最后，当使用每个对象的光源时，我们将使用不同的着色器变种。我们通过根据需要启用或禁用_LIGHTS_PER_OBJECT着色器关键字来表示这一点

```
	static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";
	
	…
	
	void SetupLights (bool useLightsPerObject) {
		…

		if (useLightsPerObject) {
			for (; i < indexMap.Length; i++) {
				indexMap[i] = -1;
			}
			cullingResults.SetLightIndexMap(indexMap);
			indexMap.Dispose();
			Shader.EnableKeyword(lightsPerObjectKeyword);
		}
		else {
			Shader.DisableKeyword(lightsPerObjectKeyword);
		}
		
		…
	}
```

### Using the Indices

要使用光源索引，请将相关的多编译预处理指令添加到我们Lit着色器的CustomLit通道中。这将确保在编译着色器时包含适用于光源索引的代码路径。

```
		#pragma multi_compile _ _LIGHTS_PER_OBJECT
```

所需的数据是UnityPerDraw缓冲区的一部分，由两个real4值组成，必须直接在unity_WorldTransformParams之后定义。首先是unity_LightData，其中包含在其Y分量中的光源数量。接下来是unity_LightIndices，这是一个长度为两个的数组。这两个向量的每个通道都包含一个光源索引，因此每个对象最多支持八个光源。

```
	real4 unity_WorldTransformParams;

	real4 unity_LightData;
	real4 unity_LightIndices[2];
```

如果定义了_LIGHTS_PER_OBJECT，那么在GetLighting中为其他光源使用替代循环。在这种情况下，可以通过unity_LightData.y找到光源的数量，光源索引必须从unity_LightIndices的适当元素和分量中检索。我们可以通过将迭代器除以4来获得正确的向量，并通过取模4来获取正确的分量。这将确保在使用每个对象的光源模式时正确检索光源数据。

```
	#if defined(_LIGHTS_PER_OBJECT)
		for (int j = 0; j < unity_LightData.y; j++) {
			int lightIndex = unity_LightIndices[j / 4][j % 4];
			Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
			color += GetLighting(surfaceWS, brdf, light);
		}
	#else
		for (int j = 0; j < GetOtherLightCount(); j++) {
			Light light = GetOtherLight(j, surfaceWS, shadowData);
			color += GetLighting(surfaceWS, brdf, light);
		}
	#endif
```

然而，尽管只有最多八个光源索引可用，但提供的光源数量并未考虑到这一限制。因此，我们必须显式地将循环限制为八次迭代，以确保不会超出光源索引的限制。

```
		for (int j = 0; j < min(unity_LightData.y, 8); j++) { … }
```

在这一点上，着色器编译器可能会抱怨整数除法和取模操作的速度较慢，至少在为D3D编译时是如此。无符号整数的等效操作更有效率。我们可以通过将j转换为uint来执行这些操作，以表示值的符号可以被忽略。

```
			int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
```

![all](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-lights/lights-per-object/all-lights.png)

![max 8](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-lights/lights-per-object/max-8-per-object.png)

请注意，启用每个对象的光源模式时，GPU实例化的效率会降低，因为只有光源数量和索引列表匹配的对象才会被分组。SRP批处理程序不受影响，因为每个对象仍然会得到自己的优化绘制调用。这是需要考虑的性能权衡。