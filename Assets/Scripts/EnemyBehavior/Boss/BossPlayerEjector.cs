// BossPlayerEjector.cs
// Purpose: Prevents the player from getting trapped inside the boss using TWO systems:
// 1. PREVENTION: A repulsion zone that continuously pushes the player away when they get too close
// 2. EJECTION (fallback): If they somehow get inside, eject them toward the arena center (not through walls)
// Attach to the boss GameObject (same object as BossRoombaBrain).

using UnityEngine;

namespace EnemyBehavior.Boss
{
    public class BossPlayerEjector : MonoBehaviour
    {
        [Header("Repulsion / Ejection")]
        [Tooltip("Single radius used for repulsion and emergency ejection checks.")]
        public float RepulsionRadius = 3.5f;

        [Tooltip("Repulsion force while player is inside repulsion radius.")]
        public float RepulsionForce = 8f;

        [Tooltip("How far above the boss center the player must be to count as 'on top' (not trapped)")]
        public float OnTopHeightThreshold = 2f;

        [Header("Ejection Settings (Fallback)")]
        [Tooltip("Force applied during emergency ejection")]
        public float EjectionForce = 20f;

        [Tooltip("Upward force component to help player clear the boss")]
        public float EjectionUpwardForce = 8f;

        [Tooltip("Minimum time between emergency ejections while overlapping the boss.")]
        [Min(0f)]
        public float EjectionCooldown = 0.35f;

        [Tooltip("If true, emergency ejection can teleport the player out when they are deeply inside the boss.")]
        public bool TeleportOnEmergencyEject = false;

        [Tooltip("Player must be within this radius (from boss center) before emergency ejection is allowed to teleport.")]
        [Min(0f)]
        public float TeleportOverlapRadius = 0.75f;

        [Header("References")]
        [Tooltip("Transform representing the center of the boss (auto-found if null)")]
        public Transform BossCenter;

        private Transform player;
        private PlayerMovement playerMovement;
        private CharacterController playerController;
        private bool isApplyingRepulsion;
        private BossRoombaBrain bossBrain;
        private float lastEjectionTime = -999f;

        private void Start()
        {
            if (BossCenter == null)
                BossCenter = transform;

            if (bossBrain == null)
                bossBrain = GetComponent<BossRoombaBrain>() ?? GetComponentInParent<BossRoombaBrain>();

            CachePlayerReference();
        }

        private void CachePlayerReference()
        {
            if (PlayerPresenceManager.IsPlayerPresent)
            {
                var presencePlayer = PlayerPresenceManager.PlayerTransform;
                if (presencePlayer != null)
                {
                    playerMovement = presencePlayer.GetComponent<PlayerMovement>()
                        ?? presencePlayer.GetComponentInParent<PlayerMovement>()
                        ?? presencePlayer.GetComponentInChildren<PlayerMovement>();

                    if (playerMovement != null)
                    {
                        player = playerMovement.transform;
                        playerController = playerMovement.GetComponent<CharacterController>()
                            ?? playerMovement.GetComponentInChildren<CharacterController>();
                        return;
                    }
                }
            }

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerMovement = playerObj.GetComponent<PlayerMovement>()
                    ?? playerObj.GetComponentInParent<PlayerMovement>()
                    ?? playerObj.GetComponentInChildren<PlayerMovement>();

                if (playerMovement != null)
                {
                    player = playerMovement.transform;
                    playerController = playerMovement.GetComponent<CharacterController>()
                        ?? playerMovement.GetComponentInChildren<CharacterController>();
                }
                else
                {
                    player = playerObj.transform;
                }
            }
        }

        private void FixedUpdate()
        {
            if (player == null)
            {
                CachePlayerReference();
                return;
            }

            if (IsPlayerIntentionallyMounted())
            {
                if (isApplyingRepulsion)
                {
                    playerMovement?.ClearExternalVelocity();
                    isApplyingRepulsion = false;
                }
                return;
            }

            // Check Y-level first: if player is above the boss (standing on top), don't apply any forces
            Vector3 bossPos = BossCenter.position;
            Vector3 playerPos = player.position;
            float yDifference = playerPos.y - bossPos.y;
            
            if (yDifference > OnTopHeightThreshold)
            {
                // Player is on top - clear any active repulsion and skip
                if (isApplyingRepulsion)
                {
                    playerMovement?.ClearExternalVelocity();
                    isApplyingRepulsion = false;
                }
                return;
            }

            // Calculate horizontal distance
            Vector3 bossPos2D = new Vector3(bossPos.x, 0, bossPos.z);
            Vector3 playerPos2D = new Vector3(playerPos.x, 0, playerPos.z);
            float distance2D = Vector3.Distance(bossPos2D, playerPos2D);

            if (distance2D < RepulsionRadius && distance2D > 0.01f)
            {
                ApplyRepulsionForce(bossPos, playerPos);

                if ((Time.time - lastEjectionTime) >= EjectionCooldown)
                {
                    lastEjectionTime = Time.time;
                    EjectPlayerSafely(bossPos, playerPos, distance2D);
                }
            }
            else if (isApplyingRepulsion)
            {
                playerMovement?.ClearExternalVelocity();
                isApplyingRepulsion = false;
            }
        }
        
        /// <summary>
        /// Call this when a dash attack ends to start a grace period.
        /// During this period, the ejector won't fire strong ejections.
        /// </summary>
        public void StartGracePeriod()
        {
            // Keep API compatibility with boss attack flow.
            // This temporarily blocks emergency ejection by resetting cooldown window.
            lastEjectionTime = Time.time;
        }

        /// <summary>
        /// Applies a repulsion force that increases as the player gets closer to the boss center.
        /// This prevents the player from getting inside the boss in the first place.
        /// </summary>
        private void ApplyRepulsionForce(Vector3 bossPos, Vector3 playerPos)
        {
            if (playerMovement == null)
                return;

            // Calculate repulsion direction (away from boss center)
            Vector3 repulsionDir = (playerPos - bossPos);
            repulsionDir.y = 0;
            repulsionDir.Normalize();

            // Apply the repulsion velocity
            Vector3 repulsionVelocity = repulsionDir * RepulsionForce;
            playerMovement.SetExternalVelocity(repulsionVelocity);
            isApplyingRepulsion = true;
        }

        /// <summary>
        /// Emergency ejection when player is fully inside the boss.
        /// Ejects toward the arena center to avoid going through walls.
        /// </summary>
        private void EjectPlayerSafely(Vector3 bossPos, Vector3 playerPos, float distance2D)
        {
            Vector3 ejectionDir = (playerPos - bossPos);
            ejectionDir.y = 0;
            
            if (ejectionDir.sqrMagnitude < 0.01f)
            {
                float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                ejectionDir = new Vector3(Mathf.Cos(randomAngle), 0, Mathf.Sin(randomAngle));
            }
            
            ejectionDir.Normalize();

            bool shouldTeleport = TeleportOnEmergencyEject && distance2D <= TeleportOverlapRadius;
            Vector3 targetPos = bossPos + ejectionDir * (RepulsionRadius + 1f);
            targetPos.y = playerPos.y;

            // Optional teleport only for deep overlap, otherwise rely on knockback impulse.
            if (shouldTeleport)
            {
                if (playerController != null)
                {
                    playerController.enabled = false;
                    player.position = targetPos;
                    playerController.enabled = true;
                }
                else
                {
                    player.position = targetPos;
                }
            }

            // Apply ejection velocity to push them further away
            if (playerMovement != null)
            {
                Vector3 ejectionVelocity = ejectionDir * EjectionForce + Vector3.up * EjectionUpwardForce;
                playerMovement.ApplyKnockback(ejectionVelocity);
            }
        }

        private bool IsPlayerIntentionallyMounted()
        {
            if (bossBrain != null && bossBrain.IsPlayerMountedOnTop)
                return true;

            if (player == null || BossCenter == null)
                return false;

            // Fallback: parented under boss hierarchy indicates intentional mount carry state.
            return player.IsChildOf(BossCenter);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = BossCenter != null ? BossCenter.position : transform.position;

            // Draw single repulsion/ejection radius
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            DrawWireDisc(center, RepulsionRadius, 32);

            // Draw hard teleport threshold
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            DrawWireDisc(center, TeleportOverlapRadius, 32);

            // Draw Y-level bounds (the vertical range where repulsion/ejection applies)
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            Vector3 topBound = center + Vector3.up * OnTopHeightThreshold;
            
            // Draw lines showing the top-safe bound
            float r = RepulsionRadius;
            Gizmos.DrawLine(center + new Vector3(r, 0, 0), topBound + new Vector3(r, 0, 0));
            Gizmos.DrawLine(center + new Vector3(-r, 0, 0), topBound + new Vector3(-r, 0, 0));
            Gizmos.DrawLine(center + new Vector3(0, 0, r), topBound + new Vector3(0, 0, r));
            Gizmos.DrawLine(center + new Vector3(0, 0, -r), topBound + new Vector3(0, 0, -r));
            
            // Draw top threshold disc (above this = on top, allowed)
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            DrawWireDisc(topBound, RepulsionRadius, 16);
        }

        private void DrawWireDisc(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }
    }
}