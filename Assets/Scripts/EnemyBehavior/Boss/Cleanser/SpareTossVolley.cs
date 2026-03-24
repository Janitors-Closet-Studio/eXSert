using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities.Combat;

namespace EnemyBehavior.Boss.Cleanser
{
    public class SpareTossVolley : MonoBehaviour
    {
        [Header("Falling Damage")]
        [Tooltip("Damage dealt if a falling spare weapon passes through the player before lodging.")]
        [SerializeField] private float fallingDamage = 14f;
        [Tooltip("Hit radius for each falling spare weapon.")]
        [SerializeField] private float fallingHitRadius = 1.25f;
        [Tooltip("Damage multiplier while player is guarding against falling spare weapons.")]
        [SerializeField, Range(0f, 1f)] private float guardDamageMultiplier = 0.25f;

        private Transform player;

        public IEnumerator LaunchVolley(
            List<SpareWeapon> weaponsToLaunch,
            Vector3 center,
            CleanserDualWieldSystem owner)
        {
            if (weaponsToLaunch == null || weaponsToLaunch.Count == 0 || owner == null)
                yield break;

            CachePlayer();

            var usedLandingPositions = new List<Vector3>();
            var routines = new List<Coroutine>(weaponsToLaunch.Count);
            int completed = 0;

            for (int i = 0; i < weaponsToLaunch.Count; i++)
            {
                var weapon = weaponsToLaunch[i];
                if (weapon == null || weapon.WeaponObject == null)
                    continue;

                Vector3 landingPos = PickLandingPosition(center, usedLandingPositions, owner);
                usedLandingPositions.Add(landingPos);

                routines.Add(StartCoroutine(TossWeaponToGroundCoroutine(weapon, landingPos, owner, () => completed++)));
            }

            while (completed < routines.Count)
            {
                yield return null;
            }
        }

        private IEnumerator TossWeaponToGroundCoroutine(SpareWeapon weapon, Vector3 landingPos, CleanserDualWieldSystem owner, System.Action onComplete)
        {
            Transform wt = weapon.WeaponObject.transform;
            wt.SetParent(null);

            Vector3 startPos = wt.position;
            Vector3 control = (startPos + landingPos) * 0.5f + Vector3.up * owner.TossArcHeight;

            float elapsed = 0f;
            bool appliedFallingHit = false;
            while (elapsed < owner.TossArcDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / owner.TossArcDuration);
                float u = 1f - t;
                wt.position = u * u * startPos + 2f * u * t * control + t * t * landingPos;

                if (!appliedFallingHit)
                {
                    appliedFallingHit = TryApplyFallingHit(wt.position);
                }

                yield return null;
            }

            wt.position = landingPos;
            wt.rotation = Quaternion.Euler(owner.LodgedRotationEuler.x, Random.Range(0f, 360f), owner.LodgedRotationEuler.z);
            weapon.IsHeld = false;
            weapon.IsAtRest = false;
            weapon.IsReturning = false;

            owner.RegisterWeaponLodged(weapon);

            if (owner.TossImpactVFX != null)
                Instantiate(owner.TossImpactVFX, landingPos, Quaternion.identity);

            owner.PlaySpareTossImpactSfx();
            onComplete?.Invoke();
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

            health.LoseHP(damage);
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
            for (int i = 0; i < attempts; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(owner.LandingRadiusMin, owner.LandingRadiusMax);
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                candidate.y = owner.transform.position.y;

                bool overlaps = false;
                for (int j = 0; j < usedPositions.Count; j++)
                {
                    if (Vector3.Distance(candidate, usedPositions[j]) < owner.MinLandingSpacing)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                    return candidate;
            }

            Vector3 fallback = center + Random.onUnitSphere * owner.LandingRadiusMax;
            fallback.y = owner.transform.position.y;
            return fallback;
        }
    }
}
