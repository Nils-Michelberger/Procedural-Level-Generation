using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Object = System.Object;

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
        Sea,
        Jungle,
        Custom
    }

    public DrawMode drawMode;

    public Biome biome;

    public TerrainData terrainData;
    public NoiseData noiseData;
    public TextureData textureData;
    public PrefabsData prefabsData;
    public Material terrainMaterial;
    public Material seaMaterial;

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

    public Transform mesh;

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

        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, terrainData.meshHeightMultiplier,
                terrainData.meshHeightCurve, editorPreviewLevelOfDetail, terrainData.useFlatShading));

            // clear all prefabs
            if (mesh != null)
            {
                for (int i = mesh.childCount; i > 0; i--)
                {
                    DestroyImmediate(mesh.transform.GetChild(0).gameObject);
                }
            }

            if (prefabsData.spawnObjects)
            {
                // spawn new prefabs1
                SpawnPrefabs(mapData.prefab1SpawnPoints, prefabsData.prefab1Types);

                //spawn new prefabs2
                SpawnPrefabs(mapData.prefab2SpawnPoints, prefabsData.prefab2Types);

                //spawn new prefabs3
                SpawnPrefabs(mapData.prefab3SpawnPoints, prefabsData.prefab3Types);

                //spawn new prefabs4
                SpawnPrefabs(mapData.prefab4SpawnPoints, prefabsData.prefab4Types);
            }
        }
        else if (drawMode == DrawMode.FalloffMap)
        {
            display.DrawTexture(
                TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(mapChunkSize)));
        }
    }

    private void SpawnPrefabs(List<Vector3> spawnPoints, GameObject[] prefabs)
    {
        if (prefabs.Length != 0)
        {
            System.Random random = new System.Random();

            foreach (Vector3 spawnPoint in spawnPoints)
            {
                Instantiate(prefabs[(int) (random.NextDouble() * prefabs.Length)], spawnPoint,
                    Quaternion.Euler(Quaternion.identity.x, (int) (random.NextDouble() * 360f), Quaternion.identity.z),
                    mesh.transform);
            }
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
                        heightMap[x, y] = heightMap[x, y] - (falloffMap[x, y] * 1.5f);
                    }
                }
            }
        }

        System.Random random = new System.Random();
        List<Vector3> prefab1SpawnPoints = new List<Vector3>();
        List<Vector3> prefab2SpawnPoints = new List<Vector3>();
        List<Vector3> prefab3SpawnPoints = new List<Vector3>();
        List<Vector3> prefab4SpawnPoints = new List<Vector3>();
        List<Vector3> spawnPoints = new List<Vector3>();
        
        if (prefabsData.spawnObjects)
        {
            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    // set spawn points for prefab1
                    if (heightMap[x, y] >= prefabsData.prefab1MinSpawnHeight &&
                        heightMap[x, y] <= prefabsData.prefab1MaxSpawnHeight &&
                        random.NextDouble() <= prefabsData.prefab1Density)
                    {
                        Vector3 prefab1SpawnPoint = GetSpawnPoint(center, heightMap, x, y,
                            prefabsData.prefab1SpawnHeightMultiplier);

                        if (!prefabsData.collisionFreeSpawning || CheckCollision(spawnPoints, prefab1SpawnPoint))
                        {
                            prefab1SpawnPoints.Add(prefab1SpawnPoint);
                            spawnPoints.Add(prefab1SpawnPoint);
                        }
                    }

                    // set spawn points for prefab2
                    if (heightMap[x, y] >= prefabsData.prefab2MinSpawnHeight &&
                        heightMap[x, y] <= prefabsData.prefab2MaxSpawnHeight &&
                        random.NextDouble() <= prefabsData.prefab2Density)
                    {
                        Vector3 prefab2SpawnPoint = GetSpawnPoint(center, heightMap, x, y,
                            prefabsData.prefab2SpawnHeightMultiplier);

                        if (!prefabsData.collisionFreeSpawning || CheckCollision(spawnPoints, prefab2SpawnPoint))
                        {
                            prefab2SpawnPoints.Add(prefab2SpawnPoint);
                            spawnPoints.Add(prefab2SpawnPoint);
                        }
                    }

                    // set spawn points for prefab3
                    if (heightMap[x, y] >= prefabsData.prefab3MinSpawnHeight &&
                        heightMap[x, y] <= prefabsData.prefab3MaxSpawnHeight &&
                        random.NextDouble() <= prefabsData.prefab3Density)
                    {
                        Vector3 prefab3SpawnPoint = GetSpawnPoint(center, heightMap, x, y,
                            prefabsData.prefab3SpawnHeightMultiplier);

                        if (!prefabsData.collisionFreeSpawning || CheckCollision(spawnPoints, prefab3SpawnPoint))
                        {
                            prefab3SpawnPoints.Add(prefab3SpawnPoint);
                            spawnPoints.Add(prefab3SpawnPoint);
                        }
                    }

                    // set spawn points for prefab4
                    if (heightMap[x, y] >= prefabsData.prefab4MinSpawnHeight &&
                        heightMap[x, y] <= prefabsData.prefab4MaxSpawnHeight &&
                        random.NextDouble() <= prefabsData.prefab4Density)
                    {
                        Vector3 prefab4SpawnPoint = GetSpawnPoint(center, heightMap, x, y,
                            prefabsData.prefab4SpawnHeightMultiplier);

                        if (!prefabsData.collisionFreeSpawning || CheckCollision(spawnPoints, prefab4SpawnPoint))
                        {
                            prefab4SpawnPoints.Add(prefab4SpawnPoint);
                            spawnPoints.Add(prefab4SpawnPoint);
                        }
                    }
                }
            }
        }

        return new MapData(heightMap, prefab1SpawnPoints, prefab2SpawnPoints, prefab3SpawnPoints, prefab4SpawnPoints);
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
                mesh.GetComponent<MeshRenderer>().material = terrainMaterial;
                break;
            case Biome.LowPoly:
                terrainData = Resources.Load<TerrainData>("Terrain Assets/Low Poly/Terrain");
                noiseData = Resources.Load<NoiseData>("Terrain Assets/Low Poly/Noise");
                textureData = Resources.Load<TextureData>("Terrain Assets/Low Poly/Texture");
                prefabsData = Resources.Load<PrefabsData>("Terrain Assets/Low Poly/Prefabs");
                mesh.GetComponent<MeshRenderer>().material = terrainMaterial;
                break;
            case Biome.Desert:
                terrainData = Resources.Load<TerrainData>("Terrain Assets/Desert/Terrain");
                noiseData = Resources.Load<NoiseData>("Terrain Assets/Desert/Noise");
                textureData = Resources.Load<TextureData>("Terrain Assets/Desert/Texture");
                prefabsData = Resources.Load<PrefabsData>("Terrain Assets/Desert/Prefabs");
                mesh.GetComponent<MeshRenderer>().material = terrainMaterial;
                break;
            case Biome.Sea:
                terrainData = Resources.Load<TerrainData>("Terrain Assets/Sea/Terrain");
                noiseData = Resources.Load<NoiseData>("Terrain Assets/Sea/Noise");
                textureData = Resources.Load<TextureData>("Terrain Assets/Sea/Texture");
                prefabsData = Resources.Load<PrefabsData>("Terrain Assets/Sea/Prefabs");
                mesh.GetComponent<MeshRenderer>().material = seaMaterial;
                break;
            case Biome.Jungle:
                terrainData = Resources.Load<TerrainData>("Terrain Assets/Jungle/Terrain");
                noiseData = Resources.Load<NoiseData>("Terrain Assets/Jungle/Noise");
                textureData = Resources.Load<TextureData>("Terrain Assets/Jungle/Texture");
                prefabsData = Resources.Load<PrefabsData>("Terrain Assets/Jungle/Prefabs");
                mesh.GetComponent<MeshRenderer>().material = terrainMaterial;
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
    public readonly List<Vector3> prefab1SpawnPoints;
    public readonly List<Vector3> prefab2SpawnPoints;
    public readonly List<Vector3> prefab3SpawnPoints;
    public readonly List<Vector3> prefab4SpawnPoints;

    public MapData(float[,] heightMap, List<Vector3> prefab1SpawnPoints, List<Vector3> prefab2SpawnPoints,
        List<Vector3> prefab3SpawnPoints, List<Vector3> prefab4SpawnPoints)
    {
        this.heightMap = heightMap;
        this.prefab1SpawnPoints = prefab1SpawnPoints;
        this.prefab2SpawnPoints = prefab2SpawnPoints;
        this.prefab3SpawnPoints = prefab3SpawnPoints;
        this.prefab4SpawnPoints = prefab4SpawnPoints;
    }
}