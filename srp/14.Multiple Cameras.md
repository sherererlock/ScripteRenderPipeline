# **Multiple Cameras**

## Combining Cameras

由于剔除、光线处理和阴影渲染是按摄像机进行的，因此每帧渲染的摄像机数量越少越好，最好只有一个。但有时我们确实需要同时渲染多个不同的视角。例如分屏多人游戏、后视镜、俯视叠加、游戏内摄像头和 3D 角色肖像。

**What about the avatar's hands and tools in a first-person game?**

在第一人称游戏中，角色所持物品通常以不同的视场角显示，出于各种原因。这可以通过第二个摄像头来实现，但也可以通过使用调整后的视图矩阵进行渲染，同时仍然使用相同的摄像头来实现。

### Split Screen

首先，让我们考虑一个分屏场景，它由两个并排的摄像头组成。左侧摄像头的视口宽度设置为 0.5。右侧摄像机的宽度也是 0.5，其 X 位置设置为 0.5。如果我们不使用后期特效，那么效果就会达到预期。

但如果我们启用后期特效，就会失败。两台摄像机都以正确的尺寸渲染，但最终覆盖了整个摄像机目标缓冲区，只有最后一台摄像机可见。

出现这种情况的原因是，调用 SetRenderTarget 也会重置视口，使其覆盖整个目标。要将视口应用到最后的特效后处理，我们必须在设置目标之后、绘制之前设置视口。我们可以复制 PostFXStack.Draw，将其重命名为 DrawFinal，然后在 SetRenderTarget 之后直接在缓冲区上调用 SetViewport，并将摄像机的像素矩形作为参数。由于这是最终绘制，我们可以用硬编码值替换除源参数以外的所有参数。

```
	void DrawFinal (RenderTargetIdentifier from) {
		buffer.SetGlobalTexture(fxSourceId, from);
		buffer.SetRenderTarget(
			BuiltinRenderTextureType.CameraTarget,
			RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
		);
		buffer.SetViewport(camera.pixelRect);
		buffer.DrawProcedural(
			Matrix4x4.identity, settings.Material,
			(int)Pass.Final, MeshTopology.Triangles, 3
		);
	}
```

在 DoColorGradingAndToneMapping 结束时调用新方法，而不是常规的 Draw 方法。

```
	void DoColorGradingAndToneMapping (int sourceId) {
		…
		Draw(…)
		DrawFinal(sourceId);
		buffer.ReleaseTemporaryRT(colorGradingLUTId);
	}
```

如果你使用的是基于瓦片的GPU，你可能会在渲染视口的边缘产生渲染伪影，超出了其边界。这是因为被遮挡部分的瓦片区域包含了无效数据。我们通过在不使用完整视口时加载目标来解决这个问题。这不仅适用于Unity 2022，但我之所以注意到这个问题是因为Apple Silicon Macs使用基于瓦片的GPU并支持"don't-care"选项，但在我写这个系列时它们还不存在。

```
	static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);
	
	…
	
		void DrawFinal (RenderTargetIdentifier from) {
		buffer.SetGlobalTexture(fxSourceId, from);
		buffer.SetRenderTarget(
			BuiltinRenderTextureType.CameraTarget,
			camera.rect == fullViewRect ?
				RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
			RenderBufferStoreAction.Store
		);
		…
	}
```

### Layering Cameras

除了渲染到不同的区域，我们还可以使摄像头的视口重叠。最简单的例子是使用一个常规的主摄像头，它覆盖整个屏幕，然后添加一个第二个摄像头，它使用相同的视图但具有较小的视口。我将第二个视口缩小到一半大小，并通过将其XY位置设置为0.25来使其居中。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/combining-cameras/two-camera-layers.png)

如果我们不使用后期特效，那么我们可以将顶部摄像头的层设置为仅清除深度，从而将其变成一个部分透明的叠加层。这会移除它的天空盒，显示下面的层。但是，当使用后期特效时，这种方法不起作用，因为此时我们将其强制设置为CameraClearFlags.Color，因此我们将看到摄像头的背景颜色，通常默认为深蓝色。

![with](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/combining-cameras/clear-depth-only-with-post-fx.png)

要使图层透明度与后期特效一起工作，可以尝试修改后期特效堆栈（PostFXStack）的着色器的最终通道，以执行Alpha混合而不是默认的One Zero模式。

```
		Pass {
			Name "Final"

			Blend SrcAlpha OneMinusSrcAlpha
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment FinalPassFragment
			ENDHLSL
		}
```

This does require us to always load the target buffer in `FinalDraw`.

```
	void DrawFinal (RenderTargetIdentifier from) {
		buffer.SetGlobalTexture(fxSourceId, from);
		buffer.SetRenderTarget(
			BuiltinRenderTextureType.CameraTarget,
			RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
		);
		…
	}
```

现在将叠加摄像头的背景颜色的alpha通道设置为零。这似乎有效，只要我们禁用了泛光效果（bloom）。我添加了两个非常明亮的自发光物体，以便清楚地看出是否激活了泛光效果。

![disabled](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/combining-cameras/bloom-disabled.png)

这不适用于泛光效果，因为该效果目前不保留透明度。我们可以通过调整最终的泛光通道，使其保留来自高分辨率源纹理的原始透明度来解决这个问题。我们需要调整BloomAddPassFragment和BloomScatterFinalPassFragment，因为任何一个都可能用于最终绘制。

```
float4 BloomAddPassFragment (Varyings input) : SV_TARGET {
	…
	float4 highRes = GetSource2(input.screenUV);
	return float4(lowRes * _BloomIntensity + highRes.rgb, highRes.a);
}

…

float4 BloomScatterFinalPassFragment (Varyings input) : SV_TARGET {
	…
	float4 highRes = GetSource2(input.screenUV);
	lowRes += highRes.rgb - ApplyBloomThreshold(highRes.rgb);
	return float4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/combining-cameras/layered-with-bloom.png)

现在透明度在泛光效果中可以工作了，但是泛光效果对透明区域的贡献不再可见。我们可以通过将最终通道切换到预乘Alpha混合来保留泛光效果。这确实需要我们将摄像头的背景颜色设置为纯透明黑色，因为它将添加到下面的层上。

```
			Name "Final"

			Blend One OneMinusSrcAlpha
```

### Layered Alpha

我们当前的图层处理方法只在我们的着色器生成合理的 alpha 值时才能与摄像头图层混合正常工作。之前我们并不关心写入的 alpha 值，因为我们从未用它们进行任何操作。但现在，如果两个 alpha 值为 0.5 的对象最终渲染到同一个像素，那么该像素的最终 alpha 应为 0.25。当其中一个 alpha 值为 1 时，结果应始终为 1。当第二个 alpha 为零时，应保留原始 alpha 值。所有这些情况都可以通过在混合 alpha 时使用 One OneMinusSrcAlpha 来处理。我们可以通过在颜色混合模式之后添加逗号，然后是 alpha 的混合模式，来单独为 alpha 通道配置着色器的混合模式。对于我们的 Lit 和 Unlit 着色器的常规通道都要这样做。

```
	Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
```

这个方法在适当的 alpha 值被使用时会起作用，通常意味着写入深度的对象应该始终生成 alpha 为 1。这对不透明材质似乎很简单，但如果它们最终使用了一个包含不同 alpha 的基础贴图，就会出现问题。而且对于剪裁材质（clip materials）也可能出现问题，因为它们依赖于一个 alpha 阈值来丢弃片段。如果片段被剪裁，那么一切都正常，但如果没有被剪裁，它的 alpha 应该变为 1。

为了确保 alpha 通道在我们的着色器中正确工作的最快方法是在 LitInput 和 UnlitInput 中向 UnityPerMaterial 缓冲区添加 _ZWrite。这将有助于正确处理 alpha 通道的深度写入。

```
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
```

然后，在两个输入文件中都添加一个带有 alpha 参数的 GetFinalAlpha 函数。如果 _ZWrite 设置为 1，它将返回 1，否则返回提供的值。

```
float GetFinalAlpha (float alpha) {
	return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}
```

在 LitPassFragment 中通过这个函数对表面的 alpha 进行过滤，以获得最终的正确 alpha 值。

```
float4 LitPassFragment (Varyings input) : SV_TARGET {
	…
	return float4(color, GetFinalAlpha(surface.alpha));
}
```

And do the same for the base alpha in `UnlitPassFragment`.

```
float4 UnlitPassFragment (Varyings input) : SV_TARGET {
	…
	return float4(base.rgb, GetFinalAlpha(base.a));
}
```

### Custom Blending

与前一摄像机层混合只对叠加摄像机有意义。除非编辑器提供了一个已清除的目标，否则底层摄像机将与摄像机目标的初始内容进行混合，而摄像机目标的初始内容要么是随机的，要么是之前帧的累积。因此，第一个摄像机应使用 "一零 "模式进行混合。为了支持替换、叠加和更奇特的分层选项，我们将为摄像机添加一个可配置的最终混合模式，在启用后期特效时使用。我们将为这些设置创建一个新的可序列化的 CameraSettings 配置类，就像我们为阴影所做的那样。为了方便起见，将源混合模式和目标混合模式都封装在一个内部的 FinalBlendMode 结构中，然后将其默认设置为 "一零混合"。

```
using System;
using UnityEngine.Rendering;

[Serializable]
public class CameraSettings {
	
	[Serializable]
	public struct FinalBlendMode {

		public BlendMode source, destination;
	}

	public FinalBlendMode finalBlendMode = new FinalBlendMode {
		source = BlendMode.One,
		destination = BlendMode.Zero
	};
}
```

我们无法将这些设置直接添加到 "摄像机 "组件中，因此我们将创建一个辅助的 （CustomRenderPipelineCamera）组件。该组件只能添加一次到作为摄像机的游戏对象中，而且只能添加一次。给它一个带有 getter 属性的 CameraSettings 配置字段。由于设置是一个类，该属性必须确保存在一个类，因此如果需要，可以创建一个新的设置对象实例。如果编辑器尚未对组件进行序列化，或者在运行时为摄像机添加了一个组件，就会出现这种情况。

```
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour {

	[SerializeField]
	CameraSettings settings = default;

	public CameraSettings Settings => settings ?? (settings = new CameraSettings());
}
```

现在，我们可以在 CameraRenderer.Render 开始时获取摄像机的 CustomRenderPipelineCamera 组件。为了支持没有自定义设置的摄像机，我们将检查我们的组件是否存在。如果存在，我们就使用它的设置，否则就使用默认设置对象，我们将创建一个默认设置对象，并在静态字段中存储引用。然后，我们会在设置堆栈时传递最终的混合模式。

```
	static CameraSettings defaultCameraSettings = new CameraSettings();

	…

	public void Render (…) {
		this.context = context;
		this.camera = camera;

		var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
		CameraSettings cameraSettings =
			crpCamera ? crpCamera.Settings : defaultCameraSettings;
		
		…
			postFXStack.Setup(
			context, camera, postFXSettings, useHDR, colorLUTResolution,
			cameraSettings.finalBlendMode
		);
		…
	}
```

`**PostFXStack**` now has to keep track of the camera's final blend mode.

```
	CameraSettings.FinalBlendMode finalBlendMode;

	…
	
	public void Setup (
		ScriptableRenderContext context, Camera camera, PostFXSettings settings,
		bool useHDR, int colorLUTResolution, CameraSettings.FinalBlendMode finalBlendMode
	) {
		this.finalBlendMode = finalBlendMode;
		…
	}
```

因此，它可以在 DrawFinal 开始时设置新的 _FinalSrcBlend 和 _FinalDstBlend 浮点着色器属性。此外，如果目标混合模式不为零，我们现在还需要加载目标缓冲区。

```
	int
		finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend"),
		finalDstBlendId = Shader.PropertyToID("_FinalDstBlend");
	
	…
	
	void DrawFinal (RenderTargetIdentifier from) {
		buffer.SetGlobalFloat(finalSrcBlendId, (float)finalBlendMode.source);
		buffer.SetGlobalFloat(finalDstBlendId, (float)finalBlendMode.destination);
		buffer.SetGlobalTexture(fxSourceId, from);
		buffer.SetRenderTarget(
			BuiltinRenderTextureType.CameraTarget,
			finalBlendMode.destination == BlendMode.Zero && camera.rect == fullViewRect ?
				RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
			RenderBufferStoreAction.Store
		);
		…
	}
```

Finally, use the new properties in the final pass instead of hard-coded blend modes.

```
			Name "Final"

			Blend [_FinalSrcBlend] [_FinalDstBlend]
```

由于默认的最终混合模式为 "一零"，因此从现在起，没有我们设置的摄像机将覆盖目标缓冲区的内容。覆盖摄像机必须采用不同的最终混合模式，通常为 One OneMinusSrcAlpha。

### Render Textures

除了创建分屏显示或直接分层摄像机外，将摄像机用于游戏内显示或作为图形用户界面的一部分也很常见。在这种情况下，摄像头的目标必须是一个渲染纹理，可以是资产，也可以是运行时创建的纹理。例如，我通过 "资产"/"创建"/"渲染纹理 "创建了一个 200×100 的渲染纹理。我没有给它设置深度缓冲区，因为我渲染了一个带有后期特效的摄像头，它会创建自己的带有深度缓冲区的中间渲染纹理。

然后，我创建了一个相机，通过将其与相机的目标纹理属性挂钩，将场景渲染到该纹理上。

与常规渲染一样，底部摄像机必须使用 One Zero 作为最终混合模式。编辑器最初会显示一个清晰的黑色纹理，但之后渲染纹理将包含最后渲染的内容。多个摄像机可以在任何视口下渲染到相同的渲染纹理，与普通模式一样。唯一不同的是，Unity 会先自动渲染具有渲染纹理目标的摄像机，然后再渲染显示屏上的摄像机。首先渲染有目标纹理的摄像机，然后再渲染没有目标纹理的摄像机。

### Unity UI

渲染纹理可以像普通纹理一样使用。要通过 Unity 的用户界面显示它，我们必须使用带有原始图像组件的游戏对象，该组件可通过 GameObject / UI / Raw Image（游戏对象/用户界面/原始图像）创建。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/combining-cameras/raw-image-inspector.png)

原始图像使用默认的用户界面材质，它执行标准的 SrcAlpha OneMinusSrcAlpha 混合。因此，透明度可以正常工作，但绽放并不是可叠加的，而且除非纹理显示为像素完美的双线性滤波，否则相机的黑色背景色会在透明边缘显示为深色轮廓。

为了支持其他混合模式，我们必须创建一个自定义 UI 着色器。我们只需复制 Default-UI 着色器，通过 _SrcBlend 和 _DstBlend 着色器属性添加对可配置混合的支持即可。我还调整了着色器代码，使其更符合本系列教程的风格。

```
Shader "Custom RP/UI Custom Blending" {
	Properties {
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15
		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
	}

	SubShader {
		Tags {
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
			"PreviewType" = "Plane"
			"CanUseSpriteAtlas" = "True"
		}

		Stencil {
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}

		Blend [_SrcBlend] [_DstBlend]
		ColorMask [_ColorMask]
		Cull Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]

		Pass { … }
	}
}
```

And here is the pass, unmodified except for style.

```
		Pass {
			Name "Default"
			
			CGPROGRAM
			#pragma vertex UIPassVertex
			#pragma fragment UIPassFragment
			#pragma target 2.0

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
			#pragma multi_compile_local _ UNITY_UI_ALPHACLIP

			struct Attributes {
				float4 positionOS : POSITION;
				float4 color : COLOR;
				float2 baseUV : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings {
				float4 positionCS : SV_POSITION;
				float2 positionUI : VAR_POSITION;
				float2 baseUV : VAR_BASE_UV;
				float4 color : COLOR;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;
			float4 _TextureSampleAdd;
			float4 _ClipRect;

			Varyings UIPassVertex (Attributes input) {
				Varyings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
				output.positionCS = UnityObjectToClipPos(input.positionOS);
				output.positionUI = input.positionOS.xy;
				output.baseUV = TRANSFORM_TEX(input.baseUV, _MainTex);
				output.color = input.color * _Color;
				return output;
			}

			float4 UIPassFragment (Varyings input) : SV_Target {
				float4 color =
					(tex2D(_MainTex, input.baseUV) + _TextureSampleAdd) * input.color;
				#if defined(UNITY_UI_CLIP_RECT)
					color.a *= UnityGet2DClipping(input.positionUI, _ClipRect);
				#endif
				#if defined(UNITY_UI_ALPHACLIP)
					clip (color.a - 0.001);
				#endif
				return color;
			}
			ENDCG
		}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/combining-cameras/raw-image-premultiplied.png)

### Post FX Settings Per Camera

在使用多台摄像机时，每台摄像机应该可以使用不同的后期特效，因此让我们添加对它的支持。为 "相机设置 "提供一个切换按钮，用于控制它是否覆盖全局后期特效设置，以及它自己的 "后期特效设置"（PostFXSettings）字段。

```
	public bool overridePostFX = false;

	public PostFXSettings postFXSettings = default;
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/combining-cameras/override-post-fx-settings.png)

让 CameraRenderer.Render 检查相机是否覆盖后期特效设置。如果是，则用摄像机的设置替换渲染管道提供的设置。

```
		var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
		CameraSettings cameraSettings =
			crpCamera ? crpCamera.Settings : defaultCameraSettings;

		if (cameraSettings.overridePostFX) {
			postFXSettings = cameraSettings.postFXSettings;
		}
```

现在，每个摄像机都可以使用默认或自定义后期特效。例如，我让底部摄像机使用默认特效，关闭了叠加摄像机的后期特效，并为渲染纹理摄像机提供了不同的后期特效，包括冷温度偏移和中性色调映射。

## Rendering Layers

在同时显示多个摄像机视图时，我们并不总是希望为所有摄像机渲染相同的场景。例如，我们可以渲染主视图和角色肖像。Unity 一次只支持一个全局场景，因此我们必须使用一种方法来限制每个摄像机看到的内容。

### Culling Masks

每个游戏对象都属于一个图层。场景窗口可以通过编辑器右上角的 "图层 "下拉菜单过滤显示的图层。同样，每个摄像机都有一个 "剔除掩码"（Culling Mask）属性，可用于限制以相同方式显示的内容。该遮罩在渲染的剔除步骤中应用。

每个对象只属于一个图层，而剔除蒙版可以包含多个图层。例如，有两台摄像机同时渲染 "默认 "层，其中一台还渲染了 "忽略光线投射 "层，而另一台则渲染了 "水 "层。这样，一些物体会同时显示在两个摄像机上，而另一些物体则只能在其中一个摄像机上看到，还有一些物体可能根本不会被渲染。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/rendering-layers/different-culling-masks.png)

灯光也有剔除遮罩。其原理是，一个物体如果被某个灯光遮挡，就好像该灯光不存在一样。物体不会被光线照亮，也不会投射阴影。但如果我们用定向光来尝试，只有它的阴影会受到影响。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/rendering-layers/culling-mask-directional-light.png)

如果禁用 RP 的 "每个对象使用灯光 "选项，使用其他灯光类型也会出现同样的情况。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/rendering-layers/culling-mask-point-light.png)

如果启用了 "按对象使用灯光"，那么灯光剔除功能就会正常工作，但仅适用于点光源和聚光灯。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/rendering-layers/culling-mask-point-light-per-object.png)

我们之所以能得到这些结果，是因为 Unity 在向 GPU 发送每个对象的光线指数时，会应用光线剔除遮罩。因此，如果我们不使用这些遮罩，剔除效果就不会起作用。而且对于定向光也不会起作用，因为我们总是对所有物体都应用定向光。阴影总是会被正确剔除，因为在从灯光的角度渲染阴影投射物时，灯光的剔除掩码会像相机的剔除掩码一样被使用。

我们目前的方法无法完全支持灯光的剔除遮罩。这一限制并不妨碍我们的工作，HDRP 也不支持灯光遮罩。Unity 提供了渲染图层作为 SRP 的替代方案。使用渲染层而不是游戏对象层有两个好处。首先，渲染器不局限于单个图层，这使得它们更加灵活。其次，渲染图层不用于其他用途，不像默认图层还用于物理效果。

在我们继续讨论渲染图层之前，让我们先在灯光检查器中显示一个警告，当灯光的剔除蒙版设置为 "Everything "以外的内容时。灯光的剔除遮罩可通过其 cullingMask 整数属性获得，其中 -1 代表所有图层。如果 CustomLightEditor 的目标将遮罩设置为其他内容，则在 OnInspectorGUI 结束时调用 EditorGUILayout.HelpBox，并使用字符串表示剔除遮罩只影响阴影，并使用 MessageType.Warning 显示警告图标。

```
	public override void OnInspectorGUI() {
		…

		var light = target as Light;
		if (light.cullingMask != -1) {
			EditorGUILayout.HelpBox(
				"Culling Mask only affects shadows.",
				MessageType.Warning
			);
		}
	}
```

我们可以说得更具体一点，"每个对象使用灯光 "设置会对非定向灯光产生影响。

```
			EditorGUILayout.HelpBox(
				light.type == LightType.Directional ?
					"Culling Mask only affects shadows." :
					"Culling Mask only affects shadow unless Use Lights Per Objects is on.",
				MessageType.Warning
			);
```

### Adjusting the Rendering Layer Mask

使用 SRP 时，灯光和网格渲染器组件的检查器会显示渲染层掩码属性，而使用默认 RP 时，该属性会被隐藏。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/rendering-layers/mesh-renderer-rendering-layer-mask.png)

默认情况下，下拉菜单会显示 32 个图层，分别命名为 Layer1、Layer2 等。这些图层的名称可以通过覆盖 RenderPipelineAsset.renderingLayerMaskNames getter 属性按 RP 进行配置。由于这纯粹是下拉菜单的外观设计，我们只需要在 Unity 编辑器中这样做。因此，将 CustomRenderPipelineAsset 变为部分类。

```
public partial class CustomRenderPipelineAsset : RenderPipelineAsset { … }
```

然后为其创建一个重载该属性的编辑器专用脚本资产。它将返回一个字符串数组，我们可以在静态构造方法中创建该数组。我们将使用与默认值相同的名称，只是在层字和数字之间加一个空格。

```
partial class CustomRenderPipelineAsset {

#if UNITY_EDITOR

	static string[] renderingLayerNames;

	static CustomRenderPipelineAsset () {
		renderingLayerNames = new string[32];
		for (int i = 0; i < renderingLayerNames.Length; i++) {
			renderingLayerNames[i] = "Layer " + (i + 1);
		}
	}

	public override string[] renderingLayerMaskNames => renderingLayerNames;

#endif
}
```

这会稍微改变渲染层标签。这对网格渲染器组件很有效，但不幸的是，灯光属性并不响应变化。呈现层下拉菜单会显示出来，但不会应用调整。我们无法直接解决这个问题，但可以添加我们自己的属性版本，这样就可以正常工作了。首先，在 CustomLightEditor 中为其创建一个 GUIContent，使用相同的标签和工具提示，说明这是上面属性的功能版本。

```
	static GUIContent renderingLayerMaskLabel =
		new GUIContent("Rendering Layer Mask", "Functional version of above property.");
```

然后创建一个 DrawRenderingLayerMask 方法，它是 LightEditor.DrawRenderingLayerMask 的替代方法，可以将更改后的值赋回属性。要使下拉菜单使用 RP 的图层名称，我们不能简单地依赖 EditorGUILayout.PropertyField。我们必须从设置中抓取相关属性，确保处理多重选择的混合值，以整数形式抓取掩码，显示它，并将更改后的值赋回给属性。这是默认灯光检查器版本中缺少的最后一步。

显示下拉菜单的方法是调用 EditorGUILayout.MaskField，并将标签、遮罩和 GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames 作为参数。

```
	void DrawRenderingLayerMask () {
		SerializedProperty property = settings.renderingLayerMask;
		EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
		EditorGUI.BeginChangeCheck();
		int mask = property.intValue;
		mask = EditorGUILayout.MaskField(
			renderingLayerMaskLabel, mask,
			GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames
		);
		if (EditorGUI.EndChangeCheck()) {
			property.intValue = mask;
		}
		EditorGUI.showMixedValue = false;
	}
```

在调用 base.OnInspectorGUI 后直接调用新方法，因此额外的渲染层掩码属性会直接显示在无功能属性的下方。此外，我们现在必须始终调用 ApplyModifiedProperties，以确保对渲染层掩码的更改会应用到灯光上。

```
	public override void OnInspectorGUI() {
		base.OnInspectorGUI();
		DrawRenderingLayerMask();
		
		if (
			!settings.lightType.hasMultipleDifferentValues &&
			(LightType)settings.lightType.enumValueIndex == LightType.Spot
		)
		{
			settings.DrawInnerAndOuterSpotAngle();
			//settings.ApplyModifiedProperties();
		}

		settings.ApplyModifiedProperties();

		…
	}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/rendering-layers/rendering-layer-mask-light.png)

除了选择 "全部 "或 "图层 32 "选项会产生与选择 "无 "相同的结果外，我们版本的属性确实应用了更改。出现这种情况是因为灯光的渲染层掩码在内部存储为无符号整数（uint）。这是有道理的，因为它被用作位掩码，但 SerializedProperty 只支持获取和设置有符号整数值。

Everything 选项用 -1 表示，该属性将其箝位为零。而第 32 层对应的是最高位，它代表一个大于 int.MaxValue 的数字，属性也会将其置换为零。

我们只需删除最后一层，将渲染层名称减少到 31，就能解决第二个问题。这样层数仍然很多。HDRP 只支持 8 层。

```
	renderingLayerNames = new string[31];
```

移除一层后，Everything 选项现在由一个除最高位外都被设置的值来表示，这与 int.MaxValue 相匹配。因此，我们可以通过显示-1，同时存储 int.MaxValue 来解决第一个问题。默认属性不会这样做，这就是为什么它会在适当的时候显示 Mixed... 而不是 Everything。HDRP 也存在这个问题。

```
		int mask = property.intValue;
		if (mask == int.MaxValue) {
			mask = -1;
		}
		mask = EditorGUILayout.MaskField(
			renderingLayerMaskLabel, mask,
			GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames
		);
		if (EditorGUI.EndChangeCheck()) {
			property.intValue = mask == -1 ? int.MaxValue : mask;
		}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/rendering-layers/functional-rendering-layer-mask-property.png)

我们终于可以正确调整灯光的渲染图层遮罩属性了。但默认情况下并不使用遮罩，因此没有任何变化。我们可以通过启用 "阴影"（Shadows）中 "阴影绘制设置"（ShadowDrawingSettings）的 "使用渲染图层遮罩测试"（useRenderingLayerMaskTest）将其应用到阴影中。对所有灯光都要这样做，因此在渲染方向阴影、渲染点阴影和渲染点阴影中都要这样做。现在我们可以通过配置对象和灯光的渲染图层遮罩来消除阴影了。

```
	var shadowSettings = new ShadowDrawingSettings(
			…
		) {
			useRenderingLayerMaskTest = true
		};
```

要将渲染层遮罩应用到 Lit 着色器的光照计算中，物体和光照的遮罩都必须在 GPU 端可用。要访问对象的遮罩，我们必须在 UnityInput 中的 UnityPerDraw 结构中添加一个 float4 unity_RenderingLayer 字段，它位于 unity_WorldTransformParams 的正下方。遮罩存储在其第一个组件中。

```
	real4 unity_WorldTransformParams;

	float4 unity_RenderingLayer;
```

We'll add the mask to our `**Surface**` struct, as a `**uint**` because it is a bit mask.

```
struct Surface {
	…
	uint renderingLayerMask;
};
```

在 LitPassFragment 中设置曲面遮罩时，我们必须使用 asuint 固有函数。这样就可以使用原始数据，而无需执行从浮点数到 uint 的数值类型转换，因为这将改变位模式。

```
	surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
	surface.renderingLayerMask = asuint(unity_RenderingLayer.x);
```

我们必须对 Light 结构做同样的处理，因此也要给它一个 uint 字段，用于渲染图层掩码。

```
struct Light {
	…
	uint renderingLayerMask;
};
```

我们负责将遮罩发送到 GPU。我们可以将其存储在 _DirectionalLightDirections 和 _OtherLightDirections 数组中未使用的第四部分。为了清晰起见，请在它们的名称后添加 AndMasks 后缀。

```
CBUFFER_START(_CustomLight)
	…
	float4 _DirectionalLightDirectionsAndMasks[MAX_DIRECTIONAL_LIGHT_COUNT];
	…
	float4 _OtherLightDirectionsAndMasks[MAX_OTHER_LIGHT_COUNT];
	…
CBUFFER_END
```

Copy the mask in `GetDirectionalLight`.

```
light.direction = _DirectionalLightDirectionsAndMasks[index].xyz;
	light.renderingLayerMask = asuint(_DirectionalLightDirectionsAndMasks[index].w);
```

And in `GetOtherLight`.

```
	float3 spotDirection = _OtherLightDirectionsAndMasks[index].xyz;
	light.renderingLayerMask = asuint(_OtherLightDirectionsAndMasks[index].w);
```

在 CPU 端，调整照明类中的标识符和数组名称，使之匹配。然后复制灯光的渲染层掩码。我们从 SetupDirectionalLight 开始，它现在也需要直接访问灯光对象。让我们将其添加为参数。

```
	void SetupDirectionalLight (
		int index, int visibleIndex, ref VisibleLight visibleLight, Light light
	) {
		dirLightColors[index] = visibleLight.finalColor;
		Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirAndMask.w = light.renderingLayerMask;
		dirLightDirectionsAndMasks[index] = dirAndMask;
		dirLightShadowData[index] =
			shadows.ReserveDirectionalShadows(light, visibleIndex);
	}
```

对 SetupSpotLight 进行同样的修改，同时添加一个光参数，以保持一致。

```
	void SetupSpotLight (
		int index, int visibleIndex, ref VisibleLight visibleLight, Light light
	) {
		…
		Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirAndMask.w = light.renderingLayerMask;
		otherLightDirectionsAndMasks[index] = dirAndMask;

		//Light light = visibleLight.light;
		…
		}
```

然后对 SetupPointLight 执行此操作，现在它还必须更改 otherLightDirectionsAndMasks。由于它不使用方向，因此可以将其设置为零。

```
	void SetupPointLight (
		int index, int visibleIndex, ref VisibleLight visibleLight, Light light
	) {
		…
		Vector4 dirAndmask = Vector4.zero;
		dirAndmask.w = light.renderingLayerMask;
		otherLightDirectionsAndMasks[index] = dirAndmask;
		//Light light = visibleLight.light;
		otherLightShadowData[index] =
			shadows.ReserveOtherShadows(light, visibleIndex);
	}
```

现在，我们必须在 SetupLights 中抓取一次灯光对象，并将其传递给所有设置方法。我们很快还将在这里对灯光进行其他操作。

```
			VisibleLight visibleLight = visibleLights[i];
			Light light = visibleLight.light;
			switch (visibleLight.lightType) {
				case LightType.Directional:
					if (dirLightCount < maxDirLightCount) {
						SetupDirectionalLight(
							dirLightCount++, i, ref visibleLight, light
						);
					}
					break;
				case LightType.Point:
					if (otherLightCount < maxOtherLightCount) {
						newIndex = otherLightCount;
						SetupPointLight(otherLightCount++, i, ref visibleLight, light);
					}
					break;
				case LightType.Spot:
					if (otherLightCount < maxOtherLightCount) {
						newIndex = otherLightCount;
						SetupSpotLight(otherLightCount++, i, ref visibleLight, light);
					}
					break;
			}
```

回到 GPU 方面，为 Lighting 添加一个 RenderingLayersOverlap 函数，返回曲面和光线的遮罩是否重叠。方法是检查位掩码的位并值是否为非零。

```
bool RenderingLayersOverlap (Surface surface, Light light) {
	return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}
```

现在，我们可以使用该方法来检查是否需要在 GetLighting 的三个循环中添加照明。

```
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		if (RenderingLayersOverlap(surfaceWS, light)) {
			color += GetLighting(surfaceWS, brdf, light);
		}
	}
	
	#if defined(_LIGHTS_PER_OBJECT)
		for (int j = 0; j < min(unity_LightData.y, 8); j++) {
			int lightIndex = unity_LightIndices[j / 4][j % 4];
			Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
			if (RenderingLayersOverlap(surfaceWS, light)) {
				color += GetLighting(surfaceWS, brdf, light);
			}
		}
	#else
		for (int j = 0; j < GetOtherLightCount(); j++) {
			Light light = GetOtherLight(j, surfaceWS, shadowData);
			if (RenderingLayersOverlap(surfaceWS, light)) {
				color += GetLighting(surfaceWS, brdf, light);
			}
		}
	#endif
```

### Reinterpreting an Int as a Float

虽然此时的渲染掩码会影响灯光，但并不能正确地影响灯光。Light.renderingLayerMask（光源渲染层遮罩）属性将其位遮罩显示为 int，而在光源设置方法中将其转换为浮点运算时会出现乱码。没有办法直接向 GPU 发送整数数组，因此我们必须在不转换的情况下以某种方式将 int 重新解释为 float，但 C# 中没有直接等价的 asuint。

我们无法像在 HLSL 中那样简单地重新解释 C# 中的数据，因为 C# 是强类型的。我们能做的是使用 union 结构来别名数据类型。我们将通过为 int 添加 ReinterpretAsFloat 扩展方法来隐藏这种方法。为该方法创建一个静态 ReinterpretExtensions 类，该类最初只是执行常规的类型转换。

```
public static class ReinterpretExtensions {

	public static float ReinterpretAsFloat (this int value) {
		return value;
	}
}
```

在三个灯光设置方法中使用 ReinterpretAsFloat，而不是依赖隐式转换。

```
		dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
```

然后在 ReinterpretExtensions 中定义一个结构类型，其中包含一个 int 和一个 float 字段。在 ReinterpretAsFloat 中初始化该类型的默认变量，设置其整数值，然后返回其 float 值。

```
	struct IntFloat {

		public int intValue;

		public float floatValue;
	}

	public static float ReinterpretAsFloat (this int value) {
		IntFloat converter = default;
		converter.intValue = value;
		return converter.floatValue;
	}
}
```

要将其转化为重新解释，我们必须使结构体的两个字段重叠，从而共享相同的数据。之所以能做到这一点，是因为这两种类型的大小都是 4 字节。为此，我们要将结构体的布局设置为显式，方法是将结构体的 StructLayout 属性设置为 LayoutKind.Explicit。然后，我们必须在字段中添加 FieldOffset 属性，以指示字段数据的放置位置。将两个偏移量都设置为零，这样它们就会重叠。这些属性来自 System.Runtime.InteropServices 命名空间。

```
using System.Runtime.InteropServices;

public static class ReinterpretExtensions {

	[StructLayout(LayoutKind.Explicit)]
	struct IntFloat {

		[FieldOffset(0)]
		public int intValue;

		[FieldOffset(0)]
		public float floatValue;
	}

	…
}
```

现在，结构体的 int 和 float 字段代表相同的数据，但解释方式不同。这样，位掩码保持不变，渲染层掩码也能正常工作。

### Camera Rendering Layer Mask

除了使用现有的剔除蒙版外，我们还可以使用渲染图层蒙版来限制摄像机的渲染内容。摄像机没有渲染图层遮罩属性，但我们可以将其添加到 CameraSettings（摄像机设置）中。我们将其设置为 int，因为灯光的遮罩也是以 int 的形式显示的。默认设置为-1，代表所有图层。

```
	public int renderingLayerMask = -1;
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/rendering-layers/camera-rendering-layer-mask-int.png)

要将遮罩显示为下拉菜单，我们必须为其创建一个自定义图形用户界面。但与其为整个 CameraSettings 类创建一个自定义编辑器，不如只为渲染图层遮罩创建一个编辑器。

首先，为了表示某个字段代表渲染图层遮罩，我们创建了一个扩展 PropertyAttribute 的 RenderingLayerMaskFieldAttribute 类。这只是一个标记属性，不需要做其他任何事情。请注意，这不是一个编辑器类型，因此不应放在编辑器文件夹中。

```
using UnityEngine;

public class RenderingLayerMaskFieldAttribute : PropertyAttribute {}
```

Attach this attribute to our rendering layer mask field.

```
[RenderingLayerMaskField]
	public int renderingLayerMask = -1;
```

现在创建一个扩展 PropertyDrawer 的自定义属性抽屉编辑器类，属性类型使用 CustomPropertyDrawer 属性。将 CustomLightEditor.DrawRenderingLayerMask 复制到该类中，重命名为 Draw，并将其设置为公共静态。然后赋予它三个参数：位置 Rect、序列化属性和 GUIContent 标签。使用这些参数调用 EditorGUI.MaskField 而不是 EditorGUILayout.MaskField。

```
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
public class RenderingLayerMaskDrawer : PropertyDrawer {

	public static void Draw (
		Rect position, SerializedProperty property, GUIContent label
	) {
		//SerializedProperty property = settings.renderingLayerMask;
		EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
		EditorGUI.BeginChangeCheck();
		int mask = property.intValue;
		if (mask == int.MaxValue) {
			mask = -1;
		}
		mask = EditorGUI.MaskField(
			position, label, mask,
			GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames
		);
		if (EditorGUI.EndChangeCheck()) {
			property.intValue = mask == -1 ? int.MaxValue : mask;
		}
		EditorGUI.showMixedValue = false;
	}
}
```

只有当属性的基础类型是 uint 时，我们才需要单独处理-1。如果属性的 type 属性等于 "uint"，就属于这种情况。

```
		int mask = property.intValue;
		bool isUint = property.type == "uint";
		if (isUint && mask == int.MaxValue) {
			mask = -1;
		}
		…
		if (EditorGUI.EndChangeCheck()) {
			property.intValue = isUint && mask == -1 ? int.MaxValue : mask;
		}
```

Then override the `OnGUI` method, simply forwarding its invocation to `Draw`.

```
	public override void OnGUI (
		Rect position, SerializedProperty property, GUIContent label
	) {
		Draw(position, property, label);
	}
```

为使 Draw 更易于使用，请添加一个不含 Rect 参数的版本。调用 EditorGUILayout.GetControlRect 从布局引擎获取单行位置矩形。

```
	public static void Draw (SerializedProperty property, GUIContent label) {
		Draw(EditorGUILayout.GetControlRect(), property, label);
	}
```

现在，我们可以删除 CustomLightEditor 中的 DrawRenderingLayerMask 方法，转而调用 RenderingLayerMaskDrawer.Draw 方法。

```
	public override void OnInspectorGUI() {
		base.OnInspectorGUI();
		//DrawRenderingLayerMask();
		RenderingLayerMaskDrawer.Draw(
			settings.renderingLayerMask, renderingLayerMaskLabel
		);
		
		…
	}

	//void DrawRenderingLayerMask () { … }
```

要应用摄像机的渲染层掩码，请在 CameraRenderer.DrawVisibleGeometry 中添加一个参数，并将其作为名为 renderingLayerMask 的参数传递给 FilteringSettings 构造函数方法，并将其转换为 uint。

```
	void DrawVisibleGeometry (
		bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
		int renderingLayerMask
	) {
		…

		var filteringSettings = new FilteringSettings(
			RenderQueueRange.opaque, renderingLayerMask: (uint)renderingLayerMask
		);

		…
	}
```

然后在渲染中调用 DrawVisibleGeometry（绘制可见几何体）时，将渲染图层遮罩传递给渲染图层。

```
		DrawVisibleGeometry(
			useDynamicBatching, useGPUInstancing, useLightsPerObject,
			cameraSettings.renderingLayerMask
		);
```

现在可以使用更灵活的渲染层掩码来控制摄像机渲染的内容。例如，我们可以让一些物体投射阴影，即使摄像机看不到它们，而不需要只显示阴影的特殊物体。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/multiple-cameras/rendering-layers/rendering-objects-not-affected-by-light.png)

需要注意的一点是，只有剔除蒙版才会被用于剔除，因此如果排除大量对象，普通剔除蒙版的效果会更好。

### Masking Lights Per Camera

虽然 Unity 的 RP 并不这样做，但除了几何体外，还可以屏蔽每个摄像机的灯光。我们将再次使用渲染图层来实现这一功能，但由于这是非标准行为，因此我们在 "摄像机设置"（CameraSettings）中添加了一个切换选项，使其成为可选项。

```
	public bool maskLights = false;
```

我们需要做的就是在 Lighting.SetupLights 中跳过蒙版灯光。为此方法添加一个渲染图层遮罩参数，然后检查每个灯光的渲染图层遮罩是否与提供的遮罩重叠。如果是，则进入 switch 语句设置灯光，否则跳过。

```
	void SetupLights (bool useLightsPerObject, int renderingLayerMask) {
		…
		for (i = 0; i < visibleLights.Length; i++) {
			int newIndex = -1;
			VisibleLight visibleLight = visibleLights[i];
			Light light = visibleLight.light;
			if ((light.renderingLayerMask & renderingLayerMask) != 0) {
				switch (visibleLight.lightType) {
					…
				}
			}
			if (useLightsPerObject) {
				indexMap[i] = newIndex;
			}
		}
		
		…
	}
```

`**Lighting**.Setup` must pass the rendering layer mask along.

```
	public void Setup (
		ScriptableRenderContext context, CullingResults cullingResults,
		ShadowSettings shadowSettings, bool useLightsPerObject, int renderingLayerMask
	) {
		…
		SetupLights(useLightsPerObject, renderingLayerMask);
		…
	}
```

此外，我们还必须在 CameraRenderer.Render 中提供摄像机的遮罩，但仅限于适用于灯光的情况，否则使用-1。

```
		lighting.Setup(
			context, cullingResults, shadowSettings, useLightsPerObject,
			cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1
		);
```

现在，我们可以让两台摄像机渲染同一个场景，但使用不同的光照，而无需在两者之间调整光照。这样也可以轻松地在世界原点渲染角色肖像等独立场景，而不会受到主场景灯光的影响。需要注意的是，这只适用于实时光照，完全烘焙的光照和混合光照的间接烘焙效果都无法遮蔽。