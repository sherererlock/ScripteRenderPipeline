## **Custom Render Pipeline**

### Project Set Up

1. 创建3D项目
2. 移除不需要的Package,留着Unity UI Package
3. 颜色空间修改为linear color space
4. 分别用standard，unlit和transparent材质在场景中创建物体

### Pipeline Asset

1. 创建CustomRenderPipelineAsset,实现CreatePipeline接口，添加资产创建按钮

2. 并且创建该资产，设置到unity的渲染管线中

   ```c#
   using UnityEngine;
   using UnityEngine.Rendering;
   
   [CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
   public class CustomRenderPipelineAsset : RenderPipelineAsset 
   {
       protected override RenderPipeline CreatePipeline () 
       {
   		return new CustomRenderPipeline();
   	}
   }
   ```

3. 创建RenderPipelineInstance，在CustomRenderPipelineAsset的接口中返回它

   ```c#
   using UnityEngine;
   using UnityEngine.Rendering;
   
   public class CustomRenderPipeline : RenderPipeline 
   {
       CameraRenderer renderer = new CameraRenderer();
   	protected override void Render (
   		ScriptableRenderContext context, Camera[] cameras
   	)
       {
      		foreach (Camera camera in cameras) {
   			renderer.Render(context, camera);
   		} 
       }
   }
   ```

   ### Rendering

   1. 创建Camera Renderer来负责每个相机的渲染,在CustomRenderPipeline中循环调用
   2. 画天空盒,并提交命令到GPU
   3. 更新相机矩阵到Shader


### Command Buffers

1. 在CameraRender中创建CommandBuffer用来渲染常规的物体
2. 统计Command用时，显示在Profiler和FrameDebug中
3. 执行CommandBuffer里的命令，之后清除Buffer以便之后使用

### Clearing the Render Target

1. 调用`buffer.ClearRenderTarget(depth, color, Color.clear);`
2. 因为`buffer.ClearRenderTarget(true, true, Color.clear);`也包含了一个BeginSample，所以得将其放在BeginSample之前
3. 将`context.SetupCameraProperties(camera); `放在Setup的最前面防止多画四边形

### Culling

遍历场景中具有`renderer`组件的物体，去除那些摄像机不可见的物体

`ScriptableCullingParameters`用来存储摄像机的的设置和矩阵，以便我们用来裁切物体（`TryGetCullingParameters`）

`CullingResults` 裁剪的结果

1. 获取相机中有关于裁切的参数
2. 调用`context.Cull(ref p);`获得可见的物体

### Drawing Geometry

`context.DrawRenderers(CullingResults, ref DrawingSettings, ref FilteringSettings)`

`SortingSettings` 

- Camera提供投影方式，以及物体的远近信息
- SortingCriteria 物体排序的方式

`drawingSettings `

- 支持的shader passes的种类
- `SortingSettings` 

`FilteringSettings`  描述了如何过滤ScriptableRenderContext.DrawRenderers接收的对象集，以便Unity绘制其中的一个子集。

1. 创建DrawSetting
   - shader passe种类： "SRPDefaultUnlit"
   - sorting 方法：SortingCriteria.CommonOpaque
2. 创建FilteringSetting，**renderQueueRange**为`RenderQueueRange.all`.
3. 调用context.DrawRenderers(CullingResults, ref DrawingSettings, ref FilteringSettings)

### Drawing Opaque and Transparent Geometry Separately

透明物体不写深度，所以在画天空盒时，透明物体的像素被天空盒给填充了。所以要修改渲染顺序

1. 画不透明的物体
   - 修改过滤设置为`RenderQueueRange.opaque`
2. 画天空盒
3. 画透明物体
   - 修改过滤设置为`RenderQueueRange.transparent`
   - 设置排序设置为SortingCriteria.CommonTransparent

## Editor Rendering

以下代码均写在CameraRender.Editor.cs中

### Drawing Legacy Shaders

将管线不支持的shader passes以error material的方式渲染到场景中，以起到提示作用

### Drawing Gizmos

`context.DrawGizmos(camera, GizmoSubset.PreImageEffects)`

### Drawing Unity UI

## Multiple Cameras

### Two Cameras

以Camera Depth升序来渲染相机

 具有相同名称的相邻scope会被合并 ，所以我们得在渲染时，给commandbuffer不同得name来区别是哪个相机渲染的命令

### Dealing with Changing Buffer Names

- 由于bufferName和buffer.name不同，所以unity会报warning
- 每次访问camea.name会分配内存，效率不高

1. 添加SampleName，在Editor下，设置为camera.name，在build版本下，设置为bufferName

### Layers

摄像机可以只用来渲染特定layer的物体

1. 将所有使用standard shader的物体标为 *Ignore Raycast* layer
2. 从MainCamera排除 *Ignore Raycast* layer，SecondaryCamera只渲染 *Ignore Raycast* layer

### Clear Flags

CameraClearFlags 枚举定义了四个值。从1到4，它们是Skybox、Color、Depth和Nothing。这些实际上并不是独立的标志值，而是代表一个递减的清除量。深度缓冲区在所有情况下都必须被清除，除了最后一种情况，所以标志值最多为深度。

当相机的`ClearFlags`小于等于Depth时，我们才clear Depth

当相机的`ClearFlags`等于color时，我们才clear color，需要用相机的backgroundColor.linear来替换颜色

 *Main Camera*的`ClearFlags`应该设置成Skybox或Color

*Secondary Camera* 的`ClearFlags`影响两者如何合并。

------

CameraRender.cs

```c#
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer {
	ScriptableRenderContext context;
	Camera camera;
    CullingResults cullingResults;
    
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    
	const string bufferName = "Render Camera";
	CommandBuffer buffer = new CommandBuffer {
		name = bufferName
	};    

	public void Render (ScriptableRenderContext context, Camera camera) {
		this.context = context;
		this.camera = camera;
         PrepareForSceneWindow();
         PrepareBuffer();
		if (!Cull()) {
			return;
		}
        
        Setup();
        DrawVisibleGeometry();
        DrawUnsupportedShaders();
        DrawGizmos();
        Submit();
	}
    
   void Setup () {
       context.SetupCameraProperties(camera); // 更新View-Peojection矩阵
       buffer.ClearRenderTarget(true, true, Color.clear);
       buffer.BeginSample(SampleName);
       ExecuteBuffer ();
	}
    
   	void DrawVisibleGeometry () {
        
        var sortingSettings = new SortingSettings(camera){
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(
            unlitShaderTagId, sortingSettings
        );
        
        //先渲染不透明物体
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(
            cullingResults, ref drawingSettings, ref filteringSettings
        );
       
        context.DrawSkybox(camera); // 此处相机决定天空盒是否被画,clear value
        
        // 再渲染透明物体
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers(
            cullingResults, ref drawingSettings, ref filteringSettings
		);        
	} 
    
    void Submit () {
        buffer.EndSample(SampleName);
        context.Submit();
	}
    
	void ExecuteBuffer () {
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
    
    bool Cull () {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
    
}
```

CameraRender.Editor.cs

```c#
public partial class CameraRenderer {
    partial void DrawUnsupportedShaders ();
    partial void DrawGizmos ();
    partial void PrepareForSceneWindow ();
    partial void PrepareBuffer ();
    
    #if UNITY_EDITOR

    string SampleName { get; set; }
    
	static ShaderTagId[] legacyShaderTagIds = {
		new ShaderTagId("Always"),
		new ShaderTagId("ForwardBase"),
		new ShaderTagId("PrepassBase"),
		new ShaderTagId("Vertex"),
		new ShaderTagId("VertexLMRGBM"),
		new ShaderTagId("VertexLM")
	};

	partial void DrawGizmos () {
		if (Handles.ShouldRenderGizmos()) {
			context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
			context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
		}
	}

	static Material errorMaterial;
	void DrawUnsupportedShaders ()
    {
		if (errorMaterial == null) {
			errorMaterial =
				new Material(Shader.Find("Hidden/InternalErrorShader"));
		}
        
		var drawingSettings = new DrawingSettings(
			legacyShaderTagIds[0], new SortingSettings(camera)
		)
        {
        	overrideMaterial = errorMaterial    
        };
        
        for (int i = 1; i < legacyShaderTagIds.Length; i++) {
			drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
		} 
		var filteringSettings = FilteringSettings.defaultValue;
		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);        
    }
    
	partial void PrepareForSceneWindow () {
		if (camera.cameraType == CameraType.SceneView) {
			ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
		}
	}    
    
   	partial void PrepareBuffer () {
		buffer.name = SampleName = camera.name; // 区别不同相机提交的渲染命令
	} 
    #else
    const string SampleName = bufferName;
	#endif    
};
```

