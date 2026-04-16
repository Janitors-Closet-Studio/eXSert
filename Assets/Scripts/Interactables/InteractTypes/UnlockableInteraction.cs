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
    internal bool canExecuteInteraction = false;

    [Header("Error SFX")]
    [SerializeField] internal AudioClip errorSFXClip;

    [Header("Events")]
    [Tooltip("Invoked when the interaction successfully executes (i.e., after unlocking conditions are met).")]
    [SerializeField] private UnityEvent onInteractionExecuted;

    protected override void Awake()
    {
        base.Awake();
        
        // Normalize required item ID
        if (needsItem)
            requiredItemID = requiredItemID.Trim().ToLowerInvariant();

        if (needsItem)
            canExecuteInteraction = false;
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

    protected void RefreshExecutionState()
    {
        canExecuteInteraction = !needsItem || canUnlock || canExecuteWithoutItem;
    }


    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        RefreshExecutionState();

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

    private void FailedInteract()
    {
        Debug.Log($"[UnlockableInteraction] Failed interaction attempt on {gameObject.name}. needsItem: {needsItem}, canUnlock: {canUnlock}, canExecuteWithoutItem: {canExecuteWithoutItem}");

        if (!needsItem)
            return;

        bool playerAlreadyHasRequiredItem = InternalPlayerInventory.Instance != null
            && InternalPlayerInventory.Instance.HasItem(requiredItemID);

        if (errorSFXClip != null && SoundManager.Instance != null && SoundManager.Instance.sfxSource != null && InteractionUI.Instance != null && !playerAlreadyHasRequiredItem)
        {
            Debug.Log($"[UnlockableInteraction] Playing error SFX: {errorSFXClip.name} on sfxSource from {gameObject.name}");
            SoundManager.Instance.sfxSource.PlayOneShot(errorSFXClip);
            RumbleManager.Instance.RumblePulse(0.5f, 0.5f, 0.2f);

            // Sub Objective Stuff
            string objectiveMessage = $"Find {requiredItemID}";

            ObjectiveManager.AddSubObjective(requiredItemID, objectiveMessage);

            // Notice stuff
            InteractionUI.Instance.OnCollectedItem("Authentication Failed", $"{requiredItemID} is required to use this machine.", 0.5f, 2f);
        }
        else
        {
            Debug.LogWarning($"[UnlockableInteraction] Cannot play error SFX. Missing SoundManager instance, sfxSource, InteractionUI instance, or errorSFXClip on {gameObject.name}");
        }
    }

    protected override void Interact()    
    {
        RefreshExecutionState();
        
        Debug.Log($"[UnlockableInteraction] Interact called on {gameObject.name}.\n needsItem: {needsItem}, canUnlock: {canUnlock}, canExecuteWithoutItem: {canExecuteWithoutItem}, canExecuteInteraction: {canExecuteInteraction}, requiredItemID: '{requiredItemID}', playerHasItem: {(InternalPlayerInventory.Instance != null ? InternalPlayerInventory.Instance.HasItem(requiredItemID) : (bool?)null)}");
        // Defensive null checks
        if (needsItem && InternalPlayerInventory.Instance == null && !canExecuteWithoutItem)
        {
            Debug.LogWarning("[UnlockableInteraction] InternalPlayerInventory.Instance is null. Cannot check for required item.");
            return;
        }

        if (!canExecuteInteraction)
        {
            FailedInteract();
            Debug.Log($"[UnlockableInteraction] Interaction failed on {gameObject.name} due to unmet conditions.");
            return;
        }

        if (onInteractionExecuted == null)
        {
            Debug.LogWarning("[UnlockableInteraction] onInteractionExecuted event is not assigned.");
        }

        ExecuteInteraction();
        onInteractionExecuted?.Invoke();

        // Only show notice immediately if not using camera transition (handled in DoorInteractions otherwise)
        if (!(this is DoorInteractions doorInt) || !(doorInt.HasActiveCameraTransition()))
        {
            if (needsItem && canUnlock && InteractionUI.Instance != null)
                InteractionUI.Instance.OnCollectedItem($"Used {requiredItemID}", $"Unlocked {this.interactId} with {requiredItemID}.", 0.5f, 6f);
        }

        if(_interactionSFX != null && SoundManager.Instance != null && SoundManager.Instance.sfxSource != null)
            SoundManager.Instance.sfxSource.PlayOneShot(_interactionSFX);
    }
}
