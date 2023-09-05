## Directional Lights

### Lighting

#### Lit Shader

1. 拷贝`UnlitPass ` HLSL文件为`LitPass`，修改相应的方法

2. 复制shader文件，重命名为Lit.Shader,修改名字和方法，将默认颜色改为灰色。

3. 修改Pass的Tag

   ```c#
   		Pass {
   			Tags {
   				"LightMode" = "CustomLit"
   			}
   
   			…
   		}
   ```

4. 新建新的shaderTagId，传入到drawSetting中，以便我们的管线支持CustomLit

   ```c#
   static ShaderTagId
       unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
   litShaderTagId = new ShaderTagId("CustomLit");
   
   var drawingSettings = new DrawingSettings(
       unlitShaderTagId, sortingSettings
   ) {
       enableDynamicBatching = useDynamicBatching,
       enableInstancing = useGPUInstancing
   };
   drawingSettings.SetShaderPassName(1, litShaderTagId);  
   ```

#### Normal Vectors

1. `Attributes `中添加normal数据，语义是`NORMAL`

2. `Varyings `中添加normal数据

3. 由于法线定义在模型空间下，需要转换到世界空间下，我们在vertex shader中作转换

   `TransformObjectToWorldNormal`：已经为我们处理了不均匀缩放时法线的转换

#### Interpolated Normals

法线定义在顶点上，管线在插值得到每个片元的法线时并没有归一化，所以长度不一，需要在fragment shader中`normalize()`

#### Surface Properties

1. 新建`Surface.hlsl`，添加Surface结构体

   ```c
   struct Surface {
   	float3 normal;
   	float3 color;
   	float alpha;
   };
   
   ```

2. 添加include到LitPass.hlsl中

3. 在shader中定义结构体对象

   ```c
   	Surface surface;
   	surface.normal = normalize(input.normalWS);
   	surface.color = base.rgb;
   	surface.alpha = base.a;
   
   	return float4(surface.color, surface.alpha);
   ```

#### Calculating Lighting

1. 新建` Lighting.hlsl`，添加`GetLighting`方法

   ```c
   #ifndef CUSTOM_LIGHTING_INCLUDED
   #define CUSTOM_LIGHTING_INCLUDED
   
   float3 GetLighting (Surface surface) {
   	return surface.normal.y; // y值与法线和向上矢量之间角度的余弦相匹配。
   }
   
   #endif
   ```

2. include到Litpass.hlsl中

   ```c
   #include "../ShaderLibrary/Surface.hlsl"
   #include "../ShaderLibrary/Lighting.hlsl"
   
   float3 color = GetLighting(surface);
   return float4(color, surface.alpha);
   ```

3. 将albedo应用到光照上

   ```c
   float3 GetLighting (Surface surface) {
   	return surface.normal.y * surface.color;
   }
   ```

### Lights

#### Light Structure

1. 添加`Light.hlsl`文件

   ```c
   #ifndef CUSTOM_LIGHT_INCLUDED
   #define CUSTOM_LIGHT_INCLUDED
   
   struct Light {
   	float3 color;
   	float3 direction;
   };
   
   Light GetDirectionalLight () {
   	Light light;
   	light.color = 1.0;
   	light.direction = float3(0.0, 1.0, 0.0);
   	return light;
   }
   
   #endif
   ```

   ```c
   #include "../ShaderLibrary/Light.hlsl"
   #include "../ShaderLibrary/Lighting.hlsl"
   ```

#### Lighting Functions

```c
// 
float3 IncomingLight (Surface surface, Light light) {
	return saturate(dot(surface.normal, light.direction)) * light.color;
}

//
float3 GetLighting (Surface surface, Light light) {
	return IncomingLight(surface, light) * surface.color;
}

float3 GetLighting (Surface surface) {
	return GetLighting(surface, GetDirectionalLight());
}
```

### Sending Light Data to the GPU

1. shader中定义uniform buffer包含light的属性

   ```c
   CBUFFER_START(_CustomLight)
   	float3 _DirectionalLightColor;
   	float3 _DirectionalLightDirection;
   CBUFFER_END
       
   Light GetDirectionalLight () {
   	Light light;
   	light.color = _DirectionalLightColor;
   	light.direction = _DirectionalLightDirection;
   	return light;
   }    
   ```

2. c#端新建`Lighting.cs`文件

   ```c#
   using Unity.Collections;
   using UnityEngine;
   using UnityEngine.Rendering;
   
   public class Lighting {
   
   	const string bufferName = "Lighting";
   
   	CommandBuffer buffer = new CommandBuffer {
   		name = bufferName
   	};
       static int
           dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
           dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
       
   	public void Setup (ScriptableRenderContext context) {
   		buffer.BeginSample(bufferName);
   		SetupDirectionalLight();
   		buffer.EndSample(bufferName);
   		context.ExecuteCommandBuffer(buffer);
   		buffer.Clear();
   	}
   	
   	void SetupDirectionalLight () {
   		Light light = RenderSettings.sun;
   		buffer.SetGlobalVector(dirLightColorId, light.color.linear * light.intensity);
   		buffer.SetGlobalVector(dirLightDirectionId, -light.transform.forward);
   	}
   }
   ```

3. CameraRender中添加Lighting的实例

   ```c#
   	Lighting lighting = new Lighting();
   
   	public void Render (
   		ScriptableRenderContext context, Camera camera,
   		bool useDynamicBatching, bool useGPUInstancing
   	) {
   		…
   
   		Setup();
   		lighting.Setup(context,cullingResults);
   		DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
   		DrawUnsupportedShaders();
   		DrawGizmos();
   		Submit();
   	}
   ```

### Visible Lights

在`Lighting.cs`中访问`CullingResult`以获得`VisibleLight`

```c#
	CullingResults cullingResults;

	public void Setup (
		ScriptableRenderContext context, CullingResults cullingResults
	) {
		this.cullingResults = cullingResults;
		buffer.BeginSample(bufferName);
		//SetupDirectionalLight();
		SetupLights();
		…
	}
	
	void SetupLights () {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
    }
```

### Multiple Directional Lights

1. 定义支持最大4个方向光的Color数组和Direction数组

2. 在`SetupDirectionalLight`中给数组填充从`visibleLights`获取的数据

   方向是`-visibleLight.localToWorldMatrix.GetColumn(2)` why?

3. Color中已经包含了Intensity的作用，只不过不是Linear的，所以在`pipeline`构造时设置`GraphicsSettings.lightsUseLinearIntensity = true;`

4. 遍历`visibleLights`,调用 `SetupDirectionalLight`,填充数组

   - 只处理方向光
   - 最大支持光限制
   - 用引用传递参数

5. 将数据发送给GPU

```c#
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting {

	const string bufferName = "Lighting";

	CommandBuffer buffer = new CommandBuffer {
		name = bufferName
	};
    static int
        //dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
        //dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
    
    	dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
		dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
		dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
    
	static Vector4[]
		dirLightColors = new Vector4[maxDirLightCount],
		dirLightDirections = new Vector4[maxDirLightCount];
    
	public void Setup (ScriptableRenderContext context, CullingResults cullingResults) {
		buffer.BeginSample(bufferName);
		SetupLights();
		buffer.EndSample(bufferName);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
	
    void SetupLights () 
    {
 		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
		int dirLightCount = 0;
		for (int i = 0; i < visibleLights.Length; i++) {
			VisibleLight visibleLight = visibleLights[i];
			if (visibleLight.lightType == LightType.Directional) {
				SetupDirectionalLight(dirLightCount++, ref visibleLight);
				if (dirLightCount >= maxDirLightCount) {
					break;
				}
			}
		}

		buffer.SetGlobalInt(dirLightCountId, visibleLights.Length);
		buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
		buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);       
    }
    
	void SetupDirectionalLight (int index, ref VisibleLight visibleLight) {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
	}
}
```

```c#
public CustomRenderPipeline (
    bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher
) {
    this.useDynamicBatching = useDynamicBatching;
    this.useGPUInstancing = useGPUInstancing;
    GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
    GraphicsSettings.lightsUseLinearIntensity = true;
}
```

### Shader Loop

1. 调整`cbuffer`的定义

   ```c
   #define MAX_DIRECTIONAL_LIGHT_COUNT 4
   
   CBUFFER_START(_CustomLight)
   	//float4 _DirectionalLightColor;
   	//float4 _DirectionalLightDirection;
   	int _DirectionalLightCount;
   	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
   	float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
   CBUFFER_END
   ```

2. 调整`GetDirectionalLight`的定义

   ```c
   int GetDirectionalLightCount () {
   	return _DirectionalLightCount;
   }
   
   Light GetDirectionalLight (int index) {
   	Light light;
   	light.color = _DirectionalLightColors[index].rgb;
   	light.direction = _DirectionalLightDirections[index].xyz;
   	return light;
   }
   ```

3. 调整`GetLighting`的实现

   ```c
   float3 GetLighting (Surface surface) {
   	float3 color = 0.0;
   	for (int i = 0; i < GetDirectionalLightCount(); i++) {
   		color += GetLighting(surface, GetDirectionalLight(i));
   	}
   	return color;
   }
   ```

### Shader Target Level

```c
			HLSLPROGRAM
			#pragma target 3.5
			…
			ENDHLSL
```

## BRDF

### Incoming Light

入射光要考虑其方向与表面法线的夹角

### Outgoing Light

与表面的属性有关，可以分为完美反射，glossy或者完美的漫反射

### Surface Properties

金属工作流

1. 在材质属性上添加`Metallic`和`Smoothness`

   ```c
   		_Metallic ("Metallic", Range(0, 1)) = 0
   		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
   ```

2. shader中`UnityPerMaterial`中加入属性

   ```c
   UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
   	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
   	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
   	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
   	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
   	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
   UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
   ```

3. surface结构体的声明

   ```
   struct Surface {
   	float3 normal;
   	float3 color;
   	float alpha;
   	float metallic;
   	float smoothness;
   };
   ```

4. 传递给结构体对象

   ```
   	Surface surface;
   	surface.normal = normalize(input.normalWS);
   	surface.color = base.rgb;
   	surface.alpha = base.a;
   	surface.metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
   	surface.smoothness =
   		UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
   ```

5. c#中处理`PerObjectMaterialProperties`

   ```c#
   	static int
   		baseColorId = Shader.PropertyToID("_BaseColor"),
   		cutoffId = Shader.PropertyToID("_Cutoff"),
   		metallicId = Shader.PropertyToID("_Metallic"),
   		smoothnessId = Shader.PropertyToID("_Smoothness");
   
   	…
   
   	[SerializeField, Range(0f, 1f)]
   	float alphaCutoff = 0.5f, metallic = 0f, smoothness = 0.5f;
   
   	…
   
   	void OnValidate () {
   		…
   		block.SetFloat(metallicId, metallic);
   		block.SetFloat(smoothnessId, smoothness);
   		GetComponent<Renderer>().SetPropertyBlock(block);
   	}
   ```

### BRDF Properties

添加`brdf.hlsl`,将`BRDF`纳入到计算中

1. brdf.hlsl文件

   ```c
   #ifndef CUSTOM_BRDF_INCLUDED
   #define CUSTOM_BRDF_INCLUDED
   
   struct BRDF {
   	float3 diffuse;
   	float3 specular;
   	float roughness;
   };
   
   BRDF GetBRDF (Surface surface) {
   	BRDF brdf;
   	brdf.diffuse = surface.color;
   	brdf.specular = 0.0;
   	brdf.roughness = 1.0;
   	return brdf;
   }
   #endif
   ```

2. 包含brdf文件

   ```c
   #include "../ShaderLibrary/Common.hlsl"
   #include "../ShaderLibrary/Surface.hlsl"
   #include "../ShaderLibrary/Light.hlsl"
   #include "../ShaderLibrary/BRDF.hlsl"
   #include "../ShaderLibrary/Lighting.hlsl"
   ```

3. 修改光照函数

   ```c
   float3 GetLighting (Surface surface, BRDF brdf, Light light) {
   	return IncomingLight(surface, light) * brdf.diffuse;
   }
   
   float3 GetLighting (Surface surface, BRDF brdf) {
   	float3 color = 0.0;
   	for (int i = 0; i < GetDirectionalLightCount(); i++) {
   		color += GetLighting(surface, brdf, GetDirectionalLight(i));
   	}
   	return color;
   }
   ```

4. 传参

   ```c
   	BRDF brdf = GetBRDF(surface);
   	float3 color = GetLighting(surface, brdf);
   ```

### Reflectivity

用`metallic `表面属性表示反射率, 完美镜面反射的表面不会出现漫反射现象

![image-20230602143539473](.\metallic.png)

```c
#define MIN_REFLECTIVITY 0.04

float OneMinusReflectivity (float metallic) {
	float range = 1.0 - MIN_REFLECTIVITY;
	return range - metallic * range;
}

float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
brdf.diffuse = surface.color * oneMinusReflectivity;
```

### Specular Color

```c
brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);
```

### Roughness

我们可以通过PerceptualRoughnessToRoughness函数转换为实际的粗糙度值，将感知值平方化。这与迪士尼的照明模型相匹配。之所以这样做，是因为在编辑材质时，调整感知版本更直观

```c
	float perceptualRoughness =
		PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
	brdf.roughness = PerceptualRoughnessToRoughness(perceptualRoughness)
```

```c
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"
```

### View Direction

获取相机位置

```c
float3 _WorldSpaceCameraPos;
```

获取shading point的世界位置

```c
struct Varyings {
	float4 positionCS : SV_POSITION;
	float3 positionWS : VAR_POSITION;
	…
};

Varyings LitPassVertex (Attributes input) {
	…
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);
	…
}
```

将相机方向作为表面属性

```c
struct Surface {
	float3 normal;
	float3 viewDirection;
	float3 color;
	float alpha;
	float metallic;
	float smoothness;
};

	surface.normal = normalize(input.normalWS);
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
```

### Specular Strength

```c
float SpecularStrength (Surface surface, BRDF brdf, Light light) {
	float3 h = SafeNormalize(light.direction + surface.viewDirection);
	float nh2 = Square(saturate(dot(surface.normal, h)));
	float lh2 = Square(saturate(dot(light.direction, h)));
	float r2 = Square(brdf.roughness);
	float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
	float normalization = brdf.roughness * 4.0 + 2.0;
	return r2 / (d2 * max(0.1, lh2) * normalization);
}
```

![image-20230602151003157](.\urpbrdf.png)

```c
float3 DirectBRDF (Surface surface, BRDF brdf, Light light) {
	return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

float3 GetLighting (Surface surface, BRDF brdf, Light light) {
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}
```

我们现在得到了镜面反射，它为我们的表面增加了高光。对于完全粗糙的表面，高光是模仿漫反射。平滑的表面会有一个更集中的高光。一个完全光滑的表面会有一个无限小的亮点，我们看不到。需要一些散射来使其可见。

由于能量守恒，光滑表面的高光可以变得非常明亮，因为大部分到达表面碎片的光都被聚焦了。因此，我们最终看到的光比由于漫反射而产生的光要多得多，而高光部分是可见的。你可以通过将最终渲染的颜色放大来验证这一点。

### Mesh Ball

```c#
	static int
		baseColorId = Shader.PropertyToID("_BaseColor"),
		metallicId = Shader.PropertyToID("_Metallic"),
		smoothnessId = Shader.PropertyToID("_Smoothness");

	…
	float[]
		metallic = new float[1023],
		smoothness = new float[1023];

	…

	void Update () {
		if (block == null) {
			block = new MaterialPropertyBlock();
			block.SetVectorArray(baseColorId, baseColors);
			block.SetFloatArray(metallicId, metallic);
			block.SetFloatArray(smoothnessId, smoothness);
		}
		Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block);
	}
	
				baseColors[i] =
				new Vector4(
					Random.value, Random.value, Random.value,
					Random.Range(0.5f, 1f)
				);
			metallic[i] = Random.value < 0.25f ? 1f : 0f;
			smoothness[i] = Random.Range(0.05f, 0.95f);
```

## Transparency

### Premultiplied Alpha

1. 将source blend mode 设置为one,destination blend mode设置成minus-source-alpha 

2. ```
   	brdf.diffuse = surface.color * oneMinusReflectivity;
   	brdf.diffuse *= surface.alpha;
   ```

### Premultiplication Toggle

```c
BRDF GetBRDF (inout Surface surface, bool applyAlphaToDiffuse = false) {
	…
	if (applyAlphaToDiffuse) {
		brdf.diffuse *= surface.alpha;
	}

	…
}
```

```c
	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif
	float3 color = GetLighting(surface, brdf);
	return float4(color, surface.alpha);
```

```
			#pragma shader_feature _CLIPPING
			#pragma shader_feature _PREMULTIPLY_ALPHA
```

```
		[Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
```

## Shader GUI

为使用shader的材质编写编辑器

### Custom Shader GUI

声明和创建材质Editor类

```c#
Shader "Custom RP/Lit" {
	…

	CustomEditor "CustomShaderGUI"
}
```

```c#
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI {

	public override void OnGUI (
		MaterialEditor materialEditor, MaterialProperty[] properties
	) {
		base.OnGUI(materialEditor, properties);
	}
}
```

### Setting Properties and Keywords

1. 保存`editor`, `material`, `properties`，以便之后访问

   ```c#
   	MaterialEditor editor;
   	Object[] materials;
   	MaterialProperty[] properties;
   
   	public override void OnGUI (
   		MaterialEditor materialEditor, MaterialProperty[] properties
   	) {
   		base.OnGUI(materialEditor, properties);
   		editor = materialEditor;
   		materials = materialEditor.targets;
   		this.properties = properties;
   	}
   ```

2. 调用`FindProperty`方法获取属性，并设置值

   ```c#
   	void SetProperty (string name, float value) {
   		FindProperty(name, properties).floatValue = value;
   	}
   ```

   

3. 增加通过关键词设置的方法

   ```c#
   	void SetKeyword (string keyword, bool enabled) {
   		if (enabled) {
   			foreach (Material m in materials) {
   				m.EnableKeyword(keyword);
   			}
   		}
   		else {
   			foreach (Material m in materials) {
   				m.DisableKeyword(keyword);
   			}
   		}
   	}
   
   	void SetProperty (string name, string keyword, bool value) {
   		SetProperty(name, value ? 1f : 0f);
   		SetKeyword(keyword, value);
   	}
   ```

   

4. 对每个属性调用该方法

   ```c#
   	bool Clipping {
   		set => SetProperty("_Clipping", "_CLIPPING", value);
   	}
   
   	bool PremultiplyAlpha {
   		set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
   	}
   
   	BlendMode SrcBlend {
   		set => SetProperty("_SrcBlend", (float)value);
   	}
   
   	BlendMode DstBlend {
   		set => SetProperty("_DstBlend", (float)value);
   	}
   
   	bool ZWrite {
   		set => SetProperty("_ZWrite", value ? 1f : 0f);
   	}
   
   	RenderQueue RenderQueue {
   		set {
   			foreach (Material m in materials) {
   				m.renderQueue = (int)value;
   			}
   		}
   	}
   ```

### Preset Buttons

1. 创建button并设置undo操作

   ```c#
   	bool PresetButton (string name) {
   		if (GUILayout.Button(name)) {
   			editor.RegisterPropertyChangeUndo(name);
   			return true;
   		}
   		return false;
   	}
   ```

2. 对各个属性创建button，设置回调时的操作

   ```c#
   	void OpaquePreset () {
   		if (PresetButton("Opaque")) {
   			Clipping = false;
   			PremultiplyAlpha = false;
   			SrcBlend = BlendMode.One;
   			DstBlend = BlendMode.Zero;
   			ZWrite = true;
   			RenderQueue = RenderQueue.Geometry;
   		}
   	}
   
   	void ClipPreset () {
   		if (PresetButton("Clip")) {
   			Clipping = true;
   			PremultiplyAlpha = false;
   			SrcBlend = BlendMode.One;
   			DstBlend = BlendMode.Zero;
   			ZWrite = true;
   			RenderQueue = RenderQueue.AlphaTest;
   		}
   	}
   
   	void FadePreset () {
   		if (PresetButton("Fade")) {
   			Clipping = false;
   			PremultiplyAlpha = false;
   			SrcBlend = BlendMode.SrcAlpha;
   			DstBlend = BlendMode.OneMinusSrcAlpha;
   			ZWrite = false;
   			RenderQueue = RenderQueue.Transparent;
   		}
   	}
   	void TransparentPreset () {
   		if (PresetButton("Transparent")) {
   			Clipping = false;
   			PremultiplyAlpha = true;
   			SrcBlend = BlendMode.One;
   			DstBlend = BlendMode.OneMinusSrcAlpha;
   			ZWrite = false;
   			RenderQueue = RenderQueue.Transparent;
   		}
   	}
   
   ```

3. 在`OnGUI`中调用(Flold out)

   ```c#
   bool showPresets;
   
   public override void OnGUI (
   		MaterialEditor materialEditor, MaterialProperty[] properties
   	) {
   		…
   
   		EditorGUILayout.Space();
   		showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
   		if (showPresets) {
   			OpaquePreset();
   			ClipPreset();
   			FadePreset();
   			TransparentPreset();
   		}
   	}
   	}
   ```

### Presets for Unlit

支持unlitshader的GUI

```C#
	bool SetProperty (string name, float value) {
		MaterialProperty property = FindProperty(name, properties, false);
		if (property != null) { // 判断属性是否存在
			property.floatValue = value;
			return true;
		}
		return false;
	}

	void SetProperty (string name, string keyword, bool value) {
		if (SetProperty(name, value ? 1f : 0f)) {
			SetKeyword(keyword, value);
		}
	}
```

### No Transparency

unlit没有Transparency的处理，所以取出它

```c#
	bool HasProperty (string name) =>
		FindProperty(name, properties, false) != null;
		
   bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");
   
   		if (HasPremultiplyAlpha && PresetButton("Transparent")) { … }
```

