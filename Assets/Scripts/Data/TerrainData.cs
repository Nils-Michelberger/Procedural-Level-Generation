using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class TerrainData : UpdatableData
{
    // scale the whole map depending on player scale
    // TODO: make accessible in editor
    public float uniformScale = 2f;
    
    public bool useFlatShading;
    
    public bool useFalloff;

    public float meshHeightMultiplier;
    public AnimationCurve meshHeightCurve;
    
    
}
