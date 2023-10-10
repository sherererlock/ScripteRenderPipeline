# **Color Grading**

## Color Adjustments

目前，我们仅对最终图像应用色调映射，以将HDR颜色调整到可见的LDR范围内。但这并不是调整图像颜色的唯一原因。对于视频、照片和数字图像，通常有大约三个步骤的颜色调整。首先是颜色校正，旨在使图像与我们观察场景时所看到的内容相匹配，以弥补媒体的限制。其次是颜色分级，这是为了实现所需的外观或感觉，不必与原始场景相匹配，也不必真实。这两个步骤通常合并为一个颜色分级步骤。然后是色调映射，将HDR颜色映射到显示范围内。

仅应用色调映射时，图像在亮度不高时往往变得不太丰富多彩。ACES略微增加了深色颜色的对比度，但不能替代颜色分级。本教程以中性色调映射为基础。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/color-grading/color-adjustments/without-adjustments.png)

### Color Grading Before Tone Mapping

颜色分级应该发生在色调映射之前。请将以下功能添加到PostFXStackPasses，在色调映射步骤之前。初始时，只需将颜色分量限制为60。

```
float3 ColorGrade (float3 color) {
	color = min(color, 60.0);
	return color;
}
```

在色调映射步骤中调用颜色分级函数，而不是在那里限制颜色。另外，添加一个新的通道，用于不进行色调映射，但进行颜色分级。

```
float4 ToneMappingNonePassFragment (Varyings input) : SV_TARGET {
	float4 color = GetSource(input.screenUV);
	color.rgb = ColorGrade(color.rgb);
	return color;
}

float4 ToneMappingACESPassFragment (Varyings input) : SV_TARGET {
	float4 color = GetSource(input.screenUV);
	color.rgb = ColorGrade(color.rgb);
	color.rgb = AcesTonemap(unity_to_ACES(color.rgb));
	return color;
}

float4 ToneMappingNeutralPassFragment (Varyings input) : SV_TARGET {
	float4 color = GetSource(input.screenUV);
	color.rgb = ColorGrade(color.rgb);
	color.rgb = NeutralTonemap(color.rgb);
	return color;
}

float4 ToneMappingReinhardPassFragment (Varyings input) : SV_TARGET {
	float4 color = GetSource(input.screenUV);
	color.rgb = ColorGrade(color.rgb);
	color.rgb /= color.rgb + 1.0;
	return color;
}
```

将相同的通道添加到着色器和PostFXStack.Pass枚举中，放在其他色调映射通道之前。然后，调整PostFXStack.DoToneMapping，以便None模式使用自己的通道而不是Copy。

```
	void DoToneMapping(int sourceId) {
		PostFXSettings.ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
		Pass pass = Pass.ToneMappingNone + (int)mode;
		Draw(sourceId, BuiltinRenderTextureType.CameraTarget, pass);
	}
```

The `**ToneMappingSettings**.**Mode**` enum must now start at zero.

```
	public struct ToneMappingSettings {

		public enum Mode { None, ACES, Neutral, Reinhard }

		public Mode mode;
	}
```

### Settings

我们将复制URP和HDRP中的色彩调整后处理工具的功能。第一步是在PostFXSettings中为其添加一个配置结构体。我已经添加了using System，因为我们需要多次添加Serializable属性。

```
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject {

	…

	[Serializable]
	public struct ColorAdjustmentsSettings {}

	[SerializeField]
	ColorAdjustmentsSettings colorAdjustments = default;

	public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;

	…
}
```

URP和HDRP的颜色分级功能是相同的。我们将按照相同的顺序为颜色分级添加相同的配置选项。首先是Post Exposure，一个无约束的浮点数。然后是对比度，一个从-100到100的滑块。下一个选项是Color Filter，这是一个不带alpha通道的HDR颜色。接下来是Hue Shift，又一个滑块，范围从-180°到+180°。最后一个选项是饱和度，同样是一个从-100到100的滑块。

```
	public struct ColorAdjustmentsSettings {

		public float postExposure;

		[Range(-100f, 100f)]
		public float contrast;

		[ColorUsage(false, true)]
		public Color colorFilter;

		[Range(-180f, 180f)]
		public float hueShift;

		[Range(-100f, 100f)]
		public float saturation;
	}
```

默认值都是零，除了颜色滤镜应该是白色。这些设置不会改变图像。

```
	ColorAdjustmentsSettings colorAdjustments = new ColorAdjustmentsSettings {
		colorFilter = Color.white
	};
```

我们同时进行颜色分级和色调映射，所以将PostFXStack.DoToneMapping重构为DoColorGradingAndToneMapping。我们将在这里经常访问PostFXSettings的内部类型，因此让我们添加using static PostFXSettings以使代码更简洁。然后，添加一个ConfigureColorAdjustments方法，在其中获取颜色调整设置，并在DoColorGradingAndToneMapping的开头调用它。

```
using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

public partial class PostFXStack {
	
	…
	
	void ConfigureColorAdjustments () {
		ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
	}

	void DoColorGradingAndToneMapping (int sourceId) {
		ConfigureColorAdjustments();

		ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
		Pass pass = Pass.ToneMappingNone + (int)mode;
		Draw(sourceId, BuiltinRenderTextureType.CameraTarget, pass);
	}
	
	…
}
```

我们可以通过设置一个着色器向量和颜色来实现颜色调整。颜色调整向量的分量包括曝光、对比度、色相偏移和饱和度。曝光以光圈值（stops）表示，这意味着我们必须将2提升到配置的曝光值的幂次方。此外，将对比度和饱和度转换为0-2范围，将色相偏移转换为-1到1范围。滤镜必须在线性颜色空间中。

我不会展示伴随的着色器属性标识符的添加。

```
		ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
		buffer.SetGlobalVector(colorAdjustmentsId, new Vector4(
			Mathf.Pow(2f, colorAdjustments.postExposure),
			colorAdjustments.contrast * 0.01f + 1f,
			colorAdjustments.hueShift * (1f / 360f),
			colorAdjustments.saturation * 0.01f + 1f
		));
		buffer.SetGlobalColor(colorFilterId, colorAdjustments.colorFilter.linear);
```

### Post Exposure

在着色器端，添加向量和颜色。我们将每个调整都放在自己的函数中，首先从曝光开始。创建一个名为ColorGradePostExposure的函数，该函数将颜色与曝光值相乘。然后在限制颜色之后在ColorGrade中应用曝光。

```
float4 _ColorAdjustments;
float4 _ColorFilter;

float3 ColorGradePostExposure (float3 color) {
	return color * _ColorAdjustments.x;
}

float3 ColorGrade (float3 color) {
	color = min(color, 60.0);
	color = ColorGradePostExposure(color);
	return color;
}
```

![minus 2](https://catlikecoding.com/unity/tutorials/custom-srp/color-grading/color-adjustments/post-exposure-minus-2.png)

![plus 2](https://catlikecoding.com/unity/tutorials/custom-srp/color-grading/color-adjustments/post-exposure-plus-2.png)

后曝光的概念是模拟相机的曝光，但是应用在所有其他后处理效果之后，紧接在所有其他颜色分级之前。它是一种非现实的艺术工具，可以用来调整曝光而不影响其他效果，比如泛光。

### Contrast

第二个调整是对比度。我们通过从颜色中减去均匀的中灰色，然后按对比度进行缩放，最后再加上中灰色来应用它。使用ACEScc_MIDGRAY作为中灰色。

What's ACEScc?

ACEScc是ACES色彩空间的对数子集。其中的中灰色值为0.4135884。

```
float3 ColorGradingContrast (float3 color) {
	return (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
}

float3 ColorGrade (float3 color) {
	color = min(color, 60.0);
	color = ColorGradePostExposure(color);
	color = ColorGradingContrast(color);
	return color;
}
```

为了获得最佳效果，这种转换应在Log C而不是线性颜色空间中进行。我们可以使用Color Core Library文件中的LinearToLogC函数将线性颜色转换为Log C，并使用LogCToLinear函数将其转换回来。

```
float3 ColorGradingContrast (float3 color) {
	color = LinearToLogC(color);
	color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
	return LogCToLinear(color);
}
```

![minus 50](https://catlikecoding.com/unity/tutorials/custom-srp/color-grading/color-adjustments/logc.png)

当增加对比度时，可能会导致颜色分量为负数，这可能会影响后续的调整。因此，在ColorGrade中在调整对比度后消除负值。

```
	color = ColorGradingContrast(color);
	color = max(color, 0.0);
```

### Color Filter

接下来是颜色滤镜，只需将其与颜色相乘即可。它可以很好地处理负值，因此我们可以在消除负值之前应用它。

```
float3 ColorGradeColorFilter (float3 color) {
	return color * _ColorFilter.rgb;
}

float3 ColorGrade (float3 color) {
	color = min(color, 60.0);
	color = ColorGradePostExposure(color);
	color = ColorGradingContrast(color);
	color = ColorGradeColorFilter(color);
	color = max(color, 0.0);
	return color;
}
```

### Hue Shift

URP和HDRP在颜色滤镜之后执行色相偏移，我们将使用相同的调整顺序。颜色的色相通过将颜色格式从RGB转换为HSV（使用RbgToHsv函数），将Hue Shift添加到H值，然后通过HsvToRgb函数进行转换来调整。由于色相在0-1的颜色轮上定义，如果超出范围，我们必须将其包装回来。我们可以使用RotateHue来实现这一点，将调整后的色相、零和1作为参数传递给它。这必须在消除负值后进行。

```
float3 ColorGradingHueShift (float3 color) {
	color = RgbToHsv(color);
	float hue = color.x + _ColorAdjustments.z;
	color.x = RotateHue(hue, 0.0, 1.0);
	return HsvToRgb(color);
}

float3 ColorGrade (float3 color) {
	color = min(color, 60.0);
	color = ColorGradePostExposure(color);
	color = ColorGradingContrast(color);
	color = ColorGradeColorFilter(color);
	color = max(color, 0.0);
	color = ColorGradingHueShift(color);
	return color;
}
```

### Saturation

最后一个调整是饱和度。首先，使用Luminance函数获取颜色的亮度。然后，与对比度类似计算结果，只是使用亮度代替中灰色，并且不在Log C中。这可能会再次产生负值，因此从ColorGrade的最终结果中去除这些负值。

```
float3 ColorGradingSaturation (float3 color) {
	float luminance = Luminance(color);
	return (color - luminance) * _ColorAdjustments.w + luminance;
}

float3 ColorGrade (float3 color) {
	color = min(color, 60.0);
	color = ColorGradePostExposure(color);
	color = ColorGradingContrast(color);
	color = ColorGradeColorFilter(color);
	color = max(color, 0.0);
	color = ColorGradingHueShift(color);
	color = ColorGradingSaturation(color);
	return max(color, 0.0);
}
```

## More Controls

色彩调整工具并不是URP和HDRP提供的唯一颜色分级选项。我们将支持一些更多的选项，再次复制Unity的方法。

### White Balance

白平衡工具使调整图像的感知温度成为可能。它有两个滑块，范围为-100至100。第一个是温度（Temperature），用于使图像变得更冷或更暖。第二个是色调（Tint），用于微调温度调整后的颜色。在PostFXSettings中为其添加一个带有默认值为零的设置结构体。

```
	[Serializable]
	public struct WhiteBalanceSettings {

		[Range(-100f, 100f)]
		public float temperature, tint;
	}

	[SerializeField]
	WhiteBalanceSettings whiteBalance = default;

	public WhiteBalanceSettings WhiteBalance => whiteBalance;
```

我们可以使用一个单独的向量着色器属性，通过在核心库中调用ColorUtils.ColorBalanceToLMSCoeffs方法来获取它，将温度和色调传递给它。在PostFXStack中设置它，并在DoColorGradingAndToneMapping中的ConfigureColorAdjustments之后调用一个专用的配置方法。

```
	void ConfigureWhiteBalance () {
		WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
		buffer.SetGlobalVector(whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs(
			whiteBalance.temperature, whiteBalance.tint
		));
	}
	
	void DoColorGradingAndToneMapping (int sourceId) {
		ConfigureColorAdjustments();
		ConfigureWhiteBalance();

		…
	}
```

在着色器端，我们通过在LMS颜色空间中将颜色与向量相乘来应用白平衡。我们可以使用LinearToLMS和LMSToLinear函数进行LMS和其他颜色空间之间的转换。在后曝光之后，对比度之前应用它。

```
float4 _ColorAdjustments;
float4 _ColorFilter;
float4 _WhiteBalance;

float3 ColorGradePostExposure (float3 color) { … }

float3 ColorGradeWhiteBalance (float3 color) {
	color = LinearToLMS(color);
	color *= _WhiteBalance.rgb;
	return LMSToLinear(color);
}

…

float3 ColorGrade (float3 color) {
	color = min(color, 60.0);
	color = ColorGradePostExposure(color);
	color = ColorGradeWhiteBalance(color);
	color = ColorGradingContrast(color);
	…
}
```

What's LMS color space?

它将颜色描述为人眼中三种光感受器锥形细胞类型的响应。

较低的温度使图像呈蓝色，而较高的温度使其呈黄色。通常会使用小幅调整，但我展示极端值以突显效果。

色调可以用来补偿不希望的颜色平衡，将图像推向绿色或品红色。

### Split Toning

分割色调工具用于分别着色图像的阴影和高光部分。一个典型的示例是将阴影推向冷蓝色，将高光推向温暖橙色。

为此创建一个设置结构体，其中包括两个没有alpha通道的LDR颜色，分别用于阴影和高光。它们的默认值是灰色。还包括一个平衡（-100至100）滑块，其默认值为零。

```
	[Serializable]
	public struct SplitToningSettings {

		[ColorUsage(false)]
		public Color shadows, highlights;

		[Range(-100f, 100f)]
		public float balance;
	}

	[SerializeField]
	SplitToningSettings splitToning = new SplitToningSettings {
		shadows = Color.gray,
		highlights = Color.gray
	};

	public SplitToningSettings SplitToning => splitToning;
```

将这两种颜色以gamma空间的方式发送到PostFXStack中的着色器。平衡值可以存储在其中一个颜色的第四个分量中，并缩放到-1至1的范围内。

```
	void ConfigureSplitToning () {
		SplitToningSettings splitToning = settings.SplitToning;
		Color splitColor = splitToning.shadows;
		splitColor.a = splitToning.balance * 0.01f;
		buffer.SetGlobalColor(splitToningShadowsId, splitColor);
		buffer.SetGlobalColor(splitToningHighlightsId, splitToning.highlights);
	}
	
	void DoColorGradingAndToneMapping (int sourceId) {
		ConfigureColorAdjustments();
		ConfigureWhiteBalance();
		ConfigureSplitToning();

		…
	}
```

在着色器端，我们将在近似的gamma空间中执行分割色调，先将颜色提升到2.2的倒数，然后再提升到2.2。这样做是为了匹配Adobe产品的分割色调。这种调整是在颜色滤镜之后，在消除负值之后进行的。

```
float4 _WhiteBalance;
float4 _SplitToningShadows, _SplitToningHighlights;

…

float3 ColorGradeSplitToning (float3 color) {
	color = PositivePow(color, 1.0 / 2.2);
	return PositivePow(color, 2.2);
}

…

float3 ColorGrade (float3 color) {
	…
	color = ColorGradeColorFilter(color);
	color = max(color, 0.0);
	color = ColorGradeSplitToning(color);
	…
}
```

我们通过在颜色和阴影着色之间执行软光混合，然后再进行高光着色，来应用这些色调。我们可以使用SoftLight函数来实现这一点，使用两次。

```
float3 ColorGradeSplitToning (float3 color) {
	color = PositivePow(color, 1.0 / 2.2);
	float3 shadows = _SplitToningShadows.rgb;
	float3 highlights = _SplitToningHighlights.rgb;
	color = SoftLight(color, shadows);
	color = SoftLight(color, highlights);
	return PositivePow(color, 2.2);
}
```

我们通过在混合之前将色调限制在它们各自的区域内，将它们在中性0.5和它们自己之间进行插值来实现。对于高光部分，我们基于饱和亮度加上平衡，再次进行饱和。对于阴影，我们则反之。

```
	float t = saturate(Luminance(saturate(color)) + _SplitToningShadows.w);
	float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t);
	float3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t);
	color = SoftLight(color, shadows);
	color = SoftLight(color, highlights);
```

### Channel Mixer

另一个我们将支持的工具是通道混合器。它允许您组合输入的RGB值以创建一个新的RGB值。例如，您可以交换R和G，从G中减去B，或者将G添加到R中以将绿色推向黄色。

混合器本质上是一个3×3的转换矩阵，具有默认的单位矩阵。我们可以使用三个Vector3值，分别表示红色、绿色和蓝色的配置。Unity的控件显示每个颜色的单独标签，每个输入通道有-100至100的滑块，但我们将直接显示这些向量。行用于输出颜色，XYZ列用于RGB输入。

```
	[Serializable]
	public struct ChannelMixerSettings {

		public Vector3 red, green, blue;
	}
	
	[SerializeField]
	ChannelMixerSettings channelMixer = new ChannelMixerSettings {
		red = Vector3.right,
		green = Vector3.up,
		blue = Vector3.forward
	};

	public ChannelMixerSettings ChannelMixer => channelMixer;
```

Send these three vectors to the GPU.

```
	void ConfigureChannelMixer () {
		ChannelMixerSettings channelMixer = settings.ChannelMixer;
		buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
		buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
		buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
	}

	void DoColorGradingAndToneMapping (int sourceId) {
		…
		ConfigureSplitToning();
		ConfigureChannelMixer();
		…
	}
```

在着色器中执行矩阵乘法。在进行分割色调之后执行这个操作。然后再次消除负值，因为负权重可能会产生负的颜色通道。

```
float4 _SplitToningShadows, _SplitToningHighlights;
float4 _ChannelMixerRed, _ChannelMixerGreen, _ChannelMixerBlue;

…

float3 ColorGradingChannelMixer (float3 color) {
	return mul(
		float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb),
		color
	);
}

float3 ColorGrade (float3 color) {
	…
	ColorGradeSplitToning(color);
	color = ColorGradingChannelMixer(color);
	color = max(color, 0.0);
	color = ColorGradingHueShift(color);
	…
}
```

### Shadows Midtones Highlights

我们将支持的最后一个工具是Shadows Midtones Highlights（SMH）。它的工作方式类似于分割色调，但它还允许调整中间色调，并将阴影和高光区域分离，使它们可以配置。

Unity的控制显示颜色轮和区域权重的可视化，但我们将使用三个HDR颜色字段和四个滑块，用于阴影和高光过渡区域的开始和结束。阴影强度从其开始到结束逐渐减小，而高光强度从其开始到结束逐渐增加。我们将使用0-2范围，以便可以进入HDR颜色空间一点。默认情况下，颜色是白色的，我们将使用与Unity相同的区域默认值，即阴影为0-0.3，高光为0.55-1。

```
	[Serializable]
	public struct ShadowsMidtonesHighlightsSettings {

		[ColorUsage(false, true)]
		public Color shadows, midtones, highlights;

		[Range(0f, 2f)]
		public float shadowsStart, shadowsEnd, highlightsStart, highLightsEnd;
	}

	[SerializeField]
	ShadowsMidtonesHighlightsSettings
		shadowsMidtonesHighlights = new ShadowsMidtonesHighlightsSettings {
			shadows = Color.white,
			midtones = Color.white,
			highlights = Color.white,
			shadowsEnd = 0.3f,
			highlightsStart = 0.55f,
			highLightsEnd = 1f
		};

	public ShadowsMidtonesHighlightsSettings ShadowsMidtonesHighlights =>
		shadowsMidtonesHighlights;
```

Why can't we use color wheels?

Unity没有默认的颜色轮编辑器小部件可以包含在编辑器中。URP和HDRP都包含了它们自己的、但是等效的版本。区域的GUI也是自定义的。

将这三种颜色转换为线性空间并发送到GPU。区域范围可以打包在一个单独的向量中。

```
	void ConfigureShadowsMidtonesHighlights () {
		ShadowsMidtonesHighlightsSettings smh = settings.ShadowsMidtonesHighlights;
		buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
		buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
		buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
		buffer.SetGlobalVector(smhRangeId, new Vector4(
			smh.shadowsStart, smh.shadowsEnd, smh.highlightsStart, smh.highLightsEnd
		));
	}

	void DoColorGradingAndToneMapping (int sourceId) {
		ConfigureColorAdjustments();
		ConfigureWhiteBalance();
		ConfigureSplitToning();
		ConfigureChannelMixer();
		ConfigureShadowsMidtonesHighlights();

		…
	}
```

在着色器中，我们将颜色分别与三种颜色相乘，每种颜色都按其自身的权重进行缩放，然后将结果相加。权重是基于亮度的。阴影权重从1开始，在其开始和结束之间逐渐减小，使用smoothstep函数。而高光权重从零逐渐增加到一。中间色调的权重等于1减去其他两种权重。这个想法是阴影和高光区域不会重叠，或者只有一点点重叠，因此中间色调的权重永远不会变成负值。然而，在检查器中，我们不会强制执行这一点，就像我们不会强制执行开始在结束之前一样。

```
float4 _ChannelMixerRed, _ChannelMixerGreen, _ChannelMixerBlue;
float4 _SMHShadows, _SMHMidtones, _SMHHighlights, _SMHRange;

…

float3 ColorGradingShadowsMidtonesHighlights (float3 color) {
	float luminance = Luminance(color);
	float shadowsWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
	float highlightsWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
	float midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;
	return
		color * _SMHShadows.rgb * shadowsWeight +
		color * _SMHMidtones.rgb * midtonesWeight +
		color * _SMHHighlights.rgb * highlightsWeight;
}

float3 ColorGrade (float3 color) {
	…
	color = ColorGradingChannelMixer(color);
	color = max(color, 0.0);
	color = ColorGradingShadowsMidtonesHighlights(color);
	…
}
```

Unity的控件中的颜色轮工作方式相同，只是它们会限制输入颜色并允许更精确的拖拽。使用HVS颜色选择模式来调整颜色，以在不加入限制的情况下模拟这种功能。

What about the *Color Curves* tool and the *Lift Gamma Gain* tool?

Color Curves是一个强大的工具，可以用于许多效果，包括去饱和除一个单一颜色之外的所有颜色。但是，它依赖于一个自定义的曲线编辑器，要重新创建它将需要很多工作，所以它没有包含在本教程中。

Unity的Lift Gamma Gain工具与Shadows Midtones Highlights类似，但不太直观，并且在应用比例-偏移-幂调整之前需要进行一些转换。因此，我只包括了其中一个工具。

### ACES Color Spaces

当使用ACES色调映射时，Unity会在ACES颜色空间而不是线性颜色空间中执行大部分颜色分级，以获得更好的结果。让我们也这样做。

后曝光和白平衡总是在线性空间中应用的。对比度是其中的一个例外。为ColorGradingContrast添加一个名为useACES的布尔参数。如果使用ACES，则首先将其从线性空间转换为ACES，然后转换为ACEScc颜色空间，而不是转换为Log C。我们可以通过unity_to_ACES和ACES_to_ACEScc来实现这一点。在调整对比度之后，将其转换为ACEScg，使用ACEScc_to_ACES和ACES_to_ACEScg，而不是回到线性空间。

```
float3 ColorGradingContrast (float3 color, bool useACES) {
	color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color);
	color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
	return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
}
```

从现在开始，在颜色分级对比度步骤之后，我们要么处于线性颜色空间，要么处于ACEScg颜色空间。一切仍然保持不变，只是亮度应该在ACEScg空间中使用AcesLuminance来计算。引入一个Luminance函数的变种，根据是否使用ACES来调用正确的函数。

```
float Luminance (float3 color, bool useACES) {
	return useACES ? AcesLuminance(color) : Luminance(color);
}
```

`ColorGradeSplitToning` uses luminance, to give it a `useACES` parameter and pass it to `Luminance`.

```
float3 ColorGradeSplitToning (float3 color, bool useACES) {
	color = PositivePow(color, 1.0 / 2.2);
	float t = saturate(Luminance(saturate(color), useACES) + _SplitToningShadows.w);
	…
}
```

Do the same for `ColorGradingShadowsMidtonesHighlights`.

```
float3 ColorGradingShadowsMidtonesHighlights (float3 color, bool useACES) {
	float luminance = Luminance(color, useACES);
	…
}
```

And for `ColorGradingSaturation`.

```
float3 ColorGradingSaturation (float3 color, bool useACES) {
	float luminance = Luminance(color, useACES);
	return luminance + _ColorAdjustments.w * (color - luminance);
}
```

然后也将该参数添加到ColorGrade中，默认设置为false。将其传递给需要它的函数。在适当的情况下，最终颜色应通过ACEScg_to_ACES转换为ACES颜色空间。

```
float3 ColorGrade (float3 color, bool useACES = false) {
	color = min(color, 60.0);
	color = ColorGradePostExposure(color);
	color = ColorGradeWhiteBalance(color);
	color = ColorGradingContrast(color, useACES);
	color = ColorGradeColorFilter(color);
	color = max(color, 0.0);
	ColorGradeSplitToning(color, useACES);
	color = ColorGradingChannelMixer(color);
	color = max(color, 0.0);
	color = ColorGradingShadowsMidtonesHighlights(color, useACES);
	color = ColorGradingHueShift(color);
	color = ColorGradingSaturation(color, useACES);
	return max(useACES ? ACEScg_to_ACES(color) : color, 0.0);
}
```

现在调整ToneMappingACESPassFragment，使其指示它使用ACES。由于ColorGrade的结果将在ACES颜色空间中，因此可以直接传递给ACESTonemap。

```
float4 ToneMappingACESPassFragment (Varyings input) : SV_TARGET {
	float4 color = GetSource(input.screenUV);
	color.rgb = ColorGrade(color.rgb, true);
	color.rgb = AcesTonemap(color.rgb);
	return color;
}
```

为了说明区别，这里是一个使用增加对比度以及调整阴影、中间色调和高光的ACES色调映射的比较。

## LUT

将所有颜色分级步骤应用于每个像素是很多工作。我们可以制作许多变体，只应用改变某些东西的步骤，但这需要大量的关键字或通道。相反，我们可以将颜色分级嵌入到查找表（LUT）中，并对其进行采样以转换颜色。LUT是一个3D纹理，通常为32×32×32。填充该纹理并稍后进行采样比直接在整个图像上执行颜色分级要少得多的工作。URP和HDRP使用相同的方法。

### LUT Resolution

通常，颜色LUT分辨率为32足够了，但让我们将其设置为可配置的。这是一个我们将添加到CustomRenderPipelineAsset的质量设置，并且将用于所有颜色分级。我们将使用一个枚举来提供16、32和64作为选项，然后将其作为整数传递给管道构造函数。

```
	public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

	[SerializeField]
	ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

	protected override RenderPipeline CreatePipeline () {
		return new CustomRenderPipeline(
			allowHDR, useDynamicBatching, useGPUInstancing, useSRPBatcher,
			useLightsPerObject, shadows, postFXSettings, (int)colorLUTResolution
		);
	}
```

URP和HDRP允许任意的LUT分辨率高达65，但是在不使用2的幂的情况下，我们将使用的方法可能导致LUT采样出现问题。

在CustomRenderPipeline中跟踪颜色LUT分辨率，并将其传递给CameraRenderer.Render方法。

```
	int colorLUTResolution;

	public CustomRenderPipeline (
		…
		PostFXSettings postFXSettings, int colorLUTResolution
	) {
		this.colorLUTResolution = colorLUTResolution;
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
				shadowSettings, postFXSettings, colorLUTResolution
			);
		}
	}
```

Which passes it to `**PostFXStack**.Setup`.

```
	public void Render (
		ScriptableRenderContext context, Camera camera, bool allowHDR,
		bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
		ShadowSettings shadowSettings, PostFXSettings postFXSettings,
		int colorLUTResolution
	) {
		…
		postFXStack.Setup(context, camera, postFXSettings, useHDR, colorLUTResolution);
		…
	}
```

And `**PostFXStack**` keeps track of it.

```
	int colorLUTResolution;

	…

	public void Setup (
		ScriptableRenderContext context, Camera camera, PostFXSettings settings,
		bool useHDR, int colorLUTResolution
	) {
		this.colorLUTResolution = colorLUTResolution;
		…
	}
```

### Rendering to a 2D LUT Texture

LUT是3D的，但普通的着色器不能渲染到3D纹理。因此，我们将使用一个宽的2D纹理来模拟3D纹理，将2D切片放在一行中。因此，LUT纹理的高度等于配置的分辨率，宽度等于分辨率的平方。在DoColorGradingAndToneMapping中配置颜色分级之后，获取一个具有该大小的临时渲染纹理，使用默认的HDR格式。

```
		ConfigureShadowsMidtonesHighlights();

		int lutHeight = colorLUTResolution;
		int lutWidth = lutHeight * lutHeight;
		buffer.GetTemporaryRT(
			colorGradingLUTId, lutWidth, lutHeight, 0,
			FilterMode.Bilinear, RenderTextureFormat.DefaultHDR
		);
```

从现在开始，我们将同时将颜色分级和色调映射渲染到LUT。相应地重命名现有的色调映射通道，因此ToneMappingNone变为ColorGradingNone，依此类推。然后绘制到LUT而不是相机目标，使用适当的通道。然后将源复制到相机目标以获取未经调整的图像作为最终结果，并释放LUT。

```
	ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
		Pass pass = Pass.ColorGradingNone + (int)mode;
		Draw(sourceId, colorGradingLUTId, pass);
		
		Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
		buffer.ReleaseTemporaryRT(colorGradingLUTId);
```

我们现在绕过了颜色分级和色调映射，但是帧调试器显示我们在最终复制之前绘制了图像的扁平版本。

### LUT Color Matrix

为了创建一个适当的LUT，我们需要使用颜色转换矩阵来填充它。我们通过调整颜色分级通道函数来实现这一点，以使用从UV坐标派生的颜色，而不是对源纹理进行采样。添加一个GetColorGradedLUT函数，用于获取颜色并立即执行颜色分级。然后，通道函数只需要在此基础上应用色调映射。

```
float3 GetColorGradedLUT (float2 uv, bool useACES = false) {
	float3 color = float3(uv, 0.0);
	return ColorGrade(color, useACES);
}

float4 ColorGradingNonePassFragment (Varyings input) : SV_TARGET {
	float3 color = GetColorGradedLUT(input.screenUV);
	return float4(color, 1.0);
}

float4 ColorGradingACESPassFragment (Varyings input) : SV_TARGET {
	float3 color = GetColorGradedLUT(input.screenUV, true);
	color = AcesTonemap(color);
	return float4(color, 1.0);
}

float4 ColorGradingNeutralPassFragment (Varyings input) : SV_TARGET {
	float3 color = GetColorGradedLUT(input.screenUV);
	color = NeutralTonemap(color);
	return float4(color, 1.0);
}

float4 ColorGradingReinhardPassFragment (Varyings input) : SV_TARGET {
	float3 color = GetColorGradedLUT(input.screenUV);
	color /= color + 1.0;
	return float4(color, 1.0);
}
```

我们可以通过GetLutStripValue函数找到LUT输入颜色。它需要UV坐标和一个颜色分级LUT参数向量，我们需要将它发送到GPU。

```
float4 _ColorGradingLUTParameters;

float3 GetColorGradedLUT (float2 uv, bool useACES = false) {
	float3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
	return ColorGrade(color, useACES);
}
```

四个矢量参数值分别是 LUT 高度、0.5 除以宽度、0.5 除以高度以及高度除以自身减 1。

```
		buffer.GetTemporaryRT(
			colorGradingLUTId, lutWidth, lutHeight, 0,
			FilterMode.Bilinear, RenderTextureFormat.DefaultHDR
		);
		buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(
			lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)
		));
```

![none](https://catlikecoding.com/unity/tutorials/custom-srp/color-grading/lut/lut-none.png)

### Log C LUT

我们得到的 LUT 矩阵是线性色彩空间，只覆盖 0-1 范围。为了支持 HDR，我们必须扩展这一范围。我们可以将输入色彩解释为对数 C 空间。这样就可以将范围扩展到 59 以下。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/color-grading/lut/linear-logc.png)

```
float3 GetColorGradedLUT (float2 uv, bool useACES = false) {
	float3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
	return ColorGrade(LogCToLinear(color), useACES);
}
```

![ACES](https://catlikecoding.com/unity/tutorials/custom-srp/color-grading/lut/lut-logc-aces.png)

与线性空间相比，Log C 对最暗值的分辨率更高一些。它在大约 0.5 时超过线性值。之后强度会迅速上升，因此矩阵分辨率会大大降低。这对覆盖 HDR 值是必要的，但如果我们不需要 HDR 值，最好还是坚持使用线性空间，否则会浪费近一半的分辨率。在着色器中添加一个布尔值来控制这一点。

```
bool _ColorGradingLUTInLogC;

float3 GetColorGradedLUT (float2 uv, bool useACES = false) {
	float3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
	return ColorGrade(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, useACES);
}
```

仅在使用 HDR 并应用色调映射时启用 Log C 模式。

```
		ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
		Pass pass = Pass.ColorGradingNone + (int)mode;
		buffer.SetGlobalFloat(
			colorGradingLUTInLogId, useHDR && pass != Pass.ColorGradingNone ? 1f : 0f
		);
		Draw(sourceId, colorGradingLUTId, pass);
```

由于我们不再依赖渲染图像，因此不再需要将范围限制为 60。它已经受到 LUT 范围的限制。

```
float3 ColorGrade (float3 color, bool useACES = false) {
	//color = min(color, 60.0);
	…
}
```

### Final Pass

为了应用 LUT，我们引入了一个新的最终通道。它需要做的就是获取源颜色并应用调色 LUT。这需要在一个单独的 ApplyColorGradingLUT 函数中完成。

```
float3 ApplyColorGradingLUT (float3 color) {
	return color;
}

float4 FinalPassFragment (Varyings input) : SV_TARGET {
	float4 color = GetSource(input.screenUV);
	color.rgb = ApplyColorGradingLUT(color.rgb);
	return color;
}
```

我们可以通过 ApplyLut2D 函数应用 LUT，该函数负责将二维 LUT 条带解释为三维纹理。它需要 LUT 纹理和采样器状态作为参数，然后是饱和输入颜色（可根据需要使用线性空间或 Log C 空间），最后是一个参数向量，不过这次只有三个分量。

```
TEXTURE2D(_ColorGradingLUT);

float3 ApplyColorGradingLUT (float3 color) {
	return ApplyLut2D(
		TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp),
		saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color),
		_ColorGradingLUTParameters.xyz
	);
}
```

在这种情况下，参数值为 LUT 宽度除以 1、高度除以 1 和高度减 1。在最终绘制前设置这些参数，现在使用最终通过。

```
	buffer.SetGlobalVector(colorGradingLUTParametersId,
			new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f)
		);
		Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Final);
		buffer.ReleaseTemporaryRT(colorGradingLUTId);
```

### LUT Banding

虽然我们现在使用 LUT 进行调色和色调映射，但结果应该和以前一样。不过，由于 LUT 的分辨率有限，而且我们使用双线性插值对其进行采样，因此会将原本平滑的色彩过渡转换为线性色带。这在分辨率为 32 的 LUT 中通常并不明显，但在 HDR 颜色梯度极高的区域则会出现明显的条带。例如，在上一教程的色调映射场景中，强度为 200 的聚光灯照亮了均匀的白色表面。

通过暂时切换到 sampler_point_clamp 采样器状态，可以非常明显地看到条带现象。这将关闭 LUT 2D 切片内部的插值。相邻切片之间仍然存在插值，因为 ApplyLut2D 是通过采样两个切片并在它们之间进行混合来模拟 3D 纹理的。

如果条带过于明显，可以将分辨率提高到 64，但色彩的变化通常足以掩盖条带。如果您在非常微妙的色彩过渡中寻找带状伪影，您更有可能发现由于 8 位帧缓冲器的限制而产生的带状，这不是 LUT 造成的，可以通过抖动来缓解，但这是另一个话题。