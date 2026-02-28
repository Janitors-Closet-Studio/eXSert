using UnityEngine;
using UnityEngine.InputSystem;
using System;
using UnityEngine.SceneManagement;
using System.Collections;

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
    protected virtual void Awake()
    {
        this.GetComponent<BoxCollider>().isTrigger = true;

        interactId = _interactId.Trim().ToLowerInvariant();
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
            if (InteractionUI.Instance != null)
            {
                if (InteractionUI.Instance._interactText != null)
                    InteractionUI.Instance._interactText.gameObject.SetActive(false);
                if (InteractionUI.Instance._interactIcon != null)
                    InteractionUI.Instance._interactIcon.gameObject.SetActive(false);
            }
        }
        else 
        {
            while (!scene.isLoaded)
            {
                yield return null; // Wait until the scene is loaded
            }
            StopCoroutine(FindPlayerScene(sceneName)); // Stop the coroutine once the scene is loaded
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

        if (InteractionUI.Instance._interactIcon != null)
            InteractionUI.Instance._interactIcon.gameObject.SetActive(false);

        if (InteractionUI.Instance._interactText != null)
            InteractionUI.Instance._interactText.gameObject.SetActive(false);
    }

    public void OnInteractButtonPressed()
    {
        if (!isPlayerNearby || !InputReader.InteractTriggered || !interactable)
            return;

        Debug.Log($"Player interacted with {gameObject.name} using InputReader Interact.");
        Interact();
    }

    private void Update()
    {
        OnInteractButtonPressed();
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

        Debug.Log($"Player entered interaction zone of {gameObject.name}");

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
            if (InteractionUI.Instance._interactText != null)
                InteractionUI.Instance._interactText.gameObject.SetActive(false);
        if (InteractionUI.Instance._interactIcon != null)
            InteractionUI.Instance._interactIcon.gameObject.SetActive(false);
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
