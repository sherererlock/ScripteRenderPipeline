## Cascaded Shadow Maps


当定向光照影响到最大阴影距离范围内的所有物体时，它们的阴影贴图最终会覆盖一个大区域。由于阴影贴图使用正交投影，每个阴影贴图中的像素都有固定的世界空间尺寸。如果这个尺寸过大，单独的阴影像素会变得明显可见，导致阴影边缘呈锯齿状，小的阴影可能会消失。增加纹理集尺寸可以减轻这个问题，但只能在一定程度上解决。

使用透视相机时，远处的物体会显得较小。在某个可视距离上，一个阴影贴图像素会映射到一个单独的显示像素，这意味着阴影分辨率在理论上是最优的。靠近相机时，我们需要更高的阴影分辨率，而远离相机时，较低的分辨率就足够了。这暗示着理想情况下，我们应该根据阴影接收者的视距使用可变的阴影贴图分辨率。

级联阴影贴图是解决这个问题的一种方法。其核心思想是多次渲染阴影投射者，使得每个光源在纹理集中获得多个瓦片，即级联。第一个级联只覆盖靠近相机的小区域，随后的级联逐渐缩小，以相同数量的像素覆盖越来越大的区域。着色器会为每个片段采样最合适的级联。

### Settings


Unity的阴影代码支持每个定向光最多四个级联。目前，我们只使用了一个单一的级联，覆盖了一切直到最大阴影距离。为了支持更多级联，我们将在定向阴影设置中添加一个级联数量滑块。虽然我们可以在每个定向光中使用不同的级联数量，但在所有投射阴影的定向光中使用相同数量最合理。

每个级联覆盖了一部分投射阴影的区域，直到最大阴影距离。我们将通过为前三个级联添加比例滑块来使确切的部分可配置。最后一个级联始终覆盖整个范围，因此不需要滑块。默认情况下将级联数量设置为四，级联比例为0.1、0.25和0.5。这些比例应该每级联递增，但我们不会在用户界面中强制执行这一点。

### Culling Spheres

In Unity,每个级联（cascade）所覆盖的区域是通过创建一个剔除球来确定的。由于阴影投影是正交且是方形的，它们最终会紧密地适应其剔除球，但也会覆盖一些周围的空间。这就是为什么一些阴影可以在剔除区域之外看到。此外，光的方向对剔除球没有影响，因此所有方向光最终都使用相同的剔除球。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/directional-shadows/cascaded-shadow-maps/culling-spheres.png)

## Shadow Quality

既然我们拥有了功能完善的级联阴影贴图，让我们专注于提高阴影的质量。我们一直观察到的伪影被称为阴影痤疮，它是由于那些与光线方向不完全对齐的表面错误的自阴影造成的。随着表面越来越接近与光线方向平行，痤疮问题会变得更严重。

增加纹理集大小会减小纹素的世界空间大小，从而使痤疮伪影变小。然而，伪影的数量也会增加，所以问题不能简单地通过增加纹理集大小来解决。

### Depth Bias

有各种方法可以减轻阴影痤疮的问题。其中最简单的方法是在投射阴影的物体的深度上添加一个恒定的偏移，将它们从光线远离，以便不再发生错误的自阴影。最快的方法是在渲染时应用全局深度偏移，在DrawShadows之前在缓冲区上调用SetGlobalDepthBias，并在之后将其设置为零。这是一个在裁剪空间中应用的深度偏移，是一个非常小的值的倍数，具体取决于用于阴影贴图的确切格式。我们可以通过使用一个大值（如50000）来了解它的工作原理。还有第二个参数用于斜率缩放偏移，但暂时将其保持为零。

```
			buffer.SetGlobalDepthBias(50000f, 0f);
			ExecuteBuffer();
			context.DrawShadows(ref shadowSettings);
			buffer.SetGlobalDepthBias(0f, 0f);
```

恒定的偏移确实简单，但只能够消除大部分正面照明的表面的伪影。要消除所有的痤疮，需要一个更大的偏移，比原来大一个数量级。

```
		buffer.SetGlobalDepthBias(500000f, 0f);
```

然而，由于深度偏移将投射阴影的物体远离光线，采样到的阴影也会朝着相同的方向移动。足够大以消除大部分痤疮的偏移会将阴影移动得太远，以至于它们看起来与其投射物分离，导致被称为“彼得潘现象”的视觉伪影。

另一种方法是应用斜率缩放偏移，这可以通过在SetGlobalDepthBias的第二个参数中使用非零值来实现。该值用于缩放X和Y维度上绝对裁剪空间深度导数的最大值。因此，对于正面照明的表面，这个值为零；当光线以至少在两个维度中的一个维度上以45°角射击时，它为1；当表面法线与光线方向的点积达到零时，它趋近于无穷大。因此，当需要更多偏移时，偏移会自动增加，但没有上限。结果是，为了消除痤疮，需要一个更低的因子，例如3，而不是500000。

### Cascade Data

因为痤疮的大小取决于世界空间纹素大小，一个适用于所有情况的一致性方法必须考虑到这一点。由于每个级联的纹素大小都不同，这意味着我们需要将一些更多的级联数据发送到GPU。为此，在Shadows中添加一个通用的级联数据向量数组。

```
	static int
		…
		cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
		cascadeDataId = Shader.PropertyToID("_CascadeData"),
		shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

	static Vector4[]
		cascadeCullingSpheres = new Vector4[maxCascades],
		cascadeData = new Vector4[maxCascades];
```

将它与其他所有内容一起发送到GPU。

```
		buffer.SetGlobalVectorArray(
			cascadeCullingSpheresId, cascadeCullingSpheres
		);
		buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
```

我们已经可以做的一件事是将级联半径的平方的倒数放入这些向量的X分量中。这样，我们就不必在着色器中执行这个除法操作。在一个新的SetCascadeData方法中完成这个步骤，同时存储剔除球，并在RenderDirectionalShadows中调用它。将级联索引、剔除球和瓦片大小作为浮点数传递给它。

```
	void RenderDirectionalShadows (int index, int split, int tileSize) {
		…
		
		for (int i = 0; i < cascadeCount; i++) {
			…
			if (index == 0) {
				SetCascadeData(i, splitData.cullingSphere, tileSize);
			}
			…
		}
	}

	void SetCascadeData (int index, Vector4 cullingSphere, float tileSize) {
		cascadeData[index].x = 1f / cullingSphere.w;
		cullingSphere.w *= cullingSphere.w;
		cascadeCullingSpheres[index] = cullingSphere;
	}
```

```
CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT];
	…
CBUFFER_END
```

不正确的自阴影发生是因为一个投射阴影的深度纹素覆盖了多个片段，这导致投射物的体积从其表面伸出。因此，如果我们缩小投射物，这种情况就不会再发生。然而，缩小投射物会使阴影变得比它们应该的更小，并且可能会引入不应该存在的空洞。

我们也可以做相反的操作：在采样阴影时膨胀表面。然后，我们在表面上采样时稍微远离表面，刚好足够避免错误的自阴影。这将稍微调整阴影的位置，可能会导致边缘处的不对齐，并添加虚假的阴影，但这些伪影通常远不如彼得潘现象明显。

我们可以通过沿着法线矢量稍微移动表面位置来采样阴影。如果我们只考虑一个维度，那么与世界空间纹素大小相等的偏移量应该足够了。我们可以在SetCascadeData中通过将剔除球的直径除以瓦片大小来找到纹素大小。将其存储在级联数据向量的Y分量中。

```
		float texelSize = 2f * cullingSphere.w / tileSize;
		cullingSphere.w *= cullingSphere.w;
		cascadeCullingSpheres[index] = cullingSphere;
		//cascadeData[index].x = 1f / cullingSphere.w;
		cascadeData[index] = new Vector4(
			1f / cullingSphere.w,
			texelSize
		);
```

然而，这并不总是足够，因为纹素是正方形的。在最坏的情况下，我们可能需要沿着正方形的对角线进行偏移，所以让我们乘以√2来进行缩放。

在着色器方面，对GetDirectionalShadowAttenuation添加一个全局阴影数据的参数。将表面法线与偏移相乘，以找到法线偏移，并在计算在阴影瓦片空间中的位置之前将其添加到世界位置中。

```
float GetDirectionalShadowAttenuation (
	DirectionalShadowData directional, ShadowData global, Surface surfaceWS
) {
	if (directional.strength <= 0.0) {
		return 1.0;
	}
	float3 normalBias = surfaceWS.normal * _CascadeData[global.cascadeIndex].y;
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[directional.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	).xyz;
	float shadow = SampleDirectionalShadowAtlas(positionSTS);
	return lerp(1.0, shadow, directional.strength);
}
```

### Configurable Biases

法线偏移可以消除阴影痤疮，而不会引入明显的新伪影，但它无法消除所有的阴影问题。例如，墙下的地板上可见到不应该存在的阴影线。这不是自阴影，而是从墙壁中伸出影响其下方地板的阴影。稍微添加一些斜率缩放偏移可以处理这些情况，但是没有一个完美的值可供使用。因此，我们将根据每个光源进行配置，使用其现有的偏移滑块。在Shadows的ShadowedDirectionalLight结构中为其添加一个字段。

### Shadow Pancaking

另一个可能引发伪影的潜在问题是Unity应用了阴影平铺技术。其思想是，在渲染方向光的投射阴影时，近裁剪面会尽量向前移动。这可以增加深度精度，但这意味着那些不在摄像机视图内的投射阴影物体可能会出现在近裁剪面的前面，导致它们在不应该被裁剪的情况下被剪切掉。

这个问题可以通过在ShadowCasterPassVertex中将顶点位置限制在近裁剪面上来解决，从而有效地将那些位于近裁剪面前方的投射阴影物体扁平化，将它们变成粘附在近裁剪面上的薄饼状。我们可以通过取裁剪空间Z和W坐标的最大值来实现这一点，或者在定义了UNITY_REVERSED_Z时取它们的最小值。为了使用正确的W坐标符号，将其与UNITY_NEAR_CLIP_VALUE相乘。

这对于完全位于近裁剪面两侧的投射阴影物体效果非常好，但是横跨裁剪面的投射阴影物体会变形，因为只有其中的一些顶点受到影响。这对于小三角形来说不太明显，但是大三角形可能会发生很大的变形，弯曲它们并经常导致它们陷入表面中。

这个问题可以通过稍微拉回近裁剪面来缓解。这就是灯光的近裁剪面滑块的作用。在ShadowedDirectionalLight中为近裁剪面偏移添加一个字段。

### Blending Cascades

更柔和的阴影看起来更好，但它们也会使级联之间的突然过渡更加明显。

我们可以通过在级联之间添加一个过渡区域来使过渡变得不那么明显，虽然不能完全隐藏。

```
struct ShadowData {
	int cascadeIndex;
	float cascadeBlend;
	float strength;
};
```

首先，在Shadows的ShadowData中添加一个级联混合值，我们将使用它来在相邻的级联之间进行插值。

在GetShadowData中最初将混合值设置为1，表示所选的级联具有完全的强度。然后，每当在循环中找到级联时，始终计算淡化因子。如果我们在最后一个级联，像以前一样将其因子与强度相乘，否则将其用于混合。

```
	data.cascadeBlend = 1.0;
	data.strength = FadedShadowStrength(
		surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y
	);
	int i;
	for (i = 0; i < _CascadeCount; i++) {
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
		if (distanceSqr < sphere.w) {
			float fade = FadedShadowStrength(
				distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z
			);
			if (i == _CascadeCount - 1) {
				data.strength *= fade;
			}
			else {
				data.cascadeBlend = fade;
			}
			break;
		}
	}
```

现在在GetDirectionalShadowAttenuation中检查级联混合值是否在检索到第一个阴影值后小于1。如果是这样，我们就在一个过渡区域中，必须同时从下一个级联中采样，并在两个值之间进行插值。

```
	float shadow = FilterDirectionalShadow(positionSTS);
	if (global.cascadeBlend < 1.0) {
		normalBias = surfaceWS.normal *
			(directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(
			_DirectionalShadowMatrices[directional.tileIndex + 1],
			float4(surfaceWS.position + normalBias, 1.0)
		).xyz;
		shadow = lerp(
			FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend
		);
	}
	return lerp(1.0, shadow, directional.strength);
```

## Transparency

在本教程中，我们将考虑透明的投射阴影物体。剪裁、淡化和透明材质都可以像不透明材质一样接收阴影，但目前只有剪裁材质会正确地投射阴影。透明物体的行为就像它们是实心的阴影投射物体一样。