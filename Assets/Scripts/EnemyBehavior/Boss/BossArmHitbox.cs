using UnityEngine;
using Utilities.Combat;

namespace EnemyBehavior.Boss
{
    /// <summary>
    /// Attach to arm hitbox root. Supports multiple collider segments (shoulder/elbow/hand).
    /// Enable/disable via Animation Events during attack sequences.
    /// </summary>
    public sealed class BossArmHitbox : MonoBehaviour
    {
        [SerializeField, Tooltip("Damage dealt by this hitbox")]
        private float damage = 1f;

        [SerializeField, Tooltip("Reference to boss brain for attack info")]
        private BossRoombaBrain bossBrain;

        [SerializeField, Tooltip("Which arm is this")]
        private ArmSide armSide = ArmSide.Left;
        
        [SerializeField, Tooltip("Root transform to search for colliders. If null, uses this GameObject. Use this for complex arm hierarchies where colliders are spread across multiple bones.")]
        private Transform colliderSearchRoot;
        
        [Header("Knockback")]
        [SerializeField, Tooltip("If true, this hitbox applies knockback (for dashes/charges)")]
        private bool applyKnockback = false;
        [SerializeField, Tooltip("Override knockback force (0 = use brain's default)")]
        private float knockbackForceOverride = 0f;

        [Header("Player Stagger")]
        [SerializeField, Tooltip("If enabled, this boss hitbox applies forced player stagger on hit.")]
        private bool applyPlayerStagger = false;
        [SerializeField, Range(0.05f, 2f), Tooltip("Forced stagger duration applied to player by this hitbox.")]
        private float playerStaggerDuration = 0.45f;
        [SerializeField, Tooltip("If enabled, player combo is reset when this stagger is applied.")]
        private bool resetPlayerComboOnStagger = true;

        private Collider[] hitboxColliders;
        private bool isActive;
        private bool hasHitThisActivation;
        
        // Temporary knockback override for dash mode
        private bool dashModeKnockback;
        private float dashModeForceOverride;

        public enum ArmSide { Left, Right, Center, Charge }

        private void Awake()
        {
            // If no search root specified, search from this GameObject
            Transform root = colliderSearchRoot != null ? colliderSearchRoot : transform;
            
            // Get all colliders on the root and its children (multi-segment arms)
            hitboxColliders = root.GetComponentsInChildren<Collider>(true);
            
            if (hitboxColliders.Length == 0)
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossArmHitbox), $"[BossArmHitbox] No colliders found under '{root.name}'! Hitbox will not work. Make sure colliders exist as children of the specified root.");
            }
            
            foreach (var col in hitboxColliders)
            {
                col.isTrigger = true;
            }
            
            if (bossBrain == null)
            {
                bossBrain = GetComponentInParent<BossRoombaBrain>();
            }

            DisableHitbox();
        }

        public void EnableHitbox()
        {
            foreach (var col in hitboxColliders)
            {
                if (col != null) col.enabled = true;
            }
            isActive = true;
            hasHitThisActivation = false;
            dashModeKnockback = false; // Reset dash mode
            dashModeForceOverride = 0f;
            EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] {armSide} arm hitbox ENABLED ({hitboxColliders.Length} segments)");
        }
        
        /// <summary>
        /// Enable hitbox with dash-mode knockback.
        /// Forces knockback to be applied even if applyKnockback is false in Inspector.
        /// </summary>
        public void EnableHitboxWithDashKnockback(float forceOverride = 0f)
        {
            foreach (var col in hitboxColliders)
            {
                if (col != null) col.enabled = true;
            }
            isActive = true;
            hasHitThisActivation = false;
            dashModeKnockback = true;
            dashModeForceOverride = forceOverride;
            EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] {armSide} arm hitbox ENABLED with DASH KNOCKBACK ({hitboxColliders.Length} segments, force override: {forceOverride})");
        }

        public void DisableHitbox()
        {
            foreach (var col in hitboxColliders)
            {
                if (col != null) col.enabled = false;
            }
            isActive = false;
            hasHitThisActivation = false;
            dashModeKnockback = false; // Clear dash mode
            dashModeForceOverride = 0f;
            EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] {armSide} arm hitbox DISABLED");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActive) return;
            if (hasHitThisActivation) return;

            if (other.CompareTag("Player"))
            {
                ApplyDamageToPlayer(other.gameObject);
                hasHitThisActivation = true;
            }
        }

        private void ApplyDamageToPlayer(GameObject player)
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] {armSide} ATTEMPTING TO HIT PLAYER");
            
            var healthSystem = player.GetComponent<IHealthSystem>();
            if (healthSystem != null)
            {
                if (CombatManager.isParrying && bossBrain != null)
                {
                    var currentAttack = bossBrain.GetCurrentAttack();
                    if (currentAttack != null && currentAttack.Parryable)
                    {
                        CombatManager.ParrySuccessful();
                        EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] Player PARRIED {currentAttack.Id}");
                        DisableHitbox();
                        return;
                    }
                }

                float finalDamage = damage;
                if (CombatManager.isGuarding)
                {
                    finalDamage *= 0.5f;
                    EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] Player GUARDED - damage reduced to {finalDamage}");
                }

                healthSystem.LoseHP(finalDamage);
                EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] {armSide} HIT PLAYER for {finalDamage} damage!");

                if (applyPlayerStagger && healthSystem is PlayerHealthBarManager playerHealth)
                    playerHealth.ApplyForcedStagger(playerStaggerDuration, resetPlayerComboOnStagger);
                
                // Apply knockback if enabled (either via Inspector setting OR dash mode)
                if ((applyKnockback || dashModeKnockback) && bossBrain != null)
                {
                    ApplyKnockbackToPlayer(player);
                }

                DisableHitbox();
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossArmHitbox), $"[BossArmHitbox] Player has no IHealthSystem component!");
            }
        }
        
        private void ApplyKnockbackToPlayer(GameObject player)
        {
            // Check if knockback was already applied by the manual collision check (or another hitbox)
            // This prevents double-knockback during dashes
            if (dashModeKnockback && bossBrain != null && bossBrain.HasDashHitBeenApplied)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] Skipping knockback - dash hit already applied by manual check");
                return;
            }
            
            // Calculate radial knockback direction (from boss to player) as base
            Vector3 radialDir = (player.transform.position - bossBrain.transform.position).normalized;
            radialDir.y = 0f;
            radialDir.Normalize();
            
            // Get attack direction from brain (if available) for directional knockback
            Vector3 attackDir = bossBrain.CurrentAttackDirection;
            
            // Blend between radial and attack direction based on weight
            // If no attack direction set, use pure radial
            Vector3 knockbackDir;
            if (attackDir.sqrMagnitude > 0.01f)
            {
                float weight = bossBrain.KnockbackAttackDirectionWeight;
                knockbackDir = Vector3.Lerp(radialDir, attackDir, weight).normalized;
                EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] Knockback blend: radial={radialDir}, attack={attackDir}, weight={weight}, result={knockbackDir}");
            }
            else
            {
                knockbackDir = radialDir;
                EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] Knockback using radial only: {knockbackDir}");
            }
            
            
            // Determine force and upward component based on attack type
            float force;
            float upwardForce;
            
            // Check if this is a charge attack (use higher force and upward)
            if (armSide == ArmSide.Charge && bossBrain.IsCharging)
            {
                force = bossBrain.IsTargetedCharge ? bossBrain.ChargeKnockbackForce : bossBrain.DashKnockbackForce;
                upwardForce = bossBrain.IsTargetedCharge ? bossBrain.ChargeKnockbackUpwardForce : bossBrain.DashKnockbackUpwardForce;
            }
            else if (dashModeKnockback)
            {
                // Dash mode - use dash knockback force (or override if set)
                force = dashModeForceOverride > 0 ? dashModeForceOverride : bossBrain.DashKnockbackForce;
                upwardForce = bossBrain.DashKnockbackUpwardForce;
                EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] Using dash mode knockback - force: {force}");
            }
            else
            {
                force = knockbackForceOverride > 0 ? knockbackForceOverride : bossBrain.DashKnockbackForce;
                upwardForce = bossBrain.DashKnockbackUpwardForce;
            }
            
            // Add upward component
            knockbackDir = (knockbackDir + Vector3.up * (upwardForce / 10f)).normalized;
            
            Vector3 knockbackImpulse = knockbackDir * force;
            
            // PREFER PlayerMovement's knockback system (handles wall collision and CharacterController properly)
            var playerMovement = player.GetComponent<PlayerMovement>();
            if (playerMovement == null)
                playerMovement = player.GetComponentInParent<PlayerMovement>();
            if (playerMovement == null)
                playerMovement = player.GetComponentInChildren<PlayerMovement>();
                
            if (playerMovement != null)
            {
                playerMovement.ApplyKnockback(knockbackImpulse);
                EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] Applied knockback via PlayerMovement.ApplyKnockback: impulse={knockbackImpulse}, magnitude={knockbackImpulse.magnitude:F1}");
                
                // Notify the brain that a dash hit was applied (if in dash mode)
                if (dashModeKnockback && bossBrain != null)
                {
                    bossBrain.NotifyDashHitApplied();
                    EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] Notified brain - dash hit applied");
                }
                return;
            }
            
            // Fallback: try Rigidbody (non-kinematic only)
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.AddForce(knockbackImpulse, ForceMode.Impulse);
                EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] Applied knockback via Rigidbody: dir={knockbackDir}, force={force}");
                
                // Notify the brain that a dash hit was applied (if in dash mode)
                if (dashModeKnockback && bossBrain != null)
                {
                    bossBrain.NotifyDashHitApplied();
                }
                return;
            }
            
            
            // Last resort: try CharacterController directly (single frame push)
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.Move(knockbackDir * force * Time.deltaTime);
                EnemyBehaviorDebugLogBools.Log(nameof(BossArmHitbox), $"[BossArmHitbox] Applied knockback via CharacterController.Move (single frame)");
                return;
            }
            
            EnemyBehaviorDebugLogBools.LogWarning(nameof(BossArmHitbox), $"[BossArmHitbox] Could not apply knockback - no PlayerMovement, Rigidbody, or CharacterController found on {player.name}");
        }

        private void OnDisable()
        {
            DisableHitbox();
        }

        private void OnDrawGizmos()
        {
            if (hitboxColliders == null || hitboxColliders.Length == 0)
                hitboxColliders = GetComponentsInChildren<Collider>(true);

            Gizmos.color = isActive ? Color.red : Color.gray;
            
            foreach (var col in hitboxColliders)
            {
                if (col == null) continue;
                
                if (col is BoxCollider box)
                {
                    Gizmos.matrix = col.transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else if (col is SphereCollider sphere)
                {
                    Gizmos.DrawWireSphere(col.transform.position + sphere.center, sphere.radius);
                }
                else if (col is CapsuleCollider capsule)
                {
                    Gizmos.DrawWireSphere(col.transform.position + capsule.center, capsule.radius);
                }
            }
        }
    }
}
