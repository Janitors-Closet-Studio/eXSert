// AlarmCarrierEnemy.cs
// Purpose: Enemy type that carries an alarm and can activate group behavior or spawn reinforcements.
// Works with: BossRoombaController, CrowdController, EnemyStateMachineConfig, EnemyBehaviorProfile, Zone
// Notes: Scene-scoped; designed to integrate with the crowd/pathfinding systems. Main logic below.

using UnityEngine;
using System.Collections;
using System.Linq;
using Behaviors;
using UnityEngine.AI;

public enum AlarmCarrierState
{
    Idle,
    Roaming,
    AlarmTriggered,
    Summoning,
    Flee,
    Death
}

[DisallowMultipleComponent]
internal sealed class AlarmCarrierDetectionTrigger : MonoBehaviour
{
    private AlarmCarrierEnemy owner;
    private SphereCollider sphere;

    internal void Initialize(AlarmCarrierEnemy alarmOwner, float radius)
    {
        owner = alarmOwner;
        if (sphere == null)
        {
            sphere = GetComponent<SphereCollider>();
            if (sphere == null)
                sphere = gameObject.AddComponent<SphereCollider>();
        }

        sphere.isTrigger = true;
        SetRadius(radius);

        // Ensure a Rigidbody exists for trigger detection to work
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (transform.parent != owner.transform)
        {
            transform.SetParent(owner.transform);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
    }

    internal void SetRadius(float radius)
    {
        if (sphere == null)
            sphere = GetComponent<SphereCollider>();

        if (sphere != null)
        {
            sphere.radius = Mathf.Max(0.1f, radius);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        owner?.HandleDetectionTriggerEnter(other);
    }
}

public enum AlarmCarrierTrigger
{
    SeePlayer,
    PlayerInRange,
    AlarmStart,
    AlarmEnd,
    Summon,
    Flee,
    Die,
    LosePlayer,
    IdleTimerElapsed
}

public class AlarmCarrierEnemy : BaseEnemy<AlarmCarrierState, AlarmCarrierTrigger>
{
    [Header("Alarm Settings")]
    [SerializeField, Tooltip("How long the alarm must go off before summoning reinforcements.")]
    private float alarmDuration = 3f;
    [SerializeField, Tooltip("Detection range for triggering the alarm.")]
    private float alarmRange = 10f;
    [SerializeField, Tooltip("How far from the pocket the alarm bot can roam while fleeing the player.")]
    private float keepNearPocketRadius = 8f;
    [SerializeField, Tooltip("How often the alarm bot updates its flee destination (seconds).")]
    private float fleeCheckInterval = 0.1f;
    [SerializeField, Tooltip("Only update destination if the new target is this far from the current destination.")]
    private float minMoveDistance = 2f;
    [SerializeField, Tooltip("Only update destination if the player is within this radius of the pocket.")]
    private float playerChaseRadius = 20f;

    [Header("Crawler Prefabs")]
    [SerializeField, Tooltip("Prefab for the base crawler enemy (can be root GameObject - component will be found on root or children).")]
    private GameObject baseCrawlerPrefab;
    [SerializeField, Tooltip("Prefab for the bomb carrier enemy (can be root GameObject - component will be found on root or children).")]
    private GameObject bombCrawlerPrefab;

    [Header("Alarm Debug")]
    [ReadOnly, SerializeField]
    private float currentDynamicSpawnInterval;

    private Coroutine spawnCoroutine;
    private Coroutine alarmCountdownCoroutine;
    private Coroutine alarmFleeCoroutine;
    private CrawlerPocket nearestPocket;

    // Track the number of active crawlers spawned by the alarm
    private int activeAlarmSpawnedCrawlers = 0;

    [Header("Detection Trigger")]
    [SerializeField, Tooltip("Optional override object that hosts the alarm detection trigger. If null, one is auto-created.")]
    private AlarmCarrierDetectionTrigger detectionTrigger;

    // Behaviors
    private IEnemyStateBehavior<AlarmCarrierState, AlarmCarrierTrigger> idleBehavior, relocateBehavior, deathBehavior;

    protected override void Awake()
    {
        base.Awake();

        // Safety: enforce minimums if Inspector values are zero or negative
        if (keepNearPocketRadius <= 0f) keepNearPocketRadius = 8f;
        if (fleeCheckInterval <= 0f) fleeCheckInterval = 0.1f;
        if (minMoveDistance <= 0f) minMoveDistance = 2f;
        if (playerChaseRadius <= 0f) playerChaseRadius = 20f;

        idleBehavior = new IdleBehavior<AlarmCarrierState, AlarmCarrierTrigger>();
        relocateBehavior = new RelocateBehavior<AlarmCarrierState, AlarmCarrierTrigger>();
        deathBehavior = new DeathBehavior<AlarmCarrierState, AlarmCarrierTrigger>();

        EnsureDetectionTrigger();

        // Assign currentZone if not set
        if (currentZone == null)
        {
            currentZone = FindNearestZone();
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(AlarmCarrierEnemy), $"{gameObject.name} assigned to zone: {currentZone?.gameObject.name}");
#endif
        }
    }

    protected virtual void Start()
    {
        InitializeStateMachine(AlarmCarrierState.Idle);
        ConfigureStateMachine();
#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log(nameof(AlarmCarrierEnemy), $"{gameObject.name} State machine initialized");
#endif
        if (enemyAI.State.Equals(AlarmCarrierState.Idle))
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(AlarmCarrierEnemy), $"{gameObject.name} Manually calling OnEnterIdle for initial Idle state");
#endif
            idleBehavior.OnEnter(this);
        }

        EnsureHealthBarBinding();
    }

    private void EnsureDetectionTrigger()
    {
        if (detectionTrigger == null)
        {
            detectionTrigger = GetComponentInChildren<AlarmCarrierDetectionTrigger>();
        }

        if (detectionTrigger == null)
        {
            var triggerGO = new GameObject("AlarmDetectionTrigger");
            triggerGO.transform.SetParent(transform);
            triggerGO.transform.localPosition = Vector3.zero;
            triggerGO.transform.localRotation = Quaternion.identity;
            triggerGO.transform.localScale = Vector3.one;
            triggerGO.tag = "Untagged";
            triggerGO.layer = gameObject.layer;

            detectionTrigger = triggerGO.AddComponent<AlarmCarrierDetectionTrigger>();
        }

        detectionTrigger.Initialize(this, alarmRange);
    }

    internal void HandleDetectionTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled)
            return;

        if ((enemyAI.State.Equals(AlarmCarrierState.Idle) || enemyAI.State.Equals(AlarmCarrierState.Roaming)) && other.CompareTag("Player"))
        {
            PlayerTarget = other.transform;
            enemyAI.Fire(AlarmCarrierTrigger.PlayerInRange);
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        if (detectionTrigger != null)
        {
            detectionTrigger.SetRadius(alarmRange);
        }
    }

    protected override void ConfigureStateMachine()
    {
        enemyAI.Configure(AlarmCarrierState.Idle)
            .OnEntry(() => idleBehavior?.OnEnter(this))
            .OnExit(() => idleBehavior?.OnExit(this))
            .Permit(AlarmCarrierTrigger.PlayerInRange, AlarmCarrierState.AlarmTriggered)
            .Permit(AlarmCarrierTrigger.IdleTimerElapsed, AlarmCarrierState.Roaming)
            .Permit(AlarmCarrierTrigger.Die, AlarmCarrierState.Death);

        enemyAI.Configure(AlarmCarrierState.Roaming)
            .OnEntry(() => relocateBehavior?.OnEnter(this))
            .OnExit(() => relocateBehavior?.OnExit(this))
            .Permit(AlarmCarrierTrigger.PlayerInRange, AlarmCarrierState.AlarmTriggered)
            .Permit(AlarmCarrierTrigger.Die, AlarmCarrierState.Death);

        enemyAI.Configure(AlarmCarrierState.AlarmTriggered)
            .OnEntry(() => {
                StartAlarm();
                if (alarmFleeCoroutine != null)
                    StopCoroutine(alarmFleeCoroutine);
                alarmFleeCoroutine = StartCoroutine(AlarmFleeBehavior());
                EnemyBehaviorDebugLogBools.Log(nameof(AlarmCarrierEnemy), $"{gameObject.name} Entered AlarmTriggered state");
            })
            .Permit(AlarmCarrierTrigger.AlarmEnd, AlarmCarrierState.Summoning)
            .Permit(AlarmCarrierTrigger.Die, AlarmCarrierState.Death);

        enemyAI.Configure(AlarmCarrierState.Summoning)
            .OnEntry(() => {
                // Reset agent to ensure movement
                if (agent != null) {
                    if (!agent.enabled) agent.enabled = true;
                    if (!agent.isOnNavMesh) {
                        NavMeshHit hit;
                        if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
                            agent.Warp(hit.position);
                    }
                    agent.ResetPath();
                    agent.isStopped = false;
                }
                EnemyBehaviorDebugLogBools.Log(nameof(AlarmCarrierEnemy), $"{gameObject.name} agent.enabled={agent.enabled}, isOnNavMesh={agent.isOnNavMesh}, isStopped={agent.isStopped}, speed={agent.speed}, acceleration={agent.acceleration}");
                StartSummoning();
            })
            .Permit(AlarmCarrierTrigger.Die, AlarmCarrierState.Death);

        enemyAI.Configure(AlarmCarrierState.Death)
            .OnEntry(() => {
                deathBehavior?.OnEnter(this);
                // Stop flee coroutine on death
                if (alarmCountdownCoroutine != null)
                {
                    StopCoroutine(alarmCountdownCoroutine);
                    alarmCountdownCoroutine = null;
                }
                if (alarmFleeCoroutine != null)
                {
                    StopCoroutine(alarmFleeCoroutine);
                    alarmFleeCoroutine = null;
                }
            });
    }

    private void StartAlarm()
    {
        if (alarmCountdownCoroutine == null)
            alarmCountdownCoroutine = StartCoroutine(AlarmCountdown());

        // Visual/audio feedback for alarm
        // Example: AudioManager.Play("AlarmSiren");
        // Example: animator.SetTrigger("Alarm");
    }

    private IEnumerator AlarmCountdown()
    {
#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log(nameof(AlarmCarrierEnemy), $"{gameObject.name} AlarmCountdown started for {alarmDuration} seconds");
#endif
        yield return WaitForSecondsCache.Get(alarmDuration);
#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log(nameof(AlarmCarrierEnemy), $"{gameObject.name} AlarmCountdown finished, firing AlarmEnd");
#endif
        enemyAI.Fire(AlarmCarrierTrigger.AlarmEnd);
        alarmCountdownCoroutine = null;
    }

    private void StartSummoning()
    {
        if (spawnCoroutine == null)
            spawnCoroutine = StartCoroutine(SpawnCrawlersRoutine());
    }

    private IEnumerator SpawnCrawlersRoutine()
    {
        // Find nearest pocket each time in case the environment changes
        while (true)
        {
            nearestPocket = FindNearestPocket();
            if (nearestPocket != null)
            {
                if (Random.value < 0.7f)
                    SpawnBaseCrawlerAtPocket(nearestPocket);
                else
                    SpawnBombCrawlerAtPocket(nearestPocket);
            }

            float minInterval = 0.5f;
            float maxInterval = 10f;
            float dynamicInterval;

            if (activeAlarmSpawnedCrawlers < 10)
            {
                // Linear increase from 1.5s to 2.5s as count goes from 0 to 10
                dynamicInterval = Mathf.Lerp(1.5f, 2.5f, activeAlarmSpawnedCrawlers / 10f);
            }
            else
            {
                // Logarithmic scaling for 11+
                dynamicInterval = Mathf.Clamp(
                    2.5f * (1f + Mathf.Log10(1f + (activeAlarmSpawnedCrawlers - 9))),
                    minInterval, maxInterval);
            }

            currentDynamicSpawnInterval = dynamicInterval; // Expose in Inspector

            yield return WaitForSecondsCache.Get(dynamicInterval);
        }
    }

    private CrawlerPocket FindNearestPocket()
    {
        var pockets = FindObjectsByType<CrawlerPocket>(FindObjectsSortMode.None);
        if (pockets.Length == 0) return null;
        return pockets.OrderBy(p => Vector3.Distance(transform.position, p.transform.position)).FirstOrDefault();
    }

    private void SpawnBaseCrawlerAtPocket(CrawlerPocket pocket)
    {
        if (baseCrawlerPrefab == null)
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning(nameof(AlarmCarrierEnemy), $"[{name}] baseCrawlerPrefab is not assigned!");
#endif
            return;
        }

        float clusterRadius = pocket.ClusterRadius;
        Vector3 spawnPos = pocket.transform.position + Random.insideUnitSphere * 0.5f * clusterRadius;
        spawnPos.y = pocket.transform.position.y;

        var spawnedObj = Instantiate(baseCrawlerPrefab, spawnPos, Quaternion.identity);
        
        // Get the BaseCrawlerEnemy component from root or children
        var crawler = spawnedObj.GetComponent<BaseCrawlerEnemy>();
        if (crawler == null)
            crawler = spawnedObj.GetComponentInChildren<BaseCrawlerEnemy>();

        if (crawler != null)
        {
            crawler.Pocket = pocket;
            crawler.AlarmSource = this;
            
            // Register with SwarmManager if available
            if (SwarmManager.Instance != null)
                SwarmManager.Instance.AddToSwarm(crawler);

            // Use PlayerPresenceManager if available
            if (PlayerPresenceManager.IsPlayerPresent)
                crawler.PlayerTarget = PlayerPresenceManager.PlayerTransform;
            else
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    crawler.PlayerTarget = playerObj.transform;
            }

            // Track alarm-spawned crawlers
            activeAlarmSpawnedCrawlers++;
            crawler.OnDestroyCallback = () => { activeAlarmSpawnedCrawlers--; };

            // Force the crawler to immediately start chasing/swarming the player
            // Alarm-spawned crawlers should skip the ambush phase
            StartCoroutine(ForceChaseAfterFrame(crawler));
        }
        else
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning(nameof(AlarmCarrierEnemy), $"[{name}] baseCrawlerPrefab does not contain a BaseCrawlerEnemy component!");
#endif
            Destroy(spawnedObj);
        }
    }

    private IEnumerator ForceChaseAfterFrame(BaseCrawlerEnemy crawler)
    {
        // Wait one frame for the crawler's state machine to initialize
        yield return null;
        
        if (crawler != null && crawler.enemyAI != null)
        {
            // Try to transition to Chase or Swarm state
            if (crawler.enemyAI.CanFire(CrawlerEnemyTrigger.AmbushReady))
            {
                crawler.enemyAI.Fire(CrawlerEnemyTrigger.AmbushReady);
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log(nameof(AlarmCarrierEnemy), $"[{crawler.name}] Alarm-spawned crawler forced to Swarm state");
#endif
            }
            else if (crawler.enemyAI.CanFire(CrawlerEnemyTrigger.LosePlayer))
            {
                crawler.enemyAI.Fire(CrawlerEnemyTrigger.LosePlayer);
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log(nameof(AlarmCarrierEnemy), $"[{crawler.name}] Alarm-spawned crawler forced to Chase state");
#endif
            }
        }
    }

    private void SpawnBombCrawlerAtPocket(CrawlerPocket pocket)
    {
        if (bombCrawlerPrefab == null)
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning(nameof(AlarmCarrierEnemy), $"[{name}] bombCrawlerPrefab is not assigned!");
#endif
            return;
        }

        Vector3 spawnPos = pocket.transform.position + Random.insideUnitSphere * 0.5f * pocket.ClusterRadius;
        spawnPos.y = pocket.transform.position.y;

        var spawnedObj = Instantiate(bombCrawlerPrefab, spawnPos, Quaternion.identity);
        
        // Get the BombCarrierEnemy component from root or children
        var bomb = spawnedObj.GetComponent<BombCarrierEnemy>();
        if (bomb == null)
            bomb = spawnedObj.GetComponentInChildren<BombCarrierEnemy>();

        if (bomb != null)
        {
            bomb.Pocket = pocket;
            bomb.SetSpawnSource(true, this, pocket); // spawnedByAlarm = true, alarm = this, pocket = pocket
        }
        else
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning(nameof(AlarmCarrierEnemy), $"[{name}] bombCrawlerPrefab does not contain a BombCarrierEnemy component!");
#endif
            Destroy(spawnedObj);
        }
    }

    // Alarm flee logic
    private IEnumerator AlarmFleeBehavior()
    {
        while (enemyAI.State == AlarmCarrierState.AlarmTriggered || enemyAI.State == AlarmCarrierState.Summoning)
        {
            nearestPocket = FindNearestPocket();

            if (PlayerTarget != null && nearestPocket != null)
            {
                Vector3 pocketPos = nearestPocket.transform.position;
                Vector3 toPlayer = (PlayerTarget.position - pocketPos).normalized;
                Vector3 keepAwayDir = -toPlayer;
                Vector3 targetPos = pocketPos + keepAwayDir * keepNearPocketRadius;
                targetPos += Random.insideUnitSphere * 1.5f;
                targetPos.y = pocketPos.y;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetPos, out hit, 2f, NavMesh.AllAreas))
                {
                    if (agent != null && agent.enabled)
                    {
                        // Only update if the new target is far from the current destination
                        if (Vector3.Distance(agent.destination, hit.position) > minMoveDistance)
                        {
                            agent.isStopped = false;
                            agent.SetDestination(hit.position);
                        }
                    }
                }
            }
            yield return WaitForSecondsCache.Get(fleeCheckInterval);
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);
        if (alarmCountdownCoroutine != null)
        {
            StopCoroutine(alarmCountdownCoroutine);
            alarmCountdownCoroutine = null;
        }
        if (alarmFleeCoroutine != null)
        {
            StopCoroutine(alarmFleeCoroutine);
            alarmFleeCoroutine = null;
        }
    }

    private Zone FindNearestZone()
    {
        // Use ZoneManager if available
        var zones = ZoneManager.Instance != null 
            ? ZoneManager.Instance.GetAllZones() 
            : FindObjectsByType<Zone>(FindObjectsSortMode.None);
        if (zones.Length == 0) return null;
        return zones.OrderBy(z => Vector3.Distance(transform.position, z.transform.position)).FirstOrDefault();
    }
}
