using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode
    {
        NoiseMap,
        ColorMap,
        Mesh
    };
    public DrawMode drawMode;
    
    public int mapWidth;
    public int mapHeight;
    public float noiseScale;

    public int octaves;

    // values greater than 1 have no significant impact
    [Range(0, 1)] public float persistance;
    public float lacunarity;

    public int seed;
    public Vector2 offset;

    public bool autoUpdate;

    public TerrainType[] regions;
    
    public void GenerateMap()
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(mapWidth, mapHeight, seed, noiseScale, octaves, persistance,
            lacunarity, offset);
        
        MapDisplay display = FindObjectOfType<MapDisplay>();
        
        Color[] colorMap = new Color[mapWidth * mapHeight];
        
        // loop over map
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                float currentHeight = noiseMap[x, y];
                
                // loop through all regions and find correct color according to height
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight <= regions[i].height)
                    {
                        colorMap[y * mapWidth + x] = regions[i].color;
                        break;
                    }
                }
            }
        }
        
        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(noiseMap));
        }
        else if (drawMode == DrawMode.ColorMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromColorMap(colorMap, mapWidth, mapHeight));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(noiseMap),
                TextureGenerator.TextureFromColorMap(colorMap, mapWidth, mapHeight));
        }
    }

    // clamp values
    private void OnValidate()
    {
        if (mapWidth < 0)
        {
            mapWidth = 1;
        }

        if (mapHeight < 0)
        {
            mapHeight = 0;
        }

        // negative values are mirrored
        if (lacunarity < 0)
        {
            lacunarity = 1;
        }

        if (octaves < 0)
        {
            octaves = 0;
        }
    }
}

[Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
}