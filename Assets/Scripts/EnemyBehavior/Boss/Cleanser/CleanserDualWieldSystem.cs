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

        [Header("Acquire Animation")]
        [Tooltip("Duration of the magnetism/telekinesis acquire animation.")]
        public float PickupAnimationDuration = 0.5f;
        
        [Tooltip("Curve for the acquire motion (0 = rest position, 1 = stockpile hover slot).")]
        public AnimationCurve PickupCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("How high a spare weapon rises straight up from its rest point before moving toward stockpile hover.")]
        public float PickupVerticalRiseHeight = 6f;

        [Tooltip("Portion of pickup duration spent on the initial straight-up rise.")]
        [Range(0.05f, 0.95f)] public float PickupVerticalRisePortion = 0.35f;
        
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
        private Coroutine pickupCoroutine;
        private readonly List<GameObject> spareWeaponVisualPrefabs = new List<GameObject>();
        private bool isPickingUp;
        private int lastSelectedVisualPrefabIndex = -1;
        private readonly List<Transform> runtimeSpawnPoints = new List<Transform>();

        /// <summary>
        /// Returns true if the Cleanser is currently holding a spare weapon.
        /// </summary>
        public bool IsHoldingSpareWeapon => stockpiledWeapons.Count > 0;
        
        /// <summary>
        /// Returns true if a pickup animation is in progress.
        /// </summary>
        public bool IsPickingUp => isPickingUp;
        
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
            spareTossVolley = spareTossVolley ?? GetComponent<SpareTossVolley>();

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
            if (isPickingUp)
                return false;

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
            if (isPickingUp)
                return false;

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

            if (pickupCoroutine != null)
                StopCoroutine(pickupCoroutine);
                
            pickupCoroutine = StartCoroutine(PickupWeaponCoroutine(target));
            return true;
        }

        private IEnumerator PickupWeaponCoroutine(SpareWeapon weapon)
        {
            if (weapon == null || weapon.WeaponObject == null)
                yield break;

            if (!weapon.WeaponObject.scene.IsValid())
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CleanserDualWieldSystem), "[CleanserDualWield] Pickup aborted: weapon reference points to a prefab asset instead of a scene instance.");
#endif
                yield break;
            }

            isPickingUp = true;
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

            while (elapsed < PickupAnimationDuration)
            {
                elapsed += Time.deltaTime;
                Vector3 endPos = GetStockpileSlotWorldPosition(stockpiledWeapons.Count);
                if (elapsed <= riseDuration)
                {
                    float tRise = PickupCurve.Evaluate(elapsed / riseDuration);
                    weapon.WeaponObject.transform.position = Vector3.Lerp(startPos, risePos, tRise);
                }
                else
                {
                    float tTravel = PickupCurve.Evaluate((elapsed - riseDuration) / travelDuration);
                    weapon.WeaponObject.transform.position = Vector3.Lerp(risePos, endPos, tTravel);
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
            isPickingUp = false;
            
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
                yield break;

            if (spareTossVolley == null)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CleanserDualWieldSystem), "[CleanserDualWield] SpareTossVolley reference is missing.");
#endif
                yield break;
            }

            PlaySFX(TossLaunchSFX);

            var weaponsToLaunch = new List<SpareWeapon>(stockpiledWeapons);
            stockpiledWeapons.Clear();
            yield return spareTossVolley.LaunchVolley(weaponsToLaunch, center, this);

            UpdateStockpileLayoutImmediate();
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
            isPickingUp = false;
            
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
                t.SetParent(transform);
                t.localPosition = GetStockpileSlotLocalPosition(i, count);
                t.localRotation = Quaternion.Euler(0f, i * (360f / Mathf.Max(1, count)), 0f);
            }
        }

        private Vector3 GetStockpileSlotWorldPosition(int index)
        {
            return transform.TransformPoint(GetStockpileSlotLocalPosition(index, Mathf.Max(1, stockpiledWeapons.Count + 1)));
        }

        private Vector3 GetStockpileSlotLocalPosition(int index, int count)
        {
            float normalized = count <= 1 ? 0f : (index / (float)(count - 1) - 0.5f);
            float yaw = normalized * HoverYawSpread;
            Vector3 lateral = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * HoverSpacing * normalized * 2f;
            Vector3 vertical = Vector3.up * (index * HoverVerticalStep);
            return HoverAnchorLocal + lateral + vertical;
        }

    }
}
