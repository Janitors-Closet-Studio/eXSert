using UnityEngine;
using UnityEngine.InputSystem;
using System;
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
    [SerializeField] private AudioClip _interactionSFX;
    [SerializeField] private string _interactionPrompt = "Press to Interact";
    
    [Space(10)]
    [Header("Input Action Reference")]
    [SerializeField, CriticalReference] internal InputActionReference _interactInputAction;

    internal InteractionUI ResolveInteractionUI()
    {
        return FindObjectOfType<InteractionUI>(true);
    }

    protected virtual void Awake()
    {
        this.GetComponent<BoxCollider>().isTrigger = true;

        interactId = _interactId.Trim().ToLowerInvariant();

        

        var ui = ResolveInteractionUI();
        if (ui == null)
            return;

        if (ui._interactText != null)
            ui._interactText.gameObject.SetActive(false);
        if (ui._interactIcon != null)
            ui._interactIcon.gameObject.SetActive(false);
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

        var ui = ResolveInteractionUI();
        if (ui == null)
            return;

        if (ui._interactIcon != null)
            ui._interactIcon.gameObject.SetActive(false);

        if (ui._interactText != null)
            ui._interactText.gameObject.SetActive(false);
    }

    public void OnInteractButtonPressed()
    {
        if (!isPlayerNearby || !InputReader.InteractTriggered || !interactable)
            return;

        Interact();
        var ui = ResolveInteractionUI();
        if(ui != null && _interactionSFX != null)
            SoundManager.Instance.sfxSource.PlayOneShot(_interactionSFX);
    }

    private void Update()
    {
        OnInteractButtonPressed();
    }

    protected abstract void Interact();

    

    public void SwapBasedOnInputMethod()
    {
        var ui = ResolveInteractionUI();
        if (ui == null)
            return;

        if (ui._interactText != null)
        {
            ui._interactText.text = string.IsNullOrWhiteSpace(_interactionPrompt)
                ? "Press to Interact"
                : _interactionPrompt;
            ui._interactText.gameObject.SetActive(true);
        }

        if (ui._interactIcon != null)
            ui._interactIcon.gameObject.SetActive(true);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        // Ensure the collider belongs to the player character, checking the root object for the "Player" tag to account for child colliders.
        if (!other.transform.root.CompareTag("Player"))
            return;

        isPlayerNearby = true;

        SwapBasedOnInputMethod();

        var ui = ResolveInteractionUI();
        if (ui == null)
            return;

        if (ui._interactText != null && interactable)
        {
            ui._interactText.gameObject.SetActive(true);
            if (ui._interactText.transform.parent != null)
                ui._interactText.transform.parent.gameObject.SetActive(true);
        }

        if (ui._interactIcon != null && interactable)
            ui._interactIcon.gameObject.SetActive(true);
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (!other.transform.root.CompareTag("Player"))
            return;

        isPlayerNearby = false;

        var ui = ResolveInteractionUI();
        if (ui == null)
            return;
        if (ui._interactText != null)
            ui._interactText.gameObject.SetActive(false);

        if (ui._interactIcon != null)
            ui._interactIcon.gameObject.SetActive(false);
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
