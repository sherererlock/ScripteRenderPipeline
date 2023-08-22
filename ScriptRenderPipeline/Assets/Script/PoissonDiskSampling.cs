using System.Collections.Generic;
using UnityEngine;

public static class PoissonDiskSampling
{
    public static List<Vector2> GeneratePoint(float radius, Vector2 regionSize, int numRejectionSmaple = 30)
    {
        float cellSize = radius / Mathf.Sqrt(2f);
        int cellNumX = Mathf.CeilToInt(regionSize.x / cellSize);
        int cellNumY = Mathf.CeilToInt(regionSize.y / cellSize);

        int[,] grid = new int[cellNumX, cellNumY];
        List<Vector2> points = new List<Vector2>();
        List<Vector2> samplePoints = new List<Vector2>();
        samplePoints.Add(regionSize / 2f);

        while (samplePoints.Count > 0)
        {
            int spawnIndex = Random.Range(0, samplePoints.Count);
            Vector2 spawnCenter = samplePoints[spawnIndex];
            bool candidateAccepted = false;
            for (int i = 0; i < numRejectionSmaple; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 candidate = spawnCenter + dir * Random.Range(radius, 2f * radius);
                if (IsValidate(candidate, regionSize, radius, cellSize, ref grid, ref points))
                {
                    points.Add(candidate);
                    samplePoints.Add(candidate);
                    grid[(int)(candidate.x / cellSize), (int)(candidate.y / cellSize)] = points.Count;
                    candidateAccepted = true;
                    break;
                }
            }

            if (!candidateAccepted)
            {
                samplePoints.RemoveAt(spawnIndex);
            }
        }

        return points;
    }

    private static bool IsValidate(Vector2 candidate, Vector2 regionSize, float radius, float cellSize, ref int[,] grid, ref List<Vector2> points)
    {
        if (candidate.x >= 0f && candidate.x <= regionSize.x && candidate.y >= 0f && candidate.y <= regionSize.y)
        {
            int cellX = (int)(candidate.x / cellSize);
            int cellY = (int)(candidate.y / cellSize);
            int startCellX = Mathf.Max(0, cellX - 2);
            int endCellX = Mathf.Min(cellX + 2, grid.GetLength(0) - 1);

            int startCellY = Mathf.Max(0, cellY - 2);
            int endCellY = Mathf.Min(cellY + 2, grid.GetLength(1) - 1);

            for (int x = startCellX; x <= endCellX; x++)
            {
                for (int y = startCellY; y <= endCellY; y++)
                {
                    int index = grid[x, y] - 1;
                    if (index != -1)
                    {
                        float dist = (points[index] - candidate).sqrMagnitude;
                        if (dist < radius * radius)
                            return false;
                    }
                }
            }

            return true;
        }

        return false;
    }
}
