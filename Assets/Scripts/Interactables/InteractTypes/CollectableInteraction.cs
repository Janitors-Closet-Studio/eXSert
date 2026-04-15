using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using System;
using System.ComponentModel;

public abstract class CollectableInteraction : InteractionManager
{
    [Header("Collectable Interaction Settings")]
    [Tooltip("The name of the item to display in the UI when collected.")]
    [SerializeField] private string displayName;
    [SerializeField] private float uiDisplayDuration = 4f;
    [SerializeField] private float uiFadeDuration = 2f;
    [SerializeField] private string bottomFlavorText = "Press Pause to View";

    // Removed local fadeOutComplete; will use InteractionUI.Instance.fadeOutComplete

    protected override void Awake()
    {
        base.Awake();

    }

    protected override void Interact()
    {

        AudioSource interactionSfxSource = GetInteractionSfxSourceIfAvailable();
        if (interactionSfxSource != null && _interactionSFX != null)
            interactionSfxSource.PlayOneShot(_interactionSFX);
        ExecuteInteraction();
        AfterExecuteInteraction();
        
        InteractionUI.Instance.OnCollectedItem(displayName, bottomFlavorText, uiFadeDuration, uiDisplayDuration);
        
        StartCoroutine(DeactivateInteractableCoroutine(this));
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

    
}
