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

        // Create the new wave gameobject as a child
        GameObject wave = encounter.GenerateNewWave();
        Undo.RegisterCreatedObjectUndo(wave, "Create Wave");

        // Select the new wave in the editor
        Selection.activeGameObject = wave;

        // Mark scene dirty so changes are saved
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(wave.scene);
    }
}