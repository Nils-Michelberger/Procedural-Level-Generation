using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class PrefabsData : UpdatableData
{
    [Header("General")]
    public bool collisionFreeSpawning;
    [Range(0, 200f)]
    public float minSpawnDistance;
    
    [Header("Prefab1")]
    public GameObject[] prefab1Types;
    [Range(0,0.2f)]
    public float prefab1Density = 0.01f;
    [Range(0,1.5f)]
    public float prefab1MinSpawnHeight = 0.3f;
    [Range(0,1.5f)]
    public float prefab1MaxSpawnHeight = 0.5f;
    [Range(0, 5f)]
    public float prefab1SpawnHeightMultiplier = 1.5f;
    
    [Header("Prefab2")]
    public GameObject[] prefab2Types;
    [Range(0,0.2f)]
    public float prefab2Density = 0.01f;
    [Range(0,1.5f)]
    public float prefab2MinSpawnHeight = 0.3f;
    [Range(0,1.5f)]
    public float prefab2MaxSpawnHeight = 0.5f;
    [Range(0, 5f)]
    public float prefab2SpawnHeightMultiplier = 1.5f;
    
    [Header("Prefab3")]
    public GameObject[] prefab3Types;
    [Range(0,0.2f)]
    public float prefab3Density = 0.01f;
    [Range(0,1.5f)]
    public float prefab3MinSpawnHeight = 0.3f;
    [Range(0,1.5f)]
    public float prefab3MaxSpawnHeight = 0.5f;
    [Range(0, 5f)]
    public float prefab3SpawnHeightMultiplier = 1.5f;
    
    [Header("Prefab4")]
    public GameObject[] prefab4Types;
    [Range(0,0.2f)]
    public float prefab4Density = 0.01f;
    [Range(0,1.5f)]
    public float prefab4MinSpawnHeight = 0.3f;
    [Range(0,1.5f)]
    public float prefab4MaxSpawnHeight = 0.5f;
    [Range(0, 5f)]
    public float prefab4SpawnHeightMultiplier = 1.5f;
    
}
