using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SceneAssetCreation
{
    private const string TargetFolder = "Assets/Scenes/Resources/Scene Assets";

    [MenuItem("Assets/Create Scene Asset From Scene", false, 20)]
    private static void CreateSceneAssetFromSelection()
    {
        Object[] selections = Selection.objects;
        if (selections == null || selections.Length == 0)
        {
            Debug.LogWarning("No asset selected.");
            return;
        }

        EnsureTargetFolderExists();

        var createdAssets = new List<SceneAsset>();
        foreach (Object obj in selections)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
                continue;

            if (Path.GetExtension(path).ToLowerInvariant() != ".unity")
                continue;

            string sceneName = Path.GetFileNameWithoutExtension(path);
            string assetPath = $"{TargetFolder}/{sceneName}.asset";

            // If an asset with the same name already exists, skip
            if (AssetDatabase.LoadAssetAtPath<global::SceneAsset>(assetPath) != null)
            {
                Debug.LogWarning($"SceneAsset already exists for scene '{sceneName}' at {assetPath}");
                continue;
            }

            // Create ScriptableObject (the project's SceneAsset ScriptableObject).
            var sceneAsset = ScriptableObject.CreateInstance<global::SceneAsset>();
            sceneAsset.name = sceneName;

            AssetDatabase.CreateAsset(sceneAsset, assetPath);

            // Register created object with the Undo system so the creation can be undone.
            Undo.RegisterCreatedObjectUndo(sceneAsset, $"Create SceneAsset '{sceneName}'");

            createdAssets.Add(sceneAsset);
        }

        if (createdAssets.Count > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select the newly created assets in Project window and ping them.
            var objs = new Object[createdAssets.Count];
            for (int i = 0; i < createdAssets.Count; i++)
            {
                objs[i] = createdAssets[i];
                EditorGUIUtility.PingObject(createdAssets[i]);
            }

            Selection.objects = objs;
            EditorUtility.FocusProjectWindow();

            Debug.Log($"Created {createdAssets.Count} SceneAsset(s) in '{TargetFolder}'.");
        }
        else
        {
            Debug.Log("No SceneAssets were created. Select one or more .unity scene files in the Project window and try again.");
        }
    }

    // Validation: only enable the menu item when at least one selected asset is a .unity scene file.
    [MenuItem("Assets/Create Scene Asset From Scene", true)]
    private static bool ValidateCreateSceneAssetFromSelection()
    {
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && Path.GetExtension(path).ToLowerInvariant() == ".unity")
                return true;
        }
        return false;
    }

    // Ensure the full folder tree exists under Assets. Uses AssetDatabase to create folders where missing.
    private static void EnsureTargetFolderExists()
    {
        if (AssetDatabase.IsValidFolder(TargetFolder))
            return;

        // Create intermediate folders if required.
        CreateFolderIfMissing("Assets", "Scenes");
        CreateFolderIfMissing("Assets/Scenes", "Resources");
        CreateFolderIfMissing("Assets/Scenes/Resources", "Scene Assets");
        AssetDatabase.Refresh();
    }

    private static void CreateFolderIfMissing(string parent, string newFolderName)
    {
        string folderPath = $"{parent}/{newFolderName}";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(parent, newFolderName);
        }
    }
}
