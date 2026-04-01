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
        [Tooltip("If enabled, falling spare-weapon hits force-stagger the player.")]
        [SerializeField] private bool staggerPlayerOnFallingHit = false;
        [Tooltip("Forced stagger duration applied to player by falling spare-weapon hits.")]
        [SerializeField, Range(0.05f, 2f)] private float fallingHitStaggerDuration = 0.4f;

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
            float launchHeight = Mathf.Max(0f, owner.TossLaunchHeight);
            float apexJitter = Mathf.Max(0f, owner.TossApexHorizontalRandom);
            Vector3 apex = startPos
                + Vector3.up * launchHeight
                + new Vector3(Random.Range(-apexJitter, apexJitter), 0f, Random.Range(-apexJitter, apexJitter));

            float totalDuration = Mathf.Max(0.05f, owner.TossArcDuration);
            float launchPortion = Mathf.Clamp(owner.TossLaunchPortion, 0.05f, 0.95f);
            float launchDuration = totalDuration * launchPortion;
            float rainDuration = Mathf.Max(0.01f, totalDuration - launchDuration);

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
            float minSpacing = Mathf.Max(0f, owner.MinLandingSpacing);

            for (int i = 0; i < attempts; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(owner.LandingRadiusMin, owner.LandingRadiusMax);
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                candidate.y = owner.transform.position.y;

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
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                candidate.y = owner.transform.position.y;

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
}
