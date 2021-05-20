using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using Random = UnityEngine.Random;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode
    {
        NoiseMap,
        Mesh,
        FalloffMap
    };

    public DrawMode drawMode;

    public TerrainData terrainData;
    public NoiseData noiseData;
    public TextureData textureData;
    public PrefabsData prefabsData;
    public Material terrainMaterial;

    [Range(0, MeshGenerator.numSupportedChunkSizes - 1)]
    public int chunkSizeIndex;

    [Range(0, MeshGenerator.numSupportedFlatshadedChunkSizes - 1)]
    public int flatshadedChunkSizeIndex;

    // default = 1 || possible values = 1, 2, 4, 6, 8
    [Range(0, MeshGenerator.numSupportedLODs - 1)]
    public int editorPreviewLevelOfDetail;

    public bool autoUpdate;

    private float[,] falloffMap;

    private Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    private Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    private Transform mesh;

    private void Awake()
    {
        textureData.ApplyToMaterial(terrainMaterial);
        textureData.UpdateMeshHeight(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
    }

    void OnValuesUpdated()
    {
        if (!Application.isPlaying)
        {
            DrawMapInEditor();
        }
    }

    void OnTextureValuesUpdated()
    {
        textureData.ApplyToMaterial(terrainMaterial);
    }

    // example: 240 + 1 (because we need to subtract 1 in LOD step (w-1)) -> 241-2 (because we have to subtract the border vertices)
    // result: supportedSize - 1
    public int mapChunkSize
    {
        get
        {
            if (terrainData.useFlatShading)
            {
                return MeshGenerator.supportedFlatshadedChunkSizes[flatshadedChunkSizeIndex] - 1;
            }
            else
            {
                return MeshGenerator.supportedChunkSizes[chunkSizeIndex] - 1;
            }
        }
    }

    public void DrawMapInEditor()
    {
        textureData.UpdateMeshHeight(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
        MapData mapData = GenerateMapData(Vector2.zero);

        MapDisplay display = FindObjectOfType<MapDisplay>();

        mesh = GameObject.FindWithTag("Mesh").transform;

        System.Random random = new System.Random();

        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, terrainData.meshHeightMultiplier,
                terrainData.meshHeightCurve, editorPreviewLevelOfDetail, terrainData.useFlatShading));

            // clear old trees
            foreach (Transform child in mesh)
            {
                DestroyImmediate(child.gameObject);
            }

            // spawn new trees
            foreach (Vector3 treeSpawnPoint in mapData.treeSpawnPoints)
            {
                Instantiate(prefabsData.trees[(int) (random.NextDouble() * prefabsData.trees.Length)], treeSpawnPoint,
                    Quaternion.identity, mesh.transform);
            }
        }
        else if (drawMode == DrawMode.FalloffMap)
        {
            display.DrawTexture(
                TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(mapChunkSize)));
        }
    }

    #region Threading

    // start the thread with callback method
    public void RequestMapData(Vector2 center, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate { MapDataThread(center, callback); };

        new Thread(threadStart).Start();
    }

    // generate MapData in thread and put finished MapData into queue
    private void MapDataThread(Vector2 center, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(center);
        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate { MeshDataThread(mapData, lod, callback); };

        new Thread(threadStart).Start();
    }

    private void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
    {
        MeshData meshData =
            MeshGenerator.GenerateTerrainMesh(mapData.heightMap, terrainData.meshHeightMultiplier,
                terrainData.meshHeightCurve, lod, terrainData.useFlatShading);
        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    // check if any MapData is ready. Execute callback method (EndlessTerrain.OnMapDataReceived)
    private void Update()
    {
        if (mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }

    #endregion

    private MapData GenerateMapData(Vector2 center)
    {
        float[,] heightMap = Noise.GenerateNoiseMap(mapChunkSize + 2, mapChunkSize + 2, noiseData.seed,
            noiseData.noiseScale, noiseData.octaves, noiseData.persistance, noiseData.lacunarity,
            center + noiseData.offset, noiseData.normalizeMode);

        if (terrainData.useFalloff)
        {
            if (falloffMap == null)
            {
                falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize + 2);
            }

            // loop over map
            for (int y = 0; y < mapChunkSize + 2; y++)
            {
                for (int x = 0; x < mapChunkSize + 2; x++)
                {
                    if (terrainData.useFalloff)
                    {
                        // value at the end indicates falloff strength
                        heightMap[x, y] = Mathf.Clamp01(heightMap[x, y] - (falloffMap[x, y] * 1.5f));
                    }
                }
            }
        }

        System.Random random = new System.Random();
        List<Vector3> treeSpawnPoints = new List<Vector3>();

        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                // set spawn points for trees
                if (heightMap[x, y] > 0.3f && heightMap[x, y] < prefabsData.maxSpawnHeight &&
                    random.NextDouble() <= prefabsData.density)
                {
                    treeSpawnPoints.Add(GetSpawnPoint(center, heightMap, x, y));
                }
            }
        }


        return new MapData(heightMap, treeSpawnPoints);
    }

    private Vector3 GetSpawnPoint(Vector2 center, float[,] heightMap, int x, int y)
    {
        return new Vector3((x - mapChunkSize / 2 + center.x) * 2,
            ((heightMap[x, y] * 2 - 1) * terrainData.meshHeightMultiplier) + 6,
            -(y - mapChunkSize / 2 - center.y) * 2);
    }

    private void OnValidate()
    {
        if (terrainData != null)
        {
            terrainData.OnValuesUpdated -= OnValuesUpdated;
            terrainData.OnValuesUpdated += OnValuesUpdated;
        }

        if (noiseData != null)
        {
            noiseData.OnValuesUpdated -= OnValuesUpdated;
            noiseData.OnValuesUpdated += OnValuesUpdated;
        }

        if (textureData != null)
        {
            textureData.OnValuesUpdated -= OnTextureValuesUpdated;
            textureData.OnValuesUpdated += OnTextureValuesUpdated;
        }

        if (prefabsData != null)
        {
            prefabsData.OnValuesUpdated -= OnValuesUpdated;
            prefabsData.OnValuesUpdated += OnValuesUpdated;
        }
    }
}

public struct MapData
{
    public readonly float[,] heightMap;
    public readonly List<Vector3> treeSpawnPoints;

    public MapData(float[,] heightMap, List<Vector3> treeSpawnPoints)
    {
        this.heightMap = heightMap;
        this.treeSpawnPoints = treeSpawnPoints;
    }
}