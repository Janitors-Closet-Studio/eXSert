// DroneSwarmManager.cs
// Purpose: Manages drone swarm creation, pooling, and high-level commands.
// Works with: DroneEnemy, DroneCluster, CrowdController, FlowFieldService.

using System.Collections.Generic;
using UnityEngine;

public class DroneSwarmManager : MonoBehaviour
{
    [Header("Component Help")]
    [SerializeField, TextArea(3, 6)] private string inspectorHelp =
        "DroneSwarmManager: spawns clusters of DroneEnemy around given spawn points.\n" +
        "Assign dronePrefab, clusterSpawnPoints, and tune dronesPerCluster/spawnRadius.\n" +
        "Integrates with CrowdController and FlowFieldService for movement guidance.";

    [Header("Spawning Control")]
    [Tooltip("Disable to prevent this manager from spawning any drone clusters or drones.")]
    [SerializeField] private bool spawningEnabled = true;

    [Header("Drone Swarm Settings")]
    public GameObject dronePrefab;
    public int dronesPerCluster = 4;
    public float spawnRadius = 10f;
    public List<Transform> clusterSpawnPoints;

    private List<DroneCluster> clusters = new List<DroneCluster>();

    private void Start()
    {
        if (!spawningEnabled)
            return;

        SpawnClusters();
    }

    public void SpawnClusters()
    {
        if (!spawningEnabled)
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(DroneSwarmManager), "[DroneSwarmManager] Spawning is disabled. No clusters will be created.");
#endif
            return;
        }

        if (clusterSpawnPoints == null || clusterSpawnPoints.Count == 0)
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning(nameof(DroneSwarmManager), "[DroneSwarmManager] No cluster spawn points assigned.");
#endif
            return;
        }

        for (int c = 0; c < clusterSpawnPoints.Count; c++)
        {
            var clusterGO = new GameObject($"DroneCluster_{c + 1}");
            var cluster = clusterGO.AddComponent<DroneCluster>();
            clusters.Add(cluster);

            for (int i = 0; i < dronesPerCluster; i++)
            {
                // Randomize only X and Z, keep Y at spawn point
                Vector2 circle = Random.insideUnitCircle * spawnRadius;
                Vector3 spawnPos = clusterSpawnPoints[c].position + new Vector3(circle.x, 0, circle.y);

                // Optionally, raise spawnPos.y a bit to avoid ground clipping
                spawnPos.y += 1.0f;

                // Snap to NavMesh - try progressively larger radii
                UnityEngine.AI.NavMeshHit hit;
                bool foundNavMesh = false;
                float[] sampleRadii = { 5f, 10f, 20f, 50f }; // Try increasingly larger radii
                
                foreach (float radius in sampleRadii)
                {
                    if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out hit, radius, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        spawnPos = hit.position;
                        foundNavMesh = true;
                        break;
                    }
                }
                
                if (!foundNavMesh)
                {
                    // Last resort: try sampling from the cluster spawn point itself
                    if (UnityEngine.AI.NavMesh.SamplePosition(clusterSpawnPoints[c].position, out hit, 50f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        // Spawn at the NavMesh point with a small random offset
                        Vector2 smallOffset = Random.insideUnitCircle * 2f;
                        spawnPos = hit.position + new Vector3(smallOffset.x, 0, smallOffset.y);
                        
                        // Re-sample to make sure the offset position is also on NavMesh
                        if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            spawnPos = hit.position;
                            foundNavMesh = true;
                        }
                        else
                        {
                            spawnPos = hit.position; // Use the original sampled position
                            foundNavMesh = true;
                        }
                    }
                }

                if (!foundNavMesh)
                {
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.LogWarning(nameof(DroneSwarmManager), $"[DroneSwarmManager] No NavMesh found near {spawnPos} even with extended search. Drone may not function correctly.");
#endif
                }

                var droneGO = Instantiate(dronePrefab, spawnPos, Quaternion.identity, clusterGO.transform);
                var drone = droneGO.GetComponent<DroneEnemy>();
                if (drone != null)
                {
                    drone.Cluster = cluster;
                    cluster.drones.Add(drone);
                }
                else
                {
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.LogWarning(nameof(DroneSwarmManager), "[DroneSwarmManager] Spawned prefab does not contain a DroneEnemy component.");
#endif
                }
            }
        }
    }
}