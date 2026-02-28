    
/*
    Written by Brandon
    
    This script will reset ALL the bindings made for a specifc control scheme.
*/

using UnityEngine;
using UnityEngine.InputSystem;
public class ResetDeviceBindings : MonoBehaviour
{
    [SerializeField] private InputActionAsset _inputActions;
    [SerializeField] private InputActionReference _resetBindingsActionReference;

    //Assign this string in the editor to the control scheme name you wish to reset
    private string _targetControlScheme;

    void OnEnable()
    {
        if (_resetBindingsActionReference != null && _resetBindingsActionReference.action != null)
        {
            _resetBindingsActionReference.action.performed += ctx => ResetControlSchemeBinding();
        }
        else
        {
            Debug.LogWarning($"Reset Bindings Input Action Reference is not set in the inspector. Reset bindings button won't work.");
        }
    }

    void OnDisable()
    {
        if (_resetBindingsActionReference != null && _resetBindingsActionReference.action != null)
        {
            _resetBindingsActionReference.action.performed -= ctx => ResetControlSchemeBinding();
        }
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
        foreach (InputActionMap map in _inputActions.actionMaps)
        {
            foreach (InputAction action in map.actions)
                {
                    action.RemoveBindingOverride(InputBinding.MaskByGroup(_targetControlScheme));
                }
        }

        Debug.Log($"Reset {_targetControlScheme} bindings to default.");
    }
}
