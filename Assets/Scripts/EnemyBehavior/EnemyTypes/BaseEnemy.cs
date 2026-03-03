using System.Collections;
using System.Collections.Generic;
using Stateless;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using EnemyBehavior;

// BaseEnemy is generic so derived classes can define their own states and triggers
public abstract class BaseEnemy<TState, TTrigger> : BaseEnemyCore, IQueuedAttacker
    where TState : struct, System.Enum
    where TTrigger : struct, System.Enum
{
    [HideInInspector]
    public NavMeshAgent agent;
    public StateMachine<TState, TTrigger> enemyAI; // StateMachine<StateEnum, TriggerEnum> is from the Stateless library

    [Header("State Machine")]
    [SerializeField, Tooltip("The current state of the enemy's state machine. Read-only; for debugging and visualization.")]
    private TState currentState;

    [Header("Health")]
    [SerializeField, Tooltip("Maximum health value for this enemy.")]
    public float maxHealth = 100f;
    [SerializeField, MaxHealthSlider, Tooltip("Current health value for this enemy.")]
    public float currentHealth = 100f;
    [SerializeField, Tooltip("Percent of max health at which the enemy is considered low health (e.g., will flee or recover).")]
    protected float lowHealthThresholdPercent = 0.25f;
    [SerializeField, Tooltip("Enable or disable low health behavior (fleeing, recovering, etc.).")]
    protected bool handleLowHealth = true;

    [Header("Zone Management")]
    [SerializeField, Tooltip("The zone this enemy is currently in.")]
    public Zone currentZone;
    [SerializeField, Tooltip("If true, the enemy can relocate to other zones. If false, the enemy stays within its current zone.")]
    public bool allowZoneRelocation = true;
    [SerializeField, Tooltip("How long the enemy remains idle before relocating to another zone.")]
    public float idleTimerDuration = 15f;

    [Header("Detection")]
    [SerializeField, Tooltip("Radius of the detection sphere for spotting the player.")]
    protected float detectionRange = 10f;
    [SerializeField, Tooltip("Show the detection range gizmo in the Scene view.")]
    protected bool showDetectionGizmo = true;

    [Header("Attack")]
    [SerializeField, Tooltip("Damage dealt to the player per attack.")]
    public float damage = 10f;
    [SerializeField, Tooltip("Size of the attack box collider (width, height, depth) used for attack range.")]
    public Vector3 attackBoxSize = new Vector3(2f, 2f, 2f);
    [SerializeField, Tooltip("Distance in front of the enemy where the attack box is positioned.")]
    public float attackBoxDistance = 1.5f;
    [SerializeField, Tooltip("Vertical offset (in meters) applied to the attack box center.")]
    public float attackBoxHeightOffset = 0f;
    [SerializeField, Tooltip("Time in seconds between attacks (attack cooldown).")]
    public float attackInterval = 1.0f;
    [SerializeField, Tooltip("Time in seconds the attack box is enabled (attack active duration).")]
    public float attackActiveDuration = 0.5f;
    [SerializeField, Tooltip("Show the attack range gizmo in the Scene view.")]
    protected bool showAttackGizmo = true;

    [SerializeField, Tooltip("Determines whether their attack can be parried")]
    public bool canBeParried = true;

    [SerializeField, Tooltip("When true, hitbox enable/disable is controlled by animation events (Attack, AttackEnd). When false, uses timer-based hitbox duration.")]
    public bool useAnimationEventAttacks = false;

    [Header("Attack Indicator VFX")]
    [SerializeField, Tooltip("VFX prefab to spawn before an attack to warn the player. Leave empty to disable.")]
    protected GameObject attackIndicatorPrefab;
    [SerializeField, Tooltip("Position offset from the enemy's transform where the indicator spawns (local space).")]
    protected Vector3 attackIndicatorOffset = new Vector3(0f, 0f, 1.5f);
    [SerializeField, Tooltip("Seconds before the attack lands that the indicator appears. Adjust per-enemy for timing.")]
    protected float attackIndicatorLeadTime = 0.5f;
    [SerializeField, Tooltip("How long the indicator stays visible. Set to 0 to auto-hide when attack starts.")]
    protected float attackIndicatorDuration = 0f;
    [SerializeField, Tooltip("If true, indicator follows the enemy's position/rotation. If false, spawns at fixed world position.")]
    protected bool attackIndicatorFollowsEnemy = true;
    [SerializeField, Tooltip("Scale multiplier for the indicator VFX.")]
    protected float attackIndicatorScale = 1f;

    // Runtime state for attack indicator
    protected GameObject attackIndicatorInstance;
    protected Coroutine attackIndicatorCoroutine;

    [Header("Enemy Health Bar")]
    [SerializeField, Tooltip("Prefab for the enemy's health bar UI.")]
    public GameObject healthBarPrefab;
    [SerializeField, Tooltip("Optional anchor transform for the health bar instance. Defaults to this enemy's transform.")]
    private Transform healthBarAnchor;

    // Non-serialized fields
    [HideInInspector]
    public EnemyHealthBar healthBarInstance;
    protected SphereCollider detectionCollider;
    [HideInInspector]
    public BoxCollider attackCollider;

    [Header("Trigger Overrides")]
    [SerializeField, Tooltip("Optional detection trigger. Leave empty to auto-create a trigger that matches Detection Range.")]
    private SphereCollider detectionColliderOverride;
    [SerializeField, Tooltip("Optional melee attack trigger. Leave empty to auto-create a trigger that matches Attack Box settings.")]
    private BoxCollider attackColliderOverride;
    [HideInInspector]
    public bool isAttackBoxActive = false;
    [HideInInspector]
    public bool hasFiredLowHealth = false;
    protected Coroutine recoverCoroutine;
    protected Coroutine idleTimerCoroutine;
    protected Coroutine idleWanderCoroutine;
    protected Coroutine zoneArrivalCoroutine;
    protected Coroutine attackLoopCoroutine;
    //private Vector3 lastZoneCheckPosition;

    protected Renderer enemyRenderer;
    protected Animator animator;

    private bool deathSequenceTriggered;
    private Coroutine deathFallbackRoutine;

    // Cached animator parameter checks to avoid allocations from repeated animator.parameters access
    private bool _hasIsMovingParam;
    private bool _hasMoveSpeedParam;
    private bool _hasLocomotionState;
    private bool _animatorParamsCached;

    [Header("External Helper Roots")]
    [SerializeField, Tooltip("Any helper GameObjects that live outside this enemy's hierarchy (IK targets, FX anchors, etc.). They will be destroyed automatically when this enemy is destroyed.")]
    private List<GameObject> externalHelperRoots = new();

    [Header("Animation Settings")]
    [SerializeField, Tooltip("Animator state name used when forcing the idle pose.")]
    private string idleStateName = "Idle";
    [SerializeField, Tooltip("Animator state used when no locomotion parameter exists.")]
    private string locomotionStateName = "Locomotion";
    [SerializeField, Tooltip("Animator state used as a fallback for attack.")]
    private string attackStateName = "Attack";
    [SerializeField, Tooltip("Animator state used as a fallback for hit reactions.")]
    private string hitStateName = "Hit";
    [SerializeField, Tooltip("Animator state used as a fallback for death.")]
    private string dieStateName = "Die";
    [SerializeField, Tooltip("Animator trigger name for attack (optional).")]
    private string attackTriggerName = "Attack";
    [SerializeField, Tooltip("Animator trigger name for hit reactions (optional).")]
    private string hitTriggerName = "Hit";
    [SerializeField, Tooltip("Animator trigger name for death (optional).")]
    private string dieTriggerName = "Die";
    [SerializeField, Tooltip("Animator float parameter name for locomotion speed (optional).")]
    private string moveSpeedParameterName = "MoveSpeed";

    [Header("SFX")]
    [SerializeField, Tooltip("Audio clip to play when the enemy is hit.")]
    private AudioClip[] hitSFX;
    
    [Header("Movement SFX")]
    [SerializeField, Tooltip("Audio clip to loop while the enemy is moving.")]
    private AudioClip movementSFXClip;
    [SerializeField, Tooltip("Audio clip to play when the enemy stops moving (optional).")]
    private AudioClip movementStopSFXClip;
    [SerializeField, Range(0f, 1f), Tooltip("Volume multiplier for movement SFX.")]
    private float movementSFXVolume = 0.5f;
    [SerializeField, Tooltip("Duration in seconds for the movement SFX to fade out when stopping.")]
    private float movementSFXFadeOutDuration = 0.3f;
    [SerializeField, Tooltip("Minimum speed threshold to consider the enemy as moving.")]
    private float movementSFXSpeedThreshold = 0.1f;
    
    // Movement SFX runtime state
    private AudioSource movementAudioSource;
    private float originalMovementSFXVolume;
    private bool wasMovingForSFX;
    private Coroutine movementSFXFadeCoroutine;
    
    [Header("Behavior Profile")]
    [SerializeField, Tooltip("Optional behavior profile for NavMeshAgent settings. If assigned, these settings will be applied on Awake.")]
    public EnemyBehaviorProfile behaviorProfile;
    
    [HideInInspector]
    public Color patrolColor = Color.green;
    [HideInInspector]
    public Color chaseColor = Color.yellow;
    [HideInInspector]
    public Color attackColor = new Color(1f, 0.5f, 0f); // Orange
    [HideInInspector]
    public Color hitboxActiveColor = Color.red;

    private Transform playerTarget;
    public Transform PlayerTarget
    {
        get => playerTarget;
        set => playerTarget = value;
    }

    #region IQueuedAttacker Implementation
    
    /// <summary>
    /// Override in derived class to return true for boss enemies.
    /// Default is false (regular enemy).
    /// </summary>
    public virtual bool IsBoss => false;
    
    /// <summary>
    /// Returns true if this enemy is alive and able to attack.
    /// </summary>
    public bool IsAlive => currentHealth > 0f && gameObject != null && gameObject.activeInHierarchy;
    public override bool isAlive => IsAlive;
    
    /// <summary>
    /// Returns the GameObject for the queue manager.
    /// </summary>
    public GameObject AttackerGameObject => gameObject;
    
    /// <summary>
    /// Check if this enemy can attack right now (is at front of queue).
    /// Call this before starting an attack.
    /// </summary>
    public bool CanAttackFromQueue()
    {
        if (EnemyAttackQueueManager.Instance == null) return true; // No queue manager = free for all
        return EnemyAttackQueueManager.Instance.CanAttack(this);
    }
    
    /// <summary>
    /// Notify the queue that this enemy is beginning an attack.
    /// Call this when the attack starts.
    /// </summary>
    public void NotifyAttackBegin()
    {
        EnemyAttackQueueManager.Instance?.BeginAttack(this);
    }
    
    /// <summary>
    /// Notify the queue that this enemy finished attacking.
    /// Call this when the attack ends (hit or miss).
    /// </summary>
    public void NotifyAttackEnd()
    {
        EnemyAttackQueueManager.Instance?.FinishAttack(this);
    }
    
    /// <summary>
    /// Register this enemy with the attack queue.
    /// Called automatically in Start, but can be called manually for pooled enemies.
    /// </summary>
    public void RegisterWithAttackQueue()
    {
        EnemyAttackQueueManager.Instance?.Register(this);
    }
    
    /// <summary>
    /// Unregister this enemy from the attack queue.
    /// Called automatically in OnDestroy, but can be called manually for pooled enemies.
    /// </summary>
    public void UnregisterFromAttackQueue()
    {
        EnemyAttackQueueManager.Instance?.Unregister(this);
    }
    
    #endregion

    /// <summary>
    /// Ensures an EnemyHealthBar exists and is bound to this enemy's IHealthSystem implementation.
    /// Prefers an already-assigned instance (e.g., from the prefab hierarchy) before instantiating a new one.
    /// </summary>
    protected void EnsureHealthBarBinding()
    {
        if (healthBarInstance == null && healthBarPrefab != null)
        {
            Transform parent = healthBarAnchor != null ? healthBarAnchor : transform;
            GameObject instance = Instantiate(healthBarPrefab, parent);
            healthBarInstance = instance.GetComponent<EnemyHealthBar>();
            if (healthBarInstance == null)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning("BaseEnemy", $"[{name}] Health bar prefab is missing the EnemyHealthBar component.");
#endif
                Destroy(instance);
            }
        }

        if (healthBarInstance != null)
        {
            healthBarInstance.BindToHealthSystem(this);
        }
        else if (healthBarPrefab == null)
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning("BaseEnemy", $"[{name}] No healthBarPrefab assigned; enemy health will not be displayed.");
#endif
        }
    }

    // Awake is called when the script instance is being loaded
    protected virtual void Awake()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        agent = this.gameObject.GetComponent<NavMeshAgent>();
        
        // Apply behavior profile if assigned
        ApplyBehaviorProfile();

        EnsureRigidBodyForTriggers();
        EnsureDetectionCollider();
        EnsureAttackCollider();
        EnsurePlayerTargetReference();

        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = GetComponentInParent<Animator>();
        }
    }
    
    /// <summary>
    /// Applies the behavior profile settings to the NavMeshAgent.
    /// Called automatically in Awake if a profile is assigned.
    /// </summary>
    protected virtual void ApplyBehaviorProfile()
    {
        if (behaviorProfile == null || agent == null)
            return;
            
        // Apply NavMeshAgent settings from profile
        agent.speed = Random.Range(behaviorProfile.SpeedRange.x, behaviorProfile.SpeedRange.y);
        agent.acceleration = behaviorProfile.Acceleration;
        agent.angularSpeed = behaviorProfile.AngularSpeed;
        agent.stoppingDistance = behaviorProfile.StoppingDistance;
        agent.avoidancePriority = behaviorProfile.AvoidancePriority;
        
#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Applied behavior profile: speed={agent.speed:F1}, accel={behaviorProfile.Acceleration}, angular={behaviorProfile.AngularSpeed}, priority={behaviorProfile.AvoidancePriority}");
#endif
    }

    // Helper to initialize the state machine and inspector state
    protected void InitializeStateMachine(TState initialState)
    {
        enemyAI = new StateMachine<TState, TTrigger>(initialState);

        // Set the initial state for the Inspector
        currentState = enemyAI.State;

        // Update currentState only when the state machine transitions
        // Stateless provides this OnTransitioned event to hook into transitions natively
        enemyAI.OnTransitioned(t => currentState = t.Destination);
    }

    protected virtual void ConfigureStateMachine()
    {
        // Intentionally left blank for derived class override
    }

    protected virtual void Start()
    {
        // Register with the attack queue system
        RegisterWithAttackQueue();
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        // Not sure if Update will be needed in the base class
        // but it is here if we need it later
        // Trying to not use it as much as possible for performance reasons
    }

    // --- PASSIVE MOVEMENT AND BEHAVIOR METHODS ---
    public void SetEnemyColor(Color color)
    {
        if (enemyRenderer != null)
            enemyRenderer.material.color = color;
    }

    // --- ANIMATION API ---
    protected virtual void PlayIdleAnim()
    {
        PlayState(idleStateName);
    }

    protected void PlayIdleAnimOn(Animator target)
    {
        PlayStateOn(target, idleStateName);
    }

    protected void RegisterExternalHelper(GameObject helper)
    {
        if (helper == null)
            return;

        if (!externalHelperRoots.Contains(helper))
            externalHelperRoots.Add(helper);
    }

    protected void UnregisterExternalHelper(GameObject helper)
    {
        if (helper == null)
            return;

        externalHelperRoots.Remove(helper);
    }

    private void CleanupExternalHelpers()
    {
        foreach (var helper in externalHelperRoots)
        {
            if (helper != null)
                Destroy(helper);
        }

        externalHelperRoots.Clear();
    }

    private void EnsureRigidBodyForTriggers()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        rb.isKinematic = true;
    }

    private void EnsureDetectionCollider()
    {
        detectionCollider = detectionColliderOverride ?? detectionCollider;

        if (detectionCollider == null)
        {
            // Prefer an existing trigger sphere on this GameObject
            detectionCollider = GetComponent<SphereCollider>();
            if (detectionCollider != null && !detectionCollider.isTrigger)
            {
                detectionCollider = null;
            }
        }

        if (detectionCollider == null)
        {
            var detectionRoot = new GameObject("DetectionTrigger");
            detectionRoot.transform.SetParent(transform);
            detectionRoot.transform.localPosition = Vector3.zero;
            detectionRoot.transform.localRotation = Quaternion.identity;
            detectionRoot.transform.localScale = Vector3.one;
            detectionRoot.layer = gameObject.layer;
            detectionCollider = detectionRoot.AddComponent<SphereCollider>();
            detectionColliderOverride = detectionCollider;
        }

        detectionCollider.isTrigger = true;
        detectionCollider.radius = GetUnscaledRadiusForRange(detectionCollider, detectionRange);
    }

    private void EnsureAttackCollider()
    {
        attackCollider = attackColliderOverride ?? attackCollider;

        if (attackCollider == null)
        {
            // Prefer an existing trigger box on this GameObject
            attackCollider = GetComponent<BoxCollider>();
            if (attackCollider != null && !attackCollider.isTrigger)
            {
                attackCollider = null;
            }
        }

        if (attackCollider == null)
        {
            var attackRoot = new GameObject("AttackTrigger");
            attackRoot.transform.SetParent(transform);
            attackRoot.transform.localPosition = Vector3.zero;
            attackRoot.transform.localRotation = Quaternion.identity;
            attackRoot.transform.localScale = Vector3.one;
            attackRoot.layer = gameObject.layer;
            attackCollider = attackRoot.AddComponent<BoxCollider>();
            attackColliderOverride = attackCollider;
        }

        attackCollider.isTrigger = true;
        attackCollider.size = attackBoxSize;
        attackCollider.center = new Vector3(0f, attackBoxHeightOffset, attackBoxDistance);
        attackCollider.enabled = false;
    }

    private void EnsurePlayerTargetReference()
    {
        if (playerTarget != null && playerTarget.gameObject != null)
            return;

        // Use PlayerPresenceManager if available
        if (PlayerPresenceManager.IsPlayerPresent)
        {
            playerTarget = PlayerPresenceManager.PlayerTransform;
        }
        else
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTarget = playerObj.transform;
            }
        }
    }

    private void EnsurePlayerTargetReference(Transform candidate)
    {
        if (candidate == null)
            return;

        if (playerTarget == null || playerTarget.gameObject == null || !playerTarget.gameObject.activeInHierarchy)
        {
            playerTarget = candidate;
        }
    }

    protected virtual void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        CleanupExternalHelpers();
        
        // Stop movement SFX
        ForceStopMovementSFX();
        
        // Unregister from the attack queue system
        UnregisterFromAttackQueue();
    }

    protected virtual void PlayLocomotionAnim(float moveSpeed)
    {
        if (animator == null)
            return;

        // Cache animator parameter checks on first use to avoid allocations
        if (!_animatorParamsCached)
        {
            _hasIsMovingParam = AnimatorHasParameter("IsMoving", AnimatorControllerParameterType.Bool);
            _hasMoveSpeedParam = !string.IsNullOrEmpty(moveSpeedParameterName) && 
                                 AnimatorHasParameter(moveSpeedParameterName, AnimatorControllerParameterType.Float);
            _hasLocomotionState = AnimatorHasState(locomotionStateName);
            _animatorParamsCached = true;
        }

        // Drive additive locomotion overlays when the optional IsMoving bool exists
        if (_hasIsMovingParam)
        {
            bool isMoving = moveSpeed > 0.05f;
            animator.SetBool("IsMoving", isMoving);
        }

        if (_hasMoveSpeedParam)
        {
            animator.SetFloat(moveSpeedParameterName, moveSpeed);
        }
        else if (_hasLocomotionState)
        {
            PlayState(locomotionStateName);
        }
        // If neither parameter nor state exists, just skip locomotion animation
        
        // Update movement SFX based on current speed
        UpdateMovementSFX(moveSpeed);
    }

    protected virtual void PlayAttackAnim()
    {
        if (!TrySetTrigger(attackTriggerName))
        {
            PlayState(attackStateName);
        }
    }

    protected void PlayAttackAnimOn(Animator target)
    {
        if (!TrySetTriggerOn(target, attackTriggerName))
        {
            PlayStateOn(target, attackStateName);
        }
    }

    protected virtual void PlayHitAnim()
    {
        if (!TrySetTrigger(hitTriggerName))
        {
            PlayState(hitStateName);
        }
    }

    protected void PlayHitAnimOn(Animator target)
    {
        if (!TrySetTriggerOn(target, hitTriggerName))
        {
            PlayStateOn(target, hitStateName);
        }
    }

    protected virtual void PlayDieAnim()
    {
        if (!TrySetTrigger(dieTriggerName))
        {
            PlayState(dieStateName);
        }
    }

    protected void PlayDieAnimOn(Animator target)
    {
        if (!TrySetTriggerOn(target, dieTriggerName))
        {
            PlayStateOn(target, dieStateName);
        }
    }

    private bool TrySetTrigger(string triggerName)
    {
        return TrySetTriggerOn(animator, triggerName);
    }

    private bool TrySetTriggerOn(Animator target, string triggerName)
    {
        if (target == null || string.IsNullOrEmpty(triggerName))
            return false;

        if (!AnimatorHasParameter(target, triggerName, AnimatorControllerParameterType.Trigger))
            return false;

        target.ResetTrigger(triggerName);
        target.SetTrigger(triggerName);
        return true;
    }

    private bool AnimatorHasParameter(string parameterName, AnimatorControllerParameterType type)
    {
        return AnimatorHasParameter(animator, parameterName, type);
    }

    private bool AnimatorHasParameter(Animator target, string parameterName, AnimatorControllerParameterType type)
    {
        if (target == null || string.IsNullOrEmpty(parameterName))
            return false;

        foreach (var parameter in target.parameters)
        {
            if (parameter.type == type && parameter.name == parameterName)
                return true;
        }
        return false;
    }

    private bool AnimatorHasState(string stateName, int layerIndex = 0)
    {
        return AnimatorHasState(animator, stateName, layerIndex);
    }

    private bool AnimatorHasState(Animator target, string stateName, int layerIndex = 0)
    {
        if (target == null || string.IsNullOrEmpty(stateName))
            return false;

        // Check if the state exists by trying to get its hash
        int stateHash = Animator.StringToHash(stateName);
        return target.HasState(layerIndex, stateHash);
    }

    private void PlayState(string stateName)
    {
        PlayStateOn(animator, stateName);
    }

    private void PlayStateOn(Animator target, string stateName)
    {
        if (target == null || string.IsNullOrEmpty(stateName))
            return;

        target.Play(stateName, 0, 0f);
    }

    public virtual void TriggerAttackAnimation()
    {
        PlayAttackAnim();
    }

    #region Animation Event Receivers
    // Buffer for physics overlap checks in animation events
    private static readonly Collider[] animEventHitBuffer = new Collider[16];
    private bool animEventDamageDealtThisAttack = false;

    /// <summary>
    /// Animation Event receiver: Called by animation to enable the attack hitbox.
    /// Add an Animation Event named "Attack" at the frame where the attack should deal damage.
    /// </summary>
    public virtual void Attack()
    {
        if (!useAnimationEventAttacks) return;
        
        EnableAttackHitbox();
        DealDamageOnAnimationEvent();
#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Animation Event: Attack - Hitbox ENABLED");
#endif
    }

    /// <summary>
    /// Animation Event receiver: Called by animation to disable the attack hitbox.
    /// Add an Animation Event named "AttackEnd" at the frame where the attack should stop dealing damage.
    /// </summary>
    public virtual void AttackEnd()
    {
        if (!useAnimationEventAttacks) return;
        
        DisableAttackHitbox();
        animEventDamageDealtThisAttack = false; // Reset for next attack
#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Animation Event: AttackEnd - Hitbox DISABLED");
#endif
    }

    /// <summary>
    /// Deals damage to player if they are in the attack box when animation event fires.
    /// Only deals damage once per attack cycle.
    /// </summary>
    protected virtual void DealDamageOnAnimationEvent()
    {
        if (animEventDamageDealtThisAttack) return;

        Vector3 boxCenter = transform.position + transform.forward * attackBoxDistance;
        boxCenter += Vector3.up * attackBoxHeightOffset;
        Vector3 boxHalfExtents = attackBoxSize * 0.5f;

        int hitCount = Physics.OverlapBoxNonAlloc(boxCenter, boxHalfExtents, animEventHitBuffer, transform.rotation);

        for (int i = 0; i < hitCount; i++)
        {
            var hit = animEventHitBuffer[i];
            if (hit.CompareTag("Player"))
            {
                // Check for parry
                if (Utilities.Combat.CombatManager.isParrying && canBeParried)
                {
                    Utilities.Combat.CombatManager.ParrySuccessful();
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Attack parried by player.");
#endif
                    animEventDamageDealtThisAttack = true;
                    return;
                }

                // Get health system and apply damage
                if (hit.TryGetComponent<IHealthSystem>(out var healthSystem))
                {
                    float dmg = damage;
                    
                    // Check for guard
                    if (Utilities.Combat.CombatManager.isGuarding)
                    {
                        dmg *= 0.25f;
#if UNITY_EDITOR
                        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Attack guarded. Applying reduced damage {dmg}.");
#endif
                    }

                    healthSystem.LoseHP(dmg);
                    animEventDamageDealtThisAttack = true;
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Animation event attack dealt {dmg} damage to player.");
#endif
                }
                return;
            }
        }
    }

    /// <summary>
    /// Enables the attack hitbox collider and sets the active flag.
    /// Can be called directly or via animation events.
    /// </summary>
    public void EnableAttackHitbox()
    {
        isAttackBoxActive = true;
        if (attackCollider != null)
            attackCollider.enabled = true;
        SetEnemyColor(hitboxActiveColor);
    }

    /// <summary>
    /// Disables the attack hitbox collider and clears the active flag.
    /// Can be called directly or via animation events.
    /// </summary>
    public void DisableAttackHitbox()
    {
        isAttackBoxActive = false;
        if (attackCollider != null)
            attackCollider.enabled = false;
        SetEnemyColor(attackColor);
    }
    #endregion

    #region Attack Indicator VFX
    /// <summary>
    /// Shows the attack indicator VFX. Call this before an attack to warn the player.
    /// Can be called from code (timer-based) or via animation events.
    /// </summary>
    /// <param name="customOffset">Optional: Override the default offset for this specific attack.</param>
    /// <param name="customDuration">Optional: Override the default duration for this specific attack.</param>
    public virtual void ShowAttackIndicator(Vector3? customOffset = null, float? customDuration = null)
    {
        if (attackIndicatorPrefab == null) return;

        // Clean up any existing indicator
        HideAttackIndicator();

        Vector3 offset = customOffset ?? attackIndicatorOffset;
        Vector3 spawnPos = transform.TransformPoint(offset);
        Quaternion spawnRot = transform.rotation;

        attackIndicatorInstance = Instantiate(attackIndicatorPrefab, spawnPos, spawnRot);
        
        if (attackIndicatorScale != 1f)
        {
            attackIndicatorInstance.transform.localScale *= attackIndicatorScale;
        }

        if (attackIndicatorFollowsEnemy)
        {
            attackIndicatorInstance.transform.SetParent(transform);
            attackIndicatorInstance.transform.localPosition = offset;
            attackIndicatorInstance.transform.localRotation = Quaternion.identity;
        }

        float duration = customDuration ?? attackIndicatorDuration;
        if (duration > 0f)
        {
            attackIndicatorCoroutine = StartCoroutine(HideIndicatorAfterDelay(duration));
        }

#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Attack indicator shown at offset {offset}");
#endif
    }

    /// <summary>
    /// Hides/destroys the attack indicator VFX.
    /// Called automatically when attack starts (if duration is 0) or after duration expires.
    /// Can also be called manually via animation events.
    /// </summary>
    public virtual void HideAttackIndicator()
    {
        if (attackIndicatorCoroutine != null)
        {
            StopCoroutine(attackIndicatorCoroutine);
            attackIndicatorCoroutine = null;
        }

        if (attackIndicatorInstance != null)
        {
            Destroy(attackIndicatorInstance);
            attackIndicatorInstance = null;
        }
    }

    /// <summary>
    /// Animation Event receiver: Shows the attack indicator.
    /// Add this animation event at the start of the attack windup.
    /// </summary>
    public void AttackIndicatorStart()
    {
        ShowAttackIndicator();
    }

    /// <summary>
    /// Animation Event receiver: Hides the attack indicator.
    /// Add this animation event when the attack begins (hitbox enabled) or when indicator should disappear.
    /// </summary>
    public void AttackIndicatorEnd()
    {
        HideAttackIndicator();
    }

    /// <summary>
    /// Shows the attack indicator and automatically starts the attack after the lead time.
    /// Useful for timer-based attacks that want indicator → attack flow.
    /// </summary>
    /// <param name="onIndicatorComplete">Action to invoke when lead time elapses (e.g., enable hitbox).</param>
    protected Coroutine ShowAttackIndicatorWithCallback(System.Action onIndicatorComplete)
    {
        return StartCoroutine(AttackIndicatorSequence(onIndicatorComplete));
    }

    private IEnumerator AttackIndicatorSequence(System.Action onIndicatorComplete)
    {
        ShowAttackIndicator();
        yield return WaitForSecondsCache.Get(attackIndicatorLeadTime);
        
        // Auto-hide if duration was 0 (hide when attack starts)
        if (attackIndicatorDuration <= 0f)
        {
            HideAttackIndicator();
        }
        
        onIndicatorComplete?.Invoke();
    }

    private IEnumerator HideIndicatorAfterDelay(float delay)
    {
        yield return WaitForSecondsCache.Get(delay);
        HideAttackIndicator();
    }

    /// <summary>
    /// Gets the world position where the attack indicator should spawn.
    /// Override in derived classes for custom positioning (e.g., at muzzle for turrets).
    /// </summary>
    protected virtual Vector3 GetAttackIndicatorWorldPosition()
    {
        return transform.TransformPoint(attackIndicatorOffset);
    }

    /// <summary>
    /// Override in derived classes to provide a custom attack indicator prefab per attack type.
    /// </summary>
    protected virtual GameObject GetAttackIndicatorPrefab()
    {
        return attackIndicatorPrefab;
    }
    #endregion

    protected virtual void OnDamageTaken(float amount)
    {
        if (currentHealth > 0f)
        {
            PlayHitAnim();
            PlaySFXOnHit();
        }
    }

    public virtual void UpdateCurrentZone()
    {
#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"{gameObject.name} Updating current zone.");
#endif
        // Use ZoneManager if available for cached zones (avoids FindObjectsByType allocation)
        Zone[] zones = ZoneManager.Instance != null 
            ? ZoneManager.Instance.GetAllZones() 
            : Object.FindObjectsByType<Zone>(FindObjectsSortMode.None);
        foreach (var zone in zones)
        {
            if (zone.Contains(transform.position))
            {
                currentZone = zone;
                return;
            }
        }
        currentZone = null; // Not in any zone
    }
    // --- HEALTH MANAGEMENT METHODS ---
    // --- IHealthSystem Implementation ---
    // Property for currentHP (read-only for interface, uses currentHealth internally)
    public override float currentHP => currentHealth;

    // Property for maxHP (read-only for interface, uses maxHealth internally)
    public override float maxHP => maxHealth;

    // LoseHP is called to apply damage to the enemy
    public override void LoseHP(float damage)
    {
        if (damage <= 0f)
            return;

        float previousHealth = currentHealth;
        SetHealth(currentHealth - damage);

        float actualDamage = Mathf.Max(0f, previousHealth - currentHealth);
        if (actualDamage > 0f)
        {
            OnDamageTaken(actualDamage);
        }
    }

    // HealHP is called to restore health (used by RecoverBehavior)
    public override void HealHP(float hp)
    {
        SetHealth(currentHealth + hp);
    }

    private void PlaySFXOnHit()
    {
        if (hitSFX != null && hitSFX.Length > 0)
        {
            int index = Random.Range(0, hitSFX.Length);
            SoundManager.Instance.sfxSource.PlayOneShot(hitSFX[index]);
        }
    }

    #region Movement SFX
    /// <summary>
    /// Updates the movement SFX based on the current movement speed.
    /// Call this from Update or from locomotion methods when the enemy's speed changes.
    /// </summary>
    /// <param name="currentSpeed">The current movement speed of the enemy.</param>
    protected virtual void UpdateMovementSFX(float currentSpeed)
    {
        if (movementSFXClip == null) return;
        
        bool isMoving = currentSpeed > movementSFXSpeedThreshold;
        
        if (isMoving && !wasMovingForSFX)
        {
            // Started moving - play movement SFX
            StartMovementSFX();
        }
        else if (!isMoving && wasMovingForSFX)
        {
            // Stopped moving - fade out and play stop clip
            StopMovementSFX();
        }
        
        wasMovingForSFX = isMoving;
    }

    /// <summary>
    /// Starts playing the movement SFX loop.
    /// </summary>
    protected virtual void StartMovementSFX()
    {
        // Stop any ongoing fade
        if (movementSFXFadeCoroutine != null)
        {
            StopCoroutine(movementSFXFadeCoroutine);
            movementSFXFadeCoroutine = null;
        }
        
        // Use SoundManager's sfxSource
        if (SoundManager.Instance == null || SoundManager.Instance.sfxSource == null) return;
        
        movementAudioSource = SoundManager.Instance.sfxSource;
        originalMovementSFXVolume = movementAudioSource.volume;
        
        movementAudioSource.clip = movementSFXClip;
        movementAudioSource.volume = originalMovementSFXVolume * movementSFXVolume;
        movementAudioSource.loop = true;
        
        if (!movementAudioSource.isPlaying)
        {
            movementAudioSource.Play();
        }

#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Movement SFX started.");
#endif
    }

    /// <summary>
    /// Stops the movement SFX with a smooth fade out.
    /// </summary>
    protected virtual void StopMovementSFX()
    {
        if (movementAudioSource == null || !movementAudioSource.isPlaying) return;
        
        // Start fade out coroutine
        if (movementSFXFadeOutDuration > 0f)
        {
            movementSFXFadeCoroutine = StartCoroutine(FadeOutMovementSFX());
        }
        else
        {
            // Immediate stop
            movementAudioSource.Stop();
            movementAudioSource.volume = originalMovementSFXVolume;
            PlayMovementStopSFX();
        }

#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Movement SFX stopping (fade: {movementSFXFadeOutDuration}s).");
#endif
    }

    /// <summary>
    /// Coroutine to smoothly fade out the movement SFX.
    /// </summary>
    private IEnumerator FadeOutMovementSFX()
    {
        if (movementAudioSource == null) yield break;
        
        float startVolume = movementAudioSource.volume;
        float elapsed = 0f;
        
        while (elapsed < movementSFXFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / movementSFXFadeOutDuration;
            movementAudioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }
        
        movementAudioSource.Stop();
        movementAudioSource.volume = originalMovementSFXVolume; // Reset volume for SoundManager
        
        PlayMovementStopSFX();
        
        movementSFXFadeCoroutine = null;
    }

    /// <summary>
    /// Plays the optional stop SFX when movement ends.
    /// </summary>
    private void PlayMovementStopSFX()
    {
        if (movementStopSFXClip != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.sfxSource.PlayOneShot(movementStopSFXClip, movementSFXVolume);
        }
    }

    /// <summary>
    /// Immediately stops all movement SFX without fade. Call this on death or disable.
    /// </summary>
    protected void ForceStopMovementSFX()
    {
        if (movementSFXFadeCoroutine != null)
        {
            StopCoroutine(movementSFXFadeCoroutine);
            movementSFXFadeCoroutine = null;
        }
        
        if (movementAudioSource != null && movementAudioSource.clip == movementSFXClip)
        {
            movementAudioSource.Stop();
            movementAudioSource.volume = originalMovementSFXVolume;
        }
        
        wasMovingForSFX = false;
    }
    #endregion

    // SetHealth now clamps and updates health, but expects the new value
    public virtual void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        if (currentHealth > 0f && deathSequenceTriggered)
        {
            deathSequenceTriggered = false;
            if (deathFallbackRoutine != null)
            {
                StopCoroutine(deathFallbackRoutine);
                deathFallbackRoutine = null;
            }
        }
        CheckHealthThreshold();
    }

    public virtual void CheckHealthThreshold()
    {
        if (currentHealth <= 0f)
        {
            if (!deathSequenceTriggered)
            {
                deathSequenceTriggered = true;
                
                // Disable colliders immediately to prevent lock-on targeting during death
                DisableCollidersForDeath();
                
                // NOTE: OnDeath event is now fired AFTER death animation completes
                // See DeathBehavior.OnDeathSequenceComplete() or DeathFallbackRoutine()

                EnemyBehaviorDebugLogBools.Log("BaseEnemy", "Health reached 0, triggering death sequence.");
                
                bool fired = TryFireTriggerByName("Die");
                if (!fired)
                {
                    BeginDeathFallback();
                }
            }
            return;
        }

        if (!handleLowHealth)
            return;

        if (!hasFiredLowHealth && currentHealth <= maxHealth * lowHealthThresholdPercent)
        {
            hasFiredLowHealth = true;
            TryFireTriggerByName("LowHealth");
        }
    }

    /// <summary>
    /// Called when the death animation/sequence has fully completed.
    /// This fires the OnDeath event and should be called by DeathBehavior or death fallback.
    /// </summary>
    public void OnDeathSequenceComplete()
    {
        InvokeOnDeath();
#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Death sequence complete. OnDeath event fired.");
#endif
    }

    /// <summary>
    /// Called when the enemy should begin its spawn sequence (e.g., play spawn animation).
    /// Override in derived classes for custom spawn behavior.
    /// Call base.Spawn() to fire the OnSpawn event.
    /// </summary>
    public override void Spawn()
    {
        // Fire the spawn event for encounter tracking
        InvokeOnSpawn();

        gameObject.SetActive(true);
        
        // Derived classes can override to play spawn animations, enable AI, etc.
#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Spawn() called. Override in derived class for custom spawn animation.");
#endif
    }

    /// <summary>
    /// Resets the enemy to its initial state (e.g., when player leaves encounter zone).
    /// Restores health, clears flags, and resets state machine if applicable.
    /// Override in derived classes for additional reset behavior.
    /// Call base.ResetEnemy() to ensure proper reset and event firing.
    /// </summary>
    public override void ResetEnemy()
    {
        // Restore health to max
        currentHealth = maxHealth;
        
        // Clear death and low health flags
        deathSequenceTriggered = false;
        hasFiredLowHealth = false;
        
        // Stop any death fallback routine
        if (deathFallbackRoutine != null)
        {
            StopCoroutine(deathFallbackRoutine);
            deathFallbackRoutine = null;
        }
        
        // Clean up any active attack indicator
        HideAttackIndicator();
        
        // Re-enable the agent if it was disabled
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
        }
        
        // Re-enable health bar if it was disabled during death
        if (healthBarInstance != null && !healthBarInstance.gameObject.activeSelf)
        {
            healthBarInstance.gameObject.SetActive(true);
        }
        
        // Re-enable colliders that were disabled during death (for lock-on targeting)
        EnableCollidersForReset();
        
        // Reset the state machine to Idle state so enemies don't spawn in Death state
        ResetStateMachineToIdle();
        
        // Ensure the GameObject is inactive (ready for Spawn to activate)
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
        
        // Re-register with attack queue if needed
        RegisterWithAttackQueue();
        
        // Fire the reset event for encounter tracking
        InvokeOnReset();
        
#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] ResetEnemy() called. Health restored to {maxHealth}. State machine reset.");
#endif
    }

    /// <summary>
    /// Resets the state machine to Idle state. Override in derived classes if a different
    /// initial state is needed or if state machine needs special reset handling.
    /// </summary>
    protected virtual void ResetStateMachineToIdle()
    {
        // Try to transition to Idle via a trigger if possible
        // This handles the case where state machine doesn't allow direct state setting
        if (enemyAI != null)
        {
            // Stateless library doesn't support direct state setting, so we need to
            // reinitialize the state machine entirely for a clean reset
            var initialState = GetInitialState();
            enemyAI = new Stateless.StateMachine<TState, TTrigger>(initialState);
            ConfigureStateMachine();
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] State machine reinitialized to {initialState}.");
#endif
        }
    }

    /// <summary>
    /// Returns the initial state for this enemy type. Override in derived classes
    /// to specify a different starting state.
    /// </summary>
    protected virtual TState GetInitialState()
    {
        // Default implementation tries to return the first enum value (usually Idle)
        // Derived classes should override this to return their specific initial state
        var values = System.Enum.GetValues(typeof(TState));
        if (values.Length > 0)
            return (TState)values.GetValue(0);
        return default;
    }

    // Method to fire triggers safely by value, returns true if fired
    protected bool FireTrigger(TTrigger trigger)
    {
        if (enemyAI.CanFire(trigger))
        {
            enemyAI.Fire(trigger);
            return true;
        }
        else
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning("BaseEnemy", $"Cannot fire trigger {trigger} from state {enemyAI.State}");
#endif
            return false;
        }
    }

    // Helper to fire triggers by name (string), returns true if fired
    public virtual bool TryFireTriggerByName(string triggerName)
    {
        if (System.Enum.TryParse(triggerName, out TTrigger trigger))
        {
            return FireTrigger(trigger);
        }
        else
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning("BaseEnemy", $"{typeof(TTrigger).Name} does not contain a '{triggerName}' trigger. Check your enum definition.");
#endif
            return false;
        }
    }

    private void BeginDeathFallback()
    {
        if (deathFallbackRoutine != null)
            return;

        deathFallbackRoutine = StartCoroutine(DeathFallbackRoutine());
    }

    /// <summary>
    /// Disables colliders to prevent lock-on targeting and other interactions during death.
    /// Called when the death sequence begins.
    /// </summary>
    protected virtual void DisableCollidersForDeath()
    {
        // Clean up any active attack indicator
        HideAttackIndicator();
        
        // Stop movement SFX immediately
        ForceStopMovementSFX();
        
        // Disable detection collider
        if (detectionCollider != null)
            detectionCollider.enabled = false;
        
        // Disable attack collider
        if (attackCollider != null)
            attackCollider.enabled = false;
        
        // Disable any other colliders on the main GameObject (used for lock-on)
        var mainCollider = GetComponent<Collider>();
        if (mainCollider != null && mainCollider != detectionCollider && mainCollider != attackCollider)
            mainCollider.enabled = false;

#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Colliders disabled for death sequence (lock-on prevention).");
#endif
    }

    /// <summary>
    /// Re-enables colliders after enemy reset for lock-on targeting and detection.
    /// Called when the enemy is reset by the encounter system.
    /// </summary>
    protected virtual void EnableCollidersForReset()
    {
        // Re-enable detection collider
        if (detectionCollider != null)
            detectionCollider.enabled = true;
        
        // Attack collider should remain disabled until an attack (keep it off)
        // attackCollider is enabled/disabled by EnableAttackHitbox/DisableAttackHitbox
        
        // Re-enable any main collider on the GameObject
        var mainCollider = GetComponent<Collider>();
        if (mainCollider != null && mainCollider != detectionCollider && mainCollider != attackCollider)
            mainCollider.enabled = true;

#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log("BaseEnemy", $"[{name}] Colliders re-enabled after reset.");
#endif
    }

    private IEnumerator DeathFallbackRoutine()
    {
        PlayDieAnim();
        if (agent != null)
        {
            agent.ResetPath();
            agent.enabled = false;
        }

        yield return WaitForSecondsCache.Get(3f);

        // Fire OnDeath event now that death animation has completed
        OnDeathSequenceComplete();

        // Hide health bar but don't destroy it (can be re-enabled on reset)
        if (healthBarInstance != null)
        {
            healthBarInstance.gameObject.SetActive(false);
        }

        CleanupExternalHelpers();
        deathFallbackRoutine = null;
        
        // Disable instead of destroy for pooling/encounter reset support
        gameObject.SetActive(false);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (enemyAI == null) return;
        
        // Skip if not Player layer (avoid checking enemy-to-enemy collisions)
        if (!other.CompareTag("Player")) return;

        EnsurePlayerTargetReference(other.transform);

        // Check detection collider
        if (detectionCollider != null && detectionCollider.enabled)
        {
            // Use sqrMagnitude for faster distance check instead of bounds.Contains
            float sqrDist = (other.transform.position - transform.position).sqrMagnitude;
            float effectiveRange = GetEffectiveDetectionRange();
            float sqrRange = effectiveRange * effectiveRange;
            
            if (sqrDist <= sqrRange)
            {
                TryFireTriggerByName("SeePlayer");
            }
        }
    }

    // Simplify OnTriggerStay
    protected virtual void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        EnsurePlayerTargetReference(other.transform);
        
        if (detectionCollider != null && detectionCollider.enabled)
        {
            // Only parse enum once, cache it
            if (!enemyAI.State.ToString().Contains("Chase")) // Or better: cache the Chase state
            {
                TryFireTriggerByName("SeePlayer");
            }
        }
    }

    // Draw gizmos for detection and attack colliders
    protected virtual void OnDrawGizmos()
    {
        // Detection range gizmo (sphere)
        if (showDetectionGizmo)
        {
            float effectiveRange = GetEffectiveDetectionRange();
            Gizmos.color = new Color(0f, 0.7f, 1f, 0.3f); // Cyan, semi-transparent
            Gizmos.DrawWireSphere(transform.position, effectiveRange);
            Gizmos.color = new Color(0f, 0.7f, 1f, 0.1f);
            Gizmos.DrawSphere(transform.position, effectiveRange);
        }

        // Attack range gizmo (box)
        if (showAttackGizmo)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f); // Red, semi-transparent
            Vector3 boxCenter = transform.position + transform.forward * attackBoxDistance;
            boxCenter += Vector3.up * attackBoxHeightOffset;
            Gizmos.matrix = Matrix4x4.TRS(boxCenter, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, attackBoxSize);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.1f);
            Gizmos.DrawCube(Vector3.zero, attackBoxSize);
            Gizmos.matrix = Matrix4x4.identity;
            // Also draw the effective attack range as a sphere for reference
            float attackRange = (Mathf.Max(attackBoxSize.x, attackBoxSize.z) * 0.5f) + attackBoxDistance;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }

    protected virtual void OnValidate()
    {
        var detectionRef = detectionCollider != null ? detectionCollider : detectionColliderOverride;
        if (detectionRef != null)
            detectionRef.radius = GetUnscaledRadiusForRange(detectionRef, detectionRange);

        var attackRef = attackCollider != null ? attackCollider : attackColliderOverride;
        if (attackRef != null)
        {
            attackRef.size = attackBoxSize;
            attackRef.center = new Vector3(0f, attackBoxHeightOffset, attackBoxDistance);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsurePlayerTargetReference();
    }

    protected float GetEffectiveDetectionRange()
    {
        if (detectionCollider != null)
            return GetScaledRadius(detectionCollider);

        if (detectionColliderOverride != null)
            return GetScaledRadius(detectionColliderOverride);

        return detectionRange;
    }

    private static float GetScaledRadius(SphereCollider collider)
    {
        if (collider == null)
            return 0f;

        Vector3 lossy = collider.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
        if (maxScale <= 0f)
            return collider.radius;

        return collider.radius * maxScale;
    }

    private static float GetUnscaledRadiusForRange(SphereCollider collider, float worldRange)
    {
        if (collider == null)
            return worldRange;

        Vector3 lossy = collider.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
        if (maxScale <= 0f)
            return worldRange;

        return worldRange / maxScale;
    }
}

#region States and Triggers
public enum EnemyState
{
    Idle,           // My idea is that when in Idle, the enemy is moving around a section of the map (zone)
                    // so it is idle in the sense that it is not actively searching for the player

    Relocate,       // Relocate is a substate of Patrol where the enemy moves to a new area or waypoint
                    // before transitioning to Idle. This could be used when the enemy loses sight of the player
                    // and needs to move to a different location to search.

    Patrol,         // Patrol is the main state where the enemy is actively moving from zone to zone.
                    // It contains the shared trigger of seeing the player to transition to Chase.

    Reinforcements, // This state would be used when another enemy calls for help

    Chase,          // Chase is when the enemy has detected the player and is actively pursuing them.

    Attack,         // Attack is when the enemy is in range to attack the player.

    Flee,           // Flee is when the enemy is low on health and tries to escape from the player.

    Fled,           // Fled is when the enemy has successfully escaped (out of attack range) and is no longer in immediate danger.

    Recover,        // Recover is when the enemy is regaining health passively while idle.

    Death           // Death is when the enemy has been defeated and is no longer active.
}

public enum EnemyTrigger
{
    SeePlayer,         // Within a certain detection radius or line of sight

    LosePlayer,        // Out of detection radius or line of sight for a certain time

    AidRequested,      // Another enemy has called for help

    FailedAid,         // The aid request was unsuccessful (e.g., arrived and player was gone)
                       // It would start a timer to relocate after a certain time after arriving if no player is seen

    LowHealth,         // I was thinking LowHealth would be like 20-25% of max health

    RecoveredHealth,   // We discussed whether or not enemies should have passive health regen
                       // I think it could be interesting if they do, but it should be slow and only while idle
                       // So I am including functionality for it for now

    InAttackRange,     // These ranges are based on the enemy's attack range, and not the player's
    OutOfAttackRange,  // Therefore, they may need to be adjusted for how they interact with fleeing behavior

    ReachZone,         // Reached the new zone after relocating

    IdleTimerElapsed,  // Timer for how long the enemy has been idle before relocating

    Attacked,           // The enemy has been attacked by the player

    Die                 // The enemy has been defeated
}
#endregion
// Static class to hold shared (default) state machine configurations
// It cannot be stored in BaseEnemy because it is generic
// This also only stores Permits, not OnEntry/OnExit actions
// Derived enemy classes will handle OnEntry/OnExit actions in their own ConfigureStateMachine method
public static class EnemyStateMachineConfig
{
    public static void ConfigureBasic(StateMachine<EnemyState, EnemyTrigger> sm)
    {
        sm.Configure(EnemyState.Idle)
            .SubstateOf(EnemyState.Patrol) // Idle is a substate of Patrol
            .Permit(EnemyTrigger.LowHealth, EnemyState.Recover) // Only in Idle will it transition to Recover
            .Permit(EnemyTrigger.IdleTimerElapsed, EnemyState.Relocate); // After some time in Idle, it relocates
                                                                         // My idea is that it would be more dynamic
                                                                         // if the enemy moved around from zone to zone
                                                                         // instead of just standing still in one spot

        sm.Configure(EnemyState.Relocate)
            .SubstateOf(EnemyState.Patrol) // Relocate is a substate of Patrol
            .Permit(EnemyTrigger.ReachZone, EnemyState.Idle); // Once it reaches the new zone, it goes to Idle

        sm.Configure(EnemyState.Patrol)
            .Permit(EnemyTrigger.SeePlayer, EnemyState.Chase) // Shared trigger to Chase from Patrol
            .Permit(EnemyTrigger.AidRequested, EnemyState.Reinforcements) // Shared trigger to call for reinforcements from Patrol
            .Permit(EnemyTrigger.Attacked, EnemyState.Chase); // If attacked while patrolling, it chases the player

        sm.Configure(EnemyState.Reinforcements)
            .Permit(EnemyTrigger.SeePlayer, EnemyState.Chase) // Shared trigger to Chase from Reinforcements
            .Permit(EnemyTrigger.FailedAid, EnemyState.Relocate); // If the aid request fails, it relocates

        sm.Configure(EnemyState.Chase)
            .Permit(EnemyTrigger.LosePlayer, EnemyState.Relocate) // If it loses the player, it relocates
            .Permit(EnemyTrigger.InAttackRange, EnemyState.Attack); // If it gets in range, it attacks

        sm.Configure(EnemyState.Attack)
            .Permit(EnemyTrigger.OutOfAttackRange, EnemyState.Chase); // If the player moves out of range, it chases again
            //.Permit(EnemyTrigger.LowHealth, EnemyState.Flee);  // If low on health, it flees
            // Commented out until fleeing behavior is functional, or if we even want to use it at all

        sm.Configure(EnemyState.Flee) // This state can be used for unique fleeing behavior like calling for reinforcements or defensive manuevers
            .Permit(EnemyTrigger.OutOfAttackRange, EnemyState.Fled); // Once out of range, it goes to Fled

        sm.Configure(EnemyState.Fled)
            .Permit(EnemyTrigger.InAttackRange, EnemyState.Flee) // If the player comes back into range, it goes back to Flee
            .Permit(EnemyTrigger.LosePlayer, EnemyState.Relocate); // If it loses the player while fleeing, it relocates

        sm.Configure(EnemyState.Recover)  // Essentially the same as Idle but with health regen, maybe no movement at all
            .SubstateOf(EnemyState.Patrol) // Recover is a substate of Patrol
            .Permit(EnemyTrigger.RecoveredHealth, EnemyState.Idle); // Once it recovers health, it goes back to Idle

        // Permit Die from any state except Death itself
        foreach (EnemyState state in System.Enum.GetValues(typeof(EnemyState)))
        {
            if (state != EnemyState.Death)
            {
                sm.Configure(state)
                    .Permit(EnemyTrigger.Die, EnemyState.Death);
            }
            else
            {
                sm.Configure(state)
                    .Ignore(EnemyTrigger.Die);
            }
        }

        // Configure Death state (no outgoing transitions)
        sm.Configure(EnemyState.Death);
    }
}
