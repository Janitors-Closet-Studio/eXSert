using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using System;
using System.ComponentModel;

public abstract class CollectableInteraction : InteractionManager
{

    [SerializeField] private bool doNotLoadFromSave = false;

    protected override void Awake()
    {
        base.Awake();

       if (!doNotLoadFromSave)
            IfInInventoryDeactivate();  
    }

    private void IfInInventoryDeactivate()
    {
        InternalPlayerInventory inventory = InternalPlayerInventory.Instance;
        if (inventory != null)
        {
            if (inventory.collectedInteractables.Contains(this.interactId.Trim().ToLowerInvariant()))
            {
                Debug.Log($"[CollectableInteraction] {this.interactId} already in inventory. Deactivating interactable.");
                gameObject.SetActive(false);
            }
            else
            {
                Debug.Log($"[CollectableInteraction] {this.interactId} not in inventory. Interactable remains active.");
            }
        }
        else
        {
            Debug.LogWarning($"[CollectableInteraction] Inventory instance not found. Cannot check for {this.interactId}.");
        }
    }

    protected override bool Interact()
    {

        AudioSource interactionSfxSource = GetInteractionSfxSourceIfAvailable();
        if (interactionSfxSource != null && _interactionSFX != null)
            interactionSfxSource.PlayOneShot(_interactionSFX);
        ExecuteInteraction();
        AfterExecuteInteraction();
        
        if (masterObjective != null)
        {
            if (debugLogging) Debug.Log($"[CollectableInteraction] Showing notice for {this.interactId}: {displayName}");
            masterObjective.CreateAndShowNotice(this, this.interactId, displayName, bottomFlavorText, uiFadeDuration, uiDisplayDuration, priority: 9);
        }
        else
        {
            Debug.LogError($"[CollectableInteraction] masterObjective is null for {this.interactId}! Notice will not show.");
        }
        
        StartCoroutine(DeactivateInteractableCoroutine(this));

        return true;
    }
    protected abstract void ExecuteInteraction();
    protected virtual void AfterExecuteInteraction() { }

    private IEnumerator DeactivateInteractableCoroutine(CollectableInteraction interaction)
    {
        // Disable all Renderer components in this object and its children
        foreach (var rend in interaction.GetComponentsInChildren<Renderer>(true))
            rend.enabled = false;

        // Disable all Collider components in this object and its children
        foreach (var col in interaction.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        this.interactable = false;

        // Hide UI elements if available
        InteractionUI interactionUI = GetInteractionUIIfAvailable();
        if (interactionUI != null)
        {
            if (interactionUI._interactText != null)
                interactionUI._interactText.gameObject.SetActive(false);

            if (interactionUI._interactIcon != null)
                interactionUI._interactIcon.gameObject.SetActive(false);
        }

        // Deactivate all direct children
        for (int i = 0; i < interaction.transform.childCount; i++)
        {
            interaction.transform.GetChild(i).gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(uiFadeDuration + uiDisplayDuration + uiFadeDuration); // Wait for fade-in + display + fade-out to complete

        // Wait until fade-out is complete in the UI before deactivating the interactable
        while (InteractionUI.Instance != null && !GetUIFadeOutComplete())
            yield return null;

        // At this point, the UI notice should have faded out. Now deactivate the root object.
        interaction.gameObject.SetActive(false);

        DeactivateInteractable(interaction);

        yield return null;

    }

    // Helper to safely access fadeOutComplete from InteractionUI
    private bool GetUIFadeOutComplete()
    {
        var ui = InteractionUI.Instance;
        if (ui == null) return true; // If UI is gone, treat as complete
        var field = typeof(InteractionUI).GetField("fadeOutComplete", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            return (bool)field.GetValue(ui);
        return true;
    }

    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        if (!other.transform.root.CompareTag("Player"))
            return;

        // Show collect prompt if player is in range
        InteractionUI interactionUI = GetInteractionUIIfAvailable();
        if (interactionUI != null && interactionUI._interactText != null)
        {
            // Respect InteractionManager ownership so text cannot appear a frame early.
            if (interactionUI.currentInteractable != this)
                return;

            interactionUI._interactText.text = this._interactionPrompt;
            interactionUI.ShowInteractPromptImmediate();
        }
    }
    
}
