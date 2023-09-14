# Post Processing

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