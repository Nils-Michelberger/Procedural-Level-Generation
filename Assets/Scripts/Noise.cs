using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise
{
    // global = values are getting localized by estimate (max, min is defined by a constant)
    // local = values are getting localized by setting max and min depending on generated values (max, min is changing during runtime) (CREATES GAPS!)
    public enum NormalizeMode
    {
        Global,
        Local
    }

    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, int seed, float scale, int octaves,
        float persistance, float lacunarity, Vector2 offset, NormalizeMode normalizeMode)
    {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        // define random seed to offset octaves
        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];
        
        float maxPossibleHeight = 0;
        float amplitude = 1;
        
        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) - offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        
            maxPossibleHeight += amplitude;
            amplitude *= persistance;
        }
        
        if (scale <= 0)
        {
            scale = 0.0001f;
        }
        
        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;
        
        // center noise map
        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;
        
        // iterate over map
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;
        
                // apply details (layering octaves)
                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = (x - halfWidth + octaveOffsets[i].x) / scale * frequency;
                    float sampleY = (y - halfHeight + octaveOffsets[i].y) / scale * frequency;
        
                    // change range from [0, 1] to [-1, 1]
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;
        
                    amplitude *= persistance;
                    frequency *= lacunarity;
                }
        
                if (noiseHeight > maxLocalNoiseHeight)
                {
                    maxLocalNoiseHeight = noiseHeight;
                }
                else if (noiseHeight < minLocalNoiseHeight)
                {
                    minLocalNoiseHeight = noiseHeight;
                }
        
                noiseMap[x, y] = noiseHeight;
            }
        }
        
        // normalize noiseMap
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                if (normalizeMode == NormalizeMode.Local)
                {
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                }
                else
                {
                    // last value is used to adjust max map height
                    float normalizedHeight = noiseMap[x, y] + 1 / (maxPossibleHeight / 2f);
                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }
        }
        
        // // -- Regular Noise --
        // System.Random random = new System.Random();
        //
        // for (int y = 0; y < mapHeight; y++)
        // {
        //     for (int x = 0; x < mapWidth; x++)
        //     {
        //         noiseMap[x, y] = (float) random.NextDouble() * 1.5f;
        //     }
        // }
        
        // // -- Raw Perlin Noise --
        // for (int y = 0; y < mapHeight; y++)
        // {
        //     for (int x = 0; x < mapWidth; x++)
        //     {
        //         float perlinNoise = (Mathf.PerlinNoise((x + 200) * 0.1f, (y + 200) * 0.1f) * 2.3f - 0.4f);
        //         noiseMap[x, y] = perlinNoise;
        //     }
        // }
    
        return noiseMap;
    }
}