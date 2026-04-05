using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider))]
public abstract class InteractionManager : MonoBehaviour, IInteractable
    

{
    // IInteractable implementation
    public string interactId { get => _interactId; set => _interactId = value; }
    public AnimationClip interactAnimation { get => _interactAnimation; set => _interactAnimation = value; }
    public bool showHitbox { get => _showHitbox; set => _showHitbox = value; }
    public bool isPlayerNearby { get; set; }

    [Header("Debugging")]
    [SerializeField] private bool _showHitbox;
    // Prevent prompt when player is attacking or dashing
    [Header("Interaction Blocking")]
    [SerializeField] protected bool blockPromptWhenAttackingOrDashing = true;
    internal bool interactable = true;

    [Space(10)]
    [Header("Interaction Animation and ID")]
    [SerializeField] private AnimationClip _interactAnimation;
    [SerializeField] private string _interactId;
    [SerializeField] internal AudioClip _interactionSFX;
    [SerializeField] private string _interactionPrompt = "Press to Interact";
    
    [Space(10)]
    [Header("Input Action Reference")]
    [SerializeField, CriticalReference] internal InputActionReference _interactInputAction;

    private PlayerCombatIdleController _combatIdleController;
    // Track input block owner for interaction
    private string _interactionInputBlockOwnerId;

    protected static InteractionUI GetInteractionUIIfAvailable()
    {
        return InteractionUI.TryGetExisting();
    }

    protected static AudioSource GetInteractionSfxSourceIfAvailable()
    {
        SoundManager soundManager = FindAnyObjectByType<SoundManager>();
        return soundManager != null ? soundManager.sfxSource : null;
    }

    protected virtual void Awake()
    {
        this.GetComponent<BoxCollider>().isTrigger = true;

        interactId = _interactId.Trim().ToLowerInvariant();
    }

    protected virtual void OnEnable()
    {
        if (_interactInputAction != null)
        {
            if (!_interactInputAction.action.enabled)
                _interactInputAction.action.Enable();
            _interactInputAction.action.performed += OnInteract;
        }
    }

    protected virtual void OnDisable()
    {
        if (_interactInputAction != null)
            _interactInputAction.action.performed -= OnInteract;

        InteractionUI interactionUI = GetInteractionUIIfAvailable();
        if (isPlayerNearby && interactionUI != null)
            interactionUI.HideInteractPrompt();

        isPlayerNearby = false;
    }

    private void Start()
    {
        StartCoroutine(FindPlayerScene("PlayerScene"));
    }

    private IEnumerator FindPlayerScene(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.isLoaded)
        {
            GetInteractionUIIfAvailable()?.HideInteractPrompt();
            CachePlayerCombatController();
        }
        else 
        {
            while (!scene.isLoaded)
            {
                yield return null; // Wait until the scene is loaded
            }
            CachePlayerCombatController();
            StopCoroutine(FindPlayerScene(sceneName)); // Stop the coroutine once the scene is loaded
        }
    }

    private void CachePlayerCombatController()
    {
        if (_combatIdleController == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _combatIdleController = player.GetComponentInChildren<PlayerCombatIdleController>();
        }
    }

    public void DeactivateInteractable(MonoBehaviour interactable)
    {
        if (interactable == null)
        {
            return;
        }

        // Disable interaction on the provided interactable object, not the manager itself.
        var collider = interactable.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        interactable.gameObject.SetActive(false);

        GetInteractionUIIfAvailable()?.HideInteractPrompt();
    }

    public virtual void SetInteractionEnabled(bool isEnabled)
    {
        interactable = isEnabled;
        var interactionUI = GetInteractionUIIfAvailable();
        // Only show prompt if enabled AND player is nearby
        if (isEnabled && isPlayerNearby)
        {
            SwapBasedOnInputMethod();
            if (interactionUI != null)
            {
                if (interactionUI._interactText != null)
                {
                    interactionUI._interactText.gameObject.SetActive(true);
                    if (interactionUI._interactText.transform.parent != null)
                        interactionUI._interactText.transform.parent.gameObject.SetActive(true);
                }
                if (interactionUI._interactIcon != null)
                    interactionUI._interactIcon.gameObject.SetActive(true);
                // Set currentInteractable if prompt is shown
                interactionUI.currentInteractable = this;
            }
        }
        else
        {
            // Only hide prompt if this is still the current interactable
            if (interactionUI != null && interactionUI.currentInteractable == this)
                interactionUI.HideInteractPrompt();
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        OnInteractButtonPressed();
    }

    public void OnInteractButtonPressed()
    {
        Debug.Log($"[InteractionManager] OnInteractButtonPressed called on {gameObject.name}");
        // Prevent interaction if gameplay input is blocked (e.g., during pause)
        if (InputReader.IsGameplayInputBlocked)
        {
            Debug.Log($"Interaction attempted with {gameObject.name}, but gameplay input is blocked.");
            return;
        }
        var interactionUI = GetInteractionUIIfAvailable();
        // Only allow if player is nearby, interactable, not dashing, and this is the current interactable (if set)
        if (!isPlayerNearby || !interactable || PlayerMovement.isDashingFlag || (interactionUI != null && interactionUI.currentInteractable != null && interactionUI.currentInteractable != this))
        {
            Debug.Log($"Interaction attempted with {gameObject.name}, but conditions not met. isPlayerNearby: {isPlayerNearby}, interactable: {interactable}, isDashing: {PlayerMovement.isDashingFlag}, isCurrent: {interactionUI?.currentInteractable == this}");
            // Only hide prompt if this is still the current interactable
            if (interactionUI != null && interactionUI.currentInteractable == this)
                interactionUI.HideInteractPrompt();
            return;
        }

        // Don't allow interactions while player is in combat
        if (_combatIdleController != null && _combatIdleController.IsInCombat)
            return;

        Debug.Log($"Player interacted with {gameObject.name} using InputReader Interact.");
        Interact();
    }

    protected abstract void Interact();
    public void SwapBasedOnInputMethod()
    {
        InteractionUI interactionUI = GetInteractionUIIfAvailable();
        if (interactionUI == null)
            return;

        if (interactionUI._interactText != null)
        {
            interactionUI._interactText.text = string.IsNullOrWhiteSpace(_interactionPrompt)
                ? "Press to Interact"
                : _interactionPrompt;
            interactionUI._interactText.gameObject.SetActive(true);
        }

        if (interactionUI._interactIcon != null)
            interactionUI._interactIcon.gameObject.SetActive(true);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        // Only set isPlayerNearby if the collider belongs to the player character
        if (other.transform.root.CompareTag("Player"))
        {
            Debug.Log($"[InteractionManager] Player entered interaction zone of {gameObject.name}. Setting isPlayerNearby true.");
            isPlayerNearby = true;

            // Block prompt if attacking or dashing
            bool isAttacking = false;
            bool isDashing = false;
            // Try to get PlayerCombatIdleController and PlayerMovement
            var player = other.transform.root.gameObject;
            var combatController = player.GetComponentInChildren<PlayerAttackManager>();
            if (combatController != null)
            {
                isAttacking = combatController.IsAttackInProgress;
            }
            var movement = player.GetComponentInChildren<PlayerMovement>();
            if (movement != null)
            {
                isDashing = PlayerMovement.isDashingFlag;
            }

            if (blockPromptWhenAttackingOrDashing && (isAttacking || isDashing))
            {
                Debug.Log($"[InteractionManager] Blocking prompt because player is attacking: {isAttacking} or dashing: {isDashing}");
                return;
            }

            SwapBasedOnInputMethod();
            var interactionUI = GetInteractionUIIfAvailable();
            if (interactionUI == null)
                return;
            // Set this as the current interactable only if prompt is shown
            interactionUI.currentInteractable = this;
            if (interactionUI._interactText != null && interactable)
            {
                interactionUI._interactText.gameObject.SetActive(true);
                if (interactionUI._interactText.transform.parent != null)
                    interactionUI._interactText.transform.parent.gameObject.SetActive(true);
            }
            if (interactionUI._interactIcon != null && interactable)
                interactionUI._interactIcon.gameObject.SetActive(true);
        }
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (other.transform.root.CompareTag("Player"))
        {
            isPlayerNearby = false;
            var interactionUI = GetInteractionUIIfAvailable();
            // Only clear currentInteractable if it matches this
            if (interactionUI != null && interactionUI.currentInteractable == this)
            {
                interactionUI.HideInteractPrompt();
            }
        }
    }

    private void OnDrawGizmos()
    {
        if(_showHitbox)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.green;
            BoxCollider box = GetComponent<BoxCollider>();
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
