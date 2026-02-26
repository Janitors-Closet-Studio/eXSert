// CleanserDualWieldSystem.cs
// Purpose: Manages dual-wield mechanics for the Cleanser boss.
// Works with: CleanserBrain, CleanserComboSystem
// Handles spare weapon pickup via magnetism/telekinesis effect.
// Note: The Cleanser ALWAYS holds his halberd in his right hand. This system manages
// the spare weapon he can pick up with his left hand (only ONE spare at a time).

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// Represents a spare weapon the Cleanser can pick up.
    /// These are pre-placed in the arena and picked up via magnetism/telekinesis.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?tab=t.0#bookmark=id.6fppdnkripgh")]
    [System.Serializable]
    public class SpareWeapon
    {
        [Tooltip("The GameObject representing this spare weapon.")]
        public GameObject WeaponObject;
        
        [Tooltip("Transform where the weapon rests when not held (in the arena).")]
        public Transform RestPosition;
        
        [Tooltip("Left hand bone/transform where the spare weapon attaches when held.")]
        public Transform HandAttachPoint;
        
        [Tooltip("If true, this weapon is currently held by the Cleanser.")]
        [HideInInspector] public bool IsHeld;
        
        [Tooltip("If true, this weapon has been destroyed/shattered.")]
        [HideInInspector] public bool IsDestroyed;
    }

    /// <summary>
    /// Manages the Cleanser's spare weapon pickup.
    /// The Cleanser always wields his halberd (right hand) - this system handles
    /// the optional spare weapon he can pick up (left hand). Only ONE spare can be held.
    /// Spare weapons shatter at the end of combos, not after a set number of attacks.
    /// </summary>
    public class CleanserDualWieldSystem : MonoBehaviour
    {
        [Header("Spare Weapon Configuration")]
        [Tooltip("List of spare weapons placed in the arena that can be picked up.")]
        public List<SpareWeapon> SpareWeapons = new List<SpareWeapon>();

        [Header("Pickup Animation")]
        [Tooltip("Duration of the magnetism/telekinesis pickup animation.")]
        public float PickupAnimationDuration = 0.5f;
        
        [Tooltip("Curve for the pickup motion (0 = rest position, 1 = left hand).")]
        public AnimationCurve PickupCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Tooltip("VFX prefab to spawn during pickup (magnetism/telekinesis effect).")]
        public GameObject PickupVFXPrefab;
        
        [Tooltip("Audio clip to play during pickup.")]
        public AudioClip PickupSFX;

        [Header("Weapon Shattering")]
        [Tooltip("VFX prefab to spawn when a spare weapon shatters.")]
        public GameObject ShatterVFXPrefab;
        
        [Tooltip("Audio clip to play when weapon shatters.")]
        public AudioClip ShatterSFX;

        [Header("References")]
        [Tooltip("Audio source for SFX (uses SoundManager if null).")]
        public AudioSource SFXSource;

        // Runtime state
        private SpareWeapon currentlyHeldSpare;
        private Coroutine pickupCoroutine;
        private bool isPickingUp;

        /// <summary>
        /// Returns true if the Cleanser is currently holding a spare weapon.
        /// </summary>
        public bool IsHoldingSpareWeapon => currentlyHeldSpare != null && currentlyHeldSpare.IsHeld;
        
        /// <summary>
        /// Returns true if a pickup animation is in progress.
        /// </summary>
        public bool IsPickingUp => isPickingUp;
        
        /// <summary>
        /// Returns the number of available (not destroyed) spare weapons.
        /// </summary>
        public int AvailableSpareWeaponCount
        {
            get
            {
                int count = 0;
                foreach (var weapon in SpareWeapons)
                {
                    if (weapon != null && !weapon.IsDestroyed && !weapon.IsHeld)
                        count++;
                }
                return count;
            }
        }

        private void Awake()
        {
            // Initialize weapons to rest positions and disabled state
            foreach (var weapon in SpareWeapons)
            {
                if (weapon?.WeaponObject != null)
                {
                    weapon.IsHeld = false;
                    weapon.IsDestroyed = false;
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
            if (isPickingUp || IsHoldingSpareWeapon)
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
            if (isPickingUp || IsHoldingSpareWeapon)
                return false;
                
            if (index < 0 || index >= SpareWeapons.Count)
                return false;
                
            var target = SpareWeapons[index];
            if (target == null || target.IsDestroyed || target.IsHeld)
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
                if (weapon == null || weapon.IsDestroyed || weapon.IsHeld)
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
            isPickingUp = true;
            
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
                
                if (weapon.HandAttachPoint != null)
                {
                    weapon.WeaponObject.transform.position = Vector3.Lerp(startPos, weapon.HandAttachPoint.position, t);
                    weapon.WeaponObject.transform.rotation = Quaternion.Slerp(startRot, weapon.HandAttachPoint.rotation, t);
                }
                
                // Move VFX with weapon
                if (vfx != null)
                    vfx.transform.position = weapon.WeaponObject.transform.position;
                
                yield return null;
            }

            // Parent to left hand
            if (weapon.HandAttachPoint != null)
            {
                weapon.WeaponObject.transform.SetParent(weapon.HandAttachPoint);
                weapon.WeaponObject.transform.localPosition = Vector3.zero;
                weapon.WeaponObject.transform.localRotation = Quaternion.identity;
            }

            weapon.IsHeld = true;
            currentlyHeldSpare = weapon;
            isPickingUp = false;
            
            // Clean up VFX
            if (vfx != null)
                Destroy(vfx, 0.5f);

#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserDualWieldSystem), "[CleanserDualWield] Picked up spare weapon.");
#endif
        }

        /// <summary>
        /// Shatters the currently held spare weapon.
        /// Called by CleanserBrain at the end of combos.
        /// </summary>
        public void ShatterCurrentWeapon()
        {
            if (!IsHoldingSpareWeapon)
                return;

            var weapon = currentlyHeldSpare;
            
            // Spawn shatter VFX
            if (ShatterVFXPrefab != null && weapon.WeaponObject != null)
            {
                Instantiate(ShatterVFXPrefab, weapon.WeaponObject.transform.position, weapon.WeaponObject.transform.rotation);
            }
            
            // Play shatter SFX
            PlaySFX(ShatterSFX);

            // Disable/destroy the weapon
            weapon.IsHeld = false;
            weapon.IsDestroyed = true;
            if (weapon.WeaponObject != null)
                weapon.WeaponObject.SetActive(false);
            
            currentlyHeldSpare = null;

#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserDualWieldSystem), "[CleanserDualWield] Spare weapon shattered.");
#endif
        }

        /// <summary>
        /// Drops the currently held spare weapon without destroying it.
        /// Returns it to its rest position in the arena.
        /// </summary>
        public void DropCurrentWeapon()
        {
            if (!IsHoldingSpareWeapon)
                return;

            var weapon = currentlyHeldSpare;
            
            // Return to rest position
            if (weapon.RestPosition != null && weapon.WeaponObject != null)
            {
                weapon.WeaponObject.transform.SetParent(weapon.RestPosition);
                weapon.WeaponObject.transform.localPosition = Vector3.zero;
                weapon.WeaponObject.transform.localRotation = Quaternion.identity;
            }
            
            weapon.IsHeld = false;
            currentlyHeldSpare = null;

#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserDualWieldSystem), "[CleanserDualWield] Spare weapon dropped.");
#endif
        }

        /// <summary>
        /// Resets all spare weapons to their initial state (for encounter reset).
        /// </summary>
        public void ResetAllWeapons()
        {
            foreach (var weapon in SpareWeapons)
            {
                if (weapon == null)
                    continue;
                    
                weapon.IsHeld = false;
                weapon.IsDestroyed = false;
                
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
            
            currentlyHeldSpare = null;
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
    }
}
