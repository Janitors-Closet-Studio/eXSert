/*
    Written by Brandon Wahl

    Unified base class for gated interactions (puzzles, doors, etc).
    Any interaction that requires a prerequisite item from the inventory.
    This combines the logic for both DoorInteractions and PuzzleInteraction.
*/

using UnityEngine;
using UnityEngine.Events;

public abstract class UnlockableInteraction : InteractionManager
{
    [Header("Unlockable Interaction Settings")]
    [Tooltip("Insert the ID of the item needed to unlock this interaction; leave empty if none is needed")]
    [SerializeField] protected string requiredItemID = "";

    [Tooltip("Prompt shown while the required item is missing.")]
    [SerializeField] private string lockedInteractionPrompt = "LOCKED";

    protected bool needsItem => !string.IsNullOrEmpty(requiredItemID);
    protected bool canUnlock => InternalPlayerInventory.Instance != null && InternalPlayerInventory.Instance.HasItem(requiredItemID);
    protected bool canExecuteWithoutItem => IsUnlockedWithoutRequiredItem();
    protected bool canExecuteInteraction => !needsItem || canUnlock || canExecuteWithoutItem;

    [Header("Error SFX")]
    [SerializeField] private AudioClip errorSFXClip;

    [Header("Events")]
    [Tooltip("Invoked when the interaction successfully executes (i.e., after unlocking conditions are met).")]
    [SerializeField] private UnityEvent onInteractionExecuted;

    protected override void Awake()
    {
        base.Awake();
        
        // Normalize required item ID
        if (needsItem)
            requiredItemID = requiredItemID.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Called when the interaction is successfully unlocked.
    /// Subclasses must implement this to define what happens when unlocked.
    /// </summary>
    protected abstract void ExecuteInteraction();

    protected virtual bool IsUnlockedWithoutRequiredItem()
    {
        return false;
    }

    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        if (!other.transform.root.CompareTag("Player"))
            return;

        if (!needsItem || canExecuteInteraction)
            return;

        if (InteractionUI.Instance != null && InteractionUI.Instance._interactText != null)
        {
            InteractionUI.Instance._interactText.text = string.IsNullOrWhiteSpace(lockedInteractionPrompt)
                ? "LOCKED"
                : lockedInteractionPrompt;
        }
    }

    protected override void Interact()
    {
        // Defensive null checks
        if (needsItem && InternalPlayerInventory.Instance == null)
        {
            Debug.LogWarning("[UnlockableInteraction] InternalPlayerInventory.Instance is null. Cannot check for required item.");
            return;
        }

        if (canExecuteInteraction)
        {
            if (onInteractionExecuted == null)
            {
                Debug.LogWarning("[UnlockableInteraction] onInteractionExecuted event is not assigned.");
            }
            ExecuteInteraction();
            onInteractionExecuted?.Invoke();
            if(_interactionSFX != null && SoundManager.Instance != null && SoundManager.Instance.sfxSource != null)
                SoundManager.Instance.sfxSource.PlayOneShot(_interactionSFX);
            return;
        }

        if (errorSFXClip != null && SoundManager.Instance != null && SoundManager.Instance.puzzleSource != null)
        {
            SoundManager.Instance.puzzleSource.PlayOneShot(errorSFXClip);
            RumbleManager.Instance.RumblePulse(0.5f, 0.5f, 0.2f);
        }
        else if (errorSFXClip != null)
        {
            Debug.LogWarning("[UnlockableInteraction] SoundManager.Instance or puzzleSource is null. Cannot play error SFX.");
        }
    }
}
