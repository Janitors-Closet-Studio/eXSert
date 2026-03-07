using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Scene = UnityEngine.SceneManagement.Scene;

/// <summary>
/// Centralized static class responsible for scene loading/unloading and player-scene specific behaviors.
/// SceneAsset remains a data container; use this class for all runtime scene management.
/// </summary>
public static class SceneLoader
{
    private const string PLAYER_SCENE = "PlayerScene"; // The name of the player scene
    private const string MAIN_MENU_SCENE = "MainMenu"; // The name of the main menu scene
    private const string LOADING_SCENE = "LoadingScene"; // The name of the loading scene

    /// <summary>Number of currently loaded scenes.</summary>
    public static int LoadedSceneCount => SceneManager.sceneCount;

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

    /// <summary>Event invoked when a forced scene reload is requested.</summary>
    public static Action OnSceneReloaded { get; internal set; }

    /// <summary>Loads a SceneAsset (additive). Returns the SceneAsset or null on error.</summary>
    public static SceneAsset Load(SceneAsset scene, bool forceReload = false)
    {
        Debug.Log($"[Scene Loader] Request to load scene '{scene}' with forceReload={forceReload}. Current loaded scenes: {LoadedSceneCount}.");
        if (scene == null)
        {
            Debug.LogError("Cannot load a null SceneAsset.");
            return null;
        }

        if (forceReload) OnSceneReloaded?.Invoke();

        if (scene.IsLoaded() && forceReload) SceneManager.UnloadSceneAsync(scene.SceneName);

        if (!scene.IsLoaded() || forceReload)
            SceneManager.LoadSceneAsync(scene.SceneName, LoadSceneMode.Additive);

        return scene;
    }

    /// <summary>Attempts to load a scene by name. If a SceneAsset exists it will be used; otherwise a direct additive load is attempted.</summary>
    public static SceneAsset Load(string sceneName)
    {
        var asset = SceneAsset.GetSceneAsset(sceneName);
        if (asset == null)
        {
            Debug.LogWarning($"[Scene Loader] SceneAsset for '{sceneName}' not found, attempting direct scene load.");
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            return null;
        }
        return Load(asset, false);
    }

    /// <summary>Load the first gameplay scene then the player scene.</summary>
    public static void LoadIntoGame(SceneAsset firstScene)
    {
        if (firstScene == null)
        {
            Debug.LogError("Cannot load a null SceneAsset into the game.");
            return;
        }

        SceneManager.LoadSceneAsync(firstScene.SceneName, LoadSceneMode.Additive).completed += static _ =>
        {
            LoadPlayerScene(characterStartInactive: false).completed += static __ => Unload(MAIN_MENU_SCENE);
        };
    }

    /// <summary>Loads the player scene and optionally positions/initializes the player.</summary>
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

    /// <summary>Unload everything except player then load the main menu.</summary>
    public static void LoadMainMenu()
    {
        UnloadAllLoadedScenes();
        Load(MAIN_MENU_SCENE);
    }

    /// <summary>Unload SceneAsset if loaded.</summary>
    public static SceneAsset Unload(SceneAsset scene)
    {
        if (scene == null)
        {
            Debug.LogError("Cannot unload a null SceneAsset.");
            return null;
        }

        if (scene.IsLoaded()) SceneManager.UnloadSceneAsync(scene.SceneName);
        else Debug.LogWarning($"Scene '{scene.SceneName}' is not loaded, cannot unload.");

        return scene;
    }

    /// <summary>Unload by scene name (tries SceneAsset first, then direct scene name unload).</summary>
    public static void Unload(string sceneName)
    {
        var asset = SceneAsset.GetSceneAsset(sceneName);
        if (asset != null)
        {
            Unload(asset);
            return;
        }

        var scene = SceneManager.GetSceneByName(sceneName);
        if (scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        else Debug.LogWarning($"Scene '{sceneName}' is not loaded, cannot unload.");
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
}