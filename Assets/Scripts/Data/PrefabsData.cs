using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class PrefabsData : UpdatableData
{
    [Header("General")]
    public bool collisionFreeSpawning;
    [Range(0, 50f)]
    public float minSpawnDistance;
    
    [Header("Trees")]
    public GameObject[] treePrefabs;
    [Range(0,0.1f)]
    public float treeDensity = 0.01f;
    [Range(0,1.5f)]
    public float treeMinSpawnHeight = 0.3f;
    [Range(0,1.5f)]
    public float treeMaxSpawnHeight = 0.5f;
    [Range(0, 5f)]
    public float treeSpawnHeightMultiplier = 1.5f;
    
    [Header("Stones")]
    public GameObject[] stonePrefabs;
    [Range(0,0.1f)]
    public float stoneDensity = 0.01f;
    [Range(0,1.5f)]
    public float stoneMinSpawnHeight = 0.3f;
    [Range(0,1.5f)]
    public float stoneMaxSpawnHeight = 0.5f;
    [Range(0, 5f)]
    public float stoneSpawnHeightMultiplier = 1.5f;
    
}
