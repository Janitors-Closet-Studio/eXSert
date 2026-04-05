using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Singletons;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;


public class InteractionUI : Singleton<InteractionUI>
    
{
    // Tracks which interactable is currently responsible for the prompt
    public MonoBehaviour currentInteractable;

    [SerializeField] private ObjectiveText objectiveText;

    [Header("Global Interaction UI")]
    public TMP_Text _interactText;
    public Image _interactIcon;
    public TMP_Text _collectText;
    public TMP_Text _collectBottomText;
    public TMP_Text _hintNameText;
    public TMP_Text _hintDescriptionText;
    public GameObject hintUI;
    public GameObject collectUI;
    private bool fadeOutComplete = false;

    internal List<string> collectablesToFind = new List<string>();

    public static InteractionUI TryGetExisting()
    {
        if (isApplicationQuitting)
            return null;

        InteractionUI[] interactionUIs = FindObjectsByType<InteractionUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return interactionUIs.Length > 0 ? interactionUIs[0] : null;
    }

    protected override void Awake()
    {
        base.Awake();
        HideInteractPrompt();
        HideCollectUI();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        Debug.Log("turning off interaction UI and unsubscribing from scene loaded event\n" + System.Environment.StackTrace);
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void WriteTextToCollectUI(string text, string bottomText)
    {
        if (_collectText != null)
            _collectText.text = text;

        if (_collectBottomText != null)
            _collectBottomText.text = bottomText;

    }

    public void HideInteractPrompt()
    {
        if (_interactText != null)
        {
            _interactText.gameObject.SetActive(false);
            if (_interactText.transform != null)
                _interactText.transform.gameObject.SetActive(false);
        }

        if (_interactIcon != null)
            _interactIcon.gameObject.SetActive(false);

        currentInteractable = null;
    }

    public void HideCollectUI()
    {
        if (collectUI != null)
            collectUI.SetActive(false);
        if (_collectText != null)            
            _collectText.gameObject.SetActive(false);
        if (_collectBottomText != null)           
             _collectBottomText.gameObject.SetActive(false);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideInteractPrompt();
    }

    public void OnCollectedItem(string collectID, string bottomText, float fadeDuration = 0.5f, float displayDuration = 1.5f)
    {
        ShowCollectableUIWithTyping(collectID, bottomText, fadeDuration, displayDuration);
    }

    private Coroutine collectableUICoroutine;
    public void ShowCollectableUIWithTyping(string collectedLabel, string bottomFlavorText, float fadeDuration = 0.5f, float displayDuration = 1.5f, float typeSpeed = 0.03f, bool invisibleCharacters = false)
    {
        if (collectableUICoroutine != null)
        {
            StopCoroutine(collectableUICoroutine);
            collectableUICoroutine = null;
        }
        collectableUICoroutine = StartCoroutine(FadeInTypeFadeOutRoutine(collectedLabel, bottomFlavorText, fadeDuration, displayDuration, typeSpeed, invisibleCharacters));
    }


    // Fades in the collect UI and text
    private IEnumerator FadeInUI(float fadeDuration)
    {
        CanvasGroup canvasGroup = collectUI.GetComponent<CanvasGroup>();

        if (collectUI == null)
        {
            Debug.LogError("InteractionUIManager instance is null. Cannot show collect UI.");
            yield break;
        }


        if (collectUI != null)
        {
            if (canvasGroup == null)
                canvasGroup = collectUI.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            collectUI.SetActive(true);
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

    }

    private IEnumerator FadeOutUI(float fadeDuration)
    {

        if (collectUI == null)
        {
            Debug.LogError("InteractionUIManager instance is null. Cannot fade out collect UI.");
            yield break;
        }

        CanvasGroup canvasGroup = collectUI.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = collectUI.AddComponent<CanvasGroup>();

        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            Debug.Log("Fading out collect UI... Elapsed time: " + elapsedTime.ToString("F2") + "s");
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsedTime / fadeDuration));
            if (canvasGroup != null)
                canvasGroup.alpha = alpha;
            yield return null;
        }
        if (collectUI != null)
            collectUI.SetActive(false);
        fadeOutComplete = true;
    }

    // Coroutine to fade in, then type, then fade out
    private IEnumerator FadeInTypeFadeOutRoutine(string collectedLabel, string bottomFlavorText, float fadeDuration, float displayDuration, float typeSpeed, bool invisibleCharacters)
    {
        if (collectUI != null)
            collectUI.SetActive(true);

        _collectText.text = "";
        _collectBottomText.text = "";

        _collectBottomText.gameObject.SetActive(true);
        _collectText.gameObject.SetActive(true);

        // Fade in first
        yield return StartCoroutine(FadeInUI(fadeDuration));

        // Start typing effect after fade in (main text first)
        if (_collectText != null)
        {
            var textMeshProUGUI = _collectText as TextMeshProUGUI;
            if (textMeshProUGUI != null)
            {
                Debug.Log("Starting typing effect for collected item: " + collectedLabel);
                WritingTextUI.AddWriter_Static(textMeshProUGUI, collectedLabel, typeSpeed, invisibleCharacters);
                float typingTime = collectedLabel.Length * typeSpeed;
                yield return new WaitForSeconds(typingTime + 0.1f); // Wait for main text to finish
            }
            else
            {
                _collectText.text = collectedLabel;
                yield return new WaitForSeconds(0.1f);
            }
        }

        // Now type the bottom text
        if (_collectBottomText != null)
        {
            var bottomTextMeshProUGUI = _collectBottomText as TextMeshProUGUI;
            if (bottomTextMeshProUGUI != null)
            {
                WritingTextUI.AddWriter_Static(bottomTextMeshProUGUI, bottomFlavorText, typeSpeed, invisibleCharacters);
                float typingTime = bottomFlavorText.Length * typeSpeed;
                yield return new WaitForSeconds(typingTime + displayDuration);  
            }
            else
            {
                _collectBottomText.text = bottomFlavorText;
                yield return new WaitForSeconds(displayDuration);
            }
        }

        // Fade out
        yield return StartCoroutine(FadeOutUI(fadeDuration));
    }

    public void AddCollectableToFindToObjective(string collectableName = "")
    {
        string nameToAdd = string.IsNullOrEmpty(collectableName) ? _collectText.text : collectableName;
        if (!string.IsNullOrWhiteSpace(nameToAdd) && !collectablesToFind.Contains(nameToAdd))
        {
            objectiveText.SetText("Find the " + nameToAdd);
            collectablesToFind.Add(nameToAdd);
        }
    }

    public void RemoveCollectableToFindFromObjective(string collectableName = "")
    {
        string nameToRemove = string.IsNullOrEmpty(collectableName) ? _collectText.text : collectableName;
        if (!collectablesToFind.Contains(nameToRemove))
        {
            // Player was not previously prompted to find this collectable, so do nothing
            return;
        }
        collectablesToFind.Remove(nameToRemove);
        if (collectablesToFind.Count == 0)
        {
            objectiveText.SetText("<s>Find the " + nameToRemove + "</s>");
            objectiveText.FadeOutText(1.5f);
            
        }
        else
        {
            objectiveText.SetText("Find the " + string.Join(", ", collectablesToFind));
        }
    }

   

}