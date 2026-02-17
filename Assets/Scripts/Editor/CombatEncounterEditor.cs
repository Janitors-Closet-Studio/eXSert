/*
 * Written by Will T
 * 
 * Custom editor for CombatEncounter to add "Add Wave" button in inspector
 * Button creates a new child GameObject named "Wave X" where X is the next wave number, and adds it as a child of the encounter
 * It also adds the wave script to the new gameobject, and selects it in the editor for easy editing
 */

using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using Progression.Encounters;

[CustomEditor(typeof(CombatEncounter))]
public class CombatEncounterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        GUILayout.Label("Wave Management", EditorStyles.boldLabel);

        if (GUILayout.Button("Add Wave"))
        {
            CreateWave((CombatEncounter)target);
        }
    }

    private void CreateWave(CombatEncounter encounter)
    {
        if (encounter == null || encounter.gameObject == null)
            return;

        GameObject parent = encounter.gameObject;

        // Count existing wave-named children (case-insensitive)
        int existing = 0;
        foreach (Transform child in parent.transform)
            if (child.name.ToLower().Contains("wave")) existing++;

        string waveName = $"Wave {existing + 1}";

        // Create the new wave gameobject as a child
        GameObject wave = new(waveName);
        Undo.RegisterCreatedObjectUndo(wave, "Create Wave");
        wave.transform.SetParent(parent.transform, false);
        wave.transform.localPosition = Vector3.zero;
        wave.AddComponent<Wave>();

        // Select the new wave in the editor
        Selection.activeGameObject = wave;

        // Mark scene dirty so changes are saved
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(wave.scene);
    }
}