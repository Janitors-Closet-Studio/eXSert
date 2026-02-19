using UnityEngine;

namespace EnemyBehavior.Boss
{
    /// <summary>
    /// Attach to pillar GameObjects to detect boss collision during Cage Bull charges.
    /// Reports collision back to BossRoombaBrain to trigger stun and form change.
    /// Only triggers when the boss is actively doing a TARGETED charge (not static charges or casual brushing).
    /// </summary>
    public sealed class BossPillarCollider : MonoBehaviour
    {
        [SerializeField, Tooltip("Index of this pillar in the arena (matches BossArenaManager.Pillars list)")]
        private int pillarIndex;

        [SerializeField, Tooltip("Reference to boss brain (auto-finds if not set)")]
        private BossRoombaBrain bossBrain;

        [SerializeField, Tooltip("If true, only triggers during targeted charges (not static charges or casual contact)")]
        private bool onlyDuringTargetedCharge = true;

        private bool hasTriggered = false;

        private void OnValidate()
        {
            if (bossBrain == null)
            {
#if UNITY_2022_3_OR_NEWER
                bossBrain = FindFirstObjectByType<BossRoombaBrain>();
#else
                bossBrain = FindObjectOfType<BossRoombaBrain>();
#endif
            }
        }

        private void Start()
        {
            // Auto-find references if not set
            if (bossBrain == null)
            {
#if UNITY_2022_3_OR_NEWER
                bossBrain = FindFirstObjectByType<BossRoombaBrain>();
#else
                bossBrain = FindObjectOfType<BossRoombaBrain>();
#endif
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryHandleCollision(collision.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHandleCollision(other.gameObject);
        }

        private void TryHandleCollision(GameObject collidedObject)
        {
            // Prevent multiple triggers
            if (hasTriggered)
                return;

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

            // IMPORTANT: Check the BossRoombaBrain's charge state (not the separate RoombaCageBullCharger)
            // The brain tracks its own isCharging and isTargetedCharge states
            if (bossBrain != null)
            {
                // Must be actively charging
                if (!bossBrain.IsCharging)
                {
                    EnemyBehaviorDebugLogBools.Log(nameof(BossPillarCollider), $"[Pillar {pillarIndex}] Boss collision ignored - not charging (casual contact)");
                    return;
                }

                // If onlyDuringTargetedCharge is true, must be a targeted charge specifically
                if (onlyDuringTargetedCharge && !bossBrain.IsTargetedCharge)
                {
                    EnemyBehaviorDebugLogBools.Log(nameof(BossPillarCollider), $"[Pillar {pillarIndex}] Boss collision ignored - static charge, not targeted");
                    return;
                }
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossPillarCollider), $"[Pillar {pillarIndex}] No BossRoombaBrain reference - cannot verify charge state!");
                return;
            }

            hasTriggered = true;
            EnemyBehaviorDebugLogBools.Log(nameof(BossPillarCollider), $"[Pillar {pillarIndex}] BOSS COLLISION DETECTED during TARGETED charge!");

            // Call the brain's collision handler (which will handle stun, form change, etc.)
            bossBrain.OnPillarCollision(pillarIndex);
        }

        /// <summary>
        /// Resets the pillar so it can trigger again (for testing/new fights).
        /// </summary>
        public void ResetPillar()
        {
            hasTriggered = false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = hasTriggered ? Color.gray : Color.green;
            Gizmos.DrawWireSphere(transform.position, 1f);
            
            
            // Draw pillar index label
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"Pillar {pillarIndex}");
        }
#endif
    }
}
