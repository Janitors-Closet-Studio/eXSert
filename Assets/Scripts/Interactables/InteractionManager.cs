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

        if (isPlayerNearby && InteractionUI.Instance != null)
            InteractionUI.Instance.HideInteractPrompt();

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
            InteractionUI.Instance?.HideInteractPrompt();
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

        InteractionUI.Instance?.HideInteractPrompt();
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
        if(InteractionUI.Instance != null && _interactionSFX != null)
            SoundManager.Instance.sfxSource.PlayOneShot(_interactionSFX);
    }

    protected abstract void Interact();

    

    public void SwapBasedOnInputMethod()
    {


        if (InteractionUI.Instance._interactText != null)
        {
            InteractionUI.Instance._interactText.text = string.IsNullOrWhiteSpace(_interactionPrompt)
                ? "Press to Interact"
                : _interactionPrompt;
            InteractionUI.Instance._interactText.gameObject.SetActive(true);
        }

        if (InteractionUI.Instance._interactIcon != null)
            InteractionUI.Instance._interactIcon.gameObject.SetActive(true);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        // Ensure the collider belongs to the player character, checking the root object for the "Player" tag to account for child colliders.
        if (!other.transform.root.CompareTag("Player"))
            return;

        Debug.Log($"[InteractionManager] Player entered interaction zone of {gameObject.name}. Setting isPlayerNearby true.");

        isPlayerNearby = true;

        SwapBasedOnInputMethod();

        if (InteractionUI.Instance._interactText != null && interactable)
        {
            InteractionUI.Instance._interactText.gameObject.SetActive(true);
            if (InteractionUI.Instance._interactText.transform.parent != null)
                InteractionUI.Instance._interactText.transform.parent.gameObject.SetActive(true);
        }

        if (InteractionUI.Instance._interactIcon != null && interactable)
            InteractionUI.Instance._interactIcon.gameObject.SetActive(true);
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (!other.transform.root.CompareTag("Player"))
            return;

        isPlayerNearby = false;

        InteractionUI.Instance?.HideInteractPrompt();
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
