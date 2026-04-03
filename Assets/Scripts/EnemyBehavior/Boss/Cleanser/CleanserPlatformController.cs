// CleanserPlatformController.cs
// Purpose: Controls the rising platforms during the Cleanser's Double Maximum Sweep ultimate.
// Works with: CleanserBrain, DoubleMaximumSweepConfig
// Handles platform rise, orbit, and player parenting.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// Represents a single platform that rises during the ultimate attack.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?tab=t.0#bookmark=id.zeda1zrjntf0")]
    [System.Serializable]
    public class FloatingPlatform
    {
        [Tooltip("The platform GameObject.")]
        public GameObject PlatformObject;
        
        [Tooltip("The collider used for player detection.")]
        public Collider PlatformCollider;
        
        [Tooltip("Starting angle in the orbit (degrees).")]
        public float StartAngle = 0f;
        
        [HideInInspector] public Vector3 RestPosition;
        [HideInInspector] public Quaternion RestRotation;
        [HideInInspector] public float CurrentAngle;
        [HideInInspector] public bool IsRisen;
    }

    /// <summary>
    /// Controls the floating platforms during the Cleanser's ultimate attack.
    /// Handles rise animation, orbit movement, and player mounting.
    /// </summary>
    public class CleanserPlatformController : MonoBehaviour
    {
        [Header("Platform Configuration")]
        [Tooltip("List of platforms that rise during the ultimate.")]
        public List<FloatingPlatform> Platforms = new List<FloatingPlatform>();
        
        [Tooltip("Height platforms rise to (world Y coordinate).")]
        public float RiseHeight = 8f;

        [Tooltip("Optional transform used as baseline for RiseHeight. When assigned, final platform Y = HeightReference.y + RiseHeight.")]
        public Transform HeightReference;
        
        [Tooltip("Time for platforms to rise (seconds).")]
        public float RiseTime = 1.5f;
        
        [Tooltip("Curve for rise animation.")]
        public AnimationCurve RiseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Orbit Settings")]
        [Tooltip("Radius of the orbit around the Cleanser.")]
        public float OrbitRadius = 4f;
        
        [Tooltip("Speed of orbit rotation (degrees per second).")]
        public float OrbitSpeed = 30f;
        
        [Tooltip("Transform that platforms orbit around (usually Cleanser).")]
        public Transform OrbitCenter;

        [Header("Player Mounting")]
        [Tooltip("Layer mask for detecting the player.")]
        public LayerMask PlayerLayerMask;
        
        [Tooltip("Distance above platform surface to check for player.")]
        public float MountCheckHeight = 1f;
        
        [Tooltip("Radius of mount detection check.")]
        public float MountCheckRadius = 1f;

        [Header("VFX")]
        [Tooltip("VFX prefab spawned when platforms rise.")]
        public GameObject RiseVFXPrefab;
        
        [Tooltip("VFX prefab spawned when platforms lower.")]
        public GameObject LowerVFXPrefab;

        // Runtime state
        private bool platformsActive;
        private Transform mountedPlayer;
        private FloatingPlatform mountedPlatform;
        private Transform originalPlayerParent;
        private Coroutine orbitCoroutine;
        private Coroutine riseCoroutine;

        /// <summary>
        /// Returns true if platforms are currently raised and orbiting.
        /// </summary>
        public bool ArePlatformsActive => platformsActive;
        
        /// <summary>
        /// Returns true if a player is currently mounted on a platform.
        /// </summary>
        public bool IsPlayerMounted => mountedPlayer != null;

        private void Awake()
        {
            // Cache rest positions
            foreach (var platform in Platforms)
            {
                if (platform?.PlatformObject != null)
                {
                    platform.RestPosition = platform.PlatformObject.transform.position;
                    platform.RestRotation = platform.PlatformObject.transform.rotation;
                    platform.CurrentAngle = platform.StartAngle;
                    platform.IsRisen = false;
                }
            }
        }

        /// <summary>
        /// Raises all platforms and starts orbit.
        /// </summary>
        public void RaisePlatforms()
        {
            if (platformsActive)
                return;

            if (riseCoroutine != null)
                StopCoroutine(riseCoroutine);
                
            riseCoroutine = StartCoroutine(RisePlatformsCoroutine());
        }

        /// <summary>
        /// Lowers all platforms back to rest position.
        /// </summary>
        public void LowerPlatforms()
        {
            if (!platformsActive)
                return;

            // Unmount player first
            UnmountPlayer();

            if (orbitCoroutine != null)
            {
                StopCoroutine(orbitCoroutine);
                orbitCoroutine = null;
            }

            if (riseCoroutine != null)
                StopCoroutine(riseCoroutine);
                
            riseCoroutine = StartCoroutine(LowerPlatformsCoroutine());
        }

        private IEnumerator RisePlatformsCoroutine()
        {
            platformsActive = true;
            
            // Spawn VFX
            if (RiseVFXPrefab != null && OrbitCenter != null)
            {
                Instantiate(RiseVFXPrefab, OrbitCenter.position, Quaternion.identity);
            }

            // Calculate target positions based on orbit
            List<Vector3> targetPositions = new List<Vector3>();
            for (int i = 0; i < Platforms.Count; i++)
            {
                var platform = Platforms[i];
                if (platform?.PlatformObject == null)
                    continue;

                float angle = platform.StartAngle * Mathf.Deg2Rad;
                Vector3 orbitPos = OrbitCenter != null ? OrbitCenter.position : transform.position;
                float baseHeight = HeightReference != null ? HeightReference.position.y : orbitPos.y;
                Vector3 targetPos = new Vector3(
                    orbitPos.x + Mathf.Cos(angle) * OrbitRadius,
                    baseHeight + RiseHeight,
                    orbitPos.z + Mathf.Sin(angle) * OrbitRadius
                );
                targetPositions.Add(targetPos);
            }

            // Animate rise
            float elapsed = 0f;
            List<Vector3> startPositions = new List<Vector3>();
            foreach (var platform in Platforms)
            {
                startPositions.Add(platform.PlatformObject != null 
                    ? platform.PlatformObject.transform.position 
                    : Vector3.zero);
            }

            while (elapsed < RiseTime)
            {
                elapsed += Time.deltaTime;
                float t = RiseCurve.Evaluate(elapsed / RiseTime);

                for (int i = 0; i < Platforms.Count; i++)
                {
                    var platform = Platforms[i];
                    if (platform?.PlatformObject == null)
                        continue;

                    platform.PlatformObject.transform.position = Vector3.Lerp(
                        startPositions[i],
                        targetPositions[i],
                        t
                    );
                }

                yield return null;
            }

            // Ensure final positions
            for (int i = 0; i < Platforms.Count; i++)
            {
                var platform = Platforms[i];
                if (platform?.PlatformObject == null)
                    continue;
                    
                platform.PlatformObject.transform.position = targetPositions[i];
                platform.IsRisen = true;
            }

            // Start orbit
            orbitCoroutine = StartCoroutine(OrbitCoroutine());
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserPlatformController), "[CleanserPlatforms] Platforms risen and orbiting.");
#endif
        }

        private IEnumerator LowerPlatformsCoroutine()
        {
            // Spawn VFX
            if (LowerVFXPrefab != null && OrbitCenter != null)
            {
                Instantiate(LowerVFXPrefab, OrbitCenter.position, Quaternion.identity);
            }

            // Animate lower
            float elapsed = 0f;
            List<Vector3> startPositions = new List<Vector3>();
            foreach (var platform in Platforms)
            {
                startPositions.Add(platform.PlatformObject != null 
                    ? platform.PlatformObject.transform.position 
                    : Vector3.zero);
            }

            while (elapsed < RiseTime)
            {
                elapsed += Time.deltaTime;
                float t = RiseCurve.Evaluate(elapsed / RiseTime);

                for (int i = 0; i < Platforms.Count; i++)
                {
                    var platform = Platforms[i];
                    if (platform?.PlatformObject == null)
                        continue;

                    platform.PlatformObject.transform.position = Vector3.Lerp(
                        startPositions[i],
                        platform.RestPosition,
                        t
                    );
                }

                yield return null;
            }

            // Ensure final positions
            for (int i = 0; i < Platforms.Count; i++)
            {
                var platform = Platforms[i];
                if (platform?.PlatformObject == null)
                    continue;
                    
                platform.PlatformObject.transform.position = platform.RestPosition;
                platform.PlatformObject.transform.rotation = platform.RestRotation;
                platform.IsRisen = false;
            }

            platformsActive = false;
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserPlatformController), "[CleanserPlatforms] Platforms lowered.");
#endif
        }

        private IEnumerator OrbitCoroutine()
        {
            while (platformsActive)
            {
                Vector3 centerPos = OrbitCenter != null ? OrbitCenter.position : transform.position;

                foreach (var platform in Platforms)
                {
                    if (platform?.PlatformObject == null || !platform.IsRisen)
                        continue;

                    // Update angle
                    platform.CurrentAngle += OrbitSpeed * Time.deltaTime;
                    if (platform.CurrentAngle >= 360f)
                        platform.CurrentAngle -= 360f;

                    // Calculate new position
                    float rad = platform.CurrentAngle * Mathf.Deg2Rad;
                    Vector3 newPos = new Vector3(
                        centerPos.x + Mathf.Cos(rad) * OrbitRadius,
                        platform.PlatformObject.transform.position.y, // Keep current height
                        centerPos.z + Mathf.Sin(rad) * OrbitRadius
                    );

                    // Move platform
                    platform.PlatformObject.transform.position = newPos;
                    
                    // Face center (optional)
                    Vector3 lookDir = (centerPos - newPos).normalized;
                    lookDir.y = 0;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        platform.PlatformObject.transform.forward = lookDir;
                    }
                }

                // Check for player mounting
                CheckPlayerMounting();

                yield return null;
            }
        }

        private void CheckPlayerMounting()
        {
            // Skip if already mounted
            if (IsPlayerMounted)
            {
                // Check if player has jumped off
                if (mountedPlayer != null && mountedPlatform != null)
                {
                    Vector3 checkPos = mountedPlatform.PlatformObject.transform.position + Vector3.up * MountCheckHeight;
                    Collider[] hits = Physics.OverlapSphere(checkPos, MountCheckRadius * 1.5f, PlayerLayerMask);
                    
                    bool stillOnPlatform = false;
                    foreach (var hit in hits)
                    {
                        if (hit.transform == mountedPlayer || hit.transform.IsChildOf(mountedPlayer))
                        {
                            stillOnPlatform = true;
                            break;
                        }
                    }
                    
                    if (!stillOnPlatform)
                    {
                        UnmountPlayer();
                    }
                }
                return;
            }

            // Check each platform for player
            foreach (var platform in Platforms)
            {
                if (platform?.PlatformObject == null || !platform.IsRisen)
                    continue;

                Vector3 checkPos = platform.PlatformObject.transform.position + Vector3.up * MountCheckHeight;
                Collider[] hits = Physics.OverlapSphere(checkPos, MountCheckRadius, PlayerLayerMask);

                foreach (var hit in hits)
                {
                    if (hit.CompareTag("Player"))
                    {
                        MountPlayer(hit.transform, platform);
                        return;
                    }
                }
            }
        }

        private void MountPlayer(Transform player, FloatingPlatform platform)
        {
            mountedPlayer = player;
            mountedPlatform = platform;
            originalPlayerParent = player.parent;
            
            // Parent player to platform
            player.SetParent(platform.PlatformObject.transform);
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserPlatformController), $"[CleanserPlatforms] Player mounted on platform.");
#endif
        }

        private void UnmountPlayer()
        {
            if (mountedPlayer == null)
                return;

            // Restore original parent
            mountedPlayer.SetParent(originalPlayerParent);
            
            mountedPlayer = null;
            mountedPlatform = null;
            originalPlayerParent = null;
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserPlatformController), "[CleanserPlatforms] Player dismounted from platform.");
#endif
        }

        /// <summary>
        /// Forcibly unmounts the player (e.g., when platforms lower).
        /// </summary>
        public void ForceUnmountPlayer()
        {
            UnmountPlayer();
        }

        /// <summary>
        /// Resets all platforms to their rest state.
        /// </summary>
        public void ResetPlatforms()
        {
            UnmountPlayer();
            
            if (orbitCoroutine != null)
            {
                StopCoroutine(orbitCoroutine);
                orbitCoroutine = null;
            }
            
            if (riseCoroutine != null)
            {
                StopCoroutine(riseCoroutine);
                riseCoroutine = null;
            }

            foreach (var platform in Platforms)
            {
                if (platform?.PlatformObject == null)
                    continue;

                platform.PlatformObject.transform.position = platform.RestPosition;
                platform.PlatformObject.transform.rotation = platform.RestRotation;
                platform.CurrentAngle = platform.StartAngle;
                platform.IsRisen = false;
            }

            platformsActive = false;
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserPlatformController), "[CleanserPlatforms] Platforms reset to rest state.");
#endif
        }

        private void OnDrawGizmosSelected()
        {
            // Draw orbit circle
            if (OrbitCenter != null)
            {
                Gizmos.color = Color.cyan;
                DrawGizmoCircle(OrbitCenter.position, OrbitRadius, 32);
                
                // Draw height line
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(OrbitCenter.position, OrbitCenter.position + Vector3.up * RiseHeight);
            }

            // Draw platform positions
            foreach (var platform in Platforms)
            {
                if (platform?.PlatformObject == null)
                    continue;

                Gizmos.color = platform.IsRisen ? Color.green : Color.gray;
                Gizmos.DrawWireSphere(platform.PlatformObject.transform.position, 0.5f);
                
                // Draw mount check area
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(
                    platform.PlatformObject.transform.position + Vector3.up * MountCheckHeight,
                    MountCheckRadius
                );
            }
        }

        private void DrawGizmoCircle(Vector3 center, float radius, int segments)
        {
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
    }
}
