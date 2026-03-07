/*
 * Author: Will Thomsen
 * 
 * Basic Scene ScriptableObject to hold scene names for easy reference.
 * 
 * Improved to be able to find the SceneAssets gameobjects are contained within
 */

using Progression.Checkpoints;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Scene = UnityEngine.SceneManagement.Scene;

[Serializable, CreateAssetMenu(fileName = "New Game Scene", menuName = "Scene/Scene Asset")]
[HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?pli=1&tab=t.0#bookmark=id.jla7jdxhssmh")]
public class SceneAsset : ScriptableObject
{
    // Data-only: expose the ScriptableObject name as the scene name
    public string SceneName { get => this.name; }
    public static implicit operator string(SceneAsset asset) => asset.SceneName; // Allow implicit conversion to string for easy use in SceneManager functions
    public static implicit operator SceneAsset(string name) => GetSceneAsset(name); // Allow implicit conversion from string to SceneAsset for easy retrieval
    public static implicit operator Scene(SceneAsset asset) => SceneManager.GetSceneByName(asset.SceneName);
    public override string ToString() => SceneName;

    // Forwarders kept for backward compatibility; prefer SceneLoader API.
    public static bool PlayerLoaded => SceneLoader.PlayerLoaded;
    public static Action OnSceneReloaded { get => SceneLoader.OnSceneReloaded; internal set => SceneLoader.OnSceneReloaded = value; }
    public static int LoadedSceneCount => SceneLoader.LoadedSceneCount;

    /// <summary>
    /// Determines whether the scene specified by <c>sceneName</c> is currently loaded.
    /// </summary>
    /// <returns><see langword="true"/> if the scene is loaded; otherwise, <see langword="false"/>.</returns>
    public bool IsLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == SceneName) return true;
        }
        return false;
    }

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