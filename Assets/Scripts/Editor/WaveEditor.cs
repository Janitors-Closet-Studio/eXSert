using System;
using System.Collections.Generic;
using System.Linq;
using Progression.Encounters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(Wave))]
public sealed class WaveEditor : Editor
{
    private const string EnemySpawnMarkerPrefabFolder = "Assets/Prefabs/EnemyUtilities/Enemy Spawn Markers";

    private static int selectedPrefabIndex;

    private readonly List<GameObject> esmPrefabs = new();
    private string[] esmPrefabDisplayNames = Array.Empty<string>();

    private void OnEnable()
    {
        RefreshEnemySpawnMarkerPrefabs();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Enemy Spawn Marker", EditorStyles.boldLabel);

        if (esmPrefabs.Count == 0)
        {
            EditorGUILayout.HelpBox(
                $"No EnemySpawnMarker prefabs were found in '{EnemySpawnMarkerPrefabFolder}'.",
                MessageType.Warning
            );

            if (GUILayout.Button("Refresh ESM List"))
                RefreshEnemySpawnMarkerPrefabs();

            return;
        }

        selectedPrefabIndex = Mathf.Clamp(selectedPrefabIndex, 0, esmPrefabs.Count - 1);
        selectedPrefabIndex = EditorGUILayout.Popup("ESM Type", selectedPrefabIndex, esmPrefabDisplayNames);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create ESM Child"))
                CreateEnemySpawnMarkerChild((Wave)target, esmPrefabs[selectedPrefabIndex]);

            if (GUILayout.Button("Refresh List", GUILayout.Width(100f)))
                RefreshEnemySpawnMarkerPrefabs();
        }
    }

    private void RefreshEnemySpawnMarkerPrefabs()
    {
        esmPrefabs.Clear();

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemySpawnMarkerPrefabFolder });
        IEnumerable<GameObject> prefabs = prefabGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(prefab => prefab != null && prefab.GetComponent<EnemySpawnMarker>() != null)
            .OrderBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase);

        esmPrefabs.AddRange(prefabs);
        esmPrefabDisplayNames = esmPrefabs
            .Select(prefab => prefab.name.StartsWith("ESM ", StringComparison.OrdinalIgnoreCase)
                ? prefab.name.Substring(4)
                : prefab.name)
            .ToArray();

        if (selectedPrefabIndex >= esmPrefabs.Count)
            selectedPrefabIndex = 0;
    }

    private static void CreateEnemySpawnMarkerChild(Wave wave, GameObject esmPrefab)
    {
        if (wave == null || esmPrefab == null)
            return;

        GameObject instance = PrefabUtility.InstantiatePrefab(esmPrefab, wave.gameObject.scene) as GameObject;
        if (instance == null)
        {
            Debug.LogError($"Failed to instantiate Enemy Spawn Marker prefab '{esmPrefab.name}'.");
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, $"Create {esmPrefab.name}");
        Undo.SetTransformParent(instance.transform, wave.transform, "Parent Enemy Spawn Marker");

        instance.name = GameObjectUtility.GetUniqueNameForSibling(wave.transform, esmPrefab.name);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(instance.scene);
    }
}