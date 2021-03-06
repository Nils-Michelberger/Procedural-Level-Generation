using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public class EndlessTerrain : MonoBehaviour
{
    private const float viewerMoveThresholdForChunkUpdate = 25f;

    private const float sqrViewerMoveThresholdForChunkUpdate =
        viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

    private const float colliderGenerationDistanceThreshold = 5;

    public int colliderLODIndex;
    public LODInfo[] detailLevels;
    public static float maxViewDist;

    public Transform viewer;
    private Material mapMaterial;

    public static Vector2 viewerPositon;
    private Vector2 viewerPositionOld;
    private static MapGenerator mapGenerator;

    private int chunkSize;

    // no. of chunks we instantiate around the viewer
    private int chunksVisibleInViewDist;

    private Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    private static List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();

    void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();

        mapMaterial = mapGenerator.terrainMaterial;

        if (mapGenerator.biome.Equals(MapGenerator.Biome.Sea))
        {
            mapMaterial = mapGenerator.seaMaterial;
        }

        maxViewDist = detailLevels[detailLevels.Length - 1].visibleDistThreshold;
        chunkSize = mapGenerator.mapChunkSize - 1; // 241 - 1 = 240
        chunksVisibleInViewDist = Mathf.RoundToInt(maxViewDist / chunkSize);

        UpdateVisibleChunks();
    }

    void Update()
    {
        viewerPositon = new Vector2(viewer.position.x, viewer.position.z) / mapGenerator.terrainData.uniformScale;

        if (viewerPositon != viewerPositionOld)
        {
            foreach (TerrainChunk chunk in visibleTerrainChunks)
            {
                chunk.UpdateCollisionMesh();
            }
        }

        if ((viewerPositionOld - viewerPositon).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate)
        {
            viewerPositionOld = viewerPositon;
            UpdateVisibleChunks();
        }
    }

    void UpdateVisibleChunks()
    {
        HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();

        for (int i = visibleTerrainChunks.Count - 1; i >= 0; i--)
        {
            alreadyUpdatedChunkCoords.Add(visibleTerrainChunks[i].coord);
            visibleTerrainChunks[i].UpdateTerrainChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(viewerPositon.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPositon.y / chunkSize);

        for (int yOffset = 0 - chunksVisibleInViewDist; yOffset <= chunksVisibleInViewDist; yOffset++)
        {
            for (int xOffset = 0 - chunksVisibleInViewDist; xOffset <= chunksVisibleInViewDist; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord))
                {
                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                    {
                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                    }
                    else
                    {
                        terrainChunkDictionary.Add(viewedChunkCoord,
                            new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, colliderLODIndex, transform,
                                mapMaterial, mapGenerator.prefabsData.prefab1Types,
                                mapGenerator.prefabsData.prefab2Types, mapGenerator.prefabsData.prefab3Types,
                                mapGenerator.prefabsData.prefab4Types));
                    }
                }
            }
        }
    }

    public class TerrainChunk
    {
        public Vector2 coord;

        private GameObject meshObject;
        private Vector2 position;
        private Bounds bounds;

        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private MeshCollider meshCollider;

        private LODInfo[] detailLevels;
        private LODMesh[] lodMeshes;
        private int colliderLODIndex;

        private MapData mapData;
        private bool mapDataReceived;
        private int previousLODIndex = -1;
        private bool hasSetCollider;

        private GameObject[] prefabs1;
        private GameObject[] prefabs2;
        private GameObject[] prefabs3;
        private GameObject[] prefabs4;

        private bool prefabsSpawned;

        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, int colliderLODIndex, Transform parent,
            Material material, GameObject[] prefabs1, GameObject[] prefabs2, GameObject[] prefabs3,
            GameObject[] prefabs4)
        {
            this.coord = coord;
            this.detailLevels = detailLevels;
            this.colliderLODIndex = colliderLODIndex;
            this.prefabs1 = prefabs1;
            this.prefabs2 = prefabs2;
            this.prefabs3 = prefabs3;
            this.prefabs4 = prefabs4;

            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshRenderer.material = material;

            meshObject.transform.position = positionV3 * mapGenerator.terrainData.uniformScale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * mapGenerator.terrainData.uniformScale;
            SetVisible(false);

            lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++)
            {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod);
                lodMeshes[i].updateCallback += UpdateTerrainChunk;
                if (i == colliderLODIndex)
                {
                    lodMeshes[i].updateCallback += UpdateCollisionMesh;
                }
            }

            // start threading with requesting MapData
            mapGenerator.RequestMapData(position, OnMapDataReceived);
        }

        // gets called by thread
        private void OnMapDataReceived(MapData mapData)
        {
            this.mapData = mapData;
            mapDataReceived = true;

            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk()
        {
            if (mapDataReceived)
            {
                float viewerDistFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPositon));

                bool wasVisible = IsVisible();
                bool visible = viewerDistFromNearestEdge <= maxViewDist;


                if (visible)
                {
                    int lodIndex = 0;

                    for (int i = 0; i < detailLevels.Length - 1; i++)
                    {
                        if (viewerDistFromNearestEdge > detailLevels[i].visibleDistThreshold)
                        {
                            lodIndex = i + 1;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (lodIndex != previousLODIndex)
                    {
                        LODMesh lodMesh = lodMeshes[lodIndex];
                        if (lodMesh.hasMesh)
                        {
                            previousLODIndex = lodIndex;
                            meshFilter.mesh = lodMesh.mesh;
                        }
                        else if (!lodMesh.hasRequestedMesh)
                        {
                            lodMesh.RequestMesh(mapData);
                        }
                    }
                }

                if (wasVisible != visible)
                {
                    if (visible)
                    {
                        visibleTerrainChunks.Add(this);
                    }
                    else
                    {
                        visibleTerrainChunks.Remove(this);
                    }
                }

                SetVisible(visible);

                if (!prefabsSpawned)
                {
                    SpawnPrefabs(mapData.prefab1SpawnPoints, prefabs1);

                    SpawnPrefabs(mapData.prefab2SpawnPoints, prefabs2);

                    SpawnPrefabs(mapData.prefab3SpawnPoints, prefabs3);

                    SpawnPrefabs(mapData.prefab4SpawnPoints, prefabs4);

                    prefabsSpawned = true;
                }
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
                            Quaternion.Euler(Quaternion.identity.x, (int) (random.NextDouble() * 360f),
                                Quaternion.identity.z))
                        .transform.SetParent(meshObject.transform);
                } 
            }
        }

        public void UpdateCollisionMesh()
        {
            if (!hasSetCollider)
            {
                float sqrDistFromViewerToEdge = bounds.SqrDistance(viewerPositon);

                if (sqrDistFromViewerToEdge < detailLevels[colliderLODIndex].sqrVisibleDistThreshold)
                {
                    if (!lodMeshes[colliderLODIndex].hasRequestedMesh)
                    {
                        lodMeshes[colliderLODIndex].RequestMesh(mapData);
                    }
                }

                if (sqrDistFromViewerToEdge < colliderGenerationDistanceThreshold * colliderGenerationDistanceThreshold)
                {
                    if (lodMeshes[colliderLODIndex].hasMesh)
                    {
                        meshCollider.sharedMesh = lodMeshes[colliderLODIndex].mesh;
                        hasSetCollider = true;
                    }
                }
            }
        }

        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }
    }

    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        private int lod;

        public event System.Action updateCallback;

        public LODMesh(int lod)
        {
            this.lod = lod;
        }

        void OnMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;

            updateCallback();
        }

        public void RequestMesh(MapData mapData)
        {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(mapData, lod, OnMeshDataReceived);
        }
    }

    [System.Serializable]
    public struct LODInfo
    {
        [Range(0, MeshGenerator.numSupportedLODs - 1)]
        public int lod;

        public float visibleDistThreshold;

        public float sqrVisibleDistThreshold => visibleDistThreshold * visibleDistThreshold;
    }
}