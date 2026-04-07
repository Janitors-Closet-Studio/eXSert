using UnityEngine;
using Singletons;
using System.Collections.Generic;
using UnityEngine.UI;


public class ActsManager : Singleton<ActsManager>
{
    [SerializeField] private Button[] actsButton;

    // Per-profile act completion: profileId -> (actNumber -> completed)
    private Dictionary<string, Dictionary<int, bool>> profileActCompletionMap = new Dictionary<string, Dictionary<int, bool>>();

    internal Dictionary<int, string> actSceneMap = new Dictionary<int, string>()
    {
        { 0, "Elevator" },
        { 1, "Hangar" },
        { 2, "Roomba" },
        { 3, "Conservatory" },
        { 4, "EngineCore" }
    };

    internal Dictionary<int, string> actDisplayNameMap = new Dictionary<int, string>()
    {
        { 0, "Act 1.1: Infiltration" },
        { 1, "Act 1.2: Hangar" },
        { 2, "Act 2.1: Augur Encounter" },
        { 3, "Act 2.2: Conservatory" },
        { 4, "Act 3.1: Final Encounter" }
    };

    protected override void Awake()
    {
        base.Awake();
        // Optionally, initialize a default profile for editor testing
        if (!profileActCompletionMap.ContainsKey("default"))
        {
            profileActCompletionMap["default"] = GetDefaultActCompletionMap();
        }
        // For editor preview, update using default profile
        UpdateActButtonsForProfile("default");
    }

    // Returns a new default act completion map (Act 0 unlocked, rest locked)
    private Dictionary<int, bool> GetDefaultActCompletionMap()
    {
        return new Dictionary<int, bool>()
        {
            { 0, true },
            { 1, false },
            { 2, false },
            { 3, false },
            { 4, false }
        };
    }

    // Get the farthest unlocked act name for a profile
    public string GetFarthestUnlockedActName(string profileId)
    {
        if (string.IsNullOrEmpty(profileId)) profileId = "default";
        if (!profileActCompletionMap.ContainsKey(profileId))
            profileActCompletionMap[profileId] = GetDefaultActCompletionMap();

        var map = profileActCompletionMap[profileId];
        for (int i = actsButton.Length - 1; i >= 0; i--)
        {
            if (map.TryGetValue(i, out bool isCompleted) && isCompleted)
            {
                return actDisplayNameMap.ContainsKey(i) ? actDisplayNameMap[i] : $"Act {i}";
            }
        }
        return null; // No acts completed
    }

    // Mark an act as completed for a profile
    public void MarkActCompleted(string profileId, int actNumber)
    {
        if (string.IsNullOrEmpty(profileId)) profileId = "default";
        if (!profileActCompletionMap.ContainsKey(profileId))
            profileActCompletionMap[profileId] = GetDefaultActCompletionMap();

        var map = profileActCompletionMap[profileId];
        if (map.ContainsKey(actNumber))
        {
            map[actNumber] = true;
            UpdateActButtonsForProfile(profileId);
            Debug.Log($"[ActsManager] Act {actNumber} marked as completed for profile '{profileId}'.");
        }
        else
        {
            Debug.LogWarning($"[ActsManager] Attempted to mark invalid act number {actNumber} as completed for profile '{profileId}'.");
        }
    }

    // Update the UI buttons for the given profile
    public void UpdateActButtonsForProfile(string profileId)
    {
        if (string.IsNullOrEmpty(profileId)) profileId = "default";
        if (!profileActCompletionMap.ContainsKey(profileId))
            profileActCompletionMap[profileId] = GetDefaultActCompletionMap();

        var map = profileActCompletionMap[profileId];
        for (int i = 0; i < actsButton.Length; i++)
        {
            if (map.TryGetValue(i, out bool isCompleted))
            {
                actsButton[i].interactable = isCompleted;
                Debug.Log($"[ActsManager] Button {i} ('{(actSceneMap.ContainsKey(i) ? actSceneMap[i] : "?")}') interactable set to {isCompleted} for profile '{profileId}'");
            }
            else
            {
                actsButton[i].interactable = false;
                Debug.Log($"[ActsManager] Button {i} ('{(actSceneMap.ContainsKey(i) ? actSceneMap[i] : "?")}') interactable set to false (no completion map entry) for profile '{profileId}'");
            }
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

}
