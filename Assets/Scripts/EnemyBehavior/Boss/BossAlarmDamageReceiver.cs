// BossAlarmDamageReceiver.cs
// Purpose: Receives damage from player attacks and forwards it to BossRoombaController.DamageAlarm()
// Attach this to the alarm GameObject that has a collider.

using UnityEngine;

namespace EnemyBehavior.Boss
{
    /// <summary>
    /// Attach to the alarm GameObject to make it damageable.
    /// Requires a Collider on the same GameObject (trigger or non-trigger).
    /// The alarm should be tagged appropriately or the player's attack system 
    /// should detect this component.
    /// </summary>
    public class BossAlarmDamageReceiver : MonoBehaviour, IHealthSystem
    {
        [Header("References")]
        [Tooltip("Reference to the BossRoombaController. Auto-finds if null.")]
        [SerializeField] private BossRoombaController controller;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        
        // IHealthSystem implementation - required for player weapons to damage this
        public float currentHP => controller != null ? controller.AlarmMaxHealth : 0f; // Placeholder
        public float maxHP => controller != null ? controller.AlarmMaxHealth : 100f;
        
        void Awake()
        {
            if (controller == null)
            {
                // Try to find on parent
                controller = GetComponentInParent<BossRoombaController>();
            }

            EnsureDamageableTag();
            
            if (controller == null)
            {
                EnemyBehaviorDebugLogBools.LogError($"[BossAlarmDamageReceiver] No BossRoombaController found! Alarm damage won't work.");
            }
        }

        private void OnValidate()
        {
            EnsureDamageableTag();
        }

        private void EnsureDamageableTag()
        {
            if (CompareTag("Enemy") || CompareTag("Boss"))
                return;

            gameObject.tag = "Enemy";
            if (showDebugLogs)
                EnemyBehaviorDebugLogBools.Log(nameof(BossAlarmDamageReceiver), $"[BossAlarmDamageReceiver] Auto-set tag to 'Enemy' on {name} so player hitboxes can damage alarm.");
        }
        
        /// <summary>
        /// IHealthSystem implementation - called by player weapons.
        /// </summary>
        public void LoseHP(float damage)
        {
            if (controller == null) return;
            
            if (showDebugLogs)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossAlarmDamageReceiver), $"[BossAlarmDamageReceiver] Received {damage} damage, forwarding to controller");
            }
            
            controller.DamageAlarm(damage);
        }
        
        /// <summary>
        /// IHealthSystem implementation - alarm doesn't heal.
        /// </summary>
        public void HealHP(float hp)
        {
            // Alarm doesn't heal
        }
        
        /// <summary>
        /// Alternative method if your damage system uses a different approach.
        /// </summary>
        public void TakeDamage(float damage)
        {
            LoseHP(damage);
        }
    }
}
