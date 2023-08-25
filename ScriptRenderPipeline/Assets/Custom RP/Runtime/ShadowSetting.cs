using UnityEngine;

[System.Serializable]
public class ShadowSetting
{
    [Min(0.001f)]
    public float maxDistance = 100.0f;

    [Range(0.001f, 1f)]
    public float distanceFade = 0.1f;

    public enum TextureSize
    {
        _256 = 256, _512 = 512, _1024 = 1024,
        _2048 = 2048, _4096 = 4096, _8192 = 8192
    }

    public enum FilterMode
    {
        PCF2X2, PCF3X3, PCF5X5, PCF7X7
    }

    [System.Serializable]
    public struct Directional
    {
        public enum CascadeBlendMode
        {
            Hard, Soft, Dither
        }

        [Range(0f, 1f)]
        public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
        [Range(1, 4f)]
        public int cascadeCount;
        public TextureSize atlasSize;

        [Range(0.001f, 1f)]
        public float cascadeFade;

        public FilterMode filterMode;

        public CascadeBlendMode cadcadeBlend;

        public Vector3 CascadeRatios => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);

    }

    public Directional directional = new Directional
    {
        atlasSize = TextureSize._1024,
        cascadeCount = 4,
        cascadeRatio1 = 0.1f,
        cascadeRatio2 = 0.25f,
        cascadeRatio3 = 0.5f,
        cascadeFade = 0.1f,
        filterMode = FilterMode.PCF2X2,
        cadcadeBlend = Directional.CascadeBlendMode.Hard,
    };
}

