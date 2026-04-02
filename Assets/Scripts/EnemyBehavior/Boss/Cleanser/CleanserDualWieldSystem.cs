// CleanserDualWieldSystem.cs
// Purpose: Manages spare-weapon stockpile behavior for the Cleanser boss.
// Works with: CleanserBrain, CleanserComboSystem
// Spare weapons are stockpiled in a hover cluster, then launched by SpareToss and lodged in ground.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// Represents a spare weapon the Cleanser can pick up.
    /// These are pre-placed in the arena and picked up via magnetism/telekinesis.
    /// Weapons return to their rest position magnetically instead of being destroyed.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?tab=t.0#bookmark=id.6fppdnkripgh")]
    [System.Serializable]
    public class SpareWeapon
    {
        [Tooltip("The GameObject representing this spare weapon.")]
        public GameObject WeaponObject;

        [Tooltip("Source prefab key used by the runtime pool.")]
        [HideInInspector] public GameObject SourcePrefab;
        
        [Tooltip("If true, this weapon is currently held by the Cleanser.")]
        [HideInInspector] public bool IsHeld;
        
        [Tooltip("If true, this weapon is currently returning to its rest position.")]
        [HideInInspector] public bool IsReturning;
        
        [Tooltip("If true, this weapon is at its rest position and available for pickup.")]
        [HideInInspector] public bool IsAtRest;

        [HideInInspector] public bool HasStockpileOffsets;
        [HideInInspector] public float StockpileVerticalOffset;
        [HideInInspector] public float StockpileRollOffset;
        [HideInInspector] public float StockpileRollSpeed;
        [HideInInspector] public Vector3 StockpileFollowVelocity;
    }

    /// <summary>
    /// Manages the Cleanser's spare weapon pickup and return.
    /// The Cleanser always wields his halberd (right hand) - this system handles
    /// the optional spare weapon he can pick up (left hand). Only ONE spare can be held.
    /// Uses pooled spare-weapon visuals for stockpile/toss/spin-dash consumption.
    /// </summary>
    public class CleanserDualWieldSystem : MonoBehaviour
    {
        [Header("Spare Weapon Configuration")]
        [Tooltip("Optional fallback spawn points used when none are provided by CleanserBrain.")]
        [SerializeField] private List<Transform> fallbackSpawnPoints = new List<Transform>();

        [Header("Stockpile Hover")]
        [Tooltip("Local anchor around the Cleanser where stockpiled weapons hover.")]
        public Vector3 HoverAnchorLocal = new Vector3(0.8f, 1.8f, -0.2f);

        [Tooltip("Horizontal spacing between stockpiled hover weapons.")]
        public float HoverSpacing = 0.6f;

        [Tooltip("Vertical offset per stockpiled weapon index to reduce overlap.")]
        public float HoverVerticalStep = 0.12f;

        [Tooltip("How much yaw spread is applied across stockpiled weapons.")]
        public float HoverYawSpread = 30f;

        [Tooltip("Forward offset from Cleanser used as look target while spare weapons travel to stockpile.")]
        [SerializeField] private float stockpileLookTargetForwardOffset = 0.8f;

        [Tooltip("Final local rotation for stockpiled spare weapons. Use this to align spear-forward orientation.")]
        [SerializeField] private Vector3 stockpileLocalRotationEuler = Vector3.zero;

        [Tooltip("Random vertical offset range applied per stockpiled weapon to break uniform staircase stacking.")]
        [SerializeField] private Vector2 stockpileVerticalRandomOffsetRange = new Vector2(-0.12f, 0.12f);

        [Tooltip("Random roll offset range (degrees) applied per stockpiled weapon while keeping forward facing direction.")]
        [SerializeField] private Vector2 stockpileRollRandomOffsetRange = new Vector2(-35f, 35f);

        [Tooltip("Random roll speed range (degrees/sec) per stockpiled weapon. Supports negative for opposite spin direction.")]
        [SerializeField] private Vector2 stockpileRollSpeedRange = new Vector2(-18f, 18f);

        [Tooltip("How quickly stockpiled weapons follow their target hover positions. Higher = tighter, lower = floatier.")]
        [SerializeField, Min(0f)] private float stockpileFollowSmoothTime = 0.18f;

        [Tooltip("How quickly stockpiled weapons rotate toward their target orientation while hovering.")]
        [SerializeField, Min(0f)] private float stockpileRotationFollowSpeed = 7f;

        [Tooltip("Maximum lean angle (degrees) applied while stockpiled weapons are moving to follow the Cleanser.")]
        [SerializeField, Range(0f, 35f)] private float stockpileFollowLeanMaxAngle = 10f;

        [Tooltip("Movement speed that maps to full lean angle for stockpiled follow motion.")]
        [SerializeField, Min(0.01f)] private float stockpileFollowLeanSpeedForMax = 4f;

        [Header("Acquire Animation")]
        [Tooltip("Duration of the magnetism/telekinesis acquire animation.")]
        public float PickupAnimationDuration = 0.5f;
        
        [Tooltip("Curve for the acquire motion (0 = rest position, 1 = stockpile hover slot).")]
        public AnimationCurve PickupCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("How high a spare weapon rises straight up from its rest point before moving toward stockpile hover.")]
        public float PickupVerticalRiseHeight = 6f;

        [Tooltip("Portion of pickup duration spent on the initial straight-up rise.")]
        [Range(0.05f, 0.95f)] public float PickupVerticalRisePortion = 0.35f;

        [Tooltip("Additional arc height used during the travel-to-stockpile phase.")]
        public float PickupArcHeight = 2f;

        [Tooltip("Forward bias toward Cleanser-facing direction for pickup arc control point.")]
        public float PickupArcForwardBias = 0.6f;

        [Tooltip("If true, pickup travel tries to orbit around the Cleanser instead of cutting directly through the body.")]
        [SerializeField] private bool avoidPassingThroughCleanser = true;

        [Tooltip("Approximate horizontal body radius used for pickup path avoidance.")]
        [SerializeField, Min(0.2f)] private float pickupBodyAvoidRadius = 1.15f;

        [Tooltip("Angular speed used while orbiting around the Cleanser during pickup travel.")]
        [SerializeField, Min(30f)] private float pickupOrbitAngularSpeed = 360f;

        [Tooltip("Maximum random delay applied per pickup when queueing multiple pickups at once.")]
        [Min(0f)] public float PickupBurstMaxStartDelay = 0.12f;
        
        [Tooltip("VFX prefab to spawn during pickup (magnetism/telekinesis effect).")]
        public GameObject PickupVFXPrefab;
        
        [Tooltip("Audio clip to play during pickup.")]
        public AudioClip PickupSFX;

        [Header("Spare Toss (Weapon Rain)")]
        [Tooltip("Arc duration when launching stockpiled weapons into the ground.")]
        public float TossArcDuration = 0.7f;

        [Tooltip("Vertical height reached during the upward launch phase before weapons start descending.")]
        public float TossLaunchHeight = 10f;

        [Tooltip("Portion of toss duration spent on the initial near-vertical launch.")]
        [Range(0.05f, 0.95f)] public float TossLaunchPortion = 0.35f;

        [Tooltip("Small horizontal randomization applied to launch apex so the volley feels less uniform.")]
        public float TossApexHorizontalRandom = 1f;

        [Tooltip("Minimum spacing between landed weapons so SpinDash points do not overlap.")]
        public float MinLandingSpacing = 2f;

        [Tooltip("Minimum radius from toss center for landed weapons.")]
        public float LandingRadiusMin = 3f;

        [Tooltip("Maximum radius from toss center for landed weapons.")]
        public float LandingRadiusMax = 9f;

        [Tooltip("Blade-down rotation applied to landed weapons.")]
        public Vector3 LodgedRotationEuler = new Vector3(180f, 0f, 0f);

        [Tooltip("Vertical offset applied to lodged weapon position after landing. Positive values keep more of the weapon visible above ground.")]
        public float LodgedHeightOffset = 0.35f;

        [Tooltip("SFX played when stockpiled weapons are launched.")]
        public AudioClip TossLaunchSFX;

        [Tooltip("SFX played when each weapon impacts the ground.")]
        public AudioClip TossImpactSFX;

        [Tooltip("Impact VFX spawned when each weapon lodges in the ground.")]
        public GameObject TossImpactVFX;

        [Header("References")]
        [Tooltip("Audio source for SFX (uses SoundManager if null).")]
        public AudioSource SFXSource;
        [Tooltip("Optional dedicated spare-toss volley handler. Auto-found on Awake if left empty.")]
        [SerializeField] private SpareTossVolley spareTossVolley;

        // Runtime state
        private readonly List<SpareWeapon> stockpiledWeapons = new List<SpareWeapon>();
        private readonly List<SpareWeapon> lodgedWeapons = new List<SpareWeapon>();
        private readonly List<SpareWeapon> spareWeaponPool = new List<SpareWeapon>();
        private readonly Dictionary<GameObject, Queue<SpareWeapon>> inactivePoolByPrefab = new Dictionary<GameObject, Queue<SpareWeapon>>();
        private readonly List<GameObject> spareWeaponVisualPrefabs = new List<GameObject>();
        private int activePickupAnimations;
        private int pendingStockpileReservations;
        private int lastSelectedVisualPrefabIndex = -1;
        private readonly List<Transform> runtimeSpawnPoints = new List<Transform>();

        /// <summary>
        /// Returns true if the Cleanser is currently holding a spare weapon.
        /// </summary>
        public bool IsHoldingSpareWeapon => stockpiledWeapons.Count > 0;
        
        /// <summary>
        /// Returns true if a pickup animation is in progress.
        /// </summary>
        public bool IsPickingUp => activePickupAnimations > 0;
        
        /// <summary>
        /// Returns the number of available spare weapons (at rest, not held, not returning).
        /// </summary>
        public int AvailableSpareWeaponCount
        {
            get
            {
                return spareWeaponVisualPrefabs != null && spareWeaponVisualPrefabs.Count > 0 ? int.MaxValue : 0;
            }
        }
        
        /// <summary>
        /// Returns true if any weapon is currently in a return/cleanup state.
        /// </summary>
        public bool IsAnyWeaponReturning
        {
            get
            {
                foreach (var weapon in spareWeaponPool)
                {
                    if (weapon != null && weapon.IsReturning)
                        return true;
                }
                return false;
            }
        }
        
        /// <summary>
        /// Gets the currently held spare weapon (null if none).
        /// </summary>
        public SpareWeapon CurrentWeapon => stockpiledWeapons.Count > 0 ? stockpiledWeapons[0] : null;

        public int StockpiledWeaponCount => stockpiledWeapons.Count;
        public int LodgedWeaponCount => lodgedWeapons.Count;

        private void Awake()
        {
            ResolveSpareTossVolleyReference();

            if (runtimeSpawnPoints.Count == 0 && fallbackSpawnPoints != null)
                runtimeSpawnPoints.AddRange(fallbackSpawnPoints);

            EnsureBasePoolInitialized();
        }

        public void SetProjectileSpawnPoints(List<Transform> spawnPoints)
        {
            runtimeSpawnPoints.Clear();
            if (spawnPoints != null)
                runtimeSpawnPoints.AddRange(spawnPoints);

            if (runtimeSpawnPoints.Count == 0 && fallbackSpawnPoints != null)
                runtimeSpawnPoints.AddRange(fallbackSpawnPoints);
        }

        private void EnsureBasePoolInitialized()
        {
            if (spareWeaponVisualPrefabs == null)
                return;

            for (int i = 0; i < spareWeaponVisualPrefabs.Count; i++)
            {
                GameObject prefab = spareWeaponVisualPrefabs[i];
                if (prefab == null)
                    continue;

                if (!inactivePoolByPrefab.TryGetValue(prefab, out Queue<SpareWeapon> queue) || queue.Count == 0)
                {
                    SpareWeapon pooled = CreatePooledWeapon(prefab);
                    ReturnWeaponToPool(pooled);
                }
            }
        }

        /// <summary>
        /// Initiates pickup of the nearest available spare weapon.
        /// </summary>
        /// <returns>True if pickup started, false if no weapons available.</returns>
        public bool PickupSpareWeapon()
        {
            if (spareWeaponVisualPrefabs == null || spareWeaponVisualPrefabs.Count == 0)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CleanserDualWieldSystem), "[CleanserDualWield] No spare weapon prefabs configured for pickup.");
#endif
                return false;
            }

            int index = GetNextVisualPrefabIndex(spareWeaponVisualPrefabs);
            if (index < 0 || index >= spareWeaponVisualPrefabs.Count)
                return false;

            return PickupSpareWeapon(index);
        }

        /// <summary>
        /// Picks up a specific spare weapon by prefab index.
        /// </summary>
        public bool PickupSpareWeapon(int index)
        {
            if (spareWeaponVisualPrefabs == null || index < 0 || index >= spareWeaponVisualPrefabs.Count)
                return false;

            GameObject prefab = spareWeaponVisualPrefabs[index];
            if (prefab == null)
                return false;

            SpareWeapon target = AcquireWeaponFromPool(prefab);
            if (target == null || target.WeaponObject == null)
                return false;

            Vector3 spawnPos = GetRandomSpawnPosition();
            target.WeaponObject.transform.position = spawnPos;
            target.WeaponObject.transform.rotation = Quaternion.identity;
            target.WeaponObject.transform.SetParent(null);

            int reservedIndex = ReserveStockpileSlot(out int reservedCount);
            activePickupAnimations++;
            StartCoroutine(PickupWeaponCoroutine(target, 0f, reservedIndex, reservedCount));
            return true;
        }

        public int QueueSpareWeaponBurst(int count)
        {
            if (count <= 0 || spareWeaponVisualPrefabs == null || spareWeaponVisualPrefabs.Count == 0)
                return 0;

            int queued = 0;
            float maxDelay = Mathf.Max(0f, PickupBurstMaxStartDelay);
            for (int i = 0; i < count; i++)
            {
                int index = GetNextVisualPrefabIndex(spareWeaponVisualPrefabs);
                if (index < 0 || index >= spareWeaponVisualPrefabs.Count)
                    continue;

                GameObject prefab = spareWeaponVisualPrefabs[index];
                if (prefab == null)
                    continue;

                SpareWeapon target = AcquireWeaponFromPool(prefab);
                if (target == null || target.WeaponObject == null)
                    continue;

                target.WeaponObject.transform.position = GetRandomSpawnPosition();
                target.WeaponObject.transform.rotation = Quaternion.identity;
                target.WeaponObject.transform.SetParent(null);

                int reservedIndex = ReserveStockpileSlot(out int reservedCount);
                float startDelay = Random.Range(0f, maxDelay);
                activePickupAnimations++;
                StartCoroutine(PickupWeaponCoroutine(target, startDelay, reservedIndex, reservedCount));
                queued++;
            }

            return queued;
        }

        private IEnumerator PickupWeaponCoroutine(SpareWeapon weapon, float startDelay, int reservedIndex, int reservedCount)
        {
            bool countersReleased = false;
            void ReleaseCountersOnce()
            {
                if (countersReleased)
                    return;

                pendingStockpileReservations = Mathf.Max(0, pendingStockpileReservations - 1);
                activePickupAnimations = Mathf.Max(0, activePickupAnimations - 1);
                countersReleased = true;
            }

            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            if (weapon == null || weapon.WeaponObject == null)
            {
                ReleaseCountersOnce();
                yield break;
            }

            if (!weapon.WeaponObject.scene.IsValid())
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CleanserDualWieldSystem), "[CleanserDualWield] Pickup aborted: weapon reference points to a prefab asset instead of a scene instance.");
#endif
                ReleaseCountersOnce();
                yield break;
            }

            weapon.IsAtRest = false;
            
            // Spawn VFX
            GameObject vfx = null;
            if (PickupVFXPrefab != null && weapon.WeaponObject != null)
            {
                vfx = Instantiate(PickupVFXPrefab, weapon.WeaponObject.transform.position, Quaternion.identity);
            }
            
            // Play SFX
            PlaySFX(PickupSFX);

            Vector3 startPos = weapon.WeaponObject.transform.position;
            Quaternion startRot = weapon.WeaponObject.transform.rotation;
            
            // Unparent for smooth motion
            weapon.WeaponObject.transform.SetParent(null);

            float elapsed = 0f;
            float risePortion = Mathf.Clamp01(PickupVerticalRisePortion);
            float riseDuration = Mathf.Max(0.01f, PickupAnimationDuration * risePortion);
            float travelDuration = Mathf.Max(0.01f, PickupAnimationDuration - riseDuration);
            Vector3 risePos = startPos + Vector3.up * Mathf.Max(0f, PickupVerticalRiseHeight);
            Vector3 arcRandomOffset = new Vector3(Random.Range(-0.45f, 0.45f), 0f, Random.Range(-0.45f, 0.45f));

            while (elapsed < PickupAnimationDuration)
            {
                elapsed += Time.deltaTime;
                int dynamicCount = Mathf.Max(reservedCount, stockpiledWeapons.Count + pendingStockpileReservations);
                EnsureStockpileOffsets(weapon);
                Vector3 endPos = GetStockpileSlotWorldPosition(reservedIndex, dynamicCount, weapon.StockpileVerticalOffset);
                if (elapsed <= riseDuration)
                {
                    float tRise = PickupCurve.Evaluate(elapsed / riseDuration);
                    weapon.WeaponObject.transform.position = Vector3.Lerp(startPos, risePos, tRise);
                }
                else
                {
                    float tTravel = PickupCurve.Evaluate((elapsed - riseDuration) / travelDuration);
                    Vector3 currentPos = weapon.WeaponObject.transform.position;

                    bool useOrbitAvoidance = false;
                    if (avoidPassingThroughCleanser)
                    {
                        Vector3 toWeapon = currentPos - transform.position;
                        Vector3 toSlot = endPos - transform.position;
                        toWeapon.y = 0f;
                        toSlot.y = 0f;

                        if (toWeapon.sqrMagnitude > 0.0001f && toSlot.sqrMagnitude > 0.0001f)
                        {
                            bool weaponInFront = Vector3.Dot(transform.forward, toWeapon.normalized) > 0.05f;
                            bool slotBehind = Vector3.Dot(transform.forward, toSlot.normalized) < -0.05f;
                            float pathClearance = DistancePointToSegmentXZ(transform.position, currentPos, endPos);
                            bool pathCutsBody = pathClearance < pickupBodyAvoidRadius;
                            useOrbitAvoidance = weaponInFront && (slotBehind || pathCutsBody) && tTravel < 0.92f;
                        }
                    }

                    if (useOrbitAvoidance)
                    {
                        Vector3 center = transform.position;
                        Vector3 toWeapon = currentPos - center;
                        Vector3 toSlot = endPos - center;
                        toWeapon.y = 0f;
                        toSlot.y = 0f;

                        float radius = Mathf.Max(pickupBodyAvoidRadius, toWeapon.magnitude);
                        float currentAngle = Mathf.Atan2(toWeapon.z, toWeapon.x) * Mathf.Rad2Deg;
                        float targetAngle = Mathf.Atan2(toSlot.z, toSlot.x) * Mathf.Rad2Deg;
                        float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, Mathf.Max(30f, pickupOrbitAngularSpeed) * Time.deltaTime);
                        float nextRad = nextAngle * Mathf.Deg2Rad;
                        Vector3 orbitPos = center + new Vector3(Mathf.Cos(nextRad), 0f, Mathf.Sin(nextRad)) * radius;
                        orbitPos.y = Mathf.Lerp(currentPos.y, endPos.y, 0.2f);

                        float step = Mathf.Max(0.01f, Vector3.Distance(risePos, endPos) / travelDuration) * Time.deltaTime;
                        weapon.WeaponObject.transform.position = Vector3.MoveTowards(currentPos, orbitPos, step);
                    }
                    else
                    {
                        Vector3 control = Vector3.Lerp(risePos, endPos, 0.5f)
                            + Vector3.up * Mathf.Max(0f, PickupArcHeight)
                            + transform.forward * PickupArcForwardBias
                            + arcRandomOffset;
                        float omt = 1f - tTravel;
                        weapon.WeaponObject.transform.position = (omt * omt * risePos) + (2f * omt * tTravel * control) + (tTravel * tTravel * endPos);
                    }
                }

                Vector3 lookTarget = transform.position + transform.forward * stockpileLookTargetForwardOffset;
                Vector3 lookDir = lookTarget - weapon.WeaponObject.transform.position;
                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                    float tRot = PickupCurve.Evaluate(elapsed / PickupAnimationDuration);
                    weapon.WeaponObject.transform.rotation = Quaternion.Slerp(startRot, targetRot, tRot);
                }
                
                // Move VFX with weapon
                if (vfx != null)
                    vfx.transform.position = weapon.WeaponObject.transform.position;
                
                yield return null;
            }

            // Parent to boss for stockpile hover layout
            weapon.WeaponObject.transform.SetParent(transform);

            weapon.IsHeld = true;
            weapon.IsReturning = false;
            weapon.IsAtRest = false;
            if (!stockpiledWeapons.Contains(weapon))
                stockpiledWeapons.Add(weapon);
            UpdateStockpileLayoutImmediate();
            ReleaseCountersOnce();
            
            // Clean up VFX
            if (vfx != null)
                Destroy(vfx, 0.5f);

#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserDualWieldSystem), "[CleanserDualWield] Picked up spare weapon.");
#endif
        }

        public void DropCurrentWeapon()
        {
            ReleaseCurrentWeapon();
        }

        /// <summary>
        /// Releases the currently held spare weapon to magnetically return to its rest position.
        /// Called by CleanserBrain at the end of combos.
        /// </summary>
        public void ReleaseCurrentWeapon()
        {
            if (stockpiledWeapons.Count == 0)
                return;

            var weapon = stockpiledWeapons[0];
            stockpiledWeapons.RemoveAt(0);
            weapon.IsHeld = false;
            UpdateStockpileLayoutImmediate();

            ReturnWeaponToPool(weapon);

#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserDualWieldSystem), "[CleanserDualWield] Spare weapon released back to pool.");
#endif
        }

        /// <summary>
        /// Returns a pooled weapon to inactive state.
        /// </summary>
        public void StartMagneticReturn(SpareWeapon weapon)
        {
            if (weapon == null || weapon.WeaponObject == null)
                return;

            stockpiledWeapons.Remove(weapon);
            lodgedWeapons.Remove(weapon);
            ReturnWeaponToPool(weapon);
        }

        /// <summary>
        /// Launches all currently stockpiled weapons in an arc and lodges them into the ground.
        /// </summary>
        public IEnumerator LaunchStockpiledWeaponsToGround(Vector3 center)
        {
            if (stockpiledWeapons.Count == 0)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CleanserDualWieldSystem), "[CleanserDualWield] LaunchStockpiledWeaponsToGround called with empty stockpile.");
#endif
                Debug.LogWarning("[CleanserDualWield] Launch requested with empty stockpile.", this);
                yield break;
            }

            ResolveSpareTossVolleyReference();

            if (spareTossVolley == null)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CleanserDualWieldSystem), "[CleanserDualWield] SpareTossVolley reference is missing.");
#endif
                Debug.LogWarning("[CleanserDualWield] Launch requested but SpareTossVolley reference is missing.", this);
                yield break;
            }

            Debug.Log($"[CleanserDualWield] LaunchStockpiledWeaponsToGround begin. Stockpiled={stockpiledWeapons.Count}, Center={center}", this);
            PlaySFX(TossLaunchSFX);

            var weaponsToLaunch = new List<SpareWeapon>(stockpiledWeapons);
            stockpiledWeapons.Clear();
            yield return spareTossVolley.LaunchVolley(weaponsToLaunch, center, this);
            Debug.Log($"[CleanserDualWield] LaunchStockpiledWeaponsToGround end. Lodged={lodgedWeapons.Count}", this);

            UpdateStockpileLayoutImmediate();
        }

        private void ResolveSpareTossVolleyReference()
        {
            if (spareTossVolley != null)
                return;

            spareTossVolley = GetComponent<SpareTossVolley>();
            if (spareTossVolley == null)
                spareTossVolley = GetComponentInChildren<SpareTossVolley>(true);
            if (spareTossVolley == null)
                spareTossVolley = GetComponentInParent<SpareTossVolley>();

            if (spareTossVolley != null)
            {
                Debug.Log($"[CleanserDualWield] Resolved SpareTossVolley reference on '{spareTossVolley.gameObject.name}'.", this);
            }
        }

        public void SetSpareWeaponVisualPrefabs(List<GameObject> prefabs)
        {
            spareWeaponVisualPrefabs.Clear();
            if (prefabs != null)
                spareWeaponVisualPrefabs.AddRange(prefabs);

            EnsureBasePoolInitialized();
        }

        private int GetNextVisualPrefabIndex(List<GameObject> prefabs)
        {
            if (prefabs == null || prefabs.Count == 0)
                return -1;

            if (prefabs.Count == 1)
            {
                lastSelectedVisualPrefabIndex = 0;
                return 0;
            }

            int next = Random.Range(0, prefabs.Count);
            int guard = 0;
            while (next == lastSelectedVisualPrefabIndex && guard < 8)
            {
                next = Random.Range(0, prefabs.Count);
                guard++;
            }

            lastSelectedVisualPrefabIndex = next;
            return next;
        }

        private SpareWeapon AcquireWeaponFromPool(GameObject prefab)
        {
            if (prefab == null)
                return null;

            if (!inactivePoolByPrefab.TryGetValue(prefab, out Queue<SpareWeapon> queue))
            {
                queue = new Queue<SpareWeapon>();
                inactivePoolByPrefab[prefab] = queue;
            }

            SpareWeapon weapon = queue.Count > 0 ? queue.Dequeue() : CreatePooledWeapon(prefab);
            if (weapon == null || weapon.WeaponObject == null)
                return null;

            weapon.WeaponObject.SetActive(true);
            weapon.IsHeld = false;
            weapon.IsReturning = false;
            weapon.IsAtRest = false;
            weapon.HasStockpileOffsets = false;
            weapon.StockpileVerticalOffset = 0f;
            weapon.StockpileRollOffset = 0f;
            weapon.StockpileRollSpeed = 0f;
            weapon.StockpileFollowVelocity = Vector3.zero;
            return weapon;
        }

        private SpareWeapon CreatePooledWeapon(GameObject prefab)
        {
            if (prefab == null)
                return null;

            var go = Instantiate(prefab, transform.position, Quaternion.identity, transform);
            var weapon = new SpareWeapon
            {
                WeaponObject = go,
                SourcePrefab = prefab,
                IsAtRest = true,
                IsHeld = false,
                IsReturning = false
            };

            spareWeaponPool.Add(weapon);
            return weapon;
        }

        private void ReturnWeaponToPool(SpareWeapon weapon)
        {
            if (weapon == null || weapon.WeaponObject == null)
                return;

            if (weapon.SourcePrefab == null)
            {
                weapon.WeaponObject.SetActive(false);
                return;
            }

            if (!inactivePoolByPrefab.TryGetValue(weapon.SourcePrefab, out Queue<SpareWeapon> queue))
            {
                queue = new Queue<SpareWeapon>();
                inactivePoolByPrefab[weapon.SourcePrefab] = queue;
            }

            weapon.IsHeld = false;
            weapon.IsReturning = false;
            weapon.IsAtRest = true;
            weapon.HasStockpileOffsets = false;
            weapon.StockpileVerticalOffset = 0f;
            weapon.StockpileRollOffset = 0f;
            weapon.StockpileRollSpeed = 0f;
            weapon.StockpileFollowVelocity = Vector3.zero;
            weapon.WeaponObject.transform.SetParent(transform);
            weapon.WeaponObject.SetActive(false);

            bool alreadyQueued = false;
            if (queue.Count > 0)
            {
                foreach (var queued in queue)
                {
                    if (queued == weapon)
                    {
                        alreadyQueued = true;
                        break;
                    }
                }
            }

            if (!alreadyQueued)
                queue.Enqueue(weapon);
        }

        private Vector3 GetRandomSpawnPosition()
        {
            if (runtimeSpawnPoints != null && runtimeSpawnPoints.Count > 0)
            {
                int idx = Random.Range(0, runtimeSpawnPoints.Count);
                Transform spawn = runtimeSpawnPoints[idx];
                if (spawn != null)
                    return spawn.position;
            }

            return transform.position + Vector3.down * 6f;
        }

        public void RegisterWeaponLodged(SpareWeapon weapon)
        {
            if (weapon == null)
                return;

            if (!lodgedWeapons.Contains(weapon))
                lodgedWeapons.Add(weapon);
        }

        public void PlaySpareTossImpactSfx()
        {
            PlaySFX(TossImpactSFX);
        }

        public List<Vector3> GetLodgedWeaponPositions()
        {
            var points = new List<Vector3>(lodgedWeapons.Count);
            foreach (var weapon in lodgedWeapons)
            {
                if (weapon?.WeaponObject != null)
                    points.Add(weapon.WeaponObject.transform.position);
            }
            return points;
        }

        public void ConsumeClosestLodgedWeapon(Vector3 point, float maxDistance = 1.5f)
        {
            if (lodgedWeapons.Count == 0)
                return;

            int bestIndex = -1;
            float bestDist = float.MaxValue;
            float threshold = Mathf.Max(0.01f, maxDistance);
            for (int i = 0; i < lodgedWeapons.Count; i++)
            {
                SpareWeapon weapon = lodgedWeapons[i];
                if (weapon?.WeaponObject == null)
                    continue;

                float d = Vector3.Distance(point, weapon.WeaponObject.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestDist > threshold)
                return;

            SpareWeapon consumed = lodgedWeapons[bestIndex];
            lodgedWeapons.RemoveAt(bestIndex);
            ReturnWeaponToPool(consumed);
        }

        public void ReturnAllLodgedWeaponsToRest()
        {
            for (int i = lodgedWeapons.Count - 1; i >= 0; i--)
            {
                var weapon = lodgedWeapons[i];
                if (weapon == null)
                    continue;

                StartMagneticReturn(weapon);
            }
            lodgedWeapons.Clear();
        }

        /// <summary>
        /// Returns pooled weapon by index to inactive state.
        /// </summary>
        public void StartMagneticReturn(int weaponIndex)
        {
            if (weaponIndex < 0 || weaponIndex >= spareWeaponPool.Count)
                return;
                
            StartMagneticReturn(spareWeaponPool[weaponIndex]);
        }

        /// <summary>
        /// Gets a weapon by its GameObject (used by projectile system to find which weapon to return).
        /// </summary>
        public SpareWeapon GetWeaponByObject(GameObject weaponObj)
        {
            foreach (var weapon in spareWeaponPool)
            {
                if (weapon?.WeaponObject == weaponObj)
                    return weapon;
            }
            return null;
        }

        /// <summary>
        /// Gets a weapon by index.
        /// </summary>
        public SpareWeapon GetWeapon(int index)
        {
            if (index < 0 || index >= spareWeaponPool.Count)
                return null;
            return spareWeaponPool[index];
        }

        /// <summary>
        /// Resets all spare weapons to their initial state (for encounter reset).
        /// Stops any in-progress returns and snaps weapons to rest positions.
        /// </summary>
        public void ResetAllWeapons()
        {
            foreach (var weapon in spareWeaponPool)
            {
                if (weapon == null)
                    continue;

                ReturnWeaponToPool(weapon);
            }

            stockpiledWeapons.Clear();
            lodgedWeapons.Clear();
            activePickupAnimations = 0;
            pendingStockpileReservations = 0;
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserDualWieldSystem), "[CleanserDualWield] All weapons reset.");
#endif
        }

        private void PlaySFX(AudioClip clip)
        {
            if (clip == null)
                return;

            if (SFXSource != null)
            {
                SFXSource.PlayOneShot(clip);
            }
            else if (SoundManager.Instance != null)
            {
                SoundManager.Instance.sfxSource.PlayOneShot(clip);
            }
        }

        private void LateUpdate()
        {
            if (stockpiledWeapons.Count > 0)
            {
                UpdateStockpileLayoutImmediate();
            }
        }

        private void UpdateStockpileLayoutImmediate()
        {
            int count = stockpiledWeapons.Count;
            if (count == 0)
                return;

            for (int i = 0; i < count; i++)
            {
                var weapon = stockpiledWeapons[i];
                if (weapon?.WeaponObject == null)
                    continue;

                Transform t = weapon.WeaponObject.transform;
                if (t.parent != null)
                    t.SetParent(null, true);

                EnsureStockpileOffsets(weapon);
                Vector3 targetWorldPos = GetStockpileSlotWorldPosition(i, count, weapon.StockpileVerticalOffset);
                Quaternion baseRotation = Quaternion.Euler(stockpileLocalRotationEuler);
                float animatedRoll = weapon.StockpileRollOffset + (Time.time * weapon.StockpileRollSpeed);
                Quaternion targetWorldRot = transform.rotation * baseRotation * Quaternion.AngleAxis(animatedRoll, Vector3.forward);

                float smoothTime = Mathf.Max(0f, stockpileFollowSmoothTime);
                if (smoothTime <= 0f)
                {
                    t.position = targetWorldPos;
                }
                else
                {
                    t.position = Vector3.SmoothDamp(t.position, targetWorldPos, ref weapon.StockpileFollowVelocity, smoothTime);
                }

                Vector3 followVelocity = weapon.StockpileFollowVelocity;
                Vector3 planarVelocity = new Vector3(followVelocity.x, 0f, followVelocity.z);
                float planarSpeed = planarVelocity.magnitude;
                if (planarSpeed > 0.001f && stockpileFollowLeanMaxAngle > 0f)
                {
                    Vector3 moveDir = planarVelocity / planarSpeed;
                    Vector3 leanAxis = Vector3.Cross(Vector3.up, moveDir);
                    if (leanAxis.sqrMagnitude > 0.0001f)
                    {
                        float leanT = Mathf.Clamp01(planarSpeed / Mathf.Max(0.01f, stockpileFollowLeanSpeedForMax));
                        float leanAngle = stockpileFollowLeanMaxAngle * leanT;
                        targetWorldRot = Quaternion.AngleAxis(leanAngle, leanAxis.normalized) * targetWorldRot;
                    }
                }

                float rotFollow = Mathf.Max(0f, stockpileRotationFollowSpeed);
                if (rotFollow <= 0f)
                {
                    t.rotation = targetWorldRot;
                }
                else
                {
                    float tRot = 1f - Mathf.Exp(-rotFollow * Time.deltaTime);
                    t.rotation = Quaternion.Slerp(t.rotation, targetWorldRot, tRot);
                }
            }
        }

        private Vector3 GetStockpileSlotWorldPosition(int index)
        {
            return transform.TransformPoint(GetStockpileSlotLocalPosition(index, Mathf.Max(1, stockpiledWeapons.Count + 1)));
        }

        private Vector3 GetStockpileSlotWorldPosition(int index, int count)
        {
            return transform.TransformPoint(GetStockpileSlotLocalPosition(index, Mathf.Max(1, count)));
        }

        private Vector3 GetStockpileSlotWorldPosition(int index, int count, float verticalOffset)
        {
            return transform.TransformPoint(GetStockpileSlotLocalPosition(index, Mathf.Max(1, count), verticalOffset));
        }

        private int ReserveStockpileSlot(out int reservedCount)
        {
            int index = stockpiledWeapons.Count + pendingStockpileReservations;
            pendingStockpileReservations++;
            reservedCount = Mathf.Max(1, stockpiledWeapons.Count + pendingStockpileReservations);
            return index;
        }

        private Vector3 GetStockpileSlotLocalPosition(int index, int count, float verticalOffset = 0f)
        {
            float normalized = count <= 1 ? 0f : (index / (float)(count - 1) - 0.5f);
            float yaw = normalized * HoverYawSpread;
            Vector3 lateral = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * HoverSpacing * normalized * 2f;
            float centeredVertical = normalized * 2f * HoverVerticalStep;
            Vector3 vertical = Vector3.up * (centeredVertical + verticalOffset);
            return HoverAnchorLocal + lateral + vertical;
        }

        private void EnsureStockpileOffsets(SpareWeapon weapon)
        {
            if (weapon == null || weapon.HasStockpileOffsets)
                return;

            float minVertical = Mathf.Min(stockpileVerticalRandomOffsetRange.x, stockpileVerticalRandomOffsetRange.y);
            float maxVertical = Mathf.Max(stockpileVerticalRandomOffsetRange.x, stockpileVerticalRandomOffsetRange.y);
            float minRoll = Mathf.Min(stockpileRollRandomOffsetRange.x, stockpileRollRandomOffsetRange.y);
            float maxRoll = Mathf.Max(stockpileRollRandomOffsetRange.x, stockpileRollRandomOffsetRange.y);
            float minRollSpeed = Mathf.Min(stockpileRollSpeedRange.x, stockpileRollSpeedRange.y);
            float maxRollSpeed = Mathf.Max(stockpileRollSpeedRange.x, stockpileRollSpeedRange.y);

            weapon.StockpileVerticalOffset = Random.Range(minVertical, maxVertical);
            weapon.StockpileRollOffset = Random.Range(minRoll, maxRoll);
            weapon.StockpileRollSpeed = Random.Range(minRollSpeed, maxRollSpeed);
            weapon.HasStockpileOffsets = true;
        }

        private static float DistancePointToSegmentXZ(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector2 p = new Vector2(point.x, point.z);
            Vector2 v = new Vector2(a.x, a.z);
            Vector2 w = new Vector2(b.x, b.z);

            Vector2 vw = w - v;
            float lenSq = vw.sqrMagnitude;
            if (lenSq <= 0.000001f)
                return Vector2.Distance(p, v);

            float t = Mathf.Clamp01(Vector2.Dot(p - v, vw) / lenSq);
            Vector2 projection = v + (vw * t);
            return Vector2.Distance(p, projection);
        }

    }
}
