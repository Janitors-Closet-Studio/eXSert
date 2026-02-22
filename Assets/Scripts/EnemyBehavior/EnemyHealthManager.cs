/*
EnemyHealthManager - Death Event Relay & Health Event Forwarder

This component listens to BaseEnemyCore.OnDeath and forwards the event to:
- onDeathEvent (static delegate) - for global systems like kill counters
- onDeath (UnityEvent) - for Inspector-wired handlers like VFX/SFX

It also provides pass-through access to BaseEnemy's health system and
exposes UnityEvents for Inspector-based health change callbacks.

NOTE: BaseEnemy is the source of truth for health. This component provides
additional event hooks and Inspector-friendly access to that system.
*/

using UnityEngine;
using UnityEngine.Events;

public class EnemyHealthManager : MonoBehaviour
{
    [Header("Events")]
    [SerializeField] private UnityEvent onDeath;
    [SerializeField] private UnityEvent<float> onHealthChanged; // passes current health percentage (0-1)
    [SerializeField] private UnityEvent onTakeDamage;

    [Header("Death Settings (Legacy)")]
    [SerializeField, Tooltip("If true, destroys the GameObject on death. If false, GameObject is disabled for pooling.")]
    private bool destroyOnDeath = false;
    [SerializeField, Tooltip("Delay before destroying the GameObject (only used if destroyOnDeath is true).")]
    private float destroyDelay = 2f;

    public delegate void KillCountProgression();
    public static event KillCountProgression onDeathEvent;

    private bool isDead = false;
    private BaseEnemy<EnemyState, EnemyTrigger> enemyScript;
    private float lastKnownHealth = -1f;

    void Awake()
    {
        enemyScript = GetComponent<BaseEnemy<EnemyState, EnemyTrigger>>();
    }

    private void OnEnable()
    {
        if (enemyScript != null)
        {
            enemyScript.OnDeath += HandleEnemyDeath;
            
            // Initialize health tracking
            lastKnownHealth = enemyScript.currentHP;
        }
        
        // Reset isDead flag when re-enabled (for pooled enemies)
        isDead = false;
    }

    private void OnDisable()
    {
        if (enemyScript != null)
        {
            enemyScript.OnDeath -= HandleEnemyDeath;
        }
    }

    private void Update()
    {
        // Poll for health changes to fire events (since BaseEnemy doesn't expose health change events)
        if (enemyScript != null && !isDead)
        {
            float currentHealth = enemyScript.currentHP;
            if (!Mathf.Approximately(currentHealth, lastKnownHealth))
            {
                // Health changed
                if (currentHealth < lastKnownHealth)
                {
                    // Took damage
                    onTakeDamage?.Invoke();
                }
                
                // Notify health change
                float healthPercent = enemyScript.maxHP > 0 ? currentHealth / enemyScript.maxHP : 0f;
                onHealthChanged?.Invoke(healthPercent);
                
                lastKnownHealth = currentHealth;
            }
        }
    }

    private void HandleEnemyDeath(BaseEnemyCore enemy)
    {
        EnemyBehaviorDebugLogBools.Log(nameof(EnemyHealthManager), $"[EnemyHealthManager] HandleEnemyDeath called for {enemy.name}, isDead={isDead}");
        
        if (isDead) return;
        isDead = true;

        // Forward death event to listeners
        onDeathEvent?.Invoke();
        onDeath?.Invoke();

        // Handle legacy destroy behavior if enabled
        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    // ============================================
    // Public Accessors (pass-through to BaseEnemy)
    // ============================================
    
    /// <summary>
    /// Returns true if the enemy is dead.
    /// </summary>
    public bool IsDead => isDead;
    
    /// <summary>
    /// Returns the current health as a percentage (0-1).
    /// </summary>
    public float HealthPercentage => enemyScript != null && enemyScript.maxHP > 0 
        ? enemyScript.currentHP / enemyScript.maxHP 
        : 0f;

    /// <summary>
    /// Returns the current health value (pass-through to BaseEnemy).
    /// </summary>
    public float CurrentHealth => enemyScript != null ? enemyScript.currentHP : 0f;

    /// <summary>
    /// Returns the maximum health value (pass-through to BaseEnemy).
    /// </summary>
    public float MaxHealth => enemyScript != null ? enemyScript.maxHP : 0f;

    // ============================================
    // Public Methods (pass-through to BaseEnemy)
    // ============================================

    /// <summary>
    /// Sets the maximum health on the underlying BaseEnemy.
    /// Also resets current health to the new max.
    /// </summary>
    public void SetMaxHealth(float newMaxHealth)
    {
        if (enemyScript != null)
        {
            enemyScript.maxHealth = newMaxHealth;
            enemyScript.currentHealth = newMaxHealth;
            lastKnownHealth = newMaxHealth;
            onHealthChanged?.Invoke(1f); // Full health
        }
        else
        {
            EnemyBehaviorDebugLogBools.LogWarning(nameof(EnemyHealthManager), $"[EnemyHealthManager] {gameObject.name}: Cannot set max health - no BaseEnemy found.");
        }
    }

    /// <summary>
    /// Sets the current health on the underlying BaseEnemy.
    /// </summary>
    public void SetCurrentHealth(float newCurrentHealth)
    {
        if (enemyScript != null)
        {
            enemyScript.SetHealth(newCurrentHealth);
            lastKnownHealth = enemyScript.currentHP;
            onHealthChanged?.Invoke(HealthPercentage);
        }
        else
        {
            EnemyBehaviorDebugLogBools.LogWarning(nameof(EnemyHealthManager), $"[EnemyHealthManager] {gameObject.name}: Cannot set current health - no BaseEnemy found.");
        }
    }

    /// <summary>
    /// Applies damage to the underlying BaseEnemy.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (enemyScript != null)
        {
            enemyScript.LoseHP(damage);
        }
        else
        {
            EnemyBehaviorDebugLogBools.LogWarning(nameof(EnemyHealthManager), $"[EnemyHealthManager] {gameObject.name}: Cannot take damage - no BaseEnemy found.");
        }
    }

    /// <summary>
    /// Heals the underlying BaseEnemy.
    /// </summary>
    public void Heal(float amount)
    {
        if (enemyScript != null)
        {
            enemyScript.HealHP(amount);
        }
        else
        {
            EnemyBehaviorDebugLogBools.LogWarning(nameof(EnemyHealthManager), $"[EnemyHealthManager] {gameObject.name}: Cannot heal - no BaseEnemy found.");
        }
    }
}