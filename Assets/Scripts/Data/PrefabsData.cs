using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class PrefabsData : UpdatableData
{
    [Header("Trees")]
    public GameObject[] treePrefabs;
    [Range(0,0.1f)]
    public float treeDensity = 0.01f;
    [Range(0,1f)]
    public float treeMaxSpawnHeight = 0.5f;
    
    [Header("Stones")]
    public GameObject[] stonePrefabs;
    [Range(0,0.1f)]
    public float stoneDensity = 0.01f;
    [Range(0,1f)]
    public float stoneMaxSpawnHeight = 0.5f;
    
}
