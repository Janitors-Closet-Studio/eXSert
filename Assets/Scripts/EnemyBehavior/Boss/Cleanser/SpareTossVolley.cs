using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.AI;
using Utilities.Combat;

namespace EnemyBehavior.Boss.Cleanser
{
    public class SpareTossVolley : MonoBehaviour
    {
        [Header("Launch Stagger")]
        [Tooltip("Minimum delay in seconds between each weapon's launch start. Creates a sequential rain effect instead of all weapons launching at exactly the same time.")]
        [SerializeField, Min(0f)] private float tossLaunchDelayPerWeapon = 0.05f;

        [Header("Falling Damage")]
        [Tooltip("Damage dealt if a falling spare weapon passes through the player before lodging.")]
        [SerializeField] private float fallingDamage = 14f;
        [Tooltip("Hit radius for each falling spare weapon.")]
        [SerializeField] private float fallingHitRadius = 1.25f;
        [Tooltip("Damage multiplier while player is guarding against falling spare weapons.")]
        [SerializeField, Range(0f, 1f)] private float guardDamageMultiplier = 0.25f;
        [Tooltip("If enabled, falling spare-weapon hits force-stagger the player.")]
        [SerializeField] private bool staggerPlayerOnFallingHit = false;
        [Tooltip("Forced stagger duration applied to player by falling spare-weapon hits.")]
        [SerializeField, Range(0.05f, 2f)] private float fallingHitStaggerDuration = 0.4f;

        [Header("Rumble")]
        [SerializeField, Tooltip("Duration of controller rumble when hit by falling spare weapon.")]
        private float rumbleDuration = 0.15f;
        [SerializeField, Tooltip("Low frequency intensity of rumble (0-1) when hit by falling spare weapon.")]
        private float rumbleLowFrequency = 0.35f;
        [SerializeField, Tooltip("High frequency intensity of rumble (0-1) when hit by falling spare weapon.")]
        private float rumbleHighFrequency = 0.35f;

        private Transform player;

        public IEnumerator LaunchVolley(
            List<SpareWeapon> weaponsToLaunch,
            Vector3 center,
            CleanserDualWieldSystem owner)
        {
            if (weaponsToLaunch == null || weaponsToLaunch.Count == 0 || owner == null)
            {
                Debug.LogWarning("[SpareTossVolley] LaunchVolley aborted due to invalid input (null/empty weapons or null owner).", this);
                yield break;
            }

            Debug.Log($"[SpareTossVolley] LaunchVolley begin. Requested={weaponsToLaunch.Count}, Center={center}", this);

            CachePlayer();

            var usedLandingPositions = new List<Vector3>();
            var routines = new List<Coroutine>(weaponsToLaunch.Count);
            int completed = 0;

            for (int i = 0; i < weaponsToLaunch.Count; i++)
            {
                var weapon = weaponsToLaunch[i];
                if (weapon == null || weapon.WeaponObject == null)
                {
                    Debug.LogWarning($"[SpareTossVolley] Skipping invalid weapon at index {i}.", this);
                    continue;
                }

                Vector3 landingPos = PickLandingPosition(center, usedLandingPositions, owner);
                usedLandingPositions.Add(landingPos);

                float launchStartDelay = i * Mathf.Max(0f, tossLaunchDelayPerWeapon);
                routines.Add(StartCoroutine(TossWeaponToGroundCoroutine(weapon, landingPos, owner, launchStartDelay, () => completed++)));
            }

            Debug.Log($"[SpareTossVolley] Launch routines started={routines.Count}.", this);

            while (completed < routines.Count)
            {
                yield return null;
            }

            Debug.Log($"[SpareTossVolley] LaunchVolley complete. Completed={completed}.", this);
        }

        private IEnumerator TossWeaponToGroundCoroutine(SpareWeapon weapon, Vector3 landingPos, CleanserDualWieldSystem owner, float startDelay, System.Action onComplete)
        {
            // Stagger launch start so weapons rain down sequentially rather than all at once.
            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            Transform wt = weapon.WeaponObject.transform;
            wt.SetParent(null);
            owner.SetWeaponControlledVfxActive(weapon, true);

            // Enable child trigger colliders and arm the hit relay for damage-on-contact.
            EnableFallingColliders(wt, true);

            Vector3 startPos = wt.position;
            float launchHeight = Mathf.Max(0f, owner.TossLaunchHeight);
            float apexJitter = Mathf.Max(0f, owner.TossApexHorizontalRandom);
            Vector3 apex = startPos
                + Vector3.up * launchHeight
                + new Vector3(Random.Range(-apexJitter, apexJitter), 0f, Random.Range(-apexJitter, apexJitter));

            float totalDuration = Mathf.Max(0.05f, owner.TossArcDuration);
            float launchPortion = Mathf.Clamp(owner.TossLaunchPortion, 0.05f, 0.95f);
            float launchDuration = totalDuration * launchPortion;
            float rainDuration = Mathf.Max(0.01f, totalDuration - launchDuration);

            SpawnWarningZone(landingPos, totalDuration, owner);

            float elapsed = 0f;
            bool appliedFallingHit = false;
            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;

                if (elapsed <= launchDuration)
                {
                    float tUp = Mathf.Clamp01(elapsed / launchDuration);
                    wt.position = Vector3.Lerp(startPos, apex, tUp);
                }
                else
                {
                    float tDown = Mathf.Clamp01((elapsed - launchDuration) / rainDuration);
                    float easedDown = tDown * tDown;
                    wt.position = Vector3.Lerp(apex, landingPos, easedDown);

                    Vector3 descendDir = (landingPos - wt.position).normalized;
                    if (descendDir.sqrMagnitude > 0.0001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(descendDir, Vector3.up) * Quaternion.Euler(owner.LodgedRotationEuler);
                        wt.rotation = Quaternion.Slerp(wt.rotation, targetRot, Time.deltaTime * 20f);
                    }
                }

                if (!appliedFallingHit)
                {
                    appliedFallingHit = TryApplyFallingHit(wt.position);
                }

                yield return null;
            }

            wt.position = landingPos;
            Quaternion bladeDown = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            Quaternion randomYaw = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            wt.rotation = randomYaw * bladeDown * Quaternion.Euler(owner.LodgedRotationEuler);
            weapon.IsHeld = false;
            weapon.IsAtRest = false;
            weapon.IsReturning = false;
            owner.SetWeaponControlledVfxActive(weapon, false);

            // Disable child trigger colliders — weapon is lodged, no longer deals falling damage.
            EnableFallingColliders(wt, false);

            owner.RegisterWeaponLodged(weapon);

            if (owner.TossImpactVFX != null)
            {
                Vector3 impactVfxPosition = landingPos + Vector3.up * owner.TossImpactVfxHeightOffset;
                Instantiate(owner.TossImpactVFX, impactVfxPosition, Quaternion.identity);
            }

            // Play the impact SFX from the weapon's own position so each landing
            // is heard as a distinct 3D sound at the correct world location.
            AudioClip impactClip = owner.GetRandomTossImpactClip();
            if (impactClip != null && weapon.WeaponObject != null)
            {
                AudioSource weaponAudio = weapon.WeaponObject.GetComponent<AudioSource>();
                if (weaponAudio == null)
                    weaponAudio = weapon.WeaponObject.AddComponent<AudioSource>();

                weaponAudio.spatialBlend = 1f;
                weaponAudio.rolloffMode = AudioRolloffMode.Linear;
                weaponAudio.minDistance = 2f;
                weaponAudio.maxDistance = 35f;
                weaponAudio.PlayOneShot(impactClip, owner.TossImpactSFXVolume);
            }

            onComplete?.Invoke();
        }

        private void SpawnWarningZone(Vector3 landingPos, float warningLifetime, CleanserDualWieldSystem owner)
        {
            if (owner == null || owner.TossWarningZoneVfx == null)
                return;

            float zoneStartSize = Mathf.Max(0f, (fallingHitRadius * 4f) + owner.TossWarningZoneSizePadding);
            float lifetime = Mathf.Max(0.05f, warningLifetime);
            Vector3 warningPosition = new Vector3(landingPos.x, owner.TossWarningZoneWorldY, landingPos.z);
            GameObject warningInstance = Instantiate(owner.TossWarningZoneVfx, warningPosition, Quaternion.identity);

            ConfigureWarningZone(warningInstance, zoneStartSize, lifetime, owner);
            Destroy(warningInstance, lifetime + 0.5f);
        }

        private static void ConfigureWarningZone(GameObject warningInstance, float zoneStartSize, float lifetime, CleanserDualWieldSystem owner)
        {
            if (warningInstance == null || owner == null)
                return;

            ParticleSystem[] particleSystems = warningInstance.GetComponentsInChildren<ParticleSystem>(true);
            if (particleSystems == null || particleSystems.Length == 0)
                return;

            float growingStartSize = Mathf.Max(
                0f,
                (zoneStartSize * owner.TossWarningZoneGrowingScaleMultiplier) + owner.TossWarningZoneGrowingScaleOffset);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem particleSystem = particleSystems[index];
                if (particleSystem == null)
                    continue;

                var main = particleSystem.main;
                main.startLifetime = lifetime;

                bool isGrowingZone = particleSystem.gameObject.name.IndexOf("GrowingOne", System.StringComparison.OrdinalIgnoreCase) >= 0;
                main.startSize = isGrowingZone ? growingStartSize : zoneStartSize;

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Clear(true);
                particleSystem.Simulate(0f, true, true, true);
                particleSystem.Play(true);
            }
        }

        /// <summary>
        /// Enables or disables all trigger colliders on child GameObjects of the weapon root,
        /// and initialises/disarms the CleanserFallingWeaponHitRelay on each.
        /// </summary>
        private void EnableFallingColliders(Transform weaponRoot, bool enable)
        {
            if (weaponRoot == null) return;

            Collider[] colliders = weaponRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null || !col.isTrigger) continue;

                if (enable)
                {
                    // Ensure relay exists and is initialised.
                    CleanserFallingWeaponHitRelay relay = col.GetComponent<CleanserFallingWeaponHitRelay>();
                    if (relay == null)
                        relay = col.gameObject.AddComponent<CleanserFallingWeaponHitRelay>();

                    relay.Initialize(fallingDamage, guardDamageMultiplier, staggerPlayerOnFallingHit, fallingHitStaggerDuration);
                    col.enabled = true;
                }
                else
                {
                    col.enabled = false;
                }
            }
        }

        private bool TryApplyFallingHit(Vector3 weaponPos)
        {
            if (player == null)
                return false;

            if (Vector3.Distance(weaponPos, player.position) > fallingHitRadius)
                return false;

            if (!player.TryGetComponent<IHealthSystem>(out var health))
                return false;

            float damage = fallingDamage;
            if (CombatManager.isGuarding)
            {
                damage *= guardDamageMultiplier;
            }

            health.LoseHP(damage, rumbleDuration, rumbleLowFrequency, rumbleHighFrequency);

            if (staggerPlayerOnFallingHit && health is PlayerHealthBarManager playerHealth)
                playerHealth.ApplyForcedStagger(fallingHitStaggerDuration, resetCombo: true);

            return true;
        }

        private void CachePlayer()
        {
            if (PlayerPresenceManager.IsPlayerPresent)
            {
                player = PlayerPresenceManager.PlayerTransform;
                return;
            }

            if (player == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                    player = playerObj.transform;
            }
        }

        private Vector3 PickLandingPosition(Vector3 center, List<Vector3> usedPositions, CleanserDualWieldSystem owner)
        {
            const int attempts = 24;
            // Generous radius so the sample succeeds even when the nav mesh surface Y
            // differs from baseY (uneven terrain, baked-mesh offsets, etc.).
            const float navSampleRadius = 6f;
            float minSpacing = Mathf.Max(0f, owner.MinLandingSpacing);
            float baseY = owner.transform.position.y + owner.LodgedHeightOffset;

            for (int i = 0; i < attempts; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(owner.LandingRadiusMin, owner.LandingRadiusMax);
                Vector3 raw = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                raw.y = baseY;

                // Snap to nav mesh if possible; fall back to raw position so spacing still works.
                Vector3 candidate = NavMesh.SamplePosition(raw, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas)
                    ? new Vector3(hit.position.x, baseY, hit.position.z)
                    : raw;

                if (GetMinDistanceToUsed(candidate, usedPositions) >= minSpacing)
                    return candidate;
            }

            // Dense fallback: choose the point with highest separation from existing landings.
            Vector3 best = center;
            float bestMinDistance = -1f;
            for (int i = 0; i < 64; i++)
            {
                float angle = (i / 64f) * Mathf.PI * 2f;
                float radius = owner.LandingRadiusMax;
                Vector3 raw = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                raw.y = baseY;

                Vector3 candidate = NavMesh.SamplePosition(raw, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas)
                    ? new Vector3(hit.position.x, baseY, hit.position.z)
                    : raw;

                float minDist = GetMinDistanceToUsed(candidate, usedPositions);
                if (minDist > bestMinDistance)
                {
                    bestMinDistance = minDist;
                    best = candidate;
                }
            }

            return best;
        }

        private float GetMinDistanceToUsed(Vector3 candidate, List<Vector3> usedPositions)
        {
            if (usedPositions == null || usedPositions.Count == 0)
                return float.MaxValue;

            float minDist = float.MaxValue;
            for (int j = 0; j < usedPositions.Count; j++)
            {
                float d = Vector3.Distance(candidate, usedPositions[j]);
                if (d < minDist)
                    minDist = d;
            }

            return minDist;
        }
    }

    public class CleanserFallingWeaponHitRelay : MonoBehaviour
    {
        private float damage;
        private float guardDamageMultiplier = 0.25f;
        private bool staggerOnHit;
        private float staggerDuration = 0.4f;
        private bool hasHit;
        private Collider ownCollider;

        private void Awake()
        {
            ownCollider = GetComponent<Collider>();
        }

        public void Initialize(float dmg, float guardMult, bool stagger, float staggerDur)
        {
            damage = dmg;
            guardDamageMultiplier = guardMult;
            staggerOnHit = stagger;
            staggerDuration = staggerDur;
            hasHit = false;
        }

        public void ResetForReuse()
        {
            hasHit = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasHit) return;
            if (ownCollider == null || !ownCollider.enabled) return;

            bool isPlayer = other.CompareTag("Player");
            if (!isPlayer)
            {
                Transform check = other.transform;
                while (check != null)
                {
                    if (check.CompareTag("Player")) { isPlayer = true; break; }
                    check = check.parent;
                }
            }
            if (!isPlayer) return;

            IHealthSystem health = other.GetComponent<IHealthSystem>()
                ?? other.GetComponentInParent<IHealthSystem>();
            if (health == null) return;

            hasHit = true;
            ownCollider.enabled = false;

            float finalDamage = CombatManager.isGuarding ? damage * guardDamageMultiplier : damage;
            health.LoseHP(finalDamage, 0.15f, 0.35f, 0.35f);

            if (staggerOnHit && health is PlayerHealthBarManager playerHealth)
                playerHealth.ApplyForcedStagger(staggerDuration, resetCombo: true);
        }
    }
}
