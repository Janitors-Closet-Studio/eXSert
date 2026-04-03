// CleanserPlatformController.cs
// Purpose: Controls the rising platforms during the Cleanser's Double Maximum Sweep ultimate.
// Works with: CleanserBrain, DoubleMaximumSweepConfig
// Handles platform rise, orbit, and player parenting.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        
        [Tooltip("Box collider used as the platform surface for player standing.")]
        public BoxCollider PlatformCollider;

        [Tooltip("Optional elevated box trigger used for mount detection above the platform surface.")]
        public BoxCollider MountCheckCollider;
        
        [Tooltip("Starting angle in the orbit (degrees).")]
        public float StartAngle = 0f;
        
        [HideInInspector] public Vector3 RestPosition;
        [HideInInspector] public Quaternion RestRotation;
        [HideInInspector] public float CurrentAngle;
        [HideInInspector] public bool IsRisen;
        [HideInInspector] public float OrbitBaseHeight;
        [HideInInspector] public float BobAmplitude;
        [HideInInspector] public float BobFrequency;
        [HideInInspector] public float BobPhase;
        [HideInInspector] public bool IsRuntimeInstantiated;
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

        [Header("Platform Bob")]
        [Tooltip("If true, each floating platform applies independent vertical bob while raised.")]
        public bool EnablePlatformBob = true;

        [Tooltip("Per-platform bob amplitude range (world units).")]
        public Vector2 PlatformBobAmplitudeRange = new Vector2(0.03f, 0.09f);

        [Tooltip("Per-platform bob frequency range (cycles per second).")]
        public Vector2 PlatformBobFrequencyRange = new Vector2(0.6f, 1.4f);

        [Header("Exit Motion")]
        [Tooltip("Orbit speed multiplier applied while platforms are exiting after a completed ultimate.")]
        [Min(1f)] public float ExitOrbitSpeedMultiplier = 2.5f;

        [Tooltip("How far outward platforms move (as a multiplier of OrbitRadius) while exiting after a completed ultimate.")]
        [Min(1f)] public float ExitOutwardRadiusMultiplier = 1.7f;

        [Tooltip("Orbit speed multiplier applied while platforms are exiting after an aerial-canceled ultimate.")]
        [Min(1f)] public float CanceledExitOrbitSpeedMultiplier = 3.5f;

        [Tooltip("How far outward platforms move (as a multiplier of OrbitRadius) while exiting after an aerial-canceled ultimate.")]
        [Min(1f)] public float CanceledExitOutwardRadiusMultiplier = 2.3f;

        [Header("Player Mounting")]
        [Tooltip("Layer mask for detecting the player.")]
        public LayerMask PlayerLayerMask;

        [Tooltip("Local Y offset for the elevated mount-check box collider above each platform.")]
        [FormerlySerializedAs("MountCheckHeight")]
        public float MountCheckBoxYOffset = 1f;

        [Tooltip("Extra size added to the elevated mount-check box collider.")]
        public Vector3 MountCheckBoxExtraSize = new Vector3(0.2f, 1f, 0.2f);

        [Tooltip("Grace time before unmounting when mount-check overlap is momentarily lost.")]
        [Min(0f)] public float MountExitGraceSeconds = 0.15f;

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
        private float mountedLastInsideTime;
        private Coroutine orbitCoroutine;
        private Coroutine riseCoroutine;
        private static readonly Collider[] mountCheckBuffer = new Collider[16];
        private bool useConfiguredScenePlatforms;
        private GameObject configuredScenePrimaryPlatform;
        private GameObject configuredSceneSecondaryPlatform;
        private GameObject configuredPrimaryPlatformPrefab;
        private GameObject configuredSecondaryPlatformPrefab;

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
            EnsureConfiguredPlatformsExist();

            // Cache rest positions
            foreach (var platform in Platforms)
            {
                CachePlatformRestState(platform);
            }
        }

        private void ConfigurePlatformBob(FloatingPlatform platform)
        {
            if (platform == null)
                return;

            if (!EnablePlatformBob)
            {
                platform.BobAmplitude = 0f;
                platform.BobFrequency = 0f;
                platform.BobPhase = 0f;
                return;
            }

            float minAmplitude = Mathf.Max(0f, Mathf.Min(PlatformBobAmplitudeRange.x, PlatformBobAmplitudeRange.y));
            float maxAmplitude = Mathf.Max(minAmplitude, Mathf.Max(PlatformBobAmplitudeRange.x, PlatformBobAmplitudeRange.y));
            float minFrequency = Mathf.Max(0f, Mathf.Min(PlatformBobFrequencyRange.x, PlatformBobFrequencyRange.y));
            float maxFrequency = Mathf.Max(minFrequency, Mathf.Max(PlatformBobFrequencyRange.x, PlatformBobFrequencyRange.y));

            platform.BobAmplitude = Random.Range(minAmplitude, maxAmplitude);
            platform.BobFrequency = Random.Range(minFrequency, maxFrequency);
            platform.BobPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        public void ConfigurePlatformSources(
            bool useScenePlatforms,
            GameObject scenePrimary,
            GameObject sceneSecondary,
            GameObject prefabPrimary,
            GameObject prefabSecondary = null)
        {
            useConfiguredScenePlatforms = useScenePlatforms;
            configuredScenePrimaryPlatform = scenePrimary;
            configuredSceneSecondaryPlatform = sceneSecondary;
            configuredPrimaryPlatformPrefab = prefabPrimary;
            configuredSecondaryPlatformPrefab = prefabSecondary;
        }

        public void ConfigurePlatformPrefabs(GameObject primaryPrefab, GameObject secondaryPrefab = null)
        {
            configuredPrimaryPlatformPrefab = primaryPrefab;
            configuredSecondaryPlatformPrefab = secondaryPrefab;
            useConfiguredScenePlatforms = false;
            configuredScenePrimaryPlatform = null;
            configuredSceneSecondaryPlatform = null;
        }

        private void EnsureConfiguredPlatformsExist()
        {
            if (useConfiguredScenePlatforms)
            {
                EnsurePlatformSlots(2);

                for (int i = 0; i < 2; i++)
                {
                    FloatingPlatform platform = Platforms[i];
                    GameObject sceneObject = i == 0
                        ? configuredScenePrimaryPlatform
                        : configuredSceneSecondaryPlatform;

                    if (sceneObject == null)
                        continue;

                    platform.PlatformObject = sceneObject;
                    platform.IsRuntimeInstantiated = false;

                    if (platform.PlatformCollider == null)
                    {
                        platform.PlatformCollider = EnsurePlatformSurfaceCollider(sceneObject);
                    }

                    EnsurePlatformMountCheckCollider(platform);

                    CachePlatformRestState(platform);
                }

                return;
            }

            if (configuredPrimaryPlatformPrefab == null)
                return;

            EnsurePlatformSlots(2);

            for (int i = 0; i < 2; i++)
            {
                FloatingPlatform platform = Platforms[i];

                if (platform.PlatformObject == null)
                {
                    GameObject prefab = i == 0
                        ? configuredPrimaryPlatformPrefab
                        : (configuredSecondaryPlatformPrefab != null ? configuredSecondaryPlatformPrefab : configuredPrimaryPlatformPrefab);

                    if (prefab == null)
                        continue;

                    if (Mathf.Abs(platform.StartAngle) < 0.001f)
                        platform.StartAngle = i == 0 ? 0f : 180f;

                    Vector3 spawnPos = GetConfiguredPlatformRestPosition(platform.StartAngle);
                    GameObject platformObject = Instantiate(prefab, spawnPos, Quaternion.identity);
                    platformObject.name = $"{prefab.name}_UltimatePlatform_{i + 1}";
                    platform.PlatformObject = platformObject;
                    platform.IsRuntimeInstantiated = true;
                }

                if (platform.PlatformCollider == null && platform.PlatformObject != null)
                {
                    platform.PlatformCollider = EnsurePlatformSurfaceCollider(platform.PlatformObject);
                }

                EnsurePlatformMountCheckCollider(platform);

                CachePlatformRestState(platform);
            }
        }

        private void EnsurePlatformSlots(int requiredCount)
        {
            for (int i = 0; i < requiredCount; i++)
            {
                if (Platforms.Count <= i)
                {
                    Platforms.Add(new FloatingPlatform { StartAngle = i == 0 ? 0f : 180f });
                    continue;
                }

                if (Platforms[i] == null)
                    Platforms[i] = new FloatingPlatform { StartAngle = i == 0 ? 0f : 180f };
            }
        }

        private Vector3 GetConfiguredPlatformRestPosition(float angleDeg)
        {
            Vector3 centerPos = OrbitCenter != null ? OrbitCenter.position : transform.position;
            float baseHeight = HeightReference != null ? HeightReference.position.y : centerPos.y;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            return new Vector3(
                centerPos.x + Mathf.Cos(angleRad) * OrbitRadius,
                baseHeight,
                centerPos.z + Mathf.Sin(angleRad) * OrbitRadius);
        }

        private void CachePlatformRestState(FloatingPlatform platform)
        {
            if (platform?.PlatformObject == null)
                return;

            platform.RestPosition = platform.PlatformObject.transform.position;
            platform.RestRotation = platform.PlatformObject.transform.rotation;
            platform.CurrentAngle = platform.StartAngle;
            platform.IsRisen = false;
            platform.OrbitBaseHeight = platform.RestPosition.y;

            if (platform.MountCheckCollider != null)
            {
                platform.MountCheckCollider.transform.localRotation = Quaternion.identity;
                platform.MountCheckCollider.transform.localPosition = new Vector3(0f, MountCheckBoxYOffset, 0f);
            }
        }

        private BoxCollider EnsurePlatformSurfaceCollider(GameObject platformObject)
        {
            if (platformObject == null)
                return null;

            BoxCollider box = platformObject.GetComponent<BoxCollider>();
            if (box == null)
                box = platformObject.AddComponent<BoxCollider>();

            box.isTrigger = false;
            EnsurePlatformPhysicsBody(platformObject);
            return box;
        }

        private void EnsurePlatformPhysicsBody(GameObject platformObject)
        {
            if (platformObject == null)
                return;

            Rigidbody rb = platformObject.GetComponent<Rigidbody>();
            if (rb == null)
                rb = platformObject.AddComponent<Rigidbody>();

            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void EnsurePlatformMountCheckCollider(FloatingPlatform platform)
        {
            if (platform == null || platform.PlatformObject == null || platform.PlatformCollider == null)
                return;

            if (platform.MountCheckCollider == null)
            {
                Transform existing = platform.PlatformObject.transform.Find("MountCheckBox");
                if (existing != null)
                    platform.MountCheckCollider = existing.GetComponent<BoxCollider>();
            }

            if (platform.MountCheckCollider == null)
            {
                GameObject mountCheckObj = new GameObject("MountCheckBox");
                mountCheckObj.transform.SetParent(platform.PlatformObject.transform, false);
                platform.MountCheckCollider = mountCheckObj.AddComponent<BoxCollider>();
            }

            platform.MountCheckCollider.isTrigger = true;
            platform.MountCheckCollider.center = Vector3.zero;
            platform.MountCheckCollider.size = platform.PlatformCollider.size + MountCheckBoxExtraSize;
            platform.MountCheckCollider.transform.localRotation = Quaternion.identity;
            platform.MountCheckCollider.transform.localPosition = new Vector3(0f, MountCheckBoxYOffset, 0f);
        }

        private static Vector3 GetWorldHalfExtents(BoxCollider box)
        {
            Vector3 worldScale = box.transform.lossyScale;
            Vector3 absScale = new Vector3(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z));
            return Vector3.Scale(box.size, absScale) * 0.5f;
        }

        /// <summary>
        /// Raises all platforms and starts orbit.
        /// </summary>
        public void RaisePlatforms()
        {
            if (platformsActive)
                return;

            EnsureConfiguredPlatformsExist();

            if (riseCoroutine != null)
                StopCoroutine(riseCoroutine);
                
            riseCoroutine = StartCoroutine(RisePlatformsCoroutine());
        }

        /// <summary>
        /// Lowers all platforms back to rest position.
        /// </summary>
        public void LowerPlatforms(bool canceledByAerial = false)
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
                
            riseCoroutine = StartCoroutine(LowerPlatformsCoroutine(canceledByAerial));
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
                platform.OrbitBaseHeight = targetPos.y;
                ConfigurePlatformBob(platform);
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

        private IEnumerator LowerPlatformsCoroutine(bool canceledByAerial)
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

            float speedMultiplier = canceledByAerial
                ? Mathf.Max(1f, CanceledExitOrbitSpeedMultiplier)
                : Mathf.Max(1f, ExitOrbitSpeedMultiplier);
            float outwardMultiplier = canceledByAerial
                ? Mathf.Max(1f, CanceledExitOutwardRadiusMultiplier)
                : Mathf.Max(1f, ExitOutwardRadiusMultiplier);

            while (elapsed < RiseTime)
            {
                elapsed += Time.deltaTime;
                float t = RiseCurve.Evaluate(elapsed / RiseTime);
                Vector3 centerPos = OrbitCenter != null ? OrbitCenter.position : transform.position;

                for (int i = 0; i < Platforms.Count; i++)
                {
                    var platform = Platforms[i];
                    if (platform?.PlatformObject == null)
                        continue;

                    float dt = Time.deltaTime;
                    platform.CurrentAngle += OrbitSpeed * speedMultiplier * dt;
                    if (platform.CurrentAngle >= 360f)
                        platform.CurrentAngle -= 360f;

                    float radius = Mathf.Lerp(OrbitRadius, OrbitRadius * outwardMultiplier, t);
                    float rad = platform.CurrentAngle * Mathf.Deg2Rad;
                    Vector3 outwardPos = new Vector3(
                        centerPos.x + Mathf.Cos(rad) * radius,
                        Mathf.Lerp(startPositions[i].y, platform.RestPosition.y, t),
                        centerPos.z + Mathf.Sin(rad) * radius);

                    if (useConfiguredScenePlatforms)
                    {
                        float returnBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.45f) / 0.55f));
                        platform.PlatformObject.transform.position = Vector3.Lerp(outwardPos, platform.RestPosition, returnBlend);
                    }
                    else
                    {
                        platform.PlatformObject.transform.position = outwardPos;
                    }

                    Vector3 lookDir = (centerPos - platform.PlatformObject.transform.position).normalized;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.001f)
                        platform.PlatformObject.transform.forward = lookDir;
                }

                yield return null;
            }

            // Ensure final positions
            for (int i = 0; i < Platforms.Count; i++)
            {
                var platform = Platforms[i];
                if (platform?.PlatformObject == null)
                    continue;

                if (useConfiguredScenePlatforms)
                {
                    platform.PlatformObject.transform.position = platform.RestPosition;
                    platform.PlatformObject.transform.rotation = platform.RestRotation;
                    platform.IsRisen = false;
                }

                if (!useConfiguredScenePlatforms && platform.IsRuntimeInstantiated)
                {
                    Destroy(platform.PlatformObject);
                    platform.PlatformObject = null;
                    platform.PlatformCollider = null;
                    platform.MountCheckCollider = null;
                    platform.IsRuntimeInstantiated = false;
                }
                else
                {
                    platform.IsRisen = false;
                }
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
                        platform.OrbitBaseHeight,
                        centerPos.z + Mathf.Sin(rad) * OrbitRadius
                    );

                    if (platform.BobAmplitude > 0f && platform.BobFrequency > 0f)
                    {
                        float bobOffset = Mathf.Sin((Time.time * platform.BobFrequency * Mathf.PI * 2f) + platform.BobPhase) * platform.BobAmplitude;
                        newPos.y += bobOffset;
                    }

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
                    BoxCollider mountCheckCollider = mountedPlatform.MountCheckCollider;
                    if (mountCheckCollider == null)
                    {
                        UnmountPlayer();
                        return;
                    }

                    Vector3 checkCenter = mountCheckCollider.transform.TransformPoint(mountCheckCollider.center);
                    Vector3 checkHalfExtents = GetWorldHalfExtents(mountCheckCollider);
                    checkHalfExtents *= 1.1f;
                    int hitCount = Physics.OverlapBoxNonAlloc(
                        checkCenter,
                        checkHalfExtents,
                        mountCheckBuffer,
                        mountCheckCollider.transform.rotation,
                        PlayerLayerMask,
                        QueryTriggerInteraction.Collide);
                    
                    bool stillOnPlatform = false;
                    for (int i = 0; i < hitCount; i++)
                    {
                        Collider hit = mountCheckBuffer[i];
                        if (hit == null)
                            continue;

                        Transform hitRoot = hit.transform.root;
                        if (hitRoot == mountedPlayer || hit.transform == mountedPlayer || hit.transform.IsChildOf(mountedPlayer))
                        {
                            stillOnPlatform = true;
                            break;
                        }
                    }

                    if (stillOnPlatform)
                    {
                        mountedLastInsideTime = Time.time;
                    }
                    else if (Time.time - mountedLastInsideTime > MountExitGraceSeconds)
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

                BoxCollider mountCheckCollider = platform.MountCheckCollider;
                if (mountCheckCollider == null)
                    continue;

                int hitCount = Physics.OverlapBoxNonAlloc(
                    mountCheckCollider.transform.TransformPoint(mountCheckCollider.center),
                    GetWorldHalfExtents(mountCheckCollider),
                    mountCheckBuffer,
                    mountCheckCollider.transform.rotation,
                    PlayerLayerMask,
                    QueryTriggerInteraction.Collide);

                for (int i = 0; i < hitCount; i++)
                {
                    Collider hit = mountCheckBuffer[i];
                    if (hit == null)
                        continue;

                    Transform hitRoot = hit.transform.root;
                    bool isPlayer = hit.CompareTag("Player") || (hitRoot != null && hitRoot.CompareTag("Player"));
                    if (isPlayer)
                    {
                        MountPlayer(hitRoot != null ? hitRoot : hit.transform, platform);
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
            mountedLastInsideTime = Time.time;
            
            // Parent player to platform
            player.SetParent(platform.PlatformObject.transform, true);
            
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
            mountedLastInsideTime = 0f;
            
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
                if (platform.MountCheckCollider != null)
                {
                    Vector3 lossy = platform.MountCheckCollider.transform.lossyScale;
                    Vector3 absLossy = new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
                    Gizmos.matrix = Matrix4x4.TRS(
                        platform.MountCheckCollider.transform.TransformPoint(platform.MountCheckCollider.center),
                        platform.MountCheckCollider.transform.rotation,
                        absLossy);
                    Gizmos.DrawWireCube(Vector3.zero, platform.MountCheckCollider.size);
                    Gizmos.matrix = Matrix4x4.identity;
                }
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
