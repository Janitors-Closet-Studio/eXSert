// VacuumSuctionEffect.cs
// Purpose: Handles vacuum suction that pulls the player towards the arena center while navigating around pillars.
// Works with: BossRoombaBrain (triggers the effect), PlayerMovement (applies external velocity), BossArenaManager (pillars/bounds)
// Notes: Uses NavMesh pathfinding to navigate around obstacles, with fallback to direct pull when path is clear.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EnemyBehavior.Boss
{
    public class VacuumSuctionEffect : MonoBehaviour
    {
        [Header("Suction Settings")]
        [Tooltip("Base pull strength (units per second at max range)")]
        public float BasePullStrength = 8f;

        [Tooltip("Maximum pull strength when very close to source")]
        public float MaxPullStrength = 15f;

        [Tooltip("Distance at which suction starts affecting the player")]
        public float EffectiveRadius = 25f;

        [Tooltip("Distance at which max pull strength is reached")]
        public float MaxStrengthRadius = 3f;

        [Tooltip("How quickly the suction ramps up (0 = instant, 1 = very gradual)")]
        [Range(0f, 1f)]
        public float RampUpSpeed = 0.3f;

        [Header("Suction Blocking")]
        [Tooltip("If true, players behind pillars (no line of sight to roomba) won't be sucked")]
        public bool BlockSuctionBehindPillars = false;

        [Tooltip("Layer mask for line-of-sight checks to pillars")]
        public LayerMask PillarBlockingLayerMask;

        [Header("Height Threshold")]
        [Tooltip("If true, players above the height threshold won't be sucked")]
        public bool UseHeightThreshold = false;

        [Tooltip("Y-level above which players won't be sucked (only used if UseHeightThreshold is true)")]
        public float HeightThreshold = 5f;

        [Header("Pathfinding")]
        [Tooltip("If true, uses NavMesh to path around obstacles (pillars). If false, pulls directly.")]
        public bool UsePathfinding = true;

        [Tooltip("How often to recalculate the path (seconds)")]
        public float PathRecalculateInterval = 0.15f;

        [Tooltip("Distance threshold to consider a waypoint reached")]
        public float WaypointReachedThreshold = 1.5f;

        [Tooltip("NavMesh area mask for pathfinding")]
        public int NavMeshAreaMask = NavMesh.AllAreas;

        [Header("Obstacle Detection")]
        [Tooltip("Layer mask for obstacle raycasts (pillars, walls)")]
        public LayerMask ObstacleLayerMask;

        [Tooltip("Minimum distance to maintain from obstacles during pull")]
        public float ObstacleAvoidanceRadius = 1.0f;

        [Header("Visual Feedback")]
        [Tooltip("Optional particle effect to show suction direction")]
        public ParticleSystem SuctionVFX;

        [Tooltip("Optional audio source for vacuum sound")]
        public AudioSource VacuumAudioSource;

        [Header("References")]
        [Tooltip("The target point to pull the player towards (usually arena center)")]
        public Transform SuctionTarget;

        [Tooltip("Reference to the arena manager for pillar positions")]
        public BossArenaManager ArenaManager;

        // Runtime state
        private Transform player;
        private PlayerMovement playerMovement;
        private bool isActive;
        private float currentPullStrength;
        private NavMeshPath currentPath;
        private int currentWaypointIndex;
        private float lastPathCalculateTime;
        private Vector3[] pathCorners = new Vector3[32];
        private int pathCornerCount;

        private void Awake()
        {
            currentPath = new NavMeshPath();
        }

        /// <summary>
        /// Sets the player references from an external source (e.g., BossRoombaBrain).
        /// This avoids repeated FindWithTag calls.
        /// </summary>
        public void SetPlayerReferences(Transform playerTransform, PlayerMovement movement)
        {
            player = playerTransform;
            playerMovement = movement;
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), $"[VacuumSuctionEffect] Player references set: player={player?.name}, hasMovement={playerMovement != null}");
#endif
        }

        /// <summary>
        /// Starts the vacuum suction effect for the specified duration.
        /// </summary>
        public void StartSuction(float duration)
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), $"[VacuumSuctionEffect] StartSuction called with duration={duration}");
#endif
            
            // Only search for player if references weren't provided via SetPlayerReferences
            if (player == null || playerMovement == null)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), "[VacuumSuctionEffect] Player references not set, searching...");
#endif
                FindPlayerReferences();
            }

            if (player == null || playerMovement == null)
            {
                EnemyBehaviorDebugLogBools.LogError($"[VacuumSuctionEffect] Cannot start suction - player={player?.name ?? "NULL"}, playerMovement={playerMovement != null}");
                return;
            }

            if (SuctionTarget == null)
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(VacuumSuctionEffect), "[VacuumSuctionEffect] SuctionTarget is null! Will use this transform as fallback.");
            }
#if UNITY_EDITOR
            else
            {
                EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), $"[VacuumSuctionEffect] SuctionTarget position: {SuctionTarget.position}");
            }
#endif

            StartCoroutine(SuctionCoroutine(duration));
        }

        /// <summary>
        /// Fallback method to find player references if not provided externally.
        /// </summary>
        private void FindPlayerReferences()
        {
            // First try PlayerPresenceManager if available
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
                        EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), $"[VacuumSuctionEffect] Found player via PlayerPresenceManager: {player.name}");
                        return;
                    }
                }
            }

            // Fallback to FindWithTag
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), $"[VacuumSuctionEffect] Found tagged object: {playerObj.name}");
                
                // PlayerMovement might be on the tagged object, a parent, or a child
                playerMovement = playerObj.GetComponent<PlayerMovement>()
                    ?? playerObj.GetComponentInParent<PlayerMovement>()
                    ?? playerObj.GetComponentInChildren<PlayerMovement>();
                
                // Use the PlayerMovement's transform as the player reference
                if (playerMovement != null)
                {
                    player = playerMovement.transform;
                    EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), $"[VacuumSuctionEffect] Player set to: {player.name} (has PlayerMovement)");
                }
                else
                {
                    player = playerObj.transform;
                    EnemyBehaviorDebugLogBools.LogWarning(nameof(VacuumSuctionEffect), $"[VacuumSuctionEffect] Could not find PlayerMovement component! Using tagged object: {playerObj.name}");
                }
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogError($"[VacuumSuctionEffect] No GameObject with tag 'Player' found in scene!");
            }
        }

        /// <summary>
        /// Immediately stops the vacuum suction effect.
        /// </summary>
        public void StopSuction()
        {
            isActive = false;
            currentPullStrength = 0f;

            if (playerMovement != null)
            {
                playerMovement.ClearExternalVelocity();
            }

            if (SuctionVFX != null)
            {
                SuctionVFX.Stop();
            }

            if (VacuumAudioSource != null)
            {
                VacuumAudioSource.Stop();
            }
        }

        private IEnumerator SuctionCoroutine(float duration)
        {
            isActive = true;
            currentPullStrength = 0f;
            lastPathCalculateTime = -999f;
            currentWaypointIndex = 0;


#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), $"[VacuumSuctionEffect] SuctionCoroutine started - duration={duration}, player={player?.name}, playerMovement={playerMovement != null}");
#endif

            // Start VFX/Audio
            if (SuctionVFX != null)
            {
                SuctionVFX.Play();
            }

            if (VacuumAudioSource != null)
            {
                VacuumAudioSource.Play();
            }

            float elapsed = 0f;
#if UNITY_EDITOR
            int frameCount = 0;
#endif

            while (elapsed < duration && isActive)
            {
                if (player == null || playerMovement == null)
                {
                    EnemyBehaviorDebugLogBools.LogWarning(nameof(VacuumSuctionEffect), "[VacuumSuctionEffect] Player reference lost during suction!");
                    yield return null;
                    continue;
                }

                // Calculate target position
                Vector3 targetPos = SuctionTarget != null ? SuctionTarget.position : transform.position;
                targetPos.y = player.position.y; // Keep on same Y plane

                float distanceToTarget = Vector3.Distance(player.position, targetPos);

                // Check if player is already in the center zone
                if (distanceToTarget < MaxStrengthRadius)
                {
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), "[VacuumSuctionEffect] Player reached center zone!");
#endif
                    break;
                }

                // Check height threshold - if enabled and player is above threshold, don't suck
                bool playerAboveThreshold = UseHeightThreshold && player.position.y > HeightThreshold;
                if (playerAboveThreshold)
                {
#if UNITY_EDITOR
                    // Only log occasionally to avoid spam
                    frameCount++;
                    if (frameCount % 60 == 0)
                    {
                        EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), $"[VacuumSuctionEffect] Player above height threshold ({player.position.y:F1} > {HeightThreshold:F1}) - no suction");
                    }
#endif
                    // Clear any existing velocity and skip this frame
                    playerMovement.ClearExternalVelocity();
                    elapsed += Time.deltaTime;
                    yield return null;
                    continue;
                }

                // Check if player is behind a pillar (no line of sight to roomba)
                bool blockedByPillar = BlockSuctionBehindPillars && !HasLineOfSightToRoomba();
                if (blockedByPillar)
                {
#if UNITY_EDITOR
                    // Only log occasionally to avoid spam
                    frameCount++;
                    if (frameCount % 60 == 0)
                    {
                        EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), "[VacuumSuctionEffect] Player blocked by pillar - no suction");
                    }
#endif
                    // Clear any existing velocity and skip this frame
                    playerMovement.ClearExternalVelocity();
                    elapsed += Time.deltaTime;
                    yield return null;
                    continue;
                }

                // Calculate pull direction
                Vector3 pullDirection = GetPullDirection(targetPos);

                // Calculate pull strength based on distance (stronger when closer)
                float distanceFactor = Mathf.InverseLerp(EffectiveRadius, MaxStrengthRadius, distanceToTarget);
                float targetStrength = Mathf.Lerp(BasePullStrength, MaxPullStrength, distanceFactor);

                // Ramp up the pull strength gradually
                currentPullStrength = Mathf.Lerp(currentPullStrength, targetStrength, Time.deltaTime / Mathf.Max(0.01f, RampUpSpeed));

                // Apply the pull velocity
                Vector3 pullVelocity = pullDirection.normalized * currentPullStrength;
                playerMovement.SetExternalVelocity(pullVelocity);

#if UNITY_EDITOR
                // Debug log every 60 frames (~1 second) to reduce spam
                frameCount++;
                if (frameCount % 60 == 0)
                {
                    EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), $"[VacuumSuctionEffect] Pulling - dist={distanceToTarget:F1}m, strength={currentPullStrength:F1}");
                }
#endif

                elapsed += Time.deltaTime;
                yield return null;
            }

#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), $"[VacuumSuctionEffect] SuctionCoroutine ended - elapsed={elapsed:F2}s, isActive={isActive}");
#endif

            // Clean up
            StopSuction();
        }

        private Vector3 GetPullDirection(Vector3 targetPos)
        {
            Vector3 playerPos = player.position;

            // Check if we have a clear line of sight to target
            if (!UsePathfinding || HasClearLineOfSight(playerPos, targetPos))
            {
                // Direct pull
                return (targetPos - playerPos).normalized;
            }

            // Need to pathfind around obstacles
            if (Time.time - lastPathCalculateTime > PathRecalculateInterval)
            {
                RecalculatePath(playerPos, targetPos);
            }

            // Get direction to next waypoint
            if (pathCornerCount > 0 && currentWaypointIndex < pathCornerCount)
            {
                Vector3 nextWaypoint = pathCorners[currentWaypointIndex];
                nextWaypoint.y = playerPos.y; // Keep on same Y plane

                float distToWaypoint = Vector3.Distance(playerPos, nextWaypoint);

                // Check if we've reached this waypoint
                if (distToWaypoint < WaypointReachedThreshold && currentWaypointIndex < pathCornerCount - 1)
                {
                    currentWaypointIndex++;
                    nextWaypoint = pathCorners[currentWaypointIndex];
                    nextWaypoint.y = playerPos.y;
                }

                return (nextWaypoint - playerPos).normalized;
            }

            // Fallback to direct pull if path failed
            return (targetPos - playerPos).normalized;
        }

        /// <summary>
        /// Checks if the player has a clear line of sight to the roomba (this transform).
        /// Used for the BlockSuctionBehindPillars feature.
        /// </summary>
        private bool HasLineOfSightToRoomba()
        {
            if (player == null)
                return true; // Assume LOS if no player reference

            Vector3 playerPos = player.position + Vector3.up * 0.5f; // Offset above ground
            Vector3 roombaPos = transform.position + Vector3.up * 0.5f;
            Vector3 direction = roombaPos - playerPos;
            float distance = direction.magnitude;

            // First check with the PillarBlockingLayerMask if set
            if (PillarBlockingLayerMask != 0)
            {
                if (Physics.Raycast(playerPos, direction.normalized, out RaycastHit hit, distance, PillarBlockingLayerMask))
                {
                    // Hit something in the blocking layer
                    return false;
                }
            }

            // Also check against known pillar positions from ArenaManager
            if (ArenaManager != null && ArenaManager.Pillars != null)
            {
                foreach (var pillar in ArenaManager.Pillars)
                {
                    if (pillar == null || !pillar.activeInHierarchy) continue;

                    // Get pillar collider for more accurate check
                    var pillarCollider = pillar.GetComponent<Collider>();
                    if (pillarCollider != null)
                    {
                        // Check if ray from player to roomba intersects pillar bounds
                        Ray ray = new Ray(playerPos, direction.normalized);
                        if (pillarCollider.bounds.IntersectRay(ray, out float enterDist))
                        {
                            if (enterDist < distance)
                            {
                                // Pillar is between player and roomba
                                return false;
                            }
                        }
                    }
                    else
                    {
                        // Fallback: simple position-based check
                        Vector3 pillarPos = pillar.transform.position;
                        pillarPos.y = playerPos.y;

                        Vector3 toPillar = pillarPos - playerPos;
                        float dot = Vector3.Dot(toPillar.normalized, direction.normalized);
                        
                        // Check if pillar is roughly in the direction of the roomba
                        if (dot > 0.7f && toPillar.magnitude < distance)
                        {
                            // Check perpendicular distance to line
                            float perpDist = Vector3.Cross(direction.normalized, toPillar).magnitude;
                            if (perpDist < ObstacleAvoidanceRadius + 0.5f)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        private bool HasClearLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            float distance = direction.magnitude;

            // Offset the raycast start slightly above ground to avoid ground collision
            Vector3 rayStart = from + Vector3.up * 0.5f;
            Vector3 rayEnd = to + Vector3.up * 0.5f;
            direction = rayEnd - rayStart;

            if (Physics.Raycast(rayStart, direction.normalized, out RaycastHit hit, distance, ObstacleLayerMask))
            {
                // Hit an obstacle
                return false;
            }

            // Also check if any pillars are in the way
            if (ArenaManager != null && ArenaManager.Pillars != null)
            {
                foreach (var pillar in ArenaManager.Pillars)
                {
                    if (pillar == null || !pillar.activeInHierarchy) continue;

                    Vector3 pillarPos = pillar.transform.position;
                    pillarPos.y = from.y;

                    // Check if the pillar is between player and target
                    Vector3 toPillar = pillarPos - from;
                    Vector3 toTarget = to - from;

                    float dot = Vector3.Dot(toPillar.normalized, toTarget.normalized);
                    if (dot > 0.5f && toPillar.magnitude < toTarget.magnitude)
                    {
                        // Pillar is roughly in the direction of the target
                        // Check perpendicular distance to line
                        float perpDist = Vector3.Cross(toTarget.normalized, toPillar).magnitude;
                        if (perpDist < ObstacleAvoidanceRadius + 1f)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private void RecalculatePath(Vector3 from, Vector3 to)
        {
            lastPathCalculateTime = Time.time;
            currentWaypointIndex = 0;

            // Sample positions on NavMesh
            NavMeshHit fromHit, toHit;

            if (!NavMesh.SamplePosition(from, out fromHit, 5f, NavMeshAreaMask))
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(VacuumSuctionEffect), "[VacuumSuctionEffect] Could not find NavMesh position near player");
                pathCornerCount = 0;
                return;
            }

            if (!NavMesh.SamplePosition(to, out toHit, 5f, NavMeshAreaMask))
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(VacuumSuctionEffect), "[VacuumSuctionEffect] Could not find NavMesh position near target");
                pathCornerCount = 0;
                return;
            }

            // Calculate path
            if (NavMesh.CalculatePath(fromHit.position, toHit.position, NavMeshAreaMask, currentPath))
            {
                pathCornerCount = currentPath.GetCornersNonAlloc(pathCorners);

                // Skip the first corner (it's the start position)
                if (pathCornerCount > 1)
                {
                    currentWaypointIndex = 1;
                }

                EnemyBehaviorDebugLogBools.Log(nameof(VacuumSuctionEffect), $"[VacuumSuctionEffect] Path calculated with {pathCornerCount} corners");
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(VacuumSuctionEffect), "[VacuumSuctionEffect] Failed to calculate NavMesh path");
                pathCornerCount = 0;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw effective radius
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(SuctionTarget != null ? SuctionTarget.position : transform.position, EffectiveRadius);

            // Draw max strength radius
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(SuctionTarget != null ? SuctionTarget.position : transform.position, MaxStrengthRadius);

            // Draw current path
            if (pathCornerCount > 0 && isActive)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < pathCornerCount - 1; i++)
                {
                    Gizmos.DrawLine(pathCorners[i], pathCorners[i + 1]);
                    Gizmos.DrawWireSphere(pathCorners[i], 0.3f);
                }

                // Highlight current waypoint
                if (currentWaypointIndex < pathCornerCount)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(pathCorners[currentWaypointIndex], 0.5f);
                }
            }
        }
    }
}
