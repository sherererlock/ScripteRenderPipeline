# **Fog**

## Forward Fog

到目前为止，我们一直将光线视为穿过真空传播。当您的场景设定在太空时，这可能是准确的，但除此之外，光线必须穿过大气或液体。在这些情况下，光线可以在空间中的任何地方被吸收、散射和反射，而不仅仅是在击中固体表面时发生。

准确渲染大气干扰需要一种昂贵的体积方法，这通常是我们无法承受的。相反，我们将采用一种只依赖于几个恒定雾参数的近似方法。它被称为雾，因为该效果通常用于雾气弥漫的大气。由清晰大气引起的视觉扭曲通常是如此微妙，以至于可以在较短距离内忽略不计。

### Standard Fog

Unity 的 Lighting 窗口包含了场景的雾设置部分。默认情况下是禁用的。当激活时，会得到默认的灰色雾。然而，这只适用于使用前向渲染路径渲染的对象。当延迟模式激活时，这一点在雾部分中会有提及。

![img](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/default-fog.png)

我们稍后再处理延迟模式。现在，让我们专注于前向雾。为此，我们需要使用前向渲染模式。您可以更改全局渲染模式，或强制主摄像机使用所需的渲染模式。因此，将摄像机的渲染路径设置为 Forward。让我们也暂时禁用 HDR 渲染。

![img](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/forward-camera.png)

创建一个小型测试场景，比如在平面或立方体上放置几个球体。使用Unity的默认白色材质。

![img](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/unnoticeable-fog.png)

使用环境光照的默认强度为1，您将得到一些非常明亮的物体，几乎没有明显的雾。

### Linear Fog

为了使雾更加明显，将其颜色设置为纯黑色。这代表了一种吸收光线而几乎不发生散射的大气，就像浓密的黑烟一样。

将雾模式设置为线性。这并不真实，但易于配置。您可以设置雾影响开始的距离以及它有效变得坚实的距离。在两者之间，雾逐渐增加。这是以视距来衡量的。在雾开始之前，可见度正常。超过那个距离，雾将逐渐遮挡物体。超过末端后，除了雾的颜色外，什么都不可见。

![game](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/linear-inspector.png)

![inspector](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/linear-game.png)


线性雾因子由以下函数计算： 𝑓=𝐸−𝑐𝐸−𝑆*f*=*E*−*S**E*−*c*

![image-20240425110107309](.\Fog\image-20240425110107309.png)

其中，𝑐*c* 是雾坐标，𝑆*S* 和 𝐸*E* 是开始和结束。然后将此因子夹紧到 0-1 范围，并用于在雾和物体的着色颜色之间插值。

雾效果调整前向渲染对象的片段颜色。因此，它只影响那些对象，而不影响天空盒。

### Exponential Fog

Unity支持的第二种雾模式是指数雾，这是对雾的更真实的近似。它使用以下函数：![image-20240425110208533](D:\games\ScripteRenderPipeline\doc\Fog\image-20240425110208533.png)

其中，𝑑*d* 是雾的密度因子。与线性版本不同，这个方程永远不会达到零。将密度增加到0.1，使得雾看起来更靠近摄像机。

![game](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/exp-inspector.png)

![inspector](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/exp-game.png)

### Exponential Squared Fog

最后一个模式是指数平方雾。它的工作原理类似于指数雾，但使用以下函数： ![image-20240425110234258](D:\games\ScripteRenderPipeline\doc\Fog\image-20240425110234258.png)

这导致了在近距离处雾量较少，但增长更快。

![game](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/exp2-inspector.png)

![inspector](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/exp2-game.png)

### Adding Fog

现在我们知道了雾是什么样子，让我们将其添加到我们自己的前向着色器中。为了更容易进行比较，将一半的对象使用我们自己的材质，而其余的对象继续使用默认材质。

![img](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/white-linear.png)

雾模式由着色器关键字控制，因此我们必须添加一个多编译指令来支持它们。我们可以使用预定义的 multi_compile_fog 指令来实现这个目的。它会为 FOG_LINEAR、FOG_EXP 和 FOG_EXP2 关键字生成额外的着色器变体。只将这个指令添加到两个前向通道中。

```
#pragma multi_compile_fog
```

接下来，让我们在我的 Lighting 中添加一个函数，将雾应用到我们的片段颜色中。它接受当前颜色和插值器作为参数，并应返回应用了雾的最终颜色。

```
float4 ApplyFog (float4 color, Interpolators i) {
	return color;
}
```

雾效果基于视距，即相机位置和片段世界位置之间的向量长度。我们可以访问这两个位置，因此我们可以计算这个距离。

```
float4 ApplyFog (float4 color, Interpolators i) {
	float viewDistance = length(_WorldSpaceCameraPos - i.worldPos);
	return color;
}
```

然后，我们将这个距离作为雾密度函数的雾坐标，该函数由 UNITY_CALC_FOG_FACTOR_RAW 宏计算。这个宏创建了 unityFogFactor 变量，我们可以用它来在雾和片段颜色之间插值。雾颜色存储在 unity_FogColor 中，它在 ShaderVariables 中定义。

```
float4 ApplyFog (float4 color, Interpolators i) {
	float viewDistance = length(_WorldSpaceCameraPos - i.worldPos);
	UNITY_CALC_FOG_FACTOR_RAW(viewDistance);
	return lerp(unity_FogColor, color, unityFogFactor);
}
```

### How does `**UNITY_CALC_FOG_FACTOR_RAW**` work?

The macro is defined in *UnityCG*. Which fog keyword is defined determines what gets computed.

```
#if defined(FOG_LINEAR)
	// factor = (end-z)/(end-start) = z * (-1/(end-start))+(end/(end-start))
	#define UNITY_CALC_FOG_FACTOR_RAW(coord) float unityFogFactor = \
		(coord) * unity_FogParams.z + unity_FogParams.w
#elif defined(FOG_EXP)
	// factor = exp(-density*z)
	#define UNITY_CALC_FOG_FACTOR_RAW(coord) float unityFogFactor = \
		unity_FogParams.y * (coord); \
		unityFogFactor = exp2(-unityFogFactor)
#elif defined(FOG_EXP2)
	// factor = exp(-(density*z)^2)
	#define UNITY_CALC_FOG_FACTOR_RAW(coord) float unityFogFactor = \
		unity_FogParams.x * (coord); \
		unityFogFactor = exp2(-unityFogFactor*unityFogFactor)
#else
	#define UNITY_CALC_FOG_FACTOR_RAW(coord) float unityFogFactor = 0.0
#endif
```

There is also a `**UNITY_CALC_FOG_FACTOR**` macro, which uses this macro. It assumes that the fog coordinate is of a specific type which requires a conversion, which is why we use the raw version directly.

The `unity_FogParams` variable is defined in *UnityShaderVariables* and contains some useful pre-computed values.

```
	// x = density / sqrt(ln(2)), useful for Exp2 mode
	// y = density / ln(2), useful for Exp mode
	// z = -1/(end-start), useful for Linear mode
	// w = end/(end-start), useful for Linear mode
	float4 unity_FogParams;
```

由于雾因子可能超出 0-1 范围，我们必须在插值之前对其进行夹紧。

```
	return lerp(unity_FogColor, color, saturate(unityFogFactor));
```

另外，由于雾不影响 alpha 分量，我们可以在插值时将其排除在外。

```
	color.rgb = lerp(unity_FogColor.rgb, color.rgb, saturate(unityFogFactor));
	return color;
```

现在我们可以在 MyFragmentProgram 中将雾应用到最终的前向通道颜色中。

```
	#if defined(DEFERRED_PASS)
		#if !defined(UNITY_HDR_ON)
			color.rgb = exp2(-color.rgb);
		#endif
		output.gBuffer0.rgb = albedo;
		output.gBuffer0.a = GetOcclusion(i);
		output.gBuffer1.rgb = specularTint;
		output.gBuffer1.a = GetSmoothness(i);
		output.gBuffer2 = float4(i.normal * 0.5 + 0.5, 1);
		output.gBuffer3 = color;
	#else
		output.color = ApplyFog(color, i);
	#endif
```

![img](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/linear-fog-different.png)

我们自己的着色器现在也包含雾。然而，它与标准着色器计算的雾不太匹配。为了使差异非常明显，请使用线性雾，并将起点和终点设为相同或几乎相同的值。这会导致从无到完全雾的突然过渡。

![img](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/distance-vs-depth.png)

### Depth-Based Fog

我们和标准着色器之间的差异是由于我们计算雾坐标的方式不同。虽然使用世界空间的视距是有道理的，但标准着色器使用了不同的度量方式。具体来说，它使用剪裁空间深度值。因此，视角不会影响雾坐标。此外，在某些情况下，距离会受到摄像机的近裁剪平面距离的影响，这会使雾远离一点。

![img](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/depth-distance.png)

*Flat depth vs. distance.*

使用深度而不是距离的优点是您无需计算平方根，因此速度更快。此外，虽然不太真实，基于深度的雾可能在某些情况下是可取的，比如侧面滚动的游戏。缺点是，由于忽略了视角，摄像机方向会影响雾。随着摄像机的旋转，雾的密度会改变，而逻辑上不应该这样。

![img](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/rotating-depth.png)

*Rotation changes depth.*

让我们为我们的着色器添加基于深度的雾支持，以匹配Unity的方法。这需要对我们的代码进行一些更改。现在，我们必须将剪裁空间深度值传递给片段程序。因此，当其中一种雾模式激活时，定义一个 FOG_DEPTH 关键字。

```
#include "UnityPBSLighting.cginc"
#include "AutoLight.cginc"

#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
	#define FOG_DEPTH 1
#endif
```

我们必须为深度值包括一个插值器。但是，我们可以将其附加到世界位置上，作为其第四个分量，而不是给它一个单独的插值器。

```
struct Interpolators {
	…
	
	#if FOG_DEPTH
		float4 worldPos : TEXCOORD4;
	#else
		float3 worldPos : TEXCOORD4;
	#endif
	
	…
}
```

为了确保我们的代码保持正确，将所有对 i.worldPos 的使用替换为 i.worldPos.xyz。之后，在片段程序中需要时，将剪裁空间深度值赋值给 i.worldPos.w。它只是齐次剪裁空间位置的 Z 坐标，在被转换为 0-1 范围内的值之前。

```
Interpolators MyVertexProgram (VertexData v) {
	Interpolators i;
	i.pos = UnityObjectToClipPos(v.vertex);
	i.worldPos.xyz = mul(unity_ObjectToWorld, v.vertex);
	#if FOG_DEPTH
		i.worldPos.w = i.pos.z;
	#endif
	i.normal = UnityObjectToWorldNormal(v.normal);

	…
}
```

在 ApplyFog 中，使用插值的深度值覆盖计算得到的视距值。保留旧的计算，因为我们稍后仍会使用它。

```
float4 ApplyFog (float4 color, Interpolators i) {
	float viewDistance = length(_WorldSpaceCameraPos - i.worldPos.xyz);
	#if FOG_DEPTH
		viewDistance = i.worldPos.w;
	#endif
	UNITY_CALC_FOG_FACTOR_RAW(viewDistance);
	return lerp(unity_FogColor, color, saturate(unityFogFactor));
}
```

![img](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/fog-depth.png)

现在您很可能得到与标准着色器相同的结果。然而，在某些情况下，剪裁空间配置不同，导致雾效果不正确。为了补偿这一点，使用 UNITY_Z_0_FAR_FROM_CLIPSPACE 宏来转换深度值。

```
viewDistance = UNITY_Z_0_FAR_FROM_CLIPSPACE(i.worldPos.w);
```

### What does `**UNITY_Z_0_FAR_FROM_CLIPSPACE**` do?

Most importantly, it compensates for a possibly reversed clip-space Z dimension.

```
#if defined(UNITY_REVERSED_Z)
	//D3d with reversed Z =>
	//z clip range is [near, 0] -> remapping to [0, far]
	//max is required to protect ourselves from near plane not being
	//correct/meaningfull in case of oblique matrices.
	#define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) \
		max(((1.0-(coord)/_ProjectionParams.y)*_ProjectionParams.z),0)
#elif UNITY_UV_STARTS_AT_TOP
	//D3d without reversed z => z clip range is [0, far] -> nothing to do
	#define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) (coord)
#else 
	//Opengl => z clip range is [-near, far] -> should remap in theory
	//but dont do it in practice to save some perf (range is close enought)
	#define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) (coord)
#endif
```

Note that the macro code mentions that a conversion is needed for OpenGL as well, but considers it not worth the effort.

The `**UNITY_CALC_FOG_FACTOR**` macro simply feeds the above to its raw equivalent.

```
#define UNITY_CALC_FOG_FACTOR(coord) \
	UNITY_CALC_FOG_FACTOR_RAW(UNITY_Z_0_FAR_FROM_CLIPSPACE(coord))
```

### Depth or Distance

因此，我们应该使用哪种度量来进行雾效？剪裁空间深度，还是世界空间距离？让我们同时支持两种！但是不值得将其作为一个着色器功能。我们将其作为一个着色器配置选项，就像 BINORMAL_PER_FRAGMENT 一样。我们假设基于深度的雾是默认的，您可以通过在着色器顶部附近的 CGINCLUDE 部分定义 FOG_DISTANCE 来切换到基于距离的雾。

```
	CGINCLUDE

	#define BINORMAL_PER_FRAGMENT
	#define FOG_DISTANCE

	ENDCG
```

如果已经定义了 FOG_DISTANCE，则在 My Lighting 中切换到基于距离的雾，我们只需要摆脱 FOG_DEPTH 定义即可。

```
#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
	#if !defined(FOG_DISTANCE)
		#define FOG_DEPTH 1
	#endif
#endif
```

### Disabling Fog

当实际上打开雾时，只包含雾代码，因为我们并不总是想要使用雾。

```
#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
	#if !defined(FOG_DISTANCE)
		#define FOG_DEPTH 1
	#endif
	#define FOG_ON 1
#endif

…

float4 ApplyFog (float4 color, Interpolators i) {
	#if FOG_ON
		float viewDistance = length(_WorldSpaceCameraPos - i.worldPos.xyz);
		#if FOG_DEPTH
			viewDistance = UNITY_Z_0_FAR_FROM_CLIPSPACE(i.worldPos.w);
		#endif
		UNITY_CALC_FOG_FACTOR_RAW(viewDistance);
		color.rgb = lerp(unity_FogColor.rgb, color.rgb, saturate(unityFogFactor));
	#endif
	return color;
}
```

### Multiple Lights

我们的雾在场景中有多个光源时会如何表现呢？当我们使用黑色雾时，它看起来很好，但也尝试使用其他颜色。

![one](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/gray-fog.png)

![two](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/two-lights-incorrect.png)

结果太亮了。这是因为我们每个光源都添加了一次雾颜色。当雾颜色是黑色时，这并不是问题。因此，解决方案是始终在加法通道中使用黑色。这样，雾会淡化额外光源的贡献，而不会使雾本身变亮。

```
		float3 fogColor = 0;
		#if defined(FORWARD_BASE_PASS)
			fogColor = unity_FogColor.rgb;
		#endif
		color.rgb = lerp(fogColor, color.rgb, saturate(unityFogFactor));
```

![img](https://catlikecoding.com/unity/tutorials/rendering/part-14/forward-fog/two-lights-correct.png)

------

- **Fog Color**：雾的颜色 

- **Fog Attenuation Distance**：控制雾的全局密度。

- **BaseHeight**：雾的高度

- **MaximumHeight**：控制随高度变化的密度衰减；允许在地面附近具有较高的密度，而在较高的位置具有较低的密度。

- **Fog Start Distance**: 雾的起始距离

- **Fog Max Distance**：雾的最大距离

  