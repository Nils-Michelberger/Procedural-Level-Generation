using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPlacer : MonoBehaviour
{
    RaycastHit hit;
    public bool isPlaced = false;
    private int mapChunkSize;

    private static MapGenerator mapGenerator;

    // Start is called before the first frame update
    void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();
        mapChunkSize = mapGenerator.mapChunkSize;
    }

    // Update is called once per frame
    void Update()
    {
        if (!isPlaced)
        {
            transform.position = new Vector3(Random.Range(-mapChunkSize, mapChunkSize), 100, Random.Range(-mapChunkSize, mapChunkSize));
            
            if (Physics.Raycast(transform.position, Vector3.down, out hit))
            {
                Debug.Log("Ray hit something");
                Debug.DrawLine(transform.position, Vector3.down * hit.distance, Color.red);

                Debug.Log(hit.point.y);
                
                // if higher than water level (TODO: water value not final, adjust me)
                if (hit.point.y > 0.5f)
                {
                    isPlaced = true;
                    transform.position = hit.point;
                }
            }
        }
    }
}