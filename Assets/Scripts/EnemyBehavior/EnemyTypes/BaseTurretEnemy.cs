// BaseTurretEnemy.cs
// Purpose: Base class for turret-style enemies (stationary or slow-moving) that fire projectiles.
// Works with: Projectile systems, EnemyBehaviorProfile, CrowdController.

using System.Collections;
using System.Collections.Generic;
using Behaviors;
using UnityEngine;
using UnityEngine.AI;

public abstract class BaseTurretEnemy : BaseEnemy<EnemyState, EnemyTrigger>, IProjectileShooter
{
    [Header("Turret Settings")]
    [Tooltip("How quickly the turret turns to face the target (degrees/second).")]
    [SerializeField] private float turnSpeed = 360f;

    [Tooltip("Seconds between shots while in Attack state.")]
    [SerializeField] protected float fireCooldown = 1.0f;

    [Tooltip("Projectile prefab spawned when firing. Should have a Rigidbody and EnemyProjectile (or custom) component.")]
    [SerializeField] protected GameObject projectilePrefab;

    [Tooltip("Initial linear velocity magnitude applied to the projectile.")]
    [SerializeField] protected float projectileSpeed = 25f;

    [Tooltip("Muzzle transform used as the spawn position and forward direction when firing.")]
    [SerializeField] protected Transform firePoint;

    [Tooltip("If true, only rotate around Y to face the target (keeps turret upright).")]
    [SerializeField] private bool rotateYawOnly = true;

    [Tooltip("Vertical aim offset in meters to target the player's center/head instead of feet.")]
    [SerializeField] private float aimYOffset = 1.0f;

    [Header("Rotation Limits")]
    [Tooltip("Prevent turrets from rotating a full 360 degrees around the base when tracking.")]
    [SerializeField] private bool clampYaw = false;
    [Tooltip("Maximum degrees the turret can turn to the left of its forward axis.")]
    [Range(0f, 180f)]
    [SerializeField] private float maxYawLeft = 90f;
    [Tooltip("Maximum degrees the turret can turn to the right of its forward axis.")]
    [Range(0f, 180f)]
    [SerializeField] private float maxYawRight = 90f;

    [Header("Firing")]
    [Tooltip("Spawn the projectile slightly forward from the muzzle to avoid self-collision at spawn.")]
    [SerializeField] private float muzzleForwardOffset = 0.1f;
    [Tooltip("Sound effect played on each shot.")]
    [SerializeField] private AudioClip gunfireSfx;
    [Tooltip("Optional AudioSource to play gunfire SFX. If empty, will use one on this object or create one.")]
    private AudioSource gunfireSource;

    [Header("Projectile Pool")]
    [Tooltip("How many projectiles to pre-instantiate for this turret.")]
    [SerializeField] private int projectilePoolSize = 16;

    [Header("Telegraph Line")]
    [Tooltip("Show a red telegraph line from the fire point while aiming.")]
    [SerializeField] private bool showTelegraphLine = true;
    [Tooltip("Line color used before firing.")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0.15f, 0.15f, 1f);
    [Tooltip("Line width for the telegraph.")]
    [SerializeField] private float telegraphWidth = 0.03f;
    [Tooltip("How far the telegraph line extends from the fire point.")]
    [SerializeField] private float telegraphDistance = 12f;
    [Tooltip("Flicker speed (cycles per second).")]
    [SerializeField] private float telegraphFlickerSpeed = 8f;
    [Tooltip("Flicker strength (0 = steady, 1 = max flicker).")]
    [SerializeField, Range(0f, 1f)] private float telegraphFlickerStrength = 0.5f;
    [Tooltip("Seconds the line stays hidden right after a shot.")]
    [SerializeField] private float telegraphHideAfterShot = 0.6f;

    [Header("Detection Hysteresis")]
    [Tooltip("Enter Attack when player is within detectionRange + this.")]
    [SerializeField] private float enterBuffer = 0.0f;

    [Tooltip("Leave Attack when player is beyond detectionRange + this.")]
    [SerializeField] private float exitBuffer = 0.5f;

    [Tooltip("Seconds the condition must hold before switching into Attack.")]
    [SerializeField] private float enterSustain = 0.15f;

    [Tooltip("Seconds the condition must hold before leaving Attack.")]
    [SerializeField] private float exitSustain = 0.25f;

    private readonly List<GameObject> projectilePool = new List<GameObject>(32);
    private Transform projectilePoolParent;

    protected Transform player;
    private Coroutine attackLoop;
    private Coroutine detectLoop;
    private Coroutine animationRestoreRoutine;
    private Coroutine telegraphHideRoutine;
    private float telegraphCycleStartTime = -1e9f;

    private IEnemyStateBehavior<EnemyState, EnemyTrigger> deathBehavior;

    // Cache own colliders to ignore self-collision on fired projectiles
    private Collider[] ownColliders;

    // Cooldown tracking persists across state flaps to prevent edge rapid-fire
    private float lastShotTime = -1e9f;

    [Header("Animation")]
    [SerializeField, Tooltip("Delay before returning to the state animation after a hit animation.")]
    private float hitAnimationRecoveryDelay = 0.15f;
    [SerializeField, Tooltip("If true, turrets hold their pose instead of returning to idle while actively targeting the player.")]
    private bool suppressIdleWhileTargeting = true;

    private bool isTargetEngaged;
    private LineRenderer telegraphLine;

    // IProjectileShooter implementation
    public GameObject ProjectilePrefab => projectilePrefab;
    public float ProjectileSpeed => projectileSpeed;
    public Transform FirePoint => firePoint;

    protected override void Awake()
    {
        // Ensure the pooled object includes any wrapper parents (e.g., turret base/box siblings).
        AdoptWrapperParentsForPooling();
        base.Awake();

        // Turret is stationary. Remove NavMeshAgent added by base.
        if (agent != null)
        {
            Destroy(agent);
            agent = null;
        }

        // Turn off melee attack collider for turrets
        if (attackCollider != null) attackCollider.enabled = false;

        if (gunfireSource == null)
        {
            gunfireSource = SoundManager.Instance != null ? SoundManager.Instance.sfxSource : GetComponent<AudioSource>();
        }

        telegraphCycleStartTime = Time.time;

        // Cache player - use PlayerPresenceManager if available
        if (PlayerPresenceManager.IsPlayerPresent)
        {
            player = PlayerPresenceManager.PlayerTransform;
        }
        else
        {
            var found = GameObject.FindGameObjectWithTag("Player");
            player = found != null ? found.transform : null;
        }
        PlayerTarget = player;

        // Cache only the turret's own colliders BEFORE creating the pool (so pool colliders are excluded)
        ownColliders = GetComponentsInChildren<Collider>(includeInactive: true);

        // Create pool parent under this turret
        var poolObj = new GameObject("ProjPool");
        poolObj.transform.SetParent(transform);
        poolObj.transform.localPosition = Vector3.zero;
        projectilePoolParent = poolObj.transform;

        deathBehavior = new DeathBehavior<EnemyState, EnemyTrigger>();

        InitializeProjectilePool();
        EnsureTelegraphLine();

        // Start state machine
        InitializeStateMachine(EnemyState.Idle);
        ConfigureStateMachine();

        EnsureHealthBarBinding();

        // Detection loop with hysteresis/debounce
        detectLoop = StartCoroutine(DetectionLoop());
    }

    protected virtual bool ShouldAimContinuously()
    {
        return enemyAI != null && player != null && enemyAI.State.Equals(EnemyState.Attack);
    }

    protected override void Update()
    {
        base.Update();

        if (ShouldAimContinuously())
        {
            AimAtTarget(player);
        }

        UpdateTelegraphLine();
    }

    protected override void ConfigureStateMachine()
    {
        enemyAI.Configure(EnemyState.Idle)
            .OnEntry(() =>
            {
                /* SetEnemyColor(patrolColor); */
                isTargetEngaged = false;
                RequestIdlePose(force: true);
                StopAttackLoop();
                SetTelegraphVisible(false);
            })
            .Permit(EnemyTrigger.SeePlayer, EnemyState.Attack)
            .Permit(EnemyTrigger.InAttackRange, EnemyState.Attack)
            .Permit(EnemyTrigger.Die, EnemyState.Death);

        enemyAI.Configure(EnemyState.Attack)
            .OnEntry(() =>
            {
                /* SetEnemyColor(attackColor); */
                isTargetEngaged = true;
                PlayAttackAnim();
                StartAttackLoop();
                SetTelegraphVisible(showTelegraphLine);
            })
            .OnExit(() =>
            {
                isTargetEngaged = false;
                StopAttackLoop();
                RequestIdlePose();
                SetTelegraphVisible(false);
            })
            .Permit(EnemyTrigger.LosePlayer, EnemyState.Idle)
            .Permit(EnemyTrigger.OutOfAttackRange, EnemyState.Idle)
            .Ignore(EnemyTrigger.SeePlayer)
            .Ignore(EnemyTrigger.InAttackRange)
            .Permit(EnemyTrigger.Die, EnemyState.Death);

        enemyAI.Configure(EnemyState.Death)
            .OnEntry(() =>
            {
                /* SetEnemyColor(Color.black); */
                isTargetEngaged = false;
                PlayDieAnim();
                StopAttackLoop();
                if (detectLoop != null)
                {
                    StopCoroutine(detectLoop);
                    detectLoop = null;
                }
                deathBehavior?.OnEnter(this);
            })
            .OnExit(() =>
            {
                deathBehavior?.OnExit(this);
            })
            .Ignore(EnemyTrigger.SeePlayer)
            .Ignore(EnemyTrigger.LosePlayer)
            .Ignore(EnemyTrigger.InAttackRange)
            .Ignore(EnemyTrigger.OutOfAttackRange)
            .Ignore(EnemyTrigger.Die);
    }

    private IEnumerator DetectionLoop()
    {
        const float interval = 0.15f; // slightly slower to reduce boundary chatter
        float enterTimer = 0f;
        float exitTimer = 0f;

        while (true)
        {
            if (player == null)
            {
                // Use PlayerPresenceManager if available
                if (PlayerPresenceManager.IsPlayerPresent)
                {
                    player = PlayerPresenceManager.PlayerTransform;
                }
                else
                {
                    var found = GameObject.FindGameObjectWithTag("Player");
                    player = found != null ? found.transform : null;
                }
                PlayerTarget = player;
                yield return WaitForSecondsCache.Get(interval);
                continue;
            }

            float dist = Vector3.Distance(transform.position, player.position);

            // Compute thresholds and enforce exit > enter
            float effectiveRange = GetEffectiveDetectionRange();
            float enterThreshold = Mathf.Max(0f, effectiveRange + Mathf.Max(0f, enterBuffer));
            float exitThreshold = effectiveRange + Mathf.Max(exitBuffer, enterBuffer + 0.5f);
            if (exitThreshold <= enterThreshold)
                exitThreshold = enterThreshold + 0.5f;

            if (enemyAI.State.Equals(EnemyState.Idle))
            {
                if (dist <= enterThreshold)
                {
                    enterTimer += interval;
                    bool yawOk = IsTargetWithinYawLimits(player.position);
                    if (enterTimer >= enterSustain && yawOk)
                    {
                        TryFireTriggerByName("SeePlayer"); // -> Attack
                        enterTimer = 0f;
                        exitTimer = 0f;
                    }
                }
                else
                {
                    enterTimer = 0f;
                }
            }
            else if (enemyAI.State.Equals(EnemyState.Attack))
            {
                if (dist > exitThreshold)
                {
                    exitTimer += interval;
                    if (exitTimer >= exitSustain)
                    {
                        TryFireTriggerByName("LosePlayer"); // -> Idle
                        exitTimer = 0f;
                        enterTimer = 0f;
                    }
                }
                else
                {
                    exitTimer = 0f;
                }

                if (clampYaw && !IsTargetWithinYawLimits(player.position))
                {
                    TryFireTriggerByName("LosePlayer");
                    enterTimer = 0f;
                    continue;
                }
            }

            yield return WaitForSecondsCache.Get(interval);
        }
    }

    private void StartAttackLoop()
    {
        StopAttackLoop();
        attackLoop = StartCoroutine(GetAttackLoopRoutine());
    }

    private void StopAttackLoop()
    {
        if (attackLoop != null)
        {
            StopCoroutine(attackLoop);
            attackLoop = null;
        }

        if (telegraphHideRoutine != null)
        {
            StopCoroutine(telegraphHideRoutine);
            telegraphHideRoutine = null;
        }
    }

    protected virtual IEnumerator GetAttackLoopRoutine()
    {
        return AttackLoop();
    }

    private IEnumerator AttackLoop()
    {
        while (enemyAI.State.Equals(EnemyState.Attack))
        {
            if (player == null)
            {
                // Use PlayerPresenceManager if available
                if (PlayerPresenceManager.IsPlayerPresent)
                {
                    player = PlayerPresenceManager.PlayerTransform;
                }
                else
                {
                    var found = GameObject.FindGameObjectWithTag("Player");
                    player = found != null ? found.transform : null;
                }
                PlayerTarget = player;
                yield return null;
                continue;
            }

            // Aim at player (optionally yaw-only)
            bool canAim = AimAtTarget(player);
            if (!canAim)
            {
                TryFireTriggerByName("LosePlayer");
                yield return null;
                continue;
            }

            // Fire on cooldown using a persistent timestamp (prevents edge rapid-fire)
            if (Time.time - lastShotTime >= fireCooldown)
            {
                FireProjectile(player);
                lastShotTime = Time.time;
            }

            yield return null;
        }
    }

    public void FireProjectile(Transform target)
    {
        if (ProjectilePrefab == null || FirePoint == null || target == null) return;

        Vector3 targetPos = target.position + Vector3.up * aimYOffset;
        Vector3 dir = (targetPos - FirePoint.position).normalized;

        var proj = GetPooledProjectile();

        // Set up pooling helper/return parent (in case prefab doesn't have it)
        var pooled = proj.GetComponent<TurretPooledProjectile>();
        if (pooled == null) pooled = proj.AddComponent<TurretPooledProjectile>();
        pooled.InitReturnParent(projectilePoolParent);

        // Prevent projectile from colliding with its own turret BEFORE activation
        IgnoreSelfCollision(proj);

        // Detach active projectile so turret rotation won't affect it
        proj.transform.SetParent(ProjectileHierarchy.GetActiveEnemyProjectilesParent(), true);
        Vector3 spawnPos = FirePoint.position + dir * Mathf.Max(0f, muzzleForwardOffset);
        proj.transform.SetPositionAndRotation(spawnPos, Quaternion.LookRotation(dir));

        // Ensure fast bullets register collisions
        var rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.angularVelocity = Vector3.zero;
        }

        // Activate after all setup to avoid immediate self-collision, then set velocity
        proj.SetActive(true);
        if (rb != null)
        {
            rb.linearVelocity = dir * ProjectileSpeed;
        }

        OnProjectileFired();
    }

    protected virtual void OnProjectileFired()
    {
        PlayAttackAnim();
        lastShotTime = Time.time;
        telegraphCycleStartTime = Time.time;
        PlayGunfireSfx();
        if (showTelegraphLine)
            HideTelegraphTemporarily(telegraphHideAfterShot);
    }

    private void PlayGunfireSfx()
    {
        if (gunfireSfx == null || gunfireSource == null)
            return;

        gunfireSource.PlayOneShot(gunfireSfx);
    }

    // Permanently ignore collisions between this turret's colliders and this projectile's colliders
    private void IgnoreSelfCollision(GameObject projectile)
    {
        if (ownColliders == null || ownColliders.Length == 0) return;

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

    private void InitializeProjectilePool()
    {
        projectilePool.Clear();
        if (ProjectilePrefab == null) return;

        for (int i = 0; i < projectilePoolSize; i++)
        {
            var proj = Instantiate(ProjectilePrefab, projectilePoolParent);
            proj.SetActive(false);

            var pooled = proj.GetComponent<TurretPooledProjectile>();
            if (pooled == null) pooled = proj.AddComponent<TurretPooledProjectile>();
            pooled.InitReturnParent(projectilePoolParent);

            projectilePool.Add(proj);
        }
    }

    private GameObject GetPooledProjectile()
    {
        for (int i = 0; i < projectilePool.Count; i++)
        {
            if (projectilePool[i] != null && !projectilePool[i].activeSelf)
                return projectilePool[i];
        }
        var proj = Instantiate(ProjectilePrefab, projectilePoolParent);
        proj.SetActive(false);
        var pooled = proj.GetComponent<TurretPooledProjectile>();
        if (pooled == null) pooled = proj.AddComponent<TurretPooledProjectile>();
        pooled.InitReturnParent(projectilePoolParent);
        projectilePool.Add(proj);
        return proj;
    }

    private void EnsureTelegraphLine()
    {
        if (!showTelegraphLine)
            return;

        if (telegraphLine != null)
            return;

        var lineObj = new GameObject("TelegraphLine");
        lineObj.transform.SetParent(transform);
        lineObj.transform.localPosition = Vector3.zero;
        lineObj.transform.localRotation = Quaternion.identity;
        lineObj.transform.localScale = Vector3.one;

        telegraphLine = lineObj.AddComponent<LineRenderer>();
        telegraphLine.useWorldSpace = true;
        telegraphLine.positionCount = 2;
        telegraphLine.startWidth = Mathf.Max(0.001f, telegraphWidth);
        telegraphLine.endWidth = Mathf.Max(0.001f, telegraphWidth);
        telegraphLine.material = new Material(Shader.Find("Sprites/Default"));
        telegraphLine.startColor = telegraphColor;
        telegraphLine.endColor = telegraphColor;
        telegraphLine.enabled = false;
    }

    private void SetTelegraphVisible(bool visible)
    {
        if (telegraphLine == null)
            return;

        telegraphLine.enabled = visible;
        if (visible)
            telegraphCycleStartTime = Time.time;
    }

    private void HideTelegraphTemporarily(float duration)
    {
        if (telegraphLine == null)
            return;

        if (telegraphHideRoutine != null)
            StopCoroutine(telegraphHideRoutine);

        telegraphHideRoutine = StartCoroutine(HideTelegraphRoutine(duration));
    }

    private IEnumerator HideTelegraphRoutine(float duration)
    {
        SetTelegraphVisible(false);
        if (duration > 0f)
            yield return WaitForSecondsCache.Get(duration);

        if (enemyAI != null && enemyAI.State.Equals(EnemyState.Attack))
            SetTelegraphVisible(showTelegraphLine);

        telegraphHideRoutine = null;
    }

    private void UpdateTelegraphLine()
    {
        if (!showTelegraphLine || telegraphLine == null || !telegraphLine.enabled)
            return;

        Transform origin = firePoint != null ? firePoint : transform;
        Vector3 start = origin.position;
        Vector3 dir = origin.forward;

        if (player != null)
        {
            Vector3 targetPos = player.position + Vector3.up * aimYOffset;
            Vector3 toTarget = targetPos - start;
            if (toTarget.sqrMagnitude > 0.0001f)
                dir = toTarget.normalized;
        }

        if (rotateYawOnly)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f)
                dir = origin.forward;
            else
                dir.Normalize();
        }

        Vector3 end;
        if (Physics.Raycast(start, dir, out var hit, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
        }
        else
        {
            float fallbackDistance = GetEffectiveDetectionRange();
            end = start + dir * fallbackDistance;
        }

        telegraphLine.SetPosition(0, start);
        telegraphLine.SetPosition(1, end);

        float flickerSpeed = Mathf.Max(0f, telegraphFlickerSpeed);
        float strength = Mathf.Clamp01(telegraphFlickerStrength);
        float flicker = 1f;
        if (fireCooldown > 0f)
        {
            float timeSinceLastShot = Time.time - lastShotTime;
            float timeToShot = fireCooldown - timeSinceLastShot;
            const float noFlickerWindow = 0.5f;

            if (timeToShot > noFlickerWindow)
            {
                float rampDuration = Mathf.Max(0.01f, fireCooldown - noFlickerWindow);
                float progress = Mathf.Clamp01(timeSinceLastShot / rampDuration);
                flickerSpeed = Mathf.Lerp(1f, 5f, progress);
                float flickerTime = Mathf.Max(0f, Time.time - telegraphCycleStartTime);
                flicker = flickerSpeed > 0f
                    ? (Mathf.Sin(flickerTime * flickerSpeed * Mathf.PI * 2f) * 0.5f + 0.5f)
                    : 1f;
            }
        }
        float alpha = Mathf.Lerp(1f - strength, 1f, flicker) * telegraphColor.a;
        Color c = new Color(telegraphColor.r, telegraphColor.g, telegraphColor.b, alpha);
        telegraphLine.startColor = c;
        telegraphLine.endColor = c;
        telegraphLine.startWidth = Mathf.Max(0.001f, telegraphWidth);
        telegraphLine.endWidth = Mathf.Max(0.001f, telegraphWidth);
    }

    private void OnDisable()
    {
        if (detectLoop != null)
        {
            StopCoroutine(detectLoop);
            detectLoop = null;
        }
        StopAttackLoop();
        SetTelegraphVisible(false);
    }

    protected bool AimAtTarget(Transform target)
    {
        if (target == null)
            return false;

        Vector3 targetPos = target.position + Vector3.up * aimYOffset;
        Vector3 dir = targetPos - transform.position;
        if (dir.sqrMagnitude <= 0.0001f)
            return false;

        Vector3 aimDir = dir.normalized;
        Vector3 planarDir = new Vector3(aimDir.x, 0f, aimDir.z);
        if (planarDir.sqrMagnitude <= 0.0001f)
            planarDir = transform.forward;

        if (rotateYawOnly)
        {
            aimDir = planarDir.normalized;
        }

        bool withinYaw = true;
        if (clampYaw && planarDir.sqrMagnitude > 0.0001f)
        {
            Vector3 referenceDir = planarDir.normalized;
            Transform parent = transform.parent;
            Vector3 localDir = parent != null ? parent.InverseTransformDirection(referenceDir) : referenceDir;
            float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            float clampedYaw = Mathf.Clamp(yaw, -maxYawLeft, maxYawRight);
            withinYaw = Mathf.Approximately(yaw, clampedYaw);
            float yawRad = clampedYaw * Mathf.Deg2Rad;
            Vector3 clampedLocalDir = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            Vector3 clampedWorldDir = parent != null ? parent.TransformDirection(clampedLocalDir) : clampedLocalDir;

            if (rotateYawOnly)
            {
                aimDir = clampedWorldDir.normalized;
            }
            else
            {
                aimDir = (clampedWorldDir.normalized + Vector3.up * aimDir.y).normalized;
            }

            if (!withinYaw)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(clampedWorldDir), turnSpeed * Time.deltaTime);
                return false;
            }
        }

        Quaternion targetRot = Quaternion.LookRotation(aimDir.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        return true;
    }

    private bool IsTargetWithinYawLimits(Vector3 targetPosition)
    {
        if (!clampYaw)
            return true;

        Vector3 dir = targetPosition - transform.position;
        Vector3 planarDir = new Vector3(dir.x, 0f, dir.z);
        if (planarDir.sqrMagnitude <= 0.0001f)
            return true;

        Transform parent = transform.parent;
        Vector3 localDir = parent != null ? parent.InverseTransformDirection(planarDir.normalized) : planarDir.normalized;
        float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        return yaw >= -maxYawLeft && yaw <= maxYawRight;
    }


    protected override void OnDamageTaken(float amount)
    {
        base.OnDamageTaken(amount);

        if (animationRestoreRoutine != null)
        {
            StopCoroutine(animationRestoreRoutine);
        }
        animationRestoreRoutine = StartCoroutine(RestoreAnimationAfterHit());
    }

    private IEnumerator RestoreAnimationAfterHit()
    {
        if (hitAnimationRecoveryDelay > 0f)
            yield return WaitForSecondsCache.Get(hitAnimationRecoveryDelay);

        if (enemyAI == null)
            yield break;

        switch (enemyAI.State)
        {
            case EnemyState.Attack:
                PlayAttackAnim();
                break;
            case EnemyState.Death:
                PlayDieAnim();
                break;
            default:
                RequestIdlePose(force: !suppressIdleWhileTargeting);
                break;
        }
    }

    protected void RequestIdlePose(bool force = false)
    {
        if (!force && suppressIdleWhileTargeting && isTargetEngaged)
            return;

        PlayIdleAnim();
    }
}
