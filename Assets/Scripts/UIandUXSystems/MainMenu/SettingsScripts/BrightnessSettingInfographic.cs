using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.SceneManagement;

public class BrightnessSettingInfographic : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Image darkestImage; // Reference to the darkest image
    [SerializeField] private Image midBrightnessImage; // Reference to the mid brightness image
    [SerializeField] private Image brightestImage; // Reference to the brightest image
    [SerializeField] private Slider brightnessSlider; // Reference to the brightness slider\
    [SerializeField] private Color32 infographicBaseColor = new Color32(63, 63, 63, 255);
    [SerializeField] private float brightnessMin = -0.5f;
    [SerializeField] private float brightnessMax = 1f;
    private float _previousSliderValue;
    private void Start() 
    {
        float initialBrightness = brightnessSlider != null ? brightnessSlider.value : PlayerPrefs.GetFloat("masterBrightness", 0.5f);
        _previousSliderValue = initialBrightness; // Initialize here
    
        if (brightnessSlider != null) 
        {
            brightnessSlider.onValueChanged.AddListener(UpdateBrightnessInfographic);
        }
        InitializeGearVisuals(initialBrightness);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("Pointer entered brightness infographic");
        FadeInAllImages(0.25f);

        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            // In player scene, don't close other menus
            return;
        }
        else
        {
            FadeOutTopMenuIfItIsASubMenu();
        }
    }


    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("Pointer exited brightness infographic");
        FadeOutAllImages(0.25f);
    }

    public void OnSelect(BaseEventData eventData)
    {
        FadeInAllImages(0.25f);

        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            // In player scene, don't close other menus
            return;
        }
        else
        {
            FadeOutTopMenuIfItIsASubMenu();
        }
        {
            FadeOutTopMenuIfItIsASubMenu();
        }
    }

    public void OnDeselect(BaseEventData eventData)
    {
        FadeOutAllImages(0.25f);
    }

    private void FadeOutTopMenuIfItIsASubMenu()
    {
        MenuListManager menuListManager = FindAnyObjectByType<MenuListManager>();
        
        foreach (var menu in menuListManager.settingPageMenus)
        {
            if (!menu.activeInHierarchy)
                continue;
            if(menu != menuListManager.menusToManage[0])
                menuListManager.GoBackToPreviousMenu();
        }
    }

    

    private IEnumerator FadeInScaleForImage(Image image, float duration)
    {
        float elapsedTime = 0f;
        Vector3 initialScale = Vector3.zero;
        Vector3 targetScale = Vector3.one;
        Vector3 initialRotation = image.transform.rotation.eulerAngles;
        Vector3 targetRotation = initialRotation + new Vector3(0f, 0f, 360f);


        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            image.transform.localScale = Vector3.Lerp(initialScale, targetScale, t);
            image.transform.rotation = Quaternion.Euler(Vector3.Lerp(initialRotation, targetRotation, t * 0.1f)); // Rotate at half speed
            yield return null;
        }

        image.transform.localScale = targetScale;
    }

    private IEnumerator FadeOutScaleForImage(Image image, float duration)
    {
        float elapsedTime = 0f;
        Vector3 initialScale = Vector3.one;
        Vector3 targetScale = Vector3.zero;
        Vector3 initialRotation = image.transform.rotation.eulerAngles;
        Vector3 targetRotation = initialRotation + new Vector3(0f, 0f, 360f);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            image.transform.localScale = Vector3.Lerp(initialScale, targetScale, t);
            image.transform.rotation = Quaternion.Euler(Vector3.Lerp(initialRotation, targetRotation, t * 0.1f)); // Rotate at half 
            yield return null;
        }

        image.transform.localScale = targetScale;
    }

    private void FadeOutAllImages(float duration)
    {
        StopAllCoroutines();

        if (darkestImage != null)
            StartCoroutine(FadeOutScaleForImage(darkestImage, duration));

        if (midBrightnessImage != null)
            StartCoroutine(FadeOutScaleForImage(midBrightnessImage, duration));

        if (brightestImage != null)
            StartCoroutine(FadeOutScaleForImage(brightestImage, duration));
    }


    private void FadeInAllImages(float duration)
    {
        StopAllCoroutines(); // Stop any ongoing animations to prevent conflicts

        if (darkestImage != null)
            StartCoroutine(FadeInScaleForImage(darkestImage, duration));

        if (midBrightnessImage != null)
            StartCoroutine(FadeInScaleForImage(midBrightnessImage, duration));

        if (brightestImage != null)
            StartCoroutine(FadeInScaleForImage(brightestImage, duration));
    }

    private void InitializeGearVisuals(float value)
    {
        float normalizedBrightness = NormalizeBrightness(value);

        if (darkestImage != null)
            darkestImage.color = BuildColorWithAlpha(1f - normalizedBrightness);

        if (midBrightnessImage != null)
            midBrightnessImage.color = BuildColorWithAlpha(1f - Mathf.Abs(0.5f - normalizedBrightness) * 2f);

        if (brightestImage != null)
            brightestImage.color = BuildColorWithAlpha(normalizedBrightness);

        FadeOutAllImages(0f); // Start with all images invisible
    }

    private void UpdateBrightnessInfographic(float value)
    {
        float normalizedBrightness = NormalizeBrightness(value);

        if (darkestImage != null) darkestImage.color = BuildColorWithAlpha(1f - normalizedBrightness);
        if (midBrightnessImage != null) midBrightnessImage.color = BuildColorWithAlpha(1f - Mathf.Abs(0.5f - normalizedBrightness) * 2f);
        if (brightestImage != null) brightestImage.color = BuildColorWithAlpha(normalizedBrightness);

        float delta = value - _previousSliderValue;
        float rotationAmount = delta * 360f; // Adjust 360 to change sensitivity

        RotateGear(darkestImage, rotationAmount);
        RotateGear(midBrightnessImage, rotationAmount);
        RotateGear(brightestImage, rotationAmount);


        _previousSliderValue = value;
    }

    private void RotateGear(Image img, float amount) {
        if (img != null) {
            // Rotates the object on the Z axis relative to its current rotation
            img.transform.Rotate(Vector3.forward, amount);
        }
    }

    public void OnSliderMove(float newValue)
    {
        UpdateBrightnessInfographic(newValue);
    }

    private float NormalizeBrightness(float value)
    {
        if (Mathf.Approximately(brightnessMin, brightnessMax))
            return 0f;

        return Mathf.InverseLerp(brightnessMin, brightnessMax, value);
    }

    private Color BuildColorWithAlpha(float alpha)
    {
        Color color = infographicBaseColor;
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
