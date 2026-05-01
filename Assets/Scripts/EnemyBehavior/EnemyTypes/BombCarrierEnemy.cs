// BombCarrierEnemy.cs
// Purpose: Enemy that carries explosive payloads and detonates under conditions.
// Works with: Explosion systems, EnemyStateMachineConfig.

using UnityEngine;
using System.Collections;
using Behaviors;
using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

public enum BombAttackBehavior
{
    ChargeAndExplode,   // Default: charge straight, explode on contact/timer
    StopAndLeap,        // Stop, leap at player, explode by proximity
    ZigZag,             // Zig-zag quickly toward player, explode on contact/timer
    TriggerCountdown    // Move until the player enters the trigger zone, then stop and count down in place
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
    private const string ExplodingAnimationStateName = "Exploding";

    [Serializable]
    private struct CountdownVfxEntry
    {
        [SerializeField, FormerlySerializedAs("prefab")]
        private GameObject effectObject;

        [SerializeField]
        private Transform anchor;

        [SerializeField]
        private Vector3 positionOffset;

        [SerializeField]
        private Vector3 rotationOffset;

        [SerializeField]
        private Vector3 scaleMultiplier;

        [SerializeField]
        private bool attachToAnchor;

        [SerializeField, Tooltip("Seconds to wait after countdown starts before this VFX appears.")]
        private float spawnDelay;

        public GameObject EffectObject => effectObject;
        public Transform Anchor => anchor;
        public Vector3 PositionOffset => positionOffset;
        public Vector3 RotationOffset => rotationOffset;
        public Vector3 ScaleMultiplier => scaleMultiplier == Vector3.zero ? Vector3.one : scaleMultiplier;
        public bool AttachToAnchor => attachToAnchor;
        public float SpawnDelay => Mathf.Max(0f, spawnDelay);
    }

    private sealed class ActiveCountdownVfxHandle
    {
        public GameObject Instance;
        public bool IsAttachedInstance;
    }

    [Header("Bomb Bot Settings")]
    [SerializeField, Tooltip("Radius at which the bomb bot will trigger its explosion when the player enters.")]
    private float triggerRadius = 1.5f;
    [SerializeField, Tooltip("Radius of the explosion when the bomb bot detonates. The default is 2x the trigger radius.")]
    public float explosionRadius = 3f;
    [SerializeField, Tooltip("Damage dealt to all targets within the explosion radius.")]
    private float explosionDamage = 100f;
    [SerializeField, Tooltip("If enabled, bomb explosions force-stagger the player.")]
    private bool staggerPlayerOnExplosion = true;
    [SerializeField, Range(0.05f, 2f), Tooltip("Forced stagger duration applied to the player on bomb explosion.")]
    private float playerExplosionStaggerDuration = 0.45f;
    [SerializeField, Tooltip("If enabled, bomb explosions knock the player back with the Knockback animation.")]
    private bool knockbackPlayerOnExplosion = true;
    [SerializeField, Min(0f), Tooltip("Magnitude of the knockback impulse applied to the player on explosion.")]
    private float explosionKnockbackForce = 18f;
    [SerializeField, Min(0f), Tooltip("Duration in seconds the player input is locked during the knockback animation on explosion.")]
    private float explosionKnockbackDuration = 0.6f;
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
    [SerializeField, Tooltip("Optional prefab to spawn when the bomb detonates. Leave empty to disable explosion VFX.")]
    private GameObject explosionVfxPrefab;
    [SerializeField, Tooltip("Optional anchor for the explosion VFX. If null, uses the enemy root transform.")]
    private Transform explosionVfxAnchor;
    [SerializeField, Tooltip("Local-space offset from the explosion VFX anchor.")]
    private Vector3 explosionVfxOffset = Vector3.zero;
    [SerializeField, Tooltip("Scale multiplier applied to the spawned explosion VFX prefab.")]
    private float explosionVfxScale = 1f;
    [SerializeField, Tooltip("If true, parent the explosion VFX to the anchor so it follows the enemy.")]
    private bool explosionVfxFollowsEnemy = true;
    [SerializeField, Tooltip("If true, draw the explosion radius gizmo in the editor.")]
    private bool showExplosionRadiusGizmo = true;
    private GameObject explosionVfxInstance;
    [SerializeField, Tooltip("How long the explosion visual remains after detonation.")]
    private float explosionVisualDuration = 0.5f;

    [Header("Countdown Heat VFX")]
    [SerializeField, Tooltip("Effects that spawn while the bomb bot is heating up during countdown. Each entry can have its own anchor and delay.")]
    private CountdownVfxEntry[] countdownVfxEntries = Array.Empty<CountdownVfxEntry>();

    [Header("Explosion Rumble")]
    [SerializeField, Tooltip("Duration of the rumble effect when the bomb explodes.")]
    private float rumbleDuration = 0.15f;
    [SerializeField, Tooltip("Low frequency of the rumble effect.")]
    private float rumbleLowFrequency = 0.35f;
    [SerializeField, Tooltip("High frequency of the rumble effect.")]
    private float rumbleHighFrequency = 0.35f;

    [Header("Explosion SFX")]
    [SerializeField, Tooltip("Sound effect to play when the bomb explodes.")]
    private AudioClip explosionSfx;

    // Spawn context
    private bool spawnedByAlarm = false;
    private AlarmCarrierEnemy alarmSource;
    private CrawlerPocket pocketSource;
    public CrawlerPocket Pocket { get; set; }

    // State
    private bool isExploding = false;
    private Coroutine attackRoutine;
    private Coroutine debugPreviewCoroutine;
    private IEnemyStateBehavior<BombStates, BombTriggers> deathBehavior;
    private readonly List<Coroutine> countdownVfxDelayCoroutines = new();
    private readonly List<ActiveCountdownVfxHandle> activeCountdownVfx = new();
    private readonly Dictionary<Transform, Vector3> countdownVfxBaseScales = new();

    [HideInInspector]
    public GameObject originalPrefab;

    public bool IsAttacking { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        handleLowHealth = false;
        deathBehavior = new DeathBehavior<BombStates, BombTriggers>();

        // Ensure a trigger SphereCollider exists for explosion/contact detection
        var trigger = GetComponent<SphereCollider>();
        if (trigger == null)
            trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = triggerRadius; // Use trigger radius for collision

        if (randomizeBehavior)
        {
            attackBehavior = (BombAttackBehavior)UnityEngine.Random.Range(0, System.Enum.GetValues(typeof(BombAttackBehavior)).Length);
        }
        InitializeStateMachine(BombStates.Idle);
        ConfigureStateMachine();

        EnsureExplosionVfxPrefabReady();
    }

    private void EnsureExplosionVfxPrefabReady()
    {
        if (explosionVfxPrefab == null && explosionVfxInstance != null)
        {
            Destroy(explosionVfxInstance);
            explosionVfxInstance = null;
        }
    }

    private Transform GetExplosionVfxAnchor()
    {
        return explosionVfxAnchor != null ? explosionVfxAnchor : transform;
    }

    private void EnsureExplosionVfxInstance()
    {
        if (explosionVfxPrefab == null)
        {
            if (explosionVfxInstance != null)
            {
                Destroy(explosionVfxInstance);
                explosionVfxInstance = null;
            }
            return;
        }

        if (explosionVfxInstance != null)
        {
            PositionExplosionVfxInstance();
            return;
        }

        Transform anchor = GetExplosionVfxAnchor();
        Vector3 spawnPosition = anchor.TransformPoint(explosionVfxOffset);
        Quaternion spawnRotation = anchor.rotation;

        explosionVfxInstance = Instantiate(explosionVfxPrefab, spawnPosition, spawnRotation);
        ApplyExplosionVfxScale();

        if (explosionVfxFollowsEnemy)
        {
            explosionVfxInstance.transform.SetParent(anchor);
            explosionVfxInstance.transform.localPosition = explosionVfxOffset;
            explosionVfxInstance.transform.localRotation = Quaternion.identity;
        }
        else
        {
            explosionVfxInstance.transform.position = spawnPosition;
            explosionVfxInstance.transform.rotation = spawnRotation;
        }

        explosionVfxInstance.SetActive(false);
    }

    private void PositionExplosionVfxInstance()
    {
        if (explosionVfxInstance == null)
            return;

        ApplyExplosionVfxScale();

        Transform anchor = GetExplosionVfxAnchor();
        if (explosionVfxFollowsEnemy)
        {
            explosionVfxInstance.transform.SetParent(anchor);
            explosionVfxInstance.transform.localPosition = explosionVfxOffset;
            explosionVfxInstance.transform.localRotation = Quaternion.identity;
        }
        else
        {
            explosionVfxInstance.transform.SetParent(null);
            explosionVfxInstance.transform.position = anchor.TransformPoint(explosionVfxOffset);
            explosionVfxInstance.transform.rotation = anchor.rotation;
        }
    }

    private void ApplyExplosionVfxScale()
    {
        if (explosionVfxInstance == null)
            return;

        float scale = Mathf.Max(0.01f, explosionVfxScale);
        explosionVfxInstance.transform.localScale = Vector3.one * scale;
    }

    private void PlayExplosionVfx()
    {
        EnsureExplosionVfxInstance();
        if (explosionVfxInstance == null)
            return;

        PositionExplosionVfxInstance();
        explosionVfxInstance.SetActive(true);

        var particleSystems = explosionVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var particleSystem in particleSystems)
        {
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        SoundManager.Instance.sfxSource.PlayOneShot(explosionSfx);
    }

    private void StopExplosionVfx()
    {
        if (explosionVfxInstance == null)
            return;

        var particleSystems = explosionVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var particleSystem in particleSystems)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        explosionVfxInstance.SetActive(false);
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
            .OnEntry(() =>
            {
                StopCountdownHeatVfx();
                PlayIdleAnim();
            })
            .Permit(BombTriggers.SeePlayer, BombStates.Approaching)
            .Permit(BombTriggers.Die, BombStates.Death);

        enemyAI.Configure(BombStates.Approaching)
            .OnEntry(() =>
            {
                StopCountdownHeatVfx();
                PlayIdleAnim();
                StartApproach();
            })
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
            .OnEntry(() =>
            {
                StopCountdownHeatVfx();
                PlayIdleAnim();
                StartReturnToPocket();
            })
            .Permit(BombTriggers.Die, BombStates.Death)
            .Permit(BombTriggers.SeePlayer, BombStates.Approaching);

        enemyAI.Configure(BombStates.Exploding)
            .OnEntry(Explode)
            .Permit(BombTriggers.Die, BombStates.Death)
            .Ignore(BombTriggers.Explode);

        enemyAI.Configure(BombStates.Death)
            .OnEntry(() =>
            {
                IsAttacking = false;
                if (attackRoutine != null)
                {
                    StopCoroutine(attackRoutine);
                    attackRoutine = null;
                }

                CancelDebugPreview();
                StopExplosionVfx();
                PlayExplodingAnim();
                deathBehavior?.OnEnter(this);
            })
            .Ignore(BombTriggers.Die)
            .Ignore(BombTriggers.Explode)
            .Ignore(BombTriggers.SeePlayer);

        // Allow explosion from any state except Exploding/Death
        PermitExplodeFromAllStates();
    }

    protected override void Start()
    {
        base.Start();

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

        PlayExplodingAnim();
        StartCountdownHeatVfx();
        ShowCountdownWarningVfx();
        EnemyBehaviorDebugLogBools.Log(nameof(BombCarrierEnemy), $"BombCarrierEnemy using attackBehavior: {attackBehavior}");
        attackRoutine = StartCoroutine(AttackBehaviorRoutine());
    }

    private void PlayExplodingAnim()
    {
        if (HasAnimatorState(animator, ExplodingAnimationStateName))
        {
            ForcePlayStateOn(animator, ExplodingAnimationStateName);
            return;
        }

        PlayAttackAnim();
    }

    private void ShowCountdownWarningVfx()
    {
        float countdownDuration = Mathf.Max(0.05f, explodeTimer);
        ShowAttackIndicator(customDuration: countdownDuration);

        if (attackIndicatorInstance == null)
            return;

        var particleSystems = attackIndicatorInstance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var particleSystem in particleSystems)
        {
            var main = particleSystem.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(countdownDuration);
            particleSystem.Play(true);
        }
    }

    private void StartCountdownHeatVfx()
    {
        StopCountdownHeatVfx();

        if (countdownVfxEntries == null || countdownVfxEntries.Length == 0)
            return;

        for (int i = 0; i < countdownVfxEntries.Length; i++)
        {
            CountdownVfxEntry entry = countdownVfxEntries[i];
            if (entry.EffectObject == null)
                continue;

            Transform anchor = entry.Anchor != null ? entry.Anchor : transform;
            if (anchor == null)
                continue;

            if (entry.SpawnDelay <= 0f)
            {
                SpawnCountdownVfxEntry(entry, anchor);
                continue;
            }

            Coroutine delayRoutine = StartCoroutine(SpawnCountdownVfxAfterDelay(entry, anchor));
            countdownVfxDelayCoroutines.Add(delayRoutine);
        }
    }

    private IEnumerator SpawnCountdownVfxAfterDelay(CountdownVfxEntry entry, Transform anchor)
    {
        yield return WaitForSecondsCache.Get(entry.SpawnDelay);

        if (!IsAttacking || isExploding)
            yield break;

        if (anchor == null || entry.EffectObject == null)
            yield break;

        SpawnCountdownVfxEntry(entry, anchor);
    }

    private void SpawnCountdownVfxEntry(CountdownVfxEntry entry, Transform anchor)
    {
        if (entry.EffectObject == null || anchor == null)
            return;

        if (ShouldUseAttachedCountdownInstance(entry.EffectObject))
        {
            ActivateAttachedCountdownInstance(entry, anchor);
            return;
        }

        Vector3 spawnPosition = anchor.TransformPoint(entry.PositionOffset);
        Quaternion spawnRotation = anchor.rotation * Quaternion.Euler(entry.RotationOffset);
        GameObject instance = Instantiate(entry.EffectObject, spawnPosition, spawnRotation);
        ApplyConfiguredCountdownScale(instance.transform, entry.ScaleMultiplier);

        if (entry.AttachToAnchor)
            instance.transform.SetParent(anchor, worldPositionStays: true);

        if (!instance.activeSelf)
            instance.SetActive(true);

        RefreshVfxPropertyBinders(instance);
        ReplayInstanceNow(instance);
        activeCountdownVfx.Add(new ActiveCountdownVfxHandle { Instance = instance });
    }

    private bool ShouldUseAttachedCountdownInstance(GameObject effectObject)
    {
        return effectObject != null && effectObject.scene.IsValid() && effectObject.transform.IsChildOf(transform);
    }

    private void ActivateAttachedCountdownInstance(CountdownVfxEntry entry, Transform anchor)
    {
        GameObject instance = entry.EffectObject;
        if (instance == null)
            return;

        if (entry.AttachToAnchor && anchor != null && instance.transform.parent != anchor)
            instance.transform.SetParent(anchor, worldPositionStays: true);

        instance.transform.localPosition = entry.PositionOffset;
        instance.transform.localRotation = Quaternion.Euler(entry.RotationOffset);
        ApplyConfiguredCountdownScale(instance.transform, entry.ScaleMultiplier);

        if (!instance.activeSelf)
            instance.SetActive(true);

        RefreshVfxPropertyBinders(instance);
        ReplayInstanceNow(instance);

        for (int i = 0; i < activeCountdownVfx.Count; i++)
        {
            if (activeCountdownVfx[i]?.Instance == instance)
                return;
        }

        activeCountdownVfx.Add(new ActiveCountdownVfxHandle { Instance = instance, IsAttachedInstance = true });
    }

    private void StopCountdownHeatVfx()
    {
        for (int i = 0; i < countdownVfxDelayCoroutines.Count; i++)
        {
            Coroutine pending = countdownVfxDelayCoroutines[i];
            if (pending != null)
                StopCoroutine(pending);
        }
        countdownVfxDelayCoroutines.Clear();

        for (int i = activeCountdownVfx.Count - 1; i >= 0; i--)
        {
            ActiveCountdownVfxHandle handle = activeCountdownVfx[i];
            if (handle?.Instance == null)
            {
                activeCountdownVfx.RemoveAt(i);
                continue;
            }

            if (handle.IsAttachedInstance)
                StopInstance(handle.Instance);
            else
                Destroy(handle.Instance);

            activeCountdownVfx.RemoveAt(i);
        }
        if (countdownVfxEntries == null)
            return;

        for (int i = 0; i < countdownVfxEntries.Length; i++)
        {
            GameObject effectObject = countdownVfxEntries[i].EffectObject;
            if (ShouldUseAttachedCountdownInstance(effectObject))
                StopInstance(effectObject);
        }
    }

    private void ApplyConfiguredCountdownScale(Transform target, Vector3 scaleMultiplier)
    {
        if (target == null)
            return;

        if (!countdownVfxBaseScales.TryGetValue(target, out Vector3 baseScale))
        {
            baseScale = target.localScale;
            countdownVfxBaseScales[target] = baseScale;
        }

        target.localScale = Vector3.Scale(baseScale, scaleMultiplier);
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
            case BombAttackBehavior.TriggerCountdown:
                yield return StartCoroutine(TriggerCountdownRoutine());
                break;
        }
        // After attack, trigger explosion
        if (canExplode)
            enemyAI.Fire(BombTriggers.Explode);
    }

    private IEnumerator TriggerCountdownRoutine()
    {
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        float timer = 0f;
        while (!isExploding && timer < explodeTimer)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        while (!canExplode && !isExploding)
            yield return null;

        if (!isExploding)
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
                    float delay = UnityEngine.Random.Range(stopBeforeLeapDelayRange.x, stopBeforeLeapDelayRange.y);
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
        IsAttacking = false;

        StopCountdownHeatVfx();

        HideAttackIndicator();

        PlayExplosionVfx();

        // Damage player and enemies in radius, but only if collider is not a trigger
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            if (!hit.isTrigger)
            {
                IHealthSystem health = hit.GetComponent<IHealthSystem>();
                health?.LoseHP(explosionDamage, rumbleDuration, rumbleLowFrequency, rumbleHighFrequency);

                if (health is PlayerHealthBarManager playerHealth)
                {
                    if (knockbackPlayerOnExplosion)
                    {
                        Vector3 knockDir = (hit.transform.position - transform.position);
                        knockDir.y = 0f;
                        if (knockDir.sqrMagnitude < 0.0001f) knockDir = Vector3.forward;
                        playerHealth.ApplyKnockbackReaction(knockDir.normalized, explosionKnockbackForce, explosionKnockbackDuration);
                    }
                    else if (staggerPlayerOnExplosion)
                    {
                        playerHealth.ApplyForcedStagger(playerExplosionStaggerDuration, resetCombo: true);
                    }
                }
            }
        }

        // Disable this enemy's visuals and logic, but keep the GameObject alive for the visual
        StartCoroutine(DisableAndDestroyAfterDelay(explosionVisualDuration));
    }

    [ContextMenu("Debug/Test Countdown Warning")]
    public void DebugPreviewCountdownWarning()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{nameof(BombCarrierEnemy)}] Enter Play Mode to preview the countdown warning.", this);
            return;
        }

        CancelDebugPreview();
        ShowCountdownWarningVfx();
    }

    [ContextMenu("Debug/Test Explosion Sequence")]
    public void DebugPreviewExplosionSequence()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{nameof(BombCarrierEnemy)}] Enter Play Mode to preview the explosion sequence.", this);
            return;
        }

        CancelDebugPreview();
        debugPreviewCoroutine = StartCoroutine(DebugPreviewExplosionSequenceRoutine());
    }

    public void CancelDebugPreview()
    {
        if (debugPreviewCoroutine != null)
        {
            StopCoroutine(debugPreviewCoroutine);
            debugPreviewCoroutine = null;
        }

        HideAttackIndicator();

        StopCountdownHeatVfx();
        StopExplosionVfx();
    }

    private IEnumerator DebugPreviewExplosionSequenceRoutine()
    {
        ShowCountdownWarningVfx();

        yield return WaitForSecondsCache.Get(Mathf.Max(0.05f, explodeTimer));

        HideAttackIndicator();
        PlayExplosionVfx();

        yield return WaitForSecondsCache.Get(Mathf.Max(0.05f, explosionVisualDuration));

        StopExplosionVfx();

        debugPreviewCoroutine = null;
    }

    private IEnumerator DisableAndDestroyAfterDelay(float delay)
    {
        // Disable all renderers except the explosion visual
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            bool isExplosionVfxRenderer = explosionVfxInstance != null
                && renderer.transform.IsChildOf(explosionVfxInstance.transform);
            if (!isExplosionVfxRenderer)
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
        if (isExploding || !canExplode || !other.CompareTag("Player"))
            return;

        if (attackBehavior == BombAttackBehavior.TriggerCountdown)
        {
            if (enemyAI.State == BombStates.Approaching)
                enemyAI.Fire(BombTriggers.InAttackRange);

            return;
        }

        enemyAI.Fire(BombTriggers.Explode);
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
        base.CheckHealthThreshold();
    }
    private void StartReturnToPocket()
    {
        IsAttacking = false;
        StopCountdownHeatVfx();

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
        CancelDebugPreview();
        StopCountdownHeatVfx();

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

            float attackStartDistance = attackBehavior == BombAttackBehavior.TriggerCountdown
                ? triggerRadius
                : chargeStartDistance;

            // Only allow attacking if cooldown is over
            if (canExplode && dist <= attackStartDistance)
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

    private static void ReplayInstanceNow(GameObject instance)
    {
        RestartParticleSystems(instance);
        RestartVisualEffects(instance);
    }

    private static void StopInstance(GameObject instance)
    {
        if (instance == null)
            return;

        var visualEffects = instance.GetComponentsInChildren<VisualEffect>(true);
        for (int i = 0; i < visualEffects.Length; i++)
        {
            VisualEffect visualEffect = visualEffects[i];
            if (visualEffect == null)
                continue;

            visualEffect.Stop();
        }

        var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
        }

        instance.SetActive(false);
    }

    private static void RefreshVfxPropertyBinders(GameObject instance)
    {
        if (instance == null)
            return;

        var binders = instance.GetComponentsInChildren<VFXPropertyBinder>(true);
        for (int i = 0; i < binders.Length; i++)
        {
            VFXPropertyBinder binder = binders[i];
            if (binder == null)
                continue;

            // Ensure enabled so UpdateBinding runs to snapshot the current position.
            binder.enabled = true;
        }
    }

    private static void RestartParticleSystems(GameObject instance)
    {
        if (instance == null)
            return;

        var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            if (!particleSystem.gameObject.activeSelf)
                particleSystem.gameObject.SetActive(true);

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    private static void RestartVisualEffects(GameObject instance)
    {
        if (instance == null)
            return;

        var visualEffects = instance.GetComponentsInChildren<VisualEffect>(true);
        for (int i = 0; i < visualEffects.Length; i++)
        {
            VisualEffect visualEffect = visualEffects[i];
            if (visualEffect == null)
                continue;

            if (!visualEffect.gameObject.activeSelf)
                visualEffect.gameObject.SetActive(true);

            if (!visualEffect.enabled)
                visualEffect.enabled = true;

            visualEffect.Stop();
            visualEffect.Reinit();
            visualEffect.Play();
        }
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        if (!showExplosionRadiusGizmo)
            return;

        float radius = Mathf.Max(0f, explosionRadius);
        if (radius <= 0f)
            return;

        Vector3 center = transform.position;

        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.18f);
        Gizmos.DrawSphere(center, radius);

        Gizmos.color = new Color(1f, 0.2f, 0.05f, 0.95f);
        Gizmos.DrawWireSphere(center, radius);
    }
}