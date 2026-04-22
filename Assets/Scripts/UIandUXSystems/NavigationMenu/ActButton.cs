using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    private Image mapLocationImageRenderer;
    private bool isHovered;
    private bool isSelected;

    private Button thisButton;

    private void Awake()
    {
        if (mapLocationImage != null)
        {
            for (int i = 0; i < mapLocationImage.Length; i++)
            {
                if (mapLocationImage[i] != null)
                {
                    mapLocationImageRenderer = mapLocationImage[i].GetComponent<Image>();
                    if (mapLocationImageRenderer != null)
                    {
                        defaultMapImageColor = mapLocationImageRenderer.color;
                    }
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
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
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
        if (mapLocationImageRenderer == null)
            return;

        if (!thisButton.interactable)
            return;

        if (isSelected)
            FadeToHighlightedColor();
        else if (isHovered)
            FadeToHighlightedColor();
        else
            FadeToDefaultColor();
    }

    private void FadeToDefaultColor()
    {
        StartCoroutine(FadeToDefaultCoroutine(0.5f));
    }

    private void FadeToHighlightedColor()
    {
        StartCoroutine(FadeToHighlightedColor(0.5f));
        StartCoroutine(PulseColorWhileHighlighted(1f));
    }

    private IEnumerator FadeToHighlightedColor(float duration)
    {
        if (mapLocationImageRenderer == null)
        {
            Debug.LogWarning("[ActButton] Cannot fade to highlighted color because mapLocationImageRenderer is not assigned.");
            yield break;
        }
            

        Color startColor = mapLocationImageRenderer.color;
        Color targetColor = isSelected ? selectedMapImageColor : hoverMapImageColor;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            mapLocationImageRenderer.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        mapLocationImageRenderer.color = targetColor;
        Debug.Log(mapLocationImageRenderer.color);
    }

    private IEnumerator FadeToDefaultCoroutine(float duration)
    {
        if (mapLocationImageRenderer == null)
        {
            Debug.LogWarning("[ActButton] Cannot fade to default color because mapLocationImageRenderer is not assigned.");
            yield break;
        }

        Color startColor = mapLocationImageRenderer.color;
        Color targetColor = defaultMapImageColor;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            mapLocationImageRenderer.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        mapLocationImageRenderer.color = targetColor;
    }

    private IEnumerator PulseColorWhileHighlighted(float pulseDuration)
    {
        if (mapLocationImageRenderer == null)
        {
            Debug.LogWarning("[ActButton] Cannot pulse color because mapLocationImageRenderer is not assigned.");
            yield break;
        }

        float pulseElapsedTime = 0f;

        while (isHovered || isSelected)
        {
            pulseElapsedTime += Time.unscaledDeltaTime;
            float pulseT = (Mathf.Sin(pulseElapsedTime / pulseDuration * Mathf.PI * 2) + 1f) / 2f; // Oscillates between 0 and 1
            Color targetColor = Color.Lerp(isSelected ? selectedMapImageColor : hoverMapImageColor, defaultMapImageColor, pulseT);
            mapLocationImageRenderer.color = targetColor;
            yield return null;
        }
    }
    
}
