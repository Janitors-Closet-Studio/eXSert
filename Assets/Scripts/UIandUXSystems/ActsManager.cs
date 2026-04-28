using UnityEngine;
using Singletons;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Timers;
using UI.Loading;


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
        {
            pauseManager = PauseManager.Instance;
        }

        RefreshMapLocationState();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        RefreshMapLocationState();
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
        string sceneName = GetHighestLoadedTrackedSceneName();
        Debug.Log($"[ActsManager] Scene loaded: {scene.name}. Highest tracked scene: {sceneName}");

        RefreshMapLocationState();
    }

    private void RefreshMapLocationState()
    {
        string currentSceneName = GetHighestLoadedTrackedSceneName();
        if (string.IsNullOrEmpty(currentSceneName))
            return;

        ActivateAllImagesBefore();

        foreach (var kvp in sceneNames)
        {
            if (kvp.Value != currentSceneName)
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
        string currentSceneName = GetHighestLoadedTrackedSceneName();
        if (!sceneNames.ContainsValue(currentSceneName))
        {
            Debug.LogWarning($"[ActsManager] Current scene '{currentSceneName}' not found in sceneNames mapping. Cannot activate map location images.");
            return;
        }

        int mapIndex = sceneNames.First(kvp => kvp.Value == currentSceneName).Key;

        for (int i = 0; i < mapIndex; i++)
        {
            mapLocationImages[i].SetActive(true);
        }

        mapLocationImages[mapIndex].SetActive(true);
        SyncActButtonsForScene(currentSceneName);
    }

    private string GetCurrentProfileId()
    {
        string profileId = "default";
        if (DataPersistenceManager.Instance != null)
        {
            var getIdMethod = DataPersistenceManager.Instance.GetType().GetMethod("GetSelectedProfileId");
            if (getIdMethod != null)
                profileId = (string)getIdMethod.Invoke(DataPersistenceManager.Instance, null);
        }

        return string.IsNullOrEmpty(profileId) ? "default" : profileId;
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

        int highestUnlockedAct = -1;
        foreach (var kvp in actSceneMap)
        {
            if (string.Equals(kvp.Value, sceneName, System.StringComparison.Ordinal))
            {
                highestUnlockedAct = kvp.Key;
                break;
            }
        }

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

            if (actsButton[i] != null && actsButton[i].TryGetComponent(out ActButton actButton))
                actButton.RefreshVisualState();
        }
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
