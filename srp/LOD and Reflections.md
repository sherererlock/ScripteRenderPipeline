# LOD and Reflections

许多小物体可以为场景增添细节，使其更加有趣。然而，那些过于小以至于无法覆盖多个像素的细节会退化成模糊的噪音。在这些视觉尺度下，最好不要渲染它们，这还可以释放 CPU 和 GPU 以渲染更重要的物体。我们还可以决定在它们仍然可以被区分的时候更早地剔除这些物体。这可以进一步提高性能，但会导致物体根据它们的视觉大小突然出现和消失。我们还可以添加中间步骤，在最终完全剔除物体之前逐渐切换到越来越不详细的可视化。Unity通过使用LOD组，可以实现所有这些操作。

### LOD Group Component

您可以通过创建一个空的游戏对象并向其添加一个LODGroup组件来将LOD（级别细节）组添加到场景中。默认组定义了四个级别：LOD 0，LOD 1，LOD 2，最后是剔除，这意味着不会渲染任何内容。这些百分比表示估计的可视大小阈值，相对于显示窗口的尺寸而言。因此，LOD 0 通常用于覆盖窗口大于60%的对象，通常考虑垂直尺寸，因为这是最小的尺寸。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/group-component.png)


然而，*Quality* 项目设置部分包含一个LODBias（LOD Bias）选项，它会调整这些阈值。默认情况下，它设置为2，这意味着它会将此评估的估计可视大小加倍。因此，LOD 0 最终用于大于30%而不是60%的一切。当Bias设置为除1以外的值时，组件的检查器会显示警告。此外，还有一个“最大LOD级别”选项，可以用来限制最高的LOD级别。例如，如果将其设置为1，那么LOD 1也会被用于替代LOD 0。

想法是将可视化LOD级别的所有游戏对象都作为组对象的子对象。例如，您可以使用相同大小的三个彩色球体来表示三个LOD级别。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/lod-group-sphere.png)

每个对象都必须分配到适当的LOD级别。您可以通过在组件中选择一个级别块，然后将对象拖放到其“渲染器列表”上，或者直接将其拖放到一个LOD级别块上来完成此操作。这样可以确保每个对象在适当的LOD级别下进行渲染。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/lod-renderers.png)

Unity会自动渲染适当的对象。但是，如果在编辑器中选择特定对象，它将覆盖这种行为，这样您可以在场景中看到所选对象。如果选择了LOD组本身，编辑器还会指示当前可见的LOD级别是哪个。这有助于您在编辑器中了解当前的LOD级别显示情况。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/rendering-lod-spheres.png)

通过移动摄像机，您可以改变每个组使用的LOD级别。或者，您还可以调整LOD bias以在保持其他所有内容不变的情况下查看可视化效果的变化。这些方法允许您根据观察视角和距离来自动切换不同的LOD级别，以提高性能并确保场景中的对象呈现适当的细节。

### Additive LOD Groups

对象可以添加到多个LOD级别中。您可以使用这一特性，在较高的级别添加较小的细节，同时在多个级别中使用相同的较大对象。例如，您可以使用叠放的扁平立方体制作一个三层金字塔。基础立方体是所有三个级别的一部分。中间的立方体是LOD 0和LOD 1的一部分，而最小的顶部立方体仅属于LOD 0。因此，可以根据可视大小来添加和移除细节，而不是替换整个对象。这允许您更精细地控制渲染细节，以优化性能。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/stacked-cubes-lod.png)

可以对LOD组进行光照贴图吗？ 可以。当您使LOD组对全局光照贡献时，它会包含在光照贴图中。LOD 0如预期一样用于光照贴图。其他LOD级别也会获得烘焙光照，但场景的其余部分只考虑LOD 0。您还可以选择仅对一些级别进行烘焙，让其他级别依赖于光探头（light probes）。这使得您可以更灵活地控制场景的光照效果。

### LOD Transitions

LOD级别的突然切换可能在视觉上显得突兀，特别是如果一个对象由于自身或摄像机的轻微移动而频繁地快速切换。通过将LOD组的Fade Mode设置为Cross Fade，可以使这种过渡变得渐进。这样，旧级别在新级别淡入的同时淡出，从而使过渡更加平滑。这对于减少视觉不连续性非常有用，让对象的LOD级别变化更加流畅。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/cross-fade-mode.png)

关于SpeedTree的渐变模式选项如何？ 该模式专门用于SpeedTree树木，它使用自己的LOD系统来折叠树木并在3D模型和广告牌表示之间进行过渡。我们不会使用它。

您可以在每个LOD级别中控制切换到下一个级别的交叉淡入何时开始。当启用交叉淡入时，此选项将变为可见。Fade Transition Width为零表示此级别与下一个较低级别之间没有淡入淡出，而值为1表示它立即开始淡入淡出。在默认设置下，当设置为0.5时，LOD 0会在80%的时候开始与LOD 1进行交叉淡入淡出。这个参数允许您精确地控制LOD级别之间的淡入淡出过渡，以满足特定的视觉需求。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/fade-transition-width.png)

在交叉淡入激活时，两个LOD级别会同时进行渲染。如何混合它们取决于着色器。Unity会为LOD_FADE_CROSSFADE关键字选择一个着色器变体，因此为我们的Lit着色器添加一个多编译指令来支持它。在CustomLit和ShadowCaster通道都需要添加这个指令。这将确保着色器能够正确地处理LOD级别之间的交叉淡入淡出效果。

```
			#pragma multi_compile _ LOD_FADE_CROSSFADE
```

如果使用了淡入淡出效果，对象的淡入程度通过UnityPerDraw缓冲区的unity_LODFade向量进行传递，我们已经定义了这个向量。它的X分量包含淡入因子。它的Y分量包含相同的因子，但以十六个步骤进行量化，我们不会使用它。让我们可视化淡入因子，如果正在使用它，可以在LitPassFragment的开头返回它。

```
float4 LitPassFragment (Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
	#if defined(LOD_FADE_CROSSFADE)
		return unity_LODFade.x;
	#endif
	
	…
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/fade-factor.png)

正在淡出的对象从1开始逐渐减小到零，这是预期的行为。但我们还看到了表示较高LOD级别的实心黑色对象。这是因为正在淡入的对象其淡入因子被取反。我们可以通过返回取反的淡入因子来观察到这一点。

```
		return -unity_LODFade.x;
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/fade-factor-negated.png)

请注意，同时存在于两个LOD级别的对象不会与自身进行交叉淡入淡出。这是因为它们已经是最高LOD级别的一部分，不需要淡入淡出效果。

### Dithering

要混合两个LOD级别，我们可以使用剪裁（clipping），采用类似于近似半透明阴影的方法。因为我们需要同时处理表面和它们的阴影，让我们在Common中添加一个名为ClipLOD的函数来实现这个功能。给它传递剪裁空间（clip-space）的XY坐标以及淡入淡出因子作为参数。然后，如果启用了交叉淡入淡出，可以根据淡入淡出因子减去一个抖动模式来进行剪裁。这样可以实现不同LOD级别的混合效果。

```
void ClipLOD (float2 positionCS, float fade) {
	#if defined(LOD_FADE_CROSSFADE)
		float dither = 0;
		clip(fade - dither);
	#endif
}
```

为了检查剪裁是否按预期工作，我们可以从一个垂直渐变开始，每32个像素重复一次。这应该会创建交替的水平条纹。通过观察这些效果，您可以验证剪裁是否按照所需的方式工作。

```
		float dither = (positionCS.y % 32) / 32;
```

在LitPassFragment中调用ClipLOD函数，而不是直接返回淡入淡出因子。这将让我们使用剪裁来处理不同LOD级别之间的混合效果，以取代直接的渲染结果。

```
	//#if defined(LOD_FADE_CROSSFADE)
	//	return unity_LODFade.x;
	//#endif
	ClipLOD(input.positionCS.xy, unity_LODFade.x);
```

同时在ShadowCasterPassFragment的开头调用ClipLOD函数，以实现阴影的交叉淡入淡出效果。这将确保在阴影通道中也使用剪裁来处理不同LOD级别之间的混合

```
void ShadowCasterPassFragment (Varyings input) {
	UNITY_SETUP_INSTANCE_ID(input);
	ClipLOD(input.positionCS.xy, unity_LODFade.x);

	…
}
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/striped-lod-half.png)

我们得到了条纹渲染，但在交叉淡入淡出时只有两个LOD级别中的一个显示出来。这是因为其中一个具有负的淡入淡出因子。为解决这个问题，当出现这种情况时，我们应该将抖动模式加上去，而不是减去它。这将确保在淡入淡出期间正确混合两个LOD级别。

```
	clip(fade + (fade < 0.0 ? dither : -dither));
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/striped-lod-complete.png)

既然它正常工作了，我们可以切换到一个适当的抖动模式。让我们选择与我们用于半透明阴影相同的抖动模式。这将提高渲染的质量和准确性。

```
		float dither = InterleavedGradientNoise(positionCS.xy, 0);
```

![img](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/dithered-lod.png)

### Animated Cross-Fading

尽管抖动可以创建相对平滑的过渡，但抖动模式仍然会显而易见。与半透明阴影一样，淡出的效果可能会不稳定和令人分心。理想情况下，交叉淡入淡出只是一个临时的效果，即使在这种情况下也不会有其他任何变化。我们可以通过启用LOD组的“Animate Cross-fading”选项来实现这一点。这将忽略淡入淡出过渡宽度，而是一旦组通过了LOD阈值，就会快速进行交叉淡入淡出。这样可以确保过渡是临时的，不会引起不稳定的效果。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/animated-cross-fading.png)

默认的动画持续时间是半秒，可以通过设置静态属性`LODGroup.crossFadeAnimationDuration`来更改所有组的动画持续时间。然而，在不处于播放模式时，Unity 2022中的过渡速度会更快。这是因为在编辑模式下，Unity通常会采用更快的速度以提高开发效率，而在播放模式下才会采用实际的持续时间来模拟淡入淡出效果。这一行为确保了在编辑时可以更快地预览效果，而在播放时才会以实际的速度呈现。

## Reflections