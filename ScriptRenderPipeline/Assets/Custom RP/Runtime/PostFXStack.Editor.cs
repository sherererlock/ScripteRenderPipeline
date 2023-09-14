using UnityEngine;
using UnityEditor;

public partial class PostFXStack
{
    partial void ApplySceneViewState();

#if UNITY_EDITOR
    partial void ApplySceneViewState()
    {
        if (camera.cameraType == CameraType.SceneView && !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects)
            postFXSetting = null;

    }
#endif
}
