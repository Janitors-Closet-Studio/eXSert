// BossPlayerEjector.cs
// Purpose: Prevents the player from getting trapped inside the boss using TWO systems:
// 1. PREVENTION: A repulsion zone that continuously pushes the player away when they get too close
// 2. EJECTION (fallback): If they somehow get inside, eject them toward the arena center (not through walls)
// Attach to the boss GameObject (same object as BossRoombaBrain).

using System.Collections;
using UnityEngine;

namespace EnemyBehavior.Boss
{
    public class BossPlayerEjector : MonoBehaviour
    {
        [Header("Repulsion Zone (Prevention)")]
        [Tooltip("Radius at which the repulsion force starts (outer edge of repulsion zone)")]
        public float RepulsionOuterRadius = 5f;
        
        [Tooltip("Radius at which repulsion force is maximum (inner edge, close to boss center)")]
        public float RepulsionInnerRadius = 2f;
        
        [Tooltip("Maximum repulsion force applied when player is at inner radius")]
        public float MaxRepulsionForce = 25f;
        
        [Tooltip("Minimum repulsion force applied at outer radius edge")]
        public float MinRepulsionForce = 5f;

        [Header("Detection Settings")]
        [Tooltip("Radius to check for player overlap with boss center (triggers emergency ejection)")]
        public float OverlapCheckRadius = 2f;

        [Tooltip("How far above the boss center the player must be to count as 'on top' (not trapped)")]
        public float OnTopHeightThreshold = 2f;

        [Tooltip("How far below the boss center to still check for trapping")]
        public float BelowThreshold = 1f;

        [Header("Ejection Settings (Fallback)")]
        [Tooltip("Force applied during emergency ejection")]
        public float EjectionForce = 20f;

        [Tooltip("Upward force component to help player clear the boss")]
        public float EjectionUpwardForce = 8f;

        [Header("References")]
        [Tooltip("Transform representing the center of the boss (auto-found if null)")]
        public Transform BossCenter;
        
        [Tooltip("Reference to BossArenaManager for arena bounds (auto-found if null)")]
        public BossArenaManager ArenaManager;
        
        [Header("Post-Attack Grace Period")]
        [Tooltip("Time in seconds after a dash attack completes before strong ejection is allowed")]
        public float PostAttackGracePeriod = 1.0f;

        private Transform player;
        private PlayerMovement playerMovement;
        private CharacterController playerController;
        private bool isApplyingRepulsion;
        
        // Grace period tracking
        private float graceEndTime;
        private bool isInGracePeriod;

        private void Start()
        {
            if (BossCenter == null)
                BossCenter = transform;
            
            if (ArenaManager == null)
                ArenaManager = GetComponent<BossArenaManager>() 
                    ?? GetComponentInParent<BossArenaManager>()
                    ?? FindObjectOfType<BossArenaManager>();

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

            // Check Y-level first: if player is above the boss (standing on top), don't apply any forces
            Vector3 bossPos = BossCenter.position;
            Vector3 playerPos = player.position;
            float yDifference = playerPos.y - bossPos.y;
            
            if (yDifference > OnTopHeightThreshold || yDifference < -BelowThreshold)
            {
                // Player is on top or below - clear any active repulsion and skip
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

            // PREVENTION: Apply repulsion force when in repulsion zone
            if (distance2D < RepulsionOuterRadius && distance2D > 0.01f)
            {
                // During grace period, only apply gentle repulsion (no strong ejection)
                if (isInGracePeriod && Time.time < graceEndTime)
                {
                    // Reduced force during grace period
                    ApplyRepulsionForce(bossPos, playerPos, distance2D, 0.3f);
                }
                else
                {
                    isInGracePeriod = false;
                    ApplyRepulsionForce(bossPos, playerPos, distance2D, 1.0f);
                }
            }
            else if (isApplyingRepulsion)
            {
                // Player left repulsion zone - clear external velocity
                playerMovement?.ClearExternalVelocity();
                isApplyingRepulsion = false;
            }

            // FALLBACK: Emergency ejection if player is fully inside
            // Skip strong ejection during grace period
            if (distance2D < OverlapCheckRadius)
            {
                if (isInGracePeriod && Time.time < graceEndTime)
                {
                    // During grace period, just apply gentle repulsion, not full ejection
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.Log(nameof(BossPlayerEjector), $"[BossPlayerEjector] Skipping strong ejection during grace period (ends in {graceEndTime - Time.time:F1}s)");
#endif
                }
                else
                {
                    isInGracePeriod = false;
                    EjectPlayerSafely(bossPos, playerPos);
                }
            }
        }
        
        /// <summary>
        /// Call this when a dash attack ends to start a grace period.
        /// During this period, the ejector won't fire strong ejections.
        /// </summary>
        public void StartGracePeriod()
        {
            graceEndTime = Time.time + PostAttackGracePeriod;
            isInGracePeriod = true;
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(BossPlayerEjector), $"[BossPlayerEjector] Grace period STARTED - no strong ejection for {PostAttackGracePeriod}s");
#endif
        }

        /// <summary>
        /// Applies a repulsion force that increases as the player gets closer to the boss center.
        /// This prevents the player from getting inside the boss in the first place.
        /// </summary>
        private void ApplyRepulsionForce(Vector3 bossPos, Vector3 playerPos, float distance2D, float forceMultiplier = 1.0f)
        {
            if (playerMovement == null)
                return;

            // Calculate repulsion direction (away from boss center)
            Vector3 repulsionDir = (playerPos - bossPos);
            repulsionDir.y = 0;
            repulsionDir.Normalize();

            // Calculate force strength based on distance (closer = stronger)
            // At outer radius: MinRepulsionForce, at inner radius: MaxRepulsionForce
            float t = Mathf.InverseLerp(RepulsionOuterRadius, RepulsionInnerRadius, distance2D);
            float forceStrength = Mathf.Lerp(MinRepulsionForce, MaxRepulsionForce, t) * forceMultiplier;

            // Apply the repulsion velocity
            Vector3 repulsionVelocity = repulsionDir * forceStrength;
            playerMovement.SetExternalVelocity(repulsionVelocity);
            isApplyingRepulsion = true;

#if UNITY_EDITOR
            // Only log occasionally to avoid spam
            if (Time.frameCount % 30 == 0 && distance2D < RepulsionInnerRadius * 1.5f)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossPlayerEjector), $"[BossPlayerEjector] Repulsion active: distance={distance2D:F2}, force={forceStrength:F1}");
            }
#endif
        }

        /// <summary>
        /// Emergency ejection when player is fully inside the boss.
        /// Ejects toward the arena center to avoid going through walls.
        /// </summary>
        private void EjectPlayerSafely(Vector3 bossPos, Vector3 playerPos)
        {
            // Calculate direction toward arena center (safe direction)
            Vector3 arenaCenter = GetSafeEjectionTarget(bossPos, playerPos);
            Vector3 ejectionDir = (arenaCenter - bossPos);
            ejectionDir.y = 0;
            
            if (ejectionDir.sqrMagnitude < 0.01f)
            {
                // Boss is at arena center - eject in a random direction
                float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                ejectionDir = new Vector3(Mathf.Cos(randomAngle), 0, Mathf.Sin(randomAngle));
            }
            
            ejectionDir.Normalize();

            // Calculate target position: between boss and arena center, at safe distance
            Vector3 targetPos = bossPos + ejectionDir * (RepulsionOuterRadius + 1f);
            targetPos.y = playerPos.y;

#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning(nameof(BossPlayerEjector), $"[BossPlayerEjector] EMERGENCY EJECTION! Player inside boss - ejecting toward arena center at {targetPos}");
#endif

            // Teleport player to safe position
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

            // Apply ejection velocity to push them further away
            if (playerMovement != null)
            {
                Vector3 ejectionVelocity = ejectionDir * EjectionForce + Vector3.up * EjectionUpwardForce;
                playerMovement.ApplyKnockback(ejectionVelocity);
            }
        }

        /// <summary>
        /// Gets a safe ejection target position (toward arena center, away from walls).
        /// </summary>
        private Vector3 GetSafeEjectionTarget(Vector3 bossPos, Vector3 playerPos)
        {
            // If we have arena manager, use the arena center
            if (ArenaManager != null)
            {
                Vector3 arenaCenter = ArenaManager.GetArenaCenter();
                
                // If boss is very close to arena center, calculate direction away from nearest wall
                float bossToCenter = Vector3.Distance(
                    new Vector3(bossPos.x, 0, bossPos.z),
                    new Vector3(arenaCenter.x, 0, arenaCenter.z));
                
                if (bossToCenter < 3f)
                {
                    // Boss is near center - use player's position relative to center instead
                    return arenaCenter;
                }
                
                return arenaCenter;
            }
            
            // Fallback: eject away from boss (original behavior)
            Vector3 awayFromBoss = (playerPos - bossPos);
            awayFromBoss.y = 0;
            if (awayFromBoss.sqrMagnitude < 0.01f)
            {
                awayFromBoss = Vector3.forward;
            }
            return bossPos + awayFromBoss.normalized * 10f;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = BossCenter != null ? BossCenter.position : transform.position;

            // Draw repulsion outer radius (where repulsion starts)
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            DrawWireDisc(center, RepulsionOuterRadius, 32);
            
            // Draw repulsion inner radius (where repulsion is maximum)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            DrawWireDisc(center, RepulsionInnerRadius, 32);

            // Draw emergency ejection threshold (fully inside)
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            DrawWireDisc(center, OverlapCheckRadius, 32);

            // Draw Y-level bounds (the vertical range where repulsion/ejection applies)
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            Vector3 topBound = center + Vector3.up * OnTopHeightThreshold;
            Vector3 bottomBound = center - Vector3.up * BelowThreshold;
            
            // Draw lines showing the Y bounds
            float r = RepulsionOuterRadius;
            Gizmos.DrawLine(center + new Vector3(r, 0, 0), topBound + new Vector3(r, 0, 0));
            Gizmos.DrawLine(center + new Vector3(-r, 0, 0), topBound + new Vector3(-r, 0, 0));
            Gizmos.DrawLine(center + new Vector3(0, 0, r), topBound + new Vector3(0, 0, r));
            Gizmos.DrawLine(center + new Vector3(0, 0, -r), topBound + new Vector3(0, 0, -r));
            
            Gizmos.DrawLine(center + new Vector3(r, 0, 0), bottomBound + new Vector3(r, 0, 0));
            Gizmos.DrawLine(center + new Vector3(-r, 0, 0), bottomBound + new Vector3(-r, 0, 0));
            Gizmos.DrawLine(center + new Vector3(0, 0, r), bottomBound + new Vector3(0, 0, r));
            Gizmos.DrawLine(center + new Vector3(0, 0, -r), bottomBound + new Vector3(0, 0, -r));
            
            // Draw top threshold disc (above this = on top, allowed)
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            DrawWireDisc(topBound, RepulsionInnerRadius, 16);
            
            // Draw arena center if available
            if (ArenaManager != null)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.8f);
                Vector3 arenaCenter = ArenaManager.GetArenaCenter();
                Gizmos.DrawWireSphere(arenaCenter, 1f);
                Gizmos.DrawLine(center, arenaCenter);
            }
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