using System;
using System.Collections;
using System.Collections.Generic;
using UI.Loading;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Video;
using Progression;
using Progression.Checkpoints;

/// <summary>
/// Centralized static class responsible for scene loading/unloading and player-scene specific behaviors.
/// SceneAsset remains a data container; use this class for all runtime scene management.
/// </summary>
public static class SceneLoader
{
    private sealed class PreparedSceneLoad
    {
        public SceneAsset SceneAsset;
        public AsyncOperation Operation;
    }

    private const string PLAYER_SCENE = "PlayerScene"; // The name of the player scene
    private const string MAIN_MENU_SCENE = "MainMenu"; // The name of the main menu scene
    private const string LOADING_SCENE = "LoadingScene";
    public const string EditorBootstrapLoadingScreenOwnerId = "ProgressionManager.EditorIsolatedLoad";
    private static readonly HashSet<string> loadingScreenSuppressionOwners = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, PreparedSceneLoad> preparedSceneLoads = new(StringComparer.Ordinal);

    /// <summary>Number of currently loaded scenes.</summary>
    public static int LoadedSceneCount => SceneManager.sceneCount;

    public static bool IsLoadingScreenSuppressed => loadingScreenSuppressionOwners.Count > 0;

    /// <summary>Whether the player scene is loaded.</summary>
    public static bool PlayerLoaded
    {
        get
        {
            var playerSceneAsset = (SceneAsset)PLAYER_SCENE;
            try
            {
                return playerSceneAsset != null ? playerSceneAsset.IsLoaded() : SceneManager.GetSceneByName(PLAYER_SCENE).isLoaded;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Scene Loader] Error checking loaded state for '{PLAYER_SCENE}': {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[Scene Loader] Scene loaded callback: '{scene.name}' with mode {mode}. LoadedSceneCount now: {LoadedSceneCount}.");
        if (scene.name == PLAYER_SCENE)
        {
            GameObject player = Player.PlayerObject;
            if (player == null) // Player null check. Shouldn't occur if set up properly
                Debug.LogError("[Scene Loader] Player object not found in the scene after loading the player scene. " +
                               "Ensure that the player scene contains a GameObject tagged 'Player' and that SceneAsset points to the correct scene.");
        }
        else
        {
            SceneManager.SetActiveScene(scene);
        }
    }

    public static void Initialize()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static string RequestLoadingScreenSuppression(string ownerId = null)
    {
        string resolvedOwnerId = string.IsNullOrWhiteSpace(ownerId)
            ? Guid.NewGuid().ToString("N")
            : ownerId;

        loadingScreenSuppressionOwners.Add(resolvedOwnerId);
        return resolvedOwnerId;
    }

    public static void ReleaseLoadingScreenSuppression(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return;

        loadingScreenSuppressionOwners.Remove(ownerId);
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SuppressLoadingScreenForEditorIsolatedBootstrap()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return;

        string sceneName = activeScene.name;
        if (string.IsNullOrEmpty(sceneName))
            return;

        if (sceneName == MAIN_MENU_SCENE || sceneName == LOADING_SCENE || sceneName == PLAYER_SCENE)
            return;

        if (SceneManager.sceneCount != 1)
            return;

        RequestLoadingScreenSuppression(EditorBootstrapLoadingScreenOwnerId);
        Debug.Log($"[Scene Loader][Editor Bootstrap] Suppressing loading screen before scene startup for '{sceneName}'.");
    }
#endif

    /// <summary>Event invoked when a forced scene reload is requested.</summary>
    public static Action OnSceneReloaded { get; internal set; }

    public static bool IsScenePrepared(SceneAsset scene)
    {
        if (scene == null)
            return false;

        if (!preparedSceneLoads.TryGetValue(scene.SceneName, out PreparedSceneLoad preparedScene))
            return false;

        return preparedScene.Operation != null && preparedScene.Operation.progress >= 0.9f;
    }

    public static AsyncOperation PreloadAdditive(SceneAsset scene)
    {
        if (scene == null)
        {
            Debug.LogError("[Scene Loader] Cannot preload a null SceneAsset.");
            return null;
        }

        if (scene.IsLoaded())
        {
            Debug.LogWarning($"[Scene Loader] Scene '{scene.SceneName}' is already loaded. Preload request ignored.");
            return null;
        }

        if (preparedSceneLoads.TryGetValue(scene.SceneName, out PreparedSceneLoad existingPreparedScene))
            return existingPreparedScene.Operation;

        AsyncOperation operation = SceneManager.LoadSceneAsync(scene.SceneName, LoadSceneMode.Additive);
        if (operation == null)
        {
            Debug.LogError($"[Scene Loader] Failed to start preload for '{scene.SceneName}'.");
            return null;
        }

        operation.allowSceneActivation = false;
        preparedSceneLoads[scene.SceneName] = new PreparedSceneLoad
        {
            SceneAsset = scene,
            Operation = operation,
        };

        operation.completed += _ => preparedSceneLoads.Remove(scene.SceneName);
        return operation;
    }

    public static AsyncOperation ActivatePreparedScene(SceneAsset scene, bool loadScreen = false)
    {
        if (scene == null)
        {
            Debug.LogError("[Scene Loader] Cannot activate a null SceneAsset.");
            return null;
        }

        if (scene.IsLoaded())
        {
            Debug.LogWarning($"[Scene Loader] Scene '{scene.SceneName}' is already loaded. Activation request ignored.");
            return null;
        }

        if (preparedSceneLoads.TryGetValue(scene.SceneName, out PreparedSceneLoad preparedScene)
            && preparedScene.Operation != null)
        {
            preparedScene.Operation.allowSceneActivation = true;
            return preparedScene.Operation;
        }

        return Load(scene, loadScreen: loadScreen);
    }

    /// <summary>Loads a SceneAsset (additive). Returns the AsyncOperation or null on error. (Legacy API)</summary>
    public static AsyncOperation Load(SceneAsset scene, bool forceReload = false, bool loadScreen = true)
    {
        Debug.Log($"[Scene Loader] Request to load scene '{scene}' with forceReload={forceReload}. Current loaded scenes: {LoadedSceneCount}.");
        if (scene == null)
        {
            Debug.LogError("Cannot load a null SceneAsset.");
            return null;
        }

        if (!forceReload && preparedSceneLoads.TryGetValue(scene.SceneName, out PreparedSceneLoad preparedScene)
            && preparedScene.Operation != null)
        {
            preparedScene.Operation.allowSceneActivation = true;
            return preparedScene.Operation;
        }

        if (forceReload) OnSceneReloaded?.Invoke();

        if (loadScreen && !IsLoadingScreenSuppressed)
        {
#if UNITY_EDITOR
            Debug.Log($"[Scene Loader][Editor Trace] Load requested loading screen for scene '{scene.SceneName}'. Stack:\n{Environment.StackTrace}");
#endif
            if (!SceneAsset.GetSceneAsset("LoadingScene").IsLoaded())
            {
                AsyncOperation loadingScreenOp = SceneManager.LoadSceneAsync("LoadingScene", LoadSceneMode.Additive);
                loadingScreenOp.completed += static _ => LoadScreen.StartLoading();
            }
            else LoadScreen.StartLoading();
        }

        // Async Operation Setup
        AsyncOperation operation = null;

        // Check if the scene is already loaded before attempting to load it again.
        // If forceReload is false, exit with a warning.
        if (scene.IsLoaded() && !forceReload)
        {
            Debug.LogWarning($"Scene '{scene.SceneName}' is already loaded. Use forceReload=true to reload it.");
            return null;
        }

        // If the scene is loaded and forceReload is true, unload it first before loading again.
        if (scene.IsLoaded())
        {
            SceneManager.UnloadSceneAsync((Scene)scene).completed +=
                operation => SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
        }

        else operation = SceneManager.LoadSceneAsync(scene.SceneName, LoadSceneMode.Additive);

        return operation;
    }

    /// <summary>
    /// Coroutine-compatible load method that yields while the requested scene loads.
    /// Use this when you want to run loading as a coroutine (for example, with LoadingScreenController.BeginLoading).
    /// </summary>
    public static IEnumerator LoadCoroutine(SceneAsset scene, bool forceReload = false, bool loadScreen = true)
    {
        Debug.Log($"[Scene Loader] Coroutine request to load scene '{scene}' with forceReload={forceReload}. Current loaded scenes: {LoadedSceneCount}.");
        if (scene == null)
        {
            Debug.LogError("Cannot load a null SceneAsset.");
            yield break;
        }

        if (!forceReload && preparedSceneLoads.TryGetValue(scene.SceneName, out PreparedSceneLoad preparedScene)
            && preparedScene.Operation != null)
        {
            preparedScene.Operation.allowSceneActivation = true;
            yield return preparedScene.Operation;
            yield break;
        }

        if (forceReload) OnSceneReloaded?.Invoke();

        // Start or ensure loading screen is present and started before loading target scene
        if (loadScreen && !IsLoadingScreenSuppressed)
        {
    #if UNITY_EDITOR
            Debug.Log($"[Scene Loader][Editor Trace] Coroutine load requested loading screen for scene '{scene.SceneName}'. Stack:\n{Environment.StackTrace}");
    #endif
            var loadingSceneAsset = SceneAsset.GetSceneAsset("LoadingScene");
            if (!loadingSceneAsset.IsLoaded())
            {
                var loadingScreenOp = SceneManager.LoadSceneAsync("LoadingScene", LoadSceneMode.Additive);
                if (loadingScreenOp == null)
                {
                    Debug.LogError("[Scene Loader] Failed to start loading 'LoadingScene' asynchronously.");
                }
                else
                {
                    yield return loadingScreenOp;
                    LoadScreen.StartLoading();
                }
            }
            else
            {
                LoadScreen.StartLoading();
            }
        }

        // If already loaded and not forcing a reload, just warn and exit.
        if (scene.IsLoaded() && !forceReload)
        {
            Debug.LogWarning($"Scene '{scene.SceneName}' is already loaded. Use forceReload=true to reload it.");
            yield break;
        }

        // If the scene is loaded and forceReload is requested, unload it first then load
        if (scene.IsLoaded())
        {
            var unloadOp = SceneManager.UnloadSceneAsync((Scene)scene);
            if (unloadOp == null)
            {
                Debug.LogError($"[Scene Loader] Failed to start unload for '{scene.SceneName}'. Aborting coroutine load.");
                yield break;
            }
            yield return unloadOp;
            var reloadOp = SceneManager.LoadSceneAsync(scene.SceneName, LoadSceneMode.Additive);
            if (reloadOp == null)
            {
                Debug.LogError($"[Scene Loader] Failed to start reload for '{scene.SceneName}'.");
                yield break;
            }
            yield return reloadOp;
        }
        else
        {
            var operation = SceneManager.LoadSceneAsync(scene.SceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                Debug.LogError($"[Scene Loader] Failed to start async load for '{scene.SceneName}'.");
                yield break;
            }
            yield return operation;
        }
    }

    /// <summary>Load the first gameplay scene then the player scene. (Legacy API)</summary>
    public static void LoadIntoGame(SceneAsset firstScene, bool newGame = false)
    {
        if (firstScene == null)
        {
            Debug.LogError("Cannot load a null SceneAsset into the game.");
            return;
        }

        Initialize();

        CoroutineRunner.Run(LoadIntoGameTransitionRoutine(firstScene, newGame));

        static IEnumerator LoadIntoGameTransitionRoutine(SceneAsset firstScene, bool newGame)
        {
            if (newGame)
            {
                yield return EnsureLoadingSceneReady();

                VideoClip openingCutscene = Cutscene.GetCutscene("Opening Cutscene");
                if (openingCutscene != null)
                {
                    CutsceneManager.PlayCutscene(openingCutscene);
                    yield return LoadIntoGameCoroutine(firstScene, newGame: false);
                    yield break;
                }

                Debug.LogWarning("[Scene Loader] Opening cutscene is unavailable. Falling back to standard loading-screen startup.");
            }

            yield return EnsureLoadingSceneReady();

            if (!LoadingScreenController.HasInstance)
            {
                Debug.LogWarning("[Scene Loader] LoadingScreenController is unavailable. Falling back to direct game load.");
                yield return LoadIntoGameCoroutine(firstScene, newGame);
                yield break;
            }

            LoadingScreenController.BeginLoading(LoadIntoGameCoroutine(firstScene, newGame));
        }
    }

    private static IEnumerator LoadIntoGameCoroutine(SceneAsset firstScene, bool newGame)
    {
        // Load first gameplay scene, wait for it
        yield return LoadCoroutine(firstScene, loadScreen: false);

        // Sets the first checkpoint to the first scene so that the player will spawn there when the player scene loads
        CheckpointBehavior.OverrideCurrentCheckpoint(ProgressionManager.GetInstance(firstScene).FirstCheckpoint);

        // Load player scene, wait for it
        yield return LoadPlayerSceneCoroutine();

        // Unload main menu (string overload)
        var mainMenuAsset = (SceneAsset)MAIN_MENU_SCENE;
        if (mainMenuAsset != null && mainMenuAsset.IsLoaded())
            yield return UnloadCoroutine(mainMenuAsset);

        Player.SpawnPlayerAtCheckpoint();

        if (newGame)
            CutsceneManager.PlayCutscene(Cutscene.GetCutscene("Opening Cutscene"));
    }

    /// <summary>Loads the player scene and optionally positions/initializes the player. (Legacy API)</summary>
    public static AsyncOperation LoadPlayerScene(bool forceReload = false, bool characterStartInactive = true)
    {
        SceneAsset playerSceneAsset = (SceneAsset)PLAYER_SCENE;

        bool isLoaded;
        try
        {
            isLoaded = playerSceneAsset != null ?
                playerSceneAsset.IsLoaded() :  // If PlayerScene is found
                SceneManager.GetSceneByName(PLAYER_SCENE).isLoaded; // If not found
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Scene Loader] Error checking loaded state for '{PLAYER_SCENE}': {ex.Message}\n{ex.StackTrace}");
            isLoaded = false;
        }

        if (isLoaded && !forceReload) return null;

        Debug.Log($"[Scene Loader] Loading player scene '{PLAYER_SCENE}' with forceReload={forceReload}. Current loaded scenes: {LoadedSceneCount}.");

        AsyncOperation operation;
        try
        {
            operation = SceneManager.LoadSceneAsync(PLAYER_SCENE, LoadSceneMode.Additive);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Scene Loader] Exception starting async load for '{PLAYER_SCENE}': {ex.Message}\n{ex.StackTrace}");
            return null;
        }

        // Adds a completion callback to handle player initialization after the scene is loaded
        operation.completed += _ =>
        {
            Debug.Log($"[Scene Loader] Player scene '{PLAYER_SCENE}' load completed callback fired. LoadedSceneCount now: {LoadedSceneCount}.");

            GameObject player = Player.PlayerObject;

            if (player == null) // Player null check. Shouldn't occur if set up properly
                Debug.LogError("[Scene Loader] Player object not found in the scene after loading the player scene. " +
                               "Ensure that the player scene contains a GameObject tagged 'Player' and that SceneAsset points to the correct scene.");

            else if (characterStartInactive) player.SetActive(false);
        };

        return operation;
    }

    /// <summary>
    /// Coroutine variant for loading the player scene. Yields until player scene load completes and performs the same initialization.
    /// </summary>
    public static IEnumerator LoadPlayerSceneCoroutine(bool forceReload = false, bool characterStartInactive = true)
    {
        SceneAsset playerSceneAsset = (SceneAsset)PLAYER_SCENE;

        bool isLoaded;
        try
        {
            isLoaded = playerSceneAsset != null ?
                playerSceneAsset.IsLoaded() :  // If PlayerScene is found
                SceneManager.GetSceneByName(PLAYER_SCENE).isLoaded; // If not found
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Scene Loader] Error checking loaded state for '{PLAYER_SCENE}': {ex.Message}\n{ex.StackTrace}");
            isLoaded = false;
        }

        if (!isLoaded || forceReload)
        {
            Debug.Log($"[Scene Loader] Coroutine loading player scene '{PLAYER_SCENE}' with forceReload={forceReload}. Current loaded scenes: {LoadedSceneCount}.");

            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(PLAYER_SCENE, LoadSceneMode.Additive);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Scene Loader] Exception starting async load for '{PLAYER_SCENE}': {ex.Message}\n{ex.StackTrace}");
                yield break;
            }

            yield return operation;

            Debug.Log($"[Scene Loader] Player scene '{PLAYER_SCENE}' coroutine load completed. LoadedSceneCount now: {LoadedSceneCount}.");
        }

        GameObject player = Player.PlayerObject;

        if (player == null)
        {
            Debug.LogError("[Scene Loader] Player object not found in the scene after loading the player scene. " +
                           "Ensure that the player scene contains a GameObject tagged 'Player' and that SceneAsset points to the correct scene.");
        }
        else
        {
            if (characterStartInactive) player.SetActive(false);
            
            Player.SpawnPlayerAtCheckpoint();
        }
    }

    /// <summary>Unload everything except player then load the main menu.</summary>
    public static void LoadMainMenu()
    {
        CoroutineRunner.Run(LoadMainMenuTransitionRoutine());
    }

    /// <summary>Coroutine variant to load the main menu (unloads other scenes first).</summary>
    public static IEnumerator LoadMainMenuCoroutine()
    {
        yield return EnsureLoadingSceneReady();

        if (!LoadingScreenController.HasInstance)
        {
            Debug.LogWarning("[Scene Loader] LoadingScreenController is unavailable. Falling back to direct main menu load.");
            yield return LoadMainMenuSequenceCoroutine();
            yield break;
        }

        LoadingScreenController.BeginLoading(LoadMainMenuSequenceCoroutine(), pauseGame: true);
    }

    /// <summary>Unload SceneAsset if loaded. (Legacy API)</summary>
    public static AsyncOperation Unload(SceneAsset scene)
    {
        AsyncOperation operation = null;

        if (scene == null)
        {
            Debug.LogError("[Scene Loader] Cannot unload a null SceneAsset.");
            return null;
        }

        if (scene.IsLoaded()) operation = SceneManager.UnloadSceneAsync(scene.SceneName);
        else Debug.LogWarning($"Scene '{scene.SceneName}' is not loaded, cannot unload.");

        return operation;
    }

    /// <summary>Coroutine variant of Unload.</summary>
    public static IEnumerator UnloadCoroutine(SceneAsset scene)
    {
        if (scene == null)
        {
            Debug.LogError("[Scene Loader] Cannot unload a null SceneAsset.");
            yield break;
        }

        if (scene.IsLoaded())
        {
            var op = SceneManager.UnloadSceneAsync(scene.SceneName);
            if (op == null)
            {
                Debug.LogError($"[Scene Loader] Failed to start unload for '{scene.SceneName}'.");
                yield break;
            }
            yield return op;
        }
        else
        {
            Debug.LogWarning($"Scene '{scene.SceneName}' is not loaded, cannot unload.");
        }
    }

    /// <summary>Unloads all loaded scenes except the player scene.</summary>
    public static void UnloadAllLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name != PLAYER_SCENE)
                SceneManager.UnloadSceneAsync(scene);
        }
    }

    private static IEnumerator LoadMainMenuTransitionRoutine()
    {
        yield return LoadMainMenuCoroutine();
    }

    private static IEnumerator EnsureLoadingSceneReady()
    {
        if (LoadingScreenController.HasInstance)
            yield break;

#if UNITY_EDITOR
        Debug.Log($"[Scene Loader][Editor Trace] EnsureLoadingSceneReady invoked. Stack:\n{Environment.StackTrace}");
#endif

        Scene loadingScene = SceneManager.GetSceneByName(LOADING_SCENE);
        if (!loadingScene.isLoaded)
        {
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(LOADING_SCENE, LoadSceneMode.Additive);
            if (loadOp == null)
            {
                Debug.LogError($"[Scene Loader] Failed to start async load for '{LOADING_SCENE}'.");
                yield break;
            }

            yield return loadOp;
        }

        float timeoutAt = Time.unscaledTime + 5f;
        while (!LoadingScreenController.HasInstance && Time.unscaledTime < timeoutAt)
            yield return null;
    }

    private static IEnumerator LoadMainMenuSequenceCoroutine()
    {
        Initialize();

        if (SoundManager.Instance != null)
            yield return SoundManager.Instance.FadeOutGameplayAudio(0.5f);

        SceneAsset mainMenuAsset = (SceneAsset)MAIN_MENU_SCENE;
        if (mainMenuAsset == null)
        {
            Debug.LogError($"[Scene Loader] Could not resolve scene asset for '{MAIN_MENU_SCENE}'.");
            yield break;
        }

        if (!mainMenuAsset.IsLoaded())
            yield return LoadCoroutine(mainMenuAsset, loadScreen: false);

        yield return UnloadAllScenesExceptCoroutine(MAIN_MENU_SCENE, LOADING_SCENE);
    }

    private static IEnumerator UnloadAllScenesExceptCoroutine(params string[] sceneNamesToKeep)
    {
        HashSet<string> keepScenes = new(sceneNamesToKeep ?? Array.Empty<string>(), StringComparer.Ordinal);

        bool unloadedAnyScene;
        do
        {
            unloadedAnyScene = false;

            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || keepScenes.Contains(scene.name))
                    continue;

                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(scene);
                if (unloadOp == null)
                {
                    Debug.LogWarning($"[Scene Loader] Failed to start unload for '{scene.name}'.");
                    continue;
                }

                unloadedAnyScene = true;
                yield return unloadOp;
            }
        }
        while (unloadedAnyScene);
    }
}