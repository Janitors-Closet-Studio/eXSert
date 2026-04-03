// DroneEnemy.cs
// Purpose: Drone enemy implementation with flying movement and swarm behavior.
// Works with: DroneSwarmManager, FlowFieldService, CrowdController.

using UnityEngine;
using UnityEngine.AI;
using Behaviors;
using System.Collections;
using System.Collections.Generic;

public enum DroneState
{
    Idle,
    Relocate,
    Chase,
    Fire,
    Death
}
#pragma warning restore CS0414

public enum DroneTrigger
{
    SeePlayer,
    LosePlayer,
    InAttackRange,
    OutOfAttackRange,
    Die,
    RelocateComplete
}

[RequireComponent(typeof(NavMeshAgent))]
public class DroneEnemy : BaseEnemy<DroneState, DroneTrigger>, IProjectileShooter
{
    // ============= MEMORY LEAK DIAGNOSTIC - Renderer Toggle Only =============
    // The memory leak is caused by the Renderer. Toggle this to disable renderers for testing.
    private static bool s_disableRenderers = false;
    
    [Header("MEMORY LEAK DIAGNOSTIC")]
    [Tooltip("Check this box at RUNTIME to disable all drone renderers (stops the memory leak)")]
    [SerializeField] private bool _disableRenderers = false;
    
    // Cached component references for toggling
    private Renderer[] _cachedRenderers;
    private bool _lastRendererDisableState;
    
    private new void OnValidate()
    {
        // Sync renderer disable flag at runtime
        if (Application.isPlaying)
        {
            s_disableRenderers = _disableRenderers;
        }
    }
    
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticFlags()
    {
        s_disableRenderers = false;
    }
#endif
    // =========================================================

    [Header("Drone Settings")]
    [Tooltip("Desired vertical hover height above ground used for visuals/pathing.")]
    [SerializeField] private float hoverHeight = 5f;



    [Tooltip("Preferred combat radius. Drones enter Fire near this distance (with hysteresis).")]
    [SerializeField] public float attackRange = 15f;

    [Tooltip("Seconds between shots for this drone.")]
    [SerializeField] private float fireCooldown = 1.5f;

    [Tooltip("Projectile prefab spawned when firing. Should have a Rigidbody and EnemyProjectile component.")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Initial linear velocity magnitude applied to the projectile.")]
    [SerializeField] private float projectileSpeed = 20f;

    [Tooltip("Muzzle transform used as the spawn position and forward direction when firing.")]
    [SerializeField] private Transform firePoint;

    [Tooltip("Max distance to pursue the player before giving up and relocating.")]
    [SerializeField] public float chaseRange = 30f;

    [Tooltip("Number of projectiles pre-instantiated for this drone's object pool.")]
    [SerializeField] private int projectilePoolSize = 20;

    [Header("NavMesh edge handling")]
    [Tooltip("Radius used with NavMesh.SamplePosition when clamping desired destinations to the mesh.")]
    [SerializeField] private float navSampleRadius = 2.0f;

    [Tooltip("Max distance to shrink the formation radius toward the player when clamping off-mesh points.")]
    [SerializeField] private float navEdgeFallbackMaxShrink = 5.0f;

    [Header("Chase/Fire hysteresis and anti-stall")]
    [Tooltip("Extra distance added to attackRange for entering Fire. Helps near NavMesh edges.")]
    [SerializeField] private float fireEnterBuffer = 1.25f;

    [Tooltip("Extra distance added to attackRange before exiting Fire back to Chase. Prevents flip-flop.")]
    [SerializeField] private float fireExitBuffer = 2.0f;

    [Tooltip("Seconds with little NavMeshAgent progress before forcing Fire near target (anti-stall).")]
    [SerializeField] private float chaseStuckSeconds = 1.5f;

    [Header("Fire movement (discrete re-positioning)")]
    [Tooltip("Angle (degrees) to rotate the formation each re-position step while in Fire.")]
    [SerializeField] private float fireStepAngleDeg = 30f;

    [Tooltip("Minimum seconds between re-position assignments in Fire. Majority must arrive before this earliest time.")]
    [SerializeField] private float fireRepositionIntervalMin = 1.0f;

    [Tooltip("Maximum seconds between re-position assignments in Fire. A hard cap even if not all arrived.")]
    [SerializeField] private float fireRepositionIntervalMax = 2.0f;

    [Tooltip("Extra random seconds added/subtracted to the interval for variety.")]
    [SerializeField] private float fireRepositionJitter = 0.25f;

    [Tooltip("Distance within which a drone is considered to have reached its Fire target.")]
    [SerializeField] private float fireArrivalEpsilon = 0.6f;

    [Tooltip("Chance (0..1) that a Fire step will flip across the circle (180°), producing \"cross-over\" swaps.")]
    [SerializeField, Range(0f, 1f)] private float fireCrossSwapChance = 0.15f;

    [Header("Projectile/Firing")]
    [Tooltip("Spawn the projectile slightly forward from the muzzle to avoid self-collision at spawn.")]
    [SerializeField] private float muzzleForwardOffset = 0.1f;

    [Header("Aiming")]
    [Tooltip("Vertical aim offset to target the player's center/head. 0 = feet, ~0.8 = chest, ~1.5 = head.")]
    [SerializeField] private float aimYOffset = 0.8f;

    [Header("Aim Randomization")]
    [Tooltip("Chance [0..1] that a shot will intentionally miss left/right. 0 = always accurate, 1 = always miss.")]
    [SerializeField, Range(0f, 1f)] private float missChance = 0.15f;

    [Tooltip("Minimum degrees to offset when missing (use 0 for any amount up to Max).")]
    [SerializeField, Range(0f, 45f)] private float minMissAngleDeg = 0f;

    [Tooltip("Maximum degrees to offset when missing (yaw only).")]
    [SerializeField, Range(0f, 45f)] private float maxMissAngleDeg = 6f;

    [Header("Facing")]
    [Tooltip("Degrees/second to rotate toward the desired facing direction.")]
    [SerializeField] private float turnSpeed = 540f;
    [Tooltip("Minimum planar speed before we use agent velocity for facing.")]
    [SerializeField] private float velocityFacingThreshold = 0.2f;

    [Header("Hit Reaction")]
    [Tooltip("Seconds the drone is staggered (no firing) after being hit.")]
    [SerializeField] private float hitStaggerDuration = 0.1f;

    [Header("Death Physics Collapse")]
    [SerializeField, Tooltip("When enabled, drones that die from any source are put into simple rigidbody collapse instead of hovering before despawn.")]
    private bool applyPhysicsCollapseOnAnyDeath = true;
    [SerializeField, Tooltip("Downward velocity used for non-plunge death collapse.")]
    private float deathCollapseDownwardVelocity = 10f;
    [SerializeField, Tooltip("Horizontal push away from impact origin for non-plunge death collapse.")]
    private float deathCollapseRadialForce = 5f;
    [SerializeField, Range(0f, 1f), Tooltip("How long movement/AI updates are briefly paused after an aerial hit displacement.")]
    private float aerialHitDisplacementLockDuration = 0.15f;
    [SerializeField, Range(0.02f, 0.6f), Tooltip("Duration of the smooth aerial hit nudge movement.")]
    private float aerialHitDisplacementDuration = 0.12f;

    private Queue<GameObject> projectilePool;

    public float HoverHeight => hoverHeight;
    public DroneCluster Cluster { get; set; }

    /// <summary>
    /// Returns true if this drone is the leader of its cluster.
    /// Only the leader should run cluster-wide logic.
    /// </summary>
    public bool IsClusterLeader()
    {
        if (Cluster == null || Cluster.drones == null || Cluster.drones.Count == 0)
            return false;
        return ReferenceEquals(Cluster.drones[0], this);
    }

    // Expose thresholds
    public float FireEnterDistance => attackRange + fireEnterBuffer;
    public float FireExitDistance  => attackRange + fireExitBuffer;
    public float ChaseStuckSeconds => chaseStuckSeconds;

    // Public read-only properties to expose the Fire tuning values to behaviors (place with other public getters)
    public float FireStepAngleDeg => fireStepAngleDeg;
    public float FireRepositionIntervalMin => fireRepositionIntervalMin;
    public float FireRepositionIntervalMax => fireRepositionIntervalMax;
    public float FireRepositionJitter => fireRepositionJitter;
    public float FireArrivalEpsilon => fireArrivalEpsilon;
    public float FireCrossSwapChance => fireCrossSwapChance;

    // IProjectileShooter implementation
    public GameObject ProjectilePrefab => projectilePrefab;
    public float ProjectileSpeed => projectileSpeed;
    public Transform FirePoint => firePoint;
    public float DetectionRange => detectionRange;

    // Cache own colliders to ignore self-hit
    private Collider[] ownColliders;

    public void FireProjectile(Transform target)
    {
        if (ProjectilePrefab == null || FirePoint == null || target == null) return;

        // Aim slightly upward (player center/head)
        Vector3 targetPos = target.position + Vector3.up * aimYOffset;
        Vector3 dir = GetAimedDirection(FirePoint.position, targetPos);

        // Apply random miss offset?
        if (Random.value < missChance)
        {
            float missAngle = Random.Range(minMissAngleDeg, maxMissAngleDeg);
            dir = Quaternion.Euler(0f, missAngle, 0f) * dir;
        }

        var proj = GetPooledProjectile();

        // Prevent projectile colliding with the drone BEFORE activation
        IgnoreSelfCollision(proj);

        // Detach so drone rotation doesn't affect flight during lifetime
        proj.transform.SetParent(ProjectileHierarchy.GetActiveEnemyProjectilesParent(), true);

        // Spawn slightly forward to avoid initial overlap
        Vector3 spawnPos = FirePoint.position + dir * Mathf.Max(0f, muzzleForwardOffset);
        proj.transform.SetPositionAndRotation(spawnPos, Quaternion.LookRotation(dir));

        var rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Ensure consistent, straight flight
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.angularVelocity = Vector3.zero;
        }

        // Activate after setup, then apply velocity
        proj.SetActive(true);
        if (rb != null)
        {
            rb.linearVelocity = dir * ProjectileSpeed;
        }

        // Keep owner reference for pool return
        var enemyProj = proj.GetComponent<EnemyProjectile>();
        if (enemyProj != null)
            enemyProj.SetOwner(this);
    }

    private Coroutine tickCoroutine;
    private float lastFireTime = 0f;
    private float staggerUntilTime = 0f;
    private Transform player;
    private bool plungePhysicsCollapsed;
    private bool deathPhysicsCollapseApplied;
    private float aerialHitDisplacementLockUntil = -1f;
    private Coroutine aerialHitDisplacementRoutine;

    private IEnemyStateBehavior<DroneState, DroneTrigger> idleBehavior, relocateBehavior, swarmBehavior, fireBehavior, deathBehavior;

    public float zoneMoveInterval = 5f;
    public float lastZoneMoveTime = 0f;

    // Add this near the other coroutine fields
    private Coroutine fireTickCoroutine;

    protected override void Awake()
    {
        base.Awake();
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        idleBehavior = new DroneIdleBehavior<DroneState, DroneTrigger>();
        relocateBehavior = new DroneRelocateBehavior<DroneState, DroneTrigger>();
        swarmBehavior = new DroneSwarmBehavior<DroneState, DroneTrigger>();
        fireBehavior = new FireBehavior<DroneState, DroneTrigger>();
        deathBehavior = new DeathBehavior<DroneState, DroneTrigger>();

        idleTimerDuration = 15f;
        detectionRange = 15f;


        // Cache own colliders (exclude pool to avoid unnecessary ignores)
        ownColliders = GetComponentsInChildren<Collider>(includeInactive: true);

        // Create BulletPool child
        var poolObj = new GameObject("BulletPool");
        poolObj.transform.SetParent(transform);
        poolObj.transform.localPosition = Vector3.zero;
        bulletPoolParent = poolObj.transform;
    }


    private new void Start()
    {
        InitializeStateMachine(DroneState.Idle);
        ConfigureStateMachine();

        if (currentZone == null)
        {
            currentZone = FindNearestZone(transform.position);
        }

        // State machine initialization typically doesn't fire OnEntry.
        // We need to manually start the idle behavior for the cluster leader.
        // Only call OnEnter for the leader to avoid duplicate coroutines.
        if (IsClusterLeader())
        {
            idleBehavior.OnEnter(this);
        }


        EnsureHealthBarBinding();

        // Cache renderer references for memory leak diagnostic toggling
        _cachedRenderers = GetComponentsInChildren<Renderer>();

        // NOTE: Don't call StartIdleTimer() here - idleBehavior.OnEnter() handles it
        // and only starts the timer when there are multiple zones
    }


    protected override void Update()
    {
        // Handle component-level toggling (for memory leak diagnosis)
        HandleComponentToggling();

        if (plungePhysicsCollapsed)
            return;

        if (Time.time < aerialHitDisplacementLockUntil)
        {
            RefreshPlayerReference();
            return;
        }
        
        base.Update();
        RefreshPlayerReference();
        
        float speed01 = 0f;
        if (agent != null && agent.enabled)
        {
            float normalizedDivisor = Mathf.Max(agent.speed, 0.01f);
            speed01 = Mathf.Clamp01(agent.velocity.magnitude / normalizedDivisor);
        }
        PlayLocomotionAnim(speed01);
        UpdateFacing();
    }

    public override void Spawn()
    {
        RestoreFromPlungePhysicsCollapse();
        base.Spawn();
    }

    public override void ResetEnemy()
    {
        RestoreFromPlungePhysicsCollapse();
        base.ResetEnemy();
    }

    public override void CheckHealthThreshold()
    {
        if (applyPhysicsCollapseOnAnyDeath
            && !deathPhysicsCollapseApplied
            && !plungePhysicsCollapsed
            && currentHealth <= 0f)
        {
            Vector3 impactOrigin = player != null
                ? player.position
                : (transform.position - transform.forward);

            deathPhysicsCollapseApplied = true;
            ApplyPlungePhysicsCollapse(
                impactOrigin,
                deathCollapseDownwardVelocity,
                deathCollapseRadialForce);
        }

        base.CheckHealthThreshold();
    }

    public void ApplyPlungePhysicsCollapse(Vector3 impactOrigin, float downwardVelocity, float radialForce)
    {
        if (plungePhysicsCollapsed)
            return;

        plungePhysicsCollapsed = true;
        deathPhysicsCollapseApplied = true;
        StopFireTick();
        ForceStopMovementSFX();

        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
                agent.ResetPath();
            agent.enabled = false;
        }

        if (animator != null)
            animator.enabled = false;

        EnsureRuntimePhysicsCollider();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.None;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 away = transform.position - impactOrigin;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = transform.forward;

        Vector3 launchVelocity = away.normalized * Mathf.Max(0f, radialForce)
            + Vector3.down * Mathf.Max(0f, downwardVelocity);
        rb.linearVelocity = launchVelocity;
    }

    public void ApplyAerialHitDisplacement(Vector3 planarDisplacement, float verticalOffset)
    {
        if (plungePhysicsCollapsed || !isAlive)
            return;

        Vector3 displacement = new Vector3(planarDisplacement.x, 0f, planarDisplacement.z);
        Vector3 targetPosition = transform.position + displacement + Vector3.up * verticalOffset;

        if (aerialHitDisplacementRoutine != null)
        {
            StopCoroutine(aerialHitDisplacementRoutine);
            aerialHitDisplacementRoutine = null;
        }

        aerialHitDisplacementRoutine = StartCoroutine(AerialHitDisplacementRoutine(targetPosition));

        float lockDuration = Mathf.Max(0f, aerialHitDisplacementLockDuration);
        if (lockDuration > 0f)
            aerialHitDisplacementLockUntil = Mathf.Max(aerialHitDisplacementLockUntil, Time.time + lockDuration);
    }

    private IEnumerator AerialHitDisplacementRoutine(Vector3 targetPosition)
    {
        bool restoreAgentAfterNudge = false;
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
                agent.ResetPath();

            agent.enabled = false;
            restoreAgentAfterNudge = true;
        }

        Vector3 startPosition = transform.position;
        float duration = Mathf.Max(0.02f, aerialHitDisplacementDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (plungePhysicsCollapsed)
                break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(startPosition, targetPosition, eased);
            yield return null;
        }

        if (!plungePhysicsCollapsed)
            transform.position = targetPosition;

        if (!plungePhysicsCollapsed && restoreAgentAfterNudge && agent != null && !agent.enabled)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.Warp(transform.position);
                agent.ResetPath();
            }
        }

        aerialHitDisplacementRoutine = null;
    }

    private void RestoreFromPlungePhysicsCollapse()
    {
        plungePhysicsCollapsed = false;
        deathPhysicsCollapseApplied = false;
        aerialHitDisplacementLockUntil = -1f;

        if (aerialHitDisplacementRoutine != null)
        {
            StopCoroutine(aerialHitDisplacementRoutine);
            aerialHitDisplacementRoutine = null;
        }

        if (animator != null)
            animator.enabled = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (agent != null && !agent.enabled)
            agent.enabled = true;
    }

    private void EnsureRuntimePhysicsCollider()
    {
        Collider[] cols = GetComponents<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && !cols[i].isTrigger)
                return;
        }

        SphereCollider runtimeBodyCollider = GetComponent<SphereCollider>();
        if (runtimeBodyCollider == null || runtimeBodyCollider.isTrigger)
            runtimeBodyCollider = gameObject.AddComponent<SphereCollider>();

        runtimeBodyCollider.isTrigger = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);

            runtimeBodyCollider.center = localCenter;
            runtimeBodyCollider.radius = Mathf.Max(0.2f, radius);
        }
    }
    
    /// <summary>
    /// Handles toggling of Renderer components for memory leak diagnosis.
    /// </summary>
    private void HandleComponentToggling()
    {
        // Toggle Renderer components
        if (s_disableRenderers != _lastRendererDisableState)
        {
            _lastRendererDisableState = s_disableRenderers;
            if (_cachedRenderers != null)
            {
                foreach (var r in _cachedRenderers)
                {
                    if (r != null) r.enabled = !s_disableRenderers;
                }
            }
        }
    }

    // Throttle player reference refresh to avoid calling FindGameObjectWithTag every frame
    private float _lastPlayerRefreshTime;
    private const float PlayerRefreshInterval = 1f;


    /// <summary>
    /// Refreshes the player reference if it's null or inactive.
    /// Throttled to avoid expensive FindGameObjectWithTag calls every frame.
    /// </summary>
    private void RefreshPlayerReference()
    {
        // If we have a valid player, no need to refresh
        if (player != null && player.gameObject.activeInHierarchy)
            return;

        // Throttle refresh attempts to avoid calling FindGameObjectWithTag every frame
        if (Time.time - _lastPlayerRefreshTime < PlayerRefreshInterval)
            return;

        _lastPlayerRefreshTime = Time.time;

        // Use PlayerPresenceManager if available (avoids redundant FindGameObjectWithTag calls)
        if (PlayerPresenceManager.IsPlayerPresent)
        {
            player = PlayerPresenceManager.PlayerTransform;
        }
        else
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            player = playerObj != null ? playerObj.transform : null;
        }
        PlayerTarget = player;
    }

    public void StartIdleTimer()
    {
        if (idleTimerCoroutine != null)
            StopCoroutine(idleTimerCoroutine);
        idleTimerCoroutine = StartCoroutine(IdleTimerRoutine());
    }

    private IEnumerator IdleTimerRoutine()
    {
        yield return WaitForSecondsCache.Get(idleTimerDuration);
        enemyAI.Fire(DroneTrigger.LosePlayer);
    }

    protected override void ConfigureStateMachine()
    {
        enemyAI.Configure(DroneState.Idle)
            .OnEntry(() => { idleBehavior.OnEnter(this); })
            .OnExit(() => idleBehavior.OnExit(this))
            .Permit(DroneTrigger.SeePlayer, DroneState.Chase)
            .Permit(DroneTrigger.Die, DroneState.Death)
            .Permit(DroneTrigger.LosePlayer, DroneState.Relocate);

        enemyAI.Configure(DroneState.Relocate)
            .OnEntry(() => { relocateBehavior.OnEnter(this); })
            .OnExit(() => relocateBehavior.OnExit(this))
            .Permit(DroneTrigger.LosePlayer, DroneState.Idle)
            .Permit(DroneTrigger.SeePlayer, DroneState.Chase)
            .Permit(DroneTrigger.Die, DroneState.Death)
            .Permit(DroneTrigger.InAttackRange, DroneState.Fire)
            .Permit(DroneTrigger.RelocateComplete, DroneState.Idle);

        enemyAI.Configure(DroneState.Chase)
            .OnEntry(() => {
                swarmBehavior.OnEnter(this);
                ResetAgentDestination();
            })
            .OnExit(() => swarmBehavior.OnExit(this))
            .Permit(DroneTrigger.InAttackRange, DroneState.Fire)
            .Permit(DroneTrigger.LosePlayer, DroneState.Relocate)
            .Permit(DroneTrigger.Die, DroneState.Death)
            .Ignore(DroneTrigger.SeePlayer)
            .Ignore(DroneTrigger.RelocateComplete);


        enemyAI.Configure(DroneState.Fire)
            .OnEntry(() => { 
                fireBehavior.OnEnter(this); 
                // ALL drones start firing, but only leader handles movement coordination
                StartFireTick(); 
            })
            .OnExit(() => { 
                fireBehavior.OnExit(this); 
                StopFireTick(); 
            })
            .Permit(DroneTrigger.OutOfAttackRange, DroneState.Chase)
            .Permit(DroneTrigger.LosePlayer, DroneState.Relocate)
            .Permit(DroneTrigger.Die, DroneState.Death)
            .Ignore(DroneTrigger.SeePlayer)
            .Ignore(DroneTrigger.InAttackRange)
            .Ignore(DroneTrigger.RelocateComplete);

        enemyAI.Configure(DroneState.Death)
            .OnEntry(() => { deathBehavior.OnEnter(this); })
            .OnExit(() => deathBehavior.OnExit(this))
            .Ignore(DroneTrigger.SeePlayer)
            .Ignore(DroneTrigger.LosePlayer)
            .Ignore(DroneTrigger.InAttackRange)
            .Ignore(DroneTrigger.OutOfAttackRange)
            .Ignore(DroneTrigger.Die);
    }

    // Destination caching to prevent redundant SetDestination calls
    private Vector3 _lastRequestedDestination = Vector3.positiveInfinity;
    private const float DestinationChangeThreshold = 1.0f;
    private const float PathRefreshInterval = 2.0f;
    private const float MinSetDestinationInterval = 0.5f;
    private float _lastPathRequestTime;
    private float _lastSetDestinationTime;

    /// <summary>
    /// Moves the drone to the specified position.
    /// Simplified for reliability - removed complex fallback logic.
    /// </summary>
    public void MoveTo(Vector3 position)
    {
        if (agent == null || !agent.enabled)
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning(nameof(DroneEnemy), $"[DroneEnemy] {name}: MoveTo failed - agent null or disabled");
#endif
            return;
        }

        // Ensure agent is on a NavMesh
        if (!agent.isOnNavMesh)
        {
            // Try to warp to nearest NavMesh position with progressively larger radii
            float[] warpRadii = { 5f, 15f, 30f, 50f };
            bool warped = false;
            
            foreach (float radius in warpRadii)
            {
                if (NavMesh.SamplePosition(transform.position, out var selfHit, radius, NavMesh.AllAreas))
                {
                    agent.Warp(selfHit.position);
                    warped = true;
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.Log(nameof(DroneEnemy), $"[DroneEnemy] {name}: Warped to NavMesh at {selfHit.position} (radius: {radius})");
#endif
                    break;
                }
            }
            
            if (!warped)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(DroneEnemy), $"[DroneEnemy] {name}: MoveTo failed - not on NavMesh and couldn't find one within 50m");
#endif
                return;
            }
        }

        // Flatten Y to the agent's current Y (NavMesh surface)
        Vector3 desired = new Vector3(position.x, agent.transform.position.y, position.z);
        
        // Throttle check - skip if destination hasn't changed much and we have a valid path
        float distanceToLastDest = Vector3.Distance(desired, _lastRequestedDestination);
        bool destinationChanged = distanceToLastDest > DestinationChangeThreshold;
        bool timeToRefresh = Time.time - _lastPathRequestTime > PathRefreshInterval;
        bool hasValidPath = agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete;
        
        if (!destinationChanged && hasValidPath && !timeToRefresh)
        {
            return; // Skip - destination same and path is valid
        }

        // Try to find a valid NavMesh position near the desired point
        Vector3 finalDestination = desired;
        if (NavMesh.SamplePosition(desired, out var hit, 10f, NavMesh.AllAreas))
        {
            finalDestination = hit.position;
        }
        else
        {
            // Fallback: just try to move toward the desired position
            // The NavMeshAgent will handle pathfinding
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning(nameof(DroneEnemy), $"[DroneEnemy] {name}: No NavMesh near desired position {desired}, using direct destination");
#endif
        }

        // Set the destination
        _lastRequestedDestination = desired;
        _lastPathRequestTime = Time.time;
        _lastSetDestinationTime = Time.time;
        
        if (agent.SetDestination(finalDestination))
        {
#if UNITY_EDITOR
            if (Time.frameCount % 300 == 0) // Log every ~5 seconds at 60fps
                EnemyBehaviorDebugLogBools.Log(nameof(DroneEnemy), $"[DroneEnemy] {name}: SetDestination to {finalDestination}");
#endif
        }
        else
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning(nameof(DroneEnemy), $"[DroneEnemy] {name}: SetDestination FAILED for {finalDestination}");
#endif
        }
    }

    /// <summary>
    /// Forces a path recalculation on next MoveTo call by invalidating the cached destination.
    /// </summary>
    public void InvalidatePathCache()
    {
        _lastRequestedDestination = Vector3.positiveInfinity;
        _lastPathRequestTime = 0f;
        _lastSetDestinationTime = 0f;
    }

    public void TryFireAtPlayer()
    {
        if (Time.time < staggerUntilTime)
            return;

        if (player == null) return;
        if (Time.time - lastFireTime < fireCooldown) return;
        
        // Check if we're roughly facing the player before firing (within 45 degrees)
        Vector3 toPlayer = (player.position - transform.position);
        toPlayer.y = 0; // Flatten to horizontal
        if (toPlayer.sqrMagnitude > 0.01f)
        {
            float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
            if (angle > 45f)
            {
                // Not facing player yet, skip firing this frame (UpdateFacing will rotate us)
                return;
            }
        }
        
        lastFireTime = Time.time;

        if (projectilePrefab != null && firePoint != null)
        {
            Vector3 targetPos = player.position + Vector3.up * aimYOffset;
            Vector3 dir = GetAimedDirection(firePoint.position, targetPos);
            var proj = GetPooledProjectile();

            // Prevent self-collision
            IgnoreSelfCollision(proj);

            // Detach and spawn forward
            proj.transform.SetParent(ProjectileHierarchy.GetActiveEnemyProjectilesParent(), true);
            Vector3 spawnPos = firePoint.position + dir * Mathf.Max(0f, muzzleForwardOffset);
            proj.transform.SetPositionAndRotation(spawnPos, Quaternion.LookRotation(dir));


            var rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.angularVelocity = Vector3.zero;
            }

            proj.SetActive(true);
            if (rb != null) rb.linearVelocity = dir * projectileSpeed;

            var enemyProj = proj.GetComponent<EnemyProjectile>();
            if (enemyProj != null) enemyProj.SetOwner(this);

            PlayAttackAnim();
        }
    }

    public bool IsPlayerInAttackRange()
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= attackRange;
    }

    public Transform GetPlayerTransform()
    {
        return player;
    }

    private Zone FindNearestZone(Vector3 position)
    {
        // Use ZoneManager if available for cached zones
        var zones = ZoneManager.Instance != null 
            ? ZoneManager.Instance.GetAllZones() 
            : FindObjectsByType<Zone>(FindObjectsSortMode.None);
        Zone nearest = null;
        float minDist = float.MaxValue;
        foreach (var zone in zones)
        {
            float dist = Vector3.Distance(position, zone.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = zone;
            }
        }
        return nearest;
    }

    public void StartTickCoroutine(System.Action tickAction, float interval = 0f)
    {
        StopTickCoroutine();
        tickCoroutine = StartCoroutine(TickRoutine(tickAction, interval));
    }

    public void StopTickCoroutine()
    {
        if (tickCoroutine != null)
        {
            StopCoroutine(tickCoroutine);
            tickCoroutine = null;
        }
    }

    private IEnumerator TickRoutine(System.Action tickAction, float interval)
    {
        while (true)
        {
            // Always run ticks - diagnostic flags removed for reliability
            tickAction?.Invoke();
            yield return interval > 0f ? WaitForSecondsCache.Get(interval) : null;
        }
    }

    public void StopIdleTimer()
    {
        if (idleTimerCoroutine != null)
        {
            StopCoroutine(idleTimerCoroutine);
            idleTimerCoroutine = null;
        }
    }

    public void ResetAgentDestination()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }
    }

    public void StartFireTick()
    {
        StopFireTick();
        // Increased interval to reduce SetDestination calls which cause memory leaks
        fireTickCoroutine = StartCoroutine(TickRoutine(() => fireBehavior.Tick(this), 0.25f));
    }

    public void StopFireTick()
    {
        if (fireTickCoroutine != null)
        {
            StopCoroutine(fireTickCoroutine);
            fireTickCoroutine = null;
        }
    }

    private void InitializeProjectilePool()
    {
        projectilePool = new Queue<GameObject>(projectilePoolSize);
        for (int i = 0; i < projectilePoolSize; i++)
        {
            var proj = Instantiate(projectilePrefab, bulletPoolParent);
            proj.SetActive(false);
            projectilePool.Enqueue(proj);
        }
    }

    private GameObject GetPooledProjectile()
    {
        if (projectilePool.Count > 0)
        {
            var proj = projectilePool.Dequeue();
            if (proj != null)
            {
                // ensure it returns to pool parent on deactivation
                return proj;
            }
        }
        var newProj = Instantiate(projectilePrefab, bulletPoolParent);
        newProj.SetActive(false);
        return newProj;
    }

    public void ReturnProjectileToPool(GameObject proj)
    {
        if (proj == null) return;

        // Reparent back under pool, reset physics, then deactivate and enqueue
        var rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        proj.transform.SetParent(bulletPoolParent, false);
        proj.SetActive(false);
        projectilePool.Enqueue(proj);
    }

    private new void OnEnable()
    {
        InitializeProjectilePool();
    }

    private new void OnDisable()
    {
        StopFireTick();
        StopTickCoroutine();
        StopIdleTimer();

        if (aerialHitDisplacementRoutine != null)
        {
            StopCoroutine(aerialHitDisplacementRoutine);
            aerialHitDisplacementRoutine = null;
        }
    }

    private Transform bulletPoolParent;

    // Ignore collisions between this drone and a given projectile's colliders
    private void IgnoreSelfCollision(GameObject projectile)
    {
        if (ownColliders == null || ownColliders.Length == 0 || projectile == null) return;
        var projCols = projectile.GetComponentsInChildren<Collider>(includeInactive: true);
        for (int i = 0; i < ownColliders.Length; i++)
        {
            var oc = ownColliders[i];
            if (oc == null) continue;
            for (int j = 0; j < projCols.Length; j++)
            {
                var pc = projCols[j];
                if (pc == null) continue;
                Physics.IgnoreCollision(pc, oc, true);
            }
        }
    }

    private Vector3 GetAimedDirection(Vector3 fireOrigin, Vector3 targetPos)
    {
        Vector3 dir = (targetPos - fireOrigin).normalized;

        // Random miss: yaw-only offset around world up so vertical aim stays unchanged
        if (maxMissAngleDeg > 0f && Random.value < Mathf.Clamp01(missChance))
        {
            float minA = Mathf.Clamp(minMissAngleDeg, 0f, maxMissAngleDeg);
            float angle = Random.Range(minA, maxMissAngleDeg);
            if (angle > 0f)
            {
                float sign = Random.value < 0.5f ? -1f : 1f; // left or right
                dir = Quaternion.AngleAxis(sign * angle, Vector3.up) * dir;
            }
        }
        return dir;
    }

    private void UpdateFacing()
    {
        Vector3 forward = Vector3.zero;

        // In Fire state, ALWAYS face the player (priority over movement direction)
        bool isInFireState = enemyAI != null && enemyAI.State.Equals(DroneState.Fire);
        
        if (isInFireState && player != null)
        {
            // Always face player when firing
            forward = new Vector3(player.position.x - transform.position.x, 0f, player.position.z - transform.position.z);
        }
        else
        {
            // Normal behavior: face movement direction, or player if stationary
            if (agent != null && agent.enabled)
            {
                Vector3 planarVel = new Vector3(agent.velocity.x, 0f, agent.velocity.z);
                if (planarVel.sqrMagnitude >= velocityFacingThreshold * velocityFacingThreshold)
                {
                    forward = planarVel;
                }
            }

            if (forward == Vector3.zero && player != null)
            {
                forward = new Vector3(player.position.x - transform.position.x, 0f, player.position.z - transform.position.z);
            }
        }

        if (forward.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
        
        // Use faster turn speed when in Fire state to snap to player quickly
        float effectiveTurnSpeed = isInFireState ? turnSpeed * 2f : turnSpeed;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, effectiveTurnSpeed * Time.deltaTime);
    }

    protected override void OnDamageTaken(float amount)
    {
        if (currentHealth > 0f)
        {
            staggerUntilTime = Time.time + hitStaggerDuration;
        }
        base.OnDamageTaken(amount);
    }

}