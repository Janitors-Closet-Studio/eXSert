/* 
 * Written by Will T
 * 
 * Manages combat stances and guarding mechanics.
 * Allows switching between single target and area of effect stances.
 * Handles guarding state and parry window timing.
 */

using UnityEngine;
using Singletons;
using System;
using System.Collections;
using System.ComponentModel;
using Progression.Encounters;

namespace Utilities.Combat
{
    public enum Stance
    {
        [Description("Single Target Stance")]
        SingleTarget,
        [Description("Area of Effect Stance")]
        AreaOfEffect,
        [Description("Guarding Stance")]
        Guarding
    }

    public class CombatManager : Singleton<CombatManager>, IHealthSystem
    {
        public float maxHealth;
        public float health;

        //Assigns the variables from the health interfaces to variables in this class
        float IHealthSystem.currentHP => health;
        float IHealthSystem.maxHP => maxHealth;

        // Read-only property to check if in single target mode as a boolean
        public static bool singleTargetMode { get; private set; } = true;

        // Read-only property to get current stance. Prioritizes guarding if the character is guarding over other stances.
        public static Stance CurrentStance => isGuarding ? Stance.Guarding : (singleTargetMode ? Stance.SingleTarget : Stance.AreaOfEffect);

        // Read-only property to get current combat stance without guarding consideration
        public static Stance currentCombatStance => singleTargetMode ? Stance.SingleTarget : Stance.AreaOfEffect;

        public static bool isGuarding { get; private set; } = false;
        public static bool isInCombat { get; private set; }

        [SerializeField, Range(0f, 1f)] private float _parryWindow = 0.3f;
        public static bool isParrying { get; private set; } = false;

        // Unity Action event for stance change for other scripts to subscribe to
        public static event Action OnStanceChanged;

        // Unity Action event for successful parry for other scripts to subscribe to
        public static event Action<BaseEnemy<EnemyState, EnemyTrigger>> OnSuccessfulParry;
        public static event Action<bool> OnInCombatChanged;

        [Header("Player Combat State Tracking")]
        [SerializeField, Range(0f, 15f)] private float combatMemoryAfterTakingDamage = 4f;
        [SerializeField, Range(0f, 15f)] private float combatMemoryAfterDealingDamage = 4f;
        [SerializeField, Range(0f, 40f)] private float enemyProximityRange = 8f;
        [SerializeField, Range(0.02f, 1f)] private float enemyProximityCheckInterval = 0.15f;
        [SerializeField, Tooltip("Layers used to detect nearby enemies for combat-state tracking.")]
        private LayerMask enemyProximityMask = ~0;
        [SerializeField] private bool logCombatStateDebug;

        private readonly Collider[] enemyProximityHits = new Collider[32];
        private Transform cachedPlayerTransform;
        private float nextEnemyProximityCheckTime;
        private bool playerNearEnemy;
        private bool playerInActiveCombatEncounter;
        private float lastPlayerDamagedTime = float.NegativeInfinity;
        private float lastEnemyHitTime = float.NegativeInfinity;
        private float nextCombatDebugLogTime;

        override protected void Awake()
        {
            base.Awake();

            // Initialize to single target mode (stance 0)
            singleTargetMode = true;
            isInCombat = false;
        }

        private void OnEnable()
        {
            PlayerHealthBarManager.OnPlayerDamaged += HandlePlayerDamaged;
            HitboxDamageManager.AttackHitConfirmed += HandleEnemyHitByPlayer;
            CombatEncounter.EncounterCombatStateChanged += HandleEncounterCombatStateChanged;
        }

        private void OnDisable()
        {
            PlayerHealthBarManager.OnPlayerDamaged -= HandlePlayerDamaged;
            HitboxDamageManager.AttackHitConfirmed -= HandleEnemyHitByPlayer;
            CombatEncounter.EncounterCombatStateChanged -= HandleEncounterCombatStateChanged;
            playerNearEnemy = false;
            playerInActiveCombatEncounter = false;
            SetInCombatState(false);
        }

        private void Update()
        {
            if (Time.time >= nextEnemyProximityCheckTime)
            {
                nextEnemyProximityCheckTime = Time.time + Mathf.Max(0.02f, enemyProximityCheckInterval);
                playerNearEnemy = IsPlayerNearAnyLivingEnemy();
            }

            bool tookDamageRecently = Time.time - lastPlayerDamagedTime <= Mathf.Max(0f, combatMemoryAfterTakingDamage);
            bool dealtDamageRecently = Time.time - lastEnemyHitTime <= Mathf.Max(0f, combatMemoryAfterDealingDamage);
            bool shouldBeInCombat = tookDamageRecently || dealtDamageRecently || playerNearEnemy || playerInActiveCombatEncounter;

            if (logCombatStateDebug && Time.time >= nextCombatDebugLogTime)
            {
                nextCombatDebugLogTime = Time.time + 0.5f;
                Debug.Log($"[CombatManager] shouldBeInCombat={shouldBeInCombat} | tookDamageRecently={tookDamageRecently} dealtDamageRecently={dealtDamageRecently} nearEnemy={playerNearEnemy} encounterActive={playerInActiveCombatEncounter}");
            }

            SetInCombatState(shouldBeInCombat);
        }

        private static Coroutine parryWindowRoutine;

        public static void ChangeStance()
        {
            singleTargetMode = !singleTargetMode;
            Debug.Log("Stance changed. Current Stance: " + currentCombatStance.ToString());

            OnStanceChanged?.Invoke();
        }

        public static void EnterGuard()
        {
            isGuarding = true;
            Debug.Log("Player is now guarding. Parry window open");
            // Start parry window
            isParrying = true;
            RestartParryWindow();
        }

        public static void ExitGuard()
        {
            if (!isGuarding)
                return;

            isGuarding = false;
            Debug.Log("Player has stopped guarding.");

            if (parryWindowRoutine != null && Instance != null)
            {
                Instance.StopCoroutine(parryWindowRoutine);
                parryWindowRoutine = null;
            }

            isParrying = false;
        }

        private static void RestartParryWindow()
        {
            if (Instance == null)
                return;

            if (parryWindowRoutine != null)
            {
                Instance.StopCoroutine(parryWindowRoutine);
                parryWindowRoutine = null;
            }

            parryWindowRoutine = Instance.StartCoroutine(ParryWindowCoroutine());
        }

        // Coroutine to handle parry window timing
        private static IEnumerator ParryWindowCoroutine()
        {
            yield return new WaitForSeconds(Instance._parryWindow);

            isParrying = false;
            parryWindowRoutine = null;
            Debug.Log("Parry window closed.");
        }

        // Call this method when a parry is successful
        public static void ParrySuccessful()
        {
            Debug.Log("Parry successful! Counterattack opportunity granted.");

            OnSuccessfulParry?.Invoke(null);

            // Additional logic for successful parry can be added here
        }

        public void HealHP(float hp)
        {
            health += hp;

            //prevents going over max health
            if (health > maxHealth)
            {
                health = maxHealth;
            }
        }

        //Grabs the function from the health interface, updates the health count, and updates the health bar
        public void LoseHP(float damage)
        {
            health -= damage;
        }

        //On death, if this is assigned to the player it will take them to the "Gameover" screen. If it is on any other object however, they will be destroyed.
        public void OnPlayerDeath()
        {
            if (health <= 0)
            {

            }
        }

        private void HandlePlayerDamaged(float _)
        {
            lastPlayerDamagedTime = Time.time;
        }

        private void HandleEnemyHitByPlayer(Utilities.Combat.Attacks.AttackType _, bool __)
        {
            lastEnemyHitTime = Time.time;
        }

        private void HandleEncounterCombatStateChanged(CombatEncounter _, bool isActive)
        {
            playerInActiveCombatEncounter = isActive;
        }

        private bool IsPlayerNearAnyLivingEnemy()
        {
            Transform player = ResolvePlayerTransform();
            if (player == null)
                return false;

            float range = Mathf.Max(0f, enemyProximityRange);
            if (range <= 0f)
                return false;

            int hitCount = Physics.OverlapSphereNonAlloc(
                player.position,
                range,
                enemyProximityHits,
                enemyProximityMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = enemyProximityHits[i];
                if (col == null)
                    continue;

                BaseEnemyCore enemy = col.GetComponentInParent<BaseEnemyCore>();
                if (enemy != null && enemy.isAlive)
                    return true;
            }

            return false;
        }

        private Transform ResolvePlayerTransform()
        {
            if (cachedPlayerTransform != null && cachedPlayerTransform.gameObject.activeInHierarchy)
                return cachedPlayerTransform;

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            cachedPlayerTransform = playerObj != null ? playerObj.transform : null;
            return cachedPlayerTransform;
        }

        private static void SetInCombatState(bool value)
        {
            if (isInCombat == value)
                return;

            isInCombat = value;
            OnInCombatChanged?.Invoke(isInCombat);
        }
    }
}
