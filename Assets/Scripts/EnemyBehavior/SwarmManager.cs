using UnityEngine;
#pragma warning disable CS0414
using System.Collections.Generic;
using System.Collections;

public class SwarmManager : MonoBehaviour
{
    public static SwarmManager Instance { get; private set; }

    [Header("Component Help")]
    [SerializeField, TextArea(3, 6)] private string inspectorHelp =
        "SwarmManager: coordinates crawler swarms, limits concurrent attackers, and applies separation.\n" +
        "Crawlers register/unregister via BaseCrawlerEnemy. Use maxAttackers to cap pressure on the player.";

    private Queue<BaseCrawlerEnemy> attackQueue = new();
    [SerializeField] private int maxAttackers = 3;

    // Expose the list in the Inspector as read-only
    [ReadOnly, SerializeField, Tooltip("Currently managed swarming enemies (debug only, do not edit).")]
    private List<BaseCrawlerEnemy> debugSwarmMembers = new();

    private readonly List<BaseCrawlerEnemy> swarmMembers = new();
    private Transform player;

    private int crawlerLayerMask;
    [SerializeField] private float minSeparation = 2f;

    private readonly Collider[] overlapBuffer = new Collider[32]; // Adjust size as needed

    // --- Event for swarm count changes ---
    public event System.Action<int> OnActiveCrawlersChanged;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
#pragma warning restore CS0414
        Instance = this;

        // Use PlayerPresenceManager if available
        if (PlayerPresenceManager.IsPlayerPresent)
            player = PlayerPresenceManager.PlayerTransform;
        else
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    private void Start()
    {
        crawlerLayerMask = 1 << LayerMask.NameToLayer("Crawler");
        StartCoroutine(SeparationRoutine());
    }

    // Unified registration method
    public void AddToSwarm(BaseCrawlerEnemy crawler)
    {
        if (!swarmMembers.Contains(crawler))
        {
            swarmMembers.Add(crawler);
            debugSwarmMembers.Clear();
            debugSwarmMembers.AddRange(swarmMembers);
            OnActiveCrawlersChanged?.Invoke(swarmMembers.Count);
            EnemyBehaviorDebugLogBools.Log(nameof(SwarmManager), $"SwarmManager: Added {crawler.gameObject.name} to swarm.", crawler);

            if (!attackQueue.Contains(crawler))
                attackQueue.Enqueue(crawler);
        }
    }

    // Unified unregistration method
    public void RemoveFromSwarm(BaseCrawlerEnemy crawler)
    {
        if (swarmMembers.Contains(crawler))
        {
            swarmMembers.Remove(crawler);
            debugSwarmMembers.Clear();
            debugSwarmMembers.AddRange(swarmMembers);
            OnActiveCrawlersChanged?.Invoke(swarmMembers.Count);
            EnemyBehaviorDebugLogBools.Log(nameof(SwarmManager), $"SwarmManager: Removed {crawler.gameObject.name} from swarm.", crawler);

            // Remove from attack queue without LINQ allocation
            RemoveFromAttackQueue(crawler);
        }
    }
    
    private void RemoveFromAttackQueue(BaseCrawlerEnemy crawler)
    {
        int count = attackQueue.Count;
        for (int i = 0; i < count; i++)
        {
            var item = attackQueue.Dequeue();
            if (item != crawler)
                attackQueue.Enqueue(item);
        }
    }

    // Reusable list to avoid allocations in UpdateSwarm
    private readonly List<BaseCrawlerEnemy> sortedSwarmBuffer = new List<BaseCrawlerEnemy>();
    
    public void UpdateSwarm()
    {
        if (player == null) return;
        
        // Sort by distance without LINQ allocation
        sortedSwarmBuffer.Clear();
        sortedSwarmBuffer.AddRange(swarmMembers);
        sortedSwarmBuffer.Sort((a, b) => 
            Vector3.Distance(a.transform.position, player.position)
            .CompareTo(Vector3.Distance(b.transform.position, player.position)));
        
        for (int i = 0; i < sortedSwarmBuffer.Count; i++)
        {
            if (i < maxAttackers)
            {
                // Allow these to attack if not already attacking
                if (sortedSwarmBuffer[i].enemyAI.State == CrawlerEnemyState.Swarm)
                    sortedSwarmBuffer[i].TryFireTriggerByName("InAttackRange");
            }
            // else: do nothing, just keep encircling
        }
    }

    // Reusable list to avoid allocations in GetAttackers
    private readonly List<BaseCrawlerEnemy> attackersBuffer = new List<BaseCrawlerEnemy>();
    
    public IReadOnlyList<BaseCrawlerEnemy> GetAttackers()
    {
        // Return the first maxAttackers without LINQ allocation
        attackersBuffer.Clear();
        int count = 0;
        foreach (var crawler in attackQueue)
        {
            if (count >= maxAttackers) break;
            attackersBuffer.Add(crawler);
            count++;
        }
        return attackersBuffer;
    }

    // Separation routine to avoid overlapping crawlers
    private IEnumerator SeparationRoutine()
    {
        while (true)
        {
            foreach (var crawler in swarmMembers)
            {
                if (crawler == null) continue; // Skip destroyed crawlers

                int hitCount = Physics.OverlapSphereNonAlloc(
                    crawler.transform.position,
                    minSeparation,
                    overlapBuffer,
                    crawlerLayerMask
                );

                Vector3 separation = Vector3.zero;
                int count = 0;
                for (int i = 0; i < hitCount; i++)
                {
                    var other = overlapBuffer[i].GetComponent<BaseCrawlerEnemy>();
                    if (other != null && other != crawler)
                    {
                        float dist = Vector3.Distance(crawler.transform.position, other.transform.position);
                        separation += (crawler.transform.position - other.transform.position).normalized * (minSeparation - dist);
                        count++;
                    }
                }
                if (count > 0)
                {
                    separation /= count;
                    crawler.agent.Move(separation * 0.5f);
                }
            }
            yield return WaitForSecondsCache.Get(0.1f);
        }
    }

    public int GetSwarmIndex(BaseCrawlerEnemy crawler)
    {
        return swarmMembers.IndexOf(crawler);
    }

    public int GetSwarmCount()
    {
        return swarmMembers.Count;
    }

    public List<BaseCrawlerEnemy> GetActiveCrawlers() => swarmMembers;

    public void RotateAttackers()
    {
        if (attackQueue.Count > 0)
        {
            var crawler = attackQueue.Dequeue();
            attackQueue.Enqueue(crawler);
        }
    }
}