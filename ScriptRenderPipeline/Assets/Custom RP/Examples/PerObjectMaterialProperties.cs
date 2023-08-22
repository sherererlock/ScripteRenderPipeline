using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    static int baseColorId = Shader.PropertyToID("_BaseColor");
    static int cutOffId = Shader.PropertyToID("_Cutoff");
    // Start is called before the first frame update

    [SerializeField]
    Color baseColor = Color.white;

    [SerializeField, Range(0f, 1f)]
    float cutoff = 0.5f;

    static MaterialPropertyBlock block;

    void Awake()
    {
        OnValidate();
    }

    void OnValidate()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        block.SetFloat(cutOffId, cutoff);
        block.SetColor(baseColorId, baseColor);
        GetComponent<Renderer>().SetPropertyBlock(block);
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
