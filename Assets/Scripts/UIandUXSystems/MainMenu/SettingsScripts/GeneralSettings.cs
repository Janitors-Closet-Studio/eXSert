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
    [SerializeField] private Slider staticSensSlider = null;

    [Header("Vibration Settings")]
    [SerializeField] private Slider vibrationSlider = null;
    [SerializeField] private float defaultVibration = 0.5f;
    [SerializeField] private Slider staticVibrationSlider = null;

    [Header("On/Off Text")]
    [SerializeField] private TMP_Text invertYText = null;
    [SerializeField] private TMP_Text comboProgressionText = null;
    private bool isInvertYOn = false;
    private bool isComboProgressionOn;
    private float vibration;

    [Space(20)]

    [SerializeField] private InputActionReference _applyAction;

    void Update()
    {
        if (_applyAction.action.WasPerformedThisFrame() && generalSettingsContainer.gameObject.activeSelf)
        {
            GeneralApply();
            Debug.Log("General Settings Applied");
        }
        else 
        {
            return;
        }
    }



    //All functions below sets values based on player choice
    public void SetSens(float sens)
    {
        SettingsManager.Instance.sensitivity = sens;
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
        SettingsManager.Instance.comboProgression = displayOn;

        Debug.Log("Combo Progression: " + !isComboProgressionOn);

        if (displayOn)
        {
            isComboProgressionOn = true;
            comboProgressionText.text = "On";
        }
        else
        {
            isComboProgressionOn = false;
            comboProgressionText.text = "Off";
        }

        if (isComboProgressionOn)
        {
            PlayerPrefs.SetInt("masterCombo", 1);
            SettingsManager.Instance.comboProgression = true;
        }
        else
        {
            PlayerPrefs.SetInt("masterCombo", 0);
            SettingsManager.Instance.comboProgression = false;
        }
    }

    public void SetInvertY(bool invertYOn)
    {
        SettingsManager.Instance.invertY = invertYOn;
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
        SettingsManager.Instance.sensitivity = sensSlider.value;
        PlayerPrefs.SetFloat("masterSens", SettingsManager.Instance.sensitivity);

        SettingsManager.Instance.rumbleStrength = vibrationSlider.value;
        PlayerPrefs.SetFloat("masterVibrateStrength", SettingsManager.Instance.rumbleStrength);

        // Update static/read-only sliders to reflect applied values
        if (staticSensSlider != null)
            staticSensSlider.value = sensSlider != null ? sensSlider.value : SettingsManager.Instance.sensitivity;

        if (staticVibrationSlider != null)
            staticVibrationSlider.value = vibrationSlider != null ? vibrationSlider.value : SettingsManager.Instance.rumbleStrength;

        PlayerPrefs.SetInt("masterInvertY", (isInvertYOn ? 1 : 0));

        PlayerPrefs.SetInt("masterCombo", (isComboProgressionOn ? 1 : 0));

        PlayerPrefs.Save();
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
