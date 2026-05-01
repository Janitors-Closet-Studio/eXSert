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
    [SerializeField] private string requiredItemDisplayName = "";

    [Tooltip("Prompt shown while the required item is missing.")]
    [SerializeField] protected string lockedInteractionPrompt = "LOCKED";

    [Header("Locked Notice Settings")]
    [Tooltip("Optional custom title shown when the interaction is locked. Falls back to 'Authentication Failed' when empty.")]
    [SerializeField] private string lockedNoticeTitle = "";
    [Tooltip("Optional custom detail text shown when the interaction is locked. Leave empty to use the default required-item message. Use {item} to insert the required item display name or ID.")]
    [SerializeField] [TextArea(2, 4)] private string lockedNoticeBottomText = "";

    [Header("Unlock Success Notice Settings")]
    [Tooltip("Optional custom title shown when the required item is successfully used. Leave empty to use the default 'Used <item>' message. Use {item} and {target} tokens if needed.")]
    [SerializeField] private string usedNoticeTitle = "";
    [Tooltip("Optional custom detail text shown when the required item successfully unlocks this interaction. Leave empty to use the default unlocked message. Use {item} and {target} tokens.")]
    [SerializeField] [TextArea(2, 4)] private string usedNoticeBottomText = "";

    [SerializeField] private bool interactOnce = true;

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

    protected virtual bool ShouldDisableAfterSuccessfulInteraction()
    {
        return interactOnce;
    }

    protected virtual bool IsUnlockedWithoutRequiredItem()
    {
        return false;
    }

    protected void RefreshExecutionState()
    {
        canExecuteInteraction = !needsItem || canUnlock || canExecuteWithoutItem;
    }

    private string GetRequiredItemNoticeName()
    {
        return !string.IsNullOrWhiteSpace(requiredItemDisplayName)
            ? requiredItemDisplayName
            : requiredItemID;
    }

    protected string GetUnlockTargetNoticeName()
    {
        return !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : interactId;
    }

    private string ReplaceNoticeTokens(string template)
    {
        return template
            .Replace("{item}", GetRequiredItemNoticeName())
            .Replace("{target}", GetUnlockTargetNoticeName());
    }

    private string ResolveLockedNoticeTitle()
    {
        return string.IsNullOrWhiteSpace(lockedNoticeTitle)
            ? "Authentication Failed"
            : lockedNoticeTitle;
    }

    private string ResolveLockedNoticeBottomText()
    {
        string itemName = GetRequiredItemNoticeName();
        if (string.IsNullOrWhiteSpace(lockedNoticeBottomText))
            return $"{itemName} is required to use this machine.";

        return lockedNoticeBottomText.Replace("{item}", itemName);
    }

    private void RefreshLockedInteractionPrompt()
    {
        if (!needsItem || canExecuteInteraction)
            return;

        if (IsPlayerBusyForInteraction())
        {
            ClearPromptIfOwned();
            return;
        }

        InteractionUI interactionUI = InteractionUI.Instance;
        if (interactionUI == null || interactionUI._interactText == null)
            return;

        if (interactionUI.currentInteractable != this)
            interactionUI.currentInteractable = this;

        string promptToShow = string.IsNullOrWhiteSpace(lockedInteractionPrompt)
            ? "LOCKED"
            : lockedInteractionPrompt;

        interactionUI._interactText.text = promptToShow;
        interactionUI.ShowInteractPromptImmediate();
    }

    protected string ResolveUsedNoticeTitle()
    {
        if (string.IsNullOrWhiteSpace(usedNoticeTitle))
            return $"Used {GetRequiredItemNoticeName()}";

        return ReplaceNoticeTokens(usedNoticeTitle);
    }

    protected string ResolveUsedNoticeBottomText()
    {
        if (string.IsNullOrWhiteSpace(usedNoticeBottomText))
            return $"Unlocked {GetUnlockTargetNoticeName()} with {GetRequiredItemNoticeName()}.";

        return ReplaceNoticeTokens(usedNoticeBottomText);
    }


    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        RefreshExecutionState();

        if (!other.transform.root.CompareTag("Player"))
            return;

        RefreshLockedInteractionPrompt();
    }

    protected override void OnTriggerStay(Collider other)
    {
        base.OnTriggerStay(other);

        if (!other.transform.root.CompareTag("Player"))
            return;

        RefreshExecutionState();
        RefreshLockedInteractionPrompt();
    }

    private void FailedInteract()
    {
        Debug.Log($"[UnlockableInteraction] Failed interaction attempt on {gameObject.name}. needsItem: {needsItem}, canUnlock: {canUnlock}, canExecuteWithoutItem: {canExecuteWithoutItem}");

        if (!needsItem) return;

        bool playerAlreadyHasRequiredItem = InternalPlayerInventory.Instance != null
            && InternalPlayerInventory.Instance.HasItem(requiredItemID);

        // Audio Stuff
        if (errorSFXClip != null && SoundManager.Instance.sfxSource != null && InteractionUI.Instance != null && !playerAlreadyHasRequiredItem)
        {
            Debug.Log($"[UnlockableInteraction] Playing error SFX: {errorSFXClip.name} on sfxSource from {gameObject.name}");
            SoundManager.Instance.sfxSource.PlayOneShot(errorSFXClip);
            RumbleManager.Instance.RumblePulse(0.5f, 0.5f, 0.2f);
        }
        else
        {
            Debug.LogWarning($"[UnlockableInteraction] Cannot play error SFX. Missing SoundManager instance, sfxSource, InteractionUI instance, or errorSFXClip on {gameObject.name}");
        }

        // Sub Objective Stuff
        string objectiveMessage = !string.IsNullOrEmpty(requiredItemDisplayName)
            ? $"Find {requiredItemDisplayName}"
            : $"Find {requiredItemID}";

        if (!ObjectiveManager.HasSubObjective(requiredItemID))
            ObjectiveManager.AddSubObjective(requiredItemID, objectiveMessage);

        // Notice stuff
        MasterObjectiveClass resolvedMasterObjective = GetMasterObjectiveIfAvailable();
        if (resolvedMasterObjective != null)
            resolvedMasterObjective.CreateAndShowNotice(this, GetContextualNoticeId("locked"), ResolveLockedNoticeTitle(), ResolveLockedNoticeBottomText(), 2f, 4f, priority: 11);
    }

    protected override bool Interact()    
    {
        MasterObjectiveClass resolvedMasterObjective = GetMasterObjectiveIfAvailable();
        if (resolvedMasterObjective != null)
            resolvedMasterObjective.CancelCurrentCollectNotice();

        RefreshExecutionState();
        
        Debug.Log($"[UnlockableInteraction] Interact called on {gameObject.name}.\n needsItem: {needsItem}, canUnlock: {canUnlock}, canExecuteWithoutItem: {canExecuteWithoutItem}, canExecuteInteraction: {canExecuteInteraction}, requiredItemID: '{requiredItemID}', playerHasItem: {(InternalPlayerInventory.Instance != null ? InternalPlayerInventory.Instance.HasItem(requiredItemID) : (bool?)null)}");
        // Defensive null checks
        if (needsItem && InternalPlayerInventory.Instance == null && !canExecuteWithoutItem)
        {
            Debug.LogWarning("[UnlockableInteraction] InternalPlayerInventory.Instance is null. Cannot check for required item.");
            return false;
        }

        if (!canExecuteInteraction)
        {
            FailedInteract();
            Debug.Log($"[UnlockableInteraction] Interaction failed on {gameObject.name} due to unmet conditions.");
            return false;
        }

        if (onInteractionExecuted == null)
        {
            Debug.LogWarning("[UnlockableInteraction] onInteractionExecuted event is not assigned.");
        }

        ExecuteInteraction();
        onInteractionExecuted?.Invoke();
    NotifyInteractionExecuted();

        // Only show notice immediately if not using camera transition (handled in DoorInteractions otherwise)
        if (!(this is DoorInteractions doorInt) || !(doorInt.HasActiveCameraTransition()))
        {
            if (needsItem && canUnlock && resolvedMasterObjective != null)
                resolvedMasterObjective.CreateAndShowNotice(this, GetContextualNoticeId("used"), ResolveUsedNoticeTitle(), ResolveUsedNoticeBottomText(), 2f, 4f, priority: 8);
        }

        if(_interactionSFX != null && SoundManager.Instance != null && SoundManager.Instance.sfxSource != null)
            SoundManager.Instance.sfxSource.PlayOneShot(_interactionSFX);

        if (ShouldDisableAfterSuccessfulInteraction())
            interactable = false;

        return true;
    }
}
