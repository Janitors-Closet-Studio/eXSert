using UnityEngine;

namespace EnemyBehavior.Boss
{
    /// <summary>
    /// Attach to wall GameObjects to detect boss collision during Cage Bull charges.
    /// Walls stop the boss but do NOT stun it (only pillars cause stuns).
    /// </summary>
    public sealed class ArenaWallCollider : MonoBehaviour
    {
        [SerializeField, Tooltip("Reference to boss brain (auto-finds if not set)")]
        private BossRoombaBrain bossBrain;

        [SerializeField, Tooltip("If true, logs collision events to console")]
        private bool debugLogCollisions = true;

        private void OnValidate()
        {
            if (bossBrain == null)
            {
                bossBrain = FindFirstObjectByType<BossRoombaBrain>();
            }
        }

        private void Start()
        {
            // Auto-find reference if not set
            if (bossBrain == null)
            {
                bossBrain = FindFirstObjectByType<BossRoombaBrain>();
            }

            // Ensure collider exists
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(ArenaWallCollider), $"[ArenaWallCollider] {gameObject.name} has no Collider component!");
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryHandleCollision(collision.gameObject, "collision");
        }

        private void OnTriggerEnter(Collider other)
        {
            // Note: Walls typically should NOT be triggers if you want them to physically block
            TryHandleCollision(other.gameObject, "trigger");
        }

        private void TryHandleCollision(GameObject collidedObject, string collisionType)
        {
            // Check if it's the boss
            // Check if it's the boss - check for BossRoombaBrain component (more reliable than tag)
            // Note: Boss uses "Enemy" tag, not "Boss" tag
            var brain = collidedObject.GetComponent<BossRoombaBrain>() 
                        ?? collidedObject.GetComponentInParent<BossRoombaBrain>();
            
            bool isBoss = brain != null;

            if (!isBoss)
                return;
            
            // Use the found brain if our reference wasn't set
            if (bossBrain == null)
                bossBrain = brain;

            // Check if boss is charging
            if (bossBrain != null && bossBrain.IsCharging)
            {
                if (debugLogCollisions)
                {
                    EnemyBehaviorDebugLogBools.Log(nameof(ArenaWallCollider), $"[ArenaWallCollider] Boss hit wall '{gameObject.name}' during charge ({collisionType}). No stun applied.");
                }

                // Wall collision just stops movement - the NavMeshAgent and Rigidbody 
                // should handle the physical stop automatically.
                // No explicit action needed here since walls physically block.
            }
            else if (debugLogCollisions)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(ArenaWallCollider), $"[ArenaWallCollider] Boss touched wall '{gameObject.name}' (not charging)");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw wall bounds for debugging
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange semi-transparent
                Gizmos.DrawCube(collider.bounds.center, collider.bounds.size);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
            }
        }
#endif
    }
}