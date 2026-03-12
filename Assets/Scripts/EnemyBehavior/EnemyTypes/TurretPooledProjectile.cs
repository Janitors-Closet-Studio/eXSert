using UnityEngine;

public class TurretPooledProjectile : MonoBehaviour
{
    [Header("Effects")]
    [SerializeField] private AudioClip impactSFX;
    [SerializeField] private float sfxVolume = 1f;

    private Transform returnParent;
    private Rigidbody rb;
    private bool hasCollided;

    public void InitReturnParent(Transform parent)
    {
        returnParent = parent;
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        hasCollided = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Skip if already handled (prevents multiple collision calls)
        if (hasCollided) return;

        // Skip if this projectile also has ExplosiveEnemyProjectile (it handles its own collision)
        if (GetComponent<ExplosiveEnemyProjectile>() != null) return;

        hasCollided = true;

        // Play impact SFX
        if (impactSFX != null)
        {
            AudioSource.PlayClipAtPoint(impactSFX, transform.position, sfxVolume);
        }

        ReparentToPoolAndDeactivate();
    }

    // Call this instead of SetActive(false) when you want to return to the pool
    public void ReparentToPoolAndDeactivate()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (returnParent != null)
        {
            // Reparent BEFORE deactivation to avoid the activation/deactivation error
            transform.SetParent(returnParent, false);
        }

        gameObject.SetActive(false);
    }

    // Do not reparent here; it's invoked during activation state change
    private void OnDisable()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}