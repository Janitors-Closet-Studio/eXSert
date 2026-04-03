/*
    Script provided by unity that will save the rebinds made to player prefs
*/

using UnityEngine;
using UnityEngine.InputSystem;

public class RebindSaveLoad : MonoBehaviour
{
    public InputActionAsset actions;
    
    //If true, it will save the load control scheme
    public bool loadControlScheme;

    public void OnEnable()
    {
        if (loadControlScheme)
        {
            var rebinds = PlayerPrefs.GetString("InputBindingOverrides");
            if (!string.IsNullOrEmpty(rebinds))
                //Loads the rebinds if player prefs isnt null
                actions.LoadBindingOverridesFromJson(rebinds);
            // Refresh keybind icons and UI after loading
            UnityEngine.InputSystem.Samples.RebindUI.RebindActionUI[] rebindUIs = FindObjectsByType<UnityEngine.InputSystem.Samples.RebindUI.RebindActionUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ui in rebindUIs)
            {
                ui.UpdateBindingDisplay();
            }
            var iconSwappers = FindObjectsByType<KeybindIconSwapper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var swapper in iconSwappers)
            {
                swapper.RefreshIcon();
            }
        }
    }

    private void SaveRebinds()
    {
        if (loadControlScheme)
        {
            var rebinds = actions.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString("InputBindingOverrides", rebinds);
            PlayerPrefs.Save();
        }
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
}
