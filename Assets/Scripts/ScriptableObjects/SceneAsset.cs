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
    // The name of the scene, serialized for easy editing in the inspector
    public string sceneName { get => this.name; }

    // Check if the scene is currently loaded
    public bool IsLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == sceneName)
            {
                return true;
            }
        }
        return false;
    }

    // Load the scene additively, with an option to force reload if already loaded
    public void Load(bool forceReload = false)
    {
        if (!IsLoaded() || forceReload)
        {
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }
    }

    // Unload the scene if it is currently loaded
    public void Unload()
    {
        if (IsLoaded())
        {
            SceneManager.UnloadSceneAsync(sceneName);
        }
        else
        {
            Debug.LogWarning($"Scene '{sceneName}' is not loaded, cannot unload.");
        }
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
    #endregion

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
}
