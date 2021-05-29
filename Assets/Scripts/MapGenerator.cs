using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode
    {
        NoiseMap,
        Mesh,
        FalloffMap
    };

    public enum Biome
    {
        Default,
        LowPoly,
        Desert,
        Custom
    }

    public DrawMode drawMode;

    public Biome biome;

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

        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, terrainData.meshHeightMultiplier,
                terrainData.meshHeightCurve, editorPreviewLevelOfDetail, terrainData.useFlatShading));

            // clear all prefabs
            for (int i = mesh.childCount; i > 0; i--)
            {
                DestroyImmediate(mesh.transform.GetChild(0).gameObject);
            }

            // spawn new trees
            SpawnPrefabs(mapData.treeSpawnPoints, prefabsData.treePrefabs);

            //spawn new stones
            SpawnPrefabs(mapData.stoneSpawnPoints, prefabsData.stonePrefabs);
        }
        else if (drawMode == DrawMode.FalloffMap)
        {
            display.DrawTexture(
                TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(mapChunkSize)));
        }
    }

    private void SpawnPrefabs(List<Vector3> spawnPoints, GameObject[] prefabs)
    {
        System.Random random = new System.Random();

        foreach (Vector3 spawnPoint in spawnPoints)
        {
            Instantiate(prefabs[(int) (random.NextDouble() * prefabs.Length)], spawnPoint,
                Quaternion.Euler(Quaternion.identity.x, (int) (random.NextDouble() * 360f), Quaternion.identity.z),
                mesh.transform);
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
        List<Vector3> stoneSpawnPoints = new List<Vector3>();
        List<Vector3> spawnPoints = new List<Vector3>();

        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                // set spawn points for trees
                if (heightMap[x, y] >= prefabsData.treeMinSpawnHeight && heightMap[x, y] <= prefabsData.treeMaxSpawnHeight &&
                    random.NextDouble() <= prefabsData.treeDensity)
                {
                    Vector3 treeSpawnPoint = GetSpawnPoint(center, heightMap, x, y, prefabsData.treeSpawnHeightMultiplier);

                    if (!prefabsData.collisionFreeSpawning || CheckCollision(spawnPoints, treeSpawnPoint))
                    {
                        treeSpawnPoints.Add(treeSpawnPoint);
                        spawnPoints.Add(treeSpawnPoint);
                    }
                }

                // set spawn points for stones
                if (heightMap[x, y] >= prefabsData.stoneMinSpawnHeight && heightMap[x, y] <= prefabsData.stoneMaxSpawnHeight &&
                    random.NextDouble() <= prefabsData.stoneDensity)
                {
                    Vector3 stoneSpawnPoint = GetSpawnPoint(center, heightMap, x, y, prefabsData.stoneSpawnHeightMultiplier);

                    if (!prefabsData.collisionFreeSpawning ||CheckCollision(spawnPoints, stoneSpawnPoint))
                    {
                        stoneSpawnPoints.Add(stoneSpawnPoint);
                        spawnPoints.Add(stoneSpawnPoint);
                    }
                }
            }
        }


        return new MapData(heightMap, treeSpawnPoints, stoneSpawnPoints);
    }

    private bool CheckCollision(List<Vector3> spawnPoints, Vector3 treeSpawnPoint)
    {
        bool setSpawnPoint = true;
        
        foreach (Vector3 spawnPoint in spawnPoints)
        {
            if (Vector3.Distance(treeSpawnPoint, spawnPoint) < prefabsData.minSpawnDistance)
            {
                setSpawnPoint = false;
                break;
            }
        }

        return setSpawnPoint;
    }

    private Vector3 GetSpawnPoint(Vector2 center, float[,] heightMap, int x, int y, float spawnHeightMultiplier)
    {
        AnimationCurve heightCurveCopy = new AnimationCurve(terrainData.meshHeightCurve.keys);
        float height = heightCurveCopy.Evaluate(heightMap[x, y]) * terrainData.meshHeightMultiplier;
        return new Vector3((x - mapChunkSize / 2 + center.x) * 2,
            height * spawnHeightMultiplier, -(y - mapChunkSize / 2 - center.y) * 2);
    }

    private void OnValidate()
    {
        switch (biome)
        {
            case Biome.Default:
                terrainData = Resources.Load<TerrainData>("Terrain Assets/Default/Terrain");
                noiseData = Resources.Load<NoiseData>("Terrain Assets/Default/Noise");
                textureData = Resources.Load<TextureData>("Terrain Assets/Default/Texture");
                prefabsData = Resources.Load<PrefabsData>("Terrain Assets/Default/Prefabs");
                break;
            case Biome.LowPoly:
                terrainData = Resources.Load<TerrainData>("Terrain Assets/Low Poly/Terrain");
                noiseData = Resources.Load<NoiseData>("Terrain Assets/Low Poly/Noise");
                textureData = Resources.Load<TextureData>("Terrain Assets/Low Poly/Texture");
                prefabsData = Resources.Load<PrefabsData>("Terrain Assets/Low Poly/Prefabs");
                break;
            case Biome.Desert:
                terrainData = Resources.Load<TerrainData>("Terrain Assets/Desert/Terrain");
                noiseData = Resources.Load<NoiseData>("Terrain Assets/Desert/Noise");
                textureData = Resources.Load<TextureData>("Terrain Assets/Desert/Texture");
                prefabsData = Resources.Load<PrefabsData>("Terrain Assets/Desert/Prefabs");
                break;
        }
        
        OnTextureValuesUpdated();

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
    public readonly List<Vector3> stoneSpawnPoints;

    public MapData(float[,] heightMap, List<Vector3> treeSpawnPoints, List<Vector3> stoneSpawnPoints)
    {
        this.heightMap = heightMap;
        this.treeSpawnPoints = treeSpawnPoints;
        this.stoneSpawnPoints = stoneSpawnPoints;
    }
}