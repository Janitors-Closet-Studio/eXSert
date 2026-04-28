    
/*
    Written by Brandon
    
    This script will reset ALL the bindings made for a specifc control scheme.
*/

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
public class ResetDeviceBindings : MonoBehaviour
{
    public static bool controlsAreOpen = false;
    public static bool WarningOpen { get; private set; } = false;

    private const string BindingOverridesKey = "InputBindingOverrides";
    private const string KeyboardSchemeName = "Keyboard&Mouse";
    private const string GamepadSchemeName = "Gamepad";


    [SerializeField] private InputActionAsset _inputActions;
    [SerializeField] private InputActionReference _resetBindingsActionReference;
    [SerializeField] private InputActionReference activateWarning;
    [SerializeField] private InputActionReference closeWarning;

    [SerializeField] private GameObject targetControlSchemeObject;

    [SerializeField] private FadeMenus fadeMenus;

    [SerializeField] private GameObject warningContainer;

    private System.Action<InputAction.CallbackContext> _resetPerformedHandler;
    private System.Action<InputAction.CallbackContext> _openWarningHandler;
    private System.Action<InputAction.CallbackContext> _closeWarningHandler;
    private Selectable lastSelected;
    private Coroutine _warningSelectionLockRoutine;
    private CanvasGroup _warningCanvasGroup;
    private bool _allowWarningInput;
    private bool _isClosingWarning;

    private InputAction _subscribedResetAction;
    private Button _button;

    //Assign this string in the editor to the control scheme name you wish to reset
    private string _targetControlScheme;

    private void Awake()
    {
        HideWarningInstant();
    }

    private InputAction ResolveRuntimeAction(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
            return null;

        var source = actionReference.action;
        var playerInput = InputReader.PlayerInput;
        if (playerInput != null && playerInput.actions != null)
        {
            string mapName = source.actionMap != null ? source.actionMap.name : string.Empty;
            if (!string.IsNullOrEmpty(mapName))
            {
                var runtimeMap = playerInput.actions.FindActionMap(mapName, throwIfNotFound: false);
                if (runtimeMap != null)
                {
                    var runtimeAction = runtimeMap.FindAction(source.name, throwIfNotFound: false);
                    if (runtimeAction != null)
                        return runtimeAction;
                }
            }

            var fallbackAction = playerInput.actions.FindAction(source.name, throwIfNotFound: false);
            if (fallbackAction != null)
                return fallbackAction;
        }

        return source;
    }

    void OnEnable()
    {
        _allowWarningInput = false;
        _subscribedResetAction = ResolveRuntimeAction(_resetBindingsActionReference);
        if (_subscribedResetAction != null)
        {
            _resetPerformedHandler ??= _ => { if (WarningOpen) ResetControlSchemeBinding(); };
            _subscribedResetAction.performed += _resetPerformedHandler;
        }
        else
        {
            Debug.LogWarning($"Reset Bindings Input Action Reference is not set in the inspector. Reset bindings button won't work.");
        }

        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OpenWarning);

        controlsAreOpen = true;

        _openWarningHandler ??= _ => TryOpenWarningFromInput();
        _closeWarningHandler ??= _ => CloseWarning();

        if (!WarningOpen)
            HideWarningInstant();
    
        if (closeWarning != null && closeWarning.action != null)
            closeWarning.action.performed += _closeWarningHandler;
        else
            Debug.LogWarning("Close Warning action reference is not set. The warning cannot be closed with a button press.");
    
        if (activateWarning != null && activateWarning.action != null)
            activateWarning.action.performed += _openWarningHandler;

        _warningCanvasGroup.alpha = 0f;
        StartCoroutine(EnableWarningInputNextFrame());
    }

    void OnDisable()
    {
        if (_resetPerformedHandler != null && _subscribedResetAction != null)
            _subscribedResetAction.performed -= _resetPerformedHandler;
        if (activateWarning != null && activateWarning.action != null)
            activateWarning.action.performed -= _openWarningHandler;
        if (closeWarning != null && closeWarning.action != null)
            closeWarning.action.performed -= _closeWarningHandler;

        if (_warningSelectionLockRoutine != null)
        {
            StopCoroutine(_warningSelectionLockRoutine);
            _warningSelectionLockRoutine = null;
        }

        WarningOpen = false;
        _allowWarningInput = false;
        _isClosingWarning = false;
        HideWarningInstant();

        _subscribedResetAction = null;

        if (_button != null)
            _button.onClick.RemoveListener(OpenWarning);
        _button = null;

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
        if (!WarningOpen)
            return;

        string targetScheme = ResolveTargetControlScheme();
        if (string.IsNullOrEmpty(targetScheme))
        {
            Debug.LogWarning("Reset bindings aborted: unable to resolve target control scheme.");
            return;
        }

        int removedBindings = 0;

        removedBindings += ResetBindingsInAsset(_inputActions, targetScheme);

        var runtimeActions = InputReader.PlayerInput != null ? InputReader.PlayerInput.actions : null;
        if (runtimeActions != null && runtimeActions != _inputActions)
            removedBindings += ResetBindingsInAsset(runtimeActions, targetScheme);

        if (removedBindings == 0)
        {
            // Controller-driven reset can occasionally resolve the wrong active scheme.
            // If nothing was reset, try the opposite scheme once.
            string fallbackScheme = string.Equals(targetScheme, KeyboardSchemeName, System.StringComparison.OrdinalIgnoreCase)
                ? GamepadSchemeName
                : KeyboardSchemeName;

            removedBindings += ResetBindingsInAsset(_inputActions, fallbackScheme);
            if (runtimeActions != null && runtimeActions != _inputActions)
                removedBindings += ResetBindingsInAsset(runtimeActions, fallbackScheme);

            if (removedBindings > 0)
                targetScheme = fallbackScheme;
        }

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
        Debug.Log($"Reset {targetScheme} bindings to default. Removed overrides: {removedBindings}.");

        StartCoroutine(CloseWarningAndRestoreSelection());
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

    private int ResetBindingsInAsset(InputActionAsset asset, string targetScheme)
    {
        if (asset == null || string.IsNullOrEmpty(targetScheme))
            return 0;

        int removedCount = 0;

        foreach (InputActionMap map in asset.actionMaps)
        {
            foreach (InputAction action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (!BindingMatchesTargetScheme(action.bindings[i], targetScheme))
                        continue;

                    InputBinding binding = action.bindings[i];
                    bool hasOverride = binding.overridePath != null
                        || binding.overrideInteractions != null
                        || binding.overrideProcessors != null;

                    if (!hasOverride)
                    {
                        continue;
                    }

                    action.RemoveBindingOverride(i);
                    removedCount++;
                }
            }
        }

        return removedCount;
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

    public void OpenWarning()
    {
        if (WarningOpen)
            return;

        if (EventSystem.current == null)
            return;

        EnsureWarningHierarchyActive();
        GetOrCreateWarningCanvasGroup();

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected != null)
        {
            bool selectedInsideWarning = warningContainer != null && currentSelected.transform.IsChildOf(warningContainer.transform);
            if (!selectedInsideWarning)
                lastSelected = currentSelected.GetComponent<Selectable>();
        }

        WarningOpen = true;

        // Force fade-in to start from transparent so first activation always animates.
        if (_warningCanvasGroup != null)
            _warningCanvasGroup.alpha = 0f;

        StartCoroutine(FadeWarningCanvas(1f, .25f));

        EventSystem.current.SetSelectedGameObject(null);
        Selectable selectableInWarning = FindSelectableInWarning();
        if (selectableInWarning != null)
            EventSystem.current.SetSelectedGameObject(selectableInWarning.gameObject);

        if (_warningSelectionLockRoutine != null)
            StopCoroutine(_warningSelectionLockRoutine);
        _warningSelectionLockRoutine = StartCoroutine(KeepSelectionInsideWarning());
    }

    private void TryOpenWarningFromInput()
    {
        if (!_allowWarningInput)
            return;

        OpenWarning();
    }

    private IEnumerator EnableWarningInputNextFrame()
    {
        yield return null;
        _allowWarningInput = true;
    }

    private Selectable FindSelectableInWarning()
    {
        if (warningContainer == null)
            return null;

        return warningContainer.GetComponentInChildren<Selectable>(true);
    }
    private IEnumerator KeepSelectionInsideWarning()
    {
        while (WarningOpen)
        {
            if (EventSystem.current == null)
                yield break;

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            bool valid = selected != null
                && warningContainer != null
                && selected.transform.IsChildOf(warningContainer.transform)
                && selected.activeInHierarchy;

            if (!valid)
            {
                Selectable selectableInWarning = FindSelectableInWarning();
                if (selectableInWarning != null)
                    EventSystem.current.SetSelectedGameObject(selectableInWarning.gameObject);
            }

            yield return null;
        }
    }

    private IEnumerator CloseWarningAndRestoreSelection()
    {
        if (!WarningOpen || _isClosingWarning)
            yield break;

        _isClosingWarning = true;
        WarningOpen = false;

        if (_warningSelectionLockRoutine != null)
        {
            StopCoroutine(_warningSelectionLockRoutine);
            _warningSelectionLockRoutine = null;
        }

        yield return StartCoroutine(FadeWarningCanvas(0f, .25f));

        if (EventSystem.current != null)
        {
            Selectable target = (lastSelected != null && lastSelected.gameObject.activeInHierarchy && lastSelected.IsInteractable())
                ? lastSelected
                : targetControlSchemeObject != null ? targetControlSchemeObject.GetComponentInChildren<Selectable>(true) : null;

            if (target != null)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        _isClosingWarning = false;
    }

    public void CloseWarning()
    {
        if (!WarningOpen || _isClosingWarning)
            return;

        StartCoroutine(CloseWarningAndRestoreSelection());
    }

    private void EnsureWarningHierarchyActive()
    {
        if (warningContainer == null)
            return;

        if (!warningContainer.activeSelf)
            warningContainer.SetActive(true);

        // Keep warning children active so they do not disappear between opens.
        Transform[] descendants = warningContainer.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            GameObject child = descendants[i].gameObject;
            if (!child.activeSelf)
                child.SetActive(true);
        }
    }

    private CanvasGroup GetOrCreateWarningCanvasGroup()
    {
        if (warningContainer == null)
            return null;

        if (_warningCanvasGroup == null)
            _warningCanvasGroup = warningContainer.GetComponent<CanvasGroup>();
        if (_warningCanvasGroup == null)
            _warningCanvasGroup = warningContainer.AddComponent<CanvasGroup>();

        return _warningCanvasGroup;
    }

    private void HideWarningInstant()
    {
        CanvasGroup warningCanvas = GetOrCreateWarningCanvasGroup();
        if (warningCanvas == null)
            return;

        warningCanvas.alpha = 0f;
        warningCanvas.blocksRaycasts = false;
        warningCanvas.interactable = false;
    }

    private IEnumerator FadeWarningCanvas(float targetAlpha, float duration)
    {
        CanvasGroup canvasGroup = GetOrCreateWarningCanvasGroup();
        if (canvasGroup == null)
            yield break;

        if (targetAlpha > 0.5f)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        if (targetAlpha <= 0.5f)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}
