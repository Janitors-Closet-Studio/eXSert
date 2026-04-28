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
    [SerializeField] private float brightnessMin = 0f;
    [SerializeField] private float brightnessMax = 1f;
    [SerializeField] private RectTransform hoverTargetOverride;
    [SerializeField] private float gearRotationDegreesPerSecond = 900f;
    private float _previousSliderValue;
    private bool isMainMenuScene;
    private bool visualsVisible;
    private bool isSelected;
    private bool isHovered;
    private bool hasExternalSelection;
    private RectTransform sliderRect;
    private RectTransform parentRowRect;
    private int hoverSourceCount;
    private int selectionSourceCount;
    private float pendingRotationDegrees;
    private Coroutine rotationRoutine;
    private Coroutine darkestFadeRoutine;
    private Coroutine midFadeRoutine;
    private Coroutine brightestFadeRoutine;
    private UIHoverRelay sliderHoverRelay;
    private UIHoverRelay parentHoverRelay;
    private bool initialized;

    private void Start() 
    {
        InitializeIfNeeded();
        RefreshFromCurrentBrightness();
    }

    private void OnEnable()
    {
        InitializeIfNeeded();

        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.AddListener(UpdateBrightnessInfographic);

        AttachHoverRelay(sliderRect, ref sliderHoverRelay);
        if (parentRowRect != sliderRect)
            AttachHoverRelay(parentRowRect, ref parentHoverRelay);

        RefreshFromCurrentBrightness();
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        isMainMenuScene = SceneManager.GetActiveScene().name == "MainMenu";
        sliderRect = brightnessSlider != null ? brightnessSlider.transform as RectTransform : null;
        parentRowRect = hoverTargetOverride != null
            ? hoverTargetOverride
            : (brightnessSlider != null ? brightnessSlider.transform.parent as RectTransform : null);

        initialized = true;
    }

    private void RefreshFromCurrentBrightness()
    {
        float currentBrightness = brightnessSlider != null ? brightnessSlider.value : PlayerPrefs.GetFloat("masterBrightness", 0.5f);
        _previousSliderValue = currentBrightness;
        pendingRotationDegrees = 0f;
        InitializeGearVisuals(currentBrightness);

        bool shouldShow = isSelected || isHovered || hasExternalSelection;
        SetVisualsVisible(shouldShow, 0f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHoverSourceState(true);
    }


    public void OnPointerExit(PointerEventData eventData)
    {
        SetHoverSourceState(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        SetVisualsVisible(true, 0.25f);

        if (isMainMenuScene)
            FadeOutTopMenuIfItIsASubMenu();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        SetVisualsVisible(isHovered || hasExternalSelection, 0.25f);
    }

    private void OnDisable()
    {
        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.RemoveListener(UpdateBrightnessInfographic);

        DetachHoverRelay(ref sliderHoverRelay);
        DetachHoverRelay(ref parentHoverRelay);

        hoverSourceCount = 0;
        selectionSourceCount = 0;
        isHovered = false;
        hasExternalSelection = false;
        pendingRotationDegrees = 0f;

        if (rotationRoutine != null)
        {
            StopCoroutine(rotationRoutine);
            rotationRoutine = null;
        }

        StopVisualCoroutines();
    }

    private void AttachHoverRelay(RectTransform target, ref UIHoverRelay relay)
    {
        if (target == null)
            return;

        relay = target.GetComponent<UIHoverRelay>();
        if (relay == null)
            relay = target.gameObject.AddComponent<UIHoverRelay>();

        relay.HoverChanged -= OnExternalHoverChanged;
        relay.HoverChanged += OnExternalHoverChanged;
        relay.SelectionChanged -= OnExternalSelectionChanged;
        relay.SelectionChanged += OnExternalSelectionChanged;
    }

    private void DetachHoverRelay(ref UIHoverRelay relay)
    {
        if (relay == null)
            return;

        relay.HoverChanged -= OnExternalHoverChanged;
        relay.SelectionChanged -= OnExternalSelectionChanged;
        relay = null;
    }

    private void OnExternalHoverChanged(bool isEntered)
    {
        SetHoverSourceState(isEntered);
    }

    private void OnExternalSelectionChanged(bool isSelectedNow)
    {
        if (isSelectedNow)
            selectionSourceCount++;
        else
            selectionSourceCount = Mathf.Max(0, selectionSourceCount - 1);

        bool selectedNow = selectionSourceCount > 0;
        if (hasExternalSelection == selectedNow)
            return;

        hasExternalSelection = selectedNow;

        if (hasExternalSelection)
        {
            SetVisualsVisible(true, 0.25f);
            if (isMainMenuScene)
                FadeOutTopMenuIfItIsASubMenu();
            return;
        }

        if (!isSelected && !isHovered)
            SetVisualsVisible(false, 0.25f);
    }

    private void SetHoverSourceState(bool isEntered)
    {
        if (isEntered)
            hoverSourceCount++;
        else
            hoverSourceCount = Mathf.Max(0, hoverSourceCount - 1);

        bool shouldBeHovered = hoverSourceCount > 0;
        if (isHovered == shouldBeHovered)
            return;

        isHovered = shouldBeHovered;
        ApplyHoverState(isHovered);
    }

    private void ApplyHoverState(bool hovered)
    {
        if (hovered)
        {
            SetVisualsVisible(true, 0.25f);
            if (isMainMenuScene)
                FadeOutTopMenuIfItIsASubMenu();
            return;
        }

        if (!isSelected && !hasExternalSelection)
            SetVisualsVisible(false, 0.25f);
    }

    private void SetVisualsVisible(bool visible, float duration)
    {
        if (visualsVisible == visible)
            return;

        visualsVisible = visible;

        if (visible)
            FadeInAllImages(duration);
        else
            FadeOutAllImages(duration);
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
        Vector3 initialScale = image.transform.localScale;
        Vector3 targetScale = Vector3.one;
        Vector3 initialRotation = image.transform.rotation.eulerAngles;
        Vector3 targetRotation = initialRotation + new Vector3(0f, 0f, 360f);


        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
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
        Vector3 initialScale = image.transform.localScale;
        Vector3 targetScale = Vector3.zero;
        Vector3 initialRotation = image.transform.rotation.eulerAngles;
        Vector3 targetRotation = initialRotation + new Vector3(0f, 0f, 360f);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            image.transform.localScale = Vector3.Lerp(initialScale, targetScale, t);
            image.transform.rotation = Quaternion.Euler(Vector3.Lerp(initialRotation, targetRotation, t * 0.1f)); // Rotate at half 
            yield return null;
        }

        image.transform.localScale = targetScale;
    }

    private void FadeOutAllImages(float duration)
    {
        StopVisualCoroutines();

        if (darkestImage != null)
            darkestFadeRoutine = StartCoroutine(FadeOutScaleForImage(darkestImage, duration));

        if (midBrightnessImage != null)
            midFadeRoutine = StartCoroutine(FadeOutScaleForImage(midBrightnessImage, duration));

        if (brightestImage != null)
            brightestFadeRoutine = StartCoroutine(FadeOutScaleForImage(brightestImage, duration));
    }


    private void FadeInAllImages(float duration)
    {
        StopVisualCoroutines();

        if (darkestImage != null)
            darkestFadeRoutine = StartCoroutine(FadeInScaleForImage(darkestImage, duration));

        if (midBrightnessImage != null)
            midFadeRoutine = StartCoroutine(FadeInScaleForImage(midBrightnessImage, duration));

        if (brightestImage != null)
            brightestFadeRoutine = StartCoroutine(FadeInScaleForImage(brightestImage, duration));
    }

    private void StopVisualCoroutines()
    {
        if (darkestFadeRoutine != null)
        {
            StopCoroutine(darkestFadeRoutine);
            darkestFadeRoutine = null;
        }

        if (midFadeRoutine != null)
        {
            StopCoroutine(midFadeRoutine);
            midFadeRoutine = null;
        }

        if (brightestFadeRoutine != null)
        {
            StopCoroutine(brightestFadeRoutine);
            brightestFadeRoutine = null;
        }
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

        visualsVisible = true;
        SetVisualsVisible(false, 0f); // Start with all images invisible
    }

    private void UpdateBrightnessInfographic(float value)
    {
        float normalizedBrightness = NormalizeBrightness(value);

        if (darkestImage != null) darkestImage.color = BuildColorWithAlpha(1f - normalizedBrightness);
        if (midBrightnessImage != null) midBrightnessImage.color = BuildColorWithAlpha(1f - Mathf.Abs(0.5f - normalizedBrightness) * 2f);
        if (brightestImage != null) brightestImage.color = BuildColorWithAlpha(normalizedBrightness);

        float delta = value - _previousSliderValue;
        float rotationAmount = delta * 360f; // Adjust 360 to change sensitivity

        pendingRotationDegrees += -rotationAmount;
        if (rotationRoutine == null)
            rotationRoutine = StartCoroutine(ApplyPendingRotation());


        _previousSliderValue = value;
    }

    private IEnumerator ApplyPendingRotation()
    {
        while (Mathf.Abs(pendingRotationDegrees) > 0.01f)
        {
            float maxStep = Mathf.Max(60f, gearRotationDegreesPerSecond) * Time.unscaledDeltaTime;
            float step = Mathf.Clamp(pendingRotationDegrees, -maxStep, maxStep);
            pendingRotationDegrees -= step;

            RotateGearByStep(darkestImage, step);
            RotateGearByStep(midBrightnessImage, step);
            RotateGearByStep(brightestImage, step);
            yield return null;
        }

        pendingRotationDegrees = 0f;
        rotationRoutine = null;
    }

    private static void RotateGearByStep(Image img, float step)
    {
        if (img == null)
            return;

        img.transform.Rotate(Vector3.forward, step);
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

public sealed class UIHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public event System.Action<bool> HoverChanged;
    public event System.Action<bool> SelectionChanged;

    public void OnPointerEnter(PointerEventData eventData)
    {
        HoverChanged?.Invoke(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HoverChanged?.Invoke(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        SelectionChanged?.Invoke(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SelectionChanged?.Invoke(false);
    }
}
