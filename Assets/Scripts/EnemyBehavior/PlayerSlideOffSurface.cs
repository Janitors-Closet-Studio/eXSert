using UnityEngine;

/// <summary>
/// Prevents the player from standing on top of this object by applying a sliding force.
/// Attach to any enemy to prevent softlocks caused by the player getting stuck on top.
/// </summary>
public class PlayerSlideOffSurface : MonoBehaviour
{
    [Header("Slide-Off Settings")]
    [Tooltip("Force applied to push the player off when standing on top. Higher values = faster slide.")]
    [SerializeField] private float slideForce = 8f;
    
    [Tooltip("Minimum vertical dot product to consider the player 'on top'. 1 = directly above, 0.5 = 45 degrees.")]
    [SerializeField, Range(0f, 1f)] private float minVerticalDot = 0.5f;
    
    [Tooltip("If true, disables this component (useful for enemies that should allow standing on top, like Roomba boss).")]
    [SerializeField] private bool disabled = false;
    
    private Collider[] enemyColliders;
    private Transform playerTransform;
    private Rigidbody playerRigidbody;
    private CharacterController playerCharacterController;
    
    private void Awake()
    {
        // Cache all colliders on this enemy
        enemyColliders = GetComponentsInChildren<Collider>();
    }
    
    private void OnCollisionStay(Collision collision)
    {
        if (disabled) return;
        
        // Check if it's the player
        if (!collision.gameObject.CompareTag("Player")) return;
        
        // Cache player components on first contact
        if (playerTransform == null || playerTransform.gameObject != collision.gameObject)
        {
            CachePlayerComponents(collision.gameObject);
        }
        
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
    }
    
    private bool IsPlayerOnTop(Collision collision)
    {
        // Check contact normals to determine if player is standing on top
        foreach (ContactPoint contact in collision.contacts)
        {
            // Contact normal points from the enemy toward the player
            // If the normal is pointing mostly upward, the player is on top
            float verticalDot = Vector3.Dot(contact.normal, Vector3.up);
            if (verticalDot >= minVerticalDot)
            {
                return true;
            }
        }
        return false;
    }
    
    private void ApplySlideForce(Collision collision)
    {
        // Calculate slide direction - away from the center of the enemy, but horizontal only
        Vector3 slideDirection = playerTransform.position - transform.position;
        slideDirection.y = 0f;
        
        // If player is directly above, pick a random horizontal direction
        if (slideDirection.sqrMagnitude < 0.01f)
        {
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            slideDirection = new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle));
        }
        
        slideDirection.Normalize();
        
        // Apply the slide force based on what the player has
        Vector3 force = slideDirection * slideForce;
        
        if (playerCharacterController != null && playerCharacterController.enabled)
        {
            // For CharacterController, use Move
            playerCharacterController.Move(force * Time.fixedDeltaTime);
        }
        else if (playerRigidbody != null && !playerRigidbody.isKinematic)
        {
            // For Rigidbody, add force
            playerRigidbody.AddForce(force, ForceMode.Acceleration);
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
