/*
    Controls the settings that involve graphics

    written by Brandon Wahl
*/
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class GraphicsSettings : MonoBehaviour
{
    private const string MasterBrightnessKey = "masterBrightness";
    private const string MasterBrightnessDefaultKey = "masterBrightnessDefault";

    [Header("Graphics Settings Container Reference")]
    [SerializeField] private GameObject graphicsSettingsContainer;

    [Space(20)]

    [Header("Quality Settings")]
    [SerializeField] private TMP_Text qualityText;
    private int qualityLevel;


    [Header("Brightness Settings")]
    [SerializeField] private Slider brightnessSlider;
    public float defaultBrightness = 0.5f;

    [SerializeField]
    [Tooltip("Assign the shared post-process volume profile here. Brightness uses its LiftGammaGain override just like PauseManager uses a VolumeProfile.")]
    private VolumeProfile brightnessVolumeProfile;

    [SerializeField]
    [Tooltip("Gamma value when the brightness slider is at its minimum.")]
    private float minBrightnessGamma = 0.5f;

    [SerializeField]
    [Tooltip("Gamma value when the brightness slider is at its maximum.")]
    private float maxBrightnessGamma = 1.5f;

    [SerializeField]
    [Tooltip("Neutral brightness value that should appear as 1.0 in the LiftGammaGain inspector.")]
    private float neutralBrightnessGamma = 1f;

    [SerializeField]
    [Tooltip("Lowest slider value for screen brightness.")]
    private float minBrightnessSliderValue = 0f;

    [SerializeField]
    [Tooltip("Highest slider value for screen brightness.")]
    private float maxBrightnessSliderValue = 1f;

    internal LiftGammaGain liftGammaGain;
    internal float brightnessLevel;

    [Header("Display Mode Settings")]
    [SerializeField] private TMP_Text displayModeText;
    private bool isFullscreen;
    private int displayModeLevel;

    [Header("FPS Mode Settings")]
    [SerializeField] private int frameRate = 60;
    [SerializeField] private TMP_Text fpsText;
    private int fpsLevel;

    [Header("Resolution Mode Settings")]
    [SerializeField] private TMP_Text resolutionText;
    private bool isResolution1920x1080 = true;

    [Header("Camera Shake Settings")]
    [SerializeField] private TMP_Text cameraShakeText;
    private bool isCameraShake;

    [Space(20)]

    [SerializeField] private InputActionReference _applyAction;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;

        FindBrightnessProfile();

        ConfigureBrightnessSlider();

        brightnessLevel = GetStartupBrightness();
        if (brightnessSlider != null)
            brightnessSlider.value = brightnessLevel;

        SetBrightness(brightnessLevel);

        fpsLevel = PlayerPrefs.GetInt("masterFPS", 60);
        SetFPS(fpsLevel);

        displayModeLevel = PlayerPrefs.GetInt("masterFullscreen", 0);
        SetDisplayMode(displayModeLevel);

        isCameraShake = PlayerPrefs.GetInt("masterCameraShake", 1) == 1;
        SetCameraShake(isCameraShake);

        isResolution1920x1080 = PlayerPrefs.GetInt("masterResolution", 0) == 0;
        SetResolution(isResolution1920x1080 ? "1920x1080" : "2560x1440");

        qualityLevel = PlayerPrefs.GetInt("masterQuality", 1);
        SetQuality(qualityLevel);

        GraphicsApply();
    }

    private void OnEnable()
    {
        if (_applyAction != null && _applyAction.action != null)
            _applyAction.action.performed += OnApplyPerformed;

        if (brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.RemoveListener(SetBrightness);
            brightnessSlider.onValueChanged.AddListener(SetBrightness);
        }
    }

    private void OnDisable()
    {
        if (_applyAction != null && _applyAction.action != null)
            _applyAction.action.performed -= OnApplyPerformed;

        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.RemoveListener(SetBrightness);

        SaveCurrentFPSSetting();
    }

    private void OnApplyPerformed(InputAction.CallbackContext context)
    {
        GraphicsApply();
    }

    public void SaveCurrentFPSSetting()
    {
        PlayerPrefs.SetInt("masterFPS", fpsLevel);
        PlayerPrefs.Save();
        ApplyRuntimeFPSSetting(fpsLevel);
        Debug.Log(
            $"[GraphicsSettings] SaveCurrentFPSSetting: fpsLevel={fpsLevel}, targetFrameRate={Application.targetFrameRate}, vSyncCount={QualitySettings.vSyncCount}"
        );
    }

    private void ApplyRuntimeFPSSetting(int appliedFrameRate)
    {
        Application.targetFrameRate = appliedFrameRate;
        FindFirstObjectByType<StrictFrameLimiter>()?.UpdateTargetFPS(appliedFrameRate);
    }

    private void FindBrightnessProfile()
    {
        if (brightnessVolumeProfile == null)
        {
            Debug.LogError("Brightness Volume Profile not assigned.");
            return;
        }

        if (!brightnessVolumeProfile.TryGet(out liftGammaGain))
        {
            liftGammaGain = brightnessVolumeProfile.Add<LiftGammaGain>(true);
            Debug.Log("LiftGammaGain was missing and has been added to the brightness volume profile.");
        }

        liftGammaGain.gamma.overrideState = true;
        liftGammaGain.active = true;
        Debug.Log(
            "LiftGammaGain ready. Current gamma alpha: " + liftGammaGain.gamma.value.w
        );
    }

    public void SetQuality(int quality)
    {
        qualityLevel = quality;

        if (quality == 0)
        {
            QualitySettings.SetQualityLevel(0);
            qualityText.text = "Low";
            return;
        }

        if (quality == 1)
        {
            QualitySettings.SetQualityLevel(1);
            qualityText.text = "Medium";
            return;
        }

        if (quality == 2)
        {
             QualitySettings.SetQualityLevel(2);
            qualityText.text = "High";
            return;
        }
    }
    public void SetBrightness(float brightness)
    {
        if (brightnessVolumeProfile == null)
        {
            Debug.LogWarning(
                "[GraphicsSettings] Could not apply brightness: no VolumeProfile is assigned."
            );
            return;
        }

        if (liftGammaGain == null)
            FindBrightnessProfile();

        if (liftGammaGain != null)
        {
            float clampedBrightness = ClampBrightnessValue(brightness);
            float gammaValue = GetGammaValueForBrightness(clampedBrightness);
            float gammaAlpha = GetGammaAlphaForBrightness(gammaValue);
            liftGammaGain.gamma.Override(new Vector4(1f, 1f, 1f, gammaAlpha));
            liftGammaGain.gamma.overrideState = true;
            liftGammaGain.active = true;
            brightnessLevel = clampedBrightness;

            if (brightnessSlider != null && !Mathf.Approximately(brightnessSlider.value, clampedBrightness))
                brightnessSlider.SetValueWithoutNotify(clampedBrightness);

            DebugLogSettingsM.ConditionalLog(
                DebugLogCategory.Settings,
                $"Brightness slider: {clampedBrightness} -> gamma {gammaValue} -> gamma alpha {liftGammaGain.gamma.value.w}"
            );
            return;
        }

        Debug.LogWarning(
            "[GraphicsSettings] Could not apply brightness: LiftGammaGain could not be initialized from the assigned VolumeProfile."
        );
    }

    public float ClampBrightnessValue(float brightness)
    {
        return Mathf.Clamp(brightness, minBrightnessSliderValue, maxBrightnessSliderValue);
    }

    private float GetStartupBrightness()
    {
        float clampedDefaultBrightness = ClampBrightnessValue(defaultBrightness);
        float storedDefaultBrightness = PlayerPrefs.GetFloat(
            MasterBrightnessDefaultKey,
            clampedDefaultBrightness
        );

        bool shouldReseedBrightness =
            !PlayerPrefs.HasKey(MasterBrightnessKey)
            || !Mathf.Approximately(storedDefaultBrightness, clampedDefaultBrightness);

        float startupBrightness = shouldReseedBrightness
            ? clampedDefaultBrightness
            : PlayerPrefs.GetFloat(MasterBrightnessKey, clampedDefaultBrightness);

        startupBrightness = ClampBrightnessValue(startupBrightness);

        PlayerPrefs.SetFloat(MasterBrightnessDefaultKey, clampedDefaultBrightness);
        PlayerPrefs.SetFloat(MasterBrightnessKey, startupBrightness);
        PlayerPrefs.Save();

        return startupBrightness;
    }

    private float GetGammaValueForBrightness(float brightness)
    {
        float normalizedBrightness = Mathf.InverseLerp(
            minBrightnessSliderValue,
            maxBrightnessSliderValue,
            brightness
        );

        return Mathf.Lerp(minBrightnessGamma, maxBrightnessGamma, normalizedBrightness);
    }

    private float GetGammaAlphaForBrightness(float gammaValue)
    {
        return gammaValue - neutralBrightnessGamma;
    }

    private void ConfigureBrightnessSlider()
    {
        if (brightnessSlider == null)
            return;

        brightnessSlider.minValue = minBrightnessSliderValue;
        brightnessSlider.maxValue = maxBrightnessSliderValue;
        brightnessSlider.wholeNumbers = false;

        defaultBrightness = ClampBrightnessValue(defaultBrightness);
    }

    public void SetDisplayMode(int displayMode)
    {
        displayModeLevel = displayMode;

        if (displayMode == 0)
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            displayModeText.text = "Fullscreen";
            isFullscreen = true;
        }
        else if (displayMode == 1)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            displayModeText.text = "Windowed";
            isFullscreen = false;
        }
        else if (displayMode == 2)
        {
            Screen.SetResolution(
                Screen.currentResolution.width,
                Screen.currentResolution.height,
                FullScreenMode.FullScreenWindow
            );
            displayModeText.text = "Borderless";
            isFullscreen = false;
        }
    }

    public void SetResolution(string resolution)
    {
        if (resolution == "1920x1080")
        {
            resolutionText.text = "1920 x 1080";
            Screen.SetResolution(1920, 1080, isFullscreen);
            isResolution1920x1080 = true;
            return;
        }

        resolutionText.text = "2560 x 1440";
        Screen.SetResolution(2560, 1440, isFullscreen);
        isResolution1920x1080 = false;
    }

    public void SetCameraShake(bool cameraShake)
    {
        isCameraShake = cameraShake;

        if (cameraShake)
        {
            cameraShakeText.text = "On";
            SettingsManager.Instance.cameraShake = true;
            return;
        }

        cameraShakeText.text = "Off";
        SettingsManager.Instance.cameraShake = false;
    }

    public void SetFPS(int framerate)
    {
        int appliedFrameRate =
            framerate == 60 ? 60
            : framerate == 30 ? 30
            : -1;

        fpsLevel = appliedFrameRate;

        if (framerate == 60)
        {
            fpsText.text = "60";
            Application.targetFrameRate = appliedFrameRate;
        }
        else if (framerate == 30)
        {
            fpsText.text = "30";
            Application.targetFrameRate = appliedFrameRate;
        }
        else
        {
            fpsText.text = "Unlimited";
            Application.targetFrameRate = appliedFrameRate;
        }

        ApplyRuntimeFPSSetting(appliedFrameRate);
        Debug.Log(
            $"[GraphicsSettings] SetFPS called: framerate={framerate}, fpsLevel={fpsLevel}, targetFrameRate={Application.targetFrameRate}, vSyncCount={QualitySettings.vSyncCount}"
        );
    }

    public void GraphicsApply()
    {
        PlayerPrefs.SetInt("masterQuality", qualityLevel);
        PlayerPrefs.SetFloat("masterBrightness", brightnessLevel);
        PlayerPrefs.SetInt("masterFPS", fpsLevel);
        ApplyRuntimeFPSSetting(fpsLevel);
        PlayerPrefs.SetInt("masterFullscreen", displayModeLevel);
        PlayerPrefs.SetInt("masterCameraShake", isCameraShake ? 1 : 0);
        PlayerPrefs.SetInt("masterResolution", isResolution1920x1080 ? 0 : 1);
        PlayerPrefs.Save();
    }

    public void ResetButton()
    {
        if (brightnessSlider != null)
            brightnessSlider.value = defaultBrightness;

        SetBrightness(defaultBrightness);
        ApplyRuntimeFPSSetting(60);
        fpsText.text = "60";
        fpsLevel = 60;

        isCameraShake = true;
        cameraShakeText.text = "On";

        isResolution1920x1080 = true;
        resolutionText.text = "1920 x 1080";

        SetDisplayMode(0);
        displayModeText.text = "Fullscreen";
        displayModeLevel = 0;

        SetQuality(1);
        qualityText.text = "Medium";
        qualityLevel = 1;
        GraphicsApply();
    }
}
