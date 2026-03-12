// CrawlerPocket.cs
// Purpose: Defines spawn points (pockets) for crawler adds used by bosses or encounters.
// Works with: BossRoombaController, ScenePoolManager

using UnityEngine;
using System.Collections.Generic;
using System.ComponentModel;

public class CrawlerPocket : MonoBehaviour
{
    [Header("Component Help")]
    [SerializeField, TextArea(4, 8)] private string inspectorHelp =
        "CrawlerPocket: spawns crawlers when the Player enters the trigger zone.\n" +
        "Configure weighted crawler prefabs, spawnCount, and clustering radius/jitter.\n" +
        "Calls AmbushReady when most crawlers reach their cluster points.\n" +
        "Automatically ensures a trigger BoxCollider matches the configured triggerZone.\n" +
        "Enable 'Alarm Bot Only' to disable player-triggered spawning (pocket becomes an anchor for AlarmBot spawning only).";

    [Header("Mode")]
    [SerializeField, Tooltip("When enabled, the pocket will NOT spawn enemies when the player enters. It will still exist in the scene so AlarmBots can find it as the nearest pocket anchor.")]
    private bool alarmBotOnly = false;

    [Header("Crawler Types & Weights")]
    [Tooltip("List of crawler enemy prefabs and their relative spawn weights for random generation.")]
    [SerializeField] private List<CrawlerTypeWeight> crawlerTypeWeights;

    [Tooltip("Number of crawlers to spawn when the pocket is triggered.")]
    [SerializeField] private int spawnCount = 10;

    [Tooltip("Radius of the cluster where crawlers will initially group after emerging from the pocket.")]
    [SerializeField] private float clusterRadius = 4f;

    [Tooltip("Amount of random jitter applied to each crawler's cluster position for a more organic look.")]
    [SerializeField] private float clusterJitter = 0.7f;

    [Tooltip("Percentage of crawlers that must reach their cluster point before the ambush begins (0.1 to 1.0).")]
    [SerializeField, Range(0.1f, 1f)] private float readyPercentage = 0.8f;

    [Header("Pocket Volume")]
    [Tooltip("Dimensions of the pocket in world space. Used for visualization and logic.")]
    public Vector3 pocketSize = new Vector3(1, 1, 1);

    [Header("Trigger Zone")]
    [Tooltip("Dimensions of the trigger zone. The player must enter this area to activate the pocket.")]
    public Vector3 triggerZoneSize = new Vector3(8, 3, 8);

    [Tooltip("Offset of the trigger zone from the pocket's position.")]
    public Vector3 triggerZoneOffset = new Vector3(0, 0, 0);

    [ReadOnly, SerializeField, Tooltip("Inactive crawlers (data only, for debug). Do not edit in Inspector.")]
    public List<CrawlerSpawnInfo> inactiveCrawlers = new();

    [ReadOnly, SerializeField, Tooltip("Active enemies (GameObjects, for debug). Do not edit in Inspector.")]
    public List<IPocketSpawnable> activeEnemies = new();

#if UNITY_EDITOR
    [ReadOnly, SerializeField, Tooltip("Active enemy GameObjects (debug only).")]
    private List<GameObject> activeEnemyObjects = new();
#endif

    private int crawlersReady = 0;
    private Coroutine spawnCoroutine;
    private int activeSwarmCrawlers = 0;
    private bool swarmDisabledDueToLowCount = false;

    private void Start()
    {
        if (inactiveCrawlers.Count == 0)
        {
            var toSpawn = GenerateRandomSpawnList(spawnCount);

            var prefabCounts = new Dictionary<GameObject, int>();
            foreach (var prefab in toSpawn)
            {
                if (prefabCounts.ContainsKey(prefab))
                    prefabCounts[prefab]++;
                else
                    prefabCounts[prefab] = 1;
            }

            inactiveCrawlers.Clear();
            foreach (var kvp in prefabCounts)
            {
                inactiveCrawlers.Add(new CrawlerSpawnInfo { prefab = kvp.Key, count = kvp.Value });
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (alarmBotOnly)
            return;

        // Remove any null (destroyed) entries from the list
        activeEnemies.RemoveAll(c => c == null);
#if UNITY_EDITOR
        activeEnemyObjects.RemoveAll(c => c == null);
#endif
        if (other.CompareTag("Player") && activeEnemies.Count == 0)
        {
            ActivateCrawlers();
        }
    }

    public void NotifyCrawlerReady(BaseCrawlerEnemy crawler)
    {
        // Only count as ready if the crawler is truly clustered
        if (!crawler.IsClustered())
            return;

        if (!activeEnemies.Contains(crawler))
            activeEnemies.Add(crawler);

        crawlersReady++;

        if (activeEnemies.Count > 0)
        {
            float percentReady = (float)crawlersReady / activeEnemies.Count;
            if (percentReady >= readyPercentage)
            {
                foreach (var c in activeEnemies)
                {
                    if (c is BaseCrawlerEnemy crawlerEnemy)
                    {
                        crawlerEnemy.enemyAI.Fire(CrawlerEnemyTrigger.AmbushReady);
                    }
                }
                // Reset for next time
                crawlersReady = 0;
                activeEnemies.Clear();
            }
        }
    }

    private void ActivateCrawlers()
    {
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnCrawlersGradually());
    }

    private System.Collections.IEnumerator SpawnCrawlersGradually()
    {
        float angleStep = 2 * Mathf.PI / spawnCount;
        int spawned = 0;

        foreach (var info in inactiveCrawlers)
        {
            for (int i = 0; i < info.count; i++)
            {
                var enemyObj = Instantiate(info.prefab, transform.position, Quaternion.identity, transform);
                var pocketSpawnable = enemyObj.GetComponent<IPocketSpawnable>();
                if (pocketSpawnable != null)
                {
                    pocketSpawnable.Pocket = this;
                    activeEnemies.Add(pocketSpawnable);
#if UNITY_EDITOR
                    activeEnemyObjects.Add((pocketSpawnable as MonoBehaviour)?.gameObject);
#endif

                    // If it's a BaseCrawlerEnemy, set cluster target and originalPrefab
                    if (pocketSpawnable is BaseCrawlerEnemy crawler)
                    {
                        float angle = angleStep * spawned;
                        Vector3 baseOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * clusterRadius;
                        Vector3 jitter = new Vector3(
                            Random.Range(-clusterJitter, clusterJitter),
                            0,
                            Random.Range(-clusterJitter, clusterJitter)
                        );
                        Vector3 clusterPoint = transform.position + baseOffset + jitter;
                        crawler.ClusterTarget = clusterPoint;
                        crawler.originalPrefab = info.prefab.GetComponent<BaseCrawlerEnemy>();

                        // --- Start moving immediately ---
                        // If you want them to move to their cluster point:
                        crawler.enemyAI.Fire(CrawlerEnemyTrigger.LosePlayer); // or another trigger that causes movement
                        // If you want them to swarm the player immediately, use:
                        // crawler.enemyAI.Fire(CrawlerEnemyTrigger.AmbushReady);
                    }
                    // If it's a BombCarrierEnemy, call SetSpawnSource
                    else if (pocketSpawnable is BombCarrierEnemy bomb)
                    {
                        bomb.originalPrefab = info.prefab;
                        bomb.SetSpawnSource(false, null, this);
                    }
                }
                spawned++;
                yield return WaitForSecondsCache.Get(0.15f);
            }
        }
        inactiveCrawlers.Clear();
        spawnCoroutine = null;

        // Register all crawlers with SwarmManager (without LINQ allocation)
        var crawlers = new List<BaseCrawlerEnemy>();
        foreach (var enemy in activeEnemies)
        {
            if (enemy is BaseCrawlerEnemy crawler)
                crawlers.Add(crawler);
        }
        
        foreach (var crawler in crawlers)
        {
            crawler.RegisterWithSwarmManager();
        }

        // After all are registered, check the count ONCE
        int aliveCount = 0;
        foreach (var c in crawlers)
        {
            if (c != null && c.gameObject.activeInHierarchy)
                aliveCount++;
        }
        
        if (aliveCount <= 3)
        {
            foreach (var crawler in crawlers)
            {
                crawler.enableSwarmBehavior = false;
            }
        }

        // After registering all crawlers:
        activeSwarmCrawlers = crawlers.Count;
        swarmDisabledDueToLowCount = false;

        foreach (var crawler in crawlers)
        {
            crawler.OnCrawlerDeathOrReturn += HandleCrawlerDeathOrReturn;
        }
    }

    private List<GameObject> GenerateRandomSpawnList(int count)
    {
        List<GameObject> result = new();
        if (crawlerTypeWeights == null || crawlerTypeWeights.Count == 0)
            return result;

        float totalWeight = 0f;
        foreach (var type in crawlerTypeWeights)
            totalWeight += type.weight;

        for (int i = 0; i < count; i++)
        {
            float rand = Random.Range(0, totalWeight);
            float cumulative = 0f;
            foreach (var type in crawlerTypeWeights)
            {
                cumulative += type.weight;
                if (rand <= cumulative)
                {
                    result.Add(type.prefab);
                    break;
                }
            }
        }
        return result;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Draw pocket position and volume
        Gizmos.color = new Color(1f, 0f, 1f, 0.2f);
        Gizmos.DrawCube(transform.position, pocketSize);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position, pocketSize);

        // Draw trigger zone
        Vector3 triggerCenter = transform.position + triggerZoneOffset;
        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        Gizmos.DrawCube(triggerCenter, triggerZoneSize);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(triggerCenter, triggerZoneSize);
    }
#endif

    private void OnValidate()
    {
        // Ensure a BoxCollider exists and matches trigger zone settings
        // (unless this pocket is in AlarmBot-only mode).
        var box = GetComponent<BoxCollider>();

        if (alarmBotOnly)
        {
            // Disable the trigger collider so designers don't accidentally rely on player-trigger spawning.
            if (box != null)
            {
                box.enabled = false;
            }
            return;
        }

        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
        }

        box.enabled = true;
        box.isTrigger = true;
        box.size = triggerZoneSize;
        box.center = triggerZoneOffset;
    }

    public void ReturnEnemyToInactive(IPocketSpawnable enemy)
    {
        // Remove from active lists BEFORE destroying
        if (activeEnemies.Contains(enemy))
        {
#if UNITY_EDITOR
            activeEnemyObjects.Remove((enemy as MonoBehaviour)?.gameObject);
#endif
            activeEnemies.Remove(enemy);
        }

        // Repopulate inactiveCrawlers with the correct prefab reference
        GameObject prefab = null;
        if (enemy is BaseCrawlerEnemy crawler)
        {
            crawler.OnCrawlerDeathOrReturn -= HandleCrawlerDeathOrReturn;
            prefab = crawler.originalPrefab != null ? crawler.originalPrefab.gameObject : null;
        }
        else if (enemy is BombCarrierEnemy bomb)
            prefab = bomb.originalPrefab != null ? bomb.originalPrefab : null;

        if (prefab != null)
        {
            // Find existing entry without LINQ
            CrawlerSpawnInfo existing = null;
            foreach (var info in inactiveCrawlers)
            {
                if (info.prefab == prefab)
                {
                    existing = info;
                    break;
                }
            }
            
            if (existing != null)
                existing.count++;
            else
                inactiveCrawlers.Add(new CrawlerSpawnInfo { prefab = prefab, count = 1 });
        }

        // Clean up missing entries
        inactiveCrawlers.RemoveAll(info => info.prefab == null);
#if UNITY_EDITOR
        activeEnemyObjects.RemoveAll(obj => obj == null);
#endif

        // Now destroy the object
        enemy.OnReturnedToPocket();
    }

    public void RemoveFromActiveLists(IPocketSpawnable enemy)
    {
        if (activeEnemies.Contains(enemy))
            activeEnemies.Remove(enemy);
#if UNITY_EDITOR
        var go = (enemy as MonoBehaviour)?.gameObject;
        if (go != null && activeEnemyObjects.Contains(go))
            activeEnemyObjects.Remove(go);
#endif
    }

    public float ClusterRadius => clusterRadius;

    private void HandleCrawlerDeathOrReturn(BaseCrawlerEnemy deadCrawler)
    {
        if (swarmDisabledDueToLowCount)
            return;

        activeSwarmCrawlers--;

        if (activeSwarmCrawlers <= 3)
        {
            // Disable swarm behavior for all remaining crawlers in this pocket
            foreach (var enemy in activeEnemies)
            {
                if (enemy is BaseCrawlerEnemy crawler && crawler != null)
                    crawler.enableSwarmBehavior = false;
            }
            swarmDisabledDueToLowCount = true;
        }
    }
}

[System.Serializable]
public class CrawlerTypeWeight
{
    [Tooltip("Enemy prefab to spawn (BaseCrawlerEnemy or BombCarrierEnemy).")]
    public GameObject prefab;
    [Tooltip("Relative spawn weight (higher = more likely).")]
    public float weight = 1f;
}

[System.Serializable]
public class CrawlerSpawnInfo
{
    [Tooltip("Enemy prefab to spawn (BaseCrawlerEnemy or BombCarrierEnemy).")]
    public GameObject prefab;
    [Tooltip("How many of this type to spawn.")]
    public int count;
}