using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Singletons;
using UnityEngine.InputSystem;
using UI.Loading;
using Utilities.Combat;

/// <summary>
/// Central scene loading system for additive scene loading.
/// Player content lives inside a dedicated PlayerScene that remains loaded.
/// Written by GitHub Copilot
/// </summary>
public class SceneLoader : Singleton<SceneLoader>
{
    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    
    [Header("Player Scene")]
    [SerializeField, Tooltip("Scene that contains the player prefab, UI, and related managers. This scene stays loaded during gameplay.")]
    private string playerSceneName = "PlayerScene";

    [Header("Loading Screen")]
    [SerializeField, Tooltip("Scene that contains the LoadingScreenController and supporting visuals.")]
    private string loadingSceneName = "LoadingScene";
    [SerializeField, Tooltip("Automatically load the loading scene when the main menu boots so the overlay is ready.")]
    private bool preloadLoadingScene = true;
    [SerializeField, Range(0f, 60f), Tooltip("Minimum number of seconds the loading screen should remain visible once the prop showcase appears.")]
    private float minimumLoadingScreenSeconds = 1.5f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private bool isLoadingScene = false;
    private bool loadingSceneReady = false;
    private bool loadingSceneLoadInProgress = false;
    private bool playerSceneReady = false;
    private bool playerSceneLoadInProgress = false;

    protected override void Awake()
    {
        base.Awake();

        if (preloadLoadingScene)
        {
            StartCoroutine(EnsureLoadingSceneLoadedCoroutine());
        }
    }

    /// <summary>
    /// Loads the main menu and cleans up all persistent objects (player, managers, etc.)
    /// </summary>
    public void LoadMainMenu()
    {
        if (isLoadingScene) return;
        
        Log("Loading Main Menu - Cleaning up persistent objects...");

        RunSceneRoutine(
            LoadMainMenuCoroutine(),
            pauseDuringLoading: true,
            minimumDisplayOverride: minimumLoadingScreenSeconds);
    }

    /// <summary>
    /// Loads the first gameplay scene from main menu.
    /// This is called when starting a new game or loading a save.
    /// </summary>
    /// <param name="sceneName">The initial scene to load</param>
    /// <param name="additiveSceneName">Optional additive scene to load once the base scene finishes</param>
    /// <param name="pauseUntilLoaded">If true, pauses time until both scenes have finished loading</param>
    /// <param name="spawnPointIdOverride">Optional spawn ID to place the player at once scenes finish loading</param>
    /// <param name="updateCheckpointAfterLoad">If true, writes the scene/spawn pair to CheckpointSystem after load</param>
    public void LoadInitialGameScene(
        string sceneName,
        string additiveSceneName = null,
        bool pauseUntilLoaded = false,
        string spawnPointIdOverride = null,
        bool updateCheckpointAfterLoad = true)
    {
        if (isLoadingScene) return;
        
        Log($"Loading initial game scene: {sceneName} (additive: {additiveSceneName ?? "<none>"})");

        RunSceneRoutine(
            LoadInitialGameSceneCoroutine(sceneName, additiveSceneName, pauseUntilLoaded, spawnPointIdOverride, updateCheckpointAfterLoad),
            pauseDuringLoading: true,
            minimumDisplayOverride: minimumLoadingScreenSeconds
        );
    }

    /// <summary>
    /// Loads a scene additively (for doors/transitions during gameplay).
    /// Player remains persistent.
    /// </summary>
    /// <param name="sceneName">The scene to load additively</param>
    public void LoadSceneAdditive(string sceneName)
    {
        if (isLoadingScene) return;
        
        Log($"Loading scene additively: {sceneName}");
        
        StartCoroutine(LoadSceneAdditiveCoroutine(sceneName));
    }

    /// <summary>
    /// Unloads a scene (for when player leaves an area).
    /// </summary>
    /// <param name="sceneName">The scene to unload</param>
    public void UnloadScene(string sceneName)
    {
        Log($"Unloading scene: {sceneName}");
        
        StartCoroutine(UnloadSceneCoroutine(sceneName));
    }

    /// <summary>
    /// Reloads from checkpoint - destroys player and reloads the checkpoint scene.
    /// </summary>
    public void RestartFromCheckpoint()
    {
        if (isLoadingScene) return;
        
        // Get checkpoint from system
        string checkpointSpawn = CheckpointSystem.Instance != null
            ? CheckpointSystem.Instance.GetCurrentSpawnPointID()
            : "default";

        string checkpointScene = CheckpointSystem.Instance != null 
            ? CheckpointSystem.Instance.GetCurrentSceneName() 
            : null;

        if (string.IsNullOrWhiteSpace(checkpointScene) && SpawnPoint.TryGetSceneForSpawn(checkpointSpawn, out var spawnScene))
        {
            checkpointScene = spawnScene;
        }

        if (string.IsNullOrWhiteSpace(checkpointScene))
        {
            checkpointScene = SceneManager.GetActiveScene().name;
        }
        
        Log($"Reloading checkpoint via scene load: scene='{checkpointScene}', spawn='{checkpointSpawn}'.");

        RunSceneRoutine(
            RestartFromCheckpointNonPersistentCoroutine(checkpointScene, checkpointSpawn),
            pauseDuringLoading: true,
            minimumDisplayOverride: minimumLoadingScreenSeconds);
    }

    private IEnumerator LoadMainMenuCoroutine()
    {
        isLoadingScene = true;
        
        // Resume time in case we're paused
        Time.timeScale = 1f;
        
        // Resolve a valid main menu scene name (handles different project naming)
        string targetMenu = ResolveMainMenuSceneName();
        if (string.IsNullOrEmpty(targetMenu))
        {
            Debug.LogError("[SceneLoader] Could not resolve a valid Main Menu scene name. Add your menu scene to Build Settings and set 'mainMenuSceneName' on SceneLoader.");
            isLoadingScene = false;
            yield break;
        }

        // Load main menu additively so LoadingScene stays intact
        Scene menuScene = SceneManager.GetSceneByName(targetMenu);
        if (!menuScene.isLoaded)
        {
            Log($"Loading main menu scene additively: {targetMenu}");
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetMenu, LoadSceneMode.Additive);
            if (loadOperation == null)
            {
                Debug.LogError($"[SceneLoader] LoadSceneAsync returned null for '{targetMenu}'. Is it added to Build Settings?");
                isLoadingScene = false;
                yield break;
            }

            while (!loadOperation.isDone)
            {
                yield return null;
            }

            menuScene = SceneManager.GetSceneByName(targetMenu);
        }

        if (menuScene.IsValid())
        {
            SceneManager.SetActiveScene(menuScene);
        }

        // Unload all other scenes except DontDestroyOnLoad and LoadingScene
        List<Scene> scenesToUnload = new();
        int sceneCount = SceneManager.sceneCount;
        for (int i = sceneCount - 1; i >= 0; i--)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;
            if (scene.handle == menuScene.handle)
                continue;
            if (ShouldSkipSceneForUnload(scene))
                continue;
            scenesToUnload.Add(scene);
        }

        foreach (Scene scene in scenesToUnload)
        {
            Log($"Unloading scene: {scene.name}");
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        if (!string.IsNullOrWhiteSpace(playerSceneName))
        {
            Scene playerScene = SceneManager.GetSceneByName(playerSceneName);
            if (playerScene.IsValid() && playerScene.isLoaded)
            {
                Log($"Unloading player scene: {playerScene.name}");
                yield return SceneManager.UnloadSceneAsync(playerScene);
                playerSceneReady = false;
            }
        }

        Log("Main menu loaded successfully");
        // Load binding overrides from PlayerPrefs
        if (PlayerPrefs.HasKey("InputBindingOverrides") && InputReader.PlayerInput != null)
            InputReader.PlayerInput.actions.LoadBindingOverridesFromJson(PlayerPrefs.GetString("InputBindingOverrides"));
        KeybindIconSwapper.RefreshAllIcons();
        isLoadingScene = false;
    }

    private IEnumerator LoadInitialGameSceneCoroutine(
        string sceneName,
        string additiveSceneName,
        bool pauseUntilLoaded,
        string spawnPointIdOverride,
        bool updateCheckpointAfterLoad)
    {
        isLoadingScene = true;

        bool loadingScreenManagingPause = pauseUntilLoaded && LoadingScreenController.HasInstance;
        float previousTimeScale = Time.timeScale;
        if (pauseUntilLoaded && !loadingScreenManagingPause)
        {
            Time.timeScale = 0f;
        }
        else if (!pauseUntilLoaded)
        {
            Time.timeScale = 1f;
        }

        string resolvedSpawn = ResolveSpawnPointId(spawnPointIdOverride);

        // Prepare scenes to unload after new content loads (keep persistent scenes alive)
        List<Scene> scenesToUnload = new();
        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;
            if (ShouldSkipSceneForUnload(scene))
                continue;
            if (string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(additiveSceneName)
                && string.Equals(scene.name, additiveSceneName, StringComparison.OrdinalIgnoreCase))
                continue;
            scenesToUnload.Add(scene);
        }

        // Load the first gameplay scene additively so loading overlay persists
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (loadOperation == null)
        {
            Debug.LogError($"[SceneLoader] Failed to load scene '{sceneName}'. Is it in Build Settings?");
            isLoadingScene = false;
            yield break;
        }

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        Scene baseScene = SceneManager.GetSceneByName(sceneName);
        if (baseScene.IsValid())
        {
            SceneManager.SetActiveScene(baseScene);
        }

        // Wait for scene to initialize
        yield return null;

        Log($"Initial game scene {sceneName} loaded");

        if (!string.IsNullOrWhiteSpace(additiveSceneName)
            && !additiveSceneName.Equals(sceneName, StringComparison.OrdinalIgnoreCase))
        {
            Log($"Loading queued additive scene after base load: {additiveSceneName}");
            AsyncOperation additiveOp = SceneManager.LoadSceneAsync(additiveSceneName, LoadSceneMode.Additive);
            if (additiveOp == null)
            {
                Debug.LogWarning($"[SceneLoader] Failed to queue additive scene '{additiveSceneName}'.");
            }
            else
            {
                while (!additiveOp.isDone)
                {
                    yield return null;
                }

                Log($"Additive scene {additiveSceneName} loaded");
            }
        }
        else if (!string.IsNullOrWhiteSpace(additiveSceneName)
                 && additiveSceneName.Equals(sceneName, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[SceneLoader] Cannot load additive scene '{additiveSceneName}' because it matches the base scene name.");
        }

        yield return EnsurePlayerSceneLoadedCoroutine();

        // Allow spawn point registry in the player scene to initialize
        yield return null;

        if (updateCheckpointAfterLoad && CheckpointSystem.Instance != null)
        {
            CheckpointSystem.Instance.SetCheckpoint(sceneName, resolvedSpawn);
        }

        PositionPlayerForScene(resolvedSpawn, healPlayer: false);

        // Unload any scenes that should no longer be resident (e.g., main menu)
        foreach (Scene scene in scenesToUnload)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                continue;
            Log($"Unloading previous scene: {scene.name}");
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        if (pauseUntilLoaded && !loadingScreenManagingPause)
        {
            Time.timeScale = previousTimeScale;
        }

        // Load binding overrides from PlayerPrefs
        if (PlayerPrefs.HasKey("InputBindingOverrides") && InputReader.PlayerInput != null)
            InputReader.PlayerInput.actions.LoadBindingOverridesFromJson(PlayerPrefs.GetString("InputBindingOverrides"));
        KeybindIconSwapper.RefreshAllIcons();
        isLoadingScene = false;
    }

    private IEnumerator LoadSceneAdditiveCoroutine(string sceneName)
    {
        isLoadingScene = true;
        
        // Check if scene is already loaded
        Scene existingScene = SceneManager.GetSceneByName(sceneName);
        if (existingScene.isLoaded)
        {
            Log($"Scene {sceneName} is already loaded");
            isLoadingScene = false;
            yield break;
        }
        
        // Load scene additively
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        
        while (!loadOperation.isDone)
        {
            yield return null;
        }
        
        Log($"Scene {sceneName} loaded additively");
        
        // Update checkpoint to new scene
        if (CheckpointSystem.Instance != null)
        {
            CheckpointSystem.Instance.SetCheckpoint(sceneName, "default");
        }
        
        // Load binding overrides from PlayerPrefs
        if (PlayerPrefs.HasKey("InputBindingOverrides") && InputReader.PlayerInput != null)
            InputReader.PlayerInput.actions.LoadBindingOverridesFromJson(PlayerPrefs.GetString("InputBindingOverrides"));
        KeybindIconSwapper.RefreshAllIcons();
        isLoadingScene = false;
    }

    private IEnumerator UnloadSceneCoroutine(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        
        if (!scene.isLoaded)
        {
            Log($"Scene {sceneName} is not loaded, cannot unload");
            yield break;
        }
        
        AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(sceneName);
        
        while (!unloadOperation.isDone)
        {
            yield return null;
        }
        
        Log($"Scene {sceneName} unloaded");
    }

    private IEnumerator RestartFromCheckpointNonPersistentCoroutine(string checkpointScene, string spawnPointId)
    {
        isLoadingScene = true;

        string resolvedSpawn = ResolveSpawnPointId(spawnPointId);
        string resolvedScene = ResolveSceneNameForSpawn(checkpointScene, resolvedSpawn);

        bool loadingScreenManagingPause = LoadingScreenController.HasInstance;
        float previousTimeScale = Time.timeScale;
        if (!loadingScreenManagingPause)
        {
            Time.timeScale = 0f;
        }

        Log($"Restarting gameplay scenes (non-persistent). TargetScene={resolvedScene}, Spawn={resolvedSpawn}");

        // Collect all progression scenes so we can unload them before resetting the checkpoint scene
        List<Scene> scenesToUnload = new();
        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (ShouldSkipSceneForUnload(scene))
                continue;
            scenesToUnload.Add(scene);
        }

        foreach (Scene scene in scenesToUnload)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            Log($"Unloading scene before checkpoint reload: {scene.name}");
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(scene);
            while (unloadOp != null && !unloadOp.isDone)
            {
                yield return null;
            }
        }

        yield return null;

        yield return EnsurePlayerSceneLoadedCoroutine();

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(resolvedScene, LoadSceneMode.Additive);
        if (loadOp == null)
        {
            Debug.LogError($"[SceneLoader] Failed to load checkpoint scene '{resolvedScene}'. Verify it is added to Build Settings.");
            if (!loadingScreenManagingPause)
            {
                Time.timeScale = previousTimeScale;
            }
            isLoadingScene = false;
            yield break;
        }

        while (!loadOp.isDone)
        {
            yield return null;
        }

        Scene checkpointSceneHandle = SceneManager.GetSceneByName(resolvedScene);
        if (checkpointSceneHandle.IsValid())
        {
            SceneManager.SetActiveScene(checkpointSceneHandle);
        }

        // Give newly loaded scene a frame to initialize its content
        yield return null;

        PositionPlayerForScene(resolvedSpawn, healPlayer: true);

        // Allow a short realtime delay so the loading overlay lingers
        yield return new WaitForSecondsRealtime(0.25f);

        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.ResumeGame();
        }
        else if (!loadingScreenManagingPause)
        {
            Time.timeScale = previousTimeScale;
        }

        isLoadingScene = false;
    }

    private string ResolveSpawnPointId(string requestedSpawnPointId)
    {
        if (!string.IsNullOrWhiteSpace(requestedSpawnPointId))
            return requestedSpawnPointId;

        if (CheckpointSystem.Instance != null)
        {
            string checkpointSpawn = CheckpointSystem.Instance.GetCurrentSpawnPointID();
            if (!string.IsNullOrWhiteSpace(checkpointSpawn))
                return checkpointSpawn;
        }

        return "default";
    }

    private string ResolveSceneNameForSpawn(string preferredSceneName, string spawnPointId)
    {
        if (!string.IsNullOrWhiteSpace(preferredSceneName))
            return preferredSceneName;

        if (!string.IsNullOrWhiteSpace(spawnPointId)
            && SpawnPoint.TryGetSceneForSpawn(spawnPointId, out var spawnScene)
            && !string.IsNullOrWhiteSpace(spawnScene))
        {
            return spawnScene;
        }

        return SceneManager.GetActiveScene().name;
    }

    private void PositionPlayerForScene(string spawnPointId, bool healPlayer)
    {
        string resolvedSpawn = ResolveSpawnPointId(spawnPointId);
        GameObject playerRoot = FindActivePlayerRoot();
        if (playerRoot == null)
        {
            Debug.LogWarning("[SceneLoader] Unable to find player root when attempting to position at spawn.");
            return;
        }

        PositionTransformAtSpawn(playerRoot.transform, resolvedSpawn);
        ResetPlayerStateForRespawn(playerRoot);

        if (healPlayer)
        {
            var healthManager = PlayerHealthBarManager.Instance;
            healthManager?.ForceFullHeal();
        }

        RefreshInputToCurrentPlayer();
        InputReader.inputBusy = false;
    }

    private bool ShouldSkipSceneForUnload(Scene scene)
    {
        if (!scene.isLoaded)
            return true;
        if (scene.name == "DontDestroyOnLoad")
            return true;
        if (IsLoadingSceneName(scene.name))
            return true;
        if (IsPlayerSceneName(scene.name))
            return true;
        return false;
    }

    private bool IsLoadingSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(loadingSceneName))
            return false;

        return string.Equals(sceneName, loadingSceneName, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsPlayerSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(playerSceneName))
            return false;

        return string.Equals(sceneName, playerSceneName, StringComparison.OrdinalIgnoreCase);
    }

    private void RunSceneRoutine(IEnumerator routine, bool pauseDuringLoading, float? minimumDisplayOverride = null)
    {
        if (routine == null)
        {
            Debug.LogWarning("[SceneLoader] Attempted to run a null scene routine.");
            return;
        }

        if (LoadingScreenController.HasInstance)
        {
            float minDuration = minimumDisplayOverride ?? minimumLoadingScreenSeconds;
            LoadingScreenController.Instance.BeginLoading(routine, pauseDuringLoading, minDuration);
            return;
        }

        StartCoroutine(RunAfterLoadingSceneReady(routine, pauseDuringLoading, minimumDisplayOverride));
    }

    private IEnumerator RunAfterLoadingSceneReady(IEnumerator routine, bool pauseDuringLoading, float? minimumDisplayOverride)
    {
        yield return EnsureLoadingSceneLoadedCoroutine();

        if (LoadingScreenController.HasInstance)
        {
            float minDuration = minimumDisplayOverride ?? minimumLoadingScreenSeconds;
            LoadingScreenController.Instance.BeginLoading(routine, pauseDuringLoading, minDuration);
            yield break;
        }

        // Fall back to running the routine directly if the loading screen still isn't available
        yield return routine;
    }

    private IEnumerator EnsureLoadingSceneLoadedCoroutine()
    {
        if (LoadingScreenController.HasInstance)
        {
            loadingSceneReady = true;
            yield break;
        }

        if (loadingSceneReady)
            yield break;

        if (loadingSceneLoadInProgress)
        {
            while (loadingSceneLoadInProgress && !loadingSceneReady && !LoadingScreenController.HasInstance)
            {
                yield return null;
            }
            yield break;
        }

        if (string.IsNullOrWhiteSpace(loadingSceneName))
        {
            Debug.LogWarning("[SceneLoader] Loading scene name is empty; cannot preload loading overlay.");
            yield break;
        }

        Scene loadingScene = SceneManager.GetSceneByName(loadingSceneName);
        if (loadingScene.isLoaded)
        {
            loadingSceneReady = true;
            yield break;
        }

        loadingSceneLoadInProgress = true;

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Additive);
        if (loadOp == null)
        {
            Debug.LogWarning($"[SceneLoader] Failed to load loading scene '{loadingSceneName}'. Is it added to Build Settings?");
            loadingSceneLoadInProgress = false;
            yield break;
        }

        while (!loadOp.isDone)
        {
            yield return null;
        }

        loadingSceneLoadInProgress = false;

        float waitTimer = 0f;
        const float controllerWaitTimeout = 2f;
        while (!LoadingScreenController.HasInstance && waitTimer < controllerWaitTimeout)
        {
            waitTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (LoadingScreenController.HasInstance)
        {
            loadingSceneReady = true;
        }
        else
        {
            Debug.LogWarning($"[SceneLoader] Loading scene '{loadingSceneName}' finished loading but LoadingScreenController was not found.");
        }
    }

    private IEnumerator EnsurePlayerSceneLoadedCoroutine()
    {
        if (string.IsNullOrWhiteSpace(playerSceneName))
            yield break;

        Scene playerScene = SceneManager.GetSceneByName(playerSceneName);
        if (playerScene.isLoaded)
        {
            playerSceneReady = true;
            yield break;
        }

        if (playerSceneLoadInProgress)
        {
            while (playerSceneLoadInProgress && !playerSceneReady)
            {
                yield return null;
            }
            yield break;
        }

        playerSceneLoadInProgress = true;
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(playerSceneName, LoadSceneMode.Additive);
        if (loadOp == null)
        {
            Debug.LogWarning($"[SceneLoader] Failed to load player scene '{playerSceneName}'. Is it added to Build Settings?");
            playerSceneLoadInProgress = false;
            yield break;
        }

        while (!loadOp.isDone)
        {
            yield return null;
        }

        playerSceneLoadInProgress = false;
        playerSceneReady = true;
    }


    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneLoader] {message}");
        }
    }

    /// <summary>
    /// Returns a loadable main menu scene name. Tries the configured name first, then common fallbacks.
    /// </summary>
    private string ResolveMainMenuSceneName()
    {
        // 1) Use configured value if it can be loaded
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            return mainMenuSceneName;
        }

        // 2) Try common project-specific names
        string[] candidates = new[] { "FP_MainMenu", "MainMenu", "Menu", "Title", "TitleScreen" };
        foreach (var candidate in candidates)
        {
            if (Application.CanStreamedLevelBeLoaded(candidate))
            {
                return candidate;
            }
        }

        // 3) Nothing found
        return null;
    }

    /// <summary>
    /// Find a spawn point by ID via SpawnPoint component or by tag/name fallback.
    /// </summary>
    private Transform FindSpawnPoint(string spawnPointID)
    {
        if (string.IsNullOrWhiteSpace(spawnPointID))
            spawnPointID = "default";

        if (SpawnPoint.TryGetSpawnPoint(spawnPointID, out var linked) && linked != null)
            return linked.transform;

        // Prefer SpawnPoint component
        var points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        foreach (var sp in points)
        {
            if (sp != null && sp.spawnPointID == spawnPointID)
                return sp.transform;
        }

        // Fallback by tag
        var tagged = GameObject.FindGameObjectsWithTag("PlayerSpawn");
        if (tagged != null && tagged.Length > 0)
        {
            if (spawnPointID == "default")
                return tagged[0].transform;

            foreach (var go in tagged)
            {
                if (go != null && go.name.IndexOf(spawnPointID, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return go.transform;
            }

            return tagged[0].transform; // last resort
        }

        return null;
    }

    private void PositionTransformAtSpawn(Transform target, string spawnPointId)
    {
        if (target == null)
            return;

        var sp = FindSpawnPoint(spawnPointId);
        if (sp == null)
        {
            Log($"Spawn point '{spawnPointId}' not found when positioning player.");
            return;
        }

        var controller = target.GetComponent<CharacterController>() ?? target.GetComponentInChildren<CharacterController>(true);
        bool controllerInitiallyEnabled = false;
        if (controller != null)
        {
            controllerInitiallyEnabled = controller.enabled;
            controller.enabled = false;
        }

        target.SetPositionAndRotation(sp.position, sp.rotation);

        if (controller != null && controllerInitiallyEnabled)
        {
            controller.enabled = true;
        }
    }

    private GameObject FindActivePlayerRoot()
    {
        var movement = FindAnyObjectByType<PlayerMovement>();
        if (movement != null)
            return movement.gameObject;

        var health = FindAnyObjectByType<PlayerHealthBarManager>();
        if (health != null)
            return health.gameObject;

        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
            return tagged;

        return null;
    }

    private void ResetPlayerStateForRespawn(GameObject playerRoot)
    {
        if (playerRoot == null)
            return;

        playerRoot.SetActive(true);

        ResetPlayerModelTransform(playerRoot);

        var movement = playerRoot.GetComponent<PlayerMovement>() ?? playerRoot.GetComponentInChildren<PlayerMovement>(true);
        if (movement != null)
        {
            movement.currentMovement = Vector3.zero;
            movement.ExitDeathState();
        }

        var attackManager = playerRoot.GetComponent<PlayerAttackManager>() ?? playerRoot.GetComponentInChildren<PlayerAttackManager>(true);
        attackManager?.ForceCancelCurrentAttack();

        var animationController = playerRoot.GetComponent<PlayerAnimationController>() ?? playerRoot.GetComponentInChildren<PlayerAnimationController>(true);
        animationController?.PlayIdle();

        CombatManager.ExitGuard();
    }

    private void ResetPlayerModelTransform(GameObject playerRoot)
    {
        var model = FindPlayerModelTransform(playerRoot);
        if (model == null)
            return;

        model.localPosition = new Vector3(0f, -1f, 0f);
        model.localRotation = Quaternion.identity;
    }

    private Transform FindPlayerModelTransform(GameObject playerRoot)
    {
        var named = playerRoot.transform.Find("PlayerModel");
        if (named != null && named != playerRoot.transform)
            return named;

        var animationController = playerRoot.GetComponentInChildren<PlayerAnimationController>(true);
        if (animationController != null && animationController.transform != playerRoot.transform)
            return animationController.transform;

        var animator = playerRoot.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.transform != playerRoot.transform)
            return animator.transform;

        var skinned = playerRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skinned != null && skinned.transform != playerRoot.transform)
            return skinned.transform;

        return null;
    }

    /// <summary>
    /// Finds the current scene's PlayerInput and rebinds the global InputReader to it,
    /// then switches to the Gameplay action map so movement/pause work immediately.
    /// </summary>
    private void RefreshInputToCurrentPlayer()
    {
        var ir = InputReader.Instance;
        if (ir == null) { Debug.LogWarning("[SceneLoader] InputReader instance not found to refresh."); return; }

        // Prefer a PlayerInput on the Player-tagged object if available
        PlayerInput pi = null;
        var playerTagged = GameObject.FindGameObjectWithTag("Player");
        if (playerTagged != null) pi = playerTagged.GetComponent<PlayerInput>();
        if (pi == null) pi = UnityEngine.Object.FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Exclude);

        if (pi == null)
        {
            Debug.LogWarning("[SceneLoader] No PlayerInput found to bind InputReader after scene load.");
            return;
        }

        ir.RebindTo(pi, switchToGameplay: true);
        if (!pi.enabled) pi.enabled = true;
    }
}