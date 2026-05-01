using UnityEngine;
using UnityEngine.Events;

public class HintInteractions : InteractionManager
{
    private Hint hint;

    [Header("Interact Events")]
    [SerializeField]
    private UnityEvent onInteract;

    public UnityEvent[] collectEvents;

    [Header("Unlockable Hint Settings")]
    [Tooltip("Insert the ID of the item needed to unlock this hint; leave empty if none is needed")]
    [SerializeField]
    private string requiredItemID = "";

    protected override void Awake()
    {
        base.Awake();

        hint = GetComponent<Hint>();
        if (hint == null)
        {
            Debug.LogWarning(
                $"HintInteractions on {gameObject.name} does not have a Hint component attached.");
            return;
        }

        hint.enabled = false;
    }

    protected override bool Interact()
    {
        Debug.Log($"[HintInteractions] Interact called on {gameObject.name}");

        bool needsItem = !string.IsNullOrEmpty(requiredItemID);
        bool canUnlock = InternalPlayerInventory.Instance != null
            && InternalPlayerInventory.Instance.HasItem(requiredItemID);
        bool canExecuteInteraction = !needsItem || canUnlock;

        if (!canExecuteInteraction)
        {
            Debug.Log(
                "[HintInteractions] Player does not have the required item. Interaction blocked.");
            return false;
        }

        if (hint == null)
        {
            return false;
        }

        if (!hint.OpenHint())
            return false;

        if (_interactionSFX != null)
        {
            Debug.Log(
                $"[HintInteractions] Playing SFX: {_interactionSFX.name} on sfxSource from {gameObject.name}");
            SoundManager.Instance.sfxSource.PlayOneShot(_interactionSFX);
        }

        foreach (UnityEvent collectEvent in collectEvents)
        {
            collectEvent?.Invoke();
        }

        onInteract?.Invoke();

        NotifyInteractionExecuted();
        return true;
    }
}
