// CleanserBrain.cs
// Purpose: Main AI controller for the Cleanser boss.
// Works with: CleanserComboSystem, CleanserDualWieldSystem, CleanserPlatformController, CleanserProjectile
// Structure mirrors BossRoombaBrain.cs for consistency.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Utilities.Combat;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// Main brain/AI controller for the Cleanser boss.
    /// Manages combat state, attack execution, and ultimate mechanics.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?tab=t.0#bookmark=id.pru60xl52koz")]
    [RequireComponent(typeof(NavMeshAgent))]
    public class CleanserBrain : MonoBehaviour, IQueuedAttacker, IHealthSystem
    {
        [Header("Component Help")]
        [SerializeField, TextArea(3, 6)] private string inspectorHelp =
            "CleanserBrain: Main AI controller for the Cleanser boss.\n" +
            "Manages combo execution, dual-wield mechanics, and ultimate attacks.\n" +
            "Wing attacks can be guarded but not parried.\n" +
            "Halberd attacks can be parried but guard won't help.";

[Header("Beta Testing Mode")]
        [Tooltip("When enabled, Cleanser becomes invulnerable and only follows the player. No attacks, no aggression system, no ultimate. For beta testing purposes.")]
        [SerializeField] private bool betaDummyMode = false;
        [Tooltip("Distance at which the Cleanser stops following the player in dummy mode.")]
        [SerializeField] private float dummyModeStoppingDistance = 3f;

        [Header("Profile")]
        [Tooltip("Behavior profile containing movement settings. If null, uses fallback values below.")]
        [SerializeField] private EnemyBehaviorProfile profile;

        [Header("Health")]
        [SerializeField] private float maxHealth = 2000f;
        [SerializeField] private float currentHealth = 2000f;
        
        [Header("Health Bar UI")]
        [Tooltip("Reference to the boss health bar script on the canvas.")]
        [SerializeField] private HealthBar healthBar;
        [Tooltip("How quickly the health bar animates to the current value.")]
        [SerializeField] private float healthBarLerpSpeed = 8f;
        private float displayedHealth;
        
        [Header("Attack Queue Settings")]
        [Tooltip("If true, this boss is exempt from the EnemyAttackQueueManager and attacks freely.")]
        [SerializeField] private bool exemptFromAttackQueue = true;
        
        [Header("Basic Attack Configurations")]
        public CleanserAttackDescriptor LungeAttack;
        public CleanserAttackDescriptor OverheadCleaveAttack;
        public CleanserAttackDescriptor SlashIntoSlapAttack;
        public CleanserAttackDescriptor RakeIntoSpinSlashAttack;
        public CleanserAttackDescriptor SpinSlashOnlyAttack;
        public CleanserAttackDescriptor LegSweepAttack;

        [Header("Spare Toss Configuration")]
        public SpareTossConfig SpareTossSettings = new SpareTossConfig();
        [Tooltip("Prefab for the thrown weapon projectile (visual only - the actual spare weapon returns magnetically).")]
        public GameObject ProjectilePrefab;
        [Tooltip("Transform where projectiles spawn from (hand).")]
        public Transform ProjectileSpawnPoint;

        [Header("Spin Dash Configuration")]
        public SpinDashConfig SpinDashSettings = new SpinDashConfig();

        [Header("Knockback Attack Configuration")]
        [Tooltip("Configuration for the knockback attack that pushes player away.")]
        public KnockbackAttackConfig KnockbackSettings = new KnockbackAttackConfig();
        
        [Header("Mini Crescent Wave Configuration")]
        [Tooltip("Configuration for the mini crescent wave ranged attack.")]
        public MiniCrescentWaveConfig MiniCrescentSettings = new MiniCrescentWaveConfig();
        
        [Header("Gap-Closing Dash Configuration")]
        [Tooltip("Configuration for the gap-closing dash (no hitbox).")]
        public GapClosingDashConfig GapCloseDashSettings = new GapClosingDashConfig();

        [Header("Strong Attack Configurations")]
        public CleanserAttackDescriptor HighDiveAttack;
        public CleanserAttackDescriptor AnimeDashSlashAttack;
        
        [Header("Whirlwind Configuration")]
        public WhirlwindConfig WhirlwindSettings = new WhirlwindConfig();

        [Header("Ultimate Attack Configuration")]
        public DoubleMaximumSweepConfig UltimateSettings = new DoubleMaximumSweepConfig();
        [Tooltip("Prefab for the crescent wave projectile.")]
        public GameObject CrescentWavePrefab;
        [Tooltip("Positions on walls where Cleanser can jump to for ultimate.")]
        public List<Transform> WallJumpPositions = new List<Transform>();

        [Header("Movement (Fallback if no Profile)")]
        [Tooltip("Base movement speed (used if profile is null).")]
        public float FallbackSpeed = 8f;
        [Tooltip("Angular speed for turning (used if profile is null).")]
        public float FallbackAngularSpeed = 120f;
        [Tooltip("Acceleration (used if profile is null).")]
        public float FallbackAcceleration = 8f;
        [Tooltip("Stopping distance from target (used if profile is null).")]
        public float FallbackStoppingDistance = 2f;

        [Header("Dual-Wield Settings")]
        [Tooltip("Chance (0-1) to pick up a spare weapon before starting a combo.")]
        [Range(0f, 1f)] public float SpareWeaponPickupChance = 0.3f;
        [Tooltip("If true, always picks up spare weapon before these specific attacks regardless of chance.")]
        public bool AlwaysPickupForSpecialAttacks = true;

        [Header("Windup Damage Reduction")]
        [Tooltip("If true, check for animation events to enable/disable damage reduction.")]
        public bool UseDamageReductionEvents = true;
        private bool isDamageReductionActive = false;
        private float currentDamageReduction = 1f;

        [Header("Stun Settings")]
        [Tooltip("Duration of stun when interrupted by aerial finisher during ultimate.")]
        public float AerialFinisherStunDuration = 3f;
        
        [Header("Ultimate Trigger")]
        [Tooltip("Health percentage at which ultimate becomes available.")]
        [Range(0f, 1f)] public float UltimateHealthThreshold = 0.5f;
        [Tooltip("Minimum attacks between ultimate uses.")]
        public int MinAttacksBetweenUltimates = 15;
        [Tooltip("If true, ultimate triggers based on health threshold. If false, uses attack count.")]
        public bool UltimateTriggeredByHealth = true;

        [Header("References")]
        [SerializeField] private CleanserComboSystem comboSystem;
        [SerializeField] private CleanserDualWieldSystem dualWieldSystem;
        [SerializeField] private CleanserPlatformController platformController;
        [SerializeField] private CleanserAggressionSystem aggressionSystem;
        [SerializeField] private VacuumSuctionEffect suctionEffect;
        [SerializeField] private CleanserAnimController animController;
        [SerializeField] private Animator animator;
        [SerializeField] private AudioSource sfxSource;

        [Header("Animator Parameters")]
        [SerializeField] private string paramMoveSpeed = "MoveSpeed";
        [SerializeField] private string paramIsMoving = "IsMoving";
        [SerializeField] private string triggerStunned = "Stunned";
        [SerializeField] private string triggerDeath = "Death";

        [Header("Attack Indicator VFX")]
        [Tooltip("VFX prefab to spawn before an attack to warn the player. Leave empty to disable.")]
        [SerializeField] private GameObject attackIndicatorPrefab;
        [Tooltip("Position offset from the boss's transform where the indicator spawns (local space).")]
        [SerializeField] private Vector3 attackIndicatorOffset = new Vector3(0f, 0.5f, 2f);
        [Tooltip("Seconds before the attack lands that the indicator appears.")]
        [SerializeField] private float attackIndicatorLeadTime = 0.4f;
        [Tooltip("How long the indicator stays visible. Set to 0 to auto-hide when attack starts.")]
        [SerializeField] private float attackIndicatorDuration = 0f;
        [Tooltip("If true, indicator follows the boss's position/rotation.")]
        [SerializeField] private bool attackIndicatorFollowsBoss = true;
        [Tooltip("Scale multiplier for the indicator VFX.")]
        [SerializeField] private float attackIndicatorScale = 1f;
        
        // Runtime state for attack indicator
        private GameObject attackIndicatorInstance;
        private Coroutine attackIndicatorCoroutine;

        // Runtime state
        private NavMeshAgent agent;
        private Transform player;
        private PlayerMovement playerMovement;
        private bool isDefeated;
        private bool isStunned;
        private bool isExecutingUltimate;
        private bool isExecutingAttack;
        private int attacksSinceUltimate;
        private bool hasUsedUltimate;
        private int aerialHitsReceived;
        private Coroutine mainLoopCoroutine;
        private Coroutine currentAttackCoroutine;
        private Dictionary<string, float> attackCooldowns = new Dictionary<string, float>();
        private AttackCategory currentAttackCategory = AttackCategory.Halberd;
        private bool pickedUpWeaponThisCombo;
        private float baseAgentSpeed;

        #region IQueuedAttacker Implementation
        
        public bool IsBoss => true;
        public bool IsAlive => currentHealth > 0f && !isDefeated;
        public GameObject AttackerGameObject => gameObject;
        
        public bool CanAttackFromQueue()
        {
            if (exemptFromAttackQueue) return true;
            if (EnemyAttackQueueManager.Instance == null) return true;
            return EnemyAttackQueueManager.Instance.CanAttack(this);
        }
        
        public void NotifyAttackBegin()
        {
            if (exemptFromAttackQueue) return;
            EnemyAttackQueueManager.Instance?.BeginAttack(this);
        }
        
        public void NotifyAttackEnd()
        {
            if (exemptFromAttackQueue) return;
            EnemyAttackQueueManager.Instance?.FinishAttack(this);
        }
        
        public void RegisterWithAttackQueue()
        {
            if (exemptFromAttackQueue) return;
            EnemyAttackQueueManager.Instance?.Register(this);
        }
        
        public void UnregisterFromAttackQueue()
        {
            if (exemptFromAttackQueue) return;
            EnemyAttackQueueManager.Instance?.Unregister(this);
        }
        
        #endregion

        #region IHealthSystem Implementation
        
        public float currentHP => currentHealth;
        public float maxHP => maxHealth;
        
        public void HealHP(float hp)
        {
            currentHealth = Mathf.Min(currentHealth + hp, maxHealth);
        }
        
        public void LoseHP(float damage)
        {
            if (isDefeated) return;
            
            // Invulnerable in dummy mode
            if (betaDummyMode) return;
            
            float finalDamage = damage;
            if (isDamageReductionActive)
            {
                finalDamage *= currentDamageReduction;
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), $"[Cleanser] Damage reduced: {damage} -> {finalDamage} (reduction: {currentDamageReduction})");
#endif
            }
            
            if (comboSystem != null && comboSystem.IsInRecovery)
            {
                finalDamage *= comboSystem.GetDamageMultiplier();
            }
            
            currentHealth -= finalDamage;

            // Notify aggression system that player hit the boss
            if (aggressionSystem != null)
            {
                aggressionSystem.OnPlayerHitsBoss();
            }
            
            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                OnDefeated();
            }
        }
        
        #endregion

        #region Attack Indicator VFX
        /// <summary>
        /// Shows the attack indicator VFX before an attack.
        /// </summary>
        /// <param name="customOffset">Optional: Override the default offset for this specific attack.</param>
        /// <param name="customDuration">Optional: Override the default duration for this specific attack.</param>
        public void ShowAttackIndicator(Vector3? customOffset = null, float? customDuration = null)
        {
            if (attackIndicatorPrefab == null) return;

            HideAttackIndicator();

            Vector3 offset = customOffset ?? attackIndicatorOffset;
            Vector3 spawnPos = transform.TransformPoint(offset);
            Quaternion spawnRot = transform.rotation;

            attackIndicatorInstance = Instantiate(attackIndicatorPrefab, spawnPos, spawnRot);
            
            if (attackIndicatorScale != 1f)
            {
                attackIndicatorInstance.transform.localScale *= attackIndicatorScale;
            }

            if (attackIndicatorFollowsBoss)
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
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), $"[Cleanser] Attack indicator shown at offset {offset}");
#endif
        }

        /// <summary>
        /// Hides/destroys the attack indicator VFX.
        /// </summary>
        public void HideAttackIndicator()
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
        /// </summary>
        public void AttackIndicatorStart()
        {
            ShowAttackIndicator();
        }

        /// <summary>
        /// Animation Event receiver: Hides the attack indicator.
        /// </summary>
        public void AttackIndicatorEnd()
        {
            HideAttackIndicator();
        }

        private IEnumerator HideIndicatorAfterDelay(float delay)
        {
            yield return WaitForSecondsCache.Get(delay);
            HideAttackIndicator();
        }
        #endregion

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            comboSystem = comboSystem ?? GetComponent<CleanserComboSystem>();
            dualWieldSystem = dualWieldSystem ?? GetComponent<CleanserDualWieldSystem>();
            platformController = platformController ?? GetComponent<CleanserPlatformController>();
            aggressionSystem = aggressionSystem ?? GetComponent<CleanserAggressionSystem>();
            animController = animController ?? GetComponent<CleanserAnimController>() ?? GetComponentInChildren<CleanserAnimController>();
            animator = animator ?? GetComponentInChildren<Animator>();
            
            ApplyMovementSettings();
            InitializeHealthBar();
            CachePlayerReference();
            InitializeAttackDescriptors();
            EnsurePlayerSlideOffSurface();
        }

        private void ApplyMovementSettings()
        {
            if (agent == null) return;
            
            
            if (profile != null)
            {
                // Use profile settings
                float speed = Random.Range(profile.SpeedRange.x, profile.SpeedRange.y);
                agent.speed = speed;
                baseAgentSpeed = speed;
                agent.angularSpeed = profile.AngularSpeed;
                agent.acceleration = profile.Acceleration;
                agent.stoppingDistance = profile.StoppingDistance;
                agent.avoidancePriority = profile.AvoidancePriority;
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), $"[Cleanser] Applied profile settings: speed={speed}, angularSpeed={profile.AngularSpeed}");
#endif
            }
            else
            {
                // Use fallback settings
                agent.speed = FallbackSpeed;
                baseAgentSpeed = FallbackSpeed;
                agent.angularSpeed = FallbackAngularSpeed;
                agent.acceleration = FallbackAcceleration;
                agent.stoppingDistance = FallbackStoppingDistance;
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CleanserBrain), "[Cleanser] No profile assigned, using fallback movement settings.");
#endif
            }
        }

        private void InitializeHealthBar()
        {
            displayedHealth = maxHealth;
            if (healthBar != null)
            {
                healthBar.SetHealth(currentHealth, maxHealth);
            }
        }

        /// <summary>
        /// Ensures a PlayerSlideOffSurface component exists to prevent the player from getting stuck on top of this boss.
        /// </summary>
        private void EnsurePlayerSlideOffSurface()
        {
            if (GetComponent<PlayerSlideOffSurface>() == null)
            {
                gameObject.AddComponent<PlayerSlideOffSurface>();
            }
        }

        private void OnEnable()
        {
            RegisterWithAttackQueue();
            
            // Subscribe to pause events for audio handling
            PauseManager.OnPaused += OnGamePaused;
            PauseManager.OnResumed += OnGameResumed;
            
            if (mainLoopCoroutine != null)
                StopCoroutine(mainLoopCoroutine);
            mainLoopCoroutine = StartCoroutine(MainCombatLoop());
        }

        private void OnDisable()
        {
            UnregisterFromAttackQueue();
            
            // Unsubscribe from pause events
            PauseManager.OnPaused -= OnGamePaused;
            PauseManager.OnResumed -= OnGameResumed;
            
            if (mainLoopCoroutine != null)
            {
                StopCoroutine(mainLoopCoroutine);
                mainLoopCoroutine = null;
            }
            
            if (currentAttackCoroutine != null)
            {
                StopCoroutine(currentAttackCoroutine);
                currentAttackCoroutine = null;
            }
        }

        private void Update()
        {
            UpdateAnimatorParameters();
            UpdateHealthBar();
            
            // Skip aggression updates in dummy mode
            if (!betaDummyMode)
            {
                UpdateAggressionBasedSpeed();
                UpdatePlayerGuardingAggression();
            }
        }
        
        /// <summary>
        /// Called when the game is paused. Pauses audio sources.
        /// </summary>
        private void OnGamePaused()
        {
            if (sfxSource != null && sfxSource.isPlaying)
            {
                sfxSource.Pause();
            }
        }
        
        /// <summary>
        /// Called when the game is resumed. Resumes audio sources.
        /// </summary>
        private void OnGameResumed()
        {
            if (sfxSource != null)
            {
                sfxSource.UnPause();
            }
        }

        private void UpdateAnimatorParameters()
        {
            if (agent == null) return;
            
            float speed = agent.velocity.magnitude;
            float normalizedSpeed = agent.speed > 0f ? speed / agent.speed : 0f;
            
            // Use animation controller if available, otherwise fall back to direct animator
            if (animController != null)
            {
                animController.PlayLocomotion(normalizedSpeed);
            }
            else if (animator != null)
            {
                animator.SetFloat(paramMoveSpeed, speed);
                animator.SetBool(paramIsMoving, speed > 0.1f);
            }
        }

        private void UpdateHealthBar()
        {
            if (healthBar == null) return;
            displayedHealth = Mathf.MoveTowards(displayedHealth, currentHealth, healthBarLerpSpeed * Time.deltaTime * maxHealth);
            healthBar.SetHealth(displayedHealth, maxHealth);
        }

        private void UpdateAggressionBasedSpeed()
        {
            if (agent == null || aggressionSystem == null) return;
            
            float speedMultiplier = aggressionSystem.GetSpeedMultiplier();
            agent.speed = baseAgentSpeed * speedMultiplier;
        }

        private void UpdatePlayerGuardingAggression()
        {
            // Guard detection is now handled internally by CleanserAggressionSystem
            // when detectPlayerGuarding is enabled (default: true).
            // This method is kept for backwards compatibility but does nothing.
            // If you need to manually control guard detection, disable detectPlayerGuarding
            // on the CleanserAggressionSystem and call aggressionSystem.OnPlayerGuarding(Time.deltaTime) here.
        }

        private void CachePlayerReference()
        {
            if (PlayerPresenceManager.IsPlayerPresent)
            {
                player = PlayerPresenceManager.PlayerTransform;
            }
            else
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                    player = playerObj.transform;
            }

            if (player != null)
            {
                playerMovement = player.GetComponent<PlayerMovement>()
                    ?? player.GetComponentInParent<PlayerMovement>()
                    ?? player.GetComponentInChildren<PlayerMovement>();
                    
                if (playerMovement != null)
                    player = playerMovement.transform;
            }
        }

        private void InitializeAttackDescriptors()
        {
            // Only initialize with defaults if not configured in inspector (ID is empty)
            // This allows designer overrides via inspector while providing sensible defaults
            
            if (string.IsNullOrEmpty(LungeAttack?.ID))
            {
                LungeAttack = new CleanserAttackDescriptor
                {
                    ID = "Lunge",
                    Category = AttackCategory.Halberd,
                    BaseDamage = 20f,
                    Cooldown = 2f,
                    RangeMin = 3f,
                    RangeMax = 8f,
                    AnimationTrigger = "Lunge",
                    HasWindupDamageReduction = true,
                    WindupDamageReduction = 0.5f,
                    IncludesMovement = true,
                    MovementDistance = 5f
                };
            }

            if (string.IsNullOrEmpty(OverheadCleaveAttack?.ID))
            {
                OverheadCleaveAttack = new CleanserAttackDescriptor
                {
                    ID = "OverheadCleave",
                    Category = AttackCategory.Halberd,
                    BaseDamage = 25f,
                    Cooldown = 2.5f,
                    RangeMin = 0f,
                    RangeMax = 4f,
                    AnimationTrigger = "OverheadAttack"
                };
            }

            if (string.IsNullOrEmpty(SlashIntoSlapAttack?.ID))
            {
                SlashIntoSlapAttack = new CleanserAttackDescriptor
                {
                    ID = "SlashIntoSlap",
                    Category = AttackCategory.Mixed,
                    BaseDamage = 15f,
                    Cooldown = 3f,
                    RangeMin = 0f,
                    RangeMax = 5f,
                    AnimationTrigger = "SlashSlap",
                    IsMultiPart = true,
                    PartCategories = new[] { AttackCategory.Halberd, AttackCategory.Wing },
                    IncludesMovement = true,
                    MovementDistance = 3f
                };
            }

            if (string.IsNullOrEmpty(RakeIntoSpinSlashAttack?.ID))
            {
                RakeIntoSpinSlashAttack = new CleanserAttackDescriptor
                {
                    ID = "RakeIntoSpinSlash",
                    Category = AttackCategory.Mixed,
                    BaseDamage = 18f,
                    Cooldown = 3f,
                    RangeMin = 0f,
                    RangeMax = 4f,
                    AnimationTrigger = "RakeSpin",
                    IsMultiPart = true,
                    PartCategories = new[] { AttackCategory.Wing, AttackCategory.Halberd }
                };
            }

            if (string.IsNullOrEmpty(SpinSlashOnlyAttack?.ID))
            {
                SpinSlashOnlyAttack = new CleanserAttackDescriptor
                {
                    ID = "SpinSlashOnly",
                    Category = AttackCategory.Halberd,
                    BaseDamage = 20f,
                    Cooldown = 2f,
                    RangeMin = 0f,
                    RangeMax = 4f,
                    AnimationTrigger = "SpinSlash"
                };
            }

            if (string.IsNullOrEmpty(LegSweepAttack?.ID))
            {
                LegSweepAttack = new CleanserAttackDescriptor
                {
                    ID = "LegSweep",
                    Category = AttackCategory.Halberd,
                    BaseDamage = 15f,
                    Cooldown = 4f,
                    RangeMin = 0f,
                    RangeMax = 5f,
                    AnimationTrigger = "LegSweep",
                    CanStunPlayer = true
                };
            }

            if (string.IsNullOrEmpty(HighDiveAttack?.ID))
            {
                HighDiveAttack = new CleanserAttackDescriptor
                {
                    ID = "HighDive",
                    Category = AttackCategory.Halberd,
                    BaseDamage = 40f,
                    Cooldown = 8f,
                    RangeMin = 0f,
                    RangeMax = 10f,
                    AnimationTrigger = "HighDive"
                };
            }

            if (string.IsNullOrEmpty(AnimeDashSlashAttack?.ID))
            {
                AnimeDashSlashAttack = new CleanserAttackDescriptor
                {
                    ID = "AnimeDashSlash",
                    Category = AttackCategory.Halberd,
                    BaseDamage = 12f,
                    Cooldown = 10f,
                    RangeMin = 0f,
                    RangeMax = 15f,
                    AnimationTrigger = "AnimeDash"
                };
            }
        }

        #region Main Combat Loop

        private IEnumerator MainCombatLoop()
        {
            yield return new WaitForSeconds(0.5f);
            
            while (!isDefeated)
            {
                // Beta dummy mode: just follow the player, no attacks
                if (betaDummyMode)
                {
                    yield return ExecuteDummyModeFollow();
                    continue;
                }
                
                while (isStunned)
                {
                    yield return null;
                }

                // Check if aggression system is countering
                if (aggressionSystem != null && aggressionSystem.IsCountering)
                {
                    yield return null;
                    continue;
                }
                
                if (ShouldTriggerUltimate())
                {
                    yield return ExecuteUltimateAttack();
                    continue;
                }
                
                if (comboSystem != null && comboSystem.IsInRecovery)
                {
                    yield return null;
                    continue;
                }
                
                if (comboSystem != null && !comboSystem.IsExecutingCombo)
                {
                    float dist = player != null ? Vector3.Distance(transform.position, player.position) : 0f;
                    var combo = comboSystem.SelectCombo(dist);
                    
                    if (combo != null)
                    {
                        yield return ExecuteCombo(combo);
                    }
                    else
                    {
                        // Use aggression-based movement when no combo available
                        yield return ExecuteAggressionBasedMovement(0.3f);
                    }
                }
                else
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Executes movement behavior based on current aggression level.
        /// </summary>
        private IEnumerator ExecuteAggressionBasedMovement(float duration)
        {
            if (player == null) yield break;

            // Check if we should use gap-closing dash at high aggression
            if (aggressionSystem != null && aggressionSystem.CanUseDash())
            {
                float dist = Vector3.Distance(transform.position, player.position);
                if (dist >= GapCloseDashSettings.MinDistanceToUse)
                {
                    yield return ExecuteGapClosingDash();
                    yield break;
                }
            }

            // Get movement behavior from aggression system
            AggressionMovementConfig movementConfig = aggressionSystem?.GetCurrentMovementConfig();
            
            if (movementConfig != null && movementConfig.AggressivelyClosesDistance)
            {
                // Aggressively close distance
                yield return MoveTowardPlayer(duration);
            }
            else if (movementConfig != null)
            {
                // More passive/observant movement - strafe or maintain distance
                float dist = Vector3.Distance(transform.position, player.position);
                float preferredDist = movementConfig.PreferredDistance;

                if (dist < preferredDist - 1f)
                {
                    // Too close, back off slightly
                    yield return MoveAwayFromPlayer(duration * 0.5f);
                }
                else if (dist > preferredDist + 2f)
                {
                    // Too far, approach slowly
                    yield return MoveTowardPlayer(duration * 0.5f);
                }
                else
                {
                    // In range - strafe or idle based on strafe chance
                    if (Random.value < movementConfig.StrafeChance)
                    {
                        yield return StrafeAroundPlayer(duration);
                    }
                    else
                    {
                        // Face player and wait (observant behavior)
                        yield return FaceTarget(player, duration);
                    }
                }
            }
            else
            {
                // Fallback to basic movement
                yield return MoveTowardPlayer(duration);
            }
        }

        /// <summary>
        /// Simple follow behavior for beta dummy mode. 
        /// Follows the player and stops at a reasonable distance. No attacks.
        /// </summary>
        private IEnumerator ExecuteDummyModeFollow()
        {
            if (player == null) yield break;

            float dist = Vector3.Distance(transform.position, player.position);
            
            if (dist > dummyModeStoppingDistance)
            {
                // Move toward player
                agent.SetDestination(player.position);
                agent.stoppingDistance = dummyModeStoppingDistance;
            }
            else
            {
                // Stop and face player
                agent.ResetPath();
                yield return FaceTarget(player, 0.1f);
            }
            
            yield return null;
        }

        private IEnumerator ExecuteCombo(CleanserCombo combo)
        {
            comboSystem.StartCombo(combo);
            pickedUpWeaponThisCombo = false;
            
            // Chance-based pickup at START of combo
            if (dualWieldSystem != null && !dualWieldSystem.IsHoldingSpareWeapon 
                && dualWieldSystem.AvailableSpareWeaponCount > 0)
            {
                if (Random.value < SpareWeaponPickupChance)
                {
                    dualWieldSystem.PickupSpareWeapon();
                    yield return new WaitUntil(() => !dualWieldSystem.IsPickingUp);
                    pickedUpWeaponThisCombo = true;
                }
            }
            
            while (comboSystem.IsExecutingCombo)
            {
                var step = comboSystem.GetCurrentStep();
                if (step == null)
                    break;
                
                // Check for forced pickup before special attacks
                if (AlwaysPickupForSpecialAttacks && step.IsFinisher && !pickedUpWeaponThisCombo)
                {
                    bool isSpecialAttack = step.StrongAttack == CleanserStrongAttack.AnimeDashSlash 
                        || step.StrongAttack == CleanserStrongAttack.Whirlwind;
                        
                    if (isSpecialAttack && dualWieldSystem != null 
                        && !dualWieldSystem.IsHoldingSpareWeapon 
                        && dualWieldSystem.AvailableSpareWeaponCount > 0)
                    {
                        dualWieldSystem.PickupSpareWeapon();
                        yield return new WaitUntil(() => !dualWieldSystem.IsPickingUp);
                        pickedUpWeaponThisCombo = true;
                    }
                }
                
                if (step.PreDelay > 0f)
                {
                    yield return new WaitForSeconds(step.PreDelay);
                }
                
                if (step.IsFinisher)
                {
                    yield return ExecuteStrongAttack(step.StrongAttack);
                }
                else
                {
                    yield return ExecuteBasicAttack(step.BasicAttack);
                }
                
                attacksSinceUltimate++;
                
                if (!comboSystem.AdvanceStep())
                    break;
                
                yield return null;
            }
            
            // Release spare weapon at END of combo (returns magnetically to rest position)
            if (pickedUpWeaponThisCombo && dualWieldSystem != null && dualWieldSystem.IsHoldingSpareWeapon)
            {
                dualWieldSystem.ReleaseCurrentWeapon();
            }
        }

        private IEnumerator ExecuteBasicAttack(CleanserBasicAttack attackType)
        {
            NotifyAttackBegin();
            isExecutingAttack = true;
            
            switch (attackType)
            {
                case CleanserBasicAttack.Lunge:
                    yield return ExecuteAttackWithAnimationEvents(LungeAttack);
                    break;
                case CleanserBasicAttack.OverheadCleave:
                    yield return ExecuteAttackWithAnimationEvents(OverheadCleaveAttack);
                    break;
                case CleanserBasicAttack.SlashIntoSlap:
                    yield return ExecuteAttackWithAnimationEvents(SlashIntoSlapAttack);
                    break;
                case CleanserBasicAttack.RakeIntoSpinSlash:
                    yield return ExecuteAttackWithAnimationEvents(RakeIntoSpinSlashAttack);
                    break;
                case CleanserBasicAttack.SpareToss:
                    yield return ExecuteSpareToss();
                    break;
                case CleanserBasicAttack.SpinDash:
                    yield return ExecuteSpinDash();
                    break;
                case CleanserBasicAttack.LegSweep:
                    yield return ExecuteAttackWithAnimationEvents(LegSweepAttack);
                    break;
                case CleanserBasicAttack.Knockback:
                    yield return ExecuteKnockbackAttack();
                    break;
                case CleanserBasicAttack.MiniCrescentWave:
                    yield return ExecuteMiniCrescentWave();
                    break;
            }
            
            isExecutingAttack = false;
            NotifyAttackEnd();
        }

        private IEnumerator ExecuteStrongAttack(CleanserStrongAttack attackType)
        {
            NotifyAttackBegin();
            isExecutingAttack = true;
            
            switch (attackType)
            {
                case CleanserStrongAttack.HighDive:
                    yield return ExecuteHighDive();
                    break;
                case CleanserStrongAttack.AnimeDashSlash:
                    yield return ExecuteAnimeDashSlash();
                    break;
                case CleanserStrongAttack.Whirlwind:
                    yield return ExecuteWhirlwind();
                    break;
            }
            
            isExecutingAttack = false;
            NotifyAttackEnd();
        }

        #endregion

        #region Animation-Event-Driven Attack Execution

        /// <summary>
        /// Executes an attack by triggering the animation and waiting for animation events to drive timing.
        /// Uses Start/End hitbox events like boxer enemies and base crawlers.
        /// </summary>
        private IEnumerator ExecuteAttackWithAnimationEvents(CleanserAttackDescriptor attack)
        {
            if (attack == null) yield break;
            
            currentAttack = attack;
            currentAttackCategory = attack.Category;
            
            // Face player before attack
            yield return FaceTarget(player, 0.15f);
            
            // Show attack indicator at start of attack
            ShowAttackIndicator();
            
            // Setup damage reduction if applicable
            if (attack.HasWindupDamageReduction)
            {
                SetDamageReduction(true, attack.WindupDamageReduction);
            }
            
            // Play SFX
            PlaySFX(attack.AttackSFX);
            
            // Spawn VFX
            if (attack.AttackVFX != null)
            {
                Instantiate(attack.AttackVFX, transform.position, transform.rotation);
            }
            
            // Trigger the single animation clip
            TriggerAnimation(attack.AnimationTrigger);
            
            // Wait for animation to complete via animation event calling OnAttackAnimationComplete()
            // For testing without animations, use a fallback timeout
            float timeout = 3f;
            float elapsed = 0f;
            attackAnimationComplete = false;
            
            while (!attackAnimationComplete && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                
                // Continuous hit checking while hitbox is active
                if (isHitboxActive)
                {
                    CheckMeleeHit(currentAttack.BaseDamage, currentAttackCategory);
                }
                
                yield return null;
            }
            
            // Ensure hitbox is disabled
            isHitboxActive = false;
            
            // Hide attack indicator when attack completes
            HideAttackIndicator();
            
            // Disable damage reduction
            SetDamageReduction(false, 1f);
            
            // If we timed out (no animation), do a basic hit check
            if (elapsed >= timeout)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CleanserBrain), $"[Cleanser] Attack '{attack.ID}' timed out waiting for animation event. Using fallback.");
#endif
                CheckMeleeHit(attack.BaseDamage, attack.Category);
            }
            
            currentAttack = null;
        }

        // Runtime state for animation events
        private bool attackAnimationComplete = false;
        private bool isHitboxActive = false;
        private CleanserAttackDescriptor currentAttack;

        /// <summary>
        /// Animation Event: Called when the attack animation is complete.
        /// </summary>
        public void OnAttackAnimationComplete()
        {
            attackAnimationComplete = true;
            isHitboxActive = false;
        }

        /// <summary>
        /// Animation Event: Called to enable the hitbox at the start of the active frames.
        /// </summary>
        public void OnAttackHitboxStart()
        {
            if (!isExecutingAttack) return;
            isHitboxActive = true;
            
            // Hide attack indicator when hitbox becomes active (if duration was 0)
            if (attackIndicatorDuration <= 0f)
            {
                HideAttackIndicator();
            }
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), "[Cleanser] Hitbox enabled.");
#endif
        }

        /// <summary>
        /// Animation Event: Called to disable the hitbox at the end of the active frames.
        /// </summary>
        public void OnAttackHitboxEnd()
        {
            isHitboxActive = false;
            
            // Play impact SFX/VFX when hitbox ends (attack follow-through)
            if (currentAttack != null)
            {
                PlaySFX(currentAttack.ImpactSFX);
                if (currentAttack.ImpactVFX != null)
                {
                    Instantiate(currentAttack.ImpactVFX, transform.position + transform.forward, transform.rotation);
                }
            }
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), "[Cleanser] Hitbox disabled.");
#endif
        }

        /// <summary>
        /// Animation Event: Called for multi-part attacks to switch category mid-attack.
        /// </summary>
        public void OnSwitchToWingCategory()
        {
            currentAttackCategory = AttackCategory.Wing;
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), "[Cleanser] Switched to Wing category.");
#endif
        }

        /// <summary>
        /// Animation Event: Called for multi-part attacks to switch category mid-attack.
        /// </summary>
        public void OnSwitchToHalberdCategory()
        {
            currentAttackCategory = AttackCategory.Halberd;
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), "[Cleanser] Switched to Halberd category.");
#endif
        }

        /// <summary>
        /// Animation Event: Called to trigger movement during an attack.
        /// </summary>
        public void OnAttackMovementStart()
        {
            if (!isExecutingAttack || player == null) return;
            StartCoroutine(DoAttackMovement());
        }

        private IEnumerator DoAttackMovement()
        {
            float moveDist = currentAttack?.MovementDistance ?? 3f;
            if (moveDist <= 0f) yield break;
            
            Vector3 dir = (player.position - transform.position).normalized;
            float moveDuration = 0.3f;
            float moveSpeed = moveDist / moveDuration;
            float elapsed = 0f;
            
            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                agent.Move(dir * moveSpeed * Time.deltaTime);
                yield return null;
            }
        }

        #endregion

        #region Special Attack Implementations

        private IEnumerator ExecuteSpareToss()
        {
            if (ProjectilePrefab == null || ProjectileSpawnPoint == null)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CleanserBrain), "[Cleanser] SpareToss failed: missing prefab or spawn point.");
#endif
                yield break;
            }

            yield return FaceTarget(player, 0.3f);
            
            // Randomize path type before throw
            SpareTossSettings.RandomizePathType();
            
            // Use animation trigger from config
            TriggerAnimation(SpareTossSettings.AnimationTrigger);
            PlaySFX(SpareTossSettings.ThrowSFX);
            
            // Wait for throw animation event or fallback
            yield return new WaitForSeconds(0.4f);
            
            for (int i = 0; i < SpareTossSettings.ProjectileCount; i++)
            {
                var projObj = Instantiate(ProjectilePrefab, ProjectileSpawnPoint.position, ProjectileSpawnPoint.rotation);
                var proj = projObj.GetComponent<CleanserProjectile>();
                
                if (proj != null)
                {
                    // Pass the dual wield system for magnetic return callback
                    proj.Initialize(player, transform, SpareTossSettings, dualWieldSystem);
                }
                
                if (SpareTossSettings.ThrowSequentially && i < SpareTossSettings.ProjectileCount - 1)
                {
                    yield return new WaitForSeconds(0.3f);
                }
            }
            
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator ExecuteSpinDash()
        {
            int dashCount = 1;
            
            if (SpinDashSettings.RandomDashCount)
            {
                dashCount = Random.value > 0.5f ? 3 : 1;
            }
            else if (SpinDashSettings.AlwaysTripleDash)
            {
                dashCount = 3;
            }

            // Use animation trigger from config
            TriggerAnimation(SpinDashSettings.AnimationTrigger);
            PlaySFX(SpinDashSettings.DashSFX);
            
            if (SpinDashSettings.DashVFX != null)
            {
                Instantiate(SpinDashSettings.DashVFX, transform.position, transform.rotation, transform);
            }
            
            yield return new WaitForSeconds(0.3f);
            
            for (int i = 0; i < dashCount; i++)
            {
                if (player == null) break;
                
                Vector3 targetPos = player.position;
                Vector3 dashDir = (targetPos - transform.position).normalized;
                transform.forward = dashDir;
                
                float elapsed = 0f;
                while (elapsed < SpinDashSettings.DashDuration)
                {
                    elapsed += Time.deltaTime;
                    agent.Move(dashDir * SpinDashSettings.DashSpeed * Time.deltaTime);
                    CheckMeleeHit(15f, AttackCategory.Halberd, 2f);
                    yield return null;
                }
                
                if (i < dashCount - 1)
                {
                    yield return new WaitForSeconds(SpinDashSettings.DelayBetweenDashes);
                }
            }
            
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator ExecuteHighDive()
        {
            if (player == null) yield break;
            
            TriggerAnimation("Attack_HighDive");
            PlaySFX(HighDiveAttack.AttackSFX);
            
            Vector3 startPos = transform.position;
            Vector3 peakPos = startPos + Vector3.up * 8f;
            
            agent.enabled = false;
            
            // Jump up
            float elapsed = 0f;
            while (elapsed < 0.6f)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, peakPos, elapsed / 0.6f);
                yield return null;
            }
            
            // Slam down
            Vector3 targetPos = player.position;
            targetPos.y = startPos.y;
            elapsed = 0f;
            
            while (elapsed < 0.4f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.4f;
                Vector3 pos = Vector3.Lerp(peakPos, targetPos, t);
                pos.y = Mathf.Lerp(peakPos.y, startPos.y, t);
                transform.position = pos;
                yield return null;
            }
            
            agent.enabled = true;
            agent.Warp(transform.position);
            
            // Impact VFX/SFX
            if (HighDiveAttack.ImpactVFX != null)
            {
                Instantiate(HighDiveAttack.ImpactVFX, transform.position, Quaternion.identity);
            }
            PlaySFX(HighDiveAttack.ImpactSFX);
            
            CheckAoEHit(HighDiveAttack.BaseDamage, 4f, AttackCategory.Halberd);
            
            yield return new WaitForSeconds(0.8f);
        }

        private IEnumerator ExecuteAnimeDashSlash()
        {
            if (player == null) yield break;
            
            // Note: The actual animation implementation for this is TBD
            // This is a placeholder for the pentagram dash attack
            
            int dashCount = 5;
            float orbitRadius = 6f;
            
            TriggerAnimation("Attack_AnimeDash");
            PlaySFX(AnimeDashSlashAttack.AttackSFX);
            
            yield return new WaitForSeconds(0.3f);
            
            Vector3 centerPos = player.position;
            agent.enabled = false;
            
            List<Vector3> points = new List<Vector3>();
            for (int i = 0; i < dashCount; i++)
            {
                float angle = (i * 144f) * Mathf.Deg2Rad;
                Vector3 point = centerPos + new Vector3(
                    Mathf.Cos(angle) * orbitRadius,
                    0f,
                    Mathf.Sin(angle) * orbitRadius
                );
                point.y = transform.position.y;
                points.Add(point);
            }
            
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 targetPoint = points[i];
                Vector3 startPoint = transform.position;
                
                float elapsed = 0f;
                while (elapsed < 0.15f)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / 0.15f;
                    transform.position = Vector3.Lerp(startPoint, targetPoint, t);
                    
                    if (t > 0.4f && t < 0.6f)
                    {
                        CheckMeleeHit(AnimeDashSlashAttack.BaseDamage, AttackCategory.Halberd, 3f);
                    }
                    
                    yield return null;
                }
                
                yield return new WaitForSeconds(0.1f);
            }
            
            agent.enabled = true;
            agent.Warp(transform.position);
            
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator ExecuteWhirlwind()
        {
            // Use animation trigger from config
            TriggerAnimation(WhirlwindSettings.AnimationTrigger);
            PlaySFX(WhirlwindSettings.SpinSFX);
            
            if (WhirlwindSettings.SpinVFX != null)
            {
                Instantiate(WhirlwindSettings.SpinVFX, transform.position, Quaternion.identity, transform);
            }
            
            yield return new WaitForSeconds(0.4f);
            
            if (suctionEffect != null)
            {
                suctionEffect.BasePullStrength = WhirlwindSettings.SuctionStrength;
                suctionEffect.MaxPullStrength = WhirlwindSettings.MaxSuctionStrength;
                suctionEffect.EffectiveRadius = WhirlwindSettings.SuctionRadius;
                suctionEffect.SetPlayerReferences(player, playerMovement);
                suctionEffect.StartSuction(WhirlwindSettings.SuctionDuration);
            }
            
            float suctionElapsed = 0f;
            float lastDamageTick = 0f;
            
            while (suctionElapsed < WhirlwindSettings.SuctionDuration)
            {
                suctionElapsed += Time.deltaTime;
                
                if (player != null)
                {
                    Vector3 dir = (player.position - transform.position).normalized;
                    agent.Move(dir * 2f * Time.deltaTime);
                }
                
                if (suctionElapsed - lastDamageTick >= WhirlwindSettings.DamageTickInterval)
                {
                    lastDamageTick = suctionElapsed;
                    CheckAoEHit(WhirlwindSettings.DamagePerTick, 3f, AttackCategory.Wing);
                }
                
                yield return null;
            }
            
            // Leap slam
            if (player != null)
            {
                Vector3 leapDir = (player.position - transform.position).normalized;
                Vector3 leapTarget = transform.position + leapDir * WhirlwindSettings.LeapDistance;
                Vector3 startPos = transform.position;
                Vector3 peakPos = (startPos + leapTarget) * 0.5f + Vector3.up * 4f;
                
                agent.enabled = false;
                
                float leapElapsed = 0f;
                while (leapElapsed < WhirlwindSettings.LeapDuration)
                {
                    leapElapsed += Time.deltaTime;
                    float t = leapElapsed / WhirlwindSettings.LeapDuration;
                    Vector3 pos = (1 - t) * (1 - t) * startPos + 2 * (1 - t) * t * peakPos + t * t * leapTarget;
                    transform.position = pos;
                    yield return null;
                }
                
                agent.enabled = true;
                agent.Warp(transform.position);
            }
            
            // Impact
            if (WhirlwindSettings.SlamVFX != null)
            {
                Instantiate(WhirlwindSettings.SlamVFX, transform.position, Quaternion.identity);
            }
            PlaySFX(WhirlwindSettings.SlamSFX);
            
            CheckAoEHit(WhirlwindSettings.SlamDamage, WhirlwindSettings.SlamAoERadius, AttackCategory.Halberd);
            
            yield return new WaitForSeconds(0.8f);
        }

        /// <summary>
        /// Executes the knockback attack that pushes player away using external force.
        /// </summary>
        private IEnumerator ExecuteKnockbackAttack()
        {
            if (player == null || playerMovement == null) yield break;

            yield return FaceTarget(player, 0.15f);

            TriggerAnimation(KnockbackSettings.AnimationTrigger);
            PlaySFX(KnockbackSettings.AttackSFX);

            // Wait for animation wind-up
            yield return new WaitForSeconds(0.3f);

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= KnockbackSettings.KnockbackRadius)
            {
                // Calculate knockback direction (away from Cleanser)
                Vector3 knockbackDir = (player.position - transform.position).normalized;
                knockbackDir.y = 0;
                
                // Apply knockback using external force system
                Vector3 knockbackImpulse = knockbackDir * KnockbackSettings.KnockbackForce;
                knockbackImpulse.y = KnockbackSettings.VerticalForce;
                
                playerMovement.ApplyKnockback(knockbackImpulse);

                // Deal damage
                if (player.TryGetComponent<IHealthSystem>(out var health))
                {
                    health.LoseHP(KnockbackSettings.Damage);
                }

                // Notify aggression system that player blocked (if they did)
                if (CombatManager.isGuarding && aggressionSystem != null)
                {
                    aggressionSystem.OnPlayerBlocks();
                }

                // Spawn impact VFX
                if (KnockbackSettings.ImpactVFX != null)
                {
                    Instantiate(KnockbackSettings.ImpactVFX, player.position, Quaternion.identity);
                }
                PlaySFX(KnockbackSettings.ImpactSFX);

#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), $"[Cleanser] Knockback applied: {knockbackImpulse}");
#endif
            }

            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// Executes the mini crescent wave ranged attack.
        /// </summary>
        private IEnumerator ExecuteMiniCrescentWave()
        {
            if (player == null) yield break;

            yield return FaceTarget(player, 0.2f);

            TriggerAnimation(MiniCrescentSettings.AnimationTrigger);
            PlaySFX(MiniCrescentSettings.SlashSFX);

            // Wait for animation slash point
            yield return new WaitForSeconds(0.25f);

            // Spawn crescent wave projectile
            if (MiniCrescentSettings.WavePrefab != null)
            {
                Vector3 spawnPos = transform.position;
                spawnPos.y += MiniCrescentSettings.WaveHeight;

                var waveObj = Instantiate(MiniCrescentSettings.WavePrefab, spawnPos, transform.rotation);

                // Configure wave movement
                var rb = waveObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = (player.position - spawnPos).normalized;
                    dir.y = 0;
                    rb.linearVelocity = dir * MiniCrescentSettings.WaveSpeed;
                }

                // Set up damage on the wave if it has a damage component
                var waveDamage = waveObj.GetComponent<CleanserProjectile>();
                if (waveDamage != null)
                {
                    // Configure using existing projectile system
                    var config = new SpareTossConfig
                    {
                        UseStraightPath = true,
                        ReturnsOnStraightPath = false,
                        ProjectileSpeedRange = new Vector2(MiniCrescentSettings.WaveSpeed, MiniCrescentSettings.WaveSpeed)
                    };
                    waveDamage.Initialize(player, transform, config);
                }

                // Auto-destroy after max distance travel time
                float travelTime = MiniCrescentSettings.MaxDistance / MiniCrescentSettings.WaveSpeed;
                Destroy(waveObj, travelTime);

#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), "[Cleanser] Mini crescent wave fired!");
#endif
            }
            else
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CleanserBrain), "[Cleanser] Mini crescent wave prefab not assigned!");
#endif
            }

            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>
        /// Executes the gap-closing dash (no hitbox, pure movement).
        /// </summary>
        private IEnumerator ExecuteGapClosingDash()
        {
            if (player == null) yield break;

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist < GapCloseDashSettings.MinDistanceToUse) yield break;

            yield return FaceTarget(player, 0.1f);

            TriggerAnimation(GapCloseDashSettings.AnimationTrigger);
            PlaySFX(GapCloseDashSettings.DashSFX);

            if (GapCloseDashSettings.DashVFX != null)
            {
                Instantiate(GapCloseDashSettings.DashVFX, transform.position, transform.rotation, transform);
            }

            // Calculate target position (stop at TargetStopDistance from player)
            Vector3 dirToPlayer = (player.position - transform.position).normalized;
            float dashDistance = Mathf.Min(dist - GapCloseDashSettings.TargetStopDistance, 
                                           GapCloseDashSettings.DashSpeed * GapCloseDashSettings.DashDuration);
            Vector3 targetPos = transform.position + dirToPlayer * dashDistance;

            // Execute dash movement (no hitbox)
            float elapsed = 0f;
            agent.enabled = false;

            while (elapsed < GapCloseDashSettings.DashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / GapCloseDashSettings.DashDuration;
                
                // Smooth dash curve
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 newPos = Vector3.Lerp(transform.position, targetPos, smoothT * Time.deltaTime * 10f);
                transform.position = Vector3.MoveTowards(transform.position, targetPos, GapCloseDashSettings.DashSpeed * Time.deltaTime);
                
                yield return null;
            }

            agent.enabled = true;
            agent.Warp(transform.position);

#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), $"[Cleanser] Gap-closing dash completed. Distance closed: {dashDistance:F2}");
#endif

            yield return new WaitForSeconds(0.2f);
        }

        #endregion

        #region Ultimate Attack

        private bool ShouldTriggerUltimate()
        {
            if (isExecutingUltimate || isStunned)
                return false;
                
            if (UltimateTriggeredByHealth)
            {
                float healthPercent = currentHealth / maxHealth;
                return healthPercent <= UltimateHealthThreshold && !hasUsedUltimate;
            }
            else
            {
                return attacksSinceUltimate >= MinAttacksBetweenUltimates;
            }
        }

        private IEnumerator ExecuteUltimateAttack()
        {
            isExecutingUltimate = true;
            hasUsedUltimate = true;
            attacksSinceUltimate = 0;
            aerialHitsReceived = 0;
            
            // Force pickup before ultimate if available
            if (dualWieldSystem != null && !dualWieldSystem.IsHoldingSpareWeapon 
                && dualWieldSystem.AvailableSpareWeaponCount > 0)
            {
                dualWieldSystem.PickupSpareWeapon();
                yield return new WaitUntil(() => !dualWieldSystem.IsPickingUp);
            }
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), "[Cleanser] ULTIMATE: Double Maximum Sweep initiated!");
#endif

            PlaySFX(UltimateSettings.InitiateSFX);

            if (WallJumpPositions.Count > 0)
            {
                Transform wallPos = WallJumpPositions[Random.Range(0, WallJumpPositions.Count)];
                yield return JumpToPosition(wallPos.position, 1f);
            }
            
            Vector3 arenaCenter = player != null ? player.position : transform.position + transform.forward * 10f;
            yield return FaceTarget(arenaCenter, 0.3f);
            
            // Sweeps
            if (CrescentWavePrefab != null)
            {
                SpawnCrescentWave(UltimateSettings.LowSweepHeight, arenaCenter);
                PlaySFX(UltimateSettings.SweepSFX);
            }
            yield return new WaitForSeconds(0.8f);
            
            if (CrescentWavePrefab != null)
            {
                SpawnCrescentWave(UltimateSettings.MidSweepHeight, arenaCenter);
                PlaySFX(UltimateSettings.SweepSFX);
            }
            yield return new WaitForSeconds(0.5f);
            
            Vector3 floatPos = arenaCenter + Vector3.up * UltimateSettings.PlatformRiseHeight;
            yield return JumpToPosition(floatPos, 0.8f);
            
            if (platformController != null)
            {
                platformController.RaisePlatforms();
            }
            
            if (UltimateSettings.PlayCutsceneOnFirstUse)
            {
                yield return new WaitForSeconds(UltimateSettings.CutsceneDuration);
                UltimateSettings.PlayCutsceneOnFirstUse = false;
            }
            
            float chargeElapsed = 0f;
            bool canceled = false;
            
            while (chargeElapsed < UltimateSettings.ChargeUpTime)
            {
                chargeElapsed += Time.deltaTime;
                
                if (aerialHitsReceived >= UltimateSettings.RequiredAerialHits)
                {
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), "[Cleanser] Ultimate can be canceled by plunge finisher!");
#endif
                }
                
                yield return null;
            }
            
            if (platformController != null)
            {
                platformController.LowerPlatforms();
            }
            
            if (!canceled)
            {
                yield return ExecuteMassiveStrike();
                
                // Notify aggression system that ultimate completed
                if (aggressionSystem != null)
                {
                    aggressionSystem.OnUltimateCompleted();
                }
            }
            else
            {
                // Notify aggression system that ultimate was canceled
                if (aggressionSystem != null)
                {
                    aggressionSystem.OnUltimateCanceled();
                }
                
                yield return ApplyStun(AerialFinisherStunDuration);
            }
            
            // Release spare weapon after ultimate (returns magnetically to rest position)
            if (dualWieldSystem != null && dualWieldSystem.IsHoldingSpareWeapon)
            {
                dualWieldSystem.ReleaseCurrentWeapon();
            }
            
            isExecutingUltimate = false;
        }

        private void SpawnCrescentWave(float height, Vector3 targetPos)
        {
            Vector3 spawnPos = transform.position;
            spawnPos.y = transform.position.y + height;
            
            var waveObj = Instantiate(CrescentWavePrefab, spawnPos, Quaternion.identity);
            
            var rb = waveObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 dir = (targetPos - spawnPos).normalized;
                dir.y = 0;
                rb.linearVelocity = dir * UltimateSettings.WaveSpeed;
            }
            
            Destroy(waveObj, 5f);
        }

        private IEnumerator ExecuteMassiveStrike()
        {
            PlaySFX(UltimateSettings.MassiveStrikeSFX);
            
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos;
            targetPos.y = 0f;
            
            agent.enabled = false;
            
            float elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.3f;
                transform.position = Vector3.Lerp(startPos, targetPos, t * t);
                yield return null;
            }
            
            agent.enabled = true;
            agent.Warp(transform.position);
            
            if (UltimateSettings.MassiveStrikeVFX != null)
            {
                Instantiate(UltimateSettings.MassiveStrikeVFX, transform.position, Quaternion.identity);
            }
            
            CheckMassiveStrikeHit();
            
            yield return new WaitForSeconds(1f);
        }

        private void CheckMassiveStrikeHit()
        {
            if (player == null) return;
            
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= UltimateSettings.MassiveStrikeRadius)
            {
                float damage = UltimateSettings.MassiveStrikeDamage;
                
                if (CombatManager.isGuarding)
                {
                    damage *= (1f - UltimateSettings.GuardMitigationCap);
                }
                
                if (player.TryGetComponent<IHealthSystem>(out var health))
                {
                    health.LoseHP(damage);
                }
            }
        }

        public void OnAerialHitReceived()
        {
            if (!isExecutingUltimate) return;
            
            aerialHitsReceived++;
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), $"[Cleanser] Aerial hit received during ultimate! ({aerialHitsReceived}/{UltimateSettings.RequiredAerialHits})");
#endif
        }

        #endregion

        #region Public Counter Interface

        /// <summary>
        /// Called by external systems when the player initiates a counter attack against the Cleanser.
        /// This triggers the Cleanser's chance-based counter response.
        /// </summary>
        /// <param name="playerStacks">Current counter stacks the player has.</param>
        /// <param name="maxStacks">Maximum counter stacks possible.</param>
        /// <param name="hasJustParried">Whether the player just parried (for max counter check).</param>
        public void OnPlayerInitiatesCounter(int playerStacks, int maxStacks, bool hasJustParried)
        {
            if (aggressionSystem == null)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CleanserBrain), "[Cleanser] AggressionSystem not found, cannot process counter!");
#endif
                return;
            }

            aggressionSystem.OnPlayerCounter(playerStacks, maxStacks, hasJustParried);
        }

        /// <summary>
        /// Called by external systems when the player attacks near the boss (proximity check, no hit needed).
        /// </summary>
        public void OnPlayerAttackNearby()
        {
            if (aggressionSystem != null)
            {
                aggressionSystem.OnPlayerAttackProximity();
            }
        }

        /// <summary>
        /// Gets the current aggression level of the Cleanser.
        /// </summary>
        public AggressionLevel GetAggressionLevel()
        {
            return aggressionSystem != null ? aggressionSystem.CurrentLevel : AggressionLevel.Level1;
        }

        /// <summary>
        /// Gets the current aggression value (0-100).
        /// </summary>
        public float GetAggressionValue()
        {
            return aggressionSystem != null ? aggressionSystem.AggressionValue : 0f;
        }

        #endregion

        #region Helper Methods

        private IEnumerator MoveTowardPlayer(float duration)
        {
            if (player == null) yield break;
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                agent.SetDestination(player.position);
                yield return null;
            }
        }

        private IEnumerator MoveAwayFromPlayer(float duration)
        {
            if (player == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                
                // Calculate position away from player
                Vector3 awayDir = (transform.position - player.position).normalized;
                Vector3 targetPos = transform.position + awayDir * 3f;
                
                agent.SetDestination(targetPos);
                yield return null;
            }
        }

        private IEnumerator StrafeAroundPlayer(float duration)
        {
            if (player == null) yield break;

            float elapsed = 0f;
            // Randomly choose strafe direction
            float strafeDir = Random.value > 0.5f ? 1f : -1f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                
                // Calculate strafe position (perpendicular to player direction)
                Vector3 toPlayer = (player.position - transform.position).normalized;
                Vector3 strafeVector = Vector3.Cross(Vector3.up, toPlayer) * strafeDir;
                Vector3 targetPos = transform.position + strafeVector * 3f;
                
                // Face player while strafing
                transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));
                
                agent.SetDestination(targetPos);
                yield return null;
            }
        }

        private IEnumerator FaceTarget(Transform target, float duration)
        {
            if (target == null) yield break;
            yield return FaceTarget(target.position, duration);
        }

        private IEnumerator FaceTarget(Vector3 targetPos, float duration)
        {
            Vector3 dir = (targetPos - transform.position).normalized;
            dir.y = 0;
            
            if (dir.sqrMagnitude < 0.001f) yield break;
            
            Quaternion startRot = transform.rotation;
            Quaternion targetRot = Quaternion.LookRotation(dir);
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRot, targetRot, elapsed / duration);
                yield return null;
            }
            
            transform.rotation = targetRot;
        }

        private IEnumerator JumpToPosition(Vector3 targetPos, float duration)
        {
            Vector3 startPos = transform.position;
            Vector3 peakPos = (startPos + targetPos) * 0.5f + Vector3.up * 5f;
            
            agent.enabled = false;
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                Vector3 pos = (1 - t) * (1 - t) * startPos + 2 * (1 - t) * t * peakPos + t * t * targetPos;
                transform.position = pos;
                yield return null;
            }
            
            transform.position = targetPos;
            agent.enabled = true;
            agent.Warp(targetPos);
        }

        private bool CheckMeleeHit(float damage, AttackCategory category, float range = 3f)
        {
            if (player == null) return false;
            
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist > range) return false;
            
            if (category == AttackCategory.Halberd)
            {
                if (CombatManager.isParrying)
                {
                    CombatManager.ParrySuccessful();
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), "[Cleanser] Attack parried!");
#endif
                    return false;
                }
            }
            else if (category == AttackCategory.Wing)
            {
                if (CombatManager.isGuarding)
                {
                    damage *= 0.25f;
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), $"[Cleanser] Wing attack guarded, reduced to {damage}.");
#endif
                }
            }
            
            if (player.TryGetComponent<IHealthSystem>(out var health))
            {
                health.LoseHP(damage);
                return true;
            }
            
            return false;
        }

        private void CheckAoEHit(float damage, float radius, AttackCategory category)
        {
            if (player == null) return;
            
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= radius)
            {
                CheckMeleeHit(damage, category, radius);
            }
        }

        private void TriggerAnimation(string triggerName)
        {
            if (string.IsNullOrEmpty(triggerName)) return;
            
            // Use animation controller if available (preferred)
            if (animController != null)
            {
                animController.PlayAttack(triggerName);
            }
            else if (animator != null)
            {
                // Fallback to direct animator trigger
                animator.SetTrigger(triggerName);
            }
        }

        private void PlaySFX(AudioClip clip)
        {
            if (clip == null) return;
            
            if (sfxSource != null)
            {
                sfxSource.PlayOneShot(clip);
            }
            else if (SoundManager.Instance != null)
            {
                SoundManager.Instance.sfxSource.PlayOneShot(clip);
            }
        }

        private void SetDamageReduction(bool active, float reduction)
        {
            isDamageReductionActive = active;
            currentDamageReduction = reduction;
        }

        private IEnumerator ApplyStun(float duration)
        {
            isStunned = true;
            TriggerAnimation(triggerStunned);
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), $"[Cleanser] Stunned for {duration} seconds!");
#endif
            
            yield return new WaitForSeconds(duration);
            
            isStunned = false;
        }

        private void OnDefeated()
        {
            if (isDefeated) return;
            
            isDefeated = true;
            
            if (mainLoopCoroutine != null)
            {
                StopCoroutine(mainLoopCoroutine);
                mainLoopCoroutine = null;
            }
            
            if (currentAttackCoroutine != null)
            {
                StopCoroutine(currentAttackCoroutine);
                currentAttackCoroutine = null;
            }
            
            if (platformController != null)
            {
                platformController.LowerPlatforms();
            }
            
            if (dualWieldSystem != null)
            {
                dualWieldSystem.DropCurrentWeapon();
            }
            
            TriggerAnimation(triggerDeath);
            UnregisterFromAttackQueue();
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserBrain), "[Cleanser] Defeated!");
#endif
        }

        #endregion

        #region Animation Event Receivers

        public void EnableDamageReduction()
        {
            if (!UseDamageReductionEvents) return;
            SetDamageReduction(true, LungeAttack.WindupDamageReduction);
        }

        public void DisableDamageReduction()
        {
            if (!UseDamageReductionEvents) return;
            SetDamageReduction(false, 1f);
        }

        #endregion
    }
}
