using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class PrefabsData : UpdatableData
{
    [Header("Trees")]
    public GameObject[] treePrefabs;
    [Range(0,0.1f)]
    public float density;
    [Range(0,1f)]
    public float maxSpawnHeight = 0.5f;
    
    [Header("Stones")]
    public GameObject[] stonePrefabs;
}
