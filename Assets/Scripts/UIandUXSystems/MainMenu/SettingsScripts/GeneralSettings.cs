/*
    Controls the items in the general settings and calls to the settings manager to edit values. This script also handles applying the settings and resetting them.

    written by Brandon Wahl
*/

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.VisualScripting;

public class GeneralSettings : MonoBehaviour
{

    [Header("General Settings Container Reference")]
    [SerializeField] private GameObject generalSettingsContainer;

    [Space(20)]


    [Header("Sensitivity Settings")]
    [SerializeField] private Slider sensSlider = null;
    [SerializeField] private float defaultSens;
    [SerializeField] private float controllerSensMin = 0.1f;
    [SerializeField] private float controllerSensMax = 3f;
    [SerializeField] private float kbSensMin = 0.5f;
    [SerializeField] private float kbSensMax = 5f;

    [Header("Vibration Settings")]
    [SerializeField] private Slider vibrationSlider = null;
    [SerializeField] private float defaultVibration = 0.5f;

    [Header("On/Off Text")]
    [SerializeField] private TMP_Text invertYText = null;
    [SerializeField] private TMP_Text comboProgressionText = null;
    private bool isInvertYOn = false;
    private bool isComboProgressionOn;
    private float vibration;

    [Space(20)]

    [SerializeField] private InputActionReference _applyAction;
    [SerializeField] private InputActionReference _resetAction;

    private void OnEnable()
    {
        // Load PlayerPrefs for toggles and settings
        float savedSens = PlayerPrefs.GetFloat("masterSens", defaultSens);
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.Settings, $"[GeneralSettings] OnEnable: Loaded masterSens from PlayerPrefs: {savedSens}");
        if (sensSlider != null)
            sensSlider.value = savedSens;
        SettingsManager.Instance.UpdatePlayerCameraSens(savedSens);

        float savedVibration = PlayerPrefs.GetFloat("masterVibrateStrength", defaultVibration);
        if (vibrationSlider != null)
            vibrationSlider.value = savedVibration;
        SettingsManager.Instance.rumbleStrength = savedVibration;

        isInvertYOn = PlayerPrefs.GetInt("masterInvertY", 0) == 1;
        SetInvertY(isInvertYOn);

        isComboProgressionOn = PlayerPrefs.GetInt("masterCombo", 1) == 1;
        SetComboProgressionDisplay(isComboProgressionOn);

        if (_applyAction != null && _applyAction.action != null)
            _applyAction.action.performed += ctx => GeneralApply();

    }

    private void Start()
    {
        StartCoroutine(WaitForPlayerInput());
        StartCoroutine(PollForSchemeChange());
    }

    private IEnumerator WaitForPlayerInput()
    {
        while (InputReader.PlayerInput == null)
        {
            DebugLogSettingsM.ConditionalLog(DebugLogCategory.Settings, "[GeneralSettings] Waiting for PlayerInput to be initialized...");
            yield return null;
        }
        // Subscribe to control scheme changes once PlayerInput is ready
        InputReader.PlayerInput.onControlsChanged += ChangeSensivityThresholds; 
        ChangeSensivityThresholds(InputReader.PlayerInput); // Set initial slider range based on current control scheme
        defaultSens = (sensSlider.minValue + sensSlider.maxValue) / 2f;
    }

    private void OnDisable()
    {
        if (_applyAction != null && _applyAction.action != null)
            _applyAction.action.performed -= ctx => GeneralApply();

        if (InputReader.PlayerInput != null)
            InputReader.PlayerInput.onControlsChanged -= ChangeSensivityThresholds; 
        _stopPolling = true;
    }

    private bool _stopPolling = false;

    private IEnumerator PollForSchemeChange()
    {
        string lastScheme = null;
        while (!_stopPolling)
        {
            if (InputReader.PlayerInput != null)
            {
                string currentScheme = InputReader.PlayerInput.currentControlScheme;
                if (currentScheme != lastScheme)
                {
                    lastScheme = currentScheme;
                    ChangeSensivityThresholds(InputReader.PlayerInput);
                }
            }
            yield return new WaitForSeconds(0.1f); // Poll every 0.1s for efficiency
        }
    }

    private void ChangeSensivityThresholds(PlayerInput input)
    {
        string schemeName = (input != null && input.currentControlScheme != null) ? input.currentControlScheme.Trim().ToLower() : string.Empty;

        if (sensSlider == null)
        {
            Debug.LogError("[GeneralSettings] sensSlider is null!");
            return;
        }

        float newMin = sensSlider.minValue;
        float newMax = sensSlider.maxValue;
        float newDefault = sensSlider.value;
        if (schemeName.Contains("gamepad"))
        {
            newMin = controllerSensMin;
            newMax = controllerSensMax;
            newDefault = (controllerSensMin + controllerSensMax) / 2f;
            DebugLogSettingsM.ConditionalLog(DebugLogCategory.Settings, $"[GeneralSettings] Set range for Gamepad: {controllerSensMin} - {controllerSensMax}");
        }
        else if (schemeName.Contains("keyboard"))
        {
            newMin = kbSensMin;
            newMax = kbSensMax;
            newDefault = (kbSensMin + kbSensMax) / 2f;
            DebugLogSettingsM.ConditionalLog(DebugLogCategory.Settings, $"[GeneralSettings] Set range for Keyboard: {kbSensMin} - {kbSensMax}");
        }
        else
        {
            Debug.LogWarning($"[GeneralSettings] Unknown control scheme: '{schemeName}'");
        }

        sensSlider.minValue = newMin;
        sensSlider.maxValue = newMax;
        float savedSens = PlayerPrefs.HasKey("masterSens") ? PlayerPrefs.GetFloat("masterSens") : newDefault;
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.Settings, $"[GeneralSettings] ChangeSensivityThresholds: Loaded masterSens from PlayerPrefs: {savedSens}");
        float clampedSens = Mathf.Clamp(savedSens, sensSlider.minValue, sensSlider.maxValue);
        sensSlider.value = clampedSens;
        // Force UI refresh
        sensSlider.onValueChanged.Invoke(clampedSens);
        SetSens(clampedSens);
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.Settings, $"[GeneralSettings] Scheme: {schemeName}, Set slider to: {clampedSens}, min: {sensSlider.minValue}, max: {sensSlider.maxValue}");
        defaultSens = newDefault;
    }
    
    //All functions below sets values based on player choice
    public void SetSens(float sens)
    {
        SettingsManager.Instance.UpdatePlayerCameraSens(sens);
        // Save the actual slider value, not the SettingsManager's field (which may not update immediately)
        PlayerPrefs.SetFloat("masterSens", sens);
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.Settings, $"[GeneralSettings] SetSens: Saved masterSens to PlayerPrefs: {sens}");
    }

    public void SetVibration(float vibrate)
    {
        SettingsManager.Instance.rumbleStrength = vibrate;
        // Update live value; defer updating the read-only/static slider until Apply.
        PlayerPrefs.SetFloat("masterVibrateStrength", SettingsManager.Instance.rumbleStrength);
    }

    public void SetComboProgressionDisplay(bool displayOn)
    {
        isComboProgressionOn = displayOn;

        if (comboProgressionText != null)
            comboProgressionText.text = isComboProgressionOn ? "On" : "Off";

        SettingsManager.Instance.UpdateComboProgressionDisplay(isComboProgressionOn);
        PlayerPrefs.SetInt("masterCombo", isComboProgressionOn ? 1 : 0);

        DebugLogSettingsM.ConditionalLog(DebugLogCategory.Settings, $"[SetComboProgressionDisplay] displayOn={displayOn}, applied={SettingsManager.Instance.comboProgression}");
    }

    public void ToggleComboProgressionDisplay(bool onOrOff)
    {
        if(onOrOff)
            SetComboProgressionDisplay(true);
        else             
            SetComboProgressionDisplay(false);
    }

    public void SetInvertY(bool invertYOn)
    {
        SettingsManager.Instance.UpdatePlayerInvertY(invertYOn);
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.Settings, "Invert Y: " + !isInvertYOn);

        if (invertYOn)
        {
            isInvertYOn = true;
            invertYText.text = "On";
        }
        else
        {
            isInvertYOn = false;
            invertYText.text = "Off";
        }
        
    }

    public void GeneralApply()
    {
        SettingsManager.Instance.UpdatePlayerCameraSens(sensSlider.value);
        PlayerPrefs.SetFloat("masterSens", SettingsManager.Instance.sensitivity);

        SettingsManager.Instance.rumbleStrength = vibrationSlider.value;
        PlayerPrefs.SetFloat("masterVibrateStrength", SettingsManager.Instance.rumbleStrength);

        DebugLogSettingsM.ConditionalLog(DebugLogCategory.Settings, $"[GeneralApply] isComboProgressionOn={isComboProgressionOn}, SettingsManager.Instance.comboProgression={SettingsManager.Instance.comboProgression}");
        PlayerPrefs.SetInt("masterInvertY", (isInvertYOn ? 1 : 0));
        PlayerPrefs.SetInt("masterCombo", (isComboProgressionOn ? 1 : 0));

        PlayerPrefs.Save();

        DebugLogSettingsM.ConditionalLog(DebugLogCategory.Settings, "General settings applied: Sensitivity = " + SettingsManager.Instance.sensitivity + ", Vibration Strength = " + SettingsManager.Instance.rumbleStrength + ", Invert Y = " + isInvertYOn + ", Combo Progression = " + isComboProgressionOn);
    }

    //Resets the settings
    public void ResetButton()
    {
        SettingsManager.Instance.sensitivity = defaultSens;
        sensSlider.value = defaultSens;

        SettingsManager.Instance.rumbleStrength = defaultVibration;
        vibrationSlider.value = defaultVibration;

        SettingsManager.Instance.invertY = false;
        invertYText.text = "Off";
        isInvertYOn = false;

        SettingsManager.Instance.comboProgression = true;
        comboProgressionText.text = "On";
        isComboProgressionOn = true;

        GeneralApply();
    }

}
