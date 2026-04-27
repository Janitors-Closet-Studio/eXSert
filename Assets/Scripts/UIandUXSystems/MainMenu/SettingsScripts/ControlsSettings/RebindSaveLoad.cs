/*
    Script provided by unity that will save the rebinds made to player prefs
*/

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class RebindSaveLoad : MonoBehaviour
{
    private const string BindingOverridesKey = "InputBindingOverrides";
    private const string ControlSchemeKey = "InputControlScheme";
    private const int DeferredLoadFrames = 120;

    public InputActionAsset actions;
    [SerializeField] private PlayerInput playerInput;
    
    //If true, it will save the load control scheme
    public bool loadControlScheme;

    public void OnEnable()
    {
        if (!loadControlScheme)
            return;

        ApplySavedRebindsToKnownAssets();
        StartCoroutine(ApplySavedRebindsDeferred());
    }

    private void Start()
    {
        if (!loadControlScheme)
            return;

        // Start can run after late PlayerInput initialization paths.
        StartCoroutine(ApplySavedRebindsDeferred());
    }

    private void SaveRebinds()
    {
        if (!loadControlScheme)
            return;

        var rebinds = GetBestOverridesJson();
        PlayerPrefs.SetString(BindingOverridesKey, rebinds);

        var runtimeInput = GetRuntimePlayerInput();
        if (runtimeInput != null && !string.IsNullOrEmpty(runtimeInput.currentControlScheme))
            PlayerPrefs.SetString(ControlSchemeKey, runtimeInput.currentControlScheme);

        PlayerPrefs.Save();
    }

    public void SaveRebindsManually() => SaveRebinds();

    private void OnDestroy()
    {
        SaveRebinds();
    }

    public void OnDisable()
    {
        SaveRebinds();
    }

    public void OnApplicationQuit()
    {
        SaveRebinds();
    }

    private PlayerInput GetRuntimePlayerInput()
    {
        if (playerInput != null)
            return playerInput;

        playerInput = FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include);
        return playerInput;
    }

    private void LoadControlScheme()
    {
        var runtimeInput = GetRuntimePlayerInput();
        if (runtimeInput == null)
            return;

        var savedScheme = PlayerPrefs.GetString(ControlSchemeKey);
        if (string.IsNullOrEmpty(savedScheme))
            return;

        if (string.Equals(runtimeInput.currentControlScheme, savedScheme, System.StringComparison.Ordinal))
            return;

        try
        {
            runtimeInput.SwitchCurrentControlScheme(savedScheme);
        }
        catch (System.Exception)
        {
            // Ignore invalid saved schemes so fallback behavior can continue.
        }
    }

    private void ApplySavedRebindsToKnownAssets()
    {
        var rebinds = PlayerPrefs.GetString(BindingOverridesKey);
        if (string.IsNullOrEmpty(rebinds))
        {
            LoadControlScheme();
            RefreshBindingUI();
            return;
        }

        if (actions != null)
            actions.LoadBindingOverridesFromJson(rebinds);

        var runtimeInput = GetRuntimePlayerInput();
        if (runtimeInput != null && runtimeInput.actions != null)
            runtimeInput.actions.LoadBindingOverridesFromJson(rebinds);

        LoadControlScheme();
        RefreshBindingUI();
    }

    private IEnumerator ApplySavedRebindsDeferred()
    {
        for (int i = 0; i < DeferredLoadFrames; i++)
        {
            var runtimeInput = GetRuntimePlayerInput();
            if (runtimeInput != null && runtimeInput.actions != null)
            {
                var rebinds = PlayerPrefs.GetString(BindingOverridesKey);
                if (!string.IsNullOrEmpty(rebinds))
                    runtimeInput.actions.LoadBindingOverridesFromJson(rebinds);

                LoadControlScheme();
                RefreshBindingUI();
                yield break;
            }

            yield return null;
        }
    }

    private string GetBestOverridesJson()
    {
        var runtimeInput = GetRuntimePlayerInput();
        if (runtimeInput != null && runtimeInput.actions != null)
            return runtimeInput.actions.SaveBindingOverridesAsJson();

        return actions != null ? actions.SaveBindingOverridesAsJson() : string.Empty;
    }

    private static void RefreshBindingUI()
    {
        // Refresh keybind labels/icons after loading saved overrides.
        var rebindUIs = FindObjectsByType<UnityEngine.InputSystem.Samples.RebindUI.RebindActionUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ui in rebindUIs)
            ui.UpdateBindingDisplay();

        var iconSwappers = FindObjectsByType<KeybindIconSwapper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var swapper in iconSwappers)
            swapper.RefreshIcon();
    }
}
