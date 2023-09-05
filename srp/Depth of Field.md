# **Depth of Field**

## Setting the Scene


我们之所以感知光线，是因为我们能感知到光子击中我们的视网膜。同样，相机之所以能记录光线，是因为光子击中了它们的底片或图像传感器。在所有情况下，光线都被聚焦以产生清晰的图像，但并非一切都可以同时处于焦点之内。只有特定距离上的物体才会处于焦点，而更近或更远的物体则会显得模糊不清。这种视觉效果被称为景深。模糊的投影外观细节被称为"bokeh"，这是日语中的"模糊"。

通常，我们用自己的眼睛不会注意到景深，因为我们关注的是我们正在专注的东西，而不是在焦点之外的事物。在照片和视频中，这一效果可能更加明显，因为我们可以查看相机没有聚焦的图像部分。尽管这是一种物理限制，但bokeh可以被巧妙地用来引导观众的注意力。因此，它是一种艺术工具。

GPU不需要聚焦光线，它们像具有无限焦点的完美相机一样运作。如果您想要创建清晰的图像，这是很棒的，但如果您想要将景深用于艺术目的，那就不太理想了。但有多种方法可以模拟它。在本教程中，我们将创建一个类似于Unity的后处理效果堆栈v2中找到的景深效果，尽管我们会尽量简化。

### Setting the Scene

为了测试我们自己的景深效果，请创建一个包含各种距离上的物体的场景。我使用了一个10×10的平面，使用了我们的电路材质，将其平铺了五次，作为地面。这样可以创建一个具有大面积、清晰、高频率颜色变化的表面。实际上，这是一个非常不好的材质，但对于测试来说非常适用。我在上面放了一堆物体，还在靠近相机的位置浮放了四个物体。您可以下载本节的资源包以获取这个场景，或者创建您自己的场景。

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/setting-the-scene/test-scene.png)

我们将使用与Bloom着色器相同的设置来创建新的DepthOfField着色器。您可以将其复制并缩减为仅执行一次blit的单个通道，目前只需如此。但是，这一次我们将把着色器放在隐藏菜单类别中，该类别不包括在着色器下拉列表中。这是唯一值得注意的新变化。

```
Shader "Hidden/DepthOfField" {
	Properties {
		_MainTex ("Texture", 2D) = "white" {}
	}

	CGINCLUDE
		#include "UnityCG.cginc"

		sampler2D _MainTex;
		float4 _MainTex_TexelSize;

		struct VertexData {
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
		};

		struct Interpolators {
			float4 pos : SV_POSITION;
			float2 uv : TEXCOORD0;
		};

		Interpolators VertexProgram (VertexData v) {
			Interpolators i;
			i.pos = UnityObjectToClipPos(v.vertex);
			i.uv = v.uv;
			return i;
		}

	ENDCG

	SubShader {
		Cull Off
		ZTest Always
		ZWrite Off

		Pass {
			CGPROGRAM
				#pragma vertex VertexProgram
				#pragma fragment FragmentProgram

				half4 FragmentProgram (Interpolators i) : SV_Target {
					return tex2D(_MainTex, i.uv);
				}
			ENDCG
		}
	}
}
```

创建一个最小的DepthOfFieldEffect组件，再次使用与泛光效果相同的方法，但将着色器属性隐藏起来。

```
using UnityEngine;
using System;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class DepthOfFieldEffect : MonoBehaviour {

	[HideInInspector]
	public Shader dofShader;

	[NonSerialized]
	Material dofMaterial;

	void OnRenderImage (RenderTexture source, RenderTexture destination) {
		if (dofMaterial == null) {
			dofMaterial = new Material(dofShader);
			dofMaterial.hideFlags = HideFlags.HideAndDontSave;
		}

		Graphics.Blit(source, destination, dofMaterial);
	}
}
```

与其要求手动分配正确的着色器，我们将其定义为组件的默认值。要这样做，选择编辑器中的脚本，并在检查器的顶部连接着色器字段。正如信息文本所提到的，这将确保在编辑器中将此组件添加到某个对象时，它将复制此引用，这非常方便。

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/setting-the-scene/default-shader-reference.png)

将我们的新效果附加到相机作为唯一的效果。再次假设我们正在线性HDR空间中渲染，因此相应地配置项目和相机。另外，因为我们需要从深度缓冲区中读取数据，所以在使用MSAA时，该效果不会正常工作。因此，请禁止相机使用MSAA。同时请注意，由于我们将依赖深度缓冲区，该效果不会考虑透明的几何形状。

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/setting-the-scene/camera.png)

透明物体看起来会受到影响，但会使用它们背后的物体的深度信息。这是所有使用深度缓冲区的技术的局限性。您仍然可以使用透明度，但只有在这些物体的背后有足够近的实体表面时，它才会看起来令人满意。这个表面将提供代理的深度信息。您还可以在应用效果之后渲染透明的几何形状，但前提是它们前面没有不透明的几何形状。

## Circle of Confusion

最简单的相机形式是完美的针孔相机。与所有相机一样，它有一个图像平面，光线被投射并记录在上面。在图像平面的前面是一个微小的孔，称为光圈，只足够让一束光线穿过它。位于相机前面的物体以多个方向发射或反射光线，产生了大量光线。对于每个点，只有一束光线能够穿过孔并被记录下来。

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/circle-of-confusion/perfect-pinhole.png)

投射的图像是否翻转？确实是的。所有由相机记录的图像，包括您的眼睛，都是翻转的。在进一步的处理过程中，图像会再次翻转，因此您不需要担心它。

因为每个点只捕捉到一束光线，所以图像总是清晰的。不幸的是，单束光线并不非常明亮，因此生成的图像几乎看不见。您需要等待一段时间，以积累足够的光线来获得清晰的图像，这意味着这种相机需要较长的曝光时间。当场景静止时，这不是问题，但即使稍微移动一下的物体也会产生大量的运动模糊。因此，这不是一台实用的相机，不能用来记录清晰的视频。

为了能够减少曝光时间，光线必须更快地积累。唯一的方法是同时记录多束光线。这可以通过增加光圈的半径来实现。假设孔是圆形的，这意味着每个点将以光锥而不是直线投射到图像平面上。因此，我们接收到更多的光，但它不再集中在单个点上。相反，它被投射为一个圆盘。覆盖的面积大小取决于点、孔和图像平面之间的距离。结果是一个更明亮但不聚焦的图像。

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/circle-of-confusion/pinhole-camera.png)

为了重新聚焦光线，我们必须以某种方式将进入的光锥带回到单个点。这是通过在相机的孔中放置一个透镜来实现的。透镜以一种方式弯曲光线，使散射的光线重新聚焦。这可以产生明亮而锐利的投影，但仅适用于距离相机固定距离的点。距离更远的点的光线不会聚焦得足够好，而距离相机太近的点的光线聚焦得太多。在这两种情况下，我们都会再次将点投射为圆盘，其大小取决于它们失焦的程度。这种失焦的投影被称为circle of confusion，简称**CoC**。

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/circle-of-confusion/lens-camera.png)

### Visualizing the CoC

焦外圆的半径是衡量点的投影失焦程度的指标。让我们从可视化这个值开始。向DepthOfFieldEffect添加一个常量，表示我们的第一个通道，焦外圆通道。在复制时明确使用这个通道。

```c#
	const int circleOfConfusionPass = 0;

	…

	void OnRenderImage (RenderTexture source, RenderTexture destination) {
		…

		Graphics.Blit(source, destination, dofMaterial, circleOfConfusionPass);
	}
```

由于焦外圆依赖于距离相机的距离，我们需要从深度缓冲区中读取数据。事实上，深度缓冲区正是我们所需的，因为相机的焦点区域是与相机平行的一个平面，假设透镜和图像平面对齐且完美。因此，从深度纹理中采样，将其转换为线性深度并渲染。

```
	CGINCLUDE
		#include "UnityCG.cginc"

		sampler2D _MainTex, _CameraDepthTexture;
		…

	ENDCG

	SubShader {
		…

		Pass { // 0 circleOfConfusionPass
			CGPROGRAM
				#pragma vertex VertexProgram
				#pragma fragment FragmentProgram

				half4 FragmentProgram (Interpolators i) : SV_Target {
//					return tex2D(_MainTex, i.uv);
					half depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
					depth = LinearEyeDepth(depth);
					return depth;
				}
			ENDCG
		}
	}
```

### Choosing a Simple CoC

我们不关心原始深度值，而是焦外圆的值。为了确定这个值，我们必须决定一个焦点距离。这是相机和焦点平面之间的距离，也就是一切都完全清晰的地方。在我们的效果组件中添加一个公共字段，并使用0.1到100的范围，并将默认值设置为10。

```
	[Range(0.1f, 100f)]
	public float focusDistance = 10f;
```

焦外圆的大小随着点到焦点平面的距离增加而增加。确切的关系取决于相机及其配置，这可能会变得相当复杂。虽然可以模拟真实的相机，但为了更容易理解和控制，我们将使用一个简单的焦点范围。我们的焦外圆将在这个范围内从零增加到最大值，相对于焦点距离。为其添加一个字段，范围可以设置为0.1到10，默认值为3。

```
	[Range(0.1f, 10f)]
	public float focusRange = 3f;
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/circle-of-confusion/focus-distance-range.png)

这些配置选项在我们的着色器中是必需的，所以在我们进行blit之前设置它们。

```
		dofMaterial.SetFloat("_FocusDistance", focusDistance);
		dofMaterial.SetFloat("_FocusRange", focusRange);

		Graphics.Blit(source, destination, dofMaterial, circleOfConfusionPass);
```

将所需的变量添加到着色器中。由于我们将使用它们来处理深度缓冲区，所以在计算焦外圆时，我们将使用float作为它们的类型。由于我们使用基于half的HDR缓冲区，所以对于其他值，我们将继续使用half，尽管在桌面硬件上这并不重要。

```
		sampler2D _MainTex, _CameraDepthTexture;
		float4 _MainTex_TexelSize;

		float _FocusDistance, _FocusRange;
```

利用深度值d、焦点距离f和焦点范围r，我们可以通过**(d - f)/r**来计算焦外圆的值。

```
				half4 FragmentProgram (Interpolators i) : SV_Target {
					float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
					depth = LinearEyeDepth(depth);
//					return depth;


				}
```

这将导致焦点距离之后的点产生正的焦外圆值，而焦点距离之前的点产生负的焦外圆值。值-1和1表示最大的焦外圆值，因此我们应该通过夹紧确保焦外圆的值不超过这个范围。

					float coc = (depth - _FocusDistance) / _FocusRange;
					coc = clamp(coc, -1, 1);
					return coc;

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/circle-of-confusion/coc.png)

我们保留负的焦外圆值，以便我们可以区分前景和背景点。要看到负的焦外圆值，您可以将它们着色，比如使用红色。

```
					coc = clamp(coc, -1, 1);
					if (coc < 0) {
						return coc * -half4(1, 0, 0, 1);
					}
					return coc;
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/circle-of-confusion/coc-negative-red.png)

### Buffering the CoC


我们将需要焦外圆来缩放点的投影，但我们将在另一个通道中完成这个操作。因此，我们将焦外圆值存储在临时缓冲区中。因为我们只需要存储一个单一值，所以我们可以使用单通道纹理，使用RenderTextureFormat.RHalf。此外，这个缓冲区包含焦外圆数据，而不是颜色值。因此，它应该始终被视为线性数据。尽管我们假设我们是在线性空间中渲染，但让我们明确表示这一点。

首先进行到焦外圆缓冲区的blit操作，然后添加一个新的blit操作将该缓冲区复制到目标中。最后，释放缓冲区。

```
		RenderTexture coc = RenderTexture.GetTemporary(
			source.width, source.height, 0,
			RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear
		);

		Graphics.Blit(source, coc, dofMaterial, circleOfConfusionPass);
		Graphics.Blit(coc, destination);

		RenderTexture.ReleaseTemporary(coc);
```

由于我们使用的纹理只有一个R通道，所以整个焦外圆的可视化现在是红色的。我们需要存储实际的焦外圆值，所以取消负值的颜色处理。另外，我们可以将片段函数的返回类型更改为单个值。

```
//				half4
				half FragmentProgram (Interpolators i) : SV_Target {
					float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
					depth = LinearEyeDepth(depth);

					float coc = (depth - _FocusDistance) / _FocusRange;
					coc = clamp(coc, -1, 1);
//					if (coc < 0) {
//						return coc * -half4(1, 0, 0, 1);
//					}
					return coc;
				}
```

## Bokeh


虽然焦外圆决定了每个点的虚化 效果的强度，但光圈决定了它的外观。基本上，一幅图像由光圈形状在图像平面上的许多投影组成。因此，创建虚化 的一种方法是使用其颜色为每个纹素渲染一个精灵，其大小和不透明度基于其焦外圆。实际上，在某些情况下确实使用了这种方法，但由于大量的overdraw，这可能会非常昂贵。

另一种方法是以相反的方向工作。与将单个片段投影到多个片段不同，每个片段从可能影响它的所有纹素累积颜色。这种技术不需要生成额外的几何图形，但需要进行多次纹理采样。我们将使用这种方法。

### Accumulating the Bokeh

创建一个用于生成虚化 效果的新通道。首先，简单地传递主纹理的颜色。我们不关心其 alpha 通道。

```
		Pass { // 0 CircleOfConfusionPass
			…
		}

		Pass { // 1 bokehPass
			CGPROGRAM
				#pragma vertex VertexProgram
				#pragma fragment FragmentProgram

				half4 FragmentProgram (Interpolators i) : SV_Target {
					half3 color = tex2D(_MainTex, i.uv).rgb;
					return half4(color, 1);
				}
			ENDCG
		}
```

在第二次和最后一次blit中，使用这个通道，并将源纹理作为输入。我们将暂时忽略CoC数据，假设整个图像完全失焦。

```
	const int circleOfConfusionPass = 0;
	const int bokehPass = 1;

	…

	void OnRenderImage (RenderTexture source, RenderTexture destination) {
		…

		Graphics.Blit(source, coc, dofMaterial, circleOfConfusionPass);
		Graphics.Blit(source, destination, dofMaterial, bokehPass);
//		Graphics.Blit(coc, destination);

		RenderTexture.ReleaseTemporary(coc);
	}
```

要创建虚化 效果，我们必须对我们正在处理的片段周围的颜色进行平均。我们将从以当前片段为中心的一个9×9纹素块的平均值开始。这总共需要81个采样。

```
				half4 FragmentProgram (Interpolators i) : SV_Target {
//					half3 color = tex2D(_MainTex, i.uv).rgb;
					half3 color = 0;
					for (int u = -4; u <= 4; u++) {
						for (int v = -4; v <= 4; v++) {
							float2 o = float2(u, v) * _MainTex_TexelSize.xy;
							color += tex2D(_MainTex, i.uv + o).rgb;
						}
					}
					color *= 1.0 / 81;
					return half4(color, 1);
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/bokeh/square-bokeh.png)


结果是一个像块状的图像。实际上，我们使用的是一个正方形光圈。图像也变得更亮，因为非常明亮的片段的影响在一个更大的区域上分散开来。这更像是泛光而不是景深，但夸张的效果使人更容易看出发生了什么。因此，我们将保持它过亮，并稍后降低亮度。

因为我们简单的虚化 方法是基于纹素大小的，所以它的视觉大小取决于目标分辨率。降低分辨率会增加纹素大小，从而增加光圈和虚化 效果。在本教程的其余部分，我将使用半分辨率的屏幕截图，以便更容易看到单个纹素。因此，虚化 形状的尺寸会增加一倍。

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/bokeh/square-bokeh-half-resolution.png)

在一个9×9纹素块中收集样本已经需要81个样本，这是很多了。如果我们想要将虚化 的尺寸加倍，我们需要将它增加到17×17。这将需要每个片段289个样本，这实在太多了。但是，我们可以通过简单地将采样偏移加倍来增加采样区域而不增加样本数量。

```
			float2 o = float2(u, v) * _MainTex_TexelSize.xy * 2;
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/bokeh/square-bokeh-sparse.png)

现在我们已经将光圈的半径加倍了，但我们采样的样本数量太少，无法完全覆盖它。由于采样不足，光圈的投影被分解，变成了一个点状区域。这就好像我们使用的是一个具有9×9个孔的相机，而不是一个单一的光圈。尽管这看起来不太好，但它允许我们清楚地看到单个样本。

### Round Bokeh

理想的光圈是圆形的，而不是方形的，产生的虚化 由许多重叠的圆盘组成。我们可以通过简单地丢弃那些偏移太大的样本来将我们的虚化 形状变成半径为四个步长的圆盘。包括多少样本决定了累积颜色的权重，我们可以用它来进行归一化。

```
					half3 color = 0;
					float weight = 0;
					for (int u = -4; u <= 4; u++) {
						for (int v = -4; v <= 4; v++) {
//							float2 o = float2(u, v) * _MainTex_TexelSize.xy * 2;
							float2 o = float2(u, v);
							if (length(o) <= 4) {
								o *= _MainTex_TexelSize.xy * 2;
								color += tex2D(_MainTex, i.uv + o).rgb;
								weight += 1;
							}
						}
					}
					color *= 1.0 / weight;
					return half4(color, 1);
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/bokeh/round-bokeh.png)

与检查每个样本是否有效相比，我们可以定义一个包含所有重要偏移的数组，并循环遍历它。这也意味着我们不受限于规则网格。由于我们要采样一个圆盘，因此使用螺旋或同心圆的模式更有意义。而不是自己设计模式，让我们使用Unity的后处理效果堆栈v2的一个采样内核，定义在DiskKernels.hlsl中。这些内核包含单位圆内的偏移。最小的内核包含16个样本：一个中心点，周围有5个样本的环以及周围有10个样本的另一个环。

```
				// From https://github.com/Unity-Technologies/PostProcessing/
				// blob/v2/PostProcessing/Shaders/Builtins/DiskKernels.hlsl
				static const int kernelSampleCount = 16;
				static const float2 kernel[kernelSampleCount] = {
					float2(0, 0),
					float2(0.54545456, 0),
					float2(0.16855472, 0.5187581),
					float2(-0.44128203, 0.3206101),
					float2(-0.44128197, -0.3206102),
					float2(0.1685548, -0.5187581),
					float2(1, 0),
					float2(0.809017, 0.58778524),
					float2(0.30901697, 0.95105654),
					float2(-0.30901703, 0.9510565),
					float2(-0.80901706, 0.5877852),
					float2(-1, 0),
					float2(-0.80901694, -0.58778536),
					float2(-0.30901664, -0.9510566),
					float2(0.30901712, -0.9510565),
					float2(0.80901694, -0.5877853),
				};

				half4 FragmentProgram (Interpolators i) : SV_Target {
					…
				}
```

循环遍历这些偏移并使用它们来累积样本。为了保持相同的圆盘半径，将偏移乘以8。

```
				half4 FragmentProgram (Interpolators i) : SV_Target {
					half3 color = 0;
//					float weight = 0;
//					for (int u = -4; u <= 4; u++) {
//						for (int v = -4; v <= 4; v++) {
//							…
//						}
//					}
					for (int k = 0; k < kernelSampleCount; k++) {
						float2 o = kernel[k];
						o *= _MainTex_TexelSize.xy * 8;
						color += tex2D(_MainTex, i.uv + o).rgb;
					}
					color *= 1.0 / kernelSampleCount;
					return half4(color, 1);
				}
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/bokeh/disk-kernel-small.png)

内核具有的样本数越多，质量越高。DiskKernels.hlsl包含了一些内核，您可以复制它们并进行比较。在本教程中，我将使用中等内核，它也有两个环，但总共有22个样本。

```
				#define BOKEH_KERNEL_MEDIUM

				// From https://github.com/Unity-Technologies/PostProcessing/
				// blob/v2/PostProcessing/Shaders/Builtins/DiskKernels.hlsl
				#if defined(BOKEH_KERNEL_SMALL)
					static const int kernelSampleCount = 16;
					static const float2 kernel[kernelSampleCount] = {
						…
					};
				#elif defined (BOKEH_KERNEL_MEDIUM)
					static const int kernelSampleCount = 22;
					static const float2 kernel[kernelSampleCount] = {
						float2(0, 0),
						float2(0.53333336, 0),
						float2(0.3325279, 0.4169768),
						float2(-0.11867785, 0.5199616),
						float2(-0.48051673, 0.2314047),
						float2(-0.48051673, -0.23140468),
						float2(-0.11867763, -0.51996166),
						float2(0.33252785, -0.4169769),
						float2(1, 0),
						float2(0.90096885, 0.43388376),
						float2(0.6234898, 0.7818315),
						float2(0.22252098, 0.9749279),
						float2(-0.22252095, 0.9749279),
						float2(-0.62349, 0.7818314),
						float2(-0.90096885, 0.43388382),
						float2(-1, 0),
						float2(-0.90096885, -0.43388376),
						float2(-0.6234896, -0.7818316),
						float2(-0.22252055, -0.974928),
						float2(0.2225215, -0.9749278),
						float2(0.6234897, -0.7818316),
						float2(0.90096885, -0.43388376),
					};
				#endif
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/bokeh/disk-kernel-medium.png)

这为我们提供了一个更高质量的内核，同时仍然容易区分两个样本环，因此我们可以看到我们的效果是如何工作的。

### Blurring Bokeh

尽管专用的采样内核比使用常规网格要好，但仍然需要大量的样本才能获得合理的虚化 效果。为了在相同数量的样本下覆盖更多区域，我们可以像泛光效果一样以半分辨率创建效果。这将略微模糊虚化 ，但这是可以接受的代价。

要在半分辨率下工作，我们首先必须将图像复制到一个半尺寸的纹理上，在该分辨率下创建虚化 效果，然后再次复制到全分辨率。这需要两个额外的临时纹理。

```
		int width = source.width / 2;
		int height = source.height / 2;
		RenderTextureFormat format = source.format;
		RenderTexture dof0 = RenderTexture.GetTemporary(width, height, 0, format);
		RenderTexture dof1 = RenderTexture.GetTemporary(width, height, 0, format);

		Graphics.Blit(source, coc, dofMaterial, circleOfConfusionPass);
		Graphics.Blit(source, dof0);
		Graphics.Blit(dof0, dof1, dofMaterial, bokehPass);
		Graphics.Blit(dof1, destination);
//		Graphics.Blit(source, destination, dofMaterial, bokehPass);

		RenderTexture.ReleaseTemporary(coc);
		RenderTexture.ReleaseTemporary(dof0);
		RenderTexture.ReleaseTemporary(dof1);
```

为了保持相同的虚化 大小，我们还必须将采样偏移减半。

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/bokeh/half-resolution-bokeh.png)

现在我们得到了更加坚实的虚化 效果，但样本仍然没有完全连接在一起。我们要么必须减小虚化 的大小，要么增加模糊度。与其进一步降采样，我们将在创建虚化 之后添加一个额外的模糊通道，以便进行后处理模糊通道。

```
	const int circleOfConfusionPass = 0;
	const int bokehPass = 1;
```

我们以半分辨率执行后处理模糊通道，可以重用第一个临时半尺寸纹理。

```
		Graphics.Blit(source, coc, dofMaterial, circleOfConfusionPass);
		Graphics.Blit(source, dof0);
		Graphics.Blit(dof0, dof1, dofMaterial, bokehPass);
		Graphics.Blit(dof1, dof0, dofMaterial, postFilterPass);
		Graphics.Blit(dof0, destination);
```

后处理模糊通道将通过使用半纹素偏移的盒状滤波器在相同分辨率下执行小的高斯模糊。这会导致样本重叠，创建一个称为帐篷滤波器的3×3内核。

![sampling](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/bokeh/tent-filter.png)

```
		Pass { // 2 postFilterPass
			CGPROGRAM
				#pragma vertex VertexProgram
				#pragma fragment FragmentProgram

				half4 FragmentProgram (Interpolators i) : SV_Target {
					float4 o = _MainTex_TexelSize.xyxy * float2(-0.5, 0.5).xxyy;
					half4 s =
						tex2D(_MainTex, i.uv + o.xy) +
						tex2D(_MainTex, i.uv + o.zy) +
						tex2D(_MainTex, i.uv + o.xw) +
						tex2D(_MainTex, i.uv + o.zw);
					return s * 0.25;
				}
			ENDCG
		}
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/bokeh/post-filter.png)

### Bokeh Size

由于后处理模糊通道，我们的虚化 在半分辨率的四个纹素半径下看起来是可接受的。虚化 并不是完全平滑的，这可以解释为不完美或脏镜头的效果。但只有在非常明亮的投影位于较暗的背景之上时，这种效果才真正可见，而我们目前正在极大地夸大这种效果。但您可能更喜欢在减小的尺寸上获得更高质量的虚化 ，或者更喜欢一个更大但质量较差的虚化 。因此，让我们通过一个字段使虚化 半径可配置，范围为1到10，默认值为4，以半分辨率纹素表示。我们不应该使用小于1的半径，因为那样我们主要只会得到降采样的模糊效果，而不是虚化 效果。

```
	[Range(1f, 10f)]
	public float bokehRadius = 4f;
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/bokeh/bokeh-radius.png)

将半径传递给着色器。

```
		dofMaterial.SetFloat("_BokehRadius", bokehRadius);
		dofMaterial.SetFloat("_FocusDistance", focusDistance);
		dofMaterial.SetFloat("_FocusRange", focusRange);
```

为此添加一个着色器变量，再次使用float，因为它用于纹理采样。

```
	float _BokehRadius, _FocusDistance, _FocusRange;
```

最后，使用可配置的半径，而不是固定值4。

```
						float2 o = kernel[k];
						o *= _MainTex_TexelSize.xy * _BokehRadius;
```

https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/bokeh/radius.mp4

## Focusing

到目前为止，我们可以确定焦外圆的大小，并且可以创建最大尺寸的虚化 效果。下一步是将这些组合起来，以呈现可变的虚化 ，模拟相机的焦点。

### Downsampling CoC

因为我们在半分辨率上创建虚化 ，所以我们也需要半分辨率的焦外圆数据。默认的blit或纹理采样只是对相邻的纹素进行平均，这对于深度值或从深度值派生的东西，如焦外圆，没有意义。因此，我们必须自己进行降采样，使用自定义的预过滤通道。

```
	const int circleOfConfusionPass = 0;
	const int preFilterPass = 1;
	const int bokehPass = 2;
	const int postFilterPass = 3;
```

除了源纹理之外，预过滤通道还需要从焦外圆纹理中读取数据。因此，在降采样之前将其传递给着色器。

```
		dofMaterial.SetTexture("_CoCTex", coc);

		Graphics.Blit(source, coc, dofMaterial, circleOfConfusionPass);
		Graphics.Blit(source, dof0, dofMaterial, preFilterPass);
		Graphics.Blit(dof0, dof1, dofMaterial, bokehPass);
		Graphics.Blit(dof1, dof0, dofMaterial, postFilterPass);
		Graphics.Blit(dof0, destination);
```

在着色器中添加相应的纹理变量。

```
		sampler2D _MainTex, _CameraDepthTexture, _CoCTex;
```

接下来，创建一个新的通道来执行降采样。颜色数据可以通过单个纹理采样来获取。但是焦外圆值需要特别注意。首先，从与低分辨率纹素相对应的四个高分辨率纹素中进行采样，并将它们平均。将结果存储在alpha通道中。

```
		Pass { // 1 preFilterPass
			CGPROGRAM
				#pragma vertex VertexProgram
				#pragma fragment FragmentProgram

				half4 FragmentProgram (Interpolators i) : SV_Target {
					float4 o = _MainTex_TexelSize.xyxy * float2(-0.5, 0.5).xxyy;
					half coc0 = tex2D(_CoCTex, i.uv + o.xy).r;
					half coc1 = tex2D(_CoCTex, i.uv + o.zy).r;
					half coc2 = tex2D(_CoCTex, i.uv + o.xw).r;
					half coc3 = tex2D(_CoCTex, i.uv + o.zw).r;
					
					half coc = (coc0 + coc1 + coc2 + coc3) * 0.25;

					return half4(tex2D(_MainTex, i.uv).rgb, coc);
				}
			ENDCG
		}

		Pass { // 2 bokehPass
			…
		}

		Pass { // 3 postFilterPass
			…
		}
```

这将是一个常规的降采样，但我们不希望这样。相反，我们将只取四个纹素中最极端的焦外圆值，无论是正值还是负值。

```
//					half coc = (coc0 + coc1 + coc2 + coc3) * 0.25;
					half cocMin = min(min(min(coc0, coc1), coc2), coc3);
					half cocMax = max(max(max(coc0, coc1), coc2), coc3);
					half coc = cocMax >= -cocMin ? cocMax : cocMin;
```

### Using the Correct CoC

要使用正确的焦外圆半径，我们在第一次通道中计算焦外圆值时必须将焦外圆值按虚化 半径进行缩放。

```
					float coc = (depth - _FocusDistance) / _FocusRange;
					coc = clamp(coc, -1, 1) * _BokehRadius;
```

为了确定一个内核样本是否对片段的虚化 产生贡献，我们必须检查该样本的焦外圆是否与这个片段重叠。我们需要知道用于该样本的内核半径，它就是其偏移的长度，因此进行计算。我们以纹素为单位来测量这个值，所以我们必须在补偿纹素大小之前进行这个计算。

```
					for (int k = 0; k < kernelSampleCount; k++) {
						float2 o = kernel[k] * _BokehRadius;
//						o *= _MainTex_TexelSize.xy * _BokehRadius;
						half radius = length(o);
						o *= _MainTex_TexelSize.xy;
						color += tex2D(_MainTex, i.uv + o).rgb;
					}
```

我们是否必须为每个样本计算半径呢？ 您也可以预先计算它们并将其存储在常量数组中，以及偏移量一起。

如果样本的焦外圆至少与其偏移所使用的内核半径一样大，那么该点的投影将重叠在片段上。如果不是，那么该点不会影响到这个片段，应该跳过。这意味着我们必须再次跟踪累积颜色的权重以进行归一化。

```
					half3 color = 0;
					half weight = 0;
					for (int k = 0; k < kernelSampleCount; k++) {
						float2 o = kernel[k] * _BokehRadius;
						half radius = length(o);
						o *= _MainTex_TexelSize.xy;
//						color += tex2D(_MainTex, i.uv + o).rgb;
						half4 s = tex2D(_MainTex, i.uv + o);

						if (abs(s.a) >= radius) {
							color += s.rgb;
							weight += 1;
						}
					}
					color *= 1.0 / weight;
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/focusing/bokeh-based-on-coc.png)

### Smoothing the Sampling

我们的图像现在包含不同大小的虚化 圆盘，但大小之间的过渡是突然的。要了解为什么会发生这种情况，增加虚化 的大小，以便您可以看到单个样本，并调整焦点范围或距离。


内核中同一环的样本倾向于具有大致相同的焦外圆值，这意味着它们倾向于同时被丢弃或包含。结果是，我们主要有三种情况：没有环、一个环和两个环。还有一种情况是，发现两个内核样本永远不会被包含，这意味着它们的未缩放内核半径实际上略大于1。

通过放宽包含样本的条件，我们可以缓解这两个问题。与其完全丢弃样本，我们将为它们分配一个在0-1范围内的权重。这个权重取决于焦外圆和偏移半径，我们可以使用一个单独的函数来计算。

```
				half Weigh (half coc, half radius) {
					return coc >= radius;
				}

				half4 FragmentProgram (Interpolators i) : SV_Target {
					half3 color = 0;
					half weight = 0;
					for (int k = 0; k < kernelSampleCount; k++) {
						…

//						if (abs(s.a) >= radius) {
//							color += s;
//							weight += 1;
//						}
						half sw = Weigh(abs(s.a), radius);
						color += s.rgb * sw;
						weight += sw;
					}
					color *= 1.0 / weight;
					return half4(color, 1);
				}
```

作为一个权重函数，我们可以使用焦外圆减去半径，然后将其夹在0-1之间。通过添加一个小值，然后除以它，我们引入了一个偏移量并将其转化为陡峭的坡度。结果是过渡更加平滑，所有内核样本都可以被包含。

```
				half Weigh (half coc, half radius) {
//					return coc >= radius;
					return saturate((coc - radius + 2) / 2);
				}
```

### Staying in Focus


以半分辨率工作的一个缺点是整个图像都会被降采样，这会强制施加最低模糊量。但是，处于焦点的片段不应受深度场景效果的影响。为了保持这些片段清晰，我们必须将半分辨率效果与全分辨率源图像相结合，并根据焦外圆在它们之间进行混合。

用一个从源到目标的新合并通道来替换我们效果的最终blit。这个通道还需要访问最终的深度场景纹理，所以也将其传递给着色器。

```
	const int circleOfConfusionPass = 0;
	const int preFilterPass = 1;
	const int bokehPass = 2;
	const int postFilterPass = 3;
	const int combinePass = 4;

	…

	void OnRenderImage (RenderTexture source, RenderTexture destination) {
		…

		dofMaterial.SetTexture("_CoCTex", coc);
		dofMaterial.SetTexture("_DoFTex", dof0);

		Graphics.Blit(source, coc, dofMaterial, circleOfConfusionPass);
		Graphics.Blit(source, dof0, dofMaterial, preFilterPass);
		Graphics.Blit(dof0, dof1, dofMaterial, bokehPass);
		Graphics.Blit(dof1, dof0, dofMaterial, postFilterPass);
		Graphics.Blit(source, destination, dofMaterial, combinePass);
//		Graphics.Blit(dof0, destination);

		…
	}
```

添加一个用于深度场景效果纹理的变量。

```
		sampler2D _MainTex, _CameraDepthTexture, _CoCTex, _DoFTex;
```

然后创建新的通道，最初执行源的简单传递。

```
		Pass { // 4 combinePass
			CGPROGRAM
				#pragma vertex VertexProgram
				#pragma fragment FragmentProgram

				half4 FragmentProgram (Interpolators i) : SV_Target {
					half4 source = tex2D(_MainTex, i.uv);

					half3 color = source.rgb;
					return half4(color, source.a);
				}
			ENDCG
		}
```

源颜色是完全在焦点中的。深度场景效果纹理包含了焦点和非焦点片段的混合，但即使它的焦点片段也被模糊了。因此，我们必须对所有焦点片段使用源。但我们不能突然从一个纹理切换到另一个，我们必须混合过渡。假设绝对焦外圆小于0.1的片段完全在焦点中，并应该使用源纹理。假设绝对焦外圆大于1的片段应完全使用深度场景效果纹理。我们使用smoothstep函数在它们之间进行混合。

```
				half4 source = tex2D(_MainTex, i.uv);
					half coc = tex2D(_CoCTex, i.uv).r;
					half4 dof = tex2D(_DoFTex, i.uv);

					half dofStrength = smoothstep(0.1, 1, abs(coc));
					half3 color = lerp(source.rgb, dof.rgb, dofStrength);
					return half4(color, source.a );
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/focusing/in-focus.png)

过渡区域是任意的。在我们的情况下，使用1作为上界意味着当焦外圆的半径为一个纹素时，深度场景效果的强度最大。您可以增加混合范围，以超越这一点，更积极地在锐化数据和虚化 数据之间进行混合。减小混合范围将显示更多只受降采样模糊影响的深度场景效果纹理。

### Splitting Foreground and Background


不幸的是，当有一个非焦点的前景位于焦点的背景前面时，与源图像混合会产生不正确的结果。这是因为前景应该部分投影在背景之上，我们的深度场景效果确实做到了，但是基于背景的焦外圆，我们选择使用源图像，这样我们就消除了这一点。为了处理这个问题，我们需要将前景和背景分开。

让我们首先只包括在背景中的内核样本，即当它们的焦外圆是正数时。在权重样本时，我们可以使用0和焦外圆的最大值，而不是绝对焦外圆。由于这可能导致零样本，所以要确保保持除法有效，例如当权重为0时，添加1。

```
				half4 FragmentProgram (Interpolators i) : SV_Target {
					half3 bgColor = 0;
					half bgWeight = 0;
					for (int k = 0; k < kernelSampleCount; k++) {
						float2 o = kernel[k] * _BokehRadius;
						half radius = length(o);
						o *= _MainTex_TexelSize.xy;
						half4 s = tex2D(_MainTex, i.uv + o);

						half bgw = Weigh(max(0, s.a), radius);
						bgColor += s.rgb * bgw;
						bgWeight += bgw;
					}
					bgColor *= 1 / (bgWeight + (bgWeight == 0));
					
					half3 color = bgColor;
					return half4(color, 1);
				}
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/focusing/background.png)

这消除了前景的贡献，但现在我们可以看到背景的一部分被投影到前景上。这不应该发生，因为前景挡住了视线。我们可以通过使用样本的焦外圆和我们正在处理的片段的焦外圆的最小值来消除这些情况。

```
					half coc = tex2D(_MainTex, i.uv).a;
					
					half3 bgColor = 0;
					half bgWeight = 0;
					for (int k = 0; k < kernelSampleCount; k++) {
						…

						half bgw = Weigh(max(0, min(s.a, coc)), radius);
						bgColor += s.rgb * bgw;
						bgWeight += bgw;
					}
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/focusing/no-foreground.png)

接下来，也要跟踪前景的颜色。前景的权重基于负焦外圆，因此它变成了正数。

```
					half3 bgColor = 0, fgColor = 0;
					half bgWeight = 0, fgWeight = 0;
					for (int k = 0; k < kernelSampleCount; k++) {
						float2 o = kernel[k] * _BokehRadius;
						half radius = length(o);
						o *= _MainTex_TexelSize.xy;
						half4 s = tex2D(_MainTex, i.uv + o);

						half bgw = Weigh(max(0, s.a), radius);
						bgColor += s.rgb * bgw;
						bgWeight += bgw;

						half fgw = Weigh(-s.a, radius);
						fgColor += s.rgb * fgw;
						fgWeight += fgw;
					}
					bgColor *= 1 / (bgWeight + (bgWeight == 0));
					fgColor *= 1 / (fgWeight + (fgWeight == 0));
					half3 color = fgColor;
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/focusing/foreground.png)

### Recombing Foreground and Background

我们不打算将前景和背景放在单独的缓冲区中。相反，我们将数据保留在一个单一的缓冲区中。因为前景位于背景前面，所以当我们至少有一个前景样本时，我们将使用它。我们可以通过根据前景权重来插值背景和前景之间的差异，最多为1，来实现这一点。

```
					fgColor *= 1 / (fgWeight + (fgWeight == 0));
					half bgfg = min(1, fgWeight);
					half3 color = lerp(bgColor, fgColor, bgfg);
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/focusing/recombined.png)

要修复与源图像的混合，我们必须修改与前景混合的方式。组合通道需要知道前景和背景是如何混合的，使用bgfg插值器，因此将其放在DoF纹理的alpha通道中。

```
					half bgfg = min(1, fgWeight);
					half3 color = lerp(bgColor, fgColor, bgfg);
					return half4(color, bgfg);
```

在组合通道中，我们现在首先必须根据正焦外圆进行插值，以考虑背景。然后，我们必须再次使用前景权重来插值结果和DoF，从而使DoF稍微更强烈。这导致了源和DoF之间的非线性插值。

```
//					half dofStrength = smoothstep(0.1, 1, abs(coc));
					half dofStrength = smoothstep(0.1, 1, coc);
					half3 color = lerp(
						source.rgb, dof.rgb,
						dofStrength + dof.a - dofStrength * dof.a
					);
```

![image-20230901174548592](D:\Games\ScriptRenderPipeline\image-20230901174548592.png)

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/focusing/preserved-foreground.png)

现在前景主导了图像，再次抹去了焦点区域。这是因为即使一个内核样本属于前景，它也是全力以赴的。为了使它成比例，将bgfg除以总样本量。

```
		half bgfg = min(1, fgWeight / kernelSampleCount);
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/focusing/proportional-foreground.png)

这使得前景变得太弱了，并且还导致了在其边缘出现了伪影。我们必须再次提高它，使用一些因子。由于我们正在处理一个圆盘，让我们使用π。这使前景比拆分前更强大，但不会太糟糕。如果您发现效果太强或太弱，可以尝试另一个因子。

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/focusing/boosted-foreground.png)

不幸的是，在前景的边缘仍然会出现一些伪影。在上面的屏幕截图中，沿平面的左下角非常明显。这是由于突然过渡到远处也不在焦点内的背景引起的。在这里，大部分内核样本最终会进入背景，削弱前景的影响。为了解决这个问题，我们可以在组合通道中再次使用绝对焦外圆。

```
		half dofStrength = smoothstep(0.1, 1, abs(coc));
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/focusing/fixed-foreground.png)

Toning Down the Bokeh

最后，我们通过减弱Bokeh的强度来结束。它不应显著改变图像的整体亮度。我们可以通过在预过滤通道中进行降采样时使用加权平均来实现这一点，而不仅仅是对四个纹理的颜色进行平均。我们将根据权重每个颜色c使用![image-20230901174738469](C:\Users\admin\AppData\Roaming\Typora\typora-user-images\image-20230901174738469.png)

```
				half Weigh (half3 c) {
					return 1 / (1 + max(max(c.r, c.g), c.b));
				}

				half4 FragmentProgram (Interpolators i) : SV_Target {
					float4 o = _MainTex_TexelSize.xyxy * float2(-0.5, 0.5).xxyy;

					half3 s0 = tex2D(_MainTex, i.uv + o.xy).rgb;
					half3 s1 = tex2D(_MainTex, i.uv + o.zy).rgb;
					half3 s2 = tex2D(_MainTex, i.uv + o.xw).rgb;
					half3 s3 = tex2D(_MainTex, i.uv + o.zw).rgb;

					half w0 = Weigh(s0);
					half w1 = Weigh(s1);
					half w2 = Weigh(s2);
					half w3 = Weigh(s3);

					half3 color = s0 * w0 + s1 * w1 + s2 * w2 + s3 * w3;
					color /= max(w0 + w1 + w2 + s3, 0.00001);

					half coc0 = tex2D(_CoCTex, i.uv + o.xy).r;
					…
					half coc = cocMax >= -cocMin ? cocMax : cocMin;

//					return half4(tex2D(_MainTex, i.uv).rgb, coc);
					return half4(color, coc);
				}
```

![img](https://catlikecoding.com/unity/tutorials/advanced-rendering/depth-of-field/focusing/weighed-bokeh.png)

现在您拥有一个简单的景深效果，它与Unity的后处理效果堆栈v2中的效果相当。要将其转化为生产质量的效果，需要进行大量调整，或者您可以使用了解其工作原理的知识来微调Unity的版本。