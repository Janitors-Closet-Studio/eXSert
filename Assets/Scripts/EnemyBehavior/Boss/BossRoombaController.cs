// BossRoombaController.cs
// Purpose: Controller for the Roomba-style boss: movement, special abilities (suction, dash), and spawning pocket adds.
// Works with: CrowdController (registers spawned adds), ScenePoolManager (uses local pools), EnemyBehaviorProfile.
// Notes: Uses scene-local pooling to reduce Instantiate/Destroy overhead for spawned adds.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EnemyBehavior.Boss;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Determines which enemy type spawns first when the alarm activates.
/// </summary>
public enum SpawnOrderPriority
{
    DronesFirst,
    CrawlersFirst
}

[RequireComponent(typeof(NavMeshAgent))]
public class BossRoombaController : MonoBehaviour
{
    [Header("Behavior Profile")]
    [SerializeField, Tooltip(
        "ScriptableObject that tunes nav/avoidance/importance and planner hints.\n" +
        "Assign an asset from Assets > Scripts > EnemyBehavior > Profiles (Create > AI > EnemyBehaviorProfile).\n" +
        "Values are applied to the boss NavMeshAgent on Start and passed to Crowd/Path systems.")]
    public EnemyBehaviorProfile profile;


    private NavMeshAgent agent;
    private Transform player;
    private Animator animator;
    public GameObject alarm;

    [Header("Alarm Settings")]
    [Tooltip("Delay in seconds after fight start or form change before alarm activates (random range)")]
    public Vector2 AlarmActivationDelayRange = new Vector2(3f, 5f);
    [Tooltip("Which enemy type spawns first when the alarm activates.")]
    public SpawnOrderPriority SpawnOrder = SpawnOrderPriority.DronesFirst;
    [Tooltip("Delay range (seconds) before the second enemy type spawns after the first.")]
    public Vector2 SecondWaveDelayRange = new Vector2(2f, 4f);
    [Tooltip("Maximum health of the alarm. When destroyed, no more adds spawn.")]
    public float AlarmMaxHealth = 100f;
    [Tooltip("Current health of the alarm (runtime).")]
    [SerializeField] private float alarmCurrentHealth;
    [Tooltip("Damage multiplier when attacking the alarm (e.g., 0.5 = half damage)")]
    public float AlarmDamageMultiplier = 0.5f;
    [Tooltip("Should the alarm object fall off and be destroyed visually when health reaches 0?")]
    public bool AlarmFallsOffWhenDestroyed = true;
    [Tooltip("Force applied when alarm breaks off")]
    public float AlarmBreakOffForce = 200f;
    private bool alarmActivated;
    private bool alarmDestroyed;
    private Coroutine alarmActivationDelayRoutine;

    [Header("Spawn Pockets")]
    [Tooltip("Pockets for drone spawns (flying)")]
    public Transform[] droneSpawnPoints;
    [Tooltip("Pockets for crawler spawns (ground)")]
    public Transform[] crawlerSpawnPoints;

    [Tooltip("Legacy: used if specific arrays are empty")]
    public Transform[] pocketSpawnPoints;


    public GameObject dronePrefab;
    public GameObject crawlerPrefab;
    [Tooltip("Maximum number of each enemy type that can exist at once")]
    public int maxDrones = 4;
    public int maxCrawlers = 2;
    public int dronesPerSpawn = 4;
    public int crawlersPerSpawn = 2;
    
    [Header("Add Flee Settings")]
    [Tooltip("Speed multiplier applied to adds when fleeing to spawn points during vacuum sequence")]
    public float AddFleeSpeedMultiplier = 2.5f;
    [Tooltip("Distance threshold to consider an add has arrived at spawn point")]
    public float AddFleeArrivalThreshold = 2f;
    [Tooltip("Time in seconds before fleeing adds are destroyed after arriving at spawn point")]
    public float AddFleeDestroyDelay = 1.5f;
    
    public float suctionRadius = 5f;
    public float suctionStrength = 10f;
    public float dashSpeedMultiplier = 3f;

    [Header("Locomotion")]
    [Tooltip("Minimum stopping distance to avoid overlapping the player.")]
    public float MinStoppingDistance = 4.0f;
    [Tooltip("Additional offset from player during combat approach (added to stopping distance).")]
    public float CombatDistanceOffset = 2.0f;
    [Tooltip("Ensure a kinematic Rigidbody for stable collisions and platform carry.")]
    public bool EnsureKinematicRigidbody = true;
    [Tooltip("Skip automatic collider setup - user will configure colliders manually.")]
    public bool ManualColliderSetup = false;
    [Tooltip("Extra buffer to re-enable movement after stopping; prevents jitter.")]
    public float ApproachHysteresis = 0.75f;
    [Tooltip("Max distance to adjust ring target to a nearby NavMesh point.")]
    public float ApproachSampleMaxDistance = 1.0f;

    [Header("Animator Parameters")]
    [SerializeField] private string ParamSpeed = "Speed";
    [SerializeField] private string ParamIsMoving = "IsMoving";
    [SerializeField] private string ParamTurn = "Turn";


    [Header("Top-Wander (Player On Top)")]
    [Tooltip("Speed multiplier during top-wander movement.")]
    public float TopWanderSpeedMultiplier = 1.1f;
    [Tooltip("Optional absolute top-wander speed. If > 0, this value is used directly instead of SpeedMultiplier × current speed.")]
    public float TopWanderSpeedOverride = 0f;
    [Tooltip("Minimum absolute movement speed while top-wander is active (prevents getting stuck if top-wander starts during low-speed turn states).")]
    public float TopWanderMinSpeed = 6f;
    [Tooltip("Random target radius range (meters) for top-wander.")]
    public Vector2 TopWanderRadiusRange = new Vector2(4f, 10f);
    [Tooltip("Time range (seconds) before repicking a new wander target.")]
    public Vector2 TopWanderRepathTimeRange = new Vector2(0.7f, 1.4f);

    [Header("Debug")]
    [Tooltip("Enable verbose [BossTopWander] logs.")]
    [SerializeField] private bool enableTopWanderDebugLogs = false;

    // Object pools
    private readonly Dictionary<Transform, GameObject> activeDrones = new Dictionary<Transform, GameObject>();
    private readonly Dictionary<Transform, GameObject> activeCrawlers = new Dictionary<Transform, GameObject>();
    private readonly Queue<GameObject> dronePool = new Queue<GameObject>();
    private readonly Queue<GameObject> crawlerPool = new Queue<GameObject>();
    public int initialPoolSize = 8;
    
    // Track which adds are currently fleeing (shouldn't be redirected by ManageSpawnsRoutine)
    private readonly HashSet<GameObject> fleeingAdds = new HashSet<GameObject>();

    private Coroutine followRoutine;
    private Coroutine animParamsRoutine;
    private Coroutine topWanderRoutine;
    private Coroutine spawnManagementRoutine;
    private Coroutine playerRefRetryRoutine;
    private float lastFollowCadence = 0.1f;

    // Saved agent settings for top-wander
    private bool topWanderActive;
    private float savedSpeed;
    private bool savedAutoBraking;
    private float savedStoppingDistance;

    void Awake()
    {
        // Roomba top-mount design expects stable standing/walking on top.
        // Disable generic slide-off behavior if present on this boss hierarchy.
        var slideOffComponents = GetComponentsInChildren<PlayerSlideOffSurface>(true);
        if (slideOffComponents != null)
        {
            foreach (var slideOff in slideOffComponents)
            {
                if (slideOff == null)
                    continue;

                slideOff.SetDisabled(true);
            }
        }

        if (EnsureKinematicRigidbody)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            
            // Only add collider if not doing manual setup
            if (!ManualColliderSetup)
            {
                // ADD: Ensure there's a physical (non-trigger) collider for standing on top
                var physicalCollider = GetComponent<CapsuleCollider>();
                if (physicalCollider == null)
                {
                    // Check if there's any non-trigger collider
                    var existingColliders = GetComponents<Collider>();
                    bool hasPhysicalCollider = false;
                    foreach (var col in existingColliders)
                    {
                        if (!col.isTrigger)
                        {
                            hasPhysicalCollider = true;
                            break;
                        }
                    }
                    
                    // If no physical collider exists, add one
                    if (!hasPhysicalCollider)
                    {
                        physicalCollider = gameObject.AddComponent<CapsuleCollider>();
                        physicalCollider.isTrigger = false;
                        physicalCollider.radius = 1.5f; // Adjust to match boss size
                        physicalCollider.height = 2f;   // Adjust to match boss height
                        physicalCollider.center = new Vector3(0, 1f, 0); // Center at half-height
                        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), "[BossRoombaController] Added physical CapsuleCollider for player collision/standing");
                    }
                }
            }
        }


        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        
        // Prewarm pools - use inactive parent to prevent OnEnable spam during instantiation
        // Objects instantiated under an inactive parent are also inactive, preventing
        // SwarmManager registration/unregistration churn during pool prewarming
        var poolParent = new GameObject("_TempPoolParent");
        poolParent.SetActive(false);
        
        for (int i = 0; i < initialPoolSize; i++)
        {
            if (dronePrefab != null)
            {
                var g = Instantiate(dronePrefab, poolParent.transform);
                // CRITICAL: Deactivate BEFORE unparenting to prevent OnEnable from firing
                g.SetActive(false);
                g.transform.SetParent(null);
                dronePool.Enqueue(g);
            }
            if (crawlerPrefab != null)
            {
                var c = Instantiate(crawlerPrefab, poolParent.transform);
                // CRITICAL: Deactivate BEFORE unparenting to prevent OnEnable from firing
                c.SetActive(false);
                c.transform.SetParent(null);
                crawlerPool.Enqueue(c);
            }
        }
        
        Destroy(poolParent);
        
        // Initialize alarm health
        InitializeAlarmHealth();
    }

    void OnEnable()
    {
        if (animParamsRoutine != null) StopCoroutine(animParamsRoutine);
        animParamsRoutine = StartCoroutine(AnimParamsLoop(0.05f));
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartPlayerRefRetryIfNeeded();
        
        alarmActivated = false;
        // Reset alarm on enable (in case of pooling/re-enabling)
        if (!alarmDestroyed)
        {
            InitializeAlarmHealth();
        }
    }

    void OnDisable()
    {
        if (animParamsRoutine != null) { StopCoroutine(animParamsRoutine); animParamsRoutine = null; }
        if (spawnManagementRoutine != null) { StopCoroutine(spawnManagementRoutine); spawnManagementRoutine = null; }
        if (playerRefRetryRoutine != null) { StopCoroutine(playerRefRetryRoutine); playerRefRetryRoutine = null; }
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopTopWander();
        StopFollowing();
    }

    private IEnumerator AnimParamsLoop(float cadence)
    {
        var wait = WaitForSecondsCache.Get(Mathf.Max(0.02f, cadence));
        if (animator == null) yield break;
        while (true)
        {
            float spd = (agent != null && agent.enabled) ? agent.velocity.magnitude : 0f;
            
            if (!string.IsNullOrEmpty(ParamSpeed))
                animator.SetFloat(ParamSpeed, spd);
            
            if (!string.IsNullOrEmpty(ParamIsMoving))
                animator.SetBool(ParamIsMoving, spd > 0.1f);

            if (!string.IsNullOrEmpty(ParamTurn) && agent != null && agent.enabled)
            {
                Vector3 desired = agent.desiredVelocity;
                if (desired.sqrMagnitude > 0.01f)
                {
                    float angle = Vector3.SignedAngle(transform.forward, desired, Vector3.up);
                    float turn = Mathf.Clamp(angle / 45f, -1f, 1f);
                    animator.SetFloat(ParamTurn, turn);
                }
                else
                {
                    animator.SetFloat(ParamTurn, 0f);
                }
            }

            yield return wait;
        }
    }

    void Start()
    {
        ApplyProfile();
        TryCachePlayerReference();
        
        var ca = new EnemyBehavior.Crowd.CrowdAgent() { Agent = agent, Profile = profile };
        if (EnemyBehavior.Crowd.CrowdController.Instance != null)
            EnemyBehavior.Crowd.CrowdController.Instance.Register(ca);
    }

    private bool TryCachePlayerReference(bool logIfMissing = false)
    {
        if (PlayerPresenceManager.IsPlayerPresent && PlayerPresenceManager.PlayerTransform != null)
        {
            player = PlayerPresenceManager.PlayerTransform;
            return true;
        }

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            return true;
        }

        if (logIfMissing)
        {
            EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), "[BossRoombaController] Player not found in loaded scenes yet. Retrying...");
        }

        return false;
    }

    private IEnumerator RetryPlayerReferenceRoutine()
    {
        while (player == null)
        {
            if (TryCachePlayerReference())
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Player resolved after delayed scene load: {player.name}");
                break;
            }

            yield return WaitForSecondsCache.Get(0.5f);
        }

        playerRefRetryRoutine = null;
    }

    private void StartPlayerRefRetryIfNeeded()
    {
        if (player != null || playerRefRetryRoutine != null)
            return;

        playerRefRetryRoutine = StartCoroutine(RetryPlayerReferenceRoutine());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (player != null)
            return;

        TryCachePlayerReference();
        StartPlayerRefRetryIfNeeded();
    }

    void ApplyProfile()
    {
        if (profile == null) return;
        agent.speed = Random.Range(profile.SpeedRange.x, profile.SpeedRange.y);
        agent.acceleration = profile.Acceleration;
        agent.angularSpeed = profile.AngularSpeed;
        agent.stoppingDistance = Mathf.Max(profile.StoppingDistance, MinStoppingDistance);
        agent.avoidancePriority = profile.AvoidancePriority;
        agent.autoBraking = false;
    }

    public void StartFollowingPlayer(float cadenceSeconds)
    {
        if (topWanderActive)
        {
            TopWanderLog("[BossTopWander] Ignoring StartFollowingPlayer while top-wander is active.");
            return;
        }

        lastFollowCadence = Mathf.Max(0.02f, cadenceSeconds);
        if (followRoutine != null) StopCoroutine(followRoutine);
        followRoutine = StartCoroutine(FollowLoop(lastFollowCadence));
    }

    public void StopFollowing()
    {
        if (followRoutine != null) { StopCoroutine(followRoutine); followRoutine = null; }
    }

    private Vector3 ComputeApproachPoint(Vector3 bossPos, Vector3 playerPos)
    {
        Vector3 toPlayer = playerPos - bossPos;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        if (dist < 0.001f)
            return bossPos;
        // Use both stopping distance and combat offset for the ring
        float ring = Mathf.Max(agent.stoppingDistance, MinStoppingDistance) + CombatDistanceOffset;
        Vector3 candidate = playerPos - toPlayer.normalized * ring;
        if (NavMesh.SamplePosition(candidate, out var hit, ApproachSampleMaxDistance, NavMesh.AllAreas))
            return hit.position;
        return candidate;
    }

    private IEnumerator FollowLoop(float cadence)
    {
        var wait = WaitForSecondsCache.Get(Mathf.Max(0.02f, cadence));
        while (true)
        {
            if (player == null)
            {
                TryCachePlayerReference();
                StartPlayerRefRetryIfNeeded();
                yield return wait;
                continue;
            }

            if (player != null)
            {
                Vector3 bossPos = transform.position;
                Vector3 playerPos = player.position;
                Vector3 flat = playerPos - bossPos; flat.y = 0f;
                float dist = flat.magnitude;
                float stop = Mathf.Max(agent.stoppingDistance, MinStoppingDistance);

                if (dist <= stop)
                {
                    if (!agent.isStopped)
                    {
                        agent.ResetPath();
                        agent.isStopped = true;
                    }
                }
                else if (dist > stop + ApproachHysteresis)
                {
                    agent.isStopped = false;
                    Vector3 target = ComputeApproachPoint(bossPos, playerPos);
                    agent.SetDestination(target);
                }
            }
            yield return wait;
        }
    }

    public void StartTopWander()
    {
        if (topWanderActive)
        {
            StopFollowing();
            if (agent != null)
            {
                agent.isStopped = false;
                if (!agent.hasPath || (agent.remainingDistance <= 0.05f && !agent.pathPending))
                {
                    Vector3 origin = transform.position;
                    float radius = Random.Range(TopWanderRadiusRange.x, TopWanderRadiusRange.y);
                    Vector2 dir2D = Random.insideUnitCircle.normalized;
                    Vector3 candidate = origin + new Vector3(dir2D.x, 0f, dir2D.y) * radius;
                    if (NavMesh.SamplePosition(candidate, out var hit, 2.0f, NavMesh.AllAreas))
                        candidate = hit.position;

                    agent.SetDestination(candidate);
                }
            }

            TopWanderLog($"[BossTopWander] StartTopWander ignored (already active). isStopped={agent.isStopped} speed={agent.speed:F2} vel={agent.velocity.magnitude:F2}");
            return;
        }
        topWanderActive = true;

        // Trigger alarm on player mount (if not already activated)
        if (!alarmActivated)
        {
            ActivateAlarm();
        }

        savedSpeed = agent.speed;
        savedAutoBraking = agent.autoBraking;
        savedStoppingDistance = agent.stoppingDistance;

        agent.autoBraking = false;
        agent.stoppingDistance = 0f;
        float topWanderSpeed = TopWanderSpeedOverride > 0f
            ? TopWanderSpeedOverride
            : savedSpeed * TopWanderSpeedMultiplier;
        if (topWanderSpeed < TopWanderMinSpeed)
            topWanderSpeed = TopWanderMinSpeed;
        agent.speed = topWanderSpeed;
        agent.isStopped = false;

        TopWanderLog($"[BossTopWander] START speed={agent.speed:F2} savedSpeed={savedSpeed:F2} minSpeed={TopWanderMinSpeed:F2} autoBraking={agent.autoBraking} stoppingDistance={agent.stoppingDistance:F2}");

        StopFollowing();
        if (topWanderRoutine != null) StopCoroutine(topWanderRoutine);
        topWanderRoutine = StartCoroutine(TopWanderLoop());
    }

    public void StopTopWander()
    {
        if (!topWanderActive) return;
        topWanderActive = false;

        TopWanderLog($"[BossTopWander] STOP restoringSpeed={savedSpeed:F2} currentVel={agent.velocity.magnitude:F2}");

        if (topWanderRoutine != null)
        {
            StopCoroutine(topWanderRoutine);
            topWanderRoutine = null;
        }

        agent.speed = savedSpeed;
        agent.autoBraking = savedAutoBraking;
        agent.stoppingDistance = savedStoppingDistance;

        StartFollowingPlayer(lastFollowCadence);
    }

    private IEnumerator TopWanderLoop()
    {
        while (true)
        {
            Vector3 origin = transform.position;
            float radius = Random.Range(TopWanderRadiusRange.x, TopWanderRadiusRange.y);
            Vector2 dir2D = Random.insideUnitCircle.normalized;
            Vector3 candidate = origin + new Vector3(dir2D.x, 0f, dir2D.y) * radius;
            bool sampledNav = false;
            if (NavMesh.SamplePosition(candidate, out var hit, 2.0f, NavMesh.AllAreas))
            {
                candidate = hit.position;
                sampledNav = true;
            }

            agent.isStopped = false;
            bool setDestinationOk = agent.SetDestination(candidate);

            TopWanderLog($"[BossTopWander] Repath origin={origin} candidate={candidate} radius={radius:F2} sampledNav={sampledNav} setDestinationOk={setDestinationOk} pathPending={agent.pathPending} hasPath={agent.hasPath} pathStatus={agent.pathStatus} remDist={agent.remainingDistance:F2} speed={agent.speed:F2} vel={agent.velocity.magnitude:F2}");

            float timeout = Random.Range(TopWanderRepathTimeRange.x, TopWanderRepathTimeRange.y);
            float t = 0f;
            while (t < timeout)
            {
                if (t <= 0.001f || t >= timeout - 0.02f)
                {
                    TopWanderLog($"[BossTopWander] Tick t={t:F2}/{timeout:F2} pos={transform.position} vel={agent.velocity.magnitude:F2} isStopped={agent.isStopped} hasPath={agent.hasPath} pathPending={agent.pathPending} remDist={agent.remainingDistance:F2}");
                }

                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    private void TopWanderLog(string message)
    {
        if (!enableTopWanderDebugLogs)
            return;

        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), message);
    }
    public void ActivateAlarm()
    {
        // Don't activate if destroyed
        if (alarmDestroyed) return;
        
        if (alarmActivated) return;
        
        alarmActivated = true;
        if (alarm != null) alarm.SetActive(true);
        
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), "Alarm ACTIVATED - Starting spawn management");
        
        // Start spawn management routine
        if (spawnManagementRoutine != null) StopCoroutine(spawnManagementRoutine);
        spawnManagementRoutine = StartCoroutine(ManageSpawnsRoutine());
    }
    
    /// <summary>
    /// Activates the alarm after a random delay (for form changes or fight start).
    /// </summary>
    public void ActivateAlarmWithDelay()
    {
        if (alarmDestroyed) return;
        if (alarmActivated) return;
        
        // Don't restart if already waiting for activation
        if (alarmActivationDelayRoutine != null) return;
        
        alarmActivationDelayRoutine = StartCoroutine(AlarmActivationDelayRoutine());
    }
    
    private IEnumerator AlarmActivationDelayRoutine()
    {
        float delay = Random.Range(AlarmActivationDelayRange.x, AlarmActivationDelayRange.y);
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"Alarm will activate in {delay:F1} seconds...");
        yield return WaitForSecondsCache.Get(delay);
        
        if (!alarmDestroyed && !alarmActivated)
        {
            ActivateAlarm();
        }
        alarmActivationDelayRoutine = null;
    }

    public void DeactivateAlarm()
    {
        alarmActivated = false;
        if (alarm != null && !alarmDestroyed) alarm.SetActive(false);
        
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), "Alarm DEACTIVATED - Stopping spawn management");
        
        // Stop delayed activation if pending
        if (alarmActivationDelayRoutine != null)
        {
            StopCoroutine(alarmActivationDelayRoutine);
            alarmActivationDelayRoutine = null;
        }
        
        if (spawnManagementRoutine != null)
        {
            StopCoroutine(spawnManagementRoutine);
            spawnManagementRoutine = null;
        }
    }
    
    /// <summary>
    /// Returns true if the alarm is still functional (not destroyed).
    /// </summary>
    public bool IsAlarmAlive => !alarmDestroyed;
    
    /// <summary>
    /// Apply damage to the alarm. Called by the damage system when player attacks the alarm.
    /// </summary>
    public void DamageAlarm(float damage)
    {
        if (alarmDestroyed) return;
        
        float actualDamage = damage * AlarmDamageMultiplier;
        alarmCurrentHealth -= actualDamage;
        
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Alarm took {actualDamage} damage ({alarmCurrentHealth}/{AlarmMaxHealth})");
        
        if (alarmCurrentHealth <= 0f)
        {
            DestroyAlarm();
        }
    }
    
    /// <summary>
    /// Destroys the alarm permanently. No more adds will spawn.
    /// Can be called from debug buttons or when alarm health reaches 0.
    /// </summary>
    public void DestroyAlarm()
    {
        if (alarmDestroyed) return;
        
        alarmDestroyed = true;
        alarmActivated = false;
        
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), "[BossRoombaController] ALARM DESTROYED - No more adds will spawn!");
        
        // Stop spawn management
        if (spawnManagementRoutine != null)
        {
            StopCoroutine(spawnManagementRoutine);
            spawnManagementRoutine = null;
        }
        
        // Stop delayed activation if pending
        if (alarmActivationDelayRoutine != null)
        {
            StopCoroutine(alarmActivationDelayRoutine);
            alarmActivationDelayRoutine = null;
        }
        
        // Handle visual destruction
        if (alarm != null)
        {
            if (AlarmFallsOffWhenDestroyed)
            {
                // Detach and apply physics
                alarm.transform.SetParent(null);
                
                var rb = alarm.GetComponent<Rigidbody>();
                if (rb == null) rb = alarm.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                
                // Add collider if needed
                if (alarm.GetComponent<Collider>() == null)
                {
                    var box = alarm.AddComponent<BoxCollider>();
                    box.size = Vector3.one * 0.5f;
                }
                
                // Apply break-off force
                Vector3 breakDirection = (alarm.transform.position - transform.position).normalized;
                breakDirection.y = 0.5f;
                rb.AddForce(breakDirection.normalized * AlarmBreakOffForce, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * AlarmBreakOffForce * 0.3f, ForceMode.Impulse);
                
                // Destroy after a delay
                Destroy(alarm, 5f);
            }
            else
            {
                alarm.SetActive(false);
            }
        }
        
        // Notify the brain
        var brain = GetComponent<EnemyBehavior.Boss.BossRoombaBrain>();
        if (brain != null)
        {
            brain.OnAlarmDestroyed();
        }
    }
    
    #region Debug Methods
    
    /// <summary>
    /// DEBUG: Kills one random active add (crawler or drone).
    /// Tests object pooling and respawn functionality.
    /// </summary>
    public void DebugKillOneRandomAdd()
    {
        var allAdds = new List<GameObject>();
        
        // Collect all active drones
        foreach (var kvp in activeDrones)
        {
            if (kvp.Value != null && kvp.Value.activeInHierarchy)
                allAdds.Add(kvp.Value);
        }
        
        // Collect all active crawlers
        foreach (var kvp in activeCrawlers)
        {
            if (kvp.Value != null && kvp.Value.activeInHierarchy)
                allAdds.Add(kvp.Value);
        }
        
        if (allAdds.Count == 0)
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), "[Boss DEBUG] No active adds to kill!");
            return;
        }
        
        // Pick a random one
        int index = UnityEngine.Random.Range(0, allAdds.Count);
        GameObject target = allAdds[index];
        
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[Boss DEBUG] Killing add: {target.name}");
        
        // Try to kill it through its health system
        var healthSystem = target.GetComponent<IHealthSystem>();
        if (healthSystem != null)
        {
            healthSystem.LoseHP(9999f);
        }
        else
        {
            // Fallback: just deactivate it
            target.SetActive(false);
        }
    }
    
    /// <summary>
    /// DEBUG: Kills all active adds (crawlers and drones).
    /// </summary>
    public void DebugKillAllAdds()
    {
        int killCount = 0;
        
        // Kill all drones
        foreach (var kvp in activeDrones)
        {
            if (kvp.Value != null && kvp.Value.activeInHierarchy)
            {
                var healthSystem = kvp.Value.GetComponent<IHealthSystem>();
                if (healthSystem != null)
                {
                    healthSystem.LoseHP(9999f);
                }
                else
                {
                    kvp.Value.SetActive(false);
                }
                killCount++;
            }
        }
        
        // Kill all crawlers
        foreach (var kvp in activeCrawlers)
        {
            if (kvp.Value != null && kvp.Value.activeInHierarchy)
            {
                var healthSystem = kvp.Value.GetComponent<IHealthSystem>();
                if (healthSystem != null)
                {
                    healthSystem.LoseHP(9999f);
                }
                else
                {
                    kvp.Value.SetActive(false);
                }
                killCount++;
            }
        }
        
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[Boss DEBUG] Killed {killCount} adds!");
    }
    
    #endregion
    
    
    
    
    /// <summary>
    /// Orders all active adds to flee to their nearest spawn point.
    /// Called when the boss starts moving towards the vacuum position,
    /// giving adds time to clear the arena center before walls go up.
    /// Adds are tracked as "fleeing" so ManageSpawnsRoutine won't redirect them.
    /// Once they arrive at spawn points, they are stopped and destroyed after a delay.
    /// </summary>
    public void OrderAddsToFleeToSpawnPoints()
    {
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), "[BossRoombaController] Ordering all adds to flee to nearest spawn points");
        
        // Clear previous fleeing tracking
        fleeingAdds.Clear();
        
        int dronesFleeing = 0;
        int crawlersFleeing = 0;
        
        // Determine which spawn points to use for each type (fall back to legacy if specific arrays are empty)
        Transform[] droneTargets = (droneSpawnPoints != null && droneSpawnPoints.Length > 0) 
            ? droneSpawnPoints 
            : pocketSpawnPoints;
        Transform[] crawlerTargets = (crawlerSpawnPoints != null && crawlerSpawnPoints.Length > 0) 
            ? crawlerSpawnPoints 
            : pocketSpawnPoints;
        
        // Log spawn point availability for debugging
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Spawn points - drones: {droneTargets?.Length ?? 0}, crawlers: {crawlerTargets?.Length ?? 0}, legacy: {pocketSpawnPoints?.Length ?? 0}");
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Active adds - drones: {activeDrones.Count}, crawlers: {activeCrawlers.Count}");
        
        // Order all active drones to flee to nearest spawn point
        var droneKeys = new List<Transform>(activeDrones.Keys);
        foreach (var key in droneKeys)
        {
            try
            {
                if (!activeDrones.TryGetValue(key, out var drone)) continue;
                if (drone == null || !drone.activeInHierarchy) continue;
                
                Transform nearestSpawn = FindNearestSpawnPoint(drone.transform.position, droneTargets);
                if (nearestSpawn != null)
                {
                    var droneAgent = drone.GetComponent<NavMeshAgent>();
                    if (droneAgent != null && droneAgent.enabled && droneAgent.isOnNavMesh)
                    {
                        // Mark as fleeing so ManageSpawnsRoutine won't redirect
                        fleeingAdds.Add(drone);
                        
                        // Apply flee speed multiplier
                        droneAgent.speed *= AddFleeSpeedMultiplier;
                        droneAgent.isStopped = false;
                        droneAgent.SetDestination(nearestSpawn.position);
                        
                        // Start coroutine to monitor arrival and destroy
                        StartCoroutine(MonitorFleeingAddArrival(drone, droneAgent, nearestSpawn.position, true));
                        
                        dronesFleeing++;
                        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Drone {drone.name} fleeing to spawn point at {nearestSpawn.position} (speed: {droneAgent.speed})");
                    }
                }
                else
                {
                    EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] No spawn point found for drone {drone.name}");
                }
            }
            catch (System.Exception e)
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] Error ordering drone to flee: {e.Message}");
            }
        }
        
        // Order all active crawlers to flee to nearest spawn point
        var crawlerKeys = new List<Transform>(activeCrawlers.Keys);
        foreach (var key in crawlerKeys)
        {
            try
            {
                if (!activeCrawlers.TryGetValue(key, out var crawlerObj)) continue;
                if (crawlerObj == null || !crawlerObj.activeInHierarchy) continue;
                
                Transform nearestSpawn = FindNearestSpawnPoint(crawlerObj.transform.position, crawlerTargets);
                if (nearestSpawn != null)
                {
                    // CRITICAL: Crawlers use BaseCrawlerEnemy.agent, not GetComponent<NavMeshAgent>()
                    // Try both root and children since component might be on a child object
                    var crawlerEnemy = crawlerObj.GetComponent<BaseCrawlerEnemy>()
                        ?? crawlerObj.GetComponentInChildren<BaseCrawlerEnemy>();
                    NavMeshAgent crawlerAgent = null;
                    
                    if (crawlerEnemy != null && crawlerEnemy.agent != null)
                    {
                        crawlerAgent = crawlerEnemy.agent;
                        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Crawler {crawlerObj.name} - found via crawlerEnemy.agent");
                    }
                    else
                    {
                        // Fallback: try direct component lookup
                        crawlerAgent = crawlerObj.GetComponent<NavMeshAgent>()
                            ?? crawlerObj.GetComponentInChildren<NavMeshAgent>();
                        
                        if (crawlerEnemy == null)
                        {
                            // Still null - log what components ARE on the object for debugging
                            var components = crawlerObj.GetComponents<Component>();
                            var componentNames = string.Join(", ", components.Select(c => c?.GetType().Name ?? "null"));
                            EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] BaseCrawlerEnemy component NOT FOUND on {crawlerObj.name}! Components: {componentNames}");
                        }
                        else
                        {
                            EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] Crawler {crawlerObj.name} - crawlerEnemy.agent is null, using fallback NavMeshAgent");
                        }
                    }
                    
                    
                if (crawlerAgent != null && crawlerAgent.enabled && crawlerAgent.isOnNavMesh)
                    {
                        // Mark as fleeing so ManageSpawnsRoutine won't redirect
                        fleeingAdds.Add(crawlerObj);
                        
                        // DEBUG: Log the crawler enemy status
                        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Crawler {crawlerObj.name} - crawlerEnemy is {(crawlerEnemy != null ? "NOT NULL" : "NULL")}");
                        
                        // CRITICAL: Stop ALL coroutines and remove from SwarmManager FIRST!
                        // The SwarmManager continuously updates crawler destinations, which will
                        // override our flee destination unless we unregister the crawler.
                        if (crawlerEnemy != null)
                        {
                            // Unregister from SwarmManager FIRST - this is critical!
                            // The SwarmManager overrides destinations every frame.
                            crawlerEnemy.UnregisterFromSwarmManager();
                            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Unregistered {crawlerObj.name} from SwarmManager");
                            
                            // Stop all coroutines (ChaseBehavior, etc.)
                            crawlerEnemy.StopAllCoroutines();
                            
                            // Disable the MonoBehaviour to prevent Update/FixedUpdate
                            crawlerEnemy.enabled = false;
                            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Stopped coroutines and disabled state machine for {crawlerObj.name}");
                        }
                        else
                        {
                            EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] crawlerEnemy is NULL for {crawlerObj.name} - cannot unregister from SwarmManager!");
                        }
                        
                        // Apply flee speed multiplier
                        crawlerAgent.speed *= AddFleeSpeedMultiplier;
                        crawlerAgent.isStopped = false;
                        crawlerAgent.ResetPath(); // Clear any existing path first
                        crawlerAgent.SetDestination(nearestSpawn.position);
                        
                        // Log distance from crawler to chosen spawn vs all spawn points for debugging
                        float chosenDist = Vector3.Distance(crawlerObj.transform.position, nearestSpawn.position);
                        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Crawler {crawlerObj.name} at {crawlerObj.transform.position} -> nearest spawn {nearestSpawn.name} at {nearestSpawn.position} (dist: {chosenDist:F1}m)");
                        
                        // Start coroutine to monitor arrival (destruction happens when walls raise)
                        StartCoroutine(MonitorFleeingAddArrival(crawlerObj, crawlerAgent, nearestSpawn.position, false));
                        
                        crawlersFleeing++;
                        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Crawler {crawlerObj.name} fleeing to spawn point at {nearestSpawn.position} (speed: {crawlerAgent.speed})");
                    }
                    else
                    {
                        EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] Crawler {crawlerObj.name} has no valid NavMeshAgent (crawlerEnemy: {crawlerEnemy != null}, agent: {crawlerAgent}, enabled: {crawlerAgent?.enabled}, onNavMesh: {crawlerAgent?.isOnNavMesh})");
                    }
                }
                else
                {
                    EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] No spawn point found for crawler {crawlerObj.name} (crawlerTargets: {crawlerTargets?.Length ?? 0})");
                }
            }
            catch (System.Exception e)
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] Error ordering crawler to flee: {e.Message}");
            }
        }
        
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Ordered {dronesFleeing} drones and {crawlersFleeing} crawlers to flee to spawn points (speed multiplier: {AddFleeSpeedMultiplier}x)");
    }
    
    
    
    
    /// <summary>
    /// Monitors a fleeing add until it arrives at its destination, then destroys it.
    /// Both drones and crawlers are destroyed on arrival (looks like returning to spawn points).
    /// OnCageMatchStart() is the fallback for any stragglers that don't arrive in time.
    /// </summary>
    private IEnumerator MonitorFleeingAddArrival(GameObject add, NavMeshAgent addAgent, Vector3 destination, bool isDrone)
    {
        if (add == null || addAgent == null) yield break;
        
        float timeout = 10f; // Safety timeout
        float elapsed = 0f;
        
        // CRITICAL: Wait a few frames before checking distance to give pathfinding time to start
        // This prevents immediate "arrival" detection for adds that were spawned at their spawn points
        yield return null;
        yield return null;
        yield return null;
        
        // Get the current distance - if the add hasn't moved at all after 3 frames,
        // check if they're actually moving toward the destination
        float initialDist = Vector3.Distance(add.transform.position, destination);
        
        // Wait until the add arrives at destination or timeout
        while (elapsed < timeout && add != null && add.activeInHierarchy)
        {
            float distToDestination = Vector3.Distance(add.transform.position, destination);
            
            // Only count as "arrived" if they actually moved OR were genuinely at the destination
            // This prevents immediate arrival for adds that start near their spawn point
            bool hasMovedSignificantly = Mathf.Abs(distToDestination - initialDist) > 0.5f;
            bool isAtDestination = distToDestination <= AddFleeArrivalThreshold;
            
            if (isAtDestination && (hasMovedSignificantly || elapsed > 2f))
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] {(isDrone ? "Drone" : "Crawler")} {add.name} arrived at spawn point (moved: {hasMovedSignificantly}, elapsed: {elapsed:F1}s)");
                break;
            }
            
            // Check if agent stopped moving (might be stuck)
            if (addAgent != null && addAgent.enabled && addAgent.velocity.magnitude < 0.1f && elapsed > 1f)
            {
                // Try to re-set destination in case it got cleared
                addAgent.SetDestination(destination);
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (add == null || !add.activeInHierarchy) yield break;
        
        // Stop the agent
        if (addAgent != null && addAgent.enabled)
        {
            addAgent.isStopped = true;
            addAgent.ResetPath();
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] {(isDrone ? "Drone" : "Crawler")} {add.name} stopped at spawn point");
        }
        
        // Wait for destroy delay, then destroy (both drones and crawlers)
        yield return WaitForSecondsCache.Get(AddFleeDestroyDelay);
        
        if (add == null || !add.activeInHierarchy) yield break;
        
        // Remove from fleeing tracking and destroy
        fleeingAdds.Remove(add);
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Destroying fleeing {(isDrone ? "drone" : "crawler")} {add.name}");
        add.SetActive(false);
        
        if (isDrone)
        {
            dronePool.Enqueue(add);
        }
        else
        {
            crawlerPool.Enqueue(add);
        }
    }
    
    /// <summary>
    /// Finds the nearest spawn point from the given position.
    /// </summary>
    private Transform FindNearestSpawnPoint(Vector3 position, Transform[] spawnPoints)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return null;
        
        Transform nearest = null;
        float nearestDist = float.MaxValue;
        
        // Get arena center for path scoring (prefer paths that don't go through center)
        Vector3 arenaCenter = Vector3.zero;
        bool hasArenaManager = false;
        var arenaManager = GetComponent<BossArenaManager>();
        if (arenaManager != null)
        {
            arenaCenter = arenaManager.GetArenaCenter();
            hasArenaManager = true;
        }
        
        foreach (var sp in spawnPoints)
        {
            if (sp == null) continue;
            
            float dist = Vector3.Distance(position, sp.position);
            
            // Penalty for paths that go through arena center
            // This encourages enemies to go around the edges instead of through the middle
            if (hasArenaManager)
            {
                Vector3 midpoint = (position + sp.position) * 0.5f;
                float midpointToCenter = Vector3.Distance(new Vector3(midpoint.x, 0, midpoint.z), 
                                                           new Vector3(arenaCenter.x, 0, arenaCenter.z));
                
                // If the midpoint of the path is very close to arena center, add a penalty
                // This biases toward spawn points that don't require crossing the center
                if (midpointToCenter < 10f) // Within 10m of center
                {
                    float penalty = (10f - midpointToCenter) * 2f; // Up to 20m penalty
                    dist += penalty;
                }
            }
            
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = sp;
            }
        }
        
        return nearest;
    }
    
    
    /// <summary>
    /// Called when cage match starts (walls go up). Despawns ALL active adds.
    /// During the cage match, no adds should be present - it's a 1v1 with the boss.
    /// </summary>
    public void OnCageMatchStart()
    {
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), "[BossRoombaController] OnCageMatchStart - despawning ALL active adds for 1v1 cage match");
        
        int despawnedDrones = 0;
        int despawnedCrawlers = 0;
        
        // Despawn ALL active drones
        foreach (var kvp in activeDrones)
        {
            if (kvp.Value != null && kvp.Value.activeInHierarchy)
            {
                // Return to pool
                kvp.Value.SetActive(false);
                dronePool.Enqueue(kvp.Value);
                despawnedDrones++;
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Despawned drone for cage match: {kvp.Value.name}");
            }
        }
        
        // Despawn ALL active crawlers
        foreach (var kvp in activeCrawlers)
        {
            if (kvp.Value != null && kvp.Value.activeInHierarchy)
            {
                // Return to pool
                kvp.Value.SetActive(false);
                crawlerPool.Enqueue(kvp.Value);
                despawnedCrawlers++;
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Despawned crawler for cage match: {kvp.Value.name}");
            }
        }
        
        // Clear fleeing tracking since all adds are despawned
        fleeingAdds.Clear();
        
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Cage match started - despawned {despawnedDrones} drones and {despawnedCrawlers} crawlers");
    }
    
    /// <summary>
    /// Initialize alarm health on Awake/Start.
    /// </summary>
    private void InitializeAlarmHealth()
    {
        alarmCurrentHealth = AlarmMaxHealth;
        alarmDestroyed = false;
    }

    private IEnumerator ManageSpawnsRoutine()
    {
        // Initial spawn wave - spawn enemies in staggered order based on SpawnOrder setting
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Initial spawn wave starting... SpawnOrder={SpawnOrder}");
        
        // Spawn first wave based on priority
        if (SpawnOrder == SpawnOrderPriority.DronesFirst)
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), "[BossRoombaController] Spawning drones first...");
            RespawnDeadEnemies(activeDrones, droneSpawnPoints, dronePrefab, maxDrones, dronePool);
        }
        else
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), "[BossRoombaController] Spawning crawlers first...");
            RespawnDeadEnemies(activeCrawlers, crawlerSpawnPoints, crawlerPrefab, maxCrawlers, crawlerPool);
        }
        
        // Wait for second wave delay
        float secondWaveDelay = Random.Range(SecondWaveDelayRange.x, SecondWaveDelayRange.y);
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Waiting {secondWaveDelay:F1}s before second wave...");
        yield return WaitForSecondsCache.Get(secondWaveDelay);
        
        // Check if alarm is still active before spawning second wave
        if (!alarmActivated || alarmDestroyed)
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), "[BossRoombaController] Alarm no longer active, skipping second wave.");
            yield break;
        }
        
        // Spawn second wave (the other enemy type)
        if (SpawnOrder == SpawnOrderPriority.DronesFirst)
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), "[BossRoombaController] Spawning crawlers (second wave)...");
            RespawnDeadEnemies(activeCrawlers, crawlerSpawnPoints, crawlerPrefab, maxCrawlers, crawlerPool);
        }
        else
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), "[BossRoombaController] Spawning drones (second wave)...");
            RespawnDeadEnemies(activeDrones, droneSpawnPoints, dronePrefab, maxDrones, dronePool);
        }
        
        // Respawn check cadence
        var wait = WaitForSecondsCache.Get(2f);
        
        while (alarmActivated && !alarmDestroyed)
        {
            yield return wait;
            
            // Check and respawn any dead enemies
            RespawnDeadEnemies(activeDrones, droneSpawnPoints, dronePrefab, maxDrones, dronePool);
            RespawnDeadEnemies(activeCrawlers, crawlerSpawnPoints, crawlerPrefab, maxCrawlers, crawlerPool);
            
            // Ensure all active enemies continue chasing player (backup for state machine issues)
            // SKIP enemies that are currently fleeing to spawn points!
            if (player != null)
            {
                // Update crawler destinations
                foreach (var kvp in activeCrawlers)
                {
                    if (kvp.Value != null && kvp.Value.activeInHierarchy)
                    {
                        // Skip if this add is fleeing
                        if (fleeingAdds.Contains(kvp.Value)) continue;
                        
                        var crawler = kvp.Value.GetComponent<BaseCrawlerEnemy>();
                        if (crawler != null && crawler.agent != null && crawler.agent.enabled && crawler.agent.isOnNavMesh)
                        {
                            crawler.agent.isStopped = false;
                            crawler.agent.SetDestination(player.position);
                        }
                    }
                }
                
                // Update drone destinations - drones need continuous destination updates too
                foreach (var kvp in activeDrones)
                {
                    if (kvp.Value != null && kvp.Value.activeInHierarchy)
                    {
                        // Skip if this add is fleeing
                        if (fleeingAdds.Contains(kvp.Value)) continue;
                        
                        var drone = kvp.Value.GetComponent<DroneEnemy>();
                        if (drone != null && drone.agent != null && drone.agent.enabled && drone.agent.isOnNavMesh)
                        {
                            float distToPlayer = Vector3.Distance(drone.transform.position, player.position);
                            
                            // If drone is in Chase state and in attack range, transition to Fire
                            if (drone.enemyAI != null && drone.enemyAI.State == DroneState.Chase)
                            {
                                if (distToPlayer <= drone.attackRange)
                                {
                                    drone.TryFireTriggerByName("InAttackRange");
                                }
                                else
                                {
                                    drone.agent.isStopped = false;
                                    drone.agent.SetDestination(player.position);
                                }
                            }
                            // If drone is in Fire state but too far, go back to Chase
                            else if (drone.enemyAI != null && drone.enemyAI.State == DroneState.Fire)
                            {
                                if (distToPlayer > drone.chaseRange)
                                {
                                    drone.TryFireTriggerByName("OutOfAttackRange");
                                }
                                // Fire state handles its own shooting logic
                            }
                            // If drone is stuck in Idle, try to transition it to Chase
                            else if (drone.enemyAI != null && drone.enemyAI.State == DroneState.Idle)
                            {
                                drone.TryFireTriggerByName("SeePlayer");
                                drone.agent.isStopped = false;
                                drone.agent.SetDestination(player.position);
                            }
                        }
                    }
                }
            }
        }
    }

    private void RespawnDeadEnemies(Dictionary<Transform, GameObject> activeEnemies, Transform[] spawnPoints, 
        GameObject prefab, int maxCount, Queue<GameObject> pool)
    {
        if (prefab == null || spawnPoints == null || spawnPoints.Length == 0) return;

        var deadSpawnPoints = new List<Transform>();
        foreach (var kvp in activeEnemies)
        {
            if (kvp.Value == null || !kvp.Value.activeInHierarchy)
            {
                deadSpawnPoints.Add(kvp.Key);
            }
        }

        foreach (var sp in deadSpawnPoints)
        {
            activeEnemies.Remove(sp);
        }

        int currentCount = activeEnemies.Count;
        int toSpawn = Mathf.Min(maxCount - currentCount, spawnPoints.Length);

        for (int i = 0; i < toSpawn; i++)
        {
            Transform spawnPoint = null;
            foreach (var sp in spawnPoints)
            {
                if (!activeEnemies.ContainsKey(sp))
                {
                    spawnPoint = sp;
                    break;
                }
            }

            if (spawnPoint != null)
            {
                var enemy = SpawnEnemy(prefab, spawnPoint.position, pool);
                if (enemy != null)
                {
                    activeEnemies[spawnPoint] = enemy;
                }
            }
        }
    }

    private GameObject SpawnEnemy(GameObject prefab, Vector3 position, Queue<GameObject> pool)
    {
        GameObject enemy = null;

        while (pool.Count > 0)
        {
            enemy = pool.Dequeue();
            if (enemy != null) break;
        }

        if (enemy == null)
        {
            enemy = Instantiate(prefab);
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Instantiated new {prefab.name}");
        }
        else
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Reused pooled {enemy.name}");
        }

        enemy.transform.position = position;
        enemy.transform.rotation = Quaternion.identity;
        enemy.SetActive(true);
        
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Spawned {enemy.name} at {position}");

        RegisterSpawned(enemy);

        return enemy;
    }

    public void TriggerSpawnWave(int totalDrones, int totalCrawlers)
    {
        StartCoroutine(SpawnAddsCoroutine(totalDrones, totalCrawlers));
    }

    private IEnumerator SpawnAddsCoroutine(int totalDrones, int totalCrawlers)
    {
        var drones = (droneSpawnPoints != null && droneSpawnPoints.Length > 0) ? droneSpawnPoints : pocketSpawnPoints;
        var crawlers = (crawlerSpawnPoints != null && crawlerSpawnPoints.Length > 0) ? crawlerSpawnPoints : pocketSpawnPoints;

        if (dronePrefab != null && drones != null && drones.Length > 0 && totalDrones > 0)
        {
            for (int i = 0; i < totalDrones; i++)
            {
                var p = drones[i % drones.Length];
                if (p == null) continue;
                var g = SpawnEnemy(dronePrefab, p.position, dronePool);
                yield return null;
            }
        }

        if (crawlerPrefab != null && crawlers != null && crawlers.Length > 0 && totalCrawlers > 0)
        {
            for (int j = 0; j < totalCrawlers; j++)
            {
                var p = crawlers[j % crawlers.Length];
                if (p == null) continue;
                var c = SpawnEnemy(crawlerPrefab, p.position, crawlerPool);
                yield return null;
            }
        }
    }

    private void RegisterSpawned(GameObject g)
    {
        var agentComp = g.GetComponent<NavMeshAgent>();
        if (agentComp != null && EnemyBehavior.Crowd.CrowdController.Instance != null)
        {
            var ca = new EnemyBehavior.Crowd.CrowdAgent() { Agent = agentComp, Profile = profile };
            EnemyBehavior.Crowd.CrowdController.Instance.Register(ca);
        }
        
        // Configure crawlers spawned by the boss alarm to skip Ambush state and go directly to Chase
        // Check both on root and children (some prefabs have the component on a child)
        var crawler = g.GetComponent<BaseCrawlerEnemy>();
        if (crawler == null)
            crawler = g.GetComponentInChildren<BaseCrawlerEnemy>();
            
        if (crawler != null)
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Found BaseCrawlerEnemy on {g.name}, configuring for boss fight...");
            
            // Clear pocket reference - boss-spawned crawlers don't have a pocket to return to
            crawler.Pocket = null;
            
            // Force them to chase the player directly (no swarming/ambush behavior for boss adds)
            crawler.ForceChasePlayer = true;
            crawler.enableSwarmBehavior = false;
            
            // Reset health if the crawler has an IHealthSystem
            var healthSystem = crawler as IHealthSystem;
            if (healthSystem != null)
            {
                crawler.SetHealth(crawler.maxHealth);
            }
            
            // Reinitialize state machine for re-pooled enemies
            ResetCrawlerState(crawler);
            
            // Ensure the NavMeshAgent is properly configured - check crawler's own agent first
            var crawlerAgent = crawler.GetComponent<NavMeshAgent>();
            if (crawlerAgent == null)
                crawlerAgent = crawler.GetComponentInParent<NavMeshAgent>();
            if (crawlerAgent == null)
                crawlerAgent = agentComp;
                
            if (crawlerAgent != null)
            {
                crawlerAgent.enabled = true;
                if (crawlerAgent.isOnNavMesh && player != null)
                {
                    crawlerAgent.SetDestination(player.position);
                    EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Set crawler destination to player");
                }
                else
                {
                    EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] Crawler agent not on NavMesh: isOnNavMesh={crawlerAgent.isOnNavMesh}");
                }
            }
            
            
            // Force the crawler to skip Ambush and go to Chase after a frame
            // (this allows the state machine to be fully initialized)
            StartCoroutine(ForceCrawlerToChase(crawler));
        }
        else
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] No BaseCrawlerEnemy found on {g.name} - checking for DroneEnemy...");
            
            // Try to configure drones to chase the player
            var drone = g.GetComponent<DroneEnemy>();
            if (drone == null)
                drone = g.GetComponentInChildren<DroneEnemy>();
                
            if (drone != null)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Found DroneEnemy on {g.name}, configuring for boss fight...");
                
                // Set the player reference so the drone can track them
                if (player != null)
                {
                    drone.PlayerTarget = player;
                }
                
                // Trigger the drone to see the player and start chasing
                StartCoroutine(ForceDroneToChase(drone));
            }
            else
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] No DroneEnemy found on {g.name} either");
            }
        }
    }
    
    /// <summary>
    /// Resets a crawler's state for re-use from the pool.
    /// </summary>
    private void ResetCrawlerState(BaseCrawlerEnemy crawler)
    {
        if (crawler == null) return;
        
        // Reset health
        crawler.SetHealth(crawler.maxHealth);
        
        // Reset any death/low health flags
        crawler.hasFiredLowHealth = false;
        
        // CRITICAL: Force the state machine back to Ambush so we can transition to Chase
        // The enemyAI might be in Death or some other state from previous use
        if (crawler.enemyAI != null)
        {
            // Use reflection or a public method to reset state if needed
            // For now, we'll let ForceCrawlerToChase handle the transition
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] ResetCrawlerState: {crawler.gameObject.name} current state = {crawler.enemyAI.State}");
        }
    }
    
    private IEnumerator ForceCrawlerToChase(BaseCrawlerEnemy crawler)
    {
        // Wait a frame for full initialization
        yield return null;
        
        if (crawler == null || crawler.gameObject == null)
        {
            EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), "[BossRoombaController] ForceCrawlerToChase: crawler is null after frame wait");
            yield break;
        }
        
        // Debug current state
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] ForceCrawlerToChase: {crawler.gameObject.name} state={crawler.enemyAI?.State}, agent.enabled={crawler.agent?.enabled}, isOnNavMesh={crawler.agent?.isOnNavMesh}");
        
        // Ensure NavMeshAgent is on NavMesh
        if (crawler.agent != null && !crawler.agent.isOnNavMesh)
        {
            EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] {crawler.gameObject.name} not on NavMesh! Attempting to warp...");
            
            // Try to sample a valid position nearby
            if (UnityEngine.AI.NavMesh.SamplePosition(crawler.transform.position, out var hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                crawler.agent.Warp(hit.position);
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Warped {crawler.gameObject.name} to {hit.position}");
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogError($"[BossRoombaController] Could not find valid NavMesh position for {crawler.gameObject.name}!");
                yield break;
            }
        }
        
        // Ensure agent is enabled and not stopped
        if (crawler.agent != null)
        {
            crawler.agent.enabled = true;
            crawler.agent.isStopped = false;
        }
        
        // CRITICAL: Set PlayerTarget BEFORE state transition so ChaseBehavior.OnEnter can capture it
        if (player != null)
        {
            crawler.PlayerTarget = player;
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Set {crawler.gameObject.name} PlayerTarget to {player.name} BEFORE state transition");
        }
        else
        {
            TryCachePlayerReference(logIfMissing: true);
            if (player != null)
            {
                crawler.PlayerTarget = player;
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Late-resolved player and assigned PlayerTarget to {crawler.gameObject.name}");
            }
            else
            {
            EnemyBehaviorDebugLogBools.LogError($"[BossRoombaController] player reference is NULL! Crawlers won't be able to chase.");
            yield break;
            }
        }
        
        // Handle different starting states - we need to get the crawler into Chase
        if (crawler.enemyAI != null)
        {
            var currentState = crawler.enemyAI.State;
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Crawler {crawler.gameObject.name} is in state: {currentState}");
            
            // If in Ambush, fire LosePlayer to go to Chase
            if (currentState == CrawlerEnemyState.Ambush)
            {
                bool fired = crawler.TryFireTriggerByName("LosePlayer");
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Fired LosePlayer trigger: {fired}");
                
                // IMPORTANT: Also directly set the destination immediately, don't wait for the behavior
                // This ensures the crawler starts moving even if the state transition is delayed
                if (crawler.agent != null && crawler.agent.enabled && crawler.agent.isOnNavMesh && player != null)
                {
                    crawler.agent.isStopped = false;
                    crawler.agent.SetDestination(player.position);
                    EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Set immediate destination for {crawler.gameObject.name}");
                }
            }
            // If in Death state, we need to reinitialize
            else if (currentState == CrawlerEnemyState.Death)
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] Crawler {crawler.gameObject.name} is in Death state! Cannot force to Chase - needs full reset.");
                // The crawler should have been properly reset before spawning
                yield break;
            }
            // If already in Chase, just ensure destination is set
            else if (currentState == CrawlerEnemyState.Chase)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Crawler already in Chase state");
            }
            // For other states (Swarm, Attack, Flee), try to transition to Chase via LosePlayer
            else
            {
                bool fired = crawler.TryFireTriggerByName("LosePlayer");
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Attempted LosePlayer trigger from {currentState}: {fired}");
            }
            
            // Wait another frame for state transition
            yield return null;
            
            // Verify and set destination
            if (crawler.enemyAI.State == CrawlerEnemyState.Chase || 
                crawler.enemyAI.State == CrawlerEnemyState.Attack ||
                crawler.enemyAI.State == CrawlerEnemyState.Swarm)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] SUCCESS: {crawler.gameObject.name} is now in state {crawler.enemyAI.State}");
                
                // Ensure destination is set (PlayerTarget was already set before transition)
                if (crawler.agent != null && crawler.agent.isOnNavMesh && player != null)
                {
                    crawler.agent.SetDestination(player.position);
                    EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Set {crawler.gameObject.name} destination to player at {player.position}");
                }
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] {crawler.gameObject.name} still in state {crawler.enemyAI.State} - manually starting chase behavior");
                
                // Last resort: directly start the chase behavior
                if (crawler.agent != null && crawler.agent.isOnNavMesh && player != null)
                {
                    crawler.agent.isStopped = false;
                    crawler.agent.SetDestination(player.position);
                }
            }
        }
    }
    
    /// <summary>
    /// Forces a drone to start chasing the player immediately after spawn.
    /// </summary>
    private IEnumerator ForceDroneToChase(DroneEnemy drone)
    {
        // Wait a frame for full initialization
        yield return null;
        
        if (drone == null || drone.gameObject == null)
        {
            EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), "[BossRoombaController] ForceDroneToChase: drone is null after frame wait");
            yield break;
        }
        
        // Debug current state
        EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] ForceDroneToChase: {drone.gameObject.name} state={drone.enemyAI?.State}, agent.enabled={drone.agent?.enabled}, isOnNavMesh={drone.agent?.isOnNavMesh}");
        
        // Ensure NavMeshAgent is on NavMesh
        if (drone.agent != null && !drone.agent.isOnNavMesh)
        {
            EnemyBehaviorDebugLogBools.LogWarning(nameof(BossRoombaController), $"[BossRoombaController] {drone.gameObject.name} not on NavMesh! Attempting to warp...");
            
            // Try to sample a valid position nearby
            if (UnityEngine.AI.NavMesh.SamplePosition(drone.transform.position, out var hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                drone.agent.Warp(hit.position);
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Warped {drone.gameObject.name} to {hit.position}");
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogError($"[BossRoombaController] Could not find valid NavMesh position for {drone.gameObject.name}!");
                yield break;
            }
        }
        
        // Ensure agent is enabled and not stopped
        if (drone.agent != null)
        {
            drone.agent.enabled = true;
            drone.agent.isStopped = false;
        }
        
        // Set the player target
        if (player != null)
        {
            drone.PlayerTarget = player;
        }
        
        // Fire the SeePlayer trigger to transition from Idle to Chase
        if (drone.enemyAI != null && drone.enemyAI.State == DroneState.Idle)
        {
            bool fired = drone.TryFireTriggerByName("SeePlayer");
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Fired SeePlayer trigger on {drone.gameObject.name}: {fired}");
        }
        
        // Wait another frame for state transition
        yield return null;
        
        // Verify state
        if (drone.enemyAI != null)
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Drone {drone.gameObject.name} is now in state: {drone.enemyAI.State}");
            
            // Set destination to player
            if (drone.agent != null && drone.agent.isOnNavMesh && player != null)
            {
                drone.agent.SetDestination(player.position);
                EnemyBehaviorDebugLogBools.Log(nameof(BossRoombaController), $"[BossRoombaController] Set {drone.gameObject.name} destination to player at {player.position}");
            }
        }
    }

    public void BeginSuction(float duration)
    {
        StartCoroutine(SuctionCoroutine(duration));
    }

    private IEnumerator SuctionCoroutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            
            if (player != null)
            {
                var dir = (transform.position - player.position);
                float d = dir.magnitude;
                if (d < suctionRadius)
                {
                    var force = dir.normalized * (suctionStrength / Mathf.Max(1f, d));
                }
            }

            var coll = Physics.OverlapSphere(transform.position, suctionRadius);
            foreach (var c in coll)
            {
                if (c == null) continue;
                if (c.gameObject.CompareTag("Enemy"))
                {
                    var rb = c.attachedRigidbody;
                    if (rb != null)
                        rb.AddForce((transform.position - c.transform.position).normalized * suctionStrength * Time.deltaTime, ForceMode.VelocityChange);
                }
            }

            yield return null;
        }
    }
}
