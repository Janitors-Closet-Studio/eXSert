using UnityEngine;
using UnityEngine.InputSystem;
using System;
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

    protected static InteractionUI GetInteractionUIIfAvailable()
    {
        return FindAnyObjectByType<InteractionUI>();
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

    private void OnEnable()
    {
        if (_interactInputAction != null)
        {
            if (!_interactInputAction.action.enabled)
                _interactInputAction.action.Enable();
            _interactInputAction.action.performed += OnInteract;
        }
    }

    private void OnDisable()
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

        if (!isEnabled)
        {
            if (isPlayerNearby)
            {
                GetInteractionUIIfAvailable()?.HideInteractPrompt();
            }

            return;
        }

        if (!isPlayerNearby)
        {
            return;
        }

        SwapBasedOnInputMethod();

        InteractionUI interactionUI = GetInteractionUIIfAvailable();
        if (interactionUI == null)
            return;

        if (interactionUI._interactText != null)
        {
            interactionUI._interactText.gameObject.SetActive(true);
            if (interactionUI._interactText.transform.parent != null)
                interactionUI._interactText.transform.parent.gameObject.SetActive(true);
        }

        if (interactionUI._interactIcon != null)
            interactionUI._interactIcon.gameObject.SetActive(true);
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        OnInteractButtonPressed();
    }

    public void OnInteractButtonPressed()
    {
        if (!isPlayerNearby || !interactable)
            return;

        // Don't allow interactions while player is in combat
        if (_combatIdleController != null && _combatIdleController.IsInCombat)
            return;

        Debug.Log($"Player interacted with {gameObject.name} using InputReader Interact.");
        Interact();

        AudioSource interactionSfxSource = GetInteractionSfxSourceIfAvailable();
        if (interactionSfxSource != null && _interactionSFX != null)
            interactionSfxSource.PlayOneShot(_interactionSFX);
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
        // Ensure the collider belongs to the player character, checking the root object for the "Player" tag to account for child colliders.
        if (!other.transform.root.CompareTag("Player"))
            return;

        Debug.Log($"[InteractionManager] Player entered interaction zone of {gameObject.name}. Setting isPlayerNearby true.");

        isPlayerNearby = true;

        SwapBasedOnInputMethod();

        InteractionUI interactionUI = GetInteractionUIIfAvailable();
        if (interactionUI == null)
            return;

        if (interactionUI._interactText != null && interactable)
        {
            interactionUI._interactText.gameObject.SetActive(true);
            if (interactionUI._interactText.transform.parent != null)
                interactionUI._interactText.transform.parent.gameObject.SetActive(true);
        }

        if (interactionUI._interactIcon != null && interactable)
            interactionUI._interactIcon.gameObject.SetActive(true);
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (!other.transform.root.CompareTag("Player"))
            return;

        isPlayerNearby = false;

        GetInteractionUIIfAvailable()?.HideInteractPrompt();
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
