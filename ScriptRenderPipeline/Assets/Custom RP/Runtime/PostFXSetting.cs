using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSetting : ScriptableObject
{
    [SerializeField]
    Shader shader = default;

    [System.NonSerialized]
    Material material;

    [System.Serializable]
    public struct BloomSettings
    {
        public enum Mode { Additive, Scattering }

        [Range(0f, 16f)]
        public int maxIterations;

        [Min(1f)]
        public int downScaleLimit;

        [Min(0f)]
        public float threshold;

        [Range(0f, 1f)]
        public float thresholdKnee;

        [Min(0f)]
        public float intensity;

        public bool bicubicUpsampling;

        public bool fadeFireFlies;

        public Mode mode;

        [Range(0.05f, 0.95f)]
        public float scatter;
    }

    [SerializeField]
    BloomSettings bloom = new BloomSettings
    {
        scatter = 0.7f
    };

    public BloomSettings Bloom => bloom;

    public Material Material
    {
        get
        { 
            if (material == null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }

            return material;
        }
    }
}

