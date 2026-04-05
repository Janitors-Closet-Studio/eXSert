using UnityEngine.Events;
using UnityEngine;

public class HintInteractions : InteractionManager
{
    private Hint hint;
    public UnityEvent[] collectEvents;

    protected override void Awake()
    {
        base.Awake();

        hint = GetComponent<Hint>();
        if (hint == null)
        {
            Debug.LogWarning($"HintInteractions on {gameObject.name} does not have a Hint component attached.");
        }
        else 
        {
            hint.enabled = false; // Ensure the hint is disabled at the start
        }
    }

    // Optional: Add requiredItemID and unlock logic if needed
    [Header("Unlockable Hint Settings")]
    [Tooltip("Insert the ID of the item needed to unlock this hint; leave empty if none is needed")]
    [SerializeField] private string requiredItemID = "";

    protected override void Interact()
        
    {
        Debug.Log($"[HintInteractions] Interact called on {gameObject.name}");
        bool needsItem = !string.IsNullOrEmpty(requiredItemID);
        bool canUnlock = InternalPlayerInventory.Instance != null && InternalPlayerInventory.Instance.HasItem(requiredItemID);
        bool canExecuteInteraction = !needsItem || canUnlock;

        if (!canExecuteInteraction)
        {
            Debug.Log("[HintInteractions] Player does not have the required item. Interaction blocked.");
            // Optionally play error SFX or show a message here
            return;
        }

        if (hint != null)
        {
            hint.enabled = true; // Enable the hint component when interacted with
            if(_interactionSFX != null)
            {
                Debug.Log($"[HintInteractions] Playing SFX: {_interactionSFX.name} on sfxSource from {gameObject.name}");
                SoundManager.Instance.sfxSource.PlayOneShot(_interactionSFX);
            }

            foreach (UnityEvent collectEvent in collectEvents)
            {
                if (collectEvent != null)
                {
                    this.hint.enabled = true; // Enable the hint component when the item is collected
                    collectEvent?.Invoke();
                }
            }
        }
    }

}
