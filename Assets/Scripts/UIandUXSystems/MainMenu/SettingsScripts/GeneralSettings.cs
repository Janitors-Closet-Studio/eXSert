/*
    Controls the items in the general settings and calls to the settings manager to edit values. This script also handles applying the settings and resetting them.

    written by Brandon Wahl
*/

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class GeneralSettings : MonoBehaviour
{

    [Header("General Settings Container Reference")]
    [SerializeField] private GameObject generalSettingsContainer;

    [Space(20)]


    [Header("Sensitivity Settings")]
    [SerializeField] private Slider sensSlider = null;
    [SerializeField] private float defaultSens = 1.5f;

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

    private void OnDisable()
    {
        if (_applyAction != null && _applyAction.action != null)
            _applyAction.action.performed -= ctx => GeneralApply();
    }


    //All functions below sets values based on player choice
    public void SetSens(float sens)
    {
        SettingsManager.Instance.UpdatePlayerCameraSens(sens);
        // Update live value; defer updating the read-only/static slider until Apply.
        PlayerPrefs.SetFloat("masterSens", SettingsManager.Instance.sensitivity);
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

        Debug.Log($"[SetComboProgressionDisplay] displayOn={displayOn}, applied={SettingsManager.Instance.comboProgression}");
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
        Debug.Log("Invert Y: " + !isInvertYOn);

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

        Debug.Log($"[GeneralApply] isComboProgressionOn={isComboProgressionOn}, SettingsManager.Instance.comboProgression={SettingsManager.Instance.comboProgression}");
        PlayerPrefs.SetInt("masterInvertY", (isInvertYOn ? 1 : 0));
        PlayerPrefs.SetInt("masterCombo", (isComboProgressionOn ? 1 : 0));

        PlayerPrefs.Save();

        Debug.Log("General settings applied: Sensitivity = " + SettingsManager.Instance.sensitivity + ", Vibration Strength = " + SettingsManager.Instance.rumbleStrength + ", Invert Y = " + isInvertYOn + ", Combo Progression = " + isComboProgressionOn);
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
