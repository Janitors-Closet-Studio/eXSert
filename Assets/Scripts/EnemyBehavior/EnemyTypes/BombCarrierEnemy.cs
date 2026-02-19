// BombCarrierEnemy.cs
// Purpose: Enemy that carries explosive payloads and detonates under conditions.
// Works with: Explosion systems, EnemyStateMachineConfig.

using UnityEngine;
using System.Collections;
using Behaviors;
using System.Linq;

public enum BombAttackBehavior
{
    ChargeAndExplode,   // Default: charge straight, explode on contact/timer
    StopAndLeap,        // Stop, leap at player, explode by proximity
    ZigZag              // Zig-zag quickly toward player, explode on contact/timer
}

public enum BombStates
{
    Idle,
    Approaching,
    Attacking,
    Exploding,
    Returning,
    Death
}

public enum BombTriggers
{
    SeePlayer,
    LosePlayer,
    InAttackRange,
    OutOfAttackRange,
    Explode,
    Die,
    ReturnToPocket
}

public class BombCarrierEnemy : BaseEnemy<BombStates, BombTriggers>, IPocketSpawnable
{
    [Header("Bomb Bot Settings")]
    [SerializeField, Tooltip("Radius at which the bomb bot will trigger its explosion when the player enters.")]
    private float triggerRadius = 1.5f;
    [SerializeField, Tooltip("Radius of the explosion when the bomb bot detonates. The default is 2x the trigger radius.")]
    public float explosionRadius = 3f;
    [SerializeField, Tooltip("Damage dealt to all targets within the explosion radius.")]
    private float explosionDamage = 100f;
    [SerializeField, Tooltip("If true, the bomb bot will randomly select an attack behavior on spawn.")]
    private bool randomizeBehavior = false;
    [SerializeField, Tooltip("The attack behavior this bomb bot will use.")]
    private BombAttackBehavior attackBehavior = BombAttackBehavior.ChargeAndExplode;
    [SerializeField, Tooltip("Time in seconds before the bomb bot explodes after starting its attack.")]
    private float explodeTimer = 1.5f;
    [SerializeField, Tooltip("Distance from player at which a pocket-spawned bomb bot will flee/return to pocket.")]
    private float fleeDistanceFromPlayer = 30f;
    [SerializeField, Tooltip("Cooldown in seconds after exiting a pocket before the bomb can explode.")]
    private float postPocketExplodeCooldown = 1.5f;
    private bool canExplode = true;

    // --- Charge Settings ---
    [Header("Charge Behavior Settings")]
    [SerializeField, Tooltip("Distance from player at which the bomb bot will start its charge.")]
    private float chargeStartDistance = 6f;
    [SerializeField, Tooltip("Speed of the charge.")]
    private float chargeSpeed = 15f;

    // --- Stop & Leap Settings ---
    [Header("Stop & Leap Behavior Settings")]
    [SerializeField, Tooltip("Force applied to the bomb bot when leaping at the player.")]
    private float leapForce = 15f;
    [SerializeField, Tooltip("Distance from the player at which the bomb bot will stop and leap.")]
    private float leapDistance = 5f;
    [SerializeField, Tooltip("Minimum and maximum delay (in seconds) to wait after stopping before leaping at the player.")]
    private Vector2 stopBeforeLeapDelayRange = new Vector2(0.3f, 1.0f);

    // --- ZigZag Settings ---
    [Header("ZigZag Behavior Settings")]
    [SerializeField, Tooltip("Speed at which the bomb bot zig-zags toward the player.")]
    private float zigZagSpeed = 10f;
    [SerializeField, Tooltip("Frequency of the zig-zag movement.")]
    private float zigZagFrequency = 8f;
    [SerializeField, Tooltip("Amplitude of the zig-zag movement.")]
    private float zigZagAmplitude = 2f;

    //[SerializeField] private GameObject warningIndicatorPrefab;

    [Header("Explosion Visual")]
    [SerializeField, Tooltip("If true, show a visual indicator for the explosion radius when detonating.")]
    private bool showExplosionVisual = false;
    private GameObject explosionVisual;
    [SerializeField, Tooltip("How long the explosion visual remains after detonation.")]
    private float explosionVisualDuration = 0.5f;

    // Spawn context
    private bool spawnedByAlarm = false;
    private AlarmCarrierEnemy alarmSource;
    private CrawlerPocket pocketSource;
    public CrawlerPocket Pocket { get; set; }

    // State
    private bool isExploding = false;
    private Coroutine attackRoutine;

    [HideInInspector]
    public GameObject originalPrefab;

    public bool IsAttacking { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        // Ensure a trigger SphereCollider exists for explosion/contact detection
        var trigger = GetComponent<SphereCollider>();
        if (trigger == null)
            trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = triggerRadius; // Use trigger radius for collision

        if (randomizeBehavior)
        {
            attackBehavior = (BombAttackBehavior)Random.Range(0, System.Enum.GetValues(typeof(BombAttackBehavior)).Length);
        }
        InitializeStateMachine(BombStates.Idle);
        ConfigureStateMachine();

        // Create the explosion visual sphere if enabled
        if (showExplosionVisual)
        {
            CreateExplosionVisual();
        }
    }

    private void CreateExplosionVisual()
    {
        explosionVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        explosionVisual.transform.SetParent(transform);
        explosionVisual.transform.localPosition = Vector3.zero;
        explosionVisual.transform.localScale = Vector3.one * explosionRadius * 2f; // Diameter

        // Make the sphere mostly transparent
        var renderer = explosionVisual.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(1f, 0.3f, 0.1f, 0.25f); // Orange, mostly transparent
        mat.SetFloat("_Mode", 3); // Transparent mode
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        renderer.material = mat;

        // Remove collider from visual
        Destroy(explosionVisual.GetComponent<Collider>());

        explosionVisual.SetActive(false); // Hide by default
    }

    private void PermitExplodeFromAllStates()
    {
        foreach (BombStates state in System.Enum.GetValues(typeof(BombStates)))
        {
            if (state != BombStates.Exploding && state != BombStates.Death)
            {
                enemyAI.Configure(state)
                    .Permit(BombTriggers.Explode, BombStates.Exploding);
            }
        }
    }

    protected override void ConfigureStateMachine()
    {
        enemyAI.Configure(BombStates.Idle)
            .Permit(BombTriggers.SeePlayer, BombStates.Approaching)
            .Permit(BombTriggers.Die, BombStates.Death);

        enemyAI.Configure(BombStates.Approaching)
            .OnEntry(StartApproach)
            .PermitIf(BombTriggers.InAttackRange, BombStates.Attacking, () => canExplode)
            .IgnoreIf(BombTriggers.InAttackRange, () => !canExplode)
            .Permit(BombTriggers.LosePlayer, BombStates.Idle)
            .Permit(BombTriggers.Die, BombStates.Death)
            .Permit(BombTriggers.ReturnToPocket, BombStates.Returning)
            .Ignore(BombTriggers.SeePlayer);

        enemyAI.Configure(BombStates.Attacking)
            .OnEntry(StartAttackBehavior)
            .Permit(BombTriggers.Die, BombStates.Death)
            .Ignore(BombTriggers.SeePlayer)
            .Permit(BombTriggers.ReturnToPocket, BombStates.Returning);

        enemyAI.Configure(BombStates.Returning)
            .OnEntry(StartReturnToPocket)
            .Permit(BombTriggers.Die, BombStates.Death)
            .Permit(BombTriggers.SeePlayer, BombStates.Approaching);

        enemyAI.Configure(BombStates.Exploding)
            .OnEntry(Explode)
            .Permit(BombTriggers.Die, BombStates.Death)
            .Ignore(BombTriggers.Explode);

        enemyAI.Configure(BombStates.Death)
            .OnEntry(() => Destroy(gameObject))
            .Ignore(BombTriggers.Die)
            .Ignore(BombTriggers.Explode)
            .Ignore(BombTriggers.SeePlayer);

        // Allow explosion from any state except Exploding/Death
        PermitExplodeFromAllStates();
    }

    private void Start()
    {
        // Immediately see player and start approaching
        if (PlayerTarget == null)
        {
            // Use PlayerPresenceManager if available
            if (PlayerPresenceManager.IsPlayerPresent)
                PlayerTarget = PlayerPresenceManager.PlayerTransform;
            else
                PlayerTarget = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        if (PlayerTarget != null)
            enemyAI.Fire(BombTriggers.SeePlayer);
    }

    private void StartApproach()
    {
        // Always go to attacking if in range, otherwise keep approaching
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(ApproachRoutine());
    }

    private void StartAttackBehavior()
    {
        IsAttacking = true;

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);
        EnemyBehaviorDebugLogBools.Log(nameof(BombCarrierEnemy), $"BombCarrierEnemy using attackBehavior: {attackBehavior}");
        attackRoutine = StartCoroutine(AttackBehaviorRoutine());
    }

    private IEnumerator AttackBehaviorRoutine()
    {
        switch (attackBehavior)
        {
            case BombAttackBehavior.ChargeAndExplode:
                yield return StartCoroutine(ChargeAndExplodeRoutine());
                break;
            case BombAttackBehavior.StopAndLeap:
                yield return StartCoroutine(StopAndLeapRoutine());
                break;
            case BombAttackBehavior.ZigZag:
                yield return StartCoroutine(ZigZagRoutine());
                break;
        }
        // After attack, trigger explosion
        if (canExplode)
            enemyAI.Fire(BombTriggers.Explode);
    }

    private IEnumerator ChargeAndExplodeRoutine()
    {
        agent.speed = chargeSpeed;
        float timer = 0f;
        while (!isExploding && timer < explodeTimer)
        {
            if (PlayerTarget != null)
            {
                agent.SetDestination(PlayerTarget.position);
            }
            timer += Time.deltaTime;
            yield return null;
        }
        // Wait for cooldown if needed
        while (!canExplode && !isExploding)
            yield return null;

        if (!isExploding)
            enemyAI.Fire(BombTriggers.Explode);
    }

    private IEnumerator StopAndLeapRoutine()
    {
        // Approach, stop at leapDistance, then wait, then leap at player and explode on proximity
        while (!isExploding)
        {
            if (PlayerTarget != null)
            {
                float dist = Vector3.Distance(transform.position, PlayerTarget.position);
                if (dist > leapDistance)
                {
                    agent.SetDestination(PlayerTarget.position);
                }
                else
                {
                    agent.isStopped = true;
                    // Wait for a random delay before leaping
                    float delay = Random.Range(stopBeforeLeapDelayRange.x, stopBeforeLeapDelayRange.y);
                    yield return WaitForSecondsCache.Get(delay);

                    Vector3 leapDir = (PlayerTarget.position - transform.position).normalized;
                    agent.velocity = leapDir * leapForce;
                    yield return WaitForSecondsCache.Get(0.3f);
                    break;
                }
            }
            yield return null;
        }
        // Wait for cooldown if needed
        while (!canExplode && !isExploding)
            yield return null;

        if (!isExploding)
            enemyAI.Fire(BombTriggers.Explode);
    }

    private IEnumerator ZigZagRoutine()
    {
        // Disable NavMeshAgent for direct movement
        if (agent.enabled)
            agent.enabled = false;

        float timer = 0f;
        Vector3 startPosition = transform.position;

        while (!isExploding && timer < explodeTimer)
        {
            if (PlayerTarget != null)
            {
                // Direction to player (on XZ plane)
                Vector3 toPlayer = PlayerTarget.position - startPosition;
                toPlayer.y = 0f;
                float totalDistance = toPlayer.magnitude;
                Vector3 forward = toPlayer.normalized;

                // Perpendicular direction for zig-zag (left/right)
                Vector3 perp = Vector3.Cross(forward, Vector3.up);

                // How far along the path we are (0=start, 1=at player)
                float progress = Mathf.Clamp01(timer / explodeTimer);

                // Move forward along the path
                Vector3 alongPath = startPosition + forward * totalDistance * progress;

                // Sine wave offset for zig-zag
                float zigzagOffset = Mathf.Sin(progress * zigZagFrequency * Mathf.PI * 2f) * zigZagAmplitude;

                // Final position with zig-zag
                Vector3 zigzagPosition = alongPath + perp * zigzagOffset;

                // Move directly to the calculated position
                transform.position = Vector3.MoveTowards(transform.position, zigzagPosition, zigZagSpeed * Time.deltaTime);
            }
            timer += Time.deltaTime;
            yield return null;
        }
        // Wait for cooldown if needed
        while (!canExplode && !isExploding)
            yield return null;

        if (!isExploding)
            enemyAI.Fire(BombTriggers.Explode);
    }

    private IEnumerator EnableExplosionAfterCooldown()
    {
        yield return WaitForSecondsCache.Get(postPocketExplodeCooldown);
        canExplode = true;
    }
    private void Explode()
    {
        if (isExploding) return;
        isExploding = true;

        // Show explosion visual if enabled
        if (showExplosionVisual && explosionVisual != null)
        {
            explosionVisual.SetActive(true);
        }

        // Damage player and enemies in radius, but only if collider is not a trigger
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            if (!hit.isTrigger)
            {
                hit.GetComponent<IHealthSystem>()?.LoseHP(explosionDamage);
            }
        }

        // Disable this enemy's visuals and logic, but keep the GameObject alive for the visual
        StartCoroutine(DisableAndDestroyAfterDelay(explosionVisualDuration));
    }

    private IEnumerator DisableAndDestroyAfterDelay(float delay)
    {
        // Disable all renderers except the explosion visual
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            if (explosionVisual == null || renderer.gameObject != explosionVisual)
                renderer.enabled = false;
        }

        // Optionally disable colliders and/or AI here
        foreach (var collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }
        // Optionally disable NavMeshAgent, scripts, etc.

        yield return WaitForSecondsCache.Get(delay);

        Destroy(gameObject);
    }

    public void SetSpawnSource(bool fromAlarm, AlarmCarrierEnemy alarm, CrawlerPocket pocket)
    {
        spawnedByAlarm = fromAlarm;
        alarmSource = alarm;
        pocketSource = pocket;

        // Only apply cooldown if spawned from a pocket (not alarm)
        if (!spawnedByAlarm)
        {
            canExplode = false;
            StartCoroutine(EnableExplosionAfterCooldown());
        }
        else
        {
            canExplode = true;
        }
    }

    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);
        if (!isExploding && canExplode && other.CompareTag("Player"))
        {
            enemyAI.Fire(BombTriggers.Explode);
        }
    }
    protected override void OnTriggerStay(Collider other)
    {
        // Do nothing in base. All logic should be in derived classes.
    }

    public void OnReturnedToPocket()
    {
        // Clean up, reset, or destroy as needed
        Destroy(gameObject);
    }

    public override void CheckHealthThreshold()
    {
        // Bomb bots do not use low health logic, so do nothing.
    }
    private void StartReturnToPocket()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(ReturnToPocketRoutine());
    }

    private IEnumerator ReturnToPocketRoutine()
    {
        if (Pocket == null)
        {
            enemyAI.Fire(BombTriggers.Die);
            yield break;
        }

        agent.isStopped = false;
        agent.SetDestination(Pocket.transform.position);

        while (Vector3.Distance(transform.position, Pocket.transform.position) > 1.0f) // 1.0f is the threshold
        {
            yield return null;
        }

        // Arrived at pocket, return to inactive
        Pocket.ReturnEnemyToInactive(this);
    }
    protected override void OnDestroy()
    {
        if (Pocket != null)
        {
            Pocket.RemoveFromActiveLists(this);
        }

        base.OnDestroy();
    }


    private IEnumerator ApproachRoutine()
    {
        while (enemyAI.State == BombStates.Approaching && !isExploding)
        {
            if (PlayerTarget == null)
            {
                yield break;
            }

            float dist = Vector3.Distance(transform.position, PlayerTarget.position);

            // Flee if spawned from pocket and player is too far
            // Only flee/return if NOT spawned by alarm
            if (!spawnedByAlarm && dist > fleeDistanceFromPlayer)
            {
                enemyAI.Fire(BombTriggers.ReturnToPocket);
                yield break;
            }
            if (spawnedByAlarm && alarmSource == null)
            {
                // Alarm bot is gone, now allow return/flee logic if needed
                if (dist > fleeDistanceFromPlayer)
                {
                    enemyAI.Fire(BombTriggers.ReturnToPocket);
                    yield break;
                }
            }

            // Only allow attacking if cooldown is over
            if (canExplode && dist <= chargeStartDistance)
            {
                // Fire the InAttackRange trigger to enter Attacking state
                enemyAI.Fire(BombTriggers.InAttackRange);
                yield break;
            }

            // Move toward player
            if (agent.enabled)
                agent.SetDestination(PlayerTarget.position);

            yield return null;
        }
    }
}