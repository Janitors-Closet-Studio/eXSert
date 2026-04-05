using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using System;
using System.ComponentModel;

public abstract class CollectableInteraction : InteractionManager
{
    [Header("Collectable Interaction Settings")]
    [SerializeField] private string collectID;
    [SerializeField] private float uiDisplayDuration = 4f;
    [SerializeField] private float uiFadeDuration = 2f;
    [SerializeField] private string bottomFlavorText = "Press Pause to View";
    private bool fadeOutComplete = false;   

    protected override void Awake()
    {
        base.Awake();

        collectID = this.interactId;
    }

    protected override void Interact()
    {
        // Keep collectID in sync in case subclasses assign interactId after Awake.
        collectID = this.interactId;

        AudioSource interactionSfxSource = GetInteractionSfxSourceIfAvailable();
        if (interactionSfxSource != null && _interactionSFX != null)
            interactionSfxSource.PlayOneShot(_interactionSFX);
        ExecuteInteraction();
        AfterExecuteInteraction();
        
        InteractionUI.Instance.OnCollectedItem(collectID, bottomFlavorText, uiFadeDuration, uiDisplayDuration);
        InteractionUI.Instance.RemoveCollectableToFindFromObjective(collectID);
        StartCoroutine(DeactivateInteractableCoroutine(this));
    }
    protected abstract void ExecuteInteraction();
    protected virtual void AfterExecuteInteraction() { }

    private IEnumerator DeactivateInteractableCoroutine(CollectableInteraction interaction)
    {
        var renderer = interaction.GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = false;

        this.interactable = false;

        Collider interactionCollider = GetComponent<Collider>();
        if (interactionCollider != null)
            interactionCollider.enabled = false;

        InteractionUI interactionUI = GetInteractionUIIfAvailable();
        if (interactionUI != null)
        {
            if (interactionUI._interactText != null)
                interactionUI._interactText.gameObject.SetActive(false);

            if (interactionUI._interactIcon != null)
                interactionUI._interactIcon.gameObject.SetActive(false);
        }

        List<GameObject> interactionChildren = new List<GameObject>();

        for(int i = 0; i < interaction.transform.childCount; i++)
        {
            interactionChildren.Add(interaction.transform.GetChild(i).gameObject);
        }

        foreach(GameObject child in interactionChildren)
            child.gameObject.SetActive(false);
        
        yield return new WaitForSeconds(uiFadeDuration + uiDisplayDuration + uiFadeDuration); // Wait for fade-in + display + fade-out to complete

        while (!fadeOutComplete)
            yield return null; // Wait until fade-out is complete before deactivating the interactable

        DeactivateInteractable(interaction);

        yield return null; 
    }

    
}
