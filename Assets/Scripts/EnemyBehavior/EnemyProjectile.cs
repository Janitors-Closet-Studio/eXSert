// EnemyProjectile.cs
// Purpose: Base projectile logic for enemy-fired projectiles, handles movement and collision damage.
// Works with: Turret weapons, ExplosiveEnemyProjectile, player hit detection.

using UnityEngine;
using System.Collections;

public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 5f; // seconds
    [SerializeField] private float damage = 10f;  // default damage, can be set on spawn
    [SerializeField, Tooltip("Optional: additional layers that should receive projectile damage.")]
    private LayerMask damageLayers = 0;
    [SerializeField, Tooltip("Tag that represents the player. Used as a fallback if layer masks are broad.")]
    private string playerTag = "Player";

    // Optional: owner for drone-side pooling
    private DroneEnemy owner;

    private Coroutine lifeRoutine;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        // Start lifetime timer
        lifeRoutine = StartCoroutine(DeactivateAfterLifetime());
    }

    private void OnDisable()
    {
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }

        // Reset physics for pooling reuse
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private IEnumerator DeactivateAfterLifetime()
    {
        yield return new WaitForSeconds(lifetime);
        DeactivateToPool();
    }

    // Handle physics collisions
    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider);
    }

    // Handle trigger hits too (in case player's collider is trigger)
    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    private void HandleHit(Collider col)
    {
        if (col == null)
            return;

        bool matchesTag = col.CompareTag(playerTag);
        bool matchesLayer = damageLayers != 0 && IsDamageLayer(col.gameObject.layer);
        if (!matchesTag && !matchesLayer)
            return;

        if (TryApplyDamage(col))
        {
            EnemyBehaviorDebugLogBools.Log(nameof(EnemyProjectile), $"[EnemyProjectile] Applied {damage} damage to {col.name}");
            DeactivateToPool();
        }
    }

    private bool TryApplyDamage(Collider col)
    {
        if (col.TryGetComponent<IHealthSystem>(out var healthSystem))
        {
            if (healthSystem is PlayerHealthBarManager playerHealth)
            {
                playerHealth.SuppressNextFlinch();
            }
            healthSystem.LoseHP(damage);
            return true;
        }

        var healthParent = col.GetComponentInParent<IHealthSystem>();
        if (healthParent != null)
        {
            if (healthParent is PlayerHealthBarManager parentPlayerHealth)
            {
                parentPlayerHealth.SuppressNextFlinch();
            }
            healthParent.LoseHP(damage);
            return true;
        }

        if (col.CompareTag(playerTag) && PlayerHealthBarManager.Instance != null)
        {
            PlayerHealthBarManager.Instance.SuppressNextFlinch();
            PlayerHealthBarManager.Instance.LoseHP(damage);
            return true;
        }

        return false;
    }

    private bool IsDamageLayer(int layer)
    {
        return (damageLayers.value & (1 << layer)) != 0;
    }

    private void DeactivateToPool()
    {
        // Prefer turret pooling helper if present
        var pooled = GetComponent<TurretPooledProjectile>();
        if (pooled != null)
        {
            pooled.ReparentToPoolAndDeactivate();
            return;
        }

        // Fallback: drone-side pooling
        if (owner != null)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            owner.ReturnProjectileToPool(gameObject);
            return;
        }

        // Last resort: just deactivate
        gameObject.SetActive(false);
    }

    public void SetDamage(float dmg) => damage = dmg;

    // For drone pooling
    public void SetOwner(DroneEnemy drone) => owner = drone;
}