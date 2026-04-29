using UnityEngine;
using Singletons;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Timers;
using UI.Loading;
using UnityEngine.EventSystems;


public class ActsManager : Singleton<ActsManager>
{
    [SerializeField] private GameObject actsHolder;

    [SerializeField] private Button[] actsButton;

    [SerializeField] private Color selectedColor;
    [SerializeField] private Color highlightColor;
    [SerializeField] private Color defaultColor;

    public Color SelectedColor => selectedColor;
    public Color HighlightColor => highlightColor;
    public Color DefaultColor => defaultColor;

    // Per-profile act completion: profileId -> (actNumber -> completed)
    private Dictionary<string, Dictionary<int, bool>> profileActCompletionMap = new Dictionary<string, Dictionary<int, bool>>();
    public List<GameObject> mapLocationImages;

    public List<GameObject> foundCheckpointZones; 

    private PauseManager pauseManager;
    private Coroutine pulseCoroutine;

    private void Start()
    {
        // Try to find PauseManager if not assigned
        if (pauseManager == null)
            pauseManager = PauseManager.Instance;

        RefreshActsUiForCurrentProfile();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        RefreshActsUiForCurrentProfile();
    }

    private void OnDisable()
    {
        StopPulsingLocation();

        foreach (var img in mapLocationImages)
        {
            img.SetActive(false);
            ResetLocationVisual(img);
        }
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    internal Dictionary<int, string> sceneNames = new Dictionary<int, string>()
    {
        { 0, "Elevator" },
        { 1, "CargoBay" },
        { 2, "CrewQuarters" },
        { 3, "Hangar" },
        { 4, "ChargingStation" },
        { 5, "Conservatory" },
        { 6, "EngineCore" }
    };

    internal Dictionary<int, string> actSceneMap = new Dictionary<int, string>()
    {
        { 0, "Elevator" },
        { 1, "Hangar" },
        { 2, "ChargingStation" },
        { 3, "Conservatory" },
        { 4, "EngineCore" }
    };

    internal Dictionary<int, string> actDisplayNameMap = new Dictionary<int, string>()
    {
        { 0, "ACT 1.1: INFILTRATION" },
        { 1, "ACT 1.2: HANGAR" },
        { 2, "ACT 2.1: AUGUR ENCOUNTER" },
        { 3, "ACT 2.2: CONSERVATORY" },
        { 4, "ACT 3.1: FINAL ENCOUNTER" }
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

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[ActsManager] Scene loaded: {scene.name}. Refreshing acts UI for current profile.");
        RefreshActsUiForCurrentProfile();
    }

    private void RefreshActsUiForCurrentProfile()
    {
        UpdateActButtonsForProfile(GetCurrentProfileId());

        if (IsActsPanelOpen())
            RestoreActPreviewFromCurrentSelection();
        else
            RefreshMapLocationState();
    }

    public void RefreshNavigationMapDisplay()
    {
        RefreshMapLocationState();
    }

    private void RefreshMapLocationState()
    {
        string currentSceneName = GetCurrentLoadedTrackedSceneName();
        if (string.IsNullOrEmpty(currentSceneName))
            return;

        ActivateAllImagesBefore(currentSceneName);

        foreach (var kvp in sceneNames)
        {
            if (!SceneNameMatchesAct(currentSceneName, kvp.Value))
                continue;

            StartPulsingLocation(kvp.Key);
            break;
        }

        ResetNonCurrentLocationVisuals(currentSceneName);
    }

    private IEnumerator PulseColorForMapIfInRespectiveScene(float pulseDuration, GameObject locationRoot = null)
    {
        if (locationRoot == null)
            yield break;

        List<Image> pulseImages = GetPulseImages(locationRoot);
        if (pulseImages.Count == 0)
        {
            Debug.LogWarning($"[ActsManager] Map location '{locationRoot.name}' does not have any Image components to pulse.");
            yield break;
        }

        locationRoot.SetActive(true);

        // Pulse indefinitely while the scene is active
        float elapsedTime = 0f;
        while (true)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = (Mathf.Sin(elapsedTime / pulseDuration * Mathf.PI * 2) + 1f) / 2f; // Oscillates between 0 and 1
            Color targetColor = Color.Lerp(defaultColor, highlightColor, t);

            foreach (Image pulseImage in pulseImages)
            {
                if (pulseImage != null)
                    pulseImage.color = targetColor;
            }

            yield return null;
        }
    }

    private void StartPulsingLocation(int locationIndex)
    {
        currentPulsingSceneIndex = locationIndex;
        StopPulsingLocation();

        if (locationIndex < 0 || locationIndex >= mapLocationImages.Count)
            return;

        GameObject pulseTarget = mapLocationImages[locationIndex];
        if (pulseTarget == null)
            return;

        pulseCoroutine = StartCoroutine(PulseColorForMapIfInRespectiveScene(2f, pulseTarget));
    }

    private void StopPulsingLocation()
    {
        if (pulseCoroutine == null)
            return;

        StopCoroutine(pulseCoroutine);
        pulseCoroutine = null;
    }

    private void ResetNonCurrentLocationVisuals(string currentSceneName)
    {
        foreach (var kvp in sceneNames)
        {
            if (kvp.Value == currentSceneName)
                continue;

            ResetLocationVisual(mapLocationImages[kvp.Key]);
        }
    }

    private void ResetLocationVisual(GameObject locationRoot)
    {
        if (locationRoot == null)
            return;

        foreach (Image pulseImage in GetPulseImages(locationRoot))
        {
            if (pulseImage != null)
                pulseImage.color = defaultColor;
        }
    }

    private List<Image> GetPulseImages(GameObject locationRoot)
    {
        List<Image> pulseImages = new List<Image>();

        if (locationRoot == null)
            return pulseImages;

        Image[] images = locationRoot.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image == null)
                continue;

            if (image.gameObject.name.EndsWith("_Image", System.StringComparison.Ordinal))
                pulseImages.Add(image);
        }

        if (pulseImages.Count > 0)
            return pulseImages;

        foreach (Image image in images)
        {
            if (image == null)
                continue;

            pulseImages.Add(image);
        }

        if (pulseImages.Count == 0)
        {
            Image rootImage = locationRoot.GetComponent<Image>();
            if (rootImage != null)
                pulseImages.Add(rootImage);
        }

        return pulseImages;
    }

    public void ActivateAllImagesBefore()
    {
        string currentSceneName = GetCurrentLoadedTrackedSceneName();
        ActivateAllImagesBefore(currentSceneName);
    }

    private void ActivateAllImagesBefore(string currentSceneName)
    {
        if (!sceneNames.ContainsValue(currentSceneName))
        {
            int fallbackIndex = -1;
            foreach (var kvp in sceneNames)
            {
                if (!SceneNameMatchesAct(currentSceneName, kvp.Value))
                    continue;

                fallbackIndex = kvp.Key;
                break;
            }

            if (fallbackIndex < 0)
            {
                Debug.LogWarning($"[ActsManager] Current scene '{currentSceneName}' not found in sceneNames mapping. Cannot activate map location images.");
                return;
            }

            HideAllMapLocationImages();
            for (int i = 0; i <= fallbackIndex && i < mapLocationImages.Count; i++)
            {
                if (mapLocationImages[i] != null)
                    mapLocationImages[i].SetActive(true);
            }

            return;
        }

        HideAllMapLocationImages();

        int mapIndex = sceneNames.First(kvp => kvp.Value == currentSceneName).Key;

        for (int i = 0; i < mapIndex; i++)
        {
            mapLocationImages[i].SetActive(true);
        }

        mapLocationImages[mapIndex].SetActive(true);
    }

    public void HideAllMapLocationImages()
    {
        StopPulsingLocation();

        foreach (GameObject imageRoot in EnumerateAllPreviewRoots())
        {
            if (imageRoot == null)
                continue;

            imageRoot.SetActive(false);
            ResetLocationVisual(imageRoot);
        }
    }

    public void ShowActPreview(int actNumber)
    {
        if (!IsActsPanelOpen())
            return;

        HideAllMapLocationImages();

        if (actsButton == null)
            return;

        HashSet<GameObject> previewRoots = GetPreviewRootsForAct(actNumber);

        foreach (GameObject previewRoot in previewRoots)
        {
            previewRoot.SetActive(true);
            ResetLocationVisual(previewRoot);
        }
    }

    public void RestoreActPreviewFromCurrentSelection()
    {
        if (!IsActsPanelOpen())
        {
            RefreshMapLocationState();
            return;
        }

        if (TryGetPreviewActButton(EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null, out ActButton selectedActButton))
        {
            ShowActPreview(selectedActButton.ActNumber);
            return;
        }

        if (actsButton != null)
        {
            foreach (Button button in actsButton)
            {
                if (button == null || !button.IsInteractable() || !button.gameObject.activeInHierarchy)
                    continue;

                if (TryGetPreviewActButton(button.gameObject, out ActButton fallbackActButton))
                {
                    ShowActPreview(fallbackActButton.ActNumber);
                    return;
                }
            }
        }

        HideAllMapLocationImages();
    }

    private static bool TryGetPreviewActButton(GameObject candidate, out ActButton actButton)
    {
        actButton = null;

        if (candidate == null)
            return false;

        if (!candidate.TryGetComponent(out actButton))
            return false;

        Button button = candidate.GetComponent<Button>();
        return button != null && button.IsInteractable();
    }

    private HashSet<GameObject> GetPreviewRootsForAct(int actNumber)
    {
        HashSet<GameObject> previewRoots = new HashSet<GameObject>();

        if (actsButton == null)
            return previewRoots;

        for (int index = 0; index < actsButton.Length; index++)
        {
            if (index > actNumber)
                break;

            Button actEntryButton = actsButton[index];
            if (actEntryButton == null || !actEntryButton.TryGetComponent(out ActButton actButton))
                continue;

            foreach (GameObject previewRoot in actButton.GetPreviewRoots())
            {
                if (previewRoot != null)
                    previewRoots.Add(previewRoot);
            }
        }

        return previewRoots;
    }

    private IEnumerable<GameObject> EnumerateAllPreviewRoots()
    {
        HashSet<GameObject> uniqueRoots = new HashSet<GameObject>();

        if (mapLocationImages != null)
        {
            foreach (GameObject imageRoot in mapLocationImages)
            {
                if (imageRoot != null && uniqueRoots.Add(imageRoot))
                    yield return imageRoot;
            }
        }

        if (actsButton == null)
            yield break;

        foreach (Button actEntryButton in actsButton)
        {
            if (actEntryButton == null || !actEntryButton.TryGetComponent(out ActButton actButton))
                continue;

            foreach (GameObject previewRoot in actButton.GetPreviewRoots())
            {
                if (previewRoot != null && uniqueRoots.Add(previewRoot))
                    yield return previewRoot;
            }
        }
    }

    private string GetCurrentProfileId()
    {
        string profileId = DataPersistenceManager.GetSelectedProfileId();
        return string.IsNullOrEmpty(profileId) ? "default" : profileId;
    }

    private void SyncActCompletionFromProfileData(string profileId)
    {
        if (string.IsNullOrEmpty(profileId))
            profileId = "default";

        profileActCompletionMap[profileId] = GetDefaultActCompletionMap();

        Dictionary<string, GameData> profiles = DataPersistenceManager.GetAllProfilesGameData() ?? new Dictionary<string, GameData>();
        profiles.TryGetValue(profileId, out GameData profileData);

        int highestUnlockedAct = ResolveHighestUnlockedAct(profileData);
        if (highestUnlockedAct < 0)
            return;

        Dictionary<int, bool> completionMap = profileActCompletionMap[profileId];
        for (int actIndex = 0; actIndex <= highestUnlockedAct; actIndex++)
        {
            if (completionMap.ContainsKey(actIndex))
                completionMap[actIndex] = true;
        }
    }

    private int ResolveHighestUnlockedAct(GameData profileData)
    {
        if (profileData != null)
        {
            int savedHighest = Mathf.Max(0, profileData.highestUnlockedActIndex);
            int inferredHighest = ResolveHighestUnlockedActFromSceneName(GetSavedProgressSceneName(profileData));
            return Mathf.Max(savedHighest, inferredHighest);
        }

        string loadedSceneName = GetCurrentLoadedTrackedSceneName();
        return ResolveHighestUnlockedActFromSceneName(loadedSceneName);
    }

    private int ResolveHighestUnlockedActFromSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return -1;

        int highestUnlockedAct = -1;
        foreach (var kvp in actSceneMap)
        {
            if (!SceneNameMatchesAct(sceneName, kvp.Value))
                continue;

            highestUnlockedAct = Mathf.Max(highestUnlockedAct, kvp.Key);
        }

        return highestUnlockedAct;
    }

    private string GetSavedProgressSceneName(GameData profileData)
    {
        if (profileData != null)
        {
            if (!string.IsNullOrWhiteSpace(profileData.currentSceneName))
                return profileData.currentSceneName;

            if (!string.IsNullOrWhiteSpace(profileData.lastSavedScene))
                return profileData.lastSavedScene;
        }

        return null;
    }

    private string GetCurrentLoadedTrackedSceneName()
    {
        Progression.Checkpoints.CheckpointBehavior activeCheckpoint = Progression.Checkpoints.CheckpointBehavior.currentCheckpoint;
        if (activeCheckpoint != null
            && activeCheckpoint.CheckpointSceneAsset != null
            && IsTrackedSceneName(activeCheckpoint.CheckpointSceneAsset.SceneName)
            && SceneManager.GetSceneByName(activeCheckpoint.CheckpointSceneAsset.SceneName).isLoaded)
        {
            return activeCheckpoint.CheckpointSceneAsset.SceneName;
        }

        return GetHighestLoadedTrackedSceneName();
    }

    private bool IsTrackedSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        foreach (var kvp in sceneNames)
        {
            if (SceneNameMatchesAct(sceneName, kvp.Value))
                return true;
        }

        return false;
    }

    private bool IsActsPanelOpen()
    {
        return actsHolder != null && actsHolder.activeInHierarchy;
    }

    private static bool SceneNameMatchesAct(string sceneName, string actSceneToken)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(actSceneToken))
            return false;

        return sceneName.IndexOf(actSceneToken, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string GetHighestLoadedTrackedSceneName()
    {
        int highestSceneIndex = -1;
        string highestSceneName = null;

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(sceneIndex);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                continue;

            foreach (var kvp in sceneNames)
            {
                if (!string.Equals(kvp.Value, loadedScene.name, System.StringComparison.Ordinal))
                    continue;

                if (kvp.Key > highestSceneIndex)
                {
                    highestSceneIndex = kvp.Key;
                    highestSceneName = kvp.Value;
                }

                break;
            }
        }

        return highestSceneName;
    }

    private void SyncActButtonsForScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        int highestUnlockedAct = ResolveHighestUnlockedActFromSceneName(sceneName);

        if (highestUnlockedAct < 0)
            return;

        string profileId = GetCurrentProfileId();
        if (!profileActCompletionMap.ContainsKey(profileId))
            profileActCompletionMap[profileId] = GetDefaultActCompletionMap();

        var map = profileActCompletionMap[profileId];
        for (int actIndex = 0; actIndex <= highestUnlockedAct; actIndex++)
        {
            if (map.ContainsKey(actIndex))
                map[actIndex] = true;
        }

        UpdateActButtonsForProfile(profileId);
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
        int highestUnlockedAct = Mathf.Max(0, GetHighestUnlockedActForProfile(profileId));
        return actDisplayNameMap.ContainsKey(highestUnlockedAct) ? actDisplayNameMap[highestUnlockedAct] : $"Act {highestUnlockedAct}";
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
        SyncActCompletionFromProfileData(profileId);
        if (!profileActCompletionMap.ContainsKey(profileId))
            profileActCompletionMap[profileId] = GetDefaultActCompletionMap();

        int highestUnlockedAct = Mathf.Max(0, GetHighestUnlockedActForProfile(profileId));
        for (int i = 0; i < actsButton.Length; i++)
        {
            bool isCompleted = i <= highestUnlockedAct;
            actsButton[i].interactable = isCompleted;
            Debug.Log($"[ActsManager] Button {i} ('{(actSceneMap.ContainsKey(i) ? actSceneMap[i] : "?")}') interactable set to {isCompleted} for profile '{profileId}' using highest unlocked act {highestUnlockedAct}");

            if (actsButton[i] != null && actsButton[i].TryGetComponent(out ActButton actButton))
                actButton.RefreshVisualState();
        }

        RefreshActButtonNavigation();

        RestoreActPreviewFromCurrentSelection();
    }

    private void RefreshActButtonNavigation()
    {
        if (actsButton == null || actsButton.Length == 0)
            return;

        List<Button> enabledButtons = new List<Button>();
        for (int i = 0; i < actsButton.Length; i++)
        {
            Button button = actsButton[i];
            if (button == null)
                continue;

            Navigation navigation = button.navigation;
            if (!button.IsInteractable())
            {
                navigation.mode = Navigation.Mode.None;
                navigation.selectOnUp = null;
                navigation.selectOnDown = null;
                button.navigation = navigation;
                continue;
            }

            enabledButtons.Add(button);
        }

        for (int i = 0; i < enabledButtons.Count; i++)
        {
            Button button = enabledButtons[i];
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = i > 0 ? enabledButtons[i - 1] : null;
            navigation.selectOnDown = i < enabledButtons.Count - 1 ? enabledButtons[i + 1] : null;
            button.navigation = navigation;
        }

        if (EventSystem.current == null)
            return;

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected == null)
            return;

        Button selectedButton = currentSelected.GetComponent<Button>();
        if (selectedButton != null && selectedButton.IsInteractable())
            return;

        if (enabledButtons.Count == 0)
        {
            EventSystem.current.SetSelectedGameObject(null);
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(enabledButtons[0].gameObject);
    }

    private int GetHighestUnlockedActForProfile(string profileId)
    {
        if (string.IsNullOrEmpty(profileId))
            profileId = "default";

        Dictionary<string, GameData> profiles = DataPersistenceManager.GetAllProfilesGameData() ?? new Dictionary<string, GameData>();
        profiles.TryGetValue(profileId, out GameData profileData);

        return ResolveHighestUnlockedAct(profileData);
    }


    /// <summary>
    /// Finds a checkpoint in the given scene and sets it as the current checkpoint.
    /// </summary>
    private void SetCheckpointForScene(string sceneName)
    {
        // Find all loaded checkpoints
        var checkpoints = GameObject.FindObjectsByType<Progression.Checkpoints.CheckpointBehavior>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var checkpoint in checkpoints)
        {
            var sceneAsset = checkpoint.CheckpointSceneAsset;
            if (sceneAsset != null && string.Equals(sceneAsset.SceneName, sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                Progression.Checkpoints.CheckpointBehavior.OverrideCurrentCheckpoint(checkpoint, true);
                Debug.Log($"[ActsManager] Set checkpoint '{checkpoint.CheckpointId}' as current for scene '{sceneName}'.");
                return;
            }
        }
        Debug.LogWarning($"[ActsManager] No checkpoint found for scene '{sceneName}'. Player will respawn at the last checkpoint.");
    }

    private void PrepareForSceneLoad(bool resumeImmediately)
    {
        if (pauseManager == null)
            pauseManager = PauseManager.Instance;

        if (pauseManager != null)
        {
            if (resumeImmediately)
                pauseManager.ResumeGame();
            else
                pauseManager.HideMenusForSceneTransition();
        }
        else if (resumeImmediately)
        {
            Time.timeScale = 1f;
        }
    }

    private static bool TryResolveSceneAssetByName(string rawSceneName, out SceneAsset sceneAsset)
    {
        sceneAsset = null;
        if (string.IsNullOrWhiteSpace(rawSceneName))
            return false;

        string trimmedName = rawSceneName.Trim();
        sceneAsset = SceneAsset.GetSceneAsset(trimmedName);
        if (sceneAsset != null)
            return true;

        SceneAsset[] allSceneAssets = Resources.LoadAll<SceneAsset>("Scene Assets");
        if (allSceneAssets == null || allSceneAssets.Length == 0)
            return false;

        for (int i = 0; i < allSceneAssets.Length; i++)
        {
            SceneAsset candidate = allSceneAssets[i];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.SceneName, trimmedName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            sceneAsset = candidate;
            return true;
        }

        return false;
    }

    /// Loads the given scene, then respawns the player at a checkpoint in that scene.
    public void LoadSceneAndRespawnAtCheckpoint(string sceneName)
    {
        if (!TryResolveSceneAssetByName(sceneName, out SceneAsset sceneAsset) || sceneAsset == null)
        {
            Debug.LogError($"[ActsManager] Unable to resolve SceneAsset from '{sceneName}'. Teleport canceled.");
            return;
        }

        InputReader.inputBusy = false;
        if (InputReader.PlayerInput != null)
            InputReader.PlayerInput.SwitchCurrentActionMap("Gameplay");

        if (SoundManager.Instance != null)
            StartCoroutine(SoundManager.Instance.FadeOutGameplayAudio(1f)); // Fade out music over 1 second

        PrepareForSceneLoad(resumeImmediately: false);

        // Unload other tracked gameplay scenes first so special transitions (like EngineCore)
        // do not stack over previously loaded scenes.
        // Use CoroutineRunner so this flow survives unloading the scene that owns ActsManager.
        CoroutineRunner.Run(LoadActSceneFromCleanState(sceneAsset));

        SceneAsset currentSceneAsset = SceneAsset.GetSceneAssetOfObject(this.gameObject);
        MasterObjectiveClass masterObjective = currentSceneAsset != null
            ? MasterObjectiveClass.GetInstance(currentSceneAsset)
            : FindFirstObjectByType<MasterObjectiveClass>(FindObjectsInactive.Include);
        if (masterObjective != null)
            masterObjective.ForceStopNoticeCoroutines();

        if (actsHolder != null)
            actsHolder.SetActive(false);

        MenuListManager menuListManager = GetComponent<MenuListManager>();

        if (menuListManager != null)
        {
            menuListManager.ClearMenuList();
        }
    }

    private IEnumerator LoadActSceneFromCleanState(SceneAsset targetScene)
    {
        if (targetScene == null)
            yield break;

        if (InternalPlayerInventory.Instance != null)
            InternalPlayerInventory.Instance.ResetCollectedItems();

        string targetName = targetScene.SceneName;

        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                continue;

            bool isTrackedGameplayScene = sceneNames.ContainsValue(loadedScene.name);
            if (!isTrackedGameplayScene)
                continue;

            if (string.Equals(loadedScene.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(loadedScene);
            if (unloadOperation != null)
                yield return unloadOperation;
        }

        // Use the same startup flow as initial game load, but force-reload the selected scene
        // so collectibles, encounters, and scene-local runtime state reset every teleport.
        Player.TriggerRespawn();
        SceneLoader.LoadIntoGame(targetScene, newGame: false, forceReloadFirstScene: true);

        // If transition stalls and target gameplay scene never appears,
        // force-load it so acts teleport cannot strand the player on a black screen.
        yield return EnsureActSceneLoadedOrRecover(targetScene);
    }

    private IEnumerator EnsureActSceneLoadedOrRecover(SceneAsset targetScene)
    {
        if (targetScene == null)
            yield break;

        const float timeoutSeconds = 8f;
        float elapsed = 0f;

        while (elapsed < timeoutSeconds)
        {
            Scene targetLoadedScene = SceneManager.GetSceneByName(targetScene.SceneName);
            if (targetLoadedScene.IsValid() && targetLoadedScene.isLoaded)
            {
                yield return FinalizeActTeleportState("ActsManager.EnsureActSceneLoadedOrRecover.SuccessPath");
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning($"[ActsManager] Act teleport recovery triggered for '{targetScene.SceneName}'. Target scene did not load in time.");

        AsyncOperation fallbackLoadOperation = SceneManager.LoadSceneAsync(targetScene.SceneName, LoadSceneMode.Additive);
        if (fallbackLoadOperation != null)
            yield return fallbackLoadOperation;

        yield return FinalizeActTeleportState("ActsManager.EnsureActSceneLoadedOrRecover.FallbackPath");

        Player.SpawnPlayerAtCheckpoint();
    }

    private static IEnumerator FinalizeActTeleportState(string context)
    {
        const float loadingSettleTimeoutSeconds = 5f;
        float elapsed = 0f;

         if (PauseManager.Instance != null)
            PauseManager.Instance.SetGameplayHUDVisible(true);

        PlayerCanvasManager canvasManager = Object.FindFirstObjectByType<PlayerCanvasManager>(FindObjectsInactive.Include);
        if (canvasManager != null)
            canvasManager.SetPlayerCanvasVisible(true);

        // Re-assert one frame later in case late scene-load callbacks toggle UI state.
        yield return null;

        if (PauseManager.Instance != null)
            PauseManager.Instance.SetGameplayHUDVisible(true);

        if (canvasManager != null)
            canvasManager.SetPlayerCanvasVisible(true);

        while (LoadingScreenController.IsLoading && elapsed < loadingSettleTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        InputReader.ForceResetInputLocks(context);

        if (InputReader.PlayerInput != null)
            InputReader.PlayerInput.SwitchCurrentActionMap("Gameplay");

       

    }


    private IEnumerator LoadSceneAndRespawnCoroutine(SceneAsset sceneName)
    {
        Debug.Log($"[ActsManager] Loading scene '{sceneName}' and will respawn at checkpoint.");
        PrepareForSceneLoad(resumeImmediately: false);

        // Start loading the scene
        var asyncOp = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncOp.isDone)
            yield return null;

        // Wait one frame to ensure all objects are initialized
        yield return null;

        // Find a checkpoint in the loaded scene and set it as current
        SetCheckpointForScene(sceneName);

    }

    private int currentPulsingSceneIndex = -1;
}
