/*
 * Author: Will Thomsen
 * 
 * Basic Scene ScriptableObject to hold scene names for easy reference.
 * 
 * Improved to be able to find the SceneAssets gameobjects are contained within
 */

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Scene = UnityEngine.SceneManagement.Scene;

[Serializable, CreateAssetMenu(fileName = "New Game Scene", menuName = "Scene/Scene Asset")]
[HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?pli=1&tab=t.0#bookmark=id.jla7jdxhssmh")]
public class SceneAsset : ScriptableObject
{
    private const string playerSceneName = "PlayerScene"; // The name of the player scene, used for checking if the player is loaded
    private const string mainMenuSceneName = "MainMenu"; // The name of the main menu scene, used for loading the main menu

    public string sceneName { get => this.name; }
    public static implicit operator string(SceneAsset asset) => asset.sceneName; // Allow implicit conversion to string for easy use in SceneManager functions
    public static implicit operator SceneAsset(string name) => GetSceneAsset(name); // Allow implicit conversion from string to SceneAsset for easy retrieval
    public override string ToString() => sceneName;

    public static bool PlayerLoaded => ((SceneAsset) playerSceneName).IsLoaded();

    public static Action OnSceneReloaded { get; internal set; }

    #region Loading and Unloading Functions
    /// <summary>
    /// Determines whether the scene specified by <c>sceneName</c> is currently loaded.
    /// </summary>
    /// <remarks>Use this method to check the loading state of a scene before performing operations that
    /// require the scene to be present in the scene hierarchy.</remarks>
    /// <returns><see langword="true"/> if the scene is loaded; otherwise, <see langword="false"/>.</returns>
    public bool IsLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == sceneName) return true;
        }
        return false;
    }

    /// <summary>
    /// Determines whether a scene with the specified name is currently loaded.
    /// </summary>
    /// <param name="name">The name of the scene to check. Cannot be <see langword="null"/> or empty.</param>
    /// <returns><see langword="true"/> if a scene with the specified name is loaded; otherwise, <see langword="false"/>.</returns>
    public static bool IsLoaded(SceneAsset scene) => scene != null && scene.IsLoaded();

    /// <summary>
    /// Loads the scene associated with this instance if it is not already loaded, or forces a reload if specified.
    /// </summary>
    /// <remarks>The scene is loaded asynchronously in additive mode. Calling this method multiple times with
    /// <paramref name="forceReload"/> set to <see langword="false"/> will not reload the scene if it is already
    /// loaded.</remarks>
    /// <param name="forceReload"><see langword="true"/> to reload the scene even if it is already loaded; <see langword="false"/> to load the
    /// scene only if it is not loaded.</param>
    public SceneAsset Load(bool forceReload = false)
    {
        if (forceReload) OnSceneReloaded?.Invoke();

        if (!IsLoaded() || forceReload)
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

        return this;
    }

    /// <summary>
    /// Loads the specified <see cref="SceneAsset"/> into memory.
    /// </summary>
    /// <remarks>If the scene is already loaded, setting <paramref name="forceReload"/> to <see
    /// langword="true"/> will reload it.</remarks>
    /// <param name="scene">The <see cref="SceneAsset"/> to load. Cannot be <see langword="null"/>.</param>
    /// <param name="forceReload"><see langword="true"/> to reload the scene even if it is already loaded; otherwise, <see langword="false"/> to
    /// load only if not already loaded.</param>
    /// <returns>The loaded <see cref="SceneAsset"/> instance, or <see langword="null"/> if <paramref name="scene"/> is <see
    /// langword="null"/>.</returns>
    public static SceneAsset Load(SceneAsset scene, bool forceReload = false)
    {
        if (scene == null)
        {
            Debug.LogError("Cannot load a null SceneAsset.");
            return null;
        }
        return scene.Load(forceReload);
    }

    /// <summary>
    /// Loads the specified scene asset into the game as the initial scene.
    /// </summary>
    /// <remarks>The scene is loaded additively, allowing additional scenes to be loaded without unloading
    /// existing ones. After the initial scene has finished loading, the player scene is automatically loaded.</remarks>
    /// <param name="firstScene">The <see cref="SceneAsset"/> representing the first scene to load. Must not be <see langword="null"/>.</param>
    public static void LoadIntoGame(SceneAsset firstScene)
    {
        if (firstScene == null)
        {
            Debug.LogError("Cannot load a null SceneAsset into the game.");
            return;
        }

        // Load the first scene additively to ensure the game is running before loading additional scenes
        SceneManager.LoadSceneAsync(firstScene.sceneName, LoadSceneMode.Additive).completed += _ =>
        {
            // Loads the player scene after the first scene has finished loading.
            LoadPlayerScene();
        };
    }

    /// <summary>
    /// Loads the player scene and positions the player at the specified spawn point.
    /// </summary>
    /// <remarks>If the player scene is not already loaded, or if <paramref name="forceReload"/> is <see
    /// langword="true"/>, the scene is loaded additively. After loading, the player object is moved to the given spawn
    /// point's position and rotation.</remarks>
    /// <param name="spawnPoint">The <see cref="Transform"/> representing the location and orientation where the player should be placed after
    /// the scene loads. Cannot be <see langword="null"/>.</param>
    /// <param name="forceReload">If <see langword="true"/>, forces the player scene to reload even if it is already loaded; otherwise, loads the
    /// scene only if it is not loaded.</param>
    public static void LoadPlayerScene(Transform spawnPoint = null, bool forceReload = false, bool characterStartInactive = true)
    {
        var playerSceneAsset = (SceneAsset) playerSceneName;
        if (playerSceneAsset.IsLoaded() && !forceReload) return;

        SceneManager.LoadSceneAsync(playerSceneName, LoadSceneMode.Additive).completed += _ =>
        {
            // After loading the player scene, move the player to the specified spawn point
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) throw new InvalidOperationException("Player object not found in the scene after loading the player scene. Ensure that the player scene contains a GameObject tagged 'Player'.");
            player = player.transform.root.gameObject; // Get the root GameObject in case the player is a child of another object
            if (spawnPoint != null) player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            if (characterStartInactive) player.SetActive(false); // Optionally start the player inactive, allowing for setup before they become active
        };
    }

    /// <summary>
    /// Loads the main menu scene, unloading all currently loaded scenes first.
    /// </summary>
    /// <remarks>This method ensures that any previously loaded scenes are unloaded before loading the main
    /// menu scene. It is typically used to reset the application state and return the user to the main menu.</remarks>
    public static void LoadMainMenu()
    {
        UnloadAllLoadedScenes();
        Load(mainMenuSceneName);
    }

    /// <summary>
    /// Unloads the scene associated with this instance if it is currently loaded.
    /// </summary>
    /// <remarks>If the scene is not loaded, no action is taken and a warning is logged. This method initiates
    /// an asynchronous unload operation; the scene may not be immediately removed.</remarks>
    public SceneAsset Unload()
    {
        if (IsLoaded()) SceneManager.UnloadSceneAsync(sceneName);
        else Debug.LogWarning($"Scene '{sceneName}' is not loaded, cannot unload.");

        return this;
    }

    /// <summary>
    /// Unloads the specified scene asset and releases its resources.
    /// </summary>
    /// <remarks>Use this method to remove a scene asset from memory when it is no longer needed. If <paramref
    /// name="scene"/> is <see langword="null"/>, the method logs an error and returns <see langword="null"/>.</remarks>
    /// <param name="scene">The <see cref="SceneAsset"/> to unload. Cannot be <see langword="null"/>.</param>
    /// <returns>The unloaded <see cref="SceneAsset"/> instance, or <see langword="null"/> if <paramref name="scene"/> is <see
    /// langword="null"/>.</returns>
    public static SceneAsset Unload(SceneAsset scene)
    {
        if (scene == null)
        {
            Debug.LogError("Cannot unload a null SceneAsset.");
            return null;
        }

        return scene.Unload();
    }

    /// <summary>
    /// Unloads all currently loaded scenes except for the player scene.
    /// </summary>
    /// <remarks>This method asynchronously unloads every loaded scene except the one identified as the player
    /// scene.  Use this method to reset the scene state or transition to a new set of scenes while preserving the
    /// player scene.</remarks>
    public static void UnloadAllLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name != playerSceneName) // Ensure the player scene is not unloaded
                SceneManager.UnloadSceneAsync(scene);
        }
    }
    #endregion

    #region GetSceneAsset Functions
    /*
     * GetSceneAsset is a function aimed to help find SceneAssets.
     * It can take both the name of the scene, or the actual scene object itself.
     * If it can't find anything it will produce an error and output null.
     */
    public static SceneAsset GetSceneAsset(string name)
    {
        SceneAsset asset = Resources.Load<SceneAsset>($"Scene Assets/{name}");
        if (asset == null)
            Debug.LogError($"Unable to find scene asset with name {name}. Ensure one is created in Scene/Resources/Scene Assets or that name is spelled correctly");
        return asset;
    }

    public static SceneAsset GetSceneAsset(Scene scene)
    {
        SceneAsset asset = Resources.Load<SceneAsset>($"Scene Assets/{scene.name}");
        if (asset == null)
            Debug.LogError($"Unable to find scene asset {scene.name}. Ensure one is created in Scene/Resources/Scene Assets");
        return asset;
    }

    // Returns the SceneAsset an inputted GameObject is in
    public static SceneAsset GetSceneAssetOfObject(GameObject go)
    {
        Scene objectScene = go.scene;
        SceneAsset asset = GetSceneAsset(objectScene);
        if (asset == null)
        {
            Debug.LogWarning("Attempt at getting scene asset of object returned null");
            return null;
        }
        return asset;
    }
    #endregion
}