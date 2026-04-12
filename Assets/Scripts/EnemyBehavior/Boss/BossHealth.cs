using UnityEngine;
using System;

namespace EnemyBehavior.Boss
{
    /// <summary>
    /// Health component for the boss. Implements IHealthSystem for compatibility with player weapons.
    /// Triggers defeat when health reaches zero.
    /// 
    /// DAMAGE FLOW:
    /// 1. Player weapon hits boss body (tagged "Enemy") → LoseHP() called → Panel-based reduction applied
    /// 2. Panel damage → handled by BossSidePanelCollider (separate health pools, NO reduction)
    /// 3. Vulnerable zone damage → BossRoombaBrain.DamageVulnerableZone() → TakeDamage() (NO reduction, already amplified)
    /// 
    /// PANEL ARMOR SYSTEM:
    /// - When enabled, intact panels reduce damage to the main body
    /// - Each intact panel reduces damage by a configurable percentage
    /// - Breaking panels reduces the armor, incentivizing panel destruction
    /// </summary>
    public class BossHealth : MonoBehaviour, IHealthSystem
    {
        [Header("Health")]
        [SerializeField, Tooltip("Maximum boss health")]
        private float maxHealth = 1000f;
        [SerializeField, Tooltip("Current boss health (set at runtime)")]
        private float currentHealth;
        
        [Header("Panel Armor System")]
        [SerializeField, Tooltip("Enable damage reduction based on intact panels")]
        private bool enablePanelArmor = true;
        [SerializeField, Tooltip("Damage reduction per intact panel (0.1 = 10% reduction per panel)")]
        [Range(0f, 0.2f)]
        private float damageReductionPerPanel = 0.1f;
        [SerializeField, Tooltip("Maximum total damage reduction (0.8 = 80% max reduction even with all panels)")]
        [Range(0f, 0.95f)]
        private float maxDamageReduction = 0.8f;
        [SerializeField, Tooltip("Minimum damage that always gets through regardless of armor (0 = can reduce to zero)")]
        private float minimumDamageThreshold = 1f;
        
        [Header("SFX")]
        [SerializeField, Tooltip("Sound effect to play when the boss takes damage")]
        private AudioClip[] damageSFX;
        [SerializeField] private AudioClip defeatSFX;

        [Header("References")]
        [SerializeField, Tooltip("Reference to boss brain for panel count and defeat callback")]
        private BossRoombaBrain brain;
        
        [Header("UI")]
        [SerializeField, Tooltip("Reference to the boss health bar script on the canvas")]
        private HealthBar healthBar;
        [SerializeField, Tooltip("How quickly the health bar animates to the current value")]
        private float healthBarLerpSpeed = 8f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private bool isDefeated = false;
        private float displayedHealth;

        public event Action BossDefeated;

        // IHealthSystem interface properties
        public float currentHP => currentHealth;
        public float maxHP => maxHealth;

        void Awake()
        {
            currentHealth = maxHealth;
            displayedHealth = maxHealth;
            
            if (brain == null)
            {
                brain = GetComponent<BossRoombaBrain>();
            }
            
            InitializeHealthBar();
        }
        
        private void InitializeHealthBar()
        {
            if (healthBar != null)
            {
                healthBar.SetHealth(currentHealth, maxHealth);
            }
        }
        
        void Update()
        {
            UpdateHealthBar();
        }
        
        private void UpdateHealthBar()
        {
            if (healthBar == null) return;
            
            // Smoothly lerp the displayed health for a nice animation effect
            displayedHealth = Mathf.MoveTowards(displayedHealth, currentHealth, healthBarLerpSpeed * Time.deltaTime * maxHealth);
            healthBar.SetHealth(displayedHealth, maxHealth);
        }

        /// <summary>
        /// IHealthSystem implementation - called by player weapons via HitboxDamageManager.
        /// This applies panel armor reduction before dealing damage.
        /// </summary>
        public void LoseHP(float damage)
        {
            float finalDamage = damage;
            
            // Apply panel armor reduction (only for direct body hits, not vulnerable zones)
            if (enablePanelArmor && brain != null)
            {
                finalDamage = ApplyPanelArmorReduction(damage);
            }
            
            TakeDamage(finalDamage);
            
            // Trigger hit reaction animation
            if (brain != null)
            {
                brain.SendMessage("TriggerRandomHitReact", SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// Calculates damage after panel armor reduction.
        /// More intact panels = more damage reduction.
        /// </summary>
        private float ApplyPanelArmorReduction(float rawDamage)
        {
            int intactPanels = GetIntactPanelCount();
            int totalPanels = brain.SidePanels.Count;
            
            if (intactPanels == 0 || totalPanels == 0)
            {
                // No panels or all destroyed - full damage
                Log($"Panel armor: 0 intact panels, full damage ({rawDamage})");
                return rawDamage;
            }
            
            // Calculate reduction: each intact panel adds damageReductionPerPanel
            float reduction = intactPanels * damageReductionPerPanel;
            reduction = Mathf.Min(reduction, maxDamageReduction); // Cap at max
            
            float damageMultiplier = 1f - reduction;
            float reducedDamage = rawDamage * damageMultiplier;
            
            // Ensure minimum damage threshold
            reducedDamage = Mathf.Max(reducedDamage, minimumDamageThreshold);
            
            Log($"Panel armor: {intactPanels}/{totalPanels} panels intact, {reduction * 100:F0}% reduction, {rawDamage} → {reducedDamage} damage");
            
            return reducedDamage;
        }

        /// <summary>
        /// Returns how many panels are still intact (not destroyed).
        /// </summary>
        private int GetIntactPanelCount()
        {
            if (brain == null || brain.SidePanels == null) return 0;
            
            int count = 0;
            foreach (var panel in brain.SidePanels)
            {
                if (!panel.isDestroyed) count++;
            }
            return count;
        }

        /// <summary>
        /// Returns the current damage reduction percentage (0-1) for UI/feedback.
        /// </summary>
        public float GetCurrentDamageReduction()
        {
            if (!enablePanelArmor || brain == null) return 0f;
            
            int intactPanels = GetIntactPanelCount();
            float reduction = intactPanels * damageReductionPerPanel;
            return Mathf.Min(reduction, maxDamageReduction);
        }

        /// <summary>
        /// IHealthSystem implementation - boss doesn't typically heal, but interface requires it.
        /// </summary>
        public void HealHP(float hp)
        {
            if (isDefeated) return;
            
            currentHealth = Mathf.Min(currentHealth + hp, maxHealth);
            Log($"Boss healed {hp}. Current health: {currentHealth}/{maxHealth}");
        }

        private void PlayDamageSFX()
        {
            if (damageSFX != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.sfxSource.PlayOneShot(damageSFX[UnityEngine.Random.Range(0, damageSFX.Length)]);
            }
        }

        private void PlayDefeatSFX()
        {
            if (defeatSFX != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.sfxSource.PlayOneShot(defeatSFX);
            }
        }

        /// <summary>
        /// Apply damage to the boss directly (bypasses panel armor).
        /// Used by vulnerable zones and internal systems.
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (isDefeated) return;

            currentHealth -= damage;
            currentHealth = Mathf.Max(0, currentHealth);
            
            PlayDamageSFX();
            Log($"Boss took {damage} damage. Current health: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0 && !isDefeated)
            {
                OnDefeated();
            }
        }

        private void OnDefeated()
        {
            isDefeated = true;
            Log("Boss defeated!");
            
            PlayDefeatSFX();
            BossDefeated?.Invoke();
            
            if (brain != null)
            {
                brain.OnBossDefeated();
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogError("[BossHealth] BossRoombaBrain reference is missing!");
            }
        }

        /// <summary>
        /// Get the current health percentage (0-1).
        /// </summary>
        public float GetHealthPercent() => maxHealth > 0 ? currentHealth / maxHealth : 0f;

        /// <summary>
        /// Check if the boss is defeated.
        /// </summary>
        public bool IsDefeated => isDefeated;

        private void Log(string message)
        {
            if (showDebugLogs)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossHealth), $"[BossHealth] {message}");
            }
        }

        #region Debug Context Menu
        
        [ContextMenu("Debug: Take 100 Damage")]
        private void DebugTakeDamage100()
        {
            TakeDamage(100f);
        }
        
        [ContextMenu("Debug: Take 250 Damage")]
        private void DebugTakeDamage250()
        {
            TakeDamage(250f);
        }
        
        [ContextMenu("Debug: Instant Kill")]
        private void DebugInstantKill()
        {
            TakeDamage(currentHealth);
        }
        
        [ContextMenu("Debug: Full Heal")]
        private void DebugFullHeal()
        {
            HealHP(maxHealth);
        }
        
        #endregion
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossHealth))]
    public class BossRoombaEnemyCoreAdapter : BaseEnemyCore
    {
        [SerializeField] private BossHealth bossHealth;

        private bool wasAlive;

        public override bool isAlive => bossHealth != null && !bossHealth.IsDefeated && gameObject.activeInHierarchy;
        public override float currentHP => bossHealth != null ? bossHealth.currentHP : 0f;
        public override float maxHP => bossHealth != null ? bossHealth.maxHP : 0f;

        private void Awake()
        {
            bossHealth ??= GetComponent<BossHealth>();
            wasAlive = isAlive;
        }

        private void OnEnable()
        {
            bossHealth ??= GetComponent<BossHealth>();
            if (bossHealth != null)
                bossHealth.BossDefeated += HandleBossDefeated;

            wasAlive = isAlive;
        }

        private void OnDisable()
        {
            if (bossHealth != null)
                bossHealth.BossDefeated -= HandleBossDefeated;
        }

        private void Update()
        {
            bool aliveNow = isAlive;
            if (wasAlive && !aliveNow)
            {
                InvokeOnDeathStarted();
                InvokeOnDeath();
            }

            wasAlive = aliveNow;
        }

        private void HandleBossDefeated()
        {
            if (!wasAlive)
                return;

            wasAlive = false;
            InvokeOnDeathStarted();
            InvokeOnDeath();
        }

        public override void Spawn()
        {
            wasAlive = isAlive;
            InvokeOnSpawn();
        }

        public override void ResetEnemy()
        {
            wasAlive = isAlive;
            InvokeOnReset();
        }

        public override void HealHP(float hp)
        {
            bossHealth?.HealHP(hp);
            wasAlive = isAlive;
        }

        public override void LoseHP(float damage)
        {
            bossHealth?.LoseHP(damage);
        }
    }
}
