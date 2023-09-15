# Post Processing

[TOC]



## Post-FX Stack

大多数情况下，渲染的图像不会直接显示出来。图像会进行后处理，应用各种效果，简称FX。常见的FX包括泛光(Bloom)、颜色分级(Color Grading)、景深(depth of filed)、运动模糊(Motion blur)和色调映射(ToneMapping)。这些FX会作为一个堆栈依次应用在图像上。在本教程中，我们将创建一个简单的后处理FX堆栈，最初只支持泛光效果。

### Settings Asset

一个项目可能需要多个后处理FX堆栈配置，因此我们首先创建一个PostFXSettings资产类型，用于存储堆栈的设置。

```
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject { }
```

在本教程中，我们将使用一个单独的堆栈，并通过向CustomRenderPipelineAsset添加一个配置选项来使其可供RP使用，然后将其传递给RP的构造函数。

```
	[SerializeField]
	PostFXSettings postFXSettings = default;

	protected override RenderPipeline CreatePipeline () {
		return new CustomRenderPipeline(
			useDynamicBatching, useGPUInstancing, useSRPBatcher,
			useLightsPerObject, shadows, postFXSettings
		);
	}
```

CustomRenderPipeline需要跟踪FX设置，并在渲染过程中将它们与其他设置一起传递给摄像机渲染器。

```
	PostFXSettings postFXSettings;

	public CustomRenderPipeline (
		bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,
		bool useLightsPerObject, ShadowSettings shadowSettings,
		PostFXSettings postFXSettings
	) {
		this.postFXSettings = postFXSettings;
		…
	}
	
	…
	
	protected override void Render (
		ScriptableRenderContext context, List<Camera> cameras
	) {
		for (int i = 0; i < cameras.Count; i++) {
			renderer.Render(
				context, cameras[i],
				useDynamicBatching, useGPUInstancing, useLightsPerObject,
				shadowSettings, postFXSettings
			);
		}
	}
```

CameraRenderer.Render initially does nothing with the settings, as we don't have a stack yet.

```
	public void Render (
		ScriptableRenderContext context, Camera camera,
		bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
		ShadowSettings shadowSettings, PostFXSettings postFXSettings
	) { … }
```

Now we can create an empty post-FX settings asset and assign it to the pipeline asset.

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/post-fx-stack/post-fx-settings-assigned.png)

### Stack Object

我们将使用与用于光照和阴影的方法相同的方法来处理堆栈。我们创建一个类来跟踪缓冲区、上下文、摄像机和后处理FX设置，然后提供一个公共的Setup方法来初始化它们。

```
using UnityEngine;
using UnityEngine.Rendering;

public class PostFXStack {

	const string bufferName = "Post FX";

	CommandBuffer buffer = new CommandBuffer {
		name = bufferName
	};

	ScriptableRenderContext context;
	
	Camera camera;

	PostFXSettings settings;

	public void Setup (
		ScriptableRenderContext context, Camera camera, PostFXSettings settings
	) {
		this.context = context;
		this.camera = camera;
		this.settings = settings;
	}
}
```

接下来，添加一个公共属性来指示堆栈是否处于活动状态，只有在存在相应的设置时才会激活。这样的设计思路是，如果没有提供设置，就应该跳过后处理。

```
	public bool IsActive => settings != null;
```

最后我们需要一个公共的Render方法来渲染堆栈。将效果应用于整个图像只需绘制一个覆盖整个图像的矩形，使用适当的着色器。目前我们还没有着色器，所以我们将简单地复制到此为止渲染的内容到摄像机的帧缓冲区。这可以通过在命令缓冲区上调用Blit来完成，传递源和目标的标识符。这些标识符可以以多种格式提供。我们将使用一个整数作为源，为此我们将添加一个参数，然后使用BuiltinRenderTextureType.CameraTarget作为目标。然后我们执行并清除缓冲区。

```
	public void Render (int sourceId) {
		buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
```

在这种情况下，我们不需要手动开始和结束缓冲区采样，因为我们不需要调用ClearRenderTarget，因为我们完全替换了目标上的内容。

### Using the Stack

CameraRenderer现在需要一个堆栈实例，并在Render中调用它的Setup方法，就像它对其Lighting对象所做的那样。

```
	Lighting lighting = new Lighting();

	PostFXStack postFXStack = new PostFXStack();

	public void Render (…) {
		…
		lighting.Setup(
			context, cullingResults, shadowSettings, useLightsPerObject
		);
		postFXStack.Setup(context, camera, postFXSettings);
		buffer.EndSample(SampleName);
		Setup();
		…
	}
```

到目前为止，我们总是直接渲染到摄像机的帧缓冲区，这是用于显示或配置的渲染纹理之一。我们无法直接控制它们，只能对其进行写操作。因此，为了为活动堆栈提供源纹理，我们必须使用渲染纹理作为摄像机的中间帧缓冲区。获取一个渲染纹理并将其设置为渲染目标的方式与阴影图类似，只是我们将使用RenderTextureFormat.Default格式。在清除渲染目标之前执行此操作。

```
	static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
	
	…
	
	void Setup () {
		context.SetupCameraProperties(camera);
		CameraClearFlags flags = camera.clearFlags;

		if (postFXStack.IsActive) {
			buffer.GetTemporaryRT(
				frameBufferId, camera.pixelWidth, camera.pixelHeight,
				32, FilterMode.Bilinear, RenderTextureFormat.Default
			);
			buffer.SetRenderTarget(
				frameBufferId,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
			);
		}

		buffer.ClearRenderTarget(…);
		buffer.BeginSample(SampleName);
		ExecuteBuffer();
	}
```

还要添加一个Cleanup方法来释放纹理，如果我们有一个活动的堆栈。我们也可以将光照的清理移动到这里。

```
	void Cleanup () {
		lighting.Cleanup();
		if (postFXStack.IsActive) {
			buffer.ReleaseTemporaryRT(frameBufferId);
		}
	}
```

在提交之前，在Render的最后调用Cleanup。在此之前，如果堆栈处于活动状态，直接渲染堆栈。

```
	public void Render (…) {
		…
		DrawGizmos();
		if (postFXStack.IsActive) {
			postFXStack.Render(frameBufferId);
		}
		Cleanup();
		//lighting.Cleanup();
		Submit();
	}
```

在这一点上，结果看起来不应该有任何不同，但是已经添加了一个额外的绘制步骤，从中间帧缓冲区复制到最终帧缓冲区。它在帧调试器中列为"Draw Dynamic"。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/post-fx-stack/rendering.png)

### Forced Clearing

当绘制到中间帧缓冲区时，我们渲染到一个填充了任意数据的纹理。为了防止随机结果，在堆栈处于活动状态时，始终清除深度和颜色。

```
		CameraClearFlags flags = camera.clearFlags;

		if (postFXStack.IsActive) {
			if (flags > CameraClearFlags.Color) {
				flags = CameraClearFlags.Color;
			}
			…
		}

		buffer.ClearRenderTarget(…);
```

请注意，这使得在使用后处理FX堆栈时，不清除的情况下使一个摄像机在另一个摄像机之上渲染变得不可能。有办法解决这个问题，但这超出了本教程的范围。

### Gizmos

目前，我们将所有的gizmos同时绘制，但在应该在后处理FX之前和之后渲染的gizmos之间存在区别。因此，让我们将DrawGizmos方法分为两部分。

```
	partial void DrawGizmosBeforeFX ();

	partial void DrawGizmosAfterFX ();
	
	…
	
#if UNITY_EDITOR
	
	…
						
	partial void DrawGizmosBeforeFX () {
		if (Handles.ShouldRenderGizmos()) {
			context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
			//context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
		}
	}

	partial void DrawGizmosAfterFX () {
		if (Handles.ShouldRenderGizmos()) {
			context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
		}
	}
```

Then we can draw them at the correct time in `Render`.

```
		//DrawGizmos();
		DrawGizmosBeforeFX();
		if (postFXStack.IsActive) {
			postFXStack.Render(frameBufferId);
		}
		DrawGizmosAfterFX();
```

请注意，当使用3D图标作为gizmos时，当堆栈处于活动状态时，它们不再被对象遮挡。这是因为场景窗口依赖于原始帧缓冲区的深度数据，而我们不使用它。我们将在将来的教程中介绍深度与后处理FX的结合使用。

![with](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/post-fx-stack/gizmos-with-fx.png)

![without](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/post-fx-stack/gizmos-without-fx.png)

### Custom Drawing

目前我们使用的Blit方法绘制了一个覆盖整个屏幕空间的四边形网格，即两个三角形。但我们可以通过只绘制一个单独的三角形来获得相同的结果，这要少一些工作。我们甚至不需要将一个单三角形的网格发送到GPU，我们可以以程序方式生成它。

Does it make a significant difference?

使用单个三角形相对于使用两个三角形的四边形网格确实会产生一些显著差异。显而易见的好处是将顶点数量从六个减少到三个。然而，更显著的差异是它消除了四边形的两个三角形相交的对角线。因为GPU以小块并行渲染片段，一些片段最终会在三角形的边缘浪费掉。由于四边形有两个三角形，对角线上的片段块会被渲染两次，这是低效的。此外，渲染单个三角形可以具有更好的局部缓存一致性。因此，使用单个三角形可能会在性能和渲染效率上有所改进。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/post-fx-stack/quad-block-rendering.png)

创建一个名为PostFXStackPasses.hlsl的文件，并将其放在RP的Shaders文件夹中。我们将把堆栈的所有通道都放在这里。首先我们要在其中定义Varyings结构体，它只需要包含剪裁空间位置和屏幕空间UV坐标。

```
#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};
```

接下来，创建一个默认的顶点通道，只有一个顶点标识符作为参数。它是一个无符号整数（uint），带有SV_VertexID语义。使用ID生成顶点位置和UV坐标。X坐标为-1、-1、3。Y坐标为-1、3、-1。为了使可见的UV坐标覆盖0-1范围，使用0、0、2作为U坐标，0、2、0作为V坐标。

```
Varyings DefaultPassVertex (uint vertexID : SV_VertexID) {
	Varyings output;
	output.positionCS = float4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.screenUV = float2(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);
	return output;
}
```

添加一个用于简单复制的片段通道，最初返回UV坐标，用于调试目的。

```
float4 CopyPassFragment (Varyings input) : SV_TARGET {
	return float4(input.screenUV, 0.0, 1.0);
}
```

在相同的文件夹中创建一个配套的着色器文件。所有通道都不使用剔除和忽略深度，所以我们可以直接将这些指令放在Subshader块中。我们还总是包含我们的Common和PostFXStackPasses文件。目前它的唯一通道是用于复制的，使用我们创建的顶点和片段函数。我们还可以使用Name指令为其命名，这在将多个通道合并到同一个着色器中时很方便，因为帧调试器将使用它作为通道标签，而不是数字。最后，将其菜单项放在Hidden文件夹下，以便在为材质选择着色器时不会显示它。

```
Shader "Hidden/Custom RP/Post FX Stack" {
	
	SubShader {
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "PostFXStackPasses.hlsl"
		ENDHLSL

		Pass {
			Name "Copy"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment CopyPassFragment
			ENDHLSL
		}
	}
}
```

We'll simply manually link the shader to our stack via its settings.

```
public class PostFXSettings : ScriptableObject {

	[SerializeField]
	Shader shader = default;
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/post-fx-stack/post-fx-shader.png)

但是在渲染时我们需要一个材质，所以添加一个公共属性，我们可以从设置资产中直接获取材质。我们将按需创建它，并设置为在项目中隐藏和不保存。另外，材质不能与资产一起序列化，因为它是按需创建的。

```
	[System.NonSerialized]
	Material material;

	public Material Material {
		get {
			if (material == null && shader != null) {
				material = new Material(shader);
				material.hideFlags = HideFlags.HideAndDontSave;
			}
			return material;
		}
	}
```

由于按名称而不是数字来引用通道很方便，因此在PostFXStack内部创建一个Pass枚举，最初只包含复制通道。

```
	enum Pass {
		Copy
	}
```

现在我们可以定义我们自己的Draw方法。为它添加两个RenderTargetIdentifier参数，用于指示从哪里绘制到哪里，以及一个通道参数。在其中，通过_PostFXSource纹理提供源，将目标用作渲染目标，然后绘制三角形。我们通过在缓冲区上调用DrawProcedural来实现，使用一个未使用的矩阵、堆栈材质和通道作为参数。之后是另外两个参数。首先是我们正在绘制的形状类型，即MeshTopology.Triangles。第二个是我们想要的顶点数，对于一个单独的三角形来说是三个。

```
	int fxSourceId = Shader.PropertyToID("_PostFXSource");
	
	…
	
	void Draw (
		RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass
	) {
		buffer.SetGlobalTexture(fxSourceId, from);
		buffer.SetRenderTarget(
			to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
		);
		buffer.DrawProcedural(
			Matrix4x4.identity, settings.Material, (int)pass,
			MeshTopology.Triangles, 3
		);
	}
```

Finally, replace the invocation of `Blit` with our own method.

```
		//buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
		Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
```

### Don't Always Apply FX

现在我们应该在场景窗口、游戏窗口中看到屏幕空间UV坐标的出现。甚至在材质预览和反射探针中，一旦它们刷新，也能看到这些坐标。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/post-fx-stack/reflection-probe-fx.png)

这个想法是后处理效果只应用于正确的摄像机，而不是其他东西。我们可以通过在PostFXStack.Setup中检查是否有游戏或场景摄像机来强制执行这一点。如果没有，我们将设置为null的设置，这将禁用该摄像机的堆栈。

```
		this.settings =
			camera.cameraType <= CameraType.SceneView ? settings : null;
```

除此之外，还可以通过场景窗口的工具栏中的效果下拉菜单来切换场景窗口中的后处理效果。可以同时打开多个场景窗口，可以单独启用或禁用后处理效果。为了支持这一点，创建一个PostFXStack的编辑器部分类，其中包含一个ApplySceneViewState方法，它在构建中什么都不做。它的编辑器版本检查是否正在处理场景视图摄像机，如果是的话，会在当前绘制的场景视图状态中禁用堆栈，如果图像效果已禁用。

```
using UnityEditor;
using UnityEngine;

partial class PostFXStack {

	partial void ApplySceneViewState ();

#if UNITY_EDITOR

	partial void ApplySceneViewState () {
		if (
			camera.cameraType == CameraType.SceneView &&
			!SceneView.currentDrawingSceneView.sceneViewState.showImageEffects
		) {
			settings = null;
		}
	}

#endif
}
```

Invoke this method at the end of `Setup`.

```
public partial class PostFXStack {

	…

	public void Setup (…) {
		…
		ApplySceneViewState();
	}
```

### Copying

完成堆栈的最后一步是使我们的复制通道返回源颜色。为此创建一个GetSource函数，它执行采样操作。我们将始终使用线性夹取采样器，以便可以明确声明。

```
TEXTURE2D(_PostFXSource);
SAMPLER(sampler_linear_clamp);

float4 GetSource(float2 screenUV) {
	return SAMPLE_TEXTURE2D(_PostFXSource, sampler_linear_clamp, screenUV);
}

float4 CopyPassFragment (Varyings input) : SV_TARGET {
	return GetSource(input.screenUV);
}
```

因为我们的缓冲区永远不会有Mip贴图，所以我们可以通过用SAMPLE_TEXTURE2D_LOD替换SAMPLE_TEXTURE2D来绕过自动Mip贴图选择，添加一个额外的参数来强制选择Mip贴图级别为零。

```
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
```

最后，我们获得了原始图像，但在某些情况下它是颠倒的，通常在场景窗口中出现。这取决于图形API和源与目标的类型。这是因为某些图形API的纹理V坐标从顶部开始，而其他的从底部开始。Unity通常会隐藏这一点，但在涉及渲染纹理的所有情况下都无法隐藏。幸运的是，Unity通过_ProjectionParams向量的X分量指示是否需要手动翻转，我们应该在UnityInput中定义这个向量。

```
float4 _ProjectionParams;
```

If the value is negative we have to flip the V coordinate in `DefaultPassVertex`.

```
Varyings DefaultPassVertex (uint vertexID : SV_VertexID) {
	…
	if (_ProjectionParams.x < 0.0) {
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}
```

## Bloom

泛光后处理效果用于使物体发光。这在物理学上有一定的基础，但经典的泛光效果更多是一种艺术效果，而不是现实主义。非现实的泛光效果非常明显，因此是一个很好的效果，用来演示我们的后处理FX堆栈是否正常工作。在下一个教程中，我们将介绍更加逼真的泛光效果，当我们讨论HDR渲染时。而现在，我们将以LDR泛光发光效果为目标。

### Bloom Pyramid

泛光代表颜色的散射，可以通过对图像进行模糊来实现。明亮的像素将渗入相邻的较暗像素中，因此看起来会发光。最简单和最快速的方式来模糊纹理是将其复制到另一个宽度和高度减半的纹理中。每个复制通道的采样最终会在四个源像素之间采样。使用双线性过滤，这将平均2×2像素块。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/2x2-bilinear-downsampling.png)

只进行一次模糊效果会很轻微。因此，我们重复这个过程，逐渐降采样直到达到所需级别，有效地构建了一个纹理金字塔。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/texture-pyramid.png)

我们需要跟踪堆栈中的纹理，但纹理的数量取决于金字塔中有多少级别，这取决于源图像的大小。让我们在PostFXStack中定义最多十六个级别，这足以将一个65,536×65,526的纹理缩小到一个像素。

```
	const int maxBloomPyramidLevels = 16;
```

为了跟踪金字塔中的纹理，我们需要纹理标识符。我们将使用属性名称 _BloomPyramid0、_BloomPyramid1 等等。但是，让我们不要显式地编写所有这些十六个名称。相反，我们将在构造方法中获取这些标识符，并只跟踪第一个。这是因为 Shader.PropertyToID 会根据请求的新属性名称的顺序按顺序分配标识符。我们只需要确保一次请求所有标识符，因为这些数字在应用程序会话中是固定的，无论是在编辑器中还是在构建中。

```
	int bloomPyramidId;
	
	…
	
	public PostFXStack () {
		bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
		for (int i = 1; i < maxBloomPyramidLevels; i++) {
			Shader.PropertyToID("_BloomPyramid" + i);
		}
	}
```

现在创建一个DoBloom方法，用于对给定的源标识符应用泛光效果。首先，将摄像机的像素宽度和高度减半，并选择默认的渲染纹理格式。最初，我们将从源复制到金字塔中的第一个纹理。跟踪这些标识符。

```
	void DoBloom (int sourceId) {
		buffer.BeginSample("Bloom");
		int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
		RenderTextureFormat format = RenderTextureFormat.Default;
		int fromId = sourceId, toId = bloomPyramidId;
		buffer.EndSample("Bloom");
	}
```

然后循环遍历所有的金字塔级别。每次迭代，首先检查级别是否会变得退化。如果是这样，我们会在那一点停止。如果不是，获取一个新的渲染纹理，复制到它，将其设置为新的源，递增目标，然后再次将维度减半。将循环迭代变量声明在循环外部，因为我们后面会用到它。

```
		int fromId = sourceId, toId = bloomPyramidId;

		int i;
		for (i = 0; i < maxBloomPyramidLevels; i++) {
			if (height < 1 || width < 1) {
				break;
			}
			buffer.GetTemporaryRT(
				toId, width, height, 0, FilterMode.Bilinear, format
			);
			Draw(fromId, toId, Pass.Copy);
			fromId = toId;
			toId += 1;
			width /= 2;
			height /= 2;
		}
```

一旦金字塔完成，将最终结果复制到摄像机目标。然后递减迭代器并反向循环，释放我们占用的所有纹理。

```
		for (i = 0; i < maxBloomPyramidLevels; i++) { … }

		Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);

		for (i -= 1; i >= 0; i--) {
			buffer.ReleaseTemporaryRT(bloomPyramidId + i);
		}
		buffer.EndSample("Bloom");
```

现在我们可以用泛光效果替换Render中的简单复制。

```
	public void Render (int sourceId) {
		//Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
		DoBloom(sourceId);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
```

### Configurable Bloom

现在我们模糊得太多，最终结果几乎是均匀的。您可以通过帧调试器检查中间步骤。这些步骤似乎更适合作为终点，因此让我们可以提前停止。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/progressive-downsampling.png)

我们可以以两种方式实现这一点。首先，我们可以限制模糊迭代的数量。其次，我们可以将降分辨率限制设置为较高的值。让我们通过在PostFXSettings中添加一个名为BloomSettings的配置结构体来支持这两种方式，并为它们提供选项。通过一个getter属性公开它。

```
	[System.Serializable]
	public struct BloomSettings {

		[Range(0f, 16f)]
		public int maxIterations;

		[Min(1f)]
		public int downscaleLimit;
	}

	[SerializeField]
	BloomSettings bloom = default;

	public BloomSettings Bloom => bloom;
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/bloom-settings.png)

Have PostFXStack.DoBloom use these settings to limit itself.

```
		PostFXSettings.BloomSettings bloom = settings.Bloom;
		int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
		RenderTextureFormat format = RenderTextureFormat.Default;
		int fromId = sourceId, toId = bloomPyramidId;

		int i;
		for (i = 0; i < bloom.maxIterations; i++) {
			if (height < bloom.downscaleLimit || width < bloom.downscaleLimit) {
				break;
			}
			buffer.GetTemporaryRT(
				toId, width, height, 0, FilterMode.Bilinear, format
			);
			…
		}
```

![3 steps](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/down-3.png)

![5 steps](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/down-5.png)

### Gaussian Filtering

使用小型2×2滤波器进行降采样会产生非常块状的结果。通过使用更大的滤波器核心，例如近似的9×9高斯滤波器，可以大大改善效果。如果我们将这个操作与双线性降采样相结合，就可以将其有效地扩展为18×18。这是Universal RP和HDRP用于它们的泛光效果的方法。

尽管这个操作混合了81个样本，但它是可分离的，这意味着它可以分为水平和垂直两个通道，每个通道混合九个样本的单行或单列。因此，我们只需要采样18次，但每次迭代需要两次绘制。

**How does a separable filter work?**

这是一个可以使用对称的行向量与其转置相乘来创建的滤波器。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/separable-filter.png)

让我们从水平通道开始。在PostFXStackPasses中为此创建一个新的BloomHorizontalPassFragment函数。它会累积在当前UV坐标上居中的九个样本。同时，我们也会进行降采样，因此每个偏移步骤都是源纹理像素宽度的两倍。从左侧开始的样本权重为0.01621622、0.05405405、0.12162162、0.19459459，然后中心为0.22702703，其他侧反向。

```
float4 _PostFXSource_TexelSize;

float4 GetSourceTexelSize () {
	return _PostFXSource_TexelSize;
}

…

float4 BloomHorizontalPassFragment (Varyings input) : SV_TARGET {
	float3 color = 0.0;
	float offsets[] = {
		-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
	};
	float weights[] = {
		0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
	};
	for (int i = 0; i < 9; i++) {
		float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
		color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
	}
	return float4(color, 1.0);
}
```

**Where do those weights come from?**

这些权重是从帕斯卡三角派生而来的。对于一个正确的9×9高斯滤波器，我们将选择三角形的第九行，即1 8 28 56 70 56 28 8 1。但这使得滤波器边缘的样本贡献过于微弱，难以察觉，因此我们下降到第十三行并切掉其边缘，得到66 220 495 792 924 792 495 220 66。这些数字的总和是4070，所以将每个数字除以4070以获得最终的权重。

还将此通道添加到PostFXStack着色器中。我将它放在复制通道之上，以保持它们按字母顺序排列。

```
		Pass {
			Name "Bloom Horizontal"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment BloomHorizontalPassFragment
			ENDHLSL
		}
```

Add an entry for it to the `**PostFXStack**.**Pass**` enum as well, again in the same order.

Now we can use the bloom-horizontal pass when downsampling in `DoBloom`.

```
	Draw(fromId, toId, Pass.BloomHorizontal);
```

![3 steps](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/down-horizontal-3.png)

![5 steps](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/down-horizontal-5.png)

在这一点上，结果显然在水平方向被拉伸，但看起来很有希望。我们可以通过复制BloomHorizontalPassFragment、重命名它，并切换从行到列来创建垂直通道。在第一个通道中我们进行了降采样，但这次我们保持相同的大小来完成高斯滤波，因此纹理大小的偏移不应该加倍。

```
float4 BloomVerticalPassFragment (Varyings input) : SV_TARGET {
	float3 color = 0.0;
	float offsets[] = {
		-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
	};
	float weights[] = {
		0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
	};
	for (int i = 0; i < 9; i++) {
		float offset = offsets[i] * GetSourceTexelSize().y;
		color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
	}
	return float4(color, 1.0);
}
```


也为它添加一个通道和枚举条目。从现在开始，我不会再显示这些步骤了。

现在，我们需要在每个金字塔级别的中间添加一个额外的步骤，为此我们还需要保留纹理标识符。我们可以通过在PostFXStack构造函数中简单地将循环限制加倍来实现。由于我们还没有引入其他着色器属性名称，标识符都将按顺序排列，否则需要重新启动Unity。

```
	public PostFXStack () {
		bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
		for (int i = 1; i < maxBloomPyramidLevels * 2; i++) {
			Shader.PropertyToID("_BloomPyramid" + i);
		}
	}
```

在DoBloom中，目标标识符现在必须从更高的值开始，并在每次降采样步骤后增加两个。然后，中间的纹理可以放在中间。水平绘制进入中间，然后是垂直绘制到目标。我们还必须释放额外的纹理，最简单的方法是从金字塔的最后一个源纹理开始向后处理。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/hv-downsamling.png)

```
	void DoBloom (int sourceId) {
		…
		int fromId = sourceId, toId = bloomPyramidId + 1;
		
		for (i = 0; i < bloom.maxIterations; i++) {
			…
			int midId = toId - 1;
			buffer.GetTemporaryRT(
				midId, width, height, 0, FilterMode.Bilinear, format
			);
			buffer.GetTemporaryRT(
				toId, width, height, 0, FilterMode.Bilinear, format
			);
			Draw(fromId, midId, Pass.BloomHorizontal);
			Draw(midId, toId, Pass.BloomVertical);
			fromId = toId;
			toId += 2;
			…
		}

		Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);

		for (i -= 1; i >= 0; i--) {
			buffer.ReleaseTemporaryRT(fromId);
			buffer.ReleaseTemporaryRT(fromId - 1);
			fromId -= 2;
		}
		buffer.EndSample("Bloom");
	}
```

![3 steps](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/down-gaussian-3.png)

![5 steps](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/down-gaussian-5.png)

我们的降采样滤波器现在已经完成，比简单的双线性过滤看起来好多了，但需要更多的纹理采样。幸运的是，我们可以通过使用双线性过滤在适当的偏移位置采样高斯采样点之间来减少一些采样量。这将九个采样减少到只有五个。我们可以在BloomVerticalPassFragment中使用这个技巧。偏移量在两个方向上变为3.23076923和1.38461538，权重为0.07027027和0.31621622。

```
	float offsets[] = {
		-3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
	};
	float weights[] = {
		0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
	};
	for (int i = 0; i < 5; i++) {
		float offset = offsets[i] * GetSourceTexelSize().y;
		color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
	}
```

我们不能在BloomHorizontalPassFragment中这样做，因为在该通道中我们已经使用双线性过滤进行降采样。它的九个样本中的每一个都平均了2×2个源像素。

### Additive Blurring

使用泛光金字塔的顶部作为最终图像会产生均匀的混合效果，看起来没有任何东西在发光。我们可以通过逐渐上采样然后再降采样回到金字塔中，将所有级别累积到单个图像中来获得所需的结果。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/additive-progressive-upsampling.png)

我们可以使用加法混合来组合两个图像，但让我们对所有通道使用相同的混合模式，而是添加第二个源纹理。在PostFXStack中为它声明一个标识符。

```
	int
		fxSourceId = Shader.PropertyToID("_PostFXSource"),
		fxSource2Id = Shader.PropertyToID("_PostFXSource2");
```

然后，不再在DoBloom完成金字塔后直接进行最终绘制。相反，释放用于上一次迭代的水平绘制的纹理，并将目标设置为用于上一级水平绘制的纹理。

```
		//Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
		buffer.ReleaseTemporaryRT(fromId - 1);
		toId -= 5;
```

当我们回到时，每次迭代都以相反的方向进行绘制，将每个级别的结果作为第二个源。这只适用于第一级，所以我们必须提前停止一步。之后，使用原始图像作为辅助源绘制到最终目标。

```
	for (i -= 1; i > 0; i--) {
			buffer.SetGlobalTexture(fxSource2Id, toId + 1);
			Draw(fromId, toId, Pass.Copy);
			buffer.ReleaseTemporaryRT(fromId);
			buffer.ReleaseTemporaryRT(toId + 1);
			fromId = toId;
			toId -= 2;
		}

		buffer.SetGlobalTexture(fxSource2Id, sourceId);
		Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
		buffer.ReleaseTemporaryRT(fromId);
		buffer.EndSample("Bloom");
```

为了使这个工作正常，我们需要将辅助源提供给着色器通道。

```
TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);
SAMPLER(sampler_linear_clamp);

…

float4 GetSource2(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);
}
```

并引入一个新的泛光组合通道，对两个纹理进行采样和相加。和之前一样，我只展示了片段程序，没有展示新的着色器通道或新的枚举条目。

```
float4 BloomCombinePassFragment (Varyings input) : SV_TARGET {
	float3 lowRes = GetSource(input.screenUV).rgb;
	float3 highRes = GetSource2(input.screenUV).rgb;
	return float4(lowRes + highRes, 1.0);
}
```

Use the new pass when upsampling.

```
		for (i -= 1; i > 0; i--) {
			buffer.SetGlobalTexture(fxSource2Id, toId + 1);
			Draw(fromId, toId, Pass.BloomCombine);
			…
		}

		buffer.SetGlobalTexture(fxSource2Id, sourceId);
		Draw(
			bloomPyramidId, BuiltinRenderTextureType.CameraTarget,
			Pass.BloomCombine
		);
```

![3 steps](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/additive-3.png)

最终，我们有了一个看起来所有东西都在发光的效果。但是，我们的新方法只在至少有两个迭代时才有效。如果我们最终只执行了一个迭代，那么我们应该跳过整个上采样阶段，只需要释放用于第一次水平通道的纹理。

```
		if (i > 1) {
			buffer.ReleaseTemporaryRT(fromId - 1);
			toId -= 5;
			for (i -= 1; i > 0; i--) {
				…
			}
		}
		else {
			buffer.ReleaseTemporaryRT(bloomPyramidId);
		}
```

如果我们最终完全跳过了泛光效果，我们必须中止并执行复制操作。

```
		int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
		
		if (
			bloom.maxIterations == 0 ||
			height < bloom.downscaleLimit || width < bloom.downscaleLimit
		) {
			Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
			buffer.EndSample("Bloom");
			return;
		}
```

### Bicubic Upsampling

虽然高斯滤波器产生了平滑的结果，但在上采样时仍然执行双线性过滤，这可能会使发光效果看起来有块状外观。这在原始图像的对比度高的地方尤为明显，尤其是在运动中。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/upsampling-bilinear.png)

我们可以通过切换到双三次过滤来平滑这些伪影。虽然硬件没有直接支持这一功能，但我们可以使用Core RP库的Filtering包含文件中定义的SampleTexture2DBicubic函数。通过传递纹理和采样器状态、UV坐标以及交换了大小对的纹理大小向量来创建我们自己的GetSourceBicubic函数。此外，它还有一个用于最大纹理坐标的参数，通常为1，然后是另一个未使用的参数，可以设置为零。

```
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

…

float4 GetSourceBicubic (float2 screenUV) {
	return SampleTexture2DBicubic(
		TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
		_PostFXSource_TexelSize.zwxy, 1.0, 0.0
	);
}
```

在泛光组合通道中使用新函数，以便我们使用双三次过滤进行上采样。

```
float4 BloomCombinePassFragment (Varyings input) : SV_TARGET {
	float3 lowRes = GetSourceBicubic(input.screenUV).rgb;
	float3 highRes = GetSource2(input.screenUV).rgb;
	return float4(lowRes + highRes, 1.0);
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/upsampling-bicubic.png)

通过着色器布尔值使双三次采样成为可选项，以便在不需要时可以关闭它。这对应于Universal RP和HDRP的高质量泛光切换。

```
bool _BloomBicubicUpsampling;

float4 BloomCombinePassFragment (Varyings input) : SV_TARGET {
	float3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	float3 highRes = GetSource2(input.screenUV).rgb;
	return float4(lowRes + highRes, 1.0);
}
```

Add a toggle option for it to `**PostFXSettings**.**BloomSettings**`.

```
public bool bicubicUpsampling;
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/bicubic-upsampling-toggle.png)

在开始上采样之前，在PostFXStack.DoBloom中将它传递给GPU。

```
	int
		bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
		fxSourceId = Shader.PropertyToID("_PostFXSource"),
		fxSource2Id = Shader.PropertyToID("_PostFXSource2");
	
	…
	
	void DoBloom (int sourceId) {
		…
		
		buffer.SetGlobalFloat(
			bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f
		);
		if (i > 1) { … }
		…
	}
```

### Half Resolution

泛光可能需要很长时间来生成，因为涉及到大量的纹理采样和绘制操作。降低成本的一个简单方法是以半分辨率生成它。由于效果是柔和的，我们可以这样做。这将改变效果的外观，因为我们实际上跳过了第一个迭代。

首先，在决定跳过泛光时，我们应该提前一步看。实际上，初始检查时降低限制加倍。

```
	if (
			bloom.maxIterations == 0 ||
			height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2
		) {
			Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
			buffer.EndSample("Bloom");
			return;
		}
```

其次，我们需要为半尺寸图像声明一个纹理，这将成为新的起点。它不是泛光金字塔的一部分，所以我们将为它声明一个新的标识符。我们将用它进行预过滤步骤，因此给它取一个合适的名称。

```
	int
		bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
		bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
```

回到DoBloom，将源复制到预过滤纹理中，并将其用作金字塔的起点，再次将宽度和高度减半。在完成金字塔后，我们不再需要预过滤纹理，因此可以在那时释放它。

```
		RenderTextureFormat format = RenderTextureFormat.Default;
		buffer.GetTemporaryRT(
			bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format
		);
		Draw(sourceId, bloomPrefilterId, Pass.Copy);
		width /= 2;
		height /= 2;

		int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;
		int i;
		for (i = 0; i < bloom.maxIterations; i++) {
			…
		}

		buffer.ReleaseTemporaryRT(bloomPrefilterId);
```

### Threshold

泛光通常在艺术上用来使一些物体发光，但我们当前的效果适用于所有物体，无论亮度如何。尽管这在物理上没有意义，但我们可以通过引入亮度阈值来限制对效果的贡献。

我们不能突然从效果中消除颜色，因为那会在预期是渐变过渡的地方引入尖锐的边界。相反，我们将颜色乘以一个权重 $$w = \frac{\max(0, b - t)}{\max(b, 0.00001)} $$其中b是其亮度，t是配置的阈值。我们将使用颜色的RGB通道的最大值作为b。当阈值为零时，结果总是为1，这不会改变颜色。随着阈值的增加，权重曲线将向下弯曲，以便在b≤t的地方变为零。由于曲线的形状，它被称为膝盖曲线。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/threshold-graph.png)

这个曲线在一个角度上达到零，这意味着虽然过渡比一个夹紧更平滑，但仍然有一个突然的截断点。这就是为什么它也被称为硬膝。我们可以通过将权重改为$$ w = \frac{\max(s, b - t)}{\max(b, 0.00001)}$$ ，其中$$ s = \frac{\min(\max(0, b - t + tk), 2tk)}{4tk + 0.00001}$$ ，tk是一个0-1的膝盖滑块，来控制膝盖的形状。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/knee-graph.png)

让我们将阈值和膝盖滑块都添加到PostFXSettings.BloomSettings中。我们将配置的阈值视为伽马值，因为从视觉上来看更直观，所以在将其发送到GPU时，我们需要将其转换为线性空间。尽管阈值大于零会在这一点上消除所有颜色，但我们仍将它设计成开放的，因为我们受到LDR的限制。

```
		[Min(0f)]
		public float threshold;

		[Range(0f, 1f)]
		public float thresholdKnee;
```

我们将通过一个名为_BloomThreshold的向量将阈值值发送到GPU。在PostFXStack中为它声明一个标识符。

```
		bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
		bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
```

我们可以计算权重函数的常数部分，并将它们放入向量的四个分量中，以保持着色器更简单：![image-20230914191927240](C:\Users\admin\AppData\Roaming\Typora\typora-user-images\image-20230914191927240.png)

我们将在一个新的预过滤器通道中使用它，这个通道将取代DoBloom中的初始复制通道，从而在减小图像尺寸的同时将阈值应用于2×2像素的平均值。

```
		Vector4 threshold;
		threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
		threshold.y = threshold.x * bloom.thresholdKnee;
		threshold.z = 2f * threshold.y;
		threshold.w = 0.25f / (threshold.y + 0.00001f);
		threshold.y -= threshold.x;
		buffer.SetGlobalVector(bloomThresholdId, threshold);

		RenderTextureFormat format = RenderTextureFormat.Default;
		buffer.GetTemporaryRT(
			bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format
		);
		Draw(sourceId, bloomPrefilterId, Pass.BloomPrefilter);
```

将阈值向量和将其应用于颜色的函数添加到PostFXShaderPasses中，然后添加使用它的新通道函数。

```
float3 ApplyBloomThreshold (float3 color) {
	float brightness = Max3(color.r, color.g, color.b);
	float soft = brightness + _BloomThreshold.y;
	soft = clamp(soft, 0.0, _BloomThreshold.z);
	soft = soft * soft * _BloomThreshold.w;
	float contribution = max(soft, brightness - _BloomThreshold.x);
	contribution /= max(brightness, 0.00001);
	return color * contribution;
}

float4 BloomPrefilterPassFragment (Varyings input) : SV_TARGET {
	float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
	return float4(color, 1.0);
}
```

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/threshold-inspector.png)

![scene](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/threshold-scene.png)

### Intensity

在本教程中，我们通过添加一个强度滑块来完成，以控制整体的泛光效果强度。我们不会设置它的上限，因此如果需要，可以使整个图像变得过曝。

```
		[Min(0f)]
		public float intensity;
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/intensity.png)

如果强度设置为零，我们可以跳过泛光效果，因此在DoBloom的开始处检查它。

```
		if (
			bloom.maxIterations == 0 || bloom.intensity <= 0f ||
			height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2
		) {
			Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
			buffer.EndSample("Bloom");
			return;
		}
```

否则，将强度传递到GPU，使用一个新的标识符_BloomIntensity。我们将在合并通道期间使用它来加权低分辨率图像，因此我们不需要创建额外的通道。在除了最后一次绘制到相机目标之外的所有绘制中将其设置为1。

```
		buffer.SetGlobalFloat(bloomIntensityId, 1f);
		if (i > 1) {
			…
		}
		else {
			buffer.ReleaseTemporaryRT(bloomPyramidId);
		}
		buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);
		buffer.SetGlobalTexture(fxSource2Id, sourceId);
		Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.BloomCombine);
```

现在我们只需要在BloomCombinePassFragment中将低分辨率颜色乘以强度即可。

```
bool _BloomBicubicUpsampling;
float _BloomIntensity;

float4 BloomCombinePassFragment (Varyings input) : SV_TARGET {
	…
	return float4(lowRes * _BloomIntensity + highRes, 1.0);
}
```

![0.5](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/intensity-05.png)

![5](https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/bloom/intensity-5.png)