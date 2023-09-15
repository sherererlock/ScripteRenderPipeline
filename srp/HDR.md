# HDR

## High Dynamic Range

到目前为止，在渲染相机时，我们一直使用低动态范围颜色（简称LDR），这是默认设置。这意味着每个颜色通道都用一个值表示，该值被夹在0到1之间。在此模式下，（0,0,0）表示黑色，（1,1,1）表示白色。虽然我们的着色器可以在此范围之外产生结果，但GPU在存储它们时会夹紧颜色，就像我们在每个片段函数的末尾使用了"saturate"一样。

### Is (1,1,1) really white?

这是理论上的白点，但其实际观察到的颜色取决于显示器以及其配置方式。调整显示器的亮度会改变其白点。此外，你的眼睛会根据你所看的整体光亮水平进行调整，从而改变你的相对白点。例如，如果你降低房间的光亮水平，你仍然会以相同的方式解释颜色，尽管观察到的强度已经发生了变化。你还可以在一定程度上对光照的色调偏移进行补偿。当照明突然变化时，这变得明显，因为调整是逐渐的。

您可以使用帧调试器来检查每个绘制调用的渲染目标类型。普通相机的目标被描述为B8G8R8A8_SRGB。这意味着它是一个RGBA缓冲区，每个通道有8位，因此每个像素有32位。此外，RGB通道以sRGB颜色空间存储。由于我们是在线性颜色空间中工作，所以GPU在从缓冲区读取和写入时会自动在这两个空间之间进行转换。一旦渲染完成，缓冲区就会被发送到显示器，后者将其解释为sRGB颜色数据。

### What about HDR displays?

Unity 2022支持HDR显示，但要获得好看的HDR输出并且需要HDR显示来测试它并不是一件简单的事情。因此，**我们假设所有显示器都是LDR sRGB。**

每个颜色通道的最大值为1,在光强度不超过此值时效果良好。但是，传入光线的强度并没有固有的上限。太阳就是一个极亮的光源的例子，这就是为什么你不应该直接盯着它看的原因。它的强度远远超过我们的眼睛在受损之前能够感知的范围。但是，许多常规光源也会产生具有超出观察者限制的强度的光，特别是在近距离观察时。要正确处理这种强度，我们必须渲染到高动态范围（HDR）缓冲区，它支持大于1的值。

### HDR Reflection Probes

HDR渲染需要HDR渲染目标。这不仅适用于常规相机，也适用于反射探头。反射探头是否包含HDR或LDR数据可以通过其HDR切换选项进行控制，该选项默认启用。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/reflection-probe-hdr.png)

当一个反射探头使用HDR时，它可以包含高强度的颜色，这些颜色主要是它捕捉到的镜面反射。你可以通过场景中它们引发的反射间接观察到它们。不完美的反射会削弱探头的颜色，从而使HDR值显得更加突出。

![with](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/reflections-with-hdr.png)

![without](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/reflections-without-hdr.png)

### HDR Cameras

相机也有一个HDR配置选项，但它本身不会产生任何影响。它可以设置为关闭（Off）或使用图形设置（Use Graphics Settings）。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/camera-hdr.png)

"使用图形设置"模式只表示相机允许HDR渲染。是否进行HDR渲染取决于渲染管线（RP）。我们将通过向CustomRenderPipelineAsset添加一个开关来允许HDR，并将其传递给管线构造函数来控制这一点。

```
	[SerializeField]
	bool allowHDR = true;
	
	…

	protected override RenderPipeline CreatePipeline () {
		return new CustomRenderPipeline(
			allowHDR, useDynamicBatching, useGPUInstancing, useSRPBatcher,
			useLightsPerObject, shadows, postFXSettings
		);
	}
```

```
	bool allowHDR;

	…

	public CustomRenderPipeline (
		bool allowHDR,
		…
	) {
		this.allowHDR = allowHDR;
		…
	}
	
	…
	
	protected override void Render (
		ScriptableRenderContext context, List<Camera> cameras
	) {
		for (int i = 0; i < cameras.Count; i++) {
			renderer.Render(
				context, cameras[i], allowHDR,
				useDynamicBatching, useGPUInstancing, useLightsPerObject,
				shadowSettings, postFXSettings
			);
		}
	}
```

```
	bool useHDR;

	public void Render (
		ScriptableRenderContext context, Camera camera, bool allowHDR,
		bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
		ShadowSettings shadowSettings, PostFXSettings postFXSettings
	) {
		…
		if (!Cull(shadowSettings.maxDistance)) {
			return;
		}
		useHDR = allowHDR && camera.allowHDR;

		…
	}
```

### HDR Render Textures

HDR渲染只在与后期处理相结合时才有意义，因为我们无法更改最终的帧缓冲区格式。因此，在CameraRenderer.Setup中创建自己的中间帧缓冲区时，我们将在适当的情况下使用默认的HDR格式，而不是通常用于LDR的默认格式。

```
			buffer.GetTemporaryRT(
				frameBufferId, camera.pixelWidth, camera.pixelHeight,
				32, FilterMode.Bilinear, useHDR ?
					RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
			);
```

帧调试器将显示默认的HDR格式为R16G16B16A16_SFloat，这意味着它是一个RGBA缓冲区，每个通道有16位，因此每个像素有64位，是LDR缓冲区大小的两倍。在这种情况下，每个值都是在线性空间中的有符号浮点数，没有被夹在0-1之间。

### Can we use different render texture formats?

是的，但您必须确保您的目标平台支持它。对于本教程，我们将坚持使用默认的HDR格式，这将始终起作用。

当逐个步进绘制调用时，你会注意到场景会比最终结果暗淡。这是因为这些步骤存储在HDR纹理中。它看起来暗淡是因为线性颜色数据被原样显示，因此被错误地解释为sRGB。

![hdr](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/hdr-before-post.png)

![ldr](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/ldr-before-post.png)

### Why does the brightness change?

sRGB格式使用非线性传输函数。显示器会进行相应的调整，执行所谓的伽马校正。通常，伽马调整函数用c^2.2来近似表示，其中c是原始颜色，尽管实际的传输函数略有不同。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/incorrect-transfer-graph.png)

### HDR Post Processing

在这一点上，结果看起来与以前没有什么不同，因为我们对扩展范围没有进行任何处理，一旦渲染到LDR目标后，它就被夹紧了。泛光可能会显得稍亮一些，但不会有太大变化，因为在预过滤传递之后颜色被夹紧。我们还必须在HDR中执行后期处理，以充分利用它。因此，在CameraRenderer.Render中调用PostFXStack.Setup时，让我们传递是否使用了HDR。

```
		postFXStack.Setup(context, camera, postFXSettings, useHDR);
```

Now `**PostFXStack**` can also keep track of whether it should use HDR.

```
	bool useHDR;

	…

	public void Setup (
		ScriptableRenderContext context, Camera camera, PostFXSettings settings,
		bool useHDR
	) {
		this.useHDR = useHDR;
		…
	}
```

And we can use the appropriate texture format in `DoBloom`.

```
		RenderTextureFormat format = useHDR ?
			RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
```

HDR和LDR的泛光差异可能会因场景的亮度而引人注目或不易察觉。通常，泛光阈值被设置为1，因此只有HDR颜色会对其产生影响。这样，发光效果可以指示对于显示器来说过于明亮的颜色。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/hdr-bloom.png)

因为泛光会对颜色进行平均处理，即使是一个非常亮的像素也会在视觉上影响到一个非常大的区域。通过比较预过滤步骤和最终结果，你可以看到这一点。即使是一个像素也可以产生一个大的圆形发光效果。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/hdr-bloom-prefilter.png)

例如，当一个2×2的值为0、0、0和1的块因降采样而被平均时，结果将为0.25。但是，如果HDR版本平均0、0、0和10，结果将为2.5。与LDR相比，似乎0.25的结果被增强到了1。这是HDR的一个显著优势，因为它可以更好地捕捉高亮度区域的细节。

### Fighting Fireflies

HDR的一个缺点是它可能会产生比周围区域明显亮的小图像区域。当这些区域的大小约为一个像素或更小时，它们可以在移动过程中大大改变相对大小，并在运动中快速出现和消失，导致闪烁。这些区域被称为"fireflies"（萤火虫）。当泛光应用到它们时，效果可能会呈现出频闪的特点。

要完全消除这个问题需要无限的分辨率，这是不可能的。我们可以采取的下一个最佳方法是在预过滤过程中更积极地模糊图像，以淡化"fireflies"。让我们在PostFXSettings.BloomSettings中添加一个切换选项来实现这一点。

```
	public bool fadeFireflies;
```

为了实现这一目的，添加一个新的预过滤"fireflies"通道。再次，我不会展示将通道添加到PostFxStack着色器和PostFXStack.Pass枚举的过程。在DoBloom中选择适当的通道进行预过滤。

```
	Draw(
			sourceId, bloomPrefilterId, bloom.fadeFireflies ?
				Pass.BloomPrefilterFireflies : Pass.BloomPrefilter
		);
```

为了淡化"fireflies"，最直接的方法是将我们的2×2降采样预过滤通道的滤波器扩展为一个大的6×6盒状滤波器。我们可以通过九个采样来实现这一点，在平均之前分别对每个采样应用泛光阈值。在PostFXStackPasses中添加所需的BloomPrefilterFirefliesPassFragment函数来实现这一点。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/6x6-box-filter.png)

```
float4 BloomPrefilterFirefliesPassFragment (Varyings input) : SV_TARGET {
	float3 color = 0.0;
	float2 offsets[] = {
		float2(0.0, 0.0),
		float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0),
		float2(-1.0, 0.0), float2(1.0, 0.0), float2(0.0, -1.0), float2(0.0, 1.0)
	};
	for (int i = 0; i < 9; i++) {
		float3 c =
			GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
		c = ApplyBloomThreshold(c);
		color += c;
	}
	color *= 1.0 / 9.0;
	return float4(color, 1.0);
}
```

但这还不足以解决问题，因为非常亮的像素只是被扩散到一个更大的区域。为了淡化"fireflies"，我们将使用基于颜色亮度的加权平均值。颜色的亮度是其感知亮度。我们将使用Core Library中Color HLSL文件中定义的Luminance函数来实现这一点。

```
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
```

样本的权重为1 / (l + 1)，其中l是其亮度。因此，对于亮度0，权重为1，对于亮度1，权重为½，对于亮度3，权重为¼，对于亮度7，权重为⅛，依此类推。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/luminance-weight.png)

最后，我们将样本总和除以这些权重的总和。这有效地将"fireflies"的亮度分散到所有其他样本中。如果其他样本比较暗，"firefly"就会逐渐消失。例如，0、0、0和10的加权平均值是10 / (11 * 3 + 1) ≈ 0.29。

```
float4 BloomPrefilterFirefliesPassFragment (Varyings input) : SV_TARGET {
	float3 color = 0.0;
	float weightSum = 0.0;
	…
	for (int i = 0; i < 9; i++) {
		…
		float w = 1.0 / (Luminance(c) + 1.0);
		color += c * w;
		weightSum += w;
	}
	color /= weightSum;
	return float4(color, 1.0);
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/luminance-weighed-average.png)

由于我们在初始的预过滤步骤之后执行高斯模糊，因此我们可以跳过直接邻接于中心的四个样本，将样本数量从九减少到五。这可以提高性能。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/x-filter.png)

```
	float2 offsets[] = {
		float2(0.0, 0.0),
		float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0)//,
		//float2(-1.0, 0.0), float2(1.0, 0.0), float2(0.0, -1.0), float2(0.0, 1.0)
	};
	for (int i = 0; i < 5; i++) { … }
```

这将会将单像素的"fireflies"转变为×形状的图案，并将单像素的水平或垂直线拆分成两条独立的线在预过滤步骤中，但在第一次模糊步骤之后，这些图案就会消失。这是一个有效的方式来减少"fireflies"的影响。

![5](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/average-5.png)

![9](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/high-dynamic-range/average-9.png)

这虽然不能完全消除"fireflies"，但可以大大减弱它们的强度，使它们不再明显显眼，除非泛光强度设置得比1高得多。这是一个有效的方法来减轻"fireflies"的影响。

## Scattering Bloom


现在我们有了HDR泛光效果，让我们考虑一种更现实的应用方式。想法是相机并不完美。它们的镜头不能正确聚焦所有光线。部分光线会散射到一个较大的区域，有点类似于我们目前的泛光效果。相机质量越好，散射越少。我们的加法泛光效果与之不同之处在于，散射不会增加光线，它只是将光线扩散开。散射可以在视觉上呈现出轻微的光晕，也可以是覆盖整个图像的轻微薄雾。

眼睛也不完美，光线在其中以复杂的方式散射。它发生在所有传入的光线上，但只有在光线很亮的时候才会真正显眼。例如，当看着一个小而明亮的光源在黑暗的背景下时，就很明显，比如夜晚的灯笼或阳光在明亮的白天的反射。

与均匀的圆形模糊光晕不同，我们将看到许多多边形的、不对称的星状图案，这些图案还具有色调变化，这是我们自己眼睛特有的。但我们的泛光效果将代表一个没有特征的相机，具有均匀的散射效果。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/scattering-bloom/cars-scattering-bloom.jpg)

### Bloom Mode

我们将同时支持传统的加法泛光和能量守恒的散射泛光。在PostFXSettings.BloomSettings中添加一个枚举选项来选择这些模式，并添加一个0到1的滑块来控制光线散射的程度

```
		public enum Mode { Additive, Scattering }

		public Mode mode;

		[Range(0f, 1f)]
		public float scatter;
```

将现有的BloomCombine通道重命名为BloomAdd，并引入一个新的BloomScatter通道。确保枚举和通道顺序保持按字母顺序排列。然后，在合并阶段的DoBloom中使用适当的通道。在散射的情况下，我们将使用散射量作为强度，而不是1。我们仍然会在最终绘制中使用配置的强度。

```
		Pass combinePass;
		if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive) {
			combinePass = Pass.BloomAdd;
			buffer.SetGlobalFloat(bloomIntensityId, 1f);
		}
		else {
			combinePass = Pass.BloomScatter;
			buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
		}
		
		if (i > 1) {
			buffer.ReleaseTemporaryRT(fromId - 1);
			toId -= 5;
			for (i -= 1; i > 0; i--) {
				buffer.SetGlobalTexture(fxSource2Id, toId + 1);
				Draw(fromId, toId, combinePass);
				…
			}
		}
		else {
			buffer.ReleaseTemporaryRT(bloomPyramidId);
		}
		buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);
		buffer.SetGlobalTexture(fxSource2Id, sourceId);
		Draw(fromId, BuiltinRenderTextureType.CameraTarget, combinePass);
```

BloomScatter通道的函数与BloomAdd相同，只是它基于强度插值高分辨率和低分辨率源，而不是将它们相加。因此，散射量为零意味着只使用最低的泛光金字塔级别，而散射为1意味着只使用最高的泛光金字塔级别。在四个级别的情况下，0.5时，连续级别的贡献分别为0.5、0.25、0.125、0.125。

```
float4 BloomScatterPassFragment (Varyings input) : SV_TARGET {
	float3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	float3 highRes = GetSource2(input.screenUV).rgb;
	return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}
```

散射泛光不会使图像变亮。它可能看起来会使上面的例子变暗，但这是因为它只显示了原始图像的一个裁剪部分。然而，能量守恒并不完美，因为高斯滤波器被夹在图像的边缘，这意味着边缘像素的贡献被放大。虽然我们可以进行补偿，但通常不会这样做，因为通常不容易察觉到这一点。

### Scatter Limits

因为散射值为0和1会消除除一个金字塔级别之外的所有级别，所以使用这些值是没有意义的。因此，让我们将散射滑块的范围减小到0.05至0.95。这使得默认值为零无效，因此要显式初始化BloomSettings的值。让我们使用0.07，这是URP和HDRP使用的相同的散射默认值。

```
	public struct BloomSettings {

		…

		[Range(0.05f, 0.95f)]
		public float scatter;
	}

	[SerializeField]
	BloomSettings bloom = new BloomSettings {
		scatter = 0.7f
	};
```

此外，对于散射泛光来说，大于1的强度是不适当的，因为那样会增加光线。因此，在DoBloom中，我们将对其进行夹紧，将最大值限制为0.95，以确保原始图像始终对结果产生一些贡献。

```
		float finalIntensity;
		if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive) {
			combinePass = Pass.BloomAdd;
			buffer.SetGlobalFloat(bloomIntensityId, 1f);
			finalIntensity = bloom.intensity;
		}
		else {
			combinePass = Pass.BloomScatter;
			buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
			finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
		}

		if (i > 1) {
			…
		}
		else {
			buffer.ReleaseTemporaryRT(bloomPyramidId);
		}
		buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/scattering-bloom/intensity-05-scatter-07.png)

散射泛光比加法泛光更加微妙。通常也使用较低的强度。这意味着，就像真实相机一样，泛光效果只对非常明亮的光线才真正明显，尽管所有的光线都会散射。

虽然这不是现实的，但仍然可以对较暗的像素应用阈值，以消除较强的散射。这可以在使用更强的泛光时保持图像清晰。然而，这会减少光线，因此会使图像变暗。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/scattering-bloom/threshold-too-dark.png)

我们必须补偿缺失的散射光。为此，我们创建了一个额外的BloomScatterFinal通道，用于散射泛光的最终绘制。

```
		Pass combinePass, finalPass;
		float finalIntensity;
		if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive) {
			combinePass = finalPass = Pass.BloomAdd;
			buffer.SetGlobalFloat(bloomIntensityId, 1f);
			finalIntensity = bloom.intensity;
		}
		else {
			combinePass = Pass.BloomScatter;
			finalPass = Pass.BloomScatterFinal;
			buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
			finalIntensity = Mathf.Min(bloom.intensity, 1f);
		}

		…
		Draw(fromId, BuiltinRenderTextureType.CameraTarget, finalPass);
	}
```

这个通道的函数与其他散射通道函数相同，只有一个区别。它将缺失的光添加到低分辨率通道中，通过将高分辨率的光添加到低分辨率通道中，然后再减去，但应用了泛光阈值。这不是一个完美的重建方法，它不是一个加权平均，也忽略了因淡化的"fireflies"而丢失的光，但足够接近，而且不会向原始图像添加光线。

```
float4 BloomScatterFinalPassFragment (Varyings input) : SV_TARGET {
	float3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	float3 highRes = GetSource2(input.screenUV).rgb;
	lowRes += highRes - ApplyBloomThreshold(highRes);
	return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/scattering-bloom/scatter-final.png)

## Tone Mapping

尽管我们可以在HDR中渲染，但常规相机的最终帧缓冲区始终是LDR。因此，颜色通道在1处被截断。实际上，最终图像的白点位于1处。极亮的颜色最终看起来与完全饱和的颜色没有区别。例如，我创建了一个具有多个光照级别和具有不同发光量的物体的场景，远远超过了1。最强的发光量为8，最亮的光强度为200。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/without-fx.png)

在不应用任何后期特效的情况下，很难甚至不可能确定哪些物体和灯光是非常明亮的。我们可以使用泛光来突显这一点。例如，我使用了阈值1、knee 0.5、强度0.2和散射0.7以及最大迭代次数。

![additive](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/bloom-additive.png)

![scattering](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/bloom-scattering.png)

发光的物体显然是明亮的，但我们仍然无法感知它们相对于场景的亮度。为了做到这一点，我们需要调整图像的亮度，即增加其白点，使最亮的颜色不再超过1。我们可以通过统一降低整个图像的亮度来实现这一点，但这会使大部分图像变得如此暗淡，以至于我们无法清晰地看到它。理想情况下，我们会大幅调整非常亮的颜色，同时对暗色调进行轻微调整。因此，我们需要进行非均匀的颜色调整。这种颜色调整不代表光线本身的物理变化，而是观察方式的变化。例如，我们的眼睛对较暗的色调更为敏感。

从HDR到LDR的转换被称为色调映射，它源自摄影和电影制作。传统的照片和电影也具有有限的范围和非均匀的光敏度，因此已经开发了许多技术来进行转换。没有一种正确的色调映射方法。不同的方法可以用来设置最终结果的情感，比如传统的电影外观。

### Extra Post FX Step

我们在泛光之后的新的后期特效步骤中执行色调映射。为此，在PostFXStack中添加一个DoToneMapping方法，最初只是将源复制到相机目标。

```
void DoToneMapping(int sourceId) {
		Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
	}
```

我们需要调整泛光的结果，因此获取一个新的全分辨率临时渲染纹理，并在DoBloom中将其用作最终目标。此外，使其返回是否绘制了任何内容，而不是在跳过效果时直接绘制到相机目标。

```
	int
		bloomBicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
		bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
		bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
		bloomResultId = Shader.PropertyToID("_BloomResult"),
		…;

	…
	
	bool DoBloom (int sourceId) {
		//buffer.BeginSample("Bloom");
		PostFXSettings.BloomSettings bloom = settings.Bloom;
		int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
		
		if (
			bloom.maxIterations == 0 || bloom.intensity <= 0f ||
			height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2
		) {
			//Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
			//buffer.EndSample("Bloom");
			return false;
		}
		
		buffer.BeginSample("Bloom");
		…
		buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);
		buffer.SetGlobalTexture(fxSource2Id, sourceId);
		buffer.GetTemporaryRT(
			bloomResultId, camera.pixelWidth, camera.pixelHeight, 0,
			FilterMode.Bilinear, format
		);
		Draw(fromId, bloomResultId, finalPass);
		buffer.ReleaseTemporaryRT(fromId);
		buffer.EndSample("Bloom");
		return true;
	}
```

调整Render方法，如果激活了泛光，则对泛光结果执行色调映射，然后释放泛光结果纹理。否则，直接在原始源上应用色调映射，完全跳过泛光。

```
	public void Render (int sourceId) {
		if (DoBloom(sourceId)) {
			DoToneMapping(bloomResultId);
			buffer.ReleaseTemporaryRT(bloomResultId);
		}
		else {
			DoToneMapping(sourceId);
		}
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
```

### Could we combine tone mapping with the final bloom pass?

是的，URP和HDRP在Uber通道中执行类似的操作以及更多其他操作。然而，将FX完全分离更清晰，使其更容易更改，这就是本教程中采用的方法。

### Tone Mapping Mode

色调映射有多种方法，我们将支持几种方法，因此在PostFXSettings中添加一个ToneMappingSettings配置结构，其中包含一个Mode枚举选项，最初只包含None。

```
	[System.Serializable]
	public struct ToneMappingSettings {

		public enum Mode { None }

		public Mode mode;
	}

	[SerializeField]
	ToneMappingSettings toneMapping = default;

	public ToneMappingSettings ToneMapping => toneMapping;
```

### Reinhard

**我们的色调映射的目标是降低图像的亮度，以便原本均匀的白色区域显示各种颜色，揭示了原本丢失的细节。就像当你的眼睛适应突然明亮的环境，直到你能再次清晰地看见一样**。但我们不希望均匀地缩小整个图像，因为这会使较暗的颜色变得难以区分，从而将过亮换成了曝光不足。因此，我们需要一个非线性的转换，不会太多地降低暗值，但会大幅降低高值。在极端情况下，零仍然保持为零，接近无限的值会降低到1。一个简单的函数可以实现这个目标，即c / (1 + c)，其中c是一个颜色通道。这个函数在其最简单的形式下被称为Reinhard色调映射操作，最初由Mark Reinhard提出，不过他将其应用于亮度，而我们将其分别应用于每个颜色通道。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/reinhard-graph.png)

在ToneMappingSettings.Mode中添加一个Reinhard选项，放在None之后。然后使枚举从-1开始，以便Reinhard的值为零。

```
	public enum Mode { None = -1, Reinhard }
```

接下来，添加一个ToneMappingReinhard通道，并在适当的情况下使PostFXStack.DoTonemapping使用它。具体来说，如果模式为负数，则执行简单的复制，否则应用Reinhard色调映射。

```
	void DoToneMapping(int sourceId) {
		PostFXSettings.ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
		Pass pass = mode < 0 ? Pass.Copy : Pass.ToneMappingReinhard;
		Draw(sourceId, BuiltinRenderTextureType.CameraTarget, pass);
	}
```

The `ToneMappingReinhardPassFragment` shader function simply applies the function.

```
float4 ToneMappingReinhardPassFragment (Varyings input) : SV_TARGET {
	float4 color = GetSource(input.screenUV);
	color.rgb /= color.rgb + 1.0;
	return color;
}
```

![additive](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/bloom-additive.png)

![scattering](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/bloom-scattering.png)

![reinhard additive](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/reinhard-additive.png)

![reinhard scattering](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/reinhard-scattering.png)

这个方法可以工作，但对于非常大的值，由于精度限制，可能会出现问题。出于同样的原因，非常大的值比无穷大早得多地变为1。因此，在执行色调映射之前，让我们将颜色夹紧。设置一个60的限制可以避免我们将支持的所有模式的潜在问题。

```
	color.rgb = min(color.rgb, 60.0);
	color.rgb /= color.rgb + 1.0;
```

### When is precision an issue?

这个问题可能会影响到某些使用半精度值的函数。由于着色器编译器的一个错误，在某些情况下，即使显式使用了float，Metal API也会出现这种情况。这也会影响到一些MacBook，而不仅仅是移动设备。

### Neutral

Reinhard色调映射的理论白点是无限的，但可以调整以使最大值更早达到，从而减弱调整。这种替代函数是$$c \frac {1 + c / w^2}{1 +c}$$，其中w是白点。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/adjusted-reinhard-graph.png)


我们可以为此添加一个配置选项，但Reinhard并不是我们唯一可以使用的函数。一个更有趣的函数是![](D:\Games\ScriptRenderPipeline\srp\Snipaste_2023-09-15_09-57-14.png)。在这种情况下，x是输入的颜色通道，而其他值是配置曲线的常数。最终颜色是t(ce)/t(w)，其中c是颜色通道，e是曝光偏差，w是白点。它可以产生一个s型曲线，从黑色向上弯曲到中间的线性部分，最终在接近白色时变平。

上述函数是由John Hable设计的。它首次在Uncharted 2中使用（参见幻灯片142和143）

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/uncharted2-graph.png)

URP和HDRP使用这个函数的一个变体，具有它们自己的配置值和一个白点为5.3，但它们还使用了曝光偏差的白度比例，所以最终的曲线是t(c/t(w))/t(w)。这导致了一个有效的白点约为4.035。它用于中性色调映射选项，并通过Color Core Library HLSL文件中的NeutralTonemap函数提供。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/reinhard-neutral-graph.png)

让我们在Mode枚举中添加一个用于此色调映射模式的选项。将其放在None之后和Reinhard之前。

```
		public enum Mode { None = -1, Neutral, Reinhard }
```

然后为它创建另一个通道。PostFXStack.DoToneMapping现在可以通过将模式添加到中性选项来找到正确的通道，如果模式不是None。

```
		Pass pass =
			mode < 0 ? Pass.Copy : Pass.ToneMappingNeutral + (int)mode;
```

然后，ToneMappingNeutralPassFragment函数只需调用NeutralTonemap。

```
float4 ToneMappingNeutralPassFragment (Varyings input) : SV_TARGET {
	float4 color = GetSource(input.screenUV);
	color.rgb = min(color.rgb, 60.0);
	color.rgb = NeutralTonemap(color.rgb);
	return color;
}
```

![reinhard additive](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/reinhard-additive.png)

![reinhard scattering](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/reinhard-scattering.png)

![neutral additive](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/neutral-additive.png)

![neutral scattering](https://catlikecoding.com/unity/tutorials/custom-srp/hdr/tone-mapping/neutral-scattering.png)

### ACES


我们将在本教程中支持的最后一个模式是ACES色调映射，URP和HDRP也使用了它。ACES是Academy Color Encoding System的缩写，是一种全球标准，用于交换数字图像文件，管理颜色工作流程，以及创建交付和存档的主文件。我们只会使用Unity实现的其色调映射方法。

首先，在Mode枚举中添加它，直接放在None之后，以保持其余选项按字母顺序排列。

```
		public enum Mode { None = -1, ACES, Neutral, Reinhard }
```

添加通道并调整PostFXStack.DoToneMapping，以便它从ACES开始。

```
		Pass pass =
			mode < 0 ? Pass.Copy : Pass.ToneMappingACES + (int)mode;
```

新的ToneMappingACESPassFragment函数可以简单地使用Core Library中的AcesTonemap函数。它通过Color包含，但还有一个单独的ACES HLSL文件，您可以查看。该函数的输入颜色必须位于ACES颜色空间中，我们可以使用unity_to_ACES函数将其转换。

```
float4 ToneMappingACESPassFragment (Varyings input) : SV_TARGET {
	float4 color = GetSource(input.screenUV);
	color.rgb = min(color.rgb, 60.0);
	color.rgb = AcesTonemap(unity_to_ACES(color.rgb));
	return color;
}
```

ACES与其他模式最明显的区别之一是它会对非常亮的颜色添加色调偏移，将它们推向白色。当相机或眼睛受到过多光线的刺激时，也会发生这种情况。与泛光结合使用时，可以清楚地看出哪些表面最亮。此外，ACES色调映射会略微降低较暗的颜色，增强了对比度。结果是一种电影般的外观。