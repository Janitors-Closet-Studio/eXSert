using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

/*
    Written by Brandon Wahl

    This script will handle the functionality of the act buttons in the navigation menu
    In the future, it will send players back to previous completed acts
*/

public class ActButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private int actNumber = 0; //0-4 

    [SerializeField] private GameObject[] mapLocationImage = null;

    [SerializeField] private string sceneName;

    [SerializeField] private Color defaultMapImageColor = Color.grey;
    [SerializeField] private Color hoverMapImageColor;
    [SerializeField] private Color selectedMapImageColor;

    private Image highlightedMapLocationImageRenderer;
    private bool isHovered;
    private bool isSelected;

    private Button thisButton;
    private ActsManager actsManager;
    private Coroutine activeColorRoutine;

    private void Awake()
    {
        actsManager = ActsManager.Instance;
        ApplyActsManagerColorsIfAvailable();

        if (mapLocationImage != null)
        {
            for (int i = 0; i < mapLocationImage.Length; i++)
            {
                if (mapLocationImage[i] != null)
                {
                    Image targetRenderer = ResolveTargetImageRenderer(mapLocationImage[i]);
                    if (targetRenderer == null)
                        continue;

                    if (highlightedMapLocationImageRenderer == null)
                        highlightedMapLocationImageRenderer = targetRenderer;

                    if (actsManager == null)
                        defaultMapImageColor = targetRenderer.color;

                    if (highlightedMapLocationImageRenderer != null)
                        break;
                }
            }
        }

        thisButton = GetComponent<Button>();

        hoverMapImageColor.a = 1f;
        selectedMapImageColor.a = 1f;

        if (actNumber == 0)
        {
            for (int i = 0; i < mapLocationImage.Length; i++)
            {
                if (mapLocationImage[i] != null)
                {
                    mapLocationImage[i].SetActive(true);
                }
            }
        }
        else
        {
            for (int i = 0; i < mapLocationImage.Length; i++)
            {
                if (mapLocationImage[i] != null)
                {
                    mapLocationImage[i].SetActive(false);
                }
            }
        }
    }

    private void OnEnable()
    {
        ApplyActsManagerColorsIfAvailable();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        StopActiveColorRoutine();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string sceneName = scene.name;
        if (sceneName == this.sceneName)
        {
            for (int i = 0; i < mapLocationImage.Length; i++)
            {
                if (mapLocationImage[i] != null)
                {
                    mapLocationImage[i].SetActive(true);
                }
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        UpdateMapLocationImageColor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        UpdateMapLocationImageColor();
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        UpdateMapLocationImageColor();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        UpdateMapLocationImageColor();
    }

    private void UpdateMapLocationImageColor()
    {
        if (highlightedMapLocationImageRenderer == null)
            return;

        if (!thisButton.interactable)
            return;

        StopActiveColorRoutine();

        if (isSelected)
            FadeToHighlightedColor();
        else if (isHovered)
            FadeToHighlightedColor();
        else
            FadeToDefaultColor();
    }

    private void FadeToDefaultColor()
    {
        activeColorRoutine = StartCoroutine(FadeToDefaultCoroutine(0.5f));
    }

    private void FadeToHighlightedColor()
    {
        activeColorRoutine = StartCoroutine(FadeAndPulseToHighlightedColor(0.5f, 1f));
    }

    private IEnumerator FadeAndPulseToHighlightedColor(float fadeDuration, float pulseDuration)
    {
        if (highlightedMapLocationImageRenderer == null)
        {
            Debug.LogWarning("[ActButton] Cannot fade to highlighted color because no highlighted map location image renderer is assigned.");
            yield break;
        }

        Color startColor = highlightedMapLocationImageRenderer.color;
        Color targetColor = isSelected ? selectedMapImageColor : hoverMapImageColor;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / fadeDuration);
            highlightedMapLocationImageRenderer.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        highlightedMapLocationImageRenderer.color = targetColor;

        float pulseElapsedTime = 0f;
        while (isHovered || isSelected)
        {
            pulseElapsedTime += Time.unscaledDeltaTime;
            float pulseT = (Mathf.Sin(pulseElapsedTime / pulseDuration * Mathf.PI * 2) + 1f) / 2f;
            Color pulseColor = Color.Lerp(isSelected ? selectedMapImageColor : hoverMapImageColor, defaultMapImageColor, pulseT);
            highlightedMapLocationImageRenderer.color = pulseColor;
            yield return null;
        }

        activeColorRoutine = null;
    }

    private IEnumerator FadeToDefaultCoroutine(float duration)
    {
        if (highlightedMapLocationImageRenderer == null)
        {
            Debug.LogWarning("[ActButton] Cannot fade to default color because no highlighted map location image renderer is assigned.");
            yield break;
        }

        Color startColor = highlightedMapLocationImageRenderer.color;
        Color targetColor = defaultMapImageColor;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            highlightedMapLocationImageRenderer.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        highlightedMapLocationImageRenderer.color = targetColor;
        activeColorRoutine = null;
    }

    private void ApplyActsManagerColorsIfAvailable()
    {
        if (actsManager == null)
            actsManager = ActsManager.Instance;

        if (actsManager == null)
            return;

        defaultMapImageColor = actsManager.DefaultColor;
        hoverMapImageColor = actsManager.HighlightColor;
        selectedMapImageColor = actsManager.SelectedColor;

        hoverMapImageColor.a = 1f;
        selectedMapImageColor.a = 1f;
    }

    private Image ResolveTargetImageRenderer(GameObject locationRoot)
    {
        if (locationRoot == null)
            return null;

        Image[] images = locationRoot.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image == null)
                continue;

            if (image.gameObject.name.EndsWith("_Image", System.StringComparison.Ordinal))
                return image;
        }

        return null;
    }

    private void StopActiveColorRoutine()
    {
        if (activeColorRoutine == null)
            return;

        StopCoroutine(activeColorRoutine);
        activeColorRoutine = null;
    }
}
