using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Prevents the player from standing on top of this object by applying a sliding force.
/// Attach to any enemy (or let BaseEnemy add it at runtime) to prevent softlocks caused by
/// the player getting stuck on top.
///
/// All tuning values can be driven globally from PlayerMovement's
/// "PlayerSlideOffSurface Global Overrides" section, which takes precedence over the
/// per-instance fields below when the override toggle is enabled.
/// </summary>
public class PlayerSlideOffSurface : MonoBehaviour
{
    [Header("Slide-Off Settings")]
    [Tooltip("Force applied to push the player off when standing on top. Higher values = faster slide.")]
    [SerializeField] private float slideForce = 4f;
    
    [Tooltip("Minimum vertical dot product to consider the player 'on top'. 1 = directly above, 0.5 = 45 degrees.")]
    [SerializeField, Range(0f, 1f)] private float minVerticalDot = 0.5f;
    
    [Tooltip("If true, disables this component (useful for enemies that should allow standing on top, like Roomba boss).")]
    [SerializeField] private bool disabled = false;
    
    private Collider[] enemyColliders;
    private bool ignoreForRoombaBoss;
    private Transform playerTransform;
    private Rigidbody playerRigidbody;
    private CharacterController playerCharacterController;
    private PlayerAttackManager playerAttackManager;
    private PlayerMovement playerMovement;
    private NavMeshAgent enemyNavAgent;
    private Rigidbody enemyRigidbody;
    
    private void Awake()
    {
        // Cache all colliders on this enemy
        enemyColliders = GetComponentsInChildren<Collider>();

        // Roomba boss should allow close/on-top interaction without slide-off push.
        ignoreForRoombaBoss = GetComponentInParent<EnemyBehavior.Boss.BossRoombaBrain>() != null;

        // Cache this enemy's own movement components for the pushback.
        enemyNavAgent  = GetComponentInParent<NavMeshAgent>() ?? GetComponent<NavMeshAgent>();
        enemyRigidbody = GetComponentInParent<Rigidbody>()   ?? GetComponent<Rigidbody>();
    }
    
    private void OnCollisionStay(Collision collision)
    {
        if (disabled || ignoreForRoombaBoss) return;
        
        // Check if it's the player
        if (!collision.gameObject.CompareTag("Player")) return;

        // Cache player components on first contact
        if (playerTransform == null || playerTransform.gameObject != collision.gameObject)
        {
            CachePlayerComponents(collision.gameObject);
        }

        // If the player is plunging, always allow the slide-off so the plunge descends through enemy tops.
        bool isPlunging = playerMovement != null && playerMovement.IsPlunging;

        // Don't push the player while they are attacking (but never block a plunge).
        if (!isPlunging && playerAttackManager != null && playerAttackManager.IsAttackInProgress)
            return;
        
        // Check if the player is on top of us
        if (!IsPlayerOnTop(collision)) return;
        
        // Apply slide force
        ApplySlideForce(collision);
    }
    
    private void CachePlayerComponents(GameObject playerObject)
    {
        playerTransform = playerObject.transform;
        playerRigidbody = playerObject.GetComponent<Rigidbody>();
        if (playerRigidbody == null)
        {
            playerRigidbody = playerObject.GetComponentInParent<Rigidbody>();
        }
        playerCharacterController = playerObject.GetComponent<CharacterController>();
        if (playerCharacterController == null)
        {
            playerCharacterController = playerObject.GetComponentInParent<CharacterController>();
        }
        playerAttackManager = playerObject.GetComponent<PlayerAttackManager>();
        if (playerAttackManager == null)
        {
            playerAttackManager = playerObject.GetComponentInParent<PlayerAttackManager>();
        }
        playerMovement = playerObject.GetComponent<PlayerMovement>();
        if (playerMovement == null)
        {
            playerMovement = playerObject.GetComponentInParent<PlayerMovement>();
        }
    }
    
    private bool IsPlayerOnTop(Collision collision)
    {
        // Pull thresholds from PlayerMovement's global override when active.
        PlayerMovement.SlideOffSurfaceConfig cfg = PlayerMovement.GlobalSlideOffConfig;
        float dotThreshold     = cfg.overrideEnabled ? cfg.minVerticalDot     : minVerticalDot;
        float looseMult        = cfg.overrideEnabled ? cfg.looseDotMultiplier : 0.65f;

        // Check contact normals to determine if player is standing on top.
        // We also check whether the player's feet are above the enemy's top bound as a
        // secondary confirmation so side-contact at near-top height still triggers the slide.
        bool playerFeetAboveEnemyTop = false;
        if (playerTransform != null && enemyColliders != null && enemyColliders.Length > 0)
        {
            float enemyTopY = float.MinValue;
            foreach (Collider col in enemyColliders)
            {
                if (col != null)
                    enemyTopY = Mathf.Max(enemyTopY, col.bounds.max.y);
            }
            // Use a CharacterController for accurate feet position, fall back to transform.
            float playerFeetY = playerCharacterController != null
                ? playerCharacterController.bounds.min.y
                : playerTransform.position.y;
            playerFeetAboveEnemyTop = playerFeetY >= enemyTopY - 0.25f;
        }

        foreach (ContactPoint contact in collision.contacts)
        {
            // Contact normal points from the enemy toward the player.
            // If the normal is pointing mostly upward, the player is on top.
            float verticalDot = Vector3.Dot(contact.normal, Vector3.up);
            if (verticalDot >= dotThreshold)
                return true;

            // Looser secondary check: shallower contact angle but player feet are clearly above enemy top.
            if (playerFeetAboveEnemyTop && verticalDot >= dotThreshold * looseMult)
                return true;
        }
        return false;
    }

    private void ApplySlideForce(Collision collision)
    {
        // Pull values from PlayerMovement's global override when active.
        PlayerMovement.SlideOffSurfaceConfig cfg = PlayerMovement.GlobalSlideOffConfig;
        float pushback = cfg.overrideEnabled ? cfg.enemyPushbackSpeed : 0f;

        // NOTE: the player-side push is intentionally NOT applied here.
        // PlayerMovement.GetEnemyTopSlideVelocity() already feeds a smooth per-frame
        // slide velocity into the CharacterController's single Move() call each update.
        // Calling CharacterController.Move() again from OnCollisionStay causes a second
        // physics displacement per frame — which is the source of the stutter/teleport.
        // This method now only handles the equal-and-opposite enemy pushback.

        if (pushback <= 0f)
            return;

        // Calculate slide direction — away from the enemy centre, horizontal only.
        Vector3 slideDirection = playerTransform.position - transform.position;
        slideDirection.y = 0f;

        // If the player is directly above, pick a random horizontal direction.
        if (slideDirection.sqrMagnitude < 0.01f)
        {
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            slideDirection = new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle));
        }

        slideDirection.Normalize();

        // --- Push the enemy in the opposite direction ---
        Vector3 enemyPushDir = -slideDirection;
        Vector3 enemyDelta   = enemyPushDir * (pushback * Time.fixedDeltaTime);

        if (enemyNavAgent != null && enemyNavAgent.enabled && enemyNavAgent.isOnNavMesh)
        {
            // Warp moves the agent on the NavMesh without triggering steering.
            enemyNavAgent.Warp(enemyNavAgent.transform.position + enemyDelta);
        }
        else if (enemyRigidbody != null && !enemyRigidbody.isKinematic)
        {
            enemyRigidbody.AddForce(enemyPushDir * pushback, ForceMode.Acceleration);
        }
        else
        {
            // Last resort: translate the root transform directly.
            transform.root.position += enemyDelta;
        }
    }
    
    /// <summary>
    /// Enable or disable the slide-off behavior at runtime.
    /// </summary>
    public void SetDisabled(bool isDisabled)
    {
        disabled = isDisabled;
    }
    
    /// <summary>
    /// Returns whether the slide-off behavior is currently disabled.
    /// </summary>
    public bool IsDisabled => disabled;
}
