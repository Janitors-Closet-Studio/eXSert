// CleanserAggressionSystem.cs
// Purpose: Manages the Cleanser's aggression level based on player actions.
// Works with: CleanserBrain, CleanserComboSystem, CombatManager
// Aggression affects combat behavior, movement style, combo availability, and counter chances.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#pragma warning disable CS0414
using Utilities.Combat;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// Aggression levels for the Cleanser boss.
    /// Each level represents a 20-point range on the 0-100 scale.
    /// </summary>
    public enum AggressionLevel
    {
        Level1 = 1,  // 0-20: Passive, observant, reading player
        Level2 = 2,  // 21-40: Cautious engagement
        Level3 = 3,  // 41-60: Balanced aggression
        Level4 = 4,  // 61-80: Aggressive pursuit
        Level5 = 5   // 81-100: Relentless assault
    }
#pragma warning restore CS0414

    /// <summary>
    /// Configuration for counter chance at different aggression levels and stack states.
    /// </summary>
    [Serializable]
    public class CounterChanceConfig
    {
        [Tooltip("Aggression level this config applies to.")]
        public AggressionLevel Level;

        [Tooltip("Chance to counter when player has 0 stacks (0-1).")]
        [Range(0f, 1f)] public float ChanceAtZeroStacks = 0.02f;

        [Tooltip("Chance to counter when player has medium stacks (0-1).")]
        [Range(0f, 1f)] public float ChanceAtMediumStacks = 0.01f;

        [Tooltip("Chance to counter when player has max stacks (0-1).")]
        [Range(0f, 1f)] public float ChanceAtMaxStacks = 0f;
    }

    /// <summary>
    /// Configuration for aggression value modifiers based on player actions.
    /// </summary>
    [Serializable]
    public class AggressionModifierConfig
    {
        [Header("Positive Modifiers (Increase Aggression)")]
        [Tooltip("Aggression added when player attacks near boss (no hit required).")]
        public float PlayerAttackProximity = 1f;

        [Tooltip("Aggression added when player's attack hits the boss.")]
        public float PlayerHitsBoss = 3f;

        [Tooltip("Aggression added when player exits the aggression range collider.")]
        public float PlayerRetreats = 2f;

        [Tooltip("Aggression added per second while player is guarding.")]
        public float PlayerGuardingPerSecond = 0.5f;

        [Tooltip("Aggression added when player successfully blocks an attack.")]
        public float PlayerBlocks = 1f;

        [Tooltip("Aggression added when player parries.")]
        public float PlayerParries = 2f;

        [Tooltip("Aggression added when player counters.")]
        public float PlayerCounters = 3f;

        [Tooltip("Aggression added when player counters with max stacks.")]
        public float PlayerMaxStacksCounter = 5f;

        [Tooltip("Aggression added when ultimate is canceled.")]
        public float UltimateCanceled = 10f;

        [Tooltip("Aggression added when ultimate completes (player survived).")]
        public float UltimateCompleted = 5f;

        [Header("Negative Modifiers (Decrease Aggression)")]
        [Tooltip("Aggression decay per second when player is idle.")]
        public float IdleDecayPerSecond = 1f;

        [Tooltip("Aggression decay when Cleanser lands a counter.")]
        public float CleanserCounterDecay = 5f;

        [Header("Idle Escalation")]
        [Tooltip("Time in seconds before idle escalation kicks in.")]
        public float IdleEscalationThreshold = 5f;

        [Tooltip("Base aggression added per second during idle escalation (scales logarithmically).")]
        public float IdleEscalationBase = 2f;

        [Tooltip("Multiplier for logarithmic scaling of idle escalation.")]
        public float IdleEscalationLogMultiplier = 3f;
    }

    /// <summary>
    /// Configuration for movement behavior at different aggression levels.
    /// </summary>
    [Serializable]
    public class AggressionMovementConfig
    {
        [Tooltip("Aggression level this config applies to.")]
        public AggressionLevel Level;

        [Tooltip("Movement speed multiplier at this level.")]
        [Range(0.5f, 2f)] public float SpeedMultiplier = 1f;

        [Tooltip("Preferred distance to maintain from player.")]
        public float PreferredDistance = 5f;

        [Tooltip("Time between repositioning attempts (lower = more active).")]
        public float RepositionInterval = 2f;

        [Tooltip("If true, actively closes distance to player.")]
        public bool AggressivelyClosesDistance = false;

        [Tooltip("Can use gap-closing dash at this level.")]
        public bool CanUseDash = false;

        [Tooltip("Chance to strafe instead of direct approach (0-1).")]
        [Range(0f, 1f)] public float StrafeChance = 0.5f;
    }

    /// <summary>
    /// Configuration for aggression level multipliers.
    /// Each level acts as a multiplier to aggression value changes.
    /// </summary>
    [Serializable]
    public class AggressionLevelMultiplierConfig
    {
        [Tooltip("Multiplier applied at Level 1.")]
        public float Level1Multiplier = 1f;

        [Tooltip("Multiplier applied at Level 2.")]
        public float Level2Multiplier = 2f;

        [Tooltip("Multiplier applied at Level 3.")]
        public float Level3Multiplier = 3f;

        [Tooltip("Multiplier applied at Level 4.")]
        public float Level4Multiplier = 4f;

        [Tooltip("Multiplier applied at Level 5.")]
        public float Level5Multiplier = 5f;

        public float GetMultiplier(AggressionLevel level)
        {
            switch (level)
            {
                case AggressionLevel.Level1: return Level1Multiplier;
                case AggressionLevel.Level2: return Level2Multiplier;
                case AggressionLevel.Level3: return Level3Multiplier;
                case AggressionLevel.Level4: return Level4Multiplier;
                case AggressionLevel.Level5: return Level5Multiplier;
                default: return 1f;
            }
        }
    }

    /// <summary>
    /// Manages the Cleanser's aggression system.
    /// Tracks aggression value (0-100), determines aggression level, and handles counter mechanics.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?tab=t.0#bookmark=id.mhq90ef11c75")]
    public class CleanserAggressionSystem : MonoBehaviour
    {
        [Header("Aggression Value")]
        [Tooltip("Current aggression value (0-100).")]
        [SerializeField] private float aggressionValue = 0f;

        [Tooltip("Minimum aggression value.")]
        [SerializeField] private float minAggressionValue = 0f;

        [Tooltip("Maximum aggression value.")]
        [SerializeField] private float maxAggressionValue = 100f;

        [Header("Forced Aggression")]
        [Tooltip("If true, force aggression to Level 5 when health drops below threshold.")]
        [SerializeField] private bool forceMaxAggressionOnLowHealth = true;

        [Tooltip("Health percentage threshold to force max aggression (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] private float forceMaxAggressionHealthThreshold = 0.25f;

        [Tooltip("If true, ForceMaxAggression() locks aggression at Level 5 permanently (no decay). If false, aggression is set to max but can decay normally.")]
        [SerializeField] private bool permanentForceMaxAggression = false;

        [Header("Aggression Range")]
        [Tooltip("Radius of the aggression range sphere. Player inside this range won't trigger retreat aggression.")]
        [SerializeField] private float aggressionRangeRadius = 10f;

        [Tooltip("Layer mask for player detection.")]
        [SerializeField] private LayerMask playerLayerMask;

        [Header("Guard Detection")]
        [Tooltip("If true, automatically detect when player is guarding and add aggression. Checked internally in Update().")]
        [SerializeField] private bool detectPlayerGuarding = true;

        [Tooltip("How often to check guard state and apply aggression (seconds). Lower = more responsive but slightly more overhead.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float guardCheckInterval = 0.1f;

        [Header("Value Modifiers")]
        [Tooltip("Configuration for aggression value changes based on player actions.")]
        [SerializeField] private AggressionModifierConfig modifiers = new AggressionModifierConfig();

        [Header("Level Multipliers")]
        [Tooltip("Multipliers applied to aggression changes based on current level.")]
        [SerializeField] private AggressionLevelMultiplierConfig levelMultipliers = new AggressionLevelMultiplierConfig();

        [Header("Counter System")]
        [Tooltip("Counter chance configurations for each aggression level.")]
        [SerializeField]
        private CounterChanceConfig[] counterChances = new CounterChanceConfig[]
        {
            new CounterChanceConfig { Level = AggressionLevel.Level1, ChanceAtZeroStacks = 0.02f, ChanceAtMediumStacks = 0.02f, ChanceAtMaxStacks = 0f },
            new CounterChanceConfig { Level = AggressionLevel.Level2, ChanceAtZeroStacks = 0.15f, ChanceAtMediumStacks = 0.05f, ChanceAtMaxStacks = 0f },
            new CounterChanceConfig { Level = AggressionLevel.Level3, ChanceAtZeroStacks = 0.35f, ChanceAtMediumStacks = 0.10f, ChanceAtMaxStacks = 0f },
            new CounterChanceConfig { Level = AggressionLevel.Level4, ChanceAtZeroStacks = 0.60f, ChanceAtMediumStacks = 0.20f, ChanceAtMaxStacks = 0.02f },
            new CounterChanceConfig { Level = AggressionLevel.Level5, ChanceAtZeroStacks = 1.00f, ChanceAtMediumStacks = 0.35f, ChanceAtMaxStacks = 0.10f }
        };

        [Header("Movement Behavior")]
        [Tooltip("Movement configurations for each aggression level.")]
        [SerializeField]
        private AggressionMovementConfig[] movementConfigs = new AggressionMovementConfig[]
        {
            new AggressionMovementConfig { Level = AggressionLevel.Level1, SpeedMultiplier = 0.7f, PreferredDistance = 8f, RepositionInterval = 3f, AggressivelyClosesDistance = false, CanUseDash = false, StrafeChance = 0.7f },
            new AggressionMovementConfig { Level = AggressionLevel.Level2, SpeedMultiplier = 0.85f, PreferredDistance = 6f, RepositionInterval = 2.5f, AggressivelyClosesDistance = false, CanUseDash = false, StrafeChance = 0.5f },
            new AggressionMovementConfig { Level = AggressionLevel.Level3, SpeedMultiplier = 1.0f, PreferredDistance = 5f, RepositionInterval = 2f, AggressivelyClosesDistance = true, CanUseDash = false, StrafeChance = 0.3f },
            new AggressionMovementConfig { Level = AggressionLevel.Level4, SpeedMultiplier = 1.15f, PreferredDistance = 4f, RepositionInterval = 1.5f, AggressivelyClosesDistance = true, CanUseDash = true, StrafeChance = 0.2f },
            new AggressionMovementConfig { Level = AggressionLevel.Level5, SpeedMultiplier = 1.3f, PreferredDistance = 3f, RepositionInterval = 1f, AggressivelyClosesDistance = true, CanUseDash = true, StrafeChance = 0.1f }
        };

        [Header("Counter Attack Settings")]
        [Tooltip("Animation trigger for the Cleanser's counter attack.")]
        [SerializeField] private string counterAnimationTrigger = "Counter";

        [Tooltip("Damage dealt by the Cleanser's counter attack.")]
        [SerializeField] private float counterDamage = 25f;

        [Tooltip("Window in seconds for player to parry the Cleanser's counter.")]
        [SerializeField] private float counterParryWindow = 0.4f;

        [Header("Debug")]
        [Tooltip("Show aggression range gizmo in editor.")]
        [SerializeField] private bool showAggressionRangeGizmo = true;
        [Tooltip("If false, suppresses CleanserAggressionSystem debug logs.")]
        [SerializeField] private bool enableDebugLogs = true;

        // Events
        public event Action<AggressionLevel> OnAggressionLevelChanged;
        public event Action OnCleanserCounterInitiated;
        public event Action<bool> OnCounterResolved; // true = Cleanser wins, false = player parried

        // Runtime state
        private AggressionLevel currentLevel = AggressionLevel.Level1;
        private Transform player;
        private PlayerMovement playerMovement;
        private CleanserBrain brain;
        private bool playerInAggressionRange;
        private float lastPlayerActionTime;
        private float idleTime;
        private bool isCountering;
        private bool forcedMaxAggression;
        private bool isAggressionLocked;
        private bool isAggressionProcessingPaused;
        private SphereCollider aggressionRangeCollider;
        private float guardCheckTimer;

        // Public properties
        public float AggressionValue => aggressionValue;
        public AggressionLevel CurrentLevel => currentLevel;
        public bool IsCountering => isCountering;
        public AggressionModifierConfig Modifiers => modifiers;
        public AggressionLevelMultiplierConfig LevelMultipliers => levelMultipliers;
        public SphereCollider AggressionRangeCollider => aggressionRangeCollider;
        public bool IsAggressionProcessingPaused => isAggressionProcessingPaused;

        private void Awake()
        {
            brain = GetComponent<CleanserBrain>();
            SetupAggressionRangeCollider();
        }

        private void Start()
        {
            CachePlayerReference();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            if (isAggressionProcessingPaused) return;

            UpdateGuardDetection();
            UpdateIdleTracking();
            UpdateAggressionDecay();
            CheckForcedMaxAggression();
            UpdateAggressionLevel();
        }

        #region Setup

        private void SetupAggressionRangeCollider()
        {
            // Create a trigger collider for aggression range detection
            aggressionRangeCollider = gameObject.AddComponent<SphereCollider>();
            aggressionRangeCollider.isTrigger = true;
            aggressionRangeCollider.radius = aggressionRangeRadius;
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
                playerMovement = player.GetComponent<PlayerMovement>();
                if (playerMovement == null)
                    playerMovement = player.GetComponentInParent<PlayerMovement>();
                if (playerMovement == null)
                    playerMovement = player.GetComponentInChildren<PlayerMovement>();
            }
        }

        private void SubscribeToEvents()
        {
            // Subscribe to combat events
            CombatManager.OnSuccessfulParry += HandlePlayerParry;
        }

        private void UnsubscribeFromEvents()
        {
            CombatManager.OnSuccessfulParry -= HandlePlayerParry;
        }

        #endregion

        #region Guard Detection

        private void UpdateGuardDetection()
        {
            if (!detectPlayerGuarding) return;

            guardCheckTimer -= Time.deltaTime;
            if (guardCheckTimer > 0f) return;

            guardCheckTimer = guardCheckInterval;

            // Check if player is guarding via CombatManager
            if (CombatManager.isGuarding)
            {
                // Apply aggression scaled by the check interval (since we're not checking every frame)
                float aggressionToAdd = modifiers.PlayerGuardingPerSecond * guardCheckInterval;
                AddAggression(aggressionToAdd);

#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserAggressionSystem), "[Cleanser Aggression] Player guarding detected.");
#endif
            }
        }

        #endregion

        #region Trigger Callbacks

        private void OnTriggerEnter(Collider other)
        {
            if (isAggressionProcessingPaused) return;

            if (((1 << other.gameObject.layer) & playerLayerMask) != 0)
            {
                playerInAggressionRange = true;
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserAggressionSystem), "[Cleanser Aggression] Player entered aggression range.");
#endif
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (isAggressionProcessingPaused) return;

            if (((1 << other.gameObject.layer) & playerLayerMask) != 0)
            {
                playerInAggressionRange = false;
                // Player retreated - add aggression
                AddAggression(modifiers.PlayerRetreats);
#if UNITY_EDITOR
                if (enableDebugLogs)
                    EnemyBehaviorDebugLogBools.Log(nameof(CleanserAggressionSystem), "[Cleanser Aggression] Player exited aggression range. Adding retreat aggression.");
#endif
            }
        }

        #endregion

        #region Aggression Value Management

        /// <summary>
        /// Adds aggression value, applying the current level multiplier.
        /// </summary>
        public void AddAggression(float amount)
        {
            if (isAggressionLocked || isAggressionProcessingPaused) return;

            float multiplier = levelMultipliers.GetMultiplier(currentLevel);
            float finalAmount = amount * multiplier;
            aggressionValue = Mathf.Clamp(aggressionValue + finalAmount, minAggressionValue, maxAggressionValue);

            lastPlayerActionTime = Time.time;
            idleTime = 0f;

#if UNITY_EDITOR
            if (enableDebugLogs)
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserAggressionSystem), string.Format("[Cleanser Aggression] Added {0:F2} (base: {1}, mult: {2}). Total: {3:F2}", finalAmount, amount, multiplier, aggressionValue));
#endif
        }

        /// <summary>
        /// Removes aggression value (decay).
        /// </summary>
        public void RemoveAggression(float amount)
        {
            if (isAggressionLocked || isAggressionProcessingPaused) return;

            aggressionValue = Mathf.Clamp(aggressionValue - amount, minAggressionValue, maxAggressionValue);

#if UNITY_EDITOR
            if (enableDebugLogs)
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserAggressionSystem), string.Format("[Cleanser Aggression] Removed {0:F2}. Total: {1:F2}", amount, aggressionValue));
#endif
        }

        public void SetAggressionProcessingPaused(bool paused)
        {
            isAggressionProcessingPaused = paused;
        }

        /// <summary>
        /// Forces aggression to maximum (Level 5).
        /// If permanentForceMaxAggression is true, aggression is locked and cannot change.
        /// If false, aggression is set to max but can decay normally.
        /// </summary>
        public void ForceMaxAggression()
        {
            forcedMaxAggression = true;
            aggressionValue = maxAggressionValue;

            // Only lock aggression if permanent mode is enabled
            if (permanentForceMaxAggression)
            {
                isAggressionLocked = true;
            }

            UpdateAggressionLevel();

#if UNITY_EDITOR
            if (enableDebugLogs)
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserAggressionSystem), 
                    permanentForceMaxAggression 
                        ? "[Cleanser Aggression] FORCED to max aggression (PERMANENT)!" 
                        : "[Cleanser Aggression] FORCED to max aggression (can decay).");
#endif
        }

        /// <summary>
        /// Resets the forced max aggression state. Aggression will resume normal behavior.
        /// </summary>
        public void ResetForcedAggression()
        {
            forcedMaxAggression = false;
            isAggressionLocked = false;

#if UNITY_EDITOR
            if (enableDebugLogs)
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserAggressionSystem), "[Cleanser Aggression] Forced aggression reset.");
#endif
        }

        private void UpdateIdleTracking()
        {
            idleTime += Time.deltaTime;

            // Check for idle escalation
            if (idleTime > modifiers.IdleEscalationThreshold)
            {
                // Logarithmic escalation: base * log(time - threshold + 1) * multiplier
                float escalationTime = idleTime - modifiers.IdleEscalationThreshold;
                float escalationAmount = modifiers.IdleEscalationBase * Mathf.Log(escalationTime + 1f) * modifiers.IdleEscalationLogMultiplier;
                AddAggression(escalationAmount * Time.deltaTime);
            }
        }

        private void UpdateAggressionDecay()
        {
            // Constant decay over time (RemoveAggression already checks isAggressionLocked)
            if (idleTime > 0.5f)
            {
                RemoveAggression(modifiers.IdleDecayPerSecond * Time.deltaTime);
            }
        }

        private void CheckForcedMaxAggression()
        {
            if (forcedMaxAggression || brain == null) return;

            if (forceMaxAggressionOnLowHealth)
            {
                float healthPercent = brain.currentHP / brain.maxHP;
                if (healthPercent <= forceMaxAggressionHealthThreshold)
                {
                    ForceMaxAggression();
                }
            }
        }

        private void UpdateAggressionLevel()
        {
            AggressionLevel newLevel = CalculateLevel(aggressionValue);

            if (newLevel != currentLevel)
            {
                AggressionLevel oldLevel = currentLevel;
                currentLevel = newLevel;
                OnAggressionLevelChanged?.Invoke(newLevel);

#if UNITY_EDITOR
                if (enableDebugLogs)
                    EnemyBehaviorDebugLogBools.Log(nameof(CleanserAggressionSystem), string.Format("[Cleanser Aggression] Level changed: {0} -> {1}", oldLevel, newLevel));
#endif
            }
        }

        private AggressionLevel CalculateLevel(float value)
        {
            if (value <= 20f) return AggressionLevel.Level1;
            if (value <= 40f) return AggressionLevel.Level2;
            if (value <= 60f) return AggressionLevel.Level3;
            if (value <= 80f) return AggressionLevel.Level4;
            return AggressionLevel.Level5;
        }

        #endregion

        #region Event Handlers for Aggression Changes

        /// <summary>
        /// Called when player attacks near the boss (proximity-based, no hit detection).
        /// </summary>
        public void OnPlayerAttackProximity()
        {
            AddAggression(modifiers.PlayerAttackProximity);
        }

        /// <summary>
        /// Called when player's attack actually hits the boss.
        /// </summary>
        public void OnPlayerHitsBoss()
        {
            AddAggression(modifiers.PlayerHitsBoss);
        }

        /// <summary>
        /// Called externally when player is guarding. Only use if detectPlayerGuarding is disabled.
        /// When detectPlayerGuarding is enabled, guard detection is handled internally in Update().
        /// </summary>
        /// <param name="deltaTime">Time since last call (use Time.deltaTime for per-frame calls).</param>
        public void OnPlayerGuarding(float deltaTime)
        {
            if (detectPlayerGuarding)
            {
#if UNITY_EDITOR
                if (enableDebugLogs)
                    Debug.LogWarning("[CleanserAggressionSystem] OnPlayerGuarding() called externally while detectPlayerGuarding is enabled. External call ignored.");
#endif
                return;
            }
            AddAggression(modifiers.PlayerGuardingPerSecond * deltaTime);
        }

        /// <summary>
        /// Called when player successfully blocks an attack.
        /// </summary>
        public void OnPlayerBlocks()
        {
            AddAggression(modifiers.PlayerBlocks);
        }

        private void HandlePlayerParry(BaseEnemy<EnemyState, EnemyTrigger> enemy)
        {
            // Only respond to parries against this boss
            AddAggression(modifiers.PlayerParries);
        }

        /// <summary>
        /// Called when player initiates a counter against the Cleanser.
        /// </summary>
        /// <param name="currentStacks">Player's current counter stacks.</param>
        /// <param name="maxStacks">Player's maximum counter stacks.</param>
        /// <param name="hasJustParried">Whether the player just parried (for max counter check).</param>
        public void OnPlayerCounter(int currentStacks, int maxStacks, bool hasJustParried)
        {
            bool isMaxCounter = currentStacks >= maxStacks && hasJustParried;

            if (isMaxCounter)
            {
                AddAggression(modifiers.PlayerMaxStacksCounter);
            }
            else
            {
                AddAggression(modifiers.PlayerCounters);
            }

            // Check if Cleanser should counter
            TryCleanserCounter(currentStacks, maxStacks);
        }

        /// <summary>
        /// Called when ultimate is canceled by player.
        /// </summary>
        public void OnUltimateCanceled()
        {
            AddAggression(modifiers.UltimateCanceled);
        }

        /// <summary>
        /// Called when ultimate completes (player survived).
        /// </summary>
        public void OnUltimateCompleted()
        {
            AddAggression(modifiers.UltimateCompleted);
        }

        /// <summary>
        /// Called when Cleanser lands a counter.
        /// </summary>
        public void OnCleanserCounterLanded()
        {
            RemoveAggression(modifiers.CleanserCounterDecay);
        }

        #endregion

        #region Counter System

        /// <summary>
        /// Attempts to have the Cleanser counter the player's counter attempt.
        /// </summary>
        private void TryCleanserCounter(int playerStacks, int maxStacks)
        {
            float counterChance = GetCounterChance(playerStacks, maxStacks);
            float roll = UnityEngine.Random.value;

#if UNITY_EDITOR
            if (enableDebugLogs)
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserAggressionSystem), string.Format("[Cleanser Counter] Chance: {0:P2}, Roll: {1:F3}", counterChance, roll));
#endif

            if (roll <= counterChance)
            {
                StartCoroutine(ExecuteCleanserCounter());
            }
            else
            {
                // Cleanser doesn't counter, player's counter goes through
                OnCounterResolved?.Invoke(false);
            }
        }

        private float GetCounterChance(int playerStacks, int maxStacks)
        {
            CounterChanceConfig config = GetCounterConfig(currentLevel);
            if (config == null) return 0f;

            float stackRatio = maxStacks > 0 ? (float)playerStacks / maxStacks : 0f;

            // 0 stacks = ZeroStacks chance
            // 0.5 ratio = MediumStacks chance
            // 1.0 ratio = MaxStacks chance
            if (stackRatio <= 0.1f)
            {
                return config.ChanceAtZeroStacks;
            }
            else if (stackRatio >= 0.9f)
            {
                return config.ChanceAtMaxStacks;
            }
            else
            {
                // Interpolate between zero and medium, then medium and max
                if (stackRatio < 0.5f)
                {
                    float t = stackRatio / 0.5f;
                    return Mathf.Lerp(config.ChanceAtZeroStacks, config.ChanceAtMediumStacks, t);
                }
                else
                {
                    float t = (stackRatio - 0.5f) / 0.5f;
                    return Mathf.Lerp(config.ChanceAtMediumStacks, config.ChanceAtMaxStacks, t);
                }
            }
        }

        private CounterChanceConfig GetCounterConfig(AggressionLevel level)
        {
            foreach (var config in counterChances)
            {
                if (config.Level == level)
                    return config;
            }
            return null;
        }

        private IEnumerator ExecuteCleanserCounter()
        {
            isCountering = true;
            OnCleanserCounterInitiated?.Invoke();

#if UNITY_EDITOR
            if (enableDebugLogs)
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserAggressionSystem), "[Cleanser] Executing counter attack!");
#endif

            // Trigger counter animation
            var animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.SetTrigger(counterAnimationTrigger);
            }

            // Wait for counter animation wind-up (adjustable)
            yield return new WaitForSeconds(counterParryWindow);

            // Check if player parried the counter (CombatManager.isParrying)
            if (CombatManager.isParrying)
            {
                // Player parried the counter - they get another chance to counter
                isCountering = false;
                OnCounterResolved?.Invoke(false);

#if UNITY_EDITOR
                if (enableDebugLogs)
                    EnemyBehaviorDebugLogBools.Log(nameof(CleanserAggressionSystem), "[Cleanser] Counter was parried by player!");
#endif
            }
            else
            {
                // Counter lands - deal damage
                if (player != null && player.TryGetComponent<IHealthSystem>(out var health))
                {
                    health.LoseHP(counterDamage);
                }

                OnCleanserCounterLanded();
                isCountering = false;
                OnCounterResolved?.Invoke(true);

#if UNITY_EDITOR
                if (enableDebugLogs)
                    EnemyBehaviorDebugLogBools.Log(nameof(CleanserAggressionSystem), "[Cleanser] Counter landed!");
#endif
            }
        }

        #endregion

        #region Movement Behavior Queries

        /// <summary>
        /// Gets the movement configuration for the current aggression level.
        /// </summary>
        public AggressionMovementConfig GetCurrentMovementConfig()
        {
            foreach (var config in movementConfigs)
            {
                if (config.Level == currentLevel)
                    return config;
            }

            // Return default if not found
            return new AggressionMovementConfig { Level = currentLevel };
        }

        /// <summary>
        /// Returns true if the Cleanser should use the gap-closing dash at current aggression.
        /// </summary>
        public bool CanUseDash()
        {
            return GetCurrentMovementConfig().CanUseDash;
        }

        /// <summary>
        /// Returns true if the Cleanser should aggressively close distance.
        /// </summary>
        public bool ShouldCloseDistance()
        {
            return GetCurrentMovementConfig().AggressivelyClosesDistance;
        }

        /// <summary>
        /// Gets the preferred distance to maintain from the player.
        /// </summary>
        public float GetPreferredDistance()
        {
            return GetCurrentMovementConfig().PreferredDistance;
        }

        /// <summary>
        /// Gets the movement speed multiplier for current aggression.
        /// </summary>
        public float GetSpeedMultiplier()
        {
            return GetCurrentMovementConfig().SpeedMultiplier;
        }

        #endregion

        #region Editor Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!showAggressionRangeGizmo) return;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, aggressionRangeRadius);
        }

        #endregion
    }
}
