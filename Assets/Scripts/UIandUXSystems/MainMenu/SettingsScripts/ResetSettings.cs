using UnityEngine;
using UnityEngine.InputSystem;
public class ResetSettings : MonoBehaviour
{
    [SerializeField] private InputActionReference _resetAction;

    [Header("Settings Containers")]
    [SerializeField] private GameObject settingsContainer;
    [SerializeField] private GeneralSettings generalSettingsContainer;
    [SerializeField] private GraphicsSettings graphicsSettingsContainer;
    [SerializeField] private AudioSettings audioSettingsContainer;

    private void OnEnable()
    {
        if (_resetAction != null && _resetAction.action != null)
            _resetAction.action.performed += ctx => ResetAllSettings();
    }

    private void OnDisable()
    {
        if (_resetAction != null && _resetAction.action != null)
            _resetAction.action.performed -= ctx => ResetAllSettings();
    }

    public void ResetAllSettings()
    {
        if (!settingsContainer.activeInHierarchy)
            return;

        if (generalSettingsContainer != null)
            generalSettingsContainer.ResetButton();
        if (graphicsSettingsContainer != null)
            graphicsSettingsContainer.ResetButton();
        if (audioSettingsContainer != null)
            audioSettingsContainer.ResetButton();
    }

}
