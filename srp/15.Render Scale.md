# Render Scale

## Variable Resolution

应用程序以固定分辨率运行。有些应用程序允许通过设置菜单更改分辨率，但这需要完全重新初始化图形。更灵活的方法是保持应用程序的分辨率固定不变，但改变摄像头用于渲染的缓冲区大小。这将影响整个渲染过程，但最终绘制到帧缓冲区的过程除外，因为此时渲染结果将被重新缩放，以匹配应用的分辨率。

调整缓冲区大小可以减少需要处理的片段数量，从而提高性能。例如，这可以用于所有 3D 渲染，同时保持用户界面在全分辨率下的清晰度。还可以动态调整缩放比例，以保持可接受的帧速率。最后，我们还可以增加缓冲区的大小来进行超采样，从而减少有限分辨率造成的混叠伪影。最后一种方法也被称为 SSAA，即超采样抗锯齿。

### Buffer Settings

调整渲染比例会影响缓冲区大小，因此我们将在 CameraBufferSettings 中添加一个可配置的渲染比例滑块。应该有一个最小比例，我们将使用 0.1。我们还使用 2 作为最大值，因为如果我们使用单步双线性插值来重新缩放，超过这个值也不会改善图像质量。如果超过 2，图像质量就会下降，因为在向下采样到最终目标分辨率时，我们会跳过很多像素。

```
using UnityEngine;

[System.Serializable]
public struct CameraBufferSettings {

	…

	[Range(0.1f, 2f)]
	public float renderScale;
}
```

The default render scale should be set to 1 in `**CustomRenderPipelineAsset**`.

```
	CameraBufferSettings cameraBuffer = new CameraBufferSettings {
		allowHDR = true,
		renderScale = 1f
	};
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/render-scale/variable-resolution/render-scale-slider.png)

### Scaled Rendering

从现在起，我们还将在 CameraRenderer 中跟踪是否使用了缩放渲染。

```
	bool useHDR, useScaledRendering;
```

我们不希望配置的渲染比例影响到场景窗口，因为它们是用于编辑的。在适当的时候，通过在 PrepareForSceneWindow 中关闭缩放渲染来实现这一点。

```
	partial void PrepareForSceneWindow () {
		if (camera.cameraType == CameraType.SceneView) {
			ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
			useScaledRendering = false;
		}
	}
```

在调用渲染中的 PrepareForSceneWindow 之前，我们要确定是否要使用缩放渲染。在一个变量中跟踪当前的渲染比例，并检查它是否不为 1。

```
		float renderScale = bufferSettings.renderScale;
		useScaledRendering = renderScale != 1f;
		PrepareBuffer();
		PrepareForSceneWindow();
```

但我们应该比这更模糊一些，因为与 1 的微小偏差既不会产生视觉上的差异，也不会产生性能上的差异。因此，我们只能在至少有 1%差异的情况下使用缩放渲染。

```
		useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;
```

从现在起，当使用缩放渲染时，我们还必须使用中间缓冲区。因此请在设置中进行检查。

```
		useIntermediateBuffer = useScaledRendering ||
			useColorTexture || useDepthTexture || postFXStack.IsActive;
```

### Buffer Size

由于相机的缓冲区大小现在可能与相机组件指示的大小不同，我们必须跟踪最终使用的缓冲区大小。为此，我们可以使用一个 Vector2Int 字段。

```
Vector2Int bufferSize;
```

剔除成功后，在渲染中设置适当的缓冲区大小。如果采用缩放渲染，则会缩放摄像机像素的宽度和高度，并将结果转换为整数，然后向下舍入。

```
		if (!Cull(shadowSettings.maxDistance)) {
			return;
		}
		
		useHDR = bufferSettings.allowHDR && camera.allowHDR;
		if (useScaledRendering) {
			bufferSize.x = (int)(camera.pixelWidth * renderScale);
			bufferSize.y = (int)(camera.pixelHeight * renderScale);
		}
		else {
			bufferSize.x = camera.pixelWidth;
			bufferSize.y = camera.pixelHeight;
		}
```

在 "设置 "中为摄像机附件获取渲染纹理时，请使用此缓冲区大小。

```
			buffer.GetTemporaryRT(
				colorAttachmentId, bufferSize.x, bufferSize.y,
				0, FilterMode.Bilinear, useHDR ?
					RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
			);
			buffer.GetTemporaryRT(
				depthAttachmentId, bufferSize.x, bufferSize.y,
				32, FilterMode.Point, RenderTextureFormat.Depth
			);
```

And also for the color and depth textures, if they are needed.

```
	void CopyAttachments () {
		if (useColorTexture) {
			buffer.GetTemporaryRT(
				colorTextureId, bufferSize.x, bufferSize.y,
				0, FilterMode.Bilinear, useHDR ?
					RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
			);
			…
		}
		if (useDepthTexture) {
			buffer.GetTemporaryRT(
				depthTextureId, bufferSize.x, bufferSize.y,
				32, FilterMode.Point, RenderTextureFormat.Depth
			);
			…
		}
		…
	}
```

首先在不使用任何后期特效的情况下进行尝试。您可以放大游戏窗口，这样就能更好地看到单个像素，从而使调整后的渲染比例更加明显。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/render-scale/variable-resolution/scale-100.png)

缩小渲染比例会加快渲染速度，同时降低图像质量。而增大渲染比例则会起到相反的作用。请记住，在不使用后期特效时，调整渲染比例需要一个中间缓冲区和额外的绘制，因此会增加一些额外的工作。

根据目标缓冲区大小重新缩放的操作由最终绘制自动完成。最终，我们只需进行简单的双线性放大或缩小操作。唯一奇怪的结果涉及 HDR 值，它似乎会破坏插值。在上述截图中央黄色球体的高光部分就会出现这种情况。我们稍后再处理这个问题。

### Fragment Screen UV

调整渲染比例会带来一个错误：颜色和深度纹理的采样会出错。你可以从粒子变形中看到这一点，很明显，粒子变形最终使用了错误的屏幕空间 UV 坐标。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/render-scale/variable-resolution/distortion-incorrect.png)

出现这种情况的原因是，Unity 在 _ScreenParams 中输入的值与摄像头的像素尺寸相匹配，而不是我们所瞄准的缓冲区的尺寸。我们引入了另一个 _CameraBufferSize 向量来解决这个问题，该向量包含摄像机调整后尺寸的数据。

```
	static int
		bufferSizeId = Shader.PropertyToID("_CameraBufferSize"),
		colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
```

在确定缓冲区大小后，我们会在 "渲染 "中将这些值发送给 GPU。我们将使用与 Unity 对 _TexelSize 向量使用的相同格式，即在宽度和高度之后加上宽度和高度的倒数。

```
		if (useScaledRendering) {
			bufferSize.x = (int)(camera.pixelWidth * renderScale);
			bufferSize.y = (int)(camera.pixelHeight * renderScale);
		}
		else {
			bufferSize.x = camera.pixelWidth;
			bufferSize.y = camera.pixelHeight;
		}

		buffer.BeginSample(SampleName);
		buffer.SetGlobalVector(bufferSizeId, new Vector4(
			1f / bufferSize.x, 1f / bufferSize.y,
			bufferSize.x, bufferSize.y,
		));
		ExecuteBuffer();
```

Add the vector to *Fragment*.

```
TEXTURE2D(_CameraColorTexture);
TEXTURE2D(_CameraDepthTexture);

float4 _CameraBufferSize;
```

然后在 GetFragment 中用它代替 _ScreenParams。现在，我们还可以用乘法代替除法。

```
	f.screenUV = f.positionSS * _CameraBufferSize.xy;
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/render-scale/variable-resolution/distortion-correct.png)

### Scaled Post FX

调整渲染比例也会影响后期特效，否则就会导致意外缩放。最稳健的方法是始终使用相同的缓冲区大小，因此我们将把它作为新的第三个参数传递给 CameraRenderer.Render 中的 PostFXStack.Setup。

```
		postFXStack.Setup(
			context, camera, bufferSize, postFXSettings, useHDR, colorLUTResolution,
			cameraSettings.finalBlendMode
		);
```

`**PostFXStack**` now has to keep track of the buffer size.

```
	Vector2Int bufferSize;

	…
	
	public void Setup (
		ScriptableRenderContext context, Camera camera, Vector2Int bufferSize,
		PostFXSettings settings, bool useHDR, int colorLUTResolution,
		CameraSettings.FinalBlendMode finalBlendMode
	) {
		this.bufferSize = bufferSize;
		…
	}
```

这必须在 DoBoom 中使用，而不是直接使用相机的像素尺寸。

```
	bool DoBloom (int sourceId) {
		BloomSettings bloom = settings.Bloom;
		int width = bufferSize.x / 2, height = bufferSize.y / 2;
		
		…
		buffer.GetTemporaryRT(
			bloomResultId, bufferSize.x, bufferSize.y, 0,
			FilterMode.Bilinear, format
		);
		…
	}
```

由于绽放效果取决于分辨率，因此调整渲染比例会改变绽放效果的外观。这一点只需迭代几次绽放效果就能一目了然。减小渲染比例会使效果变大，而增大渲染比例会使效果变小。最大迭代次数的绽放效果似乎变化不大，但在调整渲染比例时，由于分辨率的变化，可能会出现脉冲。

特别是在渲染比例逐渐调整的情况下，最好是尽可能保持绽放的一致性。为此，我们可以根据摄像机而不是缓冲区大小来确定绽放金字塔的起始大小。让我们在 BloomSettings 设置中添加忽略渲染比例的切换选项，从而对其进行配置。

```
	public struct BloomSettings {

		public bool ignoreRenderScale;
		
		…
	}
```

如果忽略渲染比例，PostFXStack.DoBloom 将和之前一样，从摄像机像素尺寸的一半开始。这意味着它不再执行默认的降采样到一半分辨率，而是取决于渲染比例。最终的绽放结果仍应与按比例缩放的缓冲区大小相匹配，因此这将在最后引入另一个自动降采样或升采样步骤。

```
	bool DoBloom (int sourceId) {
		BloomSettings bloom = settings.Bloom;
		int width, height;
		if (bloom.ignoreRenderScale) {
			width = camera.pixelWidth / 2;
			height = camera.pixelHeight / 2;
		}
		else {
			width = bufferSize.x / 2;
			height = bufferSize.y / 2;
		}
		
		…
	}
```

在忽略渲染尺度的情况下，绽放效果现在更加一致，不过在非常小的尺度下，由于可使用的数据太少，看起来还是会有差异。

### Render Scale per Camera

我们还可以让每台摄像机使用不同的渲染比例。例如，单个摄像头可以始终以一半或两倍的分辨率进行渲染。这既可以是固定的--覆盖 RP 的全局渲染比例，也可以是在顶部应用的，这样就可以相对于全局渲染比例。

在 "摄像机设置 "中添加一个渲染比例滑块，其范围与 RP 资产相同。还可通过新的内部 RenderScaleMode 枚举类型，添加可设置为继承、乘法或覆盖的渲染比例模式。

```
	public enum RenderScaleMode { Inherit, Multiply, Override }

	public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

	[Range(0.1f, 2f)]
	public float renderScale = 1f;
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/render-scale/variable-resolution/render-scale-mode.png)

要应用每台摄像机的渲染比例，还需要为 CameraSettings 提供一个公共 GetRenderScale 方法，该方法包含一个渲染比例参数，并返回最终比例。因此，根据模式的不同，它可以返回相同的缩放比例，也可以返回相机的缩放比例，或者两者相乘

```
	public float GetRenderScale (float scale) {
		return
			renderScaleMode == RenderScaleMode.Inherit ? scale :
			renderScaleMode == RenderScaleMode.Override ? renderScale :
			scale * renderScale;
	}
```

在 CameraRenderer.Render 中调用该方法来获取最终的渲染比例，并将缓冲区设置中的比例传给它。

```
float renderScale = cameraSettings.GetRenderScale(bufferSettings.renderScale);
```

如果需要的话，我们也可以将最终的渲染比例控制在 0.1-2 的范围内。这样，我们就能防止比例过小或过大，以防它被倍增。

```
	if (useScaledRendering) {
			renderScale = Mathf.Clamp(renderScale, 0.1f, 2f);
			bufferSize.x = (int)(camera.pixelWidth * renderScale);
			bufferSize.y = (int)(camera.pixelHeight * renderScale);
		}
```

由于我们对所有渲染比例都使用相同的最小值和最大值，因此我们可以将它们定义为 CameraRenderer 的公共常量。我只展示了常量的定义，并没有在 CameraRenderer、CameraBufferSettings 和 CameraSettings 中替换 0.1f 和 2f 的值。

```
public const float renderScaleMin = 0.1f, renderScaleMax = 2f;
```

## Rescaling

当使用的渲染比例不是 1 时，除了最终绘制到摄像机的目标缓冲区外，其他所有操作都将在该比例下进行。如果没有使用后期特效，这只是一个简单的复制，并重新缩放至最终尺寸。使用后期特效时，最终绘制也会隐式地执行重新缩放。不过，在最终绘制过程中重新缩放有一些缺点。

### Current Approach

我们目前采用的重新缩放方法会产生不希望看到的副作用。首先，正如我们在前面已经注意到的，无论是升频还是降频，HDR 中亮度大于 1 的颜色总是会出现混叠现象。只有在低分辨率下进行插值，才能产生平滑的效果。HDR 插值会产生仍然大于 1 的结果，而这种结果根本不会出现混合。例如，0 和 10 的平均值是 5。在 LDR 中，0 和 1 的平均值看起来好像是 1，而我们本来希望是 0.5。

在最终处理过程中重新缩放的第二个问题是，色彩校正将应用于插值色彩而非原始色彩。这会带来不希望出现的色带。最明显的就是在阴影和高光之间插值时出现中间调。如果对中间色调进行非常强烈的色彩调整，例如将其调整为红色，就会非常明显。

### Rescaling in LDR

锐利的 HDR 边缘和色彩校正伪影都是在色彩校正和色调映射之前对 HDR 色彩进行插值造成的。因此，解决方案是在调整后的渲染比例下完成这两项工作，然后再通过另一个副本对 LDR 颜色进行重新缩放。在 PostFXStack 着色器中添加一个新的最终重缩放传递，以完成这最后一步。这是一个简单的复制传递，也具有可配置的混合模式。像往常一样，在 PostFXStack.Pass 枚举中添加一个条目。

```
		Pass {
			Name "Final Rescale"

			Blend [_FinalSrcBlend] [_FinalDstBlend]
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment CopyPassFragment
			ENDHLSL
		}
```

现在我们有了两个最终通道，这就要求我们为 DrawFinal 添加一个通道参数。

```
	void DrawFinal (RenderTargetIdentifier from, Pass pass) {
		…
		buffer.DrawProcedural(
			Matrix4x4.identity, settings.Material,
			(int)pass, MeshTopology.Triangles, 3
		);
	}
```

现在，我们在 DoColorGradingAndToneMapping 中使用哪种方法，取决于我们是否在使用调整后的渲染比例。我们可以通过比较缓冲区大小和摄像机像素大小来检查这一点。检查宽度就足够了。如果两者相等，我们就会像之前一样绘制最终通道，现在我们明确地将 Pass.Final 作为参数。

```
	void DoColorGradingAndToneMapping (int sourceId) {
		…

		if (bufferSize.x == camera.pixelWidth) {
			DrawFinal(sourceId, Pass.Final);
		}
		else {}
		
		buffer.ReleaseTemporaryRT(colorGradingLUTId);
	}
```

但如果我们需要调整大小，就必须绘制两次。首先获取一个与当前缓冲区大小相匹配的新临时渲染纹理。由于我们在其中存储了 LDR 颜色，因此使用默认的渲染纹理格式就足够了。然后用最终通道执行常规绘制，并将最终混合模式设置为 One Zero。然后使用最终重缩放通道执行最终绘制，并释放中间缓冲区。

```
		if (bufferSize.x == camera.pixelWidth) {
			DrawFinal(sourceId, Pass.Final);
		}
		else {
			buffer.SetGlobalFloat(finalSrcBlendId, 1f);
			buffer.SetGlobalFloat(finalDstBlendId, 0f);
			buffer.GetTemporaryRT(
				finalResultId, bufferSize.x, bufferSize.y, 0,
				FilterMode.Bilinear, RenderTextureFormat.Default
			);
			Draw(sourceId, finalResultId, Pass.Final);
			DrawFinal(finalResultId, Pass.FinalRescale);
			buffer.ReleaseTemporaryRT(finalResultId);
		}
```

With these changes HDR colors also appear to interpolate correctly.

And color grading no longer introduces color bands that don't exist at render scale 1.

请注意，这只能在使用后期特效时解决问题。在其他情况下不会进行调色，我们假设也不会进行 HDR。

### Bicubic Sampling

当降低渲染比例时，图像会变得块状。我们添加了一个选项，使用双三次上采样来改善绽放的质量，在重新缩放至最终渲染目标时，我们也可以这样做。在 CameraBufferSettings（相机缓冲设置）中添加一个切换选项。

```
	public bool bicubicRescaling;
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/render-scale/rescaling/bicubic-rescaling-toggle.png)

为 PostFXStackPasses 添加一个新的 FinalPassFragmentRescale 函数，以及一个 _CopyBicubic 属性，用于控制使用双三次采样还是常规采样。

```
bool _CopyBicubic;

float4 FinalPassFragmentRescale (Varyings input) : SV_TARGET {
	if (_CopyBicubic) {
		return GetSourceBicubic(input.screenUV);
	}
	else {
		return GetSource(input.screenUV);
	}
}
```

更改最终的缩放传递，使用此函数而不是复制函数。

```
	#pragma fragment FinalPassFragmentRescale
```

在 PostFXStack 中添加属性标识符，并使其跟踪是否启用了双三次方重缩放（通过设置的一个新参数进行配置）。

```
	int
		copyBicubicId = Shader.PropertyToID("_CopyBicubic"),
		finalResultId = Shader.PropertyToID("_FinalResult"),
		…
	
	bool bicubicRescaling;

	…

	public void Setup (
		ScriptableRenderContext context, Camera camera, Vector2Int bufferSize,
		PostFXSettings settings, bool useHDR, int colorLUTResolution,
		CameraSettings.FinalBlendMode finalBlendMode, bool bicubicRescaling
	) {
		this.bicubicRescaling = bicubicRescaling;
		…
	}
```

Pass the buffer settings along in `**CameraRenderer**.Render`.

```
		postFXStack.Setup(
			context, camera, bufferSize, postFXSettings, useHDR, colorLUTResolution,
			cameraSettings.finalBlendMode, bufferSettings.bicubicRescaling
		);
```

然后在 PostFXStack.DoColorGradingAndToneMapping 中适当设置着色器属性，然后再执行最终的重缩放。

```
		buffer.SetGlobalFloat(copyBicubicId, bicubicRescaling ? 1f : 0f);
			DrawFinal(finalResultId, Pass.FinalRescale);
```

### Only Bicubic Upscaling

双三次插值在升频时总能改善画质，但在降频时差异就不那么明显了。在渲染比例为 2 时，双三次方重缩放总是没用的，因为每个最终像素都是四个像素的平均值，与双线性插值完全相同。因此，让我们将 BufferSettings 中的切换按钮替换为三种模式之间的选择：关闭、仅向上和上下。

```
	public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }

	public BicubicRescalingMode bicubicRescaling;
```

更改 PostFXStack 中的类型以匹配。

```
	CameraBufferSettings.BicubicRescalingMode bicubicRescaling;

	…

	public void Setup (
		…
		CameraBufferSettings.BicubicRescalingMode bicubicRescaling
	) { … }
```

最后，修改 DoColorGradingAndToneMapping（色彩分级和色调映射），使双三次采样仅用于上下模式，或者在缩小渲染比例的情况下仅用于上下模式。

```
			bool bicubicSampling =
				bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
				bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpOnly &&
				bufferSize.x < camera.pixelWidth;
			buffer.SetGlobalFloat(copyBicubicId, bicubicSampling ? 1f : 0f);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/render-scale/rescaling/bicubic-rescaling-up-only.png)