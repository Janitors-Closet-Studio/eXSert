using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
public class BrightnessSettingInfographic : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image darkestImage; // Reference to the darkest image
    [SerializeField] private Image midBrightnessImage; // Reference to the mid brightness image
    [SerializeField] private Image brightestImage; // Reference to the brightest image
    [SerializeField] private Slider brightnessSlider; // Reference to the brightness slider\
    [SerializeField] private Color32 infographicBaseColor = new Color32(63, 63, 63, 255);
    [SerializeField] private float brightnessMin = -0.5f;
    [SerializeField] private float brightnessMax = 1f;
    private void Start()
    {
        float initialBrightness = brightnessSlider != null
            ? brightnessSlider.value
            : PlayerPrefs.GetFloat("masterBrightness", 0.5f);

        if (brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.AddListener(UpdateBrightnessInfographic);
        }

        InitializeGearVisuals(initialBrightness);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
       FadeInAllImages(0.25f);
       FadeOutTopMenuIfItIsASubMenu();
    }

    public void OnPointerExit(PointerEventData eventData)
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
        if (darkestImage != null)
            StartCoroutine(FadeOutScaleForImage(darkestImage, duration));

        if (midBrightnessImage != null)
            StartCoroutine(FadeOutScaleForImage(midBrightnessImage, duration));

        if (brightestImage != null)
            StartCoroutine(FadeOutScaleForImage(brightestImage, duration));
    }

    private void FadeInAllImages(float duration)
    {
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
    }

    private void UpdateBrightnessInfographic(float value)
    {
        float normalizedBrightness = NormalizeBrightness(value);


        if (darkestImage != null)
            darkestImage.color = BuildColorWithAlpha(1f - normalizedBrightness);

        if (midBrightnessImage != null)
            midBrightnessImage.color = BuildColorWithAlpha(1f - Mathf.Abs(0.5f - normalizedBrightness) * 2f);

        if (brightestImage != null)
            brightestImage.color = BuildColorWithAlpha(normalizedBrightness);

        StartGearRotationAnimation();
    }

    private void StartGearRotationAnimation()
    {
        StartCoroutine(MoveGearRotInDirectionOfSlider(brightestImage.gameObject));
        StartCoroutine(MoveGearRotInDirectionOfSlider(midBrightnessImage.gameObject));
        StartCoroutine(MoveGearRotInDirectionOfSlider(darkestImage.gameObject));
    }

    private IEnumerator MoveGearRotInDirectionOfSlider(GameObject gear = null)
    {
        float elapsedTime = 0f;
        float duration = 0.5f; // Duration of the movement
        float prevSliderValue = brightnessSlider.value;

        float newSliderValue = brightnessSlider.value;

        Vector3 initialRotation = gear.transform.rotation.eulerAngles;
        Vector3 targetRotation = initialRotation + new Vector3(0f, 0f, 360f); // Rotate based on slider value and direction

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            gear.transform.rotation = Quaternion.Euler(Vector3.Lerp(initialRotation, targetRotation, t * 0.1f));
            yield return null;
        }

        gear.transform.rotation = Quaternion.Euler(targetRotation);
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
