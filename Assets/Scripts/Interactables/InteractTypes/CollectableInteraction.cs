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

        ExecuteInteraction();
        AfterExecuteInteraction();

        StartCoroutine(FadeInAndFadeOutUI(uiFadeDuration, uiDisplayDuration));
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

    private IEnumerator FadeInUI(float fadeDuration, float displayDuration)
    {
        InteractionUI interactionUI = GetInteractionUIIfAvailable();
        if (interactionUI == null)
            yield break;


        var collectUI = interactionUI.collectUI;
        CanvasGroup canvasGroup = collectUI.GetComponent<CanvasGroup>();

        var collectText = interactionUI._collectText;
        var collectBottomText = interactionUI._collectBottomText;

        if (collectUI == null)
        {
            Debug.LogError("InteractionUIManager instance is null. Cannot show collect UI.");
            yield break;
        }

        if (collectBottomText == null)
        {
            Debug.LogError("InteractionUIManager instance is null. Cannot show collect bottom text.");
            yield break;
        }

        if(collectText == null)
        {
            Debug.LogError("InteractionUIManager instance is null. Cannot show collect text.");
            yield break;
        }

        string collectedLabel = string.IsNullOrWhiteSpace(collectID) ? "Unknown" : collectID.Trim();
        collectText.text = "Collected: " + collectedLabel;

        if (collectUI != null)
        {

            if (canvasGroup == null)
                canvasGroup = collectUI.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            
            collectUI.SetActive(true);
        }

        

        if(collectBottomText != null)
            collectBottomText.text = bottomFlavorText;

        collectText.color = new Color(collectText.color.r, collectText.color.g, collectText.color.b, 0f);
        collectText.gameObject.SetActive(true);

        if (collectBottomText != null)
        {
            collectBottomText.color = new Color(collectBottomText.color.r, collectBottomText.color.g, collectBottomText.color.b, 0f);
            collectBottomText.gameObject.SetActive(true);
        }

        // Fade in collectUI background first
        float elapsedTime = 0f;
        while(elapsedTime < fadeDuration / 2)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / fadeDuration);

            if (collectUI != null)
            {
                canvasGroup = collectUI.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                    canvasGroup.alpha = alpha;
            }
            yield return null;
        }

        // Then fade in text
        elapsedTime = 0f;
        while(elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / fadeDuration);

            collectText.color = new Color(collectText.color.r, collectText.color.g, collectText.color.b, alpha);
            if (collectBottomText != null)
                collectBottomText.color = new Color(collectBottomText.color.r, collectBottomText.color.g, collectBottomText.color.b, alpha);
            yield return null;
        }
    }

    private IEnumerator FadeOutUI(float fadeDuration)
    {
        InteractionUI interactionUI = GetInteractionUIIfAvailable();
        if (interactionUI == null)
            yield break;

        var collectUI = interactionUI.collectUI;
        CanvasGroup canvasGroup = collectUI.GetComponent<CanvasGroup>();

        var collectText = interactionUI._collectText;
        var collectBottomText = interactionUI._collectBottomText;

        if (collectText == null)
        {
            Debug.LogError("InteractionUIManager instance is null. Cannot fade out collect text.");
            yield break;
        }

        if (collectBottomText == null)
        {
            Debug.LogError("InteractionUIManager instance is null. Cannot fade out collect bottom text.");
            yield break;
        }

        if (collectUI == null)
        {
            Debug.LogError("InteractionUIManager instance is null. Cannot fade out collect UI.");
            yield break;
        }

        if (canvasGroup == null)
            canvasGroup = collectUI.AddComponent<CanvasGroup>();

        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            Debug.Log("Fading out collect UI... Elapsed time: " + elapsedTime.ToString("F2") + "s");
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsedTime / fadeDuration));
            collectText.color = new Color(collectText.color.r, collectText.color.g, collectText.color.b, alpha);
            if (collectBottomText != null)
                collectBottomText.color = new Color(collectBottomText.color.r, collectBottomText.color.g, collectBottomText.color.b, alpha);
            if (canvasGroup != null)
                canvasGroup.alpha = alpha;
            yield return null;
        }
        collectText.gameObject.SetActive(false);
        if (collectBottomText != null)
            collectBottomText.gameObject.SetActive(false);
        if (collectUI != null)
            collectUI.SetActive(false);
        fadeOutComplete = true;
    }

    private IEnumerator FadeInAndFadeOutUI(float fadeDuration, float displayDuration)
    {
        yield return StartCoroutine(FadeInUI(fadeDuration, displayDuration));
        yield return new WaitForSeconds(displayDuration);
        yield return StartCoroutine(FadeOutUI(fadeDuration));
    }
}
