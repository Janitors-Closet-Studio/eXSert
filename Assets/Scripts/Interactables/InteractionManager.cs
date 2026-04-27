using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.SceneManagement;
using Utilities.Combat;

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
    [Tooltip("Enable verbose InteractionManager debug logs.")]
    [SerializeField] protected bool debugLogging = false;
    // Prevent prompt when player is attacking or dashing
    [Header("Interaction Blocking")]
    [SerializeField] protected bool blockPromptWhenAttackingOrDashing = false;
    public bool interactable = true;

    [Space(10)]
    [Header("Interaction Animation and ID")]
    [SerializeField] private AnimationClip _interactAnimation;
    [SerializeField, Min(0f)] private float _interactBusyFallbackDuration = 0.6f;
    [SerializeField] private string _interactId;
    [SerializeField] internal AudioClip _interactionSFX;
    [SerializeField] internal string _interactionPrompt = "Press to Interact";

    [Space(10)]
    [Header("Notice Settings")]
    [Tooltip("The name of the interaction/item shown in notices.")]
    [SerializeField] protected string displayName = "";
    [SerializeField] protected float uiDisplayDuration = 4f;
    [SerializeField] protected float uiFadeDuration = 2f;
    [SerializeField] protected string bottomFlavorText = "Press Pause to View";
    
    [Space(10)]
    [Header("Input Action Reference")]
    [SerializeField, CriticalReference] internal InputActionReference _interactInputAction;

    [Space(10)]
    [Header("Rumble Settings")]
    [SerializeField] private float _rumbleLowFrequency = 0.5f;
    [SerializeField] private float _rumbleHighFrequency = 0.5f;
    [SerializeField] private float _rumbleDuration = 0.5f;

    private PlayerCombatIdleController _combatIdleController;
    private PlayerAnimationController _playerAnimationController;
    private Coroutine _interactionBusyRoutine;
    private bool _interactionBusyOwned;

    internal MasterObjectiveClass masterObjective;

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

    private void Start()
    {
        masterObjective = MasterObjectiveClass.GetInstance(SceneAsset.GetSceneAssetOfObject(this.gameObject));
        if (masterObjective == null)
            Debug.LogWarning($"[InteractionManager] No MasterObjectiveClass instance found for {gameObject.name} in scene {SceneManager.GetActiveScene().name}. Notices will not show for this interaction.");
        StartCoroutine(FindPlayerScene("PlayerScene"));
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

        if (_interactionBusyRoutine != null)
        {
            StopCoroutine(_interactionBusyRoutine);
            _interactionBusyRoutine = null;
        }

        if (_interactionBusyOwned)
        {
            InputReader.inputBusy = false;
            _interactionBusyOwned = false;
        }

        InteractionUI interactionUI = GetInteractionUIIfAvailable();
        if (isPlayerNearby && interactionUI != null)
            interactionUI.HideInteractPrompt();

        isPlayerNearby = false;
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
        if (_combatIdleController != null && _combatIdleController.isActiveAndEnabled)
            return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _combatIdleController = player.GetComponentInChildren<PlayerCombatIdleController>(true);

        if (_combatIdleController == null)
            _combatIdleController = FindFirstObjectByType<PlayerCombatIdleController>();
    }

    private void CachePlayerAnimationController()
    {
        if (_playerAnimationController != null && _playerAnimationController.isActiveAndEnabled)
            return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerAnimationController = player.GetComponent<PlayerAnimationController>()
                ?? player.GetComponentInChildren<PlayerAnimationController>(true)
                ?? player.GetComponentInParent<PlayerAnimationController>();
        }

        if (_playerAnimationController == null)
            _playerAnimationController = FindFirstObjectByType<PlayerAnimationController>();
    }

    protected void PlayPlayerInteractAnimation()
    {
        CachePlayerAnimationController();
        _playerAnimationController?.PlayInteract();
        BeginInteractionBusyWindow();
    }

    private void BeginInteractionBusyWindow()
    {
        if (_interactionBusyRoutine != null)
            StopCoroutine(_interactionBusyRoutine);

        _interactionBusyRoutine = StartCoroutine(InteractionBusyWindowCoroutine());
    }

    private IEnumerator InteractionBusyWindowCoroutine()
    {
        bool alreadyBusy = InputReader.inputBusy;
        if (!alreadyBusy)
        {
            InputReader.inputBusy = true;
            _interactionBusyOwned = true;
        }
        else
        {
            _interactionBusyOwned = false;
        }

        float duration = _interactAnimation != null
            ? Mathf.Max(0f, _interactAnimation.length)
            : Mathf.Max(0f, _interactBusyFallbackDuration);

        if (duration > 0f)
            yield return new WaitForSecondsRealtime(duration);

        if (_interactionBusyOwned)
        {
            InputReader.inputBusy = false;
            _interactionBusyOwned = false;
        }

        _interactionBusyRoutine = null;
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
            if (interactionUI != null)
            {
                SwapBasedOnInputMethod();
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
        var interactionUI = GetInteractionUIIfAvailable();
        bool isCurrentInteractable = interactionUI != null && interactionUI.currentInteractable == this;

        if (interactionUI != null && interactionUI.currentInteractable != null && !isCurrentInteractable)
            return;

        if (!isCurrentInteractable && !isPlayerNearby)
            return;

        OnInteractButtonPressed();
    }

    public void OnInteractButtonPressed()
    {
        if (debugLogging) Debug.Log($"[InteractionManager] OnInteractButtonPressed called on {gameObject.name}");
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
            if (debugLogging) Debug.Log($"Interaction attempted with {gameObject.name}, but conditions not met. isPlayerNearby: {isPlayerNearby}, interactable: {interactable}, isDashing: {PlayerMovement.isDashingFlag}, isCurrent: {interactionUI?.currentInteractable == this}");
            // Only hide prompt if this is still the current interactable
            if (interactionUI != null && interactionUI.currentInteractable == this)
                interactionUI.HideInteractPrompt();
            return;
        }

        if (IsPlayerBusyForInteraction())
        {
            if (interactionUI != null && interactionUI.currentInteractable == this)
                interactionUI.HideInteractPrompt();
            return;
        }

        RumbleManager.Instance.RumblePulse(_rumbleLowFrequency, _rumbleHighFrequency, _rumbleDuration);

        if (debugLogging) Debug.Log($"Player interacted with {gameObject.name} using InputReader Interact.");
        if (Interact())
            PlayPlayerInteractAnimation();
    }

    private bool IsPlayerBusyForInteraction()
    {
        CachePlayerCombatController();

        if (PlayerMovement.isDashingFlag || CombatManager.isGuarding)
            return true;

        if (_combatIdleController != null && _combatIdleController.IsInCombat)
            return true;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return false;

        PlayerAttackManager attackManager = player.GetComponentInChildren<PlayerAttackManager>();
        return attackManager != null && attackManager.IsAttackInProgress;
    }

    protected abstract bool Interact();
    public void SwapBasedOnInputMethod()
    {
        InteractionUI interactionUI = GetInteractionUIIfAvailable();
        if (interactionUI == null)
            return;

        if (interactionUI._interactText != null)
        {
            string promptToShow = string.IsNullOrWhiteSpace(_interactionPrompt)
                ? "Press to Interact"
                : _interactionPrompt;

            interactionUI._interactText.text = promptToShow;
        }

        interactionUI.ShowInteractPromptImmediate();
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        // Only set isPlayerNearby if the collider belongs to the player character
        if (other.transform.root.CompareTag("Player"))
        {
            if (!interactable)
            {
                Debug.Log($"Player entered interaction zone of {gameObject.name}, but it's not interactable.");
                return;
            }

            if (debugLogging) Debug.Log($"[InteractionManager] Player entered interaction zone of {gameObject.name}. Setting isPlayerNearby true.");
            isPlayerNearby = true;

            CachePlayerCombatController();

            // Block prompt if attacking or dashing
            bool isAttacking = false;
            bool isDashing = false;
            bool isInCombatMode = _combatIdleController != null && _combatIdleController.IsInCombat;
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

            if (blockPromptWhenAttackingOrDashing && (isAttacking || isDashing || isInCombatMode))
            {
                if (debugLogging) Debug.Log($"[InteractionManager] Blocking prompt because player is attacking: {isAttacking}, dashing: {isDashing}, or in combat mode: {isInCombatMode}");
                return;
            }

            SwapBasedOnInputMethod();
            var interactionUI = GetInteractionUIIfAvailable();
            if (interactionUI == null)
                return;
            // Set this as the current interactable only if prompt is shown
            interactionUI.currentInteractable = this;
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
                interactionUI.currentInteractable = null;

            }
        }
    }

    protected virtual void OnTriggerStay(Collider other)
    {
        if (!other.transform.root.CompareTag("Player"))
            return;

        InteractionUI interactionUI = GetInteractionUIIfAvailable();
        if (interactionUI == null)
            return;

        if (IsPlayerBusyForInteraction())
        {
            if (interactionUI.currentInteractable == this)
                interactionUI.HideInteractPrompt();
            return;
        }

        if (!interactable || !isPlayerNearby)
            return;

        if (interactionUI.currentInteractable != null && interactionUI.currentInteractable != this)
            return;

        SwapBasedOnInputMethod();
        interactionUI.currentInteractable = this;
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
