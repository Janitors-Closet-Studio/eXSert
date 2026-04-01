// ExplosiveEnemyProjectile.cs
// Purpose: Projectile that explodes on impact, applying area damage.
// Works with: EnemyProjectile, Damage system, Pooling systems (optional).

using UnityEngine;

public class ExplosiveEnemyProjectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 6f;
    [SerializeField] private float splashRadius = 4f;
    [SerializeField] private float damage = 20f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField, Tooltip("If enabled, explosive turret projectile splash force-staggers the player.")]
    private bool staggerPlayerOnSplash = true;
    [SerializeField, Range(0.05f, 2f), Tooltip("Forced stagger duration for player when hit by explosive splash.")]
    private float playerSplashStaggerDuration = 0.4f;

    [Header("Effects")]
    [SerializeField] private GameObject explosionVFXPrefab;
    [SerializeField] private AudioClip explosionSFX;
    [SerializeField] private float sfxVolume = 1f;

    private void OnEnable()
    {
        Invoke(nameof(SelfDestruct), lifetime);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(SelfDestruct));
    }

    private void OnCollisionEnter(Collision collision)
    {
        // If we directly hit the player, log it explicitly
        if (collision.collider != null && collision.collider.CompareTag("Player"))
        {
            EnemyBehaviorDebugLogBools.Log(nameof(ExplosiveEnemyProjectile), "[ExplosiveEnemyProjectile] Player directly hit by explosive projectile");
        }
        Explode();
    }

    private void SelfDestruct()
    {
        Explode();
    }

    private void Explode()
    {
        bool playerHit = false;

        // Simple AoE damage check
        var hits = Physics.OverlapSphere(transform.position, splashRadius, hitMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (!playerHit && h.CompareTag("Player"))
                playerHit = true;

            var hp = h.GetComponentInParent<IHealthSystem>();
            if (hp != null)
            {
                hp.LoseHP(damage);

                if (staggerPlayerOnSplash && hp is PlayerHealthBarManager playerHealth)
                    playerHealth.ApplyForcedStagger(playerSplashStaggerDuration, resetCombo: true);
            }
        }

        if (playerHit)
        {
            EnemyBehaviorDebugLogBools.Log(nameof(ExplosiveEnemyProjectile), "[ExplosiveEnemyProjectile] Player within explosive splash radius");
        }

        // Spawn VFX at explosion position
        if (explosionVFXPrefab != null)
        {
            Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
        }

        // Play explosion SFX
        if (explosionSFX != null)
        {
            AudioSource.PlayClipAtPoint(explosionSFX, transform.position, sfxVolume);
        }

        // Pool-friendly: reparent BEFORE deactivation if used by turrets
        var pooled = GetComponent<TurretPooledProjectile>();
        if (pooled != null)
        {
            pooled.ReparentToPoolAndDeactivate();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}