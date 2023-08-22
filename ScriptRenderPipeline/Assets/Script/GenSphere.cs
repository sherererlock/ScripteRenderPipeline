
using System.Collections.Generic;
using UnityEngine;

public class GenSphere : MonoBehaviour
{
    // Update is called once per frame
    public GameObject spherePrefab; // ��ҪԤ�ȴ���һ�������Ԥ����
    public int numberOfSpheres = 76;
    public float minX = -10f; // ƽ�����СX����
    public float maxX = 10f; // ƽ������X����
    public float minZ = -10f; // ƽ�����СZ����
    public float maxZ = 10f; // ƽ������Z����
    public Material[] materials; // һ���������
    public Vector2 RegionSize = new Vector2(5, 5);
    public float radius = 0.5f;

    [ContextMenu("GenerateRandomSpheres")]
    void GenerateRandomSpheres()
    {
        for (int i = 0; i < numberOfSpheres; i++)
        {
            float randomX = Random.Range(minX, maxX);
            float randomZ = Random.Range(minZ, maxZ);
            Vector3 spawnPosition = new Vector3(randomX, 0f, randomZ);

            GameObject sphere = Instantiate(spherePrefab, spawnPosition, Quaternion.identity);
            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            if (sphereRenderer != null && materials.Length > 0)
            {
                Material randomMaterial = materials[Random.Range(0, materials.Length)];
                sphereRenderer.material = randomMaterial;
            }

            sphere.transform.parent = transform;
        }
    }

    [ContextMenu("GeneratePoissonSpheresPoisson")]
    void GeneratePoissonSpheresPoisson()
    {
        List<Vector2> points = PoissonDiskSampling.GeneratePoint(radius, RegionSize);

        for (int i = 0; i<points.Count; i++)
        {
            Vector3 spawnPosition = new Vector3();
            spawnPosition.x = points[i].x;
            spawnPosition.z = points[i].y;
            GameObject sphere = Instantiate(spherePrefab, spawnPosition, Quaternion.identity);

            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            if (sphereRenderer != null && materials.Length > 0)
            {
                Material randomMaterial = materials[Random.Range(0, materials.Length)];
                sphereRenderer.material = randomMaterial;
            }

            sphere.transform.parent = transform; 
        }
    }
}
