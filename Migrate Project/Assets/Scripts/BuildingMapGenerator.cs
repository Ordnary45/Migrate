using UnityEngine;
using System.Collections.Generic;

public class BuildingMapGenerator : MonoBehaviour
{
    [Header("Building Group Settings")]
    [SerializeField] private GameObject buildingGroupPrefab; // The prefab containing ~80 buildings

    [Header("Placement Settings")]
    [SerializeField] private float xMin = 140f;
    [SerializeField] private float xMax = 1850f;
    [SerializeField] private float zSpacingMin = 310f;
    [SerializeField] private float zSpacingMax = 360f;
    [SerializeField] private int numberOfGroups = 5; // Number of building groups to place
    [SerializeField] private float startingZ = 150f; // Starting Z position

    [Header("Debug")]
    [SerializeField] private bool generateOnStart = true;

    private List<GameObject> spawnedGroups = new List<GameObject>();
    private int currentSeed;

    void Start()
    {
        if (generateOnStart)
        {
            GenerateMap();
        }
    }

    public void GenerateMap()
    {
        // Clear existing groups
        ClearMap();

        // Randomize seed
        currentSeed = Random.Range(0, 99999);
        Random.InitState(currentSeed);
        Debug.Log($"Generating city with seed: {currentSeed}");

        float currentZ = 150;

        float xPosition = Random.Range(xMin, xMax);

        // Create position vector at startingZ
        Vector3 position = new Vector3(xPosition, 0, startingZ);

        // Instantiate the building group prefab
        GameObject buildingGroup = Instantiate(buildingGroupPrefab, position, Quaternion.identity, transform);
        spawnedGroups.Add(buildingGroup);

        for (int i = 0; i < numberOfGroups; i++)
        {
            // Calculate Z position with random spacing from first group
            float zSpacing = Random.Range(zSpacingMin, zSpacingMax);
            currentZ += zSpacing;

            // Calculate random X position within range
            xPosition = Random.Range(xMin, xMax);

            // Create position vector
            position = new Vector3(xPosition, 0, currentZ);

            // Instantiate the building group prefab
            buildingGroup = Instantiate(buildingGroupPrefab, position, Quaternion.identity, transform);

            // Add to list for cleanup
            spawnedGroups.Add(buildingGroup);
        }
    }

    public void ClearMap()
    {
        foreach (GameObject group in spawnedGroups)
        {
            if (group != null)
                DestroyImmediate(group);
        }
        spawnedGroups.Clear();
    }

    // Public method to get all spawned building groups
    public List<GameObject> GetSpawnedGroups()
    {
        return spawnedGroups;
    }

    // Public method to get the number of spawned groups
    public int GetGroupCount()
    {
        return spawnedGroups.Count;
    }
}