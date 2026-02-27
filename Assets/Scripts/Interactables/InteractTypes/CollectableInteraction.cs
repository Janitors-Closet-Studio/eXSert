using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using System;

public abstract class CollectableInteraction : InteractionManager
{
    [Header("Collectable Interaction Settings")]
    [SerializeField] private string collectID;
    [SerializeField] private float uiDisplayDuration = 4f;
    [SerializeField] private float uiFadeDuration = 2f;
    [SerializeField] private string bottomFlavorText = "Press Pause to View";

    private void Awake()
    {
        collectID = this.interactId;
    }

    protected override void Interact()
    {
        base.Interact();

        ExecuteInteraction();

        StartCoroutine(FadeInAndFadeOutUI(uiFadeDuration, uiDisplayDuration));
        StartCoroutine(DeactivateInteractableCoroutine(this));
        if(_interactionSFX != null)
            SoundManager.Instance.sfxSource.PlayOneShot(_interactionSFX);
    }
    protected abstract void ExecuteInteraction();

    private IEnumerator DeactivateInteractableCoroutine(CollectableInteraction interaction)
    {
        var renderer = interaction.GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = false;

        this.interactable = false;

        this.GetComponent<Collider>().enabled = false;

        InteractionUI.Instance._interactText.gameObject.SetActive(false);
        InteractionUI.Instance._interactIcon.gameObject.SetActive(false);

        List<GameObject> interactionChildren = new List<GameObject>();

        for(int i = 0; i < interaction.transform.childCount; i++)
        {
            interactionChildren.Add(interaction.transform.GetChild(i).gameObject);
        }

        foreach(GameObject child in interactionChildren)
            child.gameObject.SetActive(false);
        
        yield return new WaitForSeconds(uiDisplayDuration + 2); // Wait for half a second before fully deactivating

        DeactivateInteractable(interaction);

        yield return null; // Placeholder for any additional logic if needed
    }

    private IEnumerator FadeInUI(float fadeDuration, float displayDuration)
    {
        var collectText = InteractionUI.Instance._collectText;
        var collectBottomText = InteractionUI.Instance._collectBottomText;

        if(collectText == null)
        {
            Debug.LogError("InteractionUIManager instance is null. Cannot show collect text.");
            yield break;
        }

        collectText.text = "Collected: " + collectID.Trim();

        if(collectBottomText != null)
            collectBottomText.text = bottomFlavorText;

        collectText.color = new Color(collectText.color.r, collectText.color.g, collectText.color.b, 0f);
        collectText.gameObject.SetActive(true);

        collectBottomText.color = new Color(collectBottomText.color.r, collectBottomText.color.g, collectBottomText.color.b, 0f);
        collectBottomText.gameObject.SetActive(true);

        float elapsedTime = 0f;

        while(elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
            collectText.color = new Color(collectText.color.r, collectText.color.g, collectText.color.b, alpha);
            collectBottomText.color = new Color(collectBottomText.color.r, collectBottomText.color.g, collectBottomText.color.b, alpha);
            yield return null;
        }
    }

    private IEnumerator FadeOutUI(float fadeDuration)
    {
        var collectText = InteractionUI.Instance._collectText;
        var collectBottomText = InteractionUI.Instance._collectBottomText;

        if (collectText == null)
        {
            Debug.LogError("InteractionUIManager instance is null. Cannot fade out collect text.");
            yield break;
        }

        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsedTime / fadeDuration));
            collectText.color = new Color(collectText.color.r, collectText.color.g, collectText.color.b, alpha);
            collectBottomText.color = new Color(collectBottomText.color.r, collectBottomText.color.g, collectBottomText.color.b, alpha);
            yield return null;
        }
        collectText.gameObject.SetActive(false);
        collectBottomText.gameObject.SetActive(false);
    }

    private IEnumerator FadeInAndFadeOutUI(float fadeDuration, float displayDuration)
    {
        yield return StartCoroutine(FadeInUI(fadeDuration, displayDuration));
        yield return StartCoroutine(FadeOutUI(fadeDuration));
    }
}
