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
        
        [Tooltip("Transform where the weapon rests when not held (in the arena).")]
        public Transform RestPosition;
        
        [Tooltip("Legacy left-hand attach point (no longer used by current stockpile workflow).")]
        public Transform HandAttachPoint;
        
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
    /// Weapons return magnetically to their rest position instead of being destroyed.
    /// </summary>
    public class CleanserDualWieldSystem : MonoBehaviour
    {
        [Header("Spare Weapon Configuration")]
        [Tooltip("List of spare weapons placed in the arena that can be stockpiled.")]
        public List<SpareWeapon> SpareWeapons = new List<SpareWeapon>();

        [Header("Stockpile Hover")]
        [Tooltip("Local anchor around the Cleanser where stockpiled weapons hover.")]
        public Vector3 HoverAnchorLocal = new Vector3(0.8f, 1.8f, -0.2f);

        [Tooltip("Horizontal spacing between stockpiled hover weapons.")]
        public float HoverSpacing = 0.6f;

        [Tooltip("Vertical offset per stockpiled weapon index to reduce overlap.")]
        public float HoverVerticalStep = 0.12f;

        [Tooltip("How much yaw spread is applied across stockpiled weapons.")]
        public float HoverYawSpread = 30f;

        [Header("Acquire Animation")]
        [Tooltip("Duration of the magnetism/telekinesis acquire animation.")]
        public float PickupAnimationDuration = 0.5f;
        
        [Tooltip("Curve for the acquire motion (0 = rest position, 1 = stockpile hover slot).")]
        public AnimationCurve PickupCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Tooltip("VFX prefab to spawn during pickup (magnetism/telekinesis effect).")]
        public GameObject PickupVFXPrefab;
        
        [Tooltip("Audio clip to play during pickup.")]
        public AudioClip PickupSFX;

        [Header("Spare Toss (Weapon Rain)")]
        [Tooltip("Arc duration when launching stockpiled weapons into the ground.")]
        public float TossArcDuration = 0.7f;

        [Tooltip("Arc height for the toss trajectory.")]
        public float TossArcHeight = 4f;

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

        [Header("Magnetic Return Settings")]
        [Tooltip("Duration for the weapon to float back to its rest position.")]
        public float ReturnAnimationDuration = 1.5f;
        
        [Tooltip("Curve for the return motion (0 = current position, 1 = rest position).")]
        public AnimationCurve ReturnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Tooltip("Height offset for the arc during return (weapon floats up then down).")]
        public float ReturnArcHeight = 3f;
        
        [Tooltip("VFX prefab to spawn during magnetic return.")]
        public GameObject ReturnVFXPrefab;
        
        [Tooltip("Audio clip to play during magnetic return.")]
        public AudioClip ReturnSFX;
        
        [Tooltip("Spin speed during return (degrees per second).")]
        public float ReturnSpinSpeed = 360f;

        [Header("Drop/Release Settings (Legacy - Unused)")]
        [Tooltip("VFX prefab to spawn when a spare weapon shatters (legacy, unused).")]
        public GameObject ShatterVFXPrefab;
        
        [Tooltip("Audio clip to play when weapon shatters (legacy, unused).")]
        public AudioClip ShatterSFX;

        [Header("References")]
        [Tooltip("Audio source for SFX (uses SoundManager if null).")]
        public AudioSource SFXSource;
        [Tooltip("Optional dedicated spare-toss volley handler. Auto-found on Awake if left empty.")]
        [SerializeField] private SpareTossVolley spareTossVolley;

        // Runtime state
        private readonly List<SpareWeapon> stockpiledWeapons = new List<SpareWeapon>();
        private readonly List<SpareWeapon> lodgedWeapons = new List<SpareWeapon>();
        private Coroutine pickupCoroutine;
        private Dictionary<SpareWeapon, Coroutine> returnCoroutines = new Dictionary<SpareWeapon, Coroutine>();
        private bool isPickingUp;

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
                int count = 0;
                foreach (var weapon in SpareWeapons)
                {
                    if (weapon != null && weapon.IsAtRest && !weapon.IsHeld && !weapon.IsReturning)
                        count++;
                }
                return count;
            }
        }
        
        /// <summary>
        /// Returns true if any weapon is currently returning to its rest position.
        /// </summary>
        public bool IsAnyWeaponReturning
        {
            get
            {
                foreach (var weapon in SpareWeapons)
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

            // Initialize weapons to rest positions
            foreach (var weapon in SpareWeapons)
            {
                if (weapon?.WeaponObject != null)
                {
                    weapon.IsHeld = false;
                    weapon.IsReturning = false;
                    weapon.IsAtRest = true;
                    weapon.WeaponObject.SetActive(true); // Visible at rest position
                    
                    if (weapon.RestPosition != null)
                    {
                        weapon.WeaponObject.transform.position = weapon.RestPosition.position;
                        weapon.WeaponObject.transform.rotation = weapon.RestPosition.rotation;
                        weapon.WeaponObject.transform.SetParent(weapon.RestPosition);
                    }
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

            SpareWeapon target = GetNearestAvailableWeapon();
            if (target == null)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CleanserDualWieldSystem), "[CleanserDualWield] No spare weapons available for pickup.");
#endif
                return false;
            }

            if (pickupCoroutine != null)
                StopCoroutine(pickupCoroutine);
                
            pickupCoroutine = StartCoroutine(PickupWeaponCoroutine(target));
            return true;
        }

        /// <summary>
        /// Picks up a specific spare weapon by index.
        /// </summary>
        public bool PickupSpareWeapon(int index)
        {
            if (isPickingUp)
                return false;
                
            if (index < 0 || index >= SpareWeapons.Count)
                return false;
                
            var target = SpareWeapons[index];
            if (target == null || !target.IsAtRest || target.IsHeld || target.IsReturning)
                return false;

            if (pickupCoroutine != null)
                StopCoroutine(pickupCoroutine);
                
            pickupCoroutine = StartCoroutine(PickupWeaponCoroutine(target));
            return true;
        }

        private SpareWeapon GetNearestAvailableWeapon()
        {
            SpareWeapon nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var weapon in SpareWeapons)
            {
                // Only consider weapons that are at rest and not being held or returning
                if (weapon == null || !weapon.IsAtRest || weapon.IsHeld || weapon.IsReturning)
                    continue;
                    
                if (weapon.WeaponObject == null)
                    continue;

                float dist = Vector3.Distance(transform.position, weapon.WeaponObject.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = weapon;
                }
            }

            return nearest;
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
            while (elapsed < PickupAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = PickupCurve.Evaluate(elapsed / PickupAnimationDuration);
                
                Vector3 endPos = GetStockpileSlotWorldPosition(stockpiledWeapons.Count);
                weapon.WeaponObject.transform.position = Vector3.Lerp(startPos, endPos, t);
                weapon.WeaponObject.transform.rotation = Quaternion.Slerp(startRot, transform.rotation, t);
                
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

        /// <summary>
        /// Releases the currently held spare weapon to magnetically return to its rest position.
        /// Called by CleanserBrain at the end of combos (replaces shattering behavior).
        /// </summary>
        public void ReleaseCurrentWeapon()
        {
            if (stockpiledWeapons.Count == 0)
                return;

            var weapon = stockpiledWeapons[0];
            stockpiledWeapons.RemoveAt(0);
            weapon.IsHeld = false;
            UpdateStockpileLayoutImmediate();

            StartMagneticReturn(weapon);

#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserDualWieldSystem), "[CleanserDualWield] Spare weapon released for magnetic return.");
#endif
        }

        /// <summary>
        /// Legacy method - now calls ReleaseCurrentWeapon instead.
        /// Kept for backwards compatibility.
        /// </summary>
        public void ShatterCurrentWeapon()
        {
            // Redirect to release instead of shatter
            ReleaseCurrentWeapon();
        }

        /// <summary>
        /// Drops the currently held spare weapon immediately at its current position,
        /// then starts magnetic return. Use ReleaseCurrentWeapon for smoother release.
        /// </summary>
        public void DropCurrentWeapon()
        {
            if (stockpiledWeapons.Count == 0)
                return;

            var weapon = stockpiledWeapons[0];
            stockpiledWeapons.RemoveAt(0);
            weapon.IsHeld = false;
            UpdateStockpileLayoutImmediate();
            
            // Unparent from hand
            if (weapon.WeaponObject != null)
            {
                weapon.WeaponObject.transform.SetParent(null);
            }
            
            // Start magnetic return
            StartMagneticReturn(weapon);

#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserDualWieldSystem), "[CleanserDualWield] Spare weapon dropped, starting magnetic return.");
#endif
        }

        /// <summary>
        /// Starts the magnetic return animation for a weapon.
        /// Can be called on any weapon, not just the currently held one.
        /// </summary>
        public void StartMagneticReturn(SpareWeapon weapon)
        {
            if (weapon == null || weapon.WeaponObject == null || weapon.RestPosition == null)
                return;
                
            if (weapon.IsReturning)
                return; // Already returning
                
            // Cancel existing return coroutine if any
            if (returnCoroutines.TryGetValue(weapon, out var existingCoroutine) && existingCoroutine != null)
            {
                StopCoroutine(existingCoroutine);
            }
            
            // Unparent for smooth movement
            weapon.WeaponObject.transform.SetParent(null);
            
            var coroutine = StartCoroutine(MagneticReturnCoroutine(weapon));
            returnCoroutines[weapon] = coroutine;
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
        /// Starts magnetic return for a weapon by index (used by projectile system).
        /// </summary>
        public void StartMagneticReturn(int weaponIndex)
        {
            if (weaponIndex < 0 || weaponIndex >= SpareWeapons.Count)
                return;
                
            StartMagneticReturn(SpareWeapons[weaponIndex]);
        }

        private IEnumerator MagneticReturnCoroutine(SpareWeapon weapon)
        {
            weapon.IsReturning = true;
            weapon.IsAtRest = false;
            
            // Spawn VFX
            GameObject vfx = null;
            if (ReturnVFXPrefab != null && weapon.WeaponObject != null)
            {
                vfx = Instantiate(ReturnVFXPrefab, weapon.WeaponObject.transform.position, Quaternion.identity);
                vfx.transform.SetParent(weapon.WeaponObject.transform);
            }
            
            // Play SFX
            PlaySFX(ReturnSFX);
            
            Vector3 startPos = weapon.WeaponObject.transform.position;
            Quaternion startRot = weapon.WeaponObject.transform.rotation;
            Vector3 endPos = weapon.RestPosition.position;
            Quaternion endRot = weapon.RestPosition.rotation;
            
            // Calculate arc midpoint
            Vector3 midPoint = (startPos + endPos) * 0.5f + Vector3.up * ReturnArcHeight;
            
            float elapsed = 0f;
            while (elapsed < ReturnAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = ReturnCurve.Evaluate(elapsed / ReturnAnimationDuration);
                
                // Bezier curve for smooth arc
                Vector3 p0 = startPos;
                Vector3 p1 = midPoint;
                Vector3 p2 = endPos;
                
                // Quadratic bezier: B(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2
                float u = 1f - t;
                Vector3 pos = u * u * p0 + 2f * u * t * p1 + t * t * p2;
                
                weapon.WeaponObject.transform.position = pos;
                weapon.WeaponObject.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                
                // Spin during return
                weapon.WeaponObject.transform.Rotate(Vector3.forward, ReturnSpinSpeed * Time.deltaTime, Space.Self);
                
                // Move VFX with weapon
                if (vfx != null && !vfx.transform.IsChildOf(weapon.WeaponObject.transform))
                    vfx.transform.position = weapon.WeaponObject.transform.position;
                
                yield return null;
            }
            
            // Snap to final position
            weapon.WeaponObject.transform.position = endPos;
            weapon.WeaponObject.transform.rotation = endRot;
            weapon.WeaponObject.transform.SetParent(weapon.RestPosition);
            weapon.WeaponObject.transform.localPosition = Vector3.zero;
            weapon.WeaponObject.transform.localRotation = Quaternion.identity;
            
            weapon.IsReturning = false;
            weapon.IsAtRest = true;
            
            // Clean up VFX
            if (vfx != null)
                Destroy(vfx, 0.5f);
                
            // Remove from tracking dictionary
            returnCoroutines.Remove(weapon);

            lodgedWeapons.Remove(weapon);

#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserDualWieldSystem), "[CleanserDualWield] Weapon returned to rest position.");
#endif
        }

        /// <summary>
        /// Gets a weapon by its GameObject (used by projectile system to find which weapon to return).
        /// </summary>
        public SpareWeapon GetWeaponByObject(GameObject weaponObj)
        {
            foreach (var weapon in SpareWeapons)
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
            if (index < 0 || index >= SpareWeapons.Count)
                return null;
            return SpareWeapons[index];
        }

        /// <summary>
        /// Resets all spare weapons to their initial state (for encounter reset).
        /// Stops any in-progress returns and snaps weapons to rest positions.
        /// </summary>
        public void ResetAllWeapons()
        {
            // Stop all return coroutines
            foreach (var kvp in returnCoroutines)
            {
                if (kvp.Value != null)
                    StopCoroutine(kvp.Value);
            }
            returnCoroutines.Clear();
            
            foreach (var weapon in SpareWeapons)
            {
                if (weapon == null)
                    continue;
                    
                weapon.IsHeld = false;
                weapon.IsReturning = false;
                weapon.IsAtRest = true;
                
                if (weapon.WeaponObject != null)
                {
                    weapon.WeaponObject.SetActive(true);
                    
                    if (weapon.RestPosition != null)
                    {
                        weapon.WeaponObject.transform.SetParent(weapon.RestPosition);
                        weapon.WeaponObject.transform.localPosition = Vector3.zero;
                        weapon.WeaponObject.transform.localRotation = Quaternion.identity;
                    }
                }
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
