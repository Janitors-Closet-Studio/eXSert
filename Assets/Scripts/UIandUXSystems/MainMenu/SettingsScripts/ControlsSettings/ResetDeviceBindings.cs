    
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
    private const string KeyboardSchemeName = "Keyboard&Mouse";
    private const string GamepadSchemeName = "Gamepad";

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
            _targetControlScheme = KeyboardSchemeName;
        }
        else if(schemeIndex == 1)
        {
            _targetControlScheme = GamepadSchemeName;
        }
    }

    //This script looks through all the actions in Input action assigned and will reset only the bindings in the target control scheme
    public void ResetControlSchemeBinding()
    {
        if (!targetControlSchemeObject.activeInHierarchy)
            return;

        string targetScheme = ResolveTargetControlScheme();
        if (string.IsNullOrEmpty(targetScheme))
        {
            Debug.LogWarning("Reset bindings aborted: unable to resolve target control scheme.");
            return;
        }

        ResetBindingsInAsset(_inputActions, targetScheme);

        var runtimeActions = InputReader.PlayerInput != null ? InputReader.PlayerInput.actions : null;
        if (runtimeActions != null && runtimeActions != _inputActions)
            ResetBindingsInAsset(runtimeActions, targetScheme);

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

        _targetControlScheme = targetScheme;
        Debug.Log($"Reset {targetScheme} bindings to default.");
    }

    private string ResolveTargetControlScheme()
    {
        if (string.Equals(_targetControlScheme, KeyboardSchemeName, System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(_targetControlScheme, GamepadSchemeName, System.StringComparison.OrdinalIgnoreCase))
        {
            return _targetControlScheme;
        }

        var runtimeInput = InputReader.PlayerInput;
        string currentScheme = runtimeInput != null ? runtimeInput.currentControlScheme : string.Empty;
        if (!string.IsNullOrEmpty(currentScheme))
        {
            if (currentScheme.IndexOf("Keyboard", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return KeyboardSchemeName;
            if (currentScheme.IndexOf("Gamepad", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return GamepadSchemeName;
        }

        if (targetControlSchemeObject != null)
        {
            string objectName = targetControlSchemeObject.name;
            if (!string.IsNullOrEmpty(objectName))
            {
                if (objectName.IndexOf("Keyboard", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return KeyboardSchemeName;
                if (objectName.IndexOf("Gamepad", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return GamepadSchemeName;
            }
        }

        return null;
    }

    private void ResetBindingsInAsset(InputActionAsset asset, string targetScheme)
    {
        if (asset == null || string.IsNullOrEmpty(targetScheme))
            return;

        foreach (InputActionMap map in asset.actionMaps)
        {
            foreach (InputAction action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (!BindingMatchesTargetScheme(action.bindings[i], targetScheme))
                        continue;

                    action.RemoveBindingOverride(i);
                }
            }
        }
    }

    private static bool BindingMatchesTargetScheme(InputBinding binding, string targetScheme)
    {
        if (!string.IsNullOrEmpty(binding.groups))
        {
            string[] groups = binding.groups.Split(';');
            for (int i = 0; i < groups.Length; i++)
            {
                if (string.Equals(groups[i].Trim(), targetScheme, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        string path = !string.IsNullOrEmpty(binding.path) ? binding.path : binding.effectivePath;
        if (string.IsNullOrEmpty(path))
            return false;

        if (string.Equals(targetScheme, KeyboardSchemeName, System.StringComparison.OrdinalIgnoreCase))
        {
            return path.StartsWith("<Keyboard>", System.StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<Mouse>", System.StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(targetScheme, GamepadSchemeName, System.StringComparison.OrdinalIgnoreCase))
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
