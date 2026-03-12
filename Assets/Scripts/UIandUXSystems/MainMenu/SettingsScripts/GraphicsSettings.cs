/*
    Controls the settings that involve graphics

    written by Brandon Wahl
*/
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GraphicsSettings : MonoBehaviour
{

    [Header("Graphics Settings Container Reference")]
    [SerializeField] private GameObject graphicsSettingsContainer;

    [Space(20)]


    [Header("Brightness Settings")]
    [SerializeField] private Slider brightnessSlider = null;
    public float defaultBrightness = 1.25f;

    public Volume globalVolume;
    internal LiftGammaGain liftGammaGain;
    internal float brightnessLevel;
    [SerializeField] private Slider staticSlider = null;
    

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

    [Header("Motion Blur Settings")]
    [SerializeField] private TMP_Text motionBlurText;
    private bool isMotionBlur;

    [Space(20)]

    [SerializeField] private InputActionReference _applyAction;

    void Awake()
    {
        FindGlobalVolume();
    }

    private void OnEnable()
    {
        if (_applyAction != null && _applyAction.action != null)
            _applyAction.action.performed += ctx => GraphicsApply();
    }

    private void OnDisable()
    {
        if (_applyAction != null && _applyAction.action != null)
            _applyAction.action.performed -= ctx => GraphicsApply();
    }

    private void FindGlobalVolume()
    {
        if (globalVolume != null)
        {
            // Try to get the LiftGammaGain override from the volume profile
            if (globalVolume.profile.TryGet(out liftGammaGain))
            {
                Debug.Log("LiftGammaGain found. Current gamma value: " + liftGammaGain.gamma.value.w);
            }
            else
            {
                Debug.LogError("LiftGammaGain effect not found in the volume profile.");
            }
        }
        else
        {
            Debug.LogError("Global Volume not assigned.");
        }
            
    }


    //Alls functions below change values based on player choice
    public void SetBrightness(float brightness)
    {

        if (globalVolume.profile.TryGet(out liftGammaGain))
        {
            liftGammaGain.gamma.value = new Vector4(1f, 1f, 1f, brightness);
            brightnessLevel = brightness;
            Debug.Log("Brightness set to: " + brightness);
        }
    }

    public void SetDisplayMode(int displayMode)
    {
        displayModeLevel = displayMode;

        if (displayMode == 0) // Fullscreen
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            displayModeText.text = "Fullscreen";
            isFullscreen = true;
        }
        else if (displayMode == 1) // Windowed
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            displayModeText.text = "Windowed";
            isFullscreen = false;
        }
        else if (displayMode == 2) // Borderless
        {
            Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
            displayModeText.text = "Borderless";
            isFullscreen = false;
        }
    }

    public void SetMotionBlur(bool motionBlur)
    {
        isMotionBlur = motionBlur;

        if (motionBlur)
        {
            motionBlurText.text = "On";
        }
        else
        {
            motionBlurText.text = "Off";
        }
    }

    public void SetResolution(string resolution)
    {
        if (resolution == "1920x1080")
        {
            resolutionText.text = "1920x1080";
            Screen.SetResolution(1920, 1080, isFullscreen);
            isResolution1920x1080 = true;
        }
        else
        {
            resolutionText.text = "2560x1440";
            Screen.SetResolution(2560, 1440, isFullscreen);
            isResolution1920x1080 = false;
        }
    }

    public void SetCameraShake(bool cameraShake)
    {
        isCameraShake = cameraShake;

        if (cameraShake)
        {
            //Add camera shake logic here
            cameraShakeText.text = "On";

        }
        else
        {
            cameraShakeText.text = "Off";

        }
    }

    public void SetFPS(int framerate)
    {
        QualitySettings.vSyncCount = 0;

        if (framerate == 60)
        {
            fpsText.text = "60";
            Application.targetFrameRate = 60;
        }
        else if (framerate == 30)
        {
            fpsText.text = "30";
            Application.targetFrameRate = 30;
        }
        else
        {
            fpsText.text = "Unlimited";
            Application.targetFrameRate = -1;
        }
    }

    //Applies graphic settings
    public void GraphicsApply()
    {
        PlayerPrefs.SetFloat("masterBrightness", brightnessLevel);

        // Update static/read-only brightness slider to reflect the applied value
        if (staticSlider != null)
            staticSlider.value = brightnessLevel;

        PlayerPrefs.SetInt("masterFPS", fpsLevel);
        Application.targetFrameRate = fpsLevel;

        PlayerPrefs.SetInt("masterMotionBlur", (isMotionBlur ? 1 : 0));

        PlayerPrefs.SetInt("masterFullscreen", displayModeLevel);

        PlayerPrefs.SetInt("masterCameraShake", (isCameraShake ? 1 : 0));

        PlayerPrefs.SetInt("masterResolution", (isResolution1920x1080 ? 0 : 1));

        PlayerPrefs.Save();
    }

    //Resets graphics settings
    public void ResetButton()
    {

        if (brightnessSlider != null)
            brightnessSlider.value = defaultBrightness;
        SetBrightness(defaultBrightness);

        Application.targetFrameRate = 30;
        fpsText.text = "30";
        fpsLevel = 30;

        isMotionBlur = true;
        motionBlurText.text = "On";

        isCameraShake = false;
        cameraShakeText.text = "Off";

        isResolution1920x1080 = true;
        resolutionText.text = "1920x1080";

        SetDisplayMode(0); // Fullscreen
        displayModeText.text = "Fullscreen";  

        GraphicsApply();

    }
}
