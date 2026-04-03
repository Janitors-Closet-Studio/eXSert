using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;
public class LoadPrefs : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool canUse = false;
    private AudioSettings sound;
    private GraphicsSettings graphics;
    private GeneralSettings general;
    

    [Header("Audio Settings")]
    [SerializeField] Slider masterVolumeSlider = null;
    [SerializeField] Slider musicVolumeSlider = null;
    [SerializeField] Slider sfxVolumeSlider = null;
    [SerializeField] Slider voiceVolumeSlider = null;

    [Header("Graphics Settings")]
    [SerializeField] private TMP_Text displayModeText = null;
    [SerializeField] private Slider brightnessSlider = null;
    [SerializeField] private float fallbackDefaultBrightness = 0.75f;
    [SerializeField] private TMP_Text resolutionTextValue = null;
    [SerializeField] private TMP_Text motionBlurOnOffText = null;
    [SerializeField] private TMP_Text cameraShakeOnOffText = null;
    [SerializeField] private TMP_Text fpsTextValue = null;

    [Header("General Settings")]
    [SerializeField] private Slider sensSlider = null;
    [SerializeField] private TMP_Text invertYTextValue = null;
    [SerializeField] private TMP_Text comboTextValue = null;
    [SerializeField] private Slider vibrationSlider= null;

    [Space(20), Header("Static Sliders")]
    [SerializeField] private Slider staticBrightnessSlider = null;
    [SerializeField] private Slider staticSensSlider = null;
    [SerializeField] private Slider staticVibrationSlider = null;
    [SerializeField] private Slider staticMasterVolumeSlider = null;
    [SerializeField] private Slider staticMusicVolumeSlider = null;
    [SerializeField] private Slider staticSFXVolumeSlider = null;
    [SerializeField] private Slider staticVoiceVolumeSlider = null;



    [SerializeField] private GameObject settingsManager;


    private void Awake()
    {
        sound = settingsManager.GetComponent<AudioSettings>();
        graphics = settingsManager.GetComponent<GraphicsSettings>();
        general = settingsManager.GetComponent<GeneralSettings>();
    }
#pragma warning restore CS0414

    private void Start()
    {
        if (canUse)
        {
            Debug.Log("[LoadPrefs] Loading Audio Settings...");
            LoadAudioSettings();
            Debug.Log("[LoadPrefs] Loading General Settings...");
            LoadGeneralSettings();
            Debug.Log("[LoadPrefs] Loading Graphics Settings...");
            LoadGraphicsSettings();
        }
        else
        {
            if (sound != null) sound.ResetButton();
            if (graphics != null) graphics.ResetButton();
            if (general != null) general.ResetButton();
        }
    }


    public void LoadAudioSettings()
    {
        if (PlayerPrefs.HasKey("masterVolume"))
        {
            float masterVolume = PlayerPrefs.GetFloat("masterVolume");
            Debug.Log($"[LoadPrefs] Loading masterVolume: {masterVolume}");
            if (masterVolumeSlider) masterVolumeSlider.value = masterVolume;
            if (staticMasterVolumeSlider) staticMasterVolumeSlider.value = masterVolume;
            if (SoundManager.Instance != null && SoundManager.Instance.masterSource)
                SoundManager.Instance.masterSource.volume = masterVolume;
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] masterVolume key not found in PlayerPrefs.");
        }

        if (PlayerPrefs.HasKey("sfxVolume"))
        {
            float sfxVolume = PlayerPrefs.GetFloat("sfxVolume");
            Debug.Log($"[LoadPrefs] Loading sfxVolume: {sfxVolume}");
            if (sfxVolumeSlider) sfxVolumeSlider.value = sfxVolume;
            if (staticSFXVolumeSlider) staticSFXVolumeSlider.value = sfxVolume;
            if (SoundManager.Instance != null && SoundManager.Instance.sfxSource)
                SoundManager.Instance.sfxSource.volume = sfxVolume;
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] sfxVolume key not found in PlayerPrefs.");
        }

        if (PlayerPrefs.HasKey("voiceVolume"))
        {
            float voiceVolume = PlayerPrefs.GetFloat("voiceVolume");
            Debug.Log($"[LoadPrefs] Loading voiceVolume: {voiceVolume}");
            if (voiceVolumeSlider) voiceVolumeSlider.value = voiceVolume;
            if (staticVoiceVolumeSlider) staticVoiceVolumeSlider.value = voiceVolume;
            if (SoundManager.Instance != null && SoundManager.Instance.voiceSource)
                SoundManager.Instance.voiceSource.volume = voiceVolume;
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] voiceVolume key not found in PlayerPrefs.");
        }

        if (PlayerPrefs.HasKey("musicVolume"))
        {
            float musicVolume = PlayerPrefs.GetFloat("musicVolume");
            Debug.Log($"[LoadPrefs] Loading musicVolume: {musicVolume}");
            if (musicVolumeSlider) musicVolumeSlider.value = musicVolume;
            if (staticMusicVolumeSlider) staticMusicVolumeSlider.value = musicVolume;
            if (SoundManager.Instance != null && SoundManager.Instance.musicSource)
                SoundManager.Instance.musicSource.volume = musicVolume;
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] musicVolume key not found in PlayerPrefs.");
        }
    }

    public void LoadGeneralSettings()
    {
        if (PlayerPrefs.HasKey("masterVibrateStrength"))
        {
            float localVibration = PlayerPrefs.GetFloat("masterVibrateStrength");
            Debug.Log($"[LoadPrefs] Loading masterVibrateStrength: {localVibration}");
            if (vibrationSlider) vibrationSlider.value = localVibration;
            if (staticVibrationSlider) staticVibrationSlider.value = localVibration;
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] masterVibrateStrength key not found in PlayerPrefs.");
        }

        if (PlayerPrefs.HasKey("masterCombo"))
        {
            int localCombo = PlayerPrefs.GetInt("masterCombo");
            Debug.Log($"[LoadPrefs] Loading masterCombo: {localCombo}");
            if (localCombo == 1)
            {
                if (general != null)
                    SettingsManager.Instance.comboProgression = true;
            }
            else
            {
                if (general != null)
                    SettingsManager.Instance.comboProgression = false;
            }
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] masterCombo key not found in PlayerPrefs.");
        }

        if (PlayerPrefs.HasKey("masterSens"))
        {
            float localSens = PlayerPrefs.GetFloat("masterSens");
            Debug.Log($"[LoadPrefs] Loading masterSens: {localSens}");
            if (sensSlider) sensSlider.value = localSens;
            if (staticSensSlider) staticSensSlider.value = localSens;
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] masterSens key not found in PlayerPrefs.");
        }

        if (PlayerPrefs.HasKey("masterInvertY"))
        {
            int localInvert = PlayerPrefs.GetInt("masterInvertY");
            Debug.Log($"[LoadPrefs] Loading masterInvertY: {localInvert}");
            if (localInvert == 1)
            {
                if (SettingsManager.Instance != null)
                    SettingsManager.Instance.invertY = true;
            }
            else
            {
                if (SettingsManager.Instance != null)
                    SettingsManager.Instance.invertY = false;
            }
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] masterInvertY key not found in PlayerPrefs.");
        }
    }

    public void LoadGraphicsSettings()
    {
        if (PlayerPrefs.HasKey("masterFullscreen"))
        {
            int fullscreenInt = PlayerPrefs.GetInt("masterFullscreen");
            Debug.Log($"[LoadPrefs] Loading masterFullscreen: {fullscreenInt}");
            if (fullscreenInt == 0)
            {
                if (graphics != null)
                {
                    graphics.SetDisplayMode(0); // Fullscreen
                }
            }
            else if (fullscreenInt == 1)
            {
                if (graphics != null)
                {
                    graphics.SetDisplayMode(1); // Windowed
                }
            }
            else
            {
                if (graphics != null)
                {
                    graphics.SetDisplayMode(2); // Borderless
                }
            }
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] masterFullscreen key not found in PlayerPrefs.");
        }

        if (PlayerPrefs.HasKey("masterResolution"))
        {
            int resolutionInt = PlayerPrefs.GetInt("masterResolution");
            Debug.Log($"[LoadPrefs] Loading masterResolution: {resolutionInt}");
            if (resolutionInt == 0)
            {
                if (graphics != null)
                {
                    graphics.SetResolution("1920x1080"); // 1920x1080
                }
            }
            else
            {
                if (graphics != null)
                {
                    graphics.SetResolution("2560x1440"); // 2560x1440
                }
            }
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] masterResolution key not found in PlayerPrefs.");
        }

        if (PlayerPrefs.HasKey("masterCameraShake"))
        {
            int cameraShakeInt = PlayerPrefs.GetInt("masterCameraShake");
            Debug.Log($"[LoadPrefs] Loading masterCameraShake: {cameraShakeInt}");
            bool isCameraShake = cameraShakeInt == 1;
            if (graphics != null)
            {
                graphics.SetCameraShake(isCameraShake);
            }
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] masterCameraShake key not found in PlayerPrefs.");
        }

        if (PlayerPrefs.HasKey("masterFPS"))
        {
            int localFPS = PlayerPrefs.GetInt("masterFPS");
            Debug.Log($"[LoadPrefs] Loading masterFPS: {localFPS}");
            Application.targetFrameRate = localFPS;
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] masterFPS key not found in PlayerPrefs.");
        }

        if (PlayerPrefs.HasKey("masterMotionBlur"))
        {
            int motionBlurInt = PlayerPrefs.GetInt("masterMotionBlur");
            Debug.Log($"[LoadPrefs] Loading masterMotionBlur: {motionBlurInt}");
            bool isMotionBlur = motionBlurInt == 1;
            if (graphics != null)
            {
                graphics.SetMotionBlur(isMotionBlur);
            }
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] masterMotionBlur key not found in PlayerPrefs.");
        }

        if (PlayerPrefs.HasKey("masterBrightness"))
        {
            float localBrightness = PlayerPrefs.GetFloat("masterBrightness");
            Debug.Log($"[LoadPrefs] Loading masterBrightness: {localBrightness}");
            if (brightnessSlider) brightnessSlider.value = localBrightness;
            if (staticBrightnessSlider) staticBrightnessSlider.value = localBrightness;

            float defaultBrightness = fallbackDefaultBrightness;
            if (graphics != null)
            {
                defaultBrightness = graphics.defaultBrightness;
                graphics.SetBrightness(localBrightness);
                Debug.Log($"[LoadPrefs] Loaded Brightness: {localBrightness}, Applied to GraphicsSettings.");
            }
            else
            {
                if (graphics.globalVolume.profile.TryGet(out graphics.liftGammaGain))
                {
                    graphics.liftGammaGain.gamma.value = new Vector4(1f, 1f, 1f, localBrightness);
                    graphics.brightnessLevel = graphics.defaultBrightness;
                }
            }
        }
        else
        {
            Debug.LogWarning("[LoadPrefs] masterBrightness key not found in PlayerPrefs.");
        }
    }

    void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}
