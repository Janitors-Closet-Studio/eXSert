using System.Collections.Generic;
using TMPro;
using UnityEngine;

internal class DMenuUIImplementation : MonoBehaviour
{
    [SerializeField]
    private TMP_Dropdown _sceneDropdown;

    private void Start()
    {
        PopulateSceneDropdown();
    }

    private void PopulateSceneDropdown()
    {
        if (_sceneDropdown == null)
        {
            Debug.LogError("[Debug Menu UI] Scene dropdown is not assigned.");
            return;
        }

        _sceneDropdown.ClearOptions();

        // Load all SceneAssets from the Resources/Scene Assets folder
        SceneAsset[] allSceneAssets = Resources.LoadAll<SceneAsset>("Scene Assets");
        
        HashSet<string> excludedScenes = new HashSet<string> { "PlayerScene", "LoadingScene", "MainMenu" };
        List<string> options = new();

        foreach (SceneAsset asset in allSceneAssets)
        {
            if (asset != null && !excludedScenes.Contains(asset.SceneName))
            {
                options.Add(asset.SceneName);
            }
        }

        _sceneDropdown.AddOptions(options);
    }

    /// <summary>
    /// Called by a UI Button's OnClick event to load the currently selected scene from the dropdown.
    /// </summary>
    public void LoadSelectedScene()
    {
        if (_sceneDropdown == null)
        {
            Debug.LogError("[Debug Menu UI] Scene dropdown is not assigned.");
            return;
        }

        // Get the string value of the currently selected dropdown option
        string selectedSceneName = _sceneDropdown.options[_sceneDropdown.value].text;

        // SceneAsset appears to support explicit casting from a string based on SceneLoader's usage
        SceneAsset sceneAsset = (SceneAsset)selectedSceneName;

        if (sceneAsset == null)
        {
            Debug.LogError($"[D Menu UI] Could not find a valid SceneAsset for '{selectedSceneName}'.");
            return;
        }

        // Load the selected scene using SceneLoader
        SceneLoader.LoadIntoGame(sceneAsset);
    }
}
