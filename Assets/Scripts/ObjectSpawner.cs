using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    public GameObject[] objectsToSpawn;
    public int noOfObjectsToSpawn;

    private GameObject newObj;
    private int instantiatedNoOfObjects;

    // Start is called before the first frame update
    void Start()
    {
        InstantiateNewObj();
    }

    // Update is called once per frame
    void Update()
    {
        if (newObj.GetComponent<ObjectPlacer>().isPlaced && instantiatedNoOfObjects < noOfObjectsToSpawn)
        {
            InstantiateNewObj();
        }
    }

    private void InstantiateNewObj()
    {
        newObj = Instantiate(objectsToSpawn[Random.Range(0, objectsToSpawn.Length)], new Vector3(0, 0, 0), Quaternion.identity);
        instantiatedNoOfObjects++;
        Debug.Log("instantiated no. of objects: " + instantiatedNoOfObjects);
    }
}
