// BaseCrawlerEnemy.cs
// Purpose: Base class for crawler-type enemies (ground-based) providing movement and crawler-specific behaviors.
// Works with: BaseEnemy, NavMeshAgent, CrowdController, EnemyStateMachineConfig.
// Notes: Extend to implement crawler-specific OnHit and movement.

using UnityEngine;
using Behaviors;
using System.Collections;
using System.Collections.Generic;

#region States and Triggers
public enum CrawlerEnemyState
{
    Ambush,
    Swarm,
    Chase,
    Attack,
    Flee,
    Death
}
public enum CrawlerEnemyTrigger
{
    SeePlayer,
    ReachSwarm,
    InAttackRange,
    OutOfAttackRange,
    LowHealth,
    Flee,
    Die,
    SwarmDefeated,
    ReinforcementsCalled,
    AmbushReady,
    LosePlayer
}
#endregion

public class BaseCrawlerEnemy : BaseEnemy<CrawlerEnemyState, CrawlerEnemyTrigger>, IPocketSpawnable
{
    // Reference to the pocket this crawler belongs to
    public CrawlerPocket Pocket { get; set; }

    // Behaviors
    private IEnemyStateBehavior<CrawlerEnemyState, CrawlerEnemyTrigger> ambushBehavior, swarmBehavior, fleeBehavior, chaseBehavior, attackBehavior, deathBehavior;

    private Coroutine bombAvoidanceBurstCoroutine;
    private Coroutine horseshoeReturnCoroutine;
    private Coroutine waitForSwarmManagerCoroutine;
    private Coroutine ensureChaseCoroutine;

    private bool wasBombAvoiding = false;

    [Header("IK / Helper Roots")]
    [SerializeField, Tooltip("Any helper GameObjects (like IK targets) that live outside this crawler's root. They will be destroyed with this crawler to prevent orphaned references.")]
    private List<GameObject> helperRootsToCleanup = new();

    [Header("Swarm Behavior")]
    [SerializeField, Tooltip("If false, this crawler ignores swarm behavior and acts independently.")]
    public bool enableSwarmBehavior = true;

    // Optional flag to prevent double registration
    private bool _isRegisteredWithSwarmManager = false;

    protected override void Awake()
    {
        // Ensure the pooled object includes any wrapper parents (e.g., crawler IK rig roots).
        AdoptWrapperParentsForPooling();
        base.Awake();

        foreach (var helper in helperRootsToCleanup)
        {
            RegisterExternalHelper(helper);
        }

        // Get or add required behaviors
        ambushBehavior = new AmbushBehavior<CrawlerEnemyState, CrawlerEnemyTrigger>();
        swarmBehavior = new SwarmBehavior<CrawlerEnemyState, CrawlerEnemyTrigger>();
        fleeBehavior = new FleeBehavior<CrawlerEnemyState, CrawlerEnemyTrigger>();
        chaseBehavior = new ChaseBehavior<CrawlerEnemyState, CrawlerEnemyTrigger>();
        attackBehavior = new AttackBehavior<CrawlerEnemyState, CrawlerEnemyTrigger>();
        deathBehavior = new DeathBehavior<CrawlerEnemyState, CrawlerEnemyTrigger>();

#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log(nameof(BaseCrawlerEnemy), $"{gameObject.name} Awake called");
#endif

        // Find the player - use PlayerPresenceManager if available
        if (PlayerPresenceManager.IsPlayerPresent)
            PlayerTarget = PlayerPresenceManager.PlayerTransform;
        else
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                PlayerTarget = playerObj.transform;
        }

        // Initialize state machine and health bar here
        InitializeStateMachine(CrawlerEnemyState.Ambush);
        ConfigureStateMachine();
        if (enemyAI.State.Equals(CrawlerEnemyState.Ambush))
        {
            ambushBehavior.OnEnter(this);
        }

        EnsureHealthBarBinding();

        if (bombAvoidanceBurstCoroutine != null)
            StopCoroutine(bombAvoidanceBurstCoroutine);
        bombAvoidanceBurstCoroutine = StartCoroutine(BombAvoidanceBurst());

        ensureChaseCoroutine = StartCoroutine(EnsureChaseIfNoPocket());
        // Note: SwarmManager registration is handled in OnEnable() to avoid spam during pool prewarming
    }

    private IEnumerator BombAvoidanceBurst()
    {
        float timer = 1.5f;
        while (timer > 0f)
        {
            ApplySeparation();
            timer -= Time.deltaTime;
            yield return null;
        }
    }

    protected virtual void OnEnable()
    {
        // Restart the coroutine if the object is re-enabled
        if (bombAvoidanceBurstCoroutine != null)
            StopCoroutine(bombAvoidanceBurstCoroutine);
        bombAvoidanceBurstCoroutine = StartCoroutine(BombAvoidanceBurst());

        TryAutoRegisterWithSwarmManager();
    }

    protected virtual void OnDisable()
    {
        // Optionally stop the coroutine when disabled
        if (bombAvoidanceBurstCoroutine != null)
            StopCoroutine(bombAvoidanceBurstCoroutine);

        if (waitForSwarmManagerCoroutine != null)
        {
            StopCoroutine(waitForSwarmManagerCoroutine);
            waitForSwarmManagerCoroutine = null;
        }

        UnregisterFromSwarmManager();
    }

    protected virtual void LateUpdate()
    {
        // Ensure separation is applied every frame
        ApplySeparation();
    }

    public Vector3 ClusterTarget { get; set; }

    // Separation behavior to avoid clustering too tightly
    public void ApplySeparation()
    {
        float minSeparation = 1.0f;
        Vector3 separation = Vector3.zero;
        int count = 0;

        // Only apply separation if swarm behavior is enabled
        if (enableSwarmBehavior && SwarmManager.Instance != null)
        {
            foreach (var other in SwarmManager.Instance.GetActiveCrawlers())
            {
                if (other == this) continue;
                float dist = Vector3.Distance(transform.position, other.transform.position);
                if (dist < minSeparation)
                {
                    separation += (transform.position - other.transform.position).normalized * (minSeparation - dist);
                    count++;
                }
            }
        }

        var bombs = GameObject.FindObjectsByType<BombCarrierEnemy>(FindObjectsSortMode.None);
        bool bombAvoided = false;

        // 50/50 chance: true = alarm-based, false = pocket-based
        bool useAlarmBased = Random.value < 0.5f;

        foreach (var bomb in bombs)
        {
            if (bomb == this) continue;
            float bombRadius = bomb.explosionRadius * 1.05f;
            float distToBomb = Vector3.Distance(transform.position, bomb.transform.position);
            float distBombToPlayer = PlayerTarget != null ? Vector3.Distance(bomb.transform.position, PlayerTarget.position) : float.MaxValue;

            if (useAlarmBased)
            {
                // Alarm-based logic
                if (bomb.IsAttacking && PlayerTarget != null)
                {
                    float distToPlayer = Vector3.Distance(transform.position, PlayerTarget.position);
                    float fleeLogicRadius = 12f;
                    if (distToPlayer < fleeLogicRadius)
                    {
                        Vector3 awayFromBomb = (transform.position - bomb.transform.position).normalized;
                        Vector3 awayFromPlayer = (transform.position - PlayerTarget.position).normalized;
                        Vector3 fleeDir = (awayFromBomb + awayFromPlayer).normalized;
                        Vector3 safePos = transform.position + fleeDir * (bombRadius + 2f);

                        if (agent != null && agent.enabled && agent.isOnNavMesh)
                        {
                            agent.SetDestination(safePos);
                            bombAvoided = true;
                            EnemyBehaviorDebugLogBools.Log(nameof(BaseCrawlerEnemy), $"{gameObject.name} (alarm-based avoid) is fleeing attacking bomb {bomb.gameObject.name} away from player");
                            if (horseshoeReturnCoroutine != null)
                            {
                                StopCoroutine(horseshoeReturnCoroutine);
                                horseshoeReturnCoroutine = null;
                            }
                            break;
                        }
                    }
                }
            }
            else
            {
                // Pocket-based logic (original default)
                if (distBombToPlayer < bombPanicPlayerDistance && distToBomb < bombRadius && Random.value > bombAvoidanceIgnoreChance)
                {
                    Vector3 away = (transform.position - bomb.transform.position).normalized;
                    Vector3 jitter = new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
                    Vector3 safePos = bomb.transform.position + away * (bombRadius + 0.5f) + jitter;

                    if (!bombAvoidanceTarget.HasValue)
                        bombAvoidanceTarget = safePos;
                    else
                        bombAvoidanceTarget = Vector3.Lerp(bombAvoidanceTarget.Value, safePos, Time.deltaTime * bombAvoidanceLerpSpeed);

                    if (agent != null && agent.enabled && agent.isOnNavMesh && Vector3.Distance(agent.destination, bombAvoidanceTarget.Value) > 0.5f)
                    {
                        agent.SetDestination(bombAvoidanceTarget.Value);
                        bombAvoided = true;
                        if (horseshoeReturnCoroutine != null)
                        {
                            StopCoroutine(horseshoeReturnCoroutine);
                            horseshoeReturnCoroutine = null;
                        }
                        break;
                    }
                }
            }
        }

        if (!bombAvoided)
        {
            bombAvoidanceTarget = null;
            if (count > 0)
            {
                separation /= count;
                float maxSeparation = 1.0f;
                if (separation.magnitude > maxSeparation)
                    separation = separation.normalized * maxSeparation;
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                    agent.Move(separation);
            }

            // Trigger horseshoe return if we were avoiding last frame and now are not
            if (wasBombAvoiding && horseshoeReturnCoroutine == null && PlayerTarget != null)
            {
                horseshoeReturnCoroutine = StartCoroutine(HorseshoeReturnToPlayer());
            }
        }
        else
        {
            // If currently avoiding, stop any horseshoe return in progress
            if (horseshoeReturnCoroutine != null)
            {
                StopCoroutine(horseshoeReturnCoroutine);
                horseshoeReturnCoroutine = null;
            }
        }

        // Update avoidance state for next frame
        wasBombAvoiding = bombAvoided;
    }

    // Position of the pocket (for fleeing)
    public Vector3 PocketPosition => Pocket != null ? Pocket.transform.position : Vector3.zero;

    [Header("Flee Settings")]
    // Distance at which the enemy will start fleeing from the player
    [SerializeField, Tooltip("Distance from pocket at which the crawler will flee.")]
    public float fleeDistanceFromPocket = 20f;

    [SerializeField, Tooltip("If the player is within this distance of a bomb, crawlers will avoid the bomb's explosion radius.")]
    private float bombPanicPlayerDistance = 6f; // Adjust as needed

    [SerializeField, Range(0f, 1f), Tooltip("Chance (0-1) that a crawler will ignore bomb avoidance in a given frame.")]
    private float bombAvoidanceIgnoreChance = 0.2f;

    [SerializeField, Tooltip("How quickly crawlers update their bomb avoidance destination (lower = laggier).")]
    private float bombAvoidanceLerpSpeed = 3f;

    private Vector3? bombAvoidanceTarget = null;

    protected override void ConfigureStateMachine()
    {
        // Ambush: Cluster near pocket, prepare to swarm
        enemyAI.Configure(CrawlerEnemyState.Ambush)
            .OnEntry(() =>
            {
                ambushBehavior?.OnEnter(this);
                PlayIdleAnim();
            })
            .OnExit(() => ambushBehavior?.OnExit(this))
            .Permit(CrawlerEnemyTrigger.AmbushReady, CrawlerEnemyState.Swarm)
            .Permit(CrawlerEnemyTrigger.LosePlayer, CrawlerEnemyState.Chase)
            .Permit(CrawlerEnemyTrigger.Flee, CrawlerEnemyState.Flee)
            .Permit(CrawlerEnemyTrigger.Die, CrawlerEnemyState.Death)
            .Ignore(CrawlerEnemyTrigger.SeePlayer);

        // Swarm: Blob moves toward/surrounds player, attack when close
        enemyAI.Configure(CrawlerEnemyState.Swarm)
            .OnEntry(() =>
            {
                swarmBehavior?.OnEnter(this);
                // Crawlers stay on idle loop while sliding
            })
            .OnExit(() => swarmBehavior?.OnExit(this))
            .Permit(CrawlerEnemyTrigger.InAttackRange, CrawlerEnemyState.Attack)
            .Permit(CrawlerEnemyTrigger.LosePlayer, CrawlerEnemyState.Chase)
            .Permit(CrawlerEnemyTrigger.Flee, CrawlerEnemyState.Flee)
            .Permit(CrawlerEnemyTrigger.SwarmDefeated, CrawlerEnemyState.Flee)
            .Permit(CrawlerEnemyTrigger.Die, CrawlerEnemyState.Death)
            .Ignore(CrawlerEnemyTrigger.AmbushReady)
            .Ignore(CrawlerEnemyTrigger.SeePlayer);

        // Chase: Blob chases player, regroup to swarm when close
        enemyAI.Configure(CrawlerEnemyState.Chase)
            .OnEntry(() =>
            {
                chaseBehavior?.OnEnter(this);
                // No locomotion anim for crawler; keep idle playing
            })
            .OnExit(() => chaseBehavior?.OnExit(this))
            .Permit(CrawlerEnemyTrigger.InAttackRange, CrawlerEnemyState.Attack)
            .Permit(CrawlerEnemyTrigger.ReachSwarm, CrawlerEnemyState.Swarm)
            .Permit(CrawlerEnemyTrigger.AmbushReady, CrawlerEnemyState.Swarm)
            .Permit(CrawlerEnemyTrigger.Flee, CrawlerEnemyState.Flee)
            .Permit(CrawlerEnemyTrigger.Die, CrawlerEnemyState.Death)
            .Ignore(CrawlerEnemyTrigger.SeePlayer);

        // Attack: Attack logic, return to swarm or flee as needed
        enemyAI.Configure(CrawlerEnemyState.Attack)
            .OnEntry(() =>
            {
                attackBehavior?.OnEnter(this);
                PlayAttackAnim();
            })
            .OnExit(() => attackBehavior?.OnExit(this))
            .Permit(CrawlerEnemyTrigger.OutOfAttackRange, CrawlerEnemyState.Swarm)
            .Permit(CrawlerEnemyTrigger.LowHealth, CrawlerEnemyState.Flee)
            .Permit(CrawlerEnemyTrigger.LosePlayer, CrawlerEnemyState.Chase)
            .Permit(CrawlerEnemyTrigger.Die, CrawlerEnemyState.Death)
            .Permit(CrawlerEnemyTrigger.Flee, CrawlerEnemyState.Flee)
            .Ignore(CrawlerEnemyTrigger.SeePlayer);

        // Flee: Retreat to pocket, but allow chasing again if player is seen
        enemyAI.Configure(CrawlerEnemyState.Flee)
            .OnEntry(() =>
            {
                fleeBehavior?.OnEnter(this);
                // Remain on idle pose while sliding away
            })
            .OnExit(() => fleeBehavior?.OnExit(this))
            .Permit(CrawlerEnemyTrigger.SeePlayer, CrawlerEnemyState.Chase)
            .Permit(CrawlerEnemyTrigger.Die, CrawlerEnemyState.Death)
            .Ignore(CrawlerEnemyTrigger.AmbushReady)
            .Ignore(CrawlerEnemyTrigger.ReachSwarm)
            .Ignore(CrawlerEnemyTrigger.InAttackRange)
            .Ignore(CrawlerEnemyTrigger.OutOfAttackRange)
            .Ignore(CrawlerEnemyTrigger.LowHealth)
            .Ignore(CrawlerEnemyTrigger.Flee)
            .Ignore(CrawlerEnemyTrigger.SwarmDefeated)
            .Ignore(CrawlerEnemyTrigger.ReinforcementsCalled)
            .Ignore(CrawlerEnemyTrigger.LosePlayer);

        // Death: Final state
        enemyAI.Configure(CrawlerEnemyState.Death)
            .OnEntry(() => deathBehavior?.OnEnter(this))
            .OnExit(() => deathBehavior?.OnExit(this))
            .Ignore(CrawlerEnemyTrigger.LowHealth)
            .Ignore(CrawlerEnemyTrigger.Die)
            .Ignore(CrawlerEnemyTrigger.SeePlayer);
    }

    public bool IsClustered(float threshold = 0.5f)
    {
        return Vector3.Distance(transform.position, ClusterTarget) <= threshold;
    }

    [HideInInspector]
    public BaseCrawlerEnemy originalPrefab;

    public event System.Action<BaseCrawlerEnemy> OnCrawlerDeathOrReturn;

    public void OnReturnedToPocket()
    {
        UnregisterFromSwarmManager();
        OnCrawlerDeathOrReturn?.Invoke(this);
        Destroy(gameObject);
    }

    public System.Action OnDestroyCallback;

    protected override void OnDestroy()
    {
        OnDestroyCallback?.Invoke();

        if (horseshoeReturnCoroutine != null)
        {
            StopCoroutine(horseshoeReturnCoroutine);
            horseshoeReturnCoroutine = null;
        }
        if (Pocket != null)
        {
            Pocket.RemoveFromActiveLists(this);
        }
        // Use the unregister method
        UnregisterFromSwarmManager();
        OnCrawlerDeathOrReturn?.Invoke(this);

        base.OnDestroy();
    }

    protected override void OnTriggerStay(Collider other)
    {
        // Prevent alarm-spawned crawlers from fleeing/returning to pocket while alarm bot exists
        if (AlarmSource != null && AlarmSource.gameObject != null)
            return;

        if (enemyAI == null || Pocket == null || PlayerTarget == null)
            return;

        if (other.CompareTag("Player"))
        {
            // Prevent firing triggers if already dead
            if (enemyAI.State == CrawlerEnemyState.Death)
                return;

            float playerDistFromPocket = Vector3.Distance(PlayerTarget.position, Pocket.transform.position);

            // If player is within flee distance, allow transition from Flee to Chase
            if (enemyAI.State == CrawlerEnemyState.Flee && playerDistFromPocket <= fleeDistanceFromPocket)
            {
                enemyAI.Fire(CrawlerEnemyTrigger.SeePlayer);
            }
            // If player is outside flee distance, force Flee state
            else if (playerDistFromPocket > fleeDistanceFromPocket && enemyAI.State != CrawlerEnemyState.Flee)
            {
                enemyAI.Fire(CrawlerEnemyTrigger.Flee);
            }
        }
    }

    public AlarmCarrierEnemy AlarmSource { get; set; }

    public void ForceEnterState(CrawlerEnemyState state)
    {
        // Set the private currentState field (if it exists)
        var stateField = typeof(BaseEnemy<CrawlerEnemyState, CrawlerEnemyTrigger>)
            .GetField("currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (stateField != null)
            stateField.SetValue(this, state);

        // Call the appropriate behavior's OnEnter
        switch (state)
        {
            case CrawlerEnemyState.Ambush:
                ambushBehavior?.OnEnter(this);
                break;
            case CrawlerEnemyState.Swarm:
                swarmBehavior?.OnEnter(this);
                break;
            case CrawlerEnemyState.Chase:
                chaseBehavior?.OnEnter(this);
                break;
            case CrawlerEnemyState.Attack:
                attackBehavior?.OnEnter(this);
                break;
            case CrawlerEnemyState.Flee:
                fleeBehavior?.OnEnter(this);
                break;
            case CrawlerEnemyState.Death:
                deathBehavior?.OnEnter(this);
                break;
        }
    }

    public bool ForceChasePlayer { get; set; } = false;

    private IEnumerator HorseshoeReturnToPlayer()
    {
        if (PlayerTarget == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
            yield break;

        EnemyBehaviorDebugLogBools.Log(nameof(BaseCrawlerEnemy), $"{gameObject.name} starting horseshoe (arc) return!");

        Vector3 start = transform.position;
        Vector3 toPlayer = (PlayerTarget.position - start).normalized;
        float radius = Vector3.Distance(start, PlayerTarget.position);
        float arcRadius = Mathf.Max(6f, radius); // Minimum arc radius for visible effect

        // Choose left or right for the arc
        int direction = Random.value > 0.5f ? 1 : -1;
        Vector3 perp = Vector3.Cross(toPlayer, Vector3.up).normalized * direction;

        float arcAngle = 90f; // degrees to sweep around the player (tweak for more/less horseshoe)
        float arcTime = 1.2f; // seconds to complete the arc (tweak for speed)
        float elapsed = 0f;

        // Center of the arc is the player
        Vector3 center = PlayerTarget.position;

        // Initial angle from center to start
        Vector3 fromCenter = (start - center).normalized;
        float startAngle = Mathf.Atan2(fromCenter.z, fromCenter.x);

        while (elapsed < arcTime)
        {
            float t = elapsed / arcTime;
            float angle = startAngle + Mathf.Deg2Rad * arcAngle * t * direction;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * arcRadius;
            Vector3 arcPos = center + offset;

            // Move agent toward arcPos
            if (agent.enabled && agent.isOnNavMesh)
                agent.SetDestination(arcPos);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // After arc, head directly to the player
        if (PlayerTarget != null && agent.enabled && agent.isOnNavMesh)
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BaseCrawlerEnemy), $"{gameObject.name} returning to player after horseshoe arc.");
            agent.SetDestination(PlayerTarget.position);
        }
        horseshoeReturnCoroutine = null;
    }

    public override bool TryFireTriggerByName(string triggerName)
    {
        if (!enableSwarmBehavior && enemyAI != null)
        {
            if (System.Enum.TryParse(triggerName, out CrawlerEnemyTrigger trigger))
            {
                // Block any trigger that can ever lead to Swarm or Ambush
                if (trigger == CrawlerEnemyTrigger.AmbushReady ||
                    trigger == CrawlerEnemyTrigger.ReachSwarm)
                {
                    return false;
                }

                // Remap OutOfAttackRange to LosePlayer if in Attack state and swarm is disabled
                if (trigger == CrawlerEnemyTrigger.OutOfAttackRange &&
                    enemyAI.State == CrawlerEnemyState.Attack)
                {
                    // Fire LosePlayer with a frame delay to avoid re-entrancy issues
                    StartCoroutine(FireLosePlayerNextFrame());
                    return true; // Indicate the trigger was "handled"
                }
            }
        }
        return base.TryFireTriggerByName(triggerName);
    }

    private IEnumerator FireLosePlayerNextFrame()
    {
        yield return null; // Wait one frame
        base.TryFireTriggerByName(CrawlerEnemyTrigger.LosePlayer.ToString());
    }

    private IEnumerator EnsureChaseIfNoPocket()
    {
        // Allow any spawner (like CrawlerPocket) a frame to assign the Pocket reference
        yield return null;

        if (Pocket == null || !enableSwarmBehavior)
        {
            ForceChasePlayer = true;
            if (enemyAI != null && enemyAI.State == CrawlerEnemyState.Ambush)
            {
                enemyAI.Fire(CrawlerEnemyTrigger.LosePlayer);
            }
        }

        ensureChaseCoroutine = null;
    }

    private void TryAutoRegisterWithSwarmManager()
    {
        if (!enableSwarmBehavior || _isRegisteredWithSwarmManager)
            return;

        if (SwarmManager.Instance != null)
        {
            RegisterWithSwarmManager();
        }
        else if (waitForSwarmManagerCoroutine == null)
        {
            waitForSwarmManagerCoroutine = StartCoroutine(WaitForSwarmManagerThenRegister());
        }
    }

    private IEnumerator WaitForSwarmManagerThenRegister()
    {
        const float timeoutSeconds = 3f;
        float timer = 0f;

        while (SwarmManager.Instance == null && timer < timeoutSeconds)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (SwarmManager.Instance != null)
        {
            RegisterWithSwarmManager();
        }

        waitForSwarmManagerCoroutine = null;
    }

    public void RegisterWithSwarmManager()
    {
        if (_isRegisteredWithSwarmManager || !enableSwarmBehavior || SwarmManager.Instance == null)
        {
            //EnemyBehaviorDebugLogBools.Log(nameof(BaseCrawlerEnemy), $"{gameObject.name}: RegisterWithSwarmManager skipped (already registered: {_isRegisteredWithSwarmManager}, enableSwarmBehavior: {enableSwarmBehavior}, SwarmManager.Instance null: {SwarmManager.Instance == null})");
            return;
        }

        SwarmManager.Instance.AddToSwarm(this);
        _isRegisteredWithSwarmManager = true;
        //EnemyBehaviorDebugLogBools.Log(nameof(BaseCrawlerEnemy), $"{gameObject.name}: Registered with SwarmManager");
    }

    public void UnregisterFromSwarmManager()
    {
        if (!_isRegisteredWithSwarmManager || SwarmManager.Instance == null)
        {
            //EnemyBehaviorDebugLogBools.Log(nameof(BaseCrawlerEnemy), $"{gameObject.name}: UnregisterFromSwarmManager skipped (not registered: {!_isRegisteredWithSwarmManager}, SwarmManager.Instance null: {SwarmManager.Instance == null})");
            return;
        }

        SwarmManager.Instance.RemoveFromSwarm(this);
        _isRegisteredWithSwarmManager = false;
        //EnemyBehaviorDebugLogBools.Log(nameof(BaseCrawlerEnemy), $"{gameObject.name}: Unregistered from SwarmManager");
    }
}
