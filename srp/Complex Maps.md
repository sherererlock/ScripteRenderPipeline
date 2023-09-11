# **Complex Maps**

## Circuitry Material

到目前为止，我们一直使用非常简单的材质来测试我们的渲染管线。但它还应该支持复杂的材质，以便我们可以表示更有趣的表面。在本教程中，我们将创建一种类似电路的艺术材质，借助一些纹理来实现。

### Albedo

我们材质的基础是它的反照率贴图。它由几层不同深浅的绿色与顶部的金色组成。每个颜色区域都是均匀的，除了一些棕色污渍，这样可以更容易区分后面我们将添加的细节。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/circuitry-material/circuitry-albedo.png)

使用这个反照率贴图，您可以创建一个新的材质，使用Lit着色器。将其平铺设置为2乘以1，这样正方形纹理可以在球体周围包裹，而不会被拉伸得太多。默认球体的极点始终会有很大的变形，这是无法避免的。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/circuitry-material/albedo-inspector.png)

![scene](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/circuitry-material/albedo-scene.png)

### Emission

我们已经支持发射贴图，因此让我们使用一个发射贴图，在金色电路上方添加一个浅蓝色的发光图案。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/circuitry-material/circuitry-emission.png)

将发射贴图分配给材质，并将发射颜色设置为白色，以使其可见。

![inspector](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/circuitry-material/emission-inspector.png)

![scene dark](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/circuitry-material/emission-scene-dark.png)

## Mask Map

目前，我们无法采取太多措施使我们的材质更有趣。金色电路应该是金属的，而绿色电路板则不是，但我们目前只能配置均匀的金属度和光滑度值。我们需要额外的贴图来支持在表面上变化它们。

![img](https://catlikecoding.com/unity/tutorials/custom-srp/complex-maps/mask-map/metallic-smooth.png)

### MODS


我们可以添加一个独立的金属度贴图和一个光滑度贴图，但两者都只需要一个单一通道，所以我们可以将它们合并到一个单一的贴图中。这个贴图被称为遮罩贴图，它的各个通道用于遮罩不同的着色器属性。我们将使用与Unity的HDRP相同的格式，即MODS贴图，其中MODS代表金属度、遮挡度、细节和光滑度，按照这个顺序存储在RGBA通道中。

这是一个用于我们电路的这种贴图。它在所有通道中都包含数据，但目前我们只会使用其R和A通道。由于这个纹理包含的是遮罩数据而不是颜色，请确保禁用其sRGB（颜色纹理）纹理导入属性。如果不这样做，GPU在采样纹理时会错误地应用伽马到线性的转换。