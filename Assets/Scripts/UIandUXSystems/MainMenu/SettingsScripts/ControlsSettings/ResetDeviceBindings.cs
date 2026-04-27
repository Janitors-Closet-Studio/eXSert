    
/*
    Written by Brandon
    
    This script will reset ALL the bindings made for a specifc control scheme.
*/

using UnityEngine;
using UnityEngine.InputSystem;
public class ResetDeviceBindings : MonoBehaviour
{
    public static bool controlsAreOpen = false;

    private const string BindingOverridesKey = "InputBindingOverrides";

    private System.Action<InputAction.CallbackContext> resetPerformedHandler;

    [SerializeField] private InputActionAsset _inputActions;
    [SerializeField] private InputActionReference _resetBindingsActionReference;

    [SerializeField] private GameObject targetControlSchemeObject;

    //Assign this string in the editor to the control scheme name you wish to reset
    private string _targetControlScheme;

    void OnEnable()
    {
        if (_resetBindingsActionReference != null && _resetBindingsActionReference.action != null)
        {
            resetPerformedHandler ??= _ => ResetControlSchemeBinding();
            _resetBindingsActionReference.action.performed += resetPerformedHandler;
        }
        else
        {
            Debug.LogWarning($"Reset Bindings Input Action Reference is not set in the inspector. Reset bindings button won't work.");
        }

        controlsAreOpen = true;
    }

    void OnDisable()
    {
        if (_resetBindingsActionReference != null && _resetBindingsActionReference.action != null && resetPerformedHandler != null)
        {
            _resetBindingsActionReference.action.performed -= resetPerformedHandler;
        }
        controlsAreOpen = false;
    }
    


    [ContextMenu("Reset All Bindings (Inspector)")]
    public void InspectorResetAllBindings()
    {
        ResetControlSchemeBinding();
    }

    public void WhichControlSchemeIsOpen(int schemeIndex)
    {
        if(schemeIndex == 0)
        {
            _targetControlScheme = "Keyboard&Mouse";
        }
        else if(schemeIndex == 1)
        {
            _targetControlScheme = "Gamepad";
        }
    }

    //This script looks through all the actions in Input action assigned and will reset only the bindings in the target control scheme
    public void ResetControlSchemeBinding()
    {
        if (!targetControlSchemeObject.activeInHierarchy)
            return;

        ResetBindingsInAsset(_inputActions);

        var runtimeActions = InputReader.PlayerInput != null ? InputReader.PlayerInput.actions : null;
        if (runtimeActions != null && runtimeActions != _inputActions)
            ResetBindingsInAsset(runtimeActions);

        var sourceForSave = runtimeActions != null ? runtimeActions : _inputActions;
        if (sourceForSave != null)
        {
            PlayerPrefs.SetString(BindingOverridesKey, sourceForSave.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        RefreshBindingUI();

        var rebindSaveLoad = FindFirstObjectByType<RebindSaveLoad>(FindObjectsInactive.Include);
        if (rebindSaveLoad != null)
            rebindSaveLoad.SaveRebindsManually();

        Debug.Log($"Reset {_targetControlScheme} bindings to default.");
    }

    private void ResetBindingsInAsset(InputActionAsset asset)
    {
        if (asset == null || string.IsNullOrEmpty(_targetControlScheme))
            return;

        foreach (InputActionMap map in asset.actionMaps)
        {
            foreach (InputAction action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (!BindingMatchesTargetScheme(action.bindings[i]))
                        continue;

                    action.RemoveBindingOverride(i);
                }
            }
        }
    }

    private bool BindingMatchesTargetScheme(InputBinding binding)
    {
        if (!string.IsNullOrEmpty(binding.groups) && binding.groups.Contains(_targetControlScheme))
            return true;

        string path = binding.path;
        if (string.IsNullOrEmpty(path))
            return false;

        if (string.Equals(_targetControlScheme, "Keyboard&Mouse", System.StringComparison.OrdinalIgnoreCase))
        {
            return path.StartsWith("<Keyboard>", System.StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<Mouse>", System.StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(_targetControlScheme, "Gamepad", System.StringComparison.OrdinalIgnoreCase))
        {
            return path.StartsWith("<Gamepad>", System.StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<XInputController>", System.StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<DualShockGamepad>", System.StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static void RefreshBindingUI()
    {
        var rebindUIs = FindObjectsByType<UnityEngine.InputSystem.Samples.RebindUI.RebindActionUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ui in rebindUIs)
            ui.UpdateBindingDisplay();

        var iconSwappers = FindObjectsByType<KeybindIconSwapper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var swapper in iconSwappers)
            swapper.RefreshIcon();
    }
}
