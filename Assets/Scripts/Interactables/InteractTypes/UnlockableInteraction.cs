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

    protected bool needsItem => !string.IsNullOrEmpty(requiredItemID);
    protected bool canUnlock => InternalPlayerInventory.Instance.HasItem(requiredItemID);
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

    protected override void Interact()
    {
        if (!needsItem)
        { 
            ExecuteInteraction();
            onInteractionExecuted?.Invoke();
            if(_interactionSFX != null)
            SoundManager.Instance.sfxSource.PlayOneShot(_interactionSFX);
            return;
        }

        if (canUnlock)
        {
            ExecuteInteraction();
            onInteractionExecuted?.Invoke();
            if(_interactionSFX != null)
            SoundManager.Instance.sfxSource.PlayOneShot(_interactionSFX);
        }
        else
        {
            if (errorSFXClip != null)
            {
                SoundManager.Instance.puzzleSource.PlayOneShot(errorSFXClip);
            }
        }
    }
}
