using UnityEngine;
using Singletons;
using System.Collections.Generic;
using UnityEngine.UI;

public class ActsManager : Singleton<ActsManager>
{
    [SerializeField] private Button[] actsButton;

    protected override void Awake()
    {
        base.Awake();
        for(int i = 0; i < actsButton.Length; i++)
        {
            if(ActsManager.Instance.actCompletionMap.TryGetValue(i, out bool isCompleted))
            {
                actsButton[i].interactable = isCompleted;
            }
            else
            {
                actsButton[i].interactable = false;
            }
        }
    }
    internal Dictionary<int, bool> actCompletionMap = new Dictionary<int, bool>()
    {
        { 0, true },
        { 1, false },
        { 2, false },
        { 3, false },
        { 4, false }
    };

    internal Dictionary<int, string> actSceneMap = new Dictionary<int, string>()
    {
        { 0, "Elevator" },
        { 1, "Hangar" },
        { 2, "Roomba" },
        { 3, "Conservatory" },
        { 4, "EngineCore" }
    };

    public void MarkActCompleted(int actNumber)
    {
        if (actCompletionMap.ContainsKey(actNumber))
        {
            actCompletionMap[actNumber] = true;
            ActivateActButtons();
            Debug.Log($"Act {actNumber} marked as completed.");
        }
        else
        {
            Debug.LogWarning($"Attempted to mark invalid act number {actNumber} as completed.");
        }
    }

    public void LoadSelectedScene(string sceneName)
    {

        // Get the string value of the currently selected dropdown option
        string selectedSceneName = sceneName;

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

    private void ActivateActButtons()
    {
        for (int i = 0; i < actsButton.Length; i++)
        {
            if (actCompletionMap.TryGetValue(i, out bool isCompleted))
            {
                actsButton[i].interactable = isCompleted;
                Debug.Log($"[ActsManager] Button {i} ('{(actSceneMap.ContainsKey(i) ? actSceneMap[i] : "?")}') interactable set to {isCompleted}");
            }
            else
            {
                actsButton[i].interactable = false;
                Debug.Log($"[ActsManager] Button {i} ('{(actSceneMap.ContainsKey(i) ? actSceneMap[i] : "?")}') interactable set to false (no completion map entry)");
            }
        }
    }
}
