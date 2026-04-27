using UnityEngine;
using TMPro;
using System.Collections;
using Singletons;

public class NoticeManager : MonoBehaviour
{
    private const float MaxFadeInBeforeTyping = 0.35f;
    private const float DefaultTypeSpeed = 0.02f;
    private const float MinFadeOutDuration = 1f;

    private int currentCollectPriority = 0;
    private Coroutine collectableUICoroutine;
    private bool fadeOutComplete = false;
    private InteractionUI cachedInteractionUI;

    [Header("Debug")]
    [Tooltip("Enable verbose NoticeManager debug logs.")]
    [SerializeField] private bool debugLogging = false;

    // Properties that use cached reference
    private GameObject collectUI => cachedInteractionUI != null ? cachedInteractionUI.collectUI : null;
    private TMP_Text _collectText => cachedInteractionUI != null ? cachedInteractionUI._collectText : null;
    private TMP_Text _collectBottomText => cachedInteractionUI != null ? cachedInteractionUI._collectBottomText : null;

    private void Awake()
    {
        // Get reference to InteractionUI using TryGetExisting to avoid creating an empty singleton
        cachedInteractionUI = InteractionUI.TryGetExisting();
        if (cachedInteractionUI != null)
        {
            if (debugLogging) Debug.Log($"[NoticeManager] Initialized. Found InteractionUI: {cachedInteractionUI.gameObject.name}");
            if (debugLogging) Debug.Log($"[NoticeManager] collectUI: {cachedInteractionUI.collectUI}, _collectText: {cachedInteractionUI._collectText}, _collectBottomText: {cachedInteractionUI._collectBottomText}");
        }
        else
        {
            Debug.LogWarning("[NoticeManager] InteractionUI not yet initialized in scene. Will retry when needed.");
        }

        HideCollectUI();
    }


    internal void ShowNotice(string noticeText, string bottomText, float fadeDuration = 0.5f, float displayDuration = 1.5f, int priority = 0)
    {
        if (debugLogging) Debug.Log($"[NoticeManager] ShowNotice called: '{noticeText}' | '{bottomText}' | priority={priority}");
        
        // Try to refresh cache if null
        if (cachedInteractionUI == null)
        {
            cachedInteractionUI = InteractionUI.TryGetExisting();
            Debug.LogWarning("[NoticeManager] cachedInteractionUI was null, attempting to refresh from TryGetExisting()");
        }
        
        // Validate UI references are available
        if (cachedInteractionUI == null)
        {
            Debug.LogError("[NoticeManager] InteractionUI.Instance is still null after refresh. Cannot show notice.");
            return;
        }
        
        if (debugLogging) Debug.Log($"[NoticeManager] collectUI: {collectUI}, _collectText: {_collectText}, _collectBottomText: {_collectBottomText}");
        
        if (_collectText == null || _collectBottomText == null || collectUI == null)
        {
            Debug.LogError($"[NoticeManager] UI references are null! collectUI: {collectUI}, _collectText: {_collectText}, _collectBottomText: {_collectBottomText}");
            return;
        }
        
        if (debugLogging) Debug.Log("[NoticeManager] All UI references valid. Proceeding to show notice.");;
        
        // Always replace the current notice when a new collect event arrives.
        CancelCurrentCollectNotice();
        currentCollectPriority = priority;
        ShowCollectableUIWithTyping(noticeText, bottomText, fadeDuration, displayDuration, DefaultTypeSpeed, false, priority);
    }
    

    internal void HideCollectUI()
    {
        if (collectUI != null)
            collectUI.SetActive(false);
        if (_collectText != null)            
            _collectText.gameObject.SetActive(false);
        if (_collectBottomText != null)           
             _collectBottomText.gameObject.SetActive(false);
    }

    internal void CancelCurrentCollectNotice(bool turnOffUI = false)
    {
        if (collectableUICoroutine != null)
        {
            StopCoroutine(collectableUICoroutine);
            collectableUICoroutine = null;
        }

        if (_collectText is TextMeshProUGUI collectTextMesh)
            WritingTextUI.RemoveWriter_Static(collectTextMesh);

        if (_collectBottomText is TextMeshProUGUI collectBottomTextMesh)
            WritingTextUI.RemoveWriter_Static(collectBottomTextMesh);

        ClearNotice();

        if (turnOffUI && collectUI != null)
            collectUI.SetActive(false);
    }

    internal void ClearNotice()
    {
        if (_collectText != null)
            _collectText.text = "";

        if (_collectBottomText != null)
            _collectBottomText.text = "";
    }

    
    internal void ForceStopNoticeCoroutines()
    {
        CancelCurrentCollectNotice();

        if (_collectBottomText != null)
            _collectBottomText.gameObject.SetActive(false);

        if (_collectText != null)
            _collectText.gameObject.SetActive(false);

        if (collectUI != null)
            collectUI.SetActive(false);

    }

    // Legacy method to write text directly without typing effect, if needed
    private void WriteTextToCollectUI(string text, string bottomText)
    {
        if (_collectText != null)
            _collectText.text = text;

        if (_collectBottomText != null)
            _collectBottomText.text = bottomText;

    }
    private void ShowCollectableUIWithTyping(string collectedLabel, string bottomFlavorText, float fadeDuration = 0.5f, float displayDuration = 1.5f, float typeSpeed = 0.03f, bool invisibleCharacters = false, int priority = 0)
    {
        if (debugLogging) Debug.Log($"[NoticeManager] ShowCollectableUIWithTyping called. collectUI: {collectUI}, _collectText: {_collectText}, _collectBottomText: {_collectBottomText}");
        
        // Ensure the InteractionUI GameObject is active before starting a coroutine
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);
        CancelCurrentCollectNotice();
        collectableUICoroutine = StartCoroutine(FadeInTypeFadeOutRoutine(collectedLabel, bottomFlavorText, fadeDuration, displayDuration, typeSpeed, invisibleCharacters, priority));
        if (debugLogging) Debug.Log($"[NoticeManager] FadeInTypeFadeOutRoutine coroutine started.");
    }
    // Fades in the collect UI and text
    private IEnumerator FadeInUI(float fadeDuration)
    {
        if (collectUI == null)
        {
            Debug.LogError("[NoticeManager] collectUI is null. Cannot fade in.");
            yield break;
        }

        CanvasGroup canvasGroup = collectUI.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = collectUI.AddComponent<CanvasGroup>();
        
        canvasGroup.alpha = 0f;
        collectUI.SetActive(true);

        if (fadeDuration <= 0f)
        {
            canvasGroup.alpha = 1f;
            yield break;
        }

        // Fade in collectUI background first
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
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

        if (fadeDuration <= 0f)
        {
            canvasGroup.alpha = 0f;
            collectUI.SetActive(false);
            fadeOutComplete = true;
            yield break;
        }

        float startAlpha = canvasGroup.alpha;
        if (startAlpha <= 0f)
            startAlpha = 1f;

        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            if (debugLogging) Debug.Log("Fading out collect UI... Elapsed time: " + elapsedTime.ToString("F2") + "s");
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsedTime / fadeDuration));
            if (canvasGroup != null)
                canvasGroup.alpha = alpha;
            yield return null;
        }

        canvasGroup.alpha = 0f;
        if (collectUI != null)
            collectUI.SetActive(false);
        fadeOutComplete = true;
    }

    // Coroutine to fade in, then type, then fade out
    private IEnumerator FadeInTypeFadeOutRoutine(string collectedLabel, string bottomFlavorText, float fadeDuration, float displayDuration, float typeSpeed, bool invisibleCharacters, int priority)
    {
        if (debugLogging) Debug.Log($"[NoticeManager] FadeInTypeFadeOutRoutine started. collectUI: {collectUI}, _collectText: {_collectText}, _collectBottomText: {_collectBottomText}");
        
        // Validate UI references
        if (collectUI == null || _collectText == null || _collectBottomText == null)
        {
            Debug.LogError($"[NoticeManager] Cannot start FadeInTypeFadeOutRoutine - UI references are null. collectUI: {collectUI}, _collectText: {_collectText}, _collectBottomText: {_collectBottomText}");
            yield break;
        }
        
        if (collectUI != null)
            collectUI.SetActive(true);

        _collectText.text = "";
        _collectBottomText.text = "";

        _collectBottomText.gameObject.SetActive(true);
        _collectText.gameObject.SetActive(true);

        // Fade in first, but cap pre-typing delay so notices feel responsive.
        float fadeInDuration = Mathf.Min(fadeDuration, MaxFadeInBeforeTyping);
        yield return StartCoroutine(FadeInUI(fadeInDuration));

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

        // Fade out (clamped so it never feels instant)
        float fadeOutDuration = Mathf.Max(fadeDuration, MinFadeOutDuration);
        yield return StartCoroutine(FadeOutUI(fadeOutDuration));
        // Only reset priority if this is the current one
        if (priority == currentCollectPriority)
            currentCollectPriority = 0;
    }
}
