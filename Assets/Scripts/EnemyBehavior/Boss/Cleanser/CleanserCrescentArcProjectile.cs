using UnityEngine;
using Utilities.Combat;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// Dedicated crescent-wave projectile used by Cleanser crescent attacks.
    /// Supports configurable parry/guard behavior and guard mitigation per spawned instance.
    /// </summary>
    public class CleanserCrescentArcProjectile : MonoBehaviour
    {
        [Header("Collision")]
        [Tooltip("Layer mask used to detect player collisions.")]
        [SerializeField] private LayerMask playerMask = ~0;
        [Tooltip("Optional world collision mask. Leave at 0 to disable world hit checks.")]
        [SerializeField] private LayerMask worldMask = 0;
        [Tooltip("Overlap radius used for hit detection.")]
        [SerializeField] private float hitRadius = 0.45f;
        [Tooltip("Safety lifetime to auto-destroy projectile if it does not hit anything.")]
        [SerializeField] private float maxLifetime = 5f;
        [Tooltip("If enabled, this projectile force-staggers player on hit.")]
        [SerializeField] private bool staggerPlayerOnHit = false;
        [Tooltip("Forced stagger duration for player when this projectile hits.")]
        [SerializeField, Range(0.05f, 2f)] private float playerHitStaggerDuration = 0.4f;

        private Vector3 moveDirection;
        private float speed;
        private float damage;
        private float maxDistance;
        private AttackCategory category;
        private bool canBeParried;
        private bool canBeGuarded;
        private float guardDamageMultiplier;
        private Vector3 startPos;
        private bool initialized;

        private static readonly Collider[] hitBuffer = new Collider[8];

        public void Initialize(
            Vector3 direction,
            float projectileDamage,
            float projectileSpeed,
            float projectileMaxDistance,
            AttackCategory damageCategory,
            bool allowParry,
            bool allowGuard,
            float guardMitigationMultiplier)
        {
            moveDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            damage = projectileDamage;
            speed = projectileSpeed;
            maxDistance = Mathf.Max(0.1f, projectileMaxDistance);
            category = damageCategory;
            canBeParried = allowParry;
            canBeGuarded = allowGuard;
            guardDamageMultiplier = Mathf.Clamp01(guardMitigationMultiplier);

            startPos = transform.position;
            transform.forward = moveDirection;
            initialized = true;

            if (maxLifetime > 0f)
            {
                Destroy(gameObject, maxLifetime);
            }
        }

        private void Update()
        {
            if (!initialized)
                return;

            transform.position += moveDirection * speed * Time.deltaTime;

            if (CheckWorldCollision())
            {
                Destroy(gameObject);
                return;
            }

            if (TryHitPlayer())
            {
                Destroy(gameObject);
                return;
            }

            if (Vector3.Distance(startPos, transform.position) >= maxDistance)
            {
                Destroy(gameObject);
            }
        }

        private bool CheckWorldCollision()
        {
            if (worldMask == 0)
                return false;

            return Physics.CheckSphere(transform.position, hitRadius, worldMask, QueryTriggerInteraction.Ignore);
        }

        private bool TryHitPlayer()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, hitRadius, hitBuffer, playerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = hitBuffer[i];
                if (hit == null)
                    continue;

                Transform hitRoot = hit.transform.root;
                bool isPlayer = hit.CompareTag("Player") || (hitRoot != null && hitRoot.CompareTag("Player"));
                if (!isPlayer)
                    continue;

                float finalDamage = damage;

                if (canBeParried && category == AttackCategory.Halberd)
                {
                    if (CombatManager.isParrying)
                    {
                        CombatManager.ParrySuccessful();
                        return true;
                    }
                }

                if (canBeGuarded && CombatManager.isGuarding)
                {
                    finalDamage *= guardDamageMultiplier;
                }

                if (hit.TryGetComponent<IHealthSystem>(out var health))
                {
                    health.LoseHP(finalDamage);
                    if (staggerPlayerOnHit && health is PlayerHealthBarManager playerHealth)
                        playerHealth.ApplyForcedStagger(playerHitStaggerDuration, resetCombo: true);
                }
                else
                {
                    var parentHealth = hit.GetComponentInParent<IHealthSystem>();
                    parentHealth?.LoseHP(finalDamage);
                    if (staggerPlayerOnHit && parentHealth is PlayerHealthBarManager parentPlayerHealth)
                        parentPlayerHealth.ApplyForcedStagger(playerHitStaggerDuration, resetCombo: true);
                }

                return true;
            }

            return false;
        }
    }
}
